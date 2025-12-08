// 📋 OPERATION LOGS CONTROLLER - Nivel 2 (Acciones de Operador)
// API para consultar logs de operaciones

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 📋 Controller para logs de operación (Nivel 2)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        /// 📋 Obtener logs de operación con filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<OperationLogResponse>> GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? category = null,
            [FromQuery] string? action = null,
            [FromQuery] string? user = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var query = new OperationLogQuery
                {
                    Page = page,
                    PageSize = Math.Min(pageSize, 200), // Limitar tamaño máximo
                    User = user,
                    StartDate = startDate,
                    EndDate = endDate,
                    Search = search
                };

                // Parsear categoría si se proporciona
                if (!string.IsNullOrEmpty(category) && 
                    Enum.TryParse<OperationCategory>(category, true, out var categoryEnum))
                {
                    query.Category = categoryEnum;
                }

                // Parsear acción si se proporciona
                if (!string.IsNullOrEmpty(action) && 
                    Enum.TryParse<OperationAction>(action, true, out var actionEnum))
                {
                    query.Action = actionEnum;
                }

                var result = await _operationLogService.GetLogsAsync(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operation logs");
                return StatusCode(500, new { error = "Error retrieving operation logs" });
            }
        }

        /// <summary>
        /// 📋 Obtener logs recientes
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<List<OperationLogEntry>>> GetRecentLogs([FromQuery] int count = 50)
        {
            try
            {
                var logs = await _operationLogService.GetRecentLogsAsync(Math.Min(count, 200));
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent operation logs");
                return StatusCode(500, new { error = "Error retrieving recent logs" });
            }
        }

        /// <summary>
        /// ❓ Obtener información de ayuda
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
        /// 📊 Obtener categorías disponibles
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
        /// 📊 Obtener acciones disponibles
        /// </summary>
        [HttpGet("actions")]
        [AllowAnonymous]
        public ActionResult<List<object>> GetActions([FromQuery] string? category = null)
        {
            IEnumerable<OperationAction> actions = Enum.GetValues<OperationAction>();

            // Filtrar por categoría si se proporciona
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
        /// 📝 Registrar una operación manualmente (para testing)
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

                await _operationLogService.LogAsync(
                    category, 
                    action, 
                    request.Description ?? "",
                    userName,
                    request.Details,
                    ipAddress
                );

                return Ok(new { success = true, message = "Operation logged successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging operation");
                return StatusCode(500, new { error = "Error logging operation" });
            }
        }

        #region Helpers

        private static string GetCategoryLabel(OperationCategory category) => category switch
        {
            OperationCategory.Navigation => "Navegación",
            OperationCategory.Alarm => "Alarmas",
            OperationCategory.Recipe => "Recetas",
            OperationCategory.Process => "Procesos",
            OperationCategory.Setpoint => "Setpoints",
            OperationCategory.Statistics => "Estadísticas",
            OperationCategory.Export => "Exportaciones",
            OperationCategory.Backup => "Backup",
            _ => category.ToString()
        };

        private static string GetCategoryIcon(OperationCategory category) => category switch
        {
            OperationCategory.Navigation => "🧭",
            OperationCategory.Alarm => "🚨",
            OperationCategory.Recipe => "📋",
            OperationCategory.Process => "⚙️",
            OperationCategory.Setpoint => "🎯",
            OperationCategory.Statistics => "📊",
            OperationCategory.Export => "📤",
            OperationCategory.Backup => "💾",
            _ => "📝"
        };

        private static string GetActionLabel(OperationAction action) => action switch
        {
            OperationAction.ViewChange => "Cambio de vista",
            OperationAction.AlarmAcknowledge => "Reconocer alarma",
            OperationAction.AlarmReset => "Reset alarma",
            OperationAction.RecipeLoad => "Cargar receta",
            OperationAction.RecipeExecute => "Ejecutar receta",
            OperationAction.SetpointChange => "Cambio setpoint",
            OperationAction.ProcessStart => "Iniciar proceso",
            OperationAction.ProcessStop => "Parar proceso",
            _ => action.ToString()
        };

        private static IEnumerable<OperationAction> FilterActionsByCategory(OperationCategory category) => category switch
        {
            OperationCategory.Navigation => new[] { 
                OperationAction.ViewChange, 
                OperationAction.MenuOpen, 
                OperationAction.MenuClose 
            },
            OperationCategory.Alarm => new[] { 
                OperationAction.AlarmView, 
                OperationAction.AlarmAcknowledge, 
                OperationAction.AlarmReset,
                OperationAction.AlarmSilence,
                OperationAction.AlarmExport
            },
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
            OperationCategory.Setpoint => new[] {
                OperationAction.SetpointView,
                OperationAction.SetpointChange,
                OperationAction.SetpointOverride,
                OperationAction.LimitChange
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
            _ => Enum.GetValues<OperationAction>()
        };

        #endregion
    }

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
}
