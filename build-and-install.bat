@echo off
setlocal
title Project App Organizer - build and install
cd /d "%~dp0"

echo ============================================================
echo   Project App Organizer - build, publish, desktop shortcut
echo ============================================================
echo.

rem --- Locate a dotnet that has the .NET 10 SDK. The one on PATH may be runtime-only,
rem --- so every usual location is checked until one lists a 10.x SDK.
set "DOTNET="
for /f "delims=" %%D in ('where dotnet 2^>nul') do call :try_dotnet "%%D"
call :try_dotnet "%USERPROFILE%\.dotnet\dotnet.exe"
call :try_dotnet "%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET goto :no_sdk

rem --- The published files are locked while the app runs ---
tasklist /fi "IMAGENAME eq ProjectAppOrganizer.App.exe" /fo csv /nh 2>nul | findstr /i /c:"ProjectAppOrganizer.App" >nul
if not errorlevel 1 goto :running

set "PAO_DIR=%~dp0publish"
set "PAO_EXE=%PAO_DIR%\ProjectAppOrganizer.App.exe"

echo [1/2] Building and publishing to:
echo       %PAO_DIR%
echo.
"%DOTNET%" publish "%~dp0src\ProjectAppOrganizer.App" -c Release -r win-x64 --self-contained -o "%PAO_DIR%" --nologo
if errorlevel 1 goto :build_failed
if not exist "%PAO_EXE%" goto :build_failed

echo.
echo [2/2] Creating the desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Project App Organizer.lnk'; $s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk); $s.TargetPath = $env:PAO_EXE; $s.WorkingDirectory = $env:PAO_DIR; $s.IconLocation = $env:PAO_EXE + ',0'; $s.Description = 'Project App Organizer'; $s.Save(); Write-Host ('      ' + $lnk)"
if errorlevel 1 goto :shortcut_failed

echo.
echo Done. Start it from the "Project App Organizer" shortcut on your desktop.
goto :end

:try_dotnet
if defined DOTNET exit /b 0
if not exist "%~1" exit /b 0
"%~1" --list-sdks 2>nul | findstr /b /c:"10." >nul
if not errorlevel 1 set "DOTNET=%~1"
exit /b 0

:no_sdk
echo The .NET 10 SDK was not found on this PC.
echo.
echo Install it, then run this script again:
echo   winget install Microsoft.DotNet.SDK.10
echo or download it from:
echo   https://dotnet.microsoft.com/download/dotnet/10.0
goto :failed

:running
echo Project App Organizer is running.
echo Close it first, then run this script again.
goto :failed

:build_failed
echo.
echo The build failed - see the messages above.
goto :failed

:shortcut_failed
echo.
echo The app was published, but the desktop shortcut could not be created.
echo You can start it directly from:
echo   %PAO_EXE%
goto :failed

:failed
echo.
pause
exit /b 1

:end
echo.
pause
exit /b 0
