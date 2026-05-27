// ============================================================================
// ExportProfilesController.cs — CRUD de perfiles de destino del Export Manager
// ============================================================================
// Base: /api/export/profiles
// Autorización: módulo "ExportManager" (CanView para listar, CanEdit para CRUD
// y test SMTP). SuperAdmin bypass total.
// Audit log: cada operación de escritura registra evento en AuditCategory.Export.
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Authorization;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Services.Export;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/export/profiles")]
[Authorize]
public class ExportProfilesController : ControllerBase
{
    private readonly IExportProfileService _service;
    private readonly IAuditLogService _audit;
    private readonly ILogger<ExportProfilesController> _logger;

    public ExportProfilesController(
        IExportProfileService service,
        IAuditLogService audit,
        ILogger<ExportProfilesController> logger)
    {
        _service = service;
        _audit = audit;
        _logger = logger;
    }

    // ───────────── FOLDER PROFILES ─────────────

    [HttpGet("folders")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> ListFolders(CancellationToken ct = default)
        => Ok(await _service.ListFolderProfilesAsync(ct));

    [HttpGet("folders/{id}")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetFolder(string id, CancellationToken ct = default)
    {
        var p = await _service.GetFolderProfileAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("folders")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> CreateFolder([FromBody] ExportFolderProfileRequest req, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var p = await _service.CreateFolderProfileAsync(req, uname, ct);
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Success,
                $"FolderProfile creado: '{p.Name}' → {p.Path}", uid, uname);
            return CreatedAtAction(nameof(GetFolder), new { id = p.Id }, p);
        }
        catch (ArgumentException ex)
        {
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Failure, $"FolderProfile validación fallida: {ex.Message}", uid, uname);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.CreateFolder error inesperado (name='{Name}')", req?.Name);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpPut("folders/{id}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> UpdateFolder(string id, [FromBody] ExportFolderProfileRequest req, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var p = await _service.UpdateFolderProfileAsync(id, req, ct);
            if (p is null) return NotFound();
            await AuditAsync(AuditAction.ExportTaskUpdate, AuditResult.Success,
                $"FolderProfile actualizado: '{p.Name}' → {p.Path}", uid, uname);
            return Ok(p);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.UpdateFolder error inesperado (id={Id})", id);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpDelete("folders/{id}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> DeleteFolder(string id, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var ok = await _service.DeleteFolderProfileAsync(id, ct);
            if (!ok) return NotFound();
            await AuditAsync(AuditAction.ExportTaskDelete, AuditResult.Success,
                $"FolderProfile eliminado id={id}", uid, uname);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.DeleteFolder error inesperado (id={Id})", id);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    // ───────────── EMAIL PROFILES ─────────────

    [HttpGet("emails")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> ListEmails(CancellationToken ct = default)
        => Ok(await _service.ListEmailProfilesAsync(ct));

    [HttpGet("emails/{id}")]
    [RequireModulePermission("ExportManager", "view")]
    public async Task<IActionResult> GetEmail(string id, CancellationToken ct = default)
    {
        var p = await _service.GetEmailProfileAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("emails")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> CreateEmail([FromBody] ExportEmailProfileRequest req, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var p = await _service.CreateEmailProfileAsync(req, uname, ct);
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Success,
                $"EmailProfile creado: '{p.Name}' → {p.Host}:{p.Port} (SSL={p.UseSsl})", uid, uname);
            return CreatedAtAction(nameof(GetEmail), new { id = p.Id }, p);
        }
        catch (ArgumentException ex)
        {
            await AuditAsync(AuditAction.ExportTaskCreate, AuditResult.Failure, $"EmailProfile validación fallida: {ex.Message}", uid, uname);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.CreateEmail error inesperado (name='{Name}', host='{Host}')", req?.Name, req?.Host);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpPut("emails/{id}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> UpdateEmail(string id, [FromBody] ExportEmailProfileRequest req, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var p = await _service.UpdateEmailProfileAsync(id, req, ct);
            if (p is null) return NotFound();
            var passNote = string.IsNullOrEmpty(req.Password) ? "" : " [password actualizada]";
            await AuditAsync(AuditAction.ExportTaskUpdate, AuditResult.Success,
                $"EmailProfile actualizado: '{p.Name}'{passNote}", uid, uname);
            return Ok(p);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.UpdateEmail error inesperado (id={Id})", id);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpDelete("emails/{id}")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> DeleteEmail(string id, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var ok = await _service.DeleteEmailProfileAsync(id, ct);
            if (!ok) return NotFound();
            await AuditAsync(AuditAction.ExportTaskDelete, AuditResult.Success,
                $"EmailProfile eliminado id={id}", uid, uname);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.DeleteEmail error inesperado (id={Id})", id);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpPost("emails/{id}/test")]
    [RequireModulePermission("ExportManager", "edit")]
    public async Task<IActionResult> TestEmail(string id, [FromBody] ExportEmailTestRequest req, CancellationToken ct = default)
    {
        var (uid, uname) = GetUser();
        try
        {
            var resp = await _service.TestEmailProfileAsync(id, req, ct);
            await AuditAsync(
                AuditAction.ExportTaskRun,
                resp.Success ? AuditResult.Success : AuditResult.Failure,
                $"EmailProfile {id} test → {req.To}: {resp.Message}", uid, uname);
            return resp.Success ? Ok(resp) : BadRequest(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export.TestEmail error inesperado (id={Id}, to='{To}')", id, req?.To);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    // ───────────── Helpers ─────────────

    private (string userId, string userName) GetUser()
    {
        var uid = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var uname = User?.Identity?.Name ?? User?.FindFirstValue(ClaimTypes.Name) ?? "system";
        return (uid, uname);
    }

    private async Task AuditAsync(AuditAction action, AuditResult result, string details, string userId, string userName)
    {
        try
        {
            await _audit.LogAsync(
                AuditCategory.Export,
                action,
                result,
                details: details,
                userId: userId,
                userName: userName,
                ipAddress: HttpContext?.Connection?.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed");
        }
    }
}
