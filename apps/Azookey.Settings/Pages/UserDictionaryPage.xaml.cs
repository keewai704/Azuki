using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azookey.Core.Config;
using Azookey.Settings.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings.Pages;

public sealed partial class UserDictionaryPage : SettingsPageBase
{
    private readonly ObservableCollection<DictionaryEntryEditor> dictionaryEntries = [];

    public UserDictionaryPage()
    {
        InitializeComponent();
        dictionaryEntriesList.ItemsSource = dictionaryEntries;
    }

    protected override void LoadFromState()
    {
        dictionaryEntries.Clear();
        foreach (UserDictionaryEntry entry in State.Config.UserDictionary.Entries)
        {
            dictionaryEntries.Add(new DictionaryEntryEditor(entry.Reading, entry.Word));
        }
    }

    private async void OnAddDictionaryEntryClick(object sender, RoutedEventArgs e)
    {
        DictionaryEntryEditor? entry = await ShowDictionaryEntryDialogAsync(new DictionaryEntryEditor("", ""));
        if (entry is not null)
        {
            dictionaryEntries.Add(entry);
        }
    }

    private async void OnEditDictionaryEntryClick(object sender, RoutedEventArgs e)
    {
        if (dictionaryEntriesList.SelectedItem is not DictionaryEntryEditor selected)
        {
            ShowStatus("編集する項目を選択してください。");
            return;
        }

        DictionaryEntryEditor? updated = await ShowDictionaryEntryDialogAsync(selected);
        if (updated is null)
        {
            return;
        }

        int index = dictionaryEntries.IndexOf(selected);
        if (index >= 0)
        {
            dictionaryEntries[index] = updated;
        }
    }

    private void OnRemoveDictionaryEntryClick(object sender, RoutedEventArgs e)
    {
        if (dictionaryEntriesList.SelectedItem is not DictionaryEntryEditor selected)
        {
            ShowStatus("削除する項目を選択してください。");
            return;
        }

        dictionaryEntries.Remove(selected);
    }

    private async void OnSaveDictionaryClick(object sender, RoutedEventArgs e)
    {
        await SaveDictionaryAsync();
    }

    private async Task<DictionaryEntryEditor?> ShowDictionaryEntryDialogAsync(DictionaryEntryEditor initial)
    {
        TextBox readingBox = new() { Header = "読み", Text = initial.Reading };
        TextBox wordBox = new() { Header = "単語", Text = initial.Word };
        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(readingBox);
        panel.Children.Add(wordBox);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "ユーザー辞書",
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? new DictionaryEntryEditor(readingBox.Text, wordBox.Text)
            : null;
    }

    private async Task SaveDictionaryAsync()
    {
        List<UserDictionaryEntry> nextEntries = dictionaryEntries
            .Select(entry => new UserDictionaryEntry
            {
                Reading = entry.Reading,
                Word = entry.Word
            })
            .ToList();

        DictionaryValidationResult validation = DictionaryViewModel.Validate(nextEntries);
        if (!validation.IsValid)
        {
            ShowStatus(validation.Message!);
            return;
        }

        List<UserDictionaryEntry> trimmedEntries = nextEntries
            .Select(entry => new UserDictionaryEntry
            {
                Reading = entry.Reading.Trim(),
                Word = entry.Word.Trim()
            })
            .ToList();

        await SaveConfigAsync(config => config with
        {
            UserDictionary = config.UserDictionary with
            {
                Entries = trimmedEntries
            }
        });

        LoadFromState();
    }

    private sealed record DictionaryEntryEditor(string Reading, string Word)
    {
        public override string ToString() => $"{Reading} -> {Word}";
    }
}
