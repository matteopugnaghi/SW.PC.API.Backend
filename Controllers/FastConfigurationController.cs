// ============================================================================
// FastConfigurationController.cs - API para Panel de Configuración Rápida
// ============================================================================
// Endpoints para:
// - GET /api/fast-config/config: Obtener definición de parámetros desde Excel
// - GET /api/fast-config/plc: Leer valores actuales desde PLC
// - POST /api/fast-config/plc: Escribir valores al PLC
// - GET /api/fast-config/image: Obtener imagen de ayuda
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using System.Globalization;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/fast-config")]
    public class FastConfigurationController : ControllerBase
    {
        private readonly IExcelConfigService _excelConfigService;
        private readonly ITwinCATService _twinCATService;
        private readonly IRequestProjectContext _projectContext;
        private readonly IOperationLogService _operationLog;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FastConfigurationController> _logger;

        public FastConfigurationController(
            IExcelConfigService excelConfigService,
            ITwinCATService twinCATService,
            IRequestProjectContext projectContext,
            IOperationLogService operationLog,
            IWebHostEnvironment env,
            ILogger<FastConfigurationController> logger)
        {
            _excelConfigService = excelConfigService;
            _twinCATService = twinCATService;
            _projectContext = projectContext;
            _operationLog = operationLog;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Obtener la definición de parámetros de configuración rápida desde Excel (hoja "Fast_Configuration")
        /// </summary>
        /// <returns>Configuración con parámetros bool, int y lreal</returns>
        [HttpGet("config")]
        [ProducesResponseType(typeof(FastConfigurationResponse), 200)]
        public async Task<ActionResult<FastConfigurationResponse>> GetFastConfiguration()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                _logger.LogInformation("⚡ Loading fast configuration from: {Path}", excelPath);

                var excelConfig = await _excelConfigService.LoadFastConfigurationAsync(excelPath);

                // Construir URL base para imágenes
                var imageBaseUrl = "/api/fast-config/image/";

                // Convertir configuración de Excel a modelo de respuesta
                var config = new FastConfigurationResponse
                {
                    // Títulos desde Excel
                    PageTitle = excelConfig.PageTitle,
                    BoolSectionTitle = excelConfig.BoolSectionTitle,
                    IntSectionTitle = excelConfig.IntSectionTitle,
                    LRealSectionTitle = excelConfig.LRealSectionTitle,

                    BoolParameters = excelConfig.BoolSettings.Select((s, i) => new FastConfigBoolParameter
                    {
                        Id = $"fcbool_{SanitizeId(s.Description)}_{i}",
                        Description = s.Description,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) 
                            ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" 
                            : null,
                        PlcVariable = s.PlcVariable,
                        DisplayOrder = s.RowIndex,
                        Value = false // Valor por defecto, se leerá después
                    }).ToList(),

                    IntParameters = excelConfig.IntSettings.Select((s, i) => new FastConfigIntParameter
                    {
                        Id = $"fcint_{SanitizeId(s.Description)}_{i}",
                        Description = s.Description,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) 
                            ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" 
                            : null,
                        PlcVariable = s.PlcVariable,
                        DisplayOrder = s.RowIndex,
                        MinValue = s.MinValue,
                        MaxValue = s.MaxValue,
                        Unit = s.Unit,
                        Value = 0
                    }).ToList(),

                    LRealParameters = excelConfig.LRealSettings.Select((s, i) => new FastConfigLRealParameter
                    {
                        Id = $"fclreal_{SanitizeId(s.Description)}_{i}",
                        Description = s.Description,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) 
                            ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" 
                            : null,
                        PlcVariable = s.PlcVariable,
                        DisplayOrder = s.RowIndex,
                        MinValue = s.MinValue,
                        MaxValue = s.MaxValue,
                        DecimalPlaces = s.DecimalPlaces,
                        Unit = s.Unit,
                        Value = 0.0
                    }).ToList()
                };

                _logger.LogInformation("⚡ Fast configuration loaded: {BoolCount} bool, {IntCount} int, {LRealCount} lreal",
                    config.BoolParameters.Count, config.IntParameters.Count, config.LRealParameters.Count);

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fast configuration");
                return StatusCode(500, new { error = "Error loading fast configuration", details = ex.Message });
            }
        }

        /// <summary>
        /// Leer todos los valores de parámetros desde el PLC
        /// </summary>
        /// <returns>Valores actuales de todos los parámetros</returns>
        [HttpGet("plc")]
        [ProducesResponseType(typeof(FastConfigurationValuesResponse), 200)]
        public async Task<ActionResult<FastConfigurationValuesResponse>> ReadFromPlc()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadFastConfigurationAsync(excelPath);

                var response = new FastConfigurationValuesResponse
                {
                    Source = "PLC",
                    Timestamp = DateTime.UtcNow
                };

                // Leer parámetros Bool
                for (int i = 0; i < excelConfig.BoolSettings.Count; i++)
                {
                    var setting = excelConfig.BoolSettings[i];
                    var id = $"fcbool_{SanitizeId(setting.Description)}_{i}";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(bool));
                        if (value != null)
                        {
                            response.BoolValues[id] = (bool)value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Could not read fast config bool {Desc} from PLC: {Error}", setting.Description, ex.Message);
                        response.BoolValues[id] = false;
                    }
                }

                // Leer parámetros Int
                for (int i = 0; i < excelConfig.IntSettings.Count; i++)
                {
                    var setting = excelConfig.IntSettings[i];
                    var id = $"fcint_{SanitizeId(setting.Description)}_{i}";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(int));
                        if (value != null)
                        {
                            response.IntValues[id] = Convert.ToInt32(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Could not read fast config int {Desc} from PLC: {Error}", setting.Description, ex.Message);
                        response.IntValues[id] = 0;
                    }
                }

                // Leer parámetros LReal
                for (int i = 0; i < excelConfig.LRealSettings.Count; i++)
                {
                    var setting = excelConfig.LRealSettings[i];
                    var id = $"fclreal_{SanitizeId(setting.Description)}_{i}";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                        if (value != null)
                        {
                            response.LRealValues[id] = Convert.ToDouble(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Could not read fast config lreal {Desc} from PLC: {Error}", setting.Description, ex.Message);
                        response.LRealValues[id] = 0.0;
                    }
                }

                _logger.LogInformation("⚡ Read {Count} fast config values from PLC", 
                    response.BoolValues.Count + response.IntValues.Count + response.LRealValues.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading fast configuration from PLC");
                return StatusCode(500, new { error = "Error reading from PLC", details = ex.Message });
            }
        }

        /// <summary>
        /// Escribir todos los valores de parámetros al PLC
        /// </summary>
        /// <param name="request">Valores a escribir</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("plc")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> WriteToPlc([FromBody] FastConfigurationWriteRequest request)
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadFastConfigurationAsync(excelPath);
                var user = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System";
                
                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();
                var changes = new List<Dictionary<string, string>>();

                // Escribir parámetros Bool (con detección de cambios)
                foreach (var kvp in request.BoolValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.BoolSettings.Count ? excelConfig.BoolSettings[idx] : null;
                    if (setting != null && kvp.Key == $"fcbool_{SanitizeId(setting.Description)}_{idx}")
                    {
                        try
                        {
                            // Leer valor actual del PLC
                            var currentValue = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(bool));
                            var oldValueStr = currentValue?.ToString()?.ToLower() ?? "?";
                            var newValueStr = kvp.Value.ToString().ToLower();
                            
                            var success = await _twinCATService.WriteVariableAsync(setting.PlcVariable, kvp.Value, typeof(bool));
                            if (success)
                            {
                                successCount++;
                                if (oldValueStr != newValueStr)
                                {
                                    changes.Add(new Dictionary<string, string> { 
                                        {"name", setting.Description}, 
                                        {"type", "fcbool"},
                                        {"old", oldValueStr}, 
                                        {"new", newValueStr} 
                                    });
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"Bool '{kvp.Key}': write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"Bool '{kvp.Key}': {ex.Message}");
                        }
                    }
                }

                // Escribir parámetros Int (con detección de cambios)
                foreach (var kvp in request.IntValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.IntSettings.Count ? excelConfig.IntSettings[idx] : null;
                    if (setting != null && kvp.Key == $"fcint_{SanitizeId(setting.Description)}_{idx}")
                    {
                        try
                        {
                            // Leer valor actual del PLC
                            var currentValue = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(int));
                            var oldValueStr = currentValue?.ToString() ?? "?";
                            var newValueStr = kvp.Value.ToString();
                            
                            var success = await _twinCATService.WriteVariableAsync(setting.PlcVariable, kvp.Value, typeof(int));
                            if (success)
                            {
                                successCount++;
                                if (oldValueStr != newValueStr)
                                {
                                    changes.Add(new Dictionary<string, string> { 
                                        {"name", setting.Description}, 
                                        {"type", "fcint"},
                                        {"old", oldValueStr}, 
                                        {"new", newValueStr} 
                                    });
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"Int '{kvp.Key}': write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"Int '{kvp.Key}': {ex.Message}");
                        }
                    }
                }

                // Escribir parámetros LReal (con detección de cambios)
                foreach (var kvp in request.LRealValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.LRealSettings.Count ? excelConfig.LRealSettings[idx] : null;
                    if (setting != null && kvp.Key == $"fclreal_{SanitizeId(setting.Description)}_{idx}")
                    {
                        try
                        {
                            // Leer valor actual del PLC
                            var currentValue = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                            var oldValueStr = currentValue != null ? Convert.ToDouble(currentValue).ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
                            var newValueStr = kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            
                            var success = await _twinCATService.WriteVariableAsync(setting.PlcVariable, kvp.Value, typeof(double));
                            if (success)
                            {
                                successCount++;
                                if (oldValueStr != newValueStr)
                                {
                                    changes.Add(new Dictionary<string, string> { 
                                        {"name", setting.Description}, 
                                        {"type", "fclreal"},
                                        {"old", oldValueStr}, 
                                        {"new", newValueStr} 
                                    });
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"LReal '{kvp.Key}': write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"LReal '{kvp.Key}': {ex.Message}");
                        }
                    }
                }

                // Crear descripción con los primeros cambios
                string description;
                if (changes.Count == 0)
                {
                    description = $"Sin cambios ({successCount} params escritos)";
                }
                else if (changes.Count <= 3)
                {
                    var changeList = string.Join(", ", changes.Select(c => $"{c["name"]} ({c["old"]}→{c["new"]})"));
                    description = $"{changes.Count} cambios → PLC: {changeList}";
                }
                else
                {
                    var first3 = string.Join(", ", changes.Take(3).Select(c => c["name"]));
                    description = $"{changes.Count} cambios → PLC: {first3}...";
                }
                
                if (errorCount > 0)
                {
                    description += $" ({errorCount} errores)";
                }

                // Registrar operación con detalles
                await _operationLog.LogAsync(
                    OperationCategory.Configuration,
                    errorCount == 0 ? OperationAction.FastConfigWritePlc : OperationAction.FastConfigChange,
                    description,
                    user,
                    changes.Count > 0 ? new Dictionary<string, object> { { "changes", changes } } : null);

                _logger.LogInformation("⚡ Wrote {Success} fast config values to PLC ({Changes} changes, {Errors} errors)", successCount, changes.Count, errorCount);

                return Ok(new
                {
                    success = errorCount == 0,
                    successCount,
                    errorCount,
                    changesCount = changes.Count,
                    errors = errors.Take(10).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing fast configuration to PLC");
                return StatusCode(500, new { error = "Error writing to PLC", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtener una imagen de parámetro desde la carpeta config/Images del proyecto
        /// GET /api/fast-config/image/{imageName}
        /// </summary>
        /// <param name="imageName">Nombre del archivo de imagen</param>
        /// <returns>Archivo de imagen</returns>
        [HttpGet("image/{imageName}")]
        [AllowAnonymous]
        public IActionResult GetImage(string imageName)
        {
            try
            {
                _logger.LogDebug("⚡ Fast config image request: '{ImageName}'", imageName);

                if (string.IsNullOrEmpty(imageName))
                {
                    return BadRequest("Image name is required");
                }

                // Sanitizar el nombre de la imagen
                imageName = Path.GetFileName(imageName);

                // Obtener la ruta de imágenes del proyecto activo
                var configPath = _projectContext.ConfigPath;
                var imagesPath = Path.Combine(configPath, "Images", imageName);

                if (!System.IO.File.Exists(imagesPath))
                {
                    _logger.LogWarning("⚡ Fast config image not found: {ImagePath}", imagesPath);
                    return NotFound($"Image not found: {imageName}");
                }

                // Determinar el content type según la extensión
                var contentType = GetContentType(imagesPath);

                var fileBytes = System.IO.File.ReadAllBytes(imagesPath);
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚡ Error loading fast config image: {ImageName}", imageName);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #region Métodos auxiliares privados

        /// <summary>
        /// Sanitiza un nombre para usarlo como parte de un ID
        /// </summary>
        private static string SanitizeId(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            
            return name
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace("(", "_")
                .Replace(")", "_")
                .ToLowerInvariant();
        }

        /// <summary>
        /// Extrae el índice del final del ID (formato: tipo_nombre_indice)
        /// </summary>
        private static int ExtractIndexFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            var lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore < 0) return -1;
            if (int.TryParse(id.Substring(lastUnderscore + 1), out var idx))
                return idx;
            return -1;
        }

        /// <summary>
        /// Obtener content type basado en la extensión del archivo
        /// </summary>
        private static string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    #region DTOs para Fast Configuration

    /// <summary>
    /// Respuesta con la configuración completa del panel de configuración rápida
    /// </summary>
    public class FastConfigurationResponse
    {
        public string PageTitle { get; set; } = "Configuración Rápida";
        public string BoolSectionTitle { get; set; } = "Booleanos";
        public string IntSectionTitle { get; set; } = "Enteros";
        public string LRealSectionTitle { get; set; } = "Decimales";
        
        public List<FastConfigBoolParameter> BoolParameters { get; set; } = new();
        public List<FastConfigIntParameter> IntParameters { get; set; } = new();
        public List<FastConfigLRealParameter> LRealParameters { get; set; } = new();
    }

    public class FastConfigBoolParameter
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool Value { get; set; }
    }

    public class FastConfigIntParameter
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
        public string? Unit { get; set; }
        public int Value { get; set; }
    }

    public class FastConfigLRealParameter
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public int DecimalPlaces { get; set; } = 2;
        public string? Unit { get; set; }
        public double Value { get; set; }
    }

    /// <summary>
    /// Respuesta con valores leídos del PLC
    /// </summary>
    public class FastConfigurationValuesResponse
    {
        public string Source { get; set; } = "PLC";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, bool> BoolValues { get; set; } = new();
        public Dictionary<string, int> IntValues { get; set; } = new();
        public Dictionary<string, double> LRealValues { get; set; } = new();
    }

    /// <summary>
    /// Request para escribir valores al PLC
    /// </summary>
    public class FastConfigurationWriteRequest
    {
        public Dictionary<string, bool> BoolValues { get; set; } = new();
        public Dictionary<string, int> IntValues { get; set; } = new();
        public Dictionary<string, double> LRealValues { get; set; } = new();
    }

    #endregion
}
