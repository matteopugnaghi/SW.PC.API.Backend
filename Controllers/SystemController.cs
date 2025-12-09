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

        // Cache de configuración del sistema (por proyecto)
        private static readonly Dictionary<string, (SystemConfiguration Config, DateTime Timestamp)> _configCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public SystemController(
            ILogger<SystemController> logger, 
            IConfiguration configuration,
            IExcelConfigService excelConfigService,
            IWebHostEnvironment env,
            IRequestProjectContext projectContext)
        {
            _logger = logger;
            _configuration = configuration;
            _excelConfigService = excelConfigService;
            _env = env;
            _projectContext = projectContext;
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
                if (DateTime.UtcNow - cached.Timestamp < _cacheExpiration)
                {
                    return cached.Config;
                }
            }

            _logger.LogInformation("📁 SystemController: Cargando config desde {Path} (proyecto: {Project})", excelPath, projectId);
            var config = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
            _configCache[projectId] = (config, DateTime.UtcNow);
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
            
            // TODO: Guardar en base de datos de auditoría
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
                return BadRequest(new { success = false, message = "Función deshabilitada por configuración" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("logout-windows", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción" });
            }

            try
            {
                LogSystemAction("logout-windows", request.Username, request.Role, true, "Initiating Windows logout");
                
                // Ejecutar comando de logout de Windows
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/l /f", // /l = logout, /f = force
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                return Ok(new { success = true, message = "Cerrando sesión de Windows..." });
            }
            catch (Exception ex)
            {
                LogSystemAction("logout-windows", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al cerrar sesión de Windows");
                return StatusCode(500, new { success = false, message = "Error al cerrar sesión: " + ex.Message });
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
        // POST: api/system/restart-app
        // ========================================
        [HttpPost("restart-app")]
        public async Task<IActionResult> RestartApp([FromBody] SystemActionRequest request)
        {
            var config = await GetSystemConfigAsync();
            
            if (!config.AppRestartEnabled)
            {
                return BadRequest(new { success = false, message = "Reinicio de app deshabilitado por configuración" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("restart-app", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción" });
            }

            try
            {
                LogSystemAction("restart-app", request.Username, request.Role, true, "Restarting application");

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

                return Ok(new { success = true, message = "Aplicación reiniciada" });
            }
            catch (Exception ex)
            {
                LogSystemAction("restart-app", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al reiniciar aplicación");
                return StatusCode(500, new { success = false, message = "Error al reiniciar: " + ex.Message });
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
                return BadRequest(new { success = false, message = "Diagnóstico de red deshabilitado por configuración" });
            }
            
            if (!await IsAuthorizedRoleAsync(request.Role))
            {
                LogSystemAction("network-diagnostic", request.Username, request.Role, false, "Unauthorized role");
                return Unauthorized(new { success = false, message = "No autorizado para esta acción" });
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
                    Details = $"Puerto {config.ApiPort} activo",
                    Success = true
                });

                // Test 5: Verificar PLC (si está configurado)
                var plcIp = config.PlcAmsNetId?.Split('.').Take(4).FirstOrDefault() 
                    ?? _configuration["TwinCAT:PlcIpAddress"];
                if (!string.IsNullOrEmpty(plcIp) && plcIp != "127")
                {
                    diagnostics.Add(await PingHost(plcIp, "PLC TwinCAT"));
                }

                return Ok(new
                {
                    success = true,
                    message = "Diagnóstico completado",
                    timestamp = DateTime.Now,
                    results = diagnostics
                });
            }
            catch (Exception ex)
            {
                LogSystemAction("network-diagnostic", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error en diagnóstico de red");
                return StatusCode(500, new { success = false, message = "Error en diagnóstico: " + ex.Message });
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
                return Unauthorized(new { success = false, message = "No autorizado para esta acción" });
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
                        return BadRequest(new { success = false, message = $"Herramienta desconocida: {request.ToolId}" });
                }

                if (!toolEnabled)
                {
                    return BadRequest(new { success = false, message = $"Herramienta '{toolName}' deshabilitada" });
                }

                if (!System.IO.File.Exists(toolPath))
                {
                    LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, false, $"File not found: {toolPath}");
                    return NotFound(new { success = false, message = $"No se encontró: {toolPath}" });
                }

                LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, true, $"Launching: {toolPath}");

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

                return Ok(new { success = true, message = $"{toolName} iniciado correctamente" });
            }
            catch (Exception ex)
            {
                LogSystemAction($"launch-{request.ToolId}", request.Username, request.Role, false, ex.Message);
                _logger.LogError(ex, "Error al iniciar herramienta personalizada");
                return StatusCode(500, new { success = false, message = "Error al iniciar: " + ex.Message });
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
                    teamViewer = config.TeamViewerEnabled
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
    }

    // ========================================
    // Modelos de Request/Response
    // ========================================
    
    public class SystemActionRequest
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class CustomToolRequest
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string ToolId { get; set; } = "";
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
