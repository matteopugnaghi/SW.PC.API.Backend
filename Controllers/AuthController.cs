// ============================================================================
// AuthController.cs - API de Autenticación
// ============================================================================
// Endpoints REST para autenticación, gestión de usuarios y sesiones
// Compatible con EU CRA y requisitos CADRA/Alstom
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// Controller de autenticación y gestión de usuarios
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        IAuditLogService auditLog,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _auditLog = auditLog;
        _logger = logger;
    }

    #region Autenticación

    /// <summary>
    /// Iniciar sesión
    /// </summary>
    /// <param name="request">Credenciales de login</param>
    /// <returns>Token JWT y información del usuario</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();
        
        var response = await _authService.LoginAsync(request, ipAddress, userAgent);
        
        if (!response.Success)
        {
            return Unauthorized(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Cerrar sesión actual
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> Logout()
    {
        var token = GetBearerToken();
        if (string.IsNullOrEmpty(token))
        {
            return Ok(new AuthOperationResponse { Success = true, Message = "No hay sesión activa" });
        }
        
        var response = await _authService.LogoutAsync(token);
        return Ok(response);
    }

    /// <summary>
    /// Cerrar todas las sesiones del usuario actual
    /// </summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> LogoutAll()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
        {
            return Unauthorized(new AuthOperationResponse { Success = false, Message = "No autorizado" });
        }
        
        var response = await _authService.LogoutAllSessionsAsync(userId, "Cierre de todas las sesiones por usuario");
        return Ok(response);
    }

    /// <summary>
    /// Refrescar token JWT
    /// </summary>
    /// <param name="refreshToken">Refresh token</param>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var response = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);
        
        if (!response.Success)
        {
            return Unauthorized(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Validar token actual
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> ValidateToken()
    {
        var token = GetBearerToken();
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized();
        }
        
        var (isValid, user) = await _authService.ValidateTokenAsync(token);
        
        if (!isValid || user == null)
        {
            return Unauthorized();
        }
        
        return Ok(user);
    }

    #endregion

    #region Perfil de Usuario

    /// <summary>
    /// Obtener perfil del usuario actual
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        var profile = await _authService.GetUserProfileAsync(userId);
        
        if (profile == null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }
        
        return Ok(profile);
    }

    /// <summary>
    /// Cambiar contraseña del usuario actual
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        var response = await _authService.ChangePasswordAsync(userId, request);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Obtener política de contraseñas
    /// </summary>
    [HttpGet("password-policy")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasswordPolicyInfo), StatusCodes.Status200OK)]
    public ActionResult<PasswordPolicyInfo> GetPasswordPolicy()
    {
        var config = _authService.GetConfiguration();
        return Ok(new PasswordPolicyInfo
        {
            MinLength = config.PasswordMinLength,
            RequireUppercase = config.RequireUppercase,
            RequireLowercase = config.RequireLowercase,
            RequireNumbers = config.RequireNumbers,
            RequireSpecialChars = config.RequireSpecialChars,
            MaxLoginAttempts = config.MaxLoginAttempts,
            LockoutMinutes = config.LockoutMinutes,
            SessionTimeoutMinutes = config.SessionTimeoutMinutes
        });
    }

    /// <summary>
    /// Obtener banner de login (para mostrar en UI)
    /// </summary>
    [HttpGet("login-banner")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginBannerResponse), StatusCodes.Status200OK)]
    public ActionResult<LoginBannerResponse> GetLoginBanner()
    {
        var config = _authService.GetConfiguration();
        return Ok(new LoginBannerResponse
        {
            ShowBanner = config.ShowLoginBanner,
            BannerText = config.LoginBannerText
        });
    }

    #endregion

    #region Gestión de Usuarios (Solo Administradores)

    /// <summary>
    /// Listar todos los usuarios
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Administrator,Auditor")]
    [ProducesResponseType(typeof(List<UserListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserListDto>>> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Obtener usuario por ID
    /// </summary>
    [HttpGet("users/{id}")]
    [Authorize(Roles = "Administrator,Auditor")]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListDto>> GetUserById(int id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        
        if (user == null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }
        
        return Ok(user);
    }

    /// <summary>
    /// Crear nuevo usuario
    /// </summary>
    [HttpPost("users")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthOperationResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var currentUser = GetCurrentUsername();
        var response = await _authService.CreateUserAsync(request, currentUser);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }
        
        return CreatedAtAction(nameof(GetUserById), new { id = ((dynamic)response.Data!).UserId }, response);
    }

    /// <summary>
    /// Actualizar usuario existente
    /// </summary>
    [HttpPut("users/{id}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthOperationResponse>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var currentUser = GetCurrentUsername();
        var response = await _authService.UpdateUserAsync(id, request, currentUser);
        
        if (!response.Success)
        {
            if (response.Message == "Usuario no encontrado")
            {
                return NotFound(response);
            }
            return BadRequest(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Eliminar usuario
    /// </summary>
    [HttpDelete("users/{id}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthOperationResponse>> DeleteUser(int id)
    {
        var currentUser = GetCurrentUsername();
        
        // No permitir auto-eliminación
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
        {
            return BadRequest(new AuthOperationResponse 
            { 
                Success = false, 
                Message = "No puede eliminar su propio usuario" 
            });
        }
        
        var response = await _authService.DeleteUserAsync(id, currentUser);
        
        if (!response.Success)
        {
            if (response.Message == "Usuario no encontrado")
            {
                return NotFound(response);
            }
            return BadRequest(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Desbloquear usuario
    /// </summary>
    [HttpPost("users/{id}/unlock")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthOperationResponse>> UnlockUser(int id)
    {
        var currentUser = GetCurrentUsername();
        var response = await _authService.UnlockUserAsync(id, currentUser);
        
        if (!response.Success)
        {
            if (response.Message == "Usuario no encontrado")
            {
                return NotFound(response);
            }
            return BadRequest(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Resetear contraseña de usuario
    /// </summary>
    [HttpPost("users/{id}/reset-password")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthOperationResponse>> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var currentUser = GetCurrentUsername();
        var response = await _authService.ResetPasswordAsync(id, request.NewPassword, currentUser);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Cerrar todas las sesiones de un usuario específico
    /// </summary>
    [HttpPost("users/{id}/logout-all")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> LogoutUserSessions(int id)
    {
        var currentUser = GetCurrentUsername();
        var response = await _authService.LogoutAllSessionsAsync(id, $"Forzado por administrador {currentUser}");
        return Ok(response);
    }

    #endregion

    #region Estado del Sistema

    /// <summary>
    /// Obtener estado del sistema de autenticación
    /// </summary>
    [HttpGet("status")]
    [Authorize(Roles = "Administrator,Auditor")]
    [ProducesResponseType(typeof(AuthSystemStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthSystemStatus>> GetSystemStatus()
    {
        var status = await _authService.GetSystemStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Obtener roles disponibles en el sistema
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public ActionResult<List<RoleDto>> GetRoles()
    {
        // Roles predefinidos del sistema
        var roles = new List<RoleDto>
        {
            new() { Name = "Administrator", Description = "Administrador del sistema - Acceso total" },
            new() { Name = "Operator", Description = "Operador de proceso - Control de operaciones" },
            new() { Name = "Maintenance", Description = "Personal de mantenimiento - Configuración técnica" },
            new() { Name = "Viewer", Description = "Solo visualización - Acceso de solo lectura" },
            new() { Name = "Auditor", Description = "Auditor de seguridad - Acceso a logs y reportes" }
        };
        
        return Ok(roles);
    }

    #endregion

    #region Session Management (Phase 3)

    /// <summary>
    /// Obtener sesiones activas del usuario actual
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(List<SessionInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionInfoDto>>> GetMySessions()
    {
        var userId = GetCurrentUserId();
        var sessions = await _authService.GetUserSessionsAsync(userId);
        return Ok(sessions);
    }

    /// <summary>
    /// Obtener todas las sesiones activas (solo administradores)
    /// </summary>
    [HttpGet("sessions/all")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(List<SessionInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionInfoDto>>> GetAllActiveSessions()
    {
        var sessions = await _authService.GetAllActiveSessionsAsync();
        return Ok(sessions);
    }

    /// <summary>
    /// Cerrar una sesión específica del usuario actual
    /// </summary>
    /// <param name="sessionId">ID de la sesión a cerrar</param>
    [HttpDelete("sessions/{sessionId}")]
    [Authorize]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> CloseSession(int sessionId)
    {
        var userId = GetCurrentUserId();
        var result = await _authService.CloseSessionAsync(sessionId, userId, GetCurrentUsername());
        return Ok(result);
    }

    /// <summary>
    /// Cerrar sesión de otro usuario (solo administradores)
    /// </summary>
    /// <param name="sessionId">ID de la sesión a cerrar</param>
    [HttpDelete("sessions/admin/{sessionId}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(AuthOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthOperationResponse>> AdminCloseSession(int sessionId)
    {
        var result = await _authService.AdminCloseSessionAsync(sessionId, GetCurrentUsername());
        return Ok(result);
    }

    /// <summary>
    /// Obtener configuración de sesiones (para frontend)
    /// </summary>
    [HttpGet("sessions/config")]
    [Authorize]
    [ProducesResponseType(typeof(SessionConfigDto), StatusCodes.Status200OK)]
    public ActionResult<SessionConfigDto> GetSessionConfig()
    {
        var config = _authService.GetConfiguration();
        return Ok(new SessionConfigDto
        {
            MaxConcurrentSessions = config.MaxConcurrentSessions,
            SingleSessionRoles = config.SingleSessionRoles,
            InactivityTimeoutMinutes = config.InactivityTimeoutMinutes,
            SessionTimeoutMinutes = config.SessionTimeoutMinutes
        });
    }

    #endregion

    #region RBAC Configuration (Phase 4)

    /// <summary>
    /// Obtener configuración RBAC completa (para frontend)
    /// </summary>
    [HttpGet("rbac/config")]
    [Authorize]
    [ProducesResponseType(typeof(RbacConfigDto), StatusCodes.Status200OK)]
    public ActionResult<RbacConfigDto> GetRbacConfig()
    {
        var config = _authService.GetConfiguration();
        return Ok(new RbacConfigDto
        {
            DefaultRole = config.DefaultRole,
            EnableGuestRole = config.EnableGuestRole,
            GuestPermissions = config.GuestPermissions,
            RequireUserApproval = config.RequireUserApproval,
            NotifyAdminOnNewUser = config.NotifyAdminOnNewUser,
            OperatorExtraPermissions = config.OperatorExtraPermissions,
            MaintenanceExtraPermissions = config.MaintenanceExtraPermissions,
            RestrictedPermissions = config.RestrictedPermissions,
            EnableRoleHierarchy = config.EnableRoleHierarchy
        });
    }

    /// <summary>
    /// Obtener todos los permisos disponibles en el sistema
    /// </summary>
    [HttpGet("rbac/permissions")]
    [Authorize(Roles = "Administrator,Auditor")]
    [ProducesResponseType(typeof(List<PermissionGroupDto>), StatusCodes.Status200OK)]
    public ActionResult<List<PermissionGroupDto>> GetAllPermissions()
    {
        var permissions = new List<PermissionGroupDto>
        {
            new() { 
                Area = "users", 
                Description = "Gestión de usuarios",
                Permissions = new() { "create", "read", "update", "delete" }
            },
            new() { 
                Area = "roles", 
                Description = "Gestión de roles",
                Permissions = new() { "create", "read", "update", "delete" }
            },
            new() { 
                Area = "audit", 
                Description = "Auditoría y logs",
                Permissions = new() { "read", "export" }
            },
            new() { 
                Area = "config", 
                Description = "Configuración del sistema",
                Permissions = new() { "read", "update" }
            },
            new() { 
                Area = "plc", 
                Description = "Control de PLC",
                Permissions = new() { "read", "write", "config" }
            },
            new() { 
                Area = "alarms", 
                Description = "Gestión de alarmas",
                Permissions = new() { "read", "acknowledge", "config" }
            },
            new() { 
                Area = "recipes", 
                Description = "Gestión de recetas",
                Permissions = new() { "create", "read", "update", "delete", "execute" }
            },
            new() { 
                Area = "reports", 
                Description = "Reportes",
                Permissions = new() { "read", "create", "export" }
            },
            new() { 
                Area = "security", 
                Description = "Seguridad del sistema",
                Permissions = new() { "read", "update" }
            },
            new() { 
                Area = "backup", 
                Description = "Copias de seguridad",
                Permissions = new() { "create", "restore" }
            }
        };
        
        return Ok(permissions);
    }

    /// <summary>
    /// Obtener matriz de permisos por rol
    /// </summary>
    [HttpGet("rbac/matrix")]
    [Authorize(Roles = "Administrator,Auditor")]
    [ProducesResponseType(typeof(RbacMatrixDto), StatusCodes.Status200OK)]
    public ActionResult<RbacMatrixDto> GetRbacMatrix()
    {
        var config = _authService.GetConfiguration();
        
        return Ok(new RbacMatrixDto
        {
            Roles = new List<RolePermissionsDto>
            {
                new() {
                    Role = "Administrator",
                    Description = "Acceso total al sistema",
                    Permissions = new() { 
                        "users:create", "users:read", "users:update", "users:delete",
                        "roles:create", "roles:read", "roles:update", "roles:delete",
                        "audit:read", "audit:export",
                        "config:read", "config:update",
                        "plc:read", "plc:write", "plc:config",
                        "alarms:read", "alarms:acknowledge", "alarms:config",
                        "recipes:create", "recipes:read", "recipes:update", "recipes:delete", "recipes:execute",
                        "reports:read", "reports:create", "reports:export",
                        "security:read", "security:update",
                        "backup:create", "backup:restore"
                    }
                },
                new() {
                    Role = "Operator",
                    Description = "Control de proceso",
                    Permissions = new List<string> { 
                        "plc:read", "plc:write",
                        "alarms:read", "alarms:acknowledge",
                        "recipes:read", "recipes:execute",
                        "reports:read"
                    }.Concat(config.OperatorExtraPermissions).ToList()
                },
                new() {
                    Role = "Maintenance",
                    Description = "Mantenimiento técnico",
                    Permissions = new List<string> { 
                        "plc:read", "plc:write", "plc:config",
                        "alarms:read", "alarms:acknowledge", "alarms:config",
                        "recipes:create", "recipes:read", "recipes:update", "recipes:execute",
                        "reports:read", "reports:create",
                        "config:read", "config:update"
                    }.Concat(config.MaintenanceExtraPermissions).ToList()
                },
                new() {
                    Role = "Viewer",
                    Description = "Solo lectura",
                    Permissions = new() { 
                        "plc:read", "alarms:read", "recipes:read", "reports:read"
                    }
                },
                new() {
                    Role = "Auditor",
                    Description = "Auditoría de seguridad",
                    Permissions = new() { 
                        "audit:read", "audit:export",
                        "reports:read", "reports:export",
                        "security:read",
                        "users:read"
                    }
                }
            },
            RestrictedPermissions = config.RestrictedPermissions,
            EnableRoleHierarchy = config.EnableRoleHierarchy
        });
    }

    #endregion

    #region Helpers

    private string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return null;
        }
        return authHeader.Substring(7);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }

    private string GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
    }

    private string? GetClientIpAddress()
    {
        // Intentar obtener IP real detrás de proxy
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }
        
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    #endregion
}

#region DTOs Adicionales

/// <summary>
/// Request para refresh token
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Request para reset de contraseña
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response del banner de login
/// </summary>
public class LoginBannerResponse
{
    public bool ShowBanner { get; set; }
    public string BannerText { get; set; } = string.Empty;
}

/// <summary>
/// DTO de rol para listado
/// </summary>
public class RoleDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// SessionInfoDto y SessionConfigDto están definidos en AuthModels.cs

#endregion

#region RBAC DTOs (Phase 4)

/// <summary>
/// Configuración RBAC para frontend
/// </summary>
public class RbacConfigDto
{
    public string DefaultRole { get; set; } = "Viewer";
    public bool EnableGuestRole { get; set; }
    public List<string> GuestPermissions { get; set; } = new();
    public bool RequireUserApproval { get; set; }
    public bool NotifyAdminOnNewUser { get; set; }
    public List<string> OperatorExtraPermissions { get; set; } = new();
    public List<string> MaintenanceExtraPermissions { get; set; } = new();
    public List<string> RestrictedPermissions { get; set; } = new();
    public bool EnableRoleHierarchy { get; set; }
}

/// <summary>
/// Grupo de permisos por área
/// </summary>
public class PermissionGroupDto
{
    public string Area { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Permisos de un rol específico
/// </summary>
public class RolePermissionsDto
{
    public string Role { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Matriz completa de RBAC
/// </summary>
public class RbacMatrixDto
{
    public List<RolePermissionsDto> Roles { get; set; } = new();
    public List<string> RestrictedPermissions { get; set; } = new();
    public bool EnableRoleHierarchy { get; set; }
}

#endregion
