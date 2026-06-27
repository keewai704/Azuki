using Azookey.Core.Config;
using Azookey.Settings.Services;
using Grpc.Core;
using Xunit;

namespace Azookey.Settings.Tests.Services;

public sealed class SettingsAppStateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azookey-settings-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveReportsSavedAndServerAppliedWhenNotifierSucceeds()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var notifier = new RecordingNotifier(succeeds: true);
        var state = new SettingsAppState(store, notifier);
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        SaveResult result = await state.SaveAsync(config);

        Assert.True(result.Saved);
        Assert.True(result.ServerApplied);
        Assert.Null(result.Message);
        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SaveReportsSavedWhenServerIsUnavailable()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var notifier = new RecordingNotifier(succeeds: false);
        var state = new SettingsAppState(store, notifier);
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        SaveResult result = await state.SaveAsync(config);

        Assert.True(result.Saved);
        Assert.False(result.ServerApplied);
        Assert.Contains("サーバーに反映できませんでした", result.Message, StringComparison.Ordinal);
        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SaveReportsSavedWhenNotificationTimesOutWithoutCallerCancellation()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var state = new SettingsAppState(store, new OperationCanceledNotifier());
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        SaveResult result = await state.SaveAsync(config);

        Assert.True(result.Saved);
        Assert.False(result.ServerApplied);
        Assert.Contains("サーバーに反映できませんでした", result.Message, StringComparison.Ordinal);
        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SavePropagatesCancelledNotificationWhenCallerTokenIsCancelled()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var state = new SettingsAppState(store, new OperationCanceledNotifier());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => state.SaveAsync(config, cts.Token));

        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SavePropagatesCancelledGrpcNotificationWhenCallerTokenIsCancelled()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));
        var state = new SettingsAppState(store, new CancelledGrpcNotifier());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        AppConfig config = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        RpcException error = await Assert.ThrowsAsync<RpcException>(() => state.SaveAsync(config, cts.Token));

        Assert.Equal(StatusCode.Cancelled, error.StatusCode);
        Assert.True(state.Config.Zenzai.Enable);
    }

    [Fact]
    public async Task SaveFailureDoesNotReplaceInMemoryState()
    {
        var store = new ThrowingStore();
        var state = new SettingsAppState(store, new RecordingNotifier(succeeds: true));
        AppConfig updated = AppConfig.CreateDefault() with
        {
            Zenzai = new ZenzaiConfig { Enable = true, Backend = "cpu", Profile = "" }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.SaveAsync(updated));

        Assert.False(state.Config.Zenzai.Enable);
    }

    private sealed class RecordingNotifier(bool succeeds) : IServerConfigNotifier
    {
        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            if (succeeds)
            {
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("not available");
        }
    }

    private sealed class OperationCanceledNotifier : IServerConfigNotifier
    {
        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            throw new OperationCanceledException("server notification timed out", cancellationToken);
        }
    }

    private sealed class CancelledGrpcNotifier : IServerConfigNotifier
    {
        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("Expected a canceled token.");
            }

            throw new RpcException(new Status(StatusCode.Cancelled, "cancelled"));
        }
    }

    private sealed class ThrowingStore : IConfigStore
    {
        public ConfigLoadResult LoadWithRecovery() => new(AppConfig.CreateDefault(), null, null);

        public void Write(AppConfig config) => throw new InvalidOperationException("write failed");
    }
}
