using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using Azookey.Core.Config;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace Azookey.Core.Process;

public enum ServerRestartStatus
{
    RestartedByLauncher,
    StartedDirectly,
    LauncherFailed,
    DirectStartFailed
}

public sealed record ServerRestartResult(ServerRestartStatus Status, string? Message = null);

internal interface IServerProcess : IDisposable
{
    string? ExecutablePath { get; }

    void Kill();

    bool WaitForExit(TimeSpan timeout);
}

public sealed class ServerRestartService
{
    private const string ServerExeName = "azookey-server.exe";
    private static readonly TimeSpan LauncherWatchdogWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessExitWait = TimeSpan.FromSeconds(2);

    private readonly Func<CancellationToken, ValueTask<LauncherRestartResult>> requestLauncherRestart;
    private readonly Func<ProcessStartInfo, bool> startProcess;
    private readonly Func<IReadOnlyList<IServerProcess>> getServerProcesses;
    private readonly Func<TimeSpan, CancellationToken, ValueTask> waitForLauncherWatchdog;
    private readonly Func<bool> zenzaiCpuBackendSupported;

    public ServerRestartService()
        : this(
            cancellationToken => new LauncherClient().RequestRestartAsync(cancellationToken),
            startInfo => DiagnosticsProcess.Start(startInfo) is not null)
    {
    }

    internal ServerRestartService(
        Func<CancellationToken, ValueTask<LauncherRestartResult>> requestLauncherRestart,
        Func<ProcessStartInfo, bool> startProcess)
        : this(
            requestLauncherRestart,
            startProcess,
            GetServerProcesses,
            Delay,
            ZenzaiCpuBackendSupported)
    {
    }

    internal ServerRestartService(
        Func<CancellationToken, ValueTask<LauncherRestartResult>> requestLauncherRestart,
        Func<ProcessStartInfo, bool> startProcess,
        Func<IReadOnlyList<IServerProcess>> getServerProcesses,
        Func<TimeSpan, CancellationToken, ValueTask> waitForLauncherWatchdog,
        Func<bool> zenzaiCpuBackendSupported)
    {
        this.requestLauncherRestart = requestLauncherRestart;
        this.startProcess = startProcess;
        this.getServerProcesses = getServerProcesses;
        this.waitForLauncherWatchdog = waitForLauncherWatchdog;
        this.zenzaiCpuBackendSupported = zenzaiCpuBackendSupported;
    }

