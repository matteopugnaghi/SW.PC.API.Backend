@echo off
REM ============================================================================
REM  🏭 AQUAFRISCH SUPERVISOR - KIOSK LAUNCHER
REM ============================================================================
REM  Este script se configura como Shell en Windows (via Shell Launcher/Registry)
REM  Lanza el frontend en modo kiosk y se auto-reinicia si se cierra
REM ============================================================================

REM === CONFIGURACIÓN ===
SET FRONTEND_URL=http://localhost:3001/
SET BACKEND_URL=http://localhost:5000/api/models
SET BROWSER_PATH=C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
SET LOG_FILE=%~dp0kiosk_log.txt

REM Si Edge no existe, usar Chrome
IF NOT EXIST "%BROWSER_PATH%" (
    SET BROWSER_PATH=C:\Program Files\Google\Chrome\Application\chrome.exe
)
IF NOT EXIST "%BROWSER_PATH%" (
    SET BROWSER_PATH=C:\Program Files (x86)\Google\Chrome\Application\chrome.exe
)

REM === INICIO ===
echo [%date% %time%] === KIOSK INICIANDO === >> "%LOG_FILE%"
echo [%date% %time%] Frontend: %FRONTEND_URL% >> "%LOG_FILE%"
echo [%date% %time%] Browser: %BROWSER_PATH% >> "%LOG_FILE%"

:LAUNCH_LOOP
echo [%date% %time%] Lanzando navegador en modo kiosk... >> "%LOG_FILE%"

REM Lanzar navegador en modo kiosk (pantalla completa, sin controles)
REM Edge/Chrome flags:
REM   --kiosk           = Modo kiosk (F11 permanente)
REM   --disable-pinch   = Deshabilita zoom con dedos
REM   --overscroll-history-navigation=0 = Deshabilita navegación con swipe
REM   --disable-session-crashed-bubble = No muestra "Chrome no se cerró correctamente"
REM   --noerrdialogs    = Sin diálogos de error
REM   --disable-infobars = Sin barras de información

start /wait "" "%BROWSER_PATH%" --kiosk --disable-pinch --overscroll-history-navigation=0 --disable-session-crashed-bubble --noerrdialogs --disable-infobars --disable-translate --no-first-run "%FRONTEND_URL%"

REM Si llegamos aquí, el navegador se cerró
echo [%date% %time%] Navegador cerrado. Reiniciando en 3 segundos... >> "%LOG_FILE%"

REM Esperar 3 segundos antes de relanzar
timeout /t 3 /nobreak > nul

REM Volver a lanzar
goto LAUNCH_LOOP
