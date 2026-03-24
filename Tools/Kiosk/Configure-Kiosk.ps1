<#
.SYNOPSIS
    Configuración Estándar IPC Único — Aquafrisch Supervisor.

.DESCRIPTION
    Script de configuración del IPC industrial para modo kiosk.
    Aplica las medidas del documento 04.2-01 (Hardening IPC Único):
      - Cuentas de usuario (aqf, aqf-admin, aqf-advanced)
      - Políticas de contraseña y bloqueo
      - Auto-logon del usuario kiosk
      - Custom Shell (LaunchKiosk.bat → KioskWatchdog.ps1)
      - Keyboard Filter (bloqueo de atajos)
      - Reglas de firewall (puertos 5000/5001)
      - Servicio Windows AqfSupervisor
      - Deshabilitación de servicios innecesarios
      - Auditoría de eventos

    Modos de ejecución:
      LOCAL:  Ejecutar directamente en el IPC como Administrador.
      REMOTO: Ejecutar desde el PC de desarrollo con -ComputerName y -Credential.
              El script copia los archivos kiosk al IPC via WinRM y ejecuta
              cada fase remotamente.

    Rollback:
      Antes de aplicar cambios, el script guarda un snapshot del estado previo
      en un archivo JSON. Para revertir, usar -Rollback con el archivo generado.

.PARAMETER Phase
    Fase(s) a ejecutar. Valores posibles:
      All, Accounts, Passwords, AutoLogon, Shell, KeyboardFilter,
      Firewall, Service, DisableServices, Audit, AdminTools, CopyTools, Summary

.PARAMETER SupervisorPath
    Ruta de instalación del Supervisor en el IPC. Default: C:\Aquafrisch Supervisor

.PARAMETER KioskUser
    Nombre del usuario kiosk. Default: aqf

.PARAMETER AdminUser
    Nombre del usuario administrador. Default: aqf-admin

.PARAMETER AdvancedUser
    Nombre del usuario avanzado. Default: aqf-advanced

.PARAMETER SupervisorUrl
    URL del Supervisor para el navegador kiosk. Default: https://192.168.2.161:5001

.PARAMETER NewComputerName
    Nuevo nombre para el IPC (hostname). Si se especifica, renombra el equipo.
    Requiere reinicio para aplicarse.

.PARAMETER ComputerName
    IP o hostname del IPC remoto (ej: 192.168.2.161).
    Si se especifica, el script se ejecuta via WinRM en la máquina remota.

.PARAMETER Credential
    Credenciales para conectar al IPC remoto (ej: aqf-admin).
    Se puede pasar como PSCredential o se solicitará interactivamente.

.PARAMETER DryRun
    Si se especifica, muestra los cambios sin aplicarlos.

.PARAMETER Rollback
    Ruta al archivo de rollback JSON generado por una ejecución anterior.
    Revierte todos los cambios registrados en el snapshot.

.EXAMPLE
    # LOCAL — en el propio IPC:
    .\Configure-Kiosk.ps1 -Phase All
    .\Configure-Kiosk.ps1 -Phase Accounts,Shell,Firewall
    .\Configure-Kiosk.ps1 -Phase All -DryRun

    # REMOTO — desde el PC de desarrollo:
    .\Configure-Kiosk.ps1 -ComputerName 192.168.2.161 -Credential (Get-Credential) -Phase All -DryRun
    .\Configure-Kiosk.ps1 -ComputerName 192.168.2.161 -Credential (Get-Credential) -Phase Shell,Firewall

    # ROLLBACK:
    .\Configure-Kiosk.ps1 -Rollback ".\rollback_20260319_143000.json" -ComputerName 192.168.2.161 -Credential (Get-Credential)

.NOTES
    Requiere: Ejecutar como Administrador (local) o WinRM habilitado (remoto)
    Ref: 04.2-01 — Guía de Hardening y Despliegue Seguro — IPC Único
    Ref: 04.2-03 — Guía Secuencial de Preparación del IPC Industrial
#>

[CmdletBinding()]
param(
    [ValidateSet('All','Hostname','Accounts','Passwords','AutoLogon','Shell',
                 'KeyboardFilter','Firewall','Service','DisableServices',
                 'Audit','AdminTools','CopyTools','Summary')]
    [string[]]$Phase = @('All'),

    [string]$SupervisorPath = 'C:\Aquafrisch Supervisor',

    [string]$KioskUser = 'aqf',

    [string]$AdminUser = 'aqf-admin',

    [string]$AdvancedUser = 'aqf-advanced',

    [string]$SupervisorUrl = 'https://192.168.2.161:5001',

    [string]$NewComputerName,

    [string]$ComputerName,

    [PSCredential]$Credential,

    [int]$IdleTimeoutMinutes = 30,

    [switch]$DryRun,

    [string]$Rollback
)

# ============================================================================
#  PREÁMBULO
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
$timestamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile    = Join-Path $scriptDir "Configure-Kiosk_$timestamp.log"
$results    = [System.Collections.ArrayList]::new()
$script:isRemote    = [bool]$ComputerName
$script:remoteSession = $null

# Snapshot para rollback (se rellena durante la ejecución)
$script:rollbackData = @{
    Timestamp    = $timestamp
    ComputerName = if ($ComputerName) { $ComputerName } else { $env:COMPUTERNAME }
    Actions      = [System.Collections.ArrayList]::new()
}

function Write-Step {
    param([string]$Phase, [string]$Message, [string]$Status = 'INFO')
    $ts   = Get-Date -Format 'HH:mm:ss'
    $line = "[$ts] [$Status] [$Phase] $Message"
    Write-Host $line -ForegroundColor $(switch ($Status) {
        'OK'    { 'Green'  }
        'SKIP'  { 'Yellow' }
        'FAIL'  { 'Red'    }
        'DRY'   { 'Cyan'   }
        default { 'White'  }
    })
    Add-Content -Path $logFile -Value $line
    [void]$results.Add([PSCustomObject]@{
        Phase   = $Phase
        Status  = $Status
        Message = $Message
    })
}

function Save-RollbackAction {
    param([string]$Type, [hashtable]$Data)
    [void]$script:rollbackData.Actions.Add(@{
        Type = $Type
        Data = $Data
    })
}

