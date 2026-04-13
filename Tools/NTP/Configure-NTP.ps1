<#
.SYNOPSIS
    Configure NTP (W32Time) on Windows IPC -- Client or Server role.

.DESCRIPTION
    Generic NTP configuration script for industrial IPC deployments.
    Supports two roles:
      - Client: Synchronizes from an external NTP source (e.g., FortiGate, CSP NTP)
      - Server: Synchronizes from the Client IPC (internal /30 network)

    Features:
      - 4 languages: Spanish (SPA), English (ENG), French (FRA), Italian (ITA)
      - Remote execution via WinRM
      - DryRun mode (no changes applied)
      - Rollback support (JSON file)
      - Configurable NTP servers, poll interval, and role

    NTP Chain (typical Alstom architecture):
      CSP NTP (10.8.80.1/10.8.80.2)
        --> FortiGate MAL (NTP relay)
              --> IPC CLIENT (W32Time <- FortiGate)
                    --> IPC SERVER (W32Time <- CLIENT)

.PARAMETER Role
    IPC role: 'Client' or 'Server'.
    - Client: sync from external NTP (FortiGate/CSP)
    - Server: sync from Client IPC (internal network)

.PARAMETER NtpServer
    Primary NTP server IP or hostname.
    Default Client: 10.8.80.1 (CSP NTP)
    Default Server: 192.168.1.162 (IPC CLIENT)

.PARAMETER NtpFallback
    Fallback NTP server IP (optional).
    Default Client: 10.8.80.2

.PARAMETER PollIntervalSeconds
    NTP poll interval in seconds. Default: 900 (15 min).
    Common values: 60, 300, 900, 3600

.PARAMETER Language
    Output language: SPA, ENG, FRA, ITA. Default: SPA.

.PARAMETER ComputerName
    IP or hostname for remote execution via WinRM.

.PARAMETER Credential
    Credentials for remote WinRM session.

.PARAMETER DryRun
    Show changes without applying them.

.PARAMETER Rollback
    Path to rollback JSON file to revert changes.

.EXAMPLE
    # CLIENT -- Local, DryRun (Spanish):
    .\Configure-NTP.ps1 -Role Client -NtpServer 10.8.80.1 -NtpFallback 10.8.80.2 -DryRun

    # CLIENT -- Remote (French):
    .\Configure-NTP.ps1 -Role Client -NtpServer 10.8.80.1 -ComputerName 192.168.2.163 -Credential (Get-Credential) -Language FRA

    # SERVER -- Remote, sync from CLIENT:
    .\Configure-NTP.ps1 -Role Server -NtpServer 192.168.1.162 -ComputerName 192.168.2.161 -Credential (Get-Credential)

    # ROLLBACK:
    .\Configure-NTP.ps1 -Rollback ".\rollback_ntp_20260414.json"

.NOTES
    Requires: Run as Administrator (local) or WinRM enabled (remote)
    Protocol: NTP (UDP 123) -- W32Time service
    Ref: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C
#>

[CmdletBinding()]
param(
    [ValidateSet('Client', 'Server')]
    [string]$Role = 'Client',

    [string]$NtpServer,

    [string]$NtpFallback,

    [ValidateRange(30, 86400)]
    [int]$PollIntervalSeconds = 900,

    [ValidateSet('SPA', 'ENG', 'FRA', 'ITA')]
    [string]$Language = 'SPA',

    [string]$ComputerName,

    [PSCredential]$Credential,

    [switch]$DryRun,

    [string]$Rollback
)

# ============================================================================
#  TRANSLATIONS
# ============================================================================

