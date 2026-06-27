# WinUI Settings Candidate Zenzai Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the WinUI settings surface to follow current Microsoft settings guidance, restore reliable candidate-window display, remove CUDA backend support, and make Zenzai model selection persist and restart the server.

**Architecture:** Keep the existing C# WinUI apps and Rust/Swift backend contracts. Replace procedural settings page construction with a `NavigationView` shell plus focused page classes and shared view-model helpers. Fix candidate UI delivery at the Rust IPC client boundary by adding a bounded UI named-pipe retry window. Remove CUDA from config normalization, settings controls, launcher/backend mapping, installer/build scripts, and tests.

**Tech Stack:** C# 13, .NET 10, WinUI 3, Windows App SDK 2.2.0, xUnit, Rust, cargo test, Visual Studio Build Tools 2026/MSBuild.

## Global Constraints

- Worktree root is `C:/azw-winui`.
- Preserve executable names `ui.exe` and `frontend.exe`.
- Preserve named pipes `azookey_ui`, `azookey_server`, and `azookey_launcher`.
- Do not change `crates/shared/window.proto` or `crates/shared/service.proto`.
- Do not reintroduce live-conversion reading display controls.
- Do not expose or package CUDA support.
- Use TDD: every production behavior change starts with a failing test.
- Keep settings text Japanese-only and avoid mojibake markers such as `縺`, `繝`, `蜿`, `荳`, `險`, `譁`, `螟`, `邱`, `鬆`, `逕`, `繧`, and `郢`.

---

## File Structure

- Modify `apps/Azookey.Settings/MainWindow.xaml`: replace manual two-column grid with a WinUI `NavigationView`, status `InfoBar`, and `Frame`.
- Modify `apps/Azookey.Settings/MainWindow.xaml.cs`: reduce to shell navigation and state/status wiring.
- Create `apps/Azookey.Settings/Controls/SettingsRow.xaml` and `.cs`: local SettingsCard-style row if Community Toolkit is not added.
- Create `apps/Azookey.Settings/Pages/*.xaml` and `.cs`: `GeneralPage`, `InputPage`, `CandidatePage`, `ZenzaiPage`, `DictionaryPage`, `DebugPage`, `AboutPage`.
- Modify `apps/Azookey.Settings/ViewModels/SettingsPageViewModels.cs`: add Japanese validation messages and Zenzai selection helpers.
- Modify `apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs`: replace mojibake-positive assertions with Japanese/no-mojibake/page-structure assertions.
- Modify `apps/Azookey.Settings.Tests/ViewModels/SettingsPageViewModelTests.cs`: add model-selection and validation tests.
- Modify `apps/Azookey.Core/Config/AppConfig.cs`: normalize legacy CUDA backend to Vulkan.
- Modify `apps/Azookey.Core.Tests/Config/AppConfigTests.cs`: add CUDA migration test.
- Modify `apps/Azookey.Core/Process/ServerRestartService.cs`: remove `llama_cuda` mapping.
- Modify `apps/Azookey.Core.Tests/Process/ServerRestartServiceTests.cs`: update backend mapping tests.
- Modify `crates/launcher/src/main.rs`: remove `cuda -> llama_cuda` mapping and add bounded UI pipe timeout tests.
- Modify `crates/client/src/engine/ipc_service.rs`: make `UI_PIPE_BUSY_TIMEOUT` non-zero and testable.
- Modify `Makefile.toml`, `installer/Installer.iss`, `scripts/vm_build.sh`, `scripts/vm_stage_for_manual_test.sh`, `.github/workflows/actions.yml`: remove `llama_cuda` references.

---

### Task 1: Failing Tests For UI Text, CUDA Removal, Model Selection, And Candidate IPC

**Files:**
- Modify: `apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs`
- Modify: `apps/Azookey.Settings.Tests/ViewModels/SettingsPageViewModelTests.cs`
- Modify: `apps/Azookey.Core.Tests/Config/AppConfigTests.cs`
- Modify: `apps/Azookey.Core.Tests/Process/ServerRestartServiceTests.cs`
- Modify: `crates/client/src/engine/ipc_service.rs`
- Modify: `crates/launcher/src/main.rs`

