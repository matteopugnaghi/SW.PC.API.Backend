<#
.SYNOPSIS
    Watchdog kiosk — vigila Edge, health check backend, overlay mantenimiento.
    Diseñado para IPC táctil (Beckhoff CP2221-0040).

.DESCRIPTION
    1. Lee configuración desde /api/system/kiosk-config (KioskBrowserPath, Args)
    2. Lanza Edge en modo kiosk (pantalla completa)
    3. Watchdog: relanza Edge si se cierra/crashea
    4. Health check: si el backend falla N veces, reinicia servicio AqfSupervisor
    5. Overlay MANTENIMIENTO: cuando un admin se conecta por RDP,
       muestra pantalla "Mantenimiento en curso" en el kiosk

    Los controles de sesión están integrados en la web:
      - "Reiniciar App"         → Backend se rearranca via servicio Windows
      - "Cerrar Sesión Windows" → shutdown /l (sale del kiosco)
      - "TeamViewer"            → Asistencia remota

.PARAMETER SupervisorUrl
    URL del Supervisor. Default: https://192.168.2.161:5001

.PARAMETER WatchdogInterval
    Segundos entre verificaciones. Default: 30

.PARAMETER MaxFailures
    Fallos consecutivos antes de reiniciar el servicio. Default: 10

.PARAMETER ServiceName
    Nombre del servicio Windows del backend. Default: AqfSupervisor

.NOTES
    Ref: 04.2-01 §23 — Autostart y Modo Kiosco
#>

param(
    [string]$SupervisorUrl    = 'https://192.168.2.161:5001',
    [int]$WatchdogInterval    = 30,
    [int]$MaxFailures         = 10,
    [string]$ServiceName      = 'AqfSupervisor'
)

$ErrorActionPreference = 'Continue'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Win32: forzar ventana topmost sobre Edge kiosk
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32TopMost {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_SHOWWINDOW = 0x0040;

    public static void ForceTopMost(IntPtr hWnd) {
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }
}
"@

# ============================================================================
#  CONFIGURACIÓN
# ============================================================================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$logFile   = Join-Path $scriptDir 'kiosk_watchdog.log'

# Valores por defecto (se sobrescriben con API si está disponible)
$script:browserPath    = $null
$script:browserProcess = $null
$script:healthCheckUrl = "$SupervisorUrl/health"
$script:kioskArgs      = $null

# ============================================================================
#  FUNCIONES — LOG
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -Path $logFile -Value "[$ts] [$Level] $Message" -ErrorAction SilentlyContinue
}

# ============================================================================
#  FUNCIONES — CONFIGURACIÓN DESDE API
# ============================================================================

function Get-KioskConfig {
    <#
    .SYNOPSIS
        Lee configuración kiosk desde /api/system/kiosk-config.
        Si no puede conectar, usa valores por defecto.
    #>
    try {
        if (-not ([System.Management.Automation.PSTypeName]'TrustAll').Type) {
            Add-Type @"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class TrustAll {
    public static void Enable() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate { return true; };
    }
}
"@
        }
        [TrustAll]::Enable()

        $url = "$SupervisorUrl/api/system/kiosk-config"
        $response = Invoke-RestMethod -Uri $url -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        Write-Log "Configuración leída desde API"
        return $response
    } catch {
        Write-Log "No se pudo leer config del API: $($_.Exception.Message) — usando defaults" 'WARN'
        return $null
    }
}

function Initialize-BrowserConfig {
    <#
    .SYNOPSIS
        Configura ruta del navegador y argumentos.
        Prioridad: API (KioskBrowserPath/Args) > detección automática.
    #>
    param($ApiConfig)

    # Intentar desde API
    if ($ApiConfig) {
        # KioskBrowserPath desde Excel → API
        # (el endpoint no expone el path directamente, pero restart-app sí lo usa)
        # Usamos detección local como el script original
    }

    # Detección automática del navegador
    $candidates = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
    )
    $script:browserPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $script:browserPath) {
        Write-Log 'ERROR: No se encontró Edge ni Chrome' 'ERROR'
        exit 1
    }

    $script:browserProcess = if ($script:browserPath -match 'msedge') { 'msedge' } else { 'chrome' }

    # Argumentos kiosk
    $script:kioskArgs = @(
        '--kiosk', $SupervisorUrl,
        '--no-first-run',
        '--disable-session-crashed-bubble',
        '--noerrdialogs',
        '--disable-infobars'
    )
    if ($script:browserProcess -eq 'msedge') {
        $script:kioskArgs += @(
            '--edge-kiosk-type=fullscreen',
            '--disable-features=msEdgeSidebarButton'
        )
    }

    Write-Log "Browser: $($script:browserPath)"
    Write-Log "Args: $($script:kioskArgs -join ' ')"
}

