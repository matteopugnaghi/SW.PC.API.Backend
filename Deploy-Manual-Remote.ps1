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
    .\Deploy-Manual-Remote.ps1 -ProjectId "A70.AMITWP" -CodeOnly  # Solo actualizar codigo
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
    [switch]$SaveLocalCopy,  # Guardar copia local antes de enviar a remoto
    [switch]$CodeOnly  # Solo actualizar Backend+Frontend, NO tocar Projects/
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
if ($CodeOnly) {
    Write-Host "  MODO: SOLO CODIGO (Backend + Frontend)" -ForegroundColor Green
    Write-Host "  Los proyectos NO se tocan" -ForegroundColor Yellow
    Write-Host ""
}

# Preguntar configuracion de conexion
Write-Host "  Configuracion de conexion:" -ForegroundColor Cyan
Write-Host "    IP destino:   $TargetIP" -ForegroundColor White
Write-Host "    Usuario:      $TargetUser" -ForegroundColor White
Write-Host "    Password:     $('*' * $TargetPassword.Length)" -ForegroundColor White
Write-Host "    Ruta destino: $InstallPath" -ForegroundColor White
Write-Host ""
$changeConfig = Read-Host "¿Modificar configuracion de conexion? (s/N)"
if ($changeConfig -eq 's' -or $changeConfig -eq 'S') {
    $newIP = Read-Host "  IP destino [$TargetIP]"
    if (-not [string]::IsNullOrEmpty($newIP)) { $TargetIP = $newIP }
    
    $newUser = Read-Host "  Usuario [$TargetUser]"
    if (-not [string]::IsNullOrEmpty($newUser)) { $TargetUser = $newUser }
    
    $newPassword = Read-Host "  Password [$('*' * 8)]"
    if (-not [string]::IsNullOrEmpty($newPassword)) { $TargetPassword = $newPassword }
    
    $newPath = Read-Host "  Ruta instalacion [$InstallPath]"
    if (-not [string]::IsNullOrEmpty($newPath)) { $InstallPath = $newPath }
    
    Write-Host ""
    Write-Success "Configuracion actualizada:"
    Write-Info "  IP: $TargetIP | Usuario: $TargetUser | Ruta: $InstallPath"
}

Write-Host ""
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

# Preguntar tipo de deploy si no se especifico -CodeOnly
if (-not $CodeOnly) {
    Write-Host "  Tipo de despliegue:" -ForegroundColor Cyan
    Write-Host "    [1] COMPLETO - Backend + Frontend + Proyecto + TwinCAT" -ForegroundColor White
    Write-Host "    [2] SOLO CODIGO - Backend + Frontend (no toca Projects/)" -ForegroundColor White
    Write-Host ""
    $deployType = Read-Host "Selecciona tipo de despliegue (1/2) [1]"
    if ($deployType -eq '2') {
        $CodeOnly = $true
        Write-Info "Modo SOLO CODIGO activado - Los proyectos NO se tocan"
    } else {
        Write-Info "Modo COMPLETO - Se desplegara todo incluyendo el proyecto"
    }
    Write-Host ""
}

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
        
        # 🧹 Limpiar carpetas de desarrollo que no deben ir a producción
        $devFoldersToRemove = @(
            "$BackendPath\publish\wwwroot\audit",
            "$BackendPath\publish\wwwroot\models",
            "$BackendPath\publish\wwwroot\sbom"
        )
        foreach ($devFolder in $devFoldersToRemove) {
            if (Test-Path $devFolder) {
                Remove-Item -Path $devFolder -Recurse -Force -ErrorAction SilentlyContinue
                Write-Info "🧹 Eliminado de publish: $($devFolder | Split-Path -Leaf)"
            }
        }
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
        
        # Determinar si está firmado (G=good, B=bad signature, U=untrusted, X/Y=expired)
        $isSigned = $signatureCode -in @("G", "B", "U", "X", "Y", "R")
        
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
            IsSigned = $isSigned
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
# PASO 2.2: Generar CHANGELOG.md (Release Notes automáticas)
# ============================================
Write-Header "PASO 2.2: Generando CHANGELOG.md (Release Notes)"

function Generate-Changelog {
    param([string]$RepoPath, [string]$ComponentName, [int]$MaxReleases = 20)
    
    if (-not (Test-Path (Join-Path $RepoPath ".git"))) {
        Write-Warning "${ComponentName}: No es un repositorio Git, saltando CHANGELOG"
        return $null
    }
    
    Push-Location $RepoPath
    try {
        # Get all tags sorted by version descending
        $tags = git tag -l --sort=-version:refname 2>$null
        if (-not $tags) {
            Write-Warning "${ComponentName}: Sin tags, generando changelog desde todos los commits"
            $allCommits = git log --format="%H|%s|%ai|%an" --reverse 2>$null
            if (-not $allCommits) { return $null }
            
            $md = "# CHANGELOG - $ComponentName`n`n"
            $md += "> Auto-generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n`n"
            $md += "## Sin publicar`n`n"
            foreach ($line in $allCommits) {
                $parts = $line -split '\|', 4
                if ($parts.Count -ge 4) {
                    $shortHash = $parts[0].Substring(0, 7)
                    $md += "- ``$shortHash`` $($parts[1]) - *$($parts[3])*`n"
                }
            }
            return $md
        }
        
        $tagList = @($tags | Select-Object -First $MaxReleases)
        $md = "# CHANGELOG - $ComponentName`n`n"
        $md += "> Auto-generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n`n"
        
        # Unreleased changes (latest tag → HEAD)
        $latestTag = $tagList[0]
        $unreleased = git log "$latestTag..HEAD" --format="%H|%s|%ai|%an" --reverse 2>$null
        if ($unreleased) {
            $md += "## Sin publicar`n`n"
            $md += "**Desde**: $latestTag`n"
            $md += "**Commits**: $(($unreleased | Measure-Object).Count)`n`n"
            foreach ($line in $unreleased) {
                $parts = $line -split '\|', 4
                if ($parts.Count -ge 4) {
                    $shortHash = $parts[0].Substring(0, 7)
                    $md += "- ``$shortHash`` $($parts[1]) - *$($parts[3])*`n"
                }
            }
            $md += "`n---`n`n"
        }
        
        # Each tag pair
        for ($i = 0; $i -lt $tagList.Count; $i++) {
            $toTag = $tagList[$i]
            $fromTag = if ($i + 1 -lt $tagList.Count) { $tagList[$i + 1] } else { $null }
            
            $tagDate = git log -1 --format="%ai" $toTag 2>$null
            $tagMsg = git tag -l --format="%(subject)" $toTag 2>$null
            
            if ($fromTag) {
                $commits = git log "$fromTag..$toTag" --format="%H|%s|%ai|%an" --reverse 2>$null
            } else {
                $commits = git log $toTag --format="%H|%s|%ai|%an" --reverse 2>$null
            }
            
            $commitCount = if ($commits) { ($commits | Measure-Object).Count } else { 0 }
            
            $md += "## $toTag`n`n"
            $md += "**Fecha**: $($tagDate.Substring(0, 16))`n"
            if ($fromTag) { $md += "**Desde**: $fromTag`n" }
            $md += "**Commits**: $commitCount`n"
            if ($tagMsg) { $md += "**Nota**: $tagMsg`n" }
            $md += "`n"
            
            if ($commits) {
                foreach ($line in $commits) {
                    $parts = $line -split '\|', 4
                    if ($parts.Count -ge 4) {
                        $shortHash = $parts[0].Substring(0, 7)
                        $md += "- ``$shortHash`` $($parts[1]) - *$($parts[3])*`n"
                    }
                }
            } else {
                $md += "*Sin cambios registrados.*`n"
            }
            $md += "`n---`n`n"
        }
        
        return $md
    } finally {
        Pop-Location
    }
}

