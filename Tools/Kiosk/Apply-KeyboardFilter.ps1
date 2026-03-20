<#
.SYNOPSIS
    Aplica o quita bloqueo de atajos de teclado via Keyboard Filter (WEKF).

.DESCRIPTION
    Ejecutar DESPUES del primer reinicio tras habilitar Client-KeyboardFilter.
    Las clases WMI de WEKF no estan disponibles hasta que el IPC se reinicia.

    Atajos: Ctrl+Escape, Alt+Tab, Alt+F4, Win, Ctrl+Alt+Delete,
            Ctrl+Shift+Escape, Win+R, Win+E, Win+L

    Por defecto BLOQUEA los atajos. Con -Remove los DESBLOQUEA.
    Se puede ejecutar local o remotamente via WinRM.

.PARAMETER Remove
    Desbloquea los atajos en vez de bloquearlos.

.PARAMETER ComputerName
    IP o hostname del IPC (para ejecucion remota).

.PARAMETER Credential
    Credenciales para sesion remota.

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.EXAMPLE
    # BLOQUEAR atajos (remoto):
    .\Apply-KeyboardFilter.ps1 -ComputerName 192.168.2.161 -Credential (Get-Credential)

    # DESBLOQUEAR atajos (remoto):
    .\Apply-KeyboardFilter.ps1 -Remove -ComputerName 192.168.2.161 -Credential (Get-Credential)

    # BLOQUEAR en el IPC directamente:
    .\Apply-KeyboardFilter.ps1

    # DESBLOQUEAR en el IPC directamente:
    .\Apply-KeyboardFilter.ps1 -Remove

    # Dry-run:
    .\Apply-KeyboardFilter.ps1 -DryRun

.NOTES
    Ref: 04.2-01 Guia de Hardening - IPC Unico, seccion 11
#>

[CmdletBinding()]
param(
    [switch]$Remove,
    [string]$ComputerName,
    [PSCredential]$Credential,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$ns = 'root\standardcimv2\embedded'

$blockedKeys = @(
    'Ctrl+Escape',
    'Alt+Tab',
    'Alt+F4',
    'Win',
    'Ctrl+Alt+Delete',
    'Ctrl+Shift+Escape',
    'Win+R',
    'Win+E',
    'Win+L'
)

$scriptBlock = {
    param($Namespace, $Keys, $IsDryRun, $IsRemove)

    # Verificar que WEKF esta disponible
    $wekfClass = Get-WmiObject -Namespace $Namespace -Class 'WEKF_PredefinedKey' -List -ErrorAction SilentlyContinue
    if (-not $wekfClass) {
        Write-Host "  ERROR: WEKF_PredefinedKey no disponible." -ForegroundColor Red
        Write-Host "  Asegurate de que:" -ForegroundColor Red
        Write-Host "    1. Client-KeyboardFilter esta habilitado" -ForegroundColor Red
        Write-Host "    2. El IPC se ha reiniciado tras habilitarlo" -ForegroundColor Red
        return
    }

    $modeText = if ($IsRemove) { 'DESBLOQUEO' } else { 'BLOQUEO' }
    Write-Host "`n  Keyboard Filter (WEKF) - $modeText de atajos" -ForegroundColor Cyan
    Write-Host "  ================================================" -ForegroundColor Cyan

    foreach ($key in $Keys) {
        $existing = Get-WmiObject -Namespace $Namespace -Class 'WEKF_PredefinedKey' -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -eq $key }

        if ($IsRemove) {
            # --- DESBLOQUEAR ---
            if (-not $existing -or -not $existing.Enabled) {
                Write-Host "  [SKIP] $key - ya desbloqueado" -ForegroundColor Yellow
            } elseif ($IsDryRun) {
                Write-Host "  [DRY]  $key - se desbloquearia" -ForegroundColor Cyan
            } else {
                try {
                    $existing.Enabled = $false
                    $existing.Put() | Out-Null
                    Write-Host "  [OK]   $key - desbloqueado" -ForegroundColor Green
                } catch {
                    Write-Host "  [FAIL] $key - $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            # --- BLOQUEAR ---
            if ($existing -and $existing.Enabled) {
                Write-Host "  [SKIP] $key - ya bloqueado" -ForegroundColor Yellow
            } elseif ($IsDryRun) {
                Write-Host "  [DRY]  $key - se bloquearia" -ForegroundColor Cyan
            } else {
                try {
                    $obj = ([wmiclass]"\\.\${Namespace}:WEKF_PredefinedKey").CreateInstance()
                    $obj.Id = $key
                    $obj.Put() | Out-Null
                    Write-Host "  [OK]   $key - bloqueado" -ForegroundColor Green
                } catch {
                    Write-Host "  [FAIL] $key - $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
    }

    # DisableKeyboardFilterForAdministrators
    Write-Host ""
    $settings = Get-WmiObject -Namespace $Namespace -Class 'WEKF_Settings' -ErrorAction SilentlyContinue
    if ($settings) {
        if ($IsRemove) {
            Write-Host "  [INFO] DisableKeyboardFilterForAdministrators no modificado" -ForegroundColor Gray
        } elseif ($settings.DisableKeyboardFilterForAdministrators) {
            Write-Host "  [SKIP] DisableKeyboardFilterForAdministrators ya activado" -ForegroundColor Yellow
        } elseif ($IsDryRun) {
            Write-Host "  [DRY]  DisableKeyboardFilterForAdministrators = true" -ForegroundColor Cyan
        } else {
            $settings.DisableKeyboardFilterForAdministrators = $true
            $settings.Put() | Out-Null
            Write-Host "  [OK]   DisableKeyboardFilterForAdministrators = true" -ForegroundColor Green
            Write-Host "         (admins no afectados por el filtro)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  [FAIL] WEKF_Settings no disponible" -ForegroundColor Red
    }

    Write-Host ""
}

# Banner
$modeLabel = if ($Remove) { 'DESBLOQUEO' } else { 'BLOQUEO' }
Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "  Apply-KeyboardFilter.ps1" -ForegroundColor Yellow
Write-Host "  $modeLabel de atajos de teclado (WEKF)" -ForegroundColor Yellow
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
        Invoke-Command -Session $session -ScriptBlock $scriptBlock -ArgumentList $ns, $blockedKeys, $DryRun.IsPresent, $Remove.IsPresent
    } finally {
        Remove-PSSession -Session $session -ErrorAction SilentlyContinue
    }
} else {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin -and -not $DryRun) {
        Write-Host "  ERROR: Ejecutar como Administrador." -ForegroundColor Red
        exit 1
    }
    & $scriptBlock $ns $blockedKeys $DryRun.IsPresent $Remove.IsPresent
}

Write-Host "  Completado.`n" -ForegroundColor Green
