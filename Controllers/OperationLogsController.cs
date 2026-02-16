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
using ClosedXML.Excel;
using System.Text.Json;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// Controller para logs de operación (Nivel 2 - SQLite)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OperationLogsController : ControllerBase
{
    private readonly IOperationLogService _operationLogService;
    private readonly IAuditLogService _auditLogService;
    private readonly IProjectContextService _projectContext;
    private readonly ILogger<OperationLogsController> _logger;

    public OperationLogsController(
        IOperationLogService operationLogService,
        IAuditLogService auditLogService,
        IProjectContextService projectContext,
        ILogger<OperationLogsController> logger)
    {
        _operationLogService = operationLogService;
        _auditLogService = auditLogService;
        _projectContext = projectContext;
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
    /// Exportar logs de operación a Excel o JSON
    /// </summary>
    /// <param name="format">Formato: excel o json</param>
    /// <param name="categories">Categorías a exportar (PlcAlarm,PlcNotification,PlcInfo,Recipe,Configuration,etc) o "all"</param>
    /// <param name="startDate">Fecha inicio</param>
    /// <param name="endDate">Fecha fin</param>
    /// <param name="lang">Idioma para traducciones (SPA, ENG, FRA, ITA)</param>
    [HttpGet("export")]
    [Authorize]
    public async Task<IActionResult> ExportLogs(
        [FromQuery] string format = "excel",
        [FromQuery] string categories = "all",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string lang = "SPA")
    {
        try
        {
            var userName = User.Identity?.Name ?? "unknown";
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            // Parse categories (normalize to lowercase for comparison)
            var categoryList = categories?.ToLower() == "all" 
                ? new List<string>() 
                : categories?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim().ToLower())
                    .ToList() ?? new List<string>();
            
            // Default date range: last week
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-7);
            
            // Get logs with filters
            var filter = new OperationLogFilter
            {
                Page = 1,
                PageSize = 10000, // Max export
                FromDate = start,
                ToDate = end,
                Language = lang?.ToUpperInvariant() ?? "SPA"
            };
            
            var result = await _operationLogService.GetLogsAsync(filter);
            var logs = result.Items ?? new List<OperationLogDto>();
            
            // Filter by categories if specified
            if (categoryList.Any())
            {
                logs = logs.Where(l => 
                {
                    // Map category + alarmType to filter categories
                    var catKey = l.Category?.ToLower() ?? "";
                    if (catKey == "plcalarmhistory")
                    {
                        var type = l.AlarmType?.ToLower() ?? "";
                        if (type == "alarm" && categoryList.Contains("plcalarm")) return true;
                        if (type == "notification" && categoryList.Contains("plcnotification")) return true;
                        if (type == "info" && categoryList.Contains("plcinfo")) return true;
                        return false;
                    }
                    return categoryList.Contains(catKey);
                }).ToList();
            }
            
            // Load translations
            var translations = await LoadTranslationsAsync(lang?.ToUpperInvariant() ?? "SPA");
            
            // Determine audit action based on categories
            var alarmOnlyCategories = new HashSet<string> { "plcalarm" };
            var isAlarmHistoryOnly = categoryList.Any() && categoryList.All(c => alarmOnlyCategories.Contains(c));
            var auditAction = isAlarmHistoryOnly ? AuditAction.AlarmHistoryExport : AuditAction.OperationLogExport;
            
            // Log export to L1 Audit
            var exportDetails = JsonSerializer.Serialize(new
            {
                Format = format,
                Categories = categoryList.Any() ? string.Join(",", categoryList) : "all",
                StartDate = start.ToString("yyyy-MM-dd"),
                EndDate = end.ToString("yyyy-MM-dd"),
                RecordCount = logs.Count,
                Language = lang
            });
            
            await _auditLogService.LogAsync(
                AuditCategory.Export,
                auditAction,
                AuditResult.Success,
                exportDetails,
                userId,
                userName,
                ipAddress,
                logs.Count);
            
            _logger.LogInformation("📤 Export operation logs: {Count} records, format={Format}, categories={Categories}, by {User}", 
                logs.Count, format, categories, userName);
            
            if (format?.ToLower() == "json")
            {
                return ExportAsJson(logs, translations, start, end, lang ?? "SPA");
            }
            else
            {
                return await ExportAsExcelAsync(logs, translations, start, end, lang ?? "SPA");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting operation logs");
            
            // Log failure to L1 Audit
            await _auditLogService.LogAsync(
                AuditCategory.Export,
                AuditAction.OperationLogExport,
                AuditResult.Failure,
                $"Error: {ex.Message}",
                User.FindFirst("sub")?.Value,
                User.Identity?.Name ?? "unknown");
            
            return StatusCode(500, new { error = "Error exporting operation logs", details = ex.Message });
        }
    }

    /// <summary>
    /// Export logs as JSON file
    /// </summary>
    private IActionResult ExportAsJson(List<OperationLogDto> logs, Dictionary<string, string> translations, DateTime start, DateTime end, string lang)
    {
        var exportData = new
        {
            metadata = new
            {
                exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                dateRange = new { from = start.ToString("yyyy-MM-dd"), to = end.ToString("yyyy-MM-dd") },
                recordCount = logs.Count,
                language = lang,
                version = "1.0"
            },
            records = logs.Select(l => new
            {
                l.Id,
                timestamp = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                category = TranslateCategory(l.Category, l.AlarmType, translations),
                categoryKey = l.Category,
                action = TranslateAction(l.Action, translations),
                actionKey = l.Action,
                alarmType = l.AlarmType,
                alarmIndex = l.AlarmIndex,
                user = l.User,
                description = l.Message ?? l.Description,
                severity = l.Severity,
                acknowledged = l.IsAcknowledged,
                acknowledgedBy = l.AcknowledgedBy,
                acknowledgedAt = l.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList()
        };
        
        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileName = $"operation_logs_{start:yyyyMMdd}_{end:yyyyMMdd}_{lang}.json";
        
        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// Export logs as Excel file
    /// </summary>
    private async Task<IActionResult> ExportAsExcelAsync(List<OperationLogDto> logs, Dictionary<string, string> translations, DateTime start, DateTime end, string lang)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(GetTranslation(translations, "export.sheet.operationLogs", "Operation Logs"));
        
        // Header row with translations
        var headers = new[]
        {
            GetTranslation(translations, "export.column.dateTime", "Date/Time"),
            GetTranslation(translations, "export.column.category", "Category"),
            GetTranslation(translations, "export.column.action", "Action"),
            GetTranslation(translations, "export.column.user", "User"),
            GetTranslation(translations, "export.column.description", "Description"),
            GetTranslation(translations, "export.column.alarmIndex", "Alarm Index"),
            GetTranslation(translations, "export.column.severity", "Severity"),
            GetTranslation(translations, "export.column.acknowledged", "Acknowledged"),
            GetTranslation(translations, "export.column.acknowledgedBy", "Acknowledged By"),
            GetTranslation(translations, "export.column.acknowledgedAt", "Acknowledged At")
        };
        
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(23, 162, 184);
            cell.Style.Font.FontColor = XLColor.White;
        }
        
        // Data rows
        int row = 2;
        foreach (var log in logs)
        {
            worksheet.Cell(row, 1).Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 2).Value = TranslateCategory(log.Category, log.AlarmType, translations);
            worksheet.Cell(row, 3).Value = TranslateAction(log.Action, translations);
            worksheet.Cell(row, 4).Value = log.User;
            worksheet.Cell(row, 5).Value = log.Message ?? log.Description;
            worksheet.Cell(row, 6).Value = log.AlarmIndex;
            worksheet.Cell(row, 7).Value = log.Severity;
            worksheet.Cell(row, 8).Value = log.IsAcknowledged ? GetTranslation(translations, "export.yes", "Yes") : GetTranslation(translations, "export.no", "No");
            worksheet.Cell(row, 9).Value = log.AcknowledgedBy;
            worksheet.Cell(row, 10).Value = log.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Color code by category
            var color = GetCategoryColor(log.Category, log.AlarmType);
            worksheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromColor(color);
            
            row++;
        }
        
        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
        
        // Add metadata sheet
        var metaSheet = workbook.Worksheets.Add(GetTranslation(translations, "export.sheet.metadata", "Metadata"));
        metaSheet.Cell(1, 1).Value = GetTranslation(translations, "export.metadata.exportDate", "Export Date");
        metaSheet.Cell(1, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        metaSheet.Cell(2, 1).Value = GetTranslation(translations, "export.metadata.dateRange", "Date Range");
        metaSheet.Cell(2, 2).Value = $"{start:yyyy-MM-dd} - {end:yyyy-MM-dd}";
        metaSheet.Cell(3, 1).Value = GetTranslation(translations, "export.metadata.recordCount", "Record Count");
        metaSheet.Cell(3, 2).Value = logs.Count;
        metaSheet.Cell(4, 1).Value = GetTranslation(translations, "export.metadata.language", "Language");
        metaSheet.Cell(4, 2).Value = lang;
        metaSheet.Columns().AdjustToContents();
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var bytes = ms.ToArray();
        var fileName = $"operation_logs_{start:yyyyMMdd}_{end:yyyyMMdd}_{lang}.xlsx";
        
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Load translations from project translations.json
    /// </summary>
    private async Task<Dictionary<string, string>> LoadTranslationsAsync(string lang)
    {
        var translations = new Dictionary<string, string>();
        
        try
        {
            // Translations are at Projects/{projectId}/translations/translations.json
            var translationsFile = Path.Combine(_projectContext.ProjectBasePath, "translations", "translations.json");
            
            if (System.IO.File.Exists(translationsFile))
            {
                var json = await System.IO.File.ReadAllTextAsync(translationsFile);
                using var doc = JsonDocument.Parse(json);
                
                // Navigate to the labels and extract translations for the specified language
                if (doc.RootElement.TryGetProperty("labels", out var labels) ||
                    doc.RootElement.EnumerateObject().Any())
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object && 
                            prop.Value.TryGetProperty(lang, out var translation))
                        {
                            translations[prop.Name] = translation.GetString() ?? prop.Name;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load translations, using defaults");
        }
        
        return translations;
    }

    /// <summary>
    /// Get translation with fallback
    /// </summary>
    private string GetTranslation(Dictionary<string, string> translations, string key, string fallback)
    {
        return translations.TryGetValue(key, out var value) ? value : fallback;
    }

    /// <summary>
    /// Translate category name
    /// </summary>
    private string TranslateCategory(string? category, string? alarmType, Dictionary<string, string> translations)
    {
        if (category?.ToLower() == "plcalarmhistory")
        {
            var type = alarmType?.ToLower() ?? "";
            if (type == "alarm") return GetTranslation(translations, "operationLogs.category.plcAlarm", "PLC Alarm");
            if (type == "notification") return GetTranslation(translations, "operationLogs.category.plcNotification", "PLC Notification");
            if (type == "info") return GetTranslation(translations, "operationLogs.category.plcInfo", "PLC Info");
            return "PLC";
        }
        
        var key = $"operationLogs.category.{category?.ToLower() ?? "unknown"}";
        return GetTranslation(translations, key, category ?? "Unknown");
    }

    /// <summary>
    /// Translate action name
    /// </summary>
    private string TranslateAction(string? action, Dictionary<string, string> translations)
    {
        var key = $"operationLogs.action.{action ?? "Unknown"}";
        return GetTranslation(translations, key, action ?? "Unknown");
    }

    /// <summary>
    /// Get color for category (for Excel)
    /// </summary>
    private System.Drawing.Color GetCategoryColor(string? category, string? alarmType)
    {
        if (category?.ToLower() == "plcalarmhistory")
        {
            var type = alarmType?.ToLower() ?? "";
            if (type == "alarm") return System.Drawing.Color.FromArgb(220, 53, 69); // Red
            if (type == "notification") return System.Drawing.Color.FromArgb(255, 193, 7); // Yellow
            if (type == "info") return System.Drawing.Color.FromArgb(23, 162, 184); // Cyan
        }
        
        return category?.ToLower() switch
        {
            "recipe" => System.Drawing.Color.FromArgb(32, 201, 151),
            "process" => System.Drawing.Color.FromArgb(40, 167, 69),
            "configuration" => System.Drawing.Color.FromArgb(156, 39, 176),
            "plccommand" => System.Drawing.Color.FromArgb(233, 30, 99),
            "statistics" => System.Drawing.Color.FromArgb(111, 66, 193),
            _ => System.Drawing.Color.FromArgb(108, 117, 125)
        };
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
        OperationCategory.Recipe => "Recetas",
        OperationCategory.Process => "Procesos",
        OperationCategory.Statistics => "Estadísticas",
        OperationCategory.Configuration => "Configuración",
        OperationCategory.PlcAlarmHistory => "Historial Alarmas PLC",
        _ => category.ToString()
    };

    private static string GetCategoryIcon(OperationCategory category) => category switch
    {
        OperationCategory.Recipe => "📋",
        OperationCategory.Process => "⚙️",
        OperationCategory.Statistics => "📊",
        OperationCategory.Configuration => "🔧",
        OperationCategory.PlcAlarmHistory => "🔔",
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
        OperationCategory.Recipe => new[] {
            OperationAction.WashTypeCreate,
            OperationAction.WashTypeEdit,
            OperationAction.WashTypeDelete,
            OperationAction.WashTypeWritePlc,
            OperationAction.WashTypeLoad,
            OperationAction.WashTypeSaveFromPlc,
            OperationAction.WashTypeWritePlcFromEditor,
            OperationAction.TrainTypeCreate,
            OperationAction.TrainTypeEdit,
            OperationAction.TrainTypeDelete,
            OperationAction.TrainTypeLoad,
            OperationAction.TrainTypeWritePlc,
            OperationAction.TrainTypeSaveFromPlc,
            OperationAction.TrainTypeInterpolationWrite,
            OperationAction.TrainTypeWritePlcFromEditor
        },
        OperationCategory.Process => new[] {
            OperationAction.SemiautomaticToggle,
            OperationAction.ManualModeToggle
        },
        OperationCategory.Statistics => new[] {
            OperationAction.StatisticsView,
            OperationAction.StatisticsExport,
            OperationAction.ReportGenerate,
            OperationAction.ReportExport
        },
        OperationCategory.PlcAlarmHistory => new[] {
            OperationAction.PlcAlarmActivated,
            OperationAction.PlcAlarmDeactivated,
            OperationAction.PlcNotificationActivated,
            OperationAction.PlcNotificationDeactivated,
            OperationAction.PlcInfoActivated,
            OperationAction.PlcInfoDeactivated
        },
        OperationCategory.Configuration => new[] {
            OperationAction.ConfigChange,
            OperationAction.ConfigWritePlc,
            OperationAction.FastConfigWritePlc,
            OperationAction.FastConfigChange
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
                OperationCategory.Configuration
            };
            var actions = new[] {
                OperationAction.PlcAlarmActivated,
                OperationAction.PlcAlarmDeactivated,
                OperationAction.SemiautomaticToggle,
                OperationAction.ManualModeToggle,
                OperationAction.TrainTypeLoad,
                OperationAction.ConfigWritePlc
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