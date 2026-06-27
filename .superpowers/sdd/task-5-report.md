# Task 5 Report: Wire Model Path Into Server Startup

## Scope implemented

- Wired launcher startup to call `ensure_configured_model` on every server start, independent of `zenzai.enable`.
- Passed `AZOOKEY_ZENZAI_MODEL_PATH` only when the ensured model path exists and verified successfully.
- Preserved normal IME startup when model ensure/download fails by logging the failure and starting without the model env var.
- Added C# direct-start fallback wiring so `ServerRestartService` sets `AZOOKEY_ZENZAI_MODEL_PATH` only when the selected catalog model already exists under the config root.
- Added the same existing-file-only env propagation to the Tauri direct-start helper.
- Kept scope limited to launcher/direct-start wiring. I did not implement Swift server consumption.

## TDD RED evidence

Commands run before implementation:

```powershell
cargo test -p launcher model_path
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~ServerRestartServiceTests"
```

Observed failures:

- Rust: unresolved import for `apply_zenzai_model_env`
- C#: `CreateDirectStartInfo` had no `configRoot` parameter

Why this was the right RED:

- The new tests were added first.
- The failures were due to missing production wiring rather than bad test setup, which confirmed the tests were exercising behavior that did not exist yet.

## Implementation details

### `crates/launcher/src/main.rs`

- Imported `ensure_configured_model` and `BlockingModelDownloader`.
- Added `apply_zenzai_model_env(&mut Command, Option<&Path>)`.
- In `start_server_process`, resolved `shared::config_root()`, called `ensure_configured_model`, and converted config-root failure into `ModelEnsureResult { path: None, error: Some(...) }`.
- Logged ensure failures with `eprintln!` and included model path or model error in launcher crash-trace startup details.
- Ensured the server still starts when model ensure fails by omitting `AZOOKEY_ZENZAI_MODEL_PATH` instead of returning an error.

### `apps/Azookey.Core/Config/ZenzaiModelCatalog.cs`

- Added `ResolveExistingModelPath(string configRoot, string? modelId)`.
- Resolution uses only the built-in catalog and returns `null` unless the resolved file already exists.

### `apps/Azookey.Core/Process/ServerRestartService.cs`

- Extended `CreateDirectStartInfo` with optional `configRoot`.
- Resolved the effective config root to `%APPDATA%\\Azookey` when not provided.
- Set `AZOOKEY_ZENZAI_MODEL_PATH` only when `ResolveExistingModelPath(...)` returns a non-empty existing path.

### `frontend/src-tauri/src/server_process.rs`

- Resolved the configured model path from `shared::config_root()` plus the shared catalog helpers.
- Added `AZOOKEY_ZENZAI_MODEL_PATH` only when that resolved file already exists.
- Kept the direct-start helper as an existing-file-only fallback; it does not attempt downloads.

## GREEN verification

Focused tests from the brief:

```powershell
cargo test -p launcher model_path
dotnet test apps/Azookey.Core.Tests/Azookey.Core.Tests.csproj --filter "FullyQualifiedName~ServerRestartServiceTests"
cargo test -p frontend server_process --lib
```

Results:

- `cargo test -p launcher model_path`: 2 passed, 0 failed
- `dotnet test ... ServerRestartServiceTests`: 15 passed, 0 failed
- `cargo test -p frontend server_process --lib`: 4 passed, 0 failed

Additional directly relevant verification:

```powershell
cargo test -p launcher
```

Result:

- `cargo test -p launcher`: 14 passed, 0 failed

## Commit created

- `f9c8296` - `feat: pass zenzai model path to server`

## Self-review

- The launcher path is now ensured even when `zenzai.enable` is false, matching the task constraint.
- Launcher startup no longer fails just because model download/ensure fails; the failure is logged/traced and the server still spawns.
- Env propagation is guarded everywhere by verified/existing-path checks.
- Model resolution stays inside the built-in catalog on both Rust and C# paths.
- No unrelated files were modified.

## Concerns

- None.
