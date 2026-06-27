# Task 4 Report: Rebuild Settings UI Shell And Pages

## Summary

- Replaced the old button-column settings window with a WinUI `NavigationView` shell that hosts an inline `InfoBar` and `Frame`.
- Split the settings UI into dedicated pages:
  - `GeneralPage`
  - `InputPage`
  - `CandidatePage`
  - `ZenzaiPage`
  - `UserDictionaryPage`
  - `DebugPage`
  - `InfoPage`
- Added a local `SettingsRow` control so settings rows follow the project-local SettingsCard-style layout without adding CommunityToolkit.
- Preserved the `frontend` assembly name and kept all edits inside `apps/Azookey.Settings/**`, `apps/Azookey.Settings.Tests/**`, and this report file.

## Baseline RED

Ran before implementation:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
```

Observed failures:

1. `NavigationUsesReadableJapaneseLabels`
2. `SettingsPagesExistAsDedicatedPageFiles`
3. `ZenzaiModelSelectionUsesModelIdNotDisplayText`

I then added/updated tests for the new shell contract (`InfoBar`, `Frame`, `MainWindow.ShowStatus`) and re-ran the suite to confirm RED before production changes.

## What Changed

### Shell

- `MainWindow.xaml`
  - Now uses `NavigationView`, inline `InfoBar`, and `Frame`.
  - Navigation labels are readable Japanese: `一般`, `入力`, `候補`, `Zenzai`, `ユーザー辞書`, `デバッグ`, `情報`.
- `MainWindow.xaml.cs`
  - Added page routing by navigation tag.
  - Added `ShowStatus(string message)` to drive the shell `InfoBar`.
  - Added recovery-status presentation through the shell `InfoBar`.

### Shared page infrastructure

- Added `Controls/SettingsRow.xaml` and `.xaml.cs`.
- Added `Pages/SettingsPageContext.cs`.
- Added `Pages/SettingsPageBase.cs`.
  - Handles navigation context.
  - Centralizes save-status messaging.
  - Centralizes restart-server workflow used by settings pages.

### Dedicated pages

- `GeneralPage`
  - Punctuation commit options
  - Character width groups
  - Shortcut toggles
  - Romaji rule editing
- `InputPage`
  - Punctuation style
  - Symbol style
  - Space input
  - Numpad input
- `CandidatePage`
  - Candidate window after space toggle
- `ZenzaiPage`
  - Enable toggle
  - Real model `ComboBox` populated from `ZenzaiModelCatalog.Options`
  - Backend `ComboBox` exposing only `CPU` and `Vulkan`
  - Profile editor
  - Capability readout
  - Save model/backend from selected item `Tag`
  - Restart request after saved model/backend changes
- `UserDictionaryPage`
  - Add/edit/remove/save entry workflow
- `DebugPage`
  - Server log toggle
  - Log level selector
  - Crash trace toggle
  - Restart server action
- `InfoPage`
  - App name
  - Version
  - Discord link

### ViewModels / status text

- `ViewModels/SettingsPageViewModels.cs`
  - Kept dictionary validation logic in one place.
  - Added `ZenzaiSettingsViewModel` helpers for backend ids and selected model id normalization.
- Status and validation messages now use readable Japanese text, including:
  - `保存しました。`
  - `設定の保存に失敗しました: ...`
  - `サーバーに反映できませんでした: ...`
  - restart-related status messages
  - dictionary validation messages

## Verification

Executed after implementation:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
dotnet build C:/azw-winui/apps/Azookey.Settings/Azookey.Settings.csproj
rg -n "邵ｺ|郢|陷ｿ|闕ｳ|髫ｪ|隴|陞|驍ｱ|鬯|騾|郢ｧ|驛｢|CUDA|cuda" C:/azw-winui/apps/Azookey.Settings C:/azw-winui/apps/Azookey.Settings.Tests
```

Results:

- `dotnet test`: PASS (`20` passed, `0` failed)
- `dotnet build`: PASS (`0` warnings, `0` errors)
- `rg` scan: no matches

## Notes

- The restart helper moved from the old monolithic window into `SettingsPageBase`; I updated the source-based test to assert the restart contract instead of the old file-local placement.
- Build outputs under `bin/` and `obj/` were produced by verification commands and were not intended for commit.

## Fix Pass: Shortcut Labels And Source Coverage

Updated after review findings:

- `apps/Azookey.Settings/Pages/GeneralPage.xaml`
  - Replaced visible shortcut headers with Japanese labels:
    - `コントロール + スペース`
    - `オルト + バッククォート`
    - `英数キー`
- `apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs`
  - Added a regression test for the General page shortcut labels.
  - Broadened `GetCombinedSettingsSource()` to scan the full `apps/Azookey.Settings` tree for `.xaml` and `.cs`, while excluding `bin/` and `obj/`.
  - Added an assertion that the Zenzai model selector uses `model.DisplayName` in visible option text.

### Verification

Commands run:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
dotnet build C:/azw-winui/apps/Azookey.Settings/Azookey.Settings.csproj
rg -n "邵ｺ|郢|陷ｿ|闕ｳ|髫ｪ|隴|陞|驍ｱ|鬯|騾|郢ｧ|驛｢|荳|蜈|蛟|繝|諠|險|螳|菫|霎|譁|縺|蜉|鬆|闍|謨|逕|螟|肴|丐|轤|繧|蟄|邱|髮|蛻|閭|CUDA|cuda" C:/azw-winui/apps/Azookey.Settings C:/azw-winui/apps/Azookey.Settings.Tests
```

Results:

- `dotnet test`: PASS (`21` passed, `0` failed)
- `dotnet build`: PASS (`0` warnings, `0` errors)
- `rg` scan: no matches
