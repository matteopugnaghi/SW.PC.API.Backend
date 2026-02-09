using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 📋 EU CRA - Controlador de Audit Log
    /// Proporciona acceso a los registros de auditoría del sistema
    /// En modo desarrollo: usa X-Project-Id header para seleccionar proyecto
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditLogService _auditService;
        private readonly IRequestProjectContext _projectContext;
        private readonly ILogger<AuditController> _logger;

        public AuditController(
            IAuditLogService auditService, 
            IRequestProjectContext projectContext,
            ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _projectContext = projectContext;
            _logger = logger;
        }

        /// <summary>
        /// 📊 Obtener estado del sistema de auditoría
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _auditService.GetStatusAsync(_projectContext.ProjectId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting audit status");
                return StatusCode(500, new { error = "Error retrieving audit status", details = ex.Message });
            }
        }

        /// <summary>
        /// 📋 Obtener logs recientes
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 50)
        {
            try
            {
                var logs = await _auditService.GetRecentLogsAsync(count, _projectContext.ProjectId);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting recent audit logs");
                return StatusCode(500, new { error = "Error retrieving recent logs", details = ex.Message });
            }
        }

        /// <summary>
        /// 🔍 Consultar logs con filtros
        /// </summary>
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs([FromQuery] AuditLogQuery query)
        {
            try
            {
                var result = await _auditService.GetLogsAsync(query, _projectContext.ProjectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error querying audit logs");
                return StatusCode(500, new { error = "Error querying logs", details = ex.Message });
            }
        }

        /// <summary>
        /// 📤 Exportar logs
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportLogs([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var json = await _auditService.ExportLogsAsync(from, to, _projectContext.ProjectId);
                
                var fileName = $"audit_export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                
                // 📋 Audit Log - EU CRA: Registrar exportación de logs
                await _auditService.LogAsync(
                    AuditCategory.Export,
                    AuditAction.AuditExport,
                    AuditResult.Success,
                    $"Exported audit logs to {fileName} ({json.Length} bytes, from: {from?.ToString("yyyy-MM-dd") ?? "all"}, to: {to?.ToString("yyyy-MM-dd") ?? "all"})",
                    userId: User.Identity?.Name ?? "Anonymous",
                    affectedItemCount: json.Split('\n').Length,
                    projectId: _projectContext.ProjectId
                );
                
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error exporting audit logs");
                
                // 📋 Audit Log - Error
                await _auditService.LogAsync(
                    AuditCategory.Export,
                    AuditAction.AuditExport,
                    AuditResult.Error,
                    $"Failed to export audit logs: {ex.Message}",
                    userId: User.Identity?.Name ?? "Anonymous",
                    projectId: _projectContext.ProjectId
                );
                
                return StatusCode(500, new { error = "Error exporting logs", details = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Obtener resumen de auditoría
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
        {
            try
            {
                var summary = await _auditService.GetSummaryAsync(days, _projectContext.ProjectId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting audit summary");
                return StatusCode(500, new { error = "Error retrieving summary", details = ex.Message });
            }
        }

        /// <summary>
        /// 📋 Obtener logs por categoría
        /// </summary>
        [HttpGet("logs/category/{category}")]
        public async Task<IActionResult> GetLogsByCategory(string category, [FromQuery] int take = 50)
        {
            try
            {
                if (!Enum.TryParse<AuditCategory>(category, true, out var auditCategory))
                {
                    return BadRequest(new { error = $"Invalid category: {category}" });
                }

                var query = new AuditLogQuery
                {
                    Category = auditCategory,
                    Take = take
                };

                var result = await _auditService.GetLogsAsync(query, _projectContext.ProjectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting logs by category");
                return StatusCode(500, new { error = "Error retrieving logs", details = ex.Message });
            }
        }

        /// <summary>
        /// 📋 Obtener logs de integridad
        /// </summary>
        [HttpGet("integrity")]
        public async Task<IActionResult> GetIntegrityLogs([FromQuery] int take = 50)
        {
            return await GetLogsByCategory("Integrity", take);
        }

        /// <summary>
        /// 📋 Obtener logs de vulnerabilidades
        /// </summary>
        [HttpGet("vulnerabilities")]
        public async Task<IActionResult> GetVulnerabilityLogs([FromQuery] int take = 50)
        {
            return await GetLogsByCategory("Vulnerability", take);
        }
    }
}
