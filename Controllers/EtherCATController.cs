using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.EtherCAT;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🌐 API para diagnóstico de topología EtherCAT
    /// Proporciona información de la red EtherCAT para visualización en el frontend.
    /// 
    /// OPTIMIZACIÓN: Solo lee cuando se solicita (no polling automático)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EtherCATController : ControllerBase
    {
        private readonly IEtherCATDiagnosticsService _etherCATService;
        private readonly IESIParserService _esiParser;
        private readonly ILogger<EtherCATController> _logger;

        public EtherCATController(
            IEtherCATDiagnosticsService etherCATService,
            IESIParserService esiParser,
            ILogger<EtherCATController> logger)
        {
            _etherCATService = etherCATService;
            _esiParser = esiParser;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el estado de configuración del diagnóstico EtherCAT
        /// </summary>
        [HttpGet("config")]
        public ActionResult<EtherCATConfigResponse> GetConfiguration()
        {
            var config = _etherCATService.GetConfiguration();
            
            return Ok(new EtherCATConfigResponse
            {
                IsEnabled = _etherCATService.IsEnabled,
                MasterNetId = config.EtherCATMasterNetId,
                MasterDeviceId = config.EtherCATMasterDeviceId,
                ESIFilesPath = config.ESIFilesPath,
                UseESIFiles = config.UseESIFiles,
                TopologyReadIntervalMs = config.TopologyReadIntervalMs
            });
        }

        /// <summary>
        /// Obtiene el resumen rápido de la red EtherCAT (para panel compacto)
        /// Ligero y rápido, ideal para mostrar en InfoPanel
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<EtherCATSummary>> GetSummary()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Offline,
                    MasterStateText = "Disabled"
                });
            }

            try
            {
                var summary = await _etherCATService.GetSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting EtherCAT summary");
                return Ok(new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Error,
                    MasterStateText = "Error"
                });
            }
        }

        /// <summary>
        /// Obtiene la topología completa de la red EtherCAT
        /// Incluye todos los esclavos, conexiones y grafo para visualización
        /// NOTA: Esta llamada es más pesada, usar solo cuando se muestra el modal
        /// </summary>
        [HttpGet("topology")]
        public async Task<ActionResult<EtherCATTopology>> GetTopology()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = "EtherCAT diagnostics not enabled in configuration",
                    Timestamp = DateTime.Now,
                    Summary = new EtherCATSummary
                    {
                        OverallHealth = NetworkHealth.Offline,
                        MasterStateText = "Disabled"
                    }
                });
            }

            try
            {
                _logger.LogInformation("🌐 EtherCAT topology requested");
                var topology = await _etherCATService.GetTopologyAsync();
                return Ok(topology);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting EtherCAT topology");
                return StatusCode(500, new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = $"Error reading topology: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Obtiene información detallada de un esclavo específico
        /// </summary>
        [HttpGet("slave/{address:int}")]
        public async Task<ActionResult<EtherCATSlaveNode>> GetSlaveInfo(int address)
        {
            if (!_etherCATService.IsEnabled)
            {
                return NotFound("EtherCAT diagnostics not enabled");
            }

            try
            {
                var slave = await _etherCATService.GetSlaveInfoAsync((ushort)address);
                if (slave == null)
                {
                    return NotFound($"Slave at address {address} not found");
                }
                return Ok(slave);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slave info for address {Address}", address);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene solo la lista de esclavos sin el grafo completo
        /// Útil para vistas de lista/tabla
        /// </summary>
        [HttpGet("slaves")]
        public async Task<ActionResult<List<EtherCATSlaveNode>>> GetSlaves()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new List<EtherCATSlaveNode>());
            }

            try
            {
                var topology = await _etherCATService.GetTopologyAsync();
                return Ok(topology.Slaves);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slaves list");
                return StatusCode(500, new List<EtherCATSlaveNode>());
            }
        }

        /// <summary>
        /// Fuerza una nueva lectura de la topología (invalida cache)
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<EtherCATTopology>> RefreshTopology()
        {
            if (!_etherCATService.IsEnabled)
            {
                return BadRequest("EtherCAT diagnostics not enabled");
            }

            try
            {
                _logger.LogInformation("🌐 EtherCAT topology refresh requested");
                _etherCATService.InvalidateCache();
                var topology = await _etherCATService.GetTopologyAsync();
                return Ok(topology);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing EtherCAT topology");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene solo los contadores de errores de todos los esclavos
        /// Útil para vista de diagnóstico rápido
        /// </summary>
        [HttpGet("errors")]
        public async Task<ActionResult<List<SlaveErrorSummary>>> GetErrors()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new List<SlaveErrorSummary>());
            }

            try
            {
                var topology = await _etherCATService.GetTopologyAsync();
                var errors = topology.Slaves
                    .Where(s => s.ErrorCounters.HasErrors || s.State.HasError())
                    .Select(s => new SlaveErrorSummary
                    {
                        Position = s.Position,
                        Address = s.ConfiguredAddress,
                        Name = s.Name,
                        State = s.State.ToShortString(),
                        StateColor = s.State.ToColorCode(),
                        ALStatusCode = s.ALStatusCode,
                        ALStatusDescription = s.ALStatusDescription,
                        CRCErrors = s.ErrorCounters.CRCErrorCount,
                        LostLinks = s.ErrorCounters.LostLinkCount,
                        TotalErrors = s.ErrorCounters.TotalErrors
                    })
                    .ToList();

                return Ok(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error counters");
                return StatusCode(500, new List<SlaveErrorSummary>());
            }
        }

        /// <summary>
        /// Obtiene estadísticas del cache de archivos ESI
        /// Muestra cuántos dispositivos/vendors están cargados desde los archivos de TwinCAT
        /// </summary>
        [HttpGet("esi/stats")]
        public ActionResult<ESICacheStats> GetESIStats()
        {
            try
            {
                var stats = _esiParser.GetCacheStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ESI cache stats");
                return StatusCode(500, new ESICacheStats { Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Fuerza recarga del cache de archivos ESI
        /// </summary>
        [HttpPost("esi/refresh")]
        public async Task<ActionResult<ESICacheStats>> RefreshESICache()
        {
            try
            {
                _logger.LogInformation("🌐 ESI cache refresh requested");
                await _esiParser.RefreshCacheAsync();
                var stats = _esiParser.GetCacheStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing ESI cache");
                return StatusCode(500, new ESICacheStats { Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Busca información de un dispositivo por VendorId y ProductCode
        /// </summary>
        [HttpGet("esi/device/{vendorId:long}/{productCode:long}")]
        public ActionResult<ESIDeviceInfo> GetESIDeviceInfo(long vendorId, long productCode)
        {
            try
            {
                var info = _esiParser.GetDeviceInfo((uint)vendorId, (uint)productCode);
                if (info == null)
                {
                    return NotFound($"Device not found: VendorId=0x{vendorId:X4}, ProductCode=0x{productCode:X8}");
                }
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ESI device info");
                return StatusCode(500, ex.Message);
            }
        }
    }

    // ===== DTOs =====

    public class EtherCATConfigResponse
    {
        public bool IsEnabled { get; set; }
        public string MasterNetId { get; set; } = "";
        public int MasterDeviceId { get; set; }
        public string ESIFilesPath { get; set; } = "";
        public bool UseESIFiles { get; set; }
        public int TopologyReadIntervalMs { get; set; }
    }

    public class SlaveErrorSummary
    {
        public ushort Position { get; set; }
        public ushort Address { get; set; }
        public string Name { get; set; } = "";
        public string State { get; set; } = "";
        public string StateColor { get; set; } = "";
        public ushort ALStatusCode { get; set; }
        public string ALStatusDescription { get; set; } = "";
        public uint CRCErrors { get; set; }
        public uint LostLinks { get; set; }
        public long TotalErrors { get; set; }
    }
}
