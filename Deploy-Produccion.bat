@echo off
title Deploy Servidor Produccion
cd /d "%~dp0"
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Deploy-Manual-Remote.ps1"
pause
