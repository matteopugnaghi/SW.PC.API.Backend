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
        private readonly ILogger<WashTypesController> _logger;

        public WashTypesController(
            AquafrischDbContext dbContext,
            ITwinCATService twinCATService,
            IExcelConfigService excelService,
            IOperationLogService operationLog,
            ILogger<WashTypesController> logger)
        {
            _dbContext = dbContext;
            _twinCATService = twinCATService;
            _excelService = excelService;
            _operationLog = operationLog;
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
                    $"Creado tipo de lavado: {washType.Name} ({washType.Code})",
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
                    $"Actualizado tipo de lavado: {washType.Name} ({washType.Code})",
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
        /// Eliminar tipo de lavado
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(204)]
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

                // Verificar si está activo
                var isActive = await _dbContext.ActiveWashTypes.AnyAsync(a => a.WashTypeId == id);
                if (isActive)
                {
                    return BadRequest(new { error = "No se puede eliminar un tipo de lavado que está actualmente seleccionado" });
                }

                _dbContext.WashTypes.Remove(washType);
                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.WashTypeDelete,
                    $"Eliminado tipo de lavado: {washType.Name} ({washType.Code})",
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
                    OperationAction.RecipeLoad,
                    $"Seleccionado tipo de lavado: {washType.Name} ({washType.Code})",
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

                    // Actualizar parámetros
                    foreach (var param in plcValues.Parameters)
                    {
                        var existingParam = existingWashType.Parameters
                            .FirstOrDefault(p => p.ParameterCode == param.Code);
                        if (existingParam != null)
                        {
                            existingParam.Value = param.Value;
                        }
                        else
                        {
                            existingWashType.Parameters.Add(new WashTypeParameter
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
                        Parameters = plcValues.Parameters.Select(p => new WashTypeParameter
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
                        }).ToList()
                    };

                    _dbContext.WashTypes.Add(existingWashType);
                }

                await _dbContext.SaveChangesAsync();

                // Operation log (L2) - Recetas
                await _operationLog.LogAsync(
                    OperationCategory.Recipe,
                    OperationAction.RecipeReadPlc,
                    $"Guardado tipo de lavado desde PLC: Slot {dto.SlotNumber} - {existingWashType.Name}",
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

        private async Task<WriteToPlcResponseDto> WriteWashTypeToPlcInternal(
            WashType washType, 
            ActiveWashType active, 
            string username)
        {
            var errors = new List<string>();
            int parametersWritten = 0;

            // Cargar configuración del Excel para obtener variable del nombre de receta
            var excelPath = _excelService.GetExcelConfigPath();
            var excelConfig = await _excelService.LoadWashRecipeConfigAsync(excelPath);

            // Escribir nombre de receta al PLC (variable de A3 del Excel)
            if (!string.IsNullOrEmpty(excelConfig.RecipeNamePlcVariable))
            {
                try
                {
                    await _twinCATService.WriteVariableAsync(
                        excelConfig.RecipeNamePlcVariable, 
                        washType.Name, 
                        typeof(string));
                    parametersWritten++;
                    _logger.LogInformation("✅ Nombre de receta escrito al PLC: {Var} = {Value}", 
                        excelConfig.RecipeNamePlcVariable, washType.Name);
                }
                catch (Exception ex)
                {
                    var error = $"Nombre de receta: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Error escribiendo nombre de receta: {Error}", ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No hay variable PLC configurada para nombre de receta en A3 del Excel");
            }

            // Escribir línea/número de receta al PLC (variable de A4 del Excel)
            if (!string.IsNullOrEmpty(excelConfig.RecipeLineNumberPlcVariable))
            {
                try
                {
                    // Usar DisplayOrder como número de línea de receta (o Id si DisplayOrder es 0)
                    var lineNumber = washType.DisplayOrder > 0 ? washType.DisplayOrder : washType.Id;
                    await _twinCATService.WriteVariableAsync(
                        excelConfig.RecipeLineNumberPlcVariable, 
                        lineNumber, 
                        typeof(int));
                    parametersWritten++;
                    _logger.LogInformation("✅ Línea de receta escrita al PLC: {Var} = {Value}", 
                        excelConfig.RecipeLineNumberPlcVariable, lineNumber);
                }
                catch (Exception ex)
                {
                    var error = $"Línea de receta: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Error escribiendo línea de receta: {Error}", ex.Message);
                }
            }
            else
            {
                _logger.LogDebug("ℹ️ No hay variable PLC configurada para línea de receta en A4 del Excel");
            }

            // Escribir parámetros individuales
            foreach (var param in washType.Parameters.Where(p => !string.IsNullOrEmpty(p.PlcVariable)))
            {
                try
                {
                    object? valueToWrite = param.DataType.ToUpper() switch
                    {
                        "BOOL" => bool.TryParse(param.Value, out var b) ? b : false,
                        "INT" => int.TryParse(param.Value, out var i) ? i : 0,
                        "LREAL" or "REAL" or "DOUBLE" => double.TryParse(param.Value, 
                            System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            out var d) ? d : 0.0,
                        _ => param.Value
                    };

                    Type dataType = param.DataType.ToUpper() switch
                    {
                        "BOOL" => typeof(bool),
                        "INT" => typeof(int),
                        "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                        _ => typeof(string)
                    };

                    await _twinCATService.WriteVariableAsync(param.PlcVariable!, valueToWrite!, dataType);
                    parametersWritten++;
                    _logger.LogDebug("✅ Written {Variable} = {Value}", param.PlcVariable, valueToWrite);
                }
                catch (Exception ex)
                {
                    var error = $"{param.Name}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Failed to write {Variable}: {Error}", param.PlcVariable, ex.Message);
                }
            }

            // Actualizar estado de escritura
            active.WrittenToPlc = errors.Count == 0;
            active.WrittenToPlcAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Operation log (L2) - Recetas
            var status = errors.Count == 0 ? "exitosa" : $"parcial ({errors.Count} errores)";
            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.WashTypeWritePlc,
                $"Escritura al PLC {status}: {washType.Name} - {parametersWritten} parámetros",
                username);

            return new WriteToPlcResponseDto
            {
                Success = errors.Count == 0,
                Message = errors.Count == 0 
                    ? $"Tipo de lavado '{washType.Name}' escrito al PLC correctamente" 
                    : $"Escritura parcial: {parametersWritten} parámetros escritos, {errors.Count} errores",
                ParametersWritten = parametersWritten,
                Errors = errors.Count > 0 ? errors : null,
                WrittenAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Escribir tipo de lavado al PLC usando prefijo alternativo (de A14)
        /// Reemplaza "st_WashRecipe" por el prefijo especificado en las variables PLC
        /// </summary>
        private async Task<WriteToPlcResponseDto> WriteWashTypeToPlcAlternateInternal(
            WashType washType, 
            string alternatePlcPrefix,
            string username)
        {
            var errors = new List<string>();
            int parametersWritten = 0;

            _logger.LogInformation("🚿 Escribiendo tipo de lavado al PLC con prefijo alternativo: {Prefix}", alternatePlcPrefix);

            // Cargar configuración del Excel para obtener variable del nombre de receta y todas las variables PLC
            var excelPath = _excelService.GetExcelConfigPath();
            var excelConfig = await _excelService.LoadWashRecipeConfigAsync(excelPath);

            // Escribir nombre de receta al PLC (modificando la variable con el prefijo alternativo)
            if (!string.IsNullOrEmpty(excelConfig.RecipeNamePlcVariable))
            {
                try
                {
                    // Reemplazar st_WashRecipe por el prefijo alternativo
                    var alternateVar = excelConfig.RecipeNamePlcVariable.Replace("st_WashRecipe", alternatePlcPrefix);
                    
                    var writeSuccess = await _twinCATService.WriteVariableAsync(
                        alternateVar, 
                        washType.Name, 
                        typeof(string));
                    
                    if (writeSuccess)
                    {
                        parametersWritten++;
                        _logger.LogInformation("✅ Nombre de receta escrito al PLC (alternativo): {Var} = {Value}", 
                            alternateVar, washType.Name);
                    }
                    else
                    {
                        var error = $"Nombre de receta (alternativo): Error al escribir {alternateVar}";
                        errors.Add(error);
                        _logger.LogWarning("⚠️ Error escribiendo nombre de receta (alternativo): WriteVariableAsync returned false");
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Nombre de receta (alternativo): {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Error escribiendo nombre de receta (alternativo): {Error}", ex.Message);
                }
            }

            // Escribir línea/número de receta al PLC alternativo (variable de A4 del Excel con prefijo alternativo)
            if (!string.IsNullOrEmpty(excelConfig.RecipeLineNumberPlcVariable))
            {
                try
                {
                    // Reemplazar st_WashRecipe por el prefijo alternativo
                    var alternateLineVar = excelConfig.RecipeLineNumberPlcVariable.Replace("st_WashRecipe", alternatePlcPrefix);
                    
                    // Usar DisplayOrder como número de línea de receta (o Id si DisplayOrder es 0)
                    var lineNumber = washType.DisplayOrder > 0 ? washType.DisplayOrder : washType.Id;
                    
                    var writeSuccess = await _twinCATService.WriteVariableAsync(
                        alternateLineVar, 
                        lineNumber, 
                        typeof(int));
                    
                    if (writeSuccess)
                    {
                        parametersWritten++;
                        _logger.LogInformation("✅ Línea de receta escrita al PLC (alternativo): {Var} = {Value}", 
                            alternateLineVar, lineNumber);
                    }
                    else
                    {
                        var error = $"Línea de receta (alternativo): Error al escribir {alternateLineVar}";
                        errors.Add(error);
                        _logger.LogWarning("⚠️ Error escribiendo línea de receta (alternativo): WriteVariableAsync returned false");
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Línea de receta (alternativo): {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Error escribiendo línea de receta (alternativo): {Error}", ex.Message);
                }
            }

            // Escribir parámetros usando las variables del Excel (mapeando por código de parámetro)
            // Obtener todas las variables PLC definidas en el Excel
            var allExcelParams = new List<(string PlcVariable, string DataType, string Code)>();
            
            foreach (var station in excelConfig.Stations)
            {
                foreach (var boolParam in station.BoolParameters.Where(p => p.IsConfigured && !string.IsNullOrEmpty(p.PlcVariable)))
                {
                    allExcelParams.Add((boolParam.PlcVariable, "BOOL", $"S{station.Index}_B{boolParam.Index}"));
                }
                foreach (var intParam in station.IntParameters.Where(p => p.IsConfigured && !string.IsNullOrEmpty(p.PlcVariable)))
                {
                    allExcelParams.Add((intParam.PlcVariable, "INT", $"S{station.Index}_I{intParam.Index}"));
                }
            }

            _logger.LogInformation("📋 Excel tiene {Count} parámetros PLC configurados, WashType tiene {WtCount} parámetros", 
                allExcelParams.Count, washType.Parameters.Count);
            
            // Log de códigos de Excel para debug
            foreach (var ep in allExcelParams.Take(5))
            {
                _logger.LogDebug("📋 Excel param: Code={Code}, PlcVar={Var}", ep.Code, ep.PlcVariable);
            }
            
            // Log de códigos de WashType para debug
            foreach (var wp in washType.Parameters.Take(5))
            {
                _logger.LogDebug("📋 WashType param: Code={Code}, PlcVar={Var}", wp.ParameterCode, wp.PlcVariable);
            }

            // Para cada parámetro del WashType, buscar la variable PLC en el Excel por código
            foreach (var param in washType.Parameters)
            {
                // Buscar en el Excel por código de parámetro
                var excelParam = allExcelParams.FirstOrDefault(e => e.Code == param.ParameterCode);
                
                string? plcVariable = null;
                string dataType = param.DataType;
                
                if (!string.IsNullOrEmpty(excelParam.PlcVariable))
                {
                    plcVariable = excelParam.PlcVariable;
                    dataType = excelParam.DataType;
                }
                else if (!string.IsNullOrEmpty(param.PlcVariable))
                {
                    // Fallback: usar PlcVariable de la BD si existe
                    plcVariable = param.PlcVariable;
                }
                
                if (string.IsNullOrEmpty(plcVariable))
                {
                    _logger.LogDebug("⏭️ Parámetro {Code} sin variable PLC, saltando", param.ParameterCode);
                    continue;
                }

                try
                {
                    // Reemplazar st_WashRecipe por el prefijo alternativo
                    var alternateVar = plcVariable.Replace("st_WashRecipe", alternatePlcPrefix);
                    
                    object? valueToWrite = dataType.ToUpper() switch
                    {
                        "BOOL" => bool.TryParse(param.Value, out var b) ? b : false,
                        "INT" => int.TryParse(param.Value, out var i) ? i : 0,
                        "LREAL" or "REAL" or "DOUBLE" => double.TryParse(param.Value, 
                            System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            out var d) ? d : 0.0,
                        _ => param.Value
                    };

                    Type netDataType = dataType.ToUpper() switch
                    {
                        "BOOL" => typeof(bool),
                        "INT" => typeof(int),
                        "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                        _ => typeof(string)
                    };

                    var writeSuccess = await _twinCATService.WriteVariableAsync(alternateVar, valueToWrite!, netDataType);
                    if (writeSuccess)
                    {
                        parametersWritten++;
                        _logger.LogDebug("✅ Written (alternate) {Variable} = {Value}", alternateVar, valueToWrite);
                    }
                    else
                    {
                        var error = $"{param.Name}: Error al escribir {alternateVar}";
                        errors.Add(error);
                        _logger.LogWarning("⚠️ Failed to write (alternate) {Variable}: WriteVariableAsync returned false", alternateVar);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"{param.Name}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("⚠️ Failed to write (alternate) {Variable}: {Error}", plcVariable, ex.Message);
                }
            }

            // Operation log (L2) - Recetas
            var status = errors.Count == 0 && parametersWritten > 0 
                ? "exitosa" 
                : parametersWritten == 0 
                    ? "sin parámetros escritos" 
                    : $"parcial ({errors.Count} errores)";
            await _operationLog.LogAsync(
                OperationCategory.Recipe,
                OperationAction.WashTypeWritePlc,
                $"Escritura al PLC (alternativo [{alternatePlcPrefix}]) {status}: {washType.Name} - {parametersWritten} parámetros",
                username);

            return new WriteToPlcResponseDto
            {
                Success = parametersWritten > 0 && errors.Count == 0,
                Message = parametersWritten == 0
                    ? $"No se encontraron parámetros para escribir. Verifica que el WashType tenga parámetros configurados."
                    : errors.Count == 0 
                        ? $"Tipo de lavado '{washType.Name}' escrito al PLC (alternativo) correctamente ({parametersWritten} parámetros)" 
                        : $"Escritura parcial (alternativo): {parametersWritten} parámetros escritos, {errors.Count} errores",
                ParametersWritten = parametersWritten,
                Errors = errors.Count > 0 ? errors : null,
                WrittenAt = DateTime.UtcNow
            };
        }

        #endregion
    }
}
