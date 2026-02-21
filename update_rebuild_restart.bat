@echo off
setlocal ENABLEDELAYEDEXPANSION

set "ROOT_DIR=%~dp0"
set "SOLUTION_FILE=%ROOT_DIR%StandUpTracker.sln"
set "APP_EXE_PRIMARY=%ROOT_DIR%StandUpTracker\bin\Release\net8.0-windows\win-x64\StandUpTracker.exe"
set "APP_EXE_FALLBACK=%ROOT_DIR%StandUpTracker\bin\Release\net8.0-windows\StandUpTracker.exe"

echo ---------------------------------------------------
echo  StandUpTracker Update + Rebuild + Restart
echo ---------------------------------------------------
echo.

cd /d "%ROOT_DIR%"

where git >NUL 2>&1
if errorlevel 1 (
    echo [ERROR] git is not installed or not in PATH.
    pause
    exit /b 1
)

where dotnet >NUL 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet SDK is not installed or not in PATH.
    pause
    exit /b 1
)

echo [1/4] Stopping running StandUpTracker (if any)...
tasklist /FI "IMAGENAME eq StandUpTracker.exe" 2>NUL | find /I "StandUpTracker.exe" >NUL
if not errorlevel 1 (
    taskkill /F /IM StandUpTracker.exe >NUL 2>&1
    if errorlevel 1 (
        echo [ERROR] Could not stop StandUpTracker.exe. Close it manually and retry.
        pause
        exit /b 1
    )
    timeout /t 1 /nobreak >NUL
)

echo [2/4] Pulling latest code from GitHub...
for /f %%b in ('git symbolic-ref --short refs/remotes/origin/HEAD 2^>NUL') do set "DEFAULT_REMOTE_BRANCH=%%b"
if not defined DEFAULT_REMOTE_BRANCH set "DEFAULT_REMOTE_BRANCH=origin/main"
set "DEFAULT_REMOTE_BRANCH=!DEFAULT_REMOTE_BRANCH:origin/=!"

git fetch origin
if errorlevel 1 (
    echo [ERROR] git fetch failed.
    pause
    exit /b 1
)

git pull --ff-only origin !DEFAULT_REMOTE_BRANCH!
if errorlevel 1 (
    echo [ERROR] git pull failed (possibly local changes or diverged branch).
    echo         Resolve git state, then run this script again.
    pause
    exit /b 1
)

echo [3/4] Building solution (Release)...
dotnet build "%SOLUTION_FILE%" -c Release
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo [4/4] Starting StandUpTracker...
if exist "%APP_EXE_PRIMARY%" (
    start "" "%APP_EXE_PRIMARY%"
    echo [OK] Started: %APP_EXE_PRIMARY%
    goto :done
)

if exist "%APP_EXE_FALLBACK%" (
    start "" "%APP_EXE_FALLBACK%"
    echo [OK] Started: %APP_EXE_FALLBACK%
    goto :done
)

echo [ERROR] Executable not found after build.
echo         Checked:
echo         %APP_EXE_PRIMARY%
echo         %APP_EXE_FALLBACK%
pause
exit /b 1

:done
echo.
echo Completed successfully.
timeout /t 2 /nobreak >NUL
exit /b 0
