<#
.SYNOPSIS
    Lanzador interactivo - Configuracion MAL-IPC-SERVER (Firewall).
    Click derecho - "Run with PowerShell" o doble-click en el .bat.
#>

Write-Host ""
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host "  |  AQUAFRISCH - MAL-IPC-SERVER - Configuracion Firewall      |" -ForegroundColor Cyan
Write-Host "  |  Proyecto: A72.TOUTWP (Toulouse)                           |" -ForegroundColor Cyan
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host ""

# --- IP del IPC SERVER ---
$defaultIP = '192.168.1.161'
$ip = Read-Host "  IP del MAL-IPC-SERVER [$defaultIP]"
if (-not $ip) { $ip = $defaultIP }

# --- IP del CLIENT (para reglas firewall) ---
$defaultClientIP = '192.168.1.162'
$clientIP = Read-Host "  IP del MAL-IPC-CLIENT [$defaultClientIP]"
if (-not $clientIP) { $clientIP = $defaultClientIP }

# --- Hostname ---
$defaultHostname = 'MAL-IPC-SERVER'
$hostname = Read-Host "  Hostname del SERVER [$defaultHostname]"
if (-not $hostname) { $hostname = $defaultHostname }

# --- Modo ---
Write-Host ""
Write-Host "  Modos disponibles:" -ForegroundColor White
Write-Host "    1. DryRun   (simular sin cambios)" -ForegroundColor Cyan
Write-Host "    2. Real     (aplicar cambios con rollback)" -ForegroundColor Green
Write-Host "    3. Rollback (revertir cambios anteriores)" -ForegroundColor Yellow
Write-Host ""
$modo = Read-Host "  Seleccionar modo [1]"
if (-not $modo) { $modo = '1' }

# --- Credenciales ---
Write-Host ""
Write-Host "  Credenciales del MAL-IPC-SERVER ($ip):" -ForegroundColor White
$user = Read-Host "  Usuario (ej: Administrator)"
$pass = Read-Host -AsSecureString "  Contrasena"
$cred = New-Object System.Management.Automation.PSCredential($user, $pass)

$scriptPath = Join-Path $PSScriptRoot 'Configure-MAL-Server.ps1'

switch ($modo) {
    '1' {
        Write-Host "`n  Lanzando DryRun...`n" -ForegroundColor Cyan
        try {
            & $scriptPath -ComputerName $ip -Credential $cred -ClientIP $clientIP `
                -NewComputerName $hostname -Phase All -DryRun
        }
        catch { Write-Host "`n  ERROR: $($_.Exception.Message)" -ForegroundColor Red }
    }
    '2' {
        Write-Host "`n  Lanzando configuracion REAL...`n" -ForegroundColor Green
        try {
            & $scriptPath -ComputerName $ip -Credential $cred -ClientIP $clientIP `
                -NewComputerName $hostname -Phase All
        }
        catch { Write-Host "`n  ERROR: $($_.Exception.Message)" -ForegroundColor Red }
    }
    '3' {
        $rollbackFiles = Get-ChildItem -Path $PSScriptRoot -Filter 'rollback_server_*.json' | Sort-Object LastWriteTime -Descending
        if ($rollbackFiles.Count -eq 0) {
            Write-Host "`n  No se encontraron archivos de rollback." -ForegroundColor Red
        } else {
            Write-Host "`n  Archivos de rollback disponibles:" -ForegroundColor Yellow
            for ($i = 0; $i -lt $rollbackFiles.Count; $i++) {
                Write-Host "    $($i+1). $($rollbackFiles[$i].Name)  ($($rollbackFiles[$i].LastWriteTime))" -ForegroundColor White
            }
            $sel = Read-Host "`n  Seleccionar [1]"
            if (-not $sel) { $sel = '1' }
            $rbFile = $rollbackFiles[[int]$sel - 1].FullName
            Write-Host "`n  Lanzando rollback: $($rollbackFiles[[int]$sel - 1].Name)`n" -ForegroundColor Yellow
            & $scriptPath -ComputerName $ip -Credential $cred -Rollback $rbFile
        }
    }
    default {
        Write-Host "  Opcion no valida." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Pulsar ENTER para cerrar..." -ForegroundColor Gray
Read-Host
