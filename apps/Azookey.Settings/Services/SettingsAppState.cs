using System;
using System.Threading;
using System.Threading.Tasks;
using Azookey.Core.Config;
using Grpc.Core;

namespace Azookey.Settings.Services;

public sealed record SaveResult(bool Saved, bool ServerApplied, string? Message);

public interface IServerConfigNotifier
{
    Task NotifyAsync(CancellationToken cancellationToken);
}

public sealed class SettingsAppState
{
    private readonly IConfigStore store;
    private readonly IServerConfigNotifier notifier;

    public SettingsAppState(IConfigStore store, IServerConfigNotifier notifier)
    {
        this.store = store;
        this.notifier = notifier;
        LoadResult = store.LoadWithRecovery();
        Config = LoadResult.Config;
    }

    public AppConfig Config { get; private set; }

    public ConfigLoadResult LoadResult { get; }

    public async Task<SaveResult> SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        store.Write(config);
        Config = config;

        try
        {
            await notifier.NotifyAsync(cancellationToken);
            return new SaveResult(Saved: true, ServerApplied: true, Message: null);
        }
        catch (Exception error) when (!IsCancellation(error, cancellationToken))
        {
            return new SaveResult(
                Saved: true,
                ServerApplied: false,
                Message: $"サーバーに反映できませんでした: {error.Message}");
        }
    }

    private static bool IsCancellation(Exception error, CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested &&
            (error is OperationCanceledException ||
                error is RpcException { StatusCode: StatusCode.Cancelled });
    }
}
