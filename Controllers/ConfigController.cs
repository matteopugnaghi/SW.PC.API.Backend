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
        private readonly ILogger<ConfigController> _logger;
        
        public ConfigController(
            IConfigurationService configurationService, 
            IExcelConfigService excelConfigService,
            IMetricsService metricsService,
            ILogger<ConfigController> logger)
        {
            _configurationService = configurationService;
            _excelConfigService = excelConfigService;
            _metricsService = metricsService;
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
        /// <param name="fileName">Excel file name (default: ProjectConfig.xlsm)</param>
        /// <returns>List of 3D models with children configuration</returns>
        [HttpGet("3d-elements")]
        [ProducesResponseType(typeof(List<Model3DConfig>), 200)]
        public async Task<ActionResult<List<Model3DConfig>>> Get3DElements([FromQuery] string fileName = "ProjectConfig.xlsm")
        {
            try
            {
                _logger.LogInformation("📦 Loading 3D elements from Excel: {FileName}", fileName);
                
                var models = await _excelConfigService.Load3DModelsAsync(fileName);
                
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
        /// Get system configuration from Excel (hoja "System Config")
        /// </summary>
        /// <param name="fileName">Excel file name (default: ProjectConfig.xlsm)</param>
        /// <returns>System configuration with service settings, PLC config, etc.</returns>
        [HttpGet("system")]
        [ProducesResponseType(typeof(SystemConfiguration), 200)]
        public async Task<ActionResult<SystemConfiguration>> GetSystemConfiguration([FromQuery] string fileName = "ProjectConfig.xlsm")
        {
            try
            {
                _logger.LogInformation("?? Loading system configuration from Excel: {FileName}", fileName);
                
                var systemConfig = await _excelConfigService.LoadSystemConfigurationAsync(fileName);
                
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
                    timestamp = DateTime.UtcNow 
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
    }
}

