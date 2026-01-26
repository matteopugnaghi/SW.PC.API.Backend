@echo off
REM ============================================
REM Start-ServidorEmpresa.bat
REM Arranca Aquafrisch Supervisor en modo DEVELOPMENT
REM (El selector de proyectos FUNCIONA)
REM ============================================

title Aquafrisch Supervisor - Servidor Empresa (Development)

cd /d "C:\Aquafrisch Supervisor\Backend"

echo.
echo ============================================
echo  AQUAFRISCH SUPERVISOR - SERVIDOR EMPRESA
echo  Modo: DEVELOPMENT (Selector habilitado)
echo ============================================
echo.
echo  URLs de acceso:
echo    HTTP:  http://localhost:5000
echo    HTTPS: https://localhost:5001
echo    Red:   http://192.168.2.199:5000
echo.
echo  Para PARAR: Ctrl+C o cerrar esta ventana
echo ============================================
echo.

REM Configurar modo Development
set ASPNETCORE_ENVIRONMENT=Development

REM Arrancar el servidor
SW.PC.API.Backend.exe

pause
