// ============================================================================
// ExportTasksController.cs — Endpoints REST del Gestor de Exportaciones
// ============================================================================
// Base: /api/export
// Autorización: por permiso de módulo "ExportManager" persistido en RolePermissions.
//   - CanView  = listar tareas + ejecutar manualmente cualquier tarea.
//   - CanEdit  = crear / modificar / eliminar / pausar tareas.
//   - SuperAdmin: bypass total.
// Por defecto Administrator y Maintenance llevan view+edit habilitados;
// Operator/Viewer/Auditor llevan ambos deshabilitados (configurable desde UI).
// Audit log: cada operación CRUD + Run genera entrada en AuditCategory.Export.
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Authorization;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Services.Export;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportTasksController : ControllerBase
{
    private readonly IExportService _service;
    private readonly IExportDatasetRegistry _registry;
    private readonly IAuditLogService _audit;
    private readonly IRequestProjectContext _projectContext;
    private readonly IExcelConfigService _excelConfig;
    private readonly ILogger<ExportTasksController> _logger;

    public ExportTasksController(
        IExportService service,
        IExportDatasetRegistry registry,
        IAuditLogService audit,
        IRequestProjectContext projectContext,
        IExcelConfigService excelConfig,
        ILogger<ExportTasksController> logger)
    {
        _service = service;
        _registry = registry;
        _audit = audit;
        _projectContext = projectContext;
        _excelConfig = excelConfig;
        _logger = logger;
    }

    // ─────────────────────── Datasets disponibles ───────────────────────

    /// <summary>
    /// Lista los datasets disponibles para un Source (módulo anfitrión).
    /// Consumido por el wizard (Step 0) para construir la UI de campos y filtros.
    /// </summary>
    [HttpGet("datasets/{source}")]
    [RequireModulePermission("ExportManager", "view")]
    public IActionResult GetDatasetsBySource(string source)
    {
        var providers = _registry.GetBySource(source);
        return Ok(providers.Select(p => new
        {
            datasetId = p.DatasetId,
            source = p.Source,
            displayName = p.DisplayName,
            fields = p.AvailableFields,
            filters = p.AvailableFilters
        }));
    }

    // ─────────────────────── Entorno ───────────────────────

    /// <summary>
    /// Devuelve el entorno público del Export Manager para el proyecto activo:
    /// carpetas permitidas (AllowedExportFolders) y si el SMTP está configurado.
    /// Consumido por el Wizard para deshabilitar checkboxes/dropdowns inviables.
    /// No expone credenciales SMTP.
    /// </summary>
    [HttpGet("environment")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetEnvironment(CancellationToken ct = default)
    {
        var env = await _service.GetEnvironmentAsync(ct);
        return Ok(new
        {
            allowedFolders = env.AllowedFolders,
            smtpConfigured = env.SmtpConfigured,
            folderProfiles = env.FolderProfiles,
            emailProfiles = env.EmailProfiles,
        });
    }

    /// <summary>
    /// Lista variables PLC declaradas en Excel (`PLC_Variables`) filtradas por
    /// dataType (por defecto "BOOL"). Consumido por el Wizard (Step 4) para
    /// poblar el selector de variable trigger en tareas con ExecutionType="plc".
    /// </summary>
    [HttpGet("plc-variables")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetPlcVariables([FromQuery] string dataType = "BOOL", CancellationToken ct = default)
    {
        try
        {
            var path = _excelConfig.GetExcelConfigPath();
            var all = await _excelConfig.LoadPlcVariablesAsync(path);
            var filtered = (all ?? new List<PlcVariable>())
                .Where(v => string.IsNullOrWhiteSpace(dataType)
                            || string.Equals(v.DataType?.Trim(), dataType.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.VariableName, StringComparer.OrdinalIgnoreCase)
                .Select(v => new
                {
                    name = v.VariableName,
                    dataType = v.DataType,
                    description = v.Description,
                    accessMode = v.AccessMode,
                })
                .ToList();
            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Export.GetPlcVariables failed");
            return Ok(Array.Empty<object>());
        }
    }

    /// <summary>
    /// Valida una expresión cron de 5 campos. Devuelve {ok, error}. Usado por
    /// el Wizard (Step 4) para feedback inmediato al usuario.
    /// </summary>
    [HttpGet("cron/validate")]
    [RequireModulePermission("ExportManager", "view")]
    public IActionResult ValidateCron([FromQuery] string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Ok(new { ok = false, error = "Expresión vacía" });
        var (ok, error, _) = SW.PC.API.Backend.Services.Export.CronExpressionEvaluator.TryParse(expression);
        return Ok(new { ok, error });
    }

    // ─────────────────────── CRUD ───────────────────────

    [HttpGet("tasks")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetTasks([FromQuery] string? source = null, CancellationToken ct = default)
    {
        var tasks = await _service.GetTasksAsync(source, ct);
        return Ok(tasks);
    }

    [HttpGet("tasks/{id:int}")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetTask(int id, CancellationToken ct = default)
    {
        var task = await _service.GetTaskByIdAsync(id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost("tasks")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> CreateTask([FromBody] ExportTaskRequest req, CancellationToken ct = default)
    {
        var (userId, userName) = GetUser();
        try
        {
            var task = await _service.CreateTaskAsync(req, userName, ct);
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Success,
                $"Tarea '{task.Name}' creada (id={task.Id}, source={task.Source}, format={task.Format}, dest={string.Join('+', task.Destinations)})",
                userId, userName);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }
        catch (ArgumentException ex)
        {
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Failure, $"Validación fallida: {ex.Message}", userId, userName);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.CreateTask error inesperado");
            var detail = ex.InnerException?.Message ?? ex.Message;
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Error, $"Excepción: {ex.GetType().Name}: {detail}", userId, userName);
            return StatusCode(500, new { error = $"{ex.Message} | inner: {detail}", type = ex.GetType().Name, inner = ex.InnerException?.Message });
        }
    }

    [HttpPut("tasks/{id:int}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] ExportTaskRequest req, CancellationToken ct = default)
    {
        var (userId, userName) = GetUser();
        try
        {
            var task = await _service.UpdateTaskAsync(id, req, ct);
            if (task is null) return NotFound();
            await AuditAsync(AuditAction.ExportTaskUpdate, AuditResult.Success,
                $"Tarea '{task.Name}' actualizada (id={task.Id})", userId, userName);
            return Ok(task);
        }
        catch (ArgumentException ex)
        {
            await AuditAsync(AuditAction.ExportTaskUpdate, AuditResult.Failure, $"Validación fallida: {ex.Message}", userId, userName);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.UpdateTask error inesperado");
            await AuditAsync(AuditAction.ExportTaskUpdate, AuditResult.Error, $"Excepción: {ex.GetType().Name}: {ex.Message}", userId, userName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name, inner = ex.InnerException?.Message });
        }
    }

    [HttpDelete("tasks/{id:int}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken ct = default)
    {
        var (userId, userName) = GetUser();
        var ok = await _service.DeleteTaskAsync(id, ct);
        if (!ok) return NotFound();
        await AuditAsync(AuditAction.ExportTaskDelete, AuditResult.Success, $"Tarea {id} eliminada", userId, userName);
        return NoContent();
    }

    [HttpPost("tasks/{id:int}/toggle")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> ToggleTask(int id, [FromQuery] bool enabled, CancellationToken ct = default)
    {
        var (userId, userName) = GetUser();
        var task = await _service.ToggleTaskAsync(id, enabled, ct);
        if (task is null) return NotFound();
        await AuditAsync(AuditAction.ExportTaskToggle, AuditResult.Success,
            $"Tarea {id} {(enabled ? "habilitada" : "deshabilitada")}", userId, userName);
        return Ok(task);
    }

    // ─────────────────────── RUN ───────────────────────

    [HttpPost("tasks/{id:int}/run")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> RunTask(int id, [FromBody] Dictionary<string, object?>? runtimeMetadata = null, CancellationToken ct = default)
    {
        var (userId, userName) = GetUser();
        try
        {
            var result = await _service.RunTaskAsync(id, runtimeMetadata, ct);
            var auditResult = result.Success ? AuditResult.Success
                             : (result.Results.Any(r => r.Success) ? AuditResult.Warning : AuditResult.Failure);
            await AuditAsync(AuditAction.ExportTaskRun, auditResult,
                $"Tarea {id} ejecutada (manual): {result.Summary}", userId, userName,
                affectedItemCount: result.Results.Count(r => r.Success));
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            await AuditAsync(AuditAction.ExportTaskRun, AuditResult.Failure, $"Tarea {id} error: {ex.Message}", userId, userName);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado ejecutando tarea {Id}", id);
            await AuditAsync(AuditAction.ExportTaskRun, AuditResult.Error, $"Tarea {id} excepción: {ex.Message}", userId, userName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ─────────────────────── PREVIEW ───────────────────────

    [HttpPost("preview")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> Preview([FromBody] ExportPreviewRequest req, CancellationToken ct = default)
    {
        try
        {
            var dataset = await _service.PreviewAsync(req.DatasetProvider, req.Selection, ct);
            return Ok(new
            {
                columns = dataset.Columns,
                columnIds = dataset.ColumnIds,
                rows = dataset.Rows,
                totalRows = dataset.TotalRows,
                truncated = dataset.Metadata.TryGetValue("truncated", out var t) && t is bool b && b,
                metadata = dataset.Metadata
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Export.Preview rechazado por validación (dataset={Dataset})", req?.DatasetProvider);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.Preview error inesperado (dataset={Dataset})", req?.DatasetProvider);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    // ─────────────────────── Helpers ───────────────────────

    private (string? UserId, string UserName) GetUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Identity?.Name ?? "unknown";
        return (userId, userName);
    }

    private Task AuditAsync(AuditAction action, AuditResult result, string details,
        string? userId, string userName, int? affectedItemCount = null)
    {
        return _audit.LogAsync(
            AuditCategory.Export, action, result, details,
            userId: userId,
            userName: userName,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            affectedItemCount: affectedItemCount,
            projectId: _projectContext.ProjectId);
    }
}
