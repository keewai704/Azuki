using System.Diagnostics;
using Azookey.Core.Config;
using Azookey.Core.Process;
using Xunit;

namespace Azookey.Core.Tests.Process;

public sealed class ServerRestartServiceTests : IDisposable
{
    private readonly string installDirectory = Path.Combine(
        Path.GetTempPath(),
        "azookey-restart-tests",
        Guid.NewGuid().ToString("N"));

    public ServerRestartServiceTests()
    {
        Directory.CreateDirectory(installDirectory);
        File.WriteAllText(Path.Combine(installDirectory, "azookey-server.exe"), "");
    }

    public void Dispose() => Directory.Delete(installDirectory, true);

    [Theory]
    [InlineData("cpu", "EngineRuntime\\llama_cpu")]
    [InlineData("cuda", "EngineRuntime\\llama_vulkan")]
    [InlineData("vulkan", "EngineRuntime\\llama_vulkan")]
    [InlineData("CUDA", "EngineRuntime\\llama_vulkan")]
    [InlineData("", "EngineRuntime\\llama_cpu")]
    public void MapsBackendToLauncherBackendDirectory(string backend, string expectedDirectory)
    {
        Assert.Equal(expectedDirectory, ServerRestartService.BackendDirectory(backend));
    }

    [Fact]
    public void LauncherBackendDirectorySourceDoesNotReferenceCudaDirectory()
    {
        string source = File.ReadAllText(GetServerRestartServiceSourcePath());

        Assert.DoesNotContain(string.Concat("llama_", "cuda"), source);
    }

