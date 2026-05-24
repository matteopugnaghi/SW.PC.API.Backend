@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title Aquafrisch - Generador Codigo de Recuperacion

echo.
echo ============================================================
echo   GENERADOR DE CODIGO DE RECUPERACION - AQUAFRISCH
echo   (uso interno - no compartir con clientes)
echo ============================================================
echo.

set /p INSTALLATION_ID=Installation ID (ej: AQFR-A72-001): 
if "%INSTALLATION_ID%"=="" (
    echo.
    echo [ERROR] Installation ID es obligatorio.
    echo.
    pause
    exit /b 1
)

set /p USER_NAME=Username del cliente: 
if "%USER_NAME%"=="" (
    echo.
    echo [ERROR] Username es obligatorio.
    echo.
    pause
    exit /b 1
)

set /p DATE_INPUT=Fecha del sistema del cliente (YYYY-MM-DD, ENTER = HOY del PC tecnico): 

echo.
echo ------------------------------------------------------------
echo.

if "%DATE_INPUT%"=="" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateRecoveryCode.ps1" -InstallationId "%INSTALLATION_ID%" -Username "%USER_NAME%"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateRecoveryCode.ps1" -InstallationId "%INSTALLATION_ID%" -Username "%USER_NAME%" -Date "%DATE_INPUT%"
)

echo.
echo ------------------------------------------------------------
pause
endlocal