function Test-RunningAsAdmin {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Should-Run([string]$PhaseName) {
    return ($Phase -contains 'All') -or ($Phase -contains $PhaseName)
}

# ============================================================================
#  MODO REMOTO — Conectar via WinRM
# ============================================================================

if ($script:isRemote) {
    # Solicitar credenciales si no se proporcionaron
    if (-not $Credential) {
        Write-Host "`n  Conectando a $ComputerName — Introducir credenciales del IPC:" -ForegroundColor Cyan
        $Credential = Get-Credential -Message "Credenciales administrador del IPC ($ComputerName)"
    }

    Write-Host "`n  Verificando conexión WinRM a $ComputerName..." -ForegroundColor Cyan

    # Verificar elevación (necesaria para WinRM client config)
    if (-not (Test-RunningAsAdmin)) {
        Write-Host "`n  ERROR: Este script requiere ejecución como Administrador." -ForegroundColor Red
        Write-Host "  Click derecho → 'Ejecutar como Administrador' o usar el .bat lanzador." -ForegroundColor Yellow
        exit 1
    }

    # Asegurar que WinRM client está activo en la máquina local
    $winrmSvc = Get-Service WinRM -ErrorAction SilentlyContinue
    if ($winrmSvc -and $winrmSvc.Status -ne 'Running') {
        Start-Service WinRM
        Write-Host "  Servicio WinRM local iniciado" -ForegroundColor Gray
    }

    # Confiar en el host remoto (necesario para IPs con HTTPS auto-firmado)
    $trustedItem = Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue
    $currentTrusted = if ($trustedItem) { $trustedItem.Value } else { '' }
    if ($currentTrusted -notmatch [regex]::Escape($ComputerName)) {
        $newTrusted = if ($currentTrusted) { "$currentTrusted,$ComputerName" } else { $ComputerName }
        Set-Item WSMan:\localhost\Client\TrustedHosts -Value $newTrusted -Force
        Write-Host "  TrustedHosts actualizado: $newTrusted" -ForegroundColor Gray
    }

    try {
        $script:remoteSession = New-PSSession -ComputerName $ComputerName -Credential $Credential -ErrorAction Stop
        Write-Host "  Conectado a $ComputerName (Session ID: $($script:remoteSession.Id))" -ForegroundColor Green
    } catch {
        Write-Host "`n  ERROR: No se pudo conectar a $ComputerName" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "`n  Verificar:" -ForegroundColor Yellow
        Write-Host "    1. WinRM habilitado en el IPC: Enable-PSRemoting -Force" -ForegroundColor Gray
        Write-Host "    2. Firewall permite WinRM (5985/tcp)" -ForegroundColor Gray
        Write-Host "    3. Credenciales correctas (usuario Administrators)" -ForegroundColor Gray
        exit 1
    }
}

# ============================================================================
#  MODO ROLLBACK — Revertir cambios anteriores
# ============================================================================

if ($Rollback) {
    if (-not (Test-Path $Rollback)) {
        Write-Host "`n  ERROR: Archivo de rollback no encontrado: $Rollback" -ForegroundColor Red
        exit 1
    }

    $rbData = Get-Content $Rollback -Raw | ConvertFrom-Json
    Write-Host "`n" -NoNewline
    Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "  ║  AQUAFRISCH — Configuración Estándar IPC Único — ROLLBACK  ║" -ForegroundColor Magenta
    Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  Revirtiendo cambios del: $($rbData.Timestamp)" -ForegroundColor Yellow
    Write-Host "  Máquina original: $($rbData.ComputerName)" -ForegroundColor Yellow
    Write-Host "  Acciones a revertir: $($rbData.Actions.Count)" -ForegroundColor Yellow
    Write-Host ""

    $rollbackScript = {
        param($Actions)

        foreach ($action in $Actions) {
            $type = $action.Type
            $data = $action.Data

            try {
                switch ($type) {
                    'UserCreated' {
                        $user = $data.Username
                        $exists = Get-LocalUser -Name $user -ErrorAction SilentlyContinue
                        if ($exists) {
                            Remove-LocalUser -Name $user
                            Write-Output "[ROLLBACK] Usuario '$user' eliminado"
                        }
                    }
                    'UserDisabled' {
                        $user = $data.Username
                        Enable-LocalUser -Name $user -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Usuario '$user' re-habilitado"
                    }
                    'RegistrySet' {
                        $path = $data.Path
                        $name = $data.Name
                        if ($data.PreviousValue) {
                            Set-ItemProperty -Path $path -Name $name -Value $data.PreviousValue -Force
                            Write-Output "[ROLLBACK] Registry $path\$name → $($data.PreviousValue)"
                        } else {
                            Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
                            Write-Output "[ROLLBACK] Registry $path\$name eliminado"
                        }
                    }
                    'FirewallRuleCreated' {
                        $ruleName = $data.RuleName
                        Remove-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Regla firewall '$ruleName' eliminada"
                    }
                    'ServiceCreated' {
                        $svcName = $data.ServiceName
                        Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
                        & sc.exe delete $svcName 2>$null
                        Write-Output "[ROLLBACK] Servicio '$svcName' eliminado"
                    }
                    'ServiceDisabled' {
                        $svcName = $data.ServiceName
                        $prevType = $data.PreviousStartType
                        Set-Service -Name $svcName -StartupType $prevType -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Servicio '$svcName' restaurado a $prevType"
                    }
                    'FileCopied' {
                        $dest = $data.Destination
                        if (Test-Path $dest) {
                            Remove-Item $dest -Force
                            Write-Output "[ROLLBACK] Archivo eliminado: $dest"
                        }
                    }
                    'ComputerRenamed' {
                        $prevName = $data.PreviousName
                        Rename-Computer -NewName $prevName -Force
                        Write-Output "[ROLLBACK] Nombre restaurado a '$prevName' (reiniciar para aplicar)"
                    }
                    default {
                        Write-Output "[ROLLBACK] Tipo desconocido: $type — omitido"
                    }
                }
            } catch {
                Write-Output "[ROLLBACK][ERROR] $type — $($_.Exception.Message)"
            }
        }
    }

    if ($script:isRemote) {
        $output = Invoke-Command -Session $script:remoteSession -ScriptBlock $rollbackScript -ArgumentList (,$rbData.Actions)
    } else {
        $output = & $rollbackScript $rbData.Actions
    }

    $output | ForEach-Object { Write-Host "  $_" -ForegroundColor $(if ($_ -match 'ERROR') { 'Red' } else { 'Green' }) }

    Write-Host "`n  Rollback completado. Reiniciar el IPC para aplicar todos los cambios." -ForegroundColor Yellow

    if ($script:remoteSession) { Remove-PSSession $script:remoteSession }
    exit 0
}

# ============================================================================
#  FUNCIÓN AUXILIAR — Ejecutar en local o remoto
# ============================================================================

function Invoke-OnTarget {
    param(
        [scriptblock]$ScriptBlock,
        [object[]]$ArgumentList = @()
    )
    if ($script:isRemote) {
        Invoke-Command -Session $script:remoteSession -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
    } else {
        & $ScriptBlock @ArgumentList
    }
}

# ============================================================================
#  VERIFICACIONES INICIALES
# ============================================================================

if (-not $DryRun -and -not $script:isRemote) {
    # Ejecución local: verificar privilegios de admin
    if (-not (Test-RunningAsAdmin)) {
        Write-Host "`n  ERROR: Este script requiere privilegios de Administrador." -ForegroundColor Red
        Write-Host "  Ejecutar: Right-click → Run as Administrator`n" -ForegroundColor Yellow
        exit 1
    }
} elseif (-not $DryRun -and $script:isRemote) {
    # Ejecución remota: verificar que la sesión tiene privilegios
    $remoteAdmin = Invoke-Command -Session $script:remoteSession -ScriptBlock {
        $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]$identity
        $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    if (-not $remoteAdmin) {
        Write-Host "`n  ERROR: La cuenta '$($Credential.UserName)' no tiene privilegios de administrador en $ComputerName" -ForegroundColor Red
        Remove-PSSession $script:remoteSession
        exit 1
    }
    Write-Host "  Privilegios de administrador verificados en $ComputerName" -ForegroundColor Green
}

Write-Host "`n" -NoNewline
Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║  AQUAFRISCH — Configuración Estándar IPC Único             ║" -ForegroundColor Cyan
Write-Host "  ║  Ref: 04.2-01 · 04.2-03 · IEC 62443                       ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($script:isRemote) {
    Write-Host "  *** MODO REMOTO — Target: $ComputerName ***`n" -ForegroundColor Magenta
}
if ($DryRun) {
    Write-Host "  *** MODO DRY-RUN — No se aplicarán cambios ***`n" -ForegroundColor Cyan
}