$T = @{
    SPA = @{
        Title              = "CONFIGURACION NTP (W32Time)"
        RoleClient         = "Cliente -- sincroniza desde NTP externo"
        RoleServer         = "Servidor -- sincroniza desde IPC Cliente"
        NtpSource          = "Servidor NTP"
        NtpFallback        = "NTP secundario"
        PollInterval       = "Intervalo de consulta"
        Seconds            = "segundos"
        Minutes            = "minutos"
        CurrentConfig      = "Configuracion actual"
        ApplyingConfig     = "Aplicando configuracion NTP..."
        ServiceStarting    = "Iniciando servicio W32Time..."
        ServiceRunning     = "Servicio W32Time en ejecucion"
        ServiceStopped     = "Servicio W32Time detenido"
        ServiceNotFound    = "Servicio W32Time no encontrado"
        ConfigApplied      = "Configuracion NTP aplicada correctamente"
        ConfigFailed       = "Error al aplicar configuracion NTP"
        SyncForced         = "Sincronizacion forzada"
        SyncFailed         = "Error al forzar sincronizacion"
        TimeOffset         = "Desfase de tiempo"
        DryMode            = "[SIMULACION] No se aplican cambios"
        DryWouldSet        = "[SIMULACION] Se configuraria"
        RollbackSaved      = "Archivo de rollback guardado"
        RollbackLoaded     = "Ejecutando rollback desde"
        RollbackApplied    = "Rollback aplicado correctamente"
        RollbackFailed     = "Error al aplicar rollback"
        RemoteConnecting   = "Conectando a equipo remoto"
        RemoteConnected    = "Conexion remota establecida"
        RemoteFailed       = "Error de conexion remota"
        RequiresAdmin      = "Este script requiere ejecucion como Administrador"
        PreviousNtpServer  = "Servidor NTP anterior"
        PreviousPoll       = "Intervalo anterior"
        Summary            = "RESUMEN"
        Success            = "OK"
        Skipped            = "OMITIDO"
        Failed             = "ERROR"
        Simulated          = "SIMULADO"
        NoChangesNeeded    = "La configuracion actual ya es correcta"
        Reliable           = "Marcado como fuente confiable (NTP relay para Server)"
        NotReliable        = "No marcado como fuente confiable (solo cliente)"
        RegistryPoll       = "Intervalo de polling configurado en registro"
        W32tmQuery         = "Estado W32Time"
    }
    ENG = @{
        Title              = "NTP CONFIGURATION (W32Time)"
        RoleClient         = "Client -- sync from external NTP"
        RoleServer         = "Server -- sync from Client IPC"
        NtpSource          = "NTP server"
        NtpFallback        = "Fallback NTP"
        PollInterval       = "Poll interval"
        Seconds            = "seconds"
        Minutes            = "minutes"
        CurrentConfig      = "Current configuration"
        ApplyingConfig     = "Applying NTP configuration..."
        ServiceStarting    = "Starting W32Time service..."
        ServiceRunning     = "W32Time service running"
        ServiceStopped     = "W32Time service stopped"
        ServiceNotFound    = "W32Time service not found"
        ConfigApplied      = "NTP configuration applied successfully"
        ConfigFailed       = "Failed to apply NTP configuration"
        SyncForced         = "Synchronization forced"
        SyncFailed         = "Failed to force synchronization"
        TimeOffset         = "Time offset"
        DryMode            = "[DRY RUN] No changes applied"
        DryWouldSet        = "[DRY RUN] Would configure"
        RollbackSaved      = "Rollback file saved"
        RollbackLoaded     = "Executing rollback from"
        RollbackApplied    = "Rollback applied successfully"
        RollbackFailed     = "Failed to apply rollback"
        RemoteConnecting   = "Connecting to remote machine"
        RemoteConnected    = "Remote connection established"
        RemoteFailed       = "Remote connection failed"
        RequiresAdmin      = "This script requires Administrator privileges"
        PreviousNtpServer  = "Previous NTP server"
        PreviousPoll       = "Previous poll interval"
        Summary            = "SUMMARY"
        Success            = "OK"
        Skipped            = "SKIPPED"
        Failed             = "FAILED"
        Simulated          = "SIMULATED"
        NoChangesNeeded    = "Current configuration is already correct"
        Reliable           = "Marked as reliable time source (NTP relay for Server)"
        NotReliable        = "Not marked as reliable (client only)"
        RegistryPoll       = "Poll interval configured in registry"
        W32tmQuery         = "W32Time status"
    }
    FRA = @{
        Title              = "CONFIGURATION NTP (W32Time)"
        RoleClient         = "Client -- synchronisation depuis NTP externe"
        RoleServer         = "Serveur -- synchronisation depuis IPC Client"
        NtpSource          = "Serveur NTP"
        NtpFallback        = "NTP secondaire"
        PollInterval       = "Intervalle de consultation"
        Seconds            = "secondes"
        Minutes            = "minutes"
        CurrentConfig      = "Configuration actuelle"
        ApplyingConfig     = "Application de la configuration NTP..."
        ServiceStarting    = "Demarrage du service W32Time..."
        ServiceRunning     = "Service W32Time en cours d'execution"
        ServiceStopped     = "Service W32Time arrete"
        ServiceNotFound    = "Service W32Time introuvable"
        ConfigApplied      = "Configuration NTP appliquee avec succes"
        ConfigFailed       = "Erreur lors de l'application de la configuration NTP"
        SyncForced         = "Synchronisation forcee"
        SyncFailed         = "Erreur lors de la synchronisation"
        TimeOffset         = "Decalage horaire"
        DryMode            = "[SIMULATION] Aucune modification appliquee"
        DryWouldSet        = "[SIMULATION] Serait configure"
        RollbackSaved      = "Fichier de rollback sauvegarde"
        RollbackLoaded     = "Execution du rollback depuis"
        RollbackApplied    = "Rollback applique avec succes"
        RollbackFailed     = "Erreur lors de l'application du rollback"
        RemoteConnecting   = "Connexion a la machine distante"
        RemoteConnected    = "Connexion distante etablie"
        RemoteFailed       = "Erreur de connexion distante"
        RequiresAdmin      = "Ce script necessite les privileges Administrateur"
        PreviousNtpServer  = "Serveur NTP precedent"
        PreviousPoll       = "Intervalle precedent"
        Summary            = "RESUME"
        Success            = "OK"
        Skipped            = "IGNORE"
        Failed             = "ERREUR"
        Simulated          = "SIMULE"
        NoChangesNeeded    = "La configuration actuelle est deja correcte"
        Reliable           = "Marque comme source fiable (relais NTP pour le Serveur)"
        NotReliable        = "Non marque comme source fiable (client uniquement)"
        RegistryPoll       = "Intervalle de polling configure dans le registre"
        W32tmQuery         = "Etat W32Time"
    }
    ITA = @{
        Title              = "CONFIGURAZIONE NTP (W32Time)"
        RoleClient         = "Client -- sincronizzazione da NTP esterno"
        RoleServer         = "Server -- sincronizzazione da IPC Client"
        NtpSource          = "Server NTP"
        NtpFallback        = "NTP secondario"
        PollInterval       = "Intervallo di polling"
        Seconds            = "secondi"
        Minutes            = "minuti"
        CurrentConfig      = "Configurazione attuale"
        ApplyingConfig     = "Applicazione configurazione NTP..."
        ServiceStarting    = "Avvio servizio W32Time..."
        ServiceRunning     = "Servizio W32Time in esecuzione"
        ServiceStopped     = "Servizio W32Time arrestato"
        ServiceNotFound    = "Servizio W32Time non trovato"
        ConfigApplied      = "Configurazione NTP applicata con successo"
        ConfigFailed       = "Errore nell'applicazione della configurazione NTP"
        SyncForced         = "Sincronizzazione forzata"
        SyncFailed         = "Errore nella sincronizzazione forzata"
        TimeOffset         = "Sfasamento temporale"
        DryMode            = "[SIMULAZIONE] Nessuna modifica applicata"
        DryWouldSet        = "[SIMULAZIONE] Verrebbe configurato"
        RollbackSaved      = "File di rollback salvato"
        RollbackLoaded     = "Esecuzione rollback da"
        RollbackApplied    = "Rollback applicato con successo"
        RollbackFailed     = "Errore nell'applicazione del rollback"
        RemoteConnecting   = "Connessione alla macchina remota"
        RemoteConnected    = "Connessione remota stabilita"
        RemoteFailed       = "Errore di connessione remota"
        RequiresAdmin      = "Questo script richiede privilegi di Amministratore"
        PreviousNtpServer  = "Server NTP precedente"
        PreviousPoll       = "Intervallo precedente"
        Summary            = "RIEPILOGO"
        Success            = "OK"
        Skipped            = "SALTATO"
        Failed             = "ERRORE"
        Simulated          = "SIMULATO"
        NoChangesNeeded    = "La configurazione attuale e' gia' corretta"
        Reliable           = "Segnato come fonte affidabile (relay NTP per il Server)"
        NotReliable        = "Non segnato come fonte affidabile (solo client)"
        RegistryPoll       = "Intervallo di polling configurato nel registro"
        W32tmQuery         = "Stato W32Time"
    }
}

