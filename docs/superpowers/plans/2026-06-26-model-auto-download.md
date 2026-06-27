# GGUF Model Auto Download Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove bundled `zenz.gguf`, download the selected GGUF model on first app startup, and expose model selection in WinUI settings.

**Architecture:** Rust `shared` owns the Zenzai model catalog and config defaults. The Rust launcher ensures the selected model exists under `%APPDATA%\Azookey\models`, verifies SHA-256, and passes `AZOOKEY_ZENZAI_MODEL_PATH` to the Swift server. C# settings mirrors the catalog for the UI and preserves `zenzai.model_id` in `settings.json`.

**Tech Stack:** Rust 2021, `reqwest` blocking client, `sha2`, Swift 6.1, .NET 10 WinUI 3, xUnit, Inno Setup, cargo-make.

## Global Constraints

- Worktree: `C:\azw-winui`
- Branch: `codex/model-auto-download`
- Default model id: `zenz-v3.2-small-q5-k-m`
- Default model repository: `Miwa-Keita/zenz-v3.2-small-gguf`
- Model storage root: `%APPDATA%\Azookey\models\<model_id>\<filename>`
- Download timing: app startup when the selected model file is missing, regardless of `zenzai.enable`
- Download failure: do not block normal IME startup
- Model source: built-in catalog only, no custom URL
- Production code rule: write and run a failing test before each behavior change
- Push target after implementation: `keewai/codex/model-auto-download`

---

## File Structure

- Create `crates/shared/src/zenzai_models.rs`: Rust model catalog, default id, path helpers, and model id resolution.
- Modify `crates/shared/src/lib.rs`: export catalog, add `zenzai.model_id`, bump config version, expose config root.
- Create `apps/Azookey.Core/Config/ZenzaiModelCatalog.cs`: C# model catalog for settings and direct server restart fallback.
- Modify `apps/Azookey.Core/Config/AppConfig.cs`: add `ZenzaiConfig.ModelId`, normalize missing and unknown ids.
- Modify `apps/Azookey.Settings/MainWindow.xaml.cs`: add WinUI model ComboBox and save `ModelId`.
- Create `crates/launcher/src/zenzai_model_download.rs`: verified model download and atomic promotion.
- Modify `crates/launcher/src/main.rs`: call model ensure during startup and pass `AZOOKEY_ZENZAI_MODEL_PATH`.
- Modify `crates/launcher/Cargo.toml`: add `reqwest`, `sha2`, and `tempfile` test dependency.
- Modify `server-swift/Sources/azookey-server/azookey_server.swift`: resolve Zenzai weight URL from environment.
- Modify `frontend/src-tauri/src/server_process.rs`: preserve model path env when direct-starting the server.
- Modify `Makefile.toml`: stop copying repository-root `zenz.gguf`.
- Modify `installer/Installer.iss`: delete old `{app}\zenz.gguf` during upgrade.
- Modify tests in `apps/Azookey.Core.Tests`, `apps/Azookey.Settings.Tests`, `crates/shared`, `crates/launcher`, and `server-swift/Tests`.

## Model Catalog Constants

Use these exact entries in Rust and C#:

| id | display name | repository | filename | size | sha256 |
| --- | --- | --- | --- | --- | --- |
| `zenz-v3.2-small-q5-k-m` | `Zenz v3.2 small (Q5_K_M)` | `Miwa-Keita/zenz-v3.2-small-gguf` | `ggml-model-Q5_K_M.gguf` | `73871936` | `29c223d4c23327b80fd13ebb5ab2555057a46317997d5da391584ffbef0db673` |
| `zenz-v3.1-small-q5-k-m` | `Zenz v3.1 small (Q5_K_M)` | `Miwa-Keita/zenz-v3.1-small-gguf` | `ggml-model-Q5_K_M.gguf` | `73871968` | `4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c` |
| `zenz-v3-small-q5-k-m` | `Zenz v3 small (Q5_K_M)` | `Miwa-Keita/zenz-v3-small-gguf` | `ggml-model-Q5_K_M.gguf` | `72298816` | `501f605d088f5b988791a00ae19ed46985ed7c48144f364b2f3f1f951c9b2083` |
| `zenz-v2-q5-k-m` | `Zenz v2 (Q5_K_M)` | `Miwa-Keita/zenz-v2-gguf` | `zenz-v2-Q5_K_M.gguf` | `72298816` | `22b8d8190bba8c9fec075ffb5b323b0f0d65c7c5f5ff82011799a0c3049d9662` |

### Task 1: Rust Shared Catalog And Config

**Files:**
- Create: `crates/shared/src/zenzai_models.rs`
- Modify: `crates/shared/src/lib.rs`
- Test: `crates/shared/src/lib.rs`

**Interfaces:**
- Produces: `shared::zenzai_models::DEFAULT_ZENZAI_MODEL_ID: &str`
- Produces: `shared::zenzai_models::ZenzaiModel`
- Produces: `shared::zenzai_models::available_models() -> &'static [ZenzaiModel]`
- Produces: `shared::zenzai_models::resolve_model(id: &str) -> &'static ZenzaiModel`
- Produces: `shared::zenzai_models::model_path(config_root: &Path, model: &ZenzaiModel) -> PathBuf`
- Produces: `shared::config_root() -> Result<PathBuf, ConfigError>`
- Updates: `ZenzaiConfig { enable, profile, backend, model_id }`

- [ ] **Step 1: Write failing shared tests**

