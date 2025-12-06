using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 📋 EU CRA - Controlador de Audit Log
    /// Proporciona acceso a los registros de auditoría del sistema
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditLogService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditLogService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
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
                var status = await _auditService.GetStatusAsync();
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
                var logs = await _auditService.GetRecentLogsAsync(count);
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
                var result = await _auditService.GetLogsAsync(query);
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
                var json = await _auditService.ExportLogsAsync(from, to);
                
                var fileName = $"audit_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error exporting audit logs");
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
                var summary = await _auditService.GetSummaryAsync(days);
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

                var result = await _auditService.GetLogsAsync(query);
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