**Interfaces:**
- Produces tests that fail on current mojibake UI, CUDA exposure, `llama_cuda` mapping, and zero UI pipe timeout.

- [ ] **Step 1: Replace settings text tests**

In `SettingsWindowTextTests.cs`, assert:

```csharp
Assert.Contains("<NavigationView", xaml);
Assert.Contains("Content=\"一般\"", xaml);
Assert.Contains("Content=\"入力\"", xaml);
Assert.Contains("Content=\"候補\"", xaml);
Assert.Contains("Content=\"Zenzai\"", xaml);
Assert.Contains("Content=\"ユーザー辞書\"", xaml);
Assert.Contains("Content=\"デバッグ\"", xaml);
Assert.Contains("Content=\"情報\"", xaml);

foreach (string marker in MojibakeMarkers)
{
    Assert.DoesNotContain(marker, xaml);
    Assert.DoesNotContain(marker, source);
}
Assert.DoesNotContain("CUDA", source, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("llama_cuda", source, StringComparison.OrdinalIgnoreCase);
```

Add page-file existence assertions for all pages under `apps/Azookey.Settings/Pages`.

- [ ] **Step 2: Add Zenzai selection helper tests**

In `SettingsPageViewModelTests.cs`, add:

```csharp
[Fact]
public void ZenzaiModelSelectionUsesModelIdNotDisplayText()
{
    string selected = ZenzaiSettingsViewModel.ResolveSelectedModelId("zenz-v3.1-small-q5-k-m");
    Assert.Equal("zenz-v3.1-small-q5-k-m", selected);
}

[Fact]
public void ZenzaiBackendOptionsExcludeCuda()
{
    Assert.Equal(["cpu", "vulkan"], ZenzaiSettingsViewModel.BackendIds);
}
```

- [ ] **Step 3: Add core CUDA migration tests**

In `AppConfigTests.cs`, add:

```csharp
[Fact]
public void DeserializeMigratesLegacyCudaBackendToVulkan()
{
    const string json = """
    {
      "version": "0.1.3",
      "zenzai": { "enable": true, "profile": "", "backend": "cuda" }
    }
    """;

    AppConfig config = AppConfig.Deserialize(json);

    Assert.Equal("vulkan", config.Zenzai.Backend);
}
```

- [ ] **Step 4: Add backend directory tests**

In `ServerRestartServiceTests.cs`, assert:

```csharp
Assert.Equal("llama_cpu", ServerRestartService.BackendDirectory("cpu"));
Assert.Equal("llama_vulkan", ServerRestartService.BackendDirectory("vulkan"));
Assert.Equal("llama_vulkan", ServerRestartService.BackendDirectory("cuda"));
Assert.DoesNotContain("llama_cuda", source);
```

- [ ] **Step 5: Add Rust UI pipe timeout test**

Expose a test-only helper in `ipc_service.rs`:

```rust
#[cfg(test)]
fn ui_pipe_busy_timeout() -> Duration {
    UI_PIPE_BUSY_TIMEOUT
}
```

Add a test:

```rust
#[test]
fn ui_pipe_busy_timeout_is_bounded_and_non_zero() {
    let timeout = ui_pipe_busy_timeout();
    assert!(timeout >= Duration::from_millis(250));
    assert!(timeout <= Duration::from_secs(2));
}
```

- [ ] **Step 6: Verify RED**

