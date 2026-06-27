using Azookey.UI.Ipc;
using Grpc.Core;
using Window;
using Xunit;

namespace Azookey.UI.Tests.Ipc;

public sealed class WindowServiceImplTests
{
    [Fact]
    public async Task ShowWindowSendsShowAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        await service.ShowWindow(new EmptyResponse(), TestServerCallContext.Create());

        Assert.IsType<WindowAction.Show>(Assert.Single(sink.Actions));
    }

    [Fact]
    public async Task HideWindowSendsHideAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        await service.HideWindow(new EmptyResponse(), TestServerCallContext.Create());

        Assert.IsType<WindowAction.Hide>(Assert.Single(sink.Actions));
    }

    [Fact]
    public async Task SetCandidateSendsCandidateAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);
        var request = new SetCandidateRequest { Candidates = { "candidate-1", "candidate-2" } };

        await service.SetCandidate(request, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.SetCandidate>(Assert.Single(sink.Actions));
        Assert.Equal(new[] { "candidate-1", "candidate-2" }, action.Candidates);
    }

    [Fact]
    public async Task SetSelectionSendsSelectionAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        await service.SetSelection(new SetSelectionRequest { Index = 3 }, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.SetSelection>(Assert.Single(sink.Actions));
        Assert.Equal(3, action.Index);
    }

    [Fact]
    public async Task SetInputModeSendsInputModeAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        await service.SetInputMode(new SetInputModeRequest { Mode = "katakana" }, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.SetInputMode>(Assert.Single(sink.Actions));
        Assert.Equal("katakana", action.Mode);
    }

    [Fact]
    public async Task SetWindowPositionWithoutPositionReturnsInvalidArgument()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);

        RpcException error = await Assert.ThrowsAsync<RpcException>(() =>
            service.SetWindowPosition(new SetPositionRequest(), TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task SetWindowPositionWithPositionSendsPositionAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);
        var request = new SetPositionRequest
        {
            Position = new WindowPosition { Top = 10, Left = 20, Bottom = 30, Right = 40 }
        };

        await service.SetWindowPosition(request, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.SetPosition>(Assert.Single(sink.Actions));
        Assert.Equal(new WindowRect(10, 20, 30, 40), action.Position);
    }

    [Fact]
    public async Task UpdateCandidateWindowSendsBatchedAction()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);
        var request = new UpdateCandidateWindowRequest
        {
            Visible = true,
            Position = new WindowPosition { Top = 1, Left = 2, Bottom = 3, Right = 4 },
            Candidates = new CandidateList { Candidates = { "candidate-1" } },
            SelectedIndex = 0,
            InputMode = "hiragana",
            Reading = "reading",
            CandidateListVisible = true,
            ReadingVerticalAdjustment = 4
        };

        await service.UpdateCandidateWindow(request, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.UpdateCandidateWindow>(Assert.Single(sink.Actions));
        Assert.True(action.Visible);
        Assert.Equal(new WindowRect(1, 2, 3, 4), action.Position);
        Assert.Equal(new[] { "candidate-1" }, action.Candidates);
        Assert.Equal(0, action.SelectedIndex);
        Assert.Equal("hiragana", action.InputMode);
        Assert.Equal("reading", action.Reading);
        Assert.True(action.CandidateListVisible);
        Assert.Equal(4, action.ReadingVerticalAdjustment);
    }

    [Fact]
    public async Task UpdateCandidateWindowLeavesOmittedOptionalFieldsAsNull()
    {
        var sink = new TestSink();
        var service = new WindowServiceImpl(sink);
        var request = new UpdateCandidateWindowRequest
        {
            Position = new WindowPosition { Top = 5, Left = 6, Bottom = 7, Right = 8 },
            Candidates = new CandidateList { Candidates = { "candidate-1", "candidate-2" } }
        };

        await service.UpdateCandidateWindow(request, TestServerCallContext.Create());

        var action = Assert.IsType<WindowAction.UpdateCandidateWindow>(Assert.Single(sink.Actions));
        Assert.Null(action.Visible);
        Assert.Equal(new WindowRect(5, 6, 7, 8), action.Position);
        Assert.Equal(new[] { "candidate-1", "candidate-2" }, action.Candidates);
        Assert.Null(action.SelectedIndex);
        Assert.Null(action.InputMode);
        Assert.Null(action.Reading);
        Assert.Null(action.CandidateListVisible);
        Assert.Null(action.ReadingVerticalAdjustment);
    }

    private sealed class TestSink : IWindowActionSink
    {
        public List<WindowAction> Actions { get; } = [];

        public ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class TestServerCallContext : ServerCallContext
{
    public static ServerCallContext Create() => new TestServerCallContext();

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "pipe";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("anonymous", []);
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
