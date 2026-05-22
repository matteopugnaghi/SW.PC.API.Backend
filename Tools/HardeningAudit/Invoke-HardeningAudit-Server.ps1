<#
.SYNOPSIS
    Auditoria en vivo del hardening del SERVER (Beckhoff C6030) — A72.TOUTWP.

.DESCRIPTION
    Verifica los 51 puntos del Master Hardening (06.7-A72-02 v1.1, Fases 1-8 SERVER)
    + 2 puntos nuevos v1.1 relacionados con NTP relay desde el CLIENT:
        M43-NTP    : SERVER sincronizado contra CLIENT (192.168.1.162)
        M43-FW-NTP : Regla firewall outbound UDP 123 -> CLIENT

    Disenado para ejecutarse LOCALMENTE en el SERVER (RDP), delante del cliente Alstom.
    Misma estetica que Invoke-HardeningAudit.ps1 del CLIENT: salida coloreada
    (PASS/FAIL/WARN/INFO/SKIP), tabla resumen, export JSON + Markdown.

.PARAMETER OutputDir
    Carpeta de salida. Default: .\HardeningAudit-Server_<timestamp>

.PARAMETER NoExport
    Solo salida en consola.

.PARAMETER ClientRelayIP
    IP del CLIENT que actua como NTP relay. Default: 192.168.1.162

.EXAMPLE
    .\Invoke-HardeningAudit-Server.ps1
    # Auditoria completa SERVER + export JSON/MD

.NOTES
    Proyecto    : A72.TOUTWP — MAL Toulouse (Alstom/Tisseo)
    Ref. doc    : 06.7-A72-02 v1.1
    Ref. cliente: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C Rev C
    Requiere    : PowerShell 5.1+ como Administrador, ejecutar EN el SERVER
#>

[CmdletBinding()]
param(
    [string]$OutputDir,
    [switch]$NoExport,
    [string]$ClientRelayIP = '192.168.1.162',
    [ValidateSet('ES','EN')]
    [string]$Language = 'ES'
)

# ============================================================
#  i18n (post-translation dictionary, ES -> EN)
# ============================================================
$script:Lang = $Language
$script:I18N = [ordered]@{
    # Section headers
    'BIOS / TPM / Secure Boot'                                                   = 'BIOS / TPM / Secure Boot'
    'Cuentas y politicas'                                                        = 'Accounts and policies'
    'BitLocker'                                                                  = 'BitLocker'
    'VBS / HVCI'                                                                 = 'VBS / HVCI'
    'Windows Defender'                                                           = 'Windows Defender'
    'AutoPlay / AutoRun'                                                         = 'AutoPlay / AutoRun'
    'Servicios innecesarios'                                                     = 'Unnecessary services'
    'IIS \(debe estar deshabilitado en headless\)'                              = 'IIS (must be disabled on headless)'
    'Red'                                                                        = 'Network'
    'Firewall -- perfiles y postura In/Out'                                      = 'Firewall -- profiles and In/Out posture'
    'Firewall — reglas MAL-\*/IPC-\* \(Inbound \+ Outbound\)'                    = 'Firewall — MAL-*/IPC-* rules (Inbound + Outbound)'
    'Audit Policy'                                                               = 'Audit Policy'
    'NTP — SERVER sync desde CLIENT relay'                                       = 'NTP — SERVER sync from CLIENT relay'
    'Firewall outbound UDP/123 -> CLIENT'                                        = 'Firewall outbound UDP/123 -> CLIENT'
    'UWF'                                                                        = 'UWF'
    'Backup imagen \(BST\)'                                                      = 'Image backup (BST)'
    'Puertos TCP en LISTEN — evidencia para FAT'                                 = 'TCP ports in LISTEN — evidence for FAT'

    # Checks / notes (longest first)
    'TPM presente, listo y habilitado'                                           = 'TPM present, ready and enabled'
    'Secure Boot deshabilitado \(desviacion TwinCAT Kernel Mode\)'               = 'Secure Boot disabled (TwinCAT Kernel Mode deviation)'
    'OFF \(TwinCAT Kernel Mode sin firma WHQL\)'                                 = 'OFF (TwinCAT Kernel Mode without WHQL signature)'
    'Desviacion documentada en checklist v1.1'                                   = 'Deviation documented in checklist v1.1'
    'TwinCAT en RUN no deberia arrancar con SB ON'                               = 'TwinCAT in RUN should not boot with SB ON'
    'No legible \(no UEFI o sin permisos\)'                                      = 'Not readable (no UEFI or no permissions)'
    'Secure Boot'                                                                = 'Secure Boot'
    'Administrator activo \(desviacion documentada — SERVER headless aislado /30\)' = 'Administrator active (documented deviation — headless SERVER isolated /30)'
    'SERVER sin cuentas operacionales — solo admin para mantenimiento'           = 'SERVER without operational accounts — admin only for maintenance'
    'No existe'                                                                  = 'Not found'
    'Password policy SERVER \(desviacion: red aislada /30, sin contacto externo\)' = 'Password policy SERVER (deviation: isolated network /30, no external contact)'
    'Banner disuasivo configurado'                                               = 'Deterrent banner configured'
    'BitLocker OFF \(desviacion aceptable — CYBER-06117-C no lo exige\)'         = 'BitLocker OFF (acceptable deviation — CYBER-06117-C does not require it)'
    'VBS deshabilitado \(requerido TwinCAT Kernel Mode\)'                        = 'VBS disabled (required by TwinCAT Kernel Mode)'
    'HVCI no efectivo \(VBS=0 lo neutraliza\)'                                   = 'HVCI not effective (VBS=0 neutralises it)'
    'sin efecto sin VBS'                                                         = 'no effect without VBS'
    'Defender activo \(AV \+ RTP \+ Service\)'                                   = 'Defender active (AV + RTP + Service)'
    'Exclusiones TwinCAT configuradas'                                           = 'TwinCAT exclusions configured'
    'AutoPlay/AutoRun deshabilitados'                                            = 'AutoPlay/AutoRun disabled'
    'Claves de registro ausentes'                                                = 'Registry keys missing'
    'Servicios innecesarios Stopped/Disabled'                                    = 'Unnecessary services Stopped/Disabled'
    'verificados, todos OK'                                                      = 'checked, all OK'
    'IIS deshabilitado \(feature removed\)'                                      = 'IIS disabled (feature removed)'
    'IIS efectivamente neutralizado \(W3SVC Stopped/Disabled\)'                  = 'IIS effectively neutralised (W3SVC Stopped/Disabled)'
    'Sin puertos 80/443 a la escucha'                                            = 'No ports 80/443 listening'
    'Feature instalada pero servicio bloqueado -- no expone puertos web'         = 'Feature installed but service blocked -- does not expose web ports'
    'IIS deshabilitado'                                                          = 'IIS disabled'
    'Feature Disabled o W3SVC Stopped/Disabled'                                  = 'Feature Disabled or W3SVC Stopped/Disabled'
    'Configuracion IPv4'                                                         = 'IPv4 configuration'
    'IP topologia: 192\.168\.1\.161/30'                                          = 'IP topology: 192.168.1.161/30'
    '192\.168\.1\.161/30 en NIC1'                                                = '192.168.1.161/30 on NIC1'
    'No detectada'                                                               = 'Not detected'
    'IPv6 deshabilitado en todos los adaptadores'                                = 'IPv6 disabled on all adapters'
    'IPv6 deshabilitado'                                                         = 'IPv6 disabled'
    'Aun habilitado en:'                                                         = 'Still enabled on:'
    'NetBIOS deshabilitado \(Mode 2\)'                                           = 'NetBIOS disabled (Mode 2)'
    'Perfil ([A-Za-z]+): enabled \+ default Inbound=Block'                       = 'Profile $1: enabled + default Inbound=Block'
    'Perfiles firewall'                                                          = 'Firewall profiles'
    "Reglas MAL-\*/IPC-\* habilitadas: total=([0-9]+) \(In=([0-9]+) / Out=([0-9]+)\)" = 'MAL-*/IPC-* enabled rules: total=$1 (In=$2 / Out=$3)'
    'Reglas MAL-\* en SERVER'                                                    = 'MAL-* rules on SERVER'
    '0 reglas encontradas'                                                       = '0 rules found'
    "Regla '"                                                                    = "Rule '"
    '(?i)NO ENCONTRADA'                                                          = 'NOT FOUND'
    'PENDIENTE v1.1 — aplicar Apply-NTP-Client-Server.ps1'                       = 'PENDING v1.1 — apply Apply-NTP-Client-Server.ps1'
    'Reglas INBOUND MAL-\*/IPC-\* \(([0-9]+)\)'                                  = 'INBOUND MAL-*/IPC-* rules ($1)'
    'Reglas OUTBOUND MAL-\*/IPC-\* \(([0-9]+)\)'                                 = 'OUTBOUND MAL-*/IPC-* rules ($1)'
    'Reglas INBOUND de terceros \(([0-9]+)\)'                                    = 'Third-party INBOUND rules ($1)'
    'Revisar con auditor que cada regla este justificada o eliminarla antes del SAT' = 'Review with auditor that each rule is justified or remove before SAT'
    'Reglas INBOUND de Sistema Windows \(([0-9]+)\)'                             = 'Windows System INBOUND rules ($1)'
    'Audit Policy CYBER-06117-C Annexe 2 \(subcategorias clave\)'                = 'Audit Policy CYBER-06117-C Annexe 2 (key subcategories)'
    '([0-9]+) subcategorias verificadas'                                         = '$1 subcategories checked'
    'Fallan:'                                                                    = 'Failing:'
    'Ajustar con auditpol /set /subcategory:'                                    = 'Adjust with auditpol /set /subcategory:'
    'Servicio W32Time Running/Automatic'                                         = 'W32Time service Running/Automatic'
    'Servicio no encontrado'                                                     = 'Service not found'
    'Peer NTP configurado contra CLIENT relay'                                   = 'NTP peer configured against CLIENT relay'
    'Acceso denegado o servicio detenido'                                        = 'Access denied or service stopped'
    'Source NTP actual contiene'                                                 = 'Current NTP source contains'
    'Si Source = Local CMOS Clock => SERVER no sincroniza con relay'             = 'If Source = Local CMOS Clock => SERVER does not sync with relay'
    'w32tm /query /status \(stratum \+ last sync\)'                              = 'w32tm /query /status (stratum + last sync)'
    'Ping al CLIENT relay'                                                       = 'Ping to CLIENT relay'
    'Regla outbound UDP/123 ->'                                                  = 'Outbound rule UDP/123 ->'
    '\bexiste\b'                                                                 = 'exists'
    'UWF Filter \(N/A en SERVER aislado /30\)'                                   = 'UWF Filter (N/A on isolated SERVER /30)'
    'UWF aplica solo al CLIENT segun checklist 05.2-02 M46'                      = 'UWF applies only to CLIENT per checklist 05.2-02 M46'
    'Full image backup \(Beckhoff Service Tool\)'                                = 'Full image backup (Beckhoff Service Tool)'
    'PENDIENTE SAT — ultimo paso del hardening'                                  = 'PENDING SAT — last hardening step'
    'Puertos TCP en LISTEN \(evidencia\)'                                        = 'TCP ports in LISTEN (evidence)'
    'Puertos TCP en LISTEN'                                                      = 'TCP ports in LISTEN'

    # Console-only banners
    'RESUMEN DE AUDITORIA SERVER'                                                = 'SERVER AUDIT SUMMARY'
    'FALLOS A REVISAR:'                                                          = 'FAILURES TO REVIEW:'
    'Duracion total:'                                                            = 'Total duration:'
    'Reporte JSON:'                                                              = 'JSON report:'
    'Reporte MD'                                                                 = 'MD report'
    'AUDITORIA HARDENING SERVER — A72.TOUTWP / CYBER-06117-C v1.1'               = 'SERVER HARDENING AUDIT — A72.TOUTWP / CYBER-06117-C v1.1'
    'Auditoria SERVER finalizada\.'                                              = 'SERVER audit completed.'
    'CLIENT relay'                                                               = 'CLIENT relay'

    # Firewall dump labels
    '--- INBOUND \(entrante\) ---'                                               = '--- INBOUND (incoming) ---'
    '--- OUTBOUND \(saliente\) ---'                                              = '--- OUTBOUND (outgoing) ---'
    '\(ninguna regla MAL-\*/IPC-\* en Outbound -- Windows aplica politica Allow por defecto en salida\)' = '(no MAL-*/IPC-* rules in Outbound -- Windows applies default Allow policy for outbound)'
    '--- OTRAS REGLAS INBOUND HABILITADAS \(Sistema Windows: ([0-9]+) / Terceros: ([0-9]+)\) ---' = '--- OTHER ENABLED INBOUND RULES (Windows System: $1 / Third-party: $2) ---'
    '\(ninguna - solo existen reglas MAL-\*/IPC-\* en Inbound\)'                 = '(none - only MAL-*/IPC-* rules exist on Inbound)'
    '>> TERCEROS \(revisar con auditor\):'                                       = '>> THIRD-PARTY (review with auditor):'
    '>> SISTEMA WINDOWS \(([0-9]+) reglas\) - resumen por grupo:'                = '>> WINDOWS SYSTEM ($1 rules) - summary by group:'
    '\(sin grupo\)'                                                              = '(no group)'
    'sin grupo'                                                                  = 'no group'
    'reglas  -'                                                                  = 'rules  -'
    '\(ninguna\)'                                                                = '(none)'
}

function Tr {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    if ($script:Lang -eq 'ES') { return $Text }
    $s = $Text
    foreach ($k in $script:I18N.Keys) {
        try { $s = [regex]::Replace($s, $k, $script:I18N[$k]) } catch {}
    }
    return $s
}

# ============================================================
#  Helpers (mismos que el script CLIENT)
# ============================================================

$script:Results = New-Object System.Collections.Generic.List[object]
$script:StartTime = Get-Date

function Write-Header {
    param([string]$Text, [string]$Color = 'Cyan')
    $Text = Tr $Text
    $line = '=' * 70
    Write-Host ''
    Write-Host $line -ForegroundColor $Color
    Write-Host "  $Text" -ForegroundColor $Color
    Write-Host $line -ForegroundColor $Color
}