Write-Step 'INIT' "Modo: $(if ($script:isRemote) { 'REMOTO → ' + $ComputerName } else { 'LOCAL' })"
Write-Step 'INIT' "Fases seleccionadas: $($Phase -join ', ')"
Write-Step 'INIT' "SupervisorPath: $SupervisorPath"
Write-Step 'INIT' "Log: $logFile"

# ============================================================================
#  FASE 0 — NOMBRE DEL EQUIPO (Hostname)
# ============================================================================

if (Should-Run 'Hostname') {
    Write-Host "`n═══ FASE: Nombre del Equipo ═══" -ForegroundColor Yellow

    $currentName = Invoke-OnTarget -ScriptBlock { $env:COMPUTERNAME }
    Write-Host "  Nombre actual: $currentName" -ForegroundColor White

    if (-not $NewComputerName) {
        $NewComputerName = Read-Host "  Nuevo nombre (dejar vacío para no cambiar)"
    }

    if (-not $NewComputerName -or $NewComputerName -eq $currentName) {
        Write-Step 'Hostname' "Sin cambios — se mantiene '$currentName'" 'SKIP'
    } elseif ($DryRun) {
        Write-Step 'Hostname' "[DRY] Renombraría: $currentName → $NewComputerName (requiere reinicio)" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            param($NewName)
            Rename-Computer -NewName $NewName -Force | Out-Null
        } -ArgumentList $NewComputerName

        Write-Step 'Hostname' "Renombrado: $currentName → $NewComputerName (reiniciar para aplicar)" 'OK'
        Save-RollbackAction -Type 'ComputerRenamed' -Data @{
            PreviousName = $currentName
            NewName      = $NewComputerName
        }
    }
}

# ============================================================================
#  FASE 1 — CUENTAS DE USUARIO (§6 — Hardening Guide)
# ============================================================================

if (Should-Run 'Accounts') {
    Write-Host "`n═══ FASE: Cuentas de Usuario (§6) ═══" -ForegroundColor Yellow

    $accountsScript = {
        param($KioskUser, $AdminUser, $AdvancedUser, $DryRun, $KioskPwd, $AdminPwd, $AdvPwd)
        $results = @()

        # --- Usuario kiosk (aqf) ---
        $existsKiosk = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
        if ($existsKiosk) {
            $results += @{ Action = 'SKIP'; User = $KioskUser; Msg = "Ya existe" }
        } elseif ($DryRun) {
            $results += @{ Action = 'DRY'; User = $KioskUser; Msg = "Crearía (grupo Users)" }
        } else {
            $secPwd = ConvertTo-SecureString $KioskPwd -AsPlainText -Force
            New-LocalUser -Name $KioskUser -Password $secPwd -FullName 'Aquafrisch Kiosk' `
                -Description 'Kiosk - operacion diaria' -PasswordNeverExpires | Out-Null
            Add-LocalGroupMember -Group 'Users' -Member $KioskUser -ErrorAction SilentlyContinue
            $results += @{ Action = 'OK'; User = $KioskUser; Msg = "Creado (grupo Users)" }
        }

        # --- Usuario administrador (aqf-admin) ---
        $existsAdmin = Get-LocalUser -Name $AdminUser -ErrorAction SilentlyContinue
        if ($existsAdmin) {
            $results += @{ Action = 'SKIP'; User = $AdminUser; Msg = "Ya existe" }
        } elseif ($DryRun) {
            $results += @{ Action = 'DRY'; User = $AdminUser; Msg = "Crearía (grupo Administrators)" }
        } else {
            $secPwd = ConvertTo-SecureString $AdminPwd -AsPlainText -Force
            New-LocalUser -Name $AdminUser -Password $secPwd -FullName 'Aquafrisch Admin' `
                -Description 'Admin - mantenimiento' -PasswordNeverExpires | Out-Null
            Add-LocalGroupMember -Group 'Administrators' -Member $AdminUser -ErrorAction SilentlyContinue
            $results += @{ Action = 'OK'; User = $AdminUser; Msg = "Creado (grupo Administrators)" }
        }

        # --- Usuario avanzado (aqf-advanced) ---
        $existsAdvanced = Get-LocalUser -Name $AdvancedUser -ErrorAction SilentlyContinue
        if ($existsAdvanced) {
            $results += @{ Action = 'SKIP'; User = $AdvancedUser; Msg = "Ya existe" }
        } elseif ($DryRun) {
            $results += @{ Action = 'DRY'; User = $AdvancedUser; Msg = "Crearía (Users + RDP)" }
        } else {
            $secPwd = ConvertTo-SecureString $AdvPwd -AsPlainText -Force
            New-LocalUser -Name $AdvancedUser -Password $secPwd -FullName 'Aquafrisch Advanced' `
                -Description 'Advanced - acceso emergencia' -PasswordNeverExpires | Out-Null
            Add-LocalGroupMember -Group 'Users' -Member $AdvancedUser -ErrorAction SilentlyContinue
            Add-LocalGroupMember -Group 'Remote Desktop Users' -Member $AdvancedUser -ErrorAction SilentlyContinue
            $results += @{ Action = 'OK'; User = $AdvancedUser; Msg = "Creado (Users + RDP)" }
        }

        # --- Deshabilitar cuenta Administrator original ---
        # NOTA: Si estamos conectados como Administrator via WinRM, NO deshabilitarla
        #       (cortaríamos la sesión remota). Se hará manualmente después.
        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $isCurrentAdmin = $currentUser -match '\\Administrator$'
        $builtinAdmin = Get-LocalUser -Name 'Administrator' -ErrorAction SilentlyContinue
        if ($isCurrentAdmin) {
            $results += @{ Action = 'SKIP'; User = 'Administrator'; Msg = "No deshabilitada — sesión activa como Administrator. Deshabilitar manualmente después con: Disable-LocalUser -Name Administrator" }
        } elseif ($builtinAdmin -and $builtinAdmin.Enabled) {
            if ($DryRun) {
                $results += @{ Action = 'DRY'; User = 'Administrator'; Msg = "Deshabilitaría" }
            } else {
                Disable-LocalUser -Name 'Administrator' | Out-Null
                $results += @{ Action = 'DISABLED'; User = 'Administrator'; Msg = "Deshabilitada" }
            }
        } else {
            $results += @{ Action = 'SKIP'; User = 'Administrator'; Msg = "Ya deshabilitada" }
        }

        return $results
    }

    # Solicitar contraseñas si no es DryRun
    $kioskPwd = $null; $adminPwd = $null; $advPwd = $null
    if (-not $DryRun) {
        Write-Host "  Introducir contraseñas para las cuentas del IPC:" -ForegroundColor White
        $kioskPwdSec = Read-Host -AsSecureString "  Contraseña para $KioskUser"
        $adminPwdSec = Read-Host -AsSecureString "  Contraseña para $AdminUser"
        $advPwdSec   = Read-Host -AsSecureString "  Contraseña para $AdvancedUser"

        # Convertir a plain text para pasar via WinRM (se limpia después)
        $kioskPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($kioskPwdSec))
        $adminPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPwdSec))
        $advPwd   = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($advPwdSec))
    }

    $accountResults = Invoke-OnTarget -ScriptBlock $accountsScript `
        -ArgumentList $KioskUser, $AdminUser, $AdvancedUser, $DryRun, $kioskPwd, $adminPwd, $advPwd

    # Limpiar contraseñas de memoria
    $kioskPwd = $null; $adminPwd = $null; $advPwd = $null
    [GC]::Collect()

    foreach ($r in $accountResults) {
        Write-Step 'Accounts' "Usuario '$($r.User)': $($r.Msg)" $r.Action
        if ($r.Action -eq 'OK') {
            Save-RollbackAction -Type 'UserCreated' -Data @{ Username = $r.User }
        }
        if ($r.Action -eq 'DISABLED') {
            Save-RollbackAction -Type 'UserDisabled' -Data @{ Username = $r.User }
            Write-Step 'Accounts' "Cuenta 'Administrator' original deshabilitada (§6)" 'OK'
        }
    }

    # --- Asignar avatar Aquafrisch a las cuentas ---
    $avatarScript = Join-Path $PSScriptRoot "Set-UserAvatars.ps1"
    if (Test-Path $avatarScript) {
        Write-Step 'Accounts' "Asignando avatar Aquafrisch a usuarios..." 'INFO'
        try {
            & $avatarScript -Users @($KioskUser, $AdminUser, $AdvancedUser)
        }
        catch {
            Write-Step 'Accounts' "Avatar: $($_.Exception.Message)" 'WARN'
        }
    }
}

# ============================================================================
#  FASE 2 — POLÍTICAS DE CONTRASEÑA (§6.2)
# ============================================================================

if (Should-Run 'Passwords') {
    Write-Host "`n═══ FASE: Políticas de Contraseña (§6.2) ═══" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'Passwords' "[DRY] Aplicaría: MinLen=10, Complexity=On, NoExpiry, Lockout=5/15min" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            $tempInf = Join-Path $env:TEMP "aqf_secpol_$(Get-Date -Format 'yyyyMMddHHmmss').inf"
            $tempDb  = Join-Path $env:TEMP "aqf_secpol.sdb"

            secedit /export /cfg $tempInf /quiet

            $content = Get-Content $tempInf
            $newContent = $content | ForEach-Object {
                if ($_ -match '^MinimumPasswordLength')  { 'MinimumPasswordLength = 10' }
                elseif ($_ -match '^PasswordComplexity')  { 'PasswordComplexity = 1' }
                elseif ($_ -match '^MaximumPasswordAge')   { 'MaximumPasswordAge = 0' }
                elseif ($_ -match '^MinimumPasswordAge')   { 'MinimumPasswordAge = 1' }
                elseif ($_ -match '^PasswordHistorySize')  { 'PasswordHistorySize = 5' }
                elseif ($_ -match '^LockoutBadCount')      { 'LockoutBadCount = 5' }
                elseif ($_ -match '^ResetLockoutCount')    { 'ResetLockoutCount = 15' }
                elseif ($_ -match '^LockoutDuration')      { 'LockoutDuration = 15' }
                else { $_ }
            }
            Set-Content -Path $tempInf -Value $newContent

            secedit /configure /db $tempDb /cfg $tempInf /quiet
            Remove-Item $tempInf, $tempDb -Force -ErrorAction SilentlyContinue
        }

        Write-Step 'Passwords' "Política de contraseñas aplicada (10 chars, complexity, lockout 5/15min)" 'OK'
    }
}

