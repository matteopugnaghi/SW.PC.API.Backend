using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    public class SimulationToggleRequest
    {
        public string Key { get; set; } = string.Empty;
        /// <summary>"state" (ciclo de colores) o "translation" (movimiento min↔max)</summary>
        public string Kind { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public class SimulationSettingsRequest
    {
        public int? StateIntervalMs { get; set; }
        public int? TranslationPeriodMs { get; set; }
    }

    public class SimulationElementConfigRequest
    {
        public string Key { get; set; } = string.Empty;
        public double? Min { get; set; }
        public double? Max { get; set; }
        public int? PeriodMs { get; set; }
        public int? StateIntervalMs { get; set; }
    }

    /// <summary>
    /// Simulador de elementos 3D para demos. Solo operativo con UseSimulatedPlc=TRUE
    /// (Excel System Config). Con PLC real todos los toggles devuelven 409.
    /// </summary>
    [ApiController]
    [Route("api/simulation")]
    [Authorize]
    public class SimulationController : ControllerBase
    {
        private readonly ISimulationDriverService _sim;

        public SimulationController(ISimulationDriverService sim)
        {
            _sim = sim;
        }

        [HttpGet("elements")]
        public async Task<IActionResult> GetElements()
        {
            var targets = _sim.IsSimulated ? await _sim.GetTargetsAsync() : new List<SimulationTargetDto>();
            return Ok(new
            {
                simulated = _sim.IsSimulated,
                settings = _sim.GetSettings(),
                activeCount = _sim.ActiveCount,
                targets
            });
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] SimulationToggleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key) ||
                (request.Kind != "state" && request.Kind != "translation"))
            {
                return BadRequest(new { message = "Key y Kind (state|translation) son obligatorios" });
            }
            if (!_sim.IsSimulated)
            {
                return Conflict(new { message = "TwinCAT no está en modo simulado (UseSimulatedPlc=FALSE)" });
            }
            var ok = await _sim.SetEnabledAsync(request.Key, request.Kind, request.Enabled);
            return ok
                ? Ok(new { success = true, activeCount = _sim.ActiveCount })
                : BadRequest(new { message = $"Elemento '{request.Key}' no admite simulación de {request.Kind}" });
        }

        [HttpPost("settings")]
        public IActionResult UpdateSettings([FromBody] SimulationSettingsRequest request)
        {
            _sim.UpdateSettings(request.StateIntervalMs, request.TranslationPeriodMs);
            return Ok(new { success = true, settings = _sim.GetSettings() });
        }

        [HttpPost("element-config")]
        public async Task<IActionResult> UpdateElementConfig([FromBody] SimulationElementConfigRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                return BadRequest(new { message = "Key es obligatorio" });
            }
            await _sim.UpdateElementConfigAsync(request.Key, request.Min, request.Max, request.PeriodMs, request.StateIntervalMs);
            return Ok(new { success = true });
        }

        [HttpPost("disable-all")]
        public async Task<IActionResult> DisableAll()
        {
            await _sim.DisableAllAsync();
            return Ok(new { success = true });
        }
    }
}
