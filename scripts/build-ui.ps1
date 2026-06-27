$ErrorActionPreference = "Stop"
. $PSScriptRoot/build-common.ps1

$root = Resolve-Path "."
$buildDir = Join-Path $root "build"
$releaseDir = Join-Path $root "target/release"
New-Item -ItemType Directory -Force $buildDir | Out-Null
Get-ChildItem -LiteralPath $buildDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

Invoke-Native -FilePath cargo -Arguments @("build", "-p", "azookey-ui", "--bins", "--release")

$requiredFiles = @(
    "ui.exe",
    "settings.exe",
    "Microsoft.WindowsAppRuntime.Bootstrap.dll",
    "resources.pri"
)

foreach ($fileName in $requiredFiles) {
    $source = Join-Path $releaseDir $fileName
    if (!(Test-Path -LiteralPath $source)) {
        throw "Rust UI build output was not found: $source"
    }

    Copy-Item -LiteralPath $source -Destination $buildDir -Force
}
