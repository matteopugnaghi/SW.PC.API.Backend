@echo off
chcp 65001 >nul 2>&1
title Keyboard Filter - Bloqueo/Desbloqueo de Atajos
color 0E

echo.
echo  ========================================
echo   KEYBOARD FILTER - Atajos de Teclado
echo  ========================================
echo.
echo   1. BLOQUEAR atajos en el IPC
echo   2. DESBLOQUEAR atajos en el IPC
echo   3. Ver estado actual (DryRun)
echo   4. Salir
echo.

set /p OPCION="  Elige opcion (1-4): "

if "%OPCION%"=="4" exit /b 0
if "%OPCION%"=="3" goto DRYRUN
if "%OPCION%"=="2" goto DESBLOQUEAR
if "%OPCION%"=="1" goto BLOQUEAR

echo.
echo  Opcion no valida.
pause
exit /b 1

:BLOQUEAR
echo.
echo  --- BLOQUEAR atajos ---
set /p IP="  IP del IPC [192.168.2.161]: "
if "%IP%"=="" set IP=192.168.2.161
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Apply-KeyboardFilter.ps1" -ComputerName %IP%
goto FIN

:DESBLOQUEAR
echo.
echo  --- DESBLOQUEAR atajos ---
set /p IP="  IP del IPC [192.168.2.161]: "
if "%IP%"=="" set IP=192.168.2.161
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Apply-KeyboardFilter.ps1" -Remove -ComputerName %IP%
goto FIN

:DRYRUN
echo.
echo  --- VER ESTADO (sin cambios) ---
set /p IP="  IP del IPC [192.168.2.161]: "
if "%IP%"=="" set IP=192.168.2.161
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Apply-KeyboardFilter.ps1" -DryRun -ComputerName %IP%
goto FIN

:FIN
echo.
pause
