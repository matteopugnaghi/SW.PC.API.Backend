#Requires -Version 5.1
<#
.SYNOPSIS
    Aquafrisch Supervisor - Deploy Manual (Remote)
    Despliega el backend y frontend a un PC remoto para ejecucion MANUAL.

.DESCRIPTION
    Este script:
    1. Compila el Backend en modo Release
    2. Compila el Frontend (npm run build)
    3. Copia todo al PC remoto
    4. NO instala como servicio (ejecucion manual)

.NOTES
    Archivo: Deploy-Manual-Remote.ps1
    Autor: Aquafrisch
    Version: 1.0
    Fecha: 2024-12-05
    
    MODO: MANUAL
    Para servicio Windows usar: Deploy-Service-Remote.ps1 (futuro)

.EXAMPLE
    .\Deploy-Manual-Remote.ps1
    .\Deploy-Manual-Remote.ps1 -TargetIP "192.168.2.161"
#>

param(
    [string]$TargetIP = "192.168.2.161",
    [string]$TargetUser = "Administrator",
    [string]$TargetPassword = "Aqua2014$$",
    [string]$InstallPath = "C:\Aquafrisch Supervisor",
    [switch]$SkipBackendBuild,
    [switch]$SkipFrontendBuild,
    [switch]$BackupExisting
)

# ============================================
# CONFIGURACION
# ============================================
$ErrorActionPreference = "Stop"
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendPath = $ScriptPath
$FrontendPath = Join-Path (Split-Path -Parent $ScriptPath) "SW.PC.REACT.Frontend\my-3d-app"

# Colores para output
function Write-Header { param($text) Write-Host "`n$("="*60)" -ForegroundColor Cyan; Write-Host " $text" -ForegroundColor Cyan; Write-Host "$("="*60)" -ForegroundColor Cyan }
function Write-Step { param($text) Write-Host "[>] $text" -ForegroundColor Yellow }
function Write-Success { param($text) Write-Host "[OK] $text" -ForegroundColor Green }
function Write-Info { param($text) Write-Host "[i] $text" -ForegroundColor Gray }
function Write-Error2 { param($text) Write-Host "[X] $text" -ForegroundColor Red }

# ============================================
# INICIO
# ============================================
Clear-Host
Write-Header "AQUAFRISCH SUPERVISOR - DEPLOY MANUAL (REMOTE)"
Write-Host ""
Write-Info "Modo de ejecucion: MANUAL (no servicio)"
Write-Info "PC Destino: $TargetIP"
Write-Info "Ruta destino: $InstallPath"
Write-Host ""

# ============================================
# PASO 1: Verificar rutas locales
# ============================================
Write-Header "PASO 1: Verificando rutas locales"

