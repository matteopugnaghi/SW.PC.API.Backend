@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul 2>&1
title Aquafrisch Supervisor - Instalar Certificado SSL (Offline)

:: =================================================================
:: Aquafrisch Supervisor - Cert installer (offline, pendrive-ready)
:: -----------------------------------------------------------------
:: Esta version SIEMPRE deja la ventana abierta y escribe un log en
::   %TEMP%\aquafrisch-cert-install.log
:: Si algo falla, el usuario puede enviar ese fichero para soporte.
:: =================================================================

set "LOGFILE=%TEMP%\aquafrisch-cert-install.log"
set "CERT_FILE=%TEMP%\aquafrisch-supervisor.cer"
set "EXITCODE=0"

:: Reiniciar log
> "%LOGFILE%" echo === Aquafrisch cert install - %DATE% %TIME% ===
>>"%LOGFILE%" echo User=%USERNAME%  Host=%COMPUTERNAME%  OS=%OS%

echo ============================================================
echo  AQUAFRISCH SUPERVISOR - Instalacion de Certificado SSL
echo  (Version offline - distribuible por pendrive)
echo ============================================================
echo.
echo  Log detallado: %LOGFILE%
echo.

:: -----------------------------------------------------------------
:: 0/5  Verificar permisos de administrador
:: -----------------------------------------------------------------
echo  [0/5] Verificando permisos de administrador...
net session >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] Este script requiere permisos de Administrador.
    echo          Cierra esta ventana, haz click DERECHO sobre el .bat
    echo          y selecciona "Ejecutar como administrador".
    >>"%LOGFILE%" echo [ERROR] No admin rights ^(net session errorlevel=%errorlevel%^)
    set "EXITCODE=1"
    goto :end
)
echo  [OK] Ejecutando como Administrador.
>>"%LOGFILE%" echo [OK] Admin rights confirmed

:: -----------------------------------------------------------------
:: Pedir IP / hostname / puerto
:: -----------------------------------------------------------------
set "DEFAULT_HOST=192.168.2.161"
set "DEFAULT_PORT=5001"

echo.
set /p "SERVER_HOST=  IP o hostname del servidor [%DEFAULT_HOST%]: "
if "!SERVER_HOST!"=="" set "SERVER_HOST=%DEFAULT_HOST%"

set /p "SERVER_PORT=  Puerto HTTPS [%DEFAULT_PORT%]: "
if "!SERVER_PORT!"=="" set "SERVER_PORT=%DEFAULT_PORT%"

set "SERVER_URL=https://!SERVER_HOST!:!SERVER_PORT!"
set "CERT_URL=!SERVER_URL!/api/certificate/public"

echo.
echo  Servidor: !SERVER_URL!
>>"%LOGFILE%" echo Server=!SERVER_URL!

:: -----------------------------------------------------------------
:: 1/5  Comprobar que curl.exe esta disponible
:: -----------------------------------------------------------------
echo.
echo  [1/5] Verificando curl.exe...
where curl.exe >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] curl.exe no esta disponible en este sistema.
    echo          Requiere Windows 10 1803 o superior.
    >>"%LOGFILE%" echo [ERROR] curl.exe not found
    set "EXITCODE=2"
    goto :end
)
for /f "delims=" %%V in ('curl.exe --version 2^>^&1') do (
    >>"%LOGFILE%" echo curl: %%V
    goto :curl_ok
)
:curl_ok
echo  [OK] curl.exe disponible.

:: -----------------------------------------------------------------
:: 2/5  Probar conectividad TCP basica (no falla el script, solo log)
:: -----------------------------------------------------------------
echo.
echo  [2/5] Probando conectividad TCP a !SERVER_HOST!:!SERVER_PORT! ...
powershell -NoProfile -Command "try { $r = Test-NetConnection -ComputerName '!SERVER_HOST!' -Port !SERVER_PORT! -WarningAction SilentlyContinue; if ($r.TcpTestSucceeded) { 'TCP_OK' } else { 'TCP_FAIL' } } catch { 'TCP_ERR ' + $_.Exception.Message }" > "%TEMP%\aqf_tcp.txt" 2>>"%LOGFILE%"
set "TCP_RESULT=UNKNOWN"
if exist "%TEMP%\aqf_tcp.txt" set /p TCP_RESULT=<"%TEMP%\aqf_tcp.txt"
del "%TEMP%\aqf_tcp.txt" >nul 2>&1
>>"%LOGFILE%" echo TCP test: !TCP_RESULT!
echo  [INFO] Resultado test TCP: !TCP_RESULT!
if /I "!TCP_RESULT!"=="TCP_FAIL" (
    echo  [AVISO] El puerto !SERVER_PORT! no responde en !SERVER_HOST!.
    echo          Sigo intentando con curl, pero probablemente:
    echo            - El servidor no esta encendido / backend no arrancado.
    echo            - El firewall del servidor o de esta PC bloquea !SERVER_PORT!.
    echo            - La IP / hostname es incorrecta.
)

