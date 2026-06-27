using Azookey.UI.Candidate;
using Azookey.UI.Ipc;
using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class CandidateStateTests
{
    [Fact]
    public void UpdateCandidateWindowMergesOnlyProvidedFields()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetPosition(new WindowRect(9, 8, 7, 6)))
            .Apply(new WindowAction.SetCandidate(["one", "two"]))
            .Apply(new WindowAction.SetSelection(1))
            .Apply(new WindowAction.UpdateCandidateWindow(
                Visible: false,
                Position: null,
                Candidates: null,
                SelectedIndex: null,
                InputMode: null,
                Reading: null,
                CandidateListVisible: null,
                ReadingVerticalAdjustment: 7));

        CandidateState updated = state.Apply(new WindowAction.UpdateCandidateWindow(
            Visible: true,
            Position: new WindowRect(1, 2, 3, 4),
            Candidates: null,
            SelectedIndex: null,
            InputMode: "A",
            Reading: "read",
            CandidateListVisible: false,
            ReadingVerticalAdjustment: null));

        Assert.True(updated.Visible);
        Assert.Equal(new WindowRect(1, 2, 3, 4), updated.Position);
        Assert.Equal(new[] { "one", "two" }, updated.Candidates);
        Assert.Equal(1, updated.SelectedIndex);
        Assert.Equal("A", updated.InputMode);
        Assert.False(updated.InputModeIndicatorVisible);
        Assert.Equal("read", updated.Reading);
        Assert.False(updated.CandidateListVisible);
        Assert.Equal(7, updated.ReadingVerticalAdjustment);
    }

    [Fact]
    public void SetInputModeShowsTransientIndicatorState()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetInputMode("katakana"));

        Assert.Equal("katakana", state.InputMode);
        Assert.True(state.InputModeIndicatorVisible);
    }

    [Fact]
    public void ShowClearsVisibleInputModeIndicator()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetInputMode("katakana"))
            .Apply(new WindowAction.Show());

        Assert.True(state.Visible);
        Assert.Equal("katakana", state.InputMode);
        Assert.False(state.InputModeIndicatorVisible);
    }

    [Fact]
    public void UpdateCandidateWindowVisibleTrueClearsVisibleInputModeIndicator()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetInputMode("katakana"));

        CandidateState updated = state.Apply(new WindowAction.UpdateCandidateWindow(
            Visible: true,
            Position: null,
            Candidates: null,
            SelectedIndex: null,
            InputMode: "hiragana",
            Reading: null,
            CandidateListVisible: null,
            ReadingVerticalAdjustment: null));

        Assert.True(updated.Visible);
        Assert.Equal("hiragana", updated.InputMode);
        Assert.False(updated.InputModeIndicatorVisible);
    }

    [Fact]
    public void HideInputModeIndicatorClearsOnlyIndicatorVisibility()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetInputMode("katakana"))
            .Apply(new WindowAction.HideInputModeIndicator());

        Assert.Equal("katakana", state.InputMode);
        Assert.False(state.InputModeIndicatorVisible);
    }

    [Fact]
    public void SelectionIsClampedToCandidateRange()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two"]))
            .Apply(new WindowAction.SetSelection(99));

        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void UpdateCandidateWindowClampsExistingSelectionWhenCandidatesShrink()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two", "three"]))
            .Apply(new WindowAction.SetSelection(2));

        CandidateState updated = state.Apply(new WindowAction.UpdateCandidateWindow(
            Visible: null,
            Position: null,
            Candidates: ["alpha"],
            SelectedIndex: null,
            InputMode: null,
            Reading: null,
            CandidateListVisible: null,
            ReadingVerticalAdjustment: null));

        Assert.Equal(new[] { "alpha" }, updated.Candidates);
        Assert.Equal(0, updated.SelectedIndex);
    }

    [Fact]
    public void UpdateCandidateWindowClampsProvidedSelectionToCandidateRange()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two"]));

        CandidateState updated = state.Apply(new WindowAction.UpdateCandidateWindow(
            Visible: null,
            Position: null,
            Candidates: ["alpha"],
            SelectedIndex: 99,
            InputMode: null,
            Reading: null,
            CandidateListVisible: null,
            ReadingVerticalAdjustment: null));

        Assert.Equal(new[] { "alpha" }, updated.Candidates);
        Assert.Equal(0, updated.SelectedIndex);
    }

    [Fact]
    public void CandidatePageIncludesSelectedCandidateInFiveRowPage()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two", "three", "four", "five", "six", "seven"]))
            .Apply(new WindowAction.SetSelection(6));

        CandidatePage page = state.GetCandidatePage(5);

        Assert.Equal(new[] { "six", "seven" }, page.Candidates);
        Assert.Equal(1, page.SelectedRowIndex);
        Assert.Equal(5, page.StartIndex);
    }

    [Fact]
    public void CandidatePageUsesFirstPageForInitialRows()
    {
        CandidateState state = CandidateState.Initial
            .Apply(new WindowAction.SetCandidate(["one", "two", "three", "four", "five", "six"]))
            .Apply(new WindowAction.SetSelection(4));

        CandidatePage page = state.GetCandidatePage(5);

        Assert.Equal(new[] { "one", "two", "three", "four", "five" }, page.Candidates);
        Assert.Equal(4, page.SelectedRowIndex);
        Assert.Equal(0, page.StartIndex);
    }
}
