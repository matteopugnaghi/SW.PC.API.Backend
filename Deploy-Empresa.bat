@echo off
title Deploy Servidor Empresa
cd /d "%~dp0"
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Deploy-Servidor-Empresa.ps1"
pause
