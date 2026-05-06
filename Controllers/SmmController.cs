// ============================================================================
// SmmController.cs — SMM (Statistics & Maintenance Module) Public API
// ============================================================================
// Decisiones FROZEN: DEC-019, DEC-022, DEC-024, DEC-026.
// Endpoint /api/smm/info expone tier AquarIA + SystemDeliveryDate + ContinuousReadTime.
// Frontend lo consume al login para renderizar badge AquarIA BASIC/PRO.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SW.PC.API.Backend.Models.Smm;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/smm")]
    public class SmmController : ControllerBase
    {
        private readonly ILogger<SmmController> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IRequestProjectContext _projectContext;
        private readonly SmmOptions _smmOptions;

        public SmmController(
            ILogger<SmmController> logger,
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext,
            IOptions<SmmOptions> smmOptions)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _smmOptions = smmOptions.Value;
        }

        /// <summary>
        /// Endpoint público (sin auth) que expone metadata SMM mínima para el frontend.
        /// DEC-022: aquariaTier permite renderizado condicional Gama 1/2.
        /// DEC-024: systemDeliveryDate usado por AquarIA G1 cuando no hay ciclos.
        /// DEC-026: continuousReadTime hora del job nocturno Continuous.
        /// </summary>
        [HttpGet("info")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInfoAsync()
        {
            string tier = string.IsNullOrWhiteSpace(_smmOptions.Tier) ? "Gama1" : _smmOptions.Tier;

            System.DateTime? systemDeliveryDate = null;
            string continuousReadTime = "03:00";
            string? projectId = _projectContext.ProjectId;

            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (!string.IsNullOrWhiteSpace(excelPath) && System.IO.File.Exists(excelPath))
                {
                    var sysCfg = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    if (sysCfg != null)
                    {
                        systemDeliveryDate = sysCfg.SystemDeliveryDate;
                        if (!string.IsNullOrWhiteSpace(sysCfg.ContinuousReadTime))
                            continuousReadTime = sysCfg.ContinuousReadTime;
                    }
                }
                else
                {
                    _logger.LogDebug("SMM info: Excel no disponible para proyecto '{Project}'. Devolviendo defaults.", projectId);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "SMM info: error leyendo Excel del proyecto '{Project}'. Devolviendo defaults.", projectId);
            }

            return Ok(new
            {
                aquariaTier = tier,
                isPro = string.Equals(tier, "Gama2", System.StringComparison.OrdinalIgnoreCase),
                projectId,
                systemDeliveryDate = systemDeliveryDate?.ToString("yyyy-MM-dd"),
                continuousReadTime
            });
        }
    }
}
