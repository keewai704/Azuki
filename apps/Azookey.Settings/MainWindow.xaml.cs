using System;
using System.Collections.Generic;
using System.Linq;
using Azookey.Settings.Pages;
using Azookey.Settings.Services;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private static readonly IReadOnlyDictionary<string, Type> Pages = new Dictionary<string, Type>
    {
        ["general"] = typeof(GeneralPage),
        ["input"] = typeof(InputPage),
        ["candidate"] = typeof(CandidatePage),
        ["zenzai"] = typeof(ZenzaiPage),
        ["dictionary"] = typeof(UserDictionaryPage),
        ["debug"] = typeof(DebugPage),
        ["info"] = typeof(InfoPage),
    };

    private string? currentTag;

    public MainWindow(SettingsAppState state)
    {
        State = state;
        InitializeComponent();

        Title = "azooKey 設定";
        NavigateToDefaultPage();

        if (State.LoadResult.Recovery is { } recovery)
        {
            ShowStatus($"壊れた設定ファイルから設定を復旧しました。バックアップ: {recovery.BackupPath}");
        }
    }

    public SettingsAppState State { get; }

    public void ShowStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            StatusInfoBar.IsOpen = false;
            StatusInfoBar.Message = string.Empty;
            return;
        }

        StatusInfoBar.Severity = ResolveSeverity(message);
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        NavigateToTag(args.SelectedItemContainer?.Tag?.ToString());
    }

    private void NavigateToDefaultPage()
    {
        if (RootNavigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is not { } item)
        {
            return;
        }

        RootNavigation.SelectedItem = item;
        NavigateToTag(item.Tag?.ToString());
    }

    private void NavigateToTag(string? tag)
    {
        string resolvedTag = string.IsNullOrWhiteSpace(tag) ? "general" : tag;
        if (!Pages.TryGetValue(resolvedTag, out Type? pageType))
        {
            resolvedTag = "general";
            pageType = Pages[resolvedTag];
        }

        if (string.Equals(currentTag, resolvedTag, StringComparison.Ordinal))
        {
            return;
        }

        currentTag = resolvedTag;
        ContentFrame.Navigate(pageType, new SettingsPageContext(State, this));
    }

    private static InfoBarSeverity ResolveSeverity(string message)
    {
        if (message.Contains("保存しました。", StringComparison.Ordinal) ||
            message.Contains("要求しました。", StringComparison.Ordinal) ||
            message.Contains("起動しました。", StringComparison.Ordinal) ||
            message.Contains("完了しました。", StringComparison.Ordinal))
        {
            return InfoBarSeverity.Success;
        }

        if (message.Contains("復旧", StringComparison.Ordinal) ||
            message.Contains("反映できませんでした", StringComparison.Ordinal))
        {
            return InfoBarSeverity.Warning;
        }

        if (message.Contains("失敗", StringComparison.Ordinal) ||
            message.Contains("できません", StringComparison.Ordinal))
        {
            return InfoBarSeverity.Error;
        }

        return InfoBarSeverity.Informational;
    }
}
