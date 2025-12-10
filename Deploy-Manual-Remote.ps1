#Requires -Version 5.1
<#
.SYNOPSIS
    Aquafrisch Supervisor - Deploy Manual (Remote)
    Despliega el backend y frontend a un PC remoto para ejecucion MANUAL.

.DESCRIPTION
    Este script:
    1. Solicita el PROYECTO a desplegar (multi-proyecto)
    2. Compila el Backend en modo Release
    3. Compila el Frontend (npm run build)
    4. Copia todo al PC remoto
    5. Configura active-project.json con el proyecto seleccionado
    6. NO instala como servicio (ejecucion manual)

.NOTES
    Archivo: Deploy-Manual-Remote.ps1
    Autor: Aquafrisch
    Version: 2.0 (Multi-Proyecto)
    Fecha: 2025-12-10
    
    MODO: MANUAL
    Para servicio Windows usar: Deploy-Service-Remote.ps1 (futuro)

.EXAMPLE
    .\Deploy-Manual-Remote.ps1
    .\Deploy-Manual-Remote.ps1 -TargetIP "192.168.2.161"
    .\Deploy-Manual-Remote.ps1 -ProjectId "A70.AMITWP"
#>

param(
    [string]$TargetIP = "192.168.2.161",
    [string]$TargetUser = "Administrator",
    [string]$TargetPassword = 'Aqua2014$$',
    [string]$InstallPath = "C:\Aquafrisch Supervisor",
    [string]$ProjectId = "",  # Si se especifica, no pregunta
    [string]$LocalCopyPath = "",  # Ruta para guardar copia local (opcional)
    [switch]$SkipBackendBuild,
    [switch]$SkipFrontendBuild,
    [switch]$BackupExisting,
    [switch]$SaveLocalCopy  # Guardar copia local antes de enviar a remoto
)

# ============================================
# CONFIGURACION
# ============================================
$ErrorActionPreference = "Stop"
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendPath = $ScriptPath
$FrontendPath = Join-Path (Split-Path -Parent $ScriptPath) "SW.PC.REACT.Frontend\my-3d-app"
$ProjectsPath = Join-Path $BackendPath "Projects"

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
# PASO 0: Seleccionar Proyecto
# ============================================
Write-Header "PASO 0: Seleccion de Proyecto"

# Obtener lista de proyectos disponibles (excluir _template)
$availableProjects = Get-ChildItem -Path $ProjectsPath -Directory | 
    Where-Object { $_.Name -ne "_template" } |
    Select-Object -ExpandProperty Name