# ============================================================================
#  FASE 3 — AUTO-LOGON (§23 — Autostart y Modo Kiosco)
# ============================================================================

if (Should-Run 'AutoLogon') {
    Write-Host "`n═══ FASE: Auto-Logon (§23) ═══" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'AutoLogon' "[DRY] Configuraría auto-logon para usuario '$KioskUser'" 'DRY'
    } else {
        $kioskPassword = Read-Host -AsSecureString "Contraseña de $KioskUser para auto-logon"
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($kioskPassword)
        $plainPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

        # Leer valores previos para rollback
        $prevValues = Invoke-OnTarget -ScriptBlock {
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            @{
                AutoAdminLogon  = (Get-ItemProperty -Path $regPath -Name 'AutoAdminLogon'  -ErrorAction SilentlyContinue).AutoAdminLogon
                DefaultUserName = (Get-ItemProperty -Path $regPath -Name 'DefaultUserName' -ErrorAction SilentlyContinue).DefaultUserName
                DefaultPassword = $null  # No guardar contraseña en rollback por seguridad
            }
        }

        Save-RollbackAction -Type 'RegistrySet' -Data @{
            Path = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            Name = 'AutoAdminLogon'
            PreviousValue = $prevValues.AutoAdminLogon
        }
        Save-RollbackAction -Type 'RegistrySet' -Data @{
            Path = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            Name = 'DefaultUserName'
            PreviousValue = $prevValues.DefaultUserName
        }
        Save-RollbackAction -Type 'RegistrySet' -Data @{
            Path = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            Name = 'DefaultPassword'
            PreviousValue = $null
        }

        Invoke-OnTarget -ScriptBlock {
            param($KioskUser, $PlainPwd, $CompName)
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            Set-ItemProperty -Path $regPath -Name 'AutoAdminLogon'    -Value '1'
            Set-ItemProperty -Path $regPath -Name 'DefaultUserName'   -Value $KioskUser
            Set-ItemProperty -Path $regPath -Name 'DefaultPassword'   -Value $PlainPwd
            Set-ItemProperty -Path $regPath -Name 'DefaultDomainName' -Value $CompName
        } -ArgumentList $KioskUser, $plainPwd, $env:COMPUTERNAME

        # Limpiar contraseña de memoria
        $plainPwd = $null
        [GC]::Collect()

        Write-Step 'AutoLogon' "Auto-logon configurado para '$KioskUser'" 'OK'
    }
}

# ============================================================================
#  FASE 4 — CUSTOM SHELL (§23.2 — Kiosk Mode)
# ============================================================================

