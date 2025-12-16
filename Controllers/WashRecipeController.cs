// ============================================================================
// WashRecipeController.cs - API para Editor de Recetas de Lavado
// ============================================================================
// Endpoints para configuración, lectura y escritura de parámetros de lavado
// Trabaja con Excel (configuración) y PLC (valores en tiempo real)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/wash-recipe")]
    [Authorize]
    public class WashRecipeController : ControllerBase
    {
        private readonly ILogger<WashRecipeController> _logger;
        private readonly IExcelConfigService _excelService;
        private readonly ITwinCATService _twinCatService;
        private readonly IProjectContextService _projectContext;
        
        public WashRecipeController(
            ILogger<WashRecipeController> logger,
            IExcelConfigService excelService,
            ITwinCATService twinCatService,
            IProjectContextService projectContext)
        {
            _logger = logger;
            _excelService = excelService;
            _twinCatService = twinCatService;
            _projectContext = projectContext;
        }
        
        /// <summary>
        /// Obtiene la configuración del editor de recetas desde Excel (hoja WashRecipe)
        /// No incluye valores del PLC, solo la estructura de parámetros
        /// GET /api/wash-recipe/config
        /// </summary>
        [HttpGet("config")]
        public async Task<ActionResult<WashRecipeConfigResponse>> GetConfiguration()
        {
            try
            {
                _logger.LogInformation("🚿 Loading wash recipe configuration from Excel");
                
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadWashRecipeConfigAsync(excelPath);
                
                // Construir URL base para imágenes (endpoint de este mismo controlador)
                // El ImagePath del Excel puede incluir "Images/" o ser solo el nombre del archivo
                var imageBaseUrl = "/api/wash-recipe/image/";
                
                var response = new WashRecipeConfigResponse
                {
                    RecipeNameDescription = config.RecipeNameDescription,
                    LoadedAt = config.LoadedAt,
                    Stations = config.Stations.Select(s => new WashRecipeStationDto
                    {
                        Index = s.Index,
                        Name = s.Name,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) 
                            ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" 
                            : null,
                        BoolParameters = s.BoolParameters.Select(p => new WashRecipeBoolParamDto
                        {
                            Index = p.Index,
                            PlcVariable = p.PlcVariable,
                            Description = p.Description,
                            Value = p.Value,
                            IsConfigured = p.IsConfigured
                        }).ToList(),
                        IntParameters = s.IntParameters.Select(p => new WashRecipeIntParamDto
                        {
                            Index = p.Index,
                            PlcVariable = p.PlcVariable,
                            Description = p.Description,
                            Value = p.Value,
                            MinValue = p.MinValue,
                            MaxValue = p.MaxValue,
                            Unit = p.Unit,
                            IsConfigured = p.IsConfigured
                        }).ToList()
                    }).ToList()
                };
                
                _logger.LogInformation("🚿 Wash recipe config loaded: {StationCount} stations", response.Stations.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚿 Error loading wash recipe configuration");
                return StatusCode(500, new { error = "Error loading wash recipe configuration", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Lee todos los valores actuales del PLC para la configuración de recetas
        /// GET /api/wash-recipe/read-from-plc
        /// </summary>
        [HttpGet("read-from-plc")]
        public async Task<ActionResult<WashRecipePlcOperationResult>> ReadFromPlc()
        {
            try
            {
                _logger.LogInformation("🚿 Reading wash recipe values from PLC");
                
                // Primero obtener la configuración del Excel
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadWashRecipeConfigAsync(excelPath);
                
                var errors = new List<string>();
                int processed = 0;
                int failed = 0;
                
                // Leer valores de cada estación
                foreach (var station in config.Stations)
                {
                    // Leer parámetros BOOL
                    foreach (var param in station.BoolParameters.Where(p => p.IsConfigured))
                    {
                        try
                        {
                            var result = await _twinCatService.ReadVariableAsync(param.PlcVariable, typeof(bool));
                            if (result != null)
                            {
                                param.Value = Convert.ToBoolean(result);
                                processed++;
                            }
                            else
                            {
                                failed++;
                                errors.Add($"Station {station.Index}, Bool {param.Index}: Null value");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            errors.Add($"Station {station.Index}, Bool {param.Index} ({param.PlcVariable}): {ex.Message}");
                        }
                    }
                    
                    // Leer parámetros INT
                    foreach (var param in station.IntParameters.Where(p => p.IsConfigured))
                    {
                        try
                        {
                            var result = await _twinCatService.ReadVariableAsync(param.PlcVariable, typeof(int));
                            if (result != null)
                            {
                                param.Value = Convert.ToInt32(result);
                                processed++;
                            }
                            else
                            {
                                failed++;
                                errors.Add($"Station {station.Index}, Int {param.Index}: Null value");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            errors.Add($"Station {station.Index}, Int {param.Index} ({param.PlcVariable}): {ex.Message}");
                        }
                    }
                }
                
                // Construir respuesta con valores leídos
                var imageBaseUrl = "/api/wash-recipe/image/";
                var responseData = new WashRecipeConfigResponse
                {
                    RecipeNameDescription = config.RecipeNameDescription,
                    LoadedAt = DateTime.Now,
                    Stations = config.Stations.Select(s => new WashRecipeStationDto
                    {
                        Index = s.Index,
                        Name = s.Name,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" : null,
                        BoolParameters = s.BoolParameters.Select(p => new WashRecipeBoolParamDto
                        {
                            Index = p.Index,
                            PlcVariable = p.PlcVariable,
                            Description = p.Description,
                            Value = p.Value,
                            IsConfigured = p.IsConfigured
                        }).ToList(),
                        IntParameters = s.IntParameters.Select(p => new WashRecipeIntParamDto
                        {
                            Index = p.Index,
                            PlcVariable = p.PlcVariable,
                            Description = p.Description,
                            Value = p.Value,
                            MinValue = p.MinValue,
                            MaxValue = p.MaxValue,
                            Unit = p.Unit,
                            IsConfigured = p.IsConfigured
                        }).ToList()
                    }).ToList()
                };
                
                _logger.LogInformation("🚿 Read from PLC completed: {Processed} OK, {Failed} failed", processed, failed);
                
                return Ok(new WashRecipePlcOperationResult
                {
                    Success = failed == 0,
                    Message = failed == 0 
                        ? $"Leídos {processed} parámetros del PLC" 
                        : $"Leídos {processed} parámetros, {failed} fallidos",
                    ParametersProcessed = processed,
                    ParametersFailed = failed,
                    Errors = errors.Count > 0 ? errors : null,
                    Data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚿 Error reading wash recipe from PLC");
                return StatusCode(500, new WashRecipePlcOperationResult
                {
                    Success = false,
                    Message = $"Error reading from PLC: {ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// Escribe todos los valores al PLC
        /// POST /api/wash-recipe/write-to-plc
        /// Body: { stations: [{ stationIndex, boolValues: [...], intValues: [...] }] }
        /// </summary>
        [HttpPost("write-to-plc")]
        public async Task<ActionResult<WashRecipePlcOperationResult>> WriteToPlc([FromBody] WriteWashRecipeToPlcRequest request)
        {
            try
            {
                _logger.LogInformation("🚿 Writing wash recipe values to PLC");
                
                var errors = new List<string>();
                int processed = 0;
                int failed = 0;
                
                foreach (var station in request.Stations)
                {
                    // Escribir parámetros BOOL
                    foreach (var param in station.BoolValues)
                    {
                        if (string.IsNullOrEmpty(param.PlcVariable)) continue;
                        
                        try
                        {
                            var success = await _twinCatService.WriteVariableAsync(
                                param.PlcVariable, 
                                param.Value, 
                                typeof(bool));
                            
                            if (success)
                            {
                                processed++;
                            }
                            else
                            {
                                failed++;
                                errors.Add($"Station {station.StationIndex}, {param.PlcVariable}: Write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            errors.Add($"Station {station.StationIndex}, {param.PlcVariable}: {ex.Message}");
                        }
                    }
                    
                    // Escribir parámetros INT
                    foreach (var param in station.IntValues)
                    {
                        if (string.IsNullOrEmpty(param.PlcVariable)) continue;
                        
                        try
                        {
                            var success = await _twinCatService.WriteVariableAsync(
                                param.PlcVariable, 
                                param.Value, 
                                typeof(int));
                            
                            if (success)
                            {
                                processed++;
                            }
                            else
                            {
                                failed++;
                                errors.Add($"Station {station.StationIndex}, {param.PlcVariable}: Write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            errors.Add($"Station {station.StationIndex}, {param.PlcVariable}: {ex.Message}");
                        }
                    }
                }
                
                _logger.LogInformation("🚿 Write to PLC completed: {Processed} OK, {Failed} failed", processed, failed);
                
                return Ok(new WashRecipePlcOperationResult
                {
                    Success = failed == 0,
                    Message = failed == 0 
                        ? $"Escritos {processed} parámetros al PLC" 
                        : $"Escritos {processed} parámetros, {failed} fallidos",
                    ParametersProcessed = processed,
                    ParametersFailed = failed,
                    Errors = errors.Count > 0 ? errors : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚿 Error writing wash recipe to PLC");
                return StatusCode(500, new WashRecipePlcOperationResult
                {
                    Success = false,
                    Message = $"Error writing to PLC: {ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// Recarga la configuración del Excel (invalida caché)
        /// POST /api/wash-recipe/reload-config
        /// </summary>
        [HttpPost("reload-config")]
        public ActionResult ReloadConfiguration()
        {
            try
            {
                _excelService.InvalidateCache();
                _logger.LogInformation("🚿 Wash recipe configuration cache invalidated");
                return Ok(new { success = true, message = "Configuration cache cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚿 Error reloading configuration");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene una imagen de estación desde la carpeta config/Images del proyecto
        /// GET /api/wash-recipe/image/{imageName}
        /// </summary>
        [HttpGet("image/{imageName}")]
        [AllowAnonymous]
        public IActionResult GetStationImage(string imageName)
        {
            try
            {
                _logger.LogInformation("🖼️ Image request received: '{ImageName}'", imageName);
                
                if (string.IsNullOrEmpty(imageName))
                {
                    return BadRequest("Image name is required");
                }
                
                // Sanitizar el nombre de la imagen
                imageName = Path.GetFileName(imageName);
                
                // Obtener la ruta de imágenes del proyecto activo
                var configPath = _projectContext.ConfigPath;
                var imagesPath = Path.Combine(configPath, "Images", imageName);
                
                _logger.LogInformation("🖼️ Looking for image at: {ImagePath}", imagesPath);
                
                if (!System.IO.File.Exists(imagesPath))
                {
                    _logger.LogWarning("🖼️ Station image not found: {ImagePath}", imagesPath);
                    return NotFound($"Image not found: {imageName}");
                }
                
                // Determinar el content type según la extensión
                var extension = Path.GetExtension(imageName).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    ".bmp" => "image/bmp",
                    _ => "application/octet-stream"
                };
                
                var fileBytes = System.IO.File.ReadAllBytes(imagesPath);
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🖼️ Error loading station image: {ImageName}", imageName);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
