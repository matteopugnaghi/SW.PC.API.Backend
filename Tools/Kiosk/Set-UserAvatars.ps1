<#
.SYNOPSIS
    Asigna el logo Aquafrisch como avatar de los usuarios Windows.
.DESCRIPTION
    Convierte LOGO.ico a PNG (WPF IconBitmapDecoder para manejar entradas
    PNG-comprimidas de 256x256), genera JPGs en multiples tamanos, y escribe
    las rutas en el registro AccountPicture de cada usuario.

    Metodo: Registro directo HKLM AccountPicture\Users\{SID}\Image{size}
    (SetUserTile de shell32.dll no persiste en Windows IoT Enterprise).

    Ejecutar como Administrador en cada IPC.
.PARAMETER ImagePath
    Ruta a la imagen (ICO, PNG, JPG, BMP). Default: busca LOGO.ico en el Backend.
.PARAMETER SharedDir
    Carpeta donde se guardan los JPGs generados. Default: C:\ProgramData\Aquafrisch.
.PARAMETER Users
    Lista de usuarios. Default: aqf, aqf-admin, aqf-advanced.
.PARAMETER IncludeAdmin
    Incluir la cuenta Administrator. Default: true.
.EXAMPLE
    .\Set-UserAvatars.ps1
    .\Set-UserAvatars.ps1 -ImagePath "C:\ruta\logo.png"
    .\Set-UserAvatars.ps1 -Users @("aqf-admin") -IncludeAdmin $false
#>
param(
    [string]$ImagePath = "",
    [string]$SharedDir = "C:\ProgramData\Aquafrisch",
    [string[]]$Users = @("aqf", "aqf-admin", "aqf-advanced"),
    [bool]$IncludeAdmin = $true
)

# Require admin
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Ejecutar como Administrador" -ForegroundColor Red
    exit 1
}

# Auto-detect image path if not specified
if ([string]::IsNullOrEmpty($ImagePath)) {
    $candidates = @(
        (Join-Path $PSScriptRoot "..\..\wwwroot\LOGO.ico"),
        "C:\Aquafrisch Supervisor\Backend\wwwroot\LOGO.ico",
        (Join-Path $PSScriptRoot "LOGO.ico")
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
    exit 1
}

Write-Host "=== Set-UserAvatars ===" -ForegroundColor Cyan
Write-Host "Imagen: $ImagePath" -ForegroundColor Gray

# --- Ensure shared directory exists ---
if (-not (Test-Path $SharedDir)) {
    New-Item -ItemType Directory -Path $SharedDir -Force | Out-Null
}

# --- Step 1: Convert source to PNG if ICO ---
$pngPath = Join-Path $SharedDir "logo_avatar.png"

if ($ImagePath -match '\.ico$') {
    Add-Type -AssemblyName PresentationCore
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
    Write-Host "  ICO -> PNG: $pngPath" -ForegroundColor Gray
}
else {
    Copy-Item -Path $ImagePath -Destination $pngPath -Force
}

# --- Step 2: Generate JPGs in all required sizes ---
Add-Type -AssemblyName System.Drawing
$sizes = @(32, 40, 48, 96, 192, 240, 448)
$src = [System.Drawing.Image]::FromFile($pngPath)

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $sz, $sz)
    $g.Dispose()
    $outPath = Join-Path $SharedDir "avatar-$sz.jpg"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $bmp.Dispose()
}
$src.Dispose()
Write-Host "  JPGs generados: $($sizes -join ', ')px" -ForegroundColor Gray

# --- Step 3: Write registry AccountPicture for each user ---
if ($IncludeAdmin) {
    $Users = $Users + @("Administrator")
}

$success = 0
$failed = 0

foreach ($user in $Users) {
    $localUser = Get-LocalUser -Name $user -ErrorAction SilentlyContinue
    if (-not $localUser) {
        Write-Host "  SKIP  $user - usuario no existe" -ForegroundColor Yellow
        continue
    }

    try {
        $sid = $localUser.SID.Value
        $regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\$sid"

        if (-not (Test-Path $regPath)) {
            New-Item -Path $regPath -Force | Out-Null
        }

        foreach ($sz in $sizes) {
            $jpgPath = Join-Path $SharedDir "avatar-$sz.jpg"
            Set-ItemProperty -Path $regPath -Name "Image$sz" -Value $jpgPath
        }

        Write-Host "  OK    $user (SID=$sid)" -ForegroundColor Green
        $success++
    }
    catch {
        Write-Host "  FAIL  $user - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`nResultado: $success OK, $failed errores" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "Reiniciar para que la pantalla de login muestre los avatares." -ForegroundColor Gray
