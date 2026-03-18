#Requires -Version 5.1
<#
.SYNOPSIS
    Deploy al Servidor de Empresa (Modo Development)
    Para que los ingenieros puedan configurar proyectos.

.DESCRIPTION
    Este script:
    1. Compila Backend y Frontend (codigo)
    2. Para el servidor si esta corriendo (servicio o proceso)
    3. Copia SOLO codigo (backend + frontend)
    4. NO TOCA los proyectos (Excel, modelos 3D, bases de datos)
    5. Registra y arranca como Servicio de Windows (auto-start)

    IMPORTANTE: Los proyectos en Projects/ NO se tocan.
    Los ingenieros gestionan sus propios Excel, modelos y backups.

.EXAMPLE
    .\Deploy-Servidor-Empresa.ps1
    .\Deploy-Servidor-Empresa.ps1 -SkipBuild
#>

param(
    [string]$TargetIP = "192.168.2.199",
    [string]$TargetUser = "Administrator",
    [string]$TargetPassword = 'Aqua2023',
    [string]$InstallPath = "C:\Aquafrisch Supervisor",
    [switch]$SkipBackendBuild,
    [switch]$SkipFrontendBuild,
    [switch]$SkipBuild  # Salta ambos builds
)

# ============================================
# CONFIGURACION
# ============================================
$ErrorActionPreference = "Continue"
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendPath = $ScriptPath
$FrontendPath = Join-Path (Split-Path -Parent $BackendPath) "SW.PC.REACT.Frontend\my-3d-app"
$ProjectsPath = Join-Path $BackendPath "Projects"

if ($SkipBuild) {
    $SkipBackendBuild = $true
    $SkipFrontendBuild = $true
}

# Colores
function Write-Header { param($text) Write-Host "`n$("="*60)" -ForegroundColor Cyan; Write-Host " $text" -ForegroundColor Cyan; Write-Host "$("="*60)" -ForegroundColor Cyan }
function Write-Step { param($text) Write-Host "[>] $text" -ForegroundColor Yellow }
function Write-Success { param($text) Write-Host "[OK] $text" -ForegroundColor Green }
function Write-Info { param($text) Write-Host "[i] $text" -ForegroundColor Gray }
function Write-Error2 { param($text) Write-Host "[X] $text" -ForegroundColor Red }

# ============================================
# INICIO
# ============================================
Clear-Host
Write-Header "DEPLOY SERVIDOR EMPRESA (DEVELOPMENT)"
Write-Host ""
Write-Info "Modo: DEVELOPMENT (selector de proyectos HABILITADO)"
Write-Info "Destino: $TargetIP"
Write-Info "Ruta: $InstallPath"
Write-Host ""
Write-Host "  IMPORTANTE: Este script NO toca los proyectos" -ForegroundColor Yellow
Write-Host "  (Excel, modelos 3D, bases de datos se mantienen)" -ForegroundColor Yellow
Write-Host ""

# ============================================
# PASO 1: Compilar Backend
# ============================================
Write-Header "PASO 1: Compilando Backend"

if ($SkipBackendBuild) {
    Write-Info "Saltando build del backend (-SkipBackendBuild)"
} else {
    Write-Step "Limpiando carpetas anteriores..."
    Remove-Item -Recurse -Force "$BackendPath\publish" -ErrorAction SilentlyContinue
    
    Write-Step "dotnet publish -c Release (self-contained)..."
    Push-Location $BackendPath
    try {
        $publishOutput = & dotnet publish -c Release -o "$BackendPath\publish" --self-contained true -r win-x64 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error2 "Error compilando backend:"
            Write-Host $publishOutput -ForegroundColor Red
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        Write-Success "Backend compilado"
    } finally {
        Pop-Location
    }
}

# ============================================
# PASO 2: Compilar Frontend
# ============================================
Write-Header "PASO 2: Compilando Frontend"

if ($SkipFrontendBuild) {
    Write-Info "Saltando build del frontend (-SkipFrontendBuild)"
} else {
    Write-Step "npm run build..."
    Push-Location $FrontendPath
    try {
        $env:CI = "false"
        $ErrorActionPreference = "SilentlyContinue"
        cmd /c "npm run build 2>&1"
        $ErrorActionPreference = "Continue"
        
        $buildFolder = Join-Path $FrontendPath "build"
        if (-not (Test-Path "$buildFolder\index.html")) {
            Write-Error2 "Error: No se genero build/index.html"
            Read-Host "Presiona Enter para cerrar"
            exit 1
        }
        Write-Success "Frontend compilado"
    } finally {
        Pop-Location
    }
}

