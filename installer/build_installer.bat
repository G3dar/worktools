@echo off
setlocal
set "ROOT=%~dp0.."
cd /d "%ROOT%"

:: Usage: installer\build_installer.bat [version]
set "REQ_VERSION=%~1"
if not "%REQ_VERSION%"=="" (
  set "ISCC_DEFINE=/dMyAppVersion=%REQ_VERSION%"
)

echo ===== Building AppSwitcher publish (Release, win-x64) =====
call apps\AppSwitcher\build.bat || exit /b 1

set "PUBLISH_EXE=%CD%\apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%PUBLISH_EXE%" set "PUBLISH_EXE=%CD%\apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%PUBLISH_EXE%" (
  echo ERROR: Published EXE not found after build.
  exit /b 1
)

echo ===== Compiling installer with Inno Setup =====
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo ERROR: Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php
  exit /b 1
)

pushd installer
"%ISCC%" %ISCC_DEFINE% appswitcher.iss || (popd & exit /b 1)

:: Find newest installer output
set "OUTFILE="
for /f "delims=" %%A in ('dir /b /od Output\AppSwitcher-*-Setup.exe 2^>nul') do set "OUTFILE=Output\%%A"
if not defined OUTFILE for /f "delims=" %%A in ('dir /b /od AppSwitcher-*-Setup.exe 2^>nul') do set "OUTFILE=%%A"
if not defined OUTFILE (
  popd
  echo ERROR: Installer EXE not found in installer\ (nor in installer\Output\)
  exit /b 1
)
set "OUTFULL=%CD%\%OUTFILE%"
popd

echo.
echo Success. Installer built:
echo   %OUTFULL%
exit /b 0


