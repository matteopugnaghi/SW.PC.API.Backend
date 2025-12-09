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
    [string]$TargetPassword = 'Aqua2014$$',
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
    Write-Step "Limpiando carpetas de compilacion anterior..."
    Remove-Item -Recurse -Force "$BackendPath\publish" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$BackendPath\bin" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$BackendPath\obj" -ErrorAction SilentlyContinue
    Write-Success "Carpetas limpiadas"
    
    Write-Step "dotnet publish -c Release (self-contained)..."
    Push-Location $BackendPath
    try {
        # Verificar version del SDK (debe ser 8.0.1xx por global.json)
        $sdkVersion = & dotnet --version 2>&1
        Write-Info "SDK version: $sdkVersion"
        
        # Publicar como SELF-CONTAINED para incluir el runtime
        $publishOutput = & dotnet publish -c Release -o "$BackendPath\publish" --self-contained true -r win-x64 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error2 "Error compilando backend:"
            Write-Host $publishOutput -ForegroundColor Red
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        
        Write-Success "Backend compilado (self-contained) en: $BackendPath\publish"
        Write-Info "El runtime .NET esta incluido - no requiere instalacion en destino"
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

# Primero desconectar cualquier conexion existente (ignorar errores completamente)
try { net use "\\$TargetIP\C`$" /delete /y 2>&1 | Out-Null } catch { }

# Conectar al recurso compartido
$netArgs = @("use", "\\$TargetIP\C`$", "/user:$TargetIP\$TargetUser", $TargetPassword)
$netUseResult = & net @netArgs 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error2 "No se puede conectar a $TargetIP : $netUseResult"
    Write-Info "Verifica: 1) El PC esta encendido, 2) Credenciales correctas, 3) Firewall permite conexiones"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}
Write-Success "Conexion establecida con $TargetIP"

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
# PASO 8.1: Copiar Modelos 3D del Backend (FUENTE PRINCIPAL)
# ============================================
Write-Header "PASO 8.1: Copiando Modelos 3D del Backend"

$backendModelsPath = "$BackendPath\wwwroot\models"
$remoteModelsPath = "$RemotePath\Backend\wwwroot\models"

if (Test-Path $backendModelsPath) {
    Write-Step "Copiando modelos 3D desde Backend\wwwroot\models..."
    
    # Crear carpeta models si no existe
    if (-not (Test-Path $remoteModelsPath)) {
        New-Item -ItemType Directory -Path $remoteModelsPath -Force | Out-Null
    }
    
    # Copiar todos los modelos (incluyendo subcarpetas como Pumps)
    Copy-Item -Path "$backendModelsPath\*" -Destination $remoteModelsPath -Recurse -Force
    
    # Contar archivos copiados
    $modelFiles = Get-ChildItem -Path $backendModelsPath -Recurse -File
    $modelCount = $modelFiles.Count
    $subfolders = Get-ChildItem -Path $backendModelsPath -Directory
    
    Write-Success "Modelos 3D copiados: $modelCount archivos"
    
    if ($subfolders.Count -gt 0) {
        Write-Info "Subcarpetas incluidas: $($subfolders.Name -join ', ')"
    }
    
    # Listar tipos de archivos
    $extensions = $modelFiles | Group-Object Extension | ForEach-Object { "$($_.Count) $($_.Name)" }
    Write-Info "Tipos: $($extensions -join ', ')"
} else {
    Write-Info "No se encontraron modelos en Backend\wwwroot\models"
}

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

# ============================================================
# PASO 9.1: Gestionar Base de Datos SQLite (con backup)
# ============================================================
Write-Header "PASO 9.1: Gestionando Base de Datos"

$dbSourceDir = "$BackendPath\Data"
$dbRemoteDir = "$RemotePath\Backend\Data"
$dbRemotePath = "$dbRemoteDir\Aquafrisch.db"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Crear carpeta Data si no existe
if (-not (Test-Path $dbRemoteDir)) {
    New-Item -ItemType Directory -Path $dbRemoteDir -Force | Out-Null
    Write-Info "Carpeta Data creada en destino"
}

