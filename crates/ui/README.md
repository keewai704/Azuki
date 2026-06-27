# Azookey UI

This crate builds the native WinUI 3 executables:

- `ui.exe`: non-activating candidate popup backed by the `azookey_ui` named pipe. Reading text is rendered only inside this popup.
- `settings.exe`: native PowerToys-style settings shell.

The UI is implemented in Rust with `windows-rs` / `windows-reactor`. It does not use WebView, Tauri, React, C#, or .NET.