# ============================================
# PASO 2.1: Generar deploy-version.json (Software Integrity)
# ============================================
Write-Header "PASO 2.1: Generando deploy-version.json"

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
        $gpgFormat = (git config --global gpg.format 2>$null) -replace "`n|`r", ""
        $signingKey = (git config --global user.signingkey 2>$null) -replace "`n|`r", ""
        if ($gpgFormat -eq "ssh" -and $signingKey) {
            $sshDir = Split-Path $signingKey -Parent
            $allowedSignersPath = Join-Path $sshDir "allowed_signers"
            if (-not (Test-Path $allowedSignersPath)) {
                if (Test-Path $signingKey) {
                    $pubKey = (Get-Content $signingKey -Raw).Trim()
                    $gitEmail = (git config --global user.email 2>$null) -replace "`n|`r", ""
                    if (-not $gitEmail) { $gitEmail = "electronico@aquafrisch.com" }
                    Set-Content -Path $allowedSignersPath -Value "$gitEmail namespaces=`"git`" $pubKey" -Encoding UTF8
                    Write-Host "   🔐 Created allowed_signers: $allowedSignersPath" -ForegroundColor Cyan
                }
            }
            $currentAllowed = (git config --global gpg.ssh.allowedSignersFile 2>$null) -replace "`n|`r", ""
            if (-not $currentAllowed -or -not (Test-Path $currentAllowed)) {
                git config --global gpg.ssh.allowedSignersFile $allowedSignersPath 2>$null
                Write-Host "   🔐 Configured allowedSignersFile: $allowedSignersPath" -ForegroundColor Cyan
            }
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

Write-Step "Obteniendo version del Backend..."
$backendVersionInfo = Get-GitVersionInfo -RepoPath $BackendPath -ComponentName "Backend"
if ($backendVersionInfo) {
    Write-Success "Backend: v$($backendVersionInfo.Version) ($($backendVersionInfo.CommitSha)) - $($backendVersionInfo.SignatureStatus)"
} else {
    Write-Warning "No se pudo obtener version del Backend"
}

Write-Step "Obteniendo version del Frontend..."
$frontendVersionInfo = Get-GitVersionInfo -RepoPath $FrontendPath -ComponentName "Frontend"
if ($frontendVersionInfo) {
    Write-Success "Frontend: v$($frontendVersionInfo.Version) ($($frontendVersionInfo.CommitSha)) - $($frontendVersionInfo.SignatureStatus)"
} else {
    Write-Warning "No se pudo obtener version del Frontend"
}

# ============================================
# PASO 3: Conectar al servidor
# ============================================
Write-Header "PASO 3: Conectando al servidor"

$secPassword = ConvertTo-SecureString $TargetPassword -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($TargetUser, $secPassword)

$driveLetter = "Z"
$remotePath = "\\$TargetIP\C$"

Write-Step "Conectando a $remotePath..."
try {
    Write-Info "Desconectando TODAS las conexiones previas a $TargetIP..."
    # Desconectar todas las conexiones al servidor (evita error 1219)
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
    net use "\\$TargetIP\C`$" /delete /y 2>$null | Out-Null
    net use "\\$TargetIP\IPC`$" /delete /y 2>$null | Out-Null
    
    if (Test-Path "${driveLetter}:") {
        net use "${driveLetter}:" /delete /y 2>$null | Out-Null
    }
    
    Start-Sleep -Seconds 2
    
    $netResult = net use "${driveLetter}:" $remotePath /user:$TargetUser $TargetPassword 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Error conectando: $netResult"
    }
    Write-Success "Conectado a $remotePath como ${driveLetter}:"
} catch {
    Write-Error2 "No se pudo conectar al servidor: $_"
    Write-Host ""
    Write-Host "  Posibles causas:" -ForegroundColor Yellow
    Write-Host "  - Tienes una carpeta de red abierta a $TargetIP (cierrala)" -ForegroundColor White
    Write-Host "  - Usuario/contrasena incorrectos" -ForegroundColor White
    Write-Host "  - El servidor no esta accesible" -ForegroundColor White
    Write-Host ""
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$remoteInstallPath = "${driveLetter}:\Aquafrisch Supervisor"
$remoteBackendPath = "$remoteInstallPath\Backend"

# ============================================
# PASO 4: Parar el servidor si esta corriendo
# ============================================
Write-Header "PASO 4: Parando servidor remoto"

$serviceName = "AquafrischSupervisor"

# Metodo 1: Parar servicio Windows via sc.exe remoto (usa conexion SMB)
Write-Step "Parando servicio '$serviceName' via sc.exe remoto..."
$scQuery = sc.exe \\$TargetIP query $serviceName 2>&1
if ($scQuery -match "RUNNING") {
    sc.exe \\$TargetIP stop $serviceName | Out-Null
    Write-Success "Servicio '$serviceName' detenido via sc.exe"
    Start-Sleep -Seconds 2
    # Siempre forzar taskkill despues de sc.exe stop para asegurar que el proceso muere
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
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
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
    if ($taskkillResult -match "correctamente|SUCCESS") {
        Write-Success "Proceso zombie eliminado con taskkill"
        Start-Sleep -Seconds 3
    }
} else {
    Write-Info "Servicio no instalado todavia (primera instalacion)"
    # Fallback: taskkill en caso de que este corriendo como consola (modo legacy)
    $taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
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
$testFile = "${driveLetter}:\Aquafrisch Supervisor\Backend\hostfxr.dll"
if (-not (Test-Path $testFile)) {
    $testFile = "${driveLetter}:\Aquafrisch Supervisor\Backend\SW.PC.API.Backend.dll"
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
                Write-Host "  1. Abre el Administrador de Tareas en el servidor" -ForegroundColor White
                Write-Host "  2. Busca SW.PC.API.Backend" -ForegroundColor White
                Write-Host "  3. Haz clic derecho - Finalizar tarea" -ForegroundColor White
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

$folders = @(
    $remoteInstallPath,
    $remoteBackendPath,
    "$remoteBackendPath\Projects",
    "$remoteBackendPath\wwwroot",
    "$remoteInstallPath\SW.PC.Twincat_3"      # Carpeta para repos TwinCAT PLC (tecnicos clonan aqui)
)

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Success "Creado: $folder"
    } else {
        Write-Info "Ya existe: $folder"
    }
}

# Permisos de TwinCAT: el servicio corre como LocalSystem, tecnicos clonan como Administrator
$twinCatRemotePath = "$remoteInstallPath\SW.PC.Twincat_3"
if (Test-Path $twinCatRemotePath) {
    # Convertir ruta SMB a ruta local para icacls remoto
    $twinCatLocalPath = "C:\Aquafrisch Supervisor\SW.PC.Twincat_3"
    $icaclsResult = icacls.exe $twinCatRemotePath /grant Everyone:"(OI)(CI)F" /T /Q 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Permisos TwinCAT configurados (Everyone: Full Control)"
    } else {
        Write-Info "No se pudieron configurar permisos TwinCAT (configurar manualmente)"
    }
}

# ============================================
# PASO 6: Copiar Backend (SOLO CODIGO)
# ============================================
Write-Header "PASO 6: Copiando Backend (solo codigo)"

$publishPath = "$BackendPath\publish"
if (Test-Path $publishPath) {
    Write-Step "Copiando ejecutables y DLLs..."
    
    $failedFiles = @()
    Get-ChildItem -Path $publishPath -File | ForEach-Object {
        $destFile = Join-Path $remoteBackendPath $_.Name
        
        if ($_.Name -like "appsettings*.json" -and (Test-Path $destFile)) {
            Write-Info "  Manteniendo: $($_.Name) (ya existe)"
        } elseif ($_.Name -in @("authorized_signing_keys.json", "access_control_config.json") -and (Test-Path $destFile)) {
            Write-Info "  Manteniendo: $($_.Name) (configuración local)"
        } else {
            $copied = $false
            for ($attempt = 1; $attempt -le 3; $attempt++) {
                try {
                    Copy-Item $_.FullName $destFile -Force -ErrorAction Stop
                    $copied = $true
                    break
                } catch {
                    if ($attempt -lt 3) {
                        Start-Sleep -Seconds 2
                    }
                }
            }
            if (-not $copied) {
                $failedFiles += $_.Name
            }
        }
    }
    
    if ($failedFiles.Count -gt 0) {
        Write-Error2 "No se pudieron copiar $($failedFiles.Count) archivos:"
        $failedFiles | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        Write-Host ""
        Write-Host "  SOLUCION: Mata el proceso manualmente en $TargetIP y reintenta" -ForegroundColor Yellow
        Read-Host "Presiona Enter para cerrar"
        exit 1
    }
    
    Write-Success "Backend copiado ($((Get-ChildItem -Path $publishPath -File).Count) archivos)"
} else {
    Write-Error2 "No se encontro carpeta publish. Ejecuta sin -SkipBackendBuild"
    exit 1
}

# ============================================
# PASO 6.1: Crear docs/ global si no existe (fuente AQSdocs_master)
# ============================================
Write-Header "PASO 6.1: Verificando docs/ global (fuente AQSdocs_master)"

# IMPORTANTE: La carpeta docs/ en el servidor la gestiona SOLO el DMS Enterprise.
# Este script NUNCA sobreescribe ni modifica su contenido.
# Solo crea la carpeta si no existe (primera instalacion).

$docsDest = Join-Path $remoteBackendPath "docs"

if (Test-Path $docsDest) {
    $mdCount = (Get-ChildItem -Path $docsDest -Filter "*.md" -Recurse -ErrorAction SilentlyContinue).Count
    Write-Success "docs/ ya existe ($mdCount archivos .md) - NO SE TOCA"
    Write-Info "  Gestionado por DMS Enterprise"
} else {
    New-Item -ItemType Directory -Path $docsDest -Force | Out-Null
    Write-Success "docs/ creado (vacio, DMS Enterprise escribira el contenido)"
    Write-Info "  Ruta: $docsDest"
    Write-Info "  SyncMaster distribuira estos docs a cada proyecto"
}

# ============================================
# PASO 6.2: Sincronizar authorized_signing_keys.json (SSH cross-server)
# ============================================
Write-Header "PASO 6.2: Sincronizando claves SSH autorizadas"

$localAuthKeys = Join-Path $BackendPath "authorized_signing_keys.json"
$remoteAuthKeys = Join-Path $remoteBackendPath "authorized_signing_keys.json"

if (Test-Path $localAuthKeys) {
    try {
        $localKeys = Get-Content $localAuthKeys -Raw | ConvertFrom-Json
        $validLocalKeys = @($localKeys | Where-Object { $_.Fingerprint -and $_.PublicKey })
        
        if (Test-Path $remoteAuthKeys) {
            # Merge: agregar claves locales que no existan en remoto
            $remoteKeys = Get-Content $remoteAuthKeys -Raw | ConvertFrom-Json
            # Limpiar entradas inválidas (fingerprint vacío)
            $validRemoteKeys = @($remoteKeys | Where-Object { $_.Fingerprint })
            $cleaned = $remoteKeys.Count - $validRemoteKeys.Count
            if ($cleaned -gt 0) {
                Write-Info "  Limpiadas $cleaned entradas invalidas del servidor"
            }
            
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
                Write-Success "authorized_signing_keys.json actualizado (+$added claves, -$cleaned invalidas)"
            } else {
                Write-Success "authorized_signing_keys.json ya sincronizado ($($validRemoteKeys.Count) claves)"
            }
        } else {
            # No existe en destino: copiar completo (solo claves válidas)
            if ($validLocalKeys.Count -gt 0) {
                $validLocalKeys | ConvertTo-Json -Depth 10 | Set-Content $remoteAuthKeys -Encoding UTF8
                Write-Success "authorized_signing_keys.json desplegado ($($validLocalKeys.Count) claves)"
            } else {
                Write-Info "  Sin claves validas para desplegar"
            }
        }
    } catch {
        Write-Warning "No se pudo sincronizar authorized_signing_keys.json: $_"
    }
} else {
    Write-Info "  Sin authorized_signing_keys.json local"
}

