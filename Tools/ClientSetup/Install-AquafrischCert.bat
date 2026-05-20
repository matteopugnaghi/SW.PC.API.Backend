@echo off
chcp 65001 >nul
title Aquafrisch Supervisor - Instalar Certificado SSL (Offline)

setlocal EnableDelayedExpansion

echo ============================================================
echo  AQUAFRISCH SUPERVISOR - Instalacion de Certificado SSL
echo  (Version offline - distribuible por pendrive)
echo ============================================================
echo.

:: -----------------------------------------------------------------
:: Verificar permisos de administrador
:: -----------------------------------------------------------------
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] Este script requiere permisos de Administrador.
    echo  Cierra esta ventana, haz click derecho sobre el .bat
    echo  y selecciona "Ejecutar como administrador".
    echo.
    pause
    exit /b 1
)

:: -----------------------------------------------------------------
:: Pedir IP / hostname del servidor (default 192.168.2.161)
:: -----------------------------------------------------------------
set "DEFAULT_HOST=192.168.2.161"
set "DEFAULT_PORT=5001"

set /p "SERVER_HOST=  IP o hostname del servidor [%DEFAULT_HOST%]: "
if "!SERVER_HOST!"=="" set "SERVER_HOST=%DEFAULT_HOST%"

set /p "SERVER_PORT=  Puerto HTTPS [%DEFAULT_PORT%]: "
if "!SERVER_PORT!"=="" set "SERVER_PORT=%DEFAULT_PORT%"

set "SERVER_URL=https://!SERVER_HOST!:!SERVER_PORT!"
set "CERT_FILE=%TEMP%\aquafrisch-supervisor.cer"

echo.
echo  Servidor: !SERVER_URL!
echo.

:: -----------------------------------------------------------------
:: [1/4] Comprobar conectividad
:: -----------------------------------------------------------------
echo  [1/4] Comprobando conectividad con el servidor...
where curl.exe >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] curl.exe no esta disponible en este sistema.
    echo  Requiere Windows 10 1803 o superior.
    pause
    exit /b 1
)

:: -----------------------------------------------------------------
:: [2/4] Descargar certificado
:: -----------------------------------------------------------------
echo  [2/4] Descargando certificado desde !SERVER_URL!/api/certificate/public ...
if exist "!CERT_FILE!" del "!CERT_FILE!" >nul 2>&1

curl.exe -k -s --max-time 10 -o "!CERT_FILE!" "!SERVER_URL!/api/certificate/public"
if %errorlevel% neq 0 (
    echo  [ERROR] No se pudo descargar el certificado.
    echo  Verifica:
    echo    - El servidor esta accesible: !SERVER_URL!
    echo    - El firewall permite el puerto !SERVER_PORT!
    echo    - La IP / hostname es correcta
    pause
    exit /b 1
)

if not exist "!CERT_FILE!" (
    echo  [ERROR] El archivo de certificado no se ha creado.
    pause
    exit /b 1
)

for %%A in ("!CERT_FILE!") do set "CERT_SIZE=%%~zA"
if "!CERT_SIZE!"=="0" (
    echo  [ERROR] El certificado descargado esta vacio.
    echo  Posible causa: la URL no devuelve un certificado valido.
    del "!CERT_FILE!" >nul 2>&1
    pause
    exit /b 1
)
echo  [OK] Certificado descargado ^(!CERT_SIZE! bytes^)

:: -----------------------------------------------------------------
:: [3/4] Instalar en almacen Root de la maquina
:: -----------------------------------------------------------------
echo  [3/4] Instalando en "Entidades de certificacion raiz de confianza"...
certutil -addstore "Root" "!CERT_FILE!" >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] No se pudo instalar el certificado en el almacen Root.
    echo  Asegurate de estar ejecutando como Administrador.
    del "!CERT_FILE!" >nul 2>&1
    pause
    exit /b 1
)
echo  [OK] Certificado instalado correctamente

:: -----------------------------------------------------------------
:: [4/4] Limpieza
:: -----------------------------------------------------------------
echo  [4/4] Limpiando archivos temporales...
del "!CERT_FILE!" >nul 2>&1
echo  [OK] Limpieza completada

echo.
echo ============================================================
echo  INSTALACION COMPLETADA
echo ============================================================
echo.
echo  El certificado SSL de Aquafrisch Supervisor ha sido instalado.
echo.
echo  Siguientes pasos:
echo    1. Cierra y reabre el navegador (Chrome / Edge / Firefox*).
echo    2. Accede a: !SERVER_URL!
echo    3. No deberia aparecer el aviso de "conexion no segura".
echo.
echo  * Firefox usa su propio almacen de certificados:
echo      Ajustes -^> Privacidad y Seguridad -^> Certificados
echo      Importar el .cer manualmente en la pestana "Autoridades".
echo.
pause
endlocal
exit /b 0
