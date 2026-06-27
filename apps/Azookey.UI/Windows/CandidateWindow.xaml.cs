using Azookey.Core.Win32;
using Azookey.UI;
using Azookey.UI.Candidate;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Azookey.UI.Windows;

public sealed partial class CandidateWindow : Microsoft.UI.Xaml.Window
{
    private readonly Border[] rows;
    private readonly TextBlock[] candidateLabels;
    private readonly TextBlock[] indexLabels;
    private readonly SolidColorBrush selectedBackground = BrushFromRgb(0xD8, 0xEE, 0xFF);
    private readonly SolidColorBrush transparentBackground = BrushFromRgb(0x00, 0x00, 0x00, 0x00);

    public CandidateWindow()
    {
        InitializeComponent();
        rows = [Row1, Row2, Row3, Row4, Row5];
        candidateLabels = [CandidateText1, CandidateText2, CandidateText3, CandidateText4, CandidateText5];
        indexLabels = [IndexText1, IndexText2, IndexText3, IndexText4, IndexText5];
        WindowInterop.MakeImeToolWindow(this);
        AppWindow.Hide();
    }

    public WindowSize MeasureWindowSize(CandidateState state)
    {
        UpdateRows(state);
        RootBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size desired = RootBorder.DesiredSize;
        return new WindowSize((int)Math.Ceiling(desired.Width), (int)Math.Ceiling(desired.Height));
    }

    public void SetPlacement(int x, int y, WindowSize size)
    {
        WindowInterop.MoveNoActivate(this, x, y, size.Width, size.Height);
    }

    public void Render(CandidateState state)
    {
        UpdateRows(state);

        if (WindowVisibility.FromState(state).ShowCandidate)
        {
            WindowInterop.ShowNoActivate(this);
        }
        else
        {
            AppWindow.Hide();
        }
    }

    private void UpdateRows(CandidateState state)
    {
        CandidatePage page = state.GetCandidatePage(rows.Length);

        CandidateListPanel.Visibility = state.CandidateListVisible ? Visibility.Visible : Visibility.Collapsed;
        FooterBorder.Visibility = state.CandidateListVisible ? Visibility.Visible : Visibility.Collapsed;

        for (int index = 0; index < rows.Length; index++)
        {
            bool hasCandidate = index < page.Candidates.Count;
            rows[index].Visibility = Visibility.Visible;
            rows[index].Background = page.SelectedRowIndex == index ? selectedBackground : transparentBackground;
            candidateLabels[index].Text = hasCandidate ? page.Candidates[index] : string.Empty;
            indexLabels[index].Text = hasCandidate ? (page.StartIndex + index + 1).ToString() : string.Empty;
        }
    }

    private static SolidColorBrush BrushFromRgb(byte red, byte green, byte blue, byte alpha = 0xFF) =>
        new(new global::Windows.UI.Color { A = alpha, R = red, G = green, B = blue });
}
