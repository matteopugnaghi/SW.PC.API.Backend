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

# Screensaver idle timeout
$idleInput = Read-Host "Screensaver: minutos de inactividad antes de activar [30]"
if ([string]::IsNullOrEmpty($idleInput)) { $idleInput = '30' }
$IdleTimeoutMinutes = [int]$idleInput
Write-Info "Screensaver idle timeout: $IdleTimeoutMinutes minutos"
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
            "$BackendPath\publish\wwwroot\sbom",
            "$BackendPath\publish\CodeCoverage",
            "$BackendPath\publish\InstrumentationEngine",
            "$BackendPath\publish\Projects",
            "$BackendPath\publish\backups"
        )
        # 🧹 Limpiar archivos de test/coverage que no deben ir a producción
        $devFilesToRemove = Get-ChildItem -Path "$BackendPath\publish" -Filter "coverlet.*" -ErrorAction SilentlyContinue
        foreach ($devFolder in $devFoldersToRemove) {
            if (Test-Path $devFolder) {
                Remove-Item -Path $devFolder -Recurse -Force -ErrorAction SilentlyContinue
                Write-Info "🧹 Eliminado de publish: $($devFolder | Split-Path -Leaf)"
            }
        }
        foreach ($devFile in $devFilesToRemove) {
            Remove-Item -Path $devFile.FullName -Force -ErrorAction SilentlyContinue
            Write-Info "🧹 Eliminado de publish: $($devFile.Name)"
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
        
        # 🔐 Asegurar allowed_signers ANTES de verificar firmas
        # Sin esto, git log --format=%G? siempre devuelve "N" aunque el commit esté firmado
        $gpgFormat = (git config --global gpg.format 2>$null) -replace "`n|`r", ""
        $signingKey = (git config --global user.signingkey 2>$null) -replace "`n|`r", ""
        if ($gpgFormat -eq "ssh" -and $signingKey) {
            $sshDir = Split-Path $signingKey -Parent
            $allowedSignersPath = Join-Path $sshDir "allowed_signers"
            if (-not (Test-Path $allowedSignersPath)) {
                # Crear allowed_signers con la clave local
                if (Test-Path $signingKey) {
                    $pubKey = (Get-Content $signingKey -Raw).Trim()
                    $gitEmail = (git config --global user.email 2>$null) -replace "`n|`r", ""
                    if (-not $gitEmail) { $gitEmail = "electronico@aquafrisch.com" }
                    Set-Content -Path $allowedSignersPath -Value "$gitEmail namespaces=`"git`" $pubKey" -Encoding UTF8
                    Write-Host "   🔐 Created allowed_signers: $allowedSignersPath" -ForegroundColor Cyan
                }
            }
            # Asegurar que git config apunta al archivo
            $currentAllowed = (git config --global gpg.ssh.allowedSignersFile 2>$null) -replace "`n|`r", ""
            if (-not $currentAllowed -or -not (Test-Path $currentAllowed)) {
                git config --global gpg.ssh.allowedSignersFile $allowedSignersPath 2>$null
                Write-Host "   🔐 Configured allowedSignersFile: $allowedSignersPath" -ForegroundColor Cyan
            }
            # También cargar claves de authorized_signing_keys.json si existe
            $authKeysFile = Join-Path $RepoPath "authorized_signing_keys.json"
            if (-not (Test-Path $authKeysFile)) { $authKeysFile = Join-Path $ScriptPath "authorized_signing_keys.json" }
            if (Test-Path $authKeysFile) {
                try {
                    $authKeys = Get-Content $authKeysFile -Raw | ConvertFrom-Json
                    $existingContent = if (Test-Path $allowedSignersPath) { Get-Content $allowedSignersPath -Raw } else { "" }
                    foreach ($ak in $authKeys) {
                        if ($ak.PublicKey -and -not $existingContent.Contains($ak.PublicKey.Split(' ')[1])) {
                            $akEmail = if ($ak.OwnerEmail) { $ak.OwnerEmail } else { "electronico@aquafrisch.com" }
                            Add-Content -Path $allowedSignersPath -Value "$akEmail namespaces=`"git`" $($ak.PublicKey)" -Encoding UTF8
                        }
                    }
                } catch { }
            }
        } else {
            # 🔐 Modo verification-only: no hay SSH signing local, pero podemos verificar firmas de otros
            # Primero comprobar si ya está configurado (por una llamada anterior)
            $existingAllowed = (git config --global gpg.ssh.allowedSignersFile 2>$null) -replace "`n|`r", ""
            if ($existingAllowed -and (Test-Path $existingAllowed)) {
                # Ya configurado por llamada anterior (Backend) — no hacer nada
            } else {
            $authKeysFile = Join-Path $RepoPath "authorized_signing_keys.json"
            # Fallback: buscar en el directorio del script (Backend) si no existe en $RepoPath
            if (-not (Test-Path $authKeysFile)) {
                $authKeysFile = Join-Path $ScriptPath "authorized_signing_keys.json"
            }
            if (Test-Path $authKeysFile) {
                try {
                    $authKeys = Get-Content $authKeysFile -Raw | ConvertFrom-Json
                    $validKeys = @($authKeys | Where-Object { $_.PublicKey -and $_.Fingerprint })
                    if ($validKeys.Count -gt 0) {
                        $userProfile = [Environment]::GetFolderPath("UserProfile")
                        $sshDir = Join-Path $userProfile ".ssh"
                        if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Path $sshDir -Force | Out-Null }
                        $allowedSignersPath = Join-Path $sshDir "allowed_signers"
                        $signerLines = @()
                        foreach ($ak in $validKeys) {
                            $akEmail = if ($ak.OwnerEmail) { $ak.OwnerEmail } else { "electronico@aquafrisch.com" }
                            $signerLines += "$akEmail namespaces=`"git`" $($ak.PublicKey)"
                        }
                        Set-Content -Path $allowedSignersPath -Value ($signerLines -join "`n") -Encoding UTF8
                        git config --global gpg.format ssh 2>$null
                        git config --global gpg.ssh.allowedSignersFile $allowedSignersPath 2>$null
                        Write-Host "   🔐 Verification-only: created allowed_signers with $($validKeys.Count) key(s)" -ForegroundColor Cyan
                    }
                } catch { }
            }
            } # end if not already configured
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
$prevEAP = $ErrorActionPreference

# --- Fase 1: Deshabilitar recovery y auto-start via sc.exe (no requiere WinRM) ---
Write-Step "Deshabilitando auto-start y recovery del servicio..."
sc.exe \\$TargetIP failure $serviceName reset= 0 actions= "" 2>&1 | Out-Null
sc.exe \\$TargetIP config $serviceName start= demand 2>&1 | Out-Null
Write-Info "Recovery desactivado y start-type cambiado a Manual"

# --- Fase 2: Parar servicio y matar proceso via WinRM (ejecuta LOCALMENTE en el IPC) ---
Write-Step "Parando servicio y proceso via WinRM (Invoke-Command)..."
try {
    $killResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        param($svcName)
        $output = @()
        
        # Parar servicio
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -ne 'Stopped') {
            Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
            $output += "Servicio detenido"
        } elseif ($svc) {
            $output += "Servicio ya estaba parado"
        } else {
            $output += "Servicio no existe"
        }
        
        # Matar proceso (por si quedo zombie)
        $procs = Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue
        if ($procs) {
            $procs | Stop-Process -Force
            $output += "Proceso eliminado (PID: $($procs.Id -join ', '))"
        } else {
            $output += "Proceso no encontrado (limpio)"
        }
        
        # Verificar que no queda nada
        Start-Sleep -Seconds 2
        $check = Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue
        if ($check) {
            $output += "ADVERTENCIA: Proceso sigue vivo tras kill"
        } else {
            $output += "Confirmado: proceso terminado"
        }
        
        return $output
    } -ArgumentList $serviceName -ErrorAction Stop
    
    foreach ($line in $killResult) {
        if ($line -match "ADVERTENCIA") {
            Write-Error2 $line
        } elseif ($line -match "eliminado|detenido|Confirmado") {
            Write-Success $line
        } else {
            Write-Info $line
        }
    }
} catch {
    Write-Info "WinRM no disponible, usando sc.exe + taskkill remoto como fallback..."
    
    # Fallback: sc.exe stop
    $scQuery = sc.exe \\$TargetIP query $serviceName 2>&1
    if ($scQuery -match "RUNNING|START_PENDING") {
        sc.exe \\$TargetIP stop $serviceName 2>&1 | Out-Null
        Write-Info "sc.exe stop enviado, esperando 10s..."
        Start-Sleep -Seconds 10
    }
    
    # Fallback: taskkill remoto
    $ErrorActionPreference = 'SilentlyContinue'
    taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1 | Out-Null
    $ErrorActionPreference = $prevEAP
    Start-Sleep -Seconds 5
}