Extend the existing `use super::{ ... }` list in `crates/shared/src/lib.rs` tests with `zenzai_models`, then add these tests inside the existing `#[cfg(test)] mod tests`:

```rust
#[test]
fn default_config_includes_default_zenzai_model_id() {
    let config = AppConfig::default();

    assert_eq!(
        config.zenzai.model_id,
        zenzai_models::DEFAULT_ZENZAI_MODEL_ID
    );
}

#[test]
fn missing_zenzai_model_id_uses_default() {
    let json = r#"{
        "version": "0.1.2",
        "zenzai": { "enable": false, "profile": "", "backend": "cpu" }
    }"#;

    let config: AppConfig = serde_json::from_str(json).unwrap();

    assert_eq!(
        config.zenzai.model_id,
        zenzai_models::DEFAULT_ZENZAI_MODEL_ID
    );
}

#[test]
fn unknown_zenzai_model_id_resolves_to_default_catalog_entry() {
    let model = zenzai_models::resolve_model("missing-model");

    assert_eq!(model.id, zenzai_models::DEFAULT_ZENZAI_MODEL_ID);
}

#[test]
fn zenzai_model_path_uses_appdata_models_directory() {
    let root = Path::new(r"C:\Users\Test\AppData\Roaming\Azookey");
    let model = zenzai_models::resolve_model(zenzai_models::DEFAULT_ZENZAI_MODEL_ID);

    assert_eq!(
        zenzai_models::model_path(root, model),
        root.join("models")
            .join("zenz-v3.2-small-q5-k-m")
            .join("ggml-model-Q5_K_M.gguf")
    );
}
```

- [ ] **Step 2: Run shared tests to verify RED**

Run:

```powershell
cargo test -p shared zenzai_model --lib
```

Expected: FAIL because `zenzai_models` and `ZenzaiConfig.model_id` do not exist.

- [ ] **Step 3: Implement the shared catalog**

Create `crates/shared/src/zenzai_models.rs`:

```rust
use std::path::{Path, PathBuf};

pub const DEFAULT_ZENZAI_MODEL_ID: &str = "zenz-v3.2-small-q5-k-m";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ZenzaiModel {
    pub id: &'static str,
    pub display_name: &'static str,
    pub repository: &'static str,
    pub filename: &'static str,
    pub url: &'static str,
    pub expected_size_bytes: u64,
    pub sha256: &'static str,
}

const MODELS: [ZenzaiModel; 4] = [
    ZenzaiModel {
        id: "zenz-v3.2-small-q5-k-m",
        display_name: "Zenz v3.2 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3.2-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3.2-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 73_871_936,
        sha256: "29c223d4c23327b80fd13ebb5ab2555057a46317997d5da391584ffbef0db673",
    },
    ZenzaiModel {
        id: "zenz-v3.1-small-q5-k-m",
        display_name: "Zenz v3.1 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3.1-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3.1-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 73_871_968,
        sha256: "4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c",
    },
    ZenzaiModel {
        id: "zenz-v3-small-q5-k-m",
        display_name: "Zenz v3 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 72_298_816,
        sha256: "501f605d088f5b988791a00ae19ed46985ed7c48144f364b2f3f1f951c9b2083",
    },
    ZenzaiModel {
        id: "zenz-v2-q5-k-m",
        display_name: "Zenz v2 (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v2-gguf",
        filename: "zenz-v2-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v2-gguf/resolve/main/zenz-v2-Q5_K_M.gguf",
        expected_size_bytes: 72_298_816,
        sha256: "22b8d8190bba8c9fec075ffb5b323b0f0d65c7c5f5ff82011799a0c3049d9662",
    },
];

pub fn available_models() -> &'static [ZenzaiModel] {
    &MODELS
}

pub fn resolve_model(id: &str) -> &'static ZenzaiModel {
    MODELS
        .iter()
        .find(|model| model.id == id)
        .unwrap_or_else(|| {
            MODELS
                .iter()
                .find(|model| model.id == DEFAULT_ZENZAI_MODEL_ID)
                .expect("default Zenzai model must be present")
        })
}

pub fn default_model_id() -> String {
    DEFAULT_ZENZAI_MODEL_ID.to_string()
}

pub fn model_path(config_root: &Path, model: &ZenzaiModel) -> PathBuf {
    config_root
        .join("models")
        .join(model.id)
        .join(model.filename)
}
```

Modify `crates/shared/src/lib.rs`:

```rust
pub mod zenzai_models;

pub fn config_root() -> Result<PathBuf, ConfigError> {
    get_config_root()
}

const CONFIG_VERSION: &str = "0.1.3";

#[derive(Debug, Deserialize, Serialize, Clone)]
pub struct ZenzaiConfig {
    pub enable: bool,
    pub profile: String,
    pub backend: String,
    #[serde(default = "zenzai_models::default_model_id")]
    pub model_id: String,
}
```

Update `AppConfig::default()`:

```rust
zenzai: ZenzaiConfig {
    enable: false,
    profile: "".to_string(),
    backend: "cpu".to_string(),
    model_id: zenzai_models::default_model_id(),
},
```

- [ ] **Step 4: Run shared tests to verify GREEN**

Run:

```powershell
cargo test -p shared zenzai_model --lib
```

Expected: PASS.

- [ ] **Step 5: Commit Task 1**

Run:

```powershell
git add crates/shared/src/lib.rs crates/shared/src/zenzai_models.rs
git commit -m "feat: add zenzai model catalog to shared config"
```

### Task 2: C# Config And WinUI Settings Model Selector

