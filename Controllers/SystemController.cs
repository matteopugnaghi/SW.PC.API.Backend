// ============================================================================
// SystemController.cs - Control de Acciones del Sistema para Entorno Kiosk
// ============================================================================
// EU CRA Compliant - Solo roles autorizados pueden ejecutar acciones del sistema
// Permite: Logout Windows, Iniciar TeamViewer, Diagnóstico de Red, Reiniciar App
// Configurable desde Excel (System Config sheet)
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using System.Security.Claims;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly ILogger<SystemController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IWebHostEnvironment _env;
        private readonly IRequestProjectContext _projectContext;
        private readonly PlcPollingService _plcPollingService;
        private readonly ITwinCATService _twinCATService;
        private readonly IAuditLogService _auditLogService;

        // Cache de configuración del sistema (por proyecto)
        private static readonly Dictionary<string, (SystemConfiguration Config, DateTime Timestamp)> _configCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public SystemController(
            ILogger<SystemController> logger, 
            IConfiguration configuration,
            IExcelConfigService excelConfigService,
            IWebHostEnvironment env,
            IRequestProjectContext projectContext,
            PlcPollingService plcPollingService,
            ITwinCATService twinCATService,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _configuration = configuration;
            _excelConfigService = excelConfigService;
            _env = env;
            _projectContext = projectContext;
            _plcPollingService = plcPollingService;
            _twinCATService = twinCATService;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Obtiene la configuración del sistema desde Excel (con cache por proyecto)
        /// </summary>
        private async Task<SystemConfiguration> GetSystemConfigAsync()
        {
            var projectId = _projectContext.ProjectId;
            var excelPath = _projectContext.ExcelConfigPath;
            
            // Verificar cache por proyecto
            if (_configCache.TryGetValue(projectId, out var cached))
            {
                if (DateTime.Now - cached.Timestamp < _cacheExpiration)
                {
                    return cached.Config;
                }
            }

            _logger.LogInformation("📁 SystemController: Cargando config desde {Path} (proyecto: {Project})", excelPath, projectId);
            var config = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
            _configCache[projectId] = (config, DateTime.Now);
            return config;
        }

        /// <summary>
        /// Verifica si el rol está autorizado para acciones del sistema
        /// También acepta sesiones de soporte válidas (X-Support-Session header)
        /// </summary>
        private async Task<bool> IsAuthorizedRoleAsync(string role)
        {
            // 1. Primero verificar si hay una sesión de soporte activa
            var supportSessionId = Request.Headers["X-Support-Session"].FirstOrDefault();
            if (!string.IsNullOrEmpty(supportSessionId))
            {
                // Verificar sesión llamando al endpoint interno
                var isValidSession = await VerifySupportSessionAsync(supportSessionId);
                if (isValidSession)
                {
                    _logger.LogInformation("✅ Acceso autorizado via sesión de soporte: {SessionId}", supportSessionId);
                    return true;
                }
            }
            
            // 2. Si no hay sesión de soporte, verificar rol normal
            var config = await GetSystemConfigAsync();
            var allowedRoles = config.AllowedSystemToolsRoles
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToArray();
            
            return allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si una sesión de soporte es válida consultando SupportController
        /// </summary>
        private async Task<bool> VerifySupportSessionAsync(string sessionId)
        {
            try
            {
                // Usar HttpClient para verificar la sesión
                using var client = new HttpClient();
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = await client.GetAsync($"{baseUrl}/api/support/session/{sessionId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Si la respuesta contiene "valid": true, la sesión es válida
                    return content.Contains("\"valid\":true") || content.Contains("\"valid\": true");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando sesión de soporte: {SessionId}", sessionId);
            }
            return false;
        }

        /// <summary>
        /// Registra la acción en el log de auditoría
        /// </summary>
        private void LogSystemAction(string action, string username, string role, bool success, string details = "")
        {
            var logEntry = $"[SYSTEM_ACTION] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Action: {action} | User: {username} | Role: {role} | Success: {success} | Details: {details}";
            _logger.LogWarning(logEntry); // Warning para que siempre se registre
            // Audit DB writes are done directly in each endpoint via _auditLogService.LogAsync()
        }

        /// <summary>
        /// Sanitiza el rol para ocultar SuperAdmin en logs públicos
        /// SuperAdmin es un rol oculto de soporte - el cliente no debe saber que existe
        /// </summary>
        private string SanitizeRole(string role)
        {
            return role?.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true 
                ? "Administrador" 
                : role ?? "Unknown";
        }

        /// <summary>
        /// Sanitiza el username para ocultar cuentas de soporte en logs públicos
        /// </summary>
        private string SanitizeUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return "Unknown";
            
            // Ocultar usuarios de soporte (superadmin, support, etc.)
            var hiddenUsers = new[] { "superadmin", "superadministrador", "support", "soporte" };
            return hiddenUsers.Any(u => username.Equals(u, StringComparison.OrdinalIgnoreCase))
                ? "admin_sistema"
                : username;
        }

        // ========================================
        // POST: api/system/logout-windows
        // ========================================
        [HttpPost("logout-windows")]
        public async Task<IActionResult> LogoutWindows([FromBody] SystemActionRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.WindowsLogoutEnabled)
            {
                return BadRequest(new { success = false, message = "Función deshabilitada por configuración", messageKey = "systemTools.errors.featureDisabled" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("logout-windows", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            try
            {
                LogSystemAction("logout-windows", request.Username, request.Role, true, "Initiating Windows logout");
                
                // 📋 Audit Log L1 - EU CRA: Logout Windows es crítico
                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    AuditAction.ServiceStart,
                    AuditResult.Success,
                    $"Windows logout initiated by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)})",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );
                
                // 🔒 Forzar flush antes del logout para no perder el log
                await _auditLogService.FlushAsync();
                
                // Desde servicio SYSTEM: buscar la sesión de consola y hacer logoff
                // query session muestra: "console  usuario  ID  Estado  Tipo"
                var queryProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "query.exe",
                        Arguments = "session",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                queryProcess.Start();
                var sessionOutput = await queryProcess.StandardOutput.ReadToEndAsync();
                await queryProcess.WaitForExitAsync();

                // Buscar línea con "console" y extraer el ID de sesión
                var consoleLine = sessionOutput
                    .Split('\n')
                    .FirstOrDefault(l => l.IndexOf("console", StringComparison.OrdinalIgnoreCase) >= 0);

                if (consoleLine != null)
                {
                    // Extraer números de la línea — el ID de sesión es el primer número
                    var match = System.Text.RegularExpressions.Regex.Match(consoleLine, @"\s+(\d+)\s+");
                    if (match.Success)
                    {
                        var sessionId = match.Groups[1].Value;
                        _logger.LogWarning("🚪 Cerrando sesión de consola (Session ID: {Id})", sessionId);
                        var logoffProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "logoff.exe",
                                Arguments = sessionId,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        logoffProcess.Start();
                    }
                    else
                    {
                        _logger.LogWarning("🚪 No se pudo extraer session ID de: {Line}", consoleLine.Trim());
                    }
                }
                else
                {
                    _logger.LogWarning("🚪 No se encontró sesión de consola activa. Output: {Output}", sessionOutput.Trim());
                }

                return Ok(new { success = true, message = "Cerrando sesión de Windows...", messageKey = "systemTools.messages.windowsLoggingOut" });
            }
            catch (Exception ex)
            {
                LogSystemAction("logout-windows", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al cerrar sesión de Windows");
                return StatusCode(500, new { success = false, message = "Error al cerrar sesión: " + ex.Message, messageKey = "systemTools.errors.logoutFailed" });
            }
        }

        // ========================================
        // POST: api/system/launch-teamviewer
        // ========================================
        [HttpPost("launch-teamviewer")]
        public async Task<IActionResult> LaunchTeamViewer([FromBody] SystemActionRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.TeamViewerEnabled)
            {
                return BadRequest(new { success = false, message = "TeamViewer deshabilitado por configuración" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("launch-teamviewer", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción" });
            }

            try
            {
                // Rutas comunes de TeamViewer
                var teamViewerPaths = new[]
                {
                    @"C:\Program Files\TeamViewer\TeamViewer.exe",
                    @"C:\Program Files (x86)\TeamViewer\TeamViewer.exe",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\TeamViewer\TeamViewer.exe",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\TeamViewer\TeamViewer.exe"
                };

                // Primero verificar ruta configurada en Excel
                if (!string.IsNullOrEmpty(config.TeamViewerPath))
                {
                    teamViewerPaths = new[] { config.TeamViewerPath }.Concat(teamViewerPaths).ToArray();
                }
                
                // Luego verificar appsettings
                var configuredPath = _configuration["SystemTools:TeamViewerPath"];
                if (!string.IsNullOrEmpty(configuredPath))
                {
                    teamViewerPaths = new[] { configuredPath }.Concat(teamViewerPaths).ToArray();
                }

                string? foundPath = teamViewerPaths.FirstOrDefault(System.IO.File.Exists);

                if (foundPath == null)
                {
                    LogSystemAction("launch-teamviewer", request.Username, request.Role, false, "TeamViewer not found");
                    return NotFound(new { success = false, message = "TeamViewer no encontrado en el sistema" });
                }

                LogSystemAction("launch-teamviewer", request.Username, request.Role, true, $"Launching from: {foundPath}");

                // 📋 Audit Log L1 - EU CRA: Acceso remoto es crítico
                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    AuditAction.ServiceStart,
                    AuditResult.Success,
                    $"TeamViewer launched by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)}) from: {foundPath}",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = foundPath,
                        UseShellExecute = true
                    }
                };
                process.Start();

                return Ok(new { success = true, message = "TeamViewer iniciado correctamente" });
            }
            catch (Exception ex)
            {
                LogSystemAction("launch-teamviewer", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al iniciar TeamViewer");
                return StatusCode(500, new { success = false, message = "Error al iniciar TeamViewer: " + ex.Message });
            }
        }

        // ========================================
        // GET: api/system/teamviewer-status
        // ========================================
        /// <summary>
        /// Consulta si el servicio de TeamViewer está corriendo
        /// </summary>
        [HttpGet("teamviewer-status")]
        public async Task<IActionResult> GetTeamViewerStatus()
        {
            var config = await GetSystemConfigAsync();
            if (!config.TeamViewerEnabled)
            {
                return Ok(new { running = false, enabled = false, message = "TeamViewer deshabilitado por configuración" });
            }

            try
            {
                var running = IsServiceRunning("TeamViewer");
                return Ok(new { running, enabled = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando estado de TeamViewer");
                return Ok(new { running = false, enabled = true, error = ex.Message });
            }
        }

        // ========================================
        // POST: api/system/teamviewer-service
        // ========================================
        /// <summary>
        /// Inicia o detiene el servicio de TeamViewer.
        /// Seguridad: el acceso remoto solo está disponible cuando el cliente lo habilita explícitamente.
        /// </summary>
        [HttpPost("teamviewer-service")]
        public async Task<IActionResult> ToggleTeamViewerService([FromBody] TeamViewerServiceRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.TeamViewerEnabled)
            {
                return BadRequest(new { success = false, message = "TeamViewer deshabilitado por configuración", messageKey = "systemTools.errors.teamviewerDisabled" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("teamviewer-service", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            var action = request.Action?.ToLowerInvariant();
            if (action != "start" && action != "stop")
            {
                return BadRequest(new { success = false, message = "Acción inválida. Use 'start' o 'stop'", messageKey = "systemTools.errors.invalidAction" });
            }

            try
            {
                // Buscar el nombre real del servicio TeamViewer
                var serviceName = FindTeamViewerServiceName();
                if (serviceName == null)
                {
                    return NotFound(new { success = false, message = "Servicio TeamViewer no encontrado en el sistema", messageKey = "systemTools.errors.teamviewerNotFound" });
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"{action} \"{serviceName}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var running = action == "start" ? true : false;
                // Verificar estado real tras un breve delay
                await Task.Delay(1000);
                running = IsServiceRunning(serviceName);

                var actionLabel = action == "start" ? "iniciado" : "detenido";
                LogSystemAction($"teamviewer-service-{action}", request.Username, request.Role, true, $"Service {serviceName} {actionLabel}");

                // 📋 Audit Log L1 - EU CRA: Control de acceso remoto
                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    action == "start" ? AuditAction.ServiceStart : AuditAction.ServiceStop,
                    AuditResult.Success,
                    $"TeamViewer service {actionLabel} by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)}). Service: {serviceName}",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );

                return Ok(new { 
                    success = true, 
                    running, 
                    message = $"TeamViewer {actionLabel} correctamente",
                    messageKey = action == "start" ? "systemTools.messages.teamviewerStarted" : "systemTools.messages.teamviewerStopped"
                });
            }
            catch (Exception ex)
            {
                LogSystemAction($"teamviewer-service-{action}", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error controlando servicio TeamViewer");
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message, messageKey = "systemTools.errors.genericError" });
            }
        }

        /// <summary>
        /// Busca el nombre del servicio TeamViewer instalado
        /// </summary>
        private static string? FindTeamViewerServiceName()
        {
            var possibleNames = new[] { "TeamViewer", "TeamViewer_Service", "tvservice" };
            foreach (var name in possibleNames)
            {
                try
                {
                    using var sc = new System.ServiceProcess.ServiceController(name);
                    _ = sc.Status; // Provoca excepción si no existe
                    return name;
                }
                catch { /* Service not found, try next */ }
            }
            return null;
        }

        /// <summary>
        /// Verifica si un servicio Windows está corriendo
        /// </summary>
        private static bool IsServiceRunning(string serviceName)
        {
            try
            {
                using var sc = new System.ServiceProcess.ServiceController(serviceName);
                return sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // GET: api/system/usb-status
        // ========================================
        /// <summary>
        /// Consulta si el almacenamiento USB está habilitado o bloqueado
        /// </summary>
        [HttpGet("usb-status")]
        public IActionResult GetUsbStatus()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\USBSTOR");
                if (key == null)
                    return Ok(new { blocked = false, message = "Registro USBSTOR no encontrado" });

                var startValue = key.GetValue("Start");
                // Start: 3 = Manual (habilitado), 4 = Disabled (bloqueado)
                var blocked = startValue != null && (int)startValue == 4;
                return Ok(new { blocked });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando estado USB");
                return Ok(new { blocked = false, error = ex.Message });
            }
        }

        // ========================================
        // POST: api/system/usb-toggle
        // ========================================
        /// <summary>
        /// Alterna el estado de almacenamiento USB (bloquear/desbloquear)
        /// </summary>
        [HttpPost("usb-toggle")]
        public async Task<IActionResult> ToggleUsb([FromBody] SystemActionRequest request)
        {
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("usb-toggle", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            try
            {
                // Ejecutar el script PowerShell de toggle
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "Kiosk", "Toggle-UsbStorage.ps1");
                if (!System.IO.File.Exists(scriptPath))
                {
                    return NotFound(new { success = false, message = $"Script no encontrado: {scriptPath}", messageKey = "systemTools.errors.scriptNotFound" });
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();

                // Leer estado actualizado
                bool blocked = false;
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\USBSTOR"))
                {
                    if (key != null)
                    {
                        var startValue = key.GetValue("Start");
                        blocked = startValue != null && (int)startValue == 4;
                    }
                }

                var stateLabel = blocked ? "bloqueado" : "habilitado";
                LogSystemAction("usb-toggle", request.Username, request.Role, true, $"USB {stateLabel}");

                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    blocked ? AuditAction.ServiceStop : AuditAction.ServiceStart,
                    AuditResult.Success,
                    $"USB storage {stateLabel} by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)})",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );

                return Ok(new { 
                    success = true, 
                    blocked, 
                    message = $"USB {stateLabel} correctamente",
                    messageKey = blocked ? "systemTools.messages.usbBlocked" : "systemTools.messages.usbEnabled"
                });
            }
            catch (Exception ex)
            {
                LogSystemAction("usb-toggle", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error toggling USB");
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message, messageKey = "systemTools.errors.genericError" });
            }
        }

        // ========================================
        // POST: api/system/restart-app
        // ========================================
        [HttpPost("restart-app")]
        public async Task<IActionResult> RestartApp([FromBody] SystemActionRequest request)
        {
            _logger.LogWarning("🔄 restart-app llamado por {User} con rol {Role}", request?.Username ?? "null", request?.Role ?? "null");
            
            var config = await GetSystemConfigAsync();
            
            _logger.LogWarning("🔄 Config AppRestartEnabled: {Enabled}", config.AppRestartEnabled);
            
            if (!config.AppRestartEnabled)
            {
                return BadRequest(new { success = false, message = "Reinicio de app deshabilitado por configuración", messageKey = "systemTools.errors.restartDisabled" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("restart-app", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            try
            {
                _logger.LogWarning("🔄 restart-app: Iniciando proceso de reinicio...");
                
                LogSystemAction("restart-app", request.Username, request.Role, true, "Restarting application");

                // 📋 Audit Log L1 - EU CRA: Restart es crítico
                _logger.LogWarning("🔄 restart-app: Guardando audit log...");
                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    AuditAction.ServiceStart,
                    AuditResult.Success,
                    $"Application restart (backend + frontend) initiated by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)})",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );
                
                // 🔒 Forzar flush antes del reinicio para no perder el log
                _logger.LogWarning("🔄 restart-app: Forzando flush de audit logs...");
                await _auditLogService.FlushAsync();
                _logger.LogWarning("🔄 restart-app: Flush completado, continuando con reinicio...");

                // Prioridad: Excel > appsettings > rutas comunes
                var browserPaths = new[]
                {
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
                };

                // Primero verificar Excel
                string? browserPath = !string.IsNullOrEmpty(config.KioskBrowserPath) 
                    ? config.KioskBrowserPath 
                    : null;

                // Luego appsettings
                if (browserPath == null)
                {
                    var configuredBrowser = _configuration["SystemTools:BrowserPath"];
                    browserPath = !string.IsNullOrEmpty(configuredBrowser) 
                        ? configuredBrowser 
                        : browserPaths.FirstOrDefault(System.IO.File.Exists);
                }
                else if (!System.IO.File.Exists(browserPath))
                {
                    browserPath = browserPaths.FirstOrDefault(System.IO.File.Exists);
                }

                var kioskArgs = !string.IsNullOrEmpty(config.KioskBrowserArgs)
                    ? config.KioskBrowserArgs
                    : _configuration["SystemTools:KioskUrl"] ?? "--kiosk http://localhost:3001";

                // 🔧 En desarrollo, NO usar modo kiosk para facilitar pruebas
                var isDevelopment = _env.IsDevelopment();
                if (isDevelopment)
                {
                    // Quitar --kiosk de los argumentos en desarrollo
                    kioskArgs = kioskArgs.Replace("--kiosk ", "").Replace("--kiosk", "");
                    _logger.LogInformation("🔧 Modo desarrollo: Deshabilitando --kiosk para facilitar pruebas");
                }

                if (browserPath != null)
                {
                    // Cerrar instancias existentes y reiniciar
                    var browserName = Path.GetFileNameWithoutExtension(browserPath);
                    var killProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/IM {browserName}.exe /F",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    killProcess.Start();
                    killProcess.WaitForExit(3000);

                    // Pequeña pausa
                    System.Threading.Thread.Sleep(1000);

                    // Reiniciar navegador
                    var startProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = browserPath,
                            Arguments = kioskArgs,
                            UseShellExecute = true
                        }
                    };
                    startProcess.Start();
                }

                // 🔄 Reiniciar backend (en producción el servicio Windows lo reiniciará automáticamente)
                var envType = isDevelopment ? "DESARROLLO" : "PRODUCCIÓN";
                _logger.LogWarning("🔄 {Env}: Reiniciando backend en 2 segundos...", envType);
                
                // Dar tiempo a que el navegador arranque y la respuesta se envíe
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    _logger.LogWarning("🔄 Backend terminando para reinicio...");
                    Environment.Exit(0); // En producción el servicio Windows lo reinicia automáticamente
                });
                
                return Ok(new { success = true, message = "Aplicación reiniciando (backend + frontend)...", messageKey = "systemTools.messages.appRestarting" });
            }
            catch (Exception ex)
            {
                LogSystemAction("restart-app", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al reiniciar aplicación");
                return StatusCode(500, new { success = false, message = "Error al reiniciar: " + ex.Message, messageKey = "systemTools.errors.restartFailed" });
            }
        }

        // ========================================
        // POST: api/system/network-diagnostic
        // ========================================
        [HttpPost("network-diagnostic")]
        public async Task<IActionResult> NetworkDiagnostic([FromBody] SystemActionRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.NetworkDiagnosticEnabled)
            {
                return BadRequest(new { success = false, message = "Diagnóstico de red deshabilitado por configuración", messageKey = "systemTools.errors.networkDiagDisabled" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("network-diagnostic", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            try
            {
                LogSystemAction("network-diagnostic", request.Username, request.Role, true, "Running network diagnostic");

                var diagnostics = new List<NetworkTestResult>();

                // Test 1: Ping a localhost
                diagnostics.Add(await PingHost("127.0.0.1", "Localhost"));

                // Test 2: Ping al gateway (de Excel o default)
                var gateway = !string.IsNullOrEmpty(config.GatewayIP) 
                    ? config.GatewayIP 
                    : _configuration["SystemTools:GatewayIP"] ?? "192.168.1.1";
                diagnostics.Add(await PingHost(gateway, "Gateway"));

                // Test 3: Ping a DNS público
                diagnostics.Add(await PingHost("8.8.8.8", "DNS Google"));

                // Test 4: Verificar servicio backend
                diagnostics.Add(new NetworkTestResult
                {
                    Name = "Backend API",
                    Status = "Online",
                    Details = "Servicio activo",
                    Success = true
                });

                // Test 5: Verificar PLC TwinCAT (estado ADS real, no ping)
                // El PLC suele bloquear ICMP pero acepta ADS — InfoPanel usa este mismo estado
                if (!string.IsNullOrEmpty(config.PlcAmsNetId))
                {
                    var isPlcConnected = _twinCATService.IsConnected;
                    _logger.LogInformation("🔍 NetworkDiagnostic: PLC TwinCAT ADS state = {Connected} (NetId={NetId})", isPlcConnected, config.PlcAmsNetId);
                    diagnostics.Add(new NetworkTestResult
                    {
                        Name = "PLC TwinCAT",
                        Host = config.PlcAmsNetId,
                        Status = isPlcConnected ? "Online" : "Offline",
                        ResponseTime = isPlcConnected ? "ADS" : "N/A",
                        Success = isPlcConnected,
                        Details = isPlcConnected ? "Conectado vía ADS" : "Sin conexión ADS"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Diagnóstico completado",
                    messageKey = "systemTools.messages.diagnosticCompleted",
                    timestamp = DateTime.Now,
                    results = diagnostics
                });
            }
            catch (Exception ex)
            {
                LogSystemAction("network-diagnostic", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error en diagnóstico de red");
                return StatusCode(500, new { success = false, message = "Error en diagnóstico: " + ex.Message, messageKey = "systemTools.errors.diagnostic" });
            }
        }

        /// <summary>
        /// Realiza ping a un host
        /// </summary>
        private async Task<NetworkTestResult> PingHost(string host, string name)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(host, 3000);
                
                return new NetworkTestResult
                {
                    Name = name,
                    Host = host,
                    Status = reply.Status == System.Net.NetworkInformation.IPStatus.Success ? "Online" : "Offline",
                    ResponseTime = reply.Status == System.Net.NetworkInformation.IPStatus.Success ? $"{reply.RoundtripTime}ms" : "N/A",
                    Success = reply.Status == System.Net.NetworkInformation.IPStatus.Success,
                    Details = reply.Status.ToString()
                };
            }
            catch (Exception ex)
            {
                return new NetworkTestResult
                {
                    Name = name,
                    Host = host,
                    Status = "Error",
                    Success = false,
                    Details = ex.Message
                };
            }
        }

        // ========================================
        // GET: api/system/allowed-tools
        // ========================================
        /// <summary>
        /// Devuelve lista de herramientas permitidas según el rol y la configuración Excel
        /// </summary>
        [HttpGet("allowed-tools")]
        public async Task<IActionResult> GetAllowedTools([FromQuery] string role)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.KioskModeEnabled)
            {
                return Ok(new { tools = new List<object>(), kioskModeEnabled = false });
            }
            
            if (!await IsAuthorizedRoleAsync(role))
            {
                return Ok(new { tools = new List<object>(), kioskModeEnabled = true });
            }

            var tools = new List<object>();
            
            // TeamViewer
            if (config.TeamViewerEnabled)
            {
                tools.Add(new { 
                    id = "launch-teamviewer", 
                    name = "TeamViewer", 
                    icon = "📡", 
                    description = "Soporte remoto" 
                });
            }
            
            // Reiniciar App
            if (config.AppRestartEnabled)
            {
                tools.Add(new { 
                    id = "restart-app", 
                    name = "Reiniciar App", 
                    icon = "🔄", 
                    description = "Reiniciar navegador" 
                });
            }
            
            // Diagnóstico de Red
            if (config.NetworkDiagnosticEnabled)
            {
                tools.Add(new { 
                    id = "network-diagnostic", 
                    name = "Diagnóstico Red", 
                    icon = "📊", 
                    description = "Verificar conectividad" 
                });
            }
            
            // Custom Tool 1
            if (config.CustomTool1Enabled && !string.IsNullOrEmpty(config.CustomTool1Path))
            {
                tools.Add(new { 
                    id = "custom-tool-1", 
                    name = config.CustomTool1Name, 
                    icon = config.CustomTool1Icon, 
                    description = $"Ejecutar {config.CustomTool1Name}",
                    isCustom = true
                });
            }
            
            // Custom Tool 2
            if (config.CustomTool2Enabled && !string.IsNullOrEmpty(config.CustomTool2Path))
            {
                tools.Add(new { 
                    id = "custom-tool-2", 
                    name = config.CustomTool2Name, 
                    icon = config.CustomTool2Icon, 
                    description = $"Ejecutar {config.CustomTool2Name}",
                    isCustom = true
                });
            }
            
            // Custom Tool 3
            if (config.CustomTool3Enabled && !string.IsNullOrEmpty(config.CustomTool3Path))
            {
                tools.Add(new { 
                    id = "custom-tool-3", 
                    name = config.CustomTool3Name, 
                    icon = config.CustomTool3Icon, 
                    description = $"Ejecutar {config.CustomTool3Name}",
                    isCustom = true
                });
            }
            
            // Cerrar sesión de Windows (siempre último, marcado como peligroso)
            if (config.WindowsLogoutEnabled)
            {
                tools.Add(new { 
                    id = "logout-windows", 
                    name = "Cerrar Sesión", 
                    icon = "🚪", 
                    description = "Logout de Windows", 
                    dangerous = true 
                });
            }

            return Ok(new { 
                tools, 
                kioskModeEnabled = true,
                installationId = config.InstallationId
            });
        }

        // ========================================
        // POST: api/system/launch-custom-tool
        // ========================================
        /// <summary>
        /// Inicia una herramienta personalizada configurada en Excel
        /// </summary>
        [HttpPost("launch-custom-tool")]
        public async Task<IActionResult> LaunchCustomTool([FromBody] CustomToolRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction($"launch-custom-tool-{request.ToolId}", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción", messageKey = "systemTools.errors.unauthorized" });
            }

            try
            {
                string toolPath = "";
                string toolArgs = "";
                string toolName = "";
                bool toolEnabled = false;

                switch (request.ToolId)
                {
                    case "custom-tool-1":
                        toolEnabled = config.CustomTool1Enabled;
                        toolPath = config.CustomTool1Path;
                        toolArgs = config.CustomTool1Args;
                        toolName = config.CustomTool1Name;
                        break;
                    case "custom-tool-2":
                        toolEnabled = config.CustomTool2Enabled;
                        toolPath = config.CustomTool2Path;
                        toolArgs = config.CustomTool2Args;
                        toolName = config.CustomTool2Name;
                        break;
                    case "custom-tool-3":
                        toolEnabled = config.CustomTool3Enabled;
                        toolPath = config.CustomTool3Path;
                        toolArgs = config.CustomTool3Args;
                        toolName = config.CustomTool3Name;
                        break;
                    default:
                        return BadRequest(new { success = false, message = $"Herramienta desconocida: {request.ToolId}", messageKey = "systemTools.errors.unknownTool" });
                }

                if (!toolEnabled)
                {
                    return BadRequest(new { success = false, message = $"Herramienta '{toolName}' deshabilitada", messageKey = "systemTools.errors.toolDisabled" });
                }

                if (!System.IO.File.Exists(toolPath))
                {
                    LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, false, $"File not found: {toolPath}");
                    return NotFound(new { success = false, message = $"No se encontró: {toolPath}", messageKey = "systemTools.errors.toolNotFound" });
                }

                LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, true, $"Launching: {toolPath}");

                // 📋 Audit Log L1 - EU CRA: Ejecución de herramientas externas
                await _auditLogService.LogAsync(
                    AuditCategory.System,
                    AuditAction.ServiceStart,
                    AuditResult.Success,
                    $"Custom tool '{toolName}' ({request.ToolId}) launched by {SanitizeUsername(request.Username)} (role: {SanitizeRole(request.Role)}). Path: {toolPath}",
                    userId: request.Username,
                    projectId: _projectContext.ProjectId
                );

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = toolPath,
                        Arguments = toolArgs,
                        UseShellExecute = true
                    }
                };
                process.Start();

                return Ok(new { success = true, message = $"{toolName} iniciado correctamente", messageKey = "systemTools.messages.toolStarted", tool = toolName });
            }
            catch (Exception ex)
            {
                LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al iniciar herramienta personalizada");
                return StatusCode(500, new { success = false, message = "Error al iniciar: " + ex.Message, messageKey = "systemTools.errors.startFailed" });
            }
        }

        // ========================================
        // GET: api/system/kiosk-config
        // ========================================
        /// <summary>
        /// Obtiene la configuración completa del modo kiosk (para frontend)
        /// </summary>
        [HttpGet("kiosk-config")]
        public async Task<IActionResult> GetKioskConfig()
        {
            var config = await GetSystemConfigAsync();

            return Ok(new
            {
                kioskModeEnabled = config.KioskModeEnabled,
                installationId = config.InstallationId,
                allowedRoles = config.AllowedSystemToolsRoles.Split(',').Select(r => r.Trim()),
                tools = new
                {
                    windowsLogout = config.WindowsLogoutEnabled,
                    appRestart = config.AppRestartEnabled,
                    networkDiagnostic = config.NetworkDiagnosticEnabled,
                    teamViewer = config.TeamViewerEnabled,
                    usbToggle = config.UsbToggleEnabled
                },
                customTools = new[]
                {
                    new { 
                        id = "custom-tool-1", 
                        enabled = config.CustomTool1Enabled, 
                        name = config.CustomTool1Name, 
                        icon = config.CustomTool1Icon 
                    },
                    new { 
                        id = "custom-tool-2", 
                        enabled = config.CustomTool2Enabled, 
                        name = config.CustomTool2Name, 
                        icon = config.CustomTool2Icon 
                    },
                    new { 
                        id = "custom-tool-3", 
                        enabled = config.CustomTool3Enabled, 
                        name = config.CustomTool3Name, 
                        icon = config.CustomTool3Icon 
                    }
                }.Where(t => t.enabled),
                support = new
                {
                    enabled = config.SupportUnlockEnabled,
                    phoneNumber = config.SupportPhoneNumber,
                    email = config.SupportEmail,
                    unlockDurationMinutes = config.SupportUnlockDurationMinutes
                }
            });
        }

        // ========================================
        // PLC Configuration Management
        // ========================================

        /// <summary>
        /// Recarga la configuración de variables PLC desde el Excel.
        /// Útil cuando se modifica el Excel sin reiniciar el backend.
        /// NOTA: El Excel ya NO se recarga automáticamente cada X segundos.
        /// </summary>
        [HttpPost("reload-plc-config")]
        public async Task<IActionResult> ReloadPlcConfiguration()
        {
            try
            {
                var username = User.Identity?.Name ?? "System";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                
                _logger.LogInformation("🔄 Recargando configuración PLC desde Excel (petición manual)...");
                
                await _plcPollingService.ReloadExcelConfigurationAsync();

                // 📋 Audit Log L1 - EU CRA: Cambio de configuración PLC
                await _auditLogService.LogAsync(
                    AuditCategory.Plc,
                    AuditAction.ConfigChange,
                    AuditResult.Success,
                    $"PLC configuration reloaded from Excel by {SanitizeUsername(username)} (role: {SanitizeRole(userRole)})",
                    userId: username,
                    projectId: _projectContext.ProjectId
                );
                
                return Ok(new { 
                    success = true, 
                    message = "Configuración PLC recargada exitosamente desde Excel",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error recargando configuración PLC desde Excel");
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error: {ex.Message}",
                    timestamp = DateTime.Now
                });
            }
        }

        // ========================================
        // POST: api/system/notify-screen
        // ========================================
        /// <summary>
        /// Notifica al PLC la pantalla/vista actual del HMI.
        /// Útil para notificar "login" antes de que SignalR esté conectado.
        /// No requiere autenticación para permitir notificar desde la pantalla de login.
        /// Acepta string vacío para indicar HMI offline.
        /// </summary>
        [HttpPost("notify-screen")]
        [AllowAnonymous]
        [Consumes("application/json", "text/plain", "application/x-www-form-urlencoded")]
        public async Task<IActionResult> NotifyScreen()
        {
            try
            {
                // Leer el body raw para soportar sendBeacon y fetch
                string screenName = "";
                
                using (var reader = new StreamReader(Request.Body))
                {
                    var body = await reader.ReadToEndAsync();
                    _logger.LogDebug("📺 NotifyScreen raw body: '{Body}'", body);
                    
                    if (!string.IsNullOrEmpty(body))
                    {
                        // Intentar parsear como JSON
                        if (body.TrimStart().StartsWith("{"))
                        {
                            try
                            {
                                var json = System.Text.Json.JsonDocument.Parse(body);
                                if (json.RootElement.TryGetProperty("screenName", out var prop))
                                {
                                    screenName = prop.GetString() ?? "";
                                }
                            }
                            catch
                            {
                                // Si falla el JSON, usar el body directamente
                                screenName = body.Trim();
                            }
                        }
                        else
                        {
                            // No es JSON, usar como texto plano
                            screenName = body.Trim();
                        }
                    }
                }

                _logger.LogInformation("📺 API NotifyScreen: '{Screen}' (vacío = offline)", screenName);
                
                // Usar SetActiveView que internamente llama a NotifyPlcCurrentScreenAsync
                // String vacío es válido y significa HMI offline
                _plcPollingService.SetActiveView(screenName);
                
                return Ok(new { 
                    success = true, 
                    message = string.IsNullOrEmpty(screenName) 
                        ? "HMI offline notificado al PLC" 
                        : $"Pantalla '{screenName}' notificada al PLC",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error notificando pantalla al PLC");
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Error: {ex.Message}",
                    timestamp = DateTime.Now
                });
            }
        }

        // ========================================
        // PLC Diagnostic Endpoint
        // ========================================

        /// <summary>
        /// Diagnóstico directo de lectura PLC - Lee una variable específica con tipo explícito
        /// </summary>
        [HttpGet("plc-debug/read")]
        public async Task<IActionResult> ReadPlcVariableDirect(
            [FromQuery] string variableName,
            [FromQuery] string? dataType = null)
        {
            try
            {
                _logger.LogInformation("🔍 PLC Debug: Leyendo variable '{Variable}' tipo={Type}", 
                    variableName, dataType ?? "auto");

                var isConnected = _twinCATService.IsConnected;
                var isSimulated = _twinCATService.IsSimulated;

                // Determinar tipo de dato (default: double para lr_, object si no se puede detectar)
                Type resolvedType = dataType?.ToLower() switch
                {
                    "double" or "lreal" => typeof(double),
                    "float" or "real" => typeof(float),
                    "int" or "dint" => typeof(int),
                    "short" or "int16" => typeof(short),
                    "bool" => typeof(bool),
                    "string" => typeof(string),
                    "byte" => typeof(byte),
                    "uint" => typeof(uint),
                    "ushort" or "uint16" => typeof(ushort),
                    _ => DetectTypeFromVariableName(variableName)
                };

                // Intentar lectura
                object? value = null;
                string? error = null;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    value = await _twinCATService.ReadVariableAsync(variableName, resolvedType);
                }
                catch (Exception readEx)
                {
                    error = readEx.Message;
                    _logger.LogError(readEx, "❌ Error leyendo variable '{Variable}'", variableName);
                }

                stopwatch.Stop();

                // Auto-detect type from variable name for comparison
                string autoDetectedType = "unknown";
                if (variableName.Contains(".lr_")) autoDetectedType = "double (LREAL)";
                else if (variableName.Contains(".r_")) autoDetectedType = "float (REAL)";
                else if (variableName.Contains(".b_") || variableName.Contains(".btn_")) autoDetectedType = "bool";
                else if (variableName.Contains(".n_") || variableName.Contains(".i_")) autoDetectedType = "int";
                else if (variableName.Contains(".s_") || variableName.Contains(".str_")) autoDetectedType = "string";

                return Ok(new
                {
                    success = error == null,
                    variableName,
                    value,
                    valueType = value?.GetType().Name ?? "null",
                    requestedType = dataType ?? "auto",
                    autoDetectedType,
                    plcStatus = new
                    {
                        isConnected,
                        isSimulated,
                        amsNetId = _configuration["TwinCAT:AmsNetId"],
                        port = _configuration["TwinCAT:Port"]
                    },
                    readTimeMs = stopwatch.ElapsedMilliseconds,
                    error,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ PLC Debug: Error general");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Diagnóstico: Lee múltiples índices de un array PLC
        /// </summary>
        [HttpGet("plc-debug/read-array")]
        public async Task<IActionResult> ReadPlcArrayDirect(
            [FromQuery] string baseVariableName,
            [FromQuery] int startIndex = 0,
            [FromQuery] int endIndex = 10,
            [FromQuery] string? dataType = null)
        {
            try
            {
                _logger.LogInformation("🔍 PLC Debug Array: Leyendo '{Base}[{Start}..{End}]'", 
                    baseVariableName, startIndex, endIndex);

                Type resolvedType = dataType?.ToLower() switch
                {
                    "double" or "lreal" => typeof(double),
                    "float" or "real" => typeof(float),
                    "int" or "dint" => typeof(int),
                    "bool" => typeof(bool),
                    _ => DetectTypeFromVariableName(baseVariableName)
                };

                var results = new List<object>();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                for (int i = startIndex; i <= endIndex; i++)
                {
                    var varName = $"{baseVariableName}[{i}]";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(varName, resolvedType);
                        results.Add(new { index = i, value, error = (string?)null });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { index = i, value = (object?)null, error = ex.Message });
                    }
                }

                stopwatch.Stop();

                return Ok(new
                {
                    success = true,
                    baseVariableName,
                    range = $"[{startIndex}..{endIndex}]",
                    dataType = dataType ?? "auto",
                    results,
                    totalReadTimeMs = stopwatch.ElapsedMilliseconds,
                    plcStatus = new
                    {
                        isConnected = _twinCATService.IsConnected,
                        isSimulated = _twinCATService.IsSimulated
                    },
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ PLC Debug Array: Error general");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Auto-detecta el tipo de dato basándose en la convención de nombres TwinCAT
        /// </summary>
        private static Type DetectTypeFromVariableName(string variableName)
        {
            if (variableName.Contains(".lr_")) return typeof(double);  // LREAL
            if (variableName.Contains(".r_")) return typeof(float);    // REAL
            if (variableName.Contains(".b_") || variableName.Contains(".btn_")) return typeof(bool);
            if (variableName.Contains(".n_") || variableName.Contains(".i_")) return typeof(int);
            if (variableName.Contains(".s_") || variableName.Contains(".str_")) return typeof(string);
            if (variableName.Contains(".w_")) return typeof(ushort);   // WORD
            if (variableName.Contains(".dw_")) return typeof(uint);    // DWORD
            return typeof(double); // Default: LREAL para valores numéricos
        }
    }

    // ========================================
    // Modelos de Request/Response
    // ========================================
    
    public class SystemActionRequest
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class NotifyScreenRequest
    {
        public string ScreenName { get; set; } = "";
    }

    public class CustomToolRequest
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string ToolId { get; set; } = "";
    }

    public class TeamViewerServiceRequest
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string Action { get; set; } = ""; // "start" or "stop"
    }

    public class NetworkTestResult
    {
        public string Name { get; set; } = "";
        public string? Host { get; set; }
        public string Status { get; set; } = "";
        public string? ResponseTime { get; set; }
        public bool Success { get; set; }
        public string? Details { get; set; }
    }
}
