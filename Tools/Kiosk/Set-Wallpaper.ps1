<#
.SYNOPSIS
    Configura el fondo de escritorio Aquafrisch para usuarios no-kiosk.
.DESCRIPTION
    Establece una imagen como wallpaper de Windows para los usuarios que
    usan escritorio (aqf-admin, aqf-advanced, Administrator).
    El usuario kiosk (aqf) no necesita wallpaper porque ejecuta Chrome en pantalla completa.

    Aplica el wallpaper de dos formas:
    1. Usuario actual: via SystemParametersInfo (efecto inmediato)
    2. Todos los usuarios especificados: via registro NTUSER.DAT (proximo login)

    Ejecutar como Administrador.
.PARAMETER ImagePath
    Ruta a la imagen (JPG, PNG, BMP). Default: busca LOGO_wallpaper.jpg en el Backend.
.PARAMETER Style
    Estilo del wallpaper: Fill, Fit, Stretch, Center, Tile, Span. Default: Center.
.PARAMETER BackgroundColor
    Color de fondo en hex (para el área no cubierta en modo Center/Fit). Default: 1A1A2E (azul oscuro).
.PARAMETER SharedDir
    Carpeta compartida donde se copia la imagen. Default: C:\ProgramData\Aquafrisch.
.PARAMETER Users
    Lista de usuarios. Default: aqf-admin, aqf-advanced.
.EXAMPLE
    .\Set-Wallpaper.ps1
    .\Set-Wallpaper.ps1 -ImagePath "C:\ruta\fondo.jpg" -Style Fill
    .\Set-Wallpaper.ps1 -SharedDir "D:\Config\Wallpapers"
    .\Set-Wallpaper.ps1 -Users @("aqf-admin")
#>
param(
    [string]$ImagePath = "",
    [ValidateSet("Fill", "Fit", "Stretch", "Center", "Tile", "Span")]
    [string]$Style = "Center",
    [string]$BackgroundColor = "1A1A2E",
    [string]$SharedDir = "C:\ProgramData\Aquafrisch",
    [string[]]$Users = @("aqf-admin", "aqf-advanced")
)

# Require admin
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Ejecutar como Administrador" -ForegroundColor Red
    exit 1
}

# --- WallpaperStyle registry values ---
# WallpaperStyle + TileWallpaper
$styleMap = @{
    "Fill"    = @{ WallpaperStyle = "10"; TileWallpaper = "0" }
    "Fit"     = @{ WallpaperStyle = "6";  TileWallpaper = "0" }
    "Stretch" = @{ WallpaperStyle = "2";  TileWallpaper = "0" }
    "Center"  = @{ WallpaperStyle = "0";  TileWallpaper = "0" }
    "Tile"    = @{ WallpaperStyle = "0";  TileWallpaper = "1" }
    "Span"    = @{ WallpaperStyle = "22"; TileWallpaper = "0" }
}

# --- Auto-detect image path ---
if ([string]::IsNullOrEmpty($ImagePath)) {
    $candidates = @(
        (Join-Path $PSScriptRoot "..\..\wwwroot\LOGO_wallpaper.jpg"),
        (Join-Path $PSScriptRoot "..\..\wwwroot\LOGO_wallpaper.png"),
        (Join-Path $PSScriptRoot "..\..\wwwroot\LOGO.ico"),
        "C:\Aquafrisch Supervisor\Backend\wwwroot\LOGO_wallpaper.jpg",
        "C:\Aquafrisch Supervisor\Backend\wwwroot\LOGO_wallpaper.png",
        "C:\Aquafrisch Supervisor\Backend\wwwroot\LOGO.ico"
    )
    foreach ($c in $candidates) {
        $resolved = [System.IO.Path]::GetFullPath($c)
        if (Test-Path $resolved) {
            $ImagePath = $resolved
            break
        }
    }
}

if (-not (Test-Path $ImagePath)) {
    Write-Host "ERROR: Imagen no encontrada" -ForegroundColor Red
    Write-Host "Candidatos buscados:" -ForegroundColor Yellow
    foreach ($c in $candidates) { Write-Host "  - $([System.IO.Path]::GetFullPath($c))" }
    Write-Host "`nCrea una imagen de wallpaper y colocala como:" -ForegroundColor Cyan
    Write-Host "  wwwroot\LOGO_wallpaper.jpg  (recomendado: 1920x1080)" -ForegroundColor Cyan
    exit 1
}

# --- If source is ICO, convert to PNG first ---
if ($ImagePath -match '\.ico$') {
    Write-Host "ICO detectado, convirtiendo a PNG..." -ForegroundColor Gray
    Add-Type -AssemblyName PresentationCore

    $pngPath = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($ImagePath),
        "LOGO_wallpaper.png"
    )

    $stream = [System.IO.File]::OpenRead($ImagePath)
    try {
        $decoder = New-Object System.Windows.Media.Imaging.IconBitmapDecoder(
            $stream,
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        )
        $best = $decoder.Frames | Sort-Object { $_.PixelWidth * $_.PixelHeight } | Select-Object -Last 1

        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add($best)

        $outStream = [System.IO.File]::Create($pngPath)
        try { $encoder.Save($outStream) }
        finally { $outStream.Close() }
    }
    finally { $stream.Close() }

    $ImagePath = $pngPath
    Write-Host "  PNG generado: $ImagePath" -ForegroundColor Gray
}

