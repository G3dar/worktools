@echo off
setlocal
REM Build publish output and compile the Inno Setup installer.

set "ROOT=%~dp0"
cd /d "%ROOT%"

echo ===== AppSwitcher: Package (build + installer) =====
echo.

echo [1/3] Building published EXE...
call apps\AppSwitcher\build.bat || (
  echo Build failed.
  exit /b 1
)

set "PUBLISH_EXE=%CD%\apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%PUBLISH_EXE%" set "PUBLISH_EXE=%CD%\apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%PUBLISH_EXE%" (
  echo ERROR: Published EXE not found.
  goto :err
)

echo [2/3] Locating Inno Setup Compiler (ISCC.exe)...
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo ERROR: Inno Setup 6 not found. Please install it from https://jrsoftware.org/isinfo.php
  exit /b 1
)

echo [3/3] Compiling installer...
pushd installer
"%ISCC%" appswitcher.iss || (
  popd
  echo Inno Setup compilation failed.
  exit /b 1
)

REM Find newest installer file (Inno places it under Output by default)
set "OUTFILE="
for /f "delims=" %%A in ('dir /b /od Output\AppSwitcher-*-Setup.exe 2^>nul') do set "OUTFILE=Output\%%A"
if not defined OUTFILE (
  for /f "delims=" %%A in ('dir /b /od AppSwitcher-*-Setup.exe 2^>nul') do set "OUTFILE=%%A"
)
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


