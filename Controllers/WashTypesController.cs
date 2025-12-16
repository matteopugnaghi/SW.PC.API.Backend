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
        private readonly IAuditLogService _auditLog;
        private readonly ILogger<WashTypesController> _logger;

        public WashTypesController(
            AquafrischDbContext dbContext,
            ITwinCATService twinCATService,
            IAuditLogService auditLog,
            ILogger<WashTypesController> logger)
        {
            _dbContext = dbContext;
            _twinCATService = twinCATService;
            _auditLog = auditLog;
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

                // Audit log
                await _auditLog.LogAsync(
                    AuditCategory.Recipe,
                    AuditAction.ConfigChange,
                    AuditResult.Success,
                    $"Creado tipo de lavado: {washType.Name} ({washType.Code})",
                    null, username);

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

                // Audit log
                await _auditLog.LogAsync(
                    AuditCategory.Recipe,
                    AuditAction.ConfigChange,
                    AuditResult.Success,
                    $"Actualizado tipo de lavado: {washType.Name} ({washType.Code})",
                    null, username);

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

                // Audit log
                await _auditLog.LogAsync(
                    AuditCategory.Recipe,
                    AuditAction.ConfigChange,
                    AuditResult.Success,
                    $"Eliminado tipo de lavado: {washType.Name} ({washType.Code})",
                    null, username);

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

                // Audit log
                await _auditLog.LogAsync(
                    AuditCategory.Recipe,
                    AuditAction.ConfigChange,
                    AuditResult.Success,
                    $"Seleccionado tipo de lavado: {washType.Name} ({washType.Code})",
                    null, username);

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

            // Audit log
            var status = errors.Count == 0 ? "exitosa" : $"parcial ({errors.Count} errores)";
            await _auditLog.LogAsync(
                AuditCategory.Plc,
                AuditAction.ConfigChange,
                errors.Count == 0 ? AuditResult.Success : AuditResult.Warning,
                $"Escritura al PLC {status}: {washType.Name} - {parametersWritten} parámetros",
                null, username);

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

        #endregion
    }
}
