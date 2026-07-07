<#
.SYNOPSIS
    Configuracion C07-IPC-SERVER - Kiosk Completo (Arquitectura Dual IPC + PLC).

.DESCRIPTION
    Script de configuracion del IPC Server (CP2221-0040) para el proyecto C07.LANBWP.
    Arquitectura Dual:
      - CP2221-0040 (IPC SERVER) → Aquafrisch Supervisor + TwinCAT Engineering (XAE)
      - CX7000 (PLC)             → TwinCAT Runtime (embedded, no Windows)

    El TwinCAT Runtime corre en el CX7000 (PLC embedded).
    El IPC SERVER tiene TwinCAT Engineering para modificaciones del programa PLC.
    Aquafrisch Supervisor comunica con el PLC via ADS a traves del enlace p2p.

    Fases:
      0.  Hostname (C07-IPC-SERVER)
      1.  Cuentas de usuario (aqf, aqf-admin, aqf-advanced)
      2.  Politicas de contrasena
      3.  Auto-logon del usuario kiosk
      4.  Custom Shell (LaunchKiosk.bat - KioskWatchdog.ps1)
      5.  Keyboard Filter (bloqueo de atajos)
      6.  Firewall (2 NICs: NIC1 corporativa RhB, NIC2 p2p hacia PLC CX7000)
      7.  Servicio Windows AquafrischSupervisor
      8.  Deshabilitacion de servicios innecesarios
      9.  Auditoria de eventos
      10. Herramientas Admin (escritorio aqf-admin)
      11. Copiar Tools/Kiosk al IPC
      12. Resumen

    Red:
      NIC1 (corporativa): 192.168.2.165 - Red RhB (acceso clientes, Entra ID)
      NIC2 (p2p aislada): 192.168.1.162/30 - CX7000 PLC (192.168.1.161) - ADS

    Proyecto: C07.LANBWP - Drehgestell-Waschhalle Landquart (RhB)
    Ref: RhB IT Standards v9.0.4

.PARAMETER Phase
    Fase(s) a ejecutar. Valores posibles:
      All, Hostname, Accounts, Passwords, AutoLogon, Shell, KeyboardFilter,
      Firewall, Service, DisableServices, Audit, AdminTools, CopyTools, Summary

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

.PARAMETER PlcIP
    IP del CX7000 PLC (enlace p2p NIC2). Default: 192.168.1.161

.PARAMETER NewComputerName
    Nuevo hostname. Si se especifica, renombra el equipo.

.PARAMETER ComputerName
    IP o hostname del IPC SERVER remoto para conectar via WinRM.

.PARAMETER Credential
    Credenciales para conectar al IPC SERVER remoto.

.PARAMETER IdleTimeoutMinutes
    Timeout de inactividad para screensaver. Default: 30

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.PARAMETER Rollback
    Ruta al archivo de rollback JSON para revertir cambios.

.EXAMPLE
    # REMOTO - desde el PC de desarrollo:
    .\Configure-IPC-Server.ps1 -ComputerName 192.168.2.165 -Credential (Get-Credential) -Phase All -DryRun
    .\Configure-IPC-Server.ps1 -ComputerName 192.168.2.165 -Credential (Get-Credential) -PlcIP 192.168.1.161 -Phase All

    # ROLLBACK:
    .\Configure-IPC-Server.ps1 -Rollback ".\rollback_20260706.json" -ComputerName 192.168.2.165 -Credential (Get-Credential)

.NOTES
    Requiere: Ejecutar como Administrador (local) o WinRM habilitado (remoto)
    Ref: RhB IT Standards v9.0.4
    Ref: 04.2-01 - Guia de Hardening y Despliegue Seguro
    Proyecto: C07.LANBWP - Drehgestell-Waschhalle Landquart (RhB)
#>

