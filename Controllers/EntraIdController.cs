using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.EntraId;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🔑 Entra ID (SSO) API — status (diagnóstico), config (MSAL) y login (token exchange).
    /// Read-only + token exchange. Cuando el flag está OFF: status/config devuelven
    /// { enabled = false } y login responde 404 (el subsistema "no existe").
    /// No expone secretos: ClientId/TenantId son identificadores públicos (PKCE, cliente público).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EntraIdController : ControllerBase
    {
        private readonly IEntraIdService _entraIdService;
        private readonly IAuthenticationService _authService;
        private readonly IAuditLogService _auditLog;
        private readonly IRequestProjectContext _projectContext;
        private readonly ILogger<EntraIdController> _logger;
        // 📁 BD POR PROYECTO: misma factory que Auth/CertificateController (registros mTLS)
        private readonly IProjectDbContextFactory _dbFactory;

        public EntraIdController(
            IEntraIdService entraIdService,
            IAuthenticationService authService,
            IAuditLogService auditLog,
            IRequestProjectContext projectContext,
            ILogger<EntraIdController> logger,
            IProjectDbContextFactory dbFactory)
        {
            _entraIdService = entraIdService;
            _authService = authService;
            _auditLog = auditLog;
            _projectContext = projectContext;
            _logger = logger;
            _dbFactory = dbFactory;
        }

        /// <summary>Entra ID runtime status (configured / connectivity / health-check).</summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(EntraIdStatus), StatusCodes.Status200OK)]
        public ActionResult<EntraIdStatus> GetStatus()
        {
            if (!_entraIdService.IsEnabled)
                return Ok(new EntraIdStatus { Enabled = false, StatusMessage = "Disabled" });
            return Ok(_entraIdService.GetStatus());
        }

        /// <summary>
        /// Configuración pública para inicializar MSAL en el frontend (ClientId/Authority).
        /// Anónimo: el frontend la necesita ANTES del login (igual que /api/system/features).
        /// </summary>
        [HttpGet("config")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(EntraIdLoginConfig), StatusCodes.Status200OK)]
        public ActionResult<EntraIdLoginConfig> GetLoginConfig()
        {
            if (!_entraIdService.IsEnabled)
                return Ok(new EntraIdLoginConfig { Enabled = false });
            return Ok(_entraIdService.GetLoginConfig());
        }

        /// <summary>
        /// 🔁 Token exchange: ID token de Entra (validado: firma/issuer/audience/rol)
        /// → sesión interna + JWT local estándar. Downstream idéntico al login local.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")] // 🔒 EU CRA — misma protección brute-force que /api/auth/login
        [ProducesResponseType(typeof(Models.LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Models.LoginResponse>> LoginWithEntra([FromBody] EntraLoginRequest request)
        {
            // Subsistema deshabilitado ⇒ el endpoint "no existe"
            if (!_entraIdService.IsEnabled)
                return NotFound();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var (success, entraUser, error) = await _entraIdService.ValidateEntraTokenAsync(request?.IdToken ?? "");
            if (!success || entraUser == null)
            {
                _logger.LogWarning("🔑 Entra login rejected: {Error}", error);
                return Unauthorized(new Models.LoginResponse
                {
                    Success = false,
                    Message = error ?? "Token de Entra inválido"
                });
            }

            var response = await _authService.LoginWithEntraAsync(entraUser, ipAddress, userAgent, _projectContext.ProjectId);
            if (!response.Success)
                return Unauthorized(response);

            // 🔐 mTLS ESTRICTO (MtlsRequireRegisteredMachine): misma política que el login local.
            // Equipos remotos sin certificado de máquina no pueden iniciar sesión (ni por SSO).
            if (MtlsState.ShouldBlockLogin(HttpContext, response.User?.Roles))
            {
                if (!string.IsNullOrEmpty(response.Token))
                {
                    try { await _authService.LogoutAsync(response.Token); } catch { /* best-effort */ }
                }
                await _auditLog.LogAsync(
                    AuditCategory.Security,
                    AuditAction.PermissionDenied,
                    AuditResult.Warning,
                    $"Login Entra de '{entraUser.Username}' RECHAZADO: equipo no registrado (mTLS estricto) desde {ipAddress}",
                    userName: entraUser.Username,
                    ipAddress: ipAddress,
                    projectId: _projectContext.ProjectId);
                _logger.LogWarning("🔐 Login Entra de {User} rechazado: equipo no registrado (mTLS estricto) desde {Ip}",
                    entraUser.Username, ipAddress);
                return StatusCode(StatusCodes.Status403Forbidden, new Models.LoginResponse
                {
                    Success = false,
                    Message = "Este equipo no está registrado en el sistema. Solicite al administrador un código de registro de equipo o use el puesto local."
                });
            }

            // 🔐 mTLS REVOCACIÓN: mismo check que el login local — si el equipo tiene cert
            // válido pero su registro fue revocado en la BD, bloquear el login (también SSO).
            if (MtlsState.Enabled && MtlsState.RequireRegisteredMachine
                && response.User?.Roles?.Contains("SuperAdmin") != true)
            {
                var origin = OriginContext.FromHttpContext(HttpContext);
                if (!string.IsNullOrEmpty(origin.MachineName) && origin.RemoteIp != "127.0.0.1")
                {
                    await using var db = _dbFactory.CreateDbContext();
                    var isRegistered = await db.MachineRegistrationCodes
                        .AnyAsync(c => c.MachineName == origin.MachineName && c.UsedAt != null);
                    if (!isRegistered)
                    {
                        if (!string.IsNullOrEmpty(response.Token))
                            try { await _authService.LogoutAsync(response.Token); } catch { }
                        await _auditLog.LogAsync(AuditCategory.Security, AuditAction.PermissionDenied, AuditResult.Warning,
                            $"Login Entra de '{entraUser.Username}' RECHAZADO: equipo '{origin.MachineName}' revocado desde {ipAddress}",
                            userName: entraUser.Username, ipAddress: ipAddress, projectId: _projectContext.ProjectId);
                        _logger.LogWarning("🔐 Login Entra de {User} rechazado: equipo '{Machine}' revocado", entraUser.Username, origin.MachineName);
                        return StatusCode(StatusCodes.Status403Forbidden, new Models.LoginResponse
                        {
                            Success = false,
                            Message = "Este equipo no está registrado en el sistema. Solicite al administrador un código de registro de equipo o use el puesto local."
                        });
                    }
                }
            }

            return Ok(response);
        }
    }
}
