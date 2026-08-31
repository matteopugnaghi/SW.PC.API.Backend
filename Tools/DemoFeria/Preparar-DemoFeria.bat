@echo off
setlocal
REM Empaqueta la demo autonoma de feria (no requiere administrador)
echo.
echo  ==============================================
echo   PREPARAR DEMO FERIA - Aquafrisch Supervisor
echo  ==============================================
echo.
echo Proyectos disponibles en este repo:
for /D %%P in ("%~dp0..\..\Projects\*") do if /I not "%%~nxP"=="_template" echo    - %%~nxP
echo.
set "PROJECTS="
set /p PROJECTS=Proyectos a incluir (separados por coma) [A72.TOUTWP]: 
if "%PROJECTS%"=="" set "PROJECTS=A72.TOUTWP"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Preparar-DemoFeria.ps1" -Projects "%PROJECTS%"
echo.
pause
