using System.IO;
using System.IO.Pipes;
using Grpc.Net.Client;

namespace Azookey.Core.Ipc;

public static class NamedPipeGrpcClientFactory
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    public static GrpcChannel CreateChannel(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ConnectTimeout);

                return await ConnectPipeAsync(
                    new NamedPipeConnection(pipeName),
                    cts.Token);
            }
        };

        return GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = handler });
    }

    internal static async ValueTask<Stream> ConnectPipeAsync(INamedPipeConnection pipe, CancellationToken cancellationToken)
    {
        try
        {
            await pipe.ConnectAsync(cancellationToken);
            return pipe.Stream;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    private sealed class NamedPipeConnection(string pipeName) : INamedPipeConnection
    {
        private readonly NamedPipeClientStream pipe = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        public Stream Stream => pipe;

        public ValueTask ConnectAsync(CancellationToken cancellationToken) =>
            new(pipe.ConnectAsync(cancellationToken));

        public void Dispose()
        {
            pipe.Dispose();
        }
    }
}

internal interface INamedPipeConnection : IDisposable
{
    Stream Stream { get; }

    ValueTask ConnectAsync(CancellationToken cancellationToken);
}
