<#
.SYNOPSIS
    Toggle USB Storage: si está bloqueado → desbloquea, si está desbloqueado → bloquea.
.DESCRIPTION
    Usado como CustomTool2 desde el menú de herramientas del sistema.
    Modifica el servicio USBSTOR y la política RemovableStorageDevices.
    Solo afecta a dispositivos de almacenamiento (pendrives, discos USB).
    NO afecta teclados, ratones ni pantallas táctiles.
.NOTES
    Ref: 04.2-01 Guia de Hardening - IPC Unico, seccion 23
#>

$ErrorActionPreference = 'Stop'
$logFile = Join-Path $PSScriptRoot 'usb-toggle.log'

function Write-Log($msg) {
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$ts | $msg" | Out-File -FilePath $logFile -Append -Encoding UTF8
}

try {
    $usbStorReg = 'HKLM:\SYSTEM\CurrentControlSet\Services\USBSTOR'
    $policyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}'

    $currentStart = (Get-ItemProperty $usbStorReg -Name 'Start').Start
    # Start: 3 = Manual (habilitado), 4 = Disabled (bloqueado)

    if ($currentStart -eq 4) {
        # ===== DESBLOQUEAR =====
        Set-ItemProperty $usbStorReg -Name 'Start' -Value 3 -Type DWord
        if (Test-Path $policyPath) {
            Remove-Item -Path $policyPath -Recurse -Force
        }
        Write-Log "USB DESBLOQUEADO (Start=3, Policy eliminada)"
    }
    else {
        # ===== BLOQUEAR =====
        Set-ItemProperty $usbStorReg -Name 'Start' -Value 4 -Type DWord
        if (-not (Test-Path $policyPath)) {
            New-Item -Path $policyPath -Force | Out-Null
        }
        Set-ItemProperty -Path $policyPath -Name 'Deny_Read'  -Value 1 -Type DWord
        Set-ItemProperty -Path $policyPath -Name 'Deny_Write' -Value 1 -Type DWord
        Write-Log "USB BLOQUEADO (Start=4, Policy Deny_Read+Write)"
    }
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
}
