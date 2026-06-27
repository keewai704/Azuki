namespace Azookey.UI.Ipc;

public readonly record struct WindowRect(int Top, int Left, int Bottom, int Right);

public abstract record WindowAction
{
    public sealed record Show : WindowAction;
    public sealed record Hide : WindowAction;
    public sealed record SetPosition(WindowRect Position) : WindowAction;
    public sealed record SetSelection(int Index) : WindowAction;
    public sealed record SetCandidate(IReadOnlyList<string> Candidates) : WindowAction;
    public sealed record SetInputMode(string Mode) : WindowAction;
    internal sealed record HideInputModeIndicator : WindowAction;
    public sealed record UpdateCandidateWindow(
        bool? Visible,
        WindowRect? Position,
        IReadOnlyList<string>? Candidates,
        int? SelectedIndex,
        string? InputMode,
        string? Reading,
        bool? CandidateListVisible,
        int? ReadingVerticalAdjustment) : WindowAction;
}

public interface IWindowActionSink
{
    ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken);
}
