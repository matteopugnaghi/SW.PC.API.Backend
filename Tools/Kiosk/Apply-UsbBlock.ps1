<#
.SYNOPSIS
    Bloquea o desbloquea dispositivos USB de almacenamiento en el IPC.

.DESCRIPTION
    Controla el acceso a pendrives, discos externos y otros dispositivos
    de almacenamiento USB modificando el servicio USBSTOR y la politica
    de registro de Windows.

    Por defecto BLOQUEA. Con -Remove DESBLOQUEA.
    Se puede ejecutar local o remotamente via WinRM.

.PARAMETER Remove
    Desbloquea USBs en vez de bloquearlos.

.PARAMETER ComputerName
    IP o hostname del IPC (para ejecucion remota).

.PARAMETER Credential
    Credenciales para sesion remota.

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.EXAMPLE
    # BLOQUEAR USBs (remoto):
    .\Apply-UsbBlock.ps1 -ComputerName 192.168.2.161 -Credential (Get-Credential)

    # DESBLOQUEAR USBs (remoto):
    .\Apply-UsbBlock.ps1 -Remove -ComputerName 192.168.2.161 -Credential (Get-Credential)

.NOTES
    Ref: 04.2-01 Guia de Hardening - IPC Unico, seccion 23
    No afecta a teclados, ratones ni pantallas tactiles (solo almacenamiento).
#>

[CmdletBinding()]
param(
    [switch]$Remove,
    [string]$ComputerName,
    [PSCredential]$Credential,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$scriptBlock = {
    param($IsDryRun, $IsRemove)

    $modeText = if ($IsRemove) { 'DESBLOQUEO' } else { 'BLOQUEO' }
    Write-Host "`n  USB Storage - $modeText" -ForegroundColor Cyan
    Write-Host "  =============================" -ForegroundColor Cyan

    # 1. Servicio USBSTOR
    $usbStor = Get-Service -Name 'USBSTOR' -ErrorAction SilentlyContinue
    if (-not $usbStor) {
        Write-Host "  [FAIL] Servicio USBSTOR no encontrado" -ForegroundColor Red
        return
    }

    $currentStart = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\USBSTOR' -Name 'Start').Start
    # Start: 3 = Manual (habilitado), 4 = Disabled (bloqueado)

    if ($IsRemove) {
        # --- DESBLOQUEAR ---
        if ($currentStart -eq 3) {
            Write-Host "  [SKIP] USBSTOR ya habilitado (Start=$currentStart)" -ForegroundColor Yellow
        } elseif ($IsDryRun) {
            Write-Host "  [DRY]  USBSTOR: se cambiaria Start de $currentStart a 3 (Manual)" -ForegroundColor Cyan
        } else {
            Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\USBSTOR' -Name 'Start' -Value 3 -Type DWord
            Write-Host "  [OK]   USBSTOR habilitado (Start=3)" -ForegroundColor Green
        }
    } else {
        # --- BLOQUEAR ---
        if ($currentStart -eq 4) {
            Write-Host "  [SKIP] USBSTOR ya bloqueado (Start=$currentStart)" -ForegroundColor Yellow
        } elseif ($IsDryRun) {
            Write-Host "  [DRY]  USBSTOR: se cambiaria Start de $currentStart a 4 (Disabled)" -ForegroundColor Cyan
        } else {
            Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\USBSTOR' -Name 'Start' -Value 4 -Type DWord
            Write-Host "  [OK]   USBSTOR bloqueado (Start=4)" -ForegroundColor Green
        }
    }

    # 2. Politica de grupo: Deny write/read a removable media
    $removablePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}'

    if ($IsRemove) {
        # --- DESBLOQUEAR politica ---
        if (Test-Path $removablePath) {
            if ($IsDryRun) {
                Write-Host "  [DRY]  Se eliminaria politica RemovableStorageDevices" -ForegroundColor Cyan
            } else {
                Remove-Item -Path $removablePath -Recurse -Force
                Write-Host "  [OK]   Politica RemovableStorageDevices eliminada" -ForegroundColor Green
            }
        } else {
            Write-Host "  [SKIP] Politica RemovableStorageDevices no existe" -ForegroundColor Yellow
        }
    } else {
        # --- BLOQUEAR politica ---
        if (-not (Test-Path $removablePath)) {
            if ($IsDryRun) {
                Write-Host "  [DRY]  Se crearia politica RemovableStorageDevices (Deny_Read+Write)" -ForegroundColor Cyan
            } else {
                New-Item -Path $removablePath -Force | Out-Null
                Set-ItemProperty -Path $removablePath -Name 'Deny_Read'  -Value 1 -Type DWord
                Set-ItemProperty -Path $removablePath -Name 'Deny_Write' -Value 1 -Type DWord
                Write-Host "  [OK]   Politica RemovableStorageDevices creada (Deny_Read + Deny_Write)" -ForegroundColor Green
            }
        } else {
            Write-Host "  [SKIP] Politica RemovableStorageDevices ya existe" -ForegroundColor Yellow
        }
    }

    # 3. Verificacion: mostrar estado actual
    Write-Host ""
    $finalStart = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\USBSTOR' -Name 'Start').Start
    $policyExists = Test-Path $removablePath
    $statusText = if ($finalStart -eq 4 -and $policyExists) { 'BLOQUEADO' }
                  elseif ($finalStart -eq 3 -and -not $policyExists) { 'DESBLOQUEADO' }
                  else { 'PARCIAL' }
    $statusColor = switch ($statusText) { 'BLOQUEADO' { 'Red' }; 'DESBLOQUEADO' { 'Green' }; default { 'Yellow' } }

    Write-Host "  Estado actual:" -ForegroundColor White
    Write-Host "    USBSTOR Start   = $finalStart $(if ($finalStart -eq 4) {'(Disabled)'} else {'(Enabled)'})" -ForegroundColor Gray
    Write-Host "    RemovablePolicy = $(if ($policyExists) {'Activa (Deny)'} else {'No activa'})" -ForegroundColor Gray
    Write-Host "    USB Storage     = $statusText" -ForegroundColor $statusColor
    Write-Host ""
}

# Banner
$modeLabel = if ($Remove) { 'DESBLOQUEO' } else { 'BLOQUEO' }
Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "  Apply-UsbBlock.ps1" -ForegroundColor Yellow
Write-Host "  $modeLabel de USB Storage" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Yellow

# Ejecutar local o remoto
if ($ComputerName) {
    if (-not $Credential) {
        $Credential = Get-Credential -Message "Credenciales para $ComputerName"
    }
    Write-Host "  Conectando a $ComputerName..." -ForegroundColor Gray
    $session = New-PSSession -ComputerName $ComputerName -Credential $Credential
    try {
        $remoteName = Invoke-Command -Session $session -ScriptBlock { $env:COMPUTERNAME }
        Write-Host "  Conectado a: $remoteName" -ForegroundColor Green
        Invoke-Command -Session $session -ScriptBlock $scriptBlock -ArgumentList $DryRun.IsPresent, $Remove.IsPresent
    } finally {
        Remove-PSSession -Session $session -ErrorAction SilentlyContinue
    }
} else {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin -and -not $DryRun) {
        Write-Host "  ERROR: Ejecutar como Administrador." -ForegroundColor Red
        exit 1
    }
    & $scriptBlock $DryRun.IsPresent $Remove.IsPresent
}

Write-Host "  Completado.`n" -ForegroundColor Green