if (Should-Run 'Shell') {
    Write-Host "`n═══ FASE: Custom Shell — Kiosk Mode (§23.2) ═══" -ForegroundColor Yellow

    $kioskToolsDir = Join-Path $SupervisorPath 'Backend\Tools\Kiosk'
    $launchScript  = Join-Path $kioskToolsDir 'LaunchKiosk.bat'

    # Asegurar que existe el directorio en el target
    Invoke-OnTarget -ScriptBlock {
        param($Dir)
        if (-not (Test-Path $Dir)) {
            New-Item -Path $Dir -ItemType Directory -Force | Out-Null
            Write-Output "CREATED"
        } else {
            Write-Output "EXISTS"
        }
    } -ArgumentList $kioskToolsDir | ForEach-Object {
        if ($_ -eq 'CREATED') { Write-Step 'Shell' "Directorio creado: $kioskToolsDir" 'OK' }
    }

    # Copiar archivos kiosk al target
    $filesToCopy = @('LaunchKiosk.bat', 'KioskWatchdog.ps1')
    foreach ($file in $filesToCopy) {
        $srcFile = Join-Path $scriptDir $file
        $dstFile = Join-Path $kioskToolsDir $file

        if (-not (Test-Path $srcFile)) {
            Write-Step 'Shell' "Archivo fuente no encontrado: $srcFile" 'FAIL'
            continue
        }

        if ($DryRun) {
            Write-Step 'Shell' "[DRY] Copiaría $file → $kioskToolsDir" 'DRY'
        } else {
            if ($script:isRemote) {
                Copy-Item -Path $srcFile -Destination $dstFile -ToSession $script:remoteSession -Force
            } else {
                if ($srcFile -ne $dstFile) {
                    Copy-Item -Path $srcFile -Destination $dstFile -Force
                }
            }
            Write-Step 'Shell' "$file copiado a $kioskToolsDir" 'OK'
            Save-RollbackAction -Type 'FileCopied' -Data @{ Destination = $dstFile }
        }
    }

    # Inyectar IdleTimeoutMinutes en LaunchKiosk.bat
    $batDst = Join-Path $kioskToolsDir 'LaunchKiosk.bat'
    if (-not $DryRun) {
        if ($script:isRemote) {
            Invoke-Command -Session $script:remoteSession -ScriptBlock {
                param($BatPath, $Timeout)
                (Get-Content $BatPath -Raw) -replace 'SET IDLE_TIMEOUT=\d+', "SET IDLE_TIMEOUT=$Timeout" |
                    Set-Content $BatPath -NoNewline
            } -ArgumentList $batDst, $IdleTimeoutMinutes
        } else {
            (Get-Content $batDst -Raw) -replace 'SET IDLE_TIMEOUT=\d+', "SET IDLE_TIMEOUT=$IdleTimeoutMinutes" |
                Set-Content $batDst -NoNewline
        }
        Write-Step 'Shell' "Screensaver idle timeout: $IdleTimeoutMinutes minutos" 'OK'
    } else {
        Write-Step 'Shell' "[DRY] Screensaver idle timeout: $IdleTimeoutMinutes minutos" 'DRY'
    }

    # Configurar custom shell para el usuario kiosk
    if ($DryRun) {
        Write-Step 'Shell' "[DRY] Shell de '$KioskUser' → $launchScript" 'DRY'
    } else {
        $shellResult = Invoke-OnTarget -ScriptBlock {
            param($KioskUser, $ShellValue)

            $userObj = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
            if (-not $userObj) {
                return @{ Status = 'FAIL'; Msg = "Usuario '$KioskUser' no encontrado — crear primero (fase Accounts)" }
            }
            $userSID = $userObj.SID.Value

            # Leer valor previo del shell
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            $prevShell = (Get-ItemProperty -Path $regPath -Name 'Shell' -ErrorAction SilentlyContinue).Shell

            # Intentar cargar hive del usuario
            $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$userSID" -ErrorAction SilentlyContinue).ProfileImagePath
            $hiveLoaded = $false

            if ($profilePath) {
                $ntUserDat = Join-Path $profilePath 'NTUSER.DAT'
                if (-not (Test-Path "Registry::HKEY_USERS\$userSID") -and (Test-Path $ntUserDat)) {
                    reg load "HKU\$userSID" $ntUserDat 2>$null
                    $hiveLoaded = $true
                }

                $userRegPath = "Registry::HKEY_USERS\$userSID\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
                if (-not (Test-Path $userRegPath)) {
                    New-Item -Path $userRegPath -Force | Out-Null
                }
                Set-ItemProperty -Path $userRegPath -Name 'Shell' -Value $ShellValue

                if ($hiveLoaded) {
                    [GC]::Collect()
                    Start-Sleep -Milliseconds 500
                    reg unload "HKU\$userSID" 2>$null
                }
            }

            return @{ Status = 'OK'; Msg = "Custom shell → $ShellValue"; PrevShell = $prevShell }
        } -ArgumentList $KioskUser, $launchScript

        if ($shellResult.Status -eq 'FAIL') {
            Write-Step 'Shell' $shellResult.Msg 'FAIL'
        } else {
            Write-Step 'Shell' "Custom shell configurado para '$KioskUser': $launchScript" 'OK'
            Save-RollbackAction -Type 'RegistrySet' -Data @{
                Path = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
                Name = 'Shell'
                PreviousValue = $shellResult.PrevShell
            }
        }
    }
}

# ============================================================================
#  FASE 5 — KEYBOARD FILTER (§11 — Modo Kiosco)
# ============================================================================

