<#
.SYNOPSIS
    Configuracion MAL-IPC-CLIENT - Modo Kiosk Completo (PC Dual).

.DESCRIPTION
    Script de configuracion del IPC Client para la MAL (Machine a Laver) Alstom.
    Arquitectura Dual PC: el CLIENT ejecuta Backend + Frontend + Antivirus,
    conectandose al MAL-IPC-SERVER (DIN rail, solo TwinCAT) via ADS.

    Aplica las medidas del documento:
      - 04.2-01 (Hardening IPC)
      - P006-ALS-TRANS-SPT-SYS-CYBER-06117-C (Cybersecurite Alstom)

    Fases:
      0.  Hostname (MAL-IPC-CLIENT)
      1.  Cuentas de usuario (aqf, aqf-admin, aqf-advanced)
      2.  Politicas de contrasena (Alstom: 12 chars, lockout 2.5min)
      3.  Auto-logon del usuario kiosk
      4.  Custom Shell (LaunchKiosk.bat - KioskWatchdog.ps1)
      5.  Keyboard Filter (bloqueo de atajos)
      6.  Firewall (2 NICs: LAN1 - SERVER, LAN2 - Red cliente)
      7.  Servicio Windows AqfSupervisor
      8.  Deshabilitacion de servicios innecesarios
      9.  Auditoria de eventos
      10. Herramientas Admin (escritorio aqf-admin)
      11. Copiar Tools/Kiosk al IPC
      12. Banner legal Alstom

    Red:
      LAN1: 192.168.1.162/30 - MAL-IPC-SERVER (192.168.1.161) - ADS, RDP, TC IDE
      LAN2: 10.10.10.2/30 - Pare-feu MAL - Red Alstom (SOC, NTP, etc.)

    Proyecto: A72.TOUTWP - MAL Toulouse

.PARAMETER Phase
    Fase(s) a ejecutar. Valores posibles:
      All, Hostname, Accounts, Passwords, AutoLogon, Shell, KeyboardFilter,
      Firewall, Service, DisableServices, Audit, AdminTools, CopyTools, Banner, Summary

.PARAMETER SupervisorPath
    Ruta de instalacion del Supervisor en el IPC. Default: C:\Aquafrisch Supervisor

.PARAMETER KioskUser
    Nombre del usuario kiosk. Default: aqf

.PARAMETER AdminUser
    Nombre del usuario administrador. Default: aqf-admin

.PARAMETER AdvancedUser
    Nombre del usuario avanzado. Default: aqf-advanced

.PARAMETER SupervisorUrl
    URL del backend para el navegador kiosk. Default: https://localhost:5001

.PARAMETER ServerIP
    IP del MAL-IPC-SERVER (TwinCAT). Default: 192.168.1.161

.PARAMETER NewComputerName
    Nuevo hostname. Si se especifica, renombra el equipo.

.PARAMETER ComputerName
    IP o hostname del IPC CLIENT remoto para conectar via WinRM.

.PARAMETER Credential
    Credenciales para conectar al IPC CLIENT remoto.

.PARAMETER IdleTimeoutMinutes
    Timeout de inactividad para screensaver. Default: 10 (Alstom: SESSION_IDLE = 600s)

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.PARAMETER Rollback
    Ruta al archivo de rollback JSON para revertir cambios.

.EXAMPLE
    # REMOTO - desde el PC de desarrollo:
    .\Configure-MAL-Client.ps1 -ComputerName 192.168.2.163 -Credential (Get-Credential) -Phase All -DryRun
    .\Configure-MAL-Client.ps1 -ComputerName 192.168.2.163 -Credential (Get-Credential) -ServerIP 192.168.1.161 -Phase All

    # ROLLBACK:
    .\Configure-MAL-Client.ps1 -Rollback ".\rollback_client_20260323.json" -ComputerName 192.168.2.163 -Credential (Get-Credential)

.NOTES
    Requiere: Ejecutar como Administrador (local) o WinRM habilitado (remoto)
    Ref: 04.2-01 - Guia de Hardening y Despliegue Seguro
    Ref: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C - Cybersecurite Alstom
    Proyecto: A72.TOUTWP - MAL Toulouse
#>

[CmdletBinding()]
param(
    [ValidateSet('All','Hostname','Accounts','Passwords','AutoLogon','Shell',
                 'KeyboardFilter','Firewall','Service','DisableServices',
                 'Audit','AdminTools','CopyTools','Banner','Summary')]
    [string[]]$Phase = @('All'),

    [string]$SupervisorPath = 'C:\Aquafrisch Supervisor',

    [string]$KioskUser = 'aqf',

    [string]$AdminUser = 'aqf-admin',

    [string]$AdvancedUser = 'aqf-advanced',

    [string]$SupervisorUrl = 'https://localhost:5001',

    [string]$ServerIP = '192.168.1.161',

    [string]$NewComputerName,

    [string]$ComputerName,

    [PSCredential]$Credential,

    [int]$IdleTimeoutMinutes = 10,

    [switch]$DryRun,

    [string]$Rollback
)

# ============================================================================
#  PREAMBULO
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Shared kiosk files are in parent directory (Tools/Kiosk/)
$sharedKioskDir = Split-Path -Parent $scriptDir
$timestamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile    = Join-Path $scriptDir "Configure-MAL-Client_$timestamp.log"
$results    = [System.Collections.ArrayList]::new()
$script:isRemote    = [bool]$ComputerName
$script:remoteSession = $null

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
#  MODO REMOTO - Conectar via WinRM
# ============================================================================

