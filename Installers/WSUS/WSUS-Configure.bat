@echo off
echo Ejecutando WSUS-Configure...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0WSUS-Configure.ps1"
echo.
pause
