@echo off
REM ============================================================
REM  Run-HardeningAudit-Server.bat
REM  Launcher self-elevating para auditoria SERVER (FAT A72.TOUTWP)
REM  Ejecuta Invoke-HardeningAudit-Server.ps1 como Admin con consola persistente
REM ============================================================

setlocal
cd /d "%~dp0"

REM Comprobar elevacion: si no es admin, relanzar via UAC
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Solicitando elevacion UAC...
    powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================================
echo   AUDITORIA HARDENING SERVER - A72.TOUTWP
echo   Ejecutando como: %USERNAME%
echo ============================================================
echo.

REM cmd /k para mantener la ventana abierta en caso de error
cmd /k powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-HardeningAudit-Server.ps1" %*
