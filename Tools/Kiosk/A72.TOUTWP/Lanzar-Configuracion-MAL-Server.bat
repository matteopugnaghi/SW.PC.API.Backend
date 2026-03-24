@echo off
title AQUAFRISCH - Configuracion MAL-IPC-SERVER (Firewall)

:: Auto-elevate to Administrator (required for WinRM / TrustedHosts)
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

powershell -ExecutionPolicy Bypass -File "%~dp0Lanzar-Configuracion-MAL-Server.ps1"
