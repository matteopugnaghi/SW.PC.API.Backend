<#
.SYNOPSIS
    Configuracion MAL-IPC-SERVER - Solo Hostname y Firewall.

.DESCRIPTION
    Script minimo para el IPC Server (DIN rail, sin pantalla).
    Solo ejecuta TwinCAT Runtime. Este script configura:
      - Hostname del equipo
      - Reglas de firewall: permitir solo conexiones desde MAL-IPC-CLIENT

    Puertos permitidos desde CLIENT:
      - TCP 48898  (TwinCAT ADS)
      - TCP 8016   (TwinCAT Secure ADS / TLS)
      - UDP 48899  (ADS Route Discovery)
      - TCP 3389   (RDP para TwinCAT IDE remoto)
      - TCP 5985   (WinRM para gestion remota)

    Ref: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C (Cybersecurite Alstom)
    Proyecto: A72.TOUTWP - MAL Toulouse

.PARAMETER Phase
    Fase(s) a ejecutar: All, Hostname, Firewall, Summary

.PARAMETER ClientIP
    IP del MAL-IPC-CLIENT (unica fuente permitida). Default: 192.168.1.162

.PARAMETER NewComputerName
    Nuevo hostname para el SERVER. Default: interactivo.

.PARAMETER ComputerName
    IP o hostname del IPC SERVER remoto para conectar via WinRM.

.PARAMETER Credential
    Credenciales para conectar al IPC SERVER remoto.

.PARAMETER DryRun
    Muestra los cambios sin aplicarlos.

.PARAMETER Rollback
    Ruta al archivo de rollback JSON para revertir cambios.

.EXAMPLE
    .\Configure-MAL-Server.ps1 -ComputerName 192.168.1.161 -Credential (Get-Credential) -ClientIP 192.168.1.162 -Phase All -DryRun
    .\Configure-MAL-Server.ps1 -Rollback ".\rollback_server_20260323.json" -ComputerName 192.168.1.161 -Credential (Get-Credential)
#>

[CmdletBinding()]
param(
    [ValidateSet('All','Hostname','Firewall','Summary')]
    [string[]]$Phase = @('All'),

    [string]$ClientIP = '192.168.1.162',

    [string]$NewComputerName,

    [string]$ComputerName,

    [PSCredential]$Credential,

    [switch]$DryRun,

    [string]$Rollback
)

# ============================================================================
#  PREAMBULO
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
$timestamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile    = Join-Path $scriptDir "Configure-MAL-Server_$timestamp.log"
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
        $Credential = Get-Credential -Message "Credenciales administrador del MAL-IPC-SERVER ($ComputerName)"
    }

    Write-Host "`n  Verificando conexion WinRM a $ComputerName..." -ForegroundColor Cyan

    if (-not (Test-RunningAsAdmin)) {
        Write-Host "`n  ERROR: Este script requiere ejecucion como Administrador." -ForegroundColor Red
        exit 1
    }

    $winrmSvc = Get-Service WinRM -ErrorAction SilentlyContinue
    if ($winrmSvc -and $winrmSvc.Status -ne 'Running') {
        Start-Service WinRM
    }

    $trustedItem = Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue
    $currentTrusted = if ($trustedItem) { $trustedItem.Value } else { '' }
    if ($currentTrusted -notmatch [regex]::Escape($ComputerName)) {
        $newTrusted = if ($currentTrusted) { "$currentTrusted,$ComputerName" } else { $ComputerName }
        Set-Item WSMan:\localhost\Client\TrustedHosts -Value $newTrusted -Force
    }

    try {
        $script:remoteSession = New-PSSession -ComputerName $ComputerName -Credential $Credential -ErrorAction Stop
        Write-Host "  Conectado a $ComputerName (Session ID: $($script:remoteSession.Id))" -ForegroundColor Green
    } catch {
        Write-Host "`n  ERROR: No se pudo conectar a $ComputerName" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "`n  Verificar:" -ForegroundColor Yellow
        Write-Host "    1. WinRM habilitado: Enable-PSRemoting -Force" -ForegroundColor Gray
        Write-Host "    2. Firewall permite WinRM (5985/tcp)" -ForegroundColor Gray
        Write-Host "    3. Credenciales correctas" -ForegroundColor Gray
        exit 1
    }
}

# ============================================================================
#  MODO ROLLBACK
# ============================================================================

