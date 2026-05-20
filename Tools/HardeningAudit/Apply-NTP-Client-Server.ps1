#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Configura el SERVER (Beckhoff C6030) para sincronizar contra el CLIENT (NTP relay).
.NOTES
    Ejecutar EN el SERVER (RDP/WinRM). El CLIENT debe estar previamente configurado
    con Apply-NTP-Relay-Client.ps1.
    Referencia: 06.7-A72-02 v1.1 ANEXO D.5 / M43-NTP / M43-FW-NTP
#>

[CmdletBinding()]
param(
    [string]$ClientRelayIP = '192.168.1.162'
)

$ErrorActionPreference = 'Stop'
function W([string]$m,[string]$c='Gray'){ Write-Host "[NTP-Client] $m" -ForegroundColor $c }

W "===============================================================" 'Cyan'
W " Configuracion SERVER -> sync NTP desde CLIENT ($ClientRelayIP)" 'Cyan'
W "===============================================================" 'Cyan'

# 1. Servicio
W "`n[1/4] Habilitando W32Time..." 'Yellow'
Set-Service -Name W32Time -StartupType Automatic
if ((Get-Service W32Time).Status -ne 'Running') { Start-Service W32Time }
W "      OK" 'Green'

# 2. Sync peer = CLIENT
W "`n[2/4] Peer NTP = $ClientRelayIP,0x9..." 'Yellow'
& w32tm /config /manualpeerlist:"$ClientRelayIP,0x9" /syncfromflags:manual /reliable:NO /update | Out-Null
W "      OK" 'Green'

# 3. Firewall outbound UDP 123 -> CLIENT
W "`n[3/4] Regla firewall outbound UDP/123 -> $ClientRelayIP..." 'Yellow'
$ruleName = 'MAL-SRV NTP to CLIENT'
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Outbound -Protocol UDP -RemotePort 123 `
        -RemoteAddress $ClientRelayIP -Action Allow -Profile Any | Out-Null
    W "      OK" 'Green'
} else { W "      Regla ya existe" 'Gray' }

# 4. Restart + resync
W "`n[4/4] Restart W32Time + resync..." 'Yellow'
Restart-Service W32Time
Start-Sleep -Seconds 3
& w32tm /resync /rediscover 2>&1 | Out-Null

# Verificacion
W "`n===============================================================" 'Cyan'
W " Verificacion" 'Cyan'
W "===============================================================" 'Cyan'
$src    = (& w32tm /query /source 2>&1).Trim()
$status = & w32tm /query /status 2>&1
W "  Source:  $src" 'White'
$status | Select-String 'Stratum|Last Successful Sync|Source' | ForEach-Object { W "  $($_.Line.Trim())" 'White' }

if ($src -eq $ClientRelayIP) {
    W "`n  OK - SERVER sincronizado desde CLIENT relay" 'Green'
} else {
    W "`n  WARN - Source inesperado. Esperado: $ClientRelayIP" 'Yellow'
    W "         Verificar conectividad: Test-NetConnection $ClientRelayIP -Port 123 -InformationLevel Detailed" 'Yellow'
}
