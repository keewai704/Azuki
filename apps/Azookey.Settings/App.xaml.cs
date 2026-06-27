using Azookey.Core.Config;
using Azookey.Settings.Services;
using Microsoft.UI.Xaml;

namespace Azookey.Settings;

public partial class App : Application
{
    private Microsoft.UI.Xaml.Window? window;

    public App()
    {
        StartupDiagnostics.Log("App constructor entered.");
        UnhandledException += OnUnhandledException;
        InitializeComponent();
        StartupDiagnostics.Log("App InitializeComponent completed.");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            StartupDiagnostics.Log("OnLaunched entered.");
            var state = new SettingsAppState(ConfigStore.FromAppData(), new ServerConfigNotifier());
            StartupDiagnostics.Log("SettingsAppState created.");
            window = new MainWindow(state);
            StartupDiagnostics.Log("MainWindow created.");
            window.Activate();
            StartupDiagnostics.Log("MainWindow activated.");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.LogException("OnLaunched failed.", exception);
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        StartupDiagnostics.LogException("Unhandled WinUI exception.", args.Exception);
    }
}
