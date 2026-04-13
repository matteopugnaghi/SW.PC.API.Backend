@echo off
:: ============================================================================
::  Lanzar Configure-NTP.ps1 — IPC SERVER (A72.TOUTWP)
::  NTP: IPC CLIENT 192.168.1.162 (red interna /30)
:: ============================================================================

:: Auto-elevate to Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
    exit /b
)

cd /d "%~dp0"

powershell -ExecutionPolicy Bypass -File "..\..\NTP\Configure-NTP.ps1" ^
    -Role Server ^
    -NtpServer 192.168.1.162 ^
    -PollIntervalSeconds 900 ^
    -Language FRA

pause
