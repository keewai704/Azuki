# WinUI Settings, Candidate Window, and Zenzai Fix Design

## Goal

設定 UI を現在の Microsoft Windows/WinUI デザインガイドに沿って作り直し、変換候補ウィンドウが表示されない問題を修正する。あわせて CUDA backend のサポートを削除し、Zenzai model を設定 UI から確実に選択できるようにする。

## References

- Microsoft Learn: https://learn.microsoft.com/en-us/windows/apps/design/guidelines-overview
- Microsoft Learn, app settings: https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings
- Microsoft Learn, NavigationView: https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview
- Microsoft Learn, controls and patterns: https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/

The settings page follows the official guidance: full-window settings surface, constrained readable width, scrollable single-column content, related settings grouped under clear section headers, immediate save after user changes, `ToggleSwitch` for binary choices, and compact selection controls for small option sets. `NavigationView` is used for top-level settings categories because it provides adaptive navigation and keeps the page structure predictable.

## Current State

The WinUI implementation lives in `C:/azw-winui/apps`. It already contains C# projects for:

- `Azookey.UI`: `ui.exe`, the WinUI candidate/indicator UI and `azookey_ui` named pipe gRPC host.
- `Azookey.Settings`: `frontend.exe`, the WinUI settings app.
- `Azookey.Core`: shared config, Win32, process, update, and model catalog helpers.

The settings window is currently built procedurally in `MainWindow.xaml.cs`. Labels are hard-coded in mojibake text, navigation is a manual button column, and the Zenzai page still exposes CUDA. This makes the UI hard to maintain and is the direct source of English/mojibake label regressions.

The candidate UI path is:

1. Rust TSF client updates composition state.
2. `crates/client/src/engine/ipc_service.rs` sends `UpdateCandidateWindow` to `\\.\pipe\azookey_ui`.
3. `apps/Azookey.UI/Ipc/WindowServiceImpl.cs` converts the RPC request into `WindowAction`.
4. `UiWindowCoordinator` merges state and calls `CandidateWindow.Render`.

The client already retries when `window_client` is `None`, but `UI_PIPE_BUSY_TIMEOUT` is `Duration::ZERO`. If `ui.exe` is still starting, the first UI connection can be skipped immediately and repeated candidate updates can continue to miss the pipe during startup. This is the primary candidate-window root-cause hypothesis to validate and fix.

## Design

### Settings UI

Replace the single procedural `MainWindow.xaml.cs` page builder with a `NavigationView` shell and focused pages:

- `GeneralPage`
- `InputPage`
- `CandidatePage`
- `ZenzaiPage`
- `DictionaryPage`
- `DebugPage`
- `AboutPage`

Each page receives a shared `SettingsAppState` and writes settings immediately through `SaveAsync`. The shell owns the status `InfoBar`, page navigation, and application title. Pages expose only their own controls and avoid cross-page state.

The visual pattern is quiet and dense:

- left `NavigationView` with Japanese labels and built-in adaptive behavior;
- content constrained to roughly 1000 px and scrollable;
- one-column setting groups;
- 8 px or smaller corner radius;
- no marketing hero, decorative gradients, or nested cards;
- Japanese copy only;
- no live-conversion reading display controls.

Use Windows Community Toolkit `SettingsCard` / `SettingsExpander` if it builds cleanly with the pinned WinUI stack. If the package causes build/runtime problems, implement a local `SettingsRow` user control with the same structure: icon, header, optional description, and right-aligned action control.

### Candidate Window

Keep the existing WinUI `CandidateWindow` contract and state reducer. Fix delivery reliability at the Rust client boundary:

- increase the UI named pipe busy timeout from `0ms` to a short bounded window;
- preserve deferred reconnect behavior when `window_client` is missing;
- add regression tests around retry policy constants and candidate-window delivery classification;
- keep failed UI IPC non-fatal so IME conversion continues even if UI is unavailable.

