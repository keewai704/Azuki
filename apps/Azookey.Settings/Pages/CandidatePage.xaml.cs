using Microsoft.UI.Xaml;

namespace Azookey.Settings.Pages;

public sealed partial class CandidatePage : SettingsPageBase
{
    public CandidatePage()
    {
        InitializeComponent();
    }

    protected override void LoadFromState()
    {
        showCandidateWindowAfterSpaceSwitch.IsOn = State.Config.General.ShowCandidateWindowAfterSpace;
    }

    private async void OnShowCandidateWindowAfterSpaceToggled(object sender, RoutedEventArgs e)
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            General = config.General with
            {
                ShowCandidateWindowAfterSpace = showCandidateWindowAfterSpaceSwitch.IsOn
            }
        });
    }
}
