$ErrorActionPreference = "Stop"
. $PSScriptRoot/build-common.ps1

$iscc = Resolve-InnoSetupCompiler
Invoke-Native $iscc "./installer/Installer.iss"