**Files:**
- Create: `apps/Azookey.Core/Config/ZenzaiModelCatalog.cs`
- Modify: `apps/Azookey.Core/Config/AppConfig.cs`
- Modify: `apps/Azookey.Settings/MainWindow.xaml.cs`
- Test: `apps/Azookey.Core.Tests/Config/AppConfigTests.cs`
- Test: `apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs`

**Interfaces:**
- Produces: `ZenzaiModelCatalog.DefaultModelId`
- Produces: `ZenzaiModelCatalog.Options`
- Produces: `ZenzaiModelCatalog.ResolveModelId(string? modelId) -> string`
- Updates: `ZenzaiConfig.ModelId`
- Consumes: existing `AddComboBox`, `GetSelectedTag`, `SaveZenzaiAsync`

- [ ] **Step 1: Write failing C# config and UI source tests**

Add to `apps/Azookey.Core.Tests/Config/AppConfigTests.cs`:

```csharp
[Fact]
public void DefaultConfigIncludesDefaultZenzaiModel()
{
    AppConfig config = AppConfig.CreateDefault();

    Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
}

[Fact]
public void LegacyZenzaiConfigWithoutModelIdUsesDefaultModel()
{
    const string json = """
    {
      "version": "0.1.3",
      "zenzai": { "enable": false, "profile": "", "backend": "cpu" }
    }
    """;

    AppConfig config = AppConfig.Deserialize(json);

    Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
}

[Fact]
public void UnknownZenzaiModelIdNormalizesToDefaultModel()
{
    const string json = """
    {
      "version": "0.1.3",
      "zenzai": {
        "enable": false,
        "profile": "",
        "backend": "cpu",
        "model_id": "missing-model"
      }
    }
    """;

    AppConfig config = AppConfig.Deserialize(json);

    Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
}
```

Update `DefaultConfigMatchesRustDefaults` expected version to `0.1.3` and add:

```csharp
Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
```

Add to `apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs`:

```csharp
[Fact]
public void ZenzaiPageShowsJapaneseModelSelector()
{
    string source = File.ReadAllText(GetSourcePath("MainWindow.xaml.cs"));

    Assert.Contains("zenzaiModelBox", source);
    Assert.Contains("\"モデル\"", source);
    Assert.Contains("ZenzaiModelCatalog.Options", source);
    Assert.Contains("ModelId = GetSelectedTag(zenzaiModelBox", source);
}
```

- [ ] **Step 2: Run C# tests to verify RED**

Run:

```powershell
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~AppConfigTests"
dotnet test apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj --filter "FullyQualifiedName~SettingsWindowTextTests"
```

Expected: FAIL because `ZenzaiModelCatalog`, `ModelId`, and `zenzaiModelBox` do not exist.

- [ ] **Step 3: Implement C# catalog and config**

Create `apps/Azookey.Core/Config/ZenzaiModelCatalog.cs`:

```csharp
namespace Azookey.Core.Config;

public sealed record ZenzaiModelOption(
    string Id,
    string DisplayName,
    string Repository,
    string FileName,
    long ExpectedSizeBytes,
    string Sha256)
{
    public string Url => $"https://huggingface.co/{Repository}/resolve/main/{FileName}";
}

public static class ZenzaiModelCatalog
{
    public const string DefaultModelId = "zenz-v3.2-small-q5-k-m";

    public static IReadOnlyList<ZenzaiModelOption> Options { get; } =
    [
        new(
            "zenz-v3.2-small-q5-k-m",
            "Zenz v3.2 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3.2-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            73_871_936,
            "29c223d4c23327b80fd13ebb5ab2555057a46317997d5da391584ffbef0db673"),
        new(
            "zenz-v3.1-small-q5-k-m",
            "Zenz v3.1 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3.1-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            73_871_968,
            "4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c"),
        new(
            "zenz-v3-small-q5-k-m",
            "Zenz v3 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            72_298_816,
            "501f605d088f5b988791a00ae19ed46985ed7c48144f364b2f3f1f951c9b2083"),
        new(
            "zenz-v2-q5-k-m",
            "Zenz v2 (Q5_K_M)",
            "Miwa-Keita/zenz-v2-gguf",
            "zenz-v2-Q5_K_M.gguf",
            72_298_816,
            "22b8d8190bba8c9fec075ffb5b323b0f0d65c7c5f5ff82011799a0c3049d9662")
    ];

    public static string ResolveModelId(string? modelId) =>
        Options.Any(option => string.Equals(option.Id, modelId, StringComparison.Ordinal))
            ? modelId!
            : DefaultModelId;
}
```

Modify `apps/Azookey.Core/Config/AppConfig.cs`:

```csharp
public sealed record ZenzaiConfig
{
    public bool Enable { get; init; }
    public string Profile { get; init; } = "";
    public string Backend { get; init; } = "cpu";
    public string ModelId { get; init; } = ZenzaiModelCatalog.DefaultModelId;
}
```

Change:

```csharp
public const string ConfigVersion = "0.1.3";
```

Normalize Zenzai in `Deserialize` for both current and legacy config:

```csharp
public static AppConfig Deserialize(string json)
{
    AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, AzookeyJson.Options)
        ?? throw new JsonException("Failed to deserialize app config.");

    config = config with
    {
        Zenzai = NormalizeZenzai(config.Zenzai)
    };

    return config.Version == ConfigVersion
        ? config
        : config with
        {
            Version = ConfigVersion,
            General = config.General with
            {
                NumpadInput = MigrateLegacyNumpadInput(config.General.NumpadInput)
            },
            RomajiTable = config.RomajiTable with
            {
                Rows = MigrateLegacyRomajiRows(config.RomajiTable.Rows)
            }
        };
}

private static ZenzaiConfig NormalizeZenzai(ZenzaiConfig zenzai) =>
    zenzai with
    {
        ModelId = ZenzaiModelCatalog.ResolveModelId(zenzai.ModelId)
    };
```

