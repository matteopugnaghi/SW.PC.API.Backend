param(
    [string]$Ssid = 'AQF-DEMO',
    [string]$Password = 'Aquafrisch2026!',
    [switch]$Stop,
    [switch]$SoloHotspot   # Opcion C: solo hotspot (demo local), sin VPN ni ICS
)

# =====================================================================
# Setup-VpnBridge.ps1 - Puente VPN -> Hotspot WiFi para demos con Quest 3
#
# MODO PUENTE VPN (por defecto): FortiClient conectado a la VPN de empresa
#   + hotspot WiFi local. La Quest alcanza el servidor via tunel (ICS/NAT).
# MODO SOLO HOTSPOT (-SoloHotspot): solo crea el hotspot para la demo
#   LOCAL (backend en este mismo portatil). La Quest entra en:
#   https://192.168.137.1:5001  (IP fija del host en el hotspot de Windows)
#
# USO (PowerShell como Administrador):
#   .\Setup-VpnBridge.ps1                -> puente VPN (FortiClient conectado)
#   .\Setup-VpnBridge.ps1 -SoloHotspot   -> solo hotspot (demo local, opcion C)
#   .\Setup-VpnBridge.ps1 -Stop          -> detiene hotspot y comparticion
#
# IMPORTANTE: usar el MISMO Ssid/Password en todos los portatiles para
# que la Quest se conecte automaticamente a cualquiera de ellos.
# =====================================================================

$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "[VpnBridge] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[VpnBridge] $msg" -ForegroundColor Green }
function Write-Err($msg)  { Write-Host "[VpnBridge] ERROR: $msg" -ForegroundColor Red }

# --- Comprobacion de administrador ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Err 'Este script requiere PowerShell como Administrador.'
    exit 1
}

# --- Helpers WinRT (API del Punto de acceso movil de Windows) ---
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]
$asTaskAction = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction'
})[0]
function Await($winRtTask, $resultType) {
    $netTask = $asTaskGeneric.MakeGenericMethod($resultType).Invoke($null, @($winRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}
function AwaitAction($winRtAction) {
    $netTask = $asTaskAction.Invoke($null, @($winRtAction))
    $netTask.Wait(-1) | Out-Null
}

function Get-TetheringManager {
    $null = [Windows.Networking.Connectivity.NetworkInformation, Windows.Networking.Connectivity, ContentType = WindowsRuntime]
    $null = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager, Windows.Networking.NetworkOperators, ContentType = WindowsRuntime]
    $candidates = @()
    try { $candidates += [Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile() } catch { }
    try { $candidates += @([Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles()) } catch { }
    foreach ($p in ($candidates | Where-Object { $_ })) {
        try { return [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($p) } catch { }
    }
    throw 'No hay ninguna conexion compatible con el Punto de acceso movil (se necesita WiFi o Ethernet activa).'
}

function Disable-AllIcs {
    $share = New-Object -ComObject HNetCfg.HNetShare
    foreach ($conn in $share.EnumEveryConnection) {
        try {
            $cfg = $share.INetSharingConfigurationForINetConnection($conn)
            if ($cfg.SharingEnabled) {
                $name = $share.NetConnectionProps($conn).Name
                $cfg.DisableSharing()
                Write-Step "Comparticion ICS previa desactivada en '$name'"
            }
        } catch { }
    }
}

# =====================================================================
# MODO -Stop: detener puente
# =====================================================================
if ($Stop) {
    Write-Step 'Deteniendo puente VPN...'
    Disable-AllIcs
    try {
        $tm = Get-TetheringManager
        if ($tm.TetheringOperationalState -eq 1) {
            $result = Await ($tm.StopTetheringAsync()) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult])
            Write-Ok "Hotspot detenido (status=$($result.Status))"
        } else {
            Write-Ok 'El hotspot ya estaba apagado'
        }
    } catch { Write-Err $_.Exception.Message }
    exit 0
}

# =====================================================================
# PASO 1: verificar que FortiClient esta conectado (solo modo puente VPN)
# =====================================================================
$vpnAdapter = $null
if (-not $SoloHotspot) {
    Write-Step 'Buscando adaptador VPN de Fortinet activo...'
    $vpnAdapter = Get-NetAdapter | Where-Object {
        $_.Status -eq 'Up' -and ($_.InterfaceDescription -match 'Fortinet|FortiSSL' -or $_.Name -match 'fortissl')
    } | Select-Object -First 1
    if (-not $vpnAdapter) {
        Write-Err 'No se encontro el adaptador VPN de Fortinet activo.'
        Write-Host '  1. Abre FortiClient y conecta la VPN de empresa'
        Write-Host '  2. Vuelve a ejecutar este script'
        Write-Host '  (para demo local sin VPN usa el parametro -SoloHotspot)'
        exit 2
    }
    Write-Ok "VPN detectada: '$($vpnAdapter.Name)' ($($vpnAdapter.InterfaceDescription))"
} else {
    Write-Step 'Modo SOLO HOTSPOT (demo local, sin VPN)'
}

# =====================================================================
# PASO 2: configurar y arrancar el Punto de acceso movil
# =====================================================================
# Desactivar auto-apagado del hotspot sin clientes (Windows lo apaga a los ~5 min)
try {
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\icssvc\Settings' -Name 'PeerlessTimeoutEnabled' -Value 0 -Type DWord -Force
    Write-Step 'Auto-apagado del hotspot desactivado (PeerlessTimeoutEnabled=0)'
} catch {
    Write-Step "No se pudo desactivar el auto-apagado: $($_.Exception.Message)"
}
Write-Step "Configurando hotspot SSID='$Ssid'..."
$tm = Get-TetheringManager
try {
    $apConfig = $tm.GetCurrentAccessPointConfiguration()
    $apConfig.Ssid = $Ssid
    $apConfig.Passphrase = $Password
    AwaitAction ($tm.ConfigureAccessPointAsync($apConfig))
    Write-Ok 'SSID y contrasena aplicados'
} catch {
    Write-Err "No se pudo configurar el SSID ($($_.Exception.Message)). Continuo con la config actual de Windows."
}

if ($tm.TetheringOperationalState -ne 1) {
    Write-Step 'Arrancando hotspot...'
    $result = Await ($tm.StartTetheringAsync()) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult])
    if ($result.Status -ne 0) {
        Write-Err "El hotspot no arranco (status=$($result.Status)). Activalo manualmente: Configuracion > Red > Punto de acceso movil"
        exit 3
    }
    Start-Sleep -Seconds 3
}
Write-Ok 'Hotspot activo'

