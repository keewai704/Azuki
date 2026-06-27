using Azookey.Core.Win32;
using Azookey.UI;
using Azookey.UI.Candidate;
using Microsoft.UI.Xaml;

namespace Azookey.UI.Windows;

public sealed partial class IndicatorWindow : Microsoft.UI.Xaml.Window
{
    public IndicatorWindow()
    {
        InitializeComponent();
        WindowInterop.MakeImeToolWindow(this);
        AppWindow.Hide();
    }

    public WindowSize WindowSize { get; } = new(90, 90);

    public void SetPlacement(int x, int y)
    {
        WindowInterop.MoveNoActivate(this, x, y, WindowSize.Width, WindowSize.Height);
    }

    public void Render(CandidateState state)
    {
        ModeText.Text = state.InputMode;

        if (WindowVisibility.FromState(state).ShowIndicator)
        {
            WindowInterop.ShowNoActivate(this);
        }
        else
        {
            AppWindow.Hide();
        }
    }
}