Run:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj --filter "SettingsWindowTextTests|SettingsPageViewModelTests"
dotnet test C:/azw-winui/apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "AppConfigTests|ServerRestartServiceTests"
cargo test -p frontend ui_pipe_busy_timeout_is_bounded_and_non_zero
cargo test -p launcher backend_dir
```

Expected: tests fail because pages do not exist, UI text is mojibake, CUDA is still exposed, and UI timeout is zero.

- [ ] **Step 7: Commit RED tests**

```powershell
git add apps/Azookey.Settings.Tests apps/Azookey.Core.Tests crates/client/src/engine/ipc_service.rs crates/launcher/src/main.rs
git commit -m "test: cover settings candidate zenzai regressions"
```

---

### Task 2: Remove CUDA From Config, Launcher, Build, Installer, VM, And CI

**Files:**
- Modify: `apps/Azookey.Core/Config/AppConfig.cs`
- Modify: `apps/Azookey.Core/Process/ServerRestartService.cs`
- Modify: `crates/launcher/src/main.rs`
- Modify: `frontend/src-tauri/src/server_process.rs`
- Modify: `Makefile.toml`
- Modify: `installer/Installer.iss`
- Modify: `scripts/vm_build.sh`
- Modify: `scripts/vm_stage_for_manual_test.sh`
- Modify: `.github/workflows/actions.yml`

**Interfaces:**
- Produces `NormalizeZenzai` behavior that writes legacy CUDA as Vulkan.
- Produces backend directory mapping limited to `llama_cpu` and `llama_vulkan`.

- [ ] **Step 1: Implement C# backend normalization**

Update `NormalizeZenzai`:

```csharp
private static ZenzaiConfig NormalizeZenzai(ZenzaiConfig zenzai) =>
    zenzai with
    {
        Backend = NormalizeZenzaiBackend(zenzai.Backend),
        ModelId = ZenzaiModelCatalog.ResolveModelId(zenzai.ModelId)
    };

public static string NormalizeZenzaiBackend(string? backend) =>
    string.Equals(backend, "vulkan", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(backend, "cuda", StringComparison.OrdinalIgnoreCase)
        ? "vulkan"
        : "cpu";
```

- [ ] **Step 2: Remove C# `llama_cuda` mapping**

Update `ServerRestartService.BackendDirectory`:

```csharp
internal static string BackendDirectory(string backend) =>
    string.Equals(backend, "vulkan", StringComparison.OrdinalIgnoreCase)
        || string.Equals(backend, "cuda", StringComparison.OrdinalIgnoreCase)
            ? "llama_vulkan"
            : "llama_cpu";
```

- [ ] **Step 3: Remove Rust launcher `llama_cuda` mapping**

Update `backend_dir`:

```rust
fn backend_dir(config: &AppConfig) -> &'static str {
    match config.zenzai.backend.as_str() {
        "vulkan" | "cuda" => "llama_vulkan",
        _ => "llama_cpu",
    }
}
```

- [ ] **Step 4: Remove script and installer references**

Delete only the lines that copy, cache, stage, or delete `llama_cuda`:

```text
cp -Recurse -Force llama_cuda build
Type: filesandordirs; Name: "{app}\llama_cuda"
llama_cuda
```

Keep `llama_cpu` and `llama_vulkan`.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "AppConfigTests|ServerRestartServiceTests"
cargo test -p launcher backend_dir
rg -n "llama_cuda|CUDA \\(|cudart64_12|cublas64_12" C:/azw-winui/Makefile.toml C:/azw-winui/installer C:/azw-winui/scripts C:/azw-winui/.github C:/azw-winui/apps C:/azw-winui/crates
```

Expected: tests pass; `rg` finds no active packaging/UI references. Historical docs may still mention CUDA only if outside the checked paths.

- [ ] **Step 6: Commit**

```powershell
git add apps/Azookey.Core apps/Azookey.Core.Tests crates/launcher frontend/src-tauri Makefile.toml installer scripts .github
git commit -m "fix: remove cuda backend support"
```

---

### Task 3: Fix Candidate UI Named Pipe Reliability

**Files:**
- Modify: `crates/client/src/engine/ipc_service.rs`

**Interfaces:**
- Produces bounded UI named-pipe retry behavior while preserving non-fatal UI failure.

- [ ] **Step 1: Set bounded timeout**

Change:

```rust
const UI_PIPE_BUSY_TIMEOUT: Duration = Duration::ZERO;
```