    public async ValueTask<ServerRestartResult> RestartAsync(
        string installDirectory,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentNullException.ThrowIfNull(config);

        LauncherRestartResult launcherResult;
        try
        {
            launcherResult = await requestLauncherRestart(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            launcherResult = LauncherRestartResult.Unavailable(error.Message);
        }

        return launcherResult.Status switch
        {
            LauncherRestartStatus.Succeeded => new ServerRestartResult(ServerRestartStatus.RestartedByLauncher),
            LauncherRestartStatus.Failed => new ServerRestartResult(
                ServerRestartStatus.LauncherFailed,
                launcherResult.Message),
            _ => await StartDirectlyAsync(installDirectory, config, cancellationToken)
        };
    }

    internal static string BackendDirectory(string backend) =>
        Path.Combine(
            "EngineRuntime",
            string.Equals(backend, "vulkan", StringComparison.OrdinalIgnoreCase)
            || string.Equals(backend, "cuda", StringComparison.OrdinalIgnoreCase)
                ? "llama_vulkan"
                : "llama_cpu");

    internal static ProcessStartInfo CreateDirectStartInfo(
        string installDirectory,
        AppConfig config,
        string? existingPath = null,
        Func<bool>? zenzaiCpuBackendSupported = null,
        string? configRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentNullException.ThrowIfNull(config);

        string serverPath = Path.Combine(installDirectory, ServerExeName);
        if (!File.Exists(serverPath))
        {
            throw new FileNotFoundException("azookey-server.exe was not found in the install directory.", serverPath);
        }

        string swiftRuntimePath = Path.Combine(installDirectory, "EngineRuntime", "Swift");
        string backendPath = Path.Combine(installDirectory, BackendDirectory(config.Zenzai.Backend));

        var startInfo = new ProcessStartInfo
        {
            FileName = serverPath,
            WorkingDirectory = installDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.Environment["PATH"] = PrependToPath(
            [swiftRuntimePath, backendPath],
            existingPath ?? Environment.GetEnvironmentVariable("PATH") ?? "");
        startInfo.Environment["AZOOKEY_ZENZAI_CPU_SUPPORTED"] =
            (zenzaiCpuBackendSupported ?? ZenzaiCpuBackendSupported)() ? "1" : "0";
        string effectiveConfigRoot = configRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Azookey");
        string? modelPath = ZenzaiModelCatalog.ResolveExistingModelPath(
            effectiveConfigRoot,
            config.Zenzai.ModelId);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            startInfo.Environment["AZOOKEY_ZENZAI_MODEL_PATH"] = modelPath;
        }

        return startInfo;
    }

    private async ValueTask<ServerRestartResult> StartDirectlyAsync(
        string installDirectory,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            string serverPath = Path.Combine(installDirectory, ServerExeName);
            if (!File.Exists(serverPath))
            {
                return new ServerRestartResult(
                    ServerRestartStatus.DirectStartFailed,
                    "azookey-server.exe was not found in the install directory.");
            }

            TerminateMatchingServerProcesses(installDirectory);
            await waitForLauncherWatchdog(LauncherWatchdogWait, cancellationToken);
            if (HasMatchingServerProcess(installDirectory))
            {
                return new ServerRestartResult(ServerRestartStatus.RestartedByLauncher);
            }

            ProcessStartInfo startInfo = CreateDirectStartInfo(
                installDirectory,
                config,
                zenzaiCpuBackendSupported: zenzaiCpuBackendSupported);
            return startProcess(startInfo)
                ? new ServerRestartResult(ServerRestartStatus.StartedDirectly)
                : new ServerRestartResult(
                    ServerRestartStatus.DirectStartFailed,
                    "Failed to start azookey-server.exe.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return new ServerRestartResult(ServerRestartStatus.DirectStartFailed, error.Message);
        }
    }

    private void TerminateMatchingServerProcesses(string installDirectory)
    {
        foreach (IServerProcess process in getServerProcesses())
        {
            using (process)
            {
                if (!IsMatchingInstallServerProcess(installDirectory, process.ExecutablePath))
                {
                    continue;
                }

                process.Kill();
                if (!process.WaitForExit(ProcessExitWait))
                {
                    throw new TimeoutException("Timed out waiting for azookey-server.exe to exit.");
                }
            }
        }
    }

    private bool HasMatchingServerProcess(string installDirectory)
    {
        foreach (IServerProcess process in getServerProcesses())
        {
            using (process)
            {
                if (IsMatchingInstallServerProcess(installDirectory, process.ExecutablePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsMatchingInstallServerProcess(string installDirectory, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            string expected = Path.GetFullPath(Path.Combine(installDirectory, ServerExeName));
            string actual = Path.GetFullPath(executablePath);
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    internal static bool ZenzaiCpuBackendSupported() => Avx.IsSupported;

    private static IReadOnlyList<IServerProcess> GetServerProcesses() =>
        DiagnosticsProcess
            .GetProcessesByName(Path.GetFileNameWithoutExtension(ServerExeName))
            .Select(process => (IServerProcess)new DiagnosticsServerProcess(process))
            .ToList();

    private static ValueTask Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        new(Task.Delay(delay, cancellationToken));

    private static string PrependToPath(IReadOnlyList<string> paths, string existingPath)
    {
        string prefix = string.Join(";", paths);
        return string.IsNullOrEmpty(existingPath) ? prefix : $"{prefix};{existingPath}";
    }

    private sealed class DiagnosticsServerProcess(DiagnosticsProcess process) : IServerProcess
    {
        public string? ExecutablePath
        {
            get
            {
                try
                {
                    return process.MainModule?.FileName;
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception)
                {
                    return null;
                }
            }
        }

        public void Kill() => process.Kill();

        public bool WaitForExit(TimeSpan timeout)
        {
            int milliseconds = timeout.TotalMilliseconds >= int.MaxValue
                ? int.MaxValue
                : Math.Max(0, (int)timeout.TotalMilliseconds);
            return process.WaitForExit(milliseconds);
        }

        public void Dispose() => process.Dispose();
    }
}