# --- Fase 3: Verificar que los DLLs estan liberados ---
Write-Step "Verificando que los DLLs estan liberados..."
Start-Sleep -Seconds 2

$testFile = $null
foreach ($candidate in @("$RemotePath\Backend\hostfxr.dll", "$RemotePath\Backend\ClosedXML.dll", "$RemotePath\Backend\SW.PC.API.Backend.dll")) {
    if (Test-Path $candidate) { $testFile = $candidate; break }
}

if (-not $testFile) {
    Write-Info "Primera instalacion (no hay archivos previos)"
} else {
    $maxRetries = 10
    $retryCount = 0
    
    while ($retryCount -lt $maxRetries) {
        try {
            $stream = [System.IO.File]::Open($testFile, 'Open', 'Read', 'None')
            $stream.Close()
            Write-Success "Archivos liberados correctamente"
            break
        } catch {
            $retryCount++
            if ($retryCount -ge $maxRetries) {
                Write-Error2 "Los archivos siguen bloqueados despues de $maxRetries intentos"
                Write-Host "  SOLUCION: En el IPC ($TargetIP) ejecuta:" -ForegroundColor Yellow
                Write-Host "    Stop-Service $serviceName -Force" -ForegroundColor Cyan
                Write-Host "    taskkill /IM SW.PC.API.Backend.exe /F" -ForegroundColor Cyan
                Read-Host "Presiona Enter para cerrar"
                exit 1
            }
            Write-Info "DLLs bloqueados, esperando... (intento $retryCount/$maxRetries)"
            
            # En el intento 3, probar WinRM de nuevo por si el proceso reaparecio
            if ($retryCount -eq 3) {
                Write-Info "Segundo intento de kill via WinRM..."
                try {
                    Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
                        Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue | Stop-Process -Force
                    } -ErrorAction Stop
                } catch { }
            }
            
            Start-Sleep -Seconds 3
        }
    }
}

# --- Fase 4: Verificar que los puertos 5000/5001 estan libres ---
Write-Step "Verificando que los puertos 5000/5001 estan libres..."
try {
    $portCheck = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        $output = @()
        $ports = @(5000, 5001)
        foreach ($port in $ports) {
            $conn = netstat -ano | Select-String ":$port " | Select-String "LISTENING"
            if ($conn) {
                # Extraer PID del proceso que ocupa el puerto
                $pidMatch = $conn -match '\s+(\d+)\s*$'
                if ($pidMatch) {
                    $pid = ($Matches[1])
                    $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
                    $procName = if ($proc) { $proc.ProcessName } else { "desconocido" }
                    $output += "OCUPADO:Puerto $port ocupado por $procName (PID: $pid)"
                    # Matar si es nuestro proceso zombie
                    if ($procName -eq "SW.PC.API.Backend") {
                        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                        $output += "KILLED:Proceso zombie $procName (PID: $pid) eliminado"
                    }
                }
            } else {
                $output += "LIBRE:Puerto $port libre"
            }
        }
        return $output
    } -ErrorAction Stop

    foreach ($line in $portCheck) {
        if ($line -match '^KILLED:(.+)$') {
            Write-Warning $Matches[1]
            Start-Sleep -Seconds 3  # Esperar a que se libere el puerto
        } elseif ($line -match '^OCUPADO:(.+)$') {
            Write-Warning $Matches[1]
        } elseif ($line -match '^LIBRE:(.+)$') {
            Write-Success $Matches[1]
        }
    }
} catch {
    Write-Info "No se pudo verificar puertos via WinRM (se verificara antes de arrancar)"
}

Write-Success "Servidor remoto preparado para despliegue"

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
    
    # 5. authorized_signing_keys.json y access_control_config.json (SSH signing + access control)
    $authKeysFile = "$RemotePath\Backend\authorized_signing_keys.json"
    if (Test-Path $authKeysFile) {
        Copy-Item -Path $authKeysFile -Destination "$backupPath\Backend\" -Force
        Write-Info "  - authorized_signing_keys.json (SSH signing keys)"
    }
    $accessControlFile = "$RemotePath\Backend\access_control_config.json"
    if (Test-Path $accessControlFile) {
        Copy-Item -Path $accessControlFile -Destination "$backupPath\Backend\" -Force
        Write-Info "  - access_control_config.json (access control)"
    }
    
    # 6. NO hacer backup de: wwwroot/ (frontend), exe/dlls (se regeneran)
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

# 🔐 ANTES de copiar: preservar archivos de configuración que no vienen en publish
Write-Step "Preservando archivos de configuración del servidor..."
$preserveFiles = @(
    "authorized_signing_keys.json",
    "access_control_config.json",
    "active-project.json",
    "certificate.pfx",
    "appsettings.Production.json"
)
$tempPreserve = "$env:TEMP\deploy_preserve_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $tempPreserve -Force | Out-Null
$preservedCount = 0
foreach ($preserveFile in $preserveFiles) {
    $sourceFile = "$RemotePath\Backend\$preserveFile"
    if (Test-Path $sourceFile) {
        Copy-Item -Path $sourceFile -Destination "$tempPreserve\$preserveFile" -Force
        Write-Info "  🔒 Preservado: $preserveFile"
        $preservedCount++
    }
}
if ($preservedCount -eq 0) {
    Write-Info "  (primera instalación — no hay archivos que preservar)"
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

# Estrategia: copiar a carpeta temporal (sin locks) y luego mover localmente via WinRM
$remoteTempDeploy = "$RemotePath\_deploy_staging"

# 1. Limpiar staging previo si existe
if (Test-Path $remoteTempDeploy) {
    Remove-Item -Path $remoteTempDeploy -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -Path $remoteTempDeploy -ItemType Directory -Force | Out-Null

# 2. Copiar a staging (carpeta nueva = sin locks, siempre funciona)
Write-Step "Copiando a staging temporal..."
try {
    Copy-Item -Path "$publishPath\*" -Destination $remoteTempDeploy -Recurse -Force -ErrorAction Stop
    Write-Success "Staging completado: $totalFiles archivos"
} catch {
    Write-Error2 "Error copiando a staging: $($_.Exception.Message)"
    Remove-Item -Path $remoteTempDeploy -Recurse -Force -ErrorAction SilentlyContinue
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

# 3. Via WinRM: matar todo + copiar localmente (local = sin problemas de SMB)
Write-Step "Moviendo staging a Backend via WinRM (copia local en IPC)..."
$copySuccess = $false
for ($copyAttempt = 1; $copyAttempt -le 3; $copyAttempt++) {
    try {
        $moveResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
            param($StagingDir, $BackendDir, $SvcName)
            $output = @()
            
            # Kill agresivo
            & sc.exe failure $SvcName reset= 0 actions= "" 2>&1 | Out-Null
            & sc.exe config $SvcName start= demand 2>&1 | Out-Null
            Stop-Service -Name $SvcName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue | Stop-Process -Force
            Get-Process -Name "msedge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
            
            # Verificar que el proceso murio
            $still = Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue
            if ($still) {
                & taskkill /IM "SW.PC.API.Backend.exe" /F /T 2>&1 | Out-Null
                Start-Sleep -Seconds 3
            }
            
            # Copiar localmente (sin SMB locks)
            try {
                Copy-Item -Path "$StagingDir\*" -Destination $BackendDir -Recurse -Force -ErrorAction Stop
                $output += "OK:Archivos copiados localmente"
            } catch {
                $output += "FAIL:$($_.Exception.Message)"
            }
            
            return $output
        } -ArgumentList $remoteTempDeploy, "$RemotePath\Backend", $serviceName -ErrorAction Stop
        
        $failed = $false
        foreach ($line in $moveResult) {
            if ($line -match '^OK:(.+)$') {
                Write-Success $Matches[1]
                $copySuccess = $true
            } elseif ($line -match '^FAIL:(.+)$') {
                Write-Error2 "Copia local fallo: $($Matches[1])"
                $failed = $true
            }
        }
        
        if ($copySuccess) { break }
        if ($failed -and $copyAttempt -lt 3) {
            Write-Info "Reintentando en 10s... (intento $copyAttempt/3)"
            Start-Sleep -Seconds 10
        }
    } catch {
        Write-Error2 "WinRM fallo: $($_.Exception.Message)"
        if ($copyAttempt -lt 3) {
            Write-Info "Reintentando WinRM en 10s... (intento $copyAttempt/3)"
            Start-Sleep -Seconds 10
        }
    }
}

# 4. Limpiar staging
Remove-Item -Path $remoteTempDeploy -Recurse -Force -ErrorAction SilentlyContinue

if (-not $copySuccess) {
    Write-Error2 "No se pudo copiar el backend despues de 3 intentos"
    Write-Host ""
    Write-Host "  SOLUCION: En el IPC ($TargetIP) ejecuta:" -ForegroundColor Yellow
    Write-Host "    Stop-Service $serviceName -Force" -ForegroundColor Cyan
    Write-Host "    taskkill /IM SW.PC.API.Backend.exe /F" -ForegroundColor Cyan
    Write-Host "  Luego vuelve a ejecutar este script" -ForegroundColor White
    Write-Host ""
    Read-Host "Presiona Enter para cerrar"
    exit 1
}
Write-Success "Backend copiado: $totalFiles archivos"

# � Copiar scripts kiosk (Tools\Kiosk\) — no están en publish
$kioskSrcDir = Join-Path $BackendPath "Tools\Kiosk"
$kioskDstDir = "$RemotePath\Backend\Tools\Kiosk"
if (Test-Path $kioskSrcDir) {
    Write-Step "Copiando scripts kiosk (Tools\Kiosk\)..."
    if (-not (Test-Path $kioskDstDir)) {
        New-Item -Path $kioskDstDir -ItemType Directory -Force | Out-Null
    }
    $kioskFiles = Get-ChildItem -Path $kioskSrcDir -File | Where-Object { $_.Name -match '\.(ps1|bat|ttf)$' }
    foreach ($kf in $kioskFiles) {
        Copy-Item -Path $kf.FullName -Destination "$kioskDstDir\$($kf.Name)" -Force
        Write-Info "  📄 $($kf.Name)"
    }
    Write-Success "Scripts kiosk copiados ($($kioskFiles.Count) archivos)"
}

# �🔐 DESPUÉS de copiar: restaurar archivos preservados
Write-Step "Restaurando archivos de configuración preservados..."
$restoredCount = 0
foreach ($preserveFile in $preserveFiles) {
    $savedFile = "$tempPreserve\$preserveFile"
    $destFile = "$RemotePath\Backend\$preserveFile"
    if (Test-Path $savedFile) {
        Copy-Item -Path $savedFile -Destination $destFile -Force
        Write-Info "  ♻️ Restaurado: $preserveFile"
        $restoredCount++
    }
}
# Limpiar carpeta temporal
Remove-Item -Path $tempPreserve -Recurse -Force -ErrorAction SilentlyContinue
if ($restoredCount -gt 0) {
    Write-Success "Restaurados $restoredCount archivos de configuración"
}

# ⏱️ Inyectar IdleTimeoutMinutes en LaunchKiosk.bat del destino
$remoteBat = "$RemotePath\Backend\Tools\Kiosk\LaunchKiosk.bat"
if (Test-Path $remoteBat) {
    (Get-Content $remoteBat -Raw) -replace 'SET IDLE_TIMEOUT=\d+', "SET IDLE_TIMEOUT=$IdleTimeoutMinutes" |
        Set-Content $remoteBat -NoNewline
    Write-Success "Screensaver configurado: $IdleTimeoutMinutes minutos en LaunchKiosk.bat"
} else {
    Write-Info "LaunchKiosk.bat no encontrado en destino (ejecutar Configure-Kiosk.ps1 primero)"
}

# � Instalar fuentes Crillee en el IPC (si no están instaladas)
$fontSourceDir = Join-Path $BackendPath "Tools\Kiosk"
$fontFiles = @('CRILLE.ttf', 'Crillee Regular.ttf')
$fontsToInstall = @()
foreach ($fontFile in $fontFiles) {
    $srcFont = Join-Path $fontSourceDir $fontFile
    if (Test-Path $srcFont) {
        $dstFont = "$RemotePath\Backend\Tools\Kiosk\$fontFile"
        Copy-Item -Path $srcFont -Destination $dstFont -Force
        $fontsToInstall += $srcFont
    }
}
if ($fontsToInstall.Count -gt 0) {
    try {
        Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
            param($KioskDir)
            $fontFiles = Get-ChildItem -Path $KioskDir -Filter '*.ttf' -ErrorAction SilentlyContinue
            foreach ($f in $fontFiles) {
                $dest = Join-Path $env:windir "Fonts\$($f.Name)"
                if (-not (Test-Path $dest)) {
                    Copy-Item -Path $f.FullName -Destination $dest -Force
                    # Registrar en el registro
                    $regName = "$($f.BaseName) (TrueType)"
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts' -Name $regName -Value $f.Name
                    Write-Output "INSTALLED:$($f.Name)"
                } else {
                    Write-Output "EXISTS:$($f.Name)"
                }
            }
        } -ArgumentList "$RemotePath\Backend\Tools\Kiosk" -ErrorAction Stop | ForEach-Object {
            if ($_ -match '^INSTALLED:(.+)$') {
                Write-Info "  🔤 Fuente instalada: $($Matches[1])"
            } elseif ($_ -match '^EXISTS:(.+)$') {
                Write-Info "  🔤 Fuente ya existía: $($Matches[1])"
            }
        }
        Write-Success "Fuentes Crillee verificadas en el IPC"
    } catch {
        Write-Info "⚠️ No se pudieron instalar las fuentes via WinRM: $($_.Exception.Message)"
        Write-Info "  Copiar manualmente CRILLE.ttf y 'Crillee Regular.ttf' a C:\Windows\Fonts\ en el IPC"
    }
}

# �🔐 Merge: sincronizar claves SSH del desarrollo al servidor
$localAuthKeys = Join-Path $BackendPath "authorized_signing_keys.json"
$remoteAuthKeys = "$RemotePath\Backend\authorized_signing_keys.json"
if (Test-Path $localAuthKeys) {
    try {
        $localKeys = Get-Content $localAuthKeys -Raw | ConvertFrom-Json
        $validLocalKeys = @($localKeys | Where-Object { $_.Fingerprint -and $_.PublicKey })
        
        if (Test-Path $remoteAuthKeys) {
            $remoteKeys = Get-Content $remoteAuthKeys -Raw | ConvertFrom-Json
            $validRemoteKeys = @($remoteKeys | Where-Object { $_.Fingerprint })
            $cleaned = $remoteKeys.Count - $validRemoteKeys.Count
            $added = 0
            foreach ($lk in $validLocalKeys) {
                $exists = $validRemoteKeys | Where-Object { $_.Fingerprint -eq $lk.Fingerprint }
                if (-not $exists) {
                    $validRemoteKeys += $lk
                    $added++
                }
            }
            if ($added -gt 0 -or $cleaned -gt 0) {
                $validRemoteKeys | ConvertTo-Json -Depth 10 | Set-Content $remoteAuthKeys -Encoding UTF8
                Write-Info "  🔐 authorized_signing_keys.json actualizado (+$added claves, -$cleaned invalidas)"
            }
        } elseif ($validLocalKeys.Count -gt 0) {
            $validLocalKeys | ConvertTo-Json -Depth 10 | Set-Content $remoteAuthKeys -Encoding UTF8
            Write-Info "  🔐 authorized_signing_keys.json desplegado ($($validLocalKeys.Count) claves)"
        }
    } catch { Write-Info "  ⚠️ No se pudo sincronizar authorized_signing_keys.json" }
}

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
    
    # ==========================================
    # Git en destino: asegurar que existe .git y limpiar indice
    # ==========================================
    # El deploy copia archivos pero NO .git/ — si produccion no tiene .git, copiarlo desde local
    # Luego limpiar indice: rm --cached de .xti/.~u y assume-unchanged de .sln/.plcproj
    # NOTA: git -C NO funciona con rutas de red (UNC ni unidades mapeadas)
    #       Solucion: crear script .bat en remoto via SMB, ejecutar via schtasks
    
    # Buscar .git en produccion (puede ser carpeta oculta)
    $gitInProject = Get-Item "$twinCatRemoteDestPath\.git" -Force -ErrorAction SilentlyContinue
    
    if (-not $gitInProject) {
        # No hay .git en produccion — copiar desde local
        $localGitDir = Join-Path $twinCATLocalPath ".git"
        if (Test-Path $localGitDir) {
            Write-Step "Copiando .git/ a produccion (primera vez)..."
            $remoteGitDir = "$twinCatRemoteDestPath\.git"
            # Copiar toda la carpeta .git recursivamente
            Copy-Item -Path $localGitDir -Destination $remoteGitDir -Recurse -Force -ErrorAction SilentlyContinue
            $gitInProject = Get-Item $remoteGitDir -Force -ErrorAction SilentlyContinue
            if ($gitInProject) {
                Write-Success ".git/ copiado a produccion ($((Get-ChildItem $remoteGitDir -Recurse -File | Measure-Object).Count) archivos)"
            } else {
                Write-Warning "Error: no se pudo copiar .git/ a produccion"
            }
        } else {
            Write-Warning "No hay .git en local ($localGitDir) — no se puede copiar a produccion"
        }
    } else {
        Write-Info "Git repo ya existe en produccion: $twinCatRemoteDestPath\.git"
    }
    
    # Ejecutar limpieza de indice git EN la maquina remota via schtasks
    if ($gitInProject) {
        Write-Step "Limpiando indice git (ejecutando git EN la maquina remota via schtasks)..."
        
        $remoteLocalRepoPath = "$InstallPath\SW.PC.Twincat_3\$twinCATLocalName"
        $remoteLocalResultFile = "$remoteLocalRepoPath\git-cleanup-result.txt"
        
        # Crear script .bat que se ejecutara EN la maquina remota
        $batContent = @"
@echo off
cd /d "$remoteLocalRepoPath"
echo === Git Cleanup Start === > "$remoteLocalResultFile"
echo Repo: %CD% >> "$remoteLocalResultFile"
git rm --cached -r --ignore-unmatch "*.xti" >> "$remoteLocalResultFile" 2>&1
git rm --cached -r --ignore-unmatch "*.~u" >> "$remoteLocalResultFile" 2>&1
git rm --cached -r --ignore-unmatch "*.~u1" >> "$remoteLocalResultFile" 2>&1
git update-index --assume-unchanged "Twincat_Prog.sln" >> "$remoteLocalResultFile" 2>&1
git update-index --assume-unchanged "Twincat_Prog/PLC/PLC.plcproj" >> "$remoteLocalResultFile" 2>&1
git status --short >> "$remoteLocalResultFile" 2>&1
echo === Git Cleanup Done === >> "$remoteLocalResultFile"
"@
        
        $remoteBatPath = "$twinCatRemoteDestPath\git-cleanup.bat"
        $remoteResultPath = "$twinCatRemoteDestPath\git-cleanup-result.txt"
        Set-Content -Path $remoteBatPath -Value $batContent -Encoding ASCII
        
        $taskName = "AquafrischGitCleanup"
        try {
            schtasks /delete /s $TargetIP /u "$TargetIP\$TargetUser" /p "$TargetPassword" /tn $taskName /f 2>&1 | Out-Null
            
            $createResult = schtasks /create /s $TargetIP /u "$TargetIP\$TargetUser" /p "$TargetPassword" `
                /tn $taskName /tr "cmd /c `"$remoteLocalRepoPath\git-cleanup.bat`"" `
                /sc once /st 00:00 /f /ru "$TargetUser" /rp "$TargetPassword" /rl HIGHEST 2>&1
            Write-Info "schtasks create: $createResult"
            
            $runResult = schtasks /run /s $TargetIP /u "$TargetIP\$TargetUser" /p "$TargetPassword" /tn $taskName 2>&1
            Write-Info "schtasks run: $runResult"
            
            # Esperar a que termine (max 30 seg)
            $waited = 0
            while ($waited -lt 30) {
                Start-Sleep -Seconds 2
                $waited += 2
                if (Test-Path $remoteResultPath) {
                    $content = Get-Content $remoteResultPath -Raw -ErrorAction SilentlyContinue
                    if ($content -and $content -match "Git Cleanup Done") { break }
                }
            }
            
            # Mostrar resultado
            if (Test-Path $remoteResultPath) {
                $resultContent = Get-Content $remoteResultPath -ErrorAction SilentlyContinue
                Write-Info "--- Resultado git cleanup remoto ---"
                foreach ($line in $resultContent) {
                    if ($line.Trim()) { Write-Info "  $line" }
                }
                Write-Info "--- Fin resultado ---"
                Write-Success "Git cleanup ejecutado en maquina remota"
            } else {
                Write-Warning "No se encontro resultado despues de 30s — verificar manualmente"
            }
            
            # Limpieza
            schtasks /delete /s $TargetIP /u "$TargetIP\$TargetUser" /p "$TargetPassword" /tn $taskName /f 2>&1 | Out-Null
            Remove-Item $remoteBatPath -ErrorAction SilentlyContinue
            Remove-Item $remoteResultPath -ErrorAction SilentlyContinue
            
        } catch {
            Write-Warning "Error ejecutando git cleanup remoto: $_"
        }
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
$cerRemoteDest = "$RemotePath\Backend\certificate.cer"

# Verificar si el certificado existente tiene los SANs correctos (IP Address, no solo DNS Name)
$needsRegeneration = $false
if (Test-Path $certRemoteDest) {
    try {
        $existingPfx = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
            $certRemoteDest, $certPassword, 
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
        $sanExt = $existingPfx.Extensions | Where-Object { $_.Oid.Value -eq "2.5.29.17" }
        $sanText = if ($sanExt) { $sanExt.Format($false) } else { "" }
        $existingPfx.Dispose()
        
        # Verificar que tiene SAN tipo "IP Address" (no solo "DNS Name" para la IP)
        if ($sanText -match "IP.*(Address|Direcci)" -or $sanText -match "IPAddress") {
            Write-Info "Certificado SSL existente tiene IP SAN correcto - NO se sobreescribe"
            Write-Info "Para regenerar, elimina manualmente: $InstallPath\Backend\certificate.pfx"
            
            # Asegurar que el CER siempre existe junto al PFX
            if (-not (Test-Path $cerRemoteDest)) {
                Write-Step "Exportando certificado publico (CER) desde PFX existente..."
                $existingPfx2 = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
                    $certRemoteDest, $certPassword, 
                    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
                $cerBytes = $existingPfx2.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
                [System.IO.File]::WriteAllBytes($cerRemoteDest, $cerBytes)
                $existingPfx2.Dispose()
                Write-Success "Certificado publico exportado: certificate.cer"
            }
        } else {
            Write-Warning "Certificado existente NO tiene IP SAN correcto (solo DNS Name)"
            Write-Warning "Chrome/Edge requieren SAN tipo 'IP Address' para acceso por IP"
            Write-Step "Regenerando certificado con IP SAN correcto..."
            $needsRegeneration = $true
        }
    } catch {
        Write-Warning "No se pudo leer el certificado existente: $_"
        Write-Step "Regenerando certificado..."
        $needsRegeneration = $true
    }
} else {
    $needsRegeneration = $true
}

if ($needsRegeneration) {
    Write-Step "Generando certificado SSL localmente y copiando via SMB..."
    try {
        $localCertPath = "$BackendPath\publish\certificate.pfx"
        $localCerPath = "$BackendPath\publish\certificate.cer"
        
        # Obtener hostname del IPC (para SAN)
        $ipcHostname = "aquafrisch-supervisor"
        try {
            $ipcHostname = (Get-WmiObject -ComputerName $TargetIP -Class Win32_ComputerSystem -Credential $cred -ErrorAction SilentlyContinue).Name
            if (-not $ipcHostname) { $ipcHostname = "aquafrisch-supervisor" }
        } catch { }
        
        # Crear certificado autofirmado — RSA 2048 explicito (requisito IEC 62443 / Alstom TLS)
        # IMPORTANTE: IP addresses deben ir como "IPAddress=" en SAN, no como "DNS Name="
        # Chrome/Edge requieren SAN tipo IP Address para acceso por IP sin warnings
        $sanBuilder = "2.5.29.17={text}"
        $sanBuilder += "DNS=localhost&"
        $sanBuilder += "DNS=$ipcHostname&"
        $sanBuilder += "DNS=aquafrisch-supervisor&"
        $sanBuilder += "IPAddress=$TargetIP&"
        $sanBuilder += "IPAddress=127.0.0.1"
        
        $cert = New-SelfSignedCertificate `
            -Subject "CN=Aquafrisch Supervisor ($TargetIP)" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(10) `
            -FriendlyName "Aquafrisch Supervisor SSL ($TargetIP)" `
            -KeyLength 2048 `
            -KeyAlgorithm RSA `
            -HashAlgorithm SHA256 `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1", $sanBuilder)
        
        # Exportar PFX (clave privada + publica — solo para Kestrel)
        $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $localCertPath -Password $securePassword | Out-Null
        
        # Exportar CER (solo clave publica — para distribuir a clientes)
        Export-Certificate -Cert $cert -FilePath $localCerPath -Type CERT | Out-Null
        
        # Copiar PFX y CER al remoto via SMB
        Copy-Item -Path $localCertPath -Destination $certRemoteDest -Force
        Copy-Item -Path $localCerPath -Destination $cerRemoteDest -Force
        
        # Instalar certificado en Trusted Root CA del IPC (via WinRM si disponible)
        Write-Step "Instalando certificado en Trusted Root CA del IPC..."
        try {
            $certBytes = [System.IO.File]::ReadAllBytes($localCerPath)
            $certBase64 = [Convert]::ToBase64String($certBytes)
            
            Invoke-Command -ComputerName $TargetIP -Credential $cred -ScriptBlock {
                param($b64)
                $bytes = [Convert]::FromBase64String($b64)
                $tempCer = "$env:TEMP\aquafrisch-supervisor.cer"
                [System.IO.File]::WriteAllBytes($tempCer, $bytes)
                Import-Certificate -FilePath $tempCer -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
                Remove-Item $tempCer -Force -ErrorAction SilentlyContinue
            } -ArgumentList $certBase64 -ErrorAction Stop
            
            Write-Success "Certificado instalado en Trusted Root CA del IPC"
        } catch {
            Write-Warning "No se pudo instalar en Trusted Root via WinRM: $_"
            Write-Info "El certificado CER esta disponible en: $InstallPath\Backend\certificate.cer"
            Write-Info "Instalar manualmente en el IPC con:"
            Write-Info "  Import-Certificate -FilePath 'certificate.cer' -CertStoreLocation 'Cert:\LocalMachine\Root'"
        }
        
        # Limpiar archivos temporales locales
        Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue
        Remove-Item -Path $localCertPath -Force -ErrorAction SilentlyContinue
        
        Write-Success "Certificado SSL generado y copiado al servidor"
        Write-Info "Algoritmo: RSA 2048 / SHA256 (conforme IEC 62443 / Alstom TLS)"
        Write-Info "SANs: DNS=localhost, DNS=$ipcHostname, IPAddress=$TargetIP, IPAddress=127.0.0.1"
        Write-Info "El certificado es valido por 10 años"
        Write-Info "PFX (servidor): $InstallPath\Backend\certificate.pfx"
        Write-Info "CER (clientes): $InstallPath\Backend\certificate.cer"
        Write-Info "  Los PCs cliente pueden descargar el CER desde: https://$TargetIP`:5001/api/certificate/public"
        
        # Instalar certificado en el PC LOCAL (desde donde se despliega)
        Write-Step "Instalando certificado en Trusted Root del PC local..."
        try {
            Import-Certificate -FilePath $localCerPath -CertStoreLocation "Cert:\LocalMachine\Root" -ErrorAction Stop | Out-Null
            Write-Success "Certificado instalado en Trusted Root del PC local"
            Write-Info "Este PC podra acceder a https://$TargetIP`:5001 sin advertencias"
        } catch {
            Write-Warning "No se pudo instalar en Trusted Root local (requiere admin): $_"
            Write-Info "Instalar manualmente: doble click en certificate.cer -> Entidades de certificacion raiz de confianza"
        }
        
        Remove-Item -Path $localCerPath -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Error2 "No se pudo generar certificado: $_"
        Write-Info "IMPORTANTE: Debes generar el certificado manualmente en el servidor:"
        Write-Info '  $san = "2.5.29.17={text}DNS=localhost&DNS=aquafrisch-supervisor&IPAddress=192.168.2.161&IPAddress=127.0.0.1"'
        Write-Info '  $cert = New-SelfSignedCertificate -Subject "CN=Aquafrisch Supervisor (192.168.2.161)" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10) -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256 -KeyUsage DigitalSignature,KeyEncipherment -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1", $san)'
        Write-Info '  $pwd = ConvertTo-SecureString -String "Aquafrisch2024!" -Force -AsPlainText'
        Write-Info '  Export-PfxCertificate -Cert $cert -FilePath "C:\Aquafrisch Supervisor\Backend\certificate.pfx" -Password $pwd'
        Write-Info '  Export-Certificate -Cert $cert -FilePath "C:\Aquafrisch Supervisor\Backend\certificate.cer" -Type CERT'
        Write-Info '  Import-Certificate -FilePath "C:\Aquafrisch Supervisor\Backend\certificate.cer" -CertStoreLocation "Cert:\LocalMachine\Root"'
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
# CRITICO: failureflag=1 → recovery se activa TAMBIÉN con exit code 0 (salida limpia)
# sc.exe failureflag NO funciona remotamente (\\IP), hay que ejecutarlo local via WinRM
try {
    $flagResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        param($SvcName)
        $r = & sc.exe failureflag $SvcName 1 2>&1
        return "OK:$r"
    } -ArgumentList $serviceName -ErrorAction Stop
    Write-Success "Recovery + failureflag configurado (reinicio automatico siempre)"
} catch {
    Write-Warning "failureflag via WinRM fallo: $($_.Exception.Message)"
    Write-Warning "Ejecutar manualmente en el IPC: sc.exe failureflag $serviceName 1"
}

# --- Pre-arranque: Matar zombies y verificar puertos libres ---
Write-Step "Verificando puertos libres antes de arrancar..."
try {
    $preStartCheck = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
        $output = @()
        
        # Matar cualquier proceso zombie de SW.PC.API.Backend
        $zombies = Get-Process -Name "SW.PC.API.Backend" -ErrorAction SilentlyContinue
        if ($zombies) {
            $zombies | Stop-Process -Force
            $output += "ZOMBIE:Procesos zombie eliminados (PID: $($zombies.Id -join ', '))"
            Start-Sleep -Seconds 3
        }
        
        # Verificar puertos 5000 y 5001
        $maxWait = 10
        $attempt = 0
        do {
            $busy = $false
            foreach ($port in @(5000, 5001)) {
                $conn = netstat -ano | Select-String ":$port\s" | Select-String "LISTENING"
                if ($conn) {
                    $busy = $true
                    # Intentar matar el proceso que ocupa el puerto
                    if ($conn -match '\s+(\d+)\s*$') {
                        $pid = [int]$Matches[1]
                        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                        $output += "KILL:Puerto $port ocupado - matando PID $pid"
                    }
                }
            }
            if ($busy) {
                $attempt++
                Start-Sleep -Seconds 2
            }
        } while ($busy -and $attempt -lt $maxWait)
        
        if ($busy) {
            $output += "ERROR:Puertos siguen ocupados tras $maxWait intentos"
        } else {
            $output += "OK:Puertos 5000/5001 libres"
        }
        
        return $output
    } -ErrorAction Stop
    
    $portsOk = $true
    foreach ($line in $preStartCheck) {
        if ($line -match '^ZOMBIE:(.+)$') {
            Write-Warning $Matches[1]
        } elseif ($line -match '^KILL:(.+)$') {
            Write-Warning $Matches[1]
        } elseif ($line -match '^ERROR:(.+)$') {
            Write-Error2 $Matches[1]
            $portsOk = $false
        } elseif ($line -match '^OK:(.+)$') {
            Write-Success $Matches[1]
        }
    }
    
    if (-not $portsOk) {
        Write-Error2 "No se puede arrancar: puertos ocupados. Revisa manualmente en el servidor."
        Write-Host "  netstat -ano | findstr :5000" -ForegroundColor Yellow
        Write-Host "  netstat -ano | findstr :5001" -ForegroundColor Yellow
    }
} catch {
    Write-Info "No se pudo verificar puertos via WinRM, continuando..."
}

# Arrancar el servicio
Write-Step "Arrancando servicio..."
$startResult = sc.exe \\$TargetIP start $serviceName 2>&1
if ($startResult -match "START_PENDING|RUNNING") {
    Start-Sleep -Seconds 5
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

# --- Post-arranque: Health check via HTTP ---
Write-Step "Verificando que la API responde (health check)..."
$healthOk = $false
for ($hcAttempt = 1; $hcAttempt -le 5; $hcAttempt++) {
    Start-Sleep -Seconds 3
    try {
        $hcResult = Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
            try {
                $response = Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/projects/active" -UseBasicParsing -TimeoutSec 5
                return "OK:$($response.Content)"
            } catch {
                return "FAIL:$($_.Exception.Message)"
            }
        } -ErrorAction Stop
        
        if ($hcResult -match '^OK:(.+)$') {
            $apiResponse = $Matches[1] | ConvertFrom-Json
            Write-Success "API respondiendo - Proyecto: $($apiResponse.projectId) (modo: $($apiResponse.environmentMode))"
            if ($apiResponse.projectId -eq "default" -and $ProjectId -ne "default") {
                Write-Warning "El proyecto activo es 'default' pero se esperaba '$ProjectId'"
                Write-Warning "Verifica active-project.json y la carpeta Projects\$ProjectId"
            }
            $healthOk = $true
            break
        } else {
            Write-Info "Health check intento $hcAttempt/5: API no responde aun..."
        }
    } catch {
        Write-Info "Health check intento $hcAttempt/5: WinRM no disponible"
    }
}
if (-not $healthOk) {
    Write-Warning "La API no respondio tras 5 intentos. Verifica manualmente:"
    Write-Host "  Invoke-WebRequest http://127.0.0.1:5000/api/projects/active" -ForegroundColor Yellow
}

# --- Relanzar navegador kiosk (Edge fue matado durante el deploy) ---
if ($healthOk) {
    Write-Step "Relanzando navegador kiosk..."
    try {
        Invoke-Command -ComputerName $TargetIP -Credential $Credential -ScriptBlock {
            # Matar Edge residual (puede estar mostrando pagina de error)
            Get-Process -Name "msedge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2

            # Buscar Edge
            $edgePath = @(
                "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
                "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
            ) | Where-Object { Test-Path $_ } | Select-Object -First 1

            if ($edgePath) {
                $kioskUrl = "https://127.0.0.1:5001"
                $args = @('--kiosk', $kioskUrl, '--no-first-run', '--disable-session-crashed-bubble',
                          '--noerrdialogs', '--disable-infobars', '--edge-kiosk-type=fullscreen',
                          '--disable-features=msEdgeSidebarButton')
                Start-Process -FilePath $edgePath -ArgumentList $args
                return "OK:Edge kiosk relanzado"
            } else {
                return "WARN:Edge no encontrado"
            }
        } -ErrorAction Stop | ForEach-Object {
            if ($_ -match '^OK:(.+)$') { Write-Success $Matches[1] }
            elseif ($_ -match '^WARN:(.+)$') { Write-Warning $Matches[1] }
        }
    } catch {
        Write-Info "No se pudo relanzar Edge via WinRM — el watchdog lo relanzara automaticamente"
    }
}

# ============================================
# PASO 10.5: Configurar Firewall
# ============================================
Write-Header "PASO 10.5: Configurando Firewall"

Write-Info "Configurando regla de firewall para HTTPS:5001..."
Write-Info "HTTP:5000 es solo localhost - no requiere regla de firewall"

try {
    # Usar netsh remoto (no requiere WinRM/TrustedHosts)
    netsh -r $TargetIP -u $TargetUser -p $TargetPassword advfirewall firewall delete rule name="Aquafrisch Supervisor HTTPS" 2>$null | Out-Null
    netsh -r $TargetIP -u $TargetUser -p $TargetPassword advfirewall firewall delete rule name="Aquafrisch Supervisor HTTP" 2>$null | Out-Null
    
    $netshResult = netsh -r $TargetIP -u $TargetUser -p $TargetPassword advfirewall firewall add rule name="Aquafrisch Supervisor HTTPS" dir=in action=allow protocol=TCP localport=5001 profile=any 2>&1
    
    if ($netshResult -match "Correcto|Ok") {
        Write-Success "Regla de firewall configurada: solo HTTPS:5001 (HTTP:5000 = localhost-only)"
    } else {
        Write-Warning "netsh resultado: $netshResult"
        Write-Info "Verificar firewall manualmente en el servidor"
    }
}
catch {
    Write-Warning "No se pudieron configurar las reglas de firewall automaticamente."
    Write-Info "Ejecuta manualmente en el PC destino (como Admin):"
    Write-Host "  netsh advfirewall firewall add rule name=`"Aquafrisch Supervisor HTTPS`" dir=in action=allow protocol=TCP localport=5001" -ForegroundColor Yellow
}

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
Write-Host "  - HTTPS: https://${TargetIP}:5001 (UNICO ACCESO REMOTO)" -ForegroundColor Green
Write-Host "  - HTTP:  http://localhost:5000 (solo emergencia desde el propio IPC)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  NOTA: El servidor esta corriendo como servicio Windows." -ForegroundColor Yellow
Write-Host "        Se inicia automaticamente con el PC y se reinicia en caso de fallo." -ForegroundColor Yellow
Write-Host ""
Write-Host "  NOTA: El certificado SSL se instala automaticamente en el PC de despliegue." -ForegroundColor Yellow
Write-Host "        Otros PCs pueden descargar el CER desde: https://${TargetIP}:5001/api/certificate/public" -ForegroundColor Yellow
Write-Host "        HTTP:5000 solo accesible desde el propio IPC (127.0.0.1)" -ForegroundColor Yellow
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan

# Mantener ventana abierta
Write-Host ""
Read-Host "Presiona Enter para cerrar"
