using System.IO.Pipes;
using System.Text;

namespace Azookey.Core.Process;

public enum LauncherRestartStatus
{
    Succeeded,
    Unavailable,
    Failed
}

public sealed record LauncherRestartResult(LauncherRestartStatus Status, string? Message = null)
{
    public static LauncherRestartResult Succeeded() => new(LauncherRestartStatus.Succeeded);

    public static LauncherRestartResult Unavailable(string message) =>
        new(LauncherRestartStatus.Unavailable, message);

    public static LauncherRestartResult Failed(string message) =>
        new(LauncherRestartStatus.Failed, message);
}

public sealed class LauncherClient
{
    public const string PipeName = "azookey_launcher";
    internal const string RestartCommand = "restart-server\n";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    public static byte[] RestartCommandBytes => Encoding.UTF8.GetBytes(RestartCommand);

    public static bool LauncherRestartSucceeded(string response)
    {
        string trimmed = response.Trim();
        if (trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    public async ValueTask<LauncherRestartResult> RequestRestartAsync(CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, cancellationToken);
        }
        catch (TimeoutException error)
        {
            return LauncherRestartResult.Unavailable(error.Message);
        }
        catch (IOException error) when (PipeIsMissing(error))
        {
            return LauncherRestartResult.Unavailable(error.Message);
        }

        await pipe.WriteAsync(RestartCommandBytes, cancellationToken);
        await pipe.FlushAsync(cancellationToken);

        byte[] buffer = new byte[512];
        using CancellationTokenSource responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseTimeout.CancelAfter(ResponseTimeout);

        int size;
        try
        {
            size = await pipe.ReadAsync(buffer, responseTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LauncherRestartResult.Failed("Timed out waiting for launcher restart response.");
        }

        string response = Encoding.UTF8.GetString(buffer, 0, size);
        return LauncherRestartSucceeded(response)
            ? LauncherRestartResult.Succeeded()
            : LauncherRestartResult.Failed(response.Trim());
    }

    private static bool PipeIsMissing(IOException error) =>
        ErrorCode(error) is 2 or 3;

    private static int ErrorCode(IOException error) =>
        error.HResult & 0xFFFF;
}