# ============================================
# PASO 7: Copiar Frontend
# ============================================
Write-Header "PASO 7: Copiando Frontend"

$frontendBuildPath = "$FrontendPath\build"
if (Test-Path $frontendBuildPath) {
    Write-Step "Copiando React build a wwwroot..."
    
    $wwwrootPath = "$remoteBackendPath\wwwroot"
    if (Test-Path $wwwrootPath) {
        Get-ChildItem -Path $wwwrootPath -Exclude "uploads","data" | Remove-Item -Recurse -Force
    }
    
    Copy-Item -Path "$frontendBuildPath\*" -Destination $wwwrootPath -Recurse -Force
    
    Write-Success "Frontend copiado"
} else {
    Write-Error2 "No se encontro carpeta build del frontend. Ejecuta sin -SkipFrontendBuild"
    exit 1
}

# ============================================
# PASO 8: NO TOCAR PROYECTOS
# ============================================
Write-Header "PASO 8: Verificando proyectos (NO SE TOCAN)"

$remoteProjectsPath = "$remoteBackendPath\Projects"

if (Test-Path $remoteProjectsPath) {
    $serverProjects = Get-ChildItem -Path $remoteProjectsPath -Directory -ErrorAction SilentlyContinue | 
                      Where-Object { $_.Name -ne "_template" }
    
    if ($serverProjects.Count -gt 0) {
        Write-Success "Proyectos en el servidor (NO modificados):"
        foreach ($proj in $serverProjects) {
            Write-Info "  - $($proj.Name)"
        }
    } else {
        Write-Info "No hay proyectos en el servidor todavia"
        Write-Info "Los ingenieros deben crear proyectos manualmente en:"
        Write-Info "  $remoteProjectsPath"
    }
} else {
    Write-Info "Carpeta Projects/ creada. Los ingenieros anadiran proyectos."
}