- [ ] **Step 4: Implement WinUI selector**

Modify `apps/Azookey.Settings/MainWindow.xaml.cs`:

```csharp
private ComboBox? zenzaiModelBox;
```

In `BuildZenzaiPage`, add the model ComboBox after the enable toggle and before the profile textbox:

```csharp
zenzaiModelBox = AddComboBox(
    settings,
    "モデル",
    ZenzaiModelCatalog.ResolveModelId(State.Config.Zenzai.ModelId),
    OnModelSelectionChanged,
    ZenzaiModelCatalog.Options
        .Select(model => new ComboOption(model.DisplayName, model.Id))
        .ToArray());
```

Add handler:

```csharp
private async void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    await SaveZenzaiAsync();
}
```

Update `SaveZenzaiAsync`:

```csharp
Zenzai = config.Zenzai with
{
    Enable = zenzaiEnableSwitch?.IsOn ?? config.Zenzai.Enable,
    Profile = zenzaiProfileBox?.Text ?? config.Zenzai.Profile,
    Backend = GetSelectedTag(zenzaiBackendBox, config.Zenzai.Backend),
    ModelId = ZenzaiModelCatalog.ResolveModelId(
        GetSelectedTag(zenzaiModelBox, config.Zenzai.ModelId))
}
```

- [ ] **Step 5: Run C# tests to verify GREEN**

Run:

```powershell
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~AppConfigTests"
dotnet test apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj --filter "FullyQualifiedName~SettingsWindowTextTests"
```

Expected: PASS.

- [ ] **Step 6: Commit Task 2**

Run:

```powershell
git add apps/Azookey.Core/Config/AppConfig.cs apps/Azookey.Core/Config/ZenzaiModelCatalog.cs apps/Azookey.Core.Tests/Config/AppConfigTests.cs apps/Azookey.Settings/MainWindow.xaml.cs apps/Azookey.Settings.Tests/ViewModels/SettingsWindowTextTests.cs
git commit -m "feat: add zenzai model selection setting"
```

### Task 3: Remove Bundled GGUF From Build And Installer

**Files:**
- Modify: `Makefile.toml`
- Modify: `installer/Installer.iss`
- Test: `apps/Azookey.Core.Tests/Installer/StartupTaskTests.cs`

**Interfaces:**
- Produces: installer upgrade cleanup for `{app}\zenz.gguf`
- Removes: `cp zenz.gguf build`

- [ ] **Step 1: Write failing build and installer tests**

Add to `apps/Azookey.Core.Tests/Installer/StartupTaskTests.cs`:

```csharp
[Fact]
public void PostBuildDoesNotCopyBundledGguf()
{
    string repositoryRoot = FindRepositoryRoot();
    string makefilePath = Path.Combine(repositoryRoot, "Makefile.toml");
    string makefile = File.ReadAllText(makefilePath);

    Assert.DoesNotContain("cp zenz.gguf build", makefile);
}

[Fact]
public void InstallerDeletesBundledRootGgufOnUpgrade()
{
    string repositoryRoot = FindRepositoryRoot();
    string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
    string installerScript = File.ReadAllText(installerPath);

    Assert.Contains(@"Type: files; Name: ""{app}\zenz.gguf""", installerScript);
}
```

- [ ] **Step 2: Run installer tests to verify RED**

Run:

```powershell
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~StartupTaskTests"
```

Expected: FAIL because the makefile still copies `zenz.gguf` and installer upgrade cleanup is missing.

- [ ] **Step 3: Remove the build copy and add installer cleanup**

Delete this line from `Makefile.toml`:

```powershell
cp zenz.gguf build
```

Add this line under `[InstallDelete]` in `installer/Installer.iss`:

```ini
Type: files; Name: "{app}\zenz.gguf"
```

- [ ] **Step 4: Run installer tests to verify GREEN**

Run:

```powershell
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~StartupTaskTests"
```

Expected: PASS.

- [ ] **Step 5: Commit Task 3**

Run:

```powershell
git add Makefile.toml installer/Installer.iss apps/Azookey.Core.Tests/Installer/StartupTaskTests.cs
git commit -m "build: stop bundling zenzai gguf"
```

### Task 4: Launcher Verified Model Downloader

**Files:**
- Create: `crates/launcher/src/zenzai_model_download.rs`
- Modify: `crates/launcher/Cargo.toml`
- Modify: `crates/launcher/src/main.rs`

**Interfaces:**
- Consumes: `shared::zenzai_models::{model_path, resolve_model, ZenzaiModel}`
- Produces: `ensure_configured_model(config_root: &Path, config: &AppConfig, downloader: &dyn ModelDownloader) -> ModelEnsureResult`
- Produces: `BlockingModelDownloader`
- Produces: `ModelEnsureResult { path: Option<PathBuf>, error: Option<String> }`

- [ ] **Step 1: Write failing launcher downloader tests**

Add this module declaration near the top of `crates/launcher/src/main.rs` so the new tests are compiled:

```rust
mod zenzai_model_download;
```

Create `crates/launcher/src/zenzai_model_download.rs` with only tests and minimal type references:

```rust
#[cfg(test)]
mod tests {
    use super::*;
    use shared::{zenzai_models::ZenzaiModel, AppConfig};
    use std::{cell::Cell, fs};

    const EXISTING_TEST_MODEL: ZenzaiModel = ZenzaiModel {
        id: "test-existing",
        display_name: "Test existing",
        repository: "example/test",
        filename: "existing.gguf",
        url: "https://example.test/existing.gguf",
        expected_size_bytes: 14,
        sha256: "41cdbe602a4a31645f9eda434ee4adee1a5620a46066f7a29ea587b56b904a43",
    };

    const DOWNLOADED_TEST_MODEL: ZenzaiModel = ZenzaiModel {
        id: "test-downloaded",
        display_name: "Test downloaded",
        repository: "example/test",
        filename: "downloaded.gguf",
        url: "https://example.test/downloaded.gguf",
        expected_size_bytes: 16,
        sha256: "6f1b9e8b969d1ea18bd8ba51a2ba697f55142b337f163df6a7daf850453dd161",
    };

    struct FakeDownloader {
        calls: Cell<usize>,
        bytes: Vec<u8>,
        error: Option<&'static str>,
    }

    impl FakeDownloader {
        fn ok(bytes: Vec<u8>) -> Self {
            Self {
                calls: Cell::new(0),
                bytes,
                error: None,
            }
        }

        fn err(message: &'static str) -> Self {
            Self {
                calls: Cell::new(0),
                bytes: Vec::new(),
                error: Some(message),
            }
        }
    }

    impl ModelDownloader for FakeDownloader {
        fn download(&self, _url: &str, destination: &std::path::Path) -> anyhow::Result<()> {
            self.calls.set(self.calls.get() + 1);
            if let Some(message) = self.error {
                anyhow::bail!(message);
            }
            fs::write(destination, &self.bytes)?;
            Ok(())
        }
    }

    #[test]
    fn existing_verified_model_is_reused_without_download() {
        let temp = tempfile::tempdir().unwrap();
        let bytes = b"existing model".to_vec();
        let path = shared::zenzai_models::model_path(temp.path(), &EXISTING_TEST_MODEL);
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        fs::write(&path, &bytes).unwrap();
        let downloader = FakeDownloader::ok(b"replacement".to_vec());

        let result = ensure_model_file(temp.path(), &EXISTING_TEST_MODEL, &downloader).unwrap();

        assert_eq!(result, path);
        assert_eq!(downloader.calls.get(), 0);
    }

    #[test]
    fn missing_model_downloads_to_partial_and_promotes_after_hash_match() {
        let temp = tempfile::tempdir().unwrap();
        let bytes = b"downloaded model".to_vec();
        let downloader = FakeDownloader::ok(bytes.clone());

        let result = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader).unwrap();

        assert_eq!(fs::read(&result).unwrap(), bytes);
        assert!(!result.with_extension("gguf.partial").exists());
        assert_eq!(downloader.calls.get(), 1);
    }

    #[test]
    fn failed_download_does_not_create_final_model_file() {
        let temp = tempfile::tempdir().unwrap();
        let downloader = FakeDownloader::err("network down");

        let error = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader)
            .expect_err("download failure should be returned");

        assert!(error.to_string().contains("network down"));
        assert!(!shared::zenzai_models::model_path(temp.path(), &DOWNLOADED_TEST_MODEL).exists());
    }

    #[test]
    fn downloaded_model_with_wrong_hash_is_not_promoted() {
        let temp = tempfile::tempdir().unwrap();
        let downloader = FakeDownloader::ok(b"wrong model bytes".to_vec());

        let error = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader)
            .expect_err("hash mismatch should fail");

        assert!(error.to_string().contains("downloaded model did not verify"));
        assert!(!shared::zenzai_models::model_path(temp.path(), &DOWNLOADED_TEST_MODEL).exists());
    }

    #[test]
    fn ensure_configured_model_returns_error_without_path_when_download_fails() {
        let temp = tempfile::tempdir().unwrap();
        let config = AppConfig::default();
        let downloader = FakeDownloader::err("offline");

        let result = ensure_configured_model(temp.path(), &config, &downloader);

        assert!(result.path.is_none());
        assert!(result.error.unwrap().contains("offline"));
    }
}
```

- [ ] **Step 2: Run launcher downloader tests to verify RED**

Run:

```powershell
cargo test -p launcher zenzai_model_download
```

Expected: FAIL because `ModelDownloader`, `ensure_model_file`, and `ensure_configured_model` do not exist.

- [ ] **Step 3: Add dependencies**

Modify `crates/launcher/Cargo.toml`:

```toml
reqwest = { version = "0.12.12", default-features = false, features = ["blocking", "rustls-tls"] }
sha2 = "0.10"

[dev-dependencies]
tempfile = "3.14.0"
```

- [ ] **Step 4: Implement downloader module**

Replace `crates/launcher/src/zenzai_model_download.rs` with production code plus the tests from Step 1:

