@echo off
setlocal
cd /d "%~dp0"

rem Prefer published single-file EXE under x64 path
set "EXE=%CD%\bin\x64\Release\net8.0-windows\win-x64\publish\FourAppSwitcher.exe"
if not exist "%EXE%" set "EXE=%CD%\bin\Release\net8.0-windows\win-x64\publish\FourAppSwitcher.exe"
if not exist "%EXE%" set "EXE=%CD%\bin\x64\Release\net8.0-windows\win-x64\FourAppSwitcher.exe"
if not exist "%EXE%" set "EXE=%CD%\bin\Release\net8.0-windows\win-x64\FourAppSwitcher.exe"

if not exist "%EXE%" (
  call build.bat || exit /b 1
  rem re-resolve after build
  set "EXE=%CD%\bin\x64\Release\net8.0-windows\win-x64\publish\FourAppSwitcher.exe"
  if not exist "%EXE%" set "EXE=%CD%\bin\Release\net8.0-windows\win-x64\publish\FourAppSwitcher.exe"
  if not exist "%EXE%" set "EXE=%CD%\bin\x64\Release\net8.0-windows\win-x64\FourAppSwitcher.exe"
  if not exist "%EXE%" set "EXE=%CD%\bin\Release\net8.0-windows\win-x64\FourAppSwitcher.exe"
)

if not exist "%EXE%" (
  echo Could not locate FourAppSwitcher.exe after build.
  exit /b 1
)

echo Stopping any running FourAppSwitcher.exe ...
taskkill /F /IM FourAppSwitcher.exe >nul 2>nul
timeout /t 1 /nobreak >nul 2>&1

echo Starting: "%EXE%"
start "FourAppSwitcher" "%EXE%"
exit /b 0


