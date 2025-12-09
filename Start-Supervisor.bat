@echo off
echo ============================================
echo  AQUAFRISCH SUPERVISOR - Inicio Manual
echo ============================================
echo.

:: Verificar si .NET 8 ASP.NET Core Runtime esta instalado
echo [*] Verificando .NET Runtime...
set "DOTNET_PATH=C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App"
if exist "%DOTNET_PATH%\8.*" (
    echo [OK] .NET 8 Runtime encontrado
    goto :START_APP
)

:: Si no existe, intentar instalar
echo [!] .NET 8 Runtime no encontrado
set "INSTALLER=C:\Aquafrisch Supervisor\Installers\aspnetcore-runtime-8.0.22-win-x64.exe"
if not exist "%INSTALLER%" (
    echo [X] No se encontro el instalador de .NET
    echo     Descargalo de: https://dotnet.microsoft.com/download/dotnet/8.0
    echo     Y copialo a: %INSTALLER%
    pause
    exit /b 1
)

echo [*] Instalando .NET 8 Runtime...
echo     Esto puede tardar unos minutos, por favor espera...
"%INSTALLER%" /install /quiet /norestart
set INSTALL_RESULT=%ERRORLEVEL%

if %INSTALL_RESULT% EQU 0 (
    echo [OK] .NET 8 Runtime instalado correctamente
    goto :START_APP
)
if %INSTALL_RESULT% EQU 1638 (
    echo [OK] .NET 8 Runtime ya estaba instalado
    goto :START_APP
)
if %INSTALL_RESULT% EQU 3010 (
    echo [OK] .NET 8 Runtime instalado - Se requiere reinicio
    echo     Por favor reinicia el PC y vuelve a ejecutar este script
    pause
    exit /b 0
)

echo [X] Error instalando .NET Runtime (codigo: %INSTALL_RESULT%)
echo     Intenta instalar manualmente: %INSTALLER%
pause
exit /b 1

:START_APP
echo.
echo Iniciando servidor en http://localhost:5000
echo Acceso remoto: http://%COMPUTERNAME%:5000
echo Presiona Ctrl+C para detener
echo.
cd /d "C:\Aquafrisch Supervisor\Backend"
SW.PC.API.Backend.exe
pause