```rust
use anyhow::{Context as _, Result};
use sha2::{Digest, Sha256};
use shared::{
    zenzai_models::{model_path, resolve_model, ZenzaiModel},
    AppConfig,
};
use std::{
    fs::{self, File},
    io::{Read, Write},
    path::{Path, PathBuf},
};

pub trait ModelDownloader {
    fn download(&self, url: &str, destination: &Path) -> Result<()>;
}

pub struct BlockingModelDownloader;

impl ModelDownloader for BlockingModelDownloader {
    fn download(&self, url: &str, destination: &Path) -> Result<()> {
        let mut response = reqwest::blocking::get(url)
            .with_context(|| format!("failed to request {url}"))?
            .error_for_status()
            .with_context(|| format!("failed to download {url}"))?;
        let mut file = File::create(destination)
            .with_context(|| format!("failed to create {}", destination.display()))?;
        response
            .copy_to(&mut file)
            .with_context(|| format!("failed to write {}", destination.display()))?;
        file.flush()
            .with_context(|| format!("failed to flush {}", destination.display()))?;
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ModelEnsureResult {
    pub path: Option<PathBuf>,
    pub error: Option<String>,
}

pub fn ensure_configured_model(
    config_root: &Path,
    config: &AppConfig,
    downloader: &dyn ModelDownloader,
) -> ModelEnsureResult {
    let model = resolve_model(&config.zenzai.model_id);
    match ensure_model_file(config_root, model, downloader) {
        Ok(path) => ModelEnsureResult {
            path: Some(path),
            error: None,
        },
        Err(error) => ModelEnsureResult {
            path: None,
            error: Some(error.to_string()),
        },
    }
}

pub fn ensure_model_file(
    config_root: &Path,
    model: &ZenzaiModel,
    downloader: &dyn ModelDownloader,
) -> Result<PathBuf> {
    let final_path = model_path(config_root, model);
    if verify_model_file(&final_path, model).unwrap_or(false) {
        return Ok(final_path);
    }

    if let Some(parent) = final_path.parent() {
        fs::create_dir_all(parent)
            .with_context(|| format!("failed to create {}", parent.display()))?;
    }

    let partial_path = partial_path(&final_path);
    let _ = fs::remove_file(&partial_path);
    downloader.download(model.url, &partial_path)?;
    anyhow::ensure!(
        verify_model_file(&partial_path, model)?,
        "downloaded model did not verify: {}",
        partial_path.display()
    );
    fs::rename(&partial_path, &final_path).with_context(|| {
        format!(
            "failed to promote {} to {}",
            partial_path.display(),
            final_path.display()
        )
    })?;
    Ok(final_path)
}

fn verify_model_file(path: &Path, model: &ZenzaiModel) -> Result<bool> {
    let metadata = match fs::metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(false),
        Err(error) => return Err(error.into()),
    };
    if metadata.len() != model.expected_size_bytes {
        return Ok(false);
    }
    Ok(sha256_hex_file(path)? == model.sha256)
}

fn sha256_hex_file(path: &Path) -> Result<String> {
    let mut file = File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 64 * 1024];
    loop {
        let read = file.read(&mut buffer)?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    Ok(format!("{:x}", hasher.finalize()))
}

fn partial_path(path: &Path) -> PathBuf {
    let extension = path
        .extension()
        .and_then(|value| value.to_str())
        .map(|value| format!("{value}.partial"))
        .unwrap_or_else(|| "partial".to_string());
    path.with_extension(extension)
}
```

- [ ] **Step 5: Confirm module declaration**

Confirm `crates/launcher/src/main.rs` contains the module declaration added in Step 1:

```rust
mod zenzai_model_download;
```

- [ ] **Step 6: Run launcher downloader tests to verify GREEN**

Run:

```powershell
cargo test -p launcher zenzai_model_download
```

Expected: PASS.

- [ ] **Step 7: Commit Task 4**

Run:

```powershell
git add crates/launcher/Cargo.toml crates/launcher/src/main.rs crates/launcher/src/zenzai_model_download.rs Cargo.lock
git commit -m "feat: add verified zenzai model downloader"
```

### Task 5: Wire Model Path Into Server Startup

**Files:**
- Modify: `crates/launcher/src/main.rs`
- Modify: `apps/Azookey.Core/Config/ZenzaiModelCatalog.cs`
- Modify: `apps/Azookey.Core/Process/ServerRestartService.cs`
- Modify: `frontend/src-tauri/src/server_process.rs`
- Test: `crates/launcher/src/main.rs`
- Test: `apps/Azookey.Core.Tests/Process/ServerRestartServiceTests.cs`

**Interfaces:**
- Consumes: `ensure_configured_model`
- Produces: `AZOOKEY_ZENZAI_MODEL_PATH` env var for verified model paths
- Produces: C# direct-start fallback env var when a selected model already exists

- [ ] **Step 1: Write failing launcher env tests**

Add to `#[cfg(test)] mod tests` in `crates/launcher/src/main.rs`:

```rust
#[test]
fn server_command_sets_model_path_when_model_exists() {
    let temp = tempfile::tempdir().unwrap();
    let config = AppConfig::default();
    let model_path = temp.path().join("model.gguf");
    let mut command = process_command_with_backend(temp.path(), "azookey-server.exe", &config);

    apply_zenzai_model_env(&mut command, Some(&model_path));

    let envs: Vec<_> = command
        .get_envs()
        .map(|(key, value)| {
            (
                key.to_string_lossy().into_owned(),
                value.map(|value| value.to_string_lossy().into_owned()),
            )
        })
        .collect();
    assert!(envs.iter().any(|(key, value)| {
        key == "AZOOKEY_ZENZAI_MODEL_PATH"
            && value.as_deref() == Some(model_path.to_string_lossy().as_ref())
    }));
}

#[test]
fn server_command_omits_model_path_when_model_is_unavailable() {
    let temp = tempfile::tempdir().unwrap();
    let config = AppConfig::default();
    let mut command = process_command_with_backend(temp.path(), "azookey-server.exe", &config);

    apply_zenzai_model_env(&mut command, None);

    assert!(!command
        .get_envs()
        .any(|(key, _)| key == "AZOOKEY_ZENZAI_MODEL_PATH"));
}
```