if ($script:isRemote) {
    if (-not $Credential) {
        Write-Host "`n  Conectando a $ComputerName - Introducir credenciales:" -ForegroundColor Cyan
        $Credential = Get-Credential -Message "Credenciales administrador del MAL-IPC-CLIENT ($ComputerName)"
    }

    Write-Host "`n  Verificando conexion WinRM a $ComputerName..." -ForegroundColor Cyan

    if (-not (Test-RunningAsAdmin)) {
        Write-Host "`n  ERROR: Este script requiere ejecucion como Administrador." -ForegroundColor Red
        Write-Host "  Click derecho - 'Ejecutar como Administrador' o usar el .bat lanzador." -ForegroundColor Yellow
        exit 1
    }

    $winrmSvc = Get-Service WinRM -ErrorAction SilentlyContinue
    if ($winrmSvc -and $winrmSvc.Status -ne 'Running') {
        Start-Service WinRM
        Write-Host "  Servicio WinRM local iniciado" -ForegroundColor Gray
    }

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
#  MODO ROLLBACK - Revertir cambios anteriores
# ============================================================================

if ($Rollback) {
    if (-not (Test-Path $Rollback)) {
        Write-Host "`n  ERROR: Archivo de rollback no encontrado: $Rollback" -ForegroundColor Red
        exit 1
    }

    $rbData = Get-Content $Rollback -Raw | ConvertFrom-Json
    Write-Host "`n" -NoNewline
    Write-Host "  +==============================================================+" -ForegroundColor Magenta
    Write-Host "  |  MAL-IPC-CLIENT - ROLLBACK                                 |" -ForegroundColor Magenta
    Write-Host "  +==============================================================+" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  Revirtiendo cambios del: $($rbData.Timestamp)" -ForegroundColor Yellow
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
                        Enable-LocalUser -Name $data.Username -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Usuario '$($data.Username)' re-habilitado"
                    }
                    'RegistrySet' {
                        $path = $data.Path
                        $name = $data.Name
                        if ($data.PreviousValue) {
                            Set-ItemProperty -Path $path -Name $name -Value $data.PreviousValue -Force
                            Write-Output "[ROLLBACK] Registry $path\$name - $($data.PreviousValue)"
                        } else {
                            Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
                            Write-Output "[ROLLBACK] Registry $path\$name eliminado"
                        }
                    }
                    'FirewallRuleCreated' {
                        Remove-NetFirewallRule -Name $data.RuleName -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Regla firewall '$($data.RuleName)' eliminada"
                    }
                    'ServiceCreated' {
                        Stop-Service -Name $data.ServiceName -Force -ErrorAction SilentlyContinue
                        & sc.exe delete $data.ServiceName 2>$null
                        Write-Output "[ROLLBACK] Servicio '$($data.ServiceName)' eliminado"
                    }
                    'ServiceDisabled' {
                        Set-Service -Name $data.ServiceName -StartupType $data.PreviousStartType -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Servicio '$($data.ServiceName)' restaurado a $($data.PreviousStartType)"
                    }
                    'FileCopied' {
                        if (Test-Path $data.Destination) {
                            Remove-Item $data.Destination -Force
                            Write-Output "[ROLLBACK] Archivo eliminado: $($data.Destination)"
                        }
                    }
                    'ComputerRenamed' {
                        Rename-Computer -NewName $data.PreviousName -Force
                        Write-Output "[ROLLBACK] Nombre restaurado a '$($data.PreviousName)' (reiniciar)"
                    }
                    default {
                        Write-Output "[ROLLBACK] Tipo desconocido: $type"
                    }
                }
            } catch {
                Write-Output "[ROLLBACK][ERROR] $type - $($_.Exception.Message)"
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
#  FUNCION AUXILIAR - Ejecutar en local o remoto
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
    if (-not (Test-RunningAsAdmin)) {
        Write-Host "`n  ERROR: Este script requiere privilegios de Administrador." -ForegroundColor Red
        Write-Host "  Ejecutar: Right-click - Run as Administrator`n" -ForegroundColor Yellow
        exit 1
    }
} elseif (-not $DryRun -and $script:isRemote) {
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
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host "  |  AQUAFRISCH - MAL-IPC-CLIENT - Kiosk Completo              |" -ForegroundColor Cyan
Write-Host "  |  Ref: 04.2-01 . IEC 62443 . P006-ALS-CYBER-06117-C        |" -ForegroundColor Cyan
Write-Host "  |  Proyecto: A72.TOUTWP - MAL Toulouse                       |" -ForegroundColor Cyan
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host ""

if ($script:isRemote) {
    Write-Host "  *** MODO REMOTO - Target: $ComputerName ***`n" -ForegroundColor Magenta
}
if ($DryRun) {
    Write-Host "  *** MODO DRY-RUN - No se aplicaran cambios ***`n" -ForegroundColor Cyan
}

Write-Step 'INIT' "Modo: $(if ($script:isRemote) { 'REMOTO - ' + $ComputerName } else { 'LOCAL' })"
Write-Step 'INIT' "Fases seleccionadas: $($Phase -join ', ')"
Write-Step 'INIT' "SupervisorPath: $SupervisorPath"
Write-Step 'INIT' "ServerIP (TwinCAT): $ServerIP"
Write-Step 'INIT' "SupervisorUrl: $SupervisorUrl"
Write-Step 'INIT' "Log: $logFile"

# ============================================================================
#  FASE 0 - NOMBRE DEL EQUIPO (Hostname)
# ============================================================================

if (Should-Run 'Hostname') {
    Write-Host "`n=== FASE: Nombre del Equipo ===" -ForegroundColor Yellow

    $currentName = Invoke-OnTarget -ScriptBlock { $env:COMPUTERNAME }
    Write-Host "  Nombre actual: $currentName" -ForegroundColor White

    if (-not $NewComputerName) {
        $NewComputerName = Read-Host "  Nuevo nombre (dejar vacio para no cambiar)"
    }

    if (-not $NewComputerName -or $NewComputerName -eq $currentName) {
        Write-Step 'Hostname' "Sin cambios - se mantiene '$currentName'" 'SKIP'
    } elseif ($DryRun) {
        Write-Step 'Hostname' "[DRY] Renombraria: $currentName - $NewComputerName (requiere reinicio)" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            param($NewName)
            Rename-Computer -NewName $NewName -Force | Out-Null
        } -ArgumentList $NewComputerName

        Write-Step 'Hostname' "Renombrado: $currentName - $NewComputerName (reiniciar para aplicar)" 'OK'
        Save-RollbackAction -Type 'ComputerRenamed' -Data @{
            PreviousName = $currentName
            NewName      = $NewComputerName
        }
    }
}