$L = $T[$Language]

# ============================================================================
#  DEFAULTS BY ROLE
# ============================================================================

if (-not $NtpServer) {
    $NtpServer = if ($Role -eq 'Client') { '10.8.80.1' } else { '192.168.1.162' }
}
if (-not $NtpFallback -and $Role -eq 'Client') {
    $NtpFallback = '10.8.80.2'
}

# ============================================================================
#  PREAMBLE
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
$timestamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile    = Join-Path $scriptDir "Configure-NTP_${Role}_$timestamp.log"
$results    = [System.Collections.ArrayList]::new()
$script:isRemote      = [bool]$ComputerName
$script:remoteSession = $null

$script:rollbackData = @{
    Timestamp    = $timestamp
    ComputerName = if ($ComputerName) { $ComputerName } else { $env:COMPUTERNAME }
    Role         = $Role
    Actions      = [System.Collections.ArrayList]::new()
}

# ============================================================================
#  HELPER FUNCTIONS
# ============================================================================

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

function Invoke-OnTarget {
    param([scriptblock]$ScriptBlock, [object[]]$ArgumentList = @())
    if ($script:isRemote) {
        Invoke-Command -Session $script:remoteSession -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
    } else {
        & $ScriptBlock @ArgumentList
    }
}

# ============================================================================
#  BANNER
# ============================================================================

