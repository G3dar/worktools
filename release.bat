@echo off
setlocal EnableExtensions EnableDelayedExpansion

:: Release script: tags the repo and creates a GitHub release with assets.
:: Usage: release.bat <version> [notesFile]
::   version   - e.g. 0.1.1 (the tag will be v0.1.1)
::   notesFile - optional path to a markdown/text file for release notes

:: --- Elevate to Administrator if not already ---
whoami /groups | find "S-1-5-32-544" >nul 2>nul
if errorlevel 1 (
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -ArgumentList '%*' -Verb RunAs" >nul 2>&1
  exit /b
)

set "ROOT=%~dp0"
cd /d "%ROOT%"

if "%~1"=="" (
  echo Usage: %~nx0 ^<version^> [notesFile]
  echo   Example: %~nx0 0.1.1 dist\release-notes-v0.1.1.txt
  exit /b 1
)

set "VERSION=%~1"
set "TAG=v%VERSION%"
set "NOTES=%~2"

:: Ensure git is available
where git >nul 2>nul || (echo ERROR: git not found on PATH.& exit /b 1)

:: Determine current branch
for /f "usebackq tokens=*" %%B in (`git rev-parse --abbrev-ref HEAD`) do set "BRANCH=%%B"
if "%BRANCH%"=="" set "BRANCH=main"

echo.
echo ===== Pushing branch %BRANCH% and tag %TAG% =====
git push origin "%BRANCH%" || exit /b 1

:: Create or update tag
git tag -a "%TAG%" -m "AppSwitcher v%VERSION%" 2>nul
git push origin "%TAG%" || exit /b 1

:: Gather assets (all zips in dist\)
set "ASSETS="
if exist "dist" (
  for %%F in ("dist\*.zip") do set "ASSETS=!ASSETS! "%%~fF""
)

:: Ensure GitHub CLI exists
where gh >nul 2>nul || (
  echo INFO: gh (GitHub CLI) not found. Tag has been pushed.
  echo To create a release manually, run:
  echo   gh release create %TAG% %ASSETS% -t "AppSwitcher v%VERSION%" -F "^<notesFile^>"
  exit /b 0
)

:: Prepare notes file if not provided
if not defined NOTES (
  set "NOTES=dist\release-notes-%TAG%.txt"
  mkdir dist >nul 2>nul
  >"%NOTES%" (
    echo See release notes: https://lazy.toys/docs/appswitcher/releases/%VERSION%/
    echo.
    echo Includes:
    echo - Latest AppSwitcher build and packaging updates
  )
)

:: Check if release already exists
gh release view "%TAG%" >nul 2>nul
if errorlevel 1 (
  echo Creating GitHub release %TAG% ...
  gh release create "%TAG%" %ASSETS% -t "AppSwitcher v%VERSION%" -F "%NOTES%" || exit /b 1
) else (
  echo Release exists. Uploading assets ...
  if defined ASSETS gh release upload "%TAG%" %ASSETS% --clobber || echo WARN: asset upload failed or none found.
)

echo.
echo Done. View release at:
gh release view "%TAG%" --web >nul 2>nul
echo   Tag: %TAG%
exit /b 0


