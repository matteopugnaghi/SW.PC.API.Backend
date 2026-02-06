// ============================================================================
// TrainTypesController.cs - API para Tipos de Trenes
// ============================================================================
// Endpoints para:
// - GET /api/train-types: Listar todos los tipos de tren
// - GET /api/train-types/{id}: Obtener detalle de un tipo de tren
// - POST /api/train-types: Crear nuevo tipo de tren
// - PUT /api/train-types/{id}: Actualizar tipo de tren
// - DELETE /api/train-types/{id}: Eliminar tipo de tren
// - POST /api/train-types/{id}/write-to-plc: Escribir tipo de tren al PLC
// - GET /api/train-types/config: Obtener configuración de Excel (TrainRecipe)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using System.Security.Claims;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/train-types")]
    [Authorize]
    public class TrainTypesController : ControllerBase
    {
        private readonly AquafrischDbContext _dbContext;
        private readonly ITwinCATService _twinCATService;
        private readonly IExcelConfigService _excelService;
        private readonly IOperationLogService _operationLog;
        private readonly ILogger<TrainTypesController> _logger;

        public TrainTypesController(
            AquafrischDbContext dbContext,
            ITwinCATService twinCATService,
            IExcelConfigService excelService,
            IOperationLogService operationLog,
            ILogger<TrainTypesController> logger)
        {
            _dbContext = dbContext;
            _twinCATService = twinCATService;
            _excelService = excelService;
            _operationLog = operationLog;
            _logger = logger;
        }

        #region CRUD Operations

        /// <summary>
        /// Obtener lista de todos los tipos de tren
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<TrainTypeListDto>), 200)]
        public async Task<ActionResult<List<TrainTypeListDto>>> GetAllTrainTypes([FromQuery] bool includeInactive = false)
        {
            try
            {
                var query = _dbContext.TrainTypes.AsQueryable();

                if (!includeInactive)
                {
                    query = query.Where(t => t.IsActive);
                }

                var trainTypes = await query
                    .OrderBy(t => t.DisplayOrder)
                    .ThenBy(t => t.Name)
                    .Select(t => new TrainTypeListDto
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        Description = t.Description,
                        Icon = t.Icon,
                        Color = t.Color,
                        IsActive = t.IsActive,
                        IsDefault = t.IsDefault,
                        DisplayOrder = t.DisplayOrder,
                        ParameterCount = t.Parameters.Count
                    })
                    .ToListAsync();

                _logger.LogInformation("🚆 Retrieved {Count} train types", trainTypes.Count);
                return Ok(trainTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving train types");
                return StatusCode(500, new { error = "Error al obtener los tipos de tren" });
            }
        }

        /// <summary>
        /// Obtener detalle de un tipo de tren específico
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TrainTypeDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TrainTypeDetailDto>> GetTrainType(int id)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters.OrderBy(p => p.DisplayOrder))
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var dto = new TrainTypeDetailDto
                {
                    Id = trainType.Id,
                    Code = trainType.Code,
                    Name = trainType.Name,
                    Description = trainType.Description,
                    Icon = trainType.Icon,
                    Color = trainType.Color,
                    IsActive = trainType.IsActive,
                    IsDefault = trainType.IsDefault,
                    DisplayOrder = trainType.DisplayOrder,
                    CreatedAt = trainType.CreatedAt,
                    UpdatedAt = trainType.UpdatedAt,
                    CreatedBy = trainType.CreatedBy,
                    UpdatedBy = trainType.UpdatedBy,
                    Parameters = trainType.Parameters.Select(p => new TrainTypeParameterDto
                    {
                        Id = p.Id,
                        ParameterCode = p.ParameterCode,
                        Name = p.Name,
                        DataType = p.DataType,
                        Value = p.Value,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Unit = p.Unit,
                        PlcVariable = p.PlcVariable,
                        DisplayOrder = p.DisplayOrder,
                        GroupName = p.GroupName
                    }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving train type {Id}", id);
                return StatusCode(500, new { error = "Error al obtener el tipo de tren" });
            }
        }

        /// <summary>
        /// Crear nuevo tipo de tren
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(TrainTypeDetailDto), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<TrainTypeDetailDto>> CreateTrainType([FromBody] TrainTypeCreateDto dto)
        {
            try
            {
                // Validar que el código no exista
                var exists = await _dbContext.TrainTypes.AnyAsync(t => t.Code == dto.Code);
                if (exists)
                {
                    return BadRequest(new { error = $"Ya existe un tipo de tren con el código '{dto.Code}'" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Si es default, quitar default de los demás
                if (dto.IsDefault)
                {
                    await _dbContext.TrainTypes
                        .Where(t => t.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDefault, false));
                }

                var trainType = new TrainType
                {
                    Code = dto.Code,
                    Name = dto.Name,
                    Description = dto.Description,
                    Icon = dto.Icon,
                    Color = dto.Color,
                    IsActive = dto.IsActive,
                    IsDefault = dto.IsDefault,
                    DisplayOrder = dto.DisplayOrder,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };

                // Agregar parámetros si vienen en el DTO
                if (dto.Parameters != null)
                {
                    foreach (var paramDto in dto.Parameters)
                    {
                        trainType.Parameters.Add(new TrainTypeParameter
                        {
                            ParameterCode = paramDto.ParameterCode,
                            Name = paramDto.Name,
                            DataType = paramDto.DataType,
                            Value = paramDto.Value,
                            MinValue = paramDto.MinValue,
                            MaxValue = paramDto.MaxValue,
                            Unit = paramDto.Unit,
                            PlcVariable = paramDto.PlcVariable,
                            DisplayOrder = paramDto.DisplayOrder,
                            GroupName = paramDto.GroupName
                        });
                    }
                }

                _dbContext.TrainTypes.Add(trainType);
                await _dbContext.SaveChangesAsync();

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeCreate,
                    $"{trainType.Name} ({trainType.Code})",
                    username);

                _logger.LogInformation("✅ Created train type: {Name} ({Code})", trainType.Name, trainType.Code);

                return CreatedAtAction(nameof(GetTrainType), new { id = trainType.Id }, 
                    await GetTrainTypeDetailDto(trainType.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating train type");
                return StatusCode(500, new { error = "Error al crear el tipo de tren", details = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar tipo de tren existente
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(TrainTypeDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TrainTypeDetailDto>> UpdateTrainType(int id, [FromBody] TrainTypeUpdateDto dto)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Actualizar campos si vienen en el DTO
                if (dto.Name != null) trainType.Name = dto.Name;
                if (dto.Description != null) trainType.Description = dto.Description;
                if (dto.Icon != null) trainType.Icon = dto.Icon;
                if (dto.Color != null) trainType.Color = dto.Color;
                if (dto.IsActive.HasValue) trainType.IsActive = dto.IsActive.Value;
                if (dto.DisplayOrder.HasValue) trainType.DisplayOrder = dto.DisplayOrder.Value;

                // Si es default, quitar default de los demás
                if (dto.IsDefault.HasValue && dto.IsDefault.Value)
                {
                    await _dbContext.TrainTypes
                        .Where(t => t.Id != id && t.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDefault, false));
                    trainType.IsDefault = true;
                }
                else if (dto.IsDefault.HasValue)
                {
                    trainType.IsDefault = dto.IsDefault.Value;
                }

                // Actualizar parámetros si vienen en el DTO
                if (dto.Parameters != null)
                {
                    foreach (var paramDto in dto.Parameters)
                    {
                        var existingParam = trainType.Parameters
                            .FirstOrDefault(p => p.ParameterCode == paramDto.ParameterCode);

                        if (existingParam != null)
                        {
                            // Actualizar parámetro existente
                            if (paramDto.Name != null) existingParam.Name = paramDto.Name;
                            if (paramDto.Value != null) existingParam.Value = paramDto.Value;
                            if (paramDto.MinValue.HasValue) existingParam.MinValue = paramDto.MinValue;
                            if (paramDto.MaxValue.HasValue) existingParam.MaxValue = paramDto.MaxValue;
                            if (paramDto.Unit != null) existingParam.Unit = paramDto.Unit;
                            if (paramDto.PlcVariable != null) existingParam.PlcVariable = paramDto.PlcVariable;
                            if (paramDto.DisplayOrder.HasValue) existingParam.DisplayOrder = paramDto.DisplayOrder.Value;
                            if (paramDto.GroupName != null) existingParam.GroupName = paramDto.GroupName;
                        }
                        else
                        {
                            // Agregar nuevo parámetro
                            trainType.Parameters.Add(new TrainTypeParameter
                            {
                                ParameterCode = paramDto.ParameterCode,
                                Name = paramDto.Name ?? paramDto.ParameterCode,
                                DataType = "LREAL",
                                Value = paramDto.Value,
                                MinValue = paramDto.MinValue,
                                MaxValue = paramDto.MaxValue,
                                Unit = paramDto.Unit,
                                PlcVariable = paramDto.PlcVariable,
                                DisplayOrder = paramDto.DisplayOrder ?? 0,
                                GroupName = paramDto.GroupName
                            });
                        }
                    }
                }

                trainType.UpdatedAt = DateTime.UtcNow;
                trainType.UpdatedBy = username;

                await _dbContext.SaveChangesAsync();

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeEdit,
                    $"{trainType.Name} ({trainType.Code})",
                    username);

                _logger.LogInformation("✅ Updated train type: {Name} ({Code})", trainType.Name, trainType.Code);

                return Ok(await GetTrainTypeDetailDto(trainType.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating train type {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar el tipo de tren", details = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar tipo de tren
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> DeleteTrainType(int id)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var trainTypeName = trainType.Name;
                var trainTypeCode = trainType.Code;

                _dbContext.TrainTypes.Remove(trainType);
                await _dbContext.SaveChangesAsync();

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeDelete,
                    $"{trainTypeName} ({trainTypeCode})",
                    username);

                _logger.LogInformation("🗑️ Deleted train type: {Name} ({Code})", trainTypeName, trainTypeCode);

                return Ok(new { success = true, message = $"Tipo de tren '{trainTypeName}' eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting train type {Id}", id);
                return StatusCode(500, new { error = "Error al eliminar el tipo de tren", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtener el tipo de tren actualmente seleccionado
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(typeof(ActiveTrainTypeDto), 200)]
        public async Task<ActionResult<ActiveTrainTypeDto>> GetActiveTrainType()
        {
            try
            {
                var active = await _dbContext.ActiveTrainTypes
                    .Include(a => a.TrainType)
                    .OrderByDescending(a => a.SelectedAt)
                    .FirstOrDefaultAsync();

                if (active == null)
                {
                    return Ok(new ActiveTrainTypeDto
                    {
                        TrainTypeId = null,
                        TrainTypeCode = null,
                        TrainTypeName = null,
                        SelectedAt = null,
                        SelectedBy = null,
                        WrittenToPlc = false,
                        WrittenToPlcAt = null
                    });
                }

                return Ok(new ActiveTrainTypeDto
                {
                    TrainTypeId = active.TrainTypeId,
                    TrainTypeCode = active.TrainType?.Code,
                    TrainTypeName = active.TrainType?.Name,
                    SelectedAt = active.SelectedAt,
                    SelectedBy = active.SelectedBy,
                    WrittenToPlc = active.WrittenToPlc,
                    WrittenToPlcAt = active.WrittenToPlcAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving active train type");
                return StatusCode(500, new { error = "Error al obtener el tipo de tren activo" });
            }
        }

        /// <summary>
        /// Seleccionar un tipo de tren como activo (guardar en DB)
        /// </summary>
        [HttpPost("select")]
        [ProducesResponseType(typeof(ActiveTrainTypeDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ActiveTrainTypeDto>> SelectTrainType([FromBody] SelectTrainTypeDto dto)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Id == dto.TrainTypeId);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {dto.TrainTypeId} no encontrado" });
                }

                if (!trainType.IsActive)
                {
                    return BadRequest(new { error = "El tipo de tren seleccionado no está activo" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Eliminar selección anterior (solo mantener una)
                var existingActive = await _dbContext.ActiveTrainTypes.ToListAsync();
                _dbContext.ActiveTrainTypes.RemoveRange(existingActive);

                // Crear nueva selección
                var active = new ActiveTrainType
                {
                    TrainTypeId = dto.TrainTypeId,
                    SelectedAt = DateTime.UtcNow,
                    SelectedBy = username,
                    WrittenToPlc = false
                };

                _dbContext.ActiveTrainTypes.Add(active);
                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeLoad,
                    $"{trainType.Name} ({trainType.Code})",
                    username);

                _logger.LogInformation("✅ Selected train type: {Name} ({Code})", trainType.Name, trainType.Code);

                // Si se solicita, escribir al PLC
                if (dto.WriteToPlc)
                {
                    var writeResult = await WriteTrainTypeToPlcInternal(trainType, active, username);
                    if (!writeResult.Success)
                    {
                        _logger.LogWarning("⚠️ Train type selected but PLC write failed: {Errors}", 
                            string.Join(", ", writeResult.Errors ?? new List<string>()));
                    }
                }

                return Ok(new ActiveTrainTypeDto
                {
                    TrainTypeId = active.TrainTypeId,
                    TrainTypeCode = trainType.Code,
                    TrainTypeName = trainType.Name,
                    SelectedAt = active.SelectedAt,
                    SelectedBy = active.SelectedBy,
                    WrittenToPlc = active.WrittenToPlc,
                    WrittenToPlcAt = active.WrittenToPlcAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error selecting train type");
                return StatusCode(500, new { error = "Error al seleccionar el tipo de tren" });
            }
        }

        /// <summary>
        /// Escribir el tipo de tren activo al PLC
        /// </summary>
        [HttpPost("write-to-plc")]
        [ProducesResponseType(typeof(WriteToPlcResponseDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<WriteToPlcResponseDto>> WriteToPlc()
        {
            try
            {
                var active = await _dbContext.ActiveTrainTypes
                    .Include(a => a.TrainType)
                        .ThenInclude(t => t!.Parameters)
                    .OrderByDescending(a => a.SelectedAt)
                    .FirstOrDefaultAsync();

                if (active == null || active.TrainType == null)
                {
                    return BadRequest(new { error = "No hay un tipo de tren seleccionado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                var result = await WriteTrainTypeToPlcInternal(active.TrainType, active, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing train type to PLC");
                return StatusCode(500, new { error = "Error al escribir al PLC" });
            }
        }

        /// <summary>
        /// Guardar tipo de tren desde PLC (PLC → DB)
        /// Lee los valores actuales del PLC y los guarda en el slot indicado
        /// </summary>
        [HttpPost("save-from-plc")]
        [ProducesResponseType(typeof(TrainTypeDetailDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<TrainTypeDetailDto>> SaveFromPlc([FromBody] SaveFromPlcDto dto)
        {
            try
            {
                if (dto.SlotNumber < 1 || dto.SlotNumber > 20)
                {
                    return BadRequest(new { error = "El número de slot debe estar entre 1 y 20" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var slotCode = $"TRAIN_{dto.SlotNumber:D2}";

                // Leer valores del PLC usando el servicio de Excel
                var plcValues = await ReadTrainRecipeFromPlcAsync();

                // Buscar si ya existe el slot
                var existingTrainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .Include(t => t.GantryData)
                    .FirstOrDefaultAsync(t => t.Code == slotCode);

                if (existingTrainType != null)
                {
                    // Actualizar existente
                    existingTrainType.Name = plcValues.RecipeName ?? $"Tren {dto.SlotNumber}";
                    existingTrainType.UpdatedAt = DateTime.UtcNow;
                    existingTrainType.UpdatedBy = username;

                    // Actualizar parámetros
                    foreach (var param in plcValues.Parameters)
                    {
                        var existingParam = existingTrainType.Parameters
                            .FirstOrDefault(p => p.ParameterCode == param.Code);
                        if (existingParam != null)
                        {
                            existingParam.Value = param.Value;
                        }
                        else
                        {
                            existingTrainType.Parameters.Add(new TrainTypeParameter
                            {
                                ParameterCode = param.Code,
                                Name = param.Name,
                                DataType = param.DataType,
                                Value = param.Value,
                                MinValue = param.MinValue,
                                MaxValue = param.MaxValue,
                                Unit = param.Unit,
                                PlcVariable = param.PlcVariable,
                                DisplayOrder = param.DisplayOrder
                            });
                        }
                    }

                    // Actualizar datos de Gantry
                    await UpdateGantryDataAsync(existingTrainType, plcValues.GantryTables);
                }
                else
                {
                    // Crear nuevo
                    existingTrainType = new TrainType
                    {
                        Code = slotCode,
                        Name = plcValues.RecipeName ?? $"Tren {dto.SlotNumber}",
                        Description = $"Tipo de tren guardado desde PLC - Slot {dto.SlotNumber}",
                        Icon = "🚆",
                        Color = "#9b59b6",
                        IsActive = true,
                        IsDefault = dto.SlotNumber == 1,
                        DisplayOrder = dto.SlotNumber,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username,
                        Parameters = plcValues.Parameters.Select(p => new TrainTypeParameter
                        {
                            ParameterCode = p.Code,
                            Name = p.Name,
                            DataType = p.DataType,
                            Value = p.Value,
                            MinValue = p.MinValue,
                            MaxValue = p.MaxValue,
                            Unit = p.Unit,
                            PlcVariable = p.PlcVariable,
                            DisplayOrder = p.DisplayOrder
                        }).ToList(),
                        GantryData = plcValues.GantryTables.SelectMany(table => 
                            table.Rows.Select(row => new TrainTypeGantryData
                            {
                                TableId = table.TableId,
                                RowNumber = row.RowNumber,
                                EnableLine = row.EnableLine,
                                Syncron = row.Syncron,
                                Master1xStart = row.Master1xStart,
                                Slave1yStart = row.Slave1yStart,
                                SpeedSlaveY1Start = row.SpeedSlaveY1Start,
                                Master1xEnd = row.Master1xEnd,
                                Slave1yEnd = row.Slave1yEnd,
                                SpeedSlaveY1End = row.SpeedSlaveY1End
                            })).ToList()
                    };

                    _dbContext.TrainTypes.Add(existingTrainType);
                }

                await _dbContext.SaveChangesAsync();

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeSaveFromPlc,
                    $"Slot {dto.SlotNumber} - {existingTrainType.Name}",
                    username);

                _logger.LogInformation("🚆 Train type saved from PLC: {Name} (Slot {Slot})", 
                    existingTrainType.Name, dto.SlotNumber);

                // Retornar el tipo de tren actualizado
                var result = new TrainTypeDetailDto
                {
                    Id = existingTrainType.Id,
                    Code = existingTrainType.Code,
                    Name = existingTrainType.Name,
                    Description = existingTrainType.Description,
                    Icon = existingTrainType.Icon,
                    Color = existingTrainType.Color,
                    IsActive = existingTrainType.IsActive,
                    IsDefault = existingTrainType.IsDefault,
                    DisplayOrder = existingTrainType.DisplayOrder,
                    Parameters = existingTrainType.Parameters.Select(p => new TrainTypeParameterDto
                    {
                        Id = p.Id,
                        ParameterCode = p.ParameterCode,
                        Name = p.Name,
                        DataType = p.DataType,
                        Value = p.Value,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        Unit = p.Unit,
                        PlcVariable = p.PlcVariable,
                        DisplayOrder = p.DisplayOrder
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving train type from PLC");
                return StatusCode(500, new { error = "Error al guardar desde PLC", details = ex.Message });
            }
        }

        #endregion

        #region PLC Operations

        /// <summary>
        /// Escribir tipo de tren al PLC
        /// </summary>
        [HttpPost("{id:int}/write-to-plc")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> WriteTrainTypeToPlc(int id)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var excelPath = _excelService.GetExcelConfigPath();
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                // Escribir nombre del tren
                if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable))
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.TrainNamePlcVariable, trainType.Name, typeof(string));
                        successCount++;
                        _logger.LogDebug("✅ Nombre de tren escrito: {Var} = {Value}", trainConfig.TrainNamePlcVariable, trainType.Name);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"TrainName: {ex.Message}");
                    }
                }

                // Escribir número de línea
                if (!string.IsNullOrEmpty(trainConfig?.LineNumberPlcVariable))
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.LineNumberPlcVariable, trainType.DisplayOrder, typeof(int));
                        successCount++;
                        _logger.LogDebug("✅ Línea de tren escrita: {Var} = {Value}", trainConfig.LineNumberPlcVariable, trainType.DisplayOrder);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"LineNumber: {ex.Message}");
                    }
                }

                // Escribir parámetros
                foreach (var param in trainType.Parameters.Where(p => !string.IsNullOrEmpty(p.PlcVariable)))
                {
                    try
                    {
                        object? valueToWrite = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => bool.TryParse(param.Value, out var b) ? b : false,
                            "LREAL" or "REAL" or "DOUBLE" => double.TryParse(param.Value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var d) ? d : 0.0,
                            _ => param.Value
                        };

                        Type dataType = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => typeof(bool),
                            "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                            _ => typeof(string)
                        };

                        await _twinCATService.WriteVariableAsync(param.PlcVariable!, valueToWrite!, dataType);
                        successCount++;
                        _logger.LogDebug("✅ Parámetro escrito: {Var} = {Value}", param.PlcVariable, param.Value);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"{param.Name}: {ex.Message}");
                    }
                }

                // ========== ESCRIBIR TABLAS DE INTERPOLACIÓN GANTRY ==========
                // Cargar GantryData de la base de datos si no está incluido
                var gantryData = await _dbContext.TrainTypeGantryData
                    .Where(g => g.TrainTypeId == trainType.Id)
                    .OrderBy(g => g.TableId)
                    .ThenBy(g => g.RowNumber)
                    .ToListAsync();

                _logger.LogInformation("🚆 [TIPOS DE TREN] ESCRIBIR PLC - GantryData cargado: {Count} filas, trainConfig.GantryTables: {ConfigCount}", 
                    gantryData.Count, 
                    trainConfig?.GantryInterpolationTables?.Count ?? 0);

                if (gantryData.Any() && trainConfig?.GantryInterpolationTables != null)
                {
                    _logger.LogInformation("🚆 Escribiendo {Count} filas de GantryData al PLC", gantryData.Count);
                    
                    // Agrupar por tabla
                    var tableGroups = gantryData.GroupBy(g => g.TableId);
                    
                    foreach (var tableGroup in tableGroups)
                    {
                        var tableId = tableGroup.Key;
                        var tableConfig = trainConfig.GantryInterpolationTables
                            .FirstOrDefault(t => t.TableId == tableId && t.IsConfigured);
                        
                        if (tableConfig == null)
                        {
                            _logger.LogWarning("⚠️ No se encontró configuración para tabla {TableId}", tableId);
                            continue;
                        }
                        
                        var rows = tableGroup.OrderBy(r => r.RowNumber).ToList();
                        
                        // Escribir LineCount
                        if (!string.IsNullOrEmpty(tableConfig.LineCountPlcVariable))
                        {
                            try
                            {
                                await _twinCATService.WriteVariableAsync(
                                    tableConfig.LineCountPlcVariable, 
                                    rows.Count(r => r.EnableLine), 
                                    typeof(int));
                                successCount++;
                                _logger.LogDebug("✅ LineCount {TableId} = {Count}", tableId, rows.Count(r => r.EnableLine));
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"LineCount_{tableId}: {ex.Message}");
                            }
                        }
                        
                        // Escribir cada fila
                        foreach (var row in rows.Where(r => r.EnableLine))
                        {
                            // Índices de puntos según modelo encadenado
                            int startIndex = GantryInterpolationTable.GetStartPointIndex(row.RowNumber);
                            int endIndex = GantryInterpolationTable.GetEndPointIndex(row.RowNumber);
                            
                            // Convertir Syncron a FunctionType numérico
                            int functionType = row.Syncron switch
                            {
                                "Syncron" => 1,
                                "Polynom 3" => 3,
                                "Polynom 5" => 5,
                                _ => 1
                            };
                            
                            _logger.LogInformation("🔍 [ESCRITURA] {TableId} Row{Row}: Syncron='{Syncron}' → FunctionType={FuncType}", 
                                tableId, row.RowNumber, row.Syncron, functionType);
                            
                            // Escribir punto de inicio
                            try
                            {
                                var funcTypeVar = tableConfig.GetFunctionTypePlcVariable(startIndex);
                                var posXVar = tableConfig.GetPositionXPlcVariable(startIndex);
                                var posYVar = tableConfig.GetPositionYPlcVariable(startIndex);
                                var speedYVar = tableConfig.GetSpeedYPlcVariable(startIndex);
                                
                                if (!string.IsNullOrEmpty(funcTypeVar))
                                {
                                    // FunctionType en PLC es sbyte (8 bits), no int
                                    await _twinCATService.WriteVariableAsync(funcTypeVar, (sbyte)functionType, typeof(sbyte));
                                    successCount++;
                                }
                                if (!string.IsNullOrEmpty(posXVar))
                                {
                                    await _twinCATService.WriteVariableAsync(posXVar, row.Master1xStart, typeof(double));
                                    successCount++;
                                }
                                if (!string.IsNullOrEmpty(posYVar))
                                {
                                    await _twinCATService.WriteVariableAsync(posYVar, row.Slave1yStart, typeof(double));
                                    successCount++;
                                }
                                if (!string.IsNullOrEmpty(speedYVar))
                                {
                                    await _twinCATService.WriteVariableAsync(speedYVar, row.SpeedSlaveY1Start, typeof(double));
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Gantry_{tableId}_Row{row.RowNumber}_Start: {ex.Message}");
                            }
                            
                            // Escribir punto de fin
                            try
                            {
                                var posXVar = tableConfig.GetPositionXPlcVariable(endIndex);
                                var posYVar = tableConfig.GetPositionYPlcVariable(endIndex);
                                var speedYVar = tableConfig.GetSpeedYPlcVariable(endIndex);
                                
                                if (!string.IsNullOrEmpty(posXVar))
                                {
                                    await _twinCATService.WriteVariableAsync(posXVar, row.Master1xEnd, typeof(double));
                                    successCount++;
                                }
                                if (!string.IsNullOrEmpty(posYVar))
                                {
                                    await _twinCATService.WriteVariableAsync(posYVar, row.Slave1yEnd, typeof(double));
                                    successCount++;
                                }
                                if (!string.IsNullOrEmpty(speedYVar))
                                {
                                    await _twinCATService.WriteVariableAsync(speedYVar, row.SpeedSlaveY1End, typeof(double));
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Gantry_{tableId}_Row{row.RowNumber}_End: {ex.Message}");
                            }
                        }
                        
                        _logger.LogDebug("🚆 Tabla {TableId}: {RowCount} filas escritas al PLC", tableId, rows.Count(r => r.EnableLine));
                    }
                }
                else
                {
                    _logger.LogDebug("🚆 No hay GantryData para escribir al PLC para TrainType {Id}", trainType.Id);
                }

                // Escribir trigger de escritura (TRUE) - el PLC lo pondrá en FALSE al recibir
                if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.WriteTriggerPlcVariable, true, typeof(bool));
                        _logger.LogDebug("🚆 Write trigger set to TRUE: {Var}", trainConfig.WriteTriggerPlcVariable);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"WriteTrigger: {ex.Message}");
                        _logger.LogWarning("🚆 Failed to set write trigger: {Error}", ex.Message);
                    }
                }

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeWritePlc,
                    trainType.Name,
                    username);

                _logger.LogInformation("🚆 Train type '{Name}' written to PLC: {Success} success, {Errors} errors", 
                    trainType.Name, successCount, errorCount);

                if (errorCount > 0)
                {
                    return Ok(new { 
                        success = true, 
                        trainType = trainType.Name,
                        successCount, 
                        errorCount, 
                        errors,
                        message = $"Escritura parcial: {successCount} OK, {errorCount} errores"
                    });
                }

                return Ok(new { 
                    success = true, 
                    trainType = trainType.Name,
                    successCount, 
                    errorCount = 0,
                    message = $"Tipo de tren '{trainType.Name}' escrito al PLC correctamente ({successCount} parámetros)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing train type {Id} to PLC", id);
                return StatusCode(500, new { error = "Error al escribir al PLC", details = ex.Message });
            }
        }

        /// <summary>
        /// Escribir tipo de tren específico al PLC Alternativo (PLC2)
        /// Usa el prefijo alternativo definido en Excel celda A14
        /// </summary>
        [HttpPost("{id:int}/write-to-plc-alternate")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> WriteTrainTypeToPlcAlternate(int id)
        {
            try
            {
                var trainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var excelPath = _excelService.GetExcelConfigPath();
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

                // Verificar si hay prefijo alternativo configurado
                if (string.IsNullOrEmpty(trainConfig?.AlternatePlcPrefix))
                {
                    return BadRequest(new { 
                        error = "No hay prefijo alternativo configurado en Excel (celda A14)",
                        success = false
                    });
                }

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                // Helper para aplicar prefijo alternativo
                string ApplyAlternatePrefix(string? variable)
                {
                    if (string.IsNullOrEmpty(variable) || string.IsNullOrEmpty(trainConfig.AlternatePlcPrefix))
                        return variable ?? "";
                    
                    // Reemplazar el prefijo original con el alternativo
                    // Ejemplo: "PLC1.Variable" -> "PLC2.Variable"
                    var dotIndex = variable.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        return trainConfig.AlternatePlcPrefix + variable.Substring(dotIndex);
                    }
                    return variable;
                }

                // Escribir nombre del tren con prefijo alternativo
                if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable))
                {
                    try
                    {
                        var altVariable = ApplyAlternatePrefix(trainConfig.TrainNamePlcVariable);
                        await _twinCATService.WriteVariableAsync(altVariable, trainType.Name, typeof(string));
                        successCount++;
                        _logger.LogDebug("✅ [ALT] Nombre de tren escrito: {Var} = {Value}", altVariable, trainType.Name);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"TrainName (ALT): {ex.Message}");
                    }
                }

                // Escribir número de línea con prefijo alternativo
                if (!string.IsNullOrEmpty(trainConfig?.LineNumberPlcVariable))
                {
                    try
                    {
                        var altVariable = ApplyAlternatePrefix(trainConfig.LineNumberPlcVariable);
                        await _twinCATService.WriteVariableAsync(altVariable, trainType.DisplayOrder, typeof(int));
                        successCount++;
                        _logger.LogDebug("✅ [ALT] Línea de tren escrita: {Var} = {Value}", altVariable, trainType.DisplayOrder);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"LineNumber (ALT): {ex.Message}");
                    }
                }

                // Escribir parámetros con prefijo alternativo
                foreach (var param in trainType.Parameters.Where(p => !string.IsNullOrEmpty(p.PlcVariable)))
                {
                    try
                    {
                        var altVariable = ApplyAlternatePrefix(param.PlcVariable);
                        
                        object? valueToWrite = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => bool.TryParse(param.Value, out var b) ? b : false,
                            "LREAL" or "REAL" or "DOUBLE" => double.TryParse(param.Value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var d) ? d : 0.0,
                            _ => param.Value
                        };

                        Type dataType = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => typeof(bool),
                            "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                            _ => typeof(string)
                        };

                        await _twinCATService.WriteVariableAsync(altVariable, valueToWrite!, dataType);
                        successCount++;
                        _logger.LogDebug("✅ [ALT] Parámetro escrito: {Var} = {Value}", altVariable, param.Value);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"{param.Name} (ALT): {ex.Message}");
                    }
                }

                // Escribir trigger de escritura (TRUE) con prefijo alternativo - el PLC lo pondrá en FALSE al recibir
                if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
                {
                    try
                    {
                        var altTriggerVariable = ApplyAlternatePrefix(trainConfig.WriteTriggerPlcVariable);
                        await _twinCATService.WriteVariableAsync(altTriggerVariable, true, typeof(bool));
                        _logger.LogDebug("🚆 [ALT] Write trigger set to TRUE: {Var}", altTriggerVariable);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"WriteTrigger (ALT): {ex.Message}");
                        _logger.LogWarning("🚆 [ALT] Failed to set write trigger: {Error}", ex.Message);
                    }
                }

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeWritePlc,
                    $"{trainType.Name} (PLC ALT)",
                    username);

                _logger.LogInformation("🚆 [ALT] Train type '{Name}' written to ALTERNATE PLC: {Success} success, {Errors} errors", 
                    trainType.Name, successCount, errorCount);

                if (errorCount > 0)
                {
                    return Ok(new { 
                        success = true, 
                        trainType = trainType.Name,
                        successCount, 
                        errorCount, 
                        errors,
                        alternatePlc = true,
                        message = $"Escritura parcial a PLC Alternativo: {successCount} OK, {errorCount} errores"
                    });
                }

                return Ok(new { 
                    success = true, 
                    trainType = trainType.Name,
                    successCount, 
                    errorCount = 0,
                    alternatePlc = true,
                    message = $"Tipo de tren '{trainType.Name}' escrito al PLC Alternativo correctamente ({successCount} parámetros)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing train type {Id} to ALTERNATE PLC", id);
                return StatusCode(500, new { error = "Error al escribir al PLC Alternativo", details = ex.Message });
            }
        }

        /// <summary>
        /// Escribir receta de tren directamente al PLC (desde editor, sin guardar en BD)
        /// Similar a WashRecipe - escribe los parámetros editados directamente
        /// </summary>
        [HttpPost("write-recipe-to-plc")]
        [ProducesResponseType(200)]
        public async Task<ActionResult> WriteRecipeToPlc([FromBody] TrainRecipeWriteRequest request)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var excelPath = _excelService.GetExcelConfigPath();
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                // Escribir nombre del tren (usar EffectiveName para compatibilidad)
                var effectiveName = request.EffectiveName;
                if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable) && !string.IsNullOrEmpty(effectiveName))
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.TrainNamePlcVariable, effectiveName, typeof(string));
                        successCount++;
                        _logger.LogDebug("✅ Nombre de tren escrito: {Var} = {Value}", trainConfig.TrainNamePlcVariable, effectiveName);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"TrainName: {ex.Message}");
                    }
                }

                // Escribir número de línea/slot (usar EffectiveSlotNumber para compatibilidad)
                var effectiveSlot = request.EffectiveSlotNumber;
                if (!string.IsNullOrEmpty(trainConfig?.LineNumberPlcVariable) && effectiveSlot.HasValue)
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.LineNumberPlcVariable, effectiveSlot.Value, typeof(int));
                        successCount++;
                        _logger.LogDebug("✅ Línea de tren escrita: {Var} = {Value}", trainConfig.LineNumberPlcVariable, effectiveSlot.Value);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"LineNumber: {ex.Message}");
                    }
                }

                // Escribir parámetros booleanos
                if (request.BoolValues != null)
                {
                    foreach (var boolVal in request.BoolValues.Where(b => !string.IsNullOrEmpty(b.PlcVariable)))
                    {
                        try
                        {
                            await _twinCATService.WriteVariableAsync(boolVal.PlcVariable!, boolVal.Value, typeof(bool));
                            successCount++;
                            _logger.LogDebug("✅ Bool escrito: {Var} = {Value}", boolVal.PlcVariable, boolVal.Value);
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"{boolVal.PlcVariable}: {ex.Message}");
                        }
                    }
                }

                // Escribir parámetros decimales
                if (request.DecimalValues != null)
                {
                    foreach (var decVal in request.DecimalValues.Where(d => !string.IsNullOrEmpty(d.PlcVariable)))
                    {
                        try
                        {
                            await _twinCATService.WriteVariableAsync(decVal.PlcVariable!, decVal.Value, typeof(double));
                            successCount++;
                            _logger.LogDebug("✅ Decimal escrito: {Var} = {Value}", decVal.PlcVariable, decVal.Value);
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"{decVal.PlcVariable}: {ex.Message}");
                        }
                    }
                }

                // Escribir trigger de escritura (TRUE) - el PLC lo pondrá en FALSE al recibir
                if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
                {
                    try
                    {
                        await _twinCATService.WriteVariableAsync(trainConfig.WriteTriggerPlcVariable, true, typeof(bool));
                        _logger.LogDebug("🚆 Write trigger set to TRUE: {Var}", trainConfig.WriteTriggerPlcVariable);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"WriteTrigger: {ex.Message}");
                        _logger.LogWarning("🚆 Failed to set write trigger: {Error}", ex.Message);
                    }
                }

                // Operation log
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeWritePlc,
                    effectiveName ?? "N/A",
                    username);

                _logger.LogInformation("🚆 Train recipe '{Name}' written to PLC: {Success} success, {Errors} errors", 
                    effectiveName ?? "Sin nombre", successCount, errorCount);

                return Ok(new { 
                    success = errorCount == 0, 
                    successCount, 
                    errorCount, 
                    errors = errorCount > 0 ? errors : null,
                    message = errorCount == 0 
                        ? $"Receta de tren escrita al PLC correctamente ({successCount} parámetros)"
                        : $"Escritura parcial: {successCount} OK, {errorCount} errores"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing train recipe to PLC");
                return StatusCode(500, new { error = "Error al escribir al PLC", details = ex.Message });
            }
        }

        #endregion

        #region Excel Configuration

        /// <summary>
        /// Obtener configuración de TrainRecipe desde Excel
        /// </summary>
        [HttpGet("config")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TrainRecipeConfiguration), 200)]
        public async Task<ActionResult<TrainRecipeConfiguration>> GetTrainRecipeConfig()
        {
            try
            {
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

                if (config == null)
                {
                    return Ok(new TrainRecipeConfiguration());
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading TrainRecipe configuration");
                return StatusCode(500, new { error = "Error al cargar la configuración de TrainRecipe", details = ex.Message });
            }
        }

        /// <summary>
        /// Crear tipo de tren desde configuración Excel (seed)
        /// Si ya existe el código, actualiza el existente (upsert)
        /// Lee los valores actuales del PLC para guardarlos
        /// </summary>
        [HttpPost("seed")]
        [ProducesResponseType(typeof(TrainTypeDetailDto), 201)]
        public async Task<ActionResult<TrainTypeDetailDto>> SeedFromExcel([FromBody] TrainTypeSeedRequest request)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var excelPath = _excelService.GetExcelConfigPath();
                var config = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

                if (config == null)
                {
                    return BadRequest(new { error = "No se pudo cargar la configuración de TrainRecipe desde Excel" });
                }

                // Leer valores actuales del PLC
                var plcValues = await ReadTrainRecipeFromPlcAsync();

                // Buscar si ya existe el código (upsert)
                var existingTrainType = await _dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .FirstOrDefaultAsync(t => t.Code == request.Code);

                TrainType trainType;
                bool isUpdate = existingTrainType != null;

                if (isUpdate)
                {
                    // Actualizar existente
                    trainType = existingTrainType!;
                    trainType.Name = request.Name;
                    trainType.Description = request.Description;
                    trainType.Icon = request.Icon ?? trainType.Icon;
                    trainType.Color = request.Color ?? trainType.Color;
                    trainType.IsDefault = request.IsDefault;
                    trainType.DisplayOrder = request.DisplayOrder;
                    trainType.UpdatedAt = DateTime.UtcNow;
                    trainType.UpdatedBy = username;

                    // Eliminar parámetros existentes de la BD (no solo Clear)
                    if (trainType.Parameters.Any())
                    {
                        _dbContext.Set<TrainTypeParameter>().RemoveRange(trainType.Parameters);
                        await _dbContext.SaveChangesAsync();
                        trainType.Parameters.Clear();
                    }
                }
                else
                {
                    // Crear nuevo
                    trainType = new TrainType
                    {
                        Code = request.Code,
                        Name = request.Name,
                        Description = request.Description,
                        Icon = request.Icon ?? "🚆",
                        Color = request.Color ?? "#2196F3",
                        IsActive = true,
                        IsDefault = request.IsDefault,
                        DisplayOrder = request.DisplayOrder,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };
                }

                // Agregar parámetros desde los valores del PLC (usando la estructura del Excel)
                foreach (var plcParam in plcValues.Parameters)
                {
                    trainType.Parameters.Add(new TrainTypeParameter
                    {
                        ParameterCode = plcParam.Code,
                        Name = plcParam.Name,
                        DataType = plcParam.DataType,
                        Value = plcParam.Value,
                        MinValue = plcParam.MinValue,
                        MaxValue = plcParam.MaxValue,
                        Unit = plcParam.Unit,
                        PlcVariable = plcParam.PlcVariable,
                        DisplayOrder = plcParam.DisplayOrder,
                        GroupName = plcParam.DataType == "BOOL" ? "Bool" : "Decimal"
                    });
                }

                if (!isUpdate)
                {
                    _dbContext.TrainTypes.Add(trainType);
                }
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("✅ Train type {Action} from PLC: {Name} ({Code}) with {Count} parameters", 
                    isUpdate ? "updated" : "seeded", trainType.Name, trainType.Code, trainType.Parameters.Count);

                return CreatedAtAction(nameof(GetTrainType), new { id = trainType.Id }, 
                    await GetTrainTypeDetailDto(trainType.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error seeding train type from Excel");
                return StatusCode(500, new { error = "Error al crear tipo de tren desde Excel", details = ex.Message });
            }
        }

        #endregion

        #region Private Methods

        private async Task<TrainTypeDetailDto?> GetTrainTypeDetailDto(int id)
        {
            var trainType = await _dbContext.TrainTypes
                .Include(t => t.Parameters.OrderBy(p => p.DisplayOrder))
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trainType == null) return null;

            return new TrainTypeDetailDto
            {
                Id = trainType.Id,
                Code = trainType.Code,
                Name = trainType.Name,
                Description = trainType.Description,
                Icon = trainType.Icon,
                Color = trainType.Color,
                IsActive = trainType.IsActive,
                IsDefault = trainType.IsDefault,
                DisplayOrder = trainType.DisplayOrder,
                CreatedAt = trainType.CreatedAt,
                UpdatedAt = trainType.UpdatedAt,
                CreatedBy = trainType.CreatedBy,
                UpdatedBy = trainType.UpdatedBy,
                Parameters = trainType.Parameters.Select(p => new TrainTypeParameterDto
                {
                    Id = p.Id,
                    ParameterCode = p.ParameterCode,
                    Name = p.Name,
                    DataType = p.DataType,
                    Value = p.Value,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    Unit = p.Unit,
                    PlcVariable = p.PlcVariable,
                    DisplayOrder = p.DisplayOrder,
                    GroupName = p.GroupName
                }).ToList()
            };
        }

        /// <summary>
        /// Método interno para escribir tipo de tren al PLC
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteTrainTypeToPlcInternal(TrainType trainType, ActiveTrainType active, string username)
        {
            var excelPath = _excelService.GetExcelConfigPath();
            var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            // Escribir nombre del tren
            if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable))
            {
                try
                {
                    await _twinCATService.WriteVariableAsync(trainConfig.TrainNamePlcVariable, trainType.Name, typeof(string));
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"TrainName: {ex.Message}");
                }
            }

            // Escribir número de línea
            if (!string.IsNullOrEmpty(trainConfig?.LineNumberPlcVariable))
            {
                try
                {
                    await _twinCATService.WriteVariableAsync(trainConfig.LineNumberPlcVariable, trainType.DisplayOrder, typeof(int));
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"LineNumber: {ex.Message}");
                }
            }

            // Escribir parámetros
            foreach (var param in trainType.Parameters.Where(p => !string.IsNullOrEmpty(p.PlcVariable)))
            {
                try
                {
                    object? valueToWrite = param.DataType?.ToUpper() switch
                    {
                        "BOOL" => bool.TryParse(param.Value, out var b) ? b : false,
                        "LREAL" or "REAL" or "DOUBLE" => double.TryParse(param.Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var d) ? d : 0.0,
                        _ => param.Value
                    };

                    Type dataType = param.DataType?.ToUpper() switch
                    {
                        "BOOL" => typeof(bool),
                        "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                        _ => typeof(string)
                    };

                    await _twinCATService.WriteVariableAsync(param.PlcVariable!, valueToWrite!, dataType);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"{param.Name}: {ex.Message}");
                }
            }

            // Escribir trigger de escritura (TRUE) - el PLC lo pondrá en FALSE al recibir
            if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
            {
                try
                {
                    await _twinCATService.WriteVariableAsync(trainConfig.WriteTriggerPlcVariable, true, typeof(bool));
                    _logger.LogDebug("🚆 Write trigger set to TRUE: {Var}", trainConfig.WriteTriggerPlcVariable);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"WriteTrigger: {ex.Message}");
                    _logger.LogWarning("🚆 Failed to set write trigger: {Error}", ex.Message);
                }
            }

            // Actualizar estado de escritura
            active.WrittenToPlc = errorCount == 0;
            active.WrittenToPlcAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Log de operación
            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.TrainTypeWritePlc,
                trainType.Name,
                username);

            _logger.LogInformation("🚆 Train type '{Name}' written to PLC: {Success} success, {Errors} errors",
                trainType.Name, successCount, errorCount);

            return new WriteToPlcResponseDto
            {
                Success = errorCount == 0,
                Message = errorCount == 0
                    ? $"Tipo de tren '{trainType.Name}' escrito al PLC ({successCount} parámetros)"
                    : $"Escritura parcial: {successCount} OK, {errorCount} errores",
                ParametersWritten = successCount,
                Errors = errors.Count > 0 ? errors : null,
                WrittenAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Leer valores de receta de tren desde el PLC
        /// </summary>
        private async Task<PlcTrainRecipeData> ReadTrainRecipeFromPlcAsync()
        {
            var excelPath = _excelService.GetExcelConfigPath();
            _logger.LogInformation("🚆 [TIPOS DE TREN] Cargando configuración Excel desde: {Path}", excelPath);
            
            var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);
            
            _logger.LogInformation("🚆 [TIPOS DE TREN] trainConfig cargado: BoolParams={Bool}, DecimalParams={Dec}, GantryTables={Gantry}", 
                trainConfig?.BoolParameters?.Count ?? 0,
                trainConfig?.DecimalParameters?.Count ?? 0,
                trainConfig?.GantryInterpolationTables?.Count ?? 0);

            var result = new PlcTrainRecipeData
            {
                RecipeName = "Receta desde PLC",
                Parameters = new List<PlcTrainParameterData>()
            };

            // Leer nombre del tren
            if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable))
            {
                try
                {
                    var nameValue = await _twinCATService.ReadVariableAsync(trainConfig.TrainNamePlcVariable, typeof(string));
                    result.RecipeName = nameValue?.ToString() ?? "Receta desde PLC";
                }
                catch
                {
                    // Ignorar errores de lectura
                }
            }

            int order = 0;

            // Leer parámetros booleanos
            foreach (var param in trainConfig?.BoolParameters ?? new List<TrainRecipeParameter>())
            {
                if (string.IsNullOrEmpty(param.PlcVariable)) continue;

                var plcParam = new PlcTrainParameterData
                {
                    Code = $"BOOL_{order}",
                    Name = param.Name ?? $"Bool {order}",
                    DataType = "BOOL",
                    Value = "false",
                    PlcVariable = param.PlcVariable,
                    DisplayOrder = order
                };

                try
                {
                    var value = await _twinCATService.ReadVariableAsync(param.PlcVariable, typeof(bool));
                    plcParam.Value = value?.ToString()?.ToLower() ?? "false";
                }
                catch
                {
                    // Mantener valor por defecto
                }

                result.Parameters.Add(plcParam);
                order++;
            }

            // Leer parámetros decimales
            foreach (var param in trainConfig?.DecimalParameters ?? new List<TrainRecipeParameter>())
            {
                if (string.IsNullOrEmpty(param.PlcVariable)) continue;

                var plcParam = new PlcTrainParameterData
                {
                    Code = $"DECIMAL_{order}",
                    Name = param.Name ?? $"Decimal {order}",
                    DataType = "LREAL",
                    Value = "0",
                    MinValue = param.MinValue,
                    MaxValue = param.MaxValue,
                    Unit = param.Unit,
                    PlcVariable = param.PlcVariable,
                    DisplayOrder = order
                };

                try
                {
                    var value = await _twinCATService.ReadVariableAsync(param.PlcVariable, typeof(double));
                    plcParam.Value = value?.ToString() ?? "0";
                }
                catch
                {
                    // Mantener valor por defecto
                }

                result.Parameters.Add(plcParam);
                order++;
            }

            // Leer tablas de interpolación del Gantry
            result.GantryTables = await ReadGantryInterpolationTablesFromPlcAsync(trainConfig);

            return result;
        }

        /// <summary>
        /// Leer tablas de interpolación del Gantry desde el PLC
        /// </summary>
        private async Task<List<PlcGantryTableData>> ReadGantryInterpolationTablesFromPlcAsync(TrainRecipeConfiguration? trainConfig)
        {
            var tables = new List<PlcGantryTableData>();
            
            _logger.LogInformation("🚆 [TIPOS DE TREN] ReadGantryInterpolationTablesFromPlcAsync - trainConfig es null? {IsNull}, GantryTables: {Count}", 
                trainConfig == null,
                trainConfig?.GantryInterpolationTables?.Count ?? 0);
            
            if (trainConfig?.GantryInterpolationTables == null || trainConfig.GantryInterpolationTables.Count == 0)
            {
                _logger.LogWarning("🚆 [TIPOS DE TREN] ⚠️ No hay tablas de interpolación de Gantry configuradas en el Excel");
                return tables;
            }
            
            var configuredTables = trainConfig.GantryInterpolationTables.Where(t => t.IsConfigured).ToList();
            _logger.LogInformation("🚆 [TIPOS DE TREN] Tablas configuradas: {Count}/{Total}", 
                configuredTables.Count, trainConfig.GantryInterpolationTables.Count);

            foreach (var tableConfig in configuredTables)
            {
                var tableData = new PlcGantryTableData
                {
                    TableIndex = tableConfig.TableIndex,
                    TableId = tableConfig.TableId,
                    Rows = new List<PlcGantryRowData>()
                };

                // Leer el número de líneas habilitadas
                int lineCount = 1;
                if (!string.IsNullOrEmpty(tableConfig.LineCountPlcVariable))
                {
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(tableConfig.LineCountPlcVariable, typeof(int));
                        lineCount = Math.Max(1, Convert.ToInt32(value ?? 1));
                    }
                    catch
                    {
                        _logger.LogWarning("⚠️ No se pudo leer LineCount de tabla {TableId}", tableConfig.TableId);
                    }
                }

                // Leer las filas de interpolación (máximo 124 líneas según modelo encadenado)
                for (int lineNum = 1; lineNum <= lineCount && lineNum <= 124; lineNum++)
                {
                    var row = new PlcGantryRowData
                    {
                        RowNumber = lineNum,
                        EnableLine = lineNum <= lineCount
                    };

                    // Índices de puntos según modelo encadenado
                    int startIndex = GantryInterpolationTable.GetStartPointIndex(lineNum);
                    int endIndex = GantryInterpolationTable.GetEndPointIndex(lineNum);

                    // Leer punto de inicio
                    try
                    {
                        var funcTypeVar = tableConfig.GetFunctionTypePlcVariable(startIndex);
                        var posXVar = tableConfig.GetPositionXPlcVariable(startIndex);
                        var posYVar = tableConfig.GetPositionYPlcVariable(startIndex);
                        var speedYVar = tableConfig.GetSpeedYPlcVariable(startIndex);

                        if (!string.IsNullOrEmpty(funcTypeVar))
                        {
                            // FunctionType en PLC es sbyte (8 bits), no int
                            var value = await _twinCATService.ReadVariableAsync(funcTypeVar, typeof(sbyte));
                            var funcType = Convert.ToInt32(value ?? 1);
                            // Convertir FunctionType numérico a nombre: 1=Syncron, 3=Polynom 3, 5=Polynom 5
                            row.Syncron = funcType switch
                            {
                                1 => "Syncron",
                                3 => "Polynom 3",
                                5 => "Polynom 5",
                                _ => "Syncron"
                            };
                            _logger.LogInformation("🔍 [LECTURA] {TableId} Line{Line}: FunctionType={FuncType} → Syncron={Syncron}", 
                                tableConfig.TableId, lineNum, funcType, row.Syncron);
                        }
                        if (!string.IsNullOrEmpty(posXVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(posXVar, typeof(double));
                            row.Master1xStart = Convert.ToDouble(value ?? 0);
                        }
                        if (!string.IsNullOrEmpty(posYVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(posYVar, typeof(double));
                            row.Slave1yStart = Convert.ToDouble(value ?? 0);
                        }
                        if (!string.IsNullOrEmpty(speedYVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(speedYVar, typeof(double));
                            row.SpeedSlaveY1Start = Convert.ToDouble(value ?? 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error leyendo punto inicio línea {Line} tabla {Table}: {Error}", 
                            lineNum, tableConfig.TableId, ex.Message);
                    }

                    // Leer punto de fin
                    try
                    {
                        var posXVar = tableConfig.GetPositionXPlcVariable(endIndex);
                        var posYVar = tableConfig.GetPositionYPlcVariable(endIndex);
                        var speedYVar = tableConfig.GetSpeedYPlcVariable(endIndex);

                        if (!string.IsNullOrEmpty(posXVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(posXVar, typeof(double));
                            row.Master1xEnd = Convert.ToDouble(value ?? 0);
                        }
                        if (!string.IsNullOrEmpty(posYVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(posYVar, typeof(double));
                            row.Slave1yEnd = Convert.ToDouble(value ?? 0);
                        }
                        if (!string.IsNullOrEmpty(speedYVar))
                        {
                            var value = await _twinCATService.ReadVariableAsync(speedYVar, typeof(double));
                            row.SpeedSlaveY1End = Convert.ToDouble(value ?? 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error leyendo punto fin línea {Line} tabla {Table}: {Error}", 
                            lineNum, tableConfig.TableId, ex.Message);
                    }

                    tableData.Rows.Add(row);
                }

                tables.Add(tableData);
                _logger.LogDebug("🚆 Leída tabla {TableId}: {RowCount} filas", tableConfig.TableId, tableData.Rows.Count);
            }

            return tables;
        }

        /// <summary>
        /// Actualizar datos de Gantry de un TrainType existente
        /// </summary>
        private async Task UpdateGantryDataAsync(TrainType trainType, List<PlcGantryTableData> gantryTables)
        {
            _logger.LogInformation("🚆 [TIPOS DE TREN] UpdateGantryDataAsync - TrainType {Id}, gantryTables: {Count}", 
                trainType.Id, gantryTables?.Count ?? 0);
            
            if (gantryTables == null || gantryTables.Count == 0)
            {
                _logger.LogWarning("🚆 [TIPOS DE TREN] ⚠️ No hay gantryTables para guardar");
                return;
            }

            // Eliminar datos existentes
            var existingData = await _dbContext.TrainTypeGantryData
                .Where(g => g.TrainTypeId == trainType.Id)
                .ToListAsync();
            
            _logger.LogInformation("🚆 [TIPOS DE TREN] Eliminando {Count} filas existentes de GantryData", existingData.Count);
            _dbContext.TrainTypeGantryData.RemoveRange(existingData);

            // Agregar nuevos datos
            foreach (var table in gantryTables)
            {
                foreach (var row in table.Rows)
                {
                    trainType.GantryData.Add(new TrainTypeGantryData
                    {
                        TrainTypeId = trainType.Id,
                        TableId = table.TableId,
                        RowNumber = row.RowNumber,
                        EnableLine = row.EnableLine,
                        Syncron = row.Syncron,
                        Master1xStart = row.Master1xStart,
                        Slave1yStart = row.Slave1yStart,
                        SpeedSlaveY1Start = row.SpeedSlaveY1Start,
                        Master1xEnd = row.Master1xEnd,
                        Slave1yEnd = row.Slave1yEnd,
                        SpeedSlaveY1End = row.SpeedSlaveY1End
                    });
                }
            }

            var totalRows = gantryTables.Sum(t => t.Rows?.Count ?? 0);
            _logger.LogInformation("🚆 [TIPOS DE TREN] ✅ Guardados {TotalRows} filas en {TableCount} tablas de GantryData", 
                totalRows, gantryTables.Count);
        }

        #endregion
    }

    /// <summary>
    /// Request para crear tipo de tren desde Excel
    /// </summary>
    public class TrainTypeSeedRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsDefault { get; set; }
        public int DisplayOrder { get; set; }
    }
}
