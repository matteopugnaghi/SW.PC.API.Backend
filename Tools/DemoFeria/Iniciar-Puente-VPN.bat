@echo off
REM Inicia el puente VPN -> Hotspot para la demo con Quest 3 (requiere FortiClient conectado)
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0Setup-VpnBridge.ps1\"'"