if ($Rollback) {
    if (-not (Test-Path $Rollback)) {
        Write-Host "`n  ERROR: Archivo de rollback no encontrado: $Rollback" -ForegroundColor Red
        exit 1
    }

    $rbData = Get-Content $Rollback -Raw | ConvertFrom-Json
    Write-Host "`n" -NoNewline
    Write-Host "  +==============================================================+" -ForegroundColor Magenta
    Write-Host "  |  MAL-IPC-SERVER - ROLLBACK                                 |" -ForegroundColor Magenta
    Write-Host "  +==============================================================+" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  Revirtiendo cambios del: $($rbData.Timestamp)" -ForegroundColor Yellow
    Write-Host "  Acciones a revertir: $($rbData.Actions.Count)" -ForegroundColor Yellow
    Write-Host ""

    $rollbackScript = {
        param($Actions)
        foreach ($action in $Actions) {
            try {
                switch ($action.Type) {
                    'FirewallRuleCreated' {
                        Remove-NetFirewallRule -Name $action.Data.RuleName -ErrorAction SilentlyContinue
                        Write-Output "[ROLLBACK] Regla firewall '$($action.Data.RuleName)' eliminada"
                    }
                    'ComputerRenamed' {
                        Rename-Computer -NewName $action.Data.PreviousName -Force
                        Write-Output "[ROLLBACK] Nombre restaurado a '$($action.Data.PreviousName)' (reiniciar)"
                    }
                    default {
                        Write-Output "[ROLLBACK] Tipo desconocido: $($action.Type)"
                    }
                }
            } catch {
                Write-Output "[ROLLBACK][ERROR] $($action.Type) - $($_.Exception.Message)"
            }
        }
    }

    if ($script:isRemote) {
        $output = Invoke-Command -Session $script:remoteSession -ScriptBlock $rollbackScript -ArgumentList (,$rbData.Actions)
    } else {
        $output = & $rollbackScript $rbData.Actions
    }

    $output | ForEach-Object { Write-Host "  $_" -ForegroundColor $(if ($_ -match 'ERROR') { 'Red' } else { 'Green' }) }
    Write-Host "`n  Rollback completado." -ForegroundColor Yellow

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
        exit 1
    }
} elseif (-not $DryRun -and $script:isRemote) {
    $remoteAdmin = Invoke-Command -Session $script:remoteSession -ScriptBlock {
        $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]$identity
        $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    if (-not $remoteAdmin) {
        Write-Host "`n  ERROR: Sin privilegios de administrador en $ComputerName" -ForegroundColor Red
        Remove-PSSession $script:remoteSession
        exit 1
    }
}

Write-Host "`n" -NoNewline
Write-Host "  +==============================================================+" -ForegroundColor Cyan
Write-Host "  |  AQUAFRISCH - MAL-IPC-SERVER - Configuracion Firewall      |" -ForegroundColor Cyan
Write-Host "  |  Ref: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C                |" -ForegroundColor Cyan
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
Write-Step 'INIT' "ClientIP permitida: $ClientIP"
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
        Write-Step 'Hostname' "[DRY] Renombraria: $currentName - $NewComputerName" 'DRY'
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
#  FASE 1 - FIREWALL (Solo conexiones desde CLIENT)
# ============================================================================