if (-not (Test-Path $BackendPath)) {
    Write-Error2 "No se encuentra el Backend en: $BackendPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}
Write-Success "Backend encontrado: $BackendPath"

if (-not (Test-Path $FrontendPath)) {
    Write-Error2 "No se encuentra el Frontend en: $FrontendPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}
Write-Success "Frontend encontrado: $FrontendPath"

# ============================================
# PASO 2: Build Backend (Release)
# ============================================
Write-Header "PASO 2: Compilando Backend (Release)"

if ($SkipBackendBuild) {
    Write-Info "Saltando build del backend (flag -SkipBackendBuild)"
} else {
    Write-Step "dotnet publish -c Release..."
    Push-Location $BackendPath
    try {
        $publishOutput = & dotnet publish -c Release -o "$BackendPath\publish" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error2 "Error compilando backend:"
            Write-Host $publishOutput -ForegroundColor Red
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        Write-Success "Backend compilado en: $BackendPath\publish"
    } finally {
        Pop-Location
    }
}

# ============================================
# PASO 3: Build Frontend (npm run build)
# ============================================
Write-Header "PASO 3: Compilando Frontend (npm run build)"

if ($SkipFrontendBuild) {
    Write-Info "Saltando build del frontend (flag -SkipFrontendBuild)"
} else {
    Write-Step "npm run build..."
    Push-Location $FrontendPath
    try {
        # Usar cmd /c para evitar problemas con npm en PowerShell
        $npmOutput = & cmd /c "npm run build" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error2 "Error compilando frontend:"
            Write-Host $npmOutput -ForegroundColor Red
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        Write-Success "Frontend compilado en: $FrontendPath\build"
    } finally {
        Pop-Location
    }
}

# ============================================
# PASO 4: Conectar al PC remoto
# ============================================
Write-Header "PASO 4: Conectando al PC remoto ($TargetIP)"

$RemotePath = "\\$TargetIP\C`$\Aquafrisch Supervisor"
$SecurePassword = ConvertTo-SecureString $TargetPassword -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential ($TargetUser, $SecurePassword)

Write-Step "Estableciendo conexion de red..."
try {
    # Usar comillas simples para evitar interpretacion de $$ en password
    $netUseCmd = "net use `"\\$TargetIP\C`$`" /user:$TargetUser '$TargetPassword'"
    $netUseResult = Invoke-Expression $netUseCmd 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Error de conexion: $netUseResult"
    }
    Write-Success "Conexion establecida con $TargetIP"
} catch {
    Write-Error2 "No se puede conectar a $TargetIP : $_"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

# ============================================
# PASO 4.5: Parar proceso existente (si esta corriendo)
# ============================================
Write-Header "PASO 4.5: Verificando procesos existentes"

Write-Step "Comprobando si SW.PC.API.Backend esta corriendo..."
try {
    $result = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        $proc = Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue
        if ($proc) {
            Stop-Process -Name "SW.PC.API.Backend" -Force
            Start-Sleep -Seconds 2
            return "Proceso detenido"
        } else {
            return "No hay proceso corriendo"
        }
    } -ErrorAction SilentlyContinue
    
    if ($result) {
        Write-Success $result
    } else {
        Write-Info "No se pudo verificar remotamente (WinRM no disponible)"
        Write-Info "Si hay un proceso corriendo, detenlo manualmente antes de continuar"
    }
} catch {
    Write-Info "No se pudo verificar proceso remoto: $_"
    Write-Info "Si hay un proceso corriendo, detenlo manualmente"
}

# ============================================
# PASO 5: Crear estructura de carpetas
# ============================================
Write-Header "PASO 5: Creando estructura de carpetas"

$folders = @(
    $RemotePath,
    "$RemotePath\Backend",
    "$RemotePath\Backend\wwwroot",
    "$RemotePath\ExcelConfigs"
)

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        Write-Step "Creando: $folder"
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Success "Carpeta creada: $folder"
    } else {
        Write-Info "Ya existe: $folder"
    }
}

# ============================================
# PASO 6: Backup (opcional)
# ============================================
if ($BackupExisting -and (Test-Path "$RemotePath\Backend\SW.PC.API.Backend.exe")) {
    Write-Header "PASO 6: Creando backup"
    $backupName = "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    $backupPath = "$RemotePath\$backupName"
    Write-Step "Creando backup en: $backupPath"
    Copy-Item -Path "$RemotePath\Backend" -Destination $backupPath -Recurse -Force
    Write-Success "Backup creado: $backupPath"
} else {
    Write-Info "Saltando backup (no existe instalacion previa o flag no activado)"
}

# ============================================
# PASO 7: Copiar Backend
# ============================================
Write-Header "PASO 7: Copiando Backend"