# ============================================================================
#  FASE 1 - CUENTAS DE USUARIO (S6 - Hardening Guide)
# ============================================================================

if (Should-Run 'Accounts') {
    Write-Host "`n=== FASE: Cuentas de Usuario (S6) ===" -ForegroundColor Yellow

    $accountsScript = {
        param($KioskUser, $AdminUser, $AdvancedUser, $DryRun, $KioskPwd, $AdminPwd, $AdvPwd)
        $results = @()

        # --- Usuario kiosk (aqf) ---
        $existsKiosk = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
        if ($existsKiosk) {
            $results += @{ Action = 'SKIP'; User = $KioskUser; Msg = "Ya existe" }
        } elseif ($DryRun) {
            $results += @{ Action = 'DRY'; User = $KioskUser; Msg = "Crearia (grupo Users)" }
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
            $results += @{ Action = 'DRY'; User = $AdminUser; Msg = "Crearia (grupo Administrators)" }
        } else {
            $secPwd = ConvertTo-SecureString $AdminPwd -AsPlainText -Force
            New-LocalUser -Name $AdminUser -Password $secPwd -FullName 'Aquafrisch Admin' `
                -Description 'Admin - mantenimiento y TwinCAT IDE' -PasswordNeverExpires | Out-Null
            Add-LocalGroupMember -Group 'Administrators' -Member $AdminUser -ErrorAction SilentlyContinue
            $results += @{ Action = 'OK'; User = $AdminUser; Msg = "Creado (grupo Administrators)" }
        }

        # --- Usuario avanzado (aqf-advanced) ---
        $existsAdvanced = Get-LocalUser -Name $AdvancedUser -ErrorAction SilentlyContinue
        if ($existsAdvanced) {
            $results += @{ Action = 'SKIP'; User = $AdvancedUser; Msg = "Ya existe" }
        } elseif ($DryRun) {
            $results += @{ Action = 'DRY'; User = $AdvancedUser; Msg = "Crearia (Users + RDP)" }
        } else {
            $secPwd = ConvertTo-SecureString $AdvPwd -AsPlainText -Force
            New-LocalUser -Name $AdvancedUser -Password $secPwd -FullName 'Aquafrisch Advanced' `
                -Description 'Advanced - acceso emergencia' -PasswordNeverExpires | Out-Null
            Add-LocalGroupMember -Group 'Users' -Member $AdvancedUser -ErrorAction SilentlyContinue
            Add-LocalGroupMember -Group 'Remote Desktop Users' -Member $AdvancedUser -ErrorAction SilentlyContinue
            $results += @{ Action = 'OK'; User = $AdvancedUser; Msg = "Creado (Users + RDP)" }
        }

        # --- Deshabilitar cuenta Administrator original ---
        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $isCurrentAdmin = $currentUser -match '\\Administrator$'
        $builtinAdmin = Get-LocalUser -Name 'Administrator' -ErrorAction SilentlyContinue
        if ($isCurrentAdmin) {
            $results += @{ Action = 'SKIP'; User = 'Administrator'; Msg = "No deshabilitada - sesion activa. Deshabilitar manualmente: Disable-LocalUser -Name Administrator" }
        } elseif ($builtinAdmin -and $builtinAdmin.Enabled) {
            if ($DryRun) {
                $results += @{ Action = 'DRY'; User = 'Administrator'; Msg = "Deshabilitaria" }
            } else {
                Disable-LocalUser -Name 'Administrator' | Out-Null
                $results += @{ Action = 'DISABLED'; User = 'Administrator'; Msg = "Deshabilitada" }
            }
        } else {
            $results += @{ Action = 'SKIP'; User = 'Administrator'; Msg = "Ya deshabilitada" }
        }

        return $results
    }

    # Solicitar contrasenas si no es DryRun
    $kioskPwd = $null; $adminPwd = $null; $advPwd = $null
    if (-not $DryRun) {
        Write-Host "  Introducir contrasenas para las cuentas del IPC:" -ForegroundColor White
        Write-Host "  (Alstom: minimo 12 caracteres, mayusculas + minusculas + numeros + especiales)" -ForegroundColor Gray
        $kioskPwdSec = Read-Host -AsSecureString "  Contrasena para $KioskUser"
        $adminPwdSec = Read-Host -AsSecureString "  Contrasena para $AdminUser"
        $advPwdSec   = Read-Host -AsSecureString "  Contrasena para $AdvancedUser"

        $kioskPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($kioskPwdSec))
        $adminPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPwdSec))
        $advPwd   = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($advPwdSec))
    }

    $accountResults = Invoke-OnTarget -ScriptBlock $accountsScript `
        -ArgumentList $KioskUser, $AdminUser, $AdvancedUser, $DryRun, $kioskPwd, $adminPwd, $advPwd

    # Limpiar contrasenas de memoria
    $kioskPwd = $null; $adminPwd = $null; $advPwd = $null
    [GC]::Collect()

    foreach ($r in $accountResults) {
        Write-Step 'Accounts' "Usuario '$($r.User)': $($r.Msg)" $r.Action
        if ($r.Action -eq 'OK') {
            Save-RollbackAction -Type 'UserCreated' -Data @{ Username = $r.User }
        }
        if ($r.Action -eq 'DISABLED') {
            Save-RollbackAction -Type 'UserDisabled' -Data @{ Username = $r.User }
            Write-Step 'Accounts' "Cuenta 'Administrator' original deshabilitada (S6)" 'OK'
        }
    }
}

# ============================================================================
#  FASE 2 - POLITICAS DE CONTRASENA (Alstom S6.2 + CYBER-06117-C)
# ============================================================================

if (Should-Run 'Passwords') {
    Write-Host "`n=== FASE: Politicas de Contrasena (Alstom CYBER-06117-C) ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'Passwords' "[DRY] Aplicaria: MinLen=12, Complexity=On, Lockout=5/2.5min, History=5" 'DRY'
        Write-Step 'Passwords' "[DRY] Alstom: MaxPasswordAge=730 (2 anos para cuentas genericas)" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            $tempInf = Join-Path $env:TEMP "aqf_secpol_$(Get-Date -Format 'yyyyMMddHHmmss').inf"
            $tempDb  = Join-Path $env:TEMP "aqf_secpol.sdb"

            secedit /export /cfg $tempInf /quiet

            $content = Get-Content $tempInf
            $newContent = $content | ForEach-Object {
                # Alstom CYBER-06117-C: Minimum 12 characters
                if     ($_ -match '^MinimumPasswordLength')  { 'MinimumPasswordLength = 12' }
                # Complexity: uppercase + lowercase + digits + special (3 of 4 on Windows)
                elseif ($_ -match '^PasswordComplexity')     { 'PasswordComplexity = 1' }
                # Alstom: generic accounts expire after 2 years (730 days)
                elseif ($_ -match '^MaximumPasswordAge')     { 'MaximumPasswordAge = 730' }
                elseif ($_ -match '^MinimumPasswordAge')     { 'MinimumPasswordAge = 1' }
                # Alstom: different from last 5 passwords
                elseif ($_ -match '^PasswordHistorySize')    { 'PasswordHistorySize = 5' }
                # Alstom: 5 login attempts (LOGIN_ATTEMPTS)
                elseif ($_ -match '^LockoutBadCount')        { 'LockoutBadCount = 5' }
                # Alstom: 150 seconds = 2.5 minutes (DELAY_AFTER_TOO_MANY_ATTEMPTS)
                # Note: Windows secedit uses minutes, so 150s ~ 3 min (rounded up)
                elseif ($_ -match '^ResetLockoutCount')      { 'ResetLockoutCount = 3' }
                elseif ($_ -match '^LockoutDuration')        { 'LockoutDuration = 3' }
                else { $_ }
            }
            Set-Content -Path $tempInf -Value $newContent

            secedit /configure /db $tempDb /cfg $tempInf /quiet
            Remove-Item $tempInf, $tempDb -Force -ErrorAction SilentlyContinue
        }

        Write-Step 'Passwords' "Politica aplicada: MinLen=12, Complexity=On, MaxAge=730d, Lockout=5/3min" 'OK'
        Write-Step 'Passwords' "Ref: Alstom CYBER-06117-C Tableau 2 + Tableau 4" 'INFO'
    }
}

