$ErrorActionPreference = 'Stop'
# Portable install: Chocolatey will shim any EXE in tools\
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe = Join-Path $toolsDir 'AppSwitcher.exe'
if (!(Test-Path $exe)) {
  throw "Missing AppSwitcher.exe in tools folder"
}
Write-Host "AppSwitcher installed. A shim is created so you can run 'appswitcher' from Start/Run/terminal."