- [ ] **Step 2: Write failing C# direct-start env test**

Add to `apps/Azookey.Core.Tests/Process/ServerRestartServiceTests.cs`:

```csharp
[Fact]
public void DirectStartInfoSetsZenzaiModelPathWhenDownloadedModelExists()
{
    string configRoot = Path.Combine(installDirectory, "config");
    ZenzaiModelOption model = ZenzaiModelCatalog.Options[0];
    string modelPath = Path.Combine(configRoot, "models", model.Id, model.FileName);
    Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
    File.WriteAllText(modelPath, "model");

    AppConfig config = AppConfig.CreateDefault() with
    {
        Zenzai = AppConfig.CreateDefault().Zenzai with { ModelId = model.Id }
    };

    ProcessStartInfo startInfo = ServerRestartService.CreateDirectStartInfo(
        installDirectory,
        config,
        existingPath: "",
        configRoot: configRoot);

    Assert.Equal(modelPath, startInfo.Environment["AZOOKEY_ZENZAI_MODEL_PATH"]);
}
```

- [ ] **Step 3: Run tests to verify RED**

Run:

```powershell
cargo test -p launcher model_path
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~ServerRestartServiceTests"
```

Expected: FAIL because `apply_zenzai_model_env`, `configRoot`, and model path resolution do not exist.

- [ ] **Step 4: Wire launcher startup**

Modify imports in `crates/launcher/src/main.rs`:

```rust
use zenzai_model_download::{ensure_configured_model, BlockingModelDownloader};
```

Add helper:

```rust
fn apply_zenzai_model_env(command: &mut Command, model_path: Option<&Path>) {
    if let Some(model_path) = model_path {
        command.env("AZOOKEY_ZENZAI_MODEL_PATH", model_path);
    }
}
```

In `start_server_process`, before setting CPU support:

```rust
let model_result = match shared::config_root() {
    Ok(config_root) => ensure_configured_model(&config_root, &config, &BlockingModelDownloader),
    Err(error) => zenzai_model_download::ModelEnsureResult {
        path: None,
        error: Some(error.to_string()),
    },
};
apply_zenzai_model_env(&mut command, model_result.path.as_deref());
```

Extend `startup_details`:

```rust
let model_details = match (&model_result.path, &model_result.error) {
    (Some(path), _) => format!("zenzai_model_path={}", path.display()),
    (None, Some(error)) => format!("zenzai_model_error={error}"),
    (None, None) => "zenzai_model_path=".to_string(),
};
```

Use this `startup_details` format:

```rust
let startup_details = format!(
    "backend={};backend_dir={};zenzai_enable={};cpu_backend_supported={};{}",
    config.zenzai.backend,
    backend_dir(&config),
    config.zenzai.enable,
    cpu_backend_supported,
    model_details
);
```

- [ ] **Step 5: Add C# model path resolution and direct-start env**

Add to `ZenzaiModelCatalog.cs`:

```csharp
public static string? ResolveExistingModelPath(string configRoot, string? modelId)
{
    string resolvedId = ResolveModelId(modelId);
    ZenzaiModelOption model = Options.First(option => option.Id == resolvedId);
    string path = Path.Combine(configRoot, "models", model.Id, model.FileName);
    return File.Exists(path) ? path : null;
}
```

Modify `ServerRestartService.CreateDirectStartInfo` signature:

```csharp
internal static ProcessStartInfo CreateDirectStartInfo(
    string installDirectory,
    AppConfig config,
    string? existingPath = null,
    Func<bool>? zenzaiCpuBackendSupported = null,
    string? configRoot = null)
```

After CPU env setup:

```csharp
string effectiveConfigRoot = configRoot ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Azookey");
string? modelPath = ZenzaiModelCatalog.ResolveExistingModelPath(
    effectiveConfigRoot,
    config.Zenzai.ModelId);
if (!string.IsNullOrWhiteSpace(modelPath))
{
    startInfo.Environment["AZOOKEY_ZENZAI_MODEL_PATH"] = modelPath;
}
```

- [ ] **Step 6: Preserve model path in Tauri direct-start helper**

Modify `frontend/src-tauri/src/server_process.rs` `start_server` before `.spawn()`:

```rust
let config_root = shared::config_root().ok();
let model_path = config_root
    .as_deref()
    .map(|root| {
        let model = shared::zenzai_models::resolve_model(&config.zenzai.model_id);
        shared::zenzai_models::model_path(root, model)
    })
    .filter(|path| path.is_file());

let mut command = Command::new(server_path);
command
    .current_dir(install_dir)
    .env("AZOOKEY_ZENZAI_CPU_SUPPORTED", zenzai_cpu_supported_env())
    .env("PATH", path)
    .stdin(Stdio::null())
    .stdout(Stdio::null())
    .stderr(Stdio::null());
if let Some(model_path) = model_path {
    command.env("AZOOKEY_ZENZAI_MODEL_PATH", model_path);
}
command
    .spawn()
    .with_context(|| format!("Failed to start {}", server_path.display()))?;
```

- [ ] **Step 7: Run startup env tests to verify GREEN**

Run:

```powershell
cargo test -p launcher model_path
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~ServerRestartServiceTests"
cargo test -p frontend server_process --lib
```

Expected: PASS.

- [ ] **Step 8: Commit Task 5**

