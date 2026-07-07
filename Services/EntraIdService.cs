// ============================================================================
// EntraIdService.cs — Microsoft Entra ID (SSO) subsystem
// ============================================================================
// Gated por Excel `System Config → EntraIdEnabled` (patrón OPC-UA/Modbus):
//   - Flag ausente/vacío/FALSE  → Program.cs registra DisabledEntraIdService.
//     ZERO REGRESSION: sin hilos, sin HTTP, sin recursos, sin endpoints activos.
//   - Flag TRUE                 → EntraIdService (BackgroundService):
//     lee la configuración del Excel, hace health-check periódico del endpoint
//     OIDC de Entra (metadata pública, sin credenciales) y publica el estado
//     en IMetricsService (InfoPanel → Servicios Externos) + audit log L1.
//
// FASE 1 (scaffolding): este servicio NO autentica todavía. Solo:
//   - valida presencia de configuración (TenantId/ClientId),
//   - verifica conectividad hacia Entra (¿hay salida a la nube?),
//   - expone estado para diagnóstico (InfoPanel / API).
// La validación de tokens (JwtBearer) y MSAL llegan en Fases 2-3.
//
// 🛡️ AISLAMIENTO: cualquier fallo aquí se degrada a estado "error" y NUNCA
// afecta a la auth local, al PLC ni al resto del host (mismo criterio Modbus).
// ============================================================================

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using SW.PC.API.Backend.Models.EntraId;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🔑 Entra ID (SSO) service interface (own interface, aligned with
    /// IOpcUaServerService / IModbusService — no shared base).
    /// </summary>
    public interface IEntraIdService
    {
        /// <summary>Entra ID habilitado en Excel (false en el stub deshabilitado).</summary>
        bool IsEnabled { get; }

        /// <summary>App Registration configurada (TenantId + ClientId presentes).</summary>
        bool IsConfigured { get; }

        /// <summary>Conectividad con Entra verificada en el último health-check.</summary>
        bool IsConnected { get; }

        /// <summary>Estado runtime del subsistema.</summary>
        EntraIdStatus GetStatus();

        /// <summary>Configuración pública para inicializar MSAL en el frontend (sin secretos).</summary>
        EntraIdLoginConfig GetLoginConfig();

        /// <summary>
        /// Valida un ID token de Entra (firma vía discovery OIDC, issuer, audience=clientId,
        /// expiración) y mapea sus claims de rol a un SystemRole de Aquafrisch.
        /// DENY-BY-DEFAULT: sin rol mapeado ⇒ rechazo. Nunca mapea a SuperAdmin.
        /// </summary>
        Task<(bool Success, EntraUserInfo? User, string? Error)> ValidateEntraTokenAsync(string idToken);
    }

    /// <summary>
    /// 🔑 Disabled stub — registrado cuando EntraIdEnabled está ausente/FALSE en Excel.
    /// Sin hilos, sin HTTP, cero recursos. Mirrors DisabledOpcUaServerService/DisabledModbusService.
    /// </summary>
    public class DisabledEntraIdService : IEntraIdService
    {
        public bool IsEnabled => false;
        public bool IsConfigured => false;
        public bool IsConnected => false;
        public EntraIdStatus GetStatus() => new()
        {
            Enabled = false,
            Configured = false,
            Connected = false,
            StatusMessage = "Disabled in configuration"
        };
        public EntraIdLoginConfig GetLoginConfig() => new() { Enabled = false };
        public Task<(bool Success, EntraUserInfo? User, string? Error)> ValidateEntraTokenAsync(string idToken)
            => Task.FromResult<(bool, EntraUserInfo?, string?)>((false, null, "Entra ID disabled in configuration"));
    }

    /// <summary>
    /// 🔑 Entra ID BackgroundService (solo registrado con EntraIdEnabled=TRUE).
    /// Health-check periódico del discovery OIDC del tenant (documento público,
    /// sin credenciales) → estado en IMetricsService + eventos L1 en audit log.
    /// </summary>
    public class EntraIdService : BackgroundService, IEntraIdService
    {
        private readonly ILogger<EntraIdService> _logger;
        private readonly IProjectContextService _projectContext;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IMetricsService _metrics;
        private readonly IAuditLogService _auditLogService;
        private readonly IHttpClientFactory _httpClientFactory;

        private string _tenantId = "";
        private string _clientId = "";
        private string _authority = "";
        private string _redirectUris = "";
        private string _roleSource = "roles";
        private readonly Dictionary<Models.SystemRole, string> _roleMap = new();
        private bool _configured;
        private bool _connected;
        private string _statusMessage = "No iniciado";
        private DateTime? _lastCheck;
        private DateTime? _startedAt;

        // Discovery OIDC con caché automática de signing keys (refresh periódico integrado)
        private ConfigurationManager<OpenIdConnectConfiguration>? _oidcConfigManager;

        // Health-check cada 60s; si falla, el siguiente ciclo reintenta (sin backoff agresivo:
        // es una única petición GET ligera por minuto).
        private const int HealthCheckIntervalSec = 60;

        public bool IsEnabled => true; // solo se registra cuando el flag Excel está ON
        public bool IsConfigured => _configured;
        public bool IsConnected => _connected;

        public EntraIdService(
            ILogger<EntraIdService> logger,
            IProjectContextService projectContext,
            IExcelConfigService excelConfigService,
            IMetricsService metrics,
            IAuditLogService auditLogService,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _projectContext = projectContext;
            _excelConfigService = excelConfigService;
            _metrics = metrics;
            _auditLogService = auditLogService;
            _httpClientFactory = httpClientFactory;
        }

        public EntraIdStatus GetStatus() => new()
        {
            Enabled = true,
            Configured = _configured,
            Connected = _connected,
            TenantId = _tenantId,
            ClientIdConfigured = !string.IsNullOrWhiteSpace(_clientId),
            Authority = _authority,
            StatusMessage = _statusMessage,
            LastCheck = _lastCheck,
            StartedAt = _startedAt
        };

        public EntraIdLoginConfig GetLoginConfig() => new()
        {
            Enabled = true,
            Configured = _configured,
            ClientId = _clientId,
            TenantId = _tenantId,
            Authority = string.IsNullOrWhiteSpace(_authority) ? "" : _authority.Replace("/v2.0", ""),
            RedirectUris = _redirectUris
                .Split(new[] { ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList()
        };

        // ────────────────────────────────────────────────────────────────────
        // Validación de ID token de Entra (Fase 2) — firma/issuer/audience/expiry
        // + mapeo de roles Excel (Fase 4). DENY-BY-DEFAULT.
        // ────────────────────────────────────────────────────────────────────
        public async Task<(bool Success, EntraUserInfo? User, string? Error)> ValidateEntraTokenAsync(string idToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idToken))
                    return (false, null, "Token vacío");
                if (!_configured)
                    return (false, null, "Entra ID no configurado (EntraId_TenantId / EntraId_ClientId)");

                // Discovery OIDC (cacheado): issuer + signing keys del tenant
                _oidcConfigManager ??= new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{_authority}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true });

                var oidcConfig = await _oidcConfigManager.GetConfigurationAsync(CancellationToken.None);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = oidcConfig.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _clientId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = oidcConfig.SigningKeys
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(idToken, validationParameters, out _);

                // Claims estándar de Entra: oid (object ID inmutable), preferred_username, name, email
                string GetClaim(params string[] types) =>
                    types.Select(t => principal.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

                var oid = GetClaim("oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");
                var username = GetClaim("preferred_username", "upn", System.Security.Claims.ClaimTypes.Upn, "email");
                var fullName = GetClaim("name", System.Security.Claims.ClaimTypes.Name);
                var email = GetClaim("email", System.Security.Claims.ClaimTypes.Email, "preferred_username");

                if (string.IsNullOrWhiteSpace(oid))
                    return (false, null, "Token sin claim 'oid' (object ID)");
                if (string.IsNullOrWhiteSpace(username))
                    username = oid; // fallback improbable

                // ─── Mapeo de rol (Fase 4): app-roles ("roles") o grupos ("groups") según Excel ───
                var claimType = _roleSource == "groups" ? "groups" : "roles";
                var tokenRoleValues = principal.FindAll(claimType).Select(c => c.Value)
                    .Concat(principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Coincidencia contra el mapa del Excel — prioridad: rol más privilegiado.
                // NUNCA SuperAdmin (no existe en el mapa). DENY-BY-DEFAULT si no hay match.
                Models.SystemRole? mapped = null;
                foreach (var role in new[] { Models.SystemRole.Administrator, Models.SystemRole.Maintenance,
                                             Models.SystemRole.Operator, Models.SystemRole.Auditor, Models.SystemRole.Viewer })
                {
                    if (_roleMap.TryGetValue(role, out var expected) && !string.IsNullOrWhiteSpace(expected)
                        && tokenRoleValues.Contains(expected.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        mapped = role;
                        break;
                    }
                }

                if (mapped == null)
                {
                    _logger.LogWarning("🔑 Entra user {User} rejected — no mapped role (claims [{Claims}], source '{Source}')",
                        username, string.Join(", ", tokenRoleValues), _roleSource);
                    return (false, null, "Usuario sin rol autorizado en la aplicación (deny-by-default)");
                }

                return (true, new EntraUserInfo
                {
                    ObjectId = oid,
                    Username = username,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName,
                    Email = email,
                    Role = mapped.Value
                }, null);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("🔑 Entra token validation failed: {Message}", ex.Message);
                return (false, null, $"Token inválido: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔑 Entra token validation error");
                return (false, null, "Error validando el token de Entra");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 🛡️ ISOLATION: todo el subsistema va envuelto — un fallo nunca tumba el host.
            try
            {
                _startedAt = DateTime.UtcNow;
                await LoadConfigurationAsync();

                _logger.LogInformation("🔑 Entra ID service started — configured: {Configured} (tenant: {Tenant})",
                    _configured, string.IsNullOrWhiteSpace(_tenantId) ? "—" : _tenantId);

                await _auditLogService.LogAsync(
                    Models.AuditCategory.Authentication, Models.AuditAction.EntraIdServiceStart,
                    _configured ? Models.AuditResult.Success : Models.AuditResult.Warning,
                    _configured
                        ? $"Entra ID service started — tenant {_tenantId}, authority {_authority}"
                        : "Entra ID service started — ENABLED in Excel but NOT configured (missing EntraId_TenantId / EntraId_ClientId). Awaiting client data.",
                    userName: "System");

                PublishStatus();

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await HealthCheckAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "🔑 Entra ID health-check cycle error (degraded, host unaffected)");
                        SetConnected(false, $"Health-check error: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(HealthCheckIntervalSec), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔑 Entra ID subsystem failed — degraded to error state (host unaffected)");
                _statusMessage = $"Error: {ex.Message}";
                _connected = false;
                PublishStatus();
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _auditLogService.LogAsync(
                    Models.AuditCategory.Authentication, Models.AuditAction.EntraIdServiceStop,
                    Models.AuditResult.Success,
                    "Entra ID service stopped", userName: "System");
            }
            catch { /* nunca bloquear el shutdown */ }
            await base.StopAsync(cancellationToken);
        }

        // ────────────────────────────────────────────────────────────────────
        // Configuración (Excel System Config)
        // ────────────────────────────────────────────────────────────────────
        private async Task LoadConfigurationAsync()
        {
            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
                {
                    _statusMessage = "Excel de configuración no encontrado";
                    _configured = false;
                    return;
                }

                var cfg = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                _tenantId = cfg?.EntraIdTenantId?.Trim() ?? "";
                _clientId = cfg?.EntraIdClientId?.Trim() ?? "";
                _redirectUris = cfg?.EntraIdRedirectUris ?? "";
                _roleSource = string.IsNullOrWhiteSpace(cfg?.EntraIdRoleSource) ? "roles" : cfg!.EntraIdRoleSource;

                _roleMap.Clear();
                _roleMap[Models.SystemRole.Administrator] = cfg?.EntraIdRoleMapAdministrator?.Trim() ?? "";
                _roleMap[Models.SystemRole.Maintenance] = cfg?.EntraIdRoleMapMaintenance?.Trim() ?? "";
                _roleMap[Models.SystemRole.Operator] = cfg?.EntraIdRoleMapOperator?.Trim() ?? "";
                _roleMap[Models.SystemRole.Viewer] = cfg?.EntraIdRoleMapViewer?.Trim() ?? "";
                _roleMap[Models.SystemRole.Auditor] = cfg?.EntraIdRoleMapAuditor?.Trim() ?? "";

                // Defaults razonables: si el mapa está vacío, esperamos app-roles con el
                // mismo nombre que los roles del sistema (correspondencia 1:1, decisión D6).
                if (_roleMap.Values.All(string.IsNullOrWhiteSpace))
                {
                    _roleMap[Models.SystemRole.Administrator] = "Administrator";
                    _roleMap[Models.SystemRole.Maintenance] = "Maintenance";
                    _roleMap[Models.SystemRole.Operator] = "Operator";
                    _roleMap[Models.SystemRole.Viewer] = "Viewer";
                    _roleMap[Models.SystemRole.Auditor] = "Auditor";
                }

                // Authority: explícita en Excel u obtenida del tenant (cloud público por defecto)
                var explicitAuthority = cfg?.EntraIdAuthority?.Trim() ?? "";
                _authority = !string.IsNullOrWhiteSpace(explicitAuthority)
                    ? explicitAuthority.TrimEnd('/')
                    : (!string.IsNullOrWhiteSpace(_tenantId)
                        ? $"https://login.microsoftonline.com/{_tenantId}/v2.0"
                        : "");

                _configured = !string.IsNullOrWhiteSpace(_tenantId) && !string.IsNullOrWhiteSpace(_clientId);
                _statusMessage = _configured
                    ? "Configurado — pendiente de health-check"
                    : "Habilitado, pendiente de configuración (EntraId_TenantId / EntraId_ClientId)";

                if (!_configured)
                {
                    _ = _auditLogService.LogAsync(
                        Models.AuditCategory.Authentication, Models.AuditAction.EntraIdConfigWarning,
                        Models.AuditResult.Warning,
                        "EntraIdEnabled=TRUE but App Registration data missing (EntraId_TenantId / EntraId_ClientId empty)",
                        userName: "System");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔑 Could not load Entra ID configuration from Excel");
                _configured = false;
                _statusMessage = $"Error leyendo configuración: {ex.Message}";
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Health-check: GET del discovery OIDC (documento público, sin secretos)
        // ────────────────────────────────────────────────────────────────────
        private async Task HealthCheckAsync(CancellationToken ct)
        {
            _lastCheck = DateTime.UtcNow;

            if (!_configured)
            {
                // Sin tenant no hay endpoint que comprobar; comprobamos al menos la
                // salida genérica hacia Entra (¿red permite login.microsoftonline.com?).
                var reachable = await ProbeAsync("https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration", ct);
                SetConnected(reachable, reachable
                    ? "Sin configurar — salida hacia Entra OK (esperando datos de RhB)"
                    : "Sin configurar — sin salida hacia Entra (red/proxy)");
                return;
            }

            var url = $"{_authority}/.well-known/openid-configuration";
            var ok = await ProbeAsync(url, ct);
            SetConnected(ok, ok ? "Conectado — metadata OIDC del tenant accesible" : "Sin conexión con Entra (red/proxy/tenant)");
        }

        private async Task<bool> ProbeAsync(string url, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("EntraId");
                using var response = await client.GetAsync(url, ct);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                return false;
            }
        }

        private void SetConnected(bool connected, string message)
        {
            var changed = connected != _connected;
            _connected = connected;
            _statusMessage = message;
            PublishStatus();

            if (changed)
            {
                _logger.LogInformation("🔑 Entra ID connectivity: {State} — {Message}", connected ? "UP" : "DOWN", message);
                _ = _auditLogService.LogAsync(
                    Models.AuditCategory.Authentication,
                    connected ? Models.AuditAction.EntraIdConnected : Models.AuditAction.EntraIdDisconnected,
                    connected ? Models.AuditResult.Success : Models.AuditResult.Warning,
                    $"Entra ID connectivity {(connected ? "restored" : "lost")}: {message}",
                    userName: "System");
            }
        }

        private void PublishStatus()
        {
            try
            {
                _metrics.SetEntraIdStatus(true, _configured, _connected, _statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "🔑 Could not publish Entra ID status to metrics");
            }
        }
    }
}