if (Should-Run 'KeyboardFilter') {
    Write-Host "`n═══ FASE: Keyboard Filter (§11) ═══" -ForegroundColor Yellow

    if ($DryRun -and -not $script:isRemote) {
        # DryRun local: Get-WindowsOptionalFeature requiere elevación, simular
        Write-Step 'KeyboardFilter' "[DRY] Verificaría feature Client-KeyboardFilter" 'DRY'
        Write-Step 'KeyboardFilter' "[DRY] Habilitaría Keyboard Filter si no está activo" 'DRY'
        Write-Step 'KeyboardFilter' "[DRY] Configuraría MsKeyboardFilter en Automatic" 'DRY'
        $blockedKeys = @('Ctrl+Escape','Alt+Tab','Alt+F4','Win','Ctrl+Alt+Delete','Ctrl+Shift+Escape','Win+R','Win+E','Win+L')
        foreach ($key in $blockedKeys) {
            Write-Step 'KeyboardFilter' "[DRY] Bloquearía: $key para $KioskUser" 'DRY'
        }
    } else {
        $kbfResult = Invoke-OnTarget -ScriptBlock {
        param($KioskUser, $DryRun)
        $results = @()

        $kbfFeature = Get-WindowsOptionalFeature -Online -FeatureName 'Client-KeyboardFilter' -ErrorAction SilentlyContinue
        if (-not $kbfFeature) {
            $results += @{ Status = 'FAIL'; Msg = "Feature 'Client-KeyboardFilter' no encontrada — requiere Windows IoT Enterprise" }
            return $results
        }

        if ($kbfFeature.State -eq 'Enabled') {
            $results += @{ Status = 'SKIP'; Msg = "Keyboard Filter ya habilitado" }
        } elseif ($DryRun) {
            $results += @{ Status = 'DRY'; Msg = "Habilitaría Keyboard Filter (requiere reinicio)" }
        } else {
            Enable-WindowsOptionalFeature -Online -FeatureName 'Client-KeyboardFilter' -NoRestart | Out-Null
            $results += @{ Status = 'OK'; Msg = "Keyboard Filter habilitado (reiniciar para activar)" }
        }

        # Servicio MsKeyboardFilter
        $kbfService = Get-Service -Name 'MsKeyboardFilter' -ErrorAction SilentlyContinue
        if ($kbfService) {
            if ($DryRun) {
                $results += @{ Status = 'DRY'; Msg = "Configuraría MsKeyboardFilter en Automatic" }
            } else {
                Set-Service -Name 'MsKeyboardFilter' -StartupType Automatic | Out-Null
                $results += @{ Status = 'OK'; Msg = "Servicio MsKeyboardFilter → Automatic" }
            }
        }

        # Atajos bloqueados
        if ($kbfFeature.State -eq 'Enabled') {
            $blockedKeys = @('Ctrl+Escape','Alt+Tab','Alt+F4','Win','Ctrl+Alt+Delete','Ctrl+Shift+Escape','Win+R','Win+E','Win+L')
            foreach ($key in $blockedKeys) {
                if ($DryRun) {
                    $results += @{ Status = 'DRY'; Msg = "Bloquearía: $key para $KioskUser" }
                } else {
                    $results += @{ Status = 'INFO'; Msg = "Atajo $key marcado para bloqueo (aplicar via WEKF)" }
                }
            }

            # DisableKeyboardFilterForAdministrators
            if (-not $DryRun) {
                try {
                    $wekfSettings = Get-CimInstance -Namespace 'root\standardcimv2\embedded' -ClassName 'WEKF_Settings' -ErrorAction Stop
                    if ($wekfSettings) {
                        Set-CimInstance -InputObject $wekfSettings -Property @{ DisableKeyboardFilterForAdministrators = $true } -ErrorAction Stop
                        $results += @{ Status = 'OK'; Msg = "DisableKeyboardFilterForAdministrators = 1" }
                    } else {
                        $results += @{ Status = 'FAIL'; Msg = "WEKF_Settings no devolvió instancias" }
                    }
                } catch {
                    # Fallback: registry-based approach
                    $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows Embedded\KeyboardFilter'
                    if (Test-Path $regPath) {
                        Set-ItemProperty -Path $regPath -Name 'DisableKeyboardFilterForAdministrators' -Value 1 -Type DWord -Force
                        $results += @{ Status = 'OK'; Msg = "DisableKeyboardFilterForAdministrators = 1 (via registro)" }
                    } else {
                        $results += @{ Status = 'FAIL'; Msg = "WEKF_Settings no disponible: $($_.Exception.Message)" }
                    }
                }
            }
        } else {
            $results += @{ Status = 'SKIP'; Msg = "Keyboard Filter no habilitado — bloqueo aplazado" }
        }

        return $results
    } -ArgumentList $KioskUser, $DryRun

    foreach ($r in $kbfResult) {
        Write-Step 'KeyboardFilter' $r.Msg $r.Status
    }
    } # cierre del else (DryRun local vs ejecución real)
}

# ============================================================================
#  FASE 6 — FIREWALL (§18 — Red & Firewall)
# ============================================================================

if (Should-Run 'Firewall') {
    Write-Host "`n═══ FASE: Firewall (§18) ═══" -ForegroundColor Yellow

    $firewallRules = @(
        @{ Name = 'AQF Supervisor HTTPS';     Port = 5001; Protocol = 'TCP'; Action = 'Allow'; Desc = 'HTTPS red corporativa (§18)' },
        @{ Name = 'AQF Supervisor HTTP local'; Port = 5000; Protocol = 'TCP'; Action = 'Allow'; RemoteAddr = '127.0.0.1'; Desc = 'HTTP solo localhost (§18)' },
        @{ Name = 'AQF Block HTTP remote';     Port = 5000; Protocol = 'TCP'; Action = 'Block'; Desc = 'Bloquear HTTP remoto (§18)' },
        @{ Name = 'AQF Block RDP';             Port = 3389; Protocol = 'TCP'; Action = 'Block'; Desc = 'RDP bloqueado (§18)' },
        @{ Name = 'AQF Block SSH';             Port = 22;   Protocol = 'TCP'; Action = 'Block'; Desc = 'SSH bloqueado (§18)' },
        @{ Name = 'AQF Block ADS Discovery';   Port = 48899; Protocol = 'UDP'; Action = 'Block'; Desc = 'ADS Discovery bloqueado (§21)' }
    )

    foreach ($rule in $firewallRules) {
        if ($DryRun) {
            Write-Step 'Firewall' "[DRY] Crearía regla: $($rule.Name) ($($rule.Action) $($rule.Protocol)/$($rule.Port))" 'DRY'
            continue
        }

        $ruleRemoteAddr = if ($rule.ContainsKey('RemoteAddr')) { $rule.RemoteAddr } else { $null }

        $created = Invoke-OnTarget -ScriptBlock {
            param($RuleName, $Port, $Protocol, $Action, $RemoteAddr, $Desc)
            $existing = Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
            if ($existing) { return 'EXISTS' }

            $params = @{
                Name        = $RuleName
                DisplayName = $RuleName
                Direction   = 'Inbound'
                Protocol    = $Protocol
                LocalPort   = $Port
                Action      = $Action
                Description = $Desc
                Enabled     = 'True'
            }
            if ($RemoteAddr -and $RemoteAddr -ne 'Any') {
                $params['RemoteAddress'] = $RemoteAddr
            }
            New-NetFirewallRule @params | Out-Null
            return 'CREATED'
        } -ArgumentList $rule.Name, $rule.Port, $rule.Protocol, $rule.Action, $ruleRemoteAddr, $rule.Desc

        if ($created -eq 'EXISTS') {
            Write-Step 'Firewall' "Regla '$($rule.Name)' ya existe" 'SKIP'
        } else {
            Write-Step 'Firewall' "Regla creada: $($rule.Name)" 'OK'
            Save-RollbackAction -Type 'FirewallRuleCreated' -Data @{ RuleName = $rule.Name }
        }
    }

    # Deshabilitar IPv6 y NetBIOS
    if (-not $DryRun) {
        Invoke-OnTarget -ScriptBlock {
            # IPv6
            Get-NetAdapterBinding -ComponentID 'ms_tcpip6' -ErrorAction SilentlyContinue |
                Where-Object { $_.Enabled } |
                ForEach-Object { Disable-NetAdapterBinding -Name $_.Name -ComponentID 'ms_tcpip6' }

            # NetBIOS over TCP/IP
            $adapters = Get-WmiObject Win32_NetworkAdapterConfiguration -Filter "IPEnabled='True'"
            foreach ($a in $adapters) { $a.SetTcpipNetbios(2) | Out-Null }
        }
        Write-Step 'Firewall' "IPv6 deshabilitado en todos los adaptadores" 'OK'
        Write-Step 'Firewall' "NetBIOS over TCP/IP deshabilitado" 'OK'
    } else {
        Write-Step 'Firewall' "[DRY] Deshabilitaría IPv6 y NetBIOS over TCP/IP" 'DRY'
    }
}

