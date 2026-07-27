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
set "MTLS_INFO=%TEMP%\aqf_mtls.json"
set "MACHINECA_FILE=%TEMP%\aquafrisch-machine-ca.cer"
set "INF_FILE=%TEMP%\aqf_machine.inf"
set "CSR_FILE=%TEMP%\aqf_machine.csr"
set "JSON_FILE=%TEMP%\aqf_enroll.json"
set "MACHINE_CER=%TEMP%\aqf_machine.cer"
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
:: mTLS  Registro de equipo (solo si el servidor tiene MtlsEnabled)
:: -----------------------------------------------------------------
echo.
echo  [mTLS] Consultando si el servidor requiere identidad de equipo...
curl.exe -k -s --max-time 15 -o "%MTLS_INFO%" "!SERVER_URL!/api/certificate/mtls-info" >>"%LOGFILE%" 2>&1
findstr /C:"\"mtlsEnabled\":true" "%MTLS_INFO%" >nul 2>&1
if errorlevel 1 (
    echo  [INFO] mTLS desactivado en el servidor. No se requiere registro de equipo.
    >>"%LOGFILE%" echo mTLS disabled or mtls-info not reachable - skipping enrollment
    goto :mtls_done
)

echo  [INFO] El servidor tiene mTLS ACTIVO ^(identidad de equipo por certificado^).
echo.
echo  Para registrar este equipo ^(%COMPUTERNAME%^) necesitas un CODIGO DE
echo  REGISTRO de un solo uso, generado por un Administrador en la pantalla
echo  Usuarios -^> Equipos del Supervisor. Caduca a las 24h.
echo.
echo  Deja el codigo VACIO y pulsa Enter para omitir el registro.
echo.
set "REG_CODE="
set /p "REG_CODE=  Codigo de registro (XXXX-XXXX-XXXX): "
if "!REG_CODE!"=="" (
    echo  [INFO] Registro omitido. Este equipo funcionara sin identidad de maquina.
    >>"%LOGFILE%" echo mTLS enrollment skipped by user
    goto :mtls_done
)
>>"%LOGFILE%" echo mTLS enrollment started for %COMPUTERNAME%

echo.
echo  [mTLS 1/4] Instalando la CA de maquinas ^(Aquafrisch Machine CA^)...
curl.exe -k -S -f --max-time 15 -o "%MACHINECA_FILE%" "!SERVER_URL!/api/certificate/machine-ca" >>"%LOGFILE%" 2>&1
if errorlevel 1 (
    echo  [ERROR] No se pudo descargar la Machine CA. Revisa el log.
    >>"%LOGFILE%" echo [ERROR] machine-ca download failed
    set "EXITCODE=7"
    goto :end
)
certutil -addstore "Root" "%MACHINECA_FILE%" >>"%LOGFILE%" 2>&1
if errorlevel 1 (
    echo  [ERROR] No se pudo instalar la Machine CA en el almacen Root.
    set "EXITCODE=7"
    goto :end
)
echo  [OK] Machine CA instalada.

echo  [mTLS 2/4] Generando clave y solicitud de certificado ^(CSR^)...
:: Clave NO exportable en el almacen de MAQUINA. La clave privada nunca sale de este PC.
(
    echo [Version]
    echo Signature="$Windows NT$"
    echo [NewRequest]
    echo Subject = "CN=%COMPUTERNAME%"
    echo KeyLength = 2048
    echo Exportable = FALSE
    echo MachineKeySet = TRUE
    echo KeySpec = 1
    echo KeyUsage = 0x80
    echo ProviderName = "Microsoft RSA SChannel Cryptographic Provider"
    echo RequestType = PKCS10
    echo [EnhancedKeyUsageExtension]
    echo OID=1.3.6.1.5.5.7.3.2
) > "%INF_FILE%"
if exist "%CSR_FILE%" del "%CSR_FILE%" >nul 2>&1
certreq -new -f -q "%INF_FILE%" "%CSR_FILE%" >>"%LOGFILE%" 2>&1
if errorlevel 1 (
    echo  [ERROR] certreq no pudo generar el CSR. Revisa el log.
    >>"%LOGFILE%" echo [ERROR] certreq -new failed
    set "EXITCODE=8"
    goto :end
)
echo  [OK] CSR generado ^(CN=%COMPUTERNAME%^).

echo  [mTLS 3/4] Enviando CSR al servidor con el codigo de registro...
powershell -NoProfile -Command "$csr = Get-Content -Raw '%CSR_FILE%'; @{ code = '!REG_CODE!'; csr = $csr } | ConvertTo-Json | Set-Content -Encoding UTF8 '%JSON_FILE%'" >>"%LOGFILE%" 2>&1
if not exist "%JSON_FILE%" (
    echo  [ERROR] No se pudo preparar la peticion de registro.
    set "EXITCODE=9"
    goto :end
)
if exist "%MACHINE_CER%" del "%MACHINE_CER%" >nul 2>&1
curl.exe -k -S -f --max-time 30 -H "Content-Type: application/json" --data-binary "@%JSON_FILE%" -o "%MACHINE_CER%" "!SERVER_URL!/api/certificate/enroll" >>"%LOGFILE%" 2>&1
set "ENROLL_RC=!errorlevel!"
>>"%LOGFILE%" echo enroll curl exit code = !ENROLL_RC!
if not "!ENROLL_RC!"=="0" (
    echo  [ERROR] El servidor rechazo el registro ^(curl !ENROLL_RC!^).
    echo          Causa tipica: codigo invalido, ya usado o caducado.
    echo          Pide un codigo nuevo al Administrador y reejecuta el script.
    set "EXITCODE=9"
    goto :end
)
echo  [OK] Certificado de maquina emitido por el servidor.

echo  [mTLS 4/4] Instalando certificado de maquina y configurando navegadores...
certreq -accept -machine "%MACHINE_CER%" >>"%LOGFILE%" 2>&1
if errorlevel 1 (
    echo  [ERROR] certreq -accept fallo. Revisa el log.
    >>"%LOGFILE%" echo [ERROR] certreq -accept failed
    set "EXITCODE=10"
    goto :end
)
:: Politica AutoSelectCertificateForUrls: Edge y Chrome presentan el certificado
:: automaticamente al conectar al Supervisor (sin popup de seleccion).
set "AUTOSEL={\"pattern\":\"https://!SERVER_HOST!:!SERVER_PORT!\",\"filter\":{\"ISSUER\":{\"CN\":\"Aquafrisch Machine CA\"}}}"
reg add "HKLM\SOFTWARE\Policies\Microsoft\Edge\AutoSelectCertificateForUrls" /v 1 /t REG_SZ /d "!AUTOSEL!" /f >>"%LOGFILE%" 2>&1
reg add "HKLM\SOFTWARE\Policies\Google\Chrome\AutoSelectCertificateForUrls" /v 1 /t REG_SZ /d "!AUTOSEL!" /f >>"%LOGFILE%" 2>&1
echo  [OK] Equipo %COMPUTERNAME% registrado. Cierra y reabre el navegador.
>>"%LOGFILE%" echo mTLS enrollment completed for %COMPUTERNAME%

:mtls_done

:: -----------------------------------------------------------------
:: 5/5  Limpieza
:: -----------------------------------------------------------------
echo.
echo  [5/5] Limpiando ficheros temporales...
del "!CERT_FILE!" >nul 2>&1
del "%MTLS_INFO%" "%MACHINECA_FILE%" "%INF_FILE%" "%CSR_FILE%" "%JSON_FILE%" "%MACHINE_CER%" >nul 2>&1
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