to:

```rust
const UI_PIPE_BUSY_TIMEOUT: Duration = Duration::from_millis(500);
```

- [ ] **Step 2: Keep deferred reconnect**

Do not change `ensure_window_client` semantics except that it now uses the new timeout. It should still return `None` and log debug when UI is unavailable.

- [ ] **Step 3: Verify GREEN**

Run:

```powershell
cargo test -p frontend ui_pipe_busy_timeout_is_bounded_and_non_zero
cargo test -p frontend reconnect_retry_is_enabled_for_transport_like_status
```

Expected: tests pass.

- [ ] **Step 4: Commit**

```powershell
git add crates/client/src/engine/ipc_service.rs
git commit -m "fix: wait briefly for candidate ui pipe"
```

---

### Task 4: Rebuild Settings UI Shell And Pages

**Files:**
- Modify: `apps/Azookey.Settings/MainWindow.xaml`
- Modify: `apps/Azookey.Settings/MainWindow.xaml.cs`
- Create: `apps/Azookey.Settings/Controls/SettingsRow.xaml`
- Create: `apps/Azookey.Settings/Controls/SettingsRow.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/GeneralPage.xaml`
- Create: `apps/Azookey.Settings/Pages/GeneralPage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/InputPage.xaml`
- Create: `apps/Azookey.Settings/Pages/InputPage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/CandidatePage.xaml`
- Create: `apps/Azookey.Settings/Pages/CandidatePage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/ZenzaiPage.xaml`
- Create: `apps/Azookey.Settings/Pages/ZenzaiPage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/DictionaryPage.xaml`
- Create: `apps/Azookey.Settings/Pages/DictionaryPage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/DebugPage.xaml`
- Create: `apps/Azookey.Settings/Pages/DebugPage.xaml.cs`
- Create: `apps/Azookey.Settings/Pages/AboutPage.xaml`
- Create: `apps/Azookey.Settings/Pages/AboutPage.xaml.cs`
- Modify: `apps/Azookey.Settings/ViewModels/SettingsPageViewModels.cs`

**Interfaces:**
- Produces `MainWindow.ShowStatus(string message)`.
- Produces page constructors accepting `(SettingsAppState state, MainWindow shell)`.
- Produces `SettingsRow` with dependency properties `Icon`, `Header`, `Description`, and `ActionContent`.

- [ ] **Step 1: Build `NavigationView` shell**

`MainWindow.xaml` uses:

```xml
<NavigationView x:Name="RootNavigation"
                PaneDisplayMode="Auto"
                IsBackButtonVisible="Collapsed"
                IsSettingsVisible="False"
                SelectionChanged="OnNavigationSelectionChanged">
  <NavigationView.MenuItems>
    <NavigationViewItem Content="一般" Tag="general" Icon="Home" />
    <NavigationViewItem Content="入力" Tag="input" Icon="Keyboard" />
    <NavigationViewItem Content="候補" Tag="candidate" Icon="List" />
    <NavigationViewItem Content="Zenzai" Tag="zenzai" Icon="Robot" />
    <NavigationViewItem Content="ユーザー辞書" Tag="dictionary" Icon="Edit" />
    <NavigationViewItem Content="デバッグ" Tag="debug" Icon="Repair" />
    <NavigationViewItem Content="情報" Tag="about" Icon="Help" />
  </NavigationView.MenuItems>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <InfoBar x:Name="StatusInfoBar" IsOpen="False" Severity="Informational" />
    <Frame x:Name="ContentFrame" Grid.Row="1" />
  </Grid>
</NavigationView>
```

- [ ] **Step 2: Add shell navigation code**

`MainWindow.xaml.cs` maps tags to page types:

```csharp
private static readonly IReadOnlyDictionary<string, Type> Pages = new Dictionary<string, Type>
{
    ["general"] = typeof(GeneralPage),
    ["input"] = typeof(InputPage),
    ["candidate"] = typeof(CandidatePage),
    ["zenzai"] = typeof(ZenzaiPage),
    ["dictionary"] = typeof(DictionaryPage),
    ["debug"] = typeof(DebugPage),
    ["about"] = typeof(AboutPage),
};
```

