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
Write-Host "  URLs DE ACCESO:" -ForegroundColor Yellow
Write-Host "     HTTP:  http://${TargetIP}:5000" -ForegroundColor White
Write-Host "     HTTPS: https://${TargetIP}:5001" -ForegroundColor White
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
