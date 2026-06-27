# GGUF Model Auto Download Design

Date: 2026-06-26

## Summary

azooKey Windows will stop requiring a local bundled `zenz.gguf` file. On app startup, the launcher will ensure the configured Zenzai GGUF model exists in the per-user Azookey data directory. If the model is missing, the launcher downloads it from Hugging Face before starting the server process. The default selection will be the latest supported GGUF model at the time of this design, `Miwa-Keita/zenz-v3.2-small-gguf`.

The settings UI will expose the selected model as a WinUI 3 combo box. Users can choose other supported models from a built-in catalog. The selected model is persisted in `settings.json`.

## External References

- Hugging Face profile model list for `Miwa-Keita`, where `zenz-v3.2-small-gguf` is the most recently updated GGUF Zenzai model observed on 2026-06-26: <https://huggingface.co/Miwa-Keita/models>
- Default model page: <https://huggingface.co/Miwa-Keita/zenz-v3.2-small-gguf>
- Prior supported GGUF models:
  - <https://huggingface.co/Miwa-Keita/zenz-v3.1-small-gguf>
  - <https://huggingface.co/Miwa-Keita/zenz-v3-small-gguf>
  - <https://huggingface.co/Miwa-Keita/zenz-v2-gguf>

## Goals

- Remove the build-time and installer dependency on a local `zenz.gguf`.
- Download the default model automatically on first app startup, even when Zenzai is not enabled yet.
- Default to the newest supported model in the app catalog.
- Let users select another supported model in settings.
- Preserve normal IME startup when model download fails.
- Avoid corrupt or partial model files being treated as valid.
- Keep model-download behavior testable without making tests depend on the public network.

## Non-Goals

- No runtime discovery of arbitrary Hugging Face repositories.
- No user-entered custom model URL in this change.
- No background auto-upgrade of an already downloaded older model unless the selected model changes.
- No removal of the existing Zenzai enable/disable or backend settings.

## Model Catalog

A small built-in catalog will be shared by Rust launcher/config code and the WinUI settings app. Each entry will have:

- `id`: stable config value, for example `zenz-v3.2-small-q5-k-m`.
- `display_name`: Japanese UI label, for example `Zenz v3.2 small (Q5_K_M)`.
- `repo`: Hugging Face repository id.
- `filename`: GGUF file name in the repository.
- `url`: direct `resolve/main/...` download URL.
- `expected_size_bytes`: used as a lightweight sanity check.
- `sha256`: used to verify the completed download before activation.

Exact SHA-256 values will be derived from the referenced GGUF files during implementation and pinned in the production catalog before any download code is enabled.

Initial catalog:

| id | Repository | File |
| --- | --- | --- |
| `zenz-v3.2-small-q5-k-m` | `Miwa-Keita/zenz-v3.2-small-gguf` | `ggml-model-Q5_K_M.gguf` |
| `zenz-v3.1-small-q5-k-m` | `Miwa-Keita/zenz-v3.1-small-gguf` | `ggml-model-Q5_K_M.gguf` |
| `zenz-v3-small-q5-k-m` | `Miwa-Keita/zenz-v3-small-gguf` | `ggml-model-Q5_K_M.gguf` |
| `zenz-v2-q5-k-m` | `Miwa-Keita/zenz-v2-gguf` | `zenz-v2-Q5_K_M.gguf` |

The default model id will be `zenz-v3.2-small-q5-k-m`.

## Config Schema

`ZenzaiConfig` will gain a `model_id` field.

```json
{
  "zenzai": {
    "enable": false,
    "profile": "",
    "backend": "cpu",
    "model_id": "zenz-v3.2-small-q5-k-m"
  }
}
```

Migration behavior:

- Missing `model_id` is treated as the default model id.
- Unknown `model_id` falls back to the default model id at runtime and will be rewritten when the config is saved.
- The config version will be bumped so both Rust and C# migrations make the new field visible in saved settings.

## Storage Layout

Models will be stored under:

```text
%APPDATA%\Azookey\models\<model_id>\<filename>
```

In-progress downloads will use a sibling temporary file with a `.partial` suffix. A model becomes active only after the temporary file passes verification and is atomically renamed into place.

Existing root-level bundled models from older installs will not be used as the primary source. The installer will delete `{app}\zenz.gguf` during upgrade so stale bundled models do not hide download problems.

## Startup Flow

1. `launcher.exe` loads `settings.json`.
2. It resolves the configured model id against the built-in catalog.
3. It ensures the selected model file exists in `%APPDATA%\Azookey\models`.
4. If the file is missing, it downloads the model from the catalog URL.
5. It validates the completed file with size and SHA-256.
6. It starts `azookey-server.exe` and sets `AZOOKEY_ZENZAI_MODEL_PATH` to the resolved model path.
7. Swift server code reads `AZOOKEY_ZENZAI_MODEL_PATH`; if missing, blank, or not an existing file, Zenzai is treated as unavailable and standard conversion continues.

The download is unconditional with respect to `zenzai.enable`, matching the user choice that first app startup should fetch the model automatically.

## Failure Handling

Download failure must not stop the basic IME.

- If download fails and no verified model exists, launcher starts the server without `AZOOKEY_ZENZAI_MODEL_PATH`.
- The server treats Zenzai as unavailable and falls back to standard conversion.
- Launcher writes a diagnostic entry to the existing launcher crash trace/log path.
- Partial files are left only as `.partial` and are overwritten on the next attempt.
- If a corrupt final file is detected, it is moved aside or removed before retrying.

## Settings UI

The WinUI 3 settings page for Zenzai will add a model selector:

- Label: Japanese UI text `モデル`
- Control: ComboBox
- Items: all catalog entries in a stable order, default first.
- Save behavior: changing the combo writes `zenzai.model_id` to config and requests a server restart through the existing restart mechanism.

The existing Tauri settings page will be kept schema-compatible so it does not drop `model_id` when saving settings. The visible WinUI 3 settings page is the primary UI for this feature.

## Build And Installer Changes

- Remove `cp zenz.gguf build` from `Makefile.toml`.
- Keep llama backend runtime directories in the build because the downloaded GGUF still needs them.
- Add installer cleanup for `{app}\zenz.gguf` during upgrade.
- Keep uninstall cleanup for root-level `*.gguf`; model-cache cleanup can remain best-effort because models are redownloadable user cache.

## Testing Strategy

Tests will be written before production changes.

- Rust shared config tests:
  - default config includes the default model id.
  - missing `model_id` deserializes to the default.
  - unknown model id resolution returns the default catalog entry.
- Rust launcher tests:
  - existing verified model is reused without network.
  - missing model downloads to `.partial`, verifies, and atomically promotes.
  - failed download does not produce a final model file.
  - server command receives `AZOOKEY_ZENZAI_MODEL_PATH` only when a verified model path exists.
- C# config/settings tests:
  - default config includes the default model id.
  - legacy JSON missing `model_id` migrates to the default.
  - WinUI settings text includes the Japanese model selector.
- Build/installer tests:
  - build scripts no longer copy `zenz.gguf`.
  - installer removes old root-level `zenz.gguf`.
- Local verification:
  - run targeted Rust and .NET tests.
  - run a local build path that no longer requires a repository-root GGUF.

## Open Decisions Resolved

- Auto-download timing: first app startup, not only first Zenzai use.
- Default model: `Miwa-Keita/zenz-v3.2-small-gguf`.
- Model selection source: built-in supported catalog, not live arbitrary repository discovery.
