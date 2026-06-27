# Final Review Fix Report

## Changed files

- `frontend/src/pages/zenzai.tsx`
- `frontend/src/pages/general.tsx`
- `frontend/src-tauri/src/lib.rs`
- `apps/Azookey.Settings.Tests/ViewModels/LegacyFrontendSourceGuardsTests.cs`

## What changed

- Removed legacy CUDA capability exposure from the old Tauri backend command and kept only CPU/Vulkan capability reporting.
- Removed the old Tauri Zenzai page CUDA backend option and normalized any legacy backend value to the Vulkan option without surfacing forbidden text.
- Removed the old Tauri General page live-conversion reading state, update helpers, and visible controls.
- Added a focused source guard test that scans the legacy frontend sources for the forbidden CUDA and live-conversion control strings without introducing those literals into the test source.

## Verification

- `npm run build` in `C:/azw-winui/frontend`: PASS
  - Ran `npm ci` first because `node_modules` was absent locally and `tsc` was not available before dependency install.
- `cargo test -p frontend --lib`: PASS
- `dotnet test C:/azw-winui/apps/Azookey.Settings.Tests/Azookey.Settings.Tests.csproj --filter LegacyFrontendSourceGuardsTests`: PASS
- `rg -n "CUDA \(|CUDA Toolkit|cudart64_12|cublas64_12|capability\.cuda|show_live_conversion_reading|live_conversion_reading_vertical_adjustment|ライブ変換中の読み|読み表示の高さ" C:/azw-winui/frontend/src C:/azw-winui/frontend/src-tauri C:/azw-winui/apps/Azookey.Core.Tests C:/azw-winui/apps/Azookey.Settings.Tests`: no matches
