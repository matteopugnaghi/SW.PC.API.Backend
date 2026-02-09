using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using System.Text.Json;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller para gestionar la configuración del Tour Virtual (waypoints de cámara).
    /// Los waypoints se guardan en Projects/{projectId}/config/tour-waypoints.json
    /// Solo SuperAdmin puede modificar los waypoints (calibración).
    /// NOTA: Tour es UI/UX, no requiere Audit Log L1 (no es security-critical).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TourController : ControllerBase
    {
        private readonly IRequestProjectContext _projectContext;
        private readonly ILogger<TourController> _logger;

        private const string TOUR_WAYPOINTS_FILENAME = "tour-waypoints.json";

        public TourController(
            IRequestProjectContext projectContext,
            ILogger<TourController> logger)
        {
            _projectContext = projectContext;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene los waypoints del tour para el proyecto activo.
        /// Cualquier usuario autenticado puede leer.
        /// </summary>
        [HttpGet("waypoints")]
        [ProducesResponseType(typeof(TourWaypointsResponse), 200)]
        public async Task<ActionResult<TourWaypointsResponse>> GetWaypoints()
        {
            try
            {
                var filePath = GetWaypointsFilePath();
                
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogInformation("No tour waypoints file found at {Path}, returning empty array", filePath);
                    return Ok(new TourWaypointsResponse 
                    { 
                        Waypoints = new List<TourWaypoint>(),
                        ProjectId = _projectContext.ProjectId
                    });
                }

                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var waypoints = JsonSerializer.Deserialize<List<TourWaypoint>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<TourWaypoint>();

                _logger.LogInformation("Loaded {Count} tour waypoints for project {ProjectId}", 
                    waypoints.Count, _projectContext.ProjectId);

                return Ok(new TourWaypointsResponse
                {
                    Waypoints = waypoints,
                    ProjectId = _projectContext.ProjectId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tour waypoints");
                return StatusCode(500, "Error loading tour waypoints");
            }
        }

        /// <summary>
        /// Guarda los waypoints del tour para el proyecto activo.
        /// Solo SuperAdmin puede escribir (calibración).
        /// </summary>
        [HttpPut("waypoints")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(typeof(TourWaypointsResponse), 200)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<TourWaypointsResponse>> SaveWaypoints([FromBody] SaveWaypointsRequest request)
        {
            try
            {
                if (request?.Waypoints == null)
                {
                    return BadRequest("Waypoints array is required");
                }

                // Sanitizar waypoints antes de guardar
                var sanitizedWaypoints = request.Waypoints
                    .Where(w => w != null && IsValidWaypoint(w))
                    .ToList();

                var filePath = GetWaypointsFilePath();
                var directory = Path.GetDirectoryName(filePath);
                
                // Asegurar que existe la carpeta config
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(sanitizedWaypoints, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await System.IO.File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("Saved {Count} tour waypoints for project {ProjectId} by user {User}", 
                    sanitizedWaypoints.Count, _projectContext.ProjectId, User.Identity?.Name);

                return Ok(new TourWaypointsResponse
                {
                    Waypoints = sanitizedWaypoints,
                    ProjectId = _projectContext.ProjectId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving tour waypoints");
                return StatusCode(500, "Error saving tour waypoints");
            }
        }

        /// <summary>
        /// Elimina todos los waypoints del tour (reset).
        /// Solo SuperAdmin puede eliminar.
        /// </summary>
        [HttpDelete("waypoints")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(200)]
        [ProducesResponseType(403)]
        public async Task<ActionResult> DeleteWaypoints()
        {
            try
            {
                var filePath = GetWaypointsFilePath();
                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);

                    _logger.LogInformation("Deleted tour waypoints for project {ProjectId} by user {User}", 
                        _projectContext.ProjectId, User.Identity?.Name);
                }

                return Ok(new { message = "Tour waypoints deleted", projectId = _projectContext.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tour waypoints");
                return StatusCode(500, "Error deleting tour waypoints");
            }
        }

        private string GetWaypointsFilePath()
        {
            return Path.Combine(_projectContext.ConfigPath, TOUR_WAYPOINTS_FILENAME);
        }

        private static bool IsValidWaypoint(TourWaypoint wp)
        {
            return double.IsFinite(wp.Alpha) &&
                   double.IsFinite(wp.Beta) &&
                   double.IsFinite(wp.Radius) &&
                   wp.Target != null &&
                   double.IsFinite(wp.Target.X) &&
                   double.IsFinite(wp.Target.Y) &&
                   double.IsFinite(wp.Target.Z);
        }
    }

    #region DTOs

    public class TourWaypoint
    {
        public string Type { get; set; } = "arc"; // "arc" or "free"
        public double Alpha { get; set; }
        public double Beta { get; set; }
        public double Radius { get; set; }
        public TourVector3 Target { get; set; } = new();
        public TourVector3? Position { get; set; } // Only for "free" type
    }

    public class TourVector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class TourWaypointsResponse
    {
        public List<TourWaypoint> Waypoints { get; set; } = new();
        public string ProjectId { get; set; } = "";
    }

    public class SaveWaypointsRequest
    {
        public List<TourWaypoint> Waypoints { get; set; } = new();
    }

    #endregion
}
