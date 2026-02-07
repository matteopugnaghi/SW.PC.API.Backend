// ============================================================================
// SupportController.cs - "Llamar a Aquafrisch" - Soporte Técnico Remoto
// ============================================================================
// Permite a CUALQUIER usuario (incluso sin login) solicitar asistencia técnica
// y obtener un desbloqueo temporal de herramientas mediante código de respuesta.
// 
// Usa el MISMO PATRÓN que RecoveryCodeService:
// - Secret hardcodeado (solo Aquafrisch lo conoce)
// - InstallationId + Hora + Secret → genera códigos
// 
// Flujo:
// 1. Usuario ve botón "Llamar a Aquafrisch" en pantalla de login
// 2. Se muestra: Installation ID + Challenge Code (tiempo-basado)
// 3. Usuario llama/email a Aquafrisch y proporciona estos datos
// 4. Aquafrisch genera Response Code con herramienta interna (igual que recovery)
// 5. Usuario introduce Response Code → acceso temporal a herramientas
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupportController : ControllerBase
    {
        private readonly ILogger<SupportController> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IConfiguration _configuration;
        private readonly IRequestProjectContext _projectContext;
        private readonly IAuditLogService _auditLog;

        // 🔐 SECRETO CONOCIDO SOLO POR AQUAFRISCH
        // Este valor DEBE ser el mismo en:
        // - Este controller (backend)
        // - Herramienta generadora de Aquafrisch (Tools/GenerateSupportCode.ps1)
        // NUNCA compartir con el cliente
        // DIFERENTE del secret de recovery para separar responsabilidades
        private const string AQUAFRISCH_SUPPORT_SECRET = "AQF-2024-SUPP0RT-T00LS-K3Y";

        // Cache de sesiones de soporte temporales (in-memory)
        private static readonly Dictionary<string, SupportSession> _activeSessions = new();
        private static readonly object _sessionsLock = new();

        public SupportController(
            ILogger<SupportController> logger,
            IExcelConfigService excelConfigService,
            IConfiguration configuration,
            IRequestProjectContext projectContext,
            IAuditLogService auditLog)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _configuration = configuration;
            _projectContext = projectContext;
            _auditLog = auditLog;
        }

        /// <summary>
        /// Obtiene la configuración del sistema desde Excel (usa proyecto del request)
        /// </summary>
        private async Task<SystemConfiguration> GetSystemConfigAsync()
        {
            var excelPath = _projectContext.ExcelConfigPath;
            _logger.LogDebug("📁 SupportController: Cargando config desde {Path}", excelPath);
            return await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
        }

        // ========================================
        // GET: api/support/info
        // ========================================
        /// <summary>
        /// Obtiene información de soporte (público, no requiere auth)
        /// Muestra: Installation ID, Challenge Code, Teléfono, Email
        /// </summary>
        [HttpGet("info")]
        public async Task<IActionResult> GetSupportInfo()
        {
            var config = await GetSystemConfigAsync();

            if (!config.SupportUnlockEnabled)
            {
                return Ok(new
                {
                    enabled = false,
                    message = "Soporte remoto deshabilitado en esta instalación"
                });
            }

            // Generar challenge code basado en tiempo (válido por 1 hora)
            var challengeCode = GenerateChallengeCode(config.InstallationId);

            return Ok(new
            {
                enabled = true,
                installationId = config.InstallationId,
                challengeCode = challengeCode,
                phoneNumber = config.SupportPhoneNumber,
                email = config.SupportEmail,
                validForMinutes = 60, // Challenge válido por 1 hora
                message = "Llame a Aquafrisch y proporcione el ID de instalación y el código de desafío"
            });
        }

        // ========================================
        // POST: api/support/verify-code
        // ========================================
        /// <summary>
        /// Verifica el código de respuesta proporcionado por Aquafrisch
        /// Si es válido, crea una sesión temporal de acceso a herramientas
        /// </summary>
        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            var config = await GetSystemConfigAsync();

            if (!config.SupportUnlockEnabled)
            {
                return BadRequest(new { success = false, message = "Soporte remoto deshabilitado" });
            }

            if (string.IsNullOrWhiteSpace(request.ResponseCode))
            {
                return BadRequest(new { success = false, message = "Código de respuesta requerido" });
            }

            // Verificar el código de respuesta (usa secret hardcodeado, igual que RecoveryCodeService)
            var isValid = VerifyResponseCode(config.InstallationId, request.ResponseCode);
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!isValid)
            {
                _logger.LogWarning("❌ Código de soporte inválido para instalación {InstallationId}", config.InstallationId);
                
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.SupportUnlockFailed,
                    AuditResult.Failure,
                    $"Código de desbloqueo Aquafrisch inválido para instalación {config.InstallationId}",
                    ipAddress: clientIp,
                    projectId: _projectContext.ProjectId);
                
                return Unauthorized(new { success = false, message = "Código inválido o expirado" });
            }

            // Crear sesión temporal
            var sessionId = Guid.NewGuid().ToString("N")[..16].ToUpper();
            var expiresAt = DateTime.Now.AddMinutes(config.SupportUnlockDurationMinutes);

            lock (_sessionsLock)
            {
                // Limpiar sesiones expiradas
                var expiredKeys = _activeSessions
                    .Where(kvp => kvp.Value.ExpiresAt < DateTime.Now)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in expiredKeys)
                {
                    _activeSessions.Remove(key);
                }

                _activeSessions[sessionId] = new SupportSession
                {
                    SessionId = sessionId,
                    InstallationId = config.InstallationId,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = expiresAt,
                    ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                };
            }

            _logger.LogWarning("✅ Sesión de soporte creada: {SessionId} para {InstallationId}, expira: {ExpiresAt}",
                sessionId, config.InstallationId, expiresAt);
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication,
                AuditAction.SupportUnlock,
                AuditResult.Success,
                $"Acceso de soporte Aquafrisch desbloqueado para instalación {config.InstallationId}. Sesión: {sessionId}, Duración: {config.SupportUnlockDurationMinutes} min",
                ipAddress: clientIp,
                projectId: _projectContext.ProjectId);

            return Ok(new
            {
                success = true,
                sessionId = sessionId,
                expiresAt = expiresAt,
                durationMinutes = config.SupportUnlockDurationMinutes,
                message = $"Acceso temporal activado por {config.SupportUnlockDurationMinutes} minutos"
            });
        }

        // ========================================
        // GET: api/support/session/{sessionId}
        // ========================================
        /// <summary>
        /// Verifica si una sesión de soporte está activa
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public IActionResult CheckSession(string sessionId)
        {
            lock (_sessionsLock)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (session.ExpiresAt > DateTime.Now)
                    {
                        var remainingMinutes = (session.ExpiresAt - DateTime.Now).TotalMinutes;
                        return Ok(new
                        {
                            valid = true,
                            sessionId = session.SessionId,
                            expiresAt = session.ExpiresAt,
                            remainingMinutes = Math.Round(remainingMinutes, 1)
                        });
                    }
                    else
                    {
                        _activeSessions.Remove(sessionId);
                    }
                }
            }

            return Ok(new { valid = false, message = "Sesión no válida o expirada" });
        }

        // ========================================
        // POST: api/support/end-session/{sessionId}
        // ========================================
        /// <summary>
        /// Termina manualmente una sesión de soporte
        /// </summary>
        [HttpPost("end-session/{sessionId}")]
        public IActionResult EndSession(string sessionId)
        {
            lock (_sessionsLock)
            {
                if (_activeSessions.Remove(sessionId))
                {
                    _logger.LogInformation("🔒 Sesión de soporte terminada manualmente: {SessionId}", sessionId);
                    return Ok(new { success = true, message = "Sesión terminada" });
                }
            }

            return NotFound(new { success = false, message = "Sesión no encontrada" });
        }

        // ========================================
        // GET: api/support/generate-response (SOLO DESARROLLO/TESTING)
        // ========================================
        /// <summary>
        /// ⚠️ SOLO PARA DESARROLLO/TESTING
        /// Genera el código de respuesta que Aquafrisch proporcionaría
        /// En producción, esto lo haría Aquafrisch con Tools/GenerateSupportCode.ps1
        /// </summary>
        [HttpGet("generate-response")]
        public async Task<IActionResult> GenerateResponseCode()
        {
            // Solo permitir en modo desarrollo
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment != "Development")
            {
                return NotFound();
            }

            var config = await GetSystemConfigAsync();
            var challengeCode = GenerateChallengeCode(config.InstallationId);
            var responseCode = GenerateResponseCode(config.InstallationId);

            return Ok(new
            {
                warning = "⚠️ SOLO PARA DESARROLLO - Este endpoint no existe en producción",
                installationId = config.InstallationId,
                challengeCode = challengeCode,
                responseCode = responseCode,
                note = "En producción, usar Tools/GenerateSupportCode.ps1"
            });
        }

        // ========================================
        // Métodos de generación/verificación de códigos
        // Usa el mismo patrón que RecoveryCodeService
        // ========================================

        /// <summary>
        /// Genera un código de desafío basado en: InstallationId + HoraActual + Secret
        /// El código es válido durante 1 hora
        /// </summary>
        private string GenerateChallengeCode(string installationId)
        {
            // Usar la hora actual redondeada (válido por 1 hora)
            var hourSlot = DateTime.Now.ToString("yyyyMMddHH");
            var data = $"{installationId.ToUpperInvariant()}|{hourSlot}|CHALLENGE|{AQUAFRISCH_SUPPORT_SECRET}";
            
            return ComputeHmacCode(data, 6);
        }

        /// <summary>
        /// Genera el código de respuesta que Aquafrisch daría al cliente
        /// </summary>
        private string GenerateResponseCode(string installationId)
        {
            var hourSlot = DateTime.Now.ToString("yyyyMMddHH");
            var data = $"{installationId.ToUpperInvariant()}|{hourSlot}|RESPONSE|{AQUAFRISCH_SUPPORT_SECRET}";
            
            return ComputeHmacCode(data, 8);
        }

        /// <summary>
        /// Verifica si el código de respuesta es válido
        /// Acepta códigos de la hora actual y la hora anterior (para casos de transición)
        /// </summary>
        private bool VerifyResponseCode(string installationId, string providedCode)
        {
            var normalizedCode = providedCode.ToUpperInvariant().Replace("-", "").Replace(" ", "");
            
            // Verificar hora actual
            var currentSlot = DateTime.Now.ToString("yyyyMMddHH");
            var expectedCode = ComputeHmacCode(
                $"{installationId.ToUpperInvariant()}|{currentSlot}|RESPONSE|{AQUAFRISCH_SUPPORT_SECRET}", 8);
            
            if (string.Equals(normalizedCode, expectedCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Verificar hora anterior (por si generaron el código hace poco y cambió la hora)
            var previousSlot = DateTime.Now.AddHours(-1).ToString("yyyyMMddHH");
            var previousCode = ComputeHmacCode(
                $"{installationId.ToUpperInvariant()}|{previousSlot}|RESPONSE|{AQUAFRISCH_SUPPORT_SECRET}", 8);
            
            return string.Equals(normalizedCode, previousCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calcula un código HMAC-SHA256 truncado a N caracteres
        /// </summary>
        private string ComputeHmacCode(string data, int length)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AQUAFRISCH_SUPPORT_SECRET));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            
            // Convertir a string hex y truncar
            var fullHash = BitConverter.ToString(hash).Replace("-", "");
            return fullHash[..Math.Min(length, fullHash.Length)].ToUpper();
        }
    }

    // ========================================
    // Modelos de Request/Response
    // ========================================

    public class VerifyCodeRequest
    {
        public string ResponseCode { get; set; } = "";
    }

    public class SupportSession
    {
        public string SessionId { get; set; } = "";
        public string InstallationId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ClientIp { get; set; } = "";
    }
}