# ============================================================================
#  FUNCIONES — NAVEGADOR
# ============================================================================

function Start-KioskBrowser {
    Write-Log "Lanzando $($script:browserProcess) en modo kiosk"
    Start-Process -FilePath $script:browserPath -ArgumentList $script:kioskArgs
    Start-Sleep -Seconds 3
}

function Test-BrowserRunning {
    $null -ne (Get-Process -Name $script:browserProcess -ErrorAction SilentlyContinue)
}

# ============================================================================
#  FUNCIONES — HEALTH CHECK
# ============================================================================

function Test-BackendHealth {
    try {
        [TrustAll]::Enable()
        $r = Invoke-WebRequest -Uri $script:healthCheckUrl -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        return ($r.StatusCode -eq 200)
    } catch {
        return $false
    }
}

function Restart-BackendService {
    Write-Log "Reiniciando servicio $ServiceName..." 'WARN'
    try {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc) {
            Restart-Service -Name $ServiceName -Force -ErrorAction Stop
            Write-Log "Servicio $ServiceName reiniciado"
        } else {
            Write-Log "Servicio no encontrado — reiniciando proceso" 'WARN'
            Get-Process -Name 'SW.PC.API.Backend' -ErrorAction SilentlyContinue | Stop-Process -Force
            Start-Sleep -Seconds 3
            $exe = 'C:\Aquafrisch Supervisor\Backend\SW.PC.API.Backend.exe'
            if (Test-Path $exe) {
                Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
                Write-Log "Backend relanzado manualmente"
            }
        }
    } catch {
        Write-Log "ERROR reiniciando: $($_.Exception.Message)" 'ERROR'
    }
}

# ============================================================================
#  FUNCIONES — DETECCIÓN RDP Y OVERLAY MANTENIMIENTO
# ============================================================================

function Test-RdpSessionActive {
    <#
    .SYNOPSIS
        Detecta si hay una sesión RDP activa (otra sesión aparte de la del kiosk).
        Usa query session para buscar sesiones RDP-Tcp activas.
    #>
    try {
        $sessions = query session 2>$null
        if ($sessions) {
            # Buscar líneas con rdp-tcp que estén activas (no la consola)
            $rdpActive = $sessions | Where-Object {
                $_ -match 'rdp-tcp' -and $_ -match 'Active|Activ'
            }
            return ($null -ne $rdpActive)
        }
    } catch { }
    return $false
}

$script:maintenanceForm = $null
$script:maintenanceVisible = $false

function Show-MaintenanceOverlay {
    <#
    .SYNOPSIS
        Muestra pantalla completa "MANTENIMIENTO EN CURSO" sobre el kiosk.
        Se muestra cuando un admin se conecta por RDP.
    #>
    if ($script:maintenanceVisible) { return }

    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.WindowState     = 'Maximized'
    $form.TopMost         = $true
    $form.ShowInTaskbar   = $false
    $form.BackColor       = [System.Drawing.Color]::FromArgb(20, 25, 35)
    $form.StartPosition   = 'Manual'
    $form.Location        = New-Object System.Drawing.Point(0, 0)
    $form.Size            = New-Object System.Drawing.Size($screen.Width, $screen.Height)

    # Panel central
    $panel = New-Object System.Windows.Forms.Panel
    $panel.Size      = New-Object System.Drawing.Size(700, 400)
    $panel.Location  = New-Object System.Drawing.Point(
        [int](($screen.Width - 700) / 2),
        [int](($screen.Height - 400) / 2)
    )
    $panel.BackColor = [System.Drawing.Color]::FromArgb(30, 35, 50)
    $form.Controls.Add($panel)

    # Icono herramienta
    $icon = New-Object System.Windows.Forms.Label
    $icon.Text      = [char]0x2699  # ⚙
    $icon.Font      = New-Object System.Drawing.Font('Segoe UI', 64)
    $icon.ForeColor = [System.Drawing.Color]::FromArgb(0, 160, 220)
    $icon.TextAlign = 'MiddleCenter'
    $icon.Size      = New-Object System.Drawing.Size(700, 120)
    $icon.Location  = New-Object System.Drawing.Point(0, 20)
    $panel.Controls.Add($icon)

    # Título
    $title = New-Object System.Windows.Forms.Label
    $title.Text      = 'MANTENIMIENTO EN CURSO'
    $title.Font      = New-Object System.Drawing.Font('Segoe UI', 28, [System.Drawing.FontStyle]::Bold)
    $title.ForeColor = [System.Drawing.Color]::FromArgb(0, 180, 230)
    $title.TextAlign = 'MiddleCenter'
    $title.Size      = New-Object System.Drawing.Size(700, 60)
    $title.Location  = New-Object System.Drawing.Point(0, 150)
    $panel.Controls.Add($title)

    # Subtítulo
    $sub = New-Object System.Windows.Forms.Label
    $sub.Text      = 'Un técnico está realizando tareas de mantenimiento.' + "`n" + 'El sistema volverá a estar disponible en breve.'
    $sub.Font      = New-Object System.Drawing.Font('Segoe UI', 14)
    $sub.ForeColor = [System.Drawing.Color]::FromArgb(160, 170, 190)
    $sub.TextAlign = 'MiddleCenter'
    $sub.Size      = New-Object System.Drawing.Size(700, 80)
    $sub.Location  = New-Object System.Drawing.Point(0, 230)
    $panel.Controls.Add($sub)

    # Línea decorativa inferior
    $line = New-Object System.Windows.Forms.Panel
    $line.Size      = New-Object System.Drawing.Size(200, 3)
    $line.Location  = New-Object System.Drawing.Point(250, 340)
    $line.BackColor = [System.Drawing.Color]::FromArgb(0, 120, 180)
    $panel.Controls.Add($line)

    $form.Add_Shown({
        [Win32TopMost]::ForceTopMost($form.Handle)
    })

    $form.Show()
    [Win32TopMost]::ForceTopMost($form.Handle)

    $script:maintenanceForm    = $form
    $script:maintenanceVisible = $true
    Write-Log 'Overlay MANTENIMIENTO mostrado (sesión RDP detectada)' 'ACTION'
}

