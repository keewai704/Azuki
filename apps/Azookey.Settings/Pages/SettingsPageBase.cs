using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azookey.Core.Config;
using Azookey.Core.Process;
using Azookey.Settings.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Azookey.Settings.Pages;

public class SettingsPageBase : Page
{
    private readonly ServerRestartService serverRestartService = new();

    protected SettingsAppState State { get; private set; } = null!;

    protected MainWindow Shell { get; private set; } = null!;

    protected bool IsLoading { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not SettingsPageContext context)
        {
            throw new InvalidOperationException("Settings page navigation requires a SettingsPageContext parameter.");
        }

        State = context.State;
        Shell = context.Shell;

        IsLoading = true;
        try
        {
            LoadFromState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual void LoadFromState()
    {
    }

    protected void ShowStatus(string message) => Shell.ShowStatus(message);

    protected async Task<SaveResult?> SaveConfigAsync(Func<AppConfig, AppConfig> update)
    {
        try
        {
            SaveResult result = await State.SaveAsync(update(State.Config));
            ShowStatus(result.Message ?? "保存しました。");
            return result;
        }
        catch (Exception error)
        {
            ShowStatus(SaveStatusMessages.CreateSaveFailedMessage(error));
            return null;
        }
    }

    protected async Task RestartServerAsync()
    {
        try
        {
            ServerRestartResult result = await serverRestartService.RestartAsync(GetInstallDirectory(), State.Config);
            ShowStatus(result.Status switch
            {
                ServerRestartStatus.RestartedByLauncher => "サーバーの再起動を要求しました。",
                ServerRestartStatus.StartedDirectly => "サーバーを直接起動しました。",
                ServerRestartStatus.LauncherFailed => $"ランチャー経由の再起動に失敗しました: {result.Message}",
                ServerRestartStatus.DirectStartFailed => $"サーバーの起動に失敗しました: {result.Message}",
                _ => "サーバーの再起動処理が完了しました。"
            });
        }
        catch (Exception error)
        {
            ShowStatus($"サーバーの起動に失敗しました: {error.Message}");
        }
    }

    protected static ComboBoxItem CreateOption(string content, string tag) =>
        new()
        {
            Content = content,
            Tag = tag
        };

    protected static void EnsureOptions(ComboBox comboBox, IReadOnlyList<ComboBoxItem> items)
    {
        if (comboBox.Items.Count > 0)
        {
            return;
        }

        foreach (ComboBoxItem item in items)
        {
            comboBox.Items.Add(item);
        }
    }

    protected static void SelectComboTag(ComboBox comboBox, string tag)
    {
        foreach (ComboBoxItem item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    protected static string GetSelectedTag(ComboBox? comboBox, string fallback)
    {
        return comboBox?.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? fallback
            : fallback;
    }

    protected static T GetSelectedEnum<T>(ComboBox? comboBox, T fallback) where T : struct, Enum
    {
        return comboBox?.SelectedItem is ComboBoxItem item &&
            Enum.TryParse(item.Tag?.ToString(), out T parsed)
                ? parsed
                : fallback;
    }

    private static string GetInstallDirectory()
    {
        return ResolveInstallDirectory(AppContext.BaseDirectory);
    }

    internal static string ResolveInstallDirectory(string baseDirectory)
    {
        return baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