# Copiar _template si no existe
$templateSource = Join-Path $ProjectsPath "_template"
$templateDest = Join-Path $remoteProjectsPath "_template"
if ((Test-Path $templateSource) -and (-not (Test-Path $templateDest))) {
    Copy-Item -Path $templateSource -Destination $templateDest -Recurse -Force
    Write-Info "_template copiado (para crear nuevos proyectos)"
}

# ============================================
# PASO 8.1: Generar deploy-version.json en raiz de Backend
# ============================================
Write-Header "PASO 8.1: Generando deploy-version.json"

if ($backendVersionInfo -or $frontendVersionInfo) {
    $deployVersionProject = @{
        DeployedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        DeployedFrom = $env:COMPUTERNAME
        DeployedBy = $env:USERNAME
        Backend = $backendVersionInfo
        Frontend = $frontendVersionInfo
    }
    
    $deployVersionJson = $deployVersionProject | ConvertTo-Json -Depth 10
    # Guardar deploy-version.json en raiz de Backend (NO dentro de Projects/)
    # Asi copiar carpetas de proyecto nunca afecta la version del servidor
    $deployVersionPath = "$remoteBackendPath\deploy-version.json"
    Set-Content -Path $deployVersionPath -Value $deployVersionJson -Encoding UTF8
    Write-Success "deploy-version.json guardado en Backend/ (raiz)"
    Write-Info "Backend: v$($backendVersionInfo.Version) ($($backendVersionInfo.CommitSha))"
    Write-Info "Frontend: v$($frontendVersionInfo.Version) ($($frontendVersionInfo.CommitSha))"
} else {
    Write-Warning "No hay info de version - deploy-version.json no generado"
}

