// 📋 SBOM Controller - Software Bill of Materials API
// EU CRA Compliance: Endpoints for SBOM generation and retrieval

using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// SBOM Controller - Manages Software Bill of Materials
/// Required by EU Cyber Resilience Act (CRA) for software transparency
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SbomController : ControllerBase
{
    private readonly ISbomService _sbomService;
    private readonly ILogger<SbomController> _logger;

    public SbomController(ISbomService sbomService, ILogger<SbomController> logger)
    {
        _sbomService = sbomService;
        _logger = logger;
    }

    /// <summary>
    /// Get SBOM status - existence, date, component counts
    /// </summary>
    /// <returns>Current SBOM status</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SbomStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<SbomStatus>> GetStatus()
    {
        _logger.LogDebug("📋 GET /api/sbom/status");
        var status = await _sbomService.GetStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Get full SBOM document in CycloneDX format
    /// </summary>
    /// <returns>Complete SBOM document</returns>
    [HttpGet]
    [ProducesResponseType(typeof(SbomDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SbomDocument>> GetSbom()
    {
        _logger.LogDebug("📋 GET /api/sbom");
        var sbom = await _sbomService.GetSbomAsync();
        
        if (sbom == null)
        {
            return NotFound(new { message = "SBOM not found. Generate one first with POST /api/sbom/generate" });
        }
        
        return Ok(sbom);
    }

    /// <summary>
    /// Download SBOM as JSON file
    /// </summary>
    /// <returns>SBOM JSON file download</returns>
    [HttpGet("download")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSbom()
    {
        _logger.LogInformation("📥 GET /api/sbom/download");
        var sbomJson = await _sbomService.GetSbomJsonAsync();
        
        if (sbomJson == null)
        {
            return NotFound(new { message = "SBOM not found. Generate one first." });
        }
        
        var fileName = $"sbom-{DateTime.UtcNow:yyyy-MM-dd}.json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(sbomJson);
        
        return File(bytes, "application/json", fileName);
    }

    /// <summary>
    /// Generate new SBOM document
    /// </summary>
    /// <param name="request">Generation options</param>
    /// <returns>Generation result with status</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SbomGenerateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SbomGenerateResult), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SbomGenerateResult>> GenerateSbom([FromBody] SbomGenerateRequest? request = null)
    {
        request ??= new SbomGenerateRequest();
        
        _logger.LogInformation("🔄 POST /api/sbom/generate - Requested by: {RequestedBy}", request.RequestedBy);
        
        var result = await _sbomService.GenerateAsync(request);
        
        if (result.Success)
        {
            return Ok(result);
        }
        
        return StatusCode(500, result);
    }

    /// <summary>
    /// Get list of components from SBOM
    /// </summary>
    /// <param name="type">Filter by type: "nuget" or "npm"</param>
    /// <returns>List of components</returns>
    [HttpGet("components")]
    [ProducesResponseType(typeof(IEnumerable<SbomComponent>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SbomComponent>>> GetComponents([FromQuery] string? type = null)
    {
        _logger.LogDebug("📋 GET /api/sbom/components?type={Type}", type);
        var sbom = await _sbomService.GetSbomAsync();
        
        if (sbom == null)
        {
            return NotFound(new { message = "SBOM not found" });
        }
        
        var components = sbom.Components.AsEnumerable();
        
        // Filter by type if specified
        if (!string.IsNullOrEmpty(type))
        {
            components = type.ToLower() switch
            {
                "nuget" => components.Where(c => c.Purl?.StartsWith("pkg:nuget") == true),
                "npm" => components.Where(c => c.Purl?.StartsWith("pkg:npm") == true),
                _ => components
            };
        }
        
        return Ok(components);
    }

    /// <summary>
    /// Get SBOM summary statistics
    /// </summary>
    /// <returns>Summary with counts by type</returns>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSummary()
    {
        _logger.LogDebug("📋 GET /api/sbom/summary");
        var sbom = await _sbomService.GetSbomAsync();
        
        if (sbom == null)
        {
            return NotFound(new { message = "SBOM not found" });
        }
        
        var summary = new
        {
            TotalComponents = sbom.Components.Count,
            NuGetPackages = sbom.Components.Count(c => c.Purl?.StartsWith("pkg:nuget") == true),
            NpmPackages = sbom.Components.Count(c => c.Purl?.StartsWith("pkg:npm") == true),
            GeneratedAt = sbom.Metadata.Timestamp,
            SpecVersion = sbom.SpecVersion,
            Format = sbom.BomFormat,
            MainComponent = sbom.Metadata.Component?.Name,
            ComponentsByScope = sbom.Components
                .GroupBy(c => c.Scope ?? "unknown")
                .Select(g => new { Scope = g.Key, Count = g.Count() })
        };
        
        return Ok(summary);
    }
}
