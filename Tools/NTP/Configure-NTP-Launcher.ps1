<#
.SYNOPSIS
    Interactive launcher for Configure-NTP.ps1 -- works on any PC / any project.
#>

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ntpScript = Join-Path $scriptDir 'Configure-NTP.ps1'

if (-not (Test-Path $ntpScript)) {
    Write-Host ""
    Write-Host "  ERROR: Configure-NTP.ps1 not found in $scriptDir" -ForegroundColor Red
    Write-Host ""
    return
}

# ============================================================================
#  BANNER
# ============================================================================

Clear-Host
Write-Host ""
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host "    CONFIGURE NTP (W32Time) -- Interactive Launcher" -ForegroundColor Cyan
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
#  1. LANGUAGE
# ============================================================================

Write-Host "  [1] Idioma / Language / Langue / Lingua:" -ForegroundColor Yellow
Write-Host "      1) SPA - Espanol"
Write-Host "      2) ENG - English"
Write-Host "      3) FRA - Francais"
Write-Host "      4) ITA - Italiano"
Write-Host ""
$langChoice = Read-Host "      Select (1-4, default: 1)"
$Language = switch ($langChoice) {
    '2' { 'ENG' }
    '3' { 'FRA' }
    '4' { 'ITA' }
    default { 'SPA' }
}

# Translations for prompts
$P = @{
    SPA = @{
        SelectRole     = "Seleccionar rol del equipo"
        RoleClient     = "CLIENT -- sincroniza desde NTP externo (FortiGate/CSP)"
        RoleServer     = "SERVER -- sincroniza desde IPC Client (red interna)"
        EnterNtp       = "Servidor NTP principal"
        EnterFallback  = "Servidor NTP secundario (dejar vacio para omitir)"
        EnterPoll      = "Intervalo de polling en segundos"
        SelectMode     = "Seleccionar modo de ejecucion"
        ModeDry        = "SIMULACION -- solo muestra cambios, no aplica nada"
        ModeReal       = "REAL -- aplica la configuracion NTP"
        ModeRollback   = "ROLLBACK -- revertir cambios anteriores"
        RemoteQuestion = "Ejecutar en equipo REMOTO?"
        EnterIP        = "IP o hostname del equipo remoto"
        Confirm        = "Confirmar y ejecutar?"
        YesNo          = "(S/N)"
        SelectRollback = "Seleccionar archivo de rollback"
        NoRollback     = "No hay archivos de rollback disponibles"
    }
    ENG = @{
        SelectRole     = "Select machine role"
        RoleClient     = "CLIENT -- sync from external NTP (FortiGate/CSP)"
        RoleServer     = "SERVER -- sync from Client IPC (internal network)"
        EnterNtp       = "Primary NTP server"
        EnterFallback  = "Fallback NTP server (leave empty to skip)"
        EnterPoll      = "Poll interval in seconds"
        SelectMode     = "Select execution mode"
        ModeDry        = "DRY RUN -- show changes only, apply nothing"
        ModeReal       = "REAL -- apply NTP configuration"
        ModeRollback   = "ROLLBACK -- revert previous changes"
        RemoteQuestion = "Execute on REMOTE machine?"
        EnterIP        = "IP or hostname of remote machine"
        Confirm        = "Confirm and execute?"
        YesNo          = "(Y/N)"
        SelectRollback = "Select rollback file"
        NoRollback     = "No rollback files available"
    }
    FRA = @{
        SelectRole     = "Selectionner le role de la machine"
        RoleClient     = "CLIENT -- synchronisation depuis NTP externe (FortiGate/CSP)"
        RoleServer     = "SERVEUR -- synchronisation depuis IPC Client (reseau interne)"
        EnterNtp       = "Serveur NTP principal"
        EnterFallback  = "Serveur NTP secondaire (laisser vide pour ignorer)"
        EnterPoll      = "Intervalle de consultation en secondes"
        SelectMode     = "Selectionner le mode d'execution"
        ModeDry        = "SIMULATION -- affiche les changements sans les appliquer"
        ModeReal       = "REEL -- appliquer la configuration NTP"
        ModeRollback   = "ROLLBACK -- annuler les changements precedents"
        RemoteQuestion = "Executer sur une machine DISTANTE?"
        EnterIP        = "IP ou nom de la machine distante"
        Confirm        = "Confirmer et executer?"
        YesNo          = "(O/N)"
        SelectRollback = "Selectionner le fichier de rollback"
        NoRollback     = "Aucun fichier de rollback disponible"
    }
    ITA = @{
        SelectRole     = "Selezionare il ruolo della macchina"
        RoleClient     = "CLIENT -- sincronizzazione da NTP esterno (FortiGate/CSP)"
        RoleServer     = "SERVER -- sincronizzazione da IPC Client (rete interna)"
        EnterNtp       = "Server NTP principale"
        EnterFallback  = "Server NTP secondario (lasciare vuoto per saltare)"
        EnterPoll      = "Intervallo di polling in secondi"
        SelectMode     = "Selezionare la modalita di esecuzione"
        ModeDry        = "SIMULAZIONE -- mostra i cambiamenti senza applicarli"
        ModeReal       = "REALE -- applicare la configurazione NTP"
        ModeRollback   = "ROLLBACK -- annullare le modifiche precedenti"
        RemoteQuestion = "Eseguire su macchina REMOTA?"
        EnterIP        = "IP o hostname della macchina remota"
        Confirm        = "Confermare ed eseguire?"
        YesNo          = "(S/N)"
        SelectRollback = "Selezionare il file di rollback"
        NoRollback     = "Nessun file di rollback disponibile"
    }
}

$M = $P[$Language]

# ============================================================================
#  2. ROLE
# ============================================================================

