// ============================================================================
// SystemFeaturesController.cs — Feature flags globales del sistema
// ============================================================================
// Expone los toggles definidos en SystemConfig (Excel) para que el frontend
// pueda mostrar/ocultar funcionalidades de forma centralizada.
//
// Pensado para crecer: añadir nuevos flags solo requiere extender el DTO
// y el parser de ExcelConfigService.
//
// [AllowAnonymous]: el frontend lo consume al login (antes de tener token).
// No expone información sensible — solo capacidades habilitadas.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/system")]
    public class SystemFeaturesController : ControllerBase
    {
        private readonly ILogger<SystemFeaturesController> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IRequestProjectContext _projectContext;

        public SystemFeaturesController(
            ILogger<SystemFeaturesController> logger,
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
        }

        /// <summary>
        /// Devuelve los feature flags activos del sistema.
        /// El frontend lo usa para mostrar/ocultar botones de export, email, etc.
        /// </summary>
        [HttpGet("features")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeaturesAsync()
        {
            bool enableFileExport = false;
            bool enableEmailSending = false;

            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (!string.IsNullOrWhiteSpace(excelPath) && System.IO.File.Exists(excelPath))
                {
                    var sysCfg = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    if (sysCfg != null)
                    {
                        enableFileExport = sysCfg.EnableFileExport;
                        enableEmailSending = sysCfg.EnableEmailSending;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "GetFeaturesAsync: error leyendo SystemConfig, devolviendo defaults (false)");
            }

            return Ok(new
            {
                enableFileExport,
                enableEmailSending
            });
        }
    }
}
