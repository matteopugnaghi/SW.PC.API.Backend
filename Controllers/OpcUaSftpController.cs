using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🔐 OPC/UA SFTP Certificate Exchange API (Phase 2)
    /// </summary>
    [Route("api/opcua/sftp")]
    [ApiController]
    [Authorize]
    public class OpcUaSftpController : ControllerBase
    {
        private readonly IOpcUaSftpService _sftpService;
        private readonly IOpcUaCertificateService _certService;
        private readonly ILogger<OpcUaSftpController> _logger;

        public OpcUaSftpController(
            IOpcUaSftpService sftpService,
            IOpcUaCertificateService certService,
            ILogger<OpcUaSftpController> logger)
        {
            _sftpService = sftpService;
            _certService = certService;
            _logger = logger;
        }

        /// <summary>
        /// Get SFTP connection status and configuration
        /// </summary>
        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            return Ok(_sftpService.GetStatus());
        }

        /// <summary>
        /// Test SFTP connection with current configuration
        /// </summary>
        [HttpPost("test")]
        public async Task<ActionResult> TestConnection()
        {
            var result = await _sftpService.TestConnectionAsync();
            return result.Success ? Ok(result) : StatusCode(502, result);
        }

        /// <summary>
        /// Upload our server certificate (.DER) to SFTP server for Alstom to import
        /// </summary>
        [HttpPost("upload-cert")]
        public async Task<ActionResult> UploadOwnCertificate()
        {
            var certBytes = await _certService.ExportOwnCertificateDerAsync();
            if (certBytes == null || certBytes.Length == 0)
                return BadRequest(new { error = "No server certificate available to upload" });

            var fileName = $"AquafrischServer_{DateTime.UtcNow:yyyyMMdd}.der";
            var result = await _sftpService.UploadCertificateAsync(certBytes, fileName);
            return result.Success ? Ok(result) : StatusCode(502, result);
        }

        /// <summary>
        /// List files in remote SFTP certificate folder
        /// </summary>
        [HttpGet("files")]
        public async Task<ActionResult> ListRemoteFiles()
        {
            var result = await _sftpService.ListRemoteFilesAsync();
            return result.Success ? Ok(result) : StatusCode(502, result);
        }

        /// <summary>
        /// Download a file from SFTP and optionally import it as trusted certificate
        /// </summary>
        [HttpPost("download")]
        public async Task<ActionResult> DownloadFile([FromQuery] string fileName, [FromQuery] bool importAsTrusted = false)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest(new { error = "fileName is required" });

            // Sanitize filename
            fileName = Path.GetFileName(fileName);

            var result = await _sftpService.DownloadFileAsync(fileName);
            if (!result.Success || result.Data == null)
                return StatusCode(502, new { error = result.Message });

            if (importAsTrusted)
            {
                try
                {
                    var certInfo = await _certService.ImportTrustedCertificateAsync(result.Data, fileName);
                    return Ok(new
                    {
                        download = result.Message,
                        import_ = "Imported as trusted",
                        importSuccess = true,
                        certificate = certInfo
                    });
                }
                catch (InvalidOperationException alreadyEx) when (alreadyEx.Message.Contains("already trusted"))
                {
                    return Ok(new
                    {
                        download = result.Message,
                        import_ = alreadyEx.Message,
                        importSuccess = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import downloaded certificate as trusted");
                    return Ok(new
                    {
                        download = result.Message,
                        import_ = ex.Message,
                        importSuccess = false
                    });
                }
            }

            // Return the file for manual handling
            return File(result.Data, "application/x-x509-ca-cert", fileName);
        }
    }
}
