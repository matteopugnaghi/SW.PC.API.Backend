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
                
                // Leer número de tablas del Gantry desde PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(config.GantryTableCountPlcVariable))
                {
                    try
                    {
                        var result = await _twinCatService.ReadVariableAsync(config.GantryTableCountPlcVariable, typeof(int));
                        if (result != null)
                        {
                            config.GantryTableCountValue = Convert.ToInt32(result);
                            _logger.LogDebug("🚂 GantryTableCount read from PLC: {Value}", config.GantryTableCountValue);
                            Interlocked.Increment(ref processed);
                        }
                        else
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"GantryTableCount ({config.GantryTableCountPlcVariable}): Null value");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        errors.Add($"GantryTableCount: {ex.Message}");
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
                
                // Preparar tareas de lectura en paralelo para parámetros GANTRY CONFIG
                foreach (var param in config.GantryConfigParameters.Where(p => p.IsConfigured))
                {
                    var localParam = param;
                    readTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _twinCatService.ReadVariableAsync(localParam.PlcVariable, typeof(double));
                            if (result != null)
                            {
                                localParam.Value = Convert.ToDouble(result);
                                Interlocked.Increment(ref processed);
                            }
                            else
                            {
                                Interlocked.Increment(ref failed);
                                errors.Add($"GANTRY {localParam.Index} ({localParam.PlcVariable}): Null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);
                            errors.Add($"GANTRY {localParam.Index}: {ex.Message}");
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
                    // Gantry table count (desde Excel W2)
                    GantryTableCountPlcVariable = config.GantryTableCountPlcVariable,
                    GantryTableCountValue = config.GantryTableCountValue,
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
                    }).ToList(),
                    // Parámetros de configuración del Gantry (desde columnas O-V)
                    GantryConfigParameters = config.GantryConfigParameters.Select(p => new GantryConfigParamDto
                    {
                        Index = p.Index,
                        Name = p.Name,
                        ImageUrl = !string.IsNullOrEmpty(p.Image) ? $"{imageBaseUrl}{Path.GetFileName(p.Image)}" : null,
                        PlcVariable = p.PlcVariable,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Decimals = p.Decimals,
                        Unit = p.Unit,
                        Visibility = p.Visibility,
                        Value = p.Value,
                        IsConfigured = p.IsConfigured
                    }).ToList(),
                    // Tablas de interpolación del Gantry (desde columnas AC-BH)
                    GantryInterpolationTables = config.GantryInterpolationTables.Select(t => new GantryInterpolationTableDto
                    {
                        TableId = t.TableId,
                        TableIndex = t.TableIndex,
                        FunctionTypePlcTemplate = t.FunctionTypePlcTemplate,
                        PositionXPlcTemplate = t.PositionXPlcTemplate,
                        PositionYPlcTemplate = t.PositionYPlcTemplate,
                        SpeedYPlcTemplate = t.SpeedYPlcTemplate,
                        IsConfigured = t.IsConfigured
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
                
                // Escribir número de tablas del Gantry al PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(request.GantryTableCountPlcVariable) && request.GantryTableCountValue.HasValue)
                {
                    try
                    {
                        var success = await _twinCatService.WriteVariableAsync(
                            request.GantryTableCountPlcVariable,
                            request.GantryTableCountValue.Value,
                            typeof(int));
                        
                        if (success)
                        {
                            processed++;
                            _logger.LogDebug("🚂 Gantry table count written to PLC: {Value}", request.GantryTableCountValue.Value);
                        }
                        else
                        {
                            failed++;
                            errors.Add($"GantryTableCount ({request.GantryTableCountPlcVariable}): Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"GantryTableCount ({request.GantryTableCountPlcVariable}): {ex.Message}");
                    }
                }
                
                // Escribir parámetros de configuración del Gantry (columnas O-V)
                foreach (var param in request.GantryConfigValues)
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
                            _logger.LogDebug("🚂 GantryConfig {Var} = {Value}", param.PlcVariable, param.Value);
                        }
                        else
                        {
                            failed++;
                            errors.Add($"GantryConfig {param.PlcVariable}: Write failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"GantryConfig {param.PlcVariable}: {ex.Message}");
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
                
                // ============================================================
                // TRIGGER: Escribir TRUE a la variable de trigger (celda A5)
                // El PLC lo pondrá en FALSE al recibir la señal
                // ============================================================
                var excelPath = _excelService.GetExcelConfigPath();
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
                {
                    try
                    {
                        var triggerSuccess = await _twinCatService.WriteVariableAsync(
                            trainConfig.WriteTriggerPlcVariable, 
                            true, 
                            typeof(bool));
                        
                        if (triggerSuccess)
                        {
                            processed++;
                            _logger.LogInformation("🚂✅ Write TRIGGER set to TRUE: {Var}", trainConfig.WriteTriggerPlcVariable);
                        }
                        else
                        {
                            failed++;
                            errors.Add($"WriteTrigger ({trainConfig.WriteTriggerPlcVariable}): Write failed");
                            _logger.LogWarning("🚂⚠️ Write TRIGGER failed: {Var}", trainConfig.WriteTriggerPlcVariable);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"WriteTrigger ({trainConfig.WriteTriggerPlcVariable}): {ex.Message}");
                        _logger.LogWarning("🚂⚠️ Write TRIGGER exception: {Error}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogDebug("🚂 No WriteTriggerPlcVariable configured in Excel A5");
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
        
        // ========================================================================
        // Endpoints para Tablas de Interpolación del Gantry
        // ========================================================================
        
        /// <summary>
        /// Lee los puntos de interpolación de una tabla específica del Gantry
        /// POST /api/train-recipe/interpolation/read
        /// </summary>
        [HttpPost("interpolation/read")]
        [AllowAnonymous]
        public async Task<ActionResult<GantryInterpolationReadResponse>> ReadInterpolationTable([FromBody] GantryInterpolationReadRequest request)
        {
            try
            {
                _logger.LogInformation("🚂 Reading interpolation table {TableId} ({LineCount} lines)", request.TableId, request.LineCount);
                
                // Cargar configuración
                var excelPath = Path.Combine(_projectContext.ConfigPath, "ProjectConfig.xlsm");
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                // Buscar la tabla solicitada
                var table = config.GantryInterpolationTables.FirstOrDefault(t => t.TableId == request.TableId);
                if (table == null)
                {
                    return NotFound(new GantryInterpolationReadResponse
                    {
                        Success = false,
                        Message = $"Tabla '{request.TableId}' no encontrada",
                        TableId = request.TableId
                    });
                }
                
                if (!table.IsConfigured)
                {
                    return BadRequest(new GantryInterpolationReadResponse
                    {
                        Success = false,
                        Message = $"Tabla '{request.TableId}' no está configurada (faltan variables PLC)",
                        TableId = request.TableId
                    });
                }
                
                var lines = new List<GantryInterpolationLineDto>();
                var errors = new List<string>();
                
                // Crear todas las tareas de lectura en paralelo para mayor velocidad
                var readTasks = new List<Task<(int lineNumber, GantryInterpolationLineDto line, string? error)>>();
                
                for (int lineNumber = 1; lineNumber <= request.LineCount; lineNumber++)
                {
                    int ln = lineNumber; // Capturar para el closure
                    readTasks.Add(ReadLineFromPlcAsync(table, ln));
                }
                
                // Ejecutar todas las lecturas en paralelo
                var results = await Task.WhenAll(readTasks);
                
                foreach (var result in results.OrderBy(r => r.lineNumber))
                {
                    lines.Add(result.line);
                    if (result.error != null)
                        errors.Add(result.error);
                }
                
                _logger.LogInformation("🚂 Read {LineCount} interpolation lines from {TableId} (parallel)", lines.Count, request.TableId);
                
                return Ok(new GantryInterpolationReadResponse
                {
                    Success = errors.Count == 0,
                    Message = errors.Count == 0 
                        ? $"Leídas {lines.Count} líneas de interpolación" 
                        : $"Leídas {lines.Count} líneas con {errors.Count} errores",
                    TableId = request.TableId,
                    Lines = lines
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error reading interpolation table {TableId}", request.TableId);
                return StatusCode(500, new GantryInterpolationReadResponse
                {
                    Success = false,
                    Message = $"Error al leer tabla de interpolación: {ex.Message}",
                    TableId = request.TableId
                });
            }
        }
        
        /// <summary>
        /// Helper: Lee una línea completa de interpolación del PLC (START + END) en paralelo
        /// </summary>
        private async Task<(int lineNumber, GantryInterpolationLineDto line, string? error)> ReadLineFromPlcAsync(GantryInterpolationTable table, int lineNumber)
        {
            int startIndex = GantryInterpolationTable.GetStartPointIndex(lineNumber);
            int endIndex = GantryInterpolationTable.GetEndPointIndex(lineNumber);
            
            var line = new GantryInterpolationLineDto
            {
                LineNumber = lineNumber,
                Enabled = true,
                Start = new GantryInterpolationPointDto { PointIndex = startIndex, PointType = "start" },
                End = new GantryInterpolationPointDto { PointIndex = endIndex, PointType = "end" }
            };
            
            string? error = null;
            
            try
            {
                // Leer las 8 variables en paralelo (4 START + 4 END)
                var funcTypeStartTask = _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(startIndex), typeof(sbyte));
                var posXStartTask = _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(startIndex), typeof(double));
                var posYStartTask = _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(startIndex), typeof(double));
                var speedYStartTask = _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(startIndex), typeof(double));
                var funcTypeEndTask = _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(endIndex), typeof(sbyte));
                var posXEndTask = _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(endIndex), typeof(double));
                var posYEndTask = _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(endIndex), typeof(double));
                var speedYEndTask = _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(endIndex), typeof(double));
                
                await Task.WhenAll(funcTypeStartTask, posXStartTask, posYStartTask, speedYStartTask,
                                   funcTypeEndTask, posXEndTask, posYEndTask, speedYEndTask);
                
                line.Start.FunctionType = Convert.ToInt32(funcTypeStartTask.Result ?? 0);
                line.Start.PositionX = Convert.ToDouble(posXStartTask.Result ?? 0);
                line.Start.PositionY = Convert.ToDouble(posYStartTask.Result ?? 0);
                line.Start.SpeedY = Convert.ToDouble(speedYStartTask.Result ?? 0);
                
                line.End.FunctionType = Convert.ToInt32(funcTypeEndTask.Result ?? 0);
                line.End.PositionX = Convert.ToDouble(posXEndTask.Result ?? 0);
                line.End.PositionY = Convert.ToDouble(posYEndTask.Result ?? 0);
                line.End.SpeedY = Convert.ToDouble(speedYEndTask.Result ?? 0);
            }
            catch (Exception ex)
            {
                error = $"Error leyendo línea {lineNumber}: {ex.Message}";
            }
            
            return (lineNumber, line, error);
        }
        
        /// <summary>
        /// Escribe los puntos de interpolación a una tabla específica del Gantry
        /// POST /api/train-recipe/interpolation/write
        /// </summary>
        [HttpPost("interpolation/write")]
        [AllowAnonymous]
        public async Task<ActionResult<GantryInterpolationWriteResponse>> WriteInterpolationTable([FromBody] GantryInterpolationWriteRequest request)
        {
            try
            {
                _logger.LogInformation("🚂 Writing interpolation table {TableId} ({LineCount} lines)", request.TableId, request.Lines.Count);
                
                // Cargar configuración
                var excelPath = Path.Combine(_projectContext.ConfigPath, "ProjectConfig.xlsm");
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                // Buscar la tabla solicitada
                var table = config.GantryInterpolationTables.FirstOrDefault(t => t.TableId == request.TableId);
                if (table == null)
                {
                    return NotFound(new GantryInterpolationWriteResponse
                    {
                        Success = false,
                        Message = $"Tabla '{request.TableId}' no encontrada",
                        TableId = request.TableId
                    });
                }
                
                if (!table.IsConfigured)
                {
                    return BadRequest(new GantryInterpolationWriteResponse
                    {
                        Success = false,
                        Message = $"Tabla '{request.TableId}' no está configurada (faltan variables PLC)",
                        TableId = request.TableId
                    });
                }
                
                // Crear todas las tareas de escritura en paralelo
                var writeTasks = new List<Task<(int lineNumber, int pointsWritten, int pointsFailed, string? error)>>();
                
                foreach (var line in request.Lines)
                {
                    if (!line.Enabled)
                        continue;
                    
                    writeTasks.Add(WriteLineToPlcAsync(table, line));
                }
                
                // Ejecutar todas las escrituras en paralelo
                var results = await Task.WhenAll(writeTasks);
                
                int totalPointsWritten = results.Sum(r => r.pointsWritten);
                int totalPointsFailed = results.Sum(r => r.pointsFailed);
                var errors = results.Where(r => r.error != null).Select(r => r.error!).ToList();
                
                _logger.LogInformation("🚂 Written {PointsWritten} interpolation points to {TableId} (parallel)", totalPointsWritten, request.TableId);
                
                return Ok(new GantryInterpolationWriteResponse
                {
                    Success = totalPointsFailed == 0,
                    Message = totalPointsFailed == 0 
                        ? $"Escritos {totalPointsWritten} puntos de interpolación" 
                        : $"Escritos {totalPointsWritten} puntos, {totalPointsFailed} fallidos",
                    TableId = request.TableId,
                    PointsWritten = totalPointsWritten,
                    PointsFailed = totalPointsFailed,
                    Errors = errors.Count > 0 ? errors : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error writing interpolation table {TableId}", request.TableId);
                return StatusCode(500, new GantryInterpolationWriteResponse
                {
                    Success = false,
                    Message = $"Error al escribir tabla de interpolación: {ex.Message}",
                    TableId = request.TableId
                });
            }
        }
        
        /// <summary>
        /// Helper: Escribe una línea completa de interpolación al PLC (START + END) en paralelo
        /// </summary>
        private async Task<(int lineNumber, int pointsWritten, int pointsFailed, string? error)> WriteLineToPlcAsync(GantryInterpolationTable table, GantryInterpolationLineDto line)
        {
            int startIndex = GantryInterpolationTable.GetStartPointIndex(line.LineNumber);
            int endIndex = GantryInterpolationTable.GetEndPointIndex(line.LineNumber);
            
            int pointsWritten = 0;
            int pointsFailed = 0;
            string? error = null;
            
            try
            {
                // Escribir las 8 variables en paralelo (4 START + 4 END)
                var writeStartTasks = new[]
                {
                    _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(startIndex), line.Start.FunctionType, typeof(sbyte)),
                    _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(startIndex), line.Start.PositionX, typeof(double)),
                    _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(startIndex), line.Start.PositionY, typeof(double)),
                    _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(startIndex), line.Start.SpeedY, typeof(double))
                };
                
                var writeEndTasks = new[]
                {
                    _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(endIndex), line.End.FunctionType, typeof(sbyte)),
                    _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(endIndex), line.End.PositionX, typeof(double)),
                    _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(endIndex), line.End.PositionY, typeof(double)),
                    _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(endIndex), line.End.SpeedY, typeof(double))
                };
                
                await Task.WhenAll(writeStartTasks.Concat(writeEndTasks));
                pointsWritten = 2; // START + END
            }
            catch (Exception ex)
            {
                error = $"Error escribiendo línea {line.LineNumber}: {ex.Message}";
                pointsFailed = 2;
            }
            
            return (line.LineNumber, pointsWritten, pointsFailed, error);
        }
        
        /// <summary>
        /// Lee el número de líneas habilitadas de cada tabla de interpolación del PLC
        /// GET /api/train-recipe/interpolation/line-counts
        /// Retorna: { TAB1_FW_UP: 5, TAB1_FW_DOWN: 3, ... }
        /// </summary>
        [HttpGet("interpolation/line-counts")]
        [AllowAnonymous]
        public async Task<ActionResult<Dictionary<string, int>>> GetInterpolationLineCounts()
        {
            try
            {
                _logger.LogInformation("🚂 Reading interpolation table line counts from PLC");
                
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                var lineCounts = new Dictionary<string, int>();
                
                foreach (var table in config.GantryInterpolationTables)
                {
                    if (string.IsNullOrEmpty(table.LineCountPlcVariable))
                    {
                        // Si no hay variable configurada, usar 1 por defecto
                        lineCounts[table.TableId] = 1;
                        continue;
                    }
                    
                    try
                    {
                        var result = await _twinCatService.ReadVariableAsync(table.LineCountPlcVariable, typeof(int));
                        var count = Convert.ToInt32(result ?? 1);
                        // Mínimo 1 línea siempre
                        lineCounts[table.TableId] = Math.Max(1, count);
                        _logger.LogDebug("🚂 Table {TableId} line count: {Count}", table.TableId, lineCounts[table.TableId]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("🚂 Failed to read line count for {TableId}: {Error}, using default 1", 
                            table.TableId, ex.Message);
                        lineCounts[table.TableId] = 1;
                    }
                }
                
                return Ok(lineCounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error reading interpolation line counts");
                return StatusCode(500, new { error = "Error reading line counts", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Escribe el número de líneas habilitadas de una tabla de interpolación al PLC
        /// POST /api/train-recipe/interpolation/line-count
        /// Body: { tableId: "TAB1_FW_UP", lineCount: 5 }
        /// </summary>
        [HttpPost("interpolation/line-count")]
        [AllowAnonymous]
        public async Task<ActionResult> SetInterpolationLineCount([FromBody] SetLineCountRequest request)
        {
            try
            {
                _logger.LogInformation("🚂 Setting line count for {TableId}: {LineCount}", request.TableId, request.LineCount);
                
                if (request.LineCount < 1)
                {
                    return BadRequest(new { error = "El número de líneas debe ser al menos 1" });
                }
                
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
                
                var table = config.GantryInterpolationTables.FirstOrDefault(t => t.TableId == request.TableId);
                if (table == null)
                {
                    return NotFound(new { error = $"Tabla '{request.TableId}' no encontrada" });
                }
                
                if (string.IsNullOrEmpty(table.LineCountPlcVariable))
                {
                    return BadRequest(new { error = $"Tabla '{request.TableId}' no tiene variable PLC de line count configurada" });
                }
                
                await _twinCatService.WriteVariableAsync(table.LineCountPlcVariable, request.LineCount, typeof(int));
                
                return Ok(new { success = true, message = $"Line count de {request.TableId} establecido a {request.LineCount}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚂 Error setting line count for {TableId}", request.TableId);
                return StatusCode(500, new { error = "Error setting line count", details = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// Request para establecer el número de líneas de una tabla
    /// </summary>
    public class SetLineCountRequest
    {
        public string TableId { get; set; } = string.Empty;
        public int LineCount { get; set; }
    }
}
