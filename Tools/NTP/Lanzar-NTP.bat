@echo off
chcp 65001 >nul 2>&1
title Configure NTP (W32Time)
:: ============================================================================
::  Lanzar Configure-NTP.ps1 -- Generic launcher (any project / any PC)
::  Auto-elevates to Administrator
:: ============================================================================

:: Auto-elevate to Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
    exit /b
)

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File ".\Configure-NTP-Launcher.ps1"

pause
