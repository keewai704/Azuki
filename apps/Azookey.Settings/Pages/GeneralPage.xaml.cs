using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Azookey.Core.Config;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings.Pages;

public sealed partial class GeneralPage : SettingsPageBase
{
    private readonly ObservableCollection<RomajiRuleEditor> romajiRows = [];

    public GeneralPage()
    {
        InitializeComponent();
        romajiRowsList.ItemsSource = romajiRows;
    }

    protected override void LoadFromState()
    {
        EnsureWidthOptions();

        punctuationCommitSwitch.IsOn = State.Config.General.PunctuationCommit;
        punctuationCommitPunctuationSwitch.IsOn = State.Config.General.PunctuationCommitPunctuation;
        punctuationCommitExclamationSwitch.IsOn = State.Config.General.PunctuationCommitExclamation;
        punctuationCommitQuestionSwitch.IsOn = State.Config.General.PunctuationCommitQuestion;
        UpdatePunctuationCommitChildren();

        SelectComboTag(alphabetWidthBox, State.Config.CharacterWidth.Groups.Alphabet.ToString());
        SelectComboTag(numberWidthBox, State.Config.CharacterWidth.Groups.Number.ToString());
        SelectComboTag(bracketWidthBox, State.Config.CharacterWidth.Groups.Bracket.ToString());
        SelectComboTag(commaPeriodWidthBox, State.Config.CharacterWidth.Groups.CommaPeriod.ToString());
        SelectComboTag(middleDotCornerBracketWidthBox, State.Config.CharacterWidth.Groups.MiddleDotCornerBracket.ToString());
        SelectComboTag(quoteWidthBox, State.Config.CharacterWidth.Groups.Quote.ToString());
        SelectComboTag(colonSemicolonWidthBox, State.Config.CharacterWidth.Groups.ColonSemicolon.ToString());
        SelectComboTag(hashGroupWidthBox, State.Config.CharacterWidth.Groups.HashGroup.ToString());
        SelectComboTag(tildeWidthBox, State.Config.CharacterWidth.Groups.Tilde.ToString());
        SelectComboTag(mathSymbolWidthBox, State.Config.CharacterWidth.Groups.MathSymbol.ToString());
        SelectComboTag(questionExclamationWidthBox, State.Config.CharacterWidth.Groups.QuestionExclamation.ToString());

        ctrlSpaceToggleSwitch.IsOn = State.Config.Shortcuts.CtrlSpaceToggle;
        altBackquoteToggleSwitch.IsOn = State.Config.Shortcuts.AltBackquoteToggle;
        eisuToggleSwitch.IsOn = State.Config.Shortcuts.EisuToggle;

        romajiRows.Clear();
        foreach (RomajiRule row in State.Config.RomajiTable.Rows)
        {
            romajiRows.Add(new RomajiRuleEditor(row.Input, row.Output, row.NextInput));
        }
    }

    private async void OnCommitSettingChanged(object sender, RoutedEventArgs e)
    {
        UpdatePunctuationCommitChildren();
        await SaveCommitSettingsAsync();
    }

