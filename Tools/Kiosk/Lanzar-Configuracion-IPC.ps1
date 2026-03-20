<#
.SYNOPSIS
    Lanzador interactivo — Configuración Estándar IPC Único.
    Click derecho → "Run with PowerShell" o doble-click.
#>

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║  AQUAFRISCH — Lanzador Configuración IPC Único             ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# --- IP del IPC ---
$defaultIP = '192.168.2.161'
$ip = Read-Host "  IP del IPC [$defaultIP]"
if (-not $ip) { $ip = $defaultIP }

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
Write-Host "  Credenciales del IPC ($ip):" -ForegroundColor White
$user = Read-Host "  Usuario (ej: Administrator)"
$pass = Read-Host -AsSecureString "  Contraseña"
$cred = New-Object System.Management.Automation.PSCredential($user, $pass)

$scriptPath = Join-Path $PSScriptRoot 'Configure-Kiosk.ps1'

switch ($modo) {
    '1' {
        Write-Host "`n  Lanzando DryRun...`n" -ForegroundColor Cyan
        try { & $scriptPath -ComputerName $ip -Credential $cred -Phase All -DryRun }
        catch { Write-Host "`n  ERROR: $($_.Exception.Message)" -ForegroundColor Red }
    }
    '2' {
        Write-Host "`n  Lanzando configuración REAL...`n" -ForegroundColor Green
        try { & $scriptPath -ComputerName $ip -Credential $cred -Phase All }
        catch { Write-Host "`n  ERROR: $($_.Exception.Message)" -ForegroundColor Red }
    }
    '3' {
        $rollbackFiles = Get-ChildItem -Path $PSScriptRoot -Filter 'rollback_*.json' | Sort-Object LastWriteTime -Descending
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
        Write-Host "  Opción no válida." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Pulsar ENTER para cerrar..." -ForegroundColor Gray
Read-Host