# Backend CHANGELOG
$backendChangelog = Generate-Changelog -RepoPath $BackendPath -ComponentName "Backend"
if ($backendChangelog) {
    $backendChangelogPath = Join-Path $BackendPath "CHANGELOG.md"
    Set-Content -Path $backendChangelogPath -Value $backendChangelog -Encoding UTF8
    Write-Success "Backend CHANGELOG.md generado"
    
    # Also copy to publish folder
    $publishChangelogPath = Join-Path "$BackendPath\publish" "CHANGELOG.md"
    Copy-Item $backendChangelogPath $publishChangelogPath -Force -ErrorAction SilentlyContinue
}

# Frontend CHANGELOG
$frontendChangelog = Generate-Changelog -RepoPath $FrontendPath -ComponentName "Frontend"
if ($frontendChangelog) {
    $frontendChangelogPath = Join-Path $FrontendPath "CHANGELOG.md"
    Set-Content -Path $frontendChangelogPath -Value $frontendChangelog -Encoding UTF8
    Write-Success "Frontend CHANGELOG.md generado"
}

# Combined project CHANGELOG (Backend + Frontend + TwinCAT → Projects/{ProjectId}/)
$projectFolder = Join-Path $ProjectsPath $ProjectId
if ($ProjectId -ne "default" -and (Test-Path $projectFolder)) {
    $combinedMd = "# CHANGELOG - Proyecto Unificado`n`n"
    $combinedMd += "> Auto-generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
    $combinedMd += "> Backend + Frontend + TwinCAT`n`n"
    
    if ($backendChangelog) {
        $combinedMd += "# Backend`n`n"
        # Remove the per-repo header lines (first 3 lines) and add the content
        $backendLines = ($backendChangelog -split "`n") | Select-Object -Skip 3
        $combinedMd += ($backendLines -join "`n") + "`n`n"
    } else {
        $combinedMd += "# Backend`n`n*Sin changelog disponible.*`n`n---`n`n"
    }
    
    if ($frontendChangelog) {
        $combinedMd += "# Frontend`n`n"
        $frontendLines = ($frontendChangelog -split "`n") | Select-Object -Skip 3
        $combinedMd += ($frontendLines -join "`n") + "`n`n"
    } else {
        $combinedMd += "# Frontend`n`n*Sin changelog disponible.*`n`n---`n`n"
    }
    
    # TwinCAT (if repo exists - try dev path first, then deployed TwinCAT/ folder)
    $twinCATRoot = Split-Path -Parent $BackendPath
    $twinCATPath = Join-Path $twinCATRoot "SW.PC.TWINCAT.PLC"
    $twinCATChangelog = $null
    if (Test-Path (Join-Path $twinCATPath ".git")) {
        $twinCATChangelog = Generate-Changelog -RepoPath $twinCATPath -ComponentName "TwinCAT"
    } else {
        # Try TwinCAT/ folder (deployed structure) - scan for first subfolder with .git
        $twinCATFolder = Join-Path $twinCATRoot "SW.PC.Twincat_3"
        if (Test-Path $twinCATFolder) {
            $twinCATRepo = Get-ChildItem -Path $twinCATFolder -Directory -ErrorAction SilentlyContinue |
                Where-Object { Test-Path (Join-Path $_.FullName ".git") } | Select-Object -First 1
            if ($twinCATRepo) {
                $twinCATChangelog = Generate-Changelog -RepoPath $twinCATRepo.FullName -ComponentName "TwinCAT"
            }
        }
    }
    if ($twinCATChangelog) {
        $combinedMd += "# TwinCAT`n`n"
        $twinCATLines = ($twinCATChangelog -split "`n") | Select-Object -Skip 3
        $combinedMd += ($twinCATLines -join "`n") + "`n`n"
    } else {
        $combinedMd += "# TwinCAT`n`n*Repositorio no disponible.*`n`n---`n`n"
    }
    
    $combinedPath = Join-Path $projectFolder "CHANGELOG.md"
    Set-Content -Path $combinedPath -Value $combinedMd -Encoding UTF8
    Write-Success "CHANGELOG.md unificado generado en: $combinedPath"
    
    # Also copy to publish/Projects/{ProjectId}/
    $publishProjectPath = Join-Path "$BackendPath\publish\Projects\$ProjectId" ""
    if (Test-Path $publishProjectPath) {
        Copy-Item $combinedPath (Join-Path $publishProjectPath "CHANGELOG.md") -Force -ErrorAction SilentlyContinue
    }
}

