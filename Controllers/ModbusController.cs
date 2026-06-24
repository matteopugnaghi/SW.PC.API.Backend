using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.Modbus;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 📡 Modbus TCP diagnostics API (status / variables / alarms / values).
    /// Read-only — mirrors OpcUaController. When disabled, returns { enabled = false }.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ModbusController : ControllerBase
    {
        private readonly IModbusService _modbusService;
        private readonly ILogger<ModbusController> _logger;

        public ModbusController(IModbusService modbusService, ILogger<ModbusController> logger)
        {
            _modbusService = modbusService;
            _logger = logger;
        }

        /// <summary>Modbus runtime status (server + external sources).</summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ModbusStatus), StatusCodes.Status200OK)]
        public ActionResult<ModbusStatus> GetStatus()
        {
            if (!_modbusService.IsEnabled)
                return Ok(new ModbusStatus { Enabled = false, ServerRunning = false, StatusMessage = "Disabled" });
            return Ok(_modbusService.GetStatus());
        }

        /// <summary>Modbus configuration (from Excel). No secrets exposed.</summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult GetConfig()
        {
            var c = _modbusService.GetConfig();
            return Ok(new
            {
                c.Enabled,
                c.ServerBindIp,
                c.ServerPort,
                c.ServerUnitId,
                c.ServerAddressOffset,
                c.PollIntervalMs,
                Sources = c.Sources.Select(s => new { s.Id, s.Host, s.Port, s.UnitId }),
                c.ConfigWarnings
            });
        }

        /// <summary>Loaded Modbus variables (register mapping).</summary>
        [HttpGet("variables")]
        [ProducesResponseType(typeof(List<ModbusVariable>), StatusCodes.Status200OK)]
        public ActionResult<List<ModbusVariable>> GetVariables()
        {
            if (!_modbusService.IsEnabled) return Ok(new { enabled = false });
            return Ok(_modbusService.GetVariables());
        }

        /// <summary>Loaded Modbus alarms.</summary>
        [HttpGet("alarms")]
        [ProducesResponseType(typeof(List<ModbusAlarm>), StatusCodes.Status200OK)]
        public ActionResult<List<ModbusAlarm>> GetAlarms()
        {
            if (!_modbusService.IsEnabled) return Ok(new { enabled = false });
            return Ok(_modbusService.GetAlarms());
        }

        /// <summary>Current engineering values of all Modbus variables/alarms.</summary>
        [HttpGet("values")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        public ActionResult GetValues()
        {
            if (!_modbusService.IsEnabled) return Ok(new { enabled = false });
            return Ok(_modbusService.GetCurrentValues());
        }
    }
}