function Write-Section {
    param([string]$Text)
    $Text = Tr $Text
    Write-Host ''
    Write-Host "-- $Text " -NoNewline -ForegroundColor Yellow
    Write-Host ('-' * [Math]::Max(1, 66 - $Text.Length)) -ForegroundColor DarkGray
}

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Check,
        [Parameter(Mandatory)][ValidateSet('PASS','FAIL','WARN','INFO','SKIP','NA')][string]$Status,
        [string]$Expected = '',
        [string]$Actual = '',
        [string]$Note = ''
    )
    $Check    = Tr $Check
    $Note     = Tr $Note
    $Expected = Tr $Expected
    $Actual   = Tr $Actual
    $colorMap  = @{ PASS='Green'; FAIL='Red'; WARN='Yellow'; INFO='Cyan'; SKIP='DarkGray'; NA='DarkGray' }
    $symbolMap = @{ PASS='[ OK ]'; FAIL='[FAIL]'; WARN='[WARN]'; INFO='[INFO]'; SKIP='[SKIP]'; NA='[ NA ]' }
    $lblActual   = if ($script:Lang -eq 'EN') { 'actual  ' } else { 'actual  ' }
    $lblExpected = if ($script:Lang -eq 'EN') { 'expected' } else { 'esperado' }
    $lblNote     = if ($script:Lang -eq 'EN') { 'note    ' } else { 'nota    ' }
    Write-Host ("  {0} {1,-12} {2}" -f $symbolMap[$Status], $Id, $Check) -ForegroundColor $colorMap[$Status]
    if ($Actual)   { Write-Host ("           {0}: {1}" -f $lblActual,$Actual)   -ForegroundColor DarkGray }
    if ($Expected -and $Status -in @('FAIL','WARN')) {
        Write-Host ("           {0}: {1}" -f $lblExpected,$Expected) -ForegroundColor DarkGray
    }
    if ($Note)     { Write-Host ("           {0}: {1}" -f $lblNote,$Note)     -ForegroundColor DarkGray }

    $script:Results.Add([PSCustomObject]@{
        Id        = $Id
        Scope     = 'SERVER'
        Category  = $Category
        Check     = $Check
        Status    = $Status
        Expected  = $Expected
        Actual    = $Actual
        Note      = $Note
        Timestamp = (Get-Date).ToString('s')
    })
}

# ============================================================
#  CHECKS — SERVER
# ============================================================

function Test-Bios-Srv {
    Write-Section "BIOS / TPM / Secure Boot"
    $tpm = Get-Tpm -ErrorAction SilentlyContinue
    if ($tpm) {
        $ok = $tpm.TpmPresent -and $tpm.TpmReady -and $tpm.TpmEnabled
        Add-Result -Id 'M08' -Category 'BIOS' -Check 'TPM presente, listo y habilitado' `
            -Status ($(if($ok){'PASS'}else{'FAIL'})) `
            -Expected 'TpmPresent=True, TpmReady=True, TpmEnabled=True' `
            -Actual ("Present={0}, Ready={1}, Enabled={2}" -f $tpm.TpmPresent,$tpm.TpmReady,$tpm.TpmEnabled)
    } else {
        Add-Result -Id 'M08' -Category 'BIOS' -Check 'TPM' -Status 'SKIP' -Note 'Get-Tpm no disponible'
    }

    $sb = $null
    try {
        $sb = Confirm-SecureBootUEFI -ErrorAction Stop 2>$null
    } catch {
        $sb = $null
    }
    if ($null -ne $sb) {
        if ($sb -eq $false) {
            Add-Result -Id 'M07' -Category 'BIOS' -Check 'Secure Boot deshabilitado (desviacion TwinCAT Kernel Mode)' `
                -Status 'WARN' -Expected 'OFF (TwinCAT Kernel Mode sin firma WHQL)' -Actual 'Disabled' `
                -Note 'Desviacion documentada en checklist v1.1'
        } else {
            Add-Result -Id 'M07' -Category 'BIOS' -Check 'Secure Boot' -Status 'FAIL' `
                -Expected 'OFF' -Actual 'Enabled' `
                -Note 'TwinCAT en RUN no deberia arrancar con SB ON'
        }
    } else {
        Add-Result -Id 'M07' -Category 'BIOS' -Check 'Secure Boot' -Status 'SKIP' `
            -Note 'No legible (no UEFI o sin permisos)'
    }
}

