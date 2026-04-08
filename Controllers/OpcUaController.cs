using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.OpcUa;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🌐 OPC/UA Server diagnostics and management API
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OpcUaController : ControllerBase
    {
        private readonly IOpcUaServerService _opcUaService;
        private readonly ILogger<OpcUaController> _logger;

        public OpcUaController(
            IOpcUaServerService opcUaService,
            ILogger<OpcUaController> logger)
        {
            _opcUaService = opcUaService;
            _logger = logger;
        }

        /// <summary>
        /// Get OPC/UA server status and runtime information
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(OpcUaServerStatus), StatusCodes.Status200OK)]
        public ActionResult<OpcUaServerStatus> GetStatus()
        {
            var status = _opcUaService.GetStatus();
            return Ok(status);
        }

        /// <summary>
        /// Get OPC/UA server configuration (from Excel)
        /// </summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult GetConfig()
        {
            var config = _opcUaService.GetConfig();
            // Don't expose password
            return Ok(new
            {
                config.Enabled,
                config.Port,
                config.ServerUri,
                config.ServerName,
                config.CertificateMode,
                config.SecurityPolicy,
                config.SecurityMode,
                config.AllowAnonymous,
                hasUserCredentials = !string.IsNullOrEmpty(config.UserName),
                config.CrlCheckEnabled,
                hasCrlUrl = !string.IsNullOrEmpty(config.CrlUrl),
                config.CrlCheckInterval,
                hasCaCert = !string.IsNullOrEmpty(config.CaCertPath),
                hasCertificate = !string.IsNullOrEmpty(config.CertificatePath),
                // SFTP
                config.SftpEnabled,
                hasSftpHost = !string.IsNullOrEmpty(config.SftpHost),
                config.SftpPort,
                hasSftpKey = !string.IsNullOrEmpty(config.SftpKeyPath),
                config.SftpSyncInterval,
                config.ConfigWarnings
            });
        }

        /// <summary>
        /// Get list of published OPC/UA variables
        /// </summary>
        [HttpGet("variables")]
        [ProducesResponseType(typeof(List<OpcUaVariable>), StatusCodes.Status200OK)]
        public ActionResult<List<OpcUaVariable>> GetVariables()
        {
            return Ok(_opcUaService.GetVariables());
        }

        /// <summary>
        /// Get list of published OPC/UA alarms
        /// </summary>
        [HttpGet("alarms")]
        [ProducesResponseType(typeof(List<OpcUaAlarm>), StatusCodes.Status200OK)]
        public ActionResult<List<OpcUaAlarm>> GetAlarms()
        {
            return Ok(_opcUaService.GetAlarms());
        }

        /// <summary>
        /// Get connected OPC/UA clients
        /// </summary>
        [HttpGet("clients")]
        [ProducesResponseType(typeof(List<OpcUaClientInfo>), StatusCodes.Status200OK)]
        public ActionResult<List<OpcUaClientInfo>> GetClients()
        {
            var status = _opcUaService.GetStatus();
            return Ok(status.Clients);
        }

        /// <summary>
        /// Get current live values of all OPC/UA variable and alarm nodes
        /// </summary>
        [HttpGet("values")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        public ActionResult GetCurrentValues()
        {
            return Ok(_opcUaService.GetCurrentValues());
        }

        /// <summary>
        /// Quick check: is OPC/UA enabled?
        /// </summary>
        [HttpGet("enabled")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult IsEnabled()
        {
            return Ok(new { enabled = _opcUaService.IsEnabled });
        }
    }
}
