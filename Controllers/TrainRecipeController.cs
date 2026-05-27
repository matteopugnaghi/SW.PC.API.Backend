// ============================================================================
// TrainRecipeController.cs - API para Editor de Recetas de Tren
// ============================================================================
// Endpoints para configuración, lectura y escritura de parámetros de tren
// Trabaja con Excel (configuración) y PLC (valores en tiempo real)
// Similar a WashRecipeController pero simplificado (sin estaciones)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        private readonly IRequestProjectContext _projectContext;
        private readonly IOperationLogService _operationLog;
        
        public TrainRecipeController(
            ILogger<TrainRecipeController> logger,
            IExcelConfigService excelService,
            ITwinCATService twinCatService,
            IRequestProjectContext projectContext,
            IOperationLogService operationLog)
        {
            _logger = logger;
            _excelService = excelService;
            _twinCatService = twinCatService;
            _projectContext = projectContext;
            _operationLog = operationLog;
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
                var writtenParams = new List<Dictionary<string, object>>(); // Para tracking de cambios
                int processed = 0;
                int failed = 0;
                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                
                // Escribir nombre del tren al PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(request.TrainNamePlcVariable) && request.TrainNameValue != null)
                {
                    try
                    {
                        // Leer valor actual del PLC para comparar
                        var currentValueObj = await _twinCatService.ReadVariableAsync(request.TrainNamePlcVariable, typeof(string));
                        var currentName = currentValueObj?.ToString() ?? "";
                        
                        var success = await _twinCatService.WriteVariableAsync(
                            request.TrainNamePlcVariable,
                            request.TrainNameValue,
                            typeof(string));
                        
                        if (success)
                        {
                            processed++;
                            // Solo registrar si el valor cambió
                            if (currentName != request.TrainNameValue)
                            {
                                writtenParams.Add(new Dictionary<string, object> { 
                                    {"name", request.TrainNameDisplayName ?? "TrainName"}, {"old", currentName}, {"new", request.TrainNameValue}, {"type", "string"} 
                                });
                            }
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
                
                // NOTA: LineNumber NO se escribe desde el editor de tipos de tren.
                // Solo se escribe desde la página "Tipos de Trenes" (lista) cuando se selecciona un slot.
                // Ver: TrainTypesController o endpoint específico para cambio de slot.
                
                // Escribir número de tablas del Gantry al PLC (si hay variable configurada)
                if (!string.IsNullOrEmpty(request.GantryTableCountPlcVariable) && request.GantryTableCountValue.HasValue)
                {
                    try
                    {
                        // Leer valor actual del PLC para comparar
                        var currentValueObj = await _twinCatService.ReadVariableAsync(request.GantryTableCountPlcVariable, typeof(int));
                        var currentValue = currentValueObj != null ? Convert.ToInt32(currentValueObj) : 0;
                        
                        var success = await _twinCatService.WriteVariableAsync(
                            request.GantryTableCountPlcVariable,
                            request.GantryTableCountValue.Value,
                            typeof(int));
                        
                        if (success)
                        {
                            processed++;
                            // Solo registrar si el valor cambió
                            if (currentValue != request.GantryTableCountValue.Value)
                            {
                                writtenParams.Add(new Dictionary<string, object> { 
                                    {"name", request.GantryTableCountDisplayName ?? "GantryTableCount"}, {"old", currentValue}, {"new", request.GantryTableCountValue.Value}, {"type", "int"} 
                                });
                            }
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
                        // Leer valor actual del PLC para comparar
                        var currentValueObj = await _twinCatService.ReadVariableAsync(param.PlcVariable, typeof(double));
                        var currentValue = currentValueObj != null ? Convert.ToDouble(currentValueObj) : (double?)null;
                        var currentStr = currentValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
                        var newStr = param.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        
                        var success = await _twinCatService.WriteVariableAsync(
                            param.PlcVariable, 
                            param.Value, 
                            typeof(double));
                        
                        if (success)
                        {
                            processed++;
                            // Solo registrar si el valor cambió
                            if (currentStr != newStr)
                            {
                                writtenParams.Add(new Dictionary<string, object> { 
                                    {"name", param.Name ?? param.PlcVariable}, {"old", currentStr}, {"new", newStr}, {"type", "lreal"} 
                                });
                            }
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
                        // Leer valor actual del PLC para comparar
                        var currentValueObj = await _twinCatService.ReadVariableAsync(param.PlcVariable, typeof(bool));
                        var currentBool = currentValueObj != null && Convert.ToBoolean(currentValueObj);
                        
                        var success = await _twinCatService.WriteVariableAsync(
                            param.PlcVariable, 
                            param.Value, 
                            typeof(bool));
                        
                        if (success)
                        {
                            processed++;
                            // Solo registrar si el valor cambió
                            if (currentBool != param.Value)
                            {
                                writtenParams.Add(new Dictionary<string, object> { 
                                    {"name", param.Name ?? param.PlcVariable}, {"old", currentBool}, {"new", param.Value}, {"type", "bool"} 
                                });
                            }
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
                        // Leer valor actual del PLC para comparar
                        var currentValueObj = await _twinCatService.ReadVariableAsync(param.PlcVariable, typeof(double));
                        var currentValue = currentValueObj != null ? Convert.ToDouble(currentValueObj) : (double?)null;
                        var currentStr = currentValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
                        var newStr = param.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        
                        var success = await _twinCatService.WriteVariableAsync(
                            param.PlcVariable, 
                            param.Value, 
                            typeof(double));
                        
                        if (success)
                        {
                            processed++;
                            // Solo registrar si el valor cambió
                            if (currentStr != newStr)
                            {
                                writtenParams.Add(new Dictionary<string, object> { 
                                    {"name", param.Name ?? param.PlcVariable}, {"old", currentStr}, {"new", newStr}, {"type", "lreal"} 
                                });
                            }
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
                
                // 📋 Operation Log (L2) - Registrar escritura al PLC con detalles
                if (writtenParams.Count > 0)
                {
                    var details = new Dictionary<string, object>
                    {
                        { "TrainName", request.TrainNameValue ?? "?" },
                        { "Changes", writtenParams }
                    };
                    await _operationLog.LogAsync(
                        OperationCategory.Recipe,
                        OperationAction.TrainTypeWritePlcFromEditor,
                        $"{writtenParams.Count} parámetros escritos al PLC",
                        username,
                        details);
                }
                
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
                
                // Obtener el slot de receta (default 1 para compatibilidad)
                int recipeSlot = request.SlotNumber > 0 ? request.SlotNumber : 1;
                _logger.LogInformation("🚂 Using recipe slot {Slot} for interpolation read", recipeSlot);
                
                // Crear todas las tareas de lectura en paralelo para mayor velocidad
                var readTasks = new List<Task<(int lineNumber, GantryInterpolationLineDto line, string? error)>>();
                
                for (int lineNumber = 1; lineNumber <= request.LineCount; lineNumber++)
                {
                    int ln = lineNumber; // Capturar para el closure
                    readTasks.Add(ReadLineFromPlcAsync(table, ln, recipeSlot));
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
        private async Task<(int lineNumber, GantryInterpolationLineDto line, string? error)> ReadLineFromPlcAsync(GantryInterpolationTable table, int lineNumber, int recipeSlot = 1)
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
                // Usar recipeSlot para direccionar a la receta correcta
                var funcTypeStartTask = _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(startIndex, recipeSlot), typeof(sbyte));
                var posXStartTask = _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(startIndex, recipeSlot), typeof(double));
                var posYStartTask = _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(startIndex, recipeSlot), typeof(double));
                var speedYStartTask = _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(startIndex, recipeSlot), typeof(double));
                var funcTypeEndTask = _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(endIndex, recipeSlot), typeof(sbyte));
                var posXEndTask = _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(endIndex, recipeSlot), typeof(double));
                var posYEndTask = _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(endIndex, recipeSlot), typeof(double));
                var speedYEndTask = _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(endIndex, recipeSlot), typeof(double));
                
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
        /// MODELO ENCADENADO: Cada línea comparte puntos con la siguiente
        /// Line 1: índices 1,2 | Line 2: índices 2,3 | Line 3: índices 3,4
        /// Los puntos compartidos solo se escriben UNA vez
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
                
                // Obtener el slot de receta (default 1 para compatibilidad)
                int recipeSlot = request.SlotNumber > 0 ? request.SlotNumber : 1;
                _logger.LogInformation("🚂 Using recipe slot {Slot} for interpolation write", recipeSlot);
                
                // =====================================================
                // PASO 1: Leer TODOS los puntos actuales del PLC (snapshot)
                // Esto evita race conditions al comparar
                // =====================================================
                var enabledLines = request.Lines.Where(l => l.Enabled).OrderBy(l => l.LineNumber).ToList();
                
                // Determinar todos los índices de puntos únicos
                var allPointIndices = new HashSet<int>();
                foreach (var line in enabledLines)
                {
                    allPointIndices.Add(GantryInterpolationTable.GetStartPointIndex(line.LineNumber));
                    allPointIndices.Add(GantryInterpolationTable.GetEndPointIndex(line.LineNumber));
                }
                
                // Leer todos los puntos en paralelo
                var snapshotTasks = new Dictionary<int, Task<(int funcType, double posX, double posY, double speedY)>>();
                foreach (var pointIndex in allPointIndices)
                {
                    snapshotTasks[pointIndex] = ReadPointFromPlcAsync(table, pointIndex, recipeSlot);
                }
                await Task.WhenAll(snapshotTasks.Values);
                
                var snapshot = snapshotTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
                _logger.LogInformation("🚂 Snapshot read: {PointCount} unique points", snapshot.Count);
                
                // =====================================================
                // PASO 2: Comparar y escribir cada línea usando el snapshot
                // =====================================================
                var allChanges = new List<string>();
                int totalPointsWritten = 0;
                int totalPointsFailed = 0;
                var errors = new List<string>();
                
                foreach (var line in enabledLines)
                {
                    var result = await WriteLineToPlcWithSnapshotAsync(table, line, recipeSlot, snapshot);
                    totalPointsWritten += result.pointsWritten;
                    totalPointsFailed += result.pointsFailed;
                    if (result.error != null) errors.Add(result.error);
                    allChanges.AddRange(result.changes);
                }
                
                // Escribir min_height y max_height si están configurados
                _logger.LogInformation("🚂 Checking min/max height - MinHeightVar: '{MinVar}', MaxHeightVar: '{MaxVar}'", 
                    table.MinHeightPlcVariable ?? "(null)", table.MaxHeightPlcVariable ?? "(null)");
                
                _logger.LogInformation("🚂 Enabled lines count: {Count}", enabledLines.Count);
                
                if (enabledLines.Count > 0)
                {
                    // min_height = Position_X del punto START de la primera línea
                    var firstLine = enabledLines.First();
                    var minHeight = firstLine.Start.PositionX;
                    
                    // max_height = Position_X del punto END de la última línea
                    var lastLine = enabledLines.Last();
                    var maxHeight = lastLine.End.PositionX;
                    
                    _logger.LogInformation("🚂 Calculated min_height={MinH} (Line {FirstLine}), max_height={MaxH} (Line {LastLine})", 
                        minHeight, firstLine.LineNumber, maxHeight, lastLine.LineNumber);
                    
                    // Escribir min_height si la variable está configurada
                    var minHeightVar = table.GetMinHeightPlcVariable(recipeSlot);
                    if (!string.IsNullOrEmpty(minHeightVar))
                    {
                        try
                        {
                            await _twinCatService.WriteVariableAsync(minHeightVar, minHeight, typeof(double));
                            _logger.LogInformation("✅ Written min_height to {Var}: {Value}", minHeightVar, minHeight);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error writing min_height to {Var}", minHeightVar);
                            errors.Add($"Error escribiendo min_height: {ex.Message}");
                            totalPointsFailed++;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ min_height variable not configured in Excel for table {TableId}", request.TableId);
                    }
                    
                    // Escribir max_height si la variable está configurada
                    var maxHeightVar = table.GetMaxHeightPlcVariable(recipeSlot);
                    if (!string.IsNullOrEmpty(maxHeightVar))
                    {
                        try
                        {
                            await _twinCatService.WriteVariableAsync(maxHeightVar, maxHeight, typeof(double));
                            _logger.LogInformation("✅ Written max_height to {Var}: {Value}", maxHeightVar, maxHeight);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error writing max_height to {Var}", maxHeightVar);
                            errors.Add($"Error escribiendo max_height: {ex.Message}");
                            totalPointsFailed++;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ max_height variable not configured in Excel for table {TableId}", request.TableId);
                    }
                }
                
                _logger.LogInformation("🚂 Written {PointsWritten} interpolation points to {TableId} (parallel), {ChangeCount} changes detected", 
                    totalPointsWritten, request.TableId, allChanges.Count);
                
                // 📋 Operation Log (L2) - Registrar escritura de tabla de interpolación SOLO si hubo cambios reales
                if (allChanges.Count > 0)
                {
                    var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                    var details = new Dictionary<string, object>
                    {
                        { "TableId", request.TableId },
                        { "SlotNumber", request.SlotNumber },
                        { "PointsWritten", totalPointsWritten },
                        { "Changes", allChanges }
                    };
                    // Formatear mensaje con todos los cambios
                    var changesPreview = string.Join(", ", allChanges);
                    await _operationLog.LogAsync(
                        OperationCategory.Recipe,
                        OperationAction.TrainTypeInterpolationWrite,
                        $"{request.TableId}: {changesPreview}",
                        username,
                        details);
                }
                
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
        /// Helper: Lee un punto de interpolación del PLC
        /// </summary>
        private async Task<(int funcType, double posX, double posY, double speedY)> ReadPointFromPlcAsync(GantryInterpolationTable table, int pointIndex, int recipeSlot)
        {
            try
            {
                var tasks = new[]
                {
                    _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(pointIndex, recipeSlot), typeof(sbyte)),
                    _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(pointIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(pointIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(pointIndex, recipeSlot), typeof(double))
                };
                var results = await Task.WhenAll(tasks);
                return (
                    Convert.ToInt32(results[0] ?? 0),
                    Convert.ToDouble(results[1] ?? 0.0),
                    Convert.ToDouble(results[2] ?? 0.0),
                    Convert.ToDouble(results[3] ?? 0.0)
                );
            }
            catch
            {
                return (0, 0.0, 0.0, 0.0);
            }
        }
        
        /// <summary>
        /// Helper: Escribe una línea de interpolación comparando contra snapshot previo
        /// Solo registra cambios reales (comparando con el snapshot, no con el PLC actual)
        /// </summary>
        private async Task<(int lineNumber, int pointsWritten, int pointsFailed, string? error, List<string> changes)> WriteLineToPlcWithSnapshotAsync(
            GantryInterpolationTable table, 
            GantryInterpolationLineDto line, 
            int recipeSlot,
            Dictionary<int, (int funcType, double posX, double posY, double speedY)> snapshot)
        {
            int startIndex = GantryInterpolationTable.GetStartPointIndex(line.LineNumber);
            int endIndex = GantryInterpolationTable.GetEndPointIndex(line.LineNumber);
            
            int pointsWritten = 0;
            int pointsFailed = 0;
            string? error = null;
            var changes = new List<string>();
            
            try
            {
                // Obtener valores del snapshot (estado ORIGINAL del PLC)
                var startPoint = snapshot.GetValueOrDefault(startIndex);
                var endPoint = snapshot.GetValueOrDefault(endIndex);
                
                // Helper para nombre de FunctionType
                string GetFunctionTypeName(int ft) => ft switch
                {
                    0 => "—",
                    1 => "Syncron",
                    3 => "Polynom 3",
                    5 => "Polynom 5",
                    _ => $"Tipo {ft}"
                };
                
                const double tolerance = 0.0001;
                bool hasChanges = false;
                
                // =====================================================
                // Registrar cambio de TIPO de interpolación (FunctionType)
                // a nivel de LÍNEA (comparando START, que representa la línea)
                // =====================================================
                if (startPoint.funcType != line.Start.FunctionType)
                {
                    changes.Add($"L{line.LineNumber}: {GetFunctionTypeName(startPoint.funcType)}→{GetFunctionTypeName(line.Start.FunctionType)}");
                    hasChanges = true;
                }
                
                // Registrar cambios de posiciones/velocidades START
                if (Math.Abs(startPoint.posX - line.Start.PositionX) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start PosX: {startPoint.posX:F2}→{line.Start.PositionX:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(startPoint.posY - line.Start.PositionY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start PosY: {startPoint.posY:F2}→{line.Start.PositionY:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(startPoint.speedY - line.Start.SpeedY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start SpeedY: {startPoint.speedY:F2}→{line.Start.SpeedY:F2}");
                    hasChanges = true;
                }
                
                // Registrar cambios de posiciones/velocidades END
                if (Math.Abs(endPoint.posX - line.End.PositionX) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End PosX: {endPoint.posX:F2}→{line.End.PositionX:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(endPoint.posY - line.End.PositionY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End PosY: {endPoint.posY:F2}→{line.End.PositionY:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(endPoint.speedY - line.End.SpeedY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End SpeedY: {endPoint.speedY:F2}→{line.End.SpeedY:F2}");
                    hasChanges = true;
                }
                
                // FunctionType END (si es diferente al START)
                if (endPoint.funcType != line.End.FunctionType && line.End.FunctionType != line.Start.FunctionType)
                {
                    hasChanges = true;
                }
                
                // Solo escribir si hay cambios
                if (hasChanges)
                {
                    var writeStartTasks = new[]
                    {
                        _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(startIndex, recipeSlot), line.Start.FunctionType, typeof(sbyte)),
                        _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(startIndex, recipeSlot), line.Start.PositionX, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(startIndex, recipeSlot), line.Start.PositionY, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(startIndex, recipeSlot), line.Start.SpeedY, typeof(double))
                    };
                    
                    var writeEndTasks = new[]
                    {
                        _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(endIndex, recipeSlot), line.End.FunctionType, typeof(sbyte)),
                        _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(endIndex, recipeSlot), line.End.PositionX, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(endIndex, recipeSlot), line.End.PositionY, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(endIndex, recipeSlot), line.End.SpeedY, typeof(double))
                    };
                    
                    await Task.WhenAll(writeStartTasks.Concat(writeEndTasks));
                    pointsWritten = 2;
                }
            }
            catch (Exception ex)
            {
                error = $"Error escribiendo línea {line.LineNumber}: {ex.Message}";
                pointsFailed = 2;
            }
            
            return (line.LineNumber, pointsWritten, pointsFailed, error, changes);
        }
        
        /// <summary>
        /// Helper: Escribe una línea completa de interpolación al PLC (START + END) en paralelo
        /// Ahora también compara con valores actuales y retorna si hubo cambios
        /// </summary>
        private async Task<(int lineNumber, int pointsWritten, int pointsFailed, string? error, List<string> changes)> WriteLineToPlcAsync(GantryInterpolationTable table, GantryInterpolationLineDto line, int recipeSlot = 1)
        {
            int startIndex = GantryInterpolationTable.GetStartPointIndex(line.LineNumber);
            int endIndex = GantryInterpolationTable.GetEndPointIndex(line.LineNumber);
            
            int pointsWritten = 0;
            int pointsFailed = 0;
            string? error = null;
            var changes = new List<string>();
            
            try
            {
                // 1. Leer valores actuales del PLC para comparar
                var readTasks = new[]
                {
                    // START
                    _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(startIndex, recipeSlot), typeof(sbyte)),
                    _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(startIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(startIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(startIndex, recipeSlot), typeof(double)),
                    // END
                    _twinCatService.ReadVariableAsync(table.GetFunctionTypePlcVariable(endIndex, recipeSlot), typeof(sbyte)),
                    _twinCatService.ReadVariableAsync(table.GetPositionXPlcVariable(endIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetPositionYPlcVariable(endIndex, recipeSlot), typeof(double)),
                    _twinCatService.ReadVariableAsync(table.GetSpeedYPlcVariable(endIndex, recipeSlot), typeof(double))
                };
                
                var currentValues = await Task.WhenAll(readTasks);
                
                // 2. Extraer valores actuales
                var currStartFuncType = Convert.ToInt32(currentValues[0] ?? 0);
                var currStartPosX = Convert.ToDouble(currentValues[1] ?? 0.0);
                var currStartPosY = Convert.ToDouble(currentValues[2] ?? 0.0);
                var currStartSpeedY = Convert.ToDouble(currentValues[3] ?? 0.0);
                var currEndFuncType = Convert.ToInt32(currentValues[4] ?? 0);
                var currEndPosX = Convert.ToDouble(currentValues[5] ?? 0.0);
                var currEndPosY = Convert.ToDouble(currentValues[6] ?? 0.0);
                var currEndSpeedY = Convert.ToDouble(currentValues[7] ?? 0.0);
                
                // Helper para nombre de FunctionType
                string GetFunctionTypeName(int ft) => ft switch
                {
                    0 => "—",
                    1 => "Syncron",
                    3 => "Polynom 3",
                    5 => "Polynom 5",
                    _ => $"Tipo {ft}"
                };
                
                // 3. Detectar cambios (con tolerancia para doubles)
                const double tolerance = 0.0001;
                bool hasChanges = false;
                
                // FunctionType START (importante!)
                if (currStartFuncType != line.Start.FunctionType)
                {
                    changes.Add($"L{line.LineNumber} Start: {GetFunctionTypeName(currStartFuncType)}→{GetFunctionTypeName(line.Start.FunctionType)}");
                    hasChanges = true;
                }
                // Posiciones START
                if (Math.Abs(currStartPosX - line.Start.PositionX) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start PosX: {currStartPosX:F2}→{line.Start.PositionX:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(currStartPosY - line.Start.PositionY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start PosY: {currStartPosY:F2}→{line.Start.PositionY:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(currStartSpeedY - line.Start.SpeedY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} Start SpeedY: {currStartSpeedY:F2}→{line.Start.SpeedY:F2}");
                    hasChanges = true;
                }
                
                // FunctionType END (importante!)
                if (currEndFuncType != line.End.FunctionType)
                {
                    changes.Add($"L{line.LineNumber} End: {GetFunctionTypeName(currEndFuncType)}→{GetFunctionTypeName(line.End.FunctionType)}");
                    hasChanges = true;
                }
                // Posiciones END
                if (Math.Abs(currEndPosX - line.End.PositionX) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End PosX: {currEndPosX:F2}→{line.End.PositionX:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(currEndPosY - line.End.PositionY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End PosY: {currEndPosY:F2}→{line.End.PositionY:F2}");
                    hasChanges = true;
                }
                if (Math.Abs(currEndSpeedY - line.End.SpeedY) > tolerance)
                {
                    changes.Add($"L{line.LineNumber} End SpeedY: {currEndSpeedY:F2}→{line.End.SpeedY:F2}");
                    hasChanges = true;
                }
                
                // 4. Solo escribir si hay cambios
                if (hasChanges)
                {
                    var writeStartTasks = new[]
                    {
                        _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(startIndex, recipeSlot), line.Start.FunctionType, typeof(sbyte)),
                        _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(startIndex, recipeSlot), line.Start.PositionX, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(startIndex, recipeSlot), line.Start.PositionY, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(startIndex, recipeSlot), line.Start.SpeedY, typeof(double))
                    };
                    
                    var writeEndTasks = new[]
                    {
                        _twinCatService.WriteVariableAsync(table.GetFunctionTypePlcVariable(endIndex, recipeSlot), line.End.FunctionType, typeof(sbyte)),
                        _twinCatService.WriteVariableAsync(table.GetPositionXPlcVariable(endIndex, recipeSlot), line.End.PositionX, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetPositionYPlcVariable(endIndex, recipeSlot), line.End.PositionY, typeof(double)),
                        _twinCatService.WriteVariableAsync(table.GetSpeedYPlcVariable(endIndex, recipeSlot), line.End.SpeedY, typeof(double))
                    };
                    
                    await Task.WhenAll(writeStartTasks.Concat(writeEndTasks));
                    pointsWritten = 2; // START + END
                }
            }
            catch (Exception ex)
            {
                error = $"Error escribiendo línea {line.LineNumber}: {ex.Message}";
                pointsFailed = 2;
            }
            
            return (line.LineNumber, pointsWritten, pointsFailed, error, changes);
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