    private async void OnCharacterWidthSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveCharacterWidthAsync();
    }

    private async void OnShortcutToggled(object sender, RoutedEventArgs e)
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            Shortcuts = config.Shortcuts with
            {
                CtrlSpaceToggle = ctrlSpaceToggleSwitch.IsOn,
                AltBackquoteToggle = altBackquoteToggleSwitch.IsOn,
                EisuToggle = eisuToggleSwitch.IsOn
            }
        });
    }

    private async void OnAddRomajiRuleClick(object sender, RoutedEventArgs e)
    {
        RomajiRuleEditor? rule = await ShowRomajiRuleDialogAsync(new RomajiRuleEditor("", "", ""));
        if (rule is null)
        {
            return;
        }

        romajiRows.Add(rule);
        await SaveRomajiRowsAsync();
    }

    private async void OnEditRomajiRuleClick(object sender, RoutedEventArgs e)
    {
        if (romajiRowsList.SelectedItem is not RomajiRuleEditor selected)
        {
            ShowStatus("編集するルールを選択してください。");
            return;
        }

        RomajiRuleEditor? updated = await ShowRomajiRuleDialogAsync(selected);
        if (updated is null)
        {
            return;
        }

        int index = romajiRows.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        romajiRows[index] = updated;
        await SaveRomajiRowsAsync();
    }

    private async void OnRemoveRomajiRuleClick(object sender, RoutedEventArgs e)
    {
        if (romajiRowsList.SelectedItem is not RomajiRuleEditor selected)
        {
            ShowStatus("削除するルールを選択してください。");
            return;
        }

        romajiRows.Remove(selected);
        await SaveRomajiRowsAsync();
    }

    private async Task SaveCommitSettingsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            General = config.General with
            {
                PunctuationCommit = punctuationCommitSwitch.IsOn,
                PunctuationCommitPunctuation = punctuationCommitPunctuationSwitch.IsOn,
                PunctuationCommitExclamation = punctuationCommitExclamationSwitch.IsOn,
                PunctuationCommitQuestion = punctuationCommitQuestionSwitch.IsOn
            }
        });
    }

    private async Task SaveCharacterWidthAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            CharacterWidth = config.CharacterWidth with
            {
                Groups = new CharacterWidthGroups
                {
                    Alphabet = GetSelectedEnum(alphabetWidthBox, config.CharacterWidth.Groups.Alphabet),
                    Number = GetSelectedEnum(numberWidthBox, config.CharacterWidth.Groups.Number),
                    Bracket = GetSelectedEnum(bracketWidthBox, config.CharacterWidth.Groups.Bracket),
                    CommaPeriod = GetSelectedEnum(commaPeriodWidthBox, config.CharacterWidth.Groups.CommaPeriod),
                    MiddleDotCornerBracket = GetSelectedEnum(middleDotCornerBracketWidthBox, config.CharacterWidth.Groups.MiddleDotCornerBracket),
                    Quote = GetSelectedEnum(quoteWidthBox, config.CharacterWidth.Groups.Quote),
                    ColonSemicolon = GetSelectedEnum(colonSemicolonWidthBox, config.CharacterWidth.Groups.ColonSemicolon),
                    HashGroup = GetSelectedEnum(hashGroupWidthBox, config.CharacterWidth.Groups.HashGroup),
                    Tilde = GetSelectedEnum(tildeWidthBox, config.CharacterWidth.Groups.Tilde),
                    MathSymbol = GetSelectedEnum(mathSymbolWidthBox, config.CharacterWidth.Groups.MathSymbol),
                    QuestionExclamation = GetSelectedEnum(questionExclamationWidthBox, config.CharacterWidth.Groups.QuestionExclamation)
                }
            }
        });
    }

    private async Task SaveRomajiRowsAsync()
    {
        await SaveConfigAsync(config => config with
        {
            RomajiTable = config.RomajiTable with
            {
                Rows = romajiRows
                    .Select(row => new RomajiRule
                    {
                        Input = row.Input,
                        Output = row.Output,
                        NextInput = row.NextInput
                    })
                    .ToList()
            }
        });
    }

    private async Task<RomajiRuleEditor?> ShowRomajiRuleDialogAsync(RomajiRuleEditor initial)
    {
        TextBox inputBox = new() { Header = "入力", Text = initial.Input };
        TextBox outputBox = new() { Header = "出力", Text = initial.Output };
        TextBox nextInputBox = new() { Header = "次の入力", Text = initial.NextInput };
        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(inputBox);
        panel.Children.Add(outputBox);
        panel.Children.Add(nextInputBox);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "ローマ字ルール",
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? new RomajiRuleEditor(inputBox.Text, outputBox.Text, nextInputBox.Text)
            : null;
    }

    private void EnsureWidthOptions()
    {
        foreach (ComboBox comboBox in new[]
        {
            alphabetWidthBox,
            numberWidthBox,
            bracketWidthBox,
            commaPeriodWidthBox,
            middleDotCornerBracketWidthBox,
            quoteWidthBox,
            colonSemicolonWidthBox,
            hashGroupWidthBox,
            tildeWidthBox,
            mathSymbolWidthBox,
            questionExclamationWidthBox
        })
        {
            EnsureOptions(comboBox,
            [
                CreateOption("半角", WidthMode.Half.ToString()),
                CreateOption("全角", WidthMode.Full.ToString())
            ]);
        }
    }

    private void UpdatePunctuationCommitChildren()
    {
        bool enabled = punctuationCommitSwitch.IsOn;
        punctuationCommitPunctuationSwitch.IsEnabled = enabled;
        punctuationCommitExclamationSwitch.IsEnabled = enabled;
        punctuationCommitQuestionSwitch.IsEnabled = enabled;
    }

    private sealed record RomajiRuleEditor(string Input, string Output, string NextInput)
    {
        public override string ToString()
        {
            return string.IsNullOrEmpty(NextInput)
                ? $"{Input} -> {Output}"
                : $"{Input} -> {Output} / {NextInput}";
        }
    }
}
