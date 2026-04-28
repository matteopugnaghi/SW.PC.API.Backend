@echo off
REM ============================================================
REM   SBOM Scanner - Lanzador
REM   Ejecuta Scan-SBOM-OSV.ps1 en modo interactivo
REM ============================================================
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Scan-SBOM-OSV.ps1"
echo.
pause
