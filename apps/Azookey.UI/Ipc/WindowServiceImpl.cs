using Grpc.Core;
using Window;

namespace Azookey.UI.Ipc;

public sealed class WindowServiceImpl : WindowService.WindowServiceBase
{
    private readonly IWindowActionSink sink;

    public WindowServiceImpl(IWindowActionSink sink)
    {
        this.sink = sink;
    }

    public override Task<EmptyResponse> ShowWindow(EmptyResponse request, ServerCallContext context) =>
        SendAsync(new WindowAction.Show(), context.CancellationToken);

    public override Task<EmptyResponse> HideWindow(EmptyResponse request, ServerCallContext context) =>
        SendAsync(new WindowAction.Hide(), context.CancellationToken);

    public override Task<EmptyResponse> SetWindowPosition(SetPositionRequest request, ServerCallContext context)
    {
        if (request.Position is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "position is required"));
        }

        return SendAsync(new WindowAction.SetPosition(ToRect(request.Position)), context.CancellationToken);
    }

    public override Task<EmptyResponse> SetCandidate(SetCandidateRequest request, ServerCallContext context) =>
        SendAsync(new WindowAction.SetCandidate(request.Candidates.ToArray()), context.CancellationToken);

    public override Task<EmptyResponse> SetSelection(SetSelectionRequest request, ServerCallContext context) =>
        SendAsync(new WindowAction.SetSelection(request.Index), context.CancellationToken);

    public override Task<EmptyResponse> SetInputMode(SetInputModeRequest request, ServerCallContext context) =>
        SendAsync(new WindowAction.SetInputMode(request.Mode), context.CancellationToken);

    public override Task<EmptyResponse> UpdateCandidateWindow(UpdateCandidateWindowRequest request, ServerCallContext context)
    {
        WindowRect? position = request.Position is null ? null : ToRect(request.Position);
        IReadOnlyList<string>? candidates = request.Candidates is null ? null : request.Candidates.Candidates.ToArray();

        return SendAsync(
            new WindowAction.UpdateCandidateWindow(
                request.HasVisible ? request.Visible : null,
                position,
                candidates,
                request.HasSelectedIndex ? request.SelectedIndex : null,
                request.HasInputMode ? request.InputMode : null,
                request.HasReading ? request.Reading : null,
                request.HasCandidateListVisible ? request.CandidateListVisible : null,
                request.HasReadingVerticalAdjustment ? request.ReadingVerticalAdjustment : null),
            context.CancellationToken);
    }

    private async Task<EmptyResponse> SendAsync(WindowAction action, CancellationToken cancellationToken)
    {
        await sink.SendAsync(action, cancellationToken);
        return new EmptyResponse();
    }

    private static WindowRect ToRect(WindowPosition position) =>
        new(position.Top, position.Left, position.Bottom, position.Right);
}
