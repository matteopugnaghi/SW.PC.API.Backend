// 💻 IPC Info Controller - Hardware and OS Information API
// Provides system information for Industrial PC monitoring

using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// IPC System Information API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IpcInfoController : ControllerBase
{
    private readonly IIpcInfoService _ipcInfoService;
    private readonly IExcelConfigService _excelConfigService;
    private readonly ILogger<IpcInfoController> _logger;

    public IpcInfoController(
        IIpcInfoService ipcInfoService,
        IExcelConfigService excelConfigService,
        ILogger<IpcInfoController> logger)
    {
        _ipcInfoService = ipcInfoService;
        _excelConfigService = excelConfigService;
        _logger = logger;
    }

    /// <summary>
    /// Get IPC Info configuration status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStatus()
    {
        try
        {
            var config = await _excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm");
            return Ok(new
            {
                isEnabled = config.IpcInfoEnabled,
                quickPollSeconds = config.IpcInfoQuickPollSeconds,
                fullPollMinutes = config.IpcInfoFullPollMinutes,
                statusMessage = config.IpcInfoEnabled 
                    ? $"IPC monitoring enabled (Quick: {config.IpcInfoQuickPollSeconds}s, Full: {config.IpcInfoFullPollMinutes}m)"
                    : "IPC monitoring disabled. Set IpcInfoEnabled=true in Excel."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IPC info status");
            return Ok(new
            {
                isEnabled = true, // Default enabled
                quickPollSeconds = 30,
                fullPollMinutes = 5,
                statusMessage = "Using default config (Excel not available)"
            });
        }
    }

    /// <summary>
    /// Get complete IPC system information
    /// </summary>
    /// <returns>Full system info including OS, CPU, RAM, Disk, Network, Security</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IpcSystemInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<IpcSystemInfo>> GetSystemInfo()
    {
        try
        {
            // Check if enabled
            var config = await _excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm");
            if (!config.IpcInfoEnabled)
            {
                return Ok(new { 
                    error = "IPC Info disabled",
                    message = "Set IpcInfoEnabled=true in Excel SystemConfig sheet",
                    isEnabled = false
                });
            }

            var info = await _ipcInfoService.GetSystemInfoAsync();
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IPC system info");
            return StatusCode(500, new { error = "Failed to get system info", message = ex.Message });
        }
    }

    /// <summary>
    /// Get quick status (CPU, RAM, Disk usage) - for frequent polling
    /// </summary>
    /// <returns>Quick status with usage percentages</returns>
    [HttpGet("quick")]
    [ProducesResponseType(typeof(IpcQuickStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<IpcQuickStatus>> GetQuickStatus()
    {
        try
        {
            var status = await _ipcInfoService.GetQuickStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quick status");
            return StatusCode(500, new { error = "Failed to get quick status", message = ex.Message });
        }
    }

    /// <summary>
    /// Get OS information only
    /// </summary>
    [HttpGet("os")]
    [ProducesResponseType(typeof(OsInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<OsInfo>> GetOsInfo()
    {
        try
        {
            var info = await _ipcInfoService.GetSystemInfoAsync();
            return Ok(info.OperatingSystem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OS info");
            return StatusCode(500, new { error = "Failed to get OS info", message = ex.Message });
        }
    }

    /// <summary>
    /// Get security status only
    /// </summary>
    [HttpGet("security")]
    [ProducesResponseType(typeof(SecurityInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecurityInfo>> GetSecurityInfo()
    {
        try
        {
            var info = await _ipcInfoService.GetSystemInfoAsync();
            return Ok(info.Security);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security info");
            return StatusCode(500, new { error = "Failed to get security info", message = ex.Message });
        }
    }

    /// <summary>
    /// Get network information only
    /// </summary>
    [HttpGet("network")]
    [ProducesResponseType(typeof(NetworkInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<NetworkInfo>> GetNetworkInfo()
    {
        try
        {
            var info = await _ipcInfoService.GetSystemInfoAsync();
            return Ok(info.Network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network info");
            return StatusCode(500, new { error = "Failed to get network info", message = ex.Message });
        }
    }
}
