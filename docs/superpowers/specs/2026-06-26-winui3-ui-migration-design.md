# WinUI 3 UI Migration Design

## Goal

IME の変換候補ウィンドウ、ライブ変換中の読み表示、入力モードインジケータ、設定 UI をすべて WinUI 3 / XAML に置き換える。既存の IME 本体、変換サーバー、ランチャー、インストーラーとの互換性を保ちながら、Tauri/React と wry/HTML ベースの UI を廃止する。

## Approved Decisions

- 実装言語は C# / .NET とする。
- Visual Studio Build Tools 2026 の MSBuild を使う。
- `ui.exe` と `frontend.exe` のファイル名は既存互換のため維持する。
- 方式はフル C# WinUI 3 化とし、Rust shared bridge は使わない。
- Windows App SDK は Runtime 前提方式で配布する。
- .NET は self-contained publish とし、利用者に .NET Desktop Runtime の別途導入を要求しない。
- 最終成果物では Tauri/React と wry/HTML UI をすべて置き換える。

## Current System

現行の候補 UI は `crates/ui` にあり、`tao` で非アクティブ・最前面の Win32 ウィンドウを作り、`wry` WebView に HTML/CSS/JavaScript を載せている。`ui.exe` は `azookey_ui` named pipe gRPC server を公開し、IME client から `window.proto` の `WindowService` RPC を受ける。

候補 UI は3つの表示を持つ。

- 候補リスト: 変換候補、選択行、最大5件表示、高さ調整
- ルビ表示: ライブ変換中の読み、入力位置周辺への配置
- 入力モードインジケータ: `あ` / `A` 表示、短時間表示

現行の設定 UI は `frontend` にあり、Tauri + React で `frontend.exe` を生成する。Tauri command は Rust 側で `AppConfig` を読み書きし、保存後に `azookey_server` へ `UpdateConfig` を送る。言語バー右クリックは `frontend.exe` を探して起動する。

`launcher.exe` は `azookey-server.exe` と `ui.exe` を起動し、server restart 用の `azookey_launcher` pipe を持つ。Inno installer は `frontend.exe`、`ui.exe`、IME DLL、server、launcher、辞書、Zenzai model、llama backend を `{userappdata}\Azookey` へ配置する。

## Target Architecture

### `apps/Azookey.UI`

C# WinUI 3 の `ui.exe` 後継アプリ。既存 `azookey_ui` named pipe gRPC server をホストし、`crates/shared/window.proto` と同じ `WindowService` API を実装する。

内部では以下を WinUI 3 の複数トップレベルウィンドウとして管理する。

- Candidate window
- Ruby reading window
- Input mode indicator window

各ウィンドウは Win32 interop で `WS_EX_NOACTIVATE`、`WS_EX_TOOLWINDOW`、`WS_EX_TOPMOST`、`WS_POPUP` 相当の挙動を維持する。表示時は `ShowWindow(..., SW_SHOWNOACTIVATE)` と `SetWindowPos(..., HWND_TOPMOST, ..., SWP_NOACTIVATE)` 相当を使い、入力中アプリからフォーカスを奪わない。

IME client 側 Rust は原則変更しない。既存の `azookey_ui` pipe と proto 契約を維持することで、候補 UI の実装だけを置き換える。

### `apps/Azookey.Settings`

C# WinUI 3 の `frontend.exe` 後継アプリ。現行 React/Tauri の設定画面を XAML で再実装する。

ページ構成は現行と同じにする。

- 全般
- Zenzai
- 辞書
- デバッグ用設定
- Azookey について

`%APPDATA%\Azookey\settings.json` の JSON schema は既存互換を維持する。Rust `shared::AppConfig` と同じ既定値、設定復旧、破損ファイル退避、保存挙動を C# に移植する。

保存時は一時ファイルへ JSON を書き、成功後に置換する。保存後、`azookey_server` へ既存 `service.proto` の `UpdateConfig` を送る。server 通知に失敗しても設定保存自体は成功扱いにし、WinUI の InfoBar で「保存済みだが IME への即時反映に失敗した」ことを表示する。

### `apps/Azookey.Core`

両アプリで共有する C# ライブラリ。

責務は以下。

- `AppConfig` model と JSON serializer
- 既定値生成
- `settings.json` の読み書き、破損復旧、バックアップ命名
- `window.proto` / `service.proto` の named pipe gRPC helper
- Windows App SDK Runtime 検出
- Win32 window interop helper
- update checker / installer download / SHA256 verification
- launcher restart pipe helper

### Protobuf

既存の `crates/shared/window.proto` と `crates/shared/service.proto` を C# project から参照する。Rust/C# の両方が同じ proto を使うことで IPC 契約を固定する。

### Build And Installer

`Makefile.toml` の Tauri build step を C# publish step に置き換える。`frontend.exe` と `ui.exe` は .NET self-contained publish で生成する。

Inno installer は以下を更新する。

- Windows App SDK Runtime の導入チェック/インストールを追加する。
- WebView2 Runtime は不要になるため依存関係から削除する。
- `frontend.exe.WebView2` と `ui.exe.WebView2` cleanup を削除する。
- `MainBinaryName=frontend.exe` は維持する。
- `frontend.exe` と `ui.exe` の taskkill 対象名は維持する。

Windows App SDK Runtime 前提方式は Microsoft の unpackaged WinUI 3 deployment guidance に従う。`.NET` は self-contained publish とし、Windows App SDK Runtime だけを installer で面倒見る。