[CmdletBinding()]
param(
    [ValidateSet('All','Hostname','Accounts','Passwords','AutoLogon','Shell',
                 'KeyboardFilter','Firewall','Service','DisableServices',
                 'TouchKeyboard','Audit','AdminTools','CopyTools','Summary')]
    [string[]]$Phase = @('All'),

    [string]$SupervisorPath = 'C:\Aquafrisch Supervisor',

    [string]$KioskUser = 'aqf',

    [string]$AdminUser = 'aqf-admin',

    [string]$AdvancedUser = 'aqf-advanced',

    [string]$SupervisorUrl = 'https://localhost:5001',

    [string]$PlcIP = '192.168.1.161',

    [string]$NewComputerName,

    [string]$ComputerName,

    [PSCredential]$Credential,

    [int]$IdleTimeoutMinutes = 30,

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
$logFile    = Join-Path $scriptDir "Configure-IPC-Server_$timestamp.log"
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
        $Credential = Get-Credential -Message "Credenciales administrador del C07-IPC-SERVER ($ComputerName)"
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
    Write-Host "  |  C07-IPC-SERVER - ROLLBACK                                 |" -ForegroundColor Magenta
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
Write-Host "  |  AQUAFRISCH - C07-IPC-SERVER - Kiosk Completo              |" -ForegroundColor Cyan
Write-Host "  |  Ref: RhB IT Standards v9.0.4 . IEC 62443                 |" -ForegroundColor Cyan
Write-Host "  |  Proyecto: C07.LANBWP - Drehgestell-Waschhalle Landquart   |" -ForegroundColor Cyan
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
Write-Step 'INIT' "PlcIP (CX7000 ADS): $PlcIP"
Write-Step 'INIT' "SupervisorUrl: $SupervisorUrl"
Write-Step 'INIT' "NOTA: IP corporativa 192.168.2.165 es PROVISIONAL - pendiente confirmacion RhB IT"
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
#  FASE 1 - CUENTAS DE USUARIO
# ============================================================================

if (Should-Run 'Accounts') {
    Write-Host "`n=== FASE: Cuentas de Usuario ===" -ForegroundColor Yellow

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
                -Description 'Admin - mantenimiento y TwinCAT Engineering' -PasswordNeverExpires | Out-Null
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
        Write-Host "  (RhB IT Standards: minimo 10 caracteres, complejidad)" -ForegroundColor Gray
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
            Write-Step 'Accounts' "Cuenta 'Administrator' original deshabilitada" 'OK'
        }
    }

    # --- Asignar avatar Aquafrisch a las cuentas ---
    $avatarScript = Join-Path $sharedKioskDir "Set-UserAvatars.ps1"
    if (Test-Path $avatarScript) {
        Write-Step 'Accounts' "Asignando avatar Aquafrisch a usuarios..." 'INFO'
        try {
            if ($script:isRemote) {
                $remoteAvatarDir = Join-Path $SupervisorPath 'Backend\Tools\Kiosk'
                $remoteWwwRootDir = Join-Path $SupervisorPath 'Backend\wwwroot'

                Invoke-OnTarget -ScriptBlock {
                    param($ToolsDir, $WwwRootDir)
                    foreach ($dir in @($ToolsDir, $WwwRootDir)) {
                        if (-not (Test-Path $dir)) {
                            New-Item -Path $dir -ItemType Directory -Force | Out-Null
                        }
                    }
                } -ArgumentList $remoteAvatarDir, $remoteWwwRootDir

                $localLogoIcon = Join-Path $sharedKioskDir '..\..\wwwroot\LOGO.ico'
                $remoteLogoInWwwroot = Join-Path $remoteWwwRootDir 'LOGO.ico'
                $remoteLogoInTools   = Join-Path $remoteAvatarDir 'LOGO.ico'
                if (Test-Path $localLogoIcon) {
                    Copy-Item -Path $localLogoIcon -Destination $remoteLogoInWwwroot -ToSession $script:remoteSession -Force
                    Copy-Item -Path $localLogoIcon -Destination $remoteLogoInTools -ToSession $script:remoteSession -Force
                }

                $remoteAvatarScript = Join-Path $remoteAvatarDir 'Set-UserAvatars.ps1'
                Copy-Item -Path $avatarScript -Destination $remoteAvatarScript -ToSession $script:remoteSession -Force

                Invoke-Command -Session $script:remoteSession -ScriptBlock {
                    param($AvatarPath, $AvatarImagePath, $KioskUser, $AdminUser, $AdvancedUser)
                    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
                    & $AvatarPath -ImagePath $AvatarImagePath -Users @($KioskUser, $AdminUser, $AdvancedUser)
                } -ArgumentList $remoteAvatarScript, $remoteLogoInTools, $KioskUser, $AdminUser, $AdvancedUser
            } else {
                & $avatarScript -Users @($KioskUser, $AdminUser, $AdvancedUser)
            }
        }
        catch {
            Write-Step 'Accounts' "Avatar: $($_.Exception.Message)" 'WARN'
        }
    }

    # --- Aplicar wallpaper por defecto (Server.png) a usuarios de escritorio ---
    $wallpaperScript = Join-Path $sharedKioskDir "Set-Wallpaper.ps1"
    $serverWallpaper = Join-Path $sharedKioskDir "Server.png"
    if ((Test-Path $wallpaperScript) -and (Test-Path $serverWallpaper)) {
        Write-Step 'Accounts' "Aplicando wallpaper por defecto (Server.png)..." 'INFO'
        try {
            if ($script:isRemote) {
                $remoteWallpaperDir = Join-Path $SupervisorPath 'Backend\Tools\Kiosk'
                Invoke-OnTarget -ScriptBlock {
                    param($Dir)
                    if (-not (Test-Path $Dir)) { New-Item -Path $Dir -ItemType Directory -Force | Out-Null }
                } -ArgumentList $remoteWallpaperDir

                $remoteWallpaperScript = Join-Path $remoteWallpaperDir 'Set-Wallpaper.ps1'
                $remoteWallpaperImage = Join-Path $remoteWallpaperDir 'Server.png'
                Copy-Item -Path $wallpaperScript -Destination $remoteWallpaperScript -ToSession $script:remoteSession -Force
                Copy-Item -Path $serverWallpaper -Destination $remoteWallpaperImage -ToSession $script:remoteSession -Force

                Invoke-Command -Session $script:remoteSession -ScriptBlock {
                    param($WallpaperPath, $ImagePath, $AdminUser, $AdvancedUser)
                    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
                    & $WallpaperPath -ImagePath $ImagePath -Style Fill -Users @($AdminUser, $AdvancedUser)
                } -ArgumentList $remoteWallpaperScript, $remoteWallpaperImage, $AdminUser, $AdvancedUser
            } else {
                & $wallpaperScript -ImagePath $serverWallpaper -Style Fill -Users @($AdminUser, $AdvancedUser)
            }
        }
        catch {
            Write-Step 'Accounts' "Wallpaper: $($_.Exception.Message)" 'WARN'
        }
    }
}

