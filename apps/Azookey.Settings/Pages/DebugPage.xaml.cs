using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings.Pages;

public sealed partial class DebugPage : SettingsPageBase
{
    public DebugPage()
    {
        InitializeComponent();
    }

    protected override void LoadFromState()
    {
        EnsureOptions(serverLogLevelBox,
        [
            CreateOption("オフ", "off"),
            CreateOption("エラー", "error"),
            CreateOption("警告", "warn"),
            CreateOption("情報", "info"),
            CreateOption("デバッグ", "debug")
        ]);

        serverLogEnabledSwitch.IsOn = State.Config.Debug.ServerLogEnabled;
        SelectComboTag(serverLogLevelBox, State.Config.Debug.ServerLogLevel);
        serverCrashTraceEnabledSwitch.IsOn = State.Config.Debug.ServerCrashTraceEnabled;
    }

    private async void OnDebugSettingChanged(object sender, RoutedEventArgs e)
    {
        await SaveDebugAsync();
    }

    private async void OnServerLogLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveDebugAsync();
    }

    private async Task SaveDebugAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            Debug = config.Debug with
            {
                ServerLogEnabled = serverLogEnabledSwitch.IsOn,
                ServerLogLevel = GetSelectedTag(serverLogLevelBox, config.Debug.ServerLogLevel),
                ServerCrashTraceEnabled = serverCrashTraceEnabledSwitch.IsOn
            }
        });
    }

    private async void OnRestartServerClick(object sender, RoutedEventArgs e)
    {
        await RestartServerAsync();
    }
}
