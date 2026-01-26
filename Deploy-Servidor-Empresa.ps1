#Requires -Version 5.1
<#
.SYNOPSIS
    Deploy al Servidor de Empresa (Modo Development)
    Para que los ingenieros puedan configurar proyectos.

.DESCRIPTION
    Este script:
    1. Compila Backend y Frontend (codigo)
    2. Para el servidor si esta corriendo
    3. Copia SOLO codigo (backend + frontend)
    4. NO TOCA los proyectos (Excel, modelos 3D, bases de datos)
    5. Reinicia el servidor en modo Development

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
# PASO 3: Conectar al servidor
# ============================================
Write-Header "PASO 3: Conectando al servidor"

$secPassword = ConvertTo-SecureString $TargetPassword -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($TargetUser, $secPassword)

$driveLetter = "Z"
$remotePath = "\\$TargetIP\C$"

Write-Step "Conectando a $remotePath..."
try {
    Write-Info "Desconectando conexiones previas a $TargetIP..."
    net use "\\$TargetIP\C$" /delete /y 2>$null
    net use "\\$TargetIP\IPC$" /delete /y 2>$null
    
    if (Test-Path "${driveLetter}:") {
        net use "${driveLetter}:" /delete /y 2>$null
    }
    
    Start-Sleep -Seconds 1
    
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

Write-Step "Deteniendo proceso SW.PC.API.Backend..."

# Metodo 1: taskkill remoto (no requiere WinRM)
$taskkillResult = taskkill /S $TargetIP /U $TargetUser /P $TargetPassword /IM "SW.PC.API.Backend.exe" /F 2>&1
if ($taskkillResult -match "correctamente|SUCCESS") {
    Write-Success "Servidor parado con taskkill"
    Write-Info "Esperando 3 segundos para que se liberen los archivos..."
    Start-Sleep -Seconds 3
} elseif ($taskkillResult -match "no se encontr|not found|no se pudo encontrar") {
    Write-Info "El servidor no estaba corriendo"
} else {
    Write-Info "taskkill: $taskkillResult"
    
    # Metodo 2: Intentar con WMI (alternativa)
    Write-Step "Intentando con WMI..."
    try {
        $processes = Get-WmiObject -Class Win32_Process -ComputerName $TargetIP -Credential $credential -Filter "Name='SW.PC.API.Backend.exe'" -ErrorAction SilentlyContinue
        if ($processes) {
            $processes | ForEach-Object { $_.Terminate() | Out-Null }
            Write-Success "Servidor parado con WMI"
            Start-Sleep -Seconds 3
        } else {
            Write-Info "No se encontro el proceso (probablemente no esta corriendo)"
        }
    } catch {
        Write-Info "WMI no disponible: $_"
    }
}

# Verificar que los archivos estan liberados
Write-Step "Verificando que los DLLs estan liberados..."
$testFile = "${driveLetter}:\Aquafrisch Supervisor\Backend\SW.PC.API.Backend.dll"
$retryCount = 0
$maxRetries = 5

while ($retryCount -lt $maxRetries) {
    if (Test-Path $testFile) {
        try {
            $stream = [System.IO.File]::Open($testFile, 'Open', 'Read', 'None')
            $stream.Close()
            Write-Success "Archivos liberados, continuando..."
            break
        } catch {
            $retryCount++
            if ($retryCount -lt $maxRetries) {
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
    "$remoteBackendPath\wwwroot"
)

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Success "Creado: $folder"
    } else {
        Write-Info "Ya existe: $folder"
    }
}

# ============================================
# PASO 6: Copiar Backend (SOLO CODIGO)
# ============================================
Write-Header "PASO 6: Copiando Backend (solo codigo)"

$publishPath = "$BackendPath\publish"
if (Test-Path $publishPath) {
    Write-Step "Copiando ejecutables y DLLs..."
    
    Get-ChildItem -Path $publishPath -File | ForEach-Object {
        $destFile = Join-Path $remoteBackendPath $_.Name
        
        if ($_.Name -like "appsettings*.json" -and (Test-Path $destFile)) {
            Write-Info "  Manteniendo: $($_.Name) (ya existe)"
        } else {
            Copy-Item $_.FullName $destFile -Force
        }
    }
    
    Write-Success "Backend copiado"
} else {
    Write-Error2 "No se encontro carpeta publish. Ejecuta sin -SkipBackendBuild"
    exit 1
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
# PASO 9: Copiar script de arranque
# ============================================
Write-Header "PASO 9: Copiando script de arranque"

$startBatSource = Join-Path $ScriptPath "Installers\Start-ServidorEmpresa.bat"
$startBatDest = Join-Path $remoteBackendPath "Start-ServidorEmpresa.bat"

if (Test-Path $startBatSource) {
    Copy-Item -Path $startBatSource -Destination $startBatDest -Force
    Write-Success "Start-ServidorEmpresa.bat copiado"
} else {
    Write-Info "Start-ServidorEmpresa.bat no encontrado en Installers/"
}

# Crear acceso directo en escritorio
$desktopPath = "${driveLetter}:\Users\$TargetUser\Desktop"
if (Test-Path $desktopPath) {
    $shortcutPath = Join-Path $desktopPath "Aquafrisch Servidor.lnk"
    $WshShell = New-Object -ComObject WScript.Shell
    $shortcut = $WshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = "C:\Aquafrisch Supervisor\Backend\Start-ServidorEmpresa.bat"
    $shortcut.WorkingDirectory = "C:\Aquafrisch Supervisor\Backend"
    $shortcut.Description = "Iniciar Aquafrisch Supervisor (Servidor Empresa)"
    $shortcut.Save()
    Write-Success "Acceso directo creado en escritorio"
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
Write-Host ""
Write-Host "  COPIADO:" -ForegroundColor Green
Write-Host "     - Backend (ejecutables, DLLs)" -ForegroundColor White
Write-Host "     - Frontend (interfaz web)" -ForegroundColor White
Write-Host ""
Write-Host "  NO TOCADO (los ingenieros lo gestionan):" -ForegroundColor Yellow
Write-Host "     - Projects/ (Excel, modelos 3D, bases de datos)" -ForegroundColor White
Write-Host ""
Write-Host "  PARA ARRANCAR EL SERVIDOR:" -ForegroundColor Yellow
Write-Host "  1. En el servidor, doble clic en:" -ForegroundColor White
Write-Host "     'Aquafrisch Servidor' (acceso directo en escritorio)" -ForegroundColor White
Write-Host ""
Write-Host "  O ejecutar:" -ForegroundColor White
Write-Host "     C:\Aquafrisch Supervisor\Backend\Start-ServidorEmpresa.bat" -ForegroundColor Gray
Write-Host ""
Write-Host "  URLs DE ACCESO:" -ForegroundColor Yellow
Write-Host "     HTTP:  http://${TargetIP}:5000" -ForegroundColor White
Write-Host "     HTTPS: https://${TargetIP}:5001" -ForegroundColor White
Write-Host ""

# Preguntar si arrancar el servidor
$startNow = Read-Host "Arrancar el servidor ahora? (S/n)"
if ($startNow -ne 'n' -and $startNow -ne 'N') {
    Write-Step "Arrancando servidor remoto..."
    try {
        $startScript = {
            Start-Process -FilePath "C:\Aquafrisch Supervisor\Backend\Start-ServidorEmpresa.bat" -WorkingDirectory "C:\Aquafrisch Supervisor\Backend"
        }
        Invoke-Command -ComputerName $TargetIP -Credential $credential -ScriptBlock $startScript
        Write-Success "Servidor arrancado"
        Write-Host ""
        Write-Host "  Abre en tu navegador: http://${TargetIP}:5000" -ForegroundColor Cyan
    } catch {
        Write-Info "No se pudo arrancar remotamente. Arranca manualmente en el servidor."
    }
}

Write-Host ""
Read-Host "Presiona Enter para cerrar"