# ============================================================================
#  FASE 2 - POLITICAS DE CONTRASENA
# ============================================================================

if (Should-Run 'Passwords') {
    Write-Host "`n=== FASE: Politicas de Contrasena ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'Passwords' "[DRY] Aplicaria: MinLen=10, Complexity=On, NoExpiry, Lockout=5/15min" 'DRY'
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

        Write-Step 'Passwords' "Politica de contrasenas aplicada (10 chars, complexity, lockout 5/15min)" 'OK'
    }
}

# ============================================================================
#  FASE 3 - AUTO-LOGON (Modo Kiosco)
# ============================================================================

if (Should-Run 'AutoLogon') {
    Write-Host "`n=== FASE: Auto-Logon ===" -ForegroundColor Yellow

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
#  FASE 4 - CUSTOM SHELL (Kiosk Mode)
# ============================================================================

if (Should-Run 'Shell') {
    Write-Host "`n=== FASE: Custom Shell - Kiosk Mode ===" -ForegroundColor Yellow

    $kioskToolsDir = Join-Path $SupervisorPath 'Backend\Tools\Kiosk'
    $launchScript  = Join-Path $kioskToolsDir 'LaunchKiosk.bat'

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

    # Copy shared kiosk files from parent (Tools/Kiosk/) to IPC
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
        Write-Step 'Shell' "Screensaver idle timeout: $IdleTimeoutMinutes minutos" 'OK'
    } else {
        Write-Step 'Shell' "[DRY] Screensaver idle timeout: $IdleTimeoutMinutes minutos" 'DRY'
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
                    reg load "HKU\$userSID" $ntUserDat 2>$null | Out-Null
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
                    reg unload "HKU\$userSID" 2>$null | Out-Null
                }
            }

            return [PSCustomObject]@{ Status = 'OK'; Msg = "Custom shell - $ShellValue"; PrevShell = $prevShell }
        } -ArgumentList $KioskUser, $launchScript

        if ($null -eq $shellResult -or -not ($shellResult.PSObject.Properties.Name -contains 'Status')) {
            Write-Step 'Shell' "Resultado inesperado al configurar el shell: $shellResult" 'FAIL'
        } elseif ($shellResult.Status -eq 'FAIL') {
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
#  FASE 5 - KEYBOARD FILTER (Modo Kiosco)
# ============================================================================

if (Should-Run 'KeyboardFilter') {
    Write-Host "`n=== FASE: Keyboard Filter ===" -ForegroundColor Yellow

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
#  FASE 6 - FIREWALL (2 NICs: NIC1 corporativa, NIC2 p2p hacia CX7000)
# ============================================================================

if (Should-Run 'Firewall') {
    Write-Host "`n=== FASE: Firewall (2 NICs - Corporativa + PLC p2p) ===" -ForegroundColor Yellow

    if (-not $DryRun) {
        $fwProfiles = Invoke-OnTarget -ScriptBlock {
            Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True -ErrorAction SilentlyContinue | Out-Null
            $p = Get-NetFirewallProfile -Profile Domain,Private,Public -ErrorAction SilentlyContinue |
                Select-Object Name, Enabled
            return $p
        }
        foreach ($p in $fwProfiles) {
            if ($p.Enabled) {
                Write-Step 'Firewall' "Perfil $($p.Name): firewall habilitado" 'OK'
            } else {
                Write-Step 'Firewall' "Perfil $($p.Name): firewall no habilitado" 'FAIL'
            }
        }
    } else {
        Write-Step 'Firewall' "[DRY] Habilitaria firewall en perfiles Domain/Private/Public" 'DRY'
    }

    # --- NIC1 (corporativa RhB): reglas para Supervisor y acceso ---
    $firewallRulesNIC1 = @(
        @{ Name = 'C07 Supervisor HTTPS';     Port = 5001; Protocol = 'TCP'; Action = 'Allow'; Desc = 'HTTPS Supervisor - red corporativa RhB' },
        @{ Name = 'C07 Supervisor HTTP local'; Port = 5000; Protocol = 'TCP'; Action = 'Allow'; RemoteAddr = '127.0.0.1'; Desc = 'HTTP solo localhost' },
        @{ Name = 'C07 Block HTTP remote';     Port = 5000; Protocol = 'TCP'; Action = 'Block'; Desc = 'Bloquear HTTP remoto' },
        @{ Name = 'C07 Block SSH';             Port = 22;   Protocol = 'TCP'; Action = 'Block'; Desc = 'SSH bloqueado' },
        @{ Name = 'C07 Block SMB NetBIOS-NS';  Port = 137; Protocol = 'UDP'; Action = 'Block'; Desc = 'SMB/NetBIOS NS bloqueado' },
        @{ Name = 'C07 Block SMB NetBIOS-DGM'; Port = 138; Protocol = 'UDP'; Action = 'Block'; Desc = 'SMB/NetBIOS DGM bloqueado' },
        @{ Name = 'C07 Block SMB NetBIOS-SSN'; Port = 139; Protocol = 'TCP'; Action = 'Block'; Desc = 'SMB/NetBIOS SSN bloqueado' },
        @{ Name = 'C07 Block SMB Direct';      Port = 445; Protocol = 'TCP'; Action = 'Block'; Desc = 'SMB Direct bloqueado' }
    )

    # --- NIC2 (p2p hacia CX7000 PLC): reglas ADS ---
    $firewallRulesNIC2 = @(
        @{ Name = 'C07 Allow ADS to PLC';          Port = 48898; Protocol = 'TCP'; Action = 'Allow'; RemoteAddr = $PlcIP; Desc = 'TwinCAT ADS - CX7000 PLC' },
        @{ Name = 'C07 Allow Secure ADS to PLC';   Port = 8016;  Protocol = 'TCP'; Action = 'Allow'; RemoteAddr = $PlcIP; Desc = 'TwinCAT Secure ADS (TLS) - CX7000 PLC' },
        @{ Name = 'C07 Allow ADS Discovery PLC';   Port = 48899; Protocol = 'UDP'; Action = 'Allow'; RemoteAddr = $PlcIP; Desc = 'ADS Route Discovery - CX7000 PLC' },
        @{ Name = 'C07 Block ADS Discovery other';  Port = 48899; Protocol = 'UDP'; Action = 'Block'; Desc = 'ADS Discovery bloqueado (otros)' },
        @{ Name = 'C07 Block ADS classic NIC1';     Port = 48898; Protocol = 'TCP'; Action = 'Block'; Desc = 'ADS clasico bloqueado en red corporativa' }
    )

    $allFirewallRules = $firewallRulesNIC1 + $firewallRulesNIC2

    foreach ($rule in $allFirewallRules) {
        if ($DryRun) {
            $addrInfo = if ($rule.ContainsKey('RemoteAddr')) { " from $($rule.RemoteAddr)" } else { '' }
            Write-Step 'Firewall' "[DRY] Crearia: $($rule.Name) ($($rule.Action) $($rule.Protocol)/$($rule.Port)$addrInfo)" 'DRY'
            continue
        }

        $ruleRemoteAddr = if ($rule.ContainsKey('RemoteAddr')) { $rule.RemoteAddr } else { $null }

        $created = Invoke-OnTarget -ScriptBlock {
            param($RuleName, $Port, $Protocol, $Action, $RemoteAddr, $Desc)

            $existing = Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
            if ($existing) {
                Remove-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue | Out-Null
            }

            $params = @{
                Name        = $RuleName
                DisplayName = $RuleName
                Direction   = 'Inbound'
                Protocol    = $Protocol
                LocalPort   = $Port
                Action      = $Action
                Description = $Desc
                Enabled     = 'True'
                Profile     = 'Any'
            }
            if ($RemoteAddr -and $RemoteAddr -ne 'Any') {
                $params['RemoteAddress'] = $RemoteAddr
            }
            New-NetFirewallRule @params | Out-Null
            if ($existing) { return 'UPDATED' }
            return 'CREATED'
        } -ArgumentList $rule.Name, $rule.Port, $rule.Protocol, $rule.Action, $ruleRemoteAddr, $rule.Desc

        if ($created -eq 'UPDATED') {
            Write-Step 'Firewall' "Regla actualizada: $($rule.Name)" 'OK'
        } else {
            Write-Step 'Firewall' "Regla creada: $($rule.Name)" 'OK'
        }
        Save-RollbackAction -Type 'FirewallRuleCreated' -Data @{ RuleName = $rule.Name }
    }

    # Deshabilitar IPv6 y NetBIOS
    if (-not $DryRun) {
        Invoke-OnTarget -ScriptBlock {
            # IPv6 - deshabilitar en todas las NICs
            Get-NetAdapterBinding -ComponentID 'ms_tcpip6' -ErrorAction SilentlyContinue |
                Where-Object { $_.Enabled } |
                ForEach-Object { Disable-NetAdapterBinding -Name $_.Name -ComponentID 'ms_tcpip6' }

            # NetBIOS over TCP/IP - deshabilitar
            $adapters = Get-WmiObject Win32_NetworkAdapterConfiguration -Filter "IPEnabled='True'"
            foreach ($a in $adapters) { $a.SetTcpipNetbios(2) | Out-Null }

            # IP Forwarding - deshabilitar (evitar bridge entre NICs)
            Set-NetIPInterface -Forwarding Disabled -ErrorAction SilentlyContinue
        }
        Write-Step 'Firewall' "IPv6 deshabilitado en todos los adaptadores" 'OK'
        Write-Step 'Firewall' "NetBIOS over TCP/IP deshabilitado" 'OK'
        Write-Step 'Firewall' "IP Forwarding deshabilitado (sin bridge entre NICs)" 'OK'
    } else {
        Write-Step 'Firewall' "[DRY] Deshabilitaria IPv6, NetBIOS over TCP/IP e IP Forwarding" 'DRY'
    }

    Write-Host ""
    Write-Host "  RESUMEN FIREWALL C07-IPC-SERVER:" -ForegroundColor White
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray
    Write-Host "  | NIC1 (Corporativa RhB 192.168.2.165):             |" -ForegroundColor Gray
    Write-Host "  |   ALLOW TCP 5001  - HTTPS Supervisor              |" -ForegroundColor Green
    Write-Host "  |   ALLOW TCP 5000  - HTTP solo localhost            |" -ForegroundColor Green
    Write-Host "  |   BLOCK TCP 5000  - HTTP remoto                    |" -ForegroundColor Red
    Write-Host "  |   BLOCK TCP 22    - SSH                            |" -ForegroundColor Red
    Write-Host "  |   BLOCK 137-139,445 - SMB/NetBIOS                  |" -ForegroundColor Red
    Write-Host "  | NIC2 (P2P CX7000 PLC 192.168.1.162/30):           |" -ForegroundColor Gray
    Write-Host "  |   ALLOW TCP 48898 - ADS desde/hacia $PlcIP    |" -ForegroundColor Green
    Write-Host "  |   ALLOW TCP 8016  - Secure ADS desde/hacia $PlcIP |" -ForegroundColor Green
    Write-Host "  |   ALLOW UDP 48899 - ADS Discovery (solo PLC)      |" -ForegroundColor Green
    Write-Host "  |   BLOCK UDP 48899 - ADS Discovery (otros)         |" -ForegroundColor Red
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray
}

# ============================================================================
#  FASE 7 - SERVICIO WINDOWS (AquafrischSupervisor)
# ============================================================================

if (Should-Run 'Service') {
    Write-Host "`n=== FASE: Servicio Windows AquafrischSupervisor ===" -ForegroundColor Yellow

    $serviceName = 'AquafrischSupervisor'
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
            & sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
            & sc.exe failureflag $ServiceName 1 | Out-Null
            & sc.exe description $ServiceName "Aquafrisch Supervisor - API REST + HMI Web (C07.LANBWP)" | Out-Null

            $sid = (New-Object System.Security.Principal.NTAccount('aqf')).Translate([System.Security.Principal.SecurityIdentifier]).Value 2>$null
            if ($sid) {
                & sc.exe sdset $ServiceName "D:(A;;RPWPCR;;;$sid)(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)" 2>$null | Out-Null
            }

            return @{ Status = 'OK'; Msg = "Servicio '$ServiceName' creado (Automatic Delayed Start, recovery 10/30/60s)" }
        } -ArgumentList $serviceName, $backendExe

        Write-Step 'Service' $svcResult.Msg $svcResult.Status
        if ($svcResult.Status -eq 'OK') {
            Save-RollbackAction -Type 'ServiceCreated' -Data @{ ServiceName = $serviceName }
        }
    }
}

