@echo off
REM Crea solo el hotspot WiFi para la demo LOCAL (opcion C, sin VPN)
REM La Quest entra en: https://192.168.137.1:5001
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0Setup-VpnBridge.ps1\" -SoloHotspot'"
