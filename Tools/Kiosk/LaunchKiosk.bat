@echo off
REM ============================================================================
REM  LaunchKiosk.bat — Aquafrisch Supervisor Kiosk Shell
REM  Ref: 04.2-01 §23 — Autostart y Modo Kiosco
REM ============================================================================
REM
REM  Este script actúa como Custom Shell para el usuario kiosk (aqf).
REM  Reemplaza explorer.exe y lanza KioskWatchdog.ps1 que proporciona:
REM    1. Edge en modo kiosk (pantalla completa) → https://192.168.2.161:5001
REM    2. Watchdog: relanza Edge si se cierra
REM    3. Health check: reinicia IPC tras N fallos del backend
REM    4. Botón flotante táctil (mantener 3s) → menú de emergencia:
REM       - Reiniciar navegador
REM       - Cerrar sesión Windows (log-off)
REM       - Reiniciar equipo
REM
REM  IPC táctil sin teclado (Beckhoff CP2221-0040).
REM ============================================================================

SET SCRIPT_DIR=%~dp0
SET WATCHDOG=%SCRIPT_DIR%KioskWatchdog.ps1
SET LOG_FILE=%SCRIPT_DIR%kiosk_launcher.log

echo [%date% %time%] ========================================== >> "%LOG_FILE%"
echo [%date% %time%] Kiosk Shell iniciado >> "%LOG_FILE%"
echo [%date% %time%] ========================================== >> "%LOG_FILE%"

REM Esperar a que el sistema termine de arrancar
timeout /t 5 /nobreak >nul

REM Verificar que KioskWatchdog.ps1 existe
if not exist "%WATCHDOG%" (
    echo [%date% %time%] ERROR: No se encontró %WATCHDOG% >> "%LOG_FILE%"
    echo [%date% %time%] Lanzando Edge directamente como fallback >> "%LOG_FILE%"
    goto FALLBACK
)

REM Lanzar KioskWatchdog.ps1 (no retorna hasta que se cierre el botón flotante)
echo [%date% %time%] Lanzando KioskWatchdog.ps1... >> "%LOG_FILE%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%WATCHDOG%"

echo [%date% %time%] KioskWatchdog.ps1 finalizó — relanzando en 5s... >> "%LOG_FILE%"
timeout /t 5 /nobreak >nul

REM Si el watchdog se cierra, relanzarlo (bucle infinito)
goto :START_WATCHDOG

:START_WATCHDOG
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%WATCHDOG%"
echo [%date% %time%] KioskWatchdog.ps1 se cerró — relanzando... >> "%LOG_FILE%"
timeout /t 5 /nobreak >nul
goto :START_WATCHDOG

:FALLBACK
REM Fallback: lanzar Edge directamente sin watchdog
echo [%date% %time%] MODO FALLBACK — Edge sin watchdog >> "%LOG_FILE%"
start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --kiosk "https://192.168.2.161:5001" --edge-kiosk-type=fullscreen --no-first-run
pause