# --- Copy image to a shared location accessible by all users ---
if (-not (Test-Path $SharedDir)) {
    New-Item -ItemType Directory -Path $SharedDir -Force | Out-Null
}
$ext = [System.IO.Path]::GetExtension($ImagePath)
$sharedPath = Join-Path $SharedDir "wallpaper$ext"
Copy-Item -Path $ImagePath -Destination $sharedPath -Force
Write-Host "=== Set-Wallpaper ===" -ForegroundColor Cyan
Write-Host "Imagen: $sharedPath" -ForegroundColor Gray
Write-Host "Estilo: $Style" -ForegroundColor Gray
Write-Host "Fondo:  #$BackgroundColor" -ForegroundColor Gray

# --- Parse background color to RGB ---
$r = [Convert]::ToInt32($BackgroundColor.Substring(0, 2), 16)
$g = [Convert]::ToInt32($BackgroundColor.Substring(2, 2), 16)
$b = [Convert]::ToInt32($BackgroundColor.Substring(4, 2), 16)

# --- Apply to current user immediately via SystemParametersInfo ---
$currentUser = [Environment]::UserName
if ($Users -contains $currentUser -or $currentUser -eq "Administrator") {
    if (-not ([System.Management.Automation.PSTypeName]'AqfWallpaper').Type) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;

public class AqfWallpaper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
"@
    }

    # Set registry for current user
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name Wallpaper -Value $sharedPath
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name WallpaperStyle -Value $styleMap[$Style].WallpaperStyle
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name TileWallpaper -Value $styleMap[$Style].TileWallpaper
    Set-ItemProperty -Path "HKCU:\Control Panel\Colors" -Name Background -Value "$r $g $b"

    # Apply immediately (SPI_SETDESKWALLPAPER = 0x14, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE = 0x03)
    $result = [AqfWallpaper]::SystemParametersInfo(0x14, 0, $sharedPath, 0x03)
    if ($result) {
        Write-Host "  OK    $currentUser (sesion actual - aplicado inmediatamente)" -ForegroundColor Green
    }
    else {
        Write-Host "  WARN  $currentUser (registro configurado, wallpaper se aplicara al reiniciar)" -ForegroundColor Yellow
    }
}

# --- Apply to other users via NTUSER.DAT registry hive ---
$success = 0
$failed = 0

foreach ($user in $Users) {
    $localUser = Get-LocalUser -Name $user -ErrorAction SilentlyContinue
    if (-not $localUser) {
        Write-Host "  SKIP  $user - usuario no existe" -ForegroundColor Yellow
        continue
    }

    if ($user -eq $currentUser) {
        $success++  # Already applied above
        continue
    }

    # Find user profile path
    $sid = $localUser.SID.Value
    $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$sid" -ErrorAction SilentlyContinue).ProfileImagePath

    if (-not $profilePath) {
        # User never logged in — profile doesn't exist yet
        Write-Host "  SKIP  $user - perfil no creado (nunca inicio sesion)" -ForegroundColor Yellow
        continue
    }

    $ntUserDat = Join-Path $profilePath "NTUSER.DAT"
    if (-not (Test-Path $ntUserDat)) {
        Write-Host "  SKIP  $user - NTUSER.DAT no encontrado" -ForegroundColor Yellow
        continue
    }

    # Load the user's registry hive
    $hiveName = "HKU_$user"
    $loadResult = reg load "HKU\$hiveName" $ntUserDat 2>&1

    if ($LASTEXITCODE -ne 0) {
        # May already be loaded (user is logged in)
        if ($loadResult -match "already in use|ya est") {
            Write-Host "  SKIP  $user - sesion activa (editar manualmente o reiniciar)" -ForegroundColor Yellow
        }
        else {
            Write-Host "  FAIL  $user - no se pudo cargar NTUSER.DAT: $loadResult" -ForegroundColor Red
            $failed++
        }
        continue
    }

    try {
        $regPath = "Registry::HKU\$hiveName\Control Panel\Desktop"
        $colorPath = "Registry::HKU\$hiveName\Control Panel\Colors"

        # Ensure keys exist
        if (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }
        if (-not (Test-Path $colorPath)) { New-Item -Path $colorPath -Force | Out-Null }

        Set-ItemProperty -Path $regPath -Name Wallpaper -Value $sharedPath
        Set-ItemProperty -Path $regPath -Name WallpaperStyle -Value $styleMap[$Style].WallpaperStyle
        Set-ItemProperty -Path $regPath -Name TileWallpaper -Value $styleMap[$Style].TileWallpaper
        Set-ItemProperty -Path $colorPath -Name Background -Value "$r $g $b"

        Write-Host "  OK    $user - wallpaper configurado (proximo login)" -ForegroundColor Green
        $success++
    }
    catch {
        Write-Host "  FAIL  $user - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
    finally {
        # Unload hive
        [gc]::Collect()
        [gc]::WaitForPendingFinalizers()
        reg unload "HKU\$hiveName" 2>&1 | Out-Null
    }
}

Write-Host "`nResultado: $success OK, $failed errores" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "Nota: Los cambios se aplican en el proximo login de cada usuario." -ForegroundColor Gray