    [Fact]
    public void DirectStartInfoUsesInstallDirectoryAndPrependsBackendDirectoryToPath()
    {
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "vulkan", Profile = "" }
        };
        string existingPath = "C:\\Windows\\System32";

        var startInfo = ServerRestartService.CreateDirectStartInfo(
            installDirectory,
            config,
            existingPath);

        Assert.Equal(Path.Combine(installDirectory, "azookey-server.exe"), startInfo.FileName);
        Assert.Equal(installDirectory, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(
            $"{Path.Combine(installDirectory, "EngineRuntime", "Swift")};{Path.Combine(installDirectory, "EngineRuntime", "llama_vulkan")};{existingPath}",
            startInfo.Environment["PATH"]);
    }

    [Fact]
    public void DirectStartInfoSetsZenzaiCpuSupported()
    {
        ProcessStartInfo startInfo = ServerRestartService.CreateDirectStartInfo(
            installDirectory,
            AppConfig.CreateDefault(),
            existingPath: "",
            zenzaiCpuBackendSupported: () => false);

        Assert.Equal("0", startInfo.Environment["AZOOKEY_ZENZAI_CPU_SUPPORTED"]);
    }

    [Fact]
    public void DirectStartInfoSetsZenzaiModelPathWhenDownloadedModelExists()
    {
        string configRoot = Path.Combine(installDirectory, "config");
        ZenzaiModelOption model = ZenzaiModelCatalog.Options[0];
        string modelPath = Path.Combine(configRoot, "models", model.Id, model.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        File.WriteAllText(modelPath, "model");

        AppConfig defaults = AppConfig.CreateDefault();
        AppConfig config = defaults with
        {
            Zenzai = defaults.Zenzai with { ModelId = model.Id }
        };

        ProcessStartInfo startInfo = ServerRestartService.CreateDirectStartInfo(
            installDirectory,
            config,
            existingPath: "",
            configRoot: configRoot);

        Assert.Equal(modelPath, startInfo.Environment["AZOOKEY_ZENZAI_MODEL_PATH"]);
    }

    [Fact]
    public async Task RestartAsyncFallsBackToDirectStartWhenLauncherIsUnavailable()
    {
        List<string> started = [];
        bool waitedForWatchdog = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            startInfo =>
            {
                started.Add(startInfo.FileName);
                return true;
            },
            () => [],
            (_, _) =>
            {
                waitedForWatchdog = true;
                return ValueTask.CompletedTask;
            },
            () => true);

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.StartedDirectly, result.Status);
        Assert.Equal([Path.Combine(installDirectory, "azookey-server.exe")], started);
        Assert.True(waitedForWatchdog);
    }

    [Fact]
    public async Task RestartAsyncDoesNotFallBackToPathWhenInstallServerExeIsMissing()
    {
        File.Delete(Path.Combine(installDirectory, "azookey-server.exe"));
        bool started = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            _ =>
            {
                started = true;
                return true;
            });

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.DirectStartFailed, result.Status);
        Assert.False(started);
    }

    [Fact]
    public async Task RestartAsyncTerminatesMatchingInstallServerThenLetsLauncherWatchdogRecoverIt()
    {
        string serverPath = Path.Combine(installDirectory, "azookey-server.exe");
        var oldServer = new TestServerProcess(serverPath);
        var watchdogServer = new TestServerProcess(serverPath);
        int processQueryCount = 0;
        bool started = false;
        bool waitedForWatchdog = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            _ =>
            {
                started = true;
                return true;
            },
            () => processQueryCount++ == 0
                ? [oldServer]
                : [watchdogServer],
            (_, _) =>
            {
                waitedForWatchdog = true;
                return ValueTask.CompletedTask;
            },
            () => true);

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.RestartedByLauncher, result.Status);
        Assert.True(oldServer.WasKilled);
        Assert.True(oldServer.WaitedForExit);
        Assert.True(waitedForWatchdog);
        Assert.False(started);
    }

    [Fact]
    public async Task RestartAsyncReturnsDirectStartFailedWhenMatchingInstallServerKillFails()
    {
        string serverPath = Path.Combine(installDirectory, "azookey-server.exe");
        var oldServer = new TestServerProcess(
            serverPath,
            killException: new InvalidOperationException("access denied"));
        bool started = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            _ =>
            {
                started = true;
                return true;
            },
            () => [oldServer],
            (_, _) => ValueTask.CompletedTask,
            () => true);

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.DirectStartFailed, result.Status);
        Assert.True(oldServer.WasKilled);
        Assert.False(oldServer.WaitedForExit);
        Assert.False(started);
    }

    [Fact]
    public async Task RestartAsyncReturnsDirectStartFailedWhenMatchingInstallServerExitTimesOut()
    {
        string serverPath = Path.Combine(installDirectory, "azookey-server.exe");
        var oldServer = new TestServerProcess(serverPath, waitForExitResult: false);
        bool started = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            _ =>
            {
                started = true;
                return true;
            },
            () => [oldServer],
            (_, _) => ValueTask.CompletedTask,
            () => true);

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.DirectStartFailed, result.Status);
        Assert.True(oldServer.WasKilled);
        Assert.True(oldServer.WaitedForExit);
        Assert.False(started);
    }

    [Fact]
    public async Task RestartAsyncIgnoresNonInstallDirectoryServerProcessesBeforeDirectStart()
    {
        string otherServerPath = Path.Combine(Path.GetTempPath(), "other-azookey", "azookey-server.exe");
        var otherServer = new TestServerProcess(otherServerPath);
        List<string> started = [];
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Unavailable("missing pipe")),
            startInfo =>
            {
                started.Add(startInfo.FileName);
                return true;
            },
            () => [otherServer],
            (_, _) => ValueTask.CompletedTask,
            () => true);

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.StartedDirectly, result.Status);
        Assert.False(otherServer.WasKilled);
        Assert.Equal([Path.Combine(installDirectory, "azookey-server.exe")], started);
    }

    [Fact]
    public async Task RestartAsyncDoesNotDirectStartWhenLauncherReportsFailure()
    {
        bool started = false;
        var service = new ServerRestartService(
            _ => ValueTask.FromResult(LauncherRestartResult.Failed("denied")),
            _ =>
            {
                started = true;
                return true;
            });

        ServerRestartResult result = await service.RestartAsync(
            installDirectory,
            AppConfig.CreateDefault(),
            CancellationToken.None);

        Assert.Equal(ServerRestartStatus.LauncherFailed, result.Status);
        Assert.Equal("denied", result.Message);
        Assert.False(started);
    }

    private sealed class TestServerProcess(
        string executablePath,
        bool waitForExitResult = true,
        Exception? killException = null) : IServerProcess
    {
        public bool WasKilled { get; private set; }
        public bool WaitedForExit { get; private set; }

        public string? ExecutablePath => executablePath;

        public void Kill()
        {
            WasKilled = true;
            if (killException is not null)
            {
                throw killException;
            }
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            WaitedForExit = true;
            return waitForExitResult;
        }

        public void Dispose()
        {
        }
    }

    private static string GetServerRestartServiceSourcePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "apps",
                "Azookey.Core",
                "Process",
                "ServerRestartService.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate ServerRestartService.cs from {AppContext.BaseDirectory}.");
    }
}
