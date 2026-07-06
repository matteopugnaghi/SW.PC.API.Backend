<#
.SYNOPSIS
    Lanzador interactivo - Configuracion C07-IPC-SERVER (Kiosk Completo).
    Click derecho - "Run with PowerShell" o doble-click en el .bat.

.DESCRIPTION
    Proyecto: C07.LANBWP - Drehgestell-Waschhalle Landquart (RhB)
    Arquitectura Dual:
      - CP2221-0040 (IPC SERVER) → Aquafrisch Supervisor + TwinCAT Engineering
      - CX7000 (PLC)             → TwinCAT Runtime (embedded)
    Comunicacion IPC → PLC via ADS (cable p2p 192.168.1.x/30)
#>

Write-Host ""
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host "  |  AQUAFRISCH - C07-IPC-SERVER - Configuracion Kiosk         |" -ForegroundColor Cyan
Write-Host "  |  Proyecto: C07.LANBWP - Drehgestell-Waschhalle Landquart   |" -ForegroundColor Cyan
Write-Host "  |  Hardware: CP2221-0040 (Panel PC) + CX7000 (PLC)           |" -ForegroundColor Cyan
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host ""

# --- IP del IPC SERVER (NIC corporativa RhB) ---
# NOTA: La IP 192.168.2.165 es PROVISIONAL.
# RhB IT debe confirmar la IP, mascara, gateway y DNS definitivos para NIC1.
$defaultIP = '192.168.2.165'
$ip = Read-Host "  IP del C07-IPC-SERVER (acceso actual / NIC corporativa) [$defaultIP] (PROVISIONAL)"
if (-not $ip) { $ip = $defaultIP }

# --- IP del PLC (CX7000, enlace p2p) ---
$defaultPlcIP = '192.168.1.161'
$plcIP = Read-Host "  IP del CX7000 PLC (LAN2 p2p) [$defaultPlcIP]"
if (-not $plcIP) { $plcIP = $defaultPlcIP }

# --- Hostname ---
$defaultHostname = 'C07-IPC-SERVER'
$hostname = Read-Host "  Hostname del IPC SERVER [$defaultHostname]"
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
Write-Host "  Credenciales del C07-IPC-SERVER ($ip):" -ForegroundColor White
$defaultUser = 'Administrator'
$user = Read-Host "  Usuario [$defaultUser]"
if (-not $user) { $user = $defaultUser }
$pass = Read-Host -AsSecureString "  Contrasena"
if ($pass.Length -eq 0) {
    Write-Host "  ERROR: La contrasena no puede estar vacia." -ForegroundColor Red
    Write-Host "  Pulsar ENTER para cerrar..." -ForegroundColor Gray
    Read-Host
    exit 1
}
$cred = New-Object System.Management.Automation.PSCredential($user, $pass)

$scriptPath = Join-Path $PSScriptRoot 'Configure-IPC-Server.ps1'

switch ($modo) {
    '1' {
        Write-Host "`n  Lanzando DryRun...`n" -ForegroundColor Cyan
        try {
            & $scriptPath -ComputerName $ip -Credential $cred -PlcIP $plcIP `
                -NewComputerName $hostname -Phase All -DryRun
        }
        catch { Write-Host "`n  ERROR: $($_.Exception.Message)" -ForegroundColor Red }
    }
    '2' {
        Write-Host "`n  Lanzando configuracion REAL...`n" -ForegroundColor Green
        try {
            & $scriptPath -ComputerName $ip -Credential $cred -PlcIP $plcIP `
                -NewComputerName $hostname -Phase All
        }
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
        Write-Host "  Opcion no valida." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Pulsar ENTER para cerrar..." -ForegroundColor Gray
Read-Host