$publishPath = "$BackendPath\publish"
if (-not (Test-Path $publishPath)) {
    Write-Error2 "No se encuentra la carpeta publish: $publishPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Step "Copiando archivos del backend..."
$backendFiles = Get-ChildItem -Path $publishPath -Recurse
$totalFiles = $backendFiles.Count

Copy-Item -Path "$publishPath\*" -Destination "$RemotePath\Backend" -Recurse -Force
Write-Success "Backend copiado: $totalFiles archivos"

# ============================================
# PASO 8: Copiar Frontend (wwwroot)
# ============================================
Write-Header "PASO 8: Copiando Frontend (wwwroot)"

$frontendBuildPath = "$FrontendPath\build"
if (-not (Test-Path $frontendBuildPath)) {
    Write-Error2 "No se encuentra el build del frontend: $frontendBuildPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Step "Copiando archivos del frontend..."
Copy-Item -Path "$frontendBuildPath\*" -Destination "$RemotePath\Backend\wwwroot" -Recurse -Force
Write-Success "Frontend copiado a wwwroot"

# ============================================
# PASO 9: Copiar Excel Config
# ============================================
Write-Header "PASO 9: Copiando Excel Config"

$excelSource = "$BackendPath\ExcelConfigs\ProjectConfig.xlsm"
if (Test-Path $excelSource) {
    Write-Step "Copiando ProjectConfig.xlsm..."
    Copy-Item -Path $excelSource -Destination "$RemotePath\ExcelConfigs\ProjectConfig.xlsm" -Force
    Write-Success "Excel copiado"
} else {
    Write-Info "No se encuentra Excel config local, saltando..."
}

# ============================================
# PASO 10: Crear script de inicio
# ============================================
Write-Header "PASO 10: Creando script de inicio"

$batContent = '@echo off
echo ============================================
echo  AQUAFRISCH SUPERVISOR - Inicio Manual
echo ============================================
echo.
echo Iniciando servidor en http://localhost:5000
echo Acceso remoto: http://%COMPUTERNAME%:5000
echo Presiona Ctrl+C para detener
echo.
cd /d "C:\Aquafrisch Supervisor\Backend"
SW.PC.API.Backend.exe
pause'

$startScriptPath = "$RemotePath\Start-Supervisor.bat"
Set-Content -Path $startScriptPath -Value $batContent -Encoding ASCII
Write-Success "Script de inicio creado: $startScriptPath"

# ============================================
# PASO 10.5: Configurar Firewall
# ============================================
Write-Header "PASO 10.5: Configurando Firewall"

Write-Step "Anadiendo regla de firewall para puerto 5000..."
try {
    $firewallResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        Remove-NetFirewallRule -DisplayName "Aquafrisch Supervisor" -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName "Aquafrisch Supervisor" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow -Description "Permite acceso al servidor Aquafrisch Supervisor desde la red local"
        return "Regla de firewall creada correctamente"
    } -ErrorAction SilentlyContinue
    
    if ($firewallResult) {
        Write-Success $firewallResult
    } else {
        Write-Info "No se pudo configurar firewall remotamente"
        Write-Info "Ejecuta manualmente en el PC destino (como Admin):"
        Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
    }
} catch {
    Write-Info "No se pudo configurar firewall: $_"
    Write-Info "Ejecuta manualmente en el PC destino (como Admin):"
    Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
}

# ============================================
# PASO 11: Crear acceso directo en escritorio
# ============================================
Write-Header "PASO 11: Creando acceso directo"

try {
    $WshShell = New-Object -ComObject WScript.Shell
    $DesktopPath = "\\$TargetIP\C`$\Users\$TargetUser\Desktop"
    
    if (Test-Path $DesktopPath) {
        $ShortcutPath = "$DesktopPath\Aquafrisch Supervisor.lnk"
        $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
        $Shortcut.TargetPath = "$InstallPath\Start-Supervisor.bat"
        $Shortcut.WorkingDirectory = "$InstallPath\Backend"
        $Shortcut.Description = "Iniciar Aquafrisch Supervisor"
        $Shortcut.Save()
        Write-Success "Acceso directo creado en el escritorio"
    } else {
        Write-Info "No se pudo acceder al escritorio remoto"
    }
} catch {
    Write-Info "No se pudo crear acceso directo: $_"
}

# ============================================
# PASO 12: Desconectar
# ============================================
Write-Header "PASO 12: Limpieza"

& net use "\\$TargetIP\C`$" /delete /y 2>&1 | Out-Null
Write-Success "Conexion de red cerrada"

# ============================================
# RESUMEN FINAL
# ============================================
Write-Header "DESPLIEGUE COMPLETADO"
Write-Host ""
Write-Host "  PC Destino: $TargetIP" -ForegroundColor White
Write-Host "  Ruta: $InstallPath" -ForegroundColor White
Write-Host "  Modo: MANUAL" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Para iniciar el supervisor:" -ForegroundColor Cyan
Write-Host "  1. Conectar al PC: $TargetIP (RDP o presencial)" -ForegroundColor White
Write-Host "  2. Ejecutar: C:\Aquafrisch Supervisor\Start-Supervisor.bat" -ForegroundColor White
Write-Host ""
Write-Host "  URLs de acceso:" -ForegroundColor Cyan
Write-Host "  - Local:  http://localhost:5000" -ForegroundColor White
Write-Host "  - Remoto: http://${TargetIP}:5000" -ForegroundColor Green
Write-Host ""
Write-Host "  O usar el acceso directo en el escritorio" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan

# Mantener ventana abierta
Write-Host ""
Read-Host "Presiona Enter para cerrar"