# Verificar si existe DB en destino
if (Test-Path $dbRemotePath) {
    Write-Info "Base de datos existente encontrada en destino"
    
    # Crear backup de la DB existente
    $backupDir = "$RemotePath\Backend\Data\backups"
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    }
    
    $backupPath = "$backupDir\Aquafrisch_backup_$timestamp.db"
    Write-Step "Creando backup: Aquafrisch_backup_$timestamp.db"
    Copy-Item -Path $dbRemotePath -Destination $backupPath -Force
    
    # También backup de los archivos WAL si existen
    if (Test-Path "$dbRemoteDir\Aquafrisch.db-wal") {
        Copy-Item -Path "$dbRemoteDir\Aquafrisch.db-wal" -Destination "$backupDir\Aquafrisch_backup_$timestamp.db-wal" -Force
    }
    if (Test-Path "$dbRemoteDir\Aquafrisch.db-shm") {
        Copy-Item -Path "$dbRemoteDir\Aquafrisch.db-shm" -Destination "$backupDir\Aquafrisch_backup_$timestamp.db-shm" -Force
    }
    
    Write-Success "Backup creado correctamente"
    Write-Info "La base de datos existente se MANTIENE (usuarios y sesiones preservados)"
    
    # Limpiar backups antiguos (mantener últimos 5)
    $oldBackups = Get-ChildItem "$backupDir\Aquafrisch_backup_*.db" -ErrorAction SilentlyContinue | 
                  Sort-Object LastWriteTime -Descending | 
                  Select-Object -Skip 5
    if ($oldBackups) {
        $oldBackups | Remove-Item -Force
        Write-Info "Backups antiguos limpiados (se mantienen los últimos 5)"
    }
} else {
    # Primera instalación - copiar DB inicial si existe
    $dbSourcePath = "$dbSourceDir\Aquafrisch.db"
    if (Test-Path $dbSourcePath) {
        Write-Step "Primera instalación - Copiando base de datos inicial..."
        Copy-Item -Path $dbSourcePath -Destination $dbRemotePath -Force
        Write-Success "Base de datos inicial copiada"
        Write-Info "Usuarios por defecto creados (admin/operator)"
    } else {
        Write-Info "No hay DB local. Se creará automáticamente al iniciar el servidor."
    }
}

# ============================================================
# PASO 9.2: Copiar archivos de estado (si no existen)
# ============================================================
Write-Header "PASO 9.2: Archivos de estado"

# integrity-state.json - solo copiar si no existe (preservar estado)
$integritySource = "$BackendPath\integrity-state.json"
$integrityDest = "$RemotePath\Backend\integrity-state.json"
if ((Test-Path $integritySource) -and -not (Test-Path $integrityDest)) {
    Copy-Item -Path $integritySource -Destination $integrityDest -Force
    Write-Info "integrity-state.json copiado (primera instalación)"
} else {
    Write-Info "integrity-state.json preservado en destino"
}

# Crear carpeta audit si no existe
$auditDir = "$RemotePath\Backend\wwwroot\audit"
if (-not (Test-Path $auditDir)) {
    New-Item -ItemType Directory -Path $auditDir -Force | Out-Null
    Write-Info "Carpeta audit creada"
}

# ============================================================
# PASO 9.4: Generar certificado SSL autofirmado
# ============================================================
Write-Header "PASO 9.4: Generando certificado SSL"

# Ruta LOCAL en el PC remoto (no UNC path)
$certLocalPath = "$InstallPath\Backend\certificate.pfx"
$certPassword = "Aquafrisch2024!"