$roleDesc = if ($Role -eq 'Client') { $L.RoleClient } else { $L.RoleServer }
$peerList = if ($NtpFallback) { "$NtpServer,$NtpFallback" } else { $NtpServer }
$pollMin  = [math]::Round($PollIntervalSeconds / 60, 1)

Write-Host ""
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host "    $($L.Title)" -ForegroundColor Cyan
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host "    Role:       $roleDesc" -ForegroundColor Cyan
Write-Host "    $($L.NtpSource):  $peerList" -ForegroundColor Cyan
Write-Host "    $($L.PollInterval): $PollIntervalSeconds $($L.Seconds) ($pollMin $($L.Minutes))" -ForegroundColor Cyan
if ($ComputerName) {
    Write-Host "    Target:     $ComputerName" -ForegroundColor Cyan
}
if ($DryRun) {
    Write-Host "    Mode:       $($L.DryMode)" -ForegroundColor Yellow
}
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
#  ROLLBACK MODE
# ============================================================================

if ($Rollback) {
    if (-not (Test-Path $Rollback)) {
        Write-Host "  ERROR: Rollback file not found: $Rollback" -ForegroundColor Red
        exit 1
    }

    $rbData = Get-Content $Rollback -Raw | ConvertFrom-Json
    Write-Step 'Rollback' "$($L.RollbackLoaded) $Rollback" 'INFO'

    foreach ($action in $rbData.Actions) {
        try {
            $actionType = $action.Type
            $actionData = $action.Data

            Invoke-OnTarget -ScriptBlock {
                param($type, $data)
                switch ($type) {
                    'NtpConfig' {
                        $peerStr = $data.PreviousPeerList
                        w32tm /config /manualpeerlist:$peerStr /syncfromflags:manual /update 2>&1 | Out-Null
                        if ($data.PreviousReliable -eq 'Yes') {
                            w32tm /config /reliable:yes /update 2>&1 | Out-Null
                        } else {
                            w32tm /config /reliable:no /update 2>&1 | Out-Null
                        }
                        Restart-Service w32time -Force
                        w32tm /resync /force 2>&1 | Out-Null
                    }
                    'RegistryPoll' {
                        Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient' `
                            -Name 'SpecialPollInterval' -Value ([int]$data.PreviousPollInterval) -Type DWord
                    }
                }
            } -ArgumentList $actionType, $actionData

            Write-Step 'Rollback' "$actionType : $($L.RollbackApplied)" 'OK'
        } catch {
            Write-Step 'Rollback' "$actionType : $($L.RollbackFailed) -- $($_.Exception.Message)" 'FAIL'
        }
    }
    exit 0
}