# ============================================
# PASO 8.2: Generar deploy-version.json para TwinCAT (si existe repo en servidor)
# ============================================
Write-Header "PASO 8.2: Verificando TwinCAT en servidor remoto"

$twinCatRemoteBase = "$remoteInstallPath\SW.PC.Twincat_3"
$twinCatVersionGenerated = $false
if (Test-Path $twinCatRemoteBase) {
    # Buscar subfolders con .git
    $twinCatRepos = Get-ChildItem -Path $twinCatRemoteBase -Directory -ErrorAction SilentlyContinue | 
        Where-Object { Test-Path (Join-Path $_.FullName ".git") }
    
    foreach ($tcRepo in $twinCatRepos) {
        Write-Info "  Encontrado repo TwinCAT: $($tcRepo.Name)"
        
        # Leer HEAD commit SHA directamente de .git (sin ejecutar git)
        $headFile = Join-Path $tcRepo.FullName ".git\HEAD"
        $tcSha = ""
        $tcBranch = ""
        try {
            $headContent = (Get-Content $headFile -Raw).Trim()
            if ($headContent -match "^ref: (.+)$") {
                $refPath = $Matches[1]
                $tcBranch = ($refPath -split "/")[-1]
                $refFile = Join-Path $tcRepo.FullName ".git\$refPath"
                if (Test-Path $refFile) {
                    $tcSha = (Get-Content $refFile -Raw).Trim()
                }
                # Si no existe el archivo ref, buscar en packed-refs
                if (-not $tcSha) {
                    $packedRefs = Join-Path $tcRepo.FullName ".git\packed-refs"
                    if (Test-Path $packedRefs) {
                        $line = Get-Content $packedRefs | Where-Object { $_ -match $refPath } | Select-Object -First 1
                        if ($line -match "^([0-9a-f]+)\s") { $tcSha = $Matches[1] }
                    }
                }
            } else {
                $tcSha = $headContent  # detached HEAD
            }
        } catch { }
        
        if ($tcSha) {
            $tcShaShort = $tcSha.Substring(0, [Math]::Min(7, $tcSha.Length))
            
            # Crear deploy-version.json dentro del repo TwinCAT
            $tcDeployVersion = @{
                ComponentName = "TwinCAT PLC"
                Version = "0.0.0"
                CommitSha = $tcShaShort
                CommitShaFull = $tcSha
                Branch = if ($tcBranch) { $tcBranch } else { "unknown" }
                IsSigned = $false
                SignatureStatus = "PENDING"
                DeployedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
                DeployedFrom = $env:COMPUTERNAME
                DeployedBy = $env:USERNAME
                Note = "Generated by deploy script (read from .git files). Service will update with full git info on startup."
            }
            $tcDeployJson = $tcDeployVersion | ConvertTo-Json -Depth 5
            Set-Content -Path (Join-Path $tcRepo.FullName "deploy-version.json") -Value $tcDeployJson -Encoding UTF8
            Write-Success "TwinCAT ($($tcRepo.Name)): SHA $tcShaShort, branch $tcBranch"
            $twinCatVersionGenerated = $true
        } else {
            Write-Info "  No se pudo leer SHA de $($tcRepo.Name)"
        }
    }
    
    if (-not $twinCatVersionGenerated) {
        Write-Info "  No se encontraron repos TwinCAT con .git"
    }
} else {
    Write-Info "  Carpeta SW.PC.Twincat_3 no existe en servidor"
}