function Hide-MaintenanceOverlay {
    if (-not $script:maintenanceVisible) { return }
    if ($script:maintenanceForm) {
        $script:maintenanceForm.Close()
        $script:maintenanceForm.Dispose()
        $script:maintenanceForm = $null
    }
    $script:maintenanceVisible = $false
    Write-Log 'Overlay MANTENIMIENTO oculto (sesión RDP terminada)' 'ACTION'
}

# ============================================================================
#  INICIO PRINCIPAL
# ============================================================================

Write-Log '====== Kiosk Watchdog iniciado ======'
Write-Log "Supervisor URL: $SupervisorUrl"
Write-Log "Watchdog: ${WatchdogInterval}s interval, $MaxFailures max failures"
Write-Log "Backend service: $ServiceName"

# Esperar arranque del sistema
Write-Log 'Esperando arranque del sistema (10s)...'
Start-Sleep -Seconds 10

# Leer config desde API (si el backend ya está corriendo)
$apiConfig = Get-KioskConfig
Initialize-BrowserConfig -ApiConfig $apiConfig

# Lanzar navegador
Start-KioskBrowser

Write-Log 'Watchdog activo — bucle de vigilancia iniciado'

$failureCount   = 0
$zOrderCounter  = 0

# Bucle principal — usa Application.DoEvents para procesar mensajes WinForms del overlay
while ($true) {
    Start-Sleep -Seconds $WatchdogInterval

    # --- Detectar RDP y gestionar overlay ---
    $rdpActive = Test-RdpSessionActive
    if ($rdpActive -and -not $script:maintenanceVisible) {
        Show-MaintenanceOverlay
    } elseif (-not $rdpActive -and $script:maintenanceVisible) {
        Hide-MaintenanceOverlay
    }

    # Refrescar z-order del overlay si visible
    if ($script:maintenanceVisible -and $script:maintenanceForm) {
        [System.Windows.Forms.Application]::DoEvents()
        [Win32TopMost]::ForceTopMost($script:maintenanceForm.Handle)
    }

    # --- Verificar navegador (solo si NO hay mantenimiento) ---
    if (-not $rdpActive) {
        if (-not (Test-BrowserRunning)) {
            Write-Log 'Navegador no detectado — relanzando...' 'WARN'
            Start-Sleep -Seconds 3
            Start-KioskBrowser
        }
    }

    # --- Health check del backend ---
    if (Test-BackendHealth) {
        if ($failureCount -gt 0) {
            Write-Log "Backend recuperado tras $failureCount fallos"
        }
        $failureCount = 0
    } else {
        $failureCount++
        Write-Log "Health check fallido ($failureCount/$MaxFailures)" 'WARN'

        if ($failureCount -ge $MaxFailures) {
            Write-Log "CRITICO: $MaxFailures fallos consecutivos — reiniciando servicio" 'ERROR'
            Restart-BackendService
            $failureCount = 0
            Start-Sleep -Seconds 15
        }
    }
}