# ============================================================================
#  FASE 3 - AUTO-LOGON (S23 - Autostart y Modo Kiosco)
# ============================================================================

if (Should-Run 'AutoLogon') {
    Write-Host "`n=== FASE: Auto-Logon (S23) ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'AutoLogon' "[DRY] Configuraria auto-logon para usuario '$KioskUser'" 'DRY'
    } else {
        $kioskPassword = Read-Host -AsSecureString "Contrasena de $KioskUser para auto-logon"
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($kioskPassword)
        $plainPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

        $prevValues = Invoke-OnTarget -ScriptBlock {
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            @{
                AutoAdminLogon  = (Get-ItemProperty -Path $regPath -Name 'AutoAdminLogon'  -ErrorAction SilentlyContinue).AutoAdminLogon
                DefaultUserName = (Get-ItemProperty -Path $regPath -Name 'DefaultUserName' -ErrorAction SilentlyContinue).DefaultUserName
                DefaultPassword = $null
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

        $plainPwd = $null
        [GC]::Collect()

        Write-Step 'AutoLogon' "Auto-logon configurado para '$KioskUser'" 'OK'
    }
}

# ============================================================================
#  FASE 4 - CUSTOM SHELL (S23.2 - Kiosk Mode)
# ============================================================================

if (Should-Run 'Shell') {
    Write-Host "`n=== FASE: Custom Shell - Kiosk Mode (S23.2) ===" -ForegroundColor Yellow

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

    # Copiar archivos kiosk al target (desde directorio compartido padre)
    $filesToCopy = @('LaunchKiosk.bat', 'KioskWatchdog.ps1')
    foreach ($file in $filesToCopy) {
        $srcFile = Join-Path $sharedKioskDir $file
        $dstFile = Join-Path $kioskToolsDir $file

        if (-not (Test-Path $srcFile)) {
            Write-Step 'Shell' "Archivo fuente no encontrado: $srcFile" 'FAIL'
            continue
        }

        if ($DryRun) {
            Write-Step 'Shell' "[DRY] Copiaria $file - $kioskToolsDir" 'DRY'
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
        Write-Step 'Shell' "Idle timeout (Alstom SESSION_IDLE): $IdleTimeoutMinutes minutos" 'OK'
    } else {
        Write-Step 'Shell' "[DRY] Idle timeout: $IdleTimeoutMinutes minutos (Alstom: 600s)" 'DRY'
    }

    # Configurar custom shell para el usuario kiosk
    if ($DryRun) {
        Write-Step 'Shell' "[DRY] Shell de '$KioskUser' - $launchScript" 'DRY'
    } else {
        $shellResult = Invoke-OnTarget -ScriptBlock {
            param($KioskUser, $ShellValue)

            $userObj = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
            if (-not $userObj) {
                return @{ Status = 'FAIL'; Msg = "Usuario '$KioskUser' no encontrado - crear primero (fase Accounts)" }
            }
            $userSID = $userObj.SID.Value

            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
            $prevShell = (Get-ItemProperty -Path $regPath -Name 'Shell' -ErrorAction SilentlyContinue).Shell

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

            return @{ Status = 'OK'; Msg = "Custom shell - $ShellValue"; PrevShell = $prevShell }
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
#  FASE 5 - KEYBOARD FILTER (S11 - Modo Kiosco)
# ============================================================================

if (Should-Run 'KeyboardFilter') {
    Write-Host "`n=== FASE: Keyboard Filter (S11) ===" -ForegroundColor Yellow

    if ($DryRun -and -not $script:isRemote) {
        Write-Step 'KeyboardFilter' "[DRY] Verificaria feature Client-KeyboardFilter" 'DRY'
        Write-Step 'KeyboardFilter' "[DRY] Habilitaria Keyboard Filter si no esta activo" 'DRY'
        Write-Step 'KeyboardFilter' "[DRY] Configuraria MsKeyboardFilter en Automatic" 'DRY'
        $blockedKeys = @('Ctrl+Escape','Alt+Tab','Alt+F4','Win','Ctrl+Alt+Delete','Ctrl+Shift+Escape','Win+R','Win+E','Win+L')
        foreach ($key in $blockedKeys) {
            Write-Step 'KeyboardFilter' "[DRY] Bloquearia: $key para $KioskUser" 'DRY'
        }
    } else {
        $kbfResult = Invoke-OnTarget -ScriptBlock {
            param($KioskUser, $DryRun)
            $results = @()

            $kbfFeature = Get-WindowsOptionalFeature -Online -FeatureName 'Client-KeyboardFilter' -ErrorAction SilentlyContinue
            if (-not $kbfFeature) {
                $results += @{ Status = 'FAIL'; Msg = "Feature 'Client-KeyboardFilter' no encontrada - requiere Windows IoT Enterprise" }
                return $results
            }

            if ($kbfFeature.State -eq 'Enabled') {
                $results += @{ Status = 'SKIP'; Msg = "Keyboard Filter ya habilitado" }
            } elseif ($DryRun) {
                $results += @{ Status = 'DRY'; Msg = "Habilitaria Keyboard Filter (requiere reinicio)" }
            } else {
                Enable-WindowsOptionalFeature -Online -FeatureName 'Client-KeyboardFilter' -NoRestart | Out-Null
                $results += @{ Status = 'OK'; Msg = "Keyboard Filter habilitado (reiniciar para activar)" }
            }

            $kbfService = Get-Service -Name 'MsKeyboardFilter' -ErrorAction SilentlyContinue
            if ($kbfService) {
                if ($DryRun) {
                    $results += @{ Status = 'DRY'; Msg = "Configuraria MsKeyboardFilter en Automatic" }
                } else {
                    Set-Service -Name 'MsKeyboardFilter' -StartupType Automatic | Out-Null
                    $results += @{ Status = 'OK'; Msg = "Servicio MsKeyboardFilter - Automatic" }
                }
            }

            if ($kbfFeature.State -eq 'Enabled') {
                $blockedKeys = @('Ctrl+Escape','Alt+Tab','Alt+F4','Win','Ctrl+Alt+Delete','Ctrl+Shift+Escape','Win+R','Win+E','Win+L')
                foreach ($key in $blockedKeys) {
                    if ($DryRun) {
                        $results += @{ Status = 'DRY'; Msg = "Bloquearia: $key para $KioskUser" }
                    } else {
                        $results += @{ Status = 'INFO'; Msg = "Atajo $key marcado para bloqueo (aplicar via WEKF)" }
                    }
                }

                if (-not $DryRun) {
                    try {
                        $wekfSettings = Get-CimInstance -Namespace 'root\standardcimv2\embedded' -ClassName 'WEKF_Settings' -ErrorAction Stop
                        if ($wekfSettings) {
                            Set-CimInstance -InputObject $wekfSettings -Property @{ DisableKeyboardFilterForAdministrators = $true } -ErrorAction Stop
                            $results += @{ Status = 'OK'; Msg = "DisableKeyboardFilterForAdministrators = 1" }
                        }
                    } catch {
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
                $results += @{ Status = 'SKIP'; Msg = "Keyboard Filter no habilitado - bloqueo aplazado" }
            }

            return $results
        } -ArgumentList $KioskUser, $DryRun

        foreach ($r in $kbfResult) {
            Write-Step 'KeyboardFilter' $r.Msg $r.Status
        }
    }
}

# ============================================================================
#  FASE 6 - FIREWALL (Dual NIC - LAN1: SERVER, LAN2: Red cliente)
# ============================================================================

if (Should-Run 'Firewall') {
    Write-Host "`n=== FASE: Firewall - Dual NIC (Alstom CYBER-06117-C S18) ===" -ForegroundColor Yellow

    $firewallRules = @(
        # --- LAN1: Conexion local al MAL-IPC-SERVER ---
        # Backend accede al PLC via ADS
        @{ Name = 'MAL-CLI ADS to SERVER';        Port = 48898; Protocol = 'TCP'; Dir = 'Outbound'; Action = 'Allow'; RemoteAddr = $ServerIP; Desc = 'ADS TwinCAT al MAL-IPC-SERVER' },
        @{ Name = 'MAL-CLI Secure ADS to SERVER';  Port = 8016;  Protocol = 'TCP'; Dir = 'Outbound'; Action = 'Allow'; RemoteAddr = $ServerIP; Desc = 'Secure ADS (TLS) al MAL-IPC-SERVER' },
        @{ Name = 'MAL-CLI ADS Discovery to SERVER'; Port = 48899; Protocol = 'UDP'; Dir = 'Outbound'; Action = 'Allow'; RemoteAddr = $ServerIP; Desc = 'ADS Route Discovery al SERVER' },
        # RDP saliente para TwinCAT IDE (solo mantenimiento, aqf-admin)
        @{ Name = 'MAL-CLI RDP to SERVER';         Port = 3389;  Protocol = 'TCP'; Dir = 'Outbound'; Action = 'Allow'; RemoteAddr = $ServerIP; Desc = 'RDP al SERVER para TwinCAT IDE' },

        # --- Reglas Inbound ---
        # Backend HTTPS solo localhost (Edge kiosk - backend local)
        @{ Name = 'MAL-CLI HTTPS local';           Port = 5001;  Protocol = 'TCP'; Dir = 'Inbound'; Action = 'Allow'; RemoteAddr = '127.0.0.1'; Desc = 'HTTPS backend solo localhost' },
        # HTTP local solo para redireccion
        @{ Name = 'MAL-CLI HTTP local';            Port = 5000;  Protocol = 'TCP'; Dir = 'Inbound'; Action = 'Allow'; RemoteAddr = '127.0.0.1'; Desc = 'HTTP backend solo localhost' },
        # Bloquear HTTP remoto
        @{ Name = 'MAL-CLI Block HTTP remote';     Port = 5000;  Protocol = 'TCP'; Dir = 'Inbound'; Action = 'Block'; Desc = 'Bloquear HTTP remoto' },
        # Bloquear RDP entrante (nadie se conecta al CLIENT por RDP en produccion)
        @{ Name = 'MAL-CLI Block RDP inbound';     Port = 3389;  Protocol = 'TCP'; Dir = 'Inbound'; Action = 'Block'; Desc = 'Bloquear RDP entrante en produccion' },
        # Bloquear SSH
        @{ Name = 'MAL-CLI Block SSH';             Port = 22;    Protocol = 'TCP'; Dir = 'Inbound'; Action = 'Block'; Desc = 'SSH bloqueado' },
        # Bloquear ADS Discovery inbound
        @{ Name = 'MAL-CLI Block ADS Discovery';   Port = 48899; Protocol = 'UDP'; Dir = 'Inbound'; Action = 'Block'; Desc = 'ADS Discovery bloqueado inbound' }
    )

    foreach ($rule in $firewallRules) {
        if ($DryRun) {
            $dirLabel = if ($rule.Dir -eq 'Outbound') { 'OUT' } else { 'IN' }
            $addrLabel = if ($rule.ContainsKey('RemoteAddr')) { " from/to $($rule.RemoteAddr)" } else { '' }
            Write-Step 'Firewall' "[DRY] $dirLabel $($rule.Action) $($rule.Protocol)/$($rule.Port)$addrLabel - $($rule.Desc)" 'DRY'
            continue
        }

        $ruleRemoteAddr = if ($rule.ContainsKey('RemoteAddr')) { $rule.RemoteAddr } else { $null }
        $ruleDir = $rule.Dir

        $created = Invoke-OnTarget -ScriptBlock {
            param($RuleName, $Port, $Protocol, $Action, $RemoteAddr, $Desc, $Direction)
            $existing = Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
            if ($existing) { return 'EXISTS' }

            $params = @{
                Name        = $RuleName
                DisplayName = $RuleName
                Direction   = $Direction
                Protocol    = $Protocol
                Action      = $Action
                Description = $Desc
                Enabled     = 'True'
            }
            if ($Direction -eq 'Outbound') {
                $params['RemotePort'] = $Port
            } else {
                $params['LocalPort'] = $Port
            }
            if ($RemoteAddr -and $RemoteAddr -ne 'Any') {
                $params['RemoteAddress'] = $RemoteAddr
            }
            New-NetFirewallRule @params | Out-Null
            return 'CREATED'
        } -ArgumentList $rule.Name, $rule.Port, $rule.Protocol, $rule.Action, $ruleRemoteAddr, $rule.Desc, $ruleDir

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
        Write-Step 'Firewall' "[DRY] Deshabilitaria IPv6 y NetBIOS over TCP/IP" 'DRY'
    }

    Write-Host ""
    Write-Host "  RESUMEN FIREWALL MAL-IPC-CLIENT:" -ForegroundColor White
    Write-Host "  +--------------------------------------------------------+" -ForegroundColor Gray
    Write-Host "  | LAN1 OUTBOUND (- SERVER $ServerIP):               |" -ForegroundColor Gray
    Write-Host "  |   TCP 48898  - ADS TwinCAT                            |" -ForegroundColor Green
    Write-Host "  |   TCP 8016   - Secure ADS (TLS)                       |" -ForegroundColor Green
    Write-Host "  |   UDP 48899  - ADS Discovery                          |" -ForegroundColor Green
    Write-Host "  |   TCP 3389   - RDP (TwinCAT IDE, solo aqf-admin)      |" -ForegroundColor Green
    Write-Host "  | INBOUND:                                              |" -ForegroundColor Gray
    Write-Host "  |   TCP 5001   - HTTPS solo localhost                    |" -ForegroundColor Green
    Write-Host "  |   TCP 5000   - HTTP solo localhost                     |" -ForegroundColor Green
    Write-Host "  |   BLOCK: HTTP remoto, RDP, SSH, ADS Discovery         |" -ForegroundColor Red
    Write-Host "  | LAN2 (Red cliente): reglas futuras segun Alstom       |" -ForegroundColor Yellow
    Write-Host "  +--------------------------------------------------------+" -ForegroundColor Gray
}

# ============================================================================
#  FASE 7 - SERVICIO WINDOWS (S22 - Despliegue del Supervisor)
# ============================================================================

if (Should-Run 'Service') {
    Write-Host "`n=== FASE: Servicio Windows AqfSupervisor (S22) ===" -ForegroundColor Yellow

    $serviceName = 'AqfSupervisor'
    $backendExe  = Join-Path $SupervisorPath 'Backend\SW.PC.API.Backend.exe'

    if ($DryRun) {
        Write-Step 'Service' "[DRY] Crearia servicio '$serviceName' - $backendExe" 'DRY'
    } else {
        $svcResult = Invoke-OnTarget -ScriptBlock {
            param($ServiceName, $BackendExe)
            $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($existing) {
                return @{ Status = 'SKIP'; Msg = "Servicio '$ServiceName' ya existe (Estado: $($existing.Status))" }
            }
            if (-not (Test-Path $BackendExe)) {
                return @{ Status = 'FAIL'; Msg = "Ejecutable no encontrado: $BackendExe - desplegar primero" }
            }

            & sc.exe create $ServiceName binPath= "`"$BackendExe`"" start= delayed-auto DisplayName= "`"Aquafrisch Supervisor Service`"" | Out-Null
            & sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/30000/restart/30000 | Out-Null
            & sc.exe description $ServiceName "Aquafrisch Supervisor MAL - API REST + HMI Web (A72.TOUTWP)" | Out-Null

            return @{ Status = 'OK'; Msg = "Servicio '$ServiceName' creado (Automatic Delayed Start, recovery 30s)" }
        } -ArgumentList $serviceName, $backendExe

        Write-Step 'Service' $svcResult.Msg $svcResult.Status
        if ($svcResult.Status -eq 'OK') {
            Save-RollbackAction -Type 'ServiceCreated' -Data @{ ServiceName = $serviceName }
        }
    }
}

# ============================================================================
#  FASE 8 - DESHABILITAR SERVICIOS INNECESARIOS (S15/S38)
# ============================================================================

if (Should-Run 'DisableServices') {
    Write-Host "`n=== FASE: Deshabilitar Servicios Innecesarios (S15) ===" -ForegroundColor Yellow

    $servicesToDisable = @(
        'XblAuthManager', 'XblGameSave', 'XboxGipSvc', 'XboxNetApiSvc',
        'bthserv', 'MapsBroker', 'lfsvc', 'RetailDemo',
        'WMPNetworkSvc', 'WSearch', 'Fax', 'TabletInputService'
    )

    if ($DryRun) {
        foreach ($svcName in $servicesToDisable) {
            Write-Step 'DisableServices' "[DRY] Deshabilitaria: $svcName" 'DRY'
        }
        Write-Step 'DisableServices' "[DRY] Deshabilitaria AutoPlay y AutoRun" 'DRY'
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

            # AutoPlay/AutoRun (S23.1)
            $explorerPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer'
            if (-not (Test-Path $explorerPath)) {
                New-Item -Path $explorerPath -Force | Out-Null
            }
            Set-ItemProperty -Path $explorerPath -Name 'NoDriveTypeAutoRun' -Value 0xFF -Type DWord -Force
            Set-ItemProperty -Path $explorerPath -Name 'NoAutorun' -Value 1 -Type DWord -Force
            $results += @{ Status = 'OK'; Name = 'AutoPlay/AutoRun'; PrevType = $null; Msg = "Deshabilitados (S23.1)" }

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
#  FASE 9 - AUDIT POLICY (S25 - Registro de Eventos)
# ============================================================================

if (Should-Run 'Audit') {
    Write-Host "`n=== FASE: Audit Policy (S25 / Alstom Journalisation) ===" -ForegroundColor Yellow

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
            Write-Step 'Audit' "[DRY] Audit $($a.Cat) - $setting" 'DRY'
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
            Write-Step 'Audit' "Audit '$($r.Cat)' - $($r.Setting)" 'OK'
        }
    }
}

# ============================================================================
#  FASE 10 - HERRAMIENTAS ADMIN (Escritorio aqf-admin)
# ============================================================================

if (Should-Run 'AdminTools') {
    Write-Host "`n=== FASE: Herramientas Admin (Escritorio $AdminUser) ===" -ForegroundColor Yellow

    $adminDesktopTools = Invoke-OnTarget -ScriptBlock {
        param($AdminUser)
        $userObj = Get-LocalUser -Name $AdminUser -ErrorAction SilentlyContinue
        if (-not $userObj) { return $null }
        $userSID = $userObj.SID.Value
        $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$userSID" -ErrorAction SilentlyContinue).ProfileImagePath
        if (-not $profilePath) {
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

        # Scripts compartidos del directorio padre
        $adminScripts = @(
            'Apply-KeyboardFilter.ps1',
            'Keyboard-Filter.bat',
            'Apply-UsbBlock.ps1',
            'USB-Storage.bat',
            'Toggle-UsbStorage.ps1',
            'Toggle-UsbStorage.bat'
        )

        foreach ($file in $adminScripts) {
            $srcFile = Join-Path $sharedKioskDir $file
            $dstFile = Join-Path $adminDesktopTools $file

            if (-not (Test-Path $srcFile)) {
                Write-Step 'AdminTools' "No encontrado: $file (en $sharedKioskDir)" 'FAIL'
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
#  FASE 11 - COPIAR TOOLS/KIOSK AL IPC (Scripts operativos)
# ============================================================================

if (Should-Run 'CopyTools') {
    Write-Host "`n=== FASE: Copiar Tools/Kiosk al IPC ===" -ForegroundColor Yellow

    $remoteToolsDir = "$SupervisorPath\Backend\Tools\Kiosk"

    Invoke-OnTarget -ScriptBlock {
        param($Dir)
        if (-not (Test-Path $Dir)) {
            New-Item -Path $Dir -ItemType Directory -Force | Out-Null
        }
    } -ArgumentList $remoteToolsDir

    # Copiar archivos compartidos del directorio padre
    $toolFiles = Get-ChildItem -Path $sharedKioskDir -File | Where-Object { $_.Extension -in '.ps1', '.bat', '.ttf' }

    foreach ($file in $toolFiles) {
        $dstFile = Join-Path $remoteToolsDir $file.Name

        if ($DryRun) {
            Write-Step 'CopyTools' "[DRY] Copiaria $($file.Name) - $remoteToolsDir" 'DRY'
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

    # Tambien copiar scripts A72-especificos
    $a72Files = Get-ChildItem -Path $scriptDir -File | Where-Object { $_.Extension -in '.ps1', '.bat' }
    $a72DestDir = "$remoteToolsDir\A72.TOUTWP"

    Invoke-OnTarget -ScriptBlock {
        param($Dir)
        if (-not (Test-Path $Dir)) {
            New-Item -Path $Dir -ItemType Directory -Force | Out-Null
        }
    } -ArgumentList $a72DestDir

    foreach ($file in $a72Files) {
        $dstFile = Join-Path $a72DestDir $file.Name

        if ($DryRun) {
            Write-Step 'CopyTools' "[DRY] Copiaria A72/$($file.Name)" 'DRY'
        } else {
            try {
                if ($script:isRemote) {
                    Copy-Item -Path $file.FullName -Destination $dstFile -ToSession $script:remoteSession -Force
                } else {
                    Copy-Item -Path $file.FullName -Destination $dstFile -Force
                }
                Write-Step 'CopyTools' "A72/$($file.Name) copiado" 'OK'
            } catch {
                Write-Step 'CopyTools' "Error copiando A72/$($file.Name): $($_.Exception.Message)" 'FAIL'
            }
        }
    }
}

# ============================================================================
#  FASE 12 - BANNER LEGAL ALSTOM (CYBER-06117-C Tableau 4)
# ============================================================================

if (Should-Run 'Banner') {
    Write-Host "`n=== FASE: Banner Legal Alstom (CYBER-06117-C) ===" -ForegroundColor Yellow

    # Texto del banner segun Alstom CYBER-06117-C Tableau 4:
    # "Ce service est reserve aux utilisateurs autorises,
    #  Toutes les activites menees sont suivies et enregistrees par des logs de securite"
    $bannerCaption = 'Avertissement de securite'
    $bannerText    = "Ce service est reserve aux utilisateurs autorises.`nToutes les activites menees sont suivies et enregistrees par des logs de securite."

    if ($DryRun) {
        Write-Step 'Banner' "[DRY] Configuraria banner legal en pantalla de login" 'DRY'
        Write-Step 'Banner' "[DRY] Caption: $bannerCaption" 'DRY'
        Write-Step 'Banner' "[DRY] Text: $bannerText" 'DRY'
    } else {
        $prevValues = Invoke-OnTarget -ScriptBlock {
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            @{
                LegalNoticeCaption = (Get-ItemProperty -Path $regPath -Name 'legalnoticecaption' -ErrorAction SilentlyContinue).legalnoticecaption
                LegalNoticeText    = (Get-ItemProperty -Path $regPath -Name 'legalnoticetext'    -ErrorAction SilentlyContinue).legalnoticetext
            }
        }

        Save-RollbackAction -Type 'RegistrySet' -Data @{
            Path = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            Name = 'legalnoticecaption'
            PreviousValue = $prevValues.LegalNoticeCaption
        }
        Save-RollbackAction -Type 'RegistrySet' -Data @{
            Path = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            Name = 'legalnoticetext'
            PreviousValue = $prevValues.LegalNoticeText
        }

        Invoke-OnTarget -ScriptBlock {
            param($Caption, $Text)
            $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
            Set-ItemProperty -Path $regPath -Name 'legalnoticecaption' -Value $Caption -Type String -Force
            Set-ItemProperty -Path $regPath -Name 'legalnoticetext'    -Value $Text    -Type String -Force
        } -ArgumentList $bannerCaption, $bannerText

        Write-Step 'Banner' "Banner legal Alstom configurado en pantalla de login" 'OK'
        Write-Step 'Banner' "Caption: $bannerCaption" 'INFO'
    }
}

# ============================================================================
#  RESUMEN
# ============================================================================

if (Should-Run 'Summary' -or $Phase -contains 'All') {
    Write-Host "`n=== RESUMEN ===" -ForegroundColor Yellow

    $okCount   = @($results | Where-Object Status -eq 'OK').Count
    $skipCount = @($results | Where-Object Status -eq 'SKIP').Count
    $failCount = @($results | Where-Object Status -eq 'FAIL').Count
    $dryCount  = @($results | Where-Object Status -eq 'DRY').Count

    Write-Host ""
    Write-Host "  Aplicados:    $okCount" -ForegroundColor Green
    Write-Host "  Ya existian:  $skipCount" -ForegroundColor Yellow
    Write-Host "  Errores:      $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'White' })
    if ($DryRun) {
        Write-Host "  Dry-run:      $dryCount" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "  Log guardado: $logFile" -ForegroundColor Gray

    if (-not $DryRun -and $script:rollbackData.Actions.Count -gt 0) {
        $rollbackFile = Join-Path $scriptDir "rollback_client_$timestamp.json"
        $script:rollbackData | ConvertTo-Json -Depth 5 | Set-Content -Path $rollbackFile -Encoding UTF8
        Write-Host "  Rollback:     $rollbackFile" -ForegroundColor Cyan
        Write-Host "  - Para revertir: .\Configure-MAL-Client.ps1 -Rollback '$rollbackFile'" -ForegroundColor Cyan
        if ($script:isRemote) {
            Write-Host "                   -ComputerName $ComputerName -Credential <cred>" -ForegroundColor Cyan
        }
    }

    if ($failCount -gt 0) {
        Write-Host "`n  Acciones con errores:" -ForegroundColor Red
        $results | Where-Object Status -eq 'FAIL' | ForEach-Object {
            Write-Host "    - [$($_.Phase)] $($_.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  CONFIGURACION PC DUAL MAL:" -ForegroundColor Cyan
    Write-Host "  +--------------------------------------------------------+" -ForegroundColor Gray
    Write-Host "  | MAL-IPC-SERVER ($ServerIP)                         |" -ForegroundColor Gray
    Write-Host "  |   Solo TwinCAT Runtime (DIN rail, sin pantalla)       |" -ForegroundColor White
    Write-Host "  |   FW: configurar con Configure-MAL-Server.ps1         |" -ForegroundColor White
    Write-Host "  |                                                        |" -ForegroundColor Gray
    Write-Host "  | MAL-IPC-CLIENT (este equipo)                          |" -ForegroundColor Gray
    Write-Host "  |   Backend .NET - ADS a $ServerIP                   |" -ForegroundColor White
    Write-Host "  |   Frontend - Edge kiosk - $SupervisorUrl          |" -ForegroundColor White
    Write-Host "  |   TwinCAT IDE - RDP a $ServerIP (solo aqf-admin)  |" -ForegroundColor White
    Write-Host "  +--------------------------------------------------------+" -ForegroundColor Gray

    Write-Host ""
    Write-Host "  NOTA: Reiniciar el equipo para aplicar todos los cambios." -ForegroundColor Yellow
    Write-Host "  NOTA: Configurar appsettings.json con NetId del SERVER." -ForegroundColor Yellow
}

# ============================================================================
#  CLEANUP
# ============================================================================

if ($script:remoteSession) {
    Remove-PSSession $script:remoteSession
    Write-Host "  Sesion WinRM cerrada." -ForegroundColor Gray
}
