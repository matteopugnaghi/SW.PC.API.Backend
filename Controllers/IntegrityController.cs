using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models;
using System.Text.Json;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🔐 API para gestión de integridad del software y certificados EU CRA
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IntegrityController : ControllerBase
    {
        private readonly ISoftwareIntegrityService _integrityService;
        private readonly IAuditLogService _auditLog;
        private readonly ILogger<IntegrityController> _logger;

        public IntegrityController(
            ISoftwareIntegrityService integrityService,
            IAuditLogService auditLog,
            ILogger<IntegrityController> logger)
        {
            _integrityService = integrityService;
            _auditLog = auditLog;
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
        /// Diagnóstico: ver las rutas de repositorios detectadas
        /// </summary>
        [HttpGet("repo-paths")]
        public IActionResult GetRepoPaths()
        {
            var paths = _integrityService.GetRepositoryPaths();
            return Ok(new
            {
                backend = new { path = paths.Backend, exists = Directory.Exists(paths.Backend), hasGit = Directory.Exists(Path.Combine(paths.Backend ?? "", ".git")) },
                frontend = new { path = paths.Frontend, exists = Directory.Exists(paths.Frontend), hasGit = Directory.Exists(Path.Combine(paths.Frontend ?? "", ".git")) },
                twincat = new { path = paths.TwinCAT, exists = Directory.Exists(paths.TwinCAT), hasGit = Directory.Exists(Path.Combine(paths.TwinCAT ?? "", ".git")) }
            });
        }

        /// <summary>
        /// Forzar re-verificación de integridad de todos los componentes
        /// </summary>
        [HttpPost("verify")]
        [Authorize(Roles = "Administrator,Auditor")]
        public async Task<IActionResult> VerifyIntegrity([FromBody] ManualVerifyRequest? request = null)
        {
            var verifiedBy = request?.VerifiedBy ?? "Anonymous";
            _logger.LogInformation("🔐 Manual integrity verification requested by: {User}", verifiedBy);
            
            // Registrar quién hizo la verificación manual
            _integrityService.RegisterAdminVerification(verifiedBy);
            
            var result = await _integrityService.VerifyAllIntegrityAsync();
            var info = _integrityService.GetSoftwareVersionInfo();
            
            // 📋 AUDIT LOG: Manual Integrity Verification
            await _auditLog.LogAsync(
                AuditCategory.Integrity,
                AuditAction.IntegrityVerify,
                result ? AuditResult.Success : AuditResult.Warning,
                $"Verificación manual de integridad por {verifiedBy}: {(result ? "PASADA" : "CON ADVERTENCIAS")}",
                null, verifiedBy);

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

            // 📋 AUDIT LOG: Certificate Generation
            await _auditLog.LogAsync(
                AuditCategory.Certificate,
                AuditAction.CertificateGenerate,
                AuditResult.Success,
                $"Certificado de integridad generado para máquina {request.MachineId}",
                null, request.OperatorName);

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

            // 📋 AUDIT LOG: Certificate Download
            await _auditLog.LogAsync(
                AuditCategory.Certificate,
                AuditAction.CertificateDownload,
                AuditResult.Success,
                $"Certificado de integridad descargado para máquina {request.MachineId}",
                null, request.OperatorName ?? "System");

            // Usar las mismas opciones de serialización que la API global (camelCase)
            var json = JsonSerializer.Serialize(certificate, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var fileName = $"integrity_certificate_{request.MachineId}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            return File(bytes, "application/json", fileName);
        }

        /// <summary>
        /// Verificar firma de un certificado existente
        /// </summary>
        [HttpPost("certificate/verify")]
        public async Task<IActionResult> VerifyCertificate([FromBody] IntegrityCertificate certificate)
        {
            if (certificate == null || string.IsNullOrWhiteSpace(certificate.CertificateId))
            {
                return BadRequest(new { error = "Invalid certificate" });
            }

            var isValid = _integrityService.VerifyCertificateSignature(certificate);

            // 📋 AUDIT LOG: Certificate Verification
            await _auditLog.LogAsync(
                AuditCategory.Certificate,
                AuditAction.CertificateVerify,
                isValid ? AuditResult.Success : AuditResult.Warning,
                $"Verificación de certificado {certificate.CertificateId}: {(isValid ? "VÁLIDO" : "INVÁLIDO")}",
                null, "System");

            return Ok(new
            {
                certificateId = certificate.CertificateId,
                signatureValid = isValid,
                verifiedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                message = isValid 
                    ? "✅ Certificate signature is valid" 
                    : "❌ Certificate signature is INVALID - may have been tampered"
            });
        }

        /// <summary>
        /// Registrar verificación manual por administrador
        /// </summary>
        [HttpPost("admin-verify")]
        public async Task<IActionResult> RegisterAdminVerification([FromBody] AdminVerificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AdminUser))
            {
                return BadRequest(new { error = "AdminUser is required" });
            }

            _integrityService.RegisterAdminVerification(request.AdminUser);
            var info = _integrityService.GetSoftwareVersionInfo();
            
            // 📋 AUDIT LOG: Admin Verification
            await _auditLog.LogAsync(
                AuditCategory.Integrity,
                AuditAction.IntegrityVerify,
                AuditResult.Success,
                $"Verificación administrativa registrada por {request.AdminUser}",
                null, request.AdminUser);

            return Ok(new
            {
                success = true,
                verifiedBy = request.AdminUser,
                verifiedAt = info.LastVerificationDate,
                systemStatus = info.SystemStatus
            });
        }

        /// <summary>
        /// 🔧 Endpoint de diagnóstico para verificar estado del deploy-version.json
        /// Útil para debuggear problemas de N/A en Software Integrity
        /// </summary>
        [HttpGet("diagnostic")]
        public IActionResult GetDiagnostic()
        {
            var info = _integrityService.GetSoftwareVersionInfo();
            var paths = _integrityService.GetRepositoryPaths();
            
            return Ok(new
            {
                diagnostic = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    baseDirectory = AppDomain.CurrentDomain.BaseDirectory
                },
                repositoryPaths = new
                {
                    backend = paths.Backend,
                    frontend = paths.Frontend,
                    twincat = paths.TwinCAT
                },
                backend = new
                {
                    hasData = info.Backend != null,
                    name = info.Backend?.Name,
                    version = info.Backend?.Version,
                    commitSha = info.Backend?.CommitSha,
                    integrity = info.Backend?.Integrity,
                    signatureStatus = info.Backend?.SignatureStatus,
                    branch = info.Backend?.Branch
                },
                frontend = new
                {
                    hasData = info.Frontend != null,
                    name = info.Frontend?.Name,
                    version = info.Frontend?.Version,
                    commitSha = info.Frontend?.CommitSha,
                    integrity = info.Frontend?.Integrity,
                    signatureStatus = info.Frontend?.SignatureStatus,
                    branch = info.Frontend?.Branch
                },
                systemStatus = info.SystemStatus,
                lastVerification = info.LastVerificationDate
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