The WinUI candidate window continues to display only conversion candidates and the small footer. Reading/ruby display remains removed.

### CUDA Removal

CUDA is removed as a supported backend:

- settings UI only offers CPU and Vulkan;
- C# capability detection no longer checks CUDA DLLs;
- launcher backend directory mapping no longer maps to `llama_cuda`;
- existing `"cuda"` config values are migrated to `"vulkan"`;
- build, installer, VM, and CI scripts no longer copy, stage, cache, delete, or require `llama_cuda`;
- tests assert that CUDA does not appear in the current WinUI settings surface or backend mapping.

The server remains tolerant of older config values during migration, but new UI and config writes never produce `"cuda"`.

### Zenzai Model Selection

Keep the shared model catalog already introduced for automatic GGUF download. The settings UI presents a model selector backed by `ZenzaiModelCatalog.Options`. On selection:

1. The selected model ID is written to `settings.json` as `zenzai.model_id`.
2. Invalid or missing IDs normalize to `ZenzaiModelCatalog.DefaultModelId`.
3. If the model or backend changed, the settings app requests server restart.
4. On next server start, `launcher.exe` downloads/verifies the selected GGUF into `%APPDATA%/Azookey/models/<model-id>/`.

The selector must not use display text as the saved value. Tests verify the saved ID, the default ID, and restart behavior.

## Data Flow

Settings data flow:

`Page control change` -> `SettingsAppState.SaveAsync(next AppConfig)` -> atomic `settings.json` write -> `azookey_server.UpdateConfig` notification -> optional launcher restart for backend/model changes.

Candidate data flow:

`TSF composition update` -> Rust `IPCService.update_candidate_window_with_reading` -> named pipe gRPC `UpdateCandidateWindow` -> C# `WindowServiceImpl` -> `UiWindowCoordinator` -> `CandidateWindow.Render`.

Zenzai model flow:

`settings.json zenzai.model_id` -> launcher `ensure_configured_model` -> verified GGUF path in `AZOOKEY_ZENZAI_MODEL_PATH` -> Swift server enables Zenzai only when the verified path is present.

## Error Handling

- Settings save errors show an `InfoBar` and keep the prior in-memory config.
- Server notification failures are warnings after a successful settings save.
- Candidate UI IPC failures remain non-fatal and clear only the cached UI client.
- Model download failure omits `AZOOKEY_ZENZAI_MODEL_PATH`; the server falls back to standard conversion.
- Legacy `"cuda"` settings migrate to `"vulkan"` on config load; if Vulkan files are unavailable, Zenzai is still controlled by the existing server/backend capability path.

## Testing

Use TDD for production changes.

- Settings tests:
  - XAML/source text contains Japanese labels and no mojibake markers.
  - NavigationView and page files exist.
  - Zenzai page exposes model selector and CPU/Vulkan only.
  - Selecting a model saves the selected model ID.
  - Backend/model changes request server restart.

- Core/launcher tests:
  - `"cuda"` config migrates to `"vulkan"`.
  - Backend directory never returns `llama_cuda`.
  - build/installer script text no longer references `llama_cuda`.

- Candidate tests:
  - UI pipe retry timeout is non-zero and bounded.
  - deferred UI connection still treats unavailable UI as non-fatal.
  - candidate render visibility requires visible state, position, visible candidate list, and at least one candidate.

- Verification:
  - `dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj`
  - `dotnet test apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj`
  - `dotnet test apps/Azookey.UI.Tests/Azookey.UI.Tests.csproj`
  - `cargo test -p launcher`
  - targeted `cargo test -p frontend`/client tests as touched by the changes
  - build with Visual Studio Build Tools 2026/MSBuild or `cargo make build --release` when local toolchain permits

## Non-Goals

- Changing the IME conversion algorithm.
- Reintroducing live-conversion reading display.
- Changing `window.proto` or `service.proto`.
- Adding new Zenzai models beyond the existing catalog.
- Retaining CUDA as a hidden or experimental backend.
