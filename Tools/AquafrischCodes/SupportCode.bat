@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title Aquafrisch - Generador Codigo de Soporte

echo.
echo ============================================================
echo   GENERADOR DE CODIGO DE SOPORTE - AQUAFRISCH
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

set /p CHALLENGE=Challenge Code (opcional, ENTER = omitir verificacion): 
set /p DATETIME_LOCAL=Fecha/hora LOCAL del cliente (YYYY-MM-DDTHH:MM:SS, ENTER = AHORA del PC tecnico): 

echo.
echo ------------------------------------------------------------
echo.

set "ARGS=-InstallationId "%INSTALLATION_ID%""
if not "%CHALLENGE%"=="" set "ARGS=!ARGS! -ChallengeCode "%CHALLENGE%""
if not "%DATETIME_LOCAL%"=="" set "ARGS=!ARGS! -DateTime "%DATETIME_LOCAL%""

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateSupportCode.ps1" !ARGS!

echo.
echo ------------------------------------------------------------
pause
endlocal
