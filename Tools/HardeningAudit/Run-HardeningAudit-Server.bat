@echo off
REM ============================================================
REM  Run-HardeningAudit-Server.bat
REM  Launcher self-elevating para auditoria SERVER (FAT A72.TOUTWP)
REM  Ejecuta Invoke-HardeningAudit-Server.ps1 como Admin con consola persistente
REM
REM  Uso / Usage:
REM      Run-HardeningAudit-Server.bat [Language]
REM         Language = ES | EN   (default: ES)
REM ============================================================

setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

REM Idioma (1er argumento; si no, preguntar al usuario)
set "LANG=%~1"
if "%LANG%"=="" (
    echo.
    echo Seleccione idioma / Select language:
    echo    [1] Espanol  ^(ES^)   [por defecto / default]
    echo    [2] English  ^(EN^)
    echo.
    set /p "LANGCHOICE=Opcion / Option (1-2) [1]: "
    if /I "!LANGCHOICE!"=="2" (set "LANG=EN") else if /I "!LANGCHOICE!"=="EN" (set "LANG=EN") else (set "LANG=ES")
)
if /I "%LANG%"=="EN" (set "LANG=EN") else (set "LANG=ES")

REM Comprobar elevacion: si no es admin, relanzar via UAC pasando el idioma
net session >nul 2>&1
if %errorlevel% neq 0 (
    if /I "%LANG%"=="EN" (echo [INFO] Requesting UAC elevation...) else (echo [INFO] Solicitando elevacion UAC...)
    powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%LANG%' -Verb RunAs"
    exit /b
)

if /I "%LANG%"=="EN" (
    echo ============================================================
    echo   SERVER HARDENING AUDIT - A72.TOUTWP
    echo   Running as    : %USERNAME%
    echo   Language      : %LANG%
    echo ============================================================
) else (
    echo ============================================================
    echo   AUDITORIA HARDENING SERVER - A72.TOUTWP
    echo   Ejecutando como: %USERNAME%
    echo   Idioma         : %LANG%
    echo ============================================================
)
echo.

REM cmd /k para mantener la ventana abierta en caso de error
cmd /k powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-HardeningAudit-Server.ps1" -Language %LANG%