Write-Step "Generando certificado SSL autofirmado en PC remoto..."
try {
    $certResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        param($CertPath, $CertPassword, $TargetIP)
        
        # Eliminar certificado existente si hay
        Remove-Item -Path $CertPath -Force -ErrorAction SilentlyContinue
        
        # Crear certificado autofirmado
        $cert = New-SelfSignedCertificate `
            -DnsName "localhost", $env:COMPUTERNAME, $TargetIP, "aquafrisch-supervisor" `
            -CertStoreLocation "Cert:\LocalMachine\My" `
            -NotAfter (Get-Date).AddYears(10) `
            -FriendlyName "Aquafrisch Supervisor SSL" `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
        
        # Exportar a PFX
        $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $securePassword | Out-Null
        
        # Añadir al almacén de certificados raíz de confianza (para evitar advertencias locales)
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $store.Open("ReadWrite")
        $store.Add($cert)
        $store.Close()
        
        # Verificar que el archivo existe
        if (Test-Path $CertPath) {
            return "OK: Certificado SSL generado: $CertPath"
        } else {
            return "ERROR: No se pudo crear el archivo de certificado"
        }
    } -ArgumentList $certLocalPath, $certPassword, $TargetIP -ErrorAction Stop
    
    Write-Success $certResult
    Write-Info "El certificado es valido por 10 años"
    Write-Info "Contraseña del certificado: $certPassword"
} catch {
    Write-Error2 "No se pudo generar certificado SSL via WinRM: $_"
    Write-Info "Generando certificado localmente y copiando..."
    
    # Plan B: Generar localmente y copiar via SMB
    try {
        $localCertPath = "$BackendPath\publish\certificate.pfx"
        
        # Crear certificado local
        $cert = New-SelfSignedCertificate `
            -DnsName "localhost", $TargetIP, "aquafrisch-supervisor" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(10) `
            -FriendlyName "Aquafrisch Supervisor SSL" `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
        
        # Exportar
        $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $localCertPath -Password $securePassword | Out-Null
        
        # Copiar al remoto
        Copy-Item -Path $localCertPath -Destination "$RemotePath\Backend\certificate.pfx" -Force
        
        # Limpiar certificado local del almacén
        Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue
        Remove-Item -Path $localCertPath -Force -ErrorAction SilentlyContinue
        
        Write-Success "Certificado generado localmente y copiado al servidor"
        Write-Info "El certificado es valido por 10 años"
    } catch {
        Write-Error2 "No se pudo generar certificado: $_"
        Write-Info "IMPORTANTE: Debes generar el certificado manualmente en el servidor:"
        Write-Info '  $cert = New-SelfSignedCertificate -DnsName "localhost","192.168.2.161" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10)'
        Write-Info '  $pwd = ConvertTo-SecureString -String "Aquafrisch2024!" -Force -AsPlainText'
        Write-Info '  Export-PfxCertificate -Cert $cert -FilePath "C:\Aquafrisch Supervisor\Backend\certificate.pfx" -Password $pwd'
    }
}

# ============================================================
# PASO 9.5: Copiar instalador de .NET Runtime
# ============================================================
Write-Header "PASO 9.5: Preparando instalador de .NET Runtime"

$dotnetInstallerLocal = "$BackendPath\Installers\aspnetcore-runtime-8.0.22-win-x64.exe"
$dotnetInstallerRemote = "$RemotePath\Installers\aspnetcore-runtime-8.0.22-win-x64.exe"

