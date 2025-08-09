@echo off
setlocal
cd /d "%~dp0"
echo Restoring and publishing AppSwitcher (Release, win-x64, single-file, self-contained)...
taskkill /F /IM AppSwitcher.exe >nul 2>nul
timeout /t 1 /nobreak >nul 2>&1
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false
if errorlevel 1 exit /b 1
for %%A in (bin\x64\Release\net8.0-windows\win-x64\publish\AppSwitcher.exe) do set "OUT=%%~fA"
echo Built: %OUT%
exit /b 0

