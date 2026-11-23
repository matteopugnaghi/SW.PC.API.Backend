// ============================================================================
// HostInterlockController.cs — HostName Interlocking (Usuarios → Equipos)
// ============================================================================
// Lectura y edición de los niveles de interlocking por nombre de equipo:
// equipos con certificado mTLS, dispositivos por token y el equipo local.
// La regla se evalúa en el login (ver Services/HostInterlock.cs).
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/host-interlock")]
[Produces("application/json")]
[Authorize(Roles = "SuperAdmin,Administrator")]
public class HostInterlockController : ControllerBase
{
    // 📁 BD POR PROYECTO: misma factory que CertificateController (registro de equipos)
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IAuditLogService _auditLog;

    public HostInterlockController(IProjectDbContextFactory dbFactory, IAuditLogService auditLog)
    {
        _dbFactory = dbFactory;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Niveles configurados + nombre del equipo local + si la función está activa
    /// (solo con MtlsRequireRegisteredMachine=TRUE).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        await using var db = _dbFactory.CreateDbContext();
        var settings = await db.HostInterlockSettings.AsNoTracking()
            .OrderBy(s => s.HostName)
            .Select(s => new { s.HostName, s.Interlocking, s.OverrideInterlocking })
            .ToListAsync();

        return Ok(new
        {
            enabled = HostInterlock.Enabled,
            localHostName = HostInterlock.LocalHostName,
            settings
        });
    }

    /// <summary>
    /// Fija los niveles de un equipo. Override exige Interlocking. El cambio se aplica en los
    /// siguientes inicios de sesión (no expulsa a nadie por sí mismo).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] HostInterlockUpdateRequest request)
    {
        var requested = request?.HostName?.Trim();
        if (string.IsNullOrEmpty(requested) || requested.Length > 100)
            return BadRequest(new { error = "Nombre de equipo inválido (1-100 caracteres)." });
        if (request!.OverrideInterlocking && !request.Interlocking)
            return BadRequest(new { error = "Override Interlocking requiere tener Interlocking habilitado." });

        await using var db = _dbFactory.CreateDbContext();

        // Solo equipos conocidos: local, con certificado enrolado o con token no revocado.
        var registered = await HostInterlock.GetRegisteredHostNamesAsync(db);
        var hostName = registered.FirstOrDefault(n => n.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (hostName == null)
            return NotFound(new { error = $"El equipo '{requested}' no está registrado." });

        var setting = (await db.HostInterlockSettings.ToListAsync())
            .FirstOrDefault(s => s.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase));
        var before = setting == null
            ? "Interlocking=OFF, Override=OFF"
            : $"Interlocking={OnOff(setting.Interlocking)}, Override={OnOff(setting.OverrideInterlocking)}";

        if (setting == null)
        {
            setting = new HostInterlockSetting { HostName = hostName };
            db.HostInterlockSettings.Add(setting);
        }
        setting.Interlocking = request.Interlocking;
        setting.OverrideInterlocking = request.OverrideInterlocking;
        setting.UpdatedBy = User.Identity?.Name ?? "unknown";
        setting.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();

        await _auditLog.LogAsync(AuditCategory.Security, AuditAction.ConfigChange, AuditResult.Success,
            details: $"Interlocking del equipo '{hostName}' modificado: Interlocking={OnOff(setting.Interlocking)}, " +
                     $"Override={OnOff(setting.OverrideInterlocking)} (antes: {before})",
            userName: User.Identity?.Name,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new
        {
            hostName = setting.HostName,
            interlocking = setting.Interlocking,
            overrideInterlocking = setting.OverrideInterlocking
        });
    }

    // Valores neutros respecto al idioma: el visor de auditoría traduce el resto de la frase.
    private static string OnOff(bool value) => value ? "ON" : "OFF";
}
