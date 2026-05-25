// Controllers/SystemLogsController.cs
// L3 - System Logs API for real-time diagnostics
// Serves in-memory log buffer (Warning/Error/Critical)
// No persistence - buffer clears on backend restart

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/system-logs")]
    [Authorize]
    public class SystemLogsController : ControllerBase
    {
        private readonly ISystemLogService _logService;
        private readonly ILogger<SystemLogsController> _logger;

        public SystemLogsController(
            ISystemLogService logService,
            ILogger<SystemLogsController> logger)
        {
            _logService = logService;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /api/system-logs - Get filtered log entries
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Get system log entries from the in-memory buffer.
        /// Returns newest entries first.
        /// </summary>
        [HttpGet]
        public ActionResult<IReadOnlyList<SystemLogEntry>> GetLogs([FromQuery] SystemLogQuery? query)
        {
            try
            {
                var entries = _logService.GetEntries(query);
                return Ok(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system logs");
                return StatusCode(500, new { error = "Failed to retrieve system logs", details = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  GET /api/system-logs/summary - Get statistics
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Get summary statistics of the log buffer.
        /// Used by the compact card in InfoPanel.
        /// </summary>
        [HttpGet("summary")]
        public ActionResult<SystemLogSummary> GetSummary()
        {
            try
            {
                var summary = _logService.GetSummary();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system log summary");
                return StatusCode(500, new { error = "Failed to retrieve summary", details = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  POST /api/system-logs/client - Receive frontend logs
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Receive selective error/warning logs from the frontend.
        /// Only operational errors (SignalR disconnect, API timeout, 3D load fail).
        /// </summary>
        [HttpPost("client")]
        public ActionResult<SystemLogEntry> PostClientLog([FromBody] ClientLogRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(new { error = "Message is required" });

                // Sanitize: only allow Warning and Error from frontend
                if (request.Level < SystemLogLevel.Warning)
                    request.Level = SystemLogLevel.Warning;
                if (request.Level > SystemLogLevel.Error)
                    request.Level = SystemLogLevel.Error; // Frontend can't send Critical

                var entry = new SystemLogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = request.Level,
                    Source = SystemLogSource.Frontend,
                    Category = string.IsNullOrWhiteSpace(request.Category) ? "Frontend" : request.Category,
                    Message = request.Message
                };

                _logService.AddEntry(entry);

                return Ok(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording client log");
                return StatusCode(500, new { error = "Failed to record client log", details = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  DELETE /api/system-logs - Clear buffer (Admin/SuperAdmin)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Clear all entries from the buffer. Restricted to Administrator/SuperAdmin roles.
        /// </summary>
        [HttpDelete]
        [Authorize(Roles = "Administrator,SuperAdmin")]
        public ActionResult Clear()
        {
            try
            {
                _logService.Clear();
                _logger.LogInformation("System log buffer cleared by {User}", User?.Identity?.Name ?? "unknown");
                return Ok(new { message = "System log buffer cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing system logs");
                return StatusCode(500, new { error = "Failed to clear logs", details = ex.Message });
            }
        }
    }
}
