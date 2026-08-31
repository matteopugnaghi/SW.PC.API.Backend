@echo off
REM Detiene el hotspot y la comparticion ICS del puente VPN
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0Setup-VpnBridge.ps1\" -Stop'"
