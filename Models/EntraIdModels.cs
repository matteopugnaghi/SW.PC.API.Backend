// ============================================================================
// EntraIdModels.cs — Microsoft Entra ID (SSO) DTOs
// ============================================================================
// Estado runtime del subsistema Entra ID, gated por Excel `System Config →
// EntraIdEnabled` (patrón OPC-UA/Modbus). Sin secretos: TenantId/ClientId son
// identificadores públicos de la App Registration, nunca credenciales.
// ============================================================================

namespace SW.PC.API.Backend.Models.EntraId
{
    /// <summary>
    /// 🔑 Estado runtime del subsistema Entra ID (SSO).
    /// Devuelto por IEntraIdService.GetStatus() y consumido por EntraIdController.
    /// </summary>
    public class EntraIdStatus
    {
        /// <summary>Entra ID habilitado en Excel (false en el stub deshabilitado)</summary>
        public bool Enabled { get; set; }

        /// <summary>App Registration configurada (TenantId + ClientId presentes en Excel)</summary>
        public bool Configured { get; set; }

        /// <summary>Conectividad con el endpoint de Entra verificada (metadata OIDC accesible)</summary>
        public bool Connected { get; set; }

        /// <summary>Directory (tenant) ID configurado en Excel (identificador público)</summary>
        public string TenantId { get; set; } = "";

        /// <summary>Application (client) ID presente en Excel (no se expone el valor)</summary>
        public bool ClientIdConfigured { get; set; }

        /// <summary>Authority OIDC efectiva usada para el health-check</summary>
        public string Authority { get; set; } = "";

        /// <summary>Último mensaje de estado legible</summary>
        public string StatusMessage { get; set; } = "";

        /// <summary>Timestamp del último health-check contra Entra</summary>
        public DateTime? LastCheck { get; set; }

        /// <summary>Cuándo arrancó el servicio</summary>
        public DateTime? StartedAt { get; set; }
    }

    /// <summary>
    /// 🔑 Configuración pública para que el frontend inicialice MSAL.
    /// Solo identificadores públicos de la App Registration — nunca secretos
    /// (el flujo es Authorization Code + PKCE, cliente público sin secreto).
    /// </summary>
    public class EntraIdLoginConfig
    {
        public bool Enabled { get; set; }
        public bool Configured { get; set; }
        public string ClientId { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string Authority { get; set; } = "";
        public List<string> RedirectUris { get; set; } = new();
    }

    /// <summary>
    /// 🔑 Usuario Entra validado, listo para el intercambio por sesión local.
    /// </summary>
    public class EntraUserInfo
    {
        /// <summary>object ID inmutable del usuario en Entra (clave de identidad)</summary>
        public string ObjectId { get; set; } = "";

        /// <summary>UPN / preferred_username (puede cambiar con renames)</summary>
        public string Username { get; set; } = "";

        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";

        /// <summary>Rol Aquafrisch mapeado desde claims de Entra (nunca SuperAdmin)</summary>
        public SW.PC.API.Backend.Models.SystemRole Role { get; set; }
    }

    /// <summary>
    /// 🔑 Request del intercambio: token de Entra (ID token) → sesión local.
    /// </summary>
    public class EntraLoginRequest
    {
        public string IdToken { get; set; } = "";
    }
}
