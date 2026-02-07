// ============================================================================
// OperationLogsController.cs - API de Registro de Operaciones
// ============================================================================
// Endpoints para consultar y gestionar logs de operación (SQLite)
// - Historial de alarmas PLC (st_alarmHistPc)
// - Acciones de operador
// - Eventos del sistema
// EU CRA Compliance: Trazabilidad de operaciones (Nivel 2)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// Controller para logs de operación (Nivel 2 - SQLite)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OperationLogsController : ControllerBase
{
    private readonly IOperationLogService _operationLogService;
    private readonly ILogger<OperationLogsController> _logger;

    public OperationLogsController(
        IOperationLogService operationLogService,
        ILogger<OperationLogsController> logger)
    {
        _operationLogService = operationLogService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener logs de operación con filtros y paginación
    /// </summary>
    /// <param name="page">Número de página (1-based)</param>
    /// <param name="pageSize">Registros por página (max 2000)</param>
    /// <param name="category">Filtrar por categoría</param>
    /// <param name="action">Filtrar por acción</param>
    /// <param name="severity">Severidad mínima</param>
    /// <param name="user">Filtrar por usuario</param>
    /// <param name="fromDate">Fecha inicio (UTC)</param>
    /// <param name="toDate">Fecha fin (UTC)</param>
    /// <param name="search">Buscar en mensajes</param>
    /// <param name="onlyPlcAlarms">Solo alarmas PLC</param>
    /// <param name="onlyUnacknowledged">Solo no reconocidos</param>
    /// <param name="lang">Idioma (SPA, ENG)</param>
    [HttpGet]
    public async Task<ActionResult<OperationLogPagedResponse>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? user = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? onlyPlcAlarms = null,
        [FromQuery] bool? onlyUnacknowledged = null,
        [FromQuery] string lang = "SPA")
    {
        try
        {
            var filter = new OperationLogFilter
            {
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, 2000),
                User = user,
                FromDate = fromDate,
                ToDate = toDate,
                SearchText = search,
                OnlyPlcAlarms = onlyPlcAlarms,
                OnlyUnacknowledged = onlyUnacknowledged,
                Language = lang?.ToUpperInvariant() ?? "SPA"
            };

            // Parsear categoría
            if (!string.IsNullOrEmpty(category) && 
                Enum.TryParse<OperationCategory>(category, true, out var categoryEnum))
            {
                filter.Category = categoryEnum;
            }

            // Parsear acción
            if (!string.IsNullOrEmpty(action) && 
                Enum.TryParse<OperationAction>(action, true, out var actionEnum))
            {
                filter.Action = actionEnum;
            }

            // Parsear severidad
            if (!string.IsNullOrEmpty(severity) && 
                Enum.TryParse<OperationSeverity>(severity, true, out var severityEnum))
            {
                filter.MinSeverity = severityEnum;
            }

            var result = await _operationLogService.GetLogsAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operation logs");
            return StatusCode(500, new { error = "Error retrieving operation logs" });
        }
    }

    /// <summary>
    /// Obtener logs recientes
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<OperationLogDto>>> GetRecentLogs(
        [FromQuery] int count = 50,
        [FromQuery] string lang = "SPA")
    {
        try
        {
            var logs = await _operationLogService.GetRecentLogsAsync(
                Math.Clamp(count, 1, 2000), 
                lang?.ToUpperInvariant() ?? "SPA");
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent operation logs");
            return StatusCode(500, new { error = "Error retrieving recent logs" });
        }
    }

    /// <summary>
    /// Obtener resumen de logs para dashboard
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<OperationLogSummary>> GetSummary([FromQuery] string lang = "SPA")
    {
        try
        {
            var summary = await _operationLogService.GetSummaryAsync(lang?.ToUpperInvariant() ?? "SPA");
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operation log summary");
            return StatusCode(500, new { error = "Error retrieving summary" });
        }
    }

    /// <summary>
    /// Reconocer un log
    /// </summary>
    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult> AcknowledgeLog(int id)
    {
        try
        {
            var userName = User.Identity?.Name ?? "unknown";
            var success = await _operationLogService.AcknowledgeLogAsync(id, userName);
            
            if (!success)
                return NotFound(new { error = $"Log {id} not found" });
            
            return Ok(new { success = true, message = $"Log {id} acknowledged by {userName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging log {Id}", id);
            return StatusCode(500, new { error = "Error acknowledging log" });
        }
    }

    /// <summary>
    /// Reconocer múltiples logs
    /// </summary>
    [HttpPost("acknowledge-batch")]
    public async Task<ActionResult> AcknowledgeLogs([FromBody] AcknowledgeBatchRequest request)
    {
        try
        {
            if (request.Ids == null || !request.Ids.Any())
                return BadRequest(new { error = "No log IDs provided" });
            
            var userName = User.Identity?.Name ?? "unknown";
            var count = await _operationLogService.AcknowledgeLogsAsync(request.Ids, userName);
            
            return Ok(new { 
                success = true, 
                message = $"{count} logs acknowledged by {userName}",
                acknowledgedCount = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging logs batch");
            return StatusCode(500, new { error = "Error acknowledging logs" });
        }
    }

    /// <summary>
    /// Eliminar todos los logs (solo Admin y SuperAdmin)
    /// </summary>
    [HttpDelete("clear-all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> ClearAllLogs()
    {
        try
        {
            var userName = User.Identity?.Name ?? "unknown";
            var deleted = await _operationLogService.CleanupOldLogsAsync(retentionDays: 0);
            
            _logger.LogWarning("🗑️ ADMIN ACTION: Todos los operation logs eliminados ({Count}) por {User}", deleted, userName);
            
            return Ok(new { 
                success = true, 
                message = $"Eliminados {deleted} registros de operation logs",
                deletedCount = deleted,
                deletedBy = userName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all logs");
            return StatusCode(500, new { error = "Error clearing logs" });
        }
    }

    /// <summary>
    /// Obtener información de ayuda
    /// </summary>
    [HttpGet("help")]
    [AllowAnonymous]
    public async Task<ActionResult<OperationLogHelp>> GetHelp([FromQuery] string lang = "es")
    {
        try
        {
            var help = await _operationLogService.GetHelpAsync(lang);
            return Ok(help);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting help info");
            return StatusCode(500, new { error = "Error retrieving help information" });
        }
    }

    /// <summary>
    /// Obtener categorías disponibles
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    public ActionResult<List<object>> GetCategories()
    {
        var categories = Enum.GetValues<OperationCategory>()
            .Select(c => new { 
                value = c.ToString(), 
                label = GetCategoryLabel(c),
                icon = GetCategoryIcon(c)
            })
            .ToList();
        
        return Ok(categories);
    }

    /// <summary>
    /// Obtener severidades disponibles
    /// </summary>
    [HttpGet("severities")]
    [AllowAnonymous]
    public ActionResult<List<object>> GetSeverities()
    {
        var severities = Enum.GetValues<OperationSeverity>()
            .Select(s => new { 
                value = s.ToString(),
                level = (int)s,
                label = GetSeverityLabel(s),
                color = GetSeverityColor(s)
            })
            .ToList();
        
        return Ok(severities);
    }

    /// <summary>
    /// Obtener acciones disponibles
    /// </summary>
    [HttpGet("actions")]
    [AllowAnonymous]
    public ActionResult<List<object>> GetActions([FromQuery] string? category = null)
    {
        IEnumerable<OperationAction> actions = Enum.GetValues<OperationAction>();

        if (!string.IsNullOrEmpty(category) && 
            Enum.TryParse<OperationCategory>(category, true, out var categoryEnum))
        {
            actions = FilterActionsByCategory(categoryEnum);
        }

        var result = actions.Select(a => new { 
            value = a.ToString(), 
            label = GetActionLabel(a)
        }).ToList();
        
        return Ok(result);
    }

    /// <summary>
    /// Registrar una operación manualmente (para testing/admin)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrator,SuperAdmin")]
    public async Task<ActionResult> LogOperation([FromBody] OperationLogRequest request)
    {
        try
        {
            if (!Enum.TryParse<OperationCategory>(request.Category, true, out var category))
            {
                return BadRequest(new { error = "Invalid category" });
            }

            if (!Enum.TryParse<OperationAction>(request.Action, true, out var action))
            {
                return BadRequest(new { error = "Invalid action" });
            }

            var userName = User.Identity?.Name ?? "unknown";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var entry = await _operationLogService.LogAsync(
                category, 
                action, 
                request.Description ?? "",
                userName,
                request.Details,
                ipAddress
            );

            return Ok(new { 
                success = true, 
                message = "Operation logged successfully",
                id = entry.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging operation");
            return StatusCode(500, new { error = "Error logging operation" });
        }
    }

    /// <summary>
    /// Limpiar logs antiguos (solo admin)
    /// </summary>
    [HttpDelete("cleanup")]
    [Authorize(Roles = "Administrator,SuperAdmin")]
    public async Task<ActionResult> CleanupOldLogs([FromQuery] int retentionDays = 365)
    {
        try
        {
            var deletedCount = await _operationLogService.CleanupOldLogsAsync(retentionDays);
            return Ok(new { 
                success = true, 
                message = $"Deleted {deletedCount} old logs",
                deletedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old logs");
            return StatusCode(500, new { error = "Error cleaning up logs" });
        }
    }

    #region Helpers

    private static string GetCategoryLabel(OperationCategory category) => category switch
    {
        OperationCategory.Navigation => "Navegación",
        OperationCategory.Alarm => "Alarmas Usuario",
        OperationCategory.Recipe => "Recetas",
        OperationCategory.Process => "Procesos",
        OperationCategory.Statistics => "Estadísticas",
        OperationCategory.Export => "Exportaciones",
        OperationCategory.Backup => "Backup",
        OperationCategory.PlcAlarmHistory => "Historial Alarmas PLC",
        OperationCategory.System => "Sistema",
        _ => category.ToString()
    };

    private static string GetCategoryIcon(OperationCategory category) => category switch
    {
        OperationCategory.Navigation => "🧭",
        OperationCategory.Alarm => "🚨",
        OperationCategory.Recipe => "📋",
        OperationCategory.Process => "⚙️",
        OperationCategory.Statistics => "📊",
        OperationCategory.Export => "📤",
        OperationCategory.Backup => "💾",
        OperationCategory.PlcAlarmHistory => "🔔",
        OperationCategory.System => "🖥️",
        _ => "📝"
    };

    private static string GetSeverityLabel(OperationSeverity severity) => severity switch
    {
        OperationSeverity.Info => "Información",
        OperationSeverity.Notice => "Notificación",
        OperationSeverity.Warning => "Advertencia",
        OperationSeverity.Error => "Error",
        OperationSeverity.Critical => "Crítico",
        _ => severity.ToString()
    };

    private static string GetSeverityColor(OperationSeverity severity) => severity switch
    {
        OperationSeverity.Info => "#2196F3",      // Azul
        OperationSeverity.Notice => "#8BC34A",    // Verde
        OperationSeverity.Warning => "#FFC107",   // Amarillo
        OperationSeverity.Error => "#FF5722",     // Naranja
        OperationSeverity.Critical => "#F44336",  // Rojo
        _ => "#9E9E9E"                            // Gris
    };

    private static string GetActionLabel(OperationAction action) => action switch
    {
        OperationAction.ViewChange => "Cambio de vista",
        OperationAction.RecipeLoad => "Cargar receta",
        OperationAction.RecipeExecute => "Ejecutar receta",
        OperationAction.ProcessStart => "Iniciar proceso",
        OperationAction.ProcessStop => "Parar proceso",
        OperationAction.PlcAlarmActivated => "Alarma PLC activada",
        OperationAction.PlcAlarmDeactivated => "Alarma PLC desactivada",
        OperationAction.PlcNotificationActivated => "Notificación PLC activada",
        OperationAction.PlcNotificationDeactivated => "Notificación PLC desactivada",
        OperationAction.PlcInfoActivated => "Info PLC activada",
        OperationAction.PlcInfoDeactivated => "Info PLC desactivada",
        _ => action.ToString()
    };

    private static IEnumerable<OperationAction> FilterActionsByCategory(OperationCategory category) => category switch
    {
        OperationCategory.Navigation => new[] { 
            OperationAction.ViewChange, 
            OperationAction.MenuOpen, 
            OperationAction.MenuClose 
        },
        OperationCategory.Alarm => Array.Empty<OperationAction>(), // No longer used
        OperationCategory.Recipe => new[] {
            OperationAction.RecipeView,
            OperationAction.RecipeCreate,
            OperationAction.RecipeEdit,
            OperationAction.RecipeDelete,
            OperationAction.RecipeLoad,
            OperationAction.RecipeExecute,
            OperationAction.RecipePause,
            OperationAction.RecipeResume,
            OperationAction.RecipeAbort,
            OperationAction.RecipeExport,
            OperationAction.RecipeImport
        },
        OperationCategory.Process => new[] {
            OperationAction.ProcessStart,
            OperationAction.ProcessStop,
            OperationAction.ProcessPause,
            OperationAction.ProcessResume,
            OperationAction.ProcessModeChange,
            OperationAction.CommandExecute
        },
        OperationCategory.Statistics => new[] {
            OperationAction.StatisticsView,
            OperationAction.StatisticsExport,
            OperationAction.ReportGenerate,
            OperationAction.ReportExport
        },
        OperationCategory.Export => new[] { 
            OperationAction.DataExport 
        },
        OperationCategory.Backup => new[] {
            OperationAction.BackupCreate,
            OperationAction.BackupRestore,
            OperationAction.BackupDelete
        },
        OperationCategory.PlcAlarmHistory => new[] {
            OperationAction.PlcAlarmActivated,
            OperationAction.PlcAlarmDeactivated,
            OperationAction.PlcNotificationActivated,
            OperationAction.PlcNotificationDeactivated,
            OperationAction.PlcInfoActivated,
            OperationAction.PlcInfoDeactivated
        },
        OperationCategory.System => new[] {
            OperationAction.ConfigChange
        },
        _ => Enum.GetValues<OperationAction>()
    };

    /// <summary>
    /// [DEV ONLY] Crear datos de prueba
    /// </summary>
    [HttpPost("seed-test-data")]
    [AllowAnonymous]
    public async Task<ActionResult> SeedTestData([FromQuery] int count = 20)
    {
        try
        {
            // Solo permitir en desarrollo
            var env = HttpContext.RequestServices.GetService<IWebHostEnvironment>();
            if (env == null || !env.IsDevelopment())
            {
                return NotFound(); // Esconder en producción
            }

            // Obtener DbContext directamente
            var dbContext = HttpContext.RequestServices.GetRequiredService<AquafrischDbContext>();
            
            // Crear tabla si no existe (nueva estructura con ActionKey)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS OperationLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Category INTEGER NOT NULL,
                    Action INTEGER NOT NULL,
                    Severity INTEGER NOT NULL DEFAULT 0,
                    User TEXT NOT NULL DEFAULT 'System',
                    Description TEXT NOT NULL DEFAULT '',
                    PlcVariable TEXT,
                    AlarmIndex INTEGER,
                    AlarmCode TEXT,
                    AlarmType TEXT,
                    ActionKey TEXT,
                    OldValue TEXT,
                    NewValue TEXT,
                    IpAddress TEXT,
                    SessionId TEXT,
                    DetailsJson TEXT,
                    IsAcknowledged INTEGER NOT NULL DEFAULT 0,
                    AcknowledgedBy TEXT,
                    AcknowledgedAt TEXT
                )");
            
            var severities = Enum.GetValues<OperationSeverity>();
            var categories = new[] { 
                OperationCategory.Process, 
                OperationCategory.PlcAlarmHistory, 
                OperationCategory.Recipe,
                OperationCategory.Navigation
            };
            var actions = new[] {
                OperationAction.PlcAlarmActivated,
                OperationAction.PlcAlarmDeactivated,
                OperationAction.ProcessStart,
                OperationAction.ProcessStop,
                OperationAction.RecipeLoad,
                OperationAction.ViewChange
            };
            var users = new[] { "operator", "admin", "technician", "supervisor", "PLC" };
            var random = new Random();
            var logs = new List<OperationLog>();

            for (int i = 0; i < count; i++)
            {
                var category = categories[random.Next(categories.Length)];
                var action = actions[random.Next(actions.Length)];
                var severity = severities[random.Next(severities.Length)];
                var user = users[random.Next(users.Length)];
                
                string? alarmCode = null;
                string? plcVariable = null;
                int? alarmIndex = null;
                string? alarmType = null;
                string? actionKey = null;
                
                if (category == OperationCategory.PlcAlarmHistory)
                {
                    alarmIndex = random.Next(1, 18); // Índices válidos del Excel (1-17)
                    alarmType = random.Next(3) switch { 0 => "Alarm", 1 => "Notification", _ => "Info" };
                    plcVariable = $"MAIN.fbMachine.st_alarmHistPc[{alarmIndex}].{alarmType}";
                    alarmCode = $"{alarmIndex}"; // Código de alarma = índice
                    severity = alarmType == "Alarm" ? OperationSeverity.Error 
                             : alarmType == "Notification" ? OperationSeverity.Warning 
                             : OperationSeverity.Info;
                }
                else
                {
                    // Para otras categorías, generar ActionKey para i18n
                    actionKey = $"operationLogs.actions.{category.ToString().ToLowerInvariant()}.{action.ToString().ToLowerInvariant()}";
                }

                var log = new OperationLog
                {
                    Timestamp = DateTime.Now.AddHours(-random.Next(0, 72)).AddMinutes(-random.Next(0, 60)),
                    Category = category,
                    Action = action,
                    Severity = severity,
                    User = user,
                    Description = category == OperationCategory.PlcAlarmHistory
                        ? $"Alarma #{alarmIndex}"
                        : $"{category}.{action}",
                    PlcVariable = plcVariable,
                    AlarmCode = alarmCode,
                    AlarmIndex = alarmIndex,
                    AlarmType = alarmType,
                    ActionKey = actionKey,
                    IsAcknowledged = random.NextDouble() > 0.7
                };
                
                if (log.IsAcknowledged)
                {
                    log.AcknowledgedBy = users[random.Next(users.Length)];
                    log.AcknowledgedAt = log.Timestamp.AddMinutes(random.Next(1, 60));
                }
                
                logs.Add(log);
            }

            dbContext.OperationLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created {Count} test operation logs", logs.Count);
            return Ok(new { success = true, message = $"Created {logs.Count} test logs", count = logs.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding test data: {Message}", ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { 
                error = "Error creating test data", 
                details = ex.Message,
                inner = ex.InnerException?.Message 
            });
        }
    }

    #endregion
}

#region Request DTOs

/// <summary>
/// Request para registrar operación manualmente
/// </summary>
public class OperationLogRequest
{
    public string Category { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Description { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Request para reconocer múltiples logs
/// </summary>
public class AcknowledgeBatchRequest
{
    public List<int> Ids { get; set; } = new();
}

#endregion