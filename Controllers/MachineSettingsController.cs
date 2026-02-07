// ============================================================================
// MachineSettingsController.cs - API para Parámetros de Configuración de Máquina
// ============================================================================
// Endpoints para:
// - GET /api/machine-settings/config: Obtener definición de parámetros desde Excel
// - GET /api/machine-settings/plc: Leer valores actuales desde PLC
// - POST /api/machine-settings/plc: Escribir valores al PLC
// - GET /api/machine-settings/db: Leer valores almacenados en base de datos (memoria)
// - POST /api/machine-settings/db: Escribir valores a base de datos (memoria)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Database;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;
using System.Globalization;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/machine-settings")]
    public class MachineSettingsController : ControllerBase
    {
        private readonly IExcelConfigService _excelConfigService;
        private readonly ITwinCATService _twinCATService;
        private readonly AquafrischDbContext _dbContext;
        private readonly IRequestProjectContext _projectContext;
        private readonly IOperationLogService _operationLog;
        private readonly ILogger<MachineSettingsController> _logger;

        public MachineSettingsController(
            IExcelConfigService excelConfigService,
            ITwinCATService twinCATService,
            AquafrischDbContext dbContext,
            IRequestProjectContext projectContext,
            IOperationLogService operationLog,
            ILogger<MachineSettingsController> logger)
        {
            _excelConfigService = excelConfigService;
            _twinCATService = twinCATService;
            _dbContext = dbContext;
            _projectContext = projectContext;
            _operationLog = operationLog;
            _logger = logger;
        }

        /// <summary>
        /// Obtener la definición de parámetros de configuración desde Excel (hoja "setting page")
        /// </summary>
        /// <returns>Configuración con parámetros bool, int y longreal</returns>
        [HttpGet("config")]
        [ProducesResponseType(typeof(MachineSettingsConfiguration), 200)]
        public async Task<ActionResult<MachineSettingsConfiguration>> GetSettingsConfiguration()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                _logger.LogInformation("⚙️ Loading machine settings configuration from: {Path}", excelPath);

                var excelConfig = await _excelConfigService.LoadSettingsPageAsync(excelPath);

                // URL base para las imágenes (usando route parameter en lugar de query string)
                var imageBaseUrl = "/api/machine-settings/image/";

                // Convertir configuración de Excel a modelo de respuesta
                var config = new MachineSettingsConfiguration
                {
                    // Títulos de secciones desde Excel
                    BoolSectionTitle = excelConfig.BoolSectionTitle,
                    IntSectionTitle = excelConfig.IntSectionTitle,
                    LongRealSectionTitle = excelConfig.LongRealSectionTitle,
                    LongReal2SectionTitle = excelConfig.LongReal2SectionTitle,

                    BoolParameters = excelConfig.BoolSettings.Select((s, i) => new BoolSettingParameter
                    {
                        Id = $"bool_{SanitizeId(s.Name)}_{i}",
                        Name = s.Name,
                        ImagePath = s.ImagePath,
                        ImageUrl = !string.IsNullOrEmpty(s.ImagePath) 
                            ? $"{imageBaseUrl}{Path.GetFileName(s.ImagePath)}" 
                            : null,
                        PlcVariable = s.PlcVariable,
                        DisplayOrder = s.RowIndex,
                        Value = false // Valor por defecto, se leerá después
                    }).ToList(),

                    IntParameters = excelConfig.IntSettings.Select((s, i) => new IntSettingParameter
                    {
                        Id = $"int_{SanitizeId(s.Name)}_{i}",
                        Name = s.Name,
                        ImagePath = s.ImagePath,
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

                    LongRealParameters = excelConfig.LongRealSettings.Select((s, i) => new LongRealSettingParameter
                    {
                        Id = $"lreal_{SanitizeId(s.Name)}_{i}",
                        Name = s.Name,
                        ImagePath = s.ImagePath,
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
                    }).ToList(),

                    LongReal2Parameters = excelConfig.LongReal2Settings.Select((s, i) => new LongRealSettingParameter
                    {
                        Id = $"lreal2_{SanitizeId(s.Name)}_{i}",
                        Name = s.Name,
                        ImagePath = s.ImagePath,
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

                _logger.LogInformation("⚙️ Settings configuration loaded: {BoolCount} bool, {IntCount} int, {LRealCount} longreal, {LReal2Count} longreal2",
                    config.BoolParameters.Count, config.IntParameters.Count, config.LongRealParameters.Count, config.LongReal2Parameters.Count);

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading machine settings configuration");
                return StatusCode(500, new { error = "Error loading settings configuration", details = ex.Message });
            }
        }

        /// <summary>
        /// Leer todos los valores de parámetros desde el PLC
        /// </summary>
        /// <returns>Valores actuales de todos los parámetros</returns>
        [HttpGet("plc")]
        [ProducesResponseType(typeof(MachineSettingsValuesResponse), 200)]
        public async Task<ActionResult<MachineSettingsValuesResponse>> ReadFromPlc()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadSettingsPageAsync(excelPath);

                var response = new MachineSettingsValuesResponse
                {
                    Source = "PLC",
                    Timestamp = DateTime.UtcNow
                };

                // Leer parámetros Bool
                for (int i = 0; i < excelConfig.BoolSettings.Count; i++)
                {
                    var setting = excelConfig.BoolSettings[i];
                    var id = $"bool_{SanitizeId(setting.Name)}_{i}";
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
                        _logger.LogWarning("⚠️ Could not read bool {Name} from PLC: {Error}", setting.Name, ex.Message);
                        response.BoolValues[id] = false; // Valor por defecto
                    }
                }

                // Leer parámetros Int
                for (int i = 0; i < excelConfig.IntSettings.Count; i++)
                {
                    var setting = excelConfig.IntSettings[i];
                    var id = $"int_{SanitizeId(setting.Name)}_{i}";
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
                        _logger.LogWarning("⚠️ Could not read int {Name} from PLC: {Error}", setting.Name, ex.Message);
                        response.IntValues[id] = 0;
                    }
                }

                // Leer parámetros LongReal
                for (int i = 0; i < excelConfig.LongRealSettings.Count; i++)
                {
                    var setting = excelConfig.LongRealSettings[i];
                    var id = $"lreal_{SanitizeId(setting.Name)}_{i}";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                        if (value != null)
                        {
                            response.LongRealValues[id] = Convert.ToDouble(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Could not read longreal {Name} from PLC: {Error}", setting.Name, ex.Message);
                        response.LongRealValues[id] = 0.0;
                    }
                }

                // Leer parámetros LongReal2 (segunda sección)
                for (int i = 0; i < excelConfig.LongReal2Settings.Count; i++)
                {
                    var setting = excelConfig.LongReal2Settings[i];
                    var id = $"lreal2_{SanitizeId(setting.Name)}_{i}";
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                        if (value != null)
                        {
                            response.LongReal2Values[id] = Convert.ToDouble(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Could not read longreal2 {Name} from PLC: {Error}", setting.Name, ex.Message);
                        response.LongReal2Values[id] = 0.0;
                    }
                }

                _logger.LogInformation("⚙️ Read {Count} values from PLC", 
                    response.BoolValues.Count + response.IntValues.Count + response.LongRealValues.Count + response.LongReal2Values.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading machine settings from PLC");
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
        public async Task<ActionResult> WriteToPlc([FromBody] MachineSettingsWriteRequest request)
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadSettingsPageAsync(excelPath);
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
                    if (setting != null)
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
                                        {"name", setting.Name}, 
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
                    if (setting != null)
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
                                        {"name", setting.Name}, 
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

                // Escribir parámetros LongReal (con detección de cambios)
                foreach (var kvp in request.LongRealValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.LongRealSettings.Count ? excelConfig.LongRealSettings[idx] : null;
                    if (setting != null)
                    {
                        try
                        {
                            // Leer valor actual del PLC
                            var currentValue = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                            var oldValueStr = currentValue != null ? Convert.ToDouble(currentValue).ToString(CultureInfo.InvariantCulture) : "?";
                            var newValueStr = kvp.Value.ToString(CultureInfo.InvariantCulture);
                            
                            var success = await _twinCATService.WriteVariableAsync(setting.PlcVariable, kvp.Value, typeof(double));
                            if (success)
                            {
                                successCount++;
                                if (oldValueStr != newValueStr)
                                {
                                    changes.Add(new Dictionary<string, string> { 
                                        {"name", setting.Name}, 
                                        {"old", oldValueStr}, 
                                        {"new", newValueStr} 
                                    });
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"LongReal '{kvp.Key}': write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"LongReal '{kvp.Key}': {ex.Message}");
                        }
                    }
                }

                // Escribir parámetros LongReal2 (segunda sección, con detección de cambios)
                foreach (var kvp in request.LongReal2Values)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.LongReal2Settings.Count ? excelConfig.LongReal2Settings[idx] : null;
                    if (setting != null)
                    {
                        try
                        {
                            // Leer valor actual del PLC
                            var currentValue = await _twinCATService.ReadVariableAsync(setting.PlcVariable, typeof(double));
                            var oldValueStr = currentValue != null ? Convert.ToDouble(currentValue).ToString(CultureInfo.InvariantCulture) : "?";
                            var newValueStr = kvp.Value.ToString(CultureInfo.InvariantCulture);
                            
                            var success = await _twinCATService.WriteVariableAsync(setting.PlcVariable, kvp.Value, typeof(double));
                            if (success)
                            {
                                successCount++;
                                if (oldValueStr != newValueStr)
                                {
                                    changes.Add(new Dictionary<string, string> { 
                                        {"name", setting.Name}, 
                                        {"old", oldValueStr}, 
                                        {"new", newValueStr} 
                                    });
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"LongReal2 '{kvp.Key}': write failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"LongReal2 '{kvp.Key}': {ex.Message}");
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

                // Operation log (L2) - Configuración con detalles
                await _operationLog.LogAsync(
                    OperationCategory.Configuration,
                    errorCount == 0 ? OperationAction.ConfigWritePlc : OperationAction.ConfigChange,
                    description,
                    user,
                    changes.Count > 0 ? new Dictionary<string, object> { { "changes", changes } } : null);

                _logger.LogInformation("⚙️ Wrote {Success} values to PLC ({Changes} changes, {Errors} errors)", successCount, changes.Count, errorCount);

                if (errorCount > 0)
                {
                    return Ok(new { success = true, successCount, errorCount, changesCount = changes.Count, errors });
                }

                return Ok(new { success = true, successCount, errorCount = 0, changesCount = changes.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing machine settings to PLC");
                return StatusCode(500, new { error = "Error writing to PLC", details = ex.Message });
            }
        }

        /// <summary>
        /// Leer todos los valores de parámetros desde la base de datos (memoria)
        /// </summary>
        /// <returns>Valores almacenados</returns>
        [HttpGet("db")]
        [ProducesResponseType(typeof(MachineSettingsValuesResponse), 200)]
        public async Task<ActionResult<MachineSettingsValuesResponse>> ReadFromDatabase()
        {
            try
            {
                var settings = await _dbContext.MachineSettings.ToListAsync();

                var response = new MachineSettingsValuesResponse
                {
                    Source = "Database",
                    Timestamp = DateTime.UtcNow
                };

                foreach (var setting in settings)
                {
                    // Usar el prefijo del ID para determinar el tipo
                    var id = setting.ParameterId;
                    
                    if (id.StartsWith("bool_"))
                    {
                        if (bool.TryParse(setting.Value, out var boolVal))
                            response.BoolValues[id] = boolVal;
                    }
                    else if (id.StartsWith("int_"))
                    {
                        if (int.TryParse(setting.Value, out var intVal))
                            response.IntValues[id] = intVal;
                    }
                    else if (id.StartsWith("lreal2_"))
                    {
                        if (double.TryParse(setting.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dblVal))
                            response.LongReal2Values[id] = dblVal;
                    }
                    else if (id.StartsWith("lreal_"))
                    {
                        if (double.TryParse(setting.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dblVal))
                            response.LongRealValues[id] = dblVal;
                    }
                    else
                    {
                        // Fallback para datos antiguos sin prefijo
                        switch (setting.DataType.ToLower())
                        {
                            case "bool":
                                if (bool.TryParse(setting.Value, out var oldBoolVal))
                                    response.BoolValues[id] = oldBoolVal;
                                break;
                            case "int":
                                if (int.TryParse(setting.Value, out var oldIntVal))
                                    response.IntValues[id] = oldIntVal;
                                break;
                            case "longreal":
                            case "longreal2":
                            case "double":
                                if (double.TryParse(setting.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oldDblVal))
                                    response.LongRealValues[id] = oldDblVal;
                                break;
                        }
                    }
                }

                _logger.LogInformation("⚙️ Read {Count} values from database", settings.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading machine settings from database");
                return StatusCode(500, new { error = "Error reading from database", details = ex.Message });
            }
        }

        /// <summary>
        /// Escribir todos los valores de parámetros a la base de datos (memoria)
        /// </summary>
        /// <param name="request">Valores a almacenar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("db")]
        [Authorize]
        [ProducesResponseType(200)]
        public async Task<ActionResult> WriteToDatabase([FromBody] MachineSettingsWriteRequest request)
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadSettingsPageAsync(excelPath);
                var user = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System";
                var now = DateTime.UtcNow;
                int count = 0;
                var changes = new List<Dictionary<string, string>>();

                // Leer valores actuales de la BD para comparar
                var currentSettings = await _dbContext.MachineSettings.ToDictionaryAsync(s => s.ParameterId, s => s.Value);

                // Guardar parámetros Bool
                foreach (var kvp in request.BoolValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.BoolSettings.Count ? excelConfig.BoolSettings[idx] : null;
                    var newValue = kvp.Value.ToString().ToLower();
                    var oldValue = currentSettings.TryGetValue(kvp.Key, out var old) ? old : null;
                    
                    if (oldValue != newValue)
                    {
                        changes.Add(new Dictionary<string, string> { 
                            {"name", setting?.Name ?? kvp.Key}, 
                            {"old", oldValue ?? "(nuevo)"}, 
                            {"new", newValue} 
                        });
                    }
                    
                    await SaveSettingToDb(kvp.Key, setting?.PlcVariable ?? "", "Bool", newValue, user, now);
                    count++;
                }

                // Guardar parámetros Int
                foreach (var kvp in request.IntValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.IntSettings.Count ? excelConfig.IntSettings[idx] : null;
                    var newValue = kvp.Value.ToString();
                    var oldValue = currentSettings.TryGetValue(kvp.Key, out var old) ? old : null;
                    
                    if (oldValue != newValue)
                    {
                        changes.Add(new Dictionary<string, string> { 
                            {"name", setting?.Name ?? kvp.Key}, 
                            {"old", oldValue ?? "(nuevo)"}, 
                            {"new", newValue} 
                        });
                    }
                    
                    await SaveSettingToDb(kvp.Key, setting?.PlcVariable ?? "", "Int", newValue, user, now);
                    count++;
                }

                // Guardar parámetros LongReal
                foreach (var kvp in request.LongRealValues)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.LongRealSettings.Count ? excelConfig.LongRealSettings[idx] : null;
                    var newValue = kvp.Value.ToString(CultureInfo.InvariantCulture);
                    var oldValue = currentSettings.TryGetValue(kvp.Key, out var old) ? old : null;
                    
                    if (oldValue != newValue)
                    {
                        changes.Add(new Dictionary<string, string> { 
                            {"name", setting?.Name ?? kvp.Key}, 
                            {"old", oldValue ?? "(nuevo)"}, 
                            {"new", newValue} 
                        });
                    }
                    
                    await SaveSettingToDb(kvp.Key, setting?.PlcVariable ?? "", "LongReal", newValue, user, now);
                    count++;
                }

                // Guardar parámetros LongReal2 (segunda sección)
                foreach (var kvp in request.LongReal2Values)
                {
                    var idx = ExtractIndexFromId(kvp.Key);
                    var setting = idx >= 0 && idx < excelConfig.LongReal2Settings.Count ? excelConfig.LongReal2Settings[idx] : null;
                    var newValue = kvp.Value.ToString(CultureInfo.InvariantCulture);
                    var oldValue = currentSettings.TryGetValue(kvp.Key, out var old) ? old : null;
                    
                    if (oldValue != newValue)
                    {
                        changes.Add(new Dictionary<string, string> { 
                            {"name", setting?.Name ?? kvp.Key}, 
                            {"old", oldValue ?? "(nuevo)"}, 
                            {"new", newValue} 
                        });
                    }
                    
                    await SaveSettingToDb(kvp.Key, setting?.PlcVariable ?? "", "LongReal2", newValue, user, now);
                    count++;
                }

                await _dbContext.SaveChangesAsync();

                // Crear descripción con los primeros cambios
                string description;
                if (changes.Count == 0)
                {
                    description = $"Sin cambios ({count} params revisados)";
                }
                else if (changes.Count <= 3)
                {
                    var changeList = string.Join(", ", changes.Select(c => $"{c["name"]} ({c["old"]}→{c["new"]})"));
                    description = $"{changes.Count} cambios → DB: {changeList}";
                }
                else
                {
                    var first3 = string.Join(", ", changes.Take(3).Select(c => c["name"]));
                    description = $"{changes.Count} cambios → DB: {first3}...";
                }

                // Operation log (L2) - Configuración con detalles
                await _operationLog.LogAsync(
                    OperationCategory.Configuration,
                    OperationAction.ConfigChange,
                    description,
                    user,
                    changes.Count > 0 ? new Dictionary<string, object> { { "changes", changes } } : null);

                _logger.LogInformation("⚙️ Saved {Count} values to database ({Changes} changes)", count, changes.Count);

                return Ok(new { success = true, count, changesCount = changes.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing machine settings to database");
                return StatusCode(500, new { error = "Error writing to database", details = ex.Message });
            }
        }

        /// <summary>
        /// Maneja preflight CORS para el endpoint de imágenes
        /// </summary>
        [HttpOptions("image")]
        [AllowAnonymous]
        [DisableCors]  // ⭐ Deshabilitar política global, usar headers manuales
        public IActionResult GetSettingImagePreflight()
        {
            // Headers CORS explícitos para preflight
            Response.Headers.Append("Access-Control-Allow-Origin", "*");
            Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
            Response.Headers.Append("Access-Control-Allow-Headers", "*");
            Response.Headers.Append("Access-Control-Max-Age", "86400"); // Cache preflight 24h
            return Ok();
        }

        /// <summary>
        /// Obtener la imagen de ayuda de un parámetro
        /// </summary>
        /// <param name="imagePath">Ruta de la imagen (ej: "Images/pump.png")</param>
        /// <returns>Archivo de imagen</returns>
        /// <remarks>
        /// Las imágenes se buscan en: Projects/{projectId}/config/Images/
        /// El path en Excel debe ser relativo: "Images/nombre.png"
        /// CORS: Headers manuales para permitir carga desde cualquier origen (Babylon.js canvas)
        /// </remarks>
        [HttpGet("image")]
        [AllowAnonymous]
        [DisableCors]  // ⭐ Deshabilitar política global, usar headers manuales '*'
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult GetSettingImage([FromQuery] string? imagePath)
        {
            try
            {
                // Validar parámetro de entrada
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    _logger.LogDebug("🖼️ Image path not specified or empty");
                    return NotFound("Image path not specified");
                }

                // Validar que el contexto del proyecto esté disponible
                var configPath = _projectContext?.ConfigPath;
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    _logger.LogWarning("⚠️ Project config path is null or empty");
                    return NotFound("Project configuration not available");
                }

                // Sanitizar el path de entrada (remover caracteres peligrosos)
                var sanitizedPath = imagePath
                    .TrimStart('/', '\\')
                    .Replace("..", "")  // Prevenir path traversal
                    .Trim();

                if (string.IsNullOrWhiteSpace(sanitizedPath))
                {
                    _logger.LogWarning("⚠️ Image path became empty after sanitization: {Original}", imagePath);
                    return BadRequest("Invalid image path");
                }

                // Construir ruta completa relativa a la carpeta config del proyecto
                // Las imágenes están en: Projects/{projectId}/config/Images/
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(Path.Combine(configPath, sanitizedPath));
                }
                catch (Exception pathEx)
                {
                    _logger.LogWarning(pathEx, "⚠️ Invalid path combination: config={Config}, image={Image}", configPath, sanitizedPath);
                    return BadRequest("Invalid image path format");
                }

                _logger.LogDebug("🖼️ Looking for setting image at: {Path}", fullPath);

                // Seguridad: verificar que la ruta está dentro de la carpeta del proyecto
                var normalizedConfigPath = Path.GetFullPath(configPath);
                if (!fullPath.StartsWith(normalizedConfigPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("⚠️ Invalid image path (outside config folder): {Path}, ConfigPath: {ConfigPath}", imagePath, normalizedConfigPath);
                    return BadRequest("Invalid image path");
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogDebug("🖼️ Image not found: {Path}", fullPath);
                    return NotFound($"Image not found: {imagePath}");
                }

                // Leer archivo y devolverlo con headers CORS explícitos
                try
                {
                    var contentType = GetContentType(fullPath);
                    var fileBytes = System.IO.File.ReadAllBytes(fullPath);
                    
                    // ⭐ CORS: Headers explícitos para permitir carga desde Babylon.js (canvas WebGL)
                    Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
                    
                    // Cache para mejorar rendimiento
                    Response.Headers.Append("Cache-Control", "public, max-age=3600"); // Cache 1 hora
                    
                    _logger.LogDebug("🖼️ Serving image with CORS: {Path} ({Size} bytes, {ContentType})", fullPath, fileBytes.Length, contentType);
                    
                    return File(fileBytes, contentType);
                }
                catch (IOException ioEx)
                {
                    _logger.LogWarning(ioEx, "⚠️ IO error reading image file: {Path}", fullPath);
                    return StatusCode(503, "Image temporarily unavailable");
                }
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "⚠️ Invalid argument for image path: {Path}", imagePath);
                return BadRequest("Invalid image path format");
            }
            catch (Exception ex)
            {
                // Capturar cualquier excepción inesperada sin crashear el backend
                _logger.LogError(ex, "❌ Unexpected error retrieving setting image: {Path}", imagePath);
                return StatusCode(500, "Error retrieving image");
            }
        }

        /// <summary>
        /// Obtener una imagen de parámetro usando route parameter (método preferido)
        /// GET /api/machine-settings/image/{imageName}
        /// </summary>
        /// <param name="imageName">Nombre del archivo de imagen (ej: "pump.png")</param>
        /// <returns>Archivo de imagen</returns>
        /// <remarks>
        /// Este endpoint usa route parameter en lugar de query string para mayor compatibilidad.
        /// Las imágenes se buscan en: Projects/{projectId}/config/Images/
        /// </remarks>
        [HttpGet("image/{imageName}")]
        [AllowAnonymous]
        public IActionResult GetSettingImageByName(string imageName)
        {
            try
            {
                _logger.LogDebug("🖼️ Machine settings image request (route param): '{ImageName}'", imageName);

                if (string.IsNullOrEmpty(imageName))
                {
                    return BadRequest("Image name is required");
                }

                // Sanitizar el nombre de la imagen (solo el nombre del archivo)
                imageName = Path.GetFileName(imageName);

                // Validar que el contexto del proyecto esté disponible
                var configPath = _projectContext?.ConfigPath;
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    _logger.LogWarning("⚠️ Project config path is null or empty");
                    return NotFound("Project configuration not available");
                }

                // Obtener la ruta de imágenes del proyecto activo
                var imagesPath = Path.Combine(configPath, "Images", imageName);

                if (!System.IO.File.Exists(imagesPath))
                {
                    _logger.LogWarning("🖼️ Machine settings image not found: {ImagePath}", imagesPath);
                    return NotFound($"Image not found: {imageName}");
                }

                // Determinar el content type según la extensión
                var contentType = GetContentType(imagesPath);

                var fileBytes = System.IO.File.ReadAllBytes(imagesPath);
                
                // Cache para mejorar rendimiento
                Response.Headers.Append("Cache-Control", "public, max-age=3600"); // Cache 1 hora
                
                _logger.LogDebug("🖼️ Serving machine settings image: {Path} ({Size} bytes)", imagesPath, fileBytes.Length);
                
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🖼️ Error loading machine settings image: {ImageName}", imageName);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #region Helper Methods

        /// <summary>
        /// Sanitiza el nombre para usar como ID (sin espacios, caracteres especiales)
        /// </summary>
        private static string SanitizeId(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            
            // Reemplazar espacios y caracteres especiales
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
        /// Guardar o actualizar un setting en la base de datos
        /// </summary>
        private async Task SaveSettingToDb(string parameterId, string plcVariable, string dataType, string value, string user, DateTime timestamp)
        {
            var existing = await _dbContext.MachineSettings
                .FirstOrDefaultAsync(s => s.ParameterId == parameterId);

            if (existing != null)
            {
                existing.PlcVariable = plcVariable;
                existing.DataType = dataType;
                existing.Value = value;
                existing.UpdatedAt = timestamp;
                existing.UpdatedBy = user;
            }
            else
            {
                _dbContext.MachineSettings.Add(new MachineSettingValue
                {
                    ParameterId = parameterId,
                    PlcVariable = plcVariable,
                    DataType = dataType,
                    Value = value,
                    UpdatedAt = timestamp,
                    UpdatedBy = user
                });
            }
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
}