if ($availableProjects.Count -eq 0) {
    Write-Error2 "No se encontraron proyectos en: $ProjectsPath"
    Write-Info "Crea un proyecto primero en la carpeta Projects/"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Host ""
Write-Host "  Proyectos disponibles:" -ForegroundColor Cyan
Write-Host "  ----------------------" -ForegroundColor Cyan
$index = 1
foreach ($proj in $availableProjects) {
    $projectPath = Join-Path $ProjectsPath $proj
    $configExists = Test-Path (Join-Path $projectPath "config\ProjectConfig.xlsm")
    $modelsCount = (Get-ChildItem -Path (Join-Path $projectPath "models") -Filter "*.glb" -ErrorAction SilentlyContinue).Count
    $dbExists = Test-Path (Join-Path $projectPath "data\project.db")
    
    $status = @()
    if ($configExists) { $status += "Config" } else { $status += "Sin Config" }
    $status += "$modelsCount modelos"
    if ($dbExists) { $status += "DB" }
    
    Write-Host "  [$index] $proj" -ForegroundColor White -NoNewline
    Write-Host " ($($status -join ', '))" -ForegroundColor Gray
    $index++
}
# ❌ ELIMINADO: Modo legacy no permitido en producción
# Write-Host "  [0] default (modo legacy - ExcelConfigs/)" -ForegroundColor DarkGray
Write-Host ""

# Si no se especifico ProjectId, preguntar
if ([string]::IsNullOrEmpty($ProjectId)) {
    do {
        $selection = Read-Host "Selecciona el proyecto a desplegar (numero o nombre)"
        
        # Verificar si es numero
        if ($selection -match '^\d+$') {
            $selNum = [int]$selection
            if ($selNum -eq 0) {
                Write-Error2 "Modo legacy (default) NO está permitido en producción"
                Write-Info "Debes seleccionar un proyecto válido"
                continue
            } elseif ($selNum -ge 1 -and $selNum -le $availableProjects.Count) {
                $ProjectId = $availableProjects[$selNum - 1]
            } else {
                Write-Error2 "Numero invalido. Intenta de nuevo."
                continue
            }
        } else {
            # Es un nombre
            if ($selection -eq "default") {
                Write-Error2 "Modo legacy (default) NO está permitido en producción"
                Write-Info "Debes seleccionar un proyecto válido"
                continue
            } elseif ($availableProjects -contains $selection) {
                $ProjectId = $selection
            } else {
                Write-Error2 "Proyecto '$selection' no encontrado. Intenta de nuevo."
                continue
            }
        }
        break
    } while ($true)
}

# Validar proyecto seleccionado - NO permitir default
if ($ProjectId -eq "default") {
    Write-Error2 "Modo legacy (default) NO está permitido en producción"
    Write-Info "Debes especificar un proyecto válido con -ProjectId"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

if (-not ($availableProjects -contains $ProjectId)) {
    Write-Error2 "El proyecto '$ProjectId' no existe en $ProjectsPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Host ""
Write-Success "Proyecto seleccionado: $ProjectId"
Write-Info "Modo: MULTI-PROYECTO (usa Projects/$ProjectId/)"
Write-Host ""

# Preguntar si guardar copia local
if (-not $SaveLocalCopy) {
    $saveLocal = Read-Host "¿Guardar copia local del deploy? (s/N)"
    if ($saveLocal -eq 's' -or $saveLocal -eq 'S') {
        $SaveLocalCopy = $true
    }
}

if ($SaveLocalCopy) {
    if ([string]::IsNullOrEmpty($LocalCopyPath)) {
        $defaultLocalPath = Join-Path $BackendPath "LocalDeploys\$ProjectId`_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        $LocalCopyPath = Read-Host "Ruta para copia local [$defaultLocalPath]"
        if ([string]::IsNullOrEmpty($LocalCopyPath)) {
            $LocalCopyPath = $defaultLocalPath
        }
    }
    Write-Info "Copia local se guardará en: $LocalCopyPath"
}
Write-Host ""

# Confirmar
$confirm = Read-Host "Continuar con el despliegue de '$ProjectId' a $TargetIP? (S/n)"
if ($confirm -eq 'n' -or $confirm -eq 'N') {
    Write-Info "Despliegue cancelado por el usuario"
    exit 0
}

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
# PASO 2.1: Generar deploy-version.json (Software Integrity)
# ============================================
Write-Header "PASO 2.1: Generando deploy-version.json (Software Integrity)"

function Get-GitVersionInfo {
    param (
        [string]$RepoPath,
        [string]$ComponentName
    )
    
    Push-Location $RepoPath
    try {
        $gitDir = Join-Path $RepoPath ".git"
        if (-not (Test-Path $gitDir)) {
            Write-Warning "No es repositorio Git: $RepoPath"
            return $null
        }
        
        # Obtener información de Git
        $sha = (git rev-parse HEAD 2>$null) -replace "`n|`r", ""
        $shaShort = (git rev-parse --short HEAD 2>$null) -replace "`n|`r", ""
        $branch = (git rev-parse --abbrev-ref HEAD 2>$null) -replace "`n|`r", ""
        $version = (git describe --tags --always 2>$null) -replace "`n|`r", ""
        $commitDate = (git log -1 --format=%ci 2>$null) -replace "`n|`r", ""
        $author = (git log -1 --format=%an 2>$null) -replace "`n|`r", ""
        $authorEmail = (git log -1 --format=%ae 2>$null) -replace "`n|`r", ""
        $message = (git log -1 --format=%s 2>$null) -replace "`n|`r", ""
        $signatureCode = (git log -1 --format=%G? 2>$null) -replace "`n|`r", ""
        $signatureSigner = (git log -1 --format=%GS 2>$null) -replace "`n|`r", ""
        $signatureKey = (git log -1 --format=%GK 2>$null) -replace "`n|`r", ""
        $latestTag = (git tag --sort=-version:refname -l "20*" | Select-Object -First 1) -replace "`n|`r", ""
        
        # Mapear código de firma a estado legible
        $signatureStatus = switch ($signatureCode) {
            "G" { "SIGNED" }
            "B" { "BAD" }
            "U" { "UNTRUSTED" }
            "X" { "EXPIRED" }
            "Y" { "EXPIRED_KEY" }
            "R" { "REVOKED" }
            "E" { "NO_PUBKEY" }
            "N" { "UNSIGNED" }
            default { "N/A" }
        }
        
        return @{
            ComponentName = $ComponentName
            Version = if ($version) { $version } else { "0.0.0" }
            CommitSha = if ($shaShort) { $shaShort } else { "unknown" }
            CommitShaFull = if ($sha) { $sha } else { "unknown" }
            Branch = if ($branch) { $branch } else { "unknown" }
            CommitDate = if ($commitDate) { $commitDate } else { "" }
            CommitAuthor = if ($author) { $author } else { "" }
            CommitAuthorEmail = if ($authorEmail) { $authorEmail } else { "" }
            CommitMessage = if ($message) { $message } else { "" }
            LatestRelease = if ($latestTag) { $latestTag } else { "" }
            LatestReleaseDate = ""
            SignatureStatus = $signatureStatus
            SignatureSigner = if ($signatureSigner) { $signatureSigner } else { "" }
            SignatureKey = if ($signatureKey) { $signatureKey } else { "" }
            DeployedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
            DeployedFrom = $env:COMPUTERNAME
            DeployedBy = $env:USERNAME
        }
    } catch {
        Write-Warning "Error obteniendo info Git de $ComponentName : $_"
        return $null
    } finally {
        Pop-Location
    }
}

# Generar deploy-version.json para Backend
Write-Step "Generando deploy-version.json para Backend..."
$backendVersionInfo = Get-GitVersionInfo -RepoPath $BackendPath -ComponentName "Backend"
if ($backendVersionInfo) {
    $backendVersionJson = $backendVersionInfo | ConvertTo-Json -Depth 5
    $backendVersionPath = Join-Path "$BackendPath\publish" "deploy-version.json"
    Set-Content -Path $backendVersionPath -Value $backendVersionJson -Encoding UTF8
    Write-Success "Backend: v$($backendVersionInfo.Version) ($($backendVersionInfo.CommitSha)) - $($backendVersionInfo.SignatureStatus)"
} else {
    Write-Warning "No se pudo generar deploy-version.json para Backend"
}

# Generar deploy-version.json para Frontend (se copiara a wwwroot despues)
Write-Step "Generando deploy-version.json para Frontend..."
$frontendVersionInfo = Get-GitVersionInfo -RepoPath $FrontendPath -ComponentName "Frontend"
if ($frontendVersionInfo) {
    $frontendVersionJson = $frontendVersionInfo | ConvertTo-Json -Depth 5
    # Guardar temporalmente en la carpeta build del frontend
    $frontendBuildPath = Join-Path $FrontendPath "build"
    if (Test-Path $frontendBuildPath) {
        $frontendVersionPath = Join-Path $frontendBuildPath "deploy-version.json"
        Set-Content -Path $frontendVersionPath -Value $frontendVersionJson -Encoding UTF8
        Write-Success "Frontend: v$($frontendVersionInfo.Version) ($($frontendVersionInfo.CommitSha)) - $($frontendVersionInfo.SignatureStatus)"
    } else {
        Write-Warning "Carpeta build del frontend no existe aun - se generara despues del build"
        # Guardar en variable global para usar despues
        $global:FrontendVersionInfo = $frontendVersionInfo
    }
} else {
    Write-Warning "No se pudo generar deploy-version.json para Frontend"
}

Write-Success "Archivos deploy-version.json generados"

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
        
        # Generar deploy-version.json para Frontend (despues del build)
        Write-Step "Generando deploy-version.json para Frontend..."
        $frontendBuildPath = Join-Path $FrontendPath "build"
        if ($global:FrontendVersionInfo) {
            $frontendVersionJson = $global:FrontendVersionInfo | ConvertTo-Json -Depth 5
            $frontendVersionPath = Join-Path $frontendBuildPath "deploy-version.json"
            Set-Content -Path $frontendVersionPath -Value $frontendVersionJson -Encoding UTF8
            Write-Success "Frontend deploy-version.json generado"
        } elseif (-not (Test-Path (Join-Path $frontendBuildPath "deploy-version.json"))) {
            # Intentar generar si no existe
            $frontendVersionInfo = Get-GitVersionInfo -RepoPath $FrontendPath -ComponentName "Frontend"
            if ($frontendVersionInfo) {
                $frontendVersionJson = $frontendVersionInfo | ConvertTo-Json -Depth 5
                $frontendVersionPath = Join-Path $frontendBuildPath "deploy-version.json"
                Set-Content -Path $frontendVersionPath -Value $frontendVersionJson -Encoding UTF8
                Write-Success "Frontend deploy-version.json generado"
            }
        }
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

# 📦 Estructura optimizada para producción:
# - NO crear ExcelConfigs/ externo (legacy eliminado)
# - NO crear Backend/Projects/ genérico (solo el proyecto activo)
$folders = @(
    $RemotePath,
    "$RemotePath\Backend",
    "$RemotePath\Backend\wwwroot",
    "$RemotePath\Backend\Data"
)

# Añadir carpeta del proyecto activo (SOLO el proyecto que se despliega)
if ($ProjectId -ne "default") {
    $folders += "$RemotePath\Backend\Projects"
    $folders += "$RemotePath\Backend\Projects\$ProjectId"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\config"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\models"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\data"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\backups"
}

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
# PASO 5.1: Limpiar archivos/carpetas innecesarios (si existen)
# ============================================
Write-Header "PASO 5.1: Limpieza de archivos innecesarios"

# 🧹 Lista de archivos/carpetas a ELIMINAR si existen de deploys anteriores
$cleanupItems = @(
    "$RemotePath\ExcelConfigs",                           # Legacy folder (ya no se usa)
    "$RemotePath\Backend\wwwroot\robots.txt",             # SEO file (no necesario)
    "$RemotePath\Backend\wwwroot\asset-manifest.json",    # Debug file
    "$RemotePath\Backend\wwwroot\docs",                   # Documentación (ya en desarrollo)
    "$RemotePath\Backend\wwwroot\audit",                  # Logs de auditoría (se generan en runtime)
    "$RemotePath\Backend\wwwroot\models",                 # Modelos legacy (ahora en Projects/{id}/models)
    "$RemotePath\Backend\wwwroot\sbom",                   # SBOM generados (se regeneran en runtime)
    "$RemotePath\Backend\Projects\_template"              # Template de proyecto (solo para desarrollo)
)

$cleanedCount = 0
foreach ($item in $cleanupItems) {
    if (Test-Path $item) {
        Remove-Item -Path $item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Info "🧹 Eliminado: $item"
        $cleanedCount++
    }
}

if ($cleanedCount -gt 0) {
    Write-Success "Limpieza completada: $cleanedCount elementos eliminados"
} else {
    Write-Info "No se encontraron archivos innecesarios para limpiar"
}

# ============================================
# PASO 6: Backup del deploy existente (opcional)
# ============================================
if ($BackupExisting -and (Test-Path "$RemotePath\Backend\SW.PC.API.Backend.exe")) {
    Write-Header "PASO 6: Creando backup de la instalación existente"
    
    $backupTimestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupName = "Backup_${ProjectId}_$backupTimestamp"
    $backupPath = "$RemotePath\Backups\$backupName"
    
    Write-Step "Creando estructura de backup en: $backupPath"
    New-Item -ItemType Directory -Path "$backupPath\Backend" -Force | Out-Null
    New-Item -ItemType Directory -Path "$backupPath\Backend\Projects\$ProjectId" -Force | Out-Null
    
    # 📦 Backup SOLO de lo esencial (nueva estructura optimizada):
    # 1. Proyecto activo completo (config, models, data, deploy-version.json)
    $projectRemotePath = "$RemotePath\Backend\Projects\$ProjectId"
    if (Test-Path $projectRemotePath) {
        Write-Step "Backup del proyecto: $ProjectId"
        Copy-Item -Path $projectRemotePath -Destination "$backupPath\Backend\Projects\$ProjectId" -Recurse -Force
        Write-Info "  - Config, Models, Data"
        
        # Verificar que deploy-version.json existe (importante para trazabilidad)
        $deployVersionFile = "$projectRemotePath\deploy-version.json"
        if (Test-Path $deployVersionFile) {
            Write-Info "  - deploy-version.json ✓ (trazabilidad de versión)"
        } else {
            Write-Warning "  ⚠️ deploy-version.json no encontrado - se generará en este deploy"
        }
    }
    
    # 2. active-project.json
    $activeProjectFile = "$RemotePath\Backend\active-project.json"
    if (Test-Path $activeProjectFile) {
        Copy-Item -Path $activeProjectFile -Destination "$backupPath\Backend\" -Force
        Write-Info "  - active-project.json"
    }
    
    # 3. Certificado SSL
    $certFile = "$RemotePath\Backend\certificate.pfx"
    if (Test-Path $certFile) {
        Copy-Item -Path $certFile -Destination "$backupPath\Backend\" -Force
        Write-Info "  - certificate.pfx"
    }
    
    # 4. appsettings.Production.json (configuración local)
    $prodSettings = "$RemotePath\Backend\appsettings.Production.json"
    if (Test-Path $prodSettings) {
        Copy-Item -Path $prodSettings -Destination "$backupPath\Backend\" -Force
        Write-Info "  - appsettings.Production.json"
    }
    
    # 5. NO hacer backup de: wwwroot/ (frontend), exe/dlls (se regeneran)
    #    Estos se pueden restaurar desde LocalDeploys/ o regenerando
    
    # Crear archivo de info del backup
    $backupInfoContent = @"
# BACKUP - $ProjectId
# ==================
# Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# PC: $TargetIP
# Proyecto: $ProjectId
#
# Contenido:
# - Projects/$ProjectId/ (config, models, data, deploy-version.json)
# - active-project.json
# - certificate.pfx
# - appsettings.Production.json
#
# NO incluido (se regenera en deploy):
# - wwwroot/ (frontend)
# - *.exe, *.dll (backend compilado)
#
# Para restaurar:
# 1. Copiar Projects/$ProjectId/ a Backend/Projects/
# 2. Copiar active-project.json a Backend/
# 3. Re-desplegar Backend y Frontend con Deploy-Manual-Remote.ps1
"@
    Set-Content -Path "$backupPath\BACKUP_INFO.txt" -Value $backupInfoContent -Encoding UTF8
    
    # Contar archivos del backup
    $backupFileCount = (Get-ChildItem -Path $backupPath -Recurse -File).Count
    Write-Success "Backup creado: $backupFileCount archivos"
    Write-Info "Ruta: $backupPath"
    
    # Limpiar backups antiguos (mantener últimos 5)
    $backupsDir = "$RemotePath\Backups"
    if (Test-Path $backupsDir) {
        $oldBackups = Get-ChildItem -Path $backupsDir -Directory | 
            Where-Object { $_.Name -like "Backup_*" } |
            Sort-Object LastWriteTime -Descending | 
            Select-Object -Skip 5
        if ($oldBackups.Count -gt 0) {
            foreach ($old in $oldBackups) {
                Remove-Item -Path $old.FullName -Recurse -Force
            }
            Write-Info "Backups antiguos limpiados (mantenidos: 5)"
        }
    }
} else {
    Write-Info "Saltando backup (no existe instalacion previa o flag -BackupExisting no activado)"
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
# PASO 8: Copiar Frontend (wwwroot) - OPTIMIZADO
# ============================================
Write-Header "PASO 8: Copiando Frontend (wwwroot) - Optimizado"

$frontendBuildPath = "$FrontendPath\build"
if (-not (Test-Path $frontendBuildPath)) {
    Write-Error2 "No se encuentra el build del frontend: $frontendBuildPath"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Step "Copiando archivos del frontend (excluyendo innecesarios)..."

# 📦 Lista de archivos/carpetas a EXCLUIR en producción:
# - robots.txt: Solo para SEO/crawlers (app interna)
# - asset-manifest.json: Solo para debugging
# - manifest.json: Solo si quieres PWA (opcional)
# - docs/: Documentación ya está en desarrollo
# - models/: Modelos 3D vienen de Projects/{id}/models/ (NO legacy)
$excludeFiles = @('robots.txt', 'asset-manifest.json')
$excludeFolders = @('docs', 'models')

# Copiar archivos raíz (excepto los excluidos)
Get-ChildItem -Path $frontendBuildPath -File | Where-Object { $_.Name -notin $excludeFiles } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$RemotePath\Backend\wwwroot\$($_.Name)" -Force
}

# Copiar carpetas (excepto las excluidas)
Get-ChildItem -Path $frontendBuildPath -Directory | Where-Object { $_.Name -notin $excludeFolders } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$RemotePath\Backend\wwwroot\$($_.Name)" -Recurse -Force
}

# Contar archivos copiados
$copiedFiles = Get-ChildItem -Path "$RemotePath\Backend\wwwroot" -Recurse -File -ErrorAction SilentlyContinue
Write-Success "Frontend copiado: $($copiedFiles.Count) archivos"
Write-Info "Excluidos: $($excludeFiles -join ', '), carpetas: $($excludeFolders -join ', ')"

# ❌ PASO 8.1 ELIMINADO: Modo legacy no se usa en producción
# Los modelos 3D siempre vienen de Projects/{projectId}/models/
Write-Info "📦 Modelos 3D: se copiarán desde Projects/$ProjectId/models/ (paso 9.0)"

# ============================================================
# PASO 8.2: Copiar SBOM pre-generado (EU CRA Compliance)
# ============================================================
Write-Header "PASO 8.2: Copiando SBOM pre-generado (EU CRA)"

$sbomSourcePath = "$BackendPath\wwwroot\sbom"
$sbomDestPath = "$RemotePath\Backend\wwwroot\sbom"

if (Test-Path $sbomSourcePath) {
    Write-Step "Copiando SBOM pre-generado para producción..."
    
    # Crear carpeta sbom en destino
    if (-not (Test-Path $sbomDestPath)) {
        New-Item -ItemType Directory -Path $sbomDestPath -Force | Out-Null
    }
    
    # Copiar sbom-combined.json (el principal)
    $sbomFile = Join-Path $sbomSourcePath "sbom-combined.json"
    if (Test-Path $sbomFile) {
        Copy-Item -Path $sbomFile -Destination $sbomDestPath -Force
        Write-Success "SBOM copiado: sbom-combined.json"
        
        # Leer y mostrar resumen
        try {
            $sbomContent = Get-Content $sbomFile -Raw | ConvertFrom-Json
            $totalComponents = $sbomContent.components.Count
            $nugetCount = ($sbomContent.components | Where-Object { $_.purl -like "pkg:nuget*" }).Count
            $npmCount = ($sbomContent.components | Where-Object { $_.purl -like "pkg:npm*" }).Count
            Write-Info "  📦 Total componentes: $totalComponents (NuGet: $nugetCount, npm: $npmCount)"
        } catch {
            Write-Info "  SBOM copiado pero no se pudo leer el resumen"
        }
    } else {
        Write-Warning "⚠️ No se encontró sbom-combined.json - Ejecuta 'Generate SBOM' en desarrollo primero"
    }
    
    # Copiar history si existe
    $sbomHistory = Join-Path $sbomSourcePath "history"
    if (Test-Path $sbomHistory) {
        Copy-Item -Path $sbomHistory -Destination $sbomDestPath -Recurse -Force
        Write-Info "  📜 Historial SBOM copiado"
    }
} else {
    Write-Warning "⚠️ No existe carpeta SBOM en desarrollo: $sbomSourcePath"
    Write-Info "   Ejecuta 'Generate SBOM' en InfoPanel antes de desplegar"
}

# ============================================
# PASO 9: Verificar ProjectId (NO legacy en producción)
# ============================================
Write-Header "PASO 9: Verificando configuración de proyecto"

if ($ProjectId -eq "default") {
    Write-Error2 "❌ ERROR: Modo legacy (default) NO está permitido en producción"
    Write-Error2 "   Debes especificar un ProjectId válido (ej: -ProjectId 'cliente-abc')"
    Write-Info "   Los proyectos disponibles están en: $ProjectsPath"
    $availableProjects = Get-ChildItem -Path $ProjectsPath -Directory | Where-Object { $_.Name -ne '_template' }
    if ($availableProjects.Count -gt 0) {
        Write-Info "   Proyectos encontrados: $($availableProjects.Name -join ', ')"
    }
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Success "✅ Proyecto configurado: $ProjectId"

# ============================================================
# PASO 9.0: Copiar Proyecto (OBLIGATORIO - no hay modo legacy)
# ============================================================
Write-Header "PASO 9.0: Copiando Proyecto '$ProjectId'"

$projectSourcePath = Join-Path $ProjectsPath $ProjectId
$projectDestPath = "$RemotePath\Backend\Projects\$ProjectId"

# Verificar que el proyecto existe en origen
if (-not (Test-Path $projectSourcePath)) {
    Write-Error2 "❌ ERROR: No se encuentra el proyecto '$ProjectId' en: $projectSourcePath"
    $availableProjects = Get-ChildItem -Path $ProjectsPath -Directory | Where-Object { $_.Name -ne '_template' }
    if ($availableProjects.Count -gt 0) {
        Write-Info "   Proyectos disponibles: $($availableProjects.Name -join ', ')"
    }
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

# Copiar config (Excel)
$configSource = Join-Path $projectSourcePath "config"
if (Test-Path $configSource) {
    Write-Step "Copiando configuracion del proyecto (Excel)..."
    Copy-Item -Path "$configSource\*" -Destination "$projectDestPath\config" -Recurse -Force
    $configFiles = Get-ChildItem -Path $configSource -File
    Write-Success "Config copiado: $($configFiles.Count) archivos"
} else {
    Write-Warning "⚠️ No se encontró carpeta config en el proyecto"
}

# Copiar models (3D)
$modelsSource = Join-Path $projectSourcePath "models"
if (Test-Path $modelsSource) {
    Write-Step "Copiando modelos 3D del proyecto..."
    Copy-Item -Path "$modelsSource\*" -Destination "$projectDestPath\models" -Recurse -Force -ErrorAction SilentlyContinue
    $modelFiles = Get-ChildItem -Path $modelsSource -File -Recurse -ErrorAction SilentlyContinue
    Write-Success "Modelos 3D copiados: $($modelFiles.Count) archivos"
} else {
    Write-Warning "⚠️ No se encontró carpeta models en el proyecto"
}

# Copiar data (base de datos)
$dataSource = Join-Path $projectSourcePath "data"
$dbDestPath = "$projectDestPath\data\project.db"
    
    if (Test-Path "$dataSource\project.db") {
        # Verificar si existe DB en destino
        if (Test-Path $dbDestPath) {
            Write-Info "Base de datos del proyecto existente encontrada"
            # Backup de la DB existente
            $backupDir = "$projectDestPath\backups"
            if (-not (Test-Path $backupDir)) {
                New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
            }
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $backupPath = "$backupDir\project_backup_$timestamp.db"
            Copy-Item -Path $dbDestPath -Destination $backupPath -Force
            Write-Success "Backup de DB creado: project_backup_$timestamp.db"
            Write-Info "Base de datos PRESERVADA (usuarios y datos mantenidos)"
        } else {
            Write-Step "Copiando base de datos inicial del proyecto..."
            Copy-Item -Path "$dataSource\project.db" -Destination $dbDestPath -Force
            Write-Success "Base de datos inicial copiada"
        }
    } else {
        Write-Info "No hay DB en el proyecto local - se creara automaticamente"
    }
    
    Write-Success "Proyecto '$ProjectId' copiado completamente"
    
    # 🔒 SEGURIDAD: Verificar que no hay otros proyectos en producción
    Write-Step "Verificando que solo existe el proyecto activo..."
    $projectsDir = "$RemotePath\Backend\Projects"
    if (Test-Path $projectsDir) {
        $otherProjects = Get-ChildItem -Path $projectsDir -Directory | Where-Object { $_.Name -ne $ProjectId -and $_.Name -ne '_template' }
        if ($otherProjects.Count -gt 0) {
            Write-Info "⚠️ Se encontraron otros proyectos en producción:"
            foreach ($proj in $otherProjects) {
                Write-Info "   - $($proj.Name)"
            }
            $removeOthers = Read-Host "¿Eliminar proyectos que NO son '$ProjectId'? (S/N)"
            if ($removeOthers -eq 'S' -or $removeOthers -eq 's') {
                foreach ($proj in $otherProjects) {
                    Remove-Item -Path $proj.FullName -Recurse -Force
                    Write-Info "🧹 Eliminado proyecto: $($proj.Name)"
                }
                Write-Success "Solo queda el proyecto activo: $ProjectId"
            } else {
                Write-Info "Proyectos adicionales mantenidos (considera eliminarlos manualmente)"
            }
        } else {
            Write-Success "✅ Solo existe el proyecto activo: $ProjectId"
        }
    }

# Configurar active-project.json
Write-Step "Configurando active-project.json..."
$activeProjectContent = @"
{
  "activeProject": "$ProjectId",
  "description": "Proyecto configurado automaticamente por Deploy-Manual-Remote.ps1",
  "deployedAt": "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
  "deployedFrom": "$env:COMPUTERNAME"
}
"@

$activeProjectPath = "$RemotePath\Backend\active-project.json"
Set-Content -Path $activeProjectPath -Value $activeProjectContent -Encoding UTF8
Write-Success "active-project.json configurado con proyecto: $ProjectId"

# ============================================================
# PASO 9.0.1: Generar deploy-version.json en carpeta del proyecto
# ============================================================
Write-Step "Generando deploy-version.json para el proyecto..."

# Crear objeto combinado con info de Backend y Frontend
$deployVersionProject = @{
    ProjectId = $ProjectId
    DeployedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    DeployedFrom = $env:COMPUTERNAME
    DeployedBy = $env:USERNAME
    Backend = $backendVersionInfo
    Frontend = $frontendVersionInfo
}

$deployVersionJson = $deployVersionProject | ConvertTo-Json -Depth 10

# Guardar deploy-version.json en carpeta del proyecto (siempre multi-proyecto)
$projectVersionPath = "$RemotePath\Backend\Projects\$ProjectId\deploy-version.json"
Set-Content -Path $projectVersionPath -Value $deployVersionJson -Encoding UTF8
Write-Success "deploy-version.json guardado en proyecto '$ProjectId'"
Write-Info "  Backend: v$($backendVersionInfo.Version) ($($backendVersionInfo.CommitSha))"
Write-Info "  Frontend: v$($frontendVersionInfo.Version) ($($frontendVersionInfo.CommitSha))"

# ============================================================
# PASO 9.0.2: Guardar COPIA LOCAL del deploy (opcional)
# ============================================================
if ($SaveLocalCopy -and -not [string]::IsNullOrEmpty($LocalCopyPath)) {
    Write-Header "PASO 9.0.2: Guardando Copia Local del Deploy"
    
    # Crear estructura de carpetas local
    $localBackendPath = "$LocalCopyPath\Backend"
    $localProjectPath = "$LocalCopyPath\Backend\Projects\$ProjectId"
    
    Write-Step "Creando estructura local en: $LocalCopyPath"
    New-Item -ItemType Directory -Path "$localBackendPath\wwwroot" -Force | Out-Null
    New-Item -ItemType Directory -Path "$localProjectPath\config" -Force | Out-Null
    New-Item -ItemType Directory -Path "$localProjectPath\models" -Force | Out-Null
    New-Item -ItemType Directory -Path "$localProjectPath\data" -Force | Out-Null
    
    # Copiar Backend (desde publish)
    Write-Step "Copiando Backend..."
    Copy-Item -Path "$BackendPath\publish\*" -Destination $localBackendPath -Recurse -Force
    
    # Copiar Frontend (desde build a wwwroot, excluyendo innecesarios)
    Write-Step "Copiando Frontend (optimizado)..."
    $frontendBuildPath = "$FrontendPath\build"
    $excludeFiles = @('robots.txt', 'asset-manifest.json')
    $excludeFolders = @('docs', 'models')
    Get-ChildItem -Path $frontendBuildPath -File | Where-Object { $_.Name -notin $excludeFiles } | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination "$localBackendPath\wwwroot\$($_.Name)" -Force
    }
    Get-ChildItem -Path $frontendBuildPath -Directory | Where-Object { $_.Name -notin $excludeFolders } | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination "$localBackendPath\wwwroot\$($_.Name)" -Recurse -Force
    }
    
    # Copiar Proyecto (config, models)
    Write-Step "Copiando proyecto $ProjectId..."
    $projectSourcePath = Join-Path $ProjectsPath $ProjectId
    if (Test-Path "$projectSourcePath\config") {
        Copy-Item -Path "$projectSourcePath\config\*" -Destination "$localProjectPath\config" -Recurse -Force
    }
    if (Test-Path "$projectSourcePath\models") {
        Copy-Item -Path "$projectSourcePath\models\*" -Destination "$localProjectPath\models" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # Copiar active-project.json
    Set-Content -Path "$localBackendPath\active-project.json" -Value $activeProjectContent -Encoding UTF8
    
    # Copiar deploy-version.json
    Set-Content -Path "$localProjectPath\deploy-version.json" -Value $deployVersionJson -Encoding UTF8
    
    # Copiar certificado si existe
    $certPath = "$BackendPath\certificate.pfx"
    if (Test-Path $certPath) {
        Copy-Item -Path $certPath -Destination "$localBackendPath\certificate.pfx" -Force
    }
    
    # Copiar appsettings.Production.json
    $prodSettings = "$BackendPath\appsettings.Production.json"
    if (Test-Path $prodSettings) {
        Copy-Item -Path $prodSettings -Destination "$localBackendPath\appsettings.Production.json" -Force
    }
    
    # Crear archivo de info del deploy
    $deployInfoContent = @"
# DEPLOY LOCAL - $ProjectId
# ========================
# Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# Equipo origen: $env:COMPUTERNAME
# Usuario: $env:USERNAME
# Backend: v$($backendVersionInfo.Version) ($($backendVersionInfo.CommitSha))
# Frontend: v$($frontendVersionInfo.Version) ($($frontendVersionInfo.CommitSha))
#
# Esta copia local contiene todo lo necesario para restaurar
# el deploy en caso de problemas.
#
# Para restaurar manualmente:
# 1. Copiar contenido de Backend\ a C:\Aquafrisch Supervisor\Backend\
# 2. Ejecutar Start-Supervisor.bat
"@
    Set-Content -Path "$LocalCopyPath\DEPLOY_INFO.txt" -Value $deployInfoContent -Encoding UTF8
    
    # Contar archivos
    $localFileCount = (Get-ChildItem -Path $LocalCopyPath -Recurse -File).Count
    Write-Success "Copia local guardada: $localFileCount archivos"
    Write-Info "Ruta: $LocalCopyPath"
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

$batContent = @"
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
pause
"@

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
Write-Host "  PROYECTO DESPLEGADO:" -ForegroundColor Green
Write-Host "  =====================" -ForegroundColor Green
Write-Host "  Proyecto: $ProjectId" -ForegroundColor White
Write-Host "  Modo: MULTI-PROYECTO" -ForegroundColor Gray
Write-Host "  Config: $InstallPath\Backend\Projects\$ProjectId\config\" -ForegroundColor Gray
Write-Host "  Modelos: $InstallPath\Backend\Projects\$ProjectId\models\" -ForegroundColor Gray
Write-Host "  Database: $InstallPath\Backend\Projects\$ProjectId\data\project.db" -ForegroundColor Gray
Write-Host "  Version: $InstallPath\Backend\Projects\$ProjectId\deploy-version.json" -ForegroundColor Gray
Write-Host ""
Write-Host "  Archivos desplegados:" -ForegroundColor Cyan
Write-Host "  - Backend (exe + dlls)     -> $InstallPath\Backend\" -ForegroundColor Gray
Write-Host "  - Frontend (React)         -> $InstallPath\Backend\wwwroot\" -ForegroundColor Gray
Write-Host "  - Certificado SSL          -> $InstallPath\Backend\certificate.pfx" -ForegroundColor Gray
Write-Host "  - active-project.json      -> $InstallPath\Backend\ (proyecto: $ProjectId)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Base de datos:" -ForegroundColor Cyan
$dbPath = "$RemotePath\Backend\Projects\$ProjectId\data\project.db"
if (Test-Path $dbPath) {
    Write-Host "  - Estado: PRESERVADA (usuarios y sesiones mantenidos)" -ForegroundColor Green
} else {
    Write-Host "  - Estado: NUEVA (se creara al iniciar)" -ForegroundColor Yellow
}
Write-Host ""
if ($SaveLocalCopy -and -not [string]::IsNullOrEmpty($LocalCopyPath)) {
    Write-Host "  Copia local guardada:" -ForegroundColor Cyan
    Write-Host "  - Ruta: $LocalCopyPath" -ForegroundColor Green
    Write-Host "  - Info: $LocalCopyPath\DEPLOY_INFO.txt" -ForegroundColor Gray
    Write-Host ""
}
Write-Host "  Para iniciar el supervisor:" -ForegroundColor Cyan
Write-Host "  1. Conectar al PC: $TargetIP (RDP o presencial)" -ForegroundColor White
Write-Host "  2. Ejecutar: $InstallPath\Start-Supervisor.bat" -ForegroundColor White
Write-Host ""
Write-Host "  URLs de acceso:" -ForegroundColor Cyan
Write-Host "  - HTTP:  http://${TargetIP}:5000" -ForegroundColor White
Write-Host "  - HTTPS: https://${TargetIP}:5001 (SEGURO - RECOMENDADO)" -ForegroundColor Green
Write-Host ""
Write-Host "  NOTA: El servidor arrancara automaticamente con el proyecto:" -ForegroundColor Yellow
Write-Host "        $ProjectId" -ForegroundColor Yellow
Write-Host ""
Write-Host "  NOTA: El certificado SSL es autofirmado. El navegador mostrara" -ForegroundColor Yellow
Write-Host "        una advertencia la primera vez. Esto es normal en redes internas." -ForegroundColor Yellow
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan

# Mantener ventana abierta
Write-Host ""
Read-Host "Presiona Enter para cerrar"
