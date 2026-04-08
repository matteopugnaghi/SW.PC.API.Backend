// ============================================================================
// OpcUaCertificateController.cs - OPC UA Certificate Trust Management API
// ============================================================================
// Phase 1 (until June 2027): Self-signed certificates with manual .DER exchange
//
// Endpoints:
//   GET  /api/opcua/certificates/own           → Download own cert (.DER)
//   GET  /api/opcua/certificates/own/info       → Own cert metadata
//   GET  /api/opcua/certificates/{store}        → List certs in store
//   POST /api/opcua/certificates/trusted        → Import trusted cert (.DER)
//   POST /api/opcua/certificates/approve/{thumb}→ Move rejected → trusted
//   DELETE /api/opcua/certificates/{store}/{thumb} → Remove cert
//
// Alstom requirement: Manual .DER exchange between OPC UA peers
// Reference: P006-ALS-TRANS-SPT-SYS-CYBER-06117-C
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.OpcUa;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [Route("api/opcua/certificates")]
    [ApiController]
    [Authorize]
    public class OpcUaCertificateController : ControllerBase
    {
        private readonly IOpcUaCertificateService _certService;
        private readonly ILogger<OpcUaCertificateController> _logger;

        // Max upload size: 10 KB (DER certificates are typically 1-3 KB)
        private const int MaxCertSizeBytes = 10 * 1024;

        public OpcUaCertificateController(
            IOpcUaCertificateService certService,
            ILogger<OpcUaCertificateController> logger)
        {
            _certService = certService;
            _logger = logger;
        }

        /// <summary>
        /// Download own OPC UA application certificate in DER format.
        /// This is the certificate that must be imported as trusted by OPC UA clients (e.g., ATS/SCADA).
        /// </summary>
        [HttpGet("own")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadOwnCertificate()
        {
            var derBytes = await _certService.ExportOwnCertificateDerAsync();
            if (derBytes == null)
                return NotFound(new { error = "OPC UA application certificate not found. Start the OPC UA server first." });

            var hostname = Environment.MachineName ?? "aquafrisch";
            return File(derBytes, "application/x-x509-ca-cert", $"opcua-{hostname}.der");
        }

        /// <summary>
        /// Get metadata about our own OPC UA application certificate.
        /// </summary>
        [HttpGet("own/info")]
        [ProducesResponseType(typeof(OpcUaCertificateInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OpcUaCertificateInfo>> GetOwnCertificateInfo()
        {
            var info = await _certService.GetOwnCertificateInfoAsync();
            if (info == null)
                return NotFound(new { error = "OPC UA application certificate not found." });

            return Ok(info);
        }

        /// <summary>
        /// List certificates in a specific store (trusted, rejected, issuers).
        /// </summary>
        [HttpGet("{store}")]
        [ProducesResponseType(typeof(List<OpcUaCertificateInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<OpcUaCertificateInfo>>> ListCertificates(string store)
        {
            if (!IsValidStore(store))
                return BadRequest(new { error = $"Invalid store: '{store}'. Valid stores: trusted, rejected, issuers" });

            var certs = await _certService.ListCertificatesAsync(store);
            return Ok(certs);
        }

        /// <summary>
        /// Import a trusted client certificate (DER/CER format).
        /// This is the Alstom Phase 1 workflow: manual certificate exchange.
        /// The uploaded certificate will be added to the trusted peers store.
        /// </summary>
        [HttpPost("trusted")]
        [ProducesResponseType(typeof(OpcUaCertificateInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [RequestSizeLimit(MaxCertSizeBytes)]
        public async Task<ActionResult<OpcUaCertificateInfo>> ImportTrustedCertificate(
            IFormFile file,
            [FromForm] string? label = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            if (file.Length > MaxCertSizeBytes)
                return BadRequest(new { error = $"File too large. Maximum: {MaxCertSizeBytes / 1024} KB." });

            // Validate file extension
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".der" or ".cer" or ".crt"))
                return BadRequest(new { error = "Invalid file type. Accepted: .der, .cer, .crt" });

            byte[] derBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                derBytes = ms.ToArray();
            }

            try
            {
                var info = await _certService.ImportTrustedCertificateAsync(derBytes, label);
                _logger.LogInformation("🔐 Trusted certificate imported via API: {Subject}", info.Subject);
                return Ok(info);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Approve a rejected certificate (move from rejected → trusted store).
        /// When AutoAcceptUntrustedCertificates is false, unknown client certs
        /// are placed in the rejected store. This endpoint approves them.
        /// </summary>
        [HttpPost("approve/{thumbprint}")]
        [ProducesResponseType(typeof(OpcUaCertificateInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OpcUaCertificateInfo>> ApproveCertificate(string thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint) || thumbprint.Length > 64)
                return BadRequest(new { error = "Invalid thumbprint." });

            // Sanitize: thumbprint should be hex only
            if (!thumbprint.All(c => "0123456789abcdefABCDEF".Contains(c)))
                return BadRequest(new { error = "Thumbprint must be hexadecimal." });

            var info = await _certService.ApproveCertificateAsync(thumbprint);
            if (info == null)
                return NotFound(new { error = $"No rejected certificate found with thumbprint: {thumbprint}" });

            return Ok(info);
        }

        /// <summary>
        /// Remove a certificate from a store by thumbprint.
        /// Cannot remove from 'own' store (server's own certificate).
        /// </summary>
        [HttpDelete("{store}/{thumbprint}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveCertificate(string store, string thumbprint)
        {
            if (!IsValidStore(store) || store.Equals("own", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Cannot remove from 'own' store. Valid: trusted, rejected, issuers" });

            if (string.IsNullOrWhiteSpace(thumbprint) || !thumbprint.All(c => "0123456789abcdefABCDEF".Contains(c)))
                return BadRequest(new { error = "Invalid thumbprint format." });

            try
            {
                var removed = await _certService.RemoveCertificateAsync(store, thumbprint);
                if (!removed)
                    return NotFound(new { error = $"Certificate not found in {store} store." });

                return Ok(new { message = $"Certificate removed from {store} store.", thumbprint });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get a summary of all certificate stores (counts + own cert info).
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetCertificateSummary()
        {
            var own = await _certService.GetOwnCertificateInfoAsync();
            var trusted = await _certService.ListCertificatesAsync("trusted");
            var rejected = await _certService.ListCertificatesAsync("rejected");
            var issuers = await _certService.ListCertificatesAsync("issuers");
            var crls = _certService.GetCrlFiles();

            // Cross-reference: mark trusted certs as revoked if in any CRL
            _certService.MarkRevokedCertificates(trusted, crls);

            return Ok(new
            {
                ownCertificate = own,
                trustedCount = trusted.Count,
                rejectedCount = rejected.Count,
                issuersCount = issuers.Count,
                crlCount = crls.Count,
                trusted,
                rejected,
                issuers,
                crls,
                storePath = _certService.GetStorePath("own").Replace("\\own", "")
            });
        }

        private static bool IsValidStore(string store)
        {
            return store.ToLowerInvariant() is "trusted" or "rejected" or "issuers" or "own";
        }
    }
}