if (Test-Path $dotnetInstallerLocal) {
    Write-Step "Copiando instalador de .NET Runtime..."
    
    # Crear carpeta Installers en remoto
    if (-not (Test-Path "$RemotePath\Installers")) {
        New-Item -ItemType Directory -Path "$RemotePath\Installers" -Force | Out-Null
    }
    
    # Copiar instalador
    Copy-Item -Path $dotnetInstallerLocal -Destination $dotnetInstallerRemote -Force
    Write-Success "Instalador copiado a: C:\Aquafrisch Supervisor\Installers\"
    Write-Info "El script de inicio verificara e instalara .NET automaticamente si es necesario"
} else {
    Write-Info "No se encontro instalador de .NET en: $dotnetInstallerLocal"
    Write-Info "Si el PC destino no tiene .NET 8, descargalo de: https://dotnet.microsoft.com/download/dotnet/8.0"
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
if %ERRORLEVEL% EQU 0 (
    echo [OK] .NET 8 Runtime instalado correctamente
) else if %ERRORLEVEL% EQU 1638 (
    echo [OK] .NET 8 Runtime ya estaba instalado
) else if %ERRORLEVEL% EQU 3010 (
    echo [OK] .NET 8 Runtime instalado - Se requiere reinicio
    echo     Por favor reinicia el PC y vuelve a ejecutar este script
    pause
    exit /b 0
) else (
    echo [X] Error instalando .NET Runtime (codigo: %ERRORLEVEL%)
    echo     Intenta instalar manualmente: %INSTALLER%
    pause
    exit /b 1
)

:START_APP
echo.
echo Iniciando servidor...
echo   HTTP:  http://localhost:5000
echo   HTTPS: https://localhost:5001 (seguro)
echo.
echo Acceso remoto:
echo   HTTP:  http://%COMPUTERNAME%:5000
echo   HTTPS: https://%COMPUTERNAME%:5001 (recomendado)
echo.
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

Write-Step "Anadiendo reglas de firewall para puertos 5000 (HTTP) y 5001 (HTTPS)..."
try {
    $firewallResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        Remove-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTP" -ErrorAction SilentlyContinue
        Remove-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTPS" -ErrorAction SilentlyContinue
        Remove-NetFirewallRule -DisplayName "Aquafrisch Supervisor" -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTP" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow -Description "Permite acceso HTTP al servidor Aquafrisch Supervisor"
        New-NetFirewallRule -DisplayName "Aquafrisch Supervisor HTTPS" -Direction Inbound -Port 5001 -Protocol TCP -Action Allow -Description "Permite acceso HTTPS al servidor Aquafrisch Supervisor"
        return "Reglas de firewall creadas (HTTP:5000, HTTPS:5001)"
    } -ErrorAction SilentlyContinue
    
    if ($firewallResult) {
        Write-Success $firewallResult
    } else {
        Write-Info "No se pudo configurar firewall remotamente"
        Write-Info "Ejecuta manualmente en el PC destino (como Admin):"
        Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
        Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow" -ForegroundColor Yellow
    }
} catch {
    Write-Info "No se pudo configurar firewall: $_"
    Write-Info "Ejecuta manualmente en el PC destino (como Admin):"
    Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
    Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow" -ForegroundColor Yellow
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
Write-Host "  Modo: MANUAL (self-contained)" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Archivos desplegados:" -ForegroundColor Cyan
Write-Host "  - Backend (exe + dlls)     -> $InstallPath\Backend\" -ForegroundColor Gray
Write-Host "  - Frontend (React)         -> $InstallPath\Backend\wwwroot\" -ForegroundColor Gray
Write-Host "  - Modelos 3D               -> $InstallPath\Backend\wwwroot\models\" -ForegroundColor Gray
Write-Host "  - Base de datos (SQLite)   -> $InstallPath\Backend\Data\Aquafrisch.db" -ForegroundColor Gray
Write-Host "  - Certificado SSL          -> $InstallPath\Backend\certificate.pfx" -ForegroundColor Gray
Write-Host "  - Configuracion Excel      -> $InstallPath\ExcelConfigs\" -ForegroundColor Gray
Write-Host ""
Write-Host "  Base de datos:" -ForegroundColor Cyan
if (Test-Path "$RemotePath\Backend\Data\backups") {
    $backupCount = (Get-ChildItem "$RemotePath\Backend\Data\backups\Aquafrisch_backup_*.db" -ErrorAction SilentlyContinue).Count
    Write-Host "  - Estado: PRESERVADA (usuarios y sesiones mantenidos)" -ForegroundColor Green
    Write-Host "  - Backups disponibles: $backupCount (en Data\backups\)" -ForegroundColor Gray
} else {
    Write-Host "  - Estado: NUEVA (usuarios por defecto creados)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Para iniciar el supervisor:" -ForegroundColor Cyan
Write-Host "  1. Conectar al PC: $TargetIP (RDP o presencial)" -ForegroundColor White
Write-Host "  2. Ejecutar: $InstallPath\Start-Supervisor.bat" -ForegroundColor White
Write-Host ""
Write-Host "  URLs de acceso:" -ForegroundColor Cyan
Write-Host "  - HTTP:  http://${TargetIP}:5000" -ForegroundColor White
Write-Host "  - HTTPS: https://${TargetIP}:5001 (SEGURO - RECOMENDADO)" -ForegroundColor Green
Write-Host ""
Write-Host "  NOTA: El certificado SSL es autofirmado. El navegador mostrara" -ForegroundColor Yellow
Write-Host "        una advertencia la primera vez. Esto es normal en redes internas." -ForegroundColor Yellow
Write-Host ""
Write-Host "  O usar el acceso directo en el escritorio" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan

# Mantener ventana abierta
Write-Host ""
Read-Host "Presiona Enter para cerrar"
