using Azookey.UI;
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;
using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class WindowRenderPlanTests
{
    private static readonly WindowRect Target = new(20, 100, 40, 180);
    private static readonly WorkArea WorkArea = new(0, 0, 800, 600);

    [Fact]
    public void CreateSkipsPlacementsForHiddenWindows()
    {
        CandidateState state = CandidateState.Initial with
        {
            Visible = true,
            Position = Target,
            Candidates = ["candidate-1"],
            CandidateListVisible = false,
            Reading = "",
            InputMode = ""
        };

        WindowVisibility visibility = WindowVisibility.FromState(state);
        WindowRenderPlan plan = WindowRenderPlan.Create(
            state,
            visibility,
            WorkArea,
            new WindowSize(240, 120),
            new WindowSize(90, 90));

        Assert.False(visibility.ShowCandidate);
        Assert.False(visibility.ShowIndicator);
        Assert.Null(plan.Candidate);
        Assert.Null(plan.Indicator);
    }

    [Fact]
    public void CreateIgnoresReadingDisplayWhenReadingIsProvided()
    {
        CandidateState state = CandidateState.Initial with
        {
            Visible = true,
            Position = Target,
            Candidates = ["candidate-1"],
            CandidateListVisible = true,
            Reading = "reading",
            InputMode = ""
        };

        WindowVisibility visibility = WindowVisibility.FromState(state);
        WindowSize candidateSize = new(240, 120);
        WindowRenderPlan plan = WindowRenderPlan.Create(
            state,
            visibility,
            WorkArea,
            candidateSize,
            new WindowSize(90, 90));
        (int expectedCandidateX, int expectedCandidateY) = WindowGeometry.CandidateWindowPosition(
            Target,
            candidateSize,
            WorkArea);

        Assert.True(visibility.ShowCandidate);
        Assert.False(visibility.ShowIndicator);
        Assert.Equal(new WindowPlacement(expectedCandidateX, expectedCandidateY, candidateSize), plan.Candidate);
        Assert.Null(plan.Indicator);
    }

    [Fact]
    public void CreateShowsIndicatorOnlyForTransientIndicatorState()
    {
        CandidateState state = CandidateState.Initial with
        {
            Visible = true,
            Position = Target,
            Candidates = ["candidate-1"],
            CandidateListVisible = true,
            InputMode = "A",
            InputModeIndicatorVisible = true
        };

        WindowVisibility visibility = WindowVisibility.FromState(state);
        WindowRenderPlan plan = WindowRenderPlan.Create(
            state,
            visibility,
            WorkArea,
            new WindowSize(240, 120),
            new WindowSize(90, 90));

        Assert.True(visibility.ShowCandidate);
        Assert.True(visibility.ShowIndicator);
        Assert.NotNull(plan.Candidate);
        Assert.NotNull(plan.Indicator);
    }

    [Fact]
    public void CreateDoesNotShowIndicatorJustBecauseInputModeExists()
    {
        CandidateState state = CandidateState.Initial with
        {
            Visible = true,
            Position = Target,
            Candidates = ["candidate-1"],
            CandidateListVisible = true,
            InputMode = "A",
            InputModeIndicatorVisible = false
        };

        WindowVisibility visibility = WindowVisibility.FromState(state);

        Assert.True(visibility.ShowCandidate);
        Assert.False(visibility.ShowIndicator);
    }

    [Fact]
    public void VisibleCandidateListDoesNotRequireTsfTextExtPosition()
    {
        CandidateState state = CandidateState.Initial with
        {
            Visible = true,
            Position = null,
            Candidates = ["candidate-1"],
            CandidateListVisible = true,
            InputMode = "",
            InputModeIndicatorVisible = false
        };

        WindowVisibility visibility = WindowVisibility.FromState(state);

        Assert.True(visibility.ShowCandidate);
        Assert.True(visibility.HasVisibleWindow);
    }
}