Write-Host ""
Write-Host "  [2] $($M.SelectRole):" -ForegroundColor Yellow
Write-Host "      1) $($M.RoleClient)"
Write-Host "      2) $($M.RoleServer)"
Write-Host ""
$roleChoice = Read-Host "      Select (1-2, default: 1)"
$Role = if ($roleChoice -eq '2') { 'Server' } else { 'Client' }

# Defaults by role
$defaultNtp     = if ($Role -eq 'Client') { '10.11.100.122' } else { '192.168.1.162' }
$defaultFallback = if ($Role -eq 'Client') { '' } else { '' }

# ============================================================================
#  3. MODE
# ============================================================================

Write-Host ""
Write-Host "  [3] $($M.SelectMode):" -ForegroundColor Yellow
Write-Host "      1) $($M.ModeDry)" -ForegroundColor Cyan
Write-Host "      2) $($M.ModeReal)" -ForegroundColor Green
Write-Host "      3) $($M.ModeRollback)" -ForegroundColor DarkYellow
Write-Host ""
$modeChoice = Read-Host "      Select (1-3, default: 1)"

# ============================================================================
#  ROLLBACK MODE
# ============================================================================

if ($modeChoice -eq '3') {
    Write-Host ""
    Write-Host "  $($M.SelectRollback):" -ForegroundColor Yellow
    $rbFiles = Get-ChildItem -Path $scriptDir -Filter 'rollback_ntp_*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($rbFiles.Count -eq 0) {
        Write-Host "    $($M.NoRollback)" -ForegroundColor Red
        Write-Host ""
        return
    }
    $i = 1
    foreach ($f in $rbFiles) {
        Write-Host "      $i) $($f.Name)  ($($f.LastWriteTime.ToString('yyyy-MM-dd HH:mm')))" -ForegroundColor DarkGray
        $i++
    }
    Write-Host ""
    $rbChoice = Read-Host "      Select (1-$($rbFiles.Count))"
    $rbIndex = [int]$rbChoice - 1
    if ($rbIndex -lt 0 -or $rbIndex -ge $rbFiles.Count) { $rbIndex = 0 }
    $rbPath = $rbFiles[$rbIndex].FullName

    Write-Host ""
    Write-Host "  Rollback: $($rbFiles[$rbIndex].Name)" -ForegroundColor Yellow
    $confirm = Read-Host "  $($M.Confirm) $($M.YesNo)"
    if ($confirm -match '^[SsYyOo]') {
        & $ntpScript -Rollback $rbPath -Language $Language
    }
    return
}

# ============================================================================
#  4. NTP SERVERS
# ============================================================================

Write-Host ""
Write-Host "  [4] $($M.EnterNtp) (default: $defaultNtp):" -ForegroundColor Yellow
$ntpInput = Read-Host "     "
$NtpServer = if ($ntpInput) { $ntpInput } else { $defaultNtp }

Write-Host ""
Write-Host "  [5] $($M.EnterFallback) (default: $defaultFallback):" -ForegroundColor Yellow
$fbInput = Read-Host "     "
$NtpFallback = if ($fbInput) { $fbInput } elseif ($defaultFallback) { $defaultFallback } else { '' }

# ============================================================================
#  5. POLL INTERVAL
# ============================================================================

Write-Host ""
Write-Host "  [6] $($M.EnterPoll) (default: 900):" -ForegroundColor Yellow
Write-Host "      60=1min  300=5min  900=15min  3600=1h" -ForegroundColor DarkGray
$pollInput = Read-Host "     "
$PollInterval = if ($pollInput -match '^\d+$' -and [int]$pollInput -ge 30) { [int]$pollInput } else { 900 }

# ============================================================================
#  6. REMOTE?
# ============================================================================

Write-Host ""
$remoteInput = Read-Host "  [7] $($M.RemoteQuestion) $($M.YesNo)"
$ComputerName = $null
$Credential = $null
if ($remoteInput -match '^[SsYyOo]') {
    $ipInput = Read-Host "      $($M.EnterIP)"
    if ($ipInput) {
        $ComputerName = $ipInput
        $Credential = Get-Credential -Message "Admin credentials for $ComputerName"
    }
}

# ============================================================================
#  7. CONFIRM
# ============================================================================

$isDry = ($modeChoice -ne '2')

Write-Host ""
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host "    Role:       $Role" -ForegroundColor White
Write-Host "    NTP:        $NtpServer $(if ($NtpFallback) { "+ $NtpFallback" })" -ForegroundColor White
Write-Host "    Poll:       $PollInterval s" -ForegroundColor White
Write-Host "    Language:   $Language" -ForegroundColor White
Write-Host "    Mode:       $(if ($isDry) { 'DRY RUN' } else { 'REAL' })" -ForegroundColor $(if ($isDry) { 'Cyan' } else { 'Green' })
if ($ComputerName) {
Write-Host "    Target:     $ComputerName" -ForegroundColor White
}
Write-Host "  =======================================================" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "  $($M.Confirm) $($M.YesNo)"
if ($confirm -notmatch '^[SsYyOo]') {
    Write-Host ""
    Write-Host "  Cancelled." -ForegroundColor Yellow
    return
}

# ============================================================================
#  8. EXECUTE
# ============================================================================

$params = @{
    Role                = $Role
    NtpServer           = $NtpServer
    PollIntervalSeconds = $PollInterval
    Language            = $Language
}

if ($NtpFallback)   { $params.NtpFallback  = $NtpFallback }
if ($isDry)         { $params.DryRun       = $true }
if ($ComputerName)  { $params.ComputerName = $ComputerName }
if ($Credential)    { $params.Credential   = $Credential }

& $ntpScript @params
