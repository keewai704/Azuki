# Azookey UI

This crate builds the native WinUI 3 executables:

- `ui.exe`: candidate, reading, and input-mode UI backed by the `azookey_ui` named pipe.
- `settings.exe`: native settings shell.

The UI is implemented in Rust with `windows-rs` / `windows-reactor`. It does not use WebView, Tauri, React, C#, or .NET.
