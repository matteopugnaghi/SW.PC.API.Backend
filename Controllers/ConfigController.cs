using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigurationService _configurationService;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IMetricsService _metricsService;
        private readonly IRecoveryCodeService _recoveryCodeService;
        private readonly IAuditLogService _auditLog;
        private readonly IRequestProjectContext _projectContext;
        private readonly ILogger<ConfigController> _logger;
        
        public ConfigController(
            IConfigurationService configurationService, 
            IExcelConfigService excelConfigService,
            IMetricsService metricsService,
            IRecoveryCodeService recoveryCodeService,
            IAuditLogService auditLog,
            IRequestProjectContext projectContext,
            ILogger<ConfigController> logger)
        {
            _configurationService = configurationService;
            _excelConfigService = excelConfigService;
            _metricsService = metricsService;
            _recoveryCodeService = recoveryCodeService;
            _auditLog = auditLog;
            _projectContext = projectContext;
            _logger = logger;
        }
        
        /// <summary>
        /// Get application configuration
        /// </summary>
        /// <returns>Application configuration including color and viewer settings</returns>
        [HttpGet]
        [ProducesResponseType(typeof(AppConfiguration), 200)]
        public async Task<ActionResult<AppConfiguration>> GetConfiguration()
        {
            try
            {
                var config = await _configurationService.GetConfigurationAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration");
                return StatusCode(500, "Internal server error while retrieving configuration");
            }
        }
        
        /// <summary>
        /// Update application configuration
        /// </summary>
        /// <param name="configuration">Updated application configuration</param>
        /// <returns>Success status</returns>
        [HttpPost]
        [ProducesResponseType(typeof(AppConfiguration), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<AppConfiguration>> UpdateConfiguration([FromBody] AppConfiguration configuration)
        {
            try
            {
                if (configuration == null)
                {
                    return BadRequest("Configuration cannot be null");
                }
                
                var success = await _configurationService.UpdateConfigurationAsync(configuration);
                
                // 📋 AUDIT LOG: Configuration Change
                await _auditLog.LogAsync(
                    AuditCategory.Configuration,
                    AuditAction.ConfigChange,
                    success ? AuditResult.Success : AuditResult.Failure,
                    $"Actualización de configuración general del sistema",
                    null, User.Identity?.Name ?? "System");
                
                if (!success)
                {
                    return StatusCode(500, "Failed to update configuration");
                }
                
                var updatedConfig = await _configurationService.GetConfigurationAsync();
                return Ok(updatedConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration");
                return StatusCode(500, "Internal server error while updating configuration");
            }
        }
        
        /// <summary>
        /// Update color configuration only
        /// </summary>
        /// <param name="colorConfig">Updated color configuration</param>
        /// <returns>Success status</returns>
        [HttpPost("colors")]
        [ProducesResponseType(typeof(ColorConfiguration), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ColorConfiguration>> UpdateColorConfiguration([FromBody] ColorConfiguration colorConfig)
        {
            try
            {
                if (colorConfig == null)
                {
                    return BadRequest("Color configuration cannot be null");
                }
                
                var success = await _configurationService.UpdateColorConfigurationAsync(colorConfig);
                
                // 📋 AUDIT LOG: Color Configuration Change
                await _auditLog.LogAsync(
                    AuditCategory.Configuration,
                    AuditAction.ConfigChange,
                    success ? AuditResult.Success : AuditResult.Failure,
                    $"Actualización de configuración de colores",
                    null, User.Identity?.Name ?? "System");
                
                if (!success)
                {
                    return StatusCode(500, "Failed to update color configuration");
                }
                
                var updatedConfig = await _configurationService.GetConfigurationAsync();
                return Ok(updatedConfig.Colors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating color configuration");
                return StatusCode(500, "Internal server error while updating color configuration");
            }
        }
        
        /// <summary>
        /// Get only color configuration
        /// </summary>
        /// <returns>Color configuration</returns>
        [HttpGet("colors")]
        [ProducesResponseType(typeof(ColorConfiguration), 200)]
        public async Task<ActionResult<ColorConfiguration>> GetColorConfiguration()
        {
            try
            {
                var config = await _configurationService.GetConfigurationAsync();
                return Ok(config.Colors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving color configuration");
                return StatusCode(500, "Internal server error while retrieving color configuration");
            }
        }
        
        /// <summary>
        /// Get installation ID for support identification
        /// </summary>
        /// <returns>Installation ID and system info</returns>
        [HttpGet("installation-id")]
        [ProducesResponseType(200)]
        public ActionResult GetInstallationId()
        {
            try
            {
                // Usar el servicio centralizado que lee del Excel
                var installationId = _recoveryCodeService.GetInstallationId();
                
                return Ok(new
                {
                    installationId,
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    backendVersion = "1.0.0",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving installation ID");
                return StatusCode(500, "Error retrieving installation ID");
            }
        }
        
        /// <summary>
        /// Get only viewer configuration
        /// </summary>
        /// <returns>Viewer configuration</returns>
        [HttpGet("viewer")]
        [ProducesResponseType(typeof(ViewerConfiguration), 200)]
        public async Task<ActionResult<ViewerConfiguration>> GetViewerConfiguration()
        {
            try
            {
                var config = await _configurationService.GetConfigurationAsync();
                return Ok(config.Viewer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving viewer configuration");
                return StatusCode(500, "Internal server error while retrieving viewer configuration");
            }
        }
        
        /// <summary>
        /// Get PLC state color configuration from Excel (hoja: PLC_State_Colors)
        /// </summary>
        /// <param name="fileName">Nombre del archivo Excel (default: ProjectConfig.xlsx)</param>
        /// <returns>Lista de configuraciones de color por estado PLC</returns>
        [HttpGet("state-colors")]
        [ProducesResponseType(typeof(List<StateColorConfig>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<List<StateColorConfig>>> GetStateColors([FromQuery] string fileName = "ProjectConfig.xlsx")
        {
            try
            {
                _logger.LogInformation("Loading state colors from Excel: {FileName}", fileName);
                var stateColors = await _excelConfigService.LoadStateColorsAsync(fileName);
                
                if (stateColors == null || stateColors.Count == 0)
                {
                    _logger.LogWarning("No state colors found in Excel file: {FileName}", fileName);
                    return NotFound(new { 
                        message = $"No state colors found in {fileName}. Make sure the PLC_State_Colors sheet exists.",
                        fileName = fileName 
                    });
                }
                
                _logger.LogInformation("✅ Returning {Count} state color configurations", stateColors.Count);
                return Ok(stateColors);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Excel file not found: {FileName}", fileName);
                return NotFound(new { 
                    message = $"Excel file not found: {fileName}",
                    fileName = fileName 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading state colors from Excel");
                return StatusCode(500, new { 
                    message = "Internal server error while loading state colors",
                    error = ex.Message 
                });
            }
        }
        
        /// <summary>
        /// Get 3D models configuration from Excel (hoja "3D_Models") with children metadata
        /// </summary>
        /// <param name="fileName">Excel file name or full path (default: project's ProjectConfig.xlsm)</param>
        /// <returns>List of 3D models with children configuration</returns>
        [HttpGet("3d-elements")]
        [ProducesResponseType(typeof(List<Model3DConfig>), 200)]
        public async Task<ActionResult<List<Model3DConfig>>> Get3DElements([FromQuery] string? fileName = null)
        {
            try
            {
                // Usar ruta del proyecto si no se especifica fileName
                var excelPath = fileName ?? _projectContext.ExcelConfigPath;
                _logger.LogInformation("📦 Loading 3D elements from Excel: {ExcelPath} (proyecto: {Project})", excelPath, _projectContext.ProjectId);
                
                var models = await _excelConfigService.Load3DModelsAsync(excelPath);
                
                if (models == null || models.Count == 0)
                {
                    _logger.LogWarning("⚠️ No 3D models found in Excel");
                    return Ok(new List<Model3DConfig>()); // Return empty list instead of 404
                }
                
                _logger.LogInformation("✅ Returning {Count} 3D models with children", models.Count);
                return Ok(models);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Excel file not found: {FileName}", fileName);
                return NotFound(new { 
                    message = $"Excel file not found: {fileName}",
                    fileName = fileName 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading 3D models from Excel");
                return StatusCode(500, new { 
                    message = "Internal server error while loading 3D models",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get 3D elements info display settings from Excel (hoja "3D_Elements_Info_Setting")
        /// Configura qué información mostrar en los elementos 3D, con qué tipo de visualización,
        /// botones de escritura al PLC, y slots de lectura (gauges, sparklines, progress, etc.)
        /// </summary>
        /// <param name="fileName">Excel file name or full path (default: project's ProjectConfig.xlsm)</param>
        /// <param name="forceReload">Si es true, invalida la caché y recarga desde Excel</param>
        /// <returns>List of element info settings with buttons and slots configuration</returns>
        [HttpGet("3d-elements-info-setting")]
        [ProducesResponseType(typeof(List<Models.Excel.ElementInfoSettingConfig>), 200)]
        public async Task<ActionResult<List<Models.Excel.ElementInfoSettingConfig>>> Get3DElementsInfoSetting(
            [FromQuery] string? fileName = null,
            [FromQuery] bool forceReload = false)
        {
            try
            {
                // Usar ruta del proyecto si no se especifica fileName
                var excelPath = fileName ?? _projectContext.ExcelConfigPath;
                
                // Si forceReload, invalidar caché primero
                if (forceReload)
                {
                    _excelConfigService.InvalidateCache(excelPath);
                    _logger.LogInformation("🔄 Caché invalidada para 3D elements info settings (forceReload=true)");
                }
                
                _logger.LogInformation("🎛️ Loading 3D elements info settings from Excel: {ExcelPath} (proyecto: {Project})", excelPath, _projectContext.ProjectId);
                
                var settings = await _excelConfigService.Load3DElementsInfoSettingAsync(excelPath);
                
                if (settings == null || settings.Count == 0)
                {
                    _logger.LogWarning("⚠️ No 3D elements info settings found in Excel");
                    return Ok(new List<Models.Excel.ElementInfoSettingConfig>()); // Return empty list instead of 404
                }
                
                // ⭐ Convertir imágenes a base64 para evitar problemas de CORS
                var configPath = _projectContext.ConfigPath;
                foreach (var setting in settings)
                {
                    // ModelIcon
                    if (!string.IsNullOrWhiteSpace(setting.ModelIcon) && IsImagePath(setting.ModelIcon))
                    {
                        setting.ModelIconBase64 = await LoadImageAsBase64(configPath, setting.ModelIcon);
                    }
                    
                    // Slot Icons
                    foreach (var slot in setting.Slots)
                    {
                        if (!string.IsNullOrWhiteSpace(slot.Icon) && IsImagePath(slot.Icon))
                        {
                            slot.IconBase64 = await LoadImageAsBase64(configPath, slot.Icon);
                        }
                    }
                }
                
                // Log de variables PLC para diagnóstico
                var allPlcVars = settings.SelectMany(s => s.GetAllPlcVariables()).Distinct().ToList();
                _logger.LogInformation("✅ Returning {Count} elements info settings with {VarCount} unique PLC variables", settings.Count, allPlcVars.Count);
                
                return Ok(settings);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Excel file not found: {FileName}", fileName);
                return NotFound(new { 
                    message = $"Excel file not found: {fileName}",
                    fileName = fileName 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading 3D elements info settings from Excel");
                return StatusCode(500, new { 
                    message = "Internal server error while loading 3D elements info settings",
                    error = ex.Message 
                });
            }
        }
        
        /// <summary>
        /// Get system configuration from Excel (hoja "System Config")
        /// </summary>
        /// <param name="fileName">Excel file name or full path (default: project's ProjectConfig.xlsm)</param>
        /// <returns>System configuration with service settings, PLC config, etc.</returns>
        [HttpGet("system")]
        [ProducesResponseType(typeof(SystemConfiguration), 200)]
        public async Task<ActionResult<SystemConfiguration>> GetSystemConfiguration([FromQuery] string? fileName = null)
        {
            try
            {
                // Usar ruta del proyecto si no se especifica fileName
                var excelPath = fileName ?? _projectContext.ExcelConfigPath;
                _logger.LogInformation("📄 Loading system configuration from Excel: {ExcelPath} (proyecto: {Project})", excelPath, _projectContext.ProjectId);
                
                var systemConfig = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                
                if (systemConfig == null)
                {
                    _logger.LogWarning("?? No system configuration found in Excel");
                    return NotFound(new { message = "System configuration not found in Excel file" });
                }
                
                _logger.LogInformation("? Returning system configuration");
                return Ok(systemConfig);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Excel file not found: {FileName}", fileName);
                return NotFound(new { 
                    message = $"Excel file not found: {fileName}",
                    fileName = fileName 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system configuration from Excel");
                return StatusCode(500, new { 
                    message = "Internal server error while loading system configuration",
                    error = ex.Message 
                });
            }
        }
        
        /// <summary>
        /// Invalidate cache and force reload of system configuration
        /// </summary>
        [HttpPost("system/reload")]
        [ProducesResponseType(200)]
        public ActionResult ReloadSystemConfiguration()
        {
            try
            {
                _logger.LogInformation("🔄 Invalidating system configuration cache");
                _excelConfigService.InvalidateCache();
                return Ok(new { 
                    message = "System configuration cache invalidated. Next request will reload from Excel.",
                    timestamp = DateTime.Now 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache");
                return StatusCode(500, new { 
                    message = "Error invalidating cache",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Get real-time system performance metrics
        /// </summary>
        /// <returns>Current system metrics including scan times and connection counts</returns>
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(SystemMetrics), 200)]
        public ActionResult<SystemMetrics> GetSystemMetrics()
        {
            try
            {
                _logger.LogInformation("📊 Getting system metrics");
                
                var metrics = _metricsService.GetCurrentMetrics();
                
                _logger.LogInformation("✅ Returning system metrics");
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system metrics");
                return StatusCode(500, new { 
                    message = "Internal server error while getting system metrics",
                    error = ex.Message 
                });
            }
        }

        #region Helper Methods para carga de imágenes

        /// <summary>
        /// Verifica si el string es una ruta de imagen (no emoji)
        /// </summary>
        private static bool IsImagePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            
            // Si contiene / o \ es una ruta
            if (value.Contains('/') || value.Contains('\\')) return true;
            
            // Si termina en extensión de imagen
            var lowerValue = value.ToLowerInvariant();
            return lowerValue.EndsWith(".png") || 
                   lowerValue.EndsWith(".jpg") || 
                   lowerValue.EndsWith(".jpeg") ||
                   lowerValue.EndsWith(".gif") ||
                   lowerValue.EndsWith(".webp") ||
                   lowerValue.EndsWith(".svg");
        }

        /// <summary>
        /// Carga una imagen y la convierte a data URL base64
        /// </summary>
        private async Task<string?> LoadImageAsBase64(string? configPath, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(imagePath))
                return null;

            try
            {
                // Sanitizar path
                var sanitizedPath = imagePath
                    .TrimStart('/', '\\')
                    .Replace("..", "")
                    .Trim();

                if (string.IsNullOrWhiteSpace(sanitizedPath))
                    return null;

                // Construir ruta completa
                var fullPath = Path.GetFullPath(Path.Combine(configPath, sanitizedPath));
                
                // Verificar que está dentro del config path
                if (!fullPath.StartsWith(Path.GetFullPath(configPath), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("⚠️ Image path outside config folder: {Path}", imagePath);
                    return null;
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogDebug("🖼️ Image not found for base64: {Path}", fullPath);
                    return null;
                }

                // Leer y convertir a base64
                var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                var contentType = GetImageContentType(fullPath);
                var base64 = Convert.ToBase64String(bytes);
                
                _logger.LogDebug("🖼️ Image converted to base64: {Path} ({Size} bytes)", imagePath, bytes.Length);
                
                return $"data:{contentType};base64,{base64}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error loading image as base64: {Path}", imagePath);
                return null;
            }
        }

        /// <summary>
        /// Obtiene el content type de una imagen según su extensión
        /// </summary>
        private static string GetImageContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        #endregion
        
        #region ⚡ Semiautomatic Mode

        /// <summary>
        /// Get semiautomatic mode configuration from Excel
        /// </summary>
        /// <returns>Semiautomatic mode configuration with elements and visibility settings</returns>
        [HttpGet("semiautomatic")]
        [ProducesResponseType(typeof(SemiautomaticConfiguration), 200)]
        public async Task<ActionResult<SemiautomaticConfiguration>> GetSemiautomaticConfig()
        {
            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                _logger.LogInformation("⚡ Loading Semiautomatic_Mode config from: {Path}", excelPath);
                
                var config = await _excelConfigService.LoadSemiautomaticConfigAsync(excelPath);
                
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Semiautomatic_Mode configuration: {Message}", ex.Message);
                return StatusCode(500, new { error = "Error loading semiautomatic configuration", detail = ex.Message });
            }
        }

        #endregion
        
        #region 📟 PLC Info Panel

        /// <summary>
        /// Get PLC Info Panel configuration from Excel sheet "Plc_InfoPanel".
        /// Returns card configuration with lines that display WSTRING variables from PLC.
        /// </summary>
        /// <returns>PLC Info Panel configuration</returns>
        [HttpGet("plc-info-panel")]
        [ProducesResponseType(typeof(PlcInfoPanelConfig), 200)]
        public async Task<ActionResult<PlcInfoPanelConfig>> GetPlcInfoPanelConfig()
        {
            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                _logger.LogInformation("📟 Loading Plc_InfoPanel config from: {Path}", excelPath);
                
                var config = await _excelConfigService.LoadPlcInfoPanelAsync(excelPath);
                
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📟 Error loading PLC Info Panel configuration: {Message}", ex.Message);
                return StatusCode(500, new { error = "Error loading PLC Info Panel configuration", detail = ex.Message });
            }
        }

        #endregion
    }
}

