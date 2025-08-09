@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

rem Resolve candidate exe paths (pre-build)
set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\AppSwitcher.exe"

if not exist "%EXE%" (
  call "%SCRIPT_DIR%build.bat" || exit /b 1
)

rem Re-resolve after build to prefer publish x64
set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\x64\Release\net8.0-windows\win-x64\AppSwitcher.exe"
if not exist "%EXE%" set "EXE=%SCRIPT_DIR%apps\AppSwitcher\bin\Release\net8.0-windows\win-x64\AppSwitcher.exe"

if not exist "%EXE%" (
  echo Could not locate FourAppSwitcher.exe after build.
  exit /b 1
)

echo Stopping any running FourAppSwitcher.exe ...
taskkill /F /IM FourAppSwitcher.exe >nul 2>nul
timeout /t 1 /nobreak >nul 2>&1

for %%A in ("%EXE%") do set "EXE_DIR=%%~dpA"
set "PATH=%EXE_DIR%;%PATH%"

echo Starting: "%EXE%"
start "FourAppSwitcher" "%EXE%"
exit /b 0