Use `ContentFrame.Navigate(pageType, new SettingsPageContext(State, this));`.

- [ ] **Step 3: Add local `SettingsRow`**

`SettingsRow` is a `UserControl` with:

```xml
<Border CornerRadius="8" Padding="16" BorderThickness="1">
  <Grid ColumnSpacing="16">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="32" />
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <FontIcon x:Name="IconPresenter" />
    <StackPanel Grid.Column="1" Spacing="4">
      <TextBlock x:Name="HeaderText" Style="{ThemeResource BodyStrongTextBlockStyle}" />
      <TextBlock x:Name="DescriptionText" TextWrapping="Wrap" Opacity="0.75" />
    </StackPanel>
    <ContentPresenter x:Name="ActionPresenter" Grid.Column="2" VerticalAlignment="Center" />
  </Grid>
</Border>
```

- [ ] **Step 4: Move existing setting behavior into pages**

Port current controls, with Japanese labels:

```text
General: punctuation commit, shortcuts, width groups, romaji rules.
Input: punctuation style, symbol style, space input, numpad input.
Candidate: show candidate window after space.
Zenzai: enable, model selector, backend selector CPU/Vulkan, profile.
Dictionary: add/edit/remove/save entries.
Debug: server log enabled, log level, crash trace, restart server.
About: app name, version, Discord link.
```

Do not add reading display controls.

- [ ] **Step 5: Implement Zenzai page without CUDA**

Backend selector values:

```csharp
ZenzaiSettingsViewModel.BackendIds.Select(id => new ComboBoxItem
{
    Content = id == "cpu" ? "CPU" : "Vulkan",
    Tag = id
});
```

Model selector values:

```csharp
ZenzaiModelCatalog.Options.Select(model => new ComboBoxItem
{
    Content = model.DisplayName,
    Tag = model.Id
});
```

On change, save `ModelId` from `Tag`, then restart server when model/backend changed.

- [ ] **Step 6: Verify settings tests and build**

Run:

```powershell
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
dotnet build C:/azw-winui/apps/Azookey.Settings/Azookey.Settings.csproj
```

Expected: tests pass and app builds.

- [ ] **Step 7: Commit**

```powershell
git add apps/Azookey.Settings apps/Azookey.Settings.Tests
git commit -m "feat: rebuild winui settings pages"
```

---

### Task 5: Verification, Build, And Push

**Files:**
- No planned source edits unless verification reveals issues.

**Interfaces:**
- Produces a pushed branch on `keewai704/Azuki-Win`.

- [ ] **Step 1: Run C# tests**

```powershell
dotnet test C:/azw-winui/apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj
dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
dotnet test C:/azw-winui/apps/Azookey.UI.Tests/Azookey.UI.Tests.csproj
```

- [ ] **Step 2: Run Rust tests**

```powershell
cargo test -p shared --lib
cargo test -p launcher
cargo test -p frontend --lib
```

- [ ] **Step 3: Build with available toolchain**

Prefer:

```powershell
cargo make build --release
```

If full installer build is blocked by local environment, run:

```powershell
dotnet build C:/azw-winui/apps/Azookey.WinUI.sln
```

and report the blocker.

- [ ] **Step 4: Push**

```powershell
git status --short
git push -u keewai fix/winui-settings-candidate-zenzai
```

Expected: branch is available on `keewai704/Azuki-Win`.

---

## Self Review

- Spec coverage: settings redesign, Japanese text, candidate UI reliability, CUDA removal, Zenzai model selection, tests, build, and push are covered.
- Placeholder scan: no unfinished placeholder instructions remain.
- Type consistency: `SettingsPageContext`, `SettingsRow`, `ZenzaiSettingsViewModel`, `NormalizeZenzaiBackend`, and `BackendDirectory` are introduced before subsequent tasks consume them.
