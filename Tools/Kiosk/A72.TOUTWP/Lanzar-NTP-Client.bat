@echo off
:: ============================================================================
::  Lanzar Configure-NTP.ps1 — IPC CLIENT (A72.TOUTWP)
::  NTP: FortiGate 10.11.100.122 (relay desde CSP NTP 10.8.80.1 + 10.8.80.2)
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
    -Role Client ^
    -NtpServer 10.11.100.122 ^
    -NtpFallback 10.8.80.1 ^
    -PollIntervalSeconds 900 ^
    -Language FRA

pause
