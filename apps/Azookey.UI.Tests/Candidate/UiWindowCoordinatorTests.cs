using Azookey.UI;
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;
using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class UiWindowCoordinatorTests
{
    [Fact]
    public async Task CoordinatorAppliesActionsToStateBeforeRendering()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer);

        await coordinator.SendAsync(new WindowAction.SetCandidate(["a", "b"]), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.SetSelection(1), CancellationToken.None);

        Assert.Equal(2, renderer.LastState.Candidates.Count);
        Assert.Equal(1, renderer.LastState.SelectedIndex);
    }

    [Fact]
    public async Task CoordinatorHidesInputModeIndicatorAfterDisplayDuration()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer, TimeSpan.FromMilliseconds(20));

        await coordinator.SendAsync(new WindowAction.SetPosition(new WindowRect(1, 2, 3, 4)), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.SetInputMode("katakana"), CancellationToken.None);

        Assert.True(renderer.LastState.InputModeIndicatorVisible);

        await Task.Delay(120);

        Assert.False(renderer.LastState.InputModeIndicatorVisible);
        Assert.Contains(renderer.States, state => state.InputModeIndicatorVisible);
        Assert.Contains(renderer.States, state => !state.InputModeIndicatorVisible && state.InputMode == "katakana");
    }

    [Fact]
    public async Task CoordinatorRestartsIndicatorHideDelayWhenInputModeUpdates()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer, TimeSpan.FromMilliseconds(80));

        await coordinator.SendAsync(new WindowAction.SetPosition(new WindowRect(1, 2, 3, 4)), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.SetInputMode("katakana"), CancellationToken.None);
        await Task.Delay(30);
        await coordinator.SendAsync(new WindowAction.SetInputMode("hiragana"), CancellationToken.None);
        await Task.Delay(30);

        Assert.True(renderer.LastState.InputModeIndicatorVisible);

        await Task.Delay(90);

        Assert.False(renderer.LastState.InputModeIndicatorVisible);
        Assert.Equal("hiragana", renderer.LastState.InputMode);
    }

    [Fact]
    public async Task CoordinatorHidesIndicatorAfterUpdateCandidateWindowInputMode()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer, TimeSpan.FromMilliseconds(20));

        await coordinator.SendAsync(new WindowAction.UpdateCandidateWindow(
            Visible: null,
            Position: new WindowRect(1, 2, 3, 4),
            Candidates: ["one"],
            SelectedIndex: 0,
            InputMode: "hiragana",
            Reading: null,
            CandidateListVisible: true,
            ReadingVerticalAdjustment: null), CancellationToken.None);

        Assert.True(renderer.LastState.InputModeIndicatorVisible);

        await Task.Delay(120);

        Assert.False(renderer.LastState.InputModeIndicatorVisible);
        Assert.Equal("hiragana", renderer.LastState.InputMode);
    }

    [Fact]
    public async Task CoordinatorShowCancelsVisibleInputModeIndicator()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer, TimeSpan.FromMilliseconds(80));

        await coordinator.SendAsync(new WindowAction.SetInputMode("katakana"), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.Show(), CancellationToken.None);

        Assert.True(renderer.LastState.Visible);
        Assert.False(renderer.LastState.InputModeIndicatorVisible);

        await Task.Delay(120);

        Assert.False(renderer.LastState.InputModeIndicatorVisible);
        Assert.True(renderer.LastState.Visible);
    }

    [Fact]
    public async Task CoordinatorVisibleCandidateUpdateCancelsVisibleInputModeIndicator()
    {
        var renderer = new RecordingRenderer();
        var coordinator = new UiWindowCoordinator(renderer, TimeSpan.FromMilliseconds(80));

        await coordinator.SendAsync(new WindowAction.SetInputMode("katakana"), CancellationToken.None);
        await coordinator.SendAsync(new WindowAction.UpdateCandidateWindow(
            Visible: true,
            Position: new WindowRect(1, 2, 3, 4),
            Candidates: ["one"],
            SelectedIndex: 0,
            InputMode: "hiragana",
            Reading: null,
            CandidateListVisible: true,
            ReadingVerticalAdjustment: null), CancellationToken.None);

        Assert.True(renderer.LastState.Visible);
        Assert.Equal("hiragana", renderer.LastState.InputMode);
        Assert.False(renderer.LastState.InputModeIndicatorVisible);

        await Task.Delay(120);

        Assert.False(renderer.LastState.InputModeIndicatorVisible);
    }

    private sealed class RecordingRenderer : IUiWindowRenderer
    {
        private readonly object gate = new();
        private readonly List<CandidateState> states = [];

        private CandidateState lastState = CandidateState.Initial;

        public CandidateState LastState
        {
            get
            {
                lock (gate)
                {
                    return lastState;
                }
            }
        }

        public IReadOnlyList<CandidateState> States
        {
            get
            {
                lock (gate)
                {
                    return states.ToArray();
                }
            }
        }

        public void Render(CandidateState state)
        {
            lock (gate)
            {
                lastState = state;
                states.Add(state);
            }
        }
    }
}
