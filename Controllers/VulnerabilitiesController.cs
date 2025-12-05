// 🛡️ Vulnerability Scanner Controller - EU CRA Compliance
// REST API for vulnerability scanning operations

using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// Vulnerability Scanner API - Scan SBOM dependencies for CVEs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VulnerabilitiesController : ControllerBase
{
    private readonly IVulnerabilityService _vulnerabilityService;
    private readonly ILogger<VulnerabilitiesController> _logger;

    public VulnerabilitiesController(
        IVulnerabilityService vulnerabilityService,
        ILogger<VulnerabilitiesController> logger)
    {
        _vulnerabilityService = vulnerabilityService;
        _logger = logger;
    }

    /// <summary>
    /// Get vulnerability scanner status and last scan summary
    /// </summary>
    /// <returns>Current scanner status</returns>
    /// <response code="200">Returns scanner status</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(VulnerabilityScanStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<VulnerabilityScanStatus>> GetStatus()
    {
        try
        {
            var status = await _vulnerabilityService.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vulnerability status");
            return StatusCode(500, new { error = "Failed to get vulnerability status", message = ex.Message });
        }
    }

    /// <summary>
    /// Perform a full vulnerability scan on SBOM components
    /// </summary>
    /// <returns>Scan results with list of vulnerabilities</returns>
    /// <response code="200">Scan completed successfully</response>
    /// <response code="400">Scanner not configured or SBOM not available</response>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(VulnerabilityScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VulnerabilityScanResult>> Scan()
    {
        try
        {
            _logger.LogInformation("🛡️ Vulnerability scan requested");
            
            if (!_vulnerabilityService.IsEnabled)
            {
                return BadRequest(new 
                { 
                    success = false,
                    error = "Vulnerability scanner not configured",
                    message = "Configure VulnScanApiUrl in Excel System Config sheet to enable scanning"
                });
            }

            var result = await _vulnerabilityService.ScanAsync();
            
            if (!result.Success)
            {
                return BadRequest(new { success = false, error = result.Message });
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vulnerability scan failed");
            return StatusCode(500, new { success = false, error = "Scan failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed list of vulnerabilities from last scan
    /// </summary>
    /// <returns>List of vulnerabilities</returns>
    /// <response code="200">Returns list of vulnerabilities</response>
    [HttpGet("list")]
    [ProducesResponseType(typeof(List<Vulnerability>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Vulnerability>>> GetVulnerabilities()
    {
        try
        {
            var vulnerabilities = await _vulnerabilityService.GetVulnerabilitiesAsync();
            return Ok(vulnerabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vulnerabilities");
            return StatusCode(500, new { error = "Failed to get vulnerabilities", message = ex.Message });
        }
    }

    /// <summary>
    /// Get vulnerabilities filtered by severity
    /// </summary>
    /// <param name="severity">Severity level (critical, high, medium, low)</param>
    /// <returns>Filtered list of vulnerabilities</returns>
    [HttpGet("list/{severity}")]
    [ProducesResponseType(typeof(List<Vulnerability>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Vulnerability>>> GetVulnerabilitiesBySeverity(string severity)
    {
        try
        {
            var vulnerabilities = await _vulnerabilityService.GetVulnerabilitiesAsync();
            
            var severityEnum = severity.ToLowerInvariant() switch
            {
                "critical" => VulnSeverity.Critical,
                "high" => VulnSeverity.High,
                "medium" => VulnSeverity.Medium,
                "low" => VulnSeverity.Low,
                _ => (VulnSeverity?)null
            };

            if (severityEnum == null)
            {
                return BadRequest(new { error = "Invalid severity", message = "Valid values: critical, high, medium, low" });
            }

            var filtered = vulnerabilities.Where(v => v.Severity == severityEnum).ToList();
            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vulnerabilities");
            return StatusCode(500, new { error = "Failed to get vulnerabilities", message = ex.Message });
        }
    }

    /// <summary>
    /// Get summary report of vulnerabilities
    /// </summary>
    /// <returns>Vulnerability summary report</returns>
    [HttpGet("report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetReport()
    {
        try
        {
            var status = await _vulnerabilityService.GetStatusAsync();
            var vulnerabilities = await _vulnerabilityService.GetVulnerabilitiesAsync();

            var report = new
            {
                GeneratedAt = DateTime.UtcNow,
                SystemName = "SW.PC.SUPERVISOR.System",
                ScannerEnabled = status.IsEnabled,
                ApiType = status.ApiType,
                LastScan = status.LastScanDate,
                Summary = new
                {
                    PackagesScanned = status.PackagesScanned,
                    TotalVulnerabilities = status.TotalVulnerabilities,
                    Critical = status.CriticalCount,
                    High = status.HighCount,
                    Medium = status.MediumCount,
                    Low = status.LowCount
                },
                CriticalVulnerabilities = vulnerabilities
                    .Where(v => v.Severity == VulnSeverity.Critical)
                    .Select(v => new { v.Id, v.Summary, v.AffectedPackage, v.InstalledVersion, v.FixedVersion })
                    .ToList(),
                HighVulnerabilities = vulnerabilities
                    .Where(v => v.Severity == VulnSeverity.High)
                    .Select(v => new { v.Id, v.Summary, v.AffectedPackage, v.InstalledVersion, v.FixedVersion })
                    .ToList(),
                ComplianceNote = "EU Cyber Resilience Act (CRA) - Vulnerability Disclosure"
            };

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate vulnerability report");
            return StatusCode(500, new { error = "Failed to generate report", message = ex.Message });
        }
    }

    /// <summary>
    /// Check if scanner is enabled
    /// </summary>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> IsEnabled()
    {
        return Ok(new 
        { 
            enabled = _vulnerabilityService.IsEnabled,
            message = _vulnerabilityService.IsEnabled 
                ? "Vulnerability scanner is configured and ready" 
                : "Scanner disabled. Configure VulnScanApiUrl in Excel."
        });
    }
}
