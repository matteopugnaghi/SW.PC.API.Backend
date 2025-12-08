@echo off
REM ============================================================================
REM  🏭 AQUAFRISCH SUPERVISOR - KIOSK LAUNCHER CON WATCHDOG
REM ============================================================================
REM  Versión avanzada con monitoreo del backend
REM  Si el backend no responde por 5 minutos, reinicia el equipo
REM ============================================================================

REM === CONFIGURACIÓN ===
SET FRONTEND_URL=http://localhost:3001/
SET BACKEND_CHECK_URL=http://localhost:5000/api/models
SET BROWSER_PATH=C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
SET LOG_FILE=%~dp0kiosk_log.txt
SET WATCHDOG_INTERVAL=60
SET MAX_FAILURES=5
SET FAILURE_COUNT=0

REM Si Edge no existe, usar Chrome
IF NOT EXIST "%BROWSER_PATH%" (
    SET BROWSER_PATH=C:\Program Files\Google\Chrome\Application\chrome.exe
)
IF NOT EXIST "%BROWSER_PATH%" (
    SET BROWSER_PATH=C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
)

REM === INICIO ===
echo [%date% %time%] === KIOSK CON WATCHDOG INICIANDO === >> "%LOG_FILE%"

REM Lanzar navegador en segundo plano (sin /wait)
echo [%date% %time%] Lanzando navegador... >> "%LOG_FILE%"
start "" "%BROWSER_PATH%" --kiosk --disable-pinch --overscroll-history-navigation=0 --disable-session-crashed-bubble --noerrdialogs --disable-infobars --disable-translate --no-first-run "%FRONTEND_URL%"

REM Esperar a que el navegador inicie
timeout /t 10 /nobreak > nul

:WATCHDOG_LOOP
REM Verificar si el backend responde
echo [%date% %time%] Verificando backend... >> "%LOG_FILE%"

REM Usar PowerShell para hacer la petición HTTP (más confiable que curl en Windows)
powershell -Command "try { $r = Invoke-WebRequest -Uri '%BACKEND_CHECK_URL%' -TimeoutSec 10 -UseBasicParsing; exit 0 } catch { exit 1 }"

IF %ERRORLEVEL% EQU 0 (
    REM Backend OK - resetear contador
    SET FAILURE_COUNT=0
    echo [%date% %time%] Backend OK >> "%LOG_FILE%"
) ELSE (
    REM Backend falló
    SET /A FAILURE_COUNT+=1
    echo [%date% %time%] Backend NO RESPONDE. Fallo %FAILURE_COUNT% de %MAX_FAILURES% >> "%LOG_FILE%"
    
    IF %FAILURE_COUNT% GEQ %MAX_FAILURES% (
        echo [%date% %time%] !!! LIMITE DE FALLOS ALCANZADO - REINICIANDO SISTEMA !!! >> "%LOG_FILE%"
        
        REM Cerrar el navegador primero
        taskkill /F /IM msedge.exe 2>nul
        taskkill /F /IM chrome.exe 2>nul
        
        REM Reiniciar el equipo
        shutdown /r /t 30 /c "Aquafrisch Supervisor: Reiniciando por fallo de conexion al servidor"
        
        REM Salir del script
        exit
    )
)

REM Verificar que el navegador sigue corriendo
tasklist /FI "IMAGENAME eq msedge.exe" 2>NUL | find /I /N "msedge.exe">NUL
IF %ERRORLEVEL% NEQ 0 (
    tasklist /FI "IMAGENAME eq chrome.exe" 2>NUL | find /I /N "chrome.exe">NUL
    IF %ERRORLEVEL% NEQ 0 (
        echo [%date% %time%] Navegador no esta corriendo. Relanzando... >> "%LOG_FILE%"
        start "" "%BROWSER_PATH%" --kiosk --disable-pinch --overscroll-history-navigation=0 --disable-session-crashed-bubble --noerrdialogs --disable-infobars --disable-translate --no-first-run "%FRONTEND_URL%"
        timeout /t 5 /nobreak > nul
    )
)

REM Esperar intervalo antes de siguiente verificación
timeout /t %WATCHDOG_INTERVAL% /nobreak > nul
goto WATCHDOG_LOOP
