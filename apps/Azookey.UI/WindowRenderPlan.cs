using Azookey.UI.Candidate;
using Azookey.UI.Ipc;

namespace Azookey.UI;

internal readonly record struct WindowPlacement(int X, int Y, WindowSize Size);

internal readonly record struct WindowVisibility(bool ShowCandidate, bool ShowIndicator)
{
    public static WindowVisibility FromState(CandidateState state) =>
        new(
            state.Visible && state.CandidateListVisible && state.Candidates.Count > 0,
            state.InputModeIndicatorVisible && !string.IsNullOrWhiteSpace(state.InputMode));

    public bool HasVisibleWindow => ShowCandidate || ShowIndicator;
}

internal readonly record struct WindowRenderPlan(WindowPlacement? Candidate, WindowPlacement? Indicator)
{
    private const int IndicatorWindowLeftOffset = 45;

    public static WindowRenderPlan Create(
        CandidateState state,
        WindowVisibility visibility,
        WorkArea workArea,
        WindowSize candidateSize,
        WindowSize indicatorSize)
    {
        if (state.Position is not { } position)
        {
            return default;
        }

        return Create(position, visibility, workArea, candidateSize, indicatorSize);
    }

    public static WindowRenderPlan Create(
        WindowRect position,
        WindowVisibility visibility,
        WorkArea workArea,
        WindowSize candidateSize,
        WindowSize indicatorSize)
    {
        WindowPlacement? candidate = null;
        if (visibility.ShowCandidate)
        {
            (int candidateX, int candidateY) = WindowGeometry.CandidateWindowPosition(position, candidateSize, workArea);
            candidate = new WindowPlacement(candidateX, candidateY, candidateSize);
        }

        WindowPlacement? indicator = null;
        if (visibility.ShowIndicator)
        {
            int indicatorX = Math.Clamp(position.Left - IndicatorWindowLeftOffset, workArea.Left, workArea.Right - indicatorSize.Width);
            int indicatorY = Math.Clamp(position.Bottom, workArea.Top, workArea.Bottom - indicatorSize.Height);
            indicator = new WindowPlacement(indicatorX, indicatorY, indicatorSize);
        }

        return new WindowRenderPlan(candidate, indicator);
    }
}