# ============================================================================
#  ADMIN CHECK
# ============================================================================

if (-not $script:isRemote -and -not (Test-RunningAsAdmin)) {
    Write-Host ""
    Write-Host "  $($L.RequiresAdmin)" -ForegroundColor Red
    Write-Host ""
    exit 1
}

# ============================================================================
#  REMOTE CONNECTION
# ============================================================================

if ($script:isRemote) {
    Write-Step 'Remote' "$($L.RemoteConnecting) $ComputerName..." 'INFO'

    if (-not $Credential) {
        $Credential = Get-Credential -Message "Admin credentials for $ComputerName"
    }

    # Ensure WinRM is running locally
    $winrmSvc = Get-Service WinRM -ErrorAction SilentlyContinue
    if ($winrmSvc -and $winrmSvc.Status -ne 'Running') {
        Start-Service WinRM
    }

    # Add to TrustedHosts if needed
    $currentHosts = (Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue).Value
    if ($currentHosts -notmatch [regex]::Escape($ComputerName)) {
        $newHosts = if ($currentHosts) { "$currentHosts,$ComputerName" } else { $ComputerName }
        Set-Item WSMan:\localhost\Client\TrustedHosts -Value $newHosts -Force
    }

    try {
        $script:remoteSession = New-PSSession -ComputerName $ComputerName -Credential $Credential -ErrorAction Stop
        Write-Step 'Remote' "$($L.RemoteConnected) ($ComputerName)" 'OK'
    } catch {
        Write-Step 'Remote' "$($L.RemoteFailed): $($_.Exception.Message)" 'FAIL'
        exit 1
    }
}

# ============================================================================
#  PHASE 1 -- READ CURRENT CONFIGURATION
# ============================================================================

Write-Host ""
Write-Host "=== $($L.CurrentConfig) ===" -ForegroundColor Yellow

$currentState = Invoke-OnTarget -ScriptBlock {
    $result = @{}

    # Current NTP peer list
    try {
        $regNtp = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Parameters' -Name 'NtpServer' -ErrorAction SilentlyContinue
        $result.PeerList = if ($regNtp) { $regNtp.NtpServer } else { '(not set)' }

        $regType = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Parameters' -Name 'Type' -ErrorAction SilentlyContinue
        $result.Type = if ($regType) { $regType.Type } else { 'NTP' }
    } catch {
        $result.PeerList = '(error reading)'
        $result.Type = '(error)'
    }

    # Current poll interval
    try {
        $regPoll = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient' -Name 'SpecialPollInterval' -ErrorAction SilentlyContinue
        $result.PollInterval = if ($regPoll) { $regPoll.SpecialPollInterval } else { 0 }
    } catch {
        $result.PollInterval = 0
    }

    # Reliable time source
    try {
        $regReliable = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Config' -Name 'AnnounceFlags' -ErrorAction SilentlyContinue
        $result.Reliable = if ($regReliable -and ($regReliable.AnnounceFlags -band 5) -eq 5) { 'Yes' } else { 'No' }
    } catch {
        $result.Reliable = 'Unknown'
    }

    # W32Time service status
    try {
        $svc = Get-Service w32time -ErrorAction SilentlyContinue
        $result.ServiceStatus = if ($svc) { $svc.Status.ToString() } else { 'NotFound' }
        $result.ServiceStartType = if ($svc) { $svc.StartType.ToString() } else { 'NotFound' }
    } catch {
        $result.ServiceStatus = 'Error'
        $result.ServiceStartType = 'Error'
    }

    return $result
}

