@echo off
REM Saman Device Adapter - Uninstallation Script
REM This script uninstalls the Windows Service
REM Run this as Administrator

setlocal enabledelayedexpansion

echo.
echo ===================================
echo Saman Attendance Sync Service
echo Uninstallation Script
echo ===================================
echo.

REM Get the directory where this batch file is located
set BATCH_DIR=%~dp0
set EXE_PATH=%BATCH_DIR%SamanDeviceAdapter.exe

REM Check if the executable exists
if not exist "%EXE_PATH%" (
    echo ERROR: SamanDeviceAdapter.exe not found in %BATCH_DIR%
    echo Please ensure the executable is in the same directory as this script.
    echo.
    pause
    exit /b 1
)

REM Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo Please run as Administrator.
    echo.
    pause
    exit /b 1
)

echo Uninstalling service...
echo.

REM Run the executable with --uninstall argument
"%EXE_PATH%" --uninstall

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Uninstallation failed.
    echo Please check the logs for more information.
    echo.
    pause
    exit /b 1
)

echo.
echo ===================================
echo Uninstallation completed successfully!
echo ===================================
echo.
echo The service has been removed.
echo.
pause
