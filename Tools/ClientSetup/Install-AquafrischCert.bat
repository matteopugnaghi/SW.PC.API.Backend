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
set "POLICY_PS1=%TEMP%\aqf_autoselect.ps1"
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
set "IS_ADMIN=0"
if not errorlevel 1 set "IS_ADMIN=1"

if "!IS_ADMIN!"=="1" (
    echo  [OK] Ejecutando como Administrador.
    >>"%LOGFILE%" echo [OK] Admin rights confirmed
) else (
    echo  [INFO] Ejecutando como usuario estandar ^(%USERNAME%^).
    echo  [INFO] La instalacion de la CA raiz requiere Admin ^(se omitira si ya esta instalada^).
    >>"%LOGFILE%" echo [INFO] Running as standard user: %USERNAME%
)

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
:: 4/5  Instalar en almacen Root de la maquina (solo si es Admin)
:: -----------------------------------------------------------------
echo.
if "!IS_ADMIN!"=="1" (
    echo  [4/5] Instalando en "Entidades de certificacion raiz de confianza"...
    >>"%LOGFILE%" echo --- certutil output ---
    certutil -addstore "Root" "!CERT_FILE!" >>"%LOGFILE%" 2>&1
    set "CU_RC=!errorlevel!"
    >>"%LOGFILE%" echo certutil exit code = !CU_RC!
    if not "!CU_RC!"=="0" (
        echo  [ERROR] certutil ha fallado con codigo !CU_RC!.
        echo          Revisa el log: %LOGFILE%
        del "!CERT_FILE!" >nul 2>&1
        set "EXITCODE=6"
        goto :end
    )
    echo  [OK] Certificado SSL instalado en el almacen Root de la maquina.
) else (
    :: Usuario estandar: instalar en el store del usuario actual
    echo  [4/5] Instalando certificado SSL en el almacen del usuario ^(%USERNAME%^)...
    certutil -addstore -user "Root" "!CERT_FILE!" >>"%LOGFILE%" 2>&1
    echo  [OK] Certificado SSL instalado en el almacen del usuario.
    echo  [INFO] Para instalarlo a nivel de maquina ^(todos los usuarios^),
    echo         ejecutar este script como Administrador.
)

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
:: Si este usuario ya tiene un certificado de equipo valido, permitir saltar el
:: enrollment (no quema otro codigo) y solo reconfigurar el navegador.
set "EXISTING_CERT="
for /f "delims=" %%T in ('powershell -NoProfile -Command "Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=%COMPUTERNAME%' -and $_.Issuer -like '*Aquafrisch Machine CA*' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1 -ExpandProperty Thumbprint" 2^>nul') do set "EXISTING_CERT=%%T"
if defined EXISTING_CERT (
    >>"%LOGFILE%" echo existing machine cert found: !EXISTING_CERT!
    echo  [INFO] Este equipo YA tiene un certificado de equipo valido ^(huella !EXISTING_CERT!^).
    echo         Deja el codigo VACIO y pulsa Enter para CONSERVARLO y solo
    echo         configurar el navegador ^(no hace falta un codigo nuevo^).
) else (
    echo  Deja el codigo VACIO y pulsa Enter para omitir el registro.
)
echo.
echo  [INFO] El certificado de equipo se instala en el perfil del usuario %USERNAME%.
echo         El navegador debe abrirse con ESTE mismo usuario de Windows.
echo.
set "REG_CODE="
set /p "REG_CODE=  Codigo de registro (XXXX-XXXX-XXXX): "
if "!REG_CODE!"=="" (
    if defined EXISTING_CERT (
        echo  [INFO] Se conserva el certificado existente. Configurando navegador...
        >>"%LOGFILE%" echo mTLS enrollment skipped - existing cert kept, applying browser policy
        goto :mtls_policy
    )
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
if "!IS_ADMIN!"=="1" (
    certutil -addstore "Root" "%MACHINECA_FILE%" >>"%LOGFILE%" 2>&1
    if errorlevel 1 (
        echo  [ERROR] No se pudo instalar la Machine CA en el almacen Root.
        set "EXITCODE=7"
        goto :end
    )
    echo  [OK] Machine CA instalada en almacen Root ^(maquina^).
) else (
    certutil -addstore -user "Root" "%MACHINECA_FILE%" >>"%LOGFILE%" 2>&1
    echo  [OK] Machine CA instalada en almacen Root ^(usuario^).
)

echo  [mTLS 2/4] Generando clave y solicitud de certificado ^(CSR^)...
:: NOTA: los certificados de equipo anteriores NO se borran aqui. Se limpian
:: SOLO despues de que el nuevo este instalado (paso 4/4): si el enrollment
:: falla (codigo usado/caducado), el equipo conserva su certificado actual.
:: Clave en el almacen del USUARIO actual (CurrentUser\My) para que Chrome/Edge puedan usarla.
:: MachineKeySet=FALSE + Exportable=FALSE: la clave no sale del PC pero es accesible por el usuario.
(
    echo [Version]
    echo Signature="$Windows NT$"
    echo [NewRequest]
    echo Subject = "CN=%COMPUTERNAME%"
    echo KeyLength = 2048
    echo Exportable = TRUE
    echo MachineKeySet = FALSE
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
powershell -NoProfile -Command "$csr = [string](Get-Content -Raw '%CSR_FILE%'); $json = @{ code = '!REG_CODE!'; csr = $csr } | ConvertTo-Json; $utf8NoBom = New-Object System.Text.UTF8Encoding $false; [System.IO.File]::WriteAllText('%JSON_FILE%', $json, $utf8NoBom)" >>"%LOGFILE%" 2>&1
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

echo  [mTLS 4/4] Instalando certificado de maquina...
certreq -accept "%MACHINE_CER%" >>"%LOGFILE%" 2>&1
if errorlevel 1 (
    echo  [ERROR] certreq -accept fallo. Revisa el log.
    >>"%LOGFILE%" echo [ERROR] certreq -accept failed
    set "EXITCODE=10"
    goto :end
)
echo  [OK] Certificado de maquina instalado en el perfil de %USERNAME%.

:: Ahora que el nuevo certificado existe, retirar los anteriores (se conserva
:: el mas reciente). Los de LocalMachine\My son restos de versiones antiguas
:: del script (MachineKeySet=TRUE); solo se pueden borrar con Admin.
echo  [mTLS 4/4] Retirando certificados de equipo anteriores...
powershell -NoProfile -Command "Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=%COMPUTERNAME%' -and $_.Issuer -like '*Aquafrisch*' } | Sort-Object NotBefore -Descending | Select-Object -Skip 1 | ForEach-Object { Remove-Item $_.PSPath -Force; Write-Host '  Eliminado de CurrentUser\My:' $_.Thumbprint }" >>"%LOGFILE%" 2>&1
if "!IS_ADMIN!"=="1" powershell -NoProfile -Command "Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq 'CN=%COMPUTERNAME%' -and $_.Issuer -like '*Aquafrisch*' } | ForEach-Object { Remove-Item $_.PSPath -Force; Write-Host '  Eliminado de LocalMachine\My:' $_.Thumbprint }" >>"%LOGFILE%" 2>&1

:mtls_policy
echo  [mTLS 4/4] Configurando navegadores ^(autoseleccion del certificado^)...
:: Politica AutoSelectCertificateForUrls: Edge y Chrome presentan el certificado
:: automaticamente al conectar al Supervisor (sin popup de seleccion).
:: Requiere HKLM (Software\Policies es de solo lectura para usuarios incluso en
:: HKCU), asi que este paso se autoeleva con UAC si el script no es Admin. El
:: certificado ya esta en el perfil del usuario, la politica es generica (por
:: emisor), asi que escribirla a nivel de maquina es correcto.
:: Se reutiliza la entrada existente del mismo servidor o se crea un indice nuevo,
:: para NO pisar politicas de otros servidores ya registrados.
if exist "%POLICY_PS1%" del "%POLICY_PS1%" >nul 2>&1
>>"%POLICY_PS1%" echo $Url = '!SERVER_URL!'
>>"%POLICY_PS1%" echo $Log = '%LOGFILE%'
>>"%POLICY_PS1%" echo $id = [Security.Principal.WindowsIdentity]::GetCurrent()
>>"%POLICY_PS1%" echo $isAdmin = (New-Object Security.Principal.WindowsPrincipal $id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
>>"%POLICY_PS1%" echo if (-not $isAdmin) {
>>"%POLICY_PS1%" echo   try {
>>"%POLICY_PS1%" echo     $p = Start-Process powershell -Verb RunAs -Wait -PassThru -WindowStyle Hidden -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"')
>>"%POLICY_PS1%" echo     if ($null -eq $p.ExitCode) { exit 1 } else { exit $p.ExitCode }
>>"%POLICY_PS1%" echo   } catch { Add-Content $Log ('  Politica FAIL: UAC cancelado o sin permisos - ' + $_.Exception.Message); exit 1 }
>>"%POLICY_PS1%" echo }
>>"%POLICY_PS1%" echo $value = '{"pattern":"' + $Url + '","filter":{"ISSUER":{"CN":"Aquafrisch Machine CA"}}}'
>>"%POLICY_PS1%" echo $rc = 0
>>"%POLICY_PS1%" echo foreach ($rel in 'SOFTWARE\Policies\Microsoft\Edge\AutoSelectCertificateForUrls','SOFTWARE\Policies\Google\Chrome\AutoSelectCertificateForUrls') {
>>"%POLICY_PS1%" echo   $key = 'HKLM:\' + $rel
>>"%POLICY_PS1%" echo   try {
>>"%POLICY_PS1%" echo     if (-not (Test-Path $key)) { New-Item -Path $key -Force -ErrorAction Stop ^| Out-Null }
>>"%POLICY_PS1%" echo     $item = Get-Item $key
>>"%POLICY_PS1%" echo     $name = $null; $max = 0; $n = 0
>>"%POLICY_PS1%" echo     foreach ($vn in $item.GetValueNames()) {
>>"%POLICY_PS1%" echo       if ([string]$item.GetValue($vn) -like ('*"pattern":"' + $Url + '"*')) { $name = $vn }
>>"%POLICY_PS1%" echo       if ([int]::TryParse($vn, [ref]$n) -and $n -gt $max) { $max = $n }
>>"%POLICY_PS1%" echo     }
>>"%POLICY_PS1%" echo     if (-not $name) { $name = [string]($max + 1) }
>>"%POLICY_PS1%" echo     New-ItemProperty -Path $key -Name $name -Value $value -PropertyType String -Force -ErrorAction Stop ^| Out-Null
>>"%POLICY_PS1%" echo     Add-Content $Log ('  Politica OK  : ' + $key + '\' + $name + ' = ' + $value)
>>"%POLICY_PS1%" echo   } catch { Add-Content $Log ('  Politica FAIL: ' + $key + ' - ' + $_.Exception.Message); $rc = 1 }
>>"%POLICY_PS1%" echo }
>>"%POLICY_PS1%" echo exit $rc
if not "!IS_ADMIN!"=="1" (
    echo  [INFO] Se pedira permiso de Administrador ^(UAC^) SOLO para este paso.
    echo         El certificado ya esta instalado para %USERNAME%; acepta el aviso.
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%POLICY_PS1%" >>"%LOGFILE%" 2>&1
set "POL_RC=!errorlevel!"
>>"%LOGFILE%" echo autoselect policy exit code = !POL_RC!
if "!POL_RC!"=="0" (
    echo  [OK] Politica de autoseleccion escrita en HKLM para !SERVER_URL!.
) else (
    echo  [AVISO] No se pudo escribir la politica AutoSelectCertificateForUrls ^(ver log^).
    echo          Sin ella, al abrir !SERVER_URL! el navegador mostrara un selector
    echo          de certificado: elige "%COMPUTERNAME%" ^(NO lo canceles^).
)

:: Verificacion end-to-end: presentar el cert al servidor y comprobar que lo
:: reconoce como este equipo (misma prueba que hara el navegador).
echo  [mTLS check] Verificando que el servidor reconoce este equipo...
set "MACHINE_THUMB="
for /f "delims=" %%T in ('powershell -NoProfile -Command "Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=%COMPUTERNAME%' -and $_.Issuer -like '*Aquafrisch Machine CA*' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } | Sort-Object NotAfter -Descending | Select-Object -First 1 -ExpandProperty Thumbprint" 2^>nul') do set "MACHINE_THUMB=%%T"
if not defined MACHINE_THUMB (
    echo  [ERROR] No se encuentra el certificado de equipo en el perfil de %USERNAME%.
    >>"%LOGFILE%" echo [ERROR] machine cert not found in CurrentUser\My after enrollment
    set "EXITCODE=11"
    goto :end
)
>>"%LOGFILE%" echo mtls-info self-check with cert !MACHINE_THUMB!
curl.exe -k -s --max-time 15 --cert "CurrentUser\MY\!MACHINE_THUMB!" -o "%MTLS_INFO%" "!SERVER_URL!/api/certificate/mtls-info" >>"%LOGFILE%" 2>&1
type "%MTLS_INFO%" >>"%LOGFILE%" 2>&1
findstr /C:"\"machineName\":\"%COMPUTERNAME%\"" "%MTLS_INFO%" >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] El servidor NO reconoce el certificado de este equipo.
    echo          Posibles causas: la Machine CA del servidor cambio ^(redeploy^) o el
    echo          registro fue revocado. Pide un codigo nuevo y vuelve a registrar.
    >>"%LOGFILE%" echo [ERROR] mtls-info self-check failed
    set "EXITCODE=12"
    goto :end
)
echo  [OK] VERIFICADO: el servidor reconoce este equipo como %COMPUTERNAME%.

:: Cerrar todos los navegadores para forzar nuevo handshake TLS con el certificado nuevo
echo  [OK] Cerrando navegadores para aplicar el nuevo certificado...
taskkill /F /IM chrome.exe /T >nul 2>&1
taskkill /F /IM msedge.exe /T >nul 2>&1
taskkill /F /IM firefox.exe /T >nul 2>&1
taskkill /F /IM brave.exe /T >nul 2>&1
taskkill /F /IM opera.exe /T >nul 2>&1
taskkill /F /IM vivaldi.exe /T >nul 2>&1
timeout /t 2 /nobreak >nul
echo  [OK] Equipo %COMPUTERNAME% registrado. Reabre el navegador.
>>"%LOGFILE%" echo mTLS enrollment completed for %COMPUTERNAME%

:mtls_done

:: -----------------------------------------------------------------
:: 5/5  Limpieza
:: -----------------------------------------------------------------
echo.
echo  [5/5] Limpiando ficheros temporales...
del "!CERT_FILE!" >nul 2>&1
del "%MTLS_INFO%" "%MACHINECA_FILE%" "%INF_FILE%" "%CSR_FILE%" "%JSON_FILE%" "%MACHINE_CER%" "%POLICY_PS1%" >nul 2>&1
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
