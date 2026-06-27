using System;
using System.Reflection;

namespace Azookey.Settings.Pages;

public sealed partial class InfoPage : SettingsPageBase
{
    public InfoPage()
    {
        InitializeComponent();
    }

    protected override void LoadFromState()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        versionTextBlock.Text = version?.ToString() ?? "不明";
    }
}