Run:

```powershell
git add crates/launcher/src/main.rs apps/Azookey.Core/Config/ZenzaiModelCatalog.cs apps/Azookey.Core/Process/ServerRestartService.cs apps/Azookey.Core.Tests/Process/ServerRestartServiceTests.cs frontend/src-tauri/src/server_process.rs
git commit -m "feat: pass zenzai model path to server"
```

### Task 6: Swift Server Uses Environment Model Path

**Files:**
- Modify: `server-swift/Sources/azookey-server/azookey_server.swift`
- Test: `server-swift/Tests/azookey-serverTests/azookey_serverTests.swift`

**Interfaces:**
- Consumes: `AZOOKEY_ZENZAI_MODEL_PATH`
- Produces: `zenzaiWeightURL(environment:legacyURL:) -> URL`

- [ ] **Step 1: Write failing Swift tests**

Add to `server-swift/Tests/azookey-serverTests/azookey_serverTests.swift`:

```swift
@Test func zenzaiWeightURLUsesLauncherProvidedModelPath() async throws {
    let legacyURL = URL(fileURLWithPath: #"C:\Azookey\zenz.gguf"#)
    let selectedURL = zenzaiWeightURL(
        environment: ["AZOOKEY_ZENZAI_MODEL_PATH": #"C:\Users\Test\AppData\Roaming\Azookey\models\zenz-v3.2-small-q5-k-m\ggml-model-Q5_K_M.gguf"#],
        legacyURL: legacyURL
    )

    #expect(selectedURL.path().contains("zenz-v3.2-small-q5-k-m"))
    #expect(selectedURL.lastPathComponent == "ggml-model-Q5_K_M.gguf")
}

@Test func zenzaiWeightURLFallsBackToLegacyPathWhenEnvironmentIsMissing() async throws {
    let legacyURL = URL(fileURLWithPath: #"C:\Azookey\zenz.gguf"#)

    #expect(zenzaiWeightURL(environment: [:], legacyURL: legacyURL) == legacyURL)
}
```

- [ ] **Step 2: Run Swift tests to verify RED**

Run:

```powershell
Push-Location server-swift
swift test --filter zenzaiWeightURL
Pop-Location
```

Expected: FAIL because `zenzaiWeightURL(environment:legacyURL:)` does not exist.

- [ ] **Step 3: Implement environment path resolver**

Add near the existing environment helper functions in `server-swift/Sources/azookey-server/azookey_server.swift`:

```swift
func zenzaiWeightURL(
    environment: [String: String] = ProcessInfo.processInfo.environment,
    legacyURL: URL = execURL.appendingPathComponent("zenz.gguf")
) -> URL {
    guard let value = environment["AZOOKEY_ZENZAI_MODEL_PATH"],
          !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    else {
        return legacyURL
    }

    return URL(fileURLWithPath: value)
}
```

Replace both occurrences of:

```swift
zenzaiWeightURL: execURL.appendingPathComponent("zenz.gguf"),
```

with:

```swift
zenzaiWeightURL: zenzaiWeightURL(),
```

- [ ] **Step 4: Run Swift tests to verify GREEN**

Run:

```powershell
Push-Location server-swift
swift test --filter zenzaiWeightURL
Pop-Location
```

Expected: PASS.

- [ ] **Step 5: Commit Task 6**

Run:

```powershell
git add server-swift/Sources/azookey-server/azookey_server.swift server-swift/Tests/azookey-serverTests/azookey_serverTests.swift
git commit -m "feat: load zenzai model from launcher path"
```

### Task 7: End-To-End Verification And Push

**Files:**
- No new production files.
- May update plan checkboxes in this file while executing.

**Interfaces:**
- Verifies the complete branch.
- Pushes `codex/model-auto-download` to `keewai`.

- [ ] **Step 1: Run Rust test set**

Run:

```powershell
cargo test -p shared --lib
cargo test -p launcher
cargo test -p frontend --lib
```

Expected: PASS.

- [ ] **Step 2: Run .NET test set**

Run:

```powershell
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj
dotnet test apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Run Swift test set**

Run:

```powershell
Push-Location server-swift
swift test
Pop-Location
```

Expected: PASS.

- [ ] **Step 4: Verify build no longer requires repository-root GGUF**

Run:

```powershell
if (Test-Path zenz.gguf) { Rename-Item zenz.gguf zenz.gguf.local-test-hidden }
try {
    cargo make post_build -- --release
} finally {
    if (Test-Path zenz.gguf.local-test-hidden) { Rename-Item zenz.gguf.local-test-hidden zenz.gguf }
}
```

Expected: `post_build` reaches the existing compiled artifact copy steps without failing because `zenz.gguf` is missing. If compiled artifacts are absent, the failure must mention a missing target binary rather than `zenz.gguf`.

- [ ] **Step 5: Run formatting**

Run:

```powershell
cargo fmt
dotnet format apps/Azookey.WinUI.sln --include apps/Azookey.Core apps/Azookey.Settings apps/Azookey.Core.Tests apps/Azookey.Settings.Tests
```

Expected: commands complete successfully or report no changes.

- [ ] **Step 6: Inspect final diff**

Run:

```powershell
git status --short
git diff --check
git log --oneline --decorate -8
```

Expected: clean whitespace check, only intentional files modified.

- [ ] **Step 7: Push branch**

Run:

```powershell
git push -u keewai codex/model-auto-download
```

Expected: push succeeds and GitHub shows branch `codex/model-auto-download` in `keewai704/Azuki-Win`.