Write-Step 'Read' "$($L.NtpSource): $($currentState.PeerList)" 'INFO'
Write-Step 'Read' "$($L.PollInterval): $($currentState.PollInterval) $($L.Seconds)" 'INFO'
Write-Step 'Read' "Reliable: $($currentState.Reliable) | Type: $($currentState.Type)" 'INFO'
Write-Step 'Read' "W32Time: $($currentState.ServiceStatus) ($($currentState.ServiceStartType))" 'INFO'

# ============================================================================
#  PHASE 2 -- APPLY NTP CONFIGURATION
# ============================================================================

Write-Host ""
Write-Host "=== $($L.ApplyingConfig) ===" -ForegroundColor Yellow

# Build peer list in W32Time format (0x9 = SpecialInterval flag)
$w32PeerList = if ($NtpFallback) {
    "$NtpServer,0x9 $NtpFallback,0x9"
} else {
    "$NtpServer,0x9"
}

# Client role = reliable (acts as NTP relay for Server)
# Server role = not reliable (leaf node)
$isReliable = ($Role -eq 'Client')

if ($DryRun) {
    Write-Step 'NTP' "$($L.DryWouldSet): NtpServer = $w32PeerList" 'DRY'
    Write-Step 'NTP' "$($L.DryWouldSet): PollInterval = $PollIntervalSeconds $($L.Seconds)" 'DRY'
    Write-Step 'NTP' "$($L.DryWouldSet): Reliable = $isReliable" 'DRY'
    if ($isReliable) {
        Write-Step 'NTP' "$($L.DryWouldSet): NtpServer provider = Enabled (relay)" 'DRY'
    }
} else {
    # Save rollback data
    Save-RollbackAction -Type 'NtpConfig' -Data @{
        PreviousPeerList = $currentState.PeerList
        PreviousReliable = $currentState.Reliable
    }
    Save-RollbackAction -Type 'RegistryPoll' -Data @{
        PreviousPollInterval = $currentState.PollInterval
    }

    try {
        $configResult = Invoke-OnTarget -ScriptBlock {
            param($peers, $pollSec, $reliable)
            $out = @{ Steps = [System.Collections.ArrayList]::new() }

            # 1. Ensure W32Time service exists and is set to auto-start
            $svc = Get-Service w32time -ErrorAction SilentlyContinue
            if (-not $svc) {
                [void]$out.Steps.Add("W32Time not found -- registering...")
                w32tm /register 2>&1 | Out-Null
            }
            Set-Service w32time -StartupType Automatic -ErrorAction SilentlyContinue
            [void]$out.Steps.Add("W32Time startup: Automatic")

            # 2. Configure NTP peers
            w32tm /config /manualpeerlist:$peers /syncfromflags:manual /update 2>&1 | Out-Null
            [void]$out.Steps.Add("Peers: $peers")

            # 3. Set reliable flag (Client = relay for Server)
            if ($reliable) {
                w32tm /config /reliable:yes /update 2>&1 | Out-Null
                [void]$out.Steps.Add("Reliable: YES (NTP relay)")
            } else {
                w32tm /config /reliable:no /update 2>&1 | Out-Null
                [void]$out.Steps.Add("Reliable: NO (leaf)")
            }

            # 4. Set poll interval in registry
            Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient' `
                -Name 'SpecialPollInterval' -Value $pollSec -Type DWord
            [void]$out.Steps.Add("SpecialPollInterval: $pollSec s")

            # 5. Enable NtpClient provider
            Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpClient' `
                -Name 'Enabled' -Value 1 -Type DWord
            [void]$out.Steps.Add("NtpClient: Enabled")

            # 6. If Client role, also enable NtpServer provider (relay for Server IPC)
            if ($reliable) {
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpServer' `
                    -Name 'Enabled' -Value 1 -Type DWord
                [void]$out.Steps.Add("NtpServer provider: Enabled (relay)")
            }

            # 7. Restart W32Time service
            Restart-Service w32time -Force
            [void]$out.Steps.Add("W32Time restarted")

            # 8. Force sync
            $syncOut = w32tm /resync /force 2>&1
            [void]$out.Steps.Add("Resync: $syncOut")

            $out.Success = $true
            return $out
        } -ArgumentList $w32PeerList, $PollIntervalSeconds, $isReliable

        foreach ($step in $configResult.Steps) {
            Write-Step 'NTP' $step 'OK'
        }

        if ($isReliable) {
            Write-Step 'NTP' $L.Reliable 'OK'
        } else {
            Write-Step 'NTP' $L.NotReliable 'INFO'
        }

        Write-Step 'NTP' $L.ConfigApplied 'OK'

    } catch {
        Write-Step 'NTP' "$($L.ConfigFailed): $($_.Exception.Message)" 'FAIL'
    }
}