# =====================================================================
# PASO 3: localizar el adaptador virtual del hotspot
# =====================================================================
Start-Sleep -Seconds 2
# Deteccion robusta: el host del hotspot de Windows siempre tiene la IP 192.168.137.1
$hotspotAdapter = $null
$hsIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -like '192.168.137.*' } | Select-Object -First 1
if ($hsIp) {
    $hotspotAdapter = Get-NetAdapter -InterfaceIndex $hsIp.InterfaceIndex -ErrorAction SilentlyContinue
}
if (-not $hotspotAdapter) {
    # Fallback: por descripcion (algunos drivers usan otro nombre)
    $hotspotAdapter = Get-NetAdapter | Where-Object {
        $_.Status -eq 'Up' -and $_.InterfaceDescription -match 'Wi-Fi Direct Virtual|Hosted Network|Virtual Adapter'
    } | Select-Object -First 1
}
if (-not $hotspotAdapter) {
    if ($SoloHotspot) {
        # En modo local no se necesita ICS: el hotspot ya funciona, seguimos
        Write-Step 'Adaptador virtual no identificado (no importa en modo solo hotspot)'
    } else {
        Write-Err 'No se encontro el adaptador virtual del hotspot (necesario para ICS).'
        exit 4
    }
} else {
    Write-Ok "Adaptador hotspot: '$($hotspotAdapter.Name)'"
}

# =====================================================================
# PASO 4: ICS - compartir la VPN hacia el hotspot (solo modo puente VPN)
# =====================================================================
if (-not $SoloHotspot) {
    Write-Step 'Configurando comparticion ICS (VPN -> hotspot)...'
    Set-Service SharedAccess -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service SharedAccess -ErrorAction SilentlyContinue
    Disable-AllIcs

    $share = New-Object -ComObject HNetCfg.HNetShare
    $pubConn = $share.EnumEveryConnection | Where-Object { $share.NetConnectionProps($_).Name -eq $vpnAdapter.Name }
    $privConn = $share.EnumEveryConnection | Where-Object { $share.NetConnectionProps($_).Name -eq $hotspotAdapter.Name }
    if (-not $pubConn -or -not $privConn) {
        Write-Err 'No se pudieron localizar las conexiones para ICS.'
        Write-Host "  Hazlo manual: ncpa.cpl > '$($vpnAdapter.Name)' > Propiedades > Compartir >"
        Write-Host "  marcar 'Permitir que otros usuarios se conecten' y elegir '$($hotspotAdapter.Name)'"
        exit 5
    }
    try {
        $share.INetSharingConfigurationForINetConnection($pubConn).EnableSharing(0)   # 0 = publica (origen: VPN)
        $share.INetSharingConfigurationForINetConnection($privConn).EnableSharing(1)  # 1 = privada (destino: hotspot)
        Write-Ok 'ICS configurado: el trafico del hotspot sale por el tunel VPN'
    } catch {
        Write-Err "Fallo al activar ICS: $($_.Exception.Message)"
        Write-Host "  Hazlo manual: ncpa.cpl > '$($vpnAdapter.Name)' > Propiedades > Compartir"
        exit 6
    }
}

# =====================================================================
# RESUMEN
# =====================================================================
Write-Host ''
Write-Ok '=============================================='
if ($SoloHotspot) {
    Write-Ok ' HOTSPOT DEMO LOCAL LISTO (opcion C)'
    Write-Ok "  WiFi para la Quest : $Ssid"
    Write-Ok "  Contrasena         : $Password"
    Write-Ok '  Arranca Iniciar-Demo.bat y en la Quest entra en:'
    Write-Ok '    https://192.168.137.1:5001  (IP fija del portatil)'
} else {
    Write-Ok ' PUENTE VPN LISTO (opcion B)'
    Write-Ok "  WiFi para la Quest : $Ssid"
    Write-Ok "  Contrasena         : $Password"
    Write-Ok '  En la Quest, abrir el navegador y entrar en:'
    Write-Ok '    https://192.168.2.199:5001  (u otra instancia)'
}
Write-Ok '=============================================='
Write-Host ''
if (-not $SoloHotspot) {
    Write-Host 'Nota: si FortiClient se reconecta, vuelve a ejecutar este script'
    Write-Host '(el adaptador VPN cambia de estado y el ICS puede perderse).'
}