# ============================================================================
#  FASE 7 — SERVICIO WINDOWS (§22 — Despliegue del Supervisor)
# ============================================================================

if (Should-Run 'Service') {
    Write-Host "`n═══ FASE: Servicio Windows AqfSupervisor (§22) ═══" -ForegroundColor Yellow

    $serviceName = 'AqfSupervisor'
    $backendExe  = Join-Path $SupervisorPath 'Backend\SW.PC.API.Backend.exe'

    if ($DryRun) {
        Write-Step 'Service' "[DRY] Crearía servicio '$serviceName' → $backendExe" 'DRY'
    } else {
        $svcResult = Invoke-OnTarget -ScriptBlock {
            param($ServiceName, $BackendExe)
            $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($existing) {
                return @{ Status = 'SKIP'; Msg = "Servicio '$ServiceName' ya existe (Estado: $($existing.Status))" }
            }
            if (-not (Test-Path $BackendExe)) {
                return @{ Status = 'FAIL'; Msg = "Ejecutable no encontrado: $BackendExe — desplegar primero" }
            }

            & sc.exe create $ServiceName binPath= "`"$BackendExe`"" start= delayed-auto DisplayName= "`"Aquafrisch Supervisor Service`"" | Out-Null
            & sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/30000/restart/30000 | Out-Null
            & sc.exe description $ServiceName "Aquafrisch Supervisor — API REST + HMI Web (04.2-01 §22)" | Out-Null

            return @{ Status = 'OK'; Msg = "Servicio '$ServiceName' creado (Automatic Delayed Start, recovery 30s)" }
        } -ArgumentList $serviceName, $backendExe

        Write-Step 'Service' $svcResult.Msg $svcResult.Status
        if ($svcResult.Status -eq 'OK') {
            Save-RollbackAction -Type 'ServiceCreated' -Data @{ ServiceName = $serviceName }
        }
    }
}

# ============================================================================
#  FASE 8 — DESHABILITAR SERVICIOS INNECESARIOS (§15/§38)
# ============================================================================

if (Should-Run 'DisableServices') {
    Write-Host "`n═══ FASE: Deshabilitar Servicios Innecesarios (§15) ═══" -ForegroundColor Yellow

    $servicesToDisable = @(
        'XblAuthManager', 'XblGameSave', 'XboxGipSvc', 'XboxNetApiSvc',
        'bthserv', 'MapsBroker', 'lfsvc', 'RetailDemo',
        'WMPNetworkSvc', 'WSearch', 'Fax', 'TabletInputService'
    )

    if ($DryRun) {
        foreach ($svcName in $servicesToDisable) {
            Write-Step 'DisableServices' "[DRY] Deshabilitaría: $svcName" 'DRY'
        }
        Write-Step 'DisableServices' "[DRY] Deshabilitaría AutoPlay y AutoRun" 'DRY'
    } else {
        $disableResults = Invoke-OnTarget -ScriptBlock {
            param($ServiceNames)
            $results = @()

            foreach ($svcName in $ServiceNames) {
                $existing = Get-Service -Name $svcName -ErrorAction SilentlyContinue
                if (-not $existing) { continue }

                if ($existing.StartType -eq 'Disabled') {
                    $results += @{ Status = 'SKIP'; Name = $svcName; PrevType = 'Disabled'; Msg = "Ya deshabilitado" }
                    continue
                }

                $prevType = $existing.StartType.ToString()
                Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
                Set-Service -Name $svcName -StartupType Disabled
                $results += @{ Status = 'OK'; Name = $svcName; PrevType = $prevType; Msg = "Deshabilitado" }
            }

            # AutoPlay/AutoRun (§23.1)
            $explorerPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer'
            if (-not (Test-Path $explorerPath)) {
                New-Item -Path $explorerPath -Force | Out-Null
            }
            Set-ItemProperty -Path $explorerPath -Name 'NoDriveTypeAutoRun' -Value 0xFF -Type DWord -Force
            Set-ItemProperty -Path $explorerPath -Name 'NoAutorun' -Value 1 -Type DWord -Force
            $results += @{ Status = 'OK'; Name = 'AutoPlay/AutoRun'; PrevType = $null; Msg = "Deshabilitados (§23.1)" }

            return $results
        } -ArgumentList (,$servicesToDisable)

        foreach ($r in $disableResults) {
            Write-Step 'DisableServices' "$($r.Name): $($r.Msg)" $r.Status
            if ($r.Status -eq 'OK' -and $r.PrevType -and $r.Name -ne 'AutoPlay/AutoRun') {
                Save-RollbackAction -Type 'ServiceDisabled' -Data @{
                    ServiceName = $r.Name
                    PreviousStartType = $r.PrevType
                }
            }
        }
    }
}

# ============================================================================
#  FASE 9 — AUDIT POLICY (§25 — Registro de Eventos)
# ============================================================================

if (Should-Run 'Audit') {
    Write-Host "`n═══ FASE: Audit Policy (§25) ═══" -ForegroundColor Yellow

    $auditCategories = @(
        @{ Cat = 'account logon';  S = $true;  F = $true  },
        @{ Cat = 'logon/logoff';   S = $true;  F = $true  },
        @{ Cat = 'object access';  S = $false; F = $true  },
        @{ Cat = 'policy change';  S = $true;  F = $true  },
        @{ Cat = 'privilege use';  S = $false; F = $true  },
        @{ Cat = 'system';         S = $true;  F = $true  }
    )

    if ($DryRun) {
        foreach ($a in $auditCategories) {
            $setting = if ($a.S -and $a.F) { 'Success,Failure' } elseif ($a.F) { 'Failure' } else { 'Success' }
            Write-Step 'Audit' "[DRY] Audit $($a.Cat) → $setting" 'DRY'
        }
    } else {
        $auditResults = Invoke-OnTarget -ScriptBlock {
            param($Categories)
            $results = @()
            foreach ($c in $Categories) {
                $sArg = if ($c.S) { '/success:enable' } else { '/success:disable' }
                $fArg = if ($c.F) { '/failure:enable' } else { '/failure:disable' }
                auditpol /set /category:"$($c.Cat)" $sArg $fArg 2>$null | Out-Null
                $setting = if ($c.S -and $c.F) { 'Success,Failure' } elseif ($c.F) { 'Failure' } else { 'Success' }
                $results += @{ Cat = $c.Cat; Setting = $setting }
            }
            return $results
        } -ArgumentList (,$auditCategories)

        foreach ($r in $auditResults) {
            Write-Step 'Audit' "Audit '$($r.Cat)' → $($r.Setting)" 'OK'
        }
    }
}

# ============================================================================
#  FASE 10 — HERRAMIENTAS ADMIN (Escritorio aqf-admin)
# ============================================================================

