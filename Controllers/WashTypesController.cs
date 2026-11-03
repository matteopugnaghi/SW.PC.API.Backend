// ============================================================================
// WashTypesController.cs - API para Tipos de Lavados
// ============================================================================
// Endpoints para:
// - GET /api/wash-types: Listar todos los tipos de lavado
// - GET /api/wash-types/{id}: Obtener detalle de un tipo de lavado
// - POST /api/wash-types: Crear nuevo tipo de lavado
// - PUT /api/wash-types/{id}: Actualizar tipo de lavado
// - DELETE /api/wash-types/{id}: Eliminar tipo de lavado
// - GET /api/wash-types/active: Obtener tipo de lavado activo
// - POST /api/wash-types/select: Seleccionar tipo de lavado (guardar en DB)
// - POST /api/wash-types/write-to-plc: Escribir tipo activo al PLC
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
    [Route("api/wash-types")]
    [Authorize]
    public class WashTypesController : ControllerBase
    {
        private readonly AquafrischDbContext _dbContext;
        private readonly ITwinCATService _twinCATService;
        private readonly IExcelConfigService _excelService;
        private readonly IOperationLogService _operationLog;
        private readonly IPlcWriteVerificationService _verifier;
        private readonly ILogger<WashTypesController> _logger;

        public WashTypesController(
            AquafrischDbContext dbContext,
            ITwinCATService twinCATService,
            IExcelConfigService excelService,
            IOperationLogService operationLog,
            IPlcWriteVerificationService verifier,
            ILogger<WashTypesController> logger)
        {
            _dbContext = dbContext;
            _twinCATService = twinCATService;
            _excelService = excelService;
            _operationLog = operationLog;
            _verifier = verifier;
            _logger = logger;
        }

        /// <summary>
        /// Obtener lista de todos los tipos de lavado
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<WashTypeListDto>), 200)]
        public async Task<ActionResult<List<WashTypeListDto>>> GetAllWashTypes([FromQuery] bool includeInactive = false)
        {
            try
            {
                var query = _dbContext.WashTypes.AsQueryable();

                if (!includeInactive)
                {
                    query = query.Where(w => w.IsActive);
                }

                var washTypes = await query
                    .OrderBy(w => w.DisplayOrder)
                    .ThenBy(w => w.Name)
                    .Select(w => new WashTypeListDto
                    {
                        Id = w.Id,
                        Code = w.Code,
                        Name = w.Name,
                        Description = w.Description,
                        Icon = w.Icon,
                        Color = w.Color,
                        IsActive = w.IsActive,
                        IsDefault = w.IsDefault,
                        DisplayOrder = w.DisplayOrder,
                        ParameterCount = w.Parameters.Count
                    })
                    .ToListAsync();

                _logger.LogInformation("📋 Retrieved {Count} wash types", washTypes.Count);
                return Ok(washTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving wash types");
                return StatusCode(500, new { error = "Error al obtener los tipos de lavado" });
            }
        }

        /// <summary>
        /// Obtener detalle de un tipo de lavado específico
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(WashTypeDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<WashTypeDetailDto>> GetWashType(int id)
        {
            try
            {
                var washType = await _dbContext.WashTypes
                    .Include(w => w.Parameters.OrderBy(p => p.DisplayOrder))
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {id} no encontrado" });
                }

                var dto = new WashTypeDetailDto
                {
                    Id = washType.Id,
                    Code = washType.Code,
                    Name = washType.Name,
                    Description = washType.Description,
                    Icon = washType.Icon,
                    Color = washType.Color,
                    IsActive = washType.IsActive,
                    IsDefault = washType.IsDefault,
                    DisplayOrder = washType.DisplayOrder,
                    CreatedAt = washType.CreatedAt,
                    UpdatedAt = washType.UpdatedAt,
                    Parameters = washType.Parameters.Select(p => new WashTypeParameterDto
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
                        IsEditable = p.IsEditable
                    }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving wash type {Id}", id);
                return StatusCode(500, new { error = "Error al obtener el tipo de lavado" });
            }
        }

        /// <summary>
        /// Crear nuevo tipo de lavado
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(WashTypeDetailDto), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<WashTypeDetailDto>> CreateWashType([FromBody] WashTypeCreateDto dto)
        {
            try
            {
                // Validar que el código no exista
                var exists = await _dbContext.WashTypes.AnyAsync(w => w.Code == dto.Code);
                if (exists)
                {
                    return BadRequest(new { error = $"Ya existe un tipo de lavado con el código '{dto.Code}'" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Si es default, quitar default de los demás
                if (dto.IsDefault)
                {
                    await _dbContext.WashTypes
                        .Where(w => w.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false));
                }

                var washType = new WashType
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

                // Agregar parámetros si existen
                if (dto.Parameters != null)
                {
                    foreach (var paramDto in dto.Parameters)
                    {
                        washType.Parameters.Add(new WashTypeParameter
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
                            IsEditable = paramDto.IsEditable
                        });
                    }
                }

                _dbContext.WashTypes.Add(washType);
                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeCreate,
                    $"{washType.Name} ({washType.Code})",
                    username);

                _logger.LogInformation("✅ Created wash type: {Name} ({Code})", washType.Name, washType.Code);

                return CreatedAtAction(nameof(GetWashType), new { id = washType.Id }, 
                    await GetWashTypeDetailDto(washType.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating wash type");
                return StatusCode(500, new { error = "Error al crear el tipo de lavado" });
            }
        }

        /// <summary>
        /// Actualizar tipo de lavado existente
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(WashTypeDetailDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<WashTypeDetailDto>> UpdateWashType(int id, [FromBody] WashTypeCreateDto dto)
        {
            try
            {
                var washType = await _dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {id} no encontrado" });
                }

                // Validar que el código no esté en uso por otro
                var codeExists = await _dbContext.WashTypes
                    .AnyAsync(w => w.Code == dto.Code && w.Id != id);
                if (codeExists)
                {
                    return BadRequest(new { error = $"Ya existe otro tipo de lavado con el código '{dto.Code}'" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Si es default, quitar default de los demás
                if (dto.IsDefault && !washType.IsDefault)
                {
                    await _dbContext.WashTypes
                        .Where(w => w.IsDefault && w.Id != id)
                        .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false));
                }

                // Actualizar propiedades
                washType.Code = dto.Code;
                washType.Name = dto.Name;
                washType.Description = dto.Description;
                washType.Icon = dto.Icon;
                washType.Color = dto.Color;
                washType.IsActive = dto.IsActive;
                washType.IsDefault = dto.IsDefault;
                washType.DisplayOrder = dto.DisplayOrder;
                washType.UpdatedAt = DateTime.UtcNow;
                washType.UpdatedBy = username;

                // Actualizar parámetros si se proporcionan
                if (dto.Parameters != null)
                {
                    // Eliminar parámetros existentes
                    _dbContext.WashTypeParameters.RemoveRange(washType.Parameters);

                    // Agregar nuevos parámetros
                    foreach (var paramDto in dto.Parameters)
                    {
                        washType.Parameters.Add(new WashTypeParameter
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
                            IsEditable = paramDto.IsEditable
                        });
                    }
                }

                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeEdit,
                    $"{washType.Name} ({washType.Code})",
                    username);

                _logger.LogInformation("✅ Updated wash type: {Name} ({Code})", washType.Name, washType.Code);

                return Ok(await GetWashTypeDetailDto(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating wash type {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar el tipo de lavado" });
            }
        }

        /// <summary>
        /// Eliminar tipo de lavado (solo SuperAdmin / Administrator)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "SuperAdmin,Administrator")]
        [ProducesResponseType(204)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteWashType(int id)
        {
            try
            {
                var washType = await _dbContext.WashTypes.FindAsync(id);
                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Si está marcado como activo, eliminar primero las referencias (purga total para SuperAdmin/Administrator).
                // Se persisten en una transacción separada para garantizar que el DELETE de ActiveWashType
                // se ejecuta antes que el del WashType (FK ON DELETE RESTRICT a nivel de DB).
                var activeRefs = await _dbContext.ActiveWashTypes.Where(a => a.WashTypeId == id).ToListAsync();
                if (activeRefs.Count > 0)
                {
                    _dbContext.ActiveWashTypes.RemoveRange(activeRefs);
                    await _dbContext.SaveChangesAsync();
                }

                _dbContext.WashTypes.Remove(washType);
                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeDelete,
                    $"{washType.Name} ({washType.Code})",
                    username);

                _logger.LogInformation("🗑️ Deleted wash type: {Name} ({Code})", washType.Name, washType.Code);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting wash type {Id}", id);
                return StatusCode(500, new { error = "Error al eliminar el tipo de lavado" });
            }
        }

        /// <summary>
        /// Obtener el tipo de lavado actualmente seleccionado
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(typeof(ActiveWashTypeDto), 200)]
        public async Task<ActionResult<ActiveWashTypeDto>> GetActiveWashType()
        {
            try
            {
                var active = await _dbContext.ActiveWashTypes
                    .Include(a => a.WashType)
                    .OrderByDescending(a => a.SelectedAt)
                    .FirstOrDefaultAsync();

                if (active == null)
                {
                    return Ok(new ActiveWashTypeDto
                    {
                        WashTypeId = null,
                        WashTypeCode = null,
                        WashTypeName = null,
                        SelectedAt = null,
                        SelectedBy = null,
                        WrittenToPlc = false,
                        WrittenToPlcAt = null
                    });
                }

                return Ok(new ActiveWashTypeDto
                {
                    WashTypeId = active.WashTypeId,
                    WashTypeCode = active.WashType?.Code,
                    WashTypeName = active.WashType?.Name,
                    SelectedAt = active.SelectedAt,
                    SelectedBy = active.SelectedBy,
                    WrittenToPlc = active.WrittenToPlc,
                    WrittenToPlcAt = active.WrittenToPlcAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving active wash type");
                return StatusCode(500, new { error = "Error al obtener el tipo de lavado activo" });
            }
        }

        /// <summary>
        /// Seleccionar un tipo de lavado como activo (guardar en DB)
        /// </summary>
        [HttpPost("select")]
        [ProducesResponseType(typeof(ActiveWashTypeDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ActiveWashTypeDto>> SelectWashType([FromBody] SelectWashTypeDto dto)
        {
            try
            {
                var washType = await _dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .FirstOrDefaultAsync(w => w.Id == dto.WashTypeId);

                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {dto.WashTypeId} no encontrado" });
                }

                if (!washType.IsActive)
                {
                    return BadRequest(new { error = "El tipo de lavado seleccionado no está activo" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Eliminar selección anterior (solo mantener una)
                var existingActive = await _dbContext.ActiveWashTypes.ToListAsync();
                _dbContext.ActiveWashTypes.RemoveRange(existingActive);

                // Crear nueva selección
                var active = new ActiveWashType
                {
                    WashTypeId = dto.WashTypeId,
                    SelectedAt = DateTime.UtcNow,
                    SelectedBy = username,
                    WrittenToPlc = false
                };

                _dbContext.ActiveWashTypes.Add(active);
                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeLoad,
                    $"{washType.Name} ({washType.Code})",
                    username);

                _logger.LogInformation("✅ Selected wash type: {Name} ({Code})", washType.Name, washType.Code);

                // Si se solicita, escribir al PLC
                if (dto.WriteToPlc)
                {
                    var writeResult = await WriteWashTypeToPlcInternal(washType, active, username);
                    if (!writeResult.Success)
                    {
                        _logger.LogWarning("⚠️ Wash type selected but PLC write failed: {Errors}", 
                            string.Join(", ", writeResult.Errors ?? new List<string>()));
                    }
                }

                return Ok(new ActiveWashTypeDto
                {
                    WashTypeId = active.WashTypeId,
                    WashTypeCode = washType.Code,
                    WashTypeName = washType.Name,
                    SelectedAt = active.SelectedAt,
                    SelectedBy = active.SelectedBy,
                    WrittenToPlc = active.WrittenToPlc,
                    WrittenToPlcAt = active.WrittenToPlcAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error selecting wash type");
                return StatusCode(500, new { error = "Error al seleccionar el tipo de lavado" });
            }
        }

        /// <summary>
        /// Escribir el tipo de lavado activo al PLC
        /// </summary>
        [HttpPost("write-to-plc")]
        [ProducesResponseType(typeof(WriteToPlcResponseDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<WriteToPlcResponseDto>> WriteToPlc()
        {
            try
            {
                var active = await _dbContext.ActiveWashTypes
                    .Include(a => a.WashType)
                        .ThenInclude(w => w!.Parameters)
                    .OrderByDescending(a => a.SelectedAt)
                    .FirstOrDefaultAsync();

                if (active == null || active.WashType == null)
                {
                    return BadRequest(new { error = "No hay un tipo de lavado seleccionado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                var result = await WriteWashTypeToPlcInternal(active.WashType, active, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing wash type to PLC");
                return StatusCode(500, new { error = "Error al escribir al PLC" });
            }
        }

        /// <summary>
        /// Escribir un tipo de lavado específico al PLC (DB → PLC)
        /// </summary>
        [HttpPost("{id:int}/write-to-plc")]
        [ProducesResponseType(typeof(WriteToPlcResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<WriteToPlcResponseDto>> WriteSpecificToPlc(int id)
        {
            try
            {
                var washType = await _dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {id} no encontrado" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                // Crear o actualizar ActiveWashType
                var existingActive = await _dbContext.ActiveWashTypes.ToListAsync();
                _dbContext.ActiveWashTypes.RemoveRange(existingActive);

                var active = new ActiveWashType
                {
                    WashTypeId = id,
                    SelectedAt = DateTime.UtcNow,
                    SelectedBy = username,
                    WrittenToPlc = false
                };
                _dbContext.ActiveWashTypes.Add(active);
                await _dbContext.SaveChangesAsync();

                var result = await WriteWashTypeToPlcInternal(washType, active, username);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing specific wash type to PLC");
                return StatusCode(500, new { error = "Error al escribir al PLC" });
            }
        }

        /// <summary>
        /// Escribir un tipo de lavado específico al PLC usando prefijo alternativo (DB → PLC 2)
        /// Las variables PLC se modifican reemplazando "st_WashRecipe" por el valor de A14
        /// </summary>
        [HttpPost("{id:int}/write-to-plc-alternate")]
        [ProducesResponseType(typeof(WriteToPlcResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<WriteToPlcResponseDto>> WriteSpecificToPlcAlternate(int id)
        {
            try
            {
                var washType = await _dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .FirstOrDefaultAsync(w => w.Id == id);

                if (washType == null)
                {
                    return NotFound(new { error = $"Tipo de lavado con ID {id} no encontrado" });
                }

                // Cargar configuración del Excel para obtener el prefijo alternativo
                var excelPath = _excelService.GetExcelConfigPath();
                var excelConfig = await _excelService.LoadWashRecipeConfigAsync(excelPath);

                if (!excelConfig.AlternateWriteEnabled)
                {
                    return BadRequest(new { error = "Escritura alternativa no habilitada (A13 no está en ON)" });
                }

                if (string.IsNullOrEmpty(excelConfig.AlternateWritePlcPrefix))
                {
                    return BadRequest(new { error = "Prefijo PLC alternativo no configurado (A14 está vacío)" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                var result = await WriteWashTypeToPlcAlternateInternal(
                    washType, 
                    excelConfig.AlternateWritePlcPrefix,
                    username);
                    
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error writing specific wash type to PLC (alternate)");
                return StatusCode(500, new { error = "Error al escribir al PLC (alternativo)" });
            }
        }

        /// <summary>
        /// Guardar tipo de lavado desde PLC (PLC → DB)
        /// Lee los valores actuales del PLC y los guarda en el slot indicado
        /// </summary>
        [HttpPost("save-from-plc")]
        [ProducesResponseType(typeof(WashTypeDetailDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<WashTypeDetailDto>> SaveFromPlc([FromBody] SaveFromPlcDto dto)
        {
            try
            {
                if (dto.SlotNumber < 1 || dto.SlotNumber > 20)
                {
                    return BadRequest(new { error = "El número de slot debe estar entre 1 y 20" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var slotCode = $"WASH_{dto.SlotNumber:D2}";

                // Leer valores del PLC usando el servicio de Excel (que define las variables)
                var plcValues = await ReadWashRecipeFromPlcAsync();

                // Buscar si ya existe el slot
                var existingWashType = await _dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .FirstOrDefaultAsync(w => w.Code == slotCode);

                if (existingWashType != null)
                {
                    // Actualizar existente
                    existingWashType.Name = plcValues.RecipeName ?? $"Receta {dto.SlotNumber}";
                    existingWashType.UpdatedAt = DateTime.UtcNow;
                    existingWashType.UpdatedBy = username;

                    // Sincronizar por variable PLC (no por código estación/índice): actualiza
                    // metadatos+valor, añade nuevos y elimina huérfanos que ya no están en el Excel.
                    var sync = WashTypeParameterSynchronizer.Sync(existingWashType.Parameters, plcValues.Parameters);
                    if (sync.Removed.Count > 0)
                    {
                        _dbContext.Set<WashTypeParameter>().RemoveRange(sync.Removed);
                        await _verifier.LogIntegrityWarningsAsync(OperationCategory.Recipe, $"Guardar slot lavado {dto.SlotNumber} desde PLC",
                            new[] { $"Eliminados {sync.Removed.Count} parámetros huérfanos (ya no están en el Excel): {string.Join(", ", sync.Removed.Select(p => p.PlcVariable))}" },
                            username);
                    }
                    _logger.LogInformation("🚿 Slot {Slot}: parámetros sincronizados → {Updated} actualizados, {Added} nuevos, {Removed} eliminados",
                        dto.SlotNumber, sync.Updated, sync.Added, sync.Removed.Count);
                }
                else
                {
                    // Crear nuevo
                    existingWashType = new WashType
                    {
                        Code = slotCode,
                        Name = plcValues.RecipeName ?? $"Receta {dto.SlotNumber}",
                        Description = $"Receta guardada desde PLC - Slot {dto.SlotNumber}",
                        Icon = "🚿",
                        Color = "#3498db",
                        IsActive = true,
                        IsDefault = dto.SlotNumber == 1,
                        DisplayOrder = dto.SlotNumber,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username,
                        Parameters = plcValues.Parameters
                            .Where(p => !string.IsNullOrWhiteSpace(p.PlcVariable))
                            .Select(WashTypeParameterSynchronizer.ToEntity)
                            .ToList()
                    };

                    _dbContext.WashTypes.Add(existingWashType);
                }

                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeSaveFromPlc,
                    $"Slot {dto.SlotNumber} - {existingWashType.Name}",
                    username);

                _logger.LogInformation("✅ Saved wash type from PLC: Slot {Slot} - {Name}", dto.SlotNumber, existingWashType.Name);

                return Ok(new WashTypeDetailDto
                {
                    Id = existingWashType.Id,
                    Code = existingWashType.Code,
                    Name = existingWashType.Name,
                    Description = existingWashType.Description,
                    Icon = existingWashType.Icon,
                    Color = existingWashType.Color,
                    IsActive = existingWashType.IsActive,
                    IsDefault = existingWashType.IsDefault,
                    DisplayOrder = existingWashType.DisplayOrder,
                    Parameters = existingWashType.Parameters.Select(p => new WashTypeParameterDto
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
                });
            }
            catch (InvalidOperationException ex)
            {
                // Error de conexión al PLC - mensaje claro para el usuario
                _logger.LogWarning(ex, "⚠️ No hay conexión al PLC para guardar tipo de lavado");
                return StatusCode(503, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving wash type from PLC");
                return StatusCode(500, new { error = "Error al guardar desde PLC: " + ex.Message });
            }
        }

        /// <summary>
        /// Leer la receta de lavado actual desde el PLC
        /// Usa las variables definidas en el Excel (hoja WashRecipe)
        /// Lanza excepción si no hay conexión - NO genera datos falsos
        /// </summary>
        private async Task<PlcWashRecipeData> ReadWashRecipeFromPlcAsync()
        {
            var result = new PlcWashRecipeData
            {
                Parameters = new List<PlcParameterData>()
            };

            // Verificar conexión al PLC primero
            if (!_twinCATService.IsConnected)
            {
                throw new InvalidOperationException("No hay conexión con el PLC. No se pueden leer los valores actuales.");
            }

            // Cargar configuración desde Excel (hoja WashRecipe)
            var excelPath = _excelService.GetExcelConfigPath();
            var excelConfig = await _excelService.LoadWashRecipeConfigAsync(excelPath);

            // Leer nombre de receta desde PLC (variable de A3 del Excel)
            if (!string.IsNullOrEmpty(excelConfig.RecipeNamePlcVariable))
            {
                _logger.LogInformation("🚿 Leyendo nombre de receta desde PLC: {Var}", excelConfig.RecipeNamePlcVariable);
                var recipeNameValue = await _twinCATService.ReadVariableAsync(excelConfig.RecipeNamePlcVariable, typeof(string));
                result.RecipeName = recipeNameValue?.ToString() ?? "Receta PLC";
            }
            else
            {
                _logger.LogWarning("⚠️ No hay variable PLC configurada para nombre de receta en A3 del Excel");
                result.RecipeName = "Receta PLC";
            }

            int displayOrder = 1;

            // Leer parámetros de todas las estaciones configuradas en el Excel
            foreach (var station in excelConfig.Stations)
            {
                // Leer parámetros BOOL de esta estación
                foreach (var boolParam in station.BoolParameters.Where(p => p.IsConfigured))
                {
                    var value = await _twinCATService.ReadVariableAsync(boolParam.PlcVariable, typeof(bool));
                    result.Parameters.Add(new PlcParameterData
                    {
                        Code = $"BOOL_{station.Index}_{boolParam.Index}",
                        Name = boolParam.Description ?? $"Bool {boolParam.Index}",
                        DataType = "BOOL",
                        Value = value?.ToString()?.ToLower() ?? "false",
                        PlcVariable = boolParam.PlcVariable,
                        DisplayOrder = displayOrder++
                    });
                }

                // Leer parámetros INT de esta estación
                foreach (var intParam in station.IntParameters.Where(p => p.IsConfigured))
                {
                    var value = await _twinCATService.ReadVariableAsync(intParam.PlcVariable, typeof(int));
                    result.Parameters.Add(new PlcParameterData
                    {
                        Code = $"INT_{station.Index}_{intParam.Index}",
                        Name = intParam.Description ?? $"Int {intParam.Index}",
                        DataType = "INT",
                        Value = value?.ToString() ?? "0",
                        MinValue = intParam.MinValue,
                        MaxValue = intParam.MaxValue,
                        Unit = intParam.Unit,
                        PlcVariable = intParam.PlcVariable,
                        DisplayOrder = displayOrder++
                    });
                }
            }

            _logger.LogInformation("🚿 Leídos {Count} parámetros del PLC desde configuración Excel", result.Parameters.Count);
            return result;
        }

        /// <summary>
        /// Inicializar datos de prueba (solo desarrollo)
        /// </summary>
        [HttpPost("seed")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SeedTestData()
        {
            try
            {
                // Solo si no hay datos
                if (await _dbContext.WashTypes.AnyAsync())
                {
                    return Ok(new { message = "Ya existen tipos de lavado en la base de datos" });
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

                var washTypes = new List<WashType>
                {
                    new WashType
                    {
                        Code = "WASH_STANDARD",
                        Name = "Lavado Estándar",
                        Description = "Programa de lavado estándar para uso diario",
                        Icon = "🚿",
                        Color = "#3498db",
                        IsActive = true,
                        IsDefault = true,
                        DisplayOrder = 1,
                        CreatedBy = username,
                        Parameters = new List<WashTypeParameter>
                        {
                            new WashTypeParameter { ParameterCode = "TEMP_AGUA", Name = "Temperatura del Agua", DataType = "LREAL", Value = "45.0", MinValue = 20, MaxValue = 80, Unit = "°C", DisplayOrder = 1 },
                            new WashTypeParameter { ParameterCode = "PRESION", Name = "Presión", DataType = "LREAL", Value = "2.5", MinValue = 1, MaxValue = 5, Unit = "bar", DisplayOrder = 2 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_LAVADO", Name = "Tiempo de Lavado", DataType = "INT", Value = "180", MinValue = 60, MaxValue = 600, Unit = "seg", DisplayOrder = 3 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_SECADO", Name = "Tiempo de Secado", DataType = "INT", Value = "120", MinValue = 30, MaxValue = 300, Unit = "seg", DisplayOrder = 4 }
                        }
                    },
                    new WashType
                    {
                        Code = "WASH_EXPRESS",
                        Name = "Lavado Express",
                        Description = "Lavado rápido para vehículos con suciedad ligera",
                        Icon = "⚡",
                        Color = "#e74c3c",
                        IsActive = true,
                        IsDefault = false,
                        DisplayOrder = 2,
                        CreatedBy = username,
                        Parameters = new List<WashTypeParameter>
                        {
                            new WashTypeParameter { ParameterCode = "TEMP_AGUA", Name = "Temperatura del Agua", DataType = "LREAL", Value = "40.0", MinValue = 20, MaxValue = 80, Unit = "°C", DisplayOrder = 1 },
                            new WashTypeParameter { ParameterCode = "PRESION", Name = "Presión", DataType = "LREAL", Value = "3.0", MinValue = 1, MaxValue = 5, Unit = "bar", DisplayOrder = 2 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_LAVADO", Name = "Tiempo de Lavado", DataType = "INT", Value = "90", MinValue = 60, MaxValue = 600, Unit = "seg", DisplayOrder = 3 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_SECADO", Name = "Tiempo de Secado", DataType = "INT", Value = "60", MinValue = 30, MaxValue = 300, Unit = "seg", DisplayOrder = 4 }
                        }
                    },
                    new WashType
                    {
                        Code = "WASH_INTENSIVE",
                        Name = "Lavado Intensivo",
                        Description = "Programa intensivo para vehículos muy sucios",
                        Icon = "💪",
                        Color = "#27ae60",
                        IsActive = true,
                        IsDefault = false,
                        DisplayOrder = 3,
                        CreatedBy = username,
                        Parameters = new List<WashTypeParameter>
                        {
                            new WashTypeParameter { ParameterCode = "TEMP_AGUA", Name = "Temperatura del Agua", DataType = "LREAL", Value = "55.0", MinValue = 20, MaxValue = 80, Unit = "°C", DisplayOrder = 1 },
                            new WashTypeParameter { ParameterCode = "PRESION", Name = "Presión", DataType = "LREAL", Value = "3.5", MinValue = 1, MaxValue = 5, Unit = "bar", DisplayOrder = 2 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_LAVADO", Name = "Tiempo de Lavado", DataType = "INT", Value = "300", MinValue = 60, MaxValue = 600, Unit = "seg", DisplayOrder = 3 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_SECADO", Name = "Tiempo de Secado", DataType = "INT", Value = "180", MinValue = 30, MaxValue = 300, Unit = "seg", DisplayOrder = 4 },
                            new WashTypeParameter { ParameterCode = "PRELAVADO", Name = "Prelavado Activo", DataType = "BOOL", Value = "true", DisplayOrder = 5 }
                        }
                    },
                    new WashType
                    {
                        Code = "WASH_ECO",
                        Name = "Lavado Ecológico",
                        Description = "Programa ecológico con bajo consumo de agua y energía",
                        Icon = "🌿",
                        Color = "#2ecc71",
                        IsActive = true,
                        IsDefault = false,
                        DisplayOrder = 4,
                        CreatedBy = username,
                        Parameters = new List<WashTypeParameter>
                        {
                            new WashTypeParameter { ParameterCode = "TEMP_AGUA", Name = "Temperatura del Agua", DataType = "LREAL", Value = "35.0", MinValue = 20, MaxValue = 80, Unit = "°C", DisplayOrder = 1 },
                            new WashTypeParameter { ParameterCode = "PRESION", Name = "Presión", DataType = "LREAL", Value = "2.0", MinValue = 1, MaxValue = 5, Unit = "bar", DisplayOrder = 2 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_LAVADO", Name = "Tiempo de Lavado", DataType = "INT", Value = "150", MinValue = 60, MaxValue = 600, Unit = "seg", DisplayOrder = 3 },
                            new WashTypeParameter { ParameterCode = "TIEMPO_SECADO", Name = "Tiempo de Secado", DataType = "INT", Value = "90", MinValue = 30, MaxValue = 300, Unit = "seg", DisplayOrder = 4 },
                            new WashTypeParameter { ParameterCode = "RECICLAJE_AGUA", Name = "Reciclaje de Agua", DataType = "BOOL", Value = "true", DisplayOrder = 5 }
                        }
                    }
                };

                _dbContext.WashTypes.AddRange(washTypes);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("🌱 Seeded {Count} wash types", washTypes.Count);

                return Ok(new { message = $"Creados {washTypes.Count} tipos de lavado de prueba" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error seeding wash types");
                return StatusCode(500, new { error = "Error al crear datos de prueba" });
            }
        }

        #region Private Methods

        private async Task<WashTypeDetailDto> GetWashTypeDetailDto(int id)
        {
            var washType = await _dbContext.WashTypes
                .Include(w => w.Parameters.OrderBy(p => p.DisplayOrder))
                .FirstAsync(w => w.Id == id);

            return new WashTypeDetailDto
            {
                Id = washType.Id,
                Code = washType.Code,
                Name = washType.Name,
                Description = washType.Description,
                Icon = washType.Icon,
                Color = washType.Color,
                IsActive = washType.IsActive,
                IsDefault = washType.IsDefault,
                DisplayOrder = washType.DisplayOrder,
                CreatedAt = washType.CreatedAt,
                UpdatedAt = washType.UpdatedAt,
                Parameters = washType.Parameters.Select(p => new WashTypeParameterDto
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
                    IsEditable = p.IsEditable
                }).ToList()
            };
        }

        /// <summary>
        /// Escribir el tipo de lavado activo al PLC principal (verificado por relectura).
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteWashTypeToPlcInternal(
            WashType washType, 
            ActiveWashType active, 
            string username)
        {
            var result = await WriteWashTypeCoreAsync(washType, alternatePlcPrefix: null, username);

            // Actualizar estado de escritura
            active.WrittenToPlc = result.Success;
            active.WrittenToPlcAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.WashTypeWritePlc,
                result.Success ? washType.Name : $"{washType.Name} ({result.Errors?.Count ?? 0} errores)",
                username,
                result.Success ? null : new Dictionary<string, object> { ["errors"] = (result.Errors ?? new()).Take(50).ToList() });

            return result;
        }

        /// <summary>
        /// Escribir tipo de lavado al PLC alternativo usando el prefijo de A14 (sustituye "st_WashRecipe").
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteWashTypeToPlcAlternateInternal(
            WashType washType, 
            string alternatePlcPrefix,
            string username)
        {
            _logger.LogInformation("🚿 Escribiendo tipo de lavado al PLC con prefijo alternativo: {Prefix}", alternatePlcPrefix);

            var result = await WriteWashTypeCoreAsync(washType, alternatePlcPrefix, username);

            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.WashTypeWritePlc,
                result.Success ? $"{washType.Name} (PLC ALT)" : $"{washType.Name} (PLC ALT, {result.Errors?.Count ?? 0} errores)",
                username,
                result.Success ? null : new Dictionary<string, object> { ["errors"] = (result.Errors ?? new()).Take(50).ToList() });

            return result;
        }

        /// <summary>
        /// Implementación única DB → PLC de un tipo de lavado:
        ///   1. Nombre de receta (A3) y línea (A4)
        ///   2. Parámetros del slot (sólo variables definidas en el Excel actual; aviso de las que faltan)
        ///   3. Verificación por relectura → L3 + L2 (retentivo) si hay discrepancias
        /// Cada escritura comprueba el resultado del driver ADS (false = error, no éxito).
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteWashTypeCoreAsync(WashType washType, string? alternatePlcPrefix, string username)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            int parametersWritten = 0;
            string Map(string v) => WashTypeParameterSynchronizer.ApplyAlternatePrefix(v, alternatePlcPrefix);
            var label = alternatePlcPrefix == null ? "PLC" : $"PLC ALT ({alternatePlcPrefix})";
            var context = $"Tipo de lavado '{washType.Name}' ({washType.Code}) → {label}";
            var batch = _verifier.CreateBatch(context);

            var excelPath = _excelService.GetExcelConfigPath();
            var excelConfig = await _excelService.LoadWashRecipeConfigAsync(excelPath);

            async Task TryWriteAsync(string what, string? variable, object value, Type type)
            {
                if (string.IsNullOrWhiteSpace(variable))
                {
                    return;
                }
                var target = Map(variable.Trim());
                try
                {
                    await batch.WriteAsync(target, value, type);
                    parametersWritten++;
                    _logger.LogDebug("✅ {What} escrito: {Var} = {Value}", what, target, value);
                }
                catch (Exception ex)
                {
                    errors.Add($"{what} ({target}): {ex.Message}");
                    _logger.LogWarning("⚠️ Error escribiendo {What} ({Var}): {Error}", what, target, ex.Message);
                }
            }

            // 1. Cabecera
            if (string.IsNullOrEmpty(excelConfig.RecipeNamePlcVariable))
            {
                warnings.Add("No hay variable PLC configurada para el nombre de receta (Excel WashRecipe A3)");
            }
            await TryWriteAsync("Nombre de receta", excelConfig.RecipeNamePlcVariable, washType.Name, typeof(string));

            var lineNumber = washType.DisplayOrder > 0 ? washType.DisplayOrder : washType.Id;
            await TryWriteAsync("Línea de receta", excelConfig.RecipeLineNumberPlcVariable, lineNumber, typeof(int));

            // 2. Parámetros
            var writable = WashTypeParameterSynchronizer.GetWritableParameters(washType.Parameters, excelConfig, out var paramWarnings);
            warnings.AddRange(paramWarnings);

            if (writable.Count == 0)
            {
                warnings.Add("El slot no tiene parámetros con variable PLC para escribir");
            }

            foreach (var param in writable)
            {
                var (value, type) = WashTypeParameterSynchronizer.ToPlcValue(param);
                await TryWriteAsync($"'{param.Name}'", param.PlcVariable, value, type);
            }

            // 3. Verificación por relectura
            var verification = await _verifier.VerifyAsync(batch, OperationCategory.Recipe, username);
            if (!verification.Success)
            {
                errors.AddRange(verification.ToErrorStrings());
            }

            await _verifier.LogIntegrityWarningsAsync(OperationCategory.Recipe, context, warnings, username);

            var success = errors.Count == 0 && parametersWritten > 0;
            _logger.Log(success ? LogLevel.Information : LogLevel.Error,
                "🚿 {Context}: {Written} escritos, {Errors} errores, {Warnings} avisos. {Verification}",
                context, parametersWritten, errors.Count, warnings.Count, verification.Summary);

            return new WriteToPlcResponseDto
            {
                Success = success,
                Message = parametersWritten == 0
                    ? "No se escribió ningún parámetro. Verifica que el slot tenga parámetros guardados y el Excel WashRecipe esté configurado."
                    : errors.Count == 0
                        ? $"Tipo de lavado '{washType.Name}' escrito y verificado en {label} ({parametersWritten} variables)"
                        : $"Escritura con errores ({label}): {parametersWritten} OK, {errors.Count} errores",
                ParametersWritten = parametersWritten,
                Errors = errors.Count > 0 ? errors : null,
                WrittenAt = DateTime.UtcNow
            };
        }

        #endregion
    }
}
