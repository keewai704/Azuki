using Azookey.UI.Ipc;

namespace Azookey.UI.Candidate;

public sealed record CandidateState
{
    public static CandidateState Initial { get; } = new();

    public bool Visible { get; init; }
    public WindowRect? Position { get; init; }
    public IReadOnlyList<string> Candidates { get; init; } = [];
    public int SelectedIndex { get; init; }
    public string InputMode { get; init; } = "\u3042";
    public bool InputModeIndicatorVisible { get; init; }
    public string Reading { get; init; } = "";
    public bool CandidateListVisible { get; init; } = true;
    public int ReadingVerticalAdjustment { get; init; } = 4;

    public CandidateState Apply(WindowAction action) =>
        action switch
        {
            WindowAction.Show => this with { Visible = true, InputModeIndicatorVisible = false },
            WindowAction.Hide => this with { Visible = false, Reading = "", InputModeIndicatorVisible = false },
            WindowAction.SetPosition set => this with { Position = set.Position },
            WindowAction.SetCandidate set => this with
            {
                Candidates = set.Candidates.ToArray(),
                CandidateListVisible = true,
                SelectedIndex = ClampSelection(SelectedIndex, set.Candidates.Count)
            },
            WindowAction.SetSelection set => this with { SelectedIndex = ClampSelection(set.Index, Candidates.Count) },
            WindowAction.SetInputMode set => this with { InputMode = set.Mode, InputModeIndicatorVisible = true },
            WindowAction.HideInputModeIndicator => this with { InputModeIndicatorVisible = false },
            WindowAction.UpdateCandidateWindow update => Apply(update),
            _ => this
        };

    public CandidatePage GetCandidatePage(int rowCount)
    {
        if (rowCount <= 0 || Candidates.Count == 0)
        {
            return new CandidatePage([], SelectedRowIndex: 0, StartIndex: 0);
        }

        int pageStart = SelectedIndex / rowCount * rowCount;
        IReadOnlyList<string> visibleCandidates = Candidates
            .Skip(pageStart)
            .Take(rowCount)
            .ToArray();

        return new CandidatePage(visibleCandidates, SelectedIndex - pageStart, pageStart);
    }

    private CandidateState Apply(WindowAction.UpdateCandidateWindow update)
    {
        IReadOnlyList<string> candidates = update.Candidates?.ToArray() ?? Candidates;
        int selectedIndex = ClampSelection(update.SelectedIndex ?? SelectedIndex, candidates.Count);

        return this with
        {
            Visible = update.Visible ?? Visible,
            Position = update.Position ?? Position,
            Candidates = candidates,
            SelectedIndex = selectedIndex,
            InputMode = update.InputMode ?? InputMode,
            InputModeIndicatorVisible = ResolveInputModeIndicatorVisibility(update),
            Reading = update.Reading ?? Reading,
            CandidateListVisible = update.CandidateListVisible ?? CandidateListVisible,
            ReadingVerticalAdjustment = update.ReadingVerticalAdjustment ?? ReadingVerticalAdjustment
        };
    }

    private bool ResolveInputModeIndicatorVisibility(WindowAction.UpdateCandidateWindow update)
    {
        if (update.Visible is true)
        {
            return false;
        }

        return update.InputMode is null ? InputModeIndicatorVisible : true;
    }

    private static int ClampSelection(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, count - 1);
    }
}

public sealed record CandidatePage(IReadOnlyList<string> Candidates, int SelectedRowIndex, int StartIndex);