function Test-Accounts-Srv {
    Write-Section "Cuentas y politicas"
    $users = Get-LocalUser | Select-Object Name,Enabled

    $admin = $users | Where-Object Name -eq 'Administrator'
    if ($admin) {
        Add-Result -Id 'M11' -Category 'Accounts' `
            -Check 'Administrator activo (desviacion documentada — SERVER headless aislado /30)' `
            -Status 'WARN' -Actual "Enabled=$($admin.Enabled)" `
            -Note 'SERVER sin cuentas operacionales — solo admin para mantenimiento'
    } else {
        Add-Result -Id 'M11' -Category 'Accounts' -Check 'Administrator' -Status 'FAIL' -Actual 'No existe'
    }

    $netOut = net accounts
    if ($netOut) {
        $getLine = { param($pat) $m = $netOut | Select-String $pat | Select-Object -First 1; if ($m) { $m.ToString() } else { '' } }
        $minLen = (& $getLine 'Minimum password length') -replace '[^\d]',''
        $maxAge = (& $getLine 'Maximum password age')    -replace '[^\d]',''
        Add-Result -Id 'M13' -Category 'Accounts' `
            -Check 'Password policy SERVER (desviacion: red aislada /30, sin contacto externo)' `
            -Status 'WARN' -Actual "MinLen=$minLen, MaxAge=$maxAge"
    }

    $banner = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' `
        -Name legalnoticecaption,legalnoticetext -ErrorAction SilentlyContinue
    if ($banner -and $banner.legalnoticecaption) {
        Add-Result -Id 'M-BANNER' -Category 'Accounts' `
            -Check 'Banner disuasivo configurado' -Status 'PASS' `
            -Actual ("'{0}'" -f $banner.legalnoticecaption)
    }
    # Si no esta configurado, no reportar nada: N/A en SERVER headless aislado /30
    # (sin login interactivo de operadores -- ver checklist 05.2-02 M11/M13)
}

function Test-BitLocker-Srv {
    Write-Section "BitLocker"
    $bl = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction SilentlyContinue |
        Select-Object MountPoint,VolumeStatus,ProtectionStatus,EncryptionMethod
    if ($bl) {
        $off = $bl.ProtectionStatus -eq 'Off' -or $bl.ProtectionStatus -eq 0
        Add-Result -Id 'M14' -Category 'BitLocker' `
            -Check 'BitLocker OFF (desviacion aceptable — CYBER-06117-C no lo exige)' `
            -Status ($(if($off){'WARN'}else{'INFO'})) `
            -Actual ("Status={0}, Protection={1}" -f $bl.VolumeStatus,$bl.ProtectionStatus)
    } else {
        Add-Result -Id 'M14' -Category 'BitLocker' -Check 'BitLocker' -Status 'SKIP'
    }
}

function Test-VBS-Srv {
    Write-Section "VBS / HVCI"
    $dg = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard -ErrorAction SilentlyContinue
    if ($dg) {
        $vbsOk = ($dg.VirtualizationBasedSecurityStatus -eq 0)
        Add-Result -Id 'M16' -Category 'OS-Hardening' `
            -Check 'VBS deshabilitado (requerido TwinCAT Kernel Mode)' `
            -Status ($(if($vbsOk){'PASS'}else{'FAIL'})) `
            -Expected 'VirtualizationBasedSecurityStatus = 0' `
            -Actual "VBS=$($dg.VirtualizationBasedSecurityStatus), HVCI=$($dg.CodeIntegrityPolicyEnforcementStatus)"
        Add-Result -Id 'M17' -Category 'OS-Hardening' `
            -Check 'HVCI no efectivo (VBS=0 lo neutraliza)' -Status 'PASS' `
            -Actual "HVCI raw status = $($dg.CodeIntegrityPolicyEnforcementStatus) (sin efecto sin VBS)"
    } else {
        Add-Result -Id 'M16' -Category 'OS-Hardening' -Check 'VBS/HVCI' -Status 'SKIP'
    }
}

function Test-Defender-Srv {
    Write-Section "Windows Defender"
    $st = Get-MpComputerStatus -ErrorAction SilentlyContinue
    if ($st) {
        $ok = $st.AntivirusEnabled -and $st.RealTimeProtectionEnabled -and $st.AMServiceEnabled
        Add-Result -Id 'M24' -Category 'AV' `
            -Check 'Defender activo (AV + RTP + Service)' `
            -Status ($(if($ok){'PASS'}else{'FAIL'})) `
            -Actual ("AV={0}, RTP={1}, Service={2}, Sig={3}" -f $st.AntivirusEnabled,$st.RealTimeProtectionEnabled,$st.AMServiceEnabled,$st.AntivirusSignatureLastUpdated)
        $pref = (Get-MpPreference).ExclusionPath
        if ($pref) {
            Add-Result -Id 'M24b' -Category 'AV' -Check 'Exclusiones TwinCAT configuradas' `
                -Status 'INFO' -Actual ($pref -join '; ')
        }
    } else {
        Add-Result -Id 'M24' -Category 'AV' -Check 'Defender' -Status 'SKIP'
    }
}

function Test-AutoPlay-Srv {
    Write-Section "AutoPlay / AutoRun"
    $reg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer' `
        -Name NoDriveTypeAutoRun,NoAutorun -ErrorAction SilentlyContinue
    if ($reg) {
        $ok = ($reg.NoDriveTypeAutoRun -eq 255) -and ($reg.NoAutorun -eq 1)
        Add-Result -Id 'M26' -Category 'OS-Hardening' `
            -Check 'AutoPlay/AutoRun deshabilitados' `
            -Status ($(if($ok){'PASS'}else{'FAIL'})) `
            -Expected 'NoDriveTypeAutoRun=255, NoAutorun=1' `
            -Actual ("NoDriveTypeAutoRun={0}, NoAutorun={1}" -f $reg.NoDriveTypeAutoRun,$reg.NoAutorun)
    } else {
        Add-Result -Id 'M26' -Category 'OS-Hardening' -Check 'AutoPlay/AutoRun' -Status 'FAIL' `
            -Actual 'Claves de registro ausentes'
    }
}

function Test-Services-Srv {
    Write-Section "Servicios innecesarios"
    $names = 'XblAuthManager','XblGameSave','XboxNetApiSvc','XboxGipSvc','bthserv','MapsBroker','lfsvc','RetailDemo','WSearch','Fax','TabletInputService'
    $rows = $names | ForEach-Object {
        $s = Get-Service -Name $_ -ErrorAction SilentlyContinue
        if ($s) { [PSCustomObject]@{Name=$_;Status=$s.Status.ToString();StartType=$s.StartType.ToString()} }
        else    { [PSCustomObject]@{Name=$_;Status='NOT_FOUND';StartType='N/A'} }
    }
    $bad = $rows | Where-Object { $_.Status -eq 'Running' -or ($_.StartType -ne 'Disabled' -and $_.StartType -ne 'N/A') }
    if (-not $bad) {
        Add-Result -Id 'M28' -Category 'OS-Hardening' `
            -Check 'Servicios innecesarios Stopped/Disabled' -Status 'PASS' `
            -Actual ("{0} verificados, todos OK" -f $rows.Count)
    } else {
        Add-Result -Id 'M28' -Category 'OS-Hardening' `
            -Check 'Servicios innecesarios Stopped/Disabled' -Status 'WARN' `
            -Actual (($bad | ForEach-Object { "$($_.Name)=$($_.Status)/$($_.StartType)" }) -join '; ')
    }
}

function Test-IIS-Srv {
    Write-Section "IIS (debe estar deshabilitado en headless)"
    $iis = Get-WindowsOptionalFeature -Online -FeatureName 'IIS-WebServerRole' -ErrorAction SilentlyContinue
    $svc = Get-Service W3SVC -ErrorAction SilentlyContinue
    if (-not $iis) {
        Add-Result -Id 'M29' -Category 'IIS' -Check 'IIS' -Status 'SKIP'
        return
    }
    $featDisabled = $iis.State -eq 'Disabled'
    $svcDisabled  = $svc -and $svc.Status -eq 'Stopped' -and $svc.StartType -eq 'Disabled'
    if ($featDisabled) {
        Add-Result -Id 'M29' -Category 'IIS' -Check 'IIS deshabilitado (feature removed)' `
            -Status 'PASS' -Expected 'Disabled' -Actual "Feature=$($iis.State)"
    } elseif ($svcDisabled) {
        Add-Result -Id 'M29' -Category 'IIS' -Check 'IIS efectivamente neutralizado (W3SVC Stopped/Disabled)' `
            -Status 'PASS' -Expected 'Sin puertos 80/443 a la escucha' `
            -Actual "Feature=$($iis.State), W3SVC=Stopped/Disabled" `
            -Note 'Feature instalada pero servicio bloqueado -- no expone puertos web'
    } else {
        Add-Result -Id 'M29' -Category 'IIS' -Check 'IIS deshabilitado' `
            -Status 'FAIL' -Expected 'Feature Disabled o W3SVC Stopped/Disabled' `
            -Actual ("Feature={0}, W3SVC Status={1}, StartType={2}" -f $iis.State, $(if($svc){$svc.Status}else{'N/A'}), $(if($svc){$svc.StartType}else{'N/A'}))
    }
}

function Test-Network-Srv {
    Write-Section "Red"
    $ips = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object InterfaceAlias,IPAddress,PrefixLength
    Add-Result -Id 'M-IP' -Category 'Network' -Check 'Configuracion IPv4' -Status 'INFO' `
        -Actual (($ips | ForEach-Object { "$($_.InterfaceAlias)=$($_.IPAddress)/$($_.PrefixLength)" }) -join ' | ')

    # Esperado /30 con 192.168.1.161
    $hasExpected = $ips | Where-Object { $_.IPAddress -eq '192.168.1.161' -and $_.PrefixLength -eq 30 }
    if ($hasExpected) {
        Add-Result -Id 'M-IP-Topo' -Category 'Network' -Check 'IP topologia: 192.168.1.161/30' -Status 'PASS'
    } else {
        Add-Result -Id 'M-IP-Topo' -Category 'Network' -Check 'IP topologia: 192.168.1.161/30' -Status 'WARN' `
            -Expected '192.168.1.161/30 en NIC1' -Actual 'No detectada'
    }

    $ipv6 = Get-NetAdapterBinding -ComponentID 'ms_tcpip6' -ErrorAction SilentlyContinue | Where-Object Enabled
    if ($ipv6) {
        Add-Result -Id 'M32' -Category 'Network' -Check 'IPv6 deshabilitado' -Status 'FAIL' `
            -Actual ("Aun habilitado en: " + (($ipv6 | ForEach-Object Name) -join ', '))
    } else {
        Add-Result -Id 'M32' -Category 'Network' -Check 'IPv6 deshabilitado en todos los adaptadores' -Status 'PASS'
    }

    $nb = Get-CimInstance Win32_NetworkAdapterConfiguration | Where-Object IPEnabled |
        Select-Object Description,TcpipNetbiosOptions
    $bad = $nb | Where-Object { $_.TcpipNetbiosOptions -ne 2 }
    if (-not $bad) {
        Add-Result -Id 'M33' -Category 'Network' -Check 'NetBIOS deshabilitado (Mode 2)' -Status 'PASS'
    } else {
        Add-Result -Id 'M33' -Category 'Network' -Check 'NetBIOS deshabilitado (Mode 2)' -Status 'WARN' `
            -Actual (($bad | ForEach-Object { "$($_.Description)=$($_.TcpipNetbiosOptions)" }) -join '; ')
    }
}

function Test-Firewall-Srv {
    Write-Section "Firewall -- perfiles y postura In/Out"
    try {
        $profiles = Get-NetFirewallProfile -ErrorAction Stop
        foreach ($pr in $profiles) {
            $on   = $pr.Enabled
            $inA  = $pr.DefaultInboundAction
            $outA = $pr.DefaultOutboundAction
            $okPolicy = ($on -eq 'True' -or $on -eq $true) -and ($inA -eq 'Block')
            Add-Result -Id ("M-FW-" + $pr.Name) -Category 'Firewall' `
                -Check ("Perfil {0}: enabled + default Inbound=Block" -f $pr.Name) `
                -Status ($(if($okPolicy){'PASS'}else{'WARN'})) `
                -Expected 'Enabled=True, In=Block, Out=Allow|Block' `
                -Actual ("Enabled={0} In={1} Out={2}" -f $on,$inA,$outA)
        }
    } catch {
        Add-Result -Id 'M-FW-PROFILES' -Category 'Firewall' -Check 'Perfiles firewall' -Status 'WARN' -Note $_.Exception.Message
    }

    Write-Section "Firewall — reglas MAL-*/IPC-* (Inbound + Outbound)"
    $rules = Get-NetFirewallRule -Enabled True |
        Where-Object { $_.DisplayName -like 'MAL*' -or $_.DisplayName -like 'IPC*' } |
        ForEach-Object {
            $p = $_ | Get-NetFirewallPortFilter
            $a = $_ | Get-NetFirewallAddressFilter
            [PSCustomObject]@{
                Name      = $_.DisplayName
                Direction = $_.Direction.ToString()
                Action    = $_.Action.ToString()
                Protocol  = $p.Protocol
                LocalPort = ($p.LocalPort -join ',')
                RemotePort= ($p.RemotePort -join ',')
                RemoteAddr= ($a.RemoteAddress -join ',')
            }
        }
    if (-not $rules) {
        Add-Result -Id 'M-FW' -Category 'Firewall' -Check 'Reglas MAL-* en SERVER' -Status 'FAIL' `
            -Actual '0 reglas encontradas'
        return
    }

    $expected = @(
        @{Name='MAL-SRV Allow ADS from CLIENT';           Dir='Inbound'; Action='Allow'; Port='48898'; PortType='Local'},
        @{Name='MAL-SRV Allow Secure ADS from CLIENT';    Dir='Inbound'; Action='Allow'; Port='8016';  PortType='Local'},
        @{Name='MAL-SRV Allow RDP from CLIENT';           Dir='Inbound'; Action='Allow'; Port='3389';  PortType='Local'},
        @{Name='MAL-SRV Allow ADS Discovery from CLIENT'; Dir='Inbound'; Action='Allow'; Port='48899'; PortType='Local'},
        @{Name='MAL-SRV Allow WinRM from CLIENT';         Dir='Inbound'; Action='Allow'; Port='5985';  PortType='Local'},
        @{Name='MAL-SRV NTP to CLIENT';                   Dir='Outbound';Action='Allow'; Port='123';   PortType='Remote'}
    )

    foreach ($e in $expected) {
        $match = $rules | Where-Object { $_.Name -eq $e.Name }
        $id = 'FW-' + (($e.Name -replace '[^A-Za-z0-9]','').Substring(0,[Math]::Min(20,($e.Name -replace '[^A-Za-z0-9]','').Length)))
        if ($match) {
            $portsList = if ($e.PortType -eq 'Remote') { $match.RemotePort } else { $match.LocalPort }
            $okPort = ($portsList -split ',') -contains $e.Port
            $okAct  = $match.Action -eq $e.Action
            $okDir  = $match.Direction -eq $e.Dir
            $ok     = $okPort -and $okAct -and $okDir
            Add-Result -Id $id -Category 'Firewall' -Check ("Regla '" + $e.Name + "'") `
                -Status ($(if($ok){'PASS'}else{'WARN'})) `
                -Expected ("{0} {1} :{2}" -f $e.Dir,$e.Action,$e.Port) `
                -Actual ("{0} {1} proto={2} local={3} remote={4} from={5}" -f $match.Direction,$match.Action,$match.Protocol,$match.LocalPort,$match.RemotePort,$match.RemoteAddr)
        } else {
            $status = if ($e.Name -eq 'MAL-SRV NTP to CLIENT') { 'FAIL' } else { 'FAIL' }
            $note   = if ($e.Name -eq 'MAL-SRV NTP to CLIENT') { 'PENDIENTE v1.1 — aplicar Apply-NTP-Client-Server.ps1' } else { '' }
            Add-Result -Id $id -Category 'Firewall' -Check ("Regla '" + $e.Name + "'") `
                -Status $status -Expected ("{0} {1} :{2}" -f $e.Dir,$e.Action,$e.Port) `
                -Actual 'NO ENCONTRADA' -Note $note
        }
    }

    $inCount  = @($rules | Where-Object { $_.Direction -eq 'Inbound'  }).Count
    $outCount = @($rules | Where-Object { $_.Direction -eq 'Outbound' }).Count
    $total    = @($rules).Count
    Add-Result -Id 'M-FW-COUNT' -Category 'Firewall' `
        -Check ("Reglas MAL-*/IPC-* habilitadas: total={0} (In={1} / Out={2})" -f $total,$inCount,$outCount) `
        -Status 'INFO' -Actual ("Inbound={0} | Outbound={1}" -f $inCount,$outCount)

    # Volcado completo INBOUND / OUTBOUND -- evidencia para el auditor
    $inb  = $rules | Where-Object Direction -eq 'Inbound'  | Sort-Object Action,Name
    $outb = $rules | Where-Object Direction -eq 'Outbound' | Sort-Object Action,Name

    Write-Host ""
    Write-Host ("  " + (Tr '--- INBOUND (entrante) ---')) -ForegroundColor Cyan
    foreach ($r in $inb) {
        $color = if ($r.Action -eq 'Allow') { 'Green' } else { 'Yellow' }
        Write-Host (("  {0,-7} {1,-5} {2,-6}/{3,-7} from={4,-22} -> {5}") -f `
            $r.Action,'IN',$r.Protocol,$r.LocalPort,$r.RemoteAddr,$r.Name) -ForegroundColor $color
    }
    Write-Host ""
    Write-Host ("  " + (Tr '--- OUTBOUND (saliente) ---')) -ForegroundColor Cyan
    if ($outb) {
        foreach ($r in $outb) {
            $color = if ($r.Action -eq 'Allow') { 'Green' } else { 'Yellow' }
            $portOut = if ($r.RemotePort -and $r.RemotePort -ne 'Any') { $r.RemotePort } else { $r.LocalPort }
            Write-Host (("  {0,-7} {1,-5} {2,-6}/{3,-7} to={4,-22} -> {5}") -f `
                $r.Action,'OUT',$r.Protocol,$portOut,$r.RemoteAddr,$r.Name) -ForegroundColor $color
        }
    } else {
        Write-Host ("  " + (Tr '(ninguna regla MAL-*/IPC-* en Outbound -- Windows aplica politica Allow por defecto en salida)')) -ForegroundColor Gray
    }

    $inbSummary  = ($inb  | ForEach-Object { "{0}/{1}:{2}/{3}<-{4}" -f $_.Action,$_.Protocol,$_.LocalPort,$_.Name,$_.RemoteAddr }) -join ' | '
    $outbSummary = ($outb | ForEach-Object {
        $p = if ($_.RemotePort -and $_.RemotePort -ne 'Any') { $_.RemotePort } else { $_.LocalPort }
        "{0}/{1}:{2}/{3}->{4}" -f $_.Action,$_.Protocol,$p,$_.Name,$_.RemoteAddr
    }) -join ' | '

    Add-Result -Id 'M-FW-INBOUND' -Category 'Firewall' `
        -Check ("Reglas INBOUND MAL-*/IPC-* ({0})" -f $inb.Count) -Status 'INFO' `
        -Actual $inbSummary
    Add-Result -Id 'M-FW-OUTBOUND' -Category 'Firewall' `
        -Check ("Reglas OUTBOUND MAL-*/IPC-* ({0})" -f $outb.Count) -Status 'INFO' `
        -Actual $outbSummary

    # ---------- Resto de reglas INBOUND habilitadas (superficie de ataque) ----------
    $otherInbound = Get-NetFirewallRule -Enabled True -Direction Inbound -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -notlike 'MAL*' -and $_.DisplayName -notlike 'IPC*' } |
        ForEach-Object {
            $pf = $_ | Get-NetFirewallPortFilter -ErrorAction SilentlyContinue
            $af = $_ | Get-NetFirewallAddressFilter -ErrorAction SilentlyContinue
            [PSCustomObject]@{
                Name      = $_.DisplayName
                Group     = $_.Group
                Action    = $_.Action.ToString()
                Protocol  = if ($pf) { ($pf.Protocol  | Select-Object -First 1) } else { 'Any' }
                LocalPort = if ($pf) { (($pf.LocalPort  | Select-Object -First 1)) } else { 'Any' }
                RemoteAddr= if ($af) { (($af.RemoteAddress | Select-Object -First 1)) } else { 'Any' }
                System    = ($_.Group -like '@*')
            }
        }
    $sys   = @($otherInbound | Where-Object  System | Sort-Object Name -Unique)
    $third = @($otherInbound | Where-Object { -not $_.System } | Sort-Object Name -Unique)

    Write-Host ""
    Write-Host ("  " + (Tr ('--- OTRAS REGLAS INBOUND HABILITADAS (Sistema Windows: {0} / Terceros: {1}) ---' -f $sys.Count,$third.Count))) -ForegroundColor Cyan
    if (-not $otherInbound) {
        Write-Host ("  " + (Tr '(ninguna - solo existen reglas MAL-*/IPC-* en Inbound)')) -ForegroundColor Green
    }
    if ($otherInbound -or $true) {

        if ($third.Count -gt 0) {
            Write-Host ("  " + (Tr '>> TERCEROS (revisar con auditor):')) -ForegroundColor Yellow
            foreach ($r in $third) {
                $color = if ($r.Action -eq 'Allow') { 'Yellow' } else { 'DarkYellow' }
                Write-Host (("    {0,-7} {1,-5} /{2,-7} from={3,-25} -> {4}") -f $r.Action,$r.Protocol,$r.LocalPort,$r.RemoteAddr,$r.Name) -ForegroundColor $color
            }
        }
        if ($sys.Count -gt 0) {
            Write-Host ("  " + (Tr ('>> SISTEMA WINDOWS ({0} reglas) - resumen por grupo:' -f $sys.Count))) -ForegroundColor Gray
            $sys | Group-Object Group | Sort-Object Count -Descending | ForEach-Object {
                $g = if ([string]::IsNullOrEmpty($_.Name)) { (Tr '(sin grupo)') } else { $_.Name }
                Write-Host (("    {0,3} " + (Tr 'reglas  -') + "  {1}") -f $_.Count, $g) -ForegroundColor DarkGray
            }
        }

        $thirdSummary = ($third | ForEach-Object { "{0}/{1}:{2}/{3}<-{4}" -f $_.Action,$_.Protocol,$_.LocalPort,$_.Name,$_.RemoteAddr }) -join ' | '
        Add-Result -Id 'M-FW-THIRDPARTY' -Category 'Firewall' `
            -Check ("Reglas INBOUND de terceros ({0})" -f $third.Count) `
            -Status ($(if($third.Count -gt 0){'WARN'}else{'PASS'})) `
            -Actual ($(if($thirdSummary){$thirdSummary}else{'(ninguna)'})) `
            -Note 'Revisar con auditor que cada regla este justificada o eliminarla antes del SAT'
        Add-Result -Id 'M-FW-SYSTEM' -Category 'Firewall' `
            -Check ("Reglas INBOUND de Sistema Windows ({0})" -f $sys.Count) -Status 'INFO' `
            -Actual ((($sys | Group-Object Group | Sort-Object Count -Descending | Select-Object -First 10 | ForEach-Object { "{0}x{1}" -f $_.Count,$(if([string]::IsNullOrEmpty($_.Name)){'(sin grupo)'}else{$_.Name}) }) -join ' | '))
    }
}

function Test-AuditPolicy-Srv {
    Write-Section "Audit Policy"
    $out = auditpol /get /category:* 2>&1
    if (-not $out) {
        Add-Result -Id 'M43' -Category 'Audit' -Check 'auditpol' -Status 'SKIP'
        return
    }
    # Subcategorias REPRESENTATIVAS por categoria (no todas, las clave CYBER-06117-C Annexe 2).
    # Comprobamos esas concretamente para evitar falsos positivos con subcategorias
    # secundarias (IPsec, Removable Storage, Group Membership, etc.) que no son criticas.
    $expected = @(
        @{ Sub='Logon';                       Want='Success and Failure'; Cat='Logon/Logoff'  }
        @{ Sub='Special Logon';               Want='Success and Failure'; Cat='Logon/Logoff'  }
        @{ Sub='File System';                 Want='Failure';             Cat='Object Access' }
        @{ Sub='Audit Policy Change';         Want='Success and Failure'; Cat='Policy Change' }
        @{ Sub='Authentication Policy Change';Want='Success and Failure'; Cat='Policy Change' }
        @{ Sub='Sensitive Privilege Use';     Want='Failure';             Cat='Privilege Use' }
        @{ Sub='Security System Extension';   Want='Success and Failure'; Cat='System'        }
        @{ Sub='System Integrity';            Want='Success and Failure'; Cat='System'        }
        @{ Sub='Other Account Logon Events';  Want='Success and Failure'; Cat='Account Logon' }
    )
    $fails = @()
    foreach ($e in $expected) {
        # Filtrar solo lineas con valor (Success / Failure / No Auditing) para evitar
        # capturar la cabecera de categoria (ej. 'Logon/Logoff' al buscar 'Logon').
        $line = $out |
            Select-String -SimpleMatch $e.Sub |
            Where-Object { $_.ToString() -match '(Success|Failure|No Auditing)' } |
            Select-Object -First 1
        $ok = $false
        if ($line) {
            $txt = $line.ToString()
            if ($e.Want -eq 'Failure') {
                # 'Failure' o 'Success and Failure' ambos cumplen requisito 'Failure'
                $ok = $txt -match '\bFailure\b'
            } else {
                $ok = $txt -match 'Success and Failure'
            }
        }
        if (-not $ok) { $fails += ("{0} [{1}] esperado '{2}'" -f $e.Sub,$e.Cat,$e.Want) }
    }
    if ($fails.Count -eq 0) {
        Add-Result -Id 'M43' -Category 'Audit' `
            -Check 'Audit Policy CYBER-06117-C Annexe 2 (subcategorias clave)' -Status 'PASS' `
            -Actual ("{0} subcategorias verificadas" -f $expected.Count)
    } else {
        Add-Result -Id 'M43' -Category 'Audit' `
            -Check 'Audit Policy CYBER-06117-C Annexe 2 (subcategorias clave)' -Status 'WARN' `
            -Actual ("Fallan: " + ($fails -join '; ')) `
            -Note 'Ajustar con auditpol /set /subcategory:"..." /success:enable /failure:enable'
    }
}

function Test-NTP-Srv {
    Write-Section "NTP — SERVER sync desde CLIENT relay ($ClientRelayIP)"

    $svc = Get-Service W32Time -ErrorAction SilentlyContinue
    if ($svc) {
        $ok = $svc.Status -eq 'Running' -and $svc.StartType -eq 'Automatic'
        Add-Result -Id 'M43-NTP-Svc' -Category 'NTP' `
            -Check 'Servicio W32Time Running/Automatic' `
            -Status ($(if($ok){'PASS'}else{'WARN'})) `
            -Actual "Status=$($svc.Status), StartType=$($svc.StartType)"
    } else {
        Add-Result -Id 'M43-NTP-Svc' -Category 'NTP' -Check 'W32Time' -Status 'FAIL' -Actual 'Servicio no encontrado'
    }

    $cfg = w32tm /query /configuration 2>$null
    if ($cfg) {
        $nm = $cfg | Select-String 'NtpServer' | Select-Object -First 1
        $ntpSrv = if ($nm) { $nm.ToString() } else { '' }
        $hasRelay = $ntpSrv -match [regex]::Escape($ClientRelayIP)
        Add-Result -Id 'M43-NTP-Peer' -Category 'NTP' `
            -Check ("Peer NTP configurado contra CLIENT relay ($ClientRelayIP)") `
            -Status ($(if($hasRelay){'PASS'}else{'FAIL'})) `
            -Expected ("$ClientRelayIP,0x9 (syncfromflags:manual)") `
            -Actual $ntpSrv.Trim() `
            -Note ($(if($hasRelay){''}else{'PENDIENTE v1.1 — aplicar Apply-NTP-Client-Server.ps1'}))
    } else {
        Add-Result -Id 'M43-NTP-Peer' -Category 'NTP' -Check 'w32tm config' -Status 'FAIL' `
            -Note 'Acceso denegado o servicio detenido'
    }

    $src = (w32tm /query /source 2>$null)
    if ($src) {
        $srcTrim = $src.Trim()
        # Aceptar tanto '192.168.1.162' como '192.168.1.162,0x9' (formato con flags)
        $ok = $srcTrim -match [regex]::Escape($ClientRelayIP)
        Add-Result -Id 'M43-NTP-Source' -Category 'NTP' `
            -Check ("Source NTP actual contiene $ClientRelayIP") `
            -Status ($(if($ok){'PASS'}else{'FAIL'})) `
            -Expected $ClientRelayIP -Actual $srcTrim `
            -Note ($(if($ok){''}else{'Si Source = Local CMOS Clock => SERVER no sincroniza con relay'}))
    }

    $status = w32tm /query /status 2>$null
    if ($status) {
        $stm = $status | Select-String 'Stratum'              | Select-Object -First 1
        $lm  = $status | Select-String 'Last Successful Sync' | Select-Object -First 1
        $stratum = if ($stm) { $stm.ToString().Trim() } else { '' }
        $last    = if ($lm)  { $lm.ToString().Trim()  } else { '' }
        Add-Result -Id 'M43-NTP-Status' -Category 'NTP' `
            -Check 'w32tm /query /status (stratum + last sync)' -Status 'INFO' `
            -Actual ("$stratum | $last")
    }

    # Conectividad UDP/123 hacia CLIENT
    $net = Test-NetConnection -ComputerName $ClientRelayIP -InformationLevel Quiet -WarningAction SilentlyContinue
    Add-Result -Id 'M43-NTP-Ping' -Category 'NTP' `
        -Check ("Ping al CLIENT relay ($ClientRelayIP)") `
        -Status ($(if($net){'PASS'}else{'FAIL'})) `
        -Actual "PingSucceeded=$net"
}

function Test-Firewall-NtpOut {
    # Ya verificado en Test-Firewall-Srv mediante regla 'MAL-SRV NTP to CLIENT'.
    # Aqui solo doble check: existe alguna regla outbound UDP 123 -> ClientRelayIP?
    Write-Section "Firewall outbound UDP/123 -> CLIENT"
    $found = Get-NetFirewallRule -Direction Outbound -Action Allow -Enabled True -ErrorAction SilentlyContinue |
        ForEach-Object {
            $p = $_ | Get-NetFirewallPortFilter
            $a = $_ | Get-NetFirewallAddressFilter
            if ($p.Protocol -eq 'UDP' -and ($p.RemotePort -split ',') -contains '123' -and ($a.RemoteAddress -split ',') -contains $ClientRelayIP) {
                $_.DisplayName
            }
        }
    if ($found) {
        Add-Result -Id 'M43-FW-NTP' -Category 'Firewall' `
            -Check ("Regla outbound UDP/123 -> $ClientRelayIP existe") `
            -Status 'PASS' -Actual ($found -join '; ')
    } else {
        Add-Result -Id 'M43-FW-NTP' -Category 'Firewall' `
            -Check ("Regla outbound UDP/123 -> $ClientRelayIP existe") `
            -Status 'FAIL' -Expected "MAL-SRV NTP to CLIENT (Outbound, UDP, RemotePort=123, RemoteAddr=$ClientRelayIP)" `
            -Actual 'No encontrada' -Note 'PENDIENTE v1.1 — aplicar Apply-NTP-Client-Server.ps1'
    }
}

function Test-UWF-Srv {
    Write-Section "UWF"
    # Per checklist 05.2-02 (M46): UWF NO aplica al SERVER aislado /30.
    # Solo se aplica al CLIENT (kiosko expuesto). Reportamos como informativo.
    Add-Result -Id 'M46' -Category 'UWF' `
        -Check 'UWF Filter (N/A en SERVER aislado /30)' -Status 'SKIP' `
        -Note 'UWF aplica solo al CLIENT segun checklist 05.2-02 M46'
}

function Test-Backup-Srv {
    Write-Section "Backup imagen (BST)"
    Add-Result -Id 'M44' -Category 'Backup' `
        -Check 'Full image backup (Beckhoff Service Tool)' -Status 'INFO' `
        -Note 'PENDIENTE SAT — ultimo paso del hardening'
}

function Test-Listening-Srv {
    Write-Section "Puertos TCP en LISTEN — evidencia para FAT"
    try {
        $listening = Get-NetTCPConnection -State Listen -ErrorAction Stop |
            Where-Object { $_.LocalAddress -in '0.0.0.0','::','127.0.0.1','192.168.1.161' } |
            Sort-Object LocalPort -Unique
        $portsList = @()
        foreach ($c in $listening) {
            $proc = try { (Get-Process -Id $c.OwningProcess -ErrorAction Stop).ProcessName } catch { 'N/A' }
            Write-Host ("  TCP {0,-22}:{1,-6}  PID={2,-6}  Proc={3}" -f $c.LocalAddress,$c.LocalPort,$c.OwningProcess,$proc) -ForegroundColor Gray
            $portsList += "$($c.LocalAddress):$($c.LocalPort)/$proc"
        }
        Add-Result -Id 'M-NET-LISTEN' -Category 'Validation' `
            -Check 'Puertos TCP en LISTEN (evidencia)' -Status 'INFO' `
            -Actual ($portsList -join ' | ')
    } catch {
        Add-Result -Id 'M-NET-LISTEN' -Category 'Validation' `
            -Check 'Puertos TCP en LISTEN' -Status 'WARN' -Note $_.Exception.Message
    }
}

# ============================================================
#  RESUMEN
# ============================================================
function Show-Summary {
    Write-Header "RESUMEN DE AUDITORIA SERVER" 'Magenta'
    $script:Results | Group-Object Status | Sort-Object Name | ForEach-Object {
        $color = switch ($_.Name) {
            'PASS' {'Green'} 'FAIL' {'Red'} 'WARN' {'Yellow'}
            'INFO' {'Cyan'} 'SKIP' {'DarkGray'} default {'White'}
        }
        Write-Host ("  {0,-6} {1,4}" -f $_.Name,$_.Count) -ForegroundColor $color
    }
    $fails = $script:Results | Where-Object Status -eq 'FAIL'
    if ($fails) {
        Write-Host ''
        Write-Host ("  " + (Tr 'FALLOS A REVISAR:')) -ForegroundColor Red
        $fails | ForEach-Object { Write-Host ("   - $($_.Id): $($_.Check)") -ForegroundColor Red }
    }
    $elapsed = (Get-Date) - $script:StartTime
    Write-Host ''
    Write-Host (("  " + (Tr 'Duracion total:') + " {0:mm\:ss}") -f $elapsed) -ForegroundColor DarkGray
}

function Export-Report {
    param([string]$OutDir)
    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    $json = Join-Path $OutDir 'hardening-audit-server.json'
    $md   = Join-Path $OutDir 'hardening-audit-server.md'

    $script:Results | ConvertTo-Json -Depth 4 | Set-Content -Path $json -Encoding UTF8

    $sb = [System.Text.StringBuilder]::new()
    if ($script:Lang -eq 'EN') {
        [void]$sb.AppendLine("# Hardening Audit SERVER - A72.TOUTWP (MAL Toulouse)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("- **Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
        [void]$sb.AppendLine("- **Host**: $env:COMPUTERNAME")
        [void]$sb.AppendLine("- **User**: $env:USERNAME")
        [void]$sb.AppendLine("- **Doc ref.**: 06.7-A72-02 v1.1 / CYBER-06117-C Rev C")
        [void]$sb.AppendLine("- **CLIENT NTP relay**: $ClientRelayIP")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("## Summary")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Status | Count |")
        [void]$sb.AppendLine("|--------|-------|")
        $script:Results | Group-Object Status | Sort-Object Name | ForEach-Object {
            [void]$sb.AppendLine("| $($_.Name) | $($_.Count) |")
        }
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("## Detail")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| ID | Cat | Check | Status | Actual | Note |")
        [void]$sb.AppendLine("|----|-----|-------|--------|--------|------|")
    } else {
        [void]$sb.AppendLine("# Hardening Audit SERVER — A72.TOUTWP (MAL Toulouse)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("- **Fecha**: $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
        [void]$sb.AppendLine("- **Host**: $env:COMPUTERNAME")
        [void]$sb.AppendLine("- **Usuario**: $env:USERNAME")
        [void]$sb.AppendLine("- **Ref. doc**: 06.7-A72-02 v1.1 / CYBER-06117-C Rev C")
        [void]$sb.AppendLine("- **CLIENT relay NTP**: $ClientRelayIP")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("## Resumen")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Status | Count |")
        [void]$sb.AppendLine("|--------|-------|")
        $script:Results | Group-Object Status | Sort-Object Name | ForEach-Object {
            [void]$sb.AppendLine("| $($_.Name) | $($_.Count) |")
        }
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("## Detalle")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| ID | Cat | Check | Status | Actual | Nota |")
        [void]$sb.AppendLine("|----|-----|-------|--------|--------|------|")
    }
    foreach ($r in $script:Results) {
        $actual = ($r.Actual -replace '\|','\|' -replace "`r?`n",' ')
        $note   = ($r.Note   -replace '\|','\|' -replace "`r?`n",' ')
        $check  = ($r.Check  -replace '\|','\|')
        [void]$sb.AppendLine("| $($r.Id) | $($r.Category) | $check | **$($r.Status)** | $actual | $note |")
    }
    $sb.ToString() | Set-Content -Path $md -Encoding UTF8

    Write-Host ''
    Write-Host ((Tr 'Reporte JSON:') + " $json") -ForegroundColor Cyan
    Write-Host ((Tr 'Reporte MD') + "  : $md")   -ForegroundColor Cyan
}

# ============================================================
#  MAIN
# ============================================================
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    if ($script:Lang -eq 'EN') {
        Write-Warning "This script must run as Administrator. Some checks will return SKIP/FAIL."
    } else {
        Write-Warning "Este script requiere ejecutarse como Administrador. Algunas verificaciones devolveran SKIP/FAIL."
    }
}

Write-Header "AUDITORIA HARDENING SERVER — A72.TOUTWP / CYBER-06117-C v1.1" 'Magenta'
Write-Host (("  Host          : {0}") -f $env:COMPUTERNAME) -ForegroundColor White
Write-Host (("  User          : {0}") -f $env:USERNAME)     -ForegroundColor White
Write-Host (("  Date          : {0}") -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) -ForegroundColor White
Write-Host (("  " + (Tr 'CLIENT relay') + "  : {0}") -f $ClientRelayIP) -ForegroundColor White
Write-Host (("  Lang          : {0}") -f $script:Lang) -ForegroundColor White

Test-Bios-Srv
Test-Accounts-Srv
Test-BitLocker-Srv
Test-VBS-Srv
Test-Defender-Srv
Test-AutoPlay-Srv
Test-Services-Srv
Test-IIS-Srv
Test-Network-Srv
Test-Firewall-Srv
Test-AuditPolicy-Srv
Test-NTP-Srv
Test-Firewall-NtpOut
Test-UWF-Srv
Test-Backup-Srv
Test-Listening-Srv

Show-Summary

if (-not $NoExport) {
    if (-not $OutputDir) {
        $OutputDir = Join-Path (Get-Location) ("HardeningAudit-Server_{0:yyyyMMdd_HHmmss}" -f (Get-Date))
    }
    Export-Report -OutDir $OutputDir
}

Write-Host ''
if ($script:Lang -eq 'EN') {
    Write-Host "SERVER audit completed." -ForegroundColor Green
} else {
    Write-Host "Auditoria SERVER finalizada." -ForegroundColor Green
}
