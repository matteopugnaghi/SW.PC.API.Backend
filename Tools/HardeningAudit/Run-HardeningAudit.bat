@echo off
REM ============================================================
REM  Run-HardeningAudit.bat
REM  Lanzador con auto-elevacion a Administrador para
REM  Invoke-HardeningAudit.ps1 (FAT A72.TOUTWP - Alstom)
REM
REM  Uso / Usage:
REM      Run-HardeningAudit.bat [Target] [Language]
REM         Target   = Client | Server | Both   (default: Client)
REM         Language = ES | EN                  (default: ES)
REM
REM  Ejemplos:
REM      Run-HardeningAudit.bat
REM      Run-HardeningAudit.bat Client EN
REM      Run-HardeningAudit.bat Both   ES
REM ============================================================

setlocal EnableExtensions EnableDelayedExpansion

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
    set "LANG=%~3"
) else (
    set "TARGET=%~1"
    set "LANG=%~2"
)
if "%TARGET%"=="" set "TARGET=Client"

REM Si no se paso idioma como argumento, preguntar al usuario
if "%LANG%"=="" (
    echo.
    echo Seleccione idioma / Select language:
    echo    [1] Espanol  ^(ES^)   [por defecto / default]
    echo    [2] English  ^(EN^)
    echo.
    set /p "LANGCHOICE=Opcion / Option (1-2) [1]: "
    if /I "!LANGCHOICE!"=="2" (set "LANG=EN") else if /I "!LANGCHOICE!"=="EN" (set "LANG=EN") else (set "LANG=ES")
)

REM Normalizar idioma -- solo ES o EN
if /I "%LANG%"=="EN" (set "LANG=EN") else (set "LANG=ES")

echo.
if /I "%LANG%"=="EN" (
    echo ============================================================
    echo  HARDENING AUDIT - A72.TOUTWP ^(Alstom^)
    echo  Compliance: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C Rev C
    echo ============================================================
    echo  Directory : %CD%
    echo  User      : %USERNAME%   Host: %COMPUTERNAME%
    echo  Target    : %TARGET%
    echo  Language  : %LANG%
    echo ============================================================
) else (
    echo ============================================================
    echo  HARDENING AUDIT - A72.TOUTWP ^(Alstom^)
    echo  Cumplimiento: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C Rev C
    echo ============================================================
    echo  Directorio: %CD%
    echo  Usuario   : %USERNAME%   Host: %COMPUTERNAME%
    echo  Target    : %TARGET%
    echo  Idioma    : %LANG%
    echo ============================================================
)
echo.

if not exist "%~dp0Invoke-HardeningAudit.ps1" (
    if /I "%LANG%"=="EN" (
        echo [ERROR] Not found: %~dp0Invoke-HardeningAudit.ps1
        echo.
        echo Press any key to close...
    ) else (
        echo [ERROR] No se encuentra: %~dp0Invoke-HardeningAudit.ps1
        echo.
        echo Pulse una tecla para cerrar...
    )
    pause >nul
    exit /b 2
)

if /I "%LANG%"=="EN" (
    echo [INFO] Launching Invoke-HardeningAudit.ps1 ...
) else (
    echo [INFO] Lanzando Invoke-HardeningAudit.ps1 ...
)
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-HardeningAudit.ps1" -Target %TARGET% -Language %LANG%
set "RC=%ERRORLEVEL%"

echo.
if /I "%LANG%"=="EN" (
    echo ============================================================
    echo  Audit finished ^(exit code: %RC%^)
    echo  Reports in:  %~dp0  ^(subfolder HardeningAudit_YYYYMMDD_HHMMSS^)
    echo ============================================================
    echo.
    echo Press any key to close this window...
) else (
    echo ============================================================
    echo  Auditoria finalizada ^(exit code: %RC%^)
    echo  Informes en:  %~dp0  ^(subcarpeta HardeningAudit_YYYYMMDD_HHMMSS^)
    echo ============================================================
    echo.
    echo Pulse una tecla para cerrar esta ventana...
)
pause >nul
endlocal
exit /b %RC%
