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
        private readonly IPlcWriteVerificationService _verifier;
        private readonly ITrainTypePlcWriter _plcWriter;
        private readonly ILogger<TrainTypesController> _logger;

        public TrainTypesController(
            AquafrischDbContext dbContext,
            ITwinCATService twinCATService,
            IExcelConfigService excelService,
            IOperationLogService operationLog,
            IPlcWriteVerificationService verifier,
            ITrainTypePlcWriter plcWriter,
            ILogger<TrainTypesController> logger)
        {
            _dbContext = dbContext;
            _twinCATService = twinCATService;
            _excelService = excelService;
            _operationLog = operationLog;
            _verifier = verifier;
            _plcWriter = plcWriter;
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
        /// Eliminar tipo de tren (solo SuperAdmin / Administrator)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "SuperAdmin,Administrator")]
        [ProducesResponseType(200)]
        [ProducesResponseType(403)]
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

                // Si está marcado como activo, eliminar primero las referencias (purga total para SuperAdmin/Administrator).
                // IMPORTANTE: la FK ActiveTrainType -> TrainTypes está definida solo en SQL raw (ON DELETE RESTRICT)
                // y EF no la conoce, por lo que hay que persistir el borrado de las referencias en una transacción
                // separada para asegurar el orden correcto y evitar el RESTRICT del motor SQLite.
                var activeRefs = await _dbContext.ActiveTrainTypes.Where(a => a.TrainTypeId == id).ToListAsync();
                if (activeRefs.Count > 0)
                {
                    _dbContext.ActiveTrainTypes.RemoveRange(activeRefs);
                    await _dbContext.SaveChangesAsync();
                }

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
                    existingTrainType.GantryTableCount = plcValues.GantryTableCount;
                    existingTrainType.UpdatedAt = DateTime.UtcNow;
                    existingTrainType.UpdatedBy = username;

                    // Sincronizar parámetros por variable PLC (no por posición en el Excel):
                    // actualiza metadatos+valor, añade los nuevos y elimina huérfanos.
                    var sync = TrainTypeParameterSynchronizer.Sync(existingTrainType.Parameters, plcValues.Parameters);
                    if (sync.Removed.Count > 0)
                    {
                        _dbContext.Set<TrainTypeParameter>().RemoveRange(sync.Removed);
                        await _verifier.LogIntegrityWarningsAsync(OperationCategory.Recipe, $"Guardar slot {dto.SlotNumber} desde PLC",
                            new[] { $"Eliminados {sync.Removed.Count} parámetros huérfanos (ya no están en el Excel): {string.Join(", ", sync.Removed.Select(p => p.PlcVariable))}" },
                            username);
                    }
                    _logger.LogInformation("🚆 [TIPOS DE TREN] Slot {Slot}: parámetros sincronizados → {Updated} actualizados, {Added} nuevos, {Removed} eliminados",
                        dto.SlotNumber, sync.Updated, sync.Added, sync.Removed.Count);

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
                        GantryTableCount = plcValues.GantryTableCount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username,
                        Parameters = plcValues.Parameters
                            .Where(p => !string.IsNullOrWhiteSpace(p.PlcVariable))
                            .Select(TrainTypeParameterSynchronizer.ToEntity)
                            .ToList(),
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
                    GantryTableCount = existingTrainType.GantryTableCount,
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
                        DisplayOrder = p.DisplayOrder,
                        GroupName = p.GroupName
                    }).ToList()
                };

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // PLC desconectado o lectura incompleta: NO se guarda nada (modo estricto)
                _logger.LogError("❌ Guardar tipo de tren desde PLC abortado (slot {Slot}): {Error}", dto.SlotNumber, ex.Message);
                await _verifier.LogIntegrityWarningsAsync(OperationCategory.Recipe, $"Guardar slot {dto.SlotNumber} desde PLC",
                    new[] { $"Guardado ABORTADO: {ex.Message}" }, User.FindFirst(ClaimTypes.Name)?.Value);
                return StatusCode(503, new { error = ex.Message });
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
        /// Escribir tipo de tren al PLC (DB → PLC): cabecera, parámetros Bool/Decimal/Gantry,
        /// tablas de interpolación Gantry (incl. min/max height) y verificación por relectura.
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
                    .Include(t => t.GantryData)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(_excelService.GetExcelConfigPath());

                var result = await _plcWriter.WriteAsync(trainType, trainConfig, new TrainTypePlcWriteOptions
                {
                    IncludeGantryTables = true,
                    ContextLabel = "PLC"
                }, username);

                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeWritePlc,
                    result.Success ? trainType.Name : $"{trainType.Name} ({result.ErrorCount} errores)",
                    username,
                    result.Success ? null : new Dictionary<string, object> { ["errors"] = result.Errors.Take(50).ToList() });

                return Ok(new
                {
                    success = result.Success,
                    trainType = trainType.Name,
                    successCount = result.SuccessCount,
                    errorCount = result.ErrorCount,
                    errors = result.Errors,
                    warnings = result.Warnings,
                    verification = result.Verification?.Summary,
                    message = result.Message
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
                    .Include(t => t.GantryData)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (trainType == null)
                {
                    return NotFound(new { error = $"Tipo de tren con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(_excelService.GetExcelConfigPath());

                if (trainConfig == null || !trainConfig.AlternateWriteEnabled)
                {
                    return BadRequest(new
                    {
                        error = "Escritura al PLC2 no habilitada (Excel TrainRecipe A13 no está en ON)",
                        success = false
                    });
                }

                if (string.IsNullOrEmpty(trainConfig.AlternatePlcPrefix))
                {
                    return BadRequest(new
                    {
                        error = "No hay prefijo alternativo configurado en Excel (celda A14)",
                        success = false
                    });
                }

                var prefix = trainConfig.AlternatePlcPrefix;
                var result = await _plcWriter.WriteAsync(trainType, trainConfig, new TrainTypePlcWriteOptions
                {
                    // Tablas Gantry al PLC2 salvo que A15 = OFF
                    IncludeGantryTables = trainConfig.AlternateIncludeGantryTables,
                    VariableMapper = v => TrainTypePlcWriter.ApplyAlternatePrefix(v, prefix),
                    ContextLabel = $"PLC2 ({prefix})"
                }, username);

                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.TrainTypeWritePlc,
                    result.Success ? $"{trainType.Name} (PLC ALT)" : $"{trainType.Name} (PLC ALT, {result.ErrorCount} errores)",
                    username,
                    result.Success ? null : new Dictionary<string, object> { ["errors"] = result.Errors.Take(50).ToList() });

                return Ok(new
                {
                    success = result.Success,
                    trainType = trainType.Name,
                    successCount = result.SuccessCount,
                    errorCount = result.ErrorCount,
                    errors = result.Errors,
                    warnings = result.Warnings,
                    alternatePlc = true,
                    verification = result.Verification?.Summary,
                    message = result.Message
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
                var batch = _verifier.CreateBatch($"Receta de tren '{request.EffectiveName ?? "(sin nombre)"}' → PLC (write-recipe-to-plc)");

                // Escribir nombre del tren (usar EffectiveName para compatibilidad)
                var effectiveName = request.EffectiveName;
                if (!string.IsNullOrEmpty(trainConfig?.TrainNamePlcVariable) && !string.IsNullOrEmpty(effectiveName))
                {
                    try
                    {
                        await batch.WriteAsync(trainConfig.TrainNamePlcVariable, effectiveName, typeof(string));
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
                        await batch.WriteAsync(trainConfig.LineNumberPlcVariable, effectiveSlot.Value, typeof(int));
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
                            await batch.WriteAsync(boolVal.PlcVariable!, boolVal.Value, typeof(bool));
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
                            await batch.WriteAsync(decVal.PlcVariable!, decVal.Value, typeof(double));
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

                // Verificación por relectura ANTES del trigger (L3 + L2 si hay discrepancias)
                var verification = await _verifier.VerifyAsync(batch, OperationCategory.Recipe, username);
                if (!verification.Success)
                {
                    var verifyErrors = verification.ToErrorStrings();
                    errors.AddRange(verifyErrors);
                    errorCount += verifyErrors.Count;
                }

                // Escribir trigger de escritura (TRUE) - el PLC lo pondrá en FALSE al recibir
                if (!string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
                {
                    try
                    {
                        if (!await _twinCATService.WriteVariableAsync(trainConfig.WriteTriggerPlcVariable, true, typeof(bool)))
                            throw new PlcWriteException(trainConfig.WriteTriggerPlcVariable);
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
                    errorCount == 0 ? (effectiveName ?? "N/A") : $"{effectiveName ?? "N/A"} ({errorCount} errores)",
                    username,
                    errorCount == 0 ? null : new Dictionary<string, object> { ["errors"] = errors.Take(50).ToList() });

                _logger.Log(errorCount == 0 ? LogLevel.Information : LogLevel.Error,
                    "🚆 Train recipe '{Name}' written to PLC: {Success} success, {Errors} errors. {Verification}", 
                    effectiveName ?? "Sin nombre", successCount, errorCount, verification.Summary);

                return Ok(new { 
                    success = errorCount == 0, 
                    successCount, 
                    errorCount, 
                    errors = errorCount > 0 ? errors : null,
                    verification = verification.Summary,
                    message = errorCount == 0 
                        ? $"Receta de tren escrita y verificada en PLC ({successCount} parámetros)"
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
                foreach (var plcParam in plcValues.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.PlcVariable)))
                {
                    trainType.Parameters.Add(TrainTypeParameterSynchronizer.ToEntity(plcParam));
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
                GantryTableCount = trainType.GantryTableCount,
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
        /// Método interno para escribir el tipo de tren activo al PLC (select / write-to-plc).
        /// Delegado en ITrainTypePlcWriter: misma implementación verificada que /{id}/write-to-plc.
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteTrainTypeToPlcInternal(TrainType trainType, ActiveTrainType active, string username)
        {
            // Asegurar que las tablas Gantry están cargadas (las consultas de select/active sólo incluyen Parameters)
            if (!_dbContext.Entry(trainType).Collection(t => t.GantryData).IsLoaded)
            {
                await _dbContext.Entry(trainType).Collection(t => t.GantryData).LoadAsync();
            }

            var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(_excelService.GetExcelConfigPath());

            var result = await _plcWriter.WriteAsync(trainType, trainConfig, new TrainTypePlcWriteOptions
            {
                IncludeGantryTables = true,
                ContextLabel = "PLC (activo)"
            }, username);

            // Actualizar estado de escritura
            active.WrittenToPlc = result.Success;
            active.WrittenToPlcAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.TrainTypeWritePlc,
                result.Success ? trainType.Name : $"{trainType.Name} ({result.ErrorCount} errores)",
                username,
                result.Success ? null : new Dictionary<string, object> { ["errors"] = result.Errors.Take(50).ToList() });

            return new WriteToPlcResponseDto
            {
                Success = result.Success,
                Message = result.Message,
                ParametersWritten = result.SuccessCount,
                Errors = result.Errors.Count > 0 ? result.Errors : null,
                WrittenAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Leer la receta de tren completa desde el PLC según la definición del Excel (hoja TrainRecipe).
        /// MODO ESTRICTO: cualquier variable que no se pueda leer aborta la operación con
        /// InvalidOperationException. Antes, los errores se silenciaban y se guardaban valores por
        /// defecto (false/0) que luego se escribían al PLC al cargar el slot.
        /// </summary>
        private async Task<PlcTrainRecipeData> ReadTrainRecipeFromPlcAsync()
        {
            if (!_twinCATService.IsConnected)
            {
                throw new InvalidOperationException("No hay conexión con el PLC. No se pueden leer los valores actuales de la receta de tren.");
            }

            var excelPath = _excelService.GetExcelConfigPath();
            var trainConfig = await _excelService.LoadTrainRecipeConfigAsync(excelPath);

            if (trainConfig == null)
            {
                throw new InvalidOperationException($"No se pudo cargar la hoja TrainRecipe del Excel ({excelPath}). No se guarda nada.");
            }

            _logger.LogInformation("🚆 [TIPOS DE TREN] Leyendo receta del PLC — Excel: Bool={Bool}, Decimal={Dec}, GantryCfg={GCfg}, GantryTables={Gantry}",
                trainConfig.BoolParameters.Count, trainConfig.DecimalParameters.Count,
                trainConfig.GantryConfigParameters.Count, trainConfig.GantryInterpolationTables.Count);

            var readErrors = new List<string>();
            var result = new PlcTrainRecipeData
            {
                RecipeName = "Receta desde PLC",
                Parameters = new List<PlcTrainParameterData>()
            };

            // Nombre del tren
            if (!string.IsNullOrEmpty(trainConfig.TrainNamePlcVariable))
            {
                var nameValue = await ReadOrCollectAsync(trainConfig.TrainNamePlcVariable, typeof(string), "TrainName", readErrors);
                if (nameValue != null) result.RecipeName = nameValue.ToString() ?? "Receta desde PLC";
            }

            // Número de tablas activas del Gantry (1 = 4 tablas TAB1_*, 2 = 8 tablas TAB1_* + TAB2_*)
            if (!string.IsNullOrEmpty(trainConfig.GantryTableCountPlcVariable))
            {
                var countValue = await ReadOrCollectAsync(trainConfig.GantryTableCountPlcVariable, typeof(int), "GantryTableCount", readErrors);
                if (countValue != null)
                {
                    var parsed = Convert.ToInt32(countValue);
                    if (parsed != 1 && parsed != 2)
                    {
                        _logger.LogWarning("🚆 [TIPOS DE TREN] GantryTableCount={Value} fuera de rango (1-2) en {Var}: se guarda 1",
                            parsed, trainConfig.GantryTableCountPlcVariable);
                    }
                    result.GantryTableCount = (parsed == 1 || parsed == 2) ? parsed : 1;
                }
            }

            int order = 0;

            // Parámetros booleanos (columnas C-E)
            foreach (var param in trainConfig.BoolParameters)
            {
                if (string.IsNullOrEmpty(param.PlcVariable)) continue;

                var value = await ReadOrCollectAsync(param.PlcVariable, typeof(bool), $"BOOL '{param.Name}'", readErrors);
                result.Parameters.Add(new PlcTrainParameterData
                {
                    Code = TrainTypeParameterSynchronizer.BuildCode(TrainTypeParameterSynchronizer.GroupBool, order),
                    Name = param.Name ?? $"Bool {order}",
                    DataType = "BOOL",
                    Value = value != null && Convert.ToBoolean(value) ? "true" : "false",
                    PlcVariable = param.PlcVariable,
                    DisplayOrder = order,
                    GroupName = TrainTypeParameterSynchronizer.GroupBool
                });
                order++;
            }

            // Parámetros decimales (columnas G-M)
            foreach (var param in trainConfig.DecimalParameters)
            {
                if (string.IsNullOrEmpty(param.PlcVariable)) continue;

                var value = await ReadOrCollectAsync(param.PlcVariable, typeof(double), $"DECIMAL '{param.Name}'", readErrors);
                result.Parameters.Add(new PlcTrainParameterData
                {
                    Code = TrainTypeParameterSynchronizer.BuildCode(TrainTypeParameterSynchronizer.GroupDecimal, order),
                    Name = param.Name ?? $"Decimal {order}",
                    DataType = "LREAL",
                    Value = FormatDouble(value),
                    MinValue = param.MinValue,
                    MaxValue = param.MaxValue,
                    Unit = param.Unit,
                    PlcVariable = param.PlcVariable,
                    DisplayOrder = order,
                    GroupName = TrainTypeParameterSynchronizer.GroupDecimal
                });
                order++;
            }

            // Parámetros de configuración del Gantry (columnas O-V). Forman parte de la receta
            // igual que BOOL/DECIMAL: si no se persisten, al cargar el slot el PLC conserva el
            // último valor escrito desde el editor (incidente de campo r_TypeOfwash_012).
            foreach (var param in trainConfig.GantryConfigParameters)
            {
                if (string.IsNullOrEmpty(param.PlcVariable)) continue;

                var value = await ReadOrCollectAsync(param.PlcVariable, typeof(double), $"GANTRY '{param.Name}'", readErrors);
                result.Parameters.Add(new PlcTrainParameterData
                {
                    Code = TrainTypeParameterSynchronizer.BuildCode(TrainTypeParameterSynchronizer.GroupGantry, order),
                    Name = string.IsNullOrEmpty(param.Name) ? $"Gantry {order}" : param.Name,
                    DataType = "LREAL",
                    Value = FormatDouble(value),
                    MinValue = param.MinValue,
                    MaxValue = param.MaxValue,
                    Unit = param.Unit,
                    PlcVariable = param.PlcVariable,
                    DisplayOrder = order,
                    GroupName = TrainTypeParameterSynchronizer.GroupGantry
                });
                order++;
            }

            // Tablas de interpolación del Gantry
            result.GantryTables = await ReadGantryInterpolationTablesFromPlcAsync(trainConfig, readErrors);

            if (readErrors.Count > 0)
            {
                _logger.LogError("🚆 [TIPOS DE TREN] Lectura de receta INCOMPLETA: {Count} variables no legibles. NO se guarda nada. Detalle: {Errors}",
                    readErrors.Count, string.Join(" | ", readErrors.Take(20)));
                throw new InvalidOperationException(
                    $"Lectura incompleta del PLC ({readErrors.Count} variables no legibles). No se guarda la receta para no persistir datos inválidos. " +
                    $"Primer error: {readErrors[0]}");
            }

            _logger.LogInformation("🚆 [TIPOS DE TREN] Receta leída del PLC: Bool={Bool}, Decimal={Dec}, Gantry={Gantry}, Tablas={Tables} ({Rows} líneas)",
                result.Parameters.Count(p => p.GroupName == TrainTypeParameterSynchronizer.GroupBool),
                result.Parameters.Count(p => p.GroupName == TrainTypeParameterSynchronizer.GroupDecimal),
                result.Parameters.Count(p => p.GroupName == TrainTypeParameterSynchronizer.GroupGantry),
                result.GantryTables.Count,
                result.GantryTables.Sum(t => t.Rows.Count));

            return result;
        }

        /// <summary>
        /// Lee una variable; si falla, registra el error (L3) y lo acumula para abortar la operación.
        /// </summary>
        private async Task<object?> ReadOrCollectAsync(string variable, Type type, string label, List<string> readErrors)
        {
            try
            {
                return await _twinCATService.ReadVariableAsync(variable, type);
            }
            catch (Exception ex)
            {
                var msg = $"{label} ({variable}): {ex.Message}";
                readErrors.Add(msg);
                _logger.LogError("🚆 [TIPOS DE TREN] ✖ No se pudo leer {Label} ({Var}): {Error}", label, variable, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Serializa un valor numérico leído del PLC con cultura invariante (punto decimal),
        /// que es el formato que esperan las rutas de escritura al parsear Value.
        /// </summary>
        private static string FormatDouble(object? value)
        {
            if (value == null) return "0";
            try
            {
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value.ToString() ?? "0";
            }
        }

        /// <summary>
        /// Leer tablas de interpolación del Gantry desde el PLC (modelo encadenado:
        /// línea N = puntos N (START) y N+1 (END)). Errores de lectura → readErrors (modo estricto).
        /// </summary>
        private async Task<List<PlcGantryTableData>> ReadGantryInterpolationTablesFromPlcAsync(TrainRecipeConfiguration trainConfig, List<string> readErrors)
        {
            var tables = new List<PlcGantryTableData>();

            var allTables = trainConfig.GantryInterpolationTables ?? new List<GantryInterpolationTable>();
            var configuredTables = allTables.Where(t => t.IsConfigured).ToList();

            if (allTables.Count > 0 && configuredTables.Count < allTables.Count)
            {
                _logger.LogWarning("🚆 [TIPOS DE TREN] Tablas Gantry parcialmente configuradas en el Excel: {Configured}/{Total} (faltan plantillas PLC en: {Missing})",
                    configuredTables.Count, allTables.Count,
                    string.Join(", ", allTables.Where(t => !t.IsConfigured).Select(t => t.TableId)));
            }

            foreach (var tableConfig in configuredTables)
            {
                var tableData = new PlcGantryTableData
                {
                    TableIndex = tableConfig.TableIndex,
                    TableId = tableConfig.TableId,
                    Rows = new List<PlcGantryRowData>()
                };

                // Número de líneas habilitadas
                int lineCount = 1;
                if (!string.IsNullOrEmpty(tableConfig.LineCountPlcVariable))
                {
                    var value = await ReadOrCollectAsync(tableConfig.LineCountPlcVariable, typeof(int), $"Gantry {tableConfig.TableId} LineCount", readErrors);
                    if (value == null)
                    {
                        // Sin LineCount fiable no se puede reconstruir la tabla: el error ya está acumulado
                        continue;
                    }
                    lineCount = Convert.ToInt32(value);
                    if (lineCount < 1 || lineCount > 124)
                    {
                        readErrors.Add($"Gantry {tableConfig.TableId} LineCount={lineCount} fuera de rango (1-124)");
                        _logger.LogError("🚆 [TIPOS DE TREN] ✖ Gantry {TableId}: LineCount={Count} fuera de rango (1-124)", tableConfig.TableId, lineCount);
                        continue;
                    }
                }
                else
                {
                    _logger.LogWarning("🚆 [TIPOS DE TREN] Gantry {TableId}: sin variable LineCount en el Excel, se asume 1 línea", tableConfig.TableId);
                }

                for (int lineNum = 1; lineNum <= lineCount; lineNum++)
                {
                    var row = new PlcGantryRowData { RowNumber = lineNum, EnableLine = true };
                    int startIndex = GantryInterpolationTable.GetStartPointIndex(lineNum);
                    int endIndex = GantryInterpolationTable.GetEndPointIndex(lineNum);
                    string label = $"Gantry {tableConfig.TableId} L{lineNum}";

                    var funcType = await ReadOrCollectAsync(tableConfig.GetFunctionTypePlcVariable(startIndex), typeof(sbyte), $"{label} Start.FunctionType", readErrors);
                    row.Syncron = TrainTypePlcWriter.FunctionTypeToSyncron(Convert.ToInt32(funcType ?? 1));

                    var sx = await ReadOrCollectAsync(tableConfig.GetPositionXPlcVariable(startIndex), typeof(double), $"{label} Start.Position_X", readErrors);
                    var sy = await ReadOrCollectAsync(tableConfig.GetPositionYPlcVariable(startIndex), typeof(double), $"{label} Start.Position_Y", readErrors);
                    var ss = await ReadOrCollectAsync(tableConfig.GetSpeedYPlcVariable(startIndex), typeof(double), $"{label} Start.Speed_Y", readErrors);
                    var ex = await ReadOrCollectAsync(tableConfig.GetPositionXPlcVariable(endIndex), typeof(double), $"{label} End.Position_X", readErrors);
                    var ey = await ReadOrCollectAsync(tableConfig.GetPositionYPlcVariable(endIndex), typeof(double), $"{label} End.Position_Y", readErrors);
                    var es = await ReadOrCollectAsync(tableConfig.GetSpeedYPlcVariable(endIndex), typeof(double), $"{label} End.Speed_Y", readErrors);

                    row.Master1xStart = Convert.ToDouble(sx ?? 0.0);
                    row.Slave1yStart = Convert.ToDouble(sy ?? 0.0);
                    row.SpeedSlaveY1Start = Convert.ToDouble(ss ?? 0.0);
                    row.Master1xEnd = Convert.ToDouble(ex ?? 0.0);
                    row.Slave1yEnd = Convert.ToDouble(ey ?? 0.0);
                    row.SpeedSlaveY1End = Convert.ToDouble(es ?? 0.0);

                    tableData.Rows.Add(row);
                }

                tables.Add(tableData);
                _logger.LogDebug("🚆 Leída tabla {TableId}: {RowCount} líneas", tableConfig.TableId, tableData.Rows.Count);
            }

            return tables;
        }

        /// <summary>
        /// Reemplaza íntegramente los datos de Gantry de un TrainType existente por los leídos del PLC.
        /// Se ejecuta aunque la lista esté vacía (Excel sin tablas) para no dejar filas obsoletas.
        /// </summary>
        private async Task UpdateGantryDataAsync(TrainType trainType, List<PlcGantryTableData> gantryTables)
        {
            gantryTables ??= new List<PlcGantryTableData>();

            var existingData = await _dbContext.TrainTypeGantryData
                .Where(g => g.TrainTypeId == trainType.Id)
                .ToListAsync();

            _dbContext.TrainTypeGantryData.RemoveRange(existingData);
            trainType.GantryData.Clear();

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
            _logger.LogInformation("🚆 [TIPOS DE TREN] GantryData TrainType {Id}: {Removed} filas eliminadas, {Added} filas guardadas en {TableCount} tablas",
                trainType.Id, existingData.Count, totalRows, gantryTables.Count);
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
