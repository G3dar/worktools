@echo off
setlocal
cd /d "%~dp0"
set "EXE=%CD%\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%EXE%" call build.bat || exit /b 1
taskkill /F /IM AppSwitcher.exe >nul 2>nul
timeout /t 1 /nobreak >nul 2>&1
start "AppSwitcher" "%EXE%"
exit /b 0