if (Should-Run 'Firewall') {
    Write-Host "`n=== FASE: Firewall - Solo conexiones desde MAL-IPC-CLIENT ===" -ForegroundColor Yellow

    # Reglas de firewall: permitir solo desde ClientIP
    $firewallRules = @(
        @{ Name = 'MAL-SRV Allow ADS from CLIENT';        Port = 48898; Protocol = 'TCP'; RemoteAddr = $ClientIP; Desc = 'TwinCAT ADS estandar' },
        @{ Name = 'MAL-SRV Allow Secure ADS from CLIENT'; Port = 8016;  Protocol = 'TCP'; RemoteAddr = $ClientIP; Desc = 'TwinCAT Secure ADS (TLS)' },
        @{ Name = 'MAL-SRV Allow RDP from CLIENT';        Port = 3389;  Protocol = 'TCP'; RemoteAddr = $ClientIP; Desc = 'RDP para TwinCAT IDE remoto' },
        @{ Name = 'MAL-SRV Allow ADS Discovery from CLIENT'; Port = 48899; Protocol = 'UDP'; RemoteAddr = $ClientIP; Desc = 'ADS Route Discovery' },
        @{ Name = 'MAL-SRV Allow WinRM from CLIENT';      Port = 5985;  Protocol = 'TCP'; RemoteAddr = $ClientIP; Desc = 'WinRM gestion remota desde CLIENT' }
    )

    foreach ($rule in $firewallRules) {
        if ($DryRun) {
            Write-Step 'Firewall' "[DRY] Crearia: $($rule.Name) (Allow $($rule.Protocol)/$($rule.Port) from $($rule.RemoteAddr))" 'DRY'
            continue
        }

        $created = Invoke-OnTarget -ScriptBlock {
            param($RuleName, $Port, $Protocol, $RemoteAddr, $Desc)
            $existing = Get-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
            if ($existing) { return 'EXISTS' }

            New-NetFirewallRule -Name $RuleName -DisplayName $RuleName `
                -Direction Inbound -Protocol $Protocol -LocalPort $Port `
                -Action Allow -RemoteAddress $RemoteAddr `
                -Description $Desc -Enabled True | Out-Null
            return 'CREATED'
        } -ArgumentList $rule.Name, $rule.Port, $rule.Protocol, $rule.RemoteAddr, $rule.Desc

        if ($created -eq 'EXISTS') {
            Write-Step 'Firewall' "Regla '$($rule.Name)' ya existe" 'SKIP'
        } else {
            Write-Step 'Firewall' "Regla creada: $($rule.Name)" 'OK'
            Save-RollbackAction -Type 'FirewallRuleCreated' -Data @{ RuleName = $rule.Name }
        }
    }

    # Activar perfiles de firewall y configurar default deny
    if ($DryRun) {
        Write-Step 'Firewall' "[DRY] Activaria firewall con Default Inbound: Block" 'DRY'
        Write-Step 'Firewall' "[DRY] Deshabilitaria IPv6 y NetBIOS" 'DRY'
    } else {
        Invoke-OnTarget -ScriptBlock {
            # Activar firewall con default deny inbound
            Set-NetFirewallProfile -Profile Domain,Public,Private `
                -Enabled True -DefaultInboundAction Block -DefaultOutboundAction Allow

            # Deshabilitar IPv6
            Get-NetAdapterBinding -ComponentID 'ms_tcpip6' -ErrorAction SilentlyContinue |
                Where-Object { $_.Enabled } |
                ForEach-Object { Disable-NetAdapterBinding -Name $_.Name -ComponentID 'ms_tcpip6' }

            # Deshabilitar NetBIOS over TCP/IP
            $adapters = Get-WmiObject Win32_NetworkAdapterConfiguration -Filter "IPEnabled='True'"
            foreach ($a in $adapters) { $a.SetTcpipNetbios(2) | Out-Null }
        }

        Write-Step 'Firewall' "Firewall activado: Default Inbound = Block (Any-Any Deny)" 'OK'
        Write-Step 'Firewall' "IPv6 deshabilitado" 'OK'
        Write-Step 'Firewall' "NetBIOS over TCP/IP deshabilitado" 'OK'
    }

    Write-Host ""
    Write-Host "  RESUMEN FIREWALL MAL-IPC-SERVER:" -ForegroundColor White
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray
    Write-Host "  | ALLOW desde $ClientIP solamente:              |" -ForegroundColor Gray
    Write-Host "  |   TCP 48898  - ADS TwinCAT                        |" -ForegroundColor Green
    Write-Host "  |   TCP 8016   - Secure ADS (TLS)                   |" -ForegroundColor Green
    Write-Host "  |   TCP 3389   - RDP (TwinCAT IDE)                  |" -ForegroundColor Green
    Write-Host "  |   UDP 48899  - ADS Discovery                      |" -ForegroundColor Green
    Write-Host "  |   TCP 5985   - WinRM (gestion)                    |" -ForegroundColor Green
    Write-Host "  | BLOCK todo lo demas (Default Deny)                |" -ForegroundColor Red
    Write-Host "  +----------------------------------------------------+" -ForegroundColor Gray
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
        $rollbackFile = Join-Path $scriptDir "rollback_server_$timestamp.json"
        $script:rollbackData | ConvertTo-Json -Depth 5 | Set-Content -Path $rollbackFile -Encoding UTF8
        Write-Host "  Rollback:     $rollbackFile" -ForegroundColor Cyan
        Write-Host "  - Para revertir: .\Configure-MAL-Server.ps1 -Rollback '$rollbackFile'" -ForegroundColor Cyan
    }

    if ($failCount -gt 0) {
        Write-Host "`n  Acciones con errores:" -ForegroundColor Red
        $results | Where-Object Status -eq 'FAIL' | ForEach-Object {
            Write-Host "    - [$($_.Phase)] $($_.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  NOTA: Reiniciar el equipo si se cambio el hostname." -ForegroundColor Yellow
}

# ============================================================================
#  CLEANUP
# ============================================================================

if ($script:remoteSession) {
    Remove-PSSession $script:remoteSession
    Write-Host "  Sesion WinRM cerrada." -ForegroundColor Gray
}
