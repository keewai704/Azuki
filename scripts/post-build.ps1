param(
    [ValidateSet("debug", "release")]
    [string]$Profile = "debug"
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot/build-common.ps1

New-Item -ItemType Directory -Force build/x86 | Out-Null
$engineRuntime = Join-Path "build" "EngineRuntime"
$swiftRuntime = Join-Path $engineRuntime "Swift"
Remove-Item -LiteralPath $engineRuntime -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "build/llama_vulkan" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $swiftRuntime | Out-Null
$rootRuntimePatterns = @(
    "azookey-server.dll",
    "_Concurrency.dll",
    "_FoundationICU.dll",
    "BlocksRuntime.dll",
    "dispatch*.dll",
    "Foundation*.dll",
    "IndexStoreDB.dll",
    "swift*.dll",
    "Testing.dll"
)
Get-ChildItem -LiteralPath "build" -File -ErrorAction SilentlyContinue | Where-Object {
    $name = $_.Name
    $rootRuntimePatterns | Where-Object { $name -like $_ } | Select-Object -First 1
} | Remove-Item -Force

Copy-Item target/$Profile/azookey-server.exe build -Force
Copy-Item target/$Profile/azookey_windows.dll build -Force
Copy-Item target/$Profile/launcher.exe build -Force

Copy-Item target/i686-pc-windows-msvc/$Profile/azookey_windows.dll build/x86 -Force

Copy-Item server-swift/.build/x86_64-unknown-windows-msvc/release/azookey-server.dll $swiftRuntime -Force

Copy-Item -Recurse -Force llama_vulkan (Join-Path $engineRuntime "llama_vulkan")

Copy-Item $env:APPDATA/../Local/Programs/Swift/Runtimes/*/usr/bin/* $swiftRuntime -Force

Copy-Item -Recurse -Force server-swift/azooKey_emoji_dictionary_storage/EmojiDictionary build
Copy-Item -Recurse -Force server-swift/azooKey_dictionary_storage/Dictionary build

Invoke-Native icacls build/azookey_windows.dll /grant "*S-1-15-2-1:(RX)"
Invoke-Native icacls build/x86/azookey_windows.dll /grant "*S-1-15-2-1:(RX)"
