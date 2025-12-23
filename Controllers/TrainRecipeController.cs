// ============================================================================
// TrainRecipeController.cs - API para Editor de Recetas de Tren
// ============================================================================
// Endpoints para configuración, lectura y escritura de parámetros de tren
// Trabaja con Excel (configuración) y PLC (valores en tiempo real)
// Similar a WashRecipeController pero simplificado (sin estaciones)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/train-recipe")]
    [Authorize]
    public class TrainRecipeController : ControllerBase
    {
        private readonly ILogger<TrainRecipeController> _logger;
        private readonly IExcelConfigService _excelService;
        private readonly ITwinCATService _twinCatService;
        private readonly IProjectContextService _projectContext;
        
        public TrainRecipeController(
            ILogger<TrainRecipeController> logger,
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
        /// Obtiene la configuración del editor de recetas de tren desde Excel (hoja TrainRecipe)
        /// No incluye valores del PLC, solo la estructura de parámetros
        /// GET /api/train-recipe/config
        /// </summary>
        [HttpGet("config")]
        [AllowAnonymous]
        public async Task<ActionResult<TrainRecipeConfigResponse>> GetConfiguration()
        {
            try
            {
                _logger.LogInformation("🚂 Loading train recipe configuration from Excel");
                
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                // Construir URL base para imágenes
                var imageBaseUrl = "/api/train-recipe/image/";
                
                var response = new TrainRecipeConfigResponse
                {
                    TitleLabel = config.TitleLabel,
                    TrainNamePlcVariable = config.TrainNamePlcVariable,
                    LineNumberPlcVariable = config.LineNumberPlcVariable,
                    TrainNameValue = config.TrainNameValue,
                    LineNumberValue = config.LineNumberValue,
                    LoadedAt = config.LoadedAt,
                    // Nombres de secciones desde Excel (B2, F2, N2)
                    SectionBoolName = config.SectionBoolName,
                    SectionDecimalName = config.SectionDecimalName,
                    SectionGantryName = config.SectionGantryName,
                    // Imágenes de secciones desde Excel (D2, H2)
                    SectionBoolImageUrl = !string.IsNullOrEmpty(config.SectionBoolImage) ? $"{imageBaseUrl}{Path.GetFileName(config.SectionBoolImage)}" : null,
                    SectionDecimalImageUrl = !string.IsNullOrEmpty(config.SectionDecimalImage) ? $"{imageBaseUrl}{Path.GetFileName(config.SectionDecimalImage)}" : null,
                    BoolParameters = config.BoolParameters.Select(p => new TrainRecipeParamDto
                    {
                        Index = p.Index,
                        RowIndex = p.RowIndex,
                        Name = p.Name,
                        ImageUrl = !string.IsNullOrEmpty(p.Image) 
                            ? $"{imageBaseUrl}{Path.GetFileName(p.Image)}" 
                            : null,
                        PlcVariable = p.PlcVariable,
                        DataType = p.DataType,
                        BoolValue = p.BoolValue,
                        IsConfigured = p.IsConfigured
                    }).ToList(),
                    DecimalParameters = config.DecimalParameters.Select(p => new TrainRecipeParamDto
                    {
                        Index = p.Index,
                        RowIndex = p.RowIndex,
                        Name = p.Name,
                        ImageUrl = !string.IsNullOrEmpty(p.Image) 
                            ? $"{imageBaseUrl}{Path.GetFileName(p.Image)}" 
                            : null,
                        PlcVariable = p.PlcVariable,
                        DataType = p.DataType,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Decimals = p.Decimals,
                        Unit = p.Unit,
                        DecimalValue = p.DecimalValue,
                        IsConfigured = p.IsConfigured
                    }).ToList()
                };
                
                _logger.LogInformation("🚂 Train recipe config loaded: {BoolCount} BOOL, {DecimalCount} DECIMAL parameters", 
                    response.BoolParameters.Count, response.DecimalParameters.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error loading train recipe configuration");
                return StatusCode(500, new { error = "Error loading train recipe configuration", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Lee todos los valores actuales del PLC para la configuración de recetas de tren
        /// GET /api/train-recipe/read-from-plc
        /// </summary>
        [HttpGet("read-from-plc")]
        public async Task<ActionResult<TrainRecipePlcOperationResult>> ReadFromPlc()
        {
            try
            {
                _logger.LogInformation("🚂 Reading train recipe values from PLC");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Primero obtener la configuración del Excel
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
                int processed = 0;
                int failed = 0;
                
                // Leer nombre del tren desde PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(config.TrainNamePlcVariable))
                {
                    try
                    {
                        var result = await _twinCatService.ReadVariableAsync(config.TrainNamePlcVariable, typeof(string));
                        if (result != null)
                        {
                            config.TrainNameValue = result.ToString() ?? string.Empty;
                            Interlocked.Increment(ref processed);
                        }
                        else
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"TrainName ({config.TrainNamePlcVariable}): Null value");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        errors.Add($"TrainName: {ex.Message}");
                    }
                }
                
                // Leer número de línea desde PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(config.LineNumberPlcVariable))
                {
                    try
                    {
                        var result = await _twinCatService.ReadVariableAsync(config.LineNumberPlcVariable, typeof(int));
                        if (result != null)
                        {
                            config.LineNumberValue = Convert.ToInt32(result);
                            Interlocked.Increment(ref processed);
                        }
                        else
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"LineNumber ({config.LineNumberPlcVariable}): Null value");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        errors.Add($"LineNumber: {ex.Message}");
                    }
                }
                
                // Preparar tareas de lectura en paralelo para parámetros BOOL
                var readTasks = new List<Task>();
                
                foreach (var param in config.BoolParameters.Where(p => p.IsConfigured))
                {
                    var localParam = param;
                    readTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _twinCatService.ReadVariableAsync(localParam.PlcVariable, typeof(bool));
                            if (result != null)
                            {
                                localParam.BoolValue = Convert.ToBoolean(result);
                                Interlocked.Increment(ref processed);
                            }
                            else
                            {
                                Interlocked.Increment(ref failed);
                                errors.Add($"BOOL {localParam.Index} ({localParam.PlcVariable}): Null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"BOOL {localParam.Index}: {ex.Message}");
                        }
                    }));
                }
                
                // Preparar tareas de lectura en paralelo para parámetros DECIMAL
                foreach (var param in config.DecimalParameters.Where(p => p.IsConfigured))
                {
                    var localParam = param;
                    readTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _twinCatService.ReadVariableAsync(localParam.PlcVariable, typeof(double));
                            if (result != null)
                            {
                                localParam.DecimalValue = Convert.ToDouble(result);
                                Interlocked.Increment(ref processed);
                            }
                            else
                            {
                                Interlocked.Increment(ref failed);
                                errors.Add($"DECIMAL {localParam.Index} ({localParam.PlcVariable}): Null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"DECIMAL {localParam.Index}: {ex.Message}");
                        }
                    }));
                }
                
                // Ejecutar todas las lecturas en paralelo
                if (readTasks.Count > 0)
                {
                    await Task.WhenAll(readTasks);
                }
                
                stopwatch.Stop();
                _logger.LogInformation("🚂 Read from PLC completed in {Ms}ms: {Processed} OK, {Failed} failed", 
                    stopwatch.ElapsedMilliseconds, processed, failed);
                
                // Construir respuesta con valores leídos
                var imageBaseUrl = "/api/train-recipe/image/";
                var responseData = new TrainRecipeConfigResponse
                {
                    TitleLabel = config.TitleLabel,
                    TrainNamePlcVariable = config.TrainNamePlcVariable,
                    LineNumberPlcVariable = config.LineNumberPlcVariable,
                    TrainNameValue = config.TrainNameValue,
                    LineNumberValue = config.LineNumberValue,
                    LoadedAt = DateTime.Now,
                    // Nombres de secciones desde Excel (B2, F2, N2)
                    SectionBoolName = config.SectionBoolName,
                    SectionDecimalName = config.SectionDecimalName,
                    SectionGantryName = config.SectionGantryName,
                    // Imágenes de secciones desde Excel (D2, H2)
                    SectionBoolImageUrl = !string.IsNullOrEmpty(config.SectionBoolImage) ? $"{imageBaseUrl}{Path.GetFileName(config.SectionBoolImage)}" : null,
                    SectionDecimalImageUrl = !string.IsNullOrEmpty(config.SectionDecimalImage) ? $"{imageBaseUrl}{Path.GetFileName(config.SectionDecimalImage)}" : null,
                    BoolParameters = config.BoolParameters.Select(p => new TrainRecipeParamDto
                    {
                        Index = p.Index,
                        RowIndex = p.RowIndex,
                        Name = p.Name,
                        ImageUrl = !string.IsNullOrEmpty(p.Image) ? $"{imageBaseUrl}{Path.GetFileName(p.Image)}" : null,
                        PlcVariable = p.PlcVariable,
                        DataType = p.DataType,
                        BoolValue = p.BoolValue,
                        IsConfigured = p.IsConfigured
                    }).ToList(),
                    DecimalParameters = config.DecimalParameters.Select(p => new TrainRecipeParamDto
                    {
                        Index = p.Index,
                        RowIndex = p.RowIndex,
                        Name = p.Name,
                        ImageUrl = !string.IsNullOrEmpty(p.Image) ? $"{imageBaseUrl}{Path.GetFileName(p.Image)}" : null,
                        PlcVariable = p.PlcVariable,
                        DataType = p.DataType,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Decimals = p.Decimals,
                        Unit = p.Unit,
                        DecimalValue = p.DecimalValue,
                        IsConfigured = p.IsConfigured
                    }).ToList()
                };
                
                var errorsList = errors.ToList();
                return Ok(new TrainRecipePlcOperationResult
                {
                    Success = failed == 0,
                    Message = failed == 0 
                        ? $"Leídos {processed} parámetros del PLC en {stopwatch.ElapsedMilliseconds}ms" 
                        : $"Leídos {processed} parámetros, {failed} fallidos ({stopwatch.ElapsedMilliseconds}ms)",
                    ParametersProcessed = processed,
                    ParametersFailed = failed,
                    Errors = errorsList.Count > 0 ? errorsList : null,
                    Data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error reading train recipe from PLC");
                return StatusCode(500, new TrainRecipePlcOperationResult
                {
                    Success = false,
                    Message = $"Error reading from PLC: {ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// Escribe todos los valores al PLC
        /// POST /api/train-recipe/write-to-plc
        /// </summary>
        [HttpPost("write-to-plc")]
        public async Task<ActionResult<TrainRecipePlcOperationResult>> WriteToPlc([FromBody] WriteTrainRecipeToPlcRequest request)
        {
            try
            {
                _logger.LogInformation("🚂 Writing train recipe values to PLC");
                
                var errors = new List<string>();
                int processed = 0;
                int failed = 0;
                
                // Escribir nombre del tren al PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(request.TrainNamePlcVariable) && request.TrainNameValue != null)
                {
                    try
                    {
                        var success = await _twinCatService.WriteVariableAsync(
                            request.TrainNamePlcVariable,
                            request.TrainNameValue,
                            typeof(string));
                        
                        if (success)
                        {
                            processed++;
                            _logger.LogDebug("🚂 Train name written to PLC: {Name}", request.TrainNameValue);
                        }
                        else
                        {
                            failed++;
                            errors.Add($"TrainName ({request.TrainNamePlcVariable}): Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"TrainName ({request.TrainNamePlcVariable}): {ex.Message}");
                    }
                }
                
                // Escribir número de línea al PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(request.LineNumberPlcVariable) && request.LineNumberValue.HasValue)
                {
                    try
                    {
                        var success = await _twinCatService.WriteVariableAsync(
                            request.LineNumberPlcVariable,
                            request.LineNumberValue.Value,
                            typeof(int));
                        
                        if (success)
                        {
                            processed++;
                            _logger.LogDebug("🚂 Line number written to PLC: {Value}", request.LineNumberValue.Value);
                        }
                        else
                        {
                            failed++;
                            errors.Add($"LineNumber ({request.LineNumberPlcVariable}): Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"LineNumber ({request.LineNumberPlcVariable}): {ex.Message}");
                    }
                }
                
                // Escribir parámetros BOOL
                foreach (var param in request.BoolValues)
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
                            errors.Add($"BOOL {param.PlcVariable}: Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"BOOL {param.PlcVariable}: {ex.Message}");
                    }
                }
                
                // Escribir parámetros DECIMAL
                foreach (var param in request.DecimalValues)
                {
                    if (string.IsNullOrEmpty(param.PlcVariable)) continue;
                    
                    try
                    {
                        var success = await _twinCatService.WriteVariableAsync(
                            param.PlcVariable, 
                            param.Value, 
                            typeof(double));
                        
                        if (success)
                        {
                            processed++;
                        }
                        else
                        {
                            failed++;
                            errors.Add($"DECIMAL {param.PlcVariable}: Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"DECIMAL {param.PlcVariable}: {ex.Message}");
                    }
                }
                
                _logger.LogInformation("🚂 Write to PLC completed: {Processed} OK, {Failed} failed", processed, failed);
                
                return Ok(new TrainRecipePlcOperationResult
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
                _logger.LogError(ex, "🚂 Error writing train recipe to PLC");
                return StatusCode(500, new TrainRecipePlcOperationResult
                {
                    Success = false,
                    Message = $"Error writing to PLC: {ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// Recarga la configuración del Excel (invalida caché)
        /// POST /api/train-recipe/reload-config
        /// </summary>
        [HttpPost("reload-config")]
        public ActionResult ReloadConfiguration()
        {
            try
            {
                _excelService.InvalidateCache();
                _logger.LogInformation("🚂 Train recipe configuration cache invalidated");
                return Ok(new { success = true, message = "Configuration cache cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error reloading configuration");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene una imagen de parámetro desde la carpeta config/Images del proyecto
        /// GET /api/train-recipe/image/{imageName}
        /// </summary>
        [HttpGet("image/{imageName}")]
        [AllowAnonymous]
        public IActionResult GetParameterImage(string imageName)
        {
            try
            {
                _logger.LogDebug("🖼️ Train recipe image request: '{ImageName}'", imageName);
                
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
                    _logger.LogWarning("🖼️ Train recipe image not found: {ImagePath}", imagesPath);
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
                _logger.LogError(ex, "🖼️ Error loading train recipe image: {ImageName}", imageName);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