:: -----------------------------------------------------------------
:: 3/5  Descargar certificado
:: -----------------------------------------------------------------
echo.
echo  [3/5] Descargando certificado desde !CERT_URL! ...
if exist "!CERT_FILE!" del "!CERT_FILE!" >nul 2>&1

>>"%LOGFILE%" echo --- curl output ---
curl.exe -k -S -f --max-time 15 -o "!CERT_FILE!" "!CERT_URL!" >>"%LOGFILE%" 2>&1
set "CURL_RC=!errorlevel!"
>>"%LOGFILE%" echo curl exit code = !CURL_RC!

if not "!CURL_RC!"=="0" (
    echo  [ERROR] curl ha fallado con codigo !CURL_RC!.
    echo          Causas tipicas:
    echo            6  = DNS / host no resuelto         ^(IP/hostname incorrecto^)
    echo            7  = No se puede conectar           ^(servidor apagado / firewall^)
    echo            22 = HTTP 4xx/5xx                   ^(endpoint no expuesto^)
    echo            28 = Timeout                         ^(red lenta / firewall silencioso^)
    echo            35 = Error de handshake TLS        ^(HTTPS mal configurado^)
    echo          Revisa el log: %LOGFILE%
    >>"%LOGFILE%" echo [ERROR] curl failed
    set "EXITCODE=3"
    goto :end
)

if not exist "!CERT_FILE!" (
    echo  [ERROR] curl reporto OK pero no hay fichero de salida.
    >>"%LOGFILE%" echo [ERROR] cert file missing after curl
    set "EXITCODE=4"
    goto :end
)

for %%A in ("!CERT_FILE!") do set "CERT_SIZE=%%~zA"
>>"%LOGFILE%" echo cert size = !CERT_SIZE! bytes
if "!CERT_SIZE!"=="0" (
    echo  [ERROR] El certificado descargado esta vacio ^(0 bytes^).
    echo          El endpoint /api/certificate/public no devuelve datos.
    del "!CERT_FILE!" >nul 2>&1
    set "EXITCODE=5"
    goto :end
)
echo  [OK] Certificado descargado ^(!CERT_SIZE! bytes^)

:: -----------------------------------------------------------------
:: 4/5  Instalar en almacen Root de la maquina
:: -----------------------------------------------------------------
echo.
echo  [4/5] Instalando en "Entidades de certificacion raiz de confianza"...
>>"%LOGFILE%" echo --- certutil output ---
certutil -addstore "Root" "!CERT_FILE!" >>"%LOGFILE%" 2>&1
set "CU_RC=!errorlevel!"
>>"%LOGFILE%" echo certutil exit code = !CU_RC!

if not "!CU_RC!"=="0" (
    echo  [ERROR] certutil ha fallado con codigo !CU_RC!.
    echo          Revisa el log: %LOGFILE%
    echo          Verifica que el fichero descargado es un certificado valido ^(PEM o DER^).
    del "!CERT_FILE!" >nul 2>&1
    set "EXITCODE=6"
    goto :end
)
echo  [OK] Certificado instalado en el almacen Root de la maquina.

:: -----------------------------------------------------------------
:: 5/5  Limpieza
:: -----------------------------------------------------------------
echo.
echo  [5/5] Limpiando ficheros temporales...
del "!CERT_FILE!" >nul 2>&1
echo  [OK] Limpieza completada.

echo.
echo ============================================================
echo  INSTALACION COMPLETADA CON EXITO
echo ============================================================
echo.
echo  El certificado SSL de Aquafrisch Supervisor ha sido instalado.
echo.
echo  Siguientes pasos:
echo    1. Cierra y reabre el navegador ^(Chrome / Edge / Firefox*^).
echo    2. Accede a: !SERVER_URL!
echo    3. No deberia aparecer el aviso de "conexion no segura".
echo.
echo  * Firefox usa su propio almacen de certificados:
echo      Ajustes -^> Privacidad y Seguridad -^> Certificados
echo      Importar el .cer manualmente en la pestana "Autoridades".
echo.
echo  Log de esta ejecucion: %LOGFILE%
echo.

:end
echo.
if not "!EXITCODE!"=="0" (
    echo ============================================================
    echo  INSTALACION FINALIZADA CON ERRORES ^(codigo !EXITCODE!^)
    echo ============================================================
    echo  Revisa el log y envialo a soporte si es necesario:
    echo    %LOGFILE%
    echo.
)
echo  Pulsa cualquier tecla para cerrar esta ventana...
pause >nul
endlocal & exit /b %EXITCODE%