# ============================================================================
#  PHASE 3 -- VERIFY
# ============================================================================

Write-Host ""
Write-Host "=== $($L.W32tmQuery) ===" -ForegroundColor Yellow

if (-not $DryRun) {
    $verifyResult = Invoke-OnTarget -ScriptBlock {
        $out = @{}

        # Query current source
        try {
            $source = w32tm /query /source 2>&1
            $out.Source = "$source"
        } catch {
            $out.Source = "(error)"
        }

        # Query status
        try {
            $status = w32tm /query /status 2>&1 | Out-String
            $out.Status = $status.Trim()
        } catch {
            $out.Status = "(error)"
        }

        # Query peers
        try {
            $peers = w32tm /query /peers 2>&1 | Out-String
            $out.Peers = $peers.Trim()
        } catch {
            $out.Peers = "(error)"
        }

        return $out
    }

    Write-Step 'Verify' "$($L.NtpSource): $($verifyResult.Source)" 'OK'
    Write-Host ""
    Write-Host "  --- w32tm /query /status ---" -ForegroundColor DarkGray
    Write-Host $verifyResult.Status -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  --- w32tm /query /peers ---" -ForegroundColor DarkGray
    Write-Host $verifyResult.Peers -ForegroundColor DarkGray
} else {
    Write-Step 'Verify' $L.DryMode 'DRY'
}

# ============================================================================
#  SAVE ROLLBACK FILE
# ============================================================================

if (-not $DryRun -and $script:rollbackData.Actions.Count -gt 0) {
    $rbFile = Join-Path $scriptDir "rollback_ntp_${Role}_$timestamp.json"
    $script:rollbackData | ConvertTo-Json -Depth 5 | Set-Content -Path $rbFile -Encoding UTF8
    Write-Step 'Rollback' "$($L.RollbackSaved): $rbFile" 'OK'
}

# ============================================================================
#  CLEANUP REMOTE SESSION
# ============================================================================

if ($script:remoteSession) {
    Remove-PSSession $script:remoteSession -ErrorAction SilentlyContinue
}

# ============================================================================
#  SUMMARY
# ============================================================================

Write-Host ""
Write-Host "=== $($L.Summary) ===" -ForegroundColor Yellow

$okCount   = @($results | Where-Object Status -eq 'OK').Count
$failCount = @($results | Where-Object Status -eq 'FAIL').Count
$dryCount  = @($results | Where-Object Status -eq 'DRY').Count
$skipCount = @($results | Where-Object Status -eq 'SKIP').Count

Write-Host "  $($L.Success): $okCount  |  $($L.Failed): $failCount  |  $($L.Simulated): $dryCount  |  $($L.Skipped): $skipCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'Green' })

if ($failCount -gt 0) {
    Write-Host ""
    $results | Where-Object Status -eq 'FAIL' | ForEach-Object {
        Write-Host "  X [$($_.Phase)] $($_.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Log: $logFile" -ForegroundColor DarkGray
Write-Host ""
