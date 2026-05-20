@echo off
REM ============================================================
REM  Run-HardeningAudit.bat
REM  Lanzador con auto-elevacion a Administrador para
REM  Invoke-HardeningAudit.ps1 (FAT A72.TOUTWP - Alstom)
REM ============================================================

setlocal EnableExtensions

REM --- Comprobar privilegios de Administrador ---
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo [INFO] Solicitando elevacion via UAC...
    echo.
    REM Relanzar con cmd /k para que la ventana quede SIEMPRE abierta,
    REM aunque haya un error temprano.
    powershell -NoProfile -Command "Start-Process -FilePath 'cmd.exe' -ArgumentList '/k','\"\"%~f0\"\" ELEVATED %*' -Verb RunAs"
    exit /b 0
)

REM --- A partir de aqui ya tenemos privilegios de Administrador ---
cd /d "%~dp0"

REM Detectar si venimos del relanzamiento (primer arg = ELEVATED) y descartarlo
set "ARG1=%~1"
if /I "%ARG1%"=="ELEVATED" (
    set "TARGET=%~2"
) else (
    set "TARGET=%~1"
)
if "%TARGET%"=="" set "TARGET=Client"

echo.
echo ============================================================
echo  HARDENING AUDIT - A72.TOUTWP (Alstom)
echo  Cumplimiento: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C Rev C
echo ============================================================
echo  Directorio: %CD%
echo  Usuario   : %USERNAME%   Host: %COMPUTERNAME%
echo  Target    : %TARGET%
echo ============================================================
echo.

if not exist "%~dp0Invoke-HardeningAudit.ps1" (
    echo [ERROR] No se encuentra: %~dp0Invoke-HardeningAudit.ps1
    echo.
    echo Pulse una tecla para cerrar...
    pause >nul
    exit /b 2
)

echo [INFO] Lanzando Invoke-HardeningAudit.ps1 ...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-HardeningAudit.ps1" -Target %TARGET%
set "RC=%ERRORLEVEL%"

echo.
echo ============================================================
echo  Auditoria finalizada (exit code: %RC%)
echo  Informes en:  %~dp0  (subcarpeta HardeningAudit_YYYYMMDD_HHMMSS)
echo ============================================================
echo.
echo Pulse una tecla para cerrar esta ventana...
pause >nul
endlocal
exit /b %RC%