## Candidate UI Behavior

`UpdateCandidateWindow` は UI thread 外で受信し、WinUI dispatcher に state update として渡す。UI state は reducer 形式で管理し、RPC の部分更新を deterministic に適用する。

候補ウィンドウは以下を維持する。

- 候補リストの最大5件表示
- 選択候補のハイライト
- 選択候補が見えるように5件単位でスクロール
- 候補文字列長に応じた横幅計算
- 候補リスト非表示時は候補ウィンドウを隠し、必要に応じてルビ表示だけを残す
- monitor work area からはみ出さない配置
- ルビ表示と候補ウィンドウが重なる場合の退避配置

ルビウィンドウは読み文字列の実測サイズを WinUI text layout から得て、既存と同じ work area clamp を行う。

入力モードインジケータは `SetInputMode` / `UpdateCandidateWindow.input_mode` に反応し、既存と同じく短時間だけ非アクティブ最前面表示する。

## Settings UI Behavior

起動時:

- `settings.json` を読み込む。
- 存在しない場合は既定値を作成する。
- JSON が破損している場合は `settings.json.broken-<timestamp>` へ退避し、既定値で起動する。
- 復旧や読み込み失敗は InfoBar で表示する。

保存時:

- 画面 state を `AppConfig` model に反映する。
- 一時ファイルへ JSON を書く。
- 既存 `settings.json` と置換する。
- `azookey_server` へ `UpdateConfig` を送る。
- server 通知失敗時は保存成功 + 反映警告として扱う。

Zenzai:

- CPU capability は AVX 対応を C# 側で判定する。
- CUDA は `cudart64_12.dll` と `cublas64_12.dll` を PATH と current directory から探す。
- Vulkan は `vulkan-1.dll` を PATH と current directory から探す。

辞書:

- 既存の最大50件制限を維持する。
- MVP として import/export は引き続き対象外とする。

更新:

- GitHub latest release API を読む。
- `azookey-setup.exe` と `SHA256SUMS.txt` を選択する。
- SHA256 を検証してから installer を起動する。
- 結果は次回起動時に通知する。

## Error Handling

- `azookey_ui` pipe が busy の場合は短時間 retry する。
- `azookey_ui` が起動していなくても IME 本体は継続し、Rust client 側の既存挙動を保つ。
- `ui.exe` の window 作成失敗はログへ出し、process は異常終了する。
- 設定保存失敗はユーザーへ明示する。
- server 通知失敗は保存成功と区別して警告する。
- Windows App SDK Runtime 不足は installer で解消する。直接 exe 起動時に Runtime が不足していた場合は、プロセス起動直後にログへ記録し、WinUI 初期化前に表示できる Win32 message box で説明して終了する。

## Testing Strategy

TDD で実装する。C# 側の production code は対応する failing test を先に作る。

### `Azookey.Core.Tests`

- `AppConfig` 既定値が Rust 側と一致する
- `settings.json` が存在しない場合に既定値を作成する
- 破損 JSON を backup し、既定値で復旧する
- 保存は一時ファイル経由で行われる
- Windows App SDK Runtime 検出結果を injectable にしてテストできる
- release asset selection が `azookey-setup.exe` と `SHA256SUMS.txt` を選ぶ
- SHA256 mismatch を拒否する
- launcher response `ok` を受け入れ、`error:` を拒否する

### `Azookey.UI.Tests`

- candidate window position が既存 Rust tests と同じ結果になる
- ruby window position が既存 Rust tests と同じ結果になる
- ruby width が work area と DPI scale で clamp される
- candidate/ruby overlap 時に候補ウィンドウが退避する
- candidate height が5件分 + footer + padding になる
- selection index が範囲外でも安全に clamp される
- `UpdateCandidateWindow` が部分更新を正しく merge する
- hidden candidate list が candidate window を隠し、ruby state を維持する

### `Azookey.Settings.Tests`

- `update_config` 相当処理が保存成功/通知成功を返す
- server unavailable でも保存成功 + 反映警告になる
- 保存失敗時は state を成功扱いにしない
- restart-server pipe response parsing
- update result notification が一度だけ消費される

### Integration Verification

- C# `ui.exe` を起動し、`azookey_ui` pipe に接続できる
- Rust IME client から `UpdateCandidateWindow` を送って候補ウィンドウ state が更新される
- C# `frontend.exe` を起動し、設定読み込みと保存ができる
- 保存後に `azookey_server` へ `UpdateConfig` が送られる
- Inno installer が Windows App SDK Runtime を導入または既存導入済みとして通過する
- インストール後に `launcher.exe` が C# `ui.exe` を起動する

## Non-Goals

- IME の変換ロジック変更
- `azookey_server` の Swift/Rust FFI 変更
- `window.proto` / `service.proto` の破壊的変更
- 設定 JSON schema の破壊的変更
- 辞書 import/export の新規追加
- Tauri/React 画面の段階併存を最終成果物に残すこと

## Open Constraints

- Visual Studio Build Tools 2026 は存在するが、現環境では .NET SDK / Windows App SDK / WinUI workload が未導入に見える。実装計画では Build Tools 2026 に必要コンポーネントを追加する手順を含める。
- WinUI 3 の unpackaged runtime 方式では Windows App SDK Runtime の導入が必要になる。Inno installer で silent install を行う。
- `frontend.exe` と `ui.exe` のファイル名は互換性のため変更しない。
