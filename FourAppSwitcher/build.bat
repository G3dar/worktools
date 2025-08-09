@echo off
setlocal
cd /d "%~dp0"
echo Restoring and publishing FourAppSwitcher (Release, win-x64, single-file, self-contained)...
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
for %%A in (bin\Release\net8.0-windows\win-x64\publish\FourAppSwitcher.exe) do set "OUT=%%~fA"
echo Build succeeded. EXE at:
echo   %OUT%
exit /b 0