Write-Success "CHANGELOG.md generados"

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
        # Verificar que npm está disponible
        $npmPath = Get-Command npm -ErrorAction SilentlyContinue
        if (-not $npmPath) {
            Write-Error2 "npm no encontrado en PATH"
            Write-Info "Asegúrate de que Node.js está instalado y npm está en el PATH"
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        Write-Info "npm encontrado: $($npmPath.Source)"
        
        # Temporalmente cambiar ErrorActionPreference para npm (genera muchos warnings)
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        
        # Ejecutar npm build - capturar salida sin que termine el script por warnings
        Write-Info "Ejecutando build (esto puede tardar unos minutos)..."
        $npmExitCode = 0
        try {
            # Usar Start-Process para mejor control del proceso
            $npmProcess = Start-Process -FilePath "npm.cmd" -ArgumentList "run", "build" `
                -WorkingDirectory $FrontendPath `
                -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput "$env:TEMP\npm_stdout.txt" `
                -RedirectStandardError "$env:TEMP\npm_stderr.txt"
            $npmExitCode = $npmProcess.ExitCode
            
            # Mostrar salida
            if (Test-Path "$env:TEMP\npm_stdout.txt") {
                $stdout = Get-Content "$env:TEMP\npm_stdout.txt" -Raw -ErrorAction SilentlyContinue
                if ($stdout) { Write-Host $stdout -ForegroundColor Gray }
            }
            if (Test-Path "$env:TEMP\npm_stderr.txt") {
                $stderr = Get-Content "$env:TEMP\npm_stderr.txt" -Raw -ErrorAction SilentlyContinue
                if ($stderr) { 
                    # npm genera warnings en stderr, no necesariamente son errores
                    Write-Host $stderr -ForegroundColor DarkYellow 
                }
            }
        } catch {
            Write-Error2 "Excepcion durante npm build: $_"
            $npmExitCode = 1
        }
        
        # Restaurar ErrorActionPreference
        $ErrorActionPreference = $previousErrorAction
        
        # Verificar resultado
        if ($npmExitCode -ne 0) {
            Write-Error2 "Error compilando frontend (exit code: $npmExitCode)"
            Write-Info "Revisa los errores arriba"
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        
        # Verificar que la carpeta build existe
        $frontendBuildPath = Join-Path $FrontendPath "build"
        if (-not (Test-Path $frontendBuildPath)) {
            Write-Error2 "La carpeta build no existe después del npm build"
            Write-Info "Ruta esperada: $frontendBuildPath"
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        
        Write-Success "Frontend compilado en: $frontendBuildPath"
        
        # Generar deploy-version.json para Frontend (despues del build)
        Write-Step "Generando deploy-version.json para Frontend..."
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
    } catch {
        Write-Error2 "Error inesperado en PASO 3: $_"
        Read-Host "Presiona Enter para cerrar"
        exit 1
    } finally {
        Pop-Location
        # Limpiar archivos temporales
        Remove-Item "$env:TEMP\npm_stdout.txt" -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\npm_stderr.txt" -ErrorAction SilentlyContinue
    }
}

# ============================================
# PASO 4: Conectar al PC remoto
# ============================================
Write-Header "PASO 4: Conectando al PC remoto ($TargetIP)"

$RemotePath = "\\$TargetIP\C`$\$($InstallPath.TrimStart('C:\'))"
$SecurePassword = ConvertTo-SecureString $TargetPassword -AsPlainText -Force
$Credential = New-Object System.Management.Automation.PSCredential ($TargetUser, $SecurePassword)

Write-Step "Estableciendo conexion de red..."

# Desconectar TODAS las conexiones previas al servidor (evita error 1219)
Write-Info "Desconectando conexiones previas a $TargetIP..."
$existingConns = net use 2>&1 | Select-String -Pattern ([regex]::Escape("\\$TargetIP\"))
foreach ($conn in $existingConns) {
    $parts = $conn.ToString().Trim() -split '\s+'
    foreach ($part in $parts) {
        if ($part -match "^\\\\$([regex]::Escape($TargetIP))\\") {
            net use $part /delete /y 2>$null | Out-Null
            Write-Info "  Desconectado: $part"
        }
        if ($part -match '^[A-Z]:$') {
            net use $part /delete /y 2>$null | Out-Null
            Write-Info "  Desconectada unidad: $part"
        }
    }
}
# Tambien intentar las habituales por si acaso
try { net use "\\$TargetIP\C`$" /delete /y 2>&1 | Out-Null } catch { }
Start-Sleep -Seconds 2

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
# PASO 4.5: Parar servicio/proceso existente
# ============================================
Write-Header "PASO 4.5: Parando servidor remoto"

$serviceName = "AquafrischSupervisor"

# Metodo 1: Parar servicio Windows via sc.exe remoto (usa conexion SMB)
Write-Step "Parando servicio '$serviceName' via sc.exe remoto..."
$scQuery = sc.exe \\$TargetIP query $serviceName 2>&1
if ($scQuery -match "RUNNING") {
    sc.exe \\$TargetIP stop $serviceName | Out-Null
    Write-Success "Servicio '$serviceName' detenido via sc.exe"
    Start-Sleep -Seconds 2
    # Siempre forzar taskkill despues de sc.exe stop para asegurar que el proceso muere
    $prevEAP = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
    $ErrorActionPreference = $prevEAP
    if ($taskkillResult -match "correctamente|SUCCESS") {
        Write-Success "Proceso forzado a cerrar con taskkill (belt & suspenders)"
    } else {
        Write-Info "taskkill: proceso ya no existia (limpio)"
    }
    Write-Info "Esperando 5 segundos para que se liberen los archivos..."
    Start-Sleep -Seconds 5
} elseif ($scQuery -match "STOPPED|STOP_PENDING") {
    Write-Info "Servicio ya estaba parado"
    # Aun asi taskkill por si quedo un proceso zombie
    $prevEAP = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
    $ErrorActionPreference = $prevEAP
    if ($taskkillResult -match "correctamente|SUCCESS") {
        Write-Success "Proceso zombie eliminado con taskkill"
        Start-Sleep -Seconds 3
    }
} else {
    Write-Info "Servicio no instalado todavia (primera instalacion)"
    # Fallback: taskkill en caso de que este corriendo como consola (modo legacy)
    $prevEAP = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
    $ErrorActionPreference = $prevEAP
    if ($taskkillResult -match "correctamente|SUCCESS") {
        Write-Success "Proceso legacy parado con taskkill"
        Start-Sleep -Seconds 3
    } elseif ($taskkillResult -match "no se encontr|not found") {
        Write-Info "Ningun proceso corriendo (limpio)"
    }
}

# Verificar que los archivos estan liberados
Write-Step "Verificando que los DLLs estan liberados..."
# Testear hostfxr.dll (DLL nativa que tarda mas en liberarse que la managed DLL)
$testFile = "$RemotePath\Backend\hostfxr.dll"
if (-not (Test-Path $testFile)) {
    $testFile = "$RemotePath\Backend\SW.PC.API.Backend.dll"
}
$retryCount = 0
$maxRetries = 8
$killedProcess = $false

while ($retryCount -lt $maxRetries) {
    if (Test-Path $testFile) {
        try {
            $stream = [System.IO.File]::Open($testFile, 'Open', 'Read', 'None')
            $stream.Close()
            Write-Success "Archivos liberados, continuando..."
            break
        } catch {
            $retryCount++
            # Despues de 2 intentos fallidos, forzar taskkill remoto
            if ($retryCount -eq 2 -and -not $killedProcess) {
                Write-Info "Servicio no libero archivos, forzando taskkill remoto..."
                $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
                if ($taskkillResult -match "correctamente|SUCCESS") {
                    Write-Success "Proceso forzado a cerrar con taskkill"
                } else {
                    Write-Info "taskkill: $taskkillResult"
                }
                $killedProcess = $true
                Start-Sleep -Seconds 3
            } elseif ($retryCount -lt $maxRetries) {
                Write-Info "Archivos aun bloqueados, esperando... (intento $retryCount/$maxRetries)"
                Start-Sleep -Seconds 2
            } else {
                Write-Error2 "Los archivos siguen bloqueados despues de $maxRetries intentos"
                Write-Host ""
                Write-Host "  SOLUCION: Debes cerrar el servidor manualmente en $TargetIP" -ForegroundColor Yellow
                Write-Host "  1. Abre services.msc o Administrador de Tareas en el servidor" -ForegroundColor White
                Write-Host "  2. Busca el servicio '$serviceName' o SW.PC.API.Backend" -ForegroundColor White
                Write-Host "  3. Detenlo manualmente" -ForegroundColor White
                Write-Host "  4. Vuelve a ejecutar este script" -ForegroundColor White
                Write-Host ""
                Read-Host "Presiona Enter para cerrar"
                exit 1
            }
        }
    } else {
        Write-Info "Primera instalacion (no hay archivos previos)"
        break
    }
}

# ============================================
# PASO 5: Crear estructura de carpetas
# ============================================
Write-Header "PASO 5: Creando estructura de carpetas"

# 📦 Estructura optimizada para producción:
# - NO crear ExcelConfigs/ externo (legacy eliminado)
# - NO crear Backend/Data/ (legacy - Aquafrisch.db ya no se usa)
# - NO crear Backend/Projects/ genérico (solo el proyecto activo)
$folders = @(
    $RemotePath,
    "$RemotePath\Backend",
    "$RemotePath\Backend\wwwroot",
    "$RemotePath\SW.PC.Twincat_3"             # Carpeta para repos TwinCAT PLC (tecnicos clonan aqui)
)

# Añadir carpeta del proyecto activo (SOLO el proyecto que se despliega)
if ($ProjectId -ne "default") {
    $folders += "$RemotePath\Backend\Projects"
    $folders += "$RemotePath\Backend\Projects\$ProjectId"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\config"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\models"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\data"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\backups"
    $folders += "$RemotePath\Backend\Projects\$ProjectId\sbom"   # SBOM por proyecto (EU CRA)
    $folders += "$RemotePath\Backend\Projects\$ProjectId\audit"  # Audit logs por proyecto (EU CRA)
    $folders += "$RemotePath\Backend\Projects\$ProjectId\logs"   # NxLog JSONL logs (SOC PIVOT TISSEO)
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

# Permisos de TwinCAT: el servicio corre como LocalSystem, tecnicos clonan como Administrator
$twinCatRemotePath = "$RemotePath\SW.PC.Twincat_3"
if (Test-Path $twinCatRemotePath) {
    # Usar SID *S-1-1-0 (Everyone/Todos) - funciona en cualquier idioma de Windows
    $prevEAP = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $icaclsResult = icacls.exe $twinCatRemotePath /grant *S-1-1-0:"(OI)(CI)F" /T /Q 2>&1
    $ErrorActionPreference = $prevEAP
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Permisos TwinCAT configurados (Everyone/Todos: Full Control)"
    } else {
        Write-Info "No se pudieron configurar permisos TwinCAT (configurar manualmente)"
    }
}

# ============================================
# PASO 5.1: Limpiar archivos/carpetas innecesarios (si existen)
# ============================================
Write-Header "PASO 5.1: Limpieza de archivos innecesarios"

# 🧹 Lista de archivos/carpetas a ELIMINAR si existen de deploys anteriores
$cleanupItems = @(
    "$RemotePath\ExcelConfigs",                           # Legacy folder (ya no se usa)
    "$RemotePath\ExcelConfig",                            # Legacy folder sin 's' (ya no se usa)
    "$RemotePath\Backend\ExcelConfigs",                   # Legacy folder dentro de Backend
    "$RemotePath\Backend\ExcelConfig",                    # Legacy folder sin 's' dentro de Backend
    "$RemotePath\Backend\Data",                           # Legacy folder (Aquafrisch.db ya no se usa)
    "$RemotePath\Backend\backups",                        # Legacy backups folder (ahora en Projects/{id}/backups)
    "$RemotePath\Backend\n",                              # Carpeta errónea
    "$RemotePath\Backend\wwwroot\robots.txt",             # SEO file (no necesario)
    "$RemotePath\Backend\wwwroot\asset-manifest.json",    # Debug file
    "$RemotePath\Backend\wwwroot\docs",                   # Documentación (ya en desarrollo)
    "$RemotePath\Backend\wwwroot\audit",                  # Logs de auditoría legacy (ahora en Projects/{id}/audit)
    "$RemotePath\Backend\wwwroot\models",                 # Modelos legacy (ahora en Projects/{id}/models)
    "$RemotePath\Backend\wwwroot\sbom",                   # SBOM legacy (ahora en Projects/{id}/sbom)
    "$RemotePath\Backend\wwwroot\locales",                # Archivos de traducción (se copian de build)
    "$RemotePath\Backend\Projects\_template",             # Template de proyecto (solo para desarrollo)
    "$RemotePath\Installers",                             # Runtime installer (deploy es self-contained)
    "$RemotePath\Backend\Installers",                     # Runtime installer dentro de Backend
    "$RemotePath\Start-Supervisor.bat"                    # Script arranque manual legacy (servicio Windows)
)

Write-Info "Verificando $($cleanupItems.Count) elementos para limpiar..."

$cleanedCount = 0
foreach ($item in $cleanupItems) {
    if (Test-Path $item) {
        try {
            Remove-Item -Path $item -Recurse -Force -ErrorAction Stop
            Write-Info "🧹 Eliminado: $item"
            $cleanedCount++
        } catch {
            Write-Warning "⚠️ No se pudo eliminar: $item - $($_.Exception.Message)"
        }
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

# 🧹 Limpiar carpetas residuales que NO deberían existir en producción
Write-Step "Limpiando carpetas residuales en destino..."
$residualFolders = @(
    "$RemotePath\Backend\publish",              # Carpeta publish mal copiada
    "$RemotePath\Backend\backups",              # Creada por BackupScheduler si estaba mal configurado
    "$RemotePath\Backend\ExcelConfigs",         # Solo para desarrollo
    "$RemotePath\Backend\Projects\_template",   # Solo para desarrollo
    "$RemotePath\Backend\wwwroot\audit",        # Legacy - ahora en Projects/{id}/audit
    "$RemotePath\Backend\wwwroot\sbom",         # Legacy - ahora en Projects/{id}/sbom
    "$RemotePath\Backend\wwwroot\logs"          # Legacy - logs van en Projects/{id}/ o se crean dinámicamente
)
foreach ($folder in $residualFolders) {
    if (Test-Path $folder) {
        Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue
        Write-Info "🧹 Eliminada carpeta residual: $($folder | Split-Path -Leaf)"
    }
}

Write-Step "Copiando archivos del backend..."
$backendFiles = Get-ChildItem -Path $publishPath -Recurse
$totalFiles = $backendFiles.Count

$copySuccess = $false
for ($copyAttempt = 1; $copyAttempt -le 3; $copyAttempt++) {
    try {
        Copy-Item -Path "$publishPath\*" -Destination "$RemotePath\Backend" -Recurse -Force -ErrorAction Stop
        $copySuccess = $true
        break
    } catch {
        if ($copyAttempt -lt 3) {
            Write-Info "Error copiando (intento $copyAttempt/3), reintentando en 3s..."
            Start-Sleep -Seconds 3
        } else {
            Write-Error2 "No se pudo copiar el backend despues de 3 intentos: $_"
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
    }
}
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

# 🔧 FIX: Limpiar carpeta static ANTES de copiar el frontend
# El backend publish puede tener archivos JS/CSS viejos que no coinciden con el nuevo index.html
Write-Step "Limpiando carpeta static existente (evitar conflicto de hashes)..."
$staticPath = "$RemotePath\Backend\wwwroot\static"
if (Test-Path $staticPath) {
    Remove-Item -Path $staticPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Info "🧹 Carpeta static eliminada - se reemplazará con el frontend nuevo"
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
Write-Info "📦 SBOM: se copiará a Projects/$ProjectId/sbom/ (paso 9.0)"

# ✅ PASO 8.2 ELIMINADO: SBOM ahora va a Projects/{projectId}/sbom/
# El SBOM es por proyecto (cada instalación puede tener diferentes versiones)

# ============================================
# PASO 9: Verificar ProjectId (NO legacy en produccion)
# ============================================
Write-Header "PASO 9: Verificando configuracion de proyecto"

if ($ProjectId -eq "default") {
    Write-Error2 "ERROR: Modo legacy (default) NO esta permitido en produccion"
    Write-Error2 "   Debes especificar un ProjectId valido (ej: -ProjectId 'cliente-abc')"
    Write-Info "   Los proyectos disponibles estan en: $ProjectsPath"
    $availableProjects = Get-ChildItem -Path $ProjectsPath -Directory | Where-Object { $_.Name -ne '_template' }
    if ($availableProjects.Count -gt 0) {
        Write-Info "   Proyectos encontrados: $($availableProjects.Name -join ', ')"
    }
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

Write-Success "Proyecto configurado: $ProjectId"

if ($CodeOnly) {
    # ============================================================
    # MODO -CodeOnly: Solo actualizar Backend + Frontend, NO tocar Projects/
    # ============================================================
    Write-Header "PASO 9.0: MODO SOLO CODIGO (-CodeOnly)"
    Write-Info "Backend y Frontend actualizados"
    Write-Info "Proyectos NO modificados (config, modelos, DB se mantienen)"
    
    # Verificar que el proyecto existe en el servidor
    $projectDestPath = "$RemotePath\Backend\Projects\$ProjectId"
    if (Test-Path $projectDestPath) {
        Write-Success "Proyecto '$ProjectId' existe en el servidor (NO modificado)"
    } else {
        Write-Warning "Proyecto '$ProjectId' NO existe en el servidor"
        Write-Info "Para primera instalacion, ejecuta SIN -CodeOnly"
    }
    
    # Configurar active-project.json (por si cambio el proyecto seleccionado)
    Write-Step "Configurando active-project.json..."
    $activeProjectContent = @"
{
  "activeProject": "$ProjectId",
  "description": "Proyecto configurado automaticamente por Deploy-Manual-Remote.ps1 (-CodeOnly)",
  "deployedAt": "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
  "deployedFrom": "$env:COMPUTERNAME"
}
"@
    $activeProjectPath = "$RemotePath\Backend\active-project.json"
    Set-Content -Path $activeProjectPath -Value $activeProjectContent -Encoding UTF8
    Write-Success "active-project.json configurado con proyecto: $ProjectId"

} else {

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

# Copiar sbom (EU CRA Compliance) - SBOM por proyecto
$sbomSource = Join-Path $projectSourcePath "sbom"
$sbomSourceAlt = "$BackendPath\wwwroot\sbom"  # Fallback: carpeta desarrollo
if (Test-Path $sbomSource) {
    Write-Step "Copiando SBOM del proyecto (EU CRA)..."
    Copy-Item -Path "$sbomSource\*" -Destination "$projectDestPath\sbom" -Recurse -Force -ErrorAction SilentlyContinue
    $sbomFiles = Get-ChildItem -Path $sbomSource -File -Recurse -ErrorAction SilentlyContinue
    Write-Success "SBOM copiado: $($sbomFiles.Count) archivos"
} elseif (Test-Path $sbomSourceAlt) {
    # Fallback: copiar desde wwwroot/sbom si no existe en el proyecto
    Write-Step "Copiando SBOM desde desarrollo (fallback)..."
    Copy-Item -Path "$sbomSourceAlt\*" -Destination "$projectDestPath\sbom" -Recurse -Force -ErrorAction SilentlyContinue
    $sbomFiles = Get-ChildItem -Path $sbomSourceAlt -File -Recurse -ErrorAction SilentlyContinue
    Write-Success "SBOM copiado (desde desarrollo): $($sbomFiles.Count) archivos"
} else {
    Write-Warning "⚠️ No se encontró SBOM - Genera el SBOM desde InfoPanel"
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

# ============================================================
# PASO 9.0a: Copiar SW.PC.Twincat_3 (repositorio TwinCAT PLC)
# ============================================================
Write-Header "PASO 9.0a: Copiando SW.PC.Twincat_3 (TwinCAT PLC)"

# Detectar carpeta TwinCAT local (misma logica que changelog)
$twinCATRoot = Split-Path -Parent $BackendPath
$twinCATLocalPath = $null

# Intentar ruta dev primero: ../SW.PC.TWINCAT.PLC
$twinCATDevPath = Join-Path $twinCATRoot "SW.PC.TWINCAT.PLC"
if (Test-Path $twinCATDevPath) {
    $twinCATLocalPath = $twinCATDevPath
    $twinCATLocalName = "SW.PC.TWINCAT.PLC"
    Write-Info "TwinCAT encontrado (dev): $twinCATDevPath"
} else {
    # Intentar ruta deployed: ../SW.PC.Twincat_3/{subproyecto}
    $twinCATFolder = Join-Path $twinCATRoot "SW.PC.Twincat_3"
    if (Test-Path $twinCATFolder) {
        $twinCATRepo = Get-ChildItem -Path $twinCATFolder -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.FullName ".git") } | Select-Object -First 1
        if ($twinCATRepo) {
            $twinCATLocalPath = $twinCATRepo.FullName
            $twinCATLocalName = $twinCATRepo.Name
            Write-Info "TwinCAT encontrado (deployed): $($twinCATRepo.FullName)"
        }
    }
}

if ($twinCATLocalPath) {
    $twinCatRemoteDestPath = "$RemotePath\SW.PC.Twincat_3\$twinCATLocalName"
    
    Write-Step "Copiando TwinCAT a: $twinCatRemoteDestPath"
    
    # Limpiar carpeta destino (excepto .git/, .sln, .plcproj) para eliminar residuos de deploys anteriores
    if (Test-Path $twinCatRemoteDestPath) {
        Write-Info "Limpiando archivos antiguos en destino (preservando .git, .sln, .plcproj)..."
        Get-ChildItem -Path $twinCatRemoteDestPath -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notmatch '[\\/]\.git[\\/]' -and
                $_.Extension -notin '.sln', '.plcproj'
            } | Remove-Item -Force -ErrorAction SilentlyContinue
    } else {
        New-Item -ItemType Directory -Path $twinCatRemoteDestPath -Force | Out-Null
    }
    
    # Copiar archivos excluyendo:
    # - .git/ (no romper repo en destino)
    # - *.~u, *.~u1 (ficheros temporales de usuario TwinCAT)
    # - _Config/PLC/*.xti (config de target AMS NetId, especifica de cada maquina)
    $twinCATFiles = Get-ChildItem -Path $twinCATLocalPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { 
            $_.FullName -notmatch '[\\/]\.git[\\/]' -and
            $_.Name -notmatch '\.~u\d?$' -and
            $_.FullName -notmatch '[\\/]_Config[\\/]PLC[\\/].*\.xti$'
        }
    
    $copiedCount = 0
    $skippedCount = 0
    foreach ($file in $twinCATFiles) {
        $relativePath = $file.FullName.Substring($twinCATLocalPath.Length)
        $destFile = Join-Path $twinCatRemoteDestPath $relativePath
        $destDir = Split-Path -Parent $destFile
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        # .sln y .plcproj: solo copiar si NO existen en destino (evitar sobreescribir config de maquina)
        if ($file.Extension -in '.sln', '.plcproj') {
            if (Test-Path $destFile) {
                $skippedCount++
                continue
            }
        }
        
        Copy-Item -Path $file.FullName -Destination $destFile -Force -ErrorAction SilentlyContinue
        $copiedCount++
    }
    Write-Success "TwinCAT copiado: $copiedCount archivos (sin .git/, .xti, .~u)"
    if ($skippedCount -gt 0) {
        Write-Info "Preservados $skippedCount archivos de maquina (.sln, .plcproj ya existentes)"
    }
} else {
    Write-Warning "⚠️ No se encontro repositorio TwinCAT local - Saltando"
    Write-Info "   Buscado en: $twinCATDevPath y $twinCATRoot\SW.PC.Twincat_3\"
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

} # Fin del else (modo normal vs -CodeOnly)

# ============================================================
# PASO 9.0.1: Generar deploy-version.json en raiz de Backend
# ============================================================
Write-Step "Generando deploy-version.json..."

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

# Guardar deploy-version.json en raiz de Backend (NO dentro de Projects/)
# Asi copiar carpetas de proyecto nunca afecta la version del servidor
$projectVersionPath = "$RemotePath\Backend\deploy-version.json"
Set-Content -Path $projectVersionPath -Value $deployVersionJson -Encoding UTF8
Write-Success "deploy-version.json guardado en Backend/ (raiz)"
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
# PASO 9.1: ELIMINADO - Aquafrisch.db es LEGACY
# ============================================================
# ❌ NO se copia Data/Aquafrisch.db - Es solo para modo legacy (default)
# ✅ En producción se usa Projects/{projectId}/data/project.db
# La base de datos del proyecto ya se copió en el PASO 9.0
Write-Info "📦 Base de datos: Projects/$ProjectId/data/project.db (ya copiada en paso 9.0)"
Write-Info "⚠️ Data/Aquafrisch.db NO se copia (modo legacy no permitido en producción)"

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

# NOTA: La carpeta wwwroot/audit ya NO se crea aquí
# Los audit logs ahora van en Projects/{projectId}/audit/ (multi-proyecto)
# y se crean dinámicamente cuando el backend necesita escribir logs

# ============================================================
# PASO 9.4: Generar certificado SSL autofirmado
# ============================================================
Write-Header "PASO 9.4: Generando certificado SSL"

$certPassword = "Aquafrisch2024!"
$certRemoteDest = "$RemotePath\Backend\certificate.pfx"

# Solo generar si no existe (preservar certificado existente)
if (Test-Path $certRemoteDest) {
    Write-Info "Certificado SSL ya existe en destino - NO se sobreescribe"
    Write-Info "Para regenerar, elimina manualmente: $InstallPath\Backend\certificate.pfx"
} else {
    Write-Step "Generando certificado SSL localmente y copiando via SMB..."
    try {
        $localCertPath = "$BackendPath\publish\certificate.pfx"
        
        # Crear certificado autofirmado en el almacen local
        $cert = New-SelfSignedCertificate `
            -DnsName "localhost", $TargetIP, "aquafrisch-supervisor" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(10) `
            -FriendlyName "Aquafrisch Supervisor SSL" `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
        
        # Exportar a PFX
        $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $localCertPath -Password $securePassword | Out-Null
        
        # Copiar al remoto via SMB
        Copy-Item -Path $localCertPath -Destination $certRemoteDest -Force
        
        # Limpiar certificado local del almacen
        Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue
        Remove-Item -Path $localCertPath -Force -ErrorAction SilentlyContinue
        
        Write-Success "Certificado SSL generado y copiado al servidor"
        Write-Info "El certificado es valido por 10 años"
        Write-Info "Contraseña del certificado: $certPassword"
    } catch {
        Write-Error2 "No se pudo generar certificado: $_"
        Write-Info "IMPORTANTE: Debes generar el certificado manualmente en el servidor:"
        Write-Info '  $cert = New-SelfSignedCertificate -DnsName "localhost","192.168.2.161" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10)'
        Write-Info '  $pwd = ConvertTo-SecureString -String "Aquafrisch2024!" -Force -AsPlainText'
        Write-Info '  Export-PfxCertificate -Cert $cert -FilePath "C:\Aquafrisch Supervisor\Backend\certificate.pfx" -Password $pwd'
    }
}

# ============================================
# PASO 10: Registrar como Servicio de Windows
# ============================================
Write-Header "PASO 10: Registrando Servicio de Windows"

$serviceDisplayName = "Aquafrisch Supervisor"
$serviceDescription = "Aquafrisch Supervisor - SCADA/HMI Backend (Production)"
$serviceExePath = "$InstallPath\Backend\SW.PC.API.Backend.exe"
# En produccion NO se pasa --environment (por defecto usa Production)
$serviceBinPath = """$serviceExePath"""

Write-Step "Configurando servicio '$serviceName' remotamente via sc.exe..."

# Si ya existe, eliminar para recrear con configuracion actualizada
$scQuery = sc.exe \\$TargetIP query $serviceName 2>&1
if ($scQuery -match "SERVICE_NAME|NOMBRE_SERVICIO|RUNNING|STOPPED") {
    Write-Info "Servicio existente encontrado, eliminando para recrear..."
    sc.exe \\$TargetIP stop $serviceName 2>$null | Out-Null
    Start-Sleep -Seconds 3
    sc.exe \\$TargetIP delete $serviceName 2>$null | Out-Null
    Start-Sleep -Seconds 3
    Write-Info "Servicio anterior eliminado"
}

# Crear servicio nuevo (start=auto = arranca con Windows)
# NOTA: sc.exe requiere formato MUY especifico: binPath= "valor" (espacio despues del =)
Write-Step "Creando servicio..."
$scCreateCmd = "sc.exe \\$TargetIP create $serviceName binPath= `"$serviceExePath`" start= auto DisplayName= `"$serviceDisplayName`""
Write-Info "Comando: $scCreateCmd"
$createResult = cmd.exe /c $scCreateCmd 2>&1
if ($createResult -match "SUCCESS|EXITO|CORRECTO") {
    Write-Success "Servicio '$serviceName' creado"
} elseif ($createResult -match "1073|ya existe|already exists") {
    # El servicio ya existe (el delete no se completo a tiempo), reintentamos
    Write-Info "Servicio aun existe, esperando y reintentando..."
    sc.exe \\$TargetIP stop $serviceName 2>$null | Out-Null
    Start-Sleep -Seconds 5
    sc.exe \\$TargetIP delete $serviceName 2>$null | Out-Null
    Start-Sleep -Seconds 5
    $createResult2 = cmd.exe /c $scCreateCmd 2>&1
    if ($createResult2 -match "SUCCESS|EXITO|CORRECTO") {
        Write-Success "Servicio '$serviceName' creado (segundo intento)"
    } else {
        Write-Error2 "Error creando servicio: $createResult2"
        Write-Info "Puedes eliminarlo manualmente: sc.exe \\$TargetIP delete $serviceName"
        Read-Host "Presiona Enter para cerrar"
        exit 1
    }
} else {
    Write-Error2 "Error creando servicio: $createResult"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

# Configurar descripcion
cmd.exe /c "sc.exe \\$TargetIP description $serviceName `"$serviceDescription`"" 2>$null | Out-Null

# Configurar recovery: reinicio automatico a los 10s, 30s, 60s
sc.exe \\$TargetIP failure $serviceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 2>$null | Out-Null
Write-Success "Recovery configurado (reinicio automatico en caso de fallo)"

# Arrancar el servicio
Write-Step "Arrancando servicio..."
$startResult = sc.exe \\$TargetIP start $serviceName 2>&1
if ($startResult -match "START_PENDING|RUNNING") {
    Start-Sleep -Seconds 3
    # Verificar que arranco
    $scStatus = sc.exe \\$TargetIP query $serviceName 2>&1
    if ($scStatus -match "RUNNING") {
        Write-Success "Servicio '$serviceName' CORRIENDO en $TargetIP"
    } else {
        Write-Error2 "El servicio no arranco correctamente. Revisa los logs en el servidor."
    }
} else {
    Write-Error2 "Error arrancando servicio: $startResult"
}

# ============================================
# PASO 10.5: Configurar Firewall
# ============================================
Write-Header "PASO 10.5: Configurando Firewall"

Write-Info "Para que el servidor sea accesible desde la red, ejecuta esto en el PC destino (como Admin):"
Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow" -ForegroundColor Yellow
Write-Info "(Solo necesario la primera vez)"

# ============================================
# PASO 11: Desconectar
# ============================================
Write-Header "PASO 11: Limpieza"

& net use "\\$TargetIP\C`$" /delete /y 2>&1 | Out-Null
Write-Success "Conexion de red cerrada"

# ============================================
# RESUMEN FINAL
# ============================================
Write-Header "DESPLIEGUE COMPLETADO"
Write-Host ""
Write-Host "  PC Destino: $TargetIP" -ForegroundColor White
Write-Host "  Ruta: $InstallPath" -ForegroundColor White
if ($CodeOnly) {
    Write-Host "  Modo: SOLO CODIGO (Backend + Frontend actualizados)" -ForegroundColor Yellow
    Write-Host "  Proyectos: NO modificados" -ForegroundColor Green
} else {
    Write-Host "  Modo: SERVICIO WINDOWS (self-contained)" -ForegroundColor Green
}
Write-Host ""
Write-Host "  PROYECTO:" -ForegroundColor Green
Write-Host "  =====================" -ForegroundColor Green
Write-Host "  Proyecto: $ProjectId" -ForegroundColor White
Write-Host "  Config: $InstallPath\Backend\Projects\$ProjectId\config\" -ForegroundColor Gray
Write-Host "  Modelos: $InstallPath\Backend\Projects\$ProjectId\models\" -ForegroundColor Gray
Write-Host "  Database: $InstallPath\Backend\Projects\$ProjectId\data\project.db" -ForegroundColor Gray
Write-Host "  Version: $InstallPath\Backend\deploy-version.json" -ForegroundColor Gray
Write-Host ""
Write-Host "  Archivos desplegados:" -ForegroundColor Cyan
Write-Host "  - Backend (exe + dlls)     -> $InstallPath\Backend\" -ForegroundColor Gray
Write-Host "  - Frontend (React)         -> $InstallPath\Backend\wwwroot\" -ForegroundColor Gray
Write-Host "  - Certificado SSL          -> $InstallPath\Backend\certificate.pfx" -ForegroundColor Gray
Write-Host "  - active-project.json      -> $InstallPath\Backend\ (proyecto: $ProjectId)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Servicio Windows:" -ForegroundColor Cyan
Write-Host "  - Nombre: $serviceName" -ForegroundColor White
Write-Host "  - Inicio: Automatico (arranca con Windows)" -ForegroundColor White
Write-Host "  - Recovery: Reinicio automatico (10s/30s/60s)" -ForegroundColor White
Write-Host "  - Entorno: Production" -ForegroundColor White
Write-Host ""
Write-Host "  Comandos remotos utiles (desde este PC):" -ForegroundColor Cyan
Write-Host "    sc.exe \\$TargetIP query $serviceName        # Ver estado" -ForegroundColor Yellow
Write-Host "    sc.exe \\$TargetIP stop $serviceName         # Parar" -ForegroundColor Yellow
Write-Host "    sc.exe \\$TargetIP start $serviceName        # Arrancar" -ForegroundColor Yellow
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
Write-Host "  URLs de acceso:" -ForegroundColor Cyan
Write-Host "  - HTTP:  http://${TargetIP}:5000" -ForegroundColor White
Write-Host "  - HTTPS: https://${TargetIP}:5001 (SEGURO - RECOMENDADO)" -ForegroundColor Green
Write-Host ""
Write-Host "  NOTA: El servidor esta corriendo como servicio Windows." -ForegroundColor Yellow
Write-Host "        Se inicia automaticamente con el PC y se reinicia en caso de fallo." -ForegroundColor Yellow
Write-Host ""
Write-Host "  NOTA: El certificado SSL es autofirmado. El navegador mostrara" -ForegroundColor Yellow
Write-Host "        una advertencia la primera vez. Esto es normal en redes internas." -ForegroundColor Yellow
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan

# Mantener ventana abierta
Write-Host ""
Read-Host "Presiona Enter para cerrar"
