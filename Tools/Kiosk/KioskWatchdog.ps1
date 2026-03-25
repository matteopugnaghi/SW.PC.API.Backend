<#
.SYNOPSIS
    Watchdog kiosk — vigila Edge, health check backend, overlay mantenimiento.
    Diseñado para IPC táctil (Beckhoff CP2221-0040).

.DESCRIPTION
    1. Lee configuración desde /api/system/kiosk-config (KioskBrowserPath, Args)
    2. Lanza Edge en modo kiosk (pantalla completa)
    3. Watchdog: relanza Edge si se cierra/crashea
    4. Health check: si el backend falla N veces, reinicia servicio AquafrischSupervisor
    5. Overlay MANTENIMIENTO: cuando un admin se conecta por RDP,
       muestra pantalla "Mantenimiento en curso" en el kiosk (4 idiomas)
    6. SCREENSAVER: tras 30 min de inactividad, logo rebotando (anti burn-in).
       Cualquier input (ratón/teclado/táctil) lo cierra.

    Los controles de sesión están integrados en la web:
      - "Reiniciar App"         → Backend se rearranca via servicio Windows
      - "Cerrar Sesión Windows" → shutdown /l (sale del kiosco)
      - "TeamViewer"            → Asistencia remota

.PARAMETER SupervisorUrl
    URL del Supervisor. Default: https://localhost:5001

.PARAMETER WatchdogInterval
    Segundos entre verificaciones. Default: 30

.PARAMETER MaxFailures
    Fallos consecutivos antes de reiniciar el servicio. Default: 3

.PARAMETER ServiceName
    Nombre del servicio Windows del backend. Default: AquafrischSupervisor

.PARAMETER IdleTimeoutMinutes
    Minutos de inactividad antes de activar screensaver. Default: 30

.NOTES
    Ref: 04.2-01 §23 — Autostart y Modo Kiosco
#>

param(
    [string]$SupervisorUrl    = 'https://localhost:5001',
    [int]$WatchdogInterval    = 30,
    [int]$MaxFailures         = 3,
    [string]$ServiceName      = 'AquafrischSupervisor',
    [int]$IdleTimeoutMinutes   = 30
)

$ErrorActionPreference = 'Continue'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Win32: forzar ventana topmost sobre Edge kiosk + detectar inactividad
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

[StructLayout(LayoutKind.Sequential)]
public struct LASTINPUTINFO {
    public uint cbSize;
    public uint dwTime;
}

public class Win32Idle {
    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static uint GetIdleSeconds() {
        LASTINPUTINFO lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (GetLastInputInfo(ref lii)) {
            return (uint)((Environment.TickCount - lii.dwTime) / 1000);
        }
        return 0;
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
    $restarted = $false

    # Intento 1: Restart-Service (requiere permisos admin)
    try {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc) {
            Restart-Service -Name $ServiceName -Force -ErrorAction Stop
            Write-Log "Servicio $ServiceName reiniciado via Restart-Service"
            $restarted = $true
        }
    } catch {
        Write-Log "Restart-Service fallo: $($_.Exception.Message) — intentando sc.exe" 'WARN'
    }

    # Intento 2: sc.exe start (funciona sin privilegios elevados en algunos casos)
    if (-not $restarted) {
        try {
            $scResult = & sc.exe start $ServiceName 2>&1
            if ($scResult -match 'START_PENDING|RUNNING') {
                Write-Log "Servicio $ServiceName arrancado via sc.exe"
                $restarted = $true
            } else {
                Write-Log "sc.exe start fallo: $scResult" 'WARN'
            }
        } catch {
            Write-Log "sc.exe fallo: $($_.Exception.Message)" 'WARN'
        }
    }

