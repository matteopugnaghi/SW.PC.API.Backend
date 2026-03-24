<#
.SYNOPSIS
    Asigna el logo Aquafrisch como avatar de los usuarios Windows del kiosk.
.DESCRIPTION
    Convierte LOGO.ico a PNG (usando WPF IconBitmapDecoder para manejar
    entradas PNG-comprimidas de 256x256) y usa shell32.dll SetUserTile
    para asignar la imagen como avatar de cuenta Windows.
    Ejecutar como Administrador en cada IPC.
.PARAMETER IcoPath
    Ruta al archivo ICO. Default: busca LOGO.ico en el Backend.
.PARAMETER Users
    Lista de usuarios. Default: aqf, aqf-admin, aqf-advanced.
.EXAMPLE
    .\Set-UserAvatars.ps1
    .\Set-UserAvatars.ps1 -IcoPath "C:\ruta\logo.ico"
    .\Set-UserAvatars.ps1 -Users @("aqf-admin")
#>
param(
    [string]$IcoPath = "",
    [string[]]$Users = @("aqf", "aqf-admin", "aqf-advanced")
)

# Require admin
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: Ejecutar como Administrador" -ForegroundColor Red
    exit 1
}

# Auto-detect ICO path if not specified
if ([string]::IsNullOrEmpty($IcoPath)) {
    $candidates = @(
        (Join-Path $PSScriptRoot "..\..\wwwroot\LOGO.ico"),
        "C:\Aquafrisch Supervisor\Backend\wwwroot\LOGO.ico",
        (Join-Path $PSScriptRoot "LOGO.ico")
    )
    foreach ($c in $candidates) {
        $resolved = [System.IO.Path]::GetFullPath($c)
        if (Test-Path $resolved) {
            $IcoPath = $resolved
            break
        }
    }
}

if (-not (Test-Path $IcoPath)) {
    Write-Host "ERROR: LOGO.ico no encontrado" -ForegroundColor Red
    Write-Host "Candidatos buscados:" -ForegroundColor Yellow
    foreach ($c in $candidates) { Write-Host "  - $([System.IO.Path]::GetFullPath($c))" }
    exit 1
}

Write-Host "=== Set-UserAvatars ===" -ForegroundColor Cyan
Write-Host "ICO: $IcoPath" -ForegroundColor Gray

# --- Step 1: Convert ICO → PNG using WPF (handles PNG-compressed 256x256 entries) ---
$pngPath = [System.IO.Path]::ChangeExtension($IcoPath, "_avatar.png")

Add-Type -AssemblyName PresentationCore

$stream = [System.IO.File]::OpenRead($IcoPath)
try {
    $decoder = New-Object System.Windows.Media.Imaging.IconBitmapDecoder(
        $stream,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    )

    # Select largest frame
    $best = $decoder.Frames | Sort-Object { $_.PixelWidth * $_.PixelHeight } | Select-Object -Last 1

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add($best)

    $outStream = [System.IO.File]::Create($pngPath)
    try {
        $encoder.Save($outStream)
    }
    finally {
        $outStream.Close()
    }
}
finally {
    $stream.Close()
}

$pngSize = (Get-Item $pngPath).Length
Write-Host "PNG: $pngPath ($pngSize bytes)" -ForegroundColor Gray

# --- Step 2: Load shell32.dll SetUserTile API ---
if (-not ([System.Management.Automation.PSTypeName]'AqfUserTile').Type) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;

public class AqfUserTile
{
    [DllImport("shell32.dll", EntryPoint = "#262", CharSet = CharSet.Unicode)]
    public static extern int SetUserTile(string username, int reserved, string picpath);
}
"@
}

# --- Step 3: Assign avatar to each user ---
$success = 0
$failed = 0

foreach ($user in $Users) {
    $localUser = Get-LocalUser -Name $user -ErrorAction SilentlyContinue
    if (-not $localUser) {
        Write-Host "  SKIP  $user — usuario no existe" -ForegroundColor Yellow
        continue
    }

    try {
        $hr = [AqfUserTile]::SetUserTile($user, 0, $pngPath)
        if ($hr -eq 0) {
            Write-Host "  OK    $user — avatar asignado" -ForegroundColor Green
            $success++
        }
        else {
            Write-Host "  FAIL  $user (HRESULT=0x$($hr.ToString('X8')))" -ForegroundColor Red
            $failed++
        }
    }
    catch {
        Write-Host "  FAIL  $user — $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`nResultado: $success OK, $failed errores" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