# ============================================
# PASO 8.3: Generar certificado SSL autofirmado
# ============================================
Write-Header "PASO 8.3: Generando certificado SSL"

$certPassword = "Aquafrisch2024!"
$certRemoteDest = "$remoteBackendPath\certificate.pfx"
$cerRemoteDest = "$remoteBackendPath\certificate.cer"

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
        
        # Obtener hostname del servidor
        $serverHostname = "aquafrisch-supervisor"
        try {
            $serverHostname = (Get-WmiObject -ComputerName $TargetIP -Class Win32_ComputerSystem -Credential $credential -ErrorAction SilentlyContinue).Name
            if (-not $serverHostname) { $serverHostname = "aquafrisch-supervisor" }
        } catch { }
        
        # Crear certificado autofirmado — RSA 2048 (IEC 62443 / Alstom TLS)
        # IP addresses como "IPAddress=" en SAN (Chrome/Edge requieren IP Address type)
        $sanBuilder = "2.5.29.17={text}"
        $sanBuilder += "DNS=localhost&"
        $sanBuilder += "DNS=$serverHostname&"
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
        
        # Copiar PFX y CER al servidor via SMB
        Copy-Item -Path $localCertPath -Destination $certRemoteDest -Force
        Copy-Item -Path $localCerPath -Destination $cerRemoteDest -Force
        
        # Instalar certificado en Trusted Root CA del servidor (via WinRM)
        Write-Step "Instalando certificado en Trusted Root CA del servidor..."
        try {
            $certBytes = [System.IO.File]::ReadAllBytes($localCerPath)
            $certBase64 = [Convert]::ToBase64String($certBytes)
            
            Invoke-Command -ComputerName $TargetIP -Credential $credential -ScriptBlock {
                param($b64)
                $bytes = [Convert]::FromBase64String($b64)
                $tempCer = "$env:TEMP\aquafrisch-supervisor.cer"
                [System.IO.File]::WriteAllBytes($tempCer, $bytes)
                Import-Certificate -FilePath $tempCer -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
                Remove-Item $tempCer -Force -ErrorAction SilentlyContinue
            } -ArgumentList $certBase64 -ErrorAction Stop
            
            Write-Success "Certificado instalado en Trusted Root CA del servidor"
        } catch {
            Write-Warning "No se pudo instalar en Trusted Root via WinRM: $_"
            Write-Info "Instalar manualmente en el servidor:"
            Write-Info "  Import-Certificate -FilePath 'certificate.cer' -CertStoreLocation 'Cert:\LocalMachine\Root'"
        }
        
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
        
        # Limpiar archivos temporales locales
        Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue
        Remove-Item -Path $localCertPath -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $localCerPath -Force -ErrorAction SilentlyContinue
        
        Write-Success "Certificado SSL generado y copiado al servidor"
        Write-Info "SANs: DNS=localhost, DNS=$serverHostname, IPAddress=$TargetIP, IPAddress=127.0.0.1"
        Write-Info "CER descargable desde: https://$TargetIP`:5001/api/certificate/public"
        Write-Info "BAT instalador desde: https://$TargetIP`:5001/api/certificate/install-script"
    } catch {
        Write-Error2 "No se pudo generar certificado: $_"
        Write-Info "Generar manualmente en el servidor:"
        Write-Info '  $san = "2.5.29.17={text}DNS=localhost&DNS=aquafrisch-supervisor&IPAddress=' + $TargetIP + '&IPAddress=127.0.0.1"'
        Write-Info '  $cert = New-SelfSignedCertificate -Subject "CN=Aquafrisch Supervisor (' + $TargetIP + ')" -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10) -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256 -KeyUsage DigitalSignature,KeyEncipherment -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1", $san)'
        Write-Info '  $pwd = ConvertTo-SecureString -String "Aquafrisch2024!" -Force -AsPlainText'
        Write-Info '  Export-PfxCertificate -Cert $cert -FilePath "' + $InstallPath + '\Backend\certificate.pfx" -Password $pwd'
        Write-Info '  Export-Certificate -Cert $cert -FilePath "' + $InstallPath + '\Backend\certificate.cer" -Type CERT'
    }
}

