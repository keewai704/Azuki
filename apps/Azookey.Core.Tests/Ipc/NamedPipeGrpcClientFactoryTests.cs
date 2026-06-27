using Azookey.Core.Ipc;
using Xunit;

namespace Azookey.Core.Tests.Ipc;

public sealed class NamedPipeGrpcClientFactoryTests
{
    [Fact]
    public async Task ConnectPipeAsyncDisposesPipeWhenConnectFails()
    {
        var stream = new TrackableStream();
        var pipe = new FailingPipeConnection(stream, new InvalidOperationException("connect failed"));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NamedPipeGrpcClientFactory.ConnectPipeAsync(pipe, CancellationToken.None).AsTask());

        Assert.Equal("connect failed", error.Message);
        Assert.True(pipe.DisposeCalled);
        Assert.True(stream.IsDisposed);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void CreateChannelRejectsBlankPipeName(string pipeName)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            NamedPipeGrpcClientFactory.CreateChannel(pipeName));

        Assert.Equal("pipeName", error.ParamName);
    }

    private sealed class FailingPipeConnection(TrackableStream stream, Exception error) : INamedPipeConnection
    {
        public bool DisposeCalled { get; private set; }

        public Stream Stream => stream;

        public ValueTask ConnectAsync(CancellationToken cancellationToken) => ValueTask.FromException(error);

        public void Dispose()
        {
            DisposeCalled = true;
            stream.Dispose();
        }
    }

    private sealed class TrackableStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
