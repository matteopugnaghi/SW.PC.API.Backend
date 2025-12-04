using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using System.Text.Json;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🔐 API para gestión de integridad del software y certificados EU CRA
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class IntegrityController : ControllerBase
    {
        private readonly ISoftwareIntegrityService _integrityService;
        private readonly ILogger<IntegrityController> _logger;

        public IntegrityController(
            ISoftwareIntegrityService integrityService,
            ILogger<IntegrityController> logger)
        {
            _integrityService = integrityService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener estado actual de integridad del software
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetIntegrityStatus()
        {
            var info = _integrityService.GetSoftwareVersionInfo();
            return Ok(info);
        }

        /// <summary>
        /// Verificar conectividad a internet y estado de sincronización con repositorios remotos
        /// </summary>
        [HttpGet("network-status")]
        public async Task<IActionResult> GetNetworkSyncStatus()
        {
            _logger.LogInformation("🌐 Checking network and sync status...");
            var status = await _integrityService.CheckNetworkAndSyncStatusAsync();
            return Ok(status);
        }

        /// <summary>
        /// Forzar re-verificación de integridad de todos los componentes
        /// </summary>
        /// <remarks>
        /// TODO: Cuando se implemente autenticación:
        /// - Añadir [Authorize(Roles = "Admin,Auditor")]
        /// - Obtener usuario del JWT: User.Identity.Name
        /// - Registrar en audit log: quién, cuándo, IP, resultado
        /// </remarks>
        [HttpPost("verify")]
        // TODO: [Authorize(Roles = "Admin,Auditor")]
        public async Task<IActionResult> VerifyIntegrity([FromBody] ManualVerifyRequest? request = null)
        {
            var verifiedBy = request?.VerifiedBy ?? "Anonymous";
            _logger.LogInformation("🔐 Manual integrity verification requested by: {User}", verifiedBy);
            
            // Registrar quién hizo la verificación manual
            _integrityService.RegisterAdminVerification(verifiedBy);
            
            var result = await _integrityService.VerifyAllIntegrityAsync();
            var info = _integrityService.GetSoftwareVersionInfo();

            return Ok(new
            {
                success = result,
                systemStatus = info.SystemStatus,
                verifiedAt = info.LastVerificationDate,
                verifiedBy = info.VerifiedByAdmin,
                components = new
                {
                    backend = new { info.Backend.Integrity, info.Backend.WorkingDirStatus },
                    frontend = new { info.Frontend.Integrity, info.Frontend.WorkingDirStatus },
                    twinCatPlc = new { info.TwinCatPlc.Integrity, info.TwinCatPlc.WorkingDirStatus }
                }
            });
        }

        /// <summary>
        /// Generar certificado de integridad firmado (para uso offline y auditorías)
        /// </summary>
        [HttpPost("certificate/generate")]
        public async Task<IActionResult> GenerateCertificate([FromBody] GenerateCertificateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MachineId))
            {
                return BadRequest(new { error = "MachineId is required" });
            }

            if (string.IsNullOrWhiteSpace(request.OperatorName))
            {
                return BadRequest(new { error = "OperatorName is required" });
            }

            _logger.LogInformation("📜 Generating integrity certificate for machine: {MachineId}, operator: {Operator}",
                request.MachineId, request.OperatorName);

            var certificate = await _integrityService.GenerateIntegrityCertificateAsync(
                request.MachineId, request.OperatorName);

            return Ok(certificate);
        }

        /// <summary>
        /// Descargar certificado como archivo JSON
        /// </summary>
        [HttpPost("certificate/download")]
        public async Task<IActionResult> DownloadCertificate([FromBody] GenerateCertificateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MachineId))
            {
                return BadRequest(new { error = "MachineId is required" });
            }

            var certificate = await _integrityService.GenerateIntegrityCertificateAsync(
                request.MachineId, request.OperatorName ?? "System");

            var json = JsonSerializer.Serialize(certificate, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"integrity_certificate_{request.MachineId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            return File(bytes, "application/json", fileName);
        }

        /// <summary>
        /// Verificar firma de un certificado existente
        /// </summary>
        [HttpPost("certificate/verify")]
        public IActionResult VerifyCertificate([FromBody] IntegrityCertificate certificate)
        {
            if (certificate == null || string.IsNullOrWhiteSpace(certificate.CertificateId))
            {
                return BadRequest(new { error = "Invalid certificate" });
            }

            var isValid = _integrityService.VerifyCertificateSignature(certificate);

            return Ok(new
            {
                certificateId = certificate.CertificateId,
                signatureValid = isValid,
                verifiedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                message = isValid 
                    ? "✅ Certificate signature is valid" 
                    : "❌ Certificate signature is INVALID - may have been tampered"
            });
        }

        /// <summary>
        /// Registrar verificación manual por administrador
        /// </summary>
        [HttpPost("admin-verify")]
        public IActionResult RegisterAdminVerification([FromBody] AdminVerificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AdminUser))
            {
                return BadRequest(new { error = "AdminUser is required" });
            }

            _integrityService.RegisterAdminVerification(request.AdminUser);
            var info = _integrityService.GetSoftwareVersionInfo();

            return Ok(new
            {
                success = true,
                verifiedBy = request.AdminUser,
                verifiedAt = info.LastVerificationDate,
                systemStatus = info.SystemStatus
            });
        }
    }

    #region Request DTOs

    public class GenerateCertificateRequest
    {
        public string MachineId { get; set; } = "";
        public string? OperatorName { get; set; }
    }

    public class AdminVerificationRequest
    {
        public string AdminUser { get; set; } = "";
    }

    public class ManualVerifyRequest
    {
        public string VerifiedBy { get; set; } = "Anonymous";
    }

    #endregion
}