# ============================================
# PASO 8.4: Configurar Firewall (HTTPS)
# ============================================
Write-Header "PASO 8.4: Configurando Firewall"

Write-Info "Configurando regla de firewall para HTTPS:5001..."
Write-Info "HTTP:5000 es accesible en Development pero HTTPS es el acceso principal"

try {
    Invoke-Command -ComputerName $TargetIP -Credential $credential -ScriptBlock {
        # Eliminar reglas antiguas si existen (evitar duplicados)
        Get-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
        Get-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
        
        # Abrir HTTPS (acceso principal)
        New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow -Profile Any -Description 'Aquafrisch Supervisor - Puerto HTTPS (acceso principal)' | Out-Null
        # En Development tambien abrimos HTTP para compatibilidad
        New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow -Profile Any -Description 'Aquafrisch Supervisor - Puerto HTTP (desarrollo)' | Out-Null
    }
    Write-Success "Reglas de firewall configuradas: HTTPS:5001 + HTTP:5000"
}
catch {
    Write-Warning "No se pudieron configurar las reglas de firewall automaticamente."
    Write-Info "Ejecuta manualmente en el servidor (como Admin):"
    Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTPS' -Direction Inbound -Port 5001 -Protocol TCP -Action Allow" -ForegroundColor Yellow
    Write-Host "  New-NetFirewallRule -DisplayName 'Aquafrisch Supervisor HTTP' -Direction Inbound -Port 5000 -Protocol TCP -Action Allow" -ForegroundColor Yellow
}

# ============================================
# PASO 9: Registrar como Servicio de Windows
# ============================================
Write-Header "PASO 9: Registrando Servicio de Windows"

$serviceDisplayName = "Aquafrisch Supervisor"
$serviceDescription = "Aquafrisch Supervisor - SCADA/HMI Backend (Development Mode)"
$serviceExePath = "$InstallPath\Backend\SW.PC.API.Backend.exe"
# --environment Development se pasa por linea de comandos al exe
# Esto configura ASPNETCORE_ENVIRONMENT=Development sin tocar variables de entorno del servidor
$serviceBinPath = "`"$serviceExePath`" --environment Development"

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
$scCreateCmd = "sc.exe \\$TargetIP create $serviceName binPath= `"$serviceExePath --environment Development`" start= auto DisplayName= `"$serviceDisplayName`""
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

