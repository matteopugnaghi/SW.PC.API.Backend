#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Configura el CLIENT (Beckhoff CP2221) como NTP relay entre CSP Alstom y SERVER.

.DESCRIPTION
    Topologia:
        Firewall Alstom (10.11.100.122) <-- corp NIC2 (10.11.100.121) --- CLIENT
        CLIENT NIC1 (192.168.1.162) -- /30 isolated --> SERVER (192.168.1.161)

    SERVER no tiene acceso al upstream NTP (firewall Alstom), por lo que el CLIENT debe:
      1. Sincronizar contra el firewall Alstom 10.11.100.122 (upstream NTP)
      2. Servir NTP en UDP/123 a SERVER (este script)

.NOTES
    Referencia: 06.7-A72-02 v1.1 ANEXO D.5
    Item:      C73-NTP-RELAY
#>

[CmdletBinding()]
param(
    [string]$ServerIP = '192.168.1.161',
    [string[]]$UpstreamPeers = @('10.11.100.122,0x9')
)

$ErrorActionPreference = 'Stop'
function W([string]$m,[string]$c='Gray'){ Write-Host "[NTP-Relay] $m" -ForegroundColor $c }

W "===============================================================" 'Cyan'
W " Configuracion CLIENT como NTP RELAY hacia SERVER ($ServerIP)" 'Cyan'
W "===============================================================" 'Cyan'

# ---------- 1. Servicio W32Time ----------
W "`n[1/5] Habilitando servicio W32Time..." 'Yellow'
Set-Service -Name W32Time -StartupType Automatic
if ((Get-Service W32Time).Status -ne 'Running') { Start-Service W32Time }
W "      OK - W32Time Running/Automatic" 'Green'

# ---------- 2. Upstream peers (CSP) ----------
W "`n[2/5] Configurando upstream peers ($($UpstreamPeers -join ' '))..." 'Yellow'
$peerList = $UpstreamPeers -join ' '
& w32tm /config /manualpeerlist:"$peerList" /syncfromflags:manual /update | Out-Null
W "      OK" 'Green'

# ---------- 3. Activar rol NTP server (relay) ----------
W "`n[3/5] Activando rol NTP Server (AnnounceFlags=5, NtpServer Enabled=1)..." 'Yellow'
$rkCfg = 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Config'
$rkSrv = 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders\NtpServer'
Set-ItemProperty -Path $rkCfg -Name 'AnnounceFlags' -Value 5 -Type DWord
Set-ItemProperty -Path $rkSrv -Name 'Enabled'       -Value 1 -Type DWord
W "      AnnounceFlags = $((Get-ItemProperty $rkCfg).AnnounceFlags)" 'Green'
W "      NtpServer Enabled = $((Get-ItemProperty $rkSrv).Enabled)" 'Green'

# ---------- 4. Reiniciar y resync ----------
W "`n[4/5] Reiniciando W32Time y forzando resync..." 'Yellow'
Restart-Service W32Time
Start-Sleep -Seconds 3
& w32tm /resync /rediscover 2>&1 | Out-Null

$status = & w32tm /query /status 2>&1
$src    = ($status | Select-String 'Source:').ToString().Trim()
$strat  = ($status | Select-String 'Stratum:').ToString().Trim()
W "      $src" 'Green'
W "      $strat" 'Green'

# ---------- 5. Firewall inbound UDP 123 desde SERVER ----------
W "`n[5/5] Anadiendo regla firewall MAL-NTP-Relay-SERVER..." 'Yellow'
$ruleName = 'MAL-NTP-Relay-SERVER'
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    W "      Regla ya existe, omitiendo." 'Gray'
} else {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Inbound -Protocol UDP -LocalPort 123 `
        -RemoteAddress $ServerIP -Action Allow -Profile Any | Out-Null
    W "      OK - Inbound UDP 123 from $ServerIP" 'Green'
}

# ---------- Verificacion final ----------
W "`n===============================================================" 'Cyan'
W " Verificacion final" 'Cyan'
W "===============================================================" 'Cyan'
$cfg = & w32tm /query /configuration 2>&1
$cfg | Select-String 'AnnounceFlags|^Enabled|NtpServer' | ForEach-Object { W "  $($_.Line.Trim())" 'White' }

W "`nPROXIMO PASO: configurar SERVER ($ServerIP) con:" 'Yellow'
W "  w32tm /config /manualpeerlist:`"192.168.1.162,0x9`" /syncfromflags:manual /update" 'White'
W "  Restart-Service W32Time; w32tm /resync" 'White'
W "  New-NetFirewallRule -DisplayName 'MAL-SRV NTP to CLIENT' -Direction Outbound -Protocol UDP -RemotePort 123 -RemoteAddress 192.168.1.162 -Action Allow" 'White'
W "`nVerificar desde CLIENT (via WinRM):" 'Yellow'
W "  Invoke-Command -ComputerName $ServerIP -Credential Administrator -ScriptBlock { w32tm /query /source }" 'White'
W "  Esperado: 192.168.1.162" 'Green'
W ""