    # Intento 3: Lanzar proceso directamente (ultimo recurso)
    if (-not $restarted) {
        Write-Log 'Intentando arrancar backend como proceso directo...' 'WARN'
        Get-Process -Name 'SW.PC.API.Backend' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $exe = 'C:\Aquafrisch Supervisor\Backend\SW.PC.API.Backend.exe'
        if (Test-Path $exe) {
            Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
            Write-Log "Backend relanzado como proceso directo (no servicio)"
        } else {
            Write-Log "ERROR: No se encontro $exe" 'ERROR'
        }
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
    $panel.Size      = New-Object System.Drawing.Size(800, 480)
    $panel.Location  = New-Object System.Drawing.Point(
        [int](($screen.Width - 800) / 2),
        [int](($screen.Height - 480) / 2)
    )
    $panel.BackColor = [System.Drawing.Color]::FromArgb(30, 35, 50)
    $form.Controls.Add($panel)

    # Icono herramienta
    $icon = New-Object System.Windows.Forms.Label
    $icon.Text      = [char]0x2699  # ⚙
    $icon.Font      = New-Object System.Drawing.Font('Segoe UI', 64)
    $icon.ForeColor = [System.Drawing.Color]::FromArgb(0, 160, 220)
    $icon.TextAlign = 'MiddleCenter'
    $icon.Size      = New-Object System.Drawing.Size(800, 120)
    $icon.Location  = New-Object System.Drawing.Point(0, 20)
    $panel.Controls.Add($icon)

    # Título — 4 idiomas (2 líneas)
    $title = New-Object System.Windows.Forms.Label
    $title.Text      = "MANTENIMIENTO EN CURSO`nMAINTENANCE IN PROGRESS"
    $title.Font      = New-Object System.Drawing.Font('Segoe UI', 20, [System.Drawing.FontStyle]::Bold)
    $title.ForeColor = [System.Drawing.Color]::FromArgb(0, 180, 230)
    $title.TextAlign = 'MiddleCenter'
    $title.Size      = New-Object System.Drawing.Size(800, 70)
    $title.Location  = New-Object System.Drawing.Point(0, 140)
    $panel.Controls.Add($title)

    # Subtítulo — 4 idiomas (ES / EN / DE / IT)
    $sub = New-Object System.Windows.Forms.Label
    $lineES = "Un t$([char]0x00E9)cnico est$([char]0x00E1) realizando tareas de mantenimiento."
    $lineEN = 'A technician is performing maintenance tasks.'
    $lineDE = "Ein Techniker f$([char]0x00FC)hrt Wartungsarbeiten durch."
    $lineIT = 'Un tecnico sta eseguendo operazioni di manutenzione.'
    $sub.Text      = "$lineES`n$lineEN`n$lineDE`n$lineIT"
    $sub.Font      = New-Object System.Drawing.Font('Segoe UI', 12)
    $sub.ForeColor = [System.Drawing.Color]::FromArgb(160, 170, 190)
    $sub.TextAlign = 'MiddleCenter'
    $sub.Size      = New-Object System.Drawing.Size(800, 120)
    $sub.Location  = New-Object System.Drawing.Point(0, 230)
    $panel.Controls.Add($sub)

    # Línea decorativa inferior
    $line = New-Object System.Windows.Forms.Panel
    $line.Size      = New-Object System.Drawing.Size(200, 3)
    $line.Location  = New-Object System.Drawing.Point(300, 370)
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
#  FUNCIONES — SCREENSAVER (Anti burn-in)
# ============================================================================
# Tras 30 min de inactividad muestra logo "AQUAFRISCH" rebotando por la pantalla.
# Cualquier input (ratón/teclado/táctil) lo cierra automáticamente.

$script:screensaverForm    = $null
$script:screensaverVisible = $false
$script:screensaverTimer   = $null
$script:ssX      = 100   # posición X del logo
$script:ssY      = 100   # posición Y del logo
$script:ssDX     = 3     # velocidad horizontal
$script:ssDY     = 2     # velocidad vertical
$script:ssLabel  = $null
$script:IdleTimeoutSeconds = $IdleTimeoutMinutes * 60  # parámetro en minutos

function Show-Screensaver {
    if ($script:screensaverVisible) { return }

    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.WindowState     = 'Maximized'
    $form.TopMost         = $true
    $form.ShowInTaskbar   = $false
    $form.BackColor       = [System.Drawing.Color]::Black
    $form.StartPosition   = 'Manual'
    $form.Location        = New-Object System.Drawing.Point(0, 0)
    $form.Size            = New-Object System.Drawing.Size($screen.Width, $screen.Height)
    $form.Cursor          = [System.Windows.Forms.Cursors]::None
    $form.DoubleBuffered  = $true

    # Logo flotante
    $label = New-Object System.Windows.Forms.Label
    $label.Text      = 'Aquafrisch'
    $label.Font      = New-Object System.Drawing.Font('Crillee', 48, [System.Drawing.FontStyle]::Bold)
    $label.ForeColor = [System.Drawing.Color]::FromArgb(120, 120, 120)
    $label.BackColor = [System.Drawing.Color]::Transparent
    $label.AutoSize  = $true
    $label.Location  = New-Object System.Drawing.Point($script:ssX, $script:ssY)
    $form.Controls.Add($label)
    $script:ssLabel = $label

    # Guardar posicion inicial del mouse para detectar movimiento REAL
    $script:ssMouseStart = [System.Windows.Forms.Cursor]::Position

    # Cerrar con cualquier input real
    $dismissAction = {
        $this.Tag = 'dismiss'
    }
    $mouseMoveAction = {
        # Solo dismiss si el mouse se movio realmente (>10 px)
        $current = [System.Windows.Forms.Cursor]::Position
        $dx = [Math]::Abs($current.X - $script:ssMouseStart.X)
        $dy = [Math]::Abs($current.Y - $script:ssMouseStart.Y)
        if (($dx + $dy) -gt 10) {
            $this.Tag = 'dismiss'
        }
    }
    $form.Add_MouseMove($mouseMoveAction)
    $form.Add_MouseDown($dismissAction)
    $form.Add_KeyDown($dismissAction)
    $form.Add_Click($dismissAction)
    $label.Add_MouseMove($mouseMoveAction)
    $label.Add_MouseDown($dismissAction)
    $label.Add_Click($dismissAction)

    # Timer para mover el logo (cada 50ms = ~20 FPS)
    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 50
    $timer.Add_Tick({
        if (-not $script:ssLabel -or -not $script:screensaverForm) { return }
        $sw = $script:screensaverForm.ClientSize.Width
        $sh = $script:screensaverForm.ClientSize.Height
        $lw = $script:ssLabel.Width
        $lh = $script:ssLabel.Height

        $script:ssX += $script:ssDX
        $script:ssY += $script:ssDY

        # Rebotar en bordes
        if ($script:ssX -le 0) { $script:ssX = 0; $script:ssDX = [Math]::Abs($script:ssDX) }
        if ($script:ssX -ge ($sw - $lw)) { $script:ssX = $sw - $lw; $script:ssDX = -[Math]::Abs($script:ssDX) }
        if ($script:ssY -le 0) { $script:ssY = 0; $script:ssDY = [Math]::Abs($script:ssDY) }
        if ($script:ssY -ge ($sh - $lh)) { $script:ssY = $sh - $lh; $script:ssDY = -[Math]::Abs($script:ssDY) }

        $script:ssLabel.Location = New-Object System.Drawing.Point([int]$script:ssX, [int]$script:ssY)
    })
    $timer.Start()
    $script:screensaverTimer = $timer

    $form.Add_Shown({
        [Win32TopMost]::ForceTopMost($form.Handle)
    })

    $form.Show()
    [Win32TopMost]::ForceTopMost($form.Handle)

    $script:screensaverForm    = $form
    $script:screensaverVisible = $true
    Write-Log 'Screensaver activado (inactividad detectada)' 'ACTION'
}

function Hide-Screensaver {
    if (-not $script:screensaverVisible) { return }
    if ($script:screensaverTimer) {
        $script:screensaverTimer.Stop()
        $script:screensaverTimer.Dispose()
        $script:screensaverTimer = $null
    }
    if ($script:screensaverForm) {
        $script:screensaverForm.Close()
        $script:screensaverForm.Dispose()
        $script:screensaverForm = $null
    }
    $script:ssLabel = $null
    $script:screensaverVisible = $false
    Write-Log 'Screensaver desactivado (input detectado)' 'ACTION'
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
$wasRdpActive   = $false
$healthTickCounter = 0

# Bucle principal — usa Application.DoEvents para procesar mensajes WinForms del overlay
while ($true) {
    # Intervalo corto cuando screensaver activo (respuesta rápida al touch)
    if ($script:screensaverVisible) {
        Start-Sleep -Milliseconds 500
        $healthTickCounter++
    } else {
        Start-Sleep -Seconds $WatchdogInterval
        $healthTickCounter = $WatchdogInterval * 2  # forzar health check cada ciclo largo
    }

    # --- Detectar RDP y gestionar overlay ---
    $rdpActive = Test-RdpSessionActive
    if ($rdpActive -and -not $script:maintenanceVisible) {
        if ($script:screensaverVisible) { Hide-Screensaver }
        Show-MaintenanceOverlay
    } elseif (-not $rdpActive -and $script:maintenanceVisible) {
        Hide-MaintenanceOverlay
        # RDP acaba de desconectarse — forzar relanzamiento de Edge
        Write-Log 'RDP desconectado — relanzando navegador kiosk' 'ACTION'
        Get-Process -Name $script:browserProcess -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Start-KioskBrowser
    }
    $wasRdpActive = $rdpActive

    # Refrescar z-order del overlay si visible
    if ($script:maintenanceVisible -and $script:maintenanceForm) {
        [System.Windows.Forms.Application]::DoEvents()
        [Win32TopMost]::ForceTopMost($script:maintenanceForm.Handle)
    }

    # --- Screensaver (anti burn-in) — solo si NO hay mantenimiento ---
    if (-not $script:maintenanceVisible) {
        $idleSec = [Win32Idle]::GetIdleSeconds()

        if ($script:screensaverVisible) {
            # Procesar eventos WinForms (animación del timer + input)
            [System.Windows.Forms.Application]::DoEvents()
            [Win32TopMost]::ForceTopMost($script:screensaverForm.Handle)

            # Comprobar si el usuario hizo input (tag 'dismiss' o idle reseteado)
            if ($script:screensaverForm.Tag -eq 'dismiss' -or $idleSec -lt 5) {
                Hide-Screensaver
            }
        } elseif ($idleSec -ge $script:IdleTimeoutSeconds) {
            Show-Screensaver
        }
    }

    # --- Verificar navegador (solo si NO hay screensaver NI mantenimiento) ---
    if (-not $script:screensaverVisible -and -not $rdpActive) {
        if (-not (Test-BrowserRunning)) {
            Write-Log 'Navegador no detectado — relanzando...' 'WARN'
            Start-Sleep -Seconds 3
            Start-KioskBrowser
        }
    }

    # --- Health check del backend (SIEMPRE, incluso con screensaver) ---
    # Durante screensaver: cada ~30s (healthTickCounter acumula ticks de 500ms)
    $runHealthCheck = (-not $script:screensaverVisible) -or ($healthTickCounter -ge ($WatchdogInterval * 2))
    if ($runHealthCheck) {
        $healthTickCounter = 0

        if (Test-BackendHealth) {
            if ($failureCount -gt 0) {
                Write-Log "Backend recuperado tras $failureCount fallos — reiniciando navegador"
                Get-Process -Name $script:browserProcess -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 3
                Start-KioskBrowser
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
}