# ============================================================================
#  FASE 8 - DESHABILITAR SERVICIOS INNECESARIOS
# ============================================================================

if (Should-Run 'DisableServices') {
    Write-Host "`n=== FASE: Deshabilitar Servicios Innecesarios ===" -ForegroundColor Yellow

    $servicesToDisable = @(
        'XblAuthManager', 'XblGameSave', 'XboxGipSvc', 'XboxNetApiSvc',
        'bthserv', 'MapsBroker', 'lfsvc', 'RetailDemo',
        'WMPNetworkSvc', 'WSearch', 'Fax'
        # ⚠️ TabletInputService NO se deshabilita: es el servicio de Windows que gestiona
        # el teclado en pantalla (touch keyboard). En un IPC táctil es imprescindible para
        # escribir en páginas EXTERNAS que no son nuestra app (ej. login.microsoftonline.com
        # de Entra ID) — nuestra app tiene su propio teclado virtual, pero esas páginas no.
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

            # AutoPlay/AutoRun
            $explorerPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer'
            if (-not (Test-Path $explorerPath)) {
                New-Item -Path $explorerPath -Force | Out-Null
            }
            Set-ItemProperty -Path $explorerPath -Name 'NoDriveTypeAutoRun' -Value 0xFF -Type DWord -Force
            Set-ItemProperty -Path $explorerPath -Name 'NoAutorun' -Value 1 -Type DWord -Force
            $results += @{ Status = 'OK'; Name = 'AutoPlay/AutoRun'; PrevType = $null; Msg = "Deshabilitados" }

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
#  FASE 8b - TECLADO EN PANTALLA (touch keyboard) — IPC táctil sin teclado físico
# ============================================================================
# Windows solo muestra el teclado táctil automáticamente al tocar un campo de
# texto si "EnableDesktopModeAutoInvoke" = 1 en el perfil del usuario. Sin esto,
# nuestra propia app funciona (tiene su teclado virtual en React), pero páginas
# EXTERNAS (ej. login.microsoftonline.com de Entra ID) no muestran ningún
# teclado — el operador se queda sin forma de escribir. Se aplica al perfil
# "Default" (para que cualquier usuario nuevo lo herede) y al perfil del
# usuario kiosko si ya existe.
# Ref: 04.2-01 §23 — Autostart y Modo Kiosco

if (Should-Run 'TouchKeyboard') {
    Write-Host "`n=== FASE: Teclado en pantalla (touch keyboard) ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'TouchKeyboard' "[DRY] Habilitaria EnableDesktopModeAutoInvoke=1 (perfil Default + $KioskUser)" 'DRY'
    } else {
        $tkResult = Invoke-OnTarget -ScriptBlock {
            param($KioskUserName)
            $results = @()

            function Set-TouchKeyboardAutoInvoke {
                param([string]$HiveRoot, [string]$Label)
                try {
                    $keyPath = Join-Path $HiveRoot 'Software\Microsoft\TabletTip\1.7'
                    if (-not (Test-Path $keyPath)) {
                        New-Item -Path $keyPath -Force | Out-Null
                    }
                    Set-ItemProperty -Path $keyPath -Name 'EnableDesktopModeAutoInvoke' -Value 1 -Type DWord -Force
                    return @{ Status = 'OK'; Name = $Label; Msg = 'EnableDesktopModeAutoInvoke=1 aplicado' }
                } catch {
                    return @{ Status = 'FAIL'; Name = $Label; Msg = $_.Exception.Message }
                }
            }

            # 1) Perfil "Default": lo heredará cualquier usuario nuevo (autologon, etc.)
            $defaultHive = 'C:\Users\Default\NTUSER.DAT'
            if (Test-Path $defaultHive) {
                $mounted = $false
                try {
                    if (-not (Test-Path 'HKU:\DefaultTemp')) {
                        & reg.exe load 'HKU\DefaultTemp' $defaultHive 2>$null | Out-Null
                        $mounted = $true
                    }
                    $results += Set-TouchKeyboardAutoInvoke -HiveRoot 'Registry::HKEY_USERS\DefaultTemp' -Label 'Perfil Default'
                } finally {
                    if ($mounted) {
                        [gc]::Collect(); [gc]::WaitForPendingFinalizers()
                        & reg.exe unload 'HKU\DefaultTemp' 2>$null | Out-Null
                    }
                }
            } else {
                $results += @{ Status = 'SKIP'; Name = 'Perfil Default'; Msg = "No encontrado: $defaultHive" }
            }

            # 2) Usuario kiosko actual (si el perfil ya existe y está cargado, p.ej. sesión activa)
            try {
                $sid = (New-Object System.Security.Principal.NTAccount($KioskUserName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
                if (Test-Path "Registry::HKEY_USERS\$sid") {
                    $results += Set-TouchKeyboardAutoInvoke -HiveRoot "Registry::HKEY_USERS\$sid" -Label "Usuario $KioskUserName (sesión activa)"
                } else {
                    $userHive = "C:\Users\$KioskUserName\NTUSER.DAT"
                    if (Test-Path $userHive) {
                        $mounted = $false
                        try {
                            & reg.exe load "HKU\KioskTemp" $userHive 2>$null | Out-Null
                            $mounted = $true
                            $results += Set-TouchKeyboardAutoInvoke -HiveRoot 'Registry::HKEY_USERS\KioskTemp' -Label "Usuario $KioskUserName"
                        } finally {
                            if ($mounted) {
                                [gc]::Collect(); [gc]::WaitForPendingFinalizers()
                                & reg.exe unload 'HKU\KioskTemp' 2>$null | Out-Null
                            }
                        }
                    } else {
                        $results += @{ Status = 'SKIP'; Name = "Usuario $KioskUserName"; Msg = 'Perfil aún no creado (se aplicará vía perfil Default en el primer login)' }
                    }
                }
            } catch {
                $results += @{ Status = 'SKIP'; Name = "Usuario $KioskUserName"; Msg = $_.Exception.Message }
            }

            return $results
        } -ArgumentList $KioskUser

        foreach ($r in $tkResult) {
            Write-Step 'TouchKeyboard' "$($r.Name): $($r.Msg)" $r.Status
        }
        Write-Step 'TouchKeyboard' "Recuerda: TabletInputService debe estar en Manual/Automatic (ya no se deshabilita, ver FASE 8)" 'INFO'
    }
}

# ============================================================================
#  FASE 9 - AUDIT POLICY
# ============================================================================

if (Should-Run 'Audit') {
    Write-Host "`n=== FASE: Audit Policy ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'Audit' "[DRY] Habilitaria auditorias: Logon, Account Logon, Account Management, Object Access, Policy Change, Privilege Use" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            $categories = @(
                @{ Cat = 'Logon/Logoff';       Sub = 'Logon';                  Flags = '/success:enable /failure:enable' },
                @{ Cat = 'Logon/Logoff';       Sub = 'Logoff';                 Flags = '/success:enable' },
                @{ Cat = 'Logon/Logoff';       Sub = 'Special Logon';          Flags = '/success:enable' },
                @{ Cat = 'Account Logon';      Sub = 'Credential Validation';  Flags = '/success:enable /failure:enable' },
                @{ Cat = 'Account Management'; Sub = 'User Account Management'; Flags = '/success:enable /failure:enable' },
                @{ Cat = 'Object Access';      Sub = 'File System';            Flags = '/success:enable /failure:enable' },
                @{ Cat = 'Policy Change';      Sub = 'Audit Policy Change';    Flags = '/success:enable' },
                @{ Cat = 'Policy Change';      Sub = 'Authentication Policy Change'; Flags = '/success:enable' },
                @{ Cat = 'Privilege Use';      Sub = 'Sensitive Privilege Use'; Flags = '/success:enable /failure:enable' }
            )

            foreach ($c in $categories) {
                $cmd = "auditpol /set /subcategory:`"$($c.Sub)`" $($c.Flags)"
                Invoke-Expression $cmd 2>$null
            }
        }
        Write-Step 'Audit' "Audit Policy configurada (9 subcategorias)" 'OK'
    }
}

# ============================================================================
#  FASE 10 - HERRAMIENTAS ADMIN (Escritorio aqf-admin)
# ============================================================================

if (Should-Run 'AdminTools') {
    Write-Host "`n=== FASE: Herramientas Admin ===" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Step 'AdminTools' "[DRY] Copiaria herramientas admin al escritorio de $AdminUser" 'DRY'
    } else {
        $adminToolsResult = Invoke-OnTarget -ScriptBlock {
            param($AdminUser, $SupervisorPath)
            $results = @()

            $adminProfile = (Get-CimInstance Win32_UserProfile |
                Where-Object { $_.LocalPath -match "\\$AdminUser$" }).LocalPath

            if (-not $adminProfile) {
                $results += @{ Status = 'SKIP'; Msg = "Perfil de $AdminUser no encontrado (login al menos una vez)" }
                return $results
            }

            $desktop = Join-Path $adminProfile 'Desktop'
            if (-not (Test-Path $desktop)) {
                New-Item -Path $desktop -ItemType Directory -Force | Out-Null
            }

            # Acceso directo al Supervisor
            $backendDir = Join-Path $SupervisorPath 'Backend'
            $lnkPath = Join-Path $desktop 'Aquafrisch Supervisor.url'
            Set-Content -Path $lnkPath -Value "[InternetShortcut]`nURL=https://localhost:5001`n"
            $results += @{ Status = 'OK'; Msg = "Acceso directo Supervisor creado" }

            # Acceso directo a la carpeta del backend
            $lnkBackend = Join-Path $desktop 'Backend Folder.url'
            Set-Content -Path $lnkBackend -Value "[InternetShortcut]`nURL=file:///$($backendDir -replace '\\','/')`n"
            $results += @{ Status = 'OK'; Msg = "Acceso directo Backend creado" }

            return $results
        } -ArgumentList $AdminUser, $SupervisorPath

        foreach ($r in $adminToolsResult) {
            Write-Step 'AdminTools' $r.Msg $r.Status
        }
    }
}

# ============================================================================
#  FASE 11 - COPIAR TOOLS/KIOSK AL IPC
# ============================================================================

if (Should-Run 'CopyTools') {
    Write-Host "`n=== FASE: Copiar herramientas Kiosk al IPC ===" -ForegroundColor Yellow

    $remoteToolsDir = Join-Path $SupervisorPath 'Backend\Tools\Kiosk'

    $toolFiles = @(
        'Toggle-UsbStorage.ps1',
        'Toggle-UsbStorage.bat',
        'USB-Storage.bat',
        'Apply-KeyboardFilter.ps1',
        'Keyboard-Filter.bat',
        'Apply-UsbBlock.ps1'
    )

    foreach ($file in $toolFiles) {
        $srcFile = Join-Path $sharedKioskDir $file
        $dstFile = Join-Path $remoteToolsDir $file

        if (-not (Test-Path $srcFile)) { continue }

        if ($DryRun) {
            Write-Step 'CopyTools' "[DRY] Copiaria $file - $remoteToolsDir" 'DRY'
        } else {
            if ($script:isRemote) {
                Invoke-OnTarget -ScriptBlock {
                    param($Dir)
                    if (-not (Test-Path $Dir)) { New-Item -Path $Dir -ItemType Directory -Force | Out-Null }
                } -ArgumentList $remoteToolsDir

                Copy-Item -Path $srcFile -Destination $dstFile -ToSession $script:remoteSession -Force
            } else {
                if ($srcFile -ne $dstFile) {
                    Copy-Item -Path $srcFile -Destination $dstFile -Force
                }
            }
            Write-Step 'CopyTools' "$file copiado" 'OK'
            Save-RollbackAction -Type 'FileCopied' -Data @{ Destination = $dstFile }
        }
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
        $rollbackFile = Join-Path $scriptDir "rollback_$timestamp.json"
        $script:rollbackData | ConvertTo-Json -Depth 5 | Set-Content -Path $rollbackFile -Encoding UTF8
        Write-Host "  Rollback:     $rollbackFile" -ForegroundColor Cyan
        Write-Host "  - Para revertir: .\Configure-IPC-Server.ps1 -Rollback '$rollbackFile'" -ForegroundColor Cyan
    }

    if ($failCount -gt 0) {
        Write-Host "`n  Acciones con errores:" -ForegroundColor Red
        $results | Where-Object Status -eq 'FAIL' | ForEach-Object {
            Write-Host "    - [$($_.Phase)] $($_.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  ARQUITECTURA C07.LANBWP:" -ForegroundColor White
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray
    Write-Host "  | CP2221-0040 (IPC SERVER)   CX7000 (PLC)           |" -ForegroundColor Gray
    Write-Host "  | - Aquafrisch Supervisor     - TwinCAT Runtime      |" -ForegroundColor Cyan
    Write-Host "  | - TwinCAT Engineering       - Programa PLC         |" -ForegroundColor Cyan
    Write-Host "  | - Kiosk Mode (Edge)                                |" -ForegroundColor Cyan
    Write-Host "  |                                                    |" -ForegroundColor Gray
    Write-Host "  | NIC1: 192.168.2.165 (RhB)  IP: $PlcIP        |" -ForegroundColor Green
    Write-Host "  | (IP NIC1 PROVISIONAL - pendiente RhB IT)       |" -ForegroundColor Yellow
    Write-Host "  | NIC2: 192.168.1.162/30 -------- ADS p2p ----------|" -ForegroundColor Green
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray

    Write-Host ""
    Write-Host "  NOTA: Reiniciar el equipo si se cambio el hostname." -ForegroundColor Yellow
    Write-Host "  NOTA: Configurar ADS Route al CX7000 ($PlcIP) desde TwinCAT Engineering." -ForegroundColor Yellow
    Write-Host "  NOTA: IP corporativa (NIC1) 192.168.2.165 es PROVISIONAL." -ForegroundColor Yellow
    Write-Host "        RhB IT debe confirmar IP, mascara, gateway y DNS definitivos." -ForegroundColor Yellow
}

# ============================================================================
#  CLEANUP
# ============================================================================

if ($script:remoteSession) {
    Remove-PSSession $script:remoteSession
    Write-Host "  Sesion WinRM cerrada." -ForegroundColor Gray
}