# Copiar .bat legacy por si se necesita modo consola (debugging)
$startBatSource = Join-Path $ScriptPath "Installers\Start-ServidorEmpresa.bat"
$startBatDest = Join-Path $remoteBackendPath "Start-ServidorEmpresa.bat"
if (Test-Path $startBatSource) {
    Copy-Item -Path $startBatSource -Destination $startBatDest -Force
    Write-Info "Start-ServidorEmpresa.bat copiado (modo consola para debugging)"
}

# ============================================
# PASO 10: Desconectar unidad temporal y reconectar E:
# ============================================
Write-Step "Desconectando unidad temporal Z:..."
net use "${driveLetter}:" /delete 2>$null
Write-Success "Z: desconectada"

Write-Step "Reconectando E: a \\$TargetIP\C$..."
net use "E:" /delete /y 2>$null
net use "E:" "\\$TargetIP\C$" /user:$TargetUser $TargetPassword /persistent:yes 2>$null
if (Test-Path "E:\") {
    Write-Success "E: reconectada a \\$TargetIP\C$"
} else {
    Write-Info "No se pudo reconectar E: (puedes hacerlo manualmente)"
}

# ============================================
# RESUMEN
# ============================================
Write-Header "DEPLOY COMPLETADO"
Write-Host ""
Write-Host "  SERVIDOR EMPRESA ACTUALIZADO" -ForegroundColor Green
Write-Host "  =============================" -ForegroundColor Green
Write-Host ""
Write-Host "  Destino:       $TargetIP" -ForegroundColor White
Write-Host "  Ruta:          $InstallPath\Backend" -ForegroundColor White
Write-Host "  Modo:          DEVELOPMENT (selector habilitado)" -ForegroundColor Cyan
Write-Host "  Servicio:      $serviceName (Windows Service, auto-start)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  COPIADO:" -ForegroundColor Green
Write-Host "     - Backend (ejecutables, DLLs)" -ForegroundColor White
Write-Host "     - Frontend (interfaz web)" -ForegroundColor White
Write-Host ""
Write-Host "  NO TOCADO (los ingenieros lo gestionan):" -ForegroundColor Yellow
Write-Host "     - Projects/ (Excel, modelos 3D, bases de datos)" -ForegroundColor White
Write-Host ""
Write-Host "  SERVICIO DE WINDOWS:" -ForegroundColor Yellow
Write-Host "     Nombre:     $serviceName" -ForegroundColor White
Write-Host "     Auto-start: SI (arranca con Windows)" -ForegroundColor White
Write-Host "     Recovery:   Reinicio automatico en caso de fallo" -ForegroundColor White
Write-Host "     Entorno:    Development (via --environment)" -ForegroundColor White
Write-Host ""
Write-Host "  COMANDOS DESDE TU PC (remoto):" -ForegroundColor Yellow
Write-Host "     sc.exe \\$TargetIP query $serviceName     - Ver estado" -ForegroundColor Gray
Write-Host "     sc.exe \\$TargetIP stop $serviceName      - Parar" -ForegroundColor Gray
Write-Host "     sc.exe \\$TargetIP start $serviceName     - Arrancar" -ForegroundColor Gray
Write-Host ""
Write-Host "  CERTIFICADO HTTPS:" -ForegroundColor Yellow
Write-Host "     Certificado SSL autofirmado con IP SAN" -ForegroundColor White
Write-Host "     Instalador para PCs: https://${TargetIP}:5001/api/certificate/install-script" -ForegroundColor White
Write-Host ""
Write-Host "  URLs DE ACCESO:" -ForegroundColor Yellow
Write-Host "     HTTPS: https://${TargetIP}:5001  (acceso principal)" -ForegroundColor Green
Write-Host "     HTTP:  http://${TargetIP}:5000   (desarrollo)" -ForegroundColor White
Write-Host ""

# Verificar estado final del servicio
$scFinal = sc.exe \\$TargetIP query $serviceName 2>&1
if ($scFinal -match "RUNNING") {
    Write-Success "Servicio CORRIENDO en $TargetIP - Deploy exitoso!"
} elseif ($scFinal -match "STOPPED") {
    Write-Error2 "Servicio instalado pero PARADO. Revisa los logs del servidor."
} else {
    Write-Error2 "No se pudo verificar el servicio. Verifica conectividad."
}

Write-Host ""
Read-Host "Presiona Enter para cerrar"
