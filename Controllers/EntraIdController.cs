using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly IRequestProjectContext _projectContext;
        private readonly ILogger<EntraIdController> _logger;

        public EntraIdController(
            IEntraIdService entraIdService,
            IAuthenticationService authService,
            IRequestProjectContext projectContext,
            ILogger<EntraIdController> logger)
        {
            _entraIdService = entraIdService;
            _authService = authService;
            _projectContext = projectContext;
            _logger = logger;
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

            return Ok(response);
        }
    }
}