if (Should-Run 'AdminTools') {
    Write-Host "`n=== FASE: Herramientas Admin (Escritorio $AdminUser) ===" -ForegroundColor Yellow

    # Determinar ruta del escritorio de aqf-admin en el IPC
    $adminDesktopTools = $null
    $adminDesktopTools = Invoke-OnTarget -ScriptBlock {
        param($AdminUser)
        $userObj = Get-LocalUser -Name $AdminUser -ErrorAction SilentlyContinue
        if (-not $userObj) { return $null }
        $userSID = $userObj.SID.Value
        $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$userSID" -ErrorAction SilentlyContinue).ProfileImagePath
        if (-not $profilePath) {
            # Perfil no creado aun, usar ruta por defecto
            $profilePath = "C:\Users\$AdminUser"
        }
        $toolsDir = Join-Path $profilePath 'Desktop\Herramientas IPC'
        if (-not (Test-Path $toolsDir)) {
            New-Item -Path $toolsDir -ItemType Directory -Force | Out-Null
        }
        return $toolsDir
    } -ArgumentList $AdminUser

    if (-not $adminDesktopTools) {
        Write-Step 'AdminTools' "Usuario '$AdminUser' no encontrado - crear primero (fase Accounts)" 'FAIL'
    } else {
        Write-Step 'AdminTools' "Carpeta: $adminDesktopTools" 'OK'

        # Scripts a copiar
        $adminScripts = @(
            'Apply-KeyboardFilter.ps1',
            'Keyboard-Filter.bat',
            'Apply-UsbBlock.ps1',
            'USB-Storage.bat'
        )

        foreach ($file in $adminScripts) {
            $srcFile = Join-Path $scriptDir $file
            $dstFile = Join-Path $adminDesktopTools $file

            if (-not (Test-Path $srcFile)) {
                Write-Step 'AdminTools' "No encontrado: $file" 'FAIL'
                continue
            }

            if ($DryRun) {
                Write-Step 'AdminTools' "[DRY] Copiaria $file" 'DRY'
            } else {
                if ($script:isRemote) {
                    Copy-Item -Path $srcFile -Destination $dstFile -ToSession $script:remoteSession -Force
                } else {
                    Copy-Item -Path $srcFile -Destination $dstFile -Force
                }
                Write-Step 'AdminTools' "$file copiado" 'OK'
                Save-RollbackAction -Type 'FileCopied' -Data @{ Destination = $dstFile }
            }
        }
    }
}

# ============================================================================
#  FASE 11 — COPIAR TOOLS/KIOSK AL IPC (Scripts operativos)
# ============================================================================

if (Should-Run 'CopyTools') {
    Write-Host "`n═══ FASE: Copiar Tools/Kiosk al IPC ═══" -ForegroundColor Yellow

    $remoteToolsDir = "$SupervisorPath\Backend\Tools\Kiosk"

    # Crear carpeta destino si no existe
    Invoke-OnTarget -ScriptBlock {
        param($Dir)
        if (-not (Test-Path $Dir)) {
            New-Item -Path $Dir -ItemType Directory -Force | Out-Null
        }
    } -ArgumentList $remoteToolsDir

    # Copiar todos los archivos .ps1 y .bat del directorio de scripts
    $toolFiles = Get-ChildItem -Path $scriptDir -File | Where-Object { $_.Extension -in '.ps1', '.bat' }

    foreach ($file in $toolFiles) {
        $dstFile = Join-Path $remoteToolsDir $file.Name

        if ($DryRun) {
            Write-Step 'CopyTools' "[DRY] Copiaría $($file.Name) → $remoteToolsDir" 'DRY'
        } else {
            try {
                if ($script:isRemote) {
                    Copy-Item -Path $file.FullName -Destination $dstFile -ToSession $script:remoteSession -Force
                } else {
                    Copy-Item -Path $file.FullName -Destination $dstFile -Force
                }
                Write-Step 'CopyTools' "$($file.Name) copiado" 'OK'
            } catch {
                Write-Step 'CopyTools' "Error copiando $($file.Name): $($_.Exception.Message)" 'FAIL'
            }
        }
    }
}

# ============================================================================
#  RESUMEN
# ============================================================================

if (Should-Run 'Summary' -or $Phase -contains 'All') {
    Write-Host "`n═══ RESUMEN ═══" -ForegroundColor Yellow

    $okCount   = @($results | Where-Object Status -eq 'OK').Count
    $skipCount = @($results | Where-Object Status -eq 'SKIP').Count
    $failCount = @($results | Where-Object Status -eq 'FAIL').Count
    $dryCount  = @($results | Where-Object Status -eq 'DRY').Count

    Write-Host ""
    Write-Host "  Aplicados:    $okCount" -ForegroundColor Green
    Write-Host "  Ya existían:  $skipCount" -ForegroundColor Yellow
    Write-Host "  Errores:      $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'White' })
    if ($DryRun) {
        Write-Host "  Dry-run:      $dryCount" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "  Log guardado: $logFile" -ForegroundColor Gray

    # Guardar archivo de rollback si hubo cambios reales
    if (-not $DryRun -and $script:rollbackData.Actions.Count -gt 0) {
        $rollbackFile = Join-Path $scriptDir "rollback_$timestamp.json"
        $script:rollbackData | ConvertTo-Json -Depth 5 | Set-Content -Path $rollbackFile -Encoding UTF8
        Write-Host "  Rollback:     $rollbackFile" -ForegroundColor Cyan
        Write-Host "  → Para revertir: .\Configure-Kiosk.ps1 -Rollback '$rollbackFile'" -ForegroundColor Cyan
        if ($script:isRemote) {
            Write-Host "                   -ComputerName $ComputerName -Credential <cred>" -ForegroundColor Cyan
        }
    }

    if ($failCount -gt 0) {
        Write-Host "`n  ⚠ Acciones con errores:" -ForegroundColor Red
        $results | Where-Object Status -eq 'FAIL' | ForEach-Object {
            Write-Host "    - [$($_.Phase)] $($_.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  PROXIMOS PASOS MANUALES:" -ForegroundColor White
    Write-Host "  1. Verificar que LaunchKiosk.bat y KioskWatchdog.ps1 estan en $SupervisorPath\Backend\Tools\Kiosk\" -ForegroundColor Gray
    Write-Host "  2. Reiniciar el IPC para activar auto-logon y custom shell" -ForegroundColor Gray
    Write-Host "  3. Tras reinicio, aplicar bloqueo de teclado:" -ForegroundColor Gray
    Write-Host "     .\Apply-KeyboardFilter.ps1 -ComputerName $ComputerName -Credential (Get-Credential)" -ForegroundColor White
    Write-Host "  4. Deshabilitar cuenta Administrator:" -ForegroundColor Gray
    Write-Host "     Disable-LocalUser -Name Administrator  (en el IPC o via WinRM)" -ForegroundColor White
    Write-Host "  5. Completar checklist 05.2-01 (Hardening IPC Unico)" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================================
#  LIMPIEZA — Cerrar sesión remota
# ============================================================================
if ($script:remoteSession) {
    Write-Host "Cerrando sesión remota..." -ForegroundColor Gray
    Remove-PSSession -Session $script:remoteSession -ErrorAction SilentlyContinue
    $script:remoteSession = $null
}
