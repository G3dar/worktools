@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%apps\AppSwitcher"

echo Restoring and publishing AppSwitcher (Release, win-x64, single-file, self-contained)...
echo Ensuring no running instance is locking the publish output...
taskkill /F /IM AppSwitcher.exe >nul 2>nul
timeout /t 1 /nobreak >nul 2>&1
del /f /q "%CD%\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe" >nul 2>nul
del /f /q "%CD%\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe" >nul 2>nul
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
set "OUT=%CD%\bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
if not exist "%OUT%" set "OUT=%CD%\bin\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe"
echo Build succeeded. EXE at:
echo   %OUT%

echo Launching AppSwitcher...
start "AppSwitcher" "%OUT%"
exit /b 0


