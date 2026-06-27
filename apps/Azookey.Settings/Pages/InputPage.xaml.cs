using Azookey.Core.Config;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings.Pages;

public sealed partial class InputPage : SettingsPageBase
{
    public InputPage()
    {
        InitializeComponent();
    }

    protected override void LoadFromState()
    {
        EnsureOptions(punctuationStyleBox,
        [
            CreateOption("、。", PunctuationStyle.ToutenKuten.ToString()),
            CreateOption("，．", PunctuationStyle.FullwidthCommaFullwidthPeriod.ToString()),
            CreateOption("、．", PunctuationStyle.ToutenFullwidthPeriod.ToString()),
            CreateOption("，。", PunctuationStyle.FullwidthCommaKuten.ToString())
        ]);
        EnsureOptions(symbolStyleBox,
        [
            CreateOption("「」・", SymbolStyle.CornerBracketMiddleDot.ToString()),
            CreateOption("[]\\", SymbolStyle.SquareBracketBackslash.ToString()),
            CreateOption("「」\\", SymbolStyle.CornerBracketBackslash.ToString()),
            CreateOption("[]・", SymbolStyle.SquareBracketMiddleDot.ToString())
        ]);
        EnsureOptions(spaceInputBox,
        [
            CreateOption("常に半角", SpaceInputMode.AlwaysHalf.ToString()),
            CreateOption("入力モードに従う", SpaceInputMode.FollowInputMode.ToString())
        ]);
        EnsureOptions(numpadInputBox,
        [
            CreateOption("直接入力", NumpadInputMode.DirectInput.ToString()),
            CreateOption("常に半角", NumpadInputMode.AlwaysHalf.ToString()),
            CreateOption("入力モードに従う", NumpadInputMode.FollowInputMode.ToString())
        ]);

        SelectComboTag(punctuationStyleBox, State.Config.General.PunctuationStyle.ToString());
        SelectComboTag(symbolStyleBox, State.Config.General.SymbolStyle.ToString());
        SelectComboTag(spaceInputBox, State.Config.General.SpaceInput.ToString());
        SelectComboTag(numpadInputBox, State.Config.General.NumpadInput.ToString());
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoading)
        {
            return;
        }

        await SaveConfigAsync(config => config with
        {
            General = config.General with
            {
                PunctuationStyle = GetSelectedEnum(punctuationStyleBox, config.General.PunctuationStyle),
                SymbolStyle = GetSelectedEnum(symbolStyleBox, config.General.SymbolStyle),
                SpaceInput = GetSelectedEnum(spaceInputBox, config.General.SpaceInput),
                NumpadInput = GetSelectedEnum(numpadInputBox, config.General.NumpadInput)
            }
        });
    }
}
