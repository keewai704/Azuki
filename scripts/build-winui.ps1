$ErrorActionPreference = "Stop"
. $PSScriptRoot/build-common.ps1

$root = Resolve-Path "."
$publish = Join-Path $root "build"
New-Item -ItemType Directory -Force $publish | Out-Null

function Copy-XamlArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    $source = Join-Path $ProjectDirectory "bin/Release/net10.0-windows10.0.19041.0/win-x64"
    if (!(Test-Path $source)) {
        throw "WinUI XAML output was not found: $source"
    }

    $sourceRoot = (Resolve-Path $source).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    foreach ($filter in @("*.xbf", "*.pri")) {
        Get-ChildItem -LiteralPath $source -Recurse -Filter $filter | ForEach-Object {
            $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
            $target = Join-Path $Destination $relative
            $targetDirectory = Split-Path -Parent $target
            New-Item -ItemType Directory -Force $targetDirectory | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        }
    }
}

$uiExe = Join-Path $publish "ui.exe"
$settingsExe = Join-Path $publish "settings.exe"
Get-ChildItem -LiteralPath $publish -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

Invoke-Native dotnet publish apps/Azookey.UI/Azookey.UI.csproj -c Release -r win-x64 --self-contained false "-p:PublishDir=$publish/"
Copy-XamlArtifacts (Join-Path $root "apps/Azookey.UI") $publish

Invoke-Native dotnet publish apps/Azookey.Settings/Azookey.Settings.csproj -c Release -r win-x64 --self-contained false "-p:PublishDir=$publish/"
Copy-XamlArtifacts (Join-Path $root "apps/Azookey.Settings") $publish

if (!(Test-Path $uiExe)) { throw "ui.exe was not published" }
if (!(Test-Path $settingsExe)) { throw "settings.exe was not published" }
