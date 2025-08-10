@echo off
setlocal
REM Optional helper to rebuild the Chocolatey package after building the app
REM Requires: choco CLI

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo [Chocolatey] Ensuring published EXE exists...
call apps\AppSwitcher\build.bat || exit /b 1

echo [Chocolatey] Packing nuspec...
pushd packaging
if exist appswitcher.*.nupkg del /q appswitcher.*.nupkg >nul 2>nul
choco pack || (popd & exit /b 1)
for /f "delims=" %%A in ('dir /b /od appswitcher.*.nupkg') do set "NUPKG=%%A"
echo Built: %CD%\%NUPKG%
popd
exit /b 0


