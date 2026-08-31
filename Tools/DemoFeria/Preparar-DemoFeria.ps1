param(
    [Parameter(Mandatory = $true)][string[]]$Projects,
    [string]$Destination = 'C:\AquafrischDemo',
    [switch]$SkipPublish
)

# =====================================================================
# Preparar-DemoFeria.ps1 - Empaqueta una demo autonoma del Supervisor
#
# Genera en $Destination una carpeta PORTABLE (copiar a cualquier portatil):
#   Backend\  -> backend self-contained + frontend + proyectos + certificado
#   Backend\Iniciar-Demo.bat -> menu de proyectos y arranque
#
# USO (en el PC de desarrollo):
#   .\Preparar-DemoFeria.ps1 -Projects A72.TOUTWP
#   .\Preparar-DemoFeria.ps1 -Projects A72.TOUTWP,C07.LANBWP -SkipPublish
#
# En el portatil de feria NO hace falta instalar nada (.NET incluido).
# =====================================================================

$ErrorActionPreference = 'Stop'
function Write-Step($m) { Write-Host "[Demo] $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "[Demo] $m" -ForegroundColor Green }

# Aceptar tambien "A72.TOUTWP,C07.LANBWP" como cadena unica (llamada desde .bat)
$Projects = @($Projects | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$publishDir = Join-Path $repoRoot 'publish'
$frontendBuild = Join-Path (Split-Path -Parent $repoRoot) 'SW.PC.REACT.Frontend\my-3d-app\build'
$backendDst = Join-Path $Destination 'Backend'
$certPassword = 'Aquafrisch2024!'   # debe coincidir con appsettings.Production.json

# --- PASO 1: publish self-contained (incluye runtime .NET) ---
if (-not $SkipPublish) {
    Write-Step 'Publicando backend self-contained (puede tardar unos minutos)...'
    dotnet publish (Join-Path $repoRoot 'SW.PC.API.Backend.csproj') -c Release -r win-x64 --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fallo' }
}
if (-not (Test-Path (Join-Path $publishDir 'SW.PC.API.Backend.exe'))) {
    throw "No existe $publishDir\SW.PC.API.Backend.exe - ejecuta sin -SkipPublish"
}
if (-not (Test-Path (Join-Path $frontendBuild 'index.html'))) {
    throw "No existe el build del frontend en $frontendBuild - ejecuta 'npm run build'"
}
foreach ($p in $Projects) {
    if (-not (Test-Path (Join-Path $repoRoot "Projects\$p\config"))) {
        throw "Proyecto no encontrado: Projects\$p"
    }
}

# --- PASO 2: copiar backend ---
Write-Step "Copiando backend a $backendDst..."
robocopy $publishDir $backendDst /E /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy backend fallo (code $LASTEXITCODE)" }

# --- PASO 3: frontend build -> wwwroot (espejo: elimina hashes antiguos) ---
Write-Step 'Copiando frontend (wwwroot)...'
robocopy $frontendBuild (Join-Path $backendDst 'wwwroot') /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy wwwroot fallo (code $LASTEXITCODE)" }

# --- PASO 4: proyectos (sin backups) ---
foreach ($p in $Projects) {
    Write-Step "Copiando proyecto $p..."
    robocopy (Join-Path $repoRoot "Projects\$p") (Join-Path $backendDst "Projects\$p") /E /XD backups /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy proyecto $p fallo (code $LASTEXITCODE)" }
}

# --- PASO 5: proyecto activo por defecto ---
@{ activeProject = $Projects[0]; description = 'Proyecto activo demo feria' } |
    ConvertTo-Json | Set-Content (Join-Path $backendDst 'active-project.json') -Encoding UTF8

# --- PASO 6: certificado HTTPS autofirmado (password estandar del appsettings) ---
Write-Step 'Generando certificate.pfx...'
$cert = New-SelfSignedCertificate -DnsName 'aqf-demo', 'localhost', $env:COMPUTERNAME `
    -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(5) -FriendlyName 'Aquafrisch Demo Feria'
$securePwd = ConvertTo-SecureString $certPassword -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $backendDst 'certificate.pfx') -Password $securePwd | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force

# --- PASO 7: lanzador con menu de proyectos ---
$bat = @'
@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"
echo.
echo  ============================================
echo   DEMO AQUAFRISCH SUPERVISOR - FERIA
echo  ============================================
echo.
echo Proyectos disponibles:
set i=0
for /D %%P in ("Projects\*") do (
  set /a i+=1
  set "proj!i!=%%~nxP"
  echo    !i!. %%~nxP
)
if %i%==0 ( echo No hay proyectos en Projects\ & pause & exit /b 1 )
set /p sel=Elige proyecto [1-%i%]: 
set "SUPERVISOR_PROJECT=!proj%sel%!"
if "!SUPERVISOR_PROJECT!"=="" ( echo Seleccion no valida & pause & exit /b 1 )
set "ASPNETCORE_ENVIRONMENT=Production"
netsh advfirewall firewall add rule name="Aquafrisch Demo 5001" dir=in action=allow protocol=TCP localport=5001 >nul 2>&1
echo.
echo IPs de este portatil (en la Quest: https://IP:5001):
for /f "tokens=2 delims=:" %%A in ('ipconfig ^| findstr /C:"IPv4"') do echo   %%A
echo.
echo Proyecto activo: !SUPERVISOR_PROJECT!
echo Arrancando backend... (cerrar esta ventana detiene la demo)
echo.
SW.PC.API.Backend.exe
pause
'@
Set-Content (Join-Path $backendDst 'Iniciar-Demo.bat') -Value $bat -Encoding ASCII

# --- PASO 8: incluir scripts de red y guia en el paquete (autocontenido) ---
Write-Step 'Copiando scripts de red al paquete...'
foreach ($f in 'Setup-VpnBridge.ps1', 'Iniciar-Hotspot-Demo.bat', 'Iniciar-Puente-VPN.bat', 'Detener-Puente-VPN.bat', 'LEEME-FERIA.txt') {
    Copy-Item (Join-Path $PSScriptRoot $f) $Destination -Force
}

# --- RESUMEN ---
Write-Host ''
Write-Ok '=================================================='
Write-Ok " PAQUETE DEMO LISTO EN: $Destination"
Write-Ok "  Proyectos: $($Projects -join ', ')"
Write-Ok '  Copiar la CARPETA COMPLETA al portatil de feria.'
Write-Ok '  Uso en el portatil:'
Write-Ok '   1. Iniciar-Hotspot-Demo.bat  (WiFi AQF-DEMO)'
Write-Ok '   2. Backend\Iniciar-Demo.bat  (1a vez como Admin)'
Write-Ok '   3. Quest -> WiFi AQF-DEMO -> https://192.168.137.1:5001'
Write-Ok '  (opcion B: FortiClient + Iniciar-Puente-VPN.bat)'
Write-Ok '=================================================='
Write-Host ''
Write-Host 'RECORDATORIOS por cada proyecto empaquetado (editar su ProjectConfig.xlsm):' -ForegroundColor Yellow
Write-Host '  - System Config -> Simulated PLC = TRUE  (sin PLC real la demo se arrastra)' -ForegroundColor Yellow
Write-Host '  - System Config -> WebXREnabled = TRUE   (boton VR)' -ForegroundColor Yellow
Write-Host '  - El Excel DEBE llamarse ProjectConfig.xlsm' -ForegroundColor Yellow
