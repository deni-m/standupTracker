@echo off
set "SOLUTION_DIR=%~dp0"
set "APP_EXE=StandUpTracker\bin\Release\net8.0-windows\win-x64\StandUpTracker.exe"

echo ---------------------------------------------------
echo  StandUpTracker Build & Run Script
echo ---------------------------------------------------

:: 1. Kill existing process if running
echo.
echo [1/3] Checking for running process...
tasklist /FI "IMAGENAME eq StandUpTracker.exe" 2>NUL | find /I /N "StandUpTracker.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo       Process found. Terminating...
    taskkill /F /IM StandUpTracker.exe >NUL 2>&1
    if "%ERRORLEVEL%"=="0" (
        echo       Process terminated successfully.
    ) else (
        echo       Failed to terminate process. Please close it manually.
        pause
        exit /b 1
    )
    :: Wait a moment for file handles to release
    timeout /t 1 /nobreak >NUL
) else (
    echo       Process not running.
)

:: 2. Build Solution
echo.
echo [2/3] Building solution (Release)...
dotnet build -c Release
if "%ERRORLEVEL%" NEQ "0" (
    echo.
    echo [ERROR] Build failed! Check errors above.
    pause
    exit /b %ERRORLEVEL%
)

:: 3. Run Application
echo.
echo [3/3] Starting application...
if exist "%APP_EXE%" (
    start "" "%APP_EXE%"
    echo       Application started successfully!
) else (
    echo.
    echo [ERROR] Executable not found at:
    echo       %APP_EXE%
    pause
    exit /b 1
)

:: Optional: Close this window automatically
timeout /t 3
