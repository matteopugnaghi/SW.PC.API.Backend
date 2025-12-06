// ============================================================================
// AuthenticationService.cs - Servicio de Autenticación
// ============================================================================
// Implementación completa de autenticación según EU CRA y CADRA/Alstom
// - Autenticación local con BCrypt
// - Active Directory preparado (deshabilitado por defecto)
// - JWT tokens con refresh
// - Bloqueo de cuenta por intentos fallidos
// - Auditoría completa de eventos
// ============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interface del servicio de autenticación
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Autenticar usuario</summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);
    
    /// <summary>Cerrar sesión</summary>
    Task<AuthOperationResponse> LogoutAsync(string token);
    
    /// <summary>Cerrar todas las sesiones del usuario</summary>
    Task<AuthOperationResponse> LogoutAllSessionsAsync(int userId, string reason);
    
    /// <summary>Validar token JWT</summary>
    Task<(bool IsValid, UserProfileDto? User)> ValidateTokenAsync(string token);
    
    /// <summary>Refrescar token</summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, string? ipAddress);
    
    /// <summary>Cambiar contraseña</summary>
    Task<ChangePasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    
    /// <summary>Validar política de contraseñas</summary>
    PasswordValidationResult ValidatePassword(string password);
    
    /// <summary>Obtener perfil del usuario</summary>
    Task<UserProfileDto?> GetUserProfileAsync(int userId);
    
    /// <summary>Obtener estado del sistema de autenticación</summary>
    Task<AuthSystemStatus> GetSystemStatusAsync();
    
    /// <summary>Obtener configuración de autenticación</summary>
    AuthConfiguration GetConfiguration();
    
    // Gestión de usuarios
    Task<AuthOperationResponse> CreateUserAsync(CreateUserRequest request, string createdBy);
    Task<AuthOperationResponse> UpdateUserAsync(int userId, UpdateUserRequest request, string modifiedBy);
    Task<AuthOperationResponse> DeleteUserAsync(int userId, string deletedBy);
    Task<AuthOperationResponse> UnlockUserAsync(int userId, string unlockedBy);
    Task<AuthOperationResponse> ResetPasswordAsync(int userId, string newPassword, string resetBy);
    Task<List<UserListDto>> GetAllUsersAsync();
    Task<UserListDto?> GetUserByIdAsync(int userId);
    
    // Gestión de sesiones (Phase 3)
    Task<List<SessionInfoDto>> GetUserSessionsAsync(int userId);
    Task<List<SessionInfoDto>> GetAllActiveSessionsAsync();
    Task<AuthOperationResponse> CloseSessionAsync(int sessionId, int requestingUserId, string requestingUsername);
    Task<AuthOperationResponse> AdminCloseSessionAsync(int sessionId, string adminUsername);
    
    // Inicialización
    Task InitializeAsync();
    Task EnsureAdminUserExistsAsync();
}

/// <summary>
/// Implementación del servicio de autenticación
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly AquafrischDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AuthConfiguration _config;
    
    // BCrypt work factor (12 = ~250ms hash time, buen balance seguridad/rendimiento)
    private const int BCRYPT_WORK_FACTOR = 12;
    
    public AuthenticationService(
        AquafrischDbContext context,
        IAuditLogService auditLog,
        ILogger<AuthenticationService> logger,
        IExcelConfigService excelConfig,
        IConfiguration configuration)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
        _configuration = configuration;
        
        // Cargar configuración desde Excel
        _config = LoadConfigFromExcel(excelConfig);
        
        // JWT Secret Key: Prioridad 1) Excel, 2) appsettings.json, 3) Generar nuevo
        if (string.IsNullOrEmpty(_config.JwtSecretKey))
        {
            _config.JwtSecretKey = _configuration["Jwt:Key"] ?? string.Empty;
        }
        if (string.IsNullOrEmpty(_config.JwtSecretKey))
        {
            _config.JwtSecretKey = GenerateSecureKey(64);
            _logger.LogWarning("JWT Secret Key generado automáticamente. Considere configurar uno fijo en producción.");
        }
        
        // JWT Issuer/Audience: Prioridad 1) Excel, 2) appsettings.json
        if (string.IsNullOrEmpty(_config.JwtIssuer))
        {
            _config.JwtIssuer = _configuration["Jwt:Issuer"] ?? "AquafrischSupervisor";
        }
        if (string.IsNullOrEmpty(_config.JwtAudience))
        {
            _config.JwtAudience = _configuration["Jwt:Audience"] ?? "AquafrischClients";
        }
    }
    
    #region Autenticación
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
    {
        try
        {
            _logger.LogInformation("Intento de login para usuario: {Username} desde IP: {IP}", 
                request.Username, ipAddress ?? "unknown");
            
            // Buscar usuario
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());
            
            // Usuario no existe
            if (user == null)
            {
                await LogLoginAttemptAsync(request.Username, false, AuthEventType.LoginFailed, 
                    ipAddress, userAgent, "Usuario no encontrado", "Local");
                
                // Respuesta genérica para no revelar si el usuario existe
                return new LoginResponse
                {
                    Success = false,
                    Message = "Credenciales inválidas"
                };
            }
            
            // Verificar si cuenta está bloqueada
            if (user.Status == UserStatus.Locked || 
                (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
            {
                var remainingMinutes = user.LockedUntil.HasValue 
                    ? (int)(user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes + 1 
                    : _config.LockoutMinutes;
                
                await LogLoginAttemptAsync(request.Username, false, AuthEventType.LoginBlocked, 
                    ipAddress, userAgent, "Cuenta bloqueada", "Local");
                
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Cuenta bloqueada. Intente nuevamente en {remainingMinutes} minutos.",
                    LockoutMinutes = remainingMinutes
                };
            }
            
            // Verificar si cuenta está deshabilitada
            if (user.Status == UserStatus.Disabled)
            {
                await LogLoginAttemptAsync(request.Username, false, AuthEventType.LoginFailed, 
                    ipAddress, userAgent, "Cuenta deshabilitada", "Local");
                
                return new LoginResponse
                {
                    Success = false,
                    Message = "Cuenta deshabilitada. Contacte al administrador."
                };
            }
            
            // Autenticar según tipo de usuario
            bool authenticated = false;
            string authMethod = "Local";
            
            if (user.IsActiveDirectoryUser && _config.EnableActiveDirectory)
            {
                // Intentar autenticación AD
                authenticated = await AuthenticateWithActiveDirectoryAsync(request.Username, request.Password);
                authMethod = "ActiveDirectory";
                
                // Fallback a local si AD falla y está habilitado
                if (!authenticated && _config.FallbackToLocal && !string.IsNullOrEmpty(user.PasswordHash))
                {
                    authenticated = VerifyPassword(request.Password, user.PasswordHash);
                    authMethod = "LocalFallback";
                }
            }
            else
            {
                // Autenticación local
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    authenticated = VerifyPassword(request.Password, user.PasswordHash);
                }
            }
            
            if (!authenticated)
            {
                // Incrementar intentos fallidos
                user.FailedLoginAttempts++;
                user.LastFailedLoginAt = DateTime.UtcNow;
                
                var remainingAttempts = _config.MaxLoginAttempts - user.FailedLoginAttempts;
                
                // Bloquear cuenta si excede intentos
                if (user.FailedLoginAttempts >= _config.MaxLoginAttempts)
                {
                    user.Status = UserStatus.Locked;
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(_config.LockoutMinutes);
                    
                    await LogLoginAttemptAsync(request.Username, false, AuthEventType.AccountLocked, 
                        ipAddress, userAgent, $"Cuenta bloqueada después de {user.FailedLoginAttempts} intentos", authMethod);
                    
                    await _auditLog.LogAsync(
                        AuditCategory.Authentication, 
                        AuditAction.AccountLocked, 
                        AuditResult.Warning,
                        $"Cuenta {request.Username} bloqueada por {user.FailedLoginAttempts} intentos fallidos",
                        request.Username, request.Username, ipAddress);
                    
                    await _context.SaveChangesAsync();
                    
                    return new LoginResponse
                    {
                        Success = false,
                        Message = $"Cuenta bloqueada por {_config.LockoutMinutes} minutos debido a múltiples intentos fallidos.",
                        LockoutMinutes = _config.LockoutMinutes
                    };
                }
                
                await _context.SaveChangesAsync();
                
                await LogLoginAttemptAsync(request.Username, false, AuthEventType.LoginFailed, 
                    ipAddress, userAgent, "Contraseña incorrecta", authMethod);
                
                return new LoginResponse
                {
                    Success = false,
                    Message = "Credenciales inválidas",
                    RemainingAttempts = remainingAttempts > 0 ? remainingAttempts : 0
                };
            }
            
            // Login exitoso - resetear intentos fallidos
            user.FailedLoginAttempts = 0;
            user.LastFailedLoginAt = null;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            
            if (user.Status == UserStatus.Locked)
            {
                user.Status = UserStatus.Active;
            }
            
            // ===== CONTROL DE SESIONES (Phase 3) =====
            var userRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            
            // 1. Verificar roles con sesión única
            foreach (var role in userRoles)
            {
                if (_config.SingleSessionRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    // Buscar si hay otro usuario con este rol activo
                    var existingRoleSession = await _context.UserSessions
                        .Include(s => s.User)
                            .ThenInclude(u => u.UserRoles)
                                .ThenInclude(ur => ur.Role)
                        .Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow && s.UserId != user.Id)
                        .FirstOrDefaultAsync(s => s.User.UserRoles.Any(ur => ur.Role.Name == role));
                    
                    if (existingRoleSession != null)
                    {
                        if (_config.SingleSessionBehavior == "force")
                        {
                            // Expulsar sesión anterior
                            existingRoleSession.IsRevoked = true;
                            existingRoleSession.RevokedAt = DateTime.UtcNow;
                            existingRoleSession.RevokedReason = $"Expulsado por nuevo login de {request.Username} (rol {role})";
                            
                            await _auditLog.LogAsync(
                                AuditCategory.Authentication, 
                                AuditAction.LogoutAllSessions, 
                                AuditResult.Warning,
                                $"Usuario {existingRoleSession.User.Username} expulsado por login de {request.Username} (rol único: {role})",
                                existingRoleSession.User.Id.ToString(), existingRoleSession.User.Username);
                            
                            _logger.LogWarning("🔐 Usuario {OldUser} expulsado por login de {NewUser} (rol: {Role})",
                                existingRoleSession.User.Username, request.Username, role);
                        }
                        else
                        {
                            // Rechazar nuevo login
                            await LogLoginAttemptAsync(request.Username, false, AuthEventType.LoginBlocked, 
                                ipAddress, userAgent, $"Rol {role} ya tiene sesión activa ({existingRoleSession.User.Username})", authMethod);
                            
                            return new LoginResponse
                            {
                                Success = false,
                                Message = $"Ya hay un {role} activo en el sistema ({existingRoleSession.User.Username}). Solo se permite una sesión por rol."
                            };
                        }
                    }
                }
            }
            
            // 2. Verificar máximo de sesiones concurrentes por usuario
            if (_config.MaxConcurrentSessions > 0)
            {
                var activeSessions = await _context.UserSessions
                    .Where(s => s.UserId == user.Id && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
                    .OrderBy(s => s.CreatedAt)
                    .ToListAsync();
                
                if (activeSessions.Count >= _config.MaxConcurrentSessions)
                {
                    // Cerrar las sesiones más antiguas para hacer espacio
                    var sessionsToRevoke = activeSessions.Take(activeSessions.Count - _config.MaxConcurrentSessions + 1);
                    foreach (var oldSession in sessionsToRevoke)
                    {
                        oldSession.IsRevoked = true;
                        oldSession.RevokedAt = DateTime.UtcNow;
                        oldSession.RevokedReason = "Exceso de sesiones concurrentes";
                    }
                    
                    _logger.LogInformation("🔐 Cerradas {Count} sesiones antiguas de {User} por límite de sesiones concurrentes",
                        sessionsToRevoke.Count(), request.Username);
                }
            }
            
            // Generar tokens
            var tokenExpiry = request.RememberMe 
                ? DateTime.UtcNow.AddDays(7) 
                : DateTime.UtcNow.AddMinutes(_config.SessionTimeoutMinutes);
            
            var token = GenerateJwtToken(user, tokenExpiry);
            var refreshToken = GenerateRefreshToken();
            
            // Crear sesión
            var session = new UserSession
            {
                UserId = user.Id,
                Token = token,
                RefreshToken = refreshToken,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = tokenExpiry,
                LastActivityAt = DateTime.UtcNow
            };
            
            _context.UserSessions.Add(session);
            await _context.SaveChangesAsync();
            
            // Log exitoso
            await LogLoginAttemptAsync(request.Username, true, AuthEventType.LoginSuccess, 
                ipAddress, userAgent, null, authMethod);
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.Login, 
                AuditResult.Success,
                $"Usuario {request.Username} inició sesión exitosamente desde {ipAddress}",
                user.Id.ToString(), request.Username, ipAddress);
            
            _logger.LogInformation("Login exitoso para usuario: {Username}", request.Username);
            
            return new LoginResponse
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = tokenExpiry,
                MustChangePassword = user.MustChangePassword,
                User = MapToUserProfile(user),
                Message = user.MustChangePassword 
                    ? "Debe cambiar su contraseña antes de continuar" 
                    : "Login exitoso"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante login para usuario: {Username}", request.Username);
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.LoginFailed, 
                AuditResult.Error,
                $"Error durante login de {request.Username}: {ex.Message}",
                null, request.Username, ipAddress);
            
            return new LoginResponse
            {
                Success = false,
                Message = "Error interno durante autenticación"
            };
        }
    }
    
    public async Task<AuthOperationResponse> LogoutAsync(string token)
    {
        try
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Token == token && !s.IsRevoked);
            
            if (session == null)
            {
                return new AuthOperationResponse { Success = true, Message = "Sesión no encontrada o ya cerrada" };
            }
            
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = "Logout por usuario";
            
            await _context.SaveChangesAsync();
            
            await LogLoginAttemptAsync(session.User?.Username ?? "unknown", true, 
                AuthEventType.Logout, session.IpAddress, session.UserAgent, null, "Token");
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.Logout, 
                AuditResult.Success,
                $"Usuario {session.User?.Username} cerró sesión",
                session.User?.Id.ToString(), session.User?.Username, session.IpAddress);
            
            return new AuthOperationResponse { Success = true, Message = "Sesión cerrada correctamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante logout");
            return new AuthOperationResponse { Success = false, Message = "Error al cerrar sesión" };
        }
    }
    
    public async Task<AuthOperationResponse> LogoutAllSessionsAsync(int userId, string reason)
    {
        try
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .ToListAsync();
            
            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedReason = reason;
            }
            
            await _context.SaveChangesAsync();
            
            var user = await _context.Users.FindAsync(userId);
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.LogoutAllSessions, 
                AuditResult.Success,
                $"Todas las sesiones del usuario {user?.Username} fueron cerradas: {reason}",
                user?.Id.ToString(), user?.Username);
            
            return new AuthOperationResponse 
            { 
                Success = true, 
                Message = $"Se cerraron {sessions.Count} sesiones" 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar todas las sesiones del usuario {UserId}", userId);
            return new AuthOperationResponse { Success = false, Message = "Error al cerrar sesiones" };
        }
    }
    
    public async Task<(bool IsValid, UserProfileDto? User)> ValidateTokenAsync(string token)
    {
        try
        {
            // Verificar si el token está revocado
            var session = await _context.UserSessions
                .Include(s => s.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(s => s.Token == token);
            
            if (session == null || session.IsRevoked || session.ExpiresAt < DateTime.UtcNow)
            {
                return (false, null);
            }
            
            // ===== VERIFICAR INACTIVIDAD (Phase 3) =====
            if (_config.InactivityTimeoutMinutes > 0)
            {
                var inactiveMinutes = (DateTime.UtcNow - session.LastActivityAt).TotalMinutes;
                if (inactiveMinutes > _config.InactivityTimeoutMinutes)
                {
                    // Revocar sesión por inactividad
                    session.IsRevoked = true;
                    session.RevokedAt = DateTime.UtcNow;
                    session.RevokedReason = $"Sesión expirada por inactividad ({(int)inactiveMinutes} minutos)";
                    await _context.SaveChangesAsync();
                    
                    await _auditLog.LogAsync(
                        AuditCategory.Authentication, 
                        AuditAction.Logout, 
                        AuditResult.Warning,
                        $"Sesión de {session.User?.Username} cerrada por inactividad ({(int)inactiveMinutes} min)",
                        session.User?.Id.ToString(), session.User?.Username);
                    
                    _logger.LogInformation("🔐 Sesión de {User} cerrada por inactividad ({Minutes} min)",
                        session.User?.Username, (int)inactiveMinutes);
                    
                    return (false, null);
                }
            }
            
            // Validar firma JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config.JwtSecretKey);
            
            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _config.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _config.JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                }, out _);
            }
            catch
            {
                return (false, null);
            }
            
            // Actualizar última actividad (si está habilitado)
            if (_config.TrackLastActivity)
            {
                session.LastActivityAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            
            return (true, MapToUserProfile(session.User!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando token");
            return (false, null);
        }
    }
    
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, string? ipAddress)
    {
        try
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && !s.IsRevoked);
            
            if (session == null || session.User == null)
            {
                return new LoginResponse { Success = false, Message = "Refresh token inválido" };
            }
            
            // Revocar sesión anterior
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = "Token refrescado";
            
            // Crear nueva sesión
            var tokenExpiry = DateTime.UtcNow.AddMinutes(_config.SessionTimeoutMinutes);
            var newToken = GenerateJwtToken(session.User, tokenExpiry);
            var newRefreshToken = GenerateRefreshToken();
            
            var newSession = new UserSession
            {
                UserId = session.UserId,
                Token = newToken,
                RefreshToken = newRefreshToken,
                IpAddress = ipAddress ?? session.IpAddress,
                UserAgent = session.UserAgent,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = tokenExpiry,
                LastActivityAt = DateTime.UtcNow
            };
            
            _context.UserSessions.Add(newSession);
            await _context.SaveChangesAsync();
            
            await LogLoginAttemptAsync(session.User.Username, true, AuthEventType.SessionRefreshed, 
                ipAddress, session.UserAgent, null, "RefreshToken");
            
            return new LoginResponse
            {
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = tokenExpiry,
                User = MapToUserProfile(session.User),
                MustChangePassword = session.User.MustChangePassword
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refrescando token");
            return new LoginResponse { Success = false, Message = "Error al refrescar sesión" };
        }
    }
    
    #endregion
    
    #region Cambio de Contraseña
    
    public async Task<ChangePasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "Usuario no encontrado" 
                };
            }
            
            // Usuarios AD no pueden cambiar contraseña aquí
            if (user.IsActiveDirectoryUser && !_config.FallbackToLocal)
            {
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "Los usuarios de Active Directory deben cambiar su contraseña en el dominio" 
                };
            }
            
            // Verificar contraseña actual
            if (string.IsNullOrEmpty(user.PasswordHash) || 
                !VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication, 
                    AuditAction.PasswordChangeFailed, 
                    AuditResult.Failure,
                    $"Intento fallido de cambio de contraseña para {user.Username}: contraseña actual incorrecta",
                    user.Id.ToString(), user.Username);
                
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "Contraseña actual incorrecta" 
                };
            }
            
            // Verificar que nueva contraseña y confirmación coinciden
            if (request.NewPassword != request.ConfirmPassword)
            {
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "La nueva contraseña y la confirmación no coinciden" 
                };
            }
            
            // Validar política de contraseñas
            var validation = ValidatePassword(request.NewPassword);
            if (!validation.IsValid)
            {
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "La nueva contraseña no cumple los requisitos",
                    ValidationErrors = validation.Errors
                };
            }
            
            // Verificar que no sea igual a la anterior
            if (VerifyPassword(request.NewPassword, user.PasswordHash))
            {
                return new ChangePasswordResponse 
                { 
                    Success = false, 
                    Message = "La nueva contraseña no puede ser igual a la anterior" 
                };
            }
            
            // Actualizar contraseña
            user.PasswordHash = HashPassword(request.NewPassword);
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = false;
            user.ModifiedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            // Revocar todas las sesiones anteriores por seguridad
            await LogoutAllSessionsAsync(userId, "Cambio de contraseña");
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.PasswordChanged, 
                AuditResult.Success,
                $"Usuario {user.Username} cambió su contraseña exitosamente",
                user.Id.ToString(), user.Username);
            
            _logger.LogInformation("Contraseña cambiada exitosamente para usuario: {Username}", user.Username);
            
            return new ChangePasswordResponse 
            { 
                Success = true, 
                Message = "Contraseña cambiada exitosamente. Debe iniciar sesión nuevamente." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cambiando contraseña para usuario {UserId}", userId);
            return new ChangePasswordResponse 
            { 
                Success = false, 
                Message = "Error al cambiar contraseña" 
            };
        }
    }
    
    public PasswordValidationResult ValidatePassword(string password)
    {
        var result = new PasswordValidationResult { IsValid = true };
        
        if (string.IsNullOrEmpty(password))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña no puede estar vacía");
            return result;
        }
        
        if (password.Length < _config.PasswordMinLength)
        {
            result.IsValid = false;
            result.Errors.Add($"La contraseña debe tener al menos {_config.PasswordMinLength} caracteres");
        }
        
        if (_config.RequireUppercase && !password.Any(char.IsUpper))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña debe contener al menos una letra mayúscula");
        }
        
        if (_config.RequireLowercase && !password.Any(char.IsLower))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña debe contener al menos una letra minúscula");
        }
        
        if (_config.RequireNumbers && !password.Any(char.IsDigit))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña debe contener al menos un número");
        }
        
        if (_config.RequireSpecialChars && !password.Any(c => !char.IsLetterOrDigit(c)))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña debe contener al menos un carácter especial");
        }
        
        // Verificar patrones comunes inseguros
        var commonPatterns = new[] { "123456", "password", "qwerty", "abc123", "admin" };
        if (commonPatterns.Any(p => password.ToLower().Contains(p)))
        {
            result.IsValid = false;
            result.Errors.Add("La contraseña contiene patrones comunes no permitidos");
        }
        
        return result;
    }
    
    #endregion
    
    #region Gestión de Usuarios
    
    public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        
        return user != null ? MapToUserProfile(user) : null;
    }
    
    public async Task<AuthOperationResponse> CreateUserAsync(CreateUserRequest request, string createdBy)
    {
        try
        {
            // Verificar si usuario ya existe
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
            {
                return new AuthOperationResponse 
                { 
                    Success = false, 
                    Message = "El nombre de usuario ya existe" 
                };
            }
            
            // Validar contraseña si es usuario local
            if (!request.IsActiveDirectoryUser)
            {
                if (string.IsNullOrEmpty(request.Password))
                {
                    return new AuthOperationResponse 
                    { 
                        Success = false, 
                        Message = "La contraseña es requerida para usuarios locales" 
                    };
                }
                
                var validation = ValidatePassword(request.Password);
                if (!validation.IsValid)
                {
                    return new AuthOperationResponse 
                    { 
                        Success = false, 
                        Message = "La contraseña no cumple los requisitos: " + string.Join(", ", validation.Errors) 
                    };
                }
            }
            
            var user = new User
            {
                Username = request.Username,
                PasswordHash = !string.IsNullOrEmpty(request.Password) ? HashPassword(request.Password) : null,
                FullName = request.FullName,
                Email = request.Email,
                IsActiveDirectoryUser = request.IsActiveDirectoryUser,
                ActiveDirectoryDN = request.ActiveDirectoryDN,
                Status = UserStatus.Active,
                MustChangePassword = _config.ForcePasswordChangeOnFirstLogin && !request.IsActiveDirectoryUser,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Notes = request.Notes
            };
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            // Asignar roles
            foreach (var roleName in request.Roles)
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());
                if (role != null)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = role.Id,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = createdBy
                    });
                }
            }
            
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.UserCreated, 
                AuditResult.Success,
                $"Usuario {request.Username} creado por {createdBy}",
                null, createdBy);
            
            _logger.LogInformation("Usuario {Username} creado por {CreatedBy}", request.Username, createdBy);
            
            return new AuthOperationResponse 
            { 
                Success = true, 
                Message = "Usuario creado exitosamente",
                Data = new { UserId = user.Id }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando usuario {Username}", request.Username);
            return new AuthOperationResponse { Success = false, Message = "Error al crear usuario" };
        }
    }
    
    public async Task<AuthOperationResponse> UpdateUserAsync(int userId, UpdateUserRequest request, string modifiedBy)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Usuario no encontrado" };
            }
            
            // Actualizar campos
            if (request.FullName != null) user.FullName = request.FullName;
            if (request.Email != null) user.Email = request.Email;
            if (request.Status.HasValue) user.Status = request.Status.Value;
            if (request.MustChangePassword.HasValue) user.MustChangePassword = request.MustChangePassword.Value;
            if (request.Notes != null) user.Notes = request.Notes;
            
            user.ModifiedAt = DateTime.UtcNow;
            user.ModifiedBy = modifiedBy;
            
            // Actualizar roles si se especificaron
            if (request.Roles != null)
            {
                // Remover roles actuales
                _context.UserRoles.RemoveRange(user.UserRoles);
                
                // Asignar nuevos roles
                foreach (var roleName in request.Roles)
                {
                    var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());
                    if (role != null)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = role.Id,
                            AssignedAt = DateTime.UtcNow,
                            AssignedBy = modifiedBy
                        });
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.UserUpdated, 
                AuditResult.Success,
                $"Usuario {user.Username} modificado por {modifiedBy}",
                null, modifiedBy);
            
            return new AuthOperationResponse { Success = true, Message = "Usuario actualizado exitosamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando usuario {UserId}", userId);
            return new AuthOperationResponse { Success = false, Message = "Error al actualizar usuario" };
        }
    }
    
    public async Task<AuthOperationResponse> DeleteUserAsync(int userId, string deletedBy)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Usuario no encontrado" };
            }
            
            // No permitir eliminar el último administrador
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.SystemRole == SystemRole.Administrator);
            if (adminRole != null)
            {
                var isAdmin = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
                if (isAdmin)
                {
                    var adminCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id);
                    if (adminCount <= 1)
                    {
                        return new AuthOperationResponse 
                        { 
                            Success = false, 
                            Message = "No se puede eliminar el último administrador del sistema" 
                        };
                    }
                }
            }
            
            var username = user.Username;
            
            // Revocar todas las sesiones
            await LogoutAllSessionsAsync(userId, "Usuario eliminado");
            
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.UserDeleted, 
                AuditResult.Success,
                $"Usuario {username} eliminado por {deletedBy}",
                null, deletedBy);
            
            return new AuthOperationResponse { Success = true, Message = "Usuario eliminado exitosamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando usuario {UserId}", userId);
            return new AuthOperationResponse { Success = false, Message = "Error al eliminar usuario" };
        }
    }
    
    public async Task<AuthOperationResponse> UnlockUserAsync(int userId, string unlockedBy)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Usuario no encontrado" };
            }
            
            user.Status = UserStatus.Active;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.ModifiedAt = DateTime.UtcNow;
            user.ModifiedBy = unlockedBy;
            
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.AccountUnlocked, 
                AuditResult.Success,
                $"Usuario {user.Username} desbloqueado por {unlockedBy}",
                null, unlockedBy);
            
            return new AuthOperationResponse { Success = true, Message = "Usuario desbloqueado exitosamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error desbloqueando usuario {UserId}", userId);
            return new AuthOperationResponse { Success = false, Message = "Error al desbloquear usuario" };
        }
    }
    
    public async Task<AuthOperationResponse> ResetPasswordAsync(int userId, string newPassword, string resetBy)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Usuario no encontrado" };
            }
            
            if (user.IsActiveDirectoryUser && !_config.FallbackToLocal)
            {
                return new AuthOperationResponse 
                { 
                    Success = false, 
                    Message = "No se puede resetear la contraseña de usuarios de Active Directory" 
                };
            }
            
            var validation = ValidatePassword(newPassword);
            if (!validation.IsValid)
            {
                return new AuthOperationResponse 
                { 
                    Success = false, 
                    Message = "La contraseña no cumple los requisitos: " + string.Join(", ", validation.Errors) 
                };
            }
            
            user.PasswordHash = HashPassword(newPassword);
            user.MustChangePassword = true;
            user.PasswordChangedAt = DateTime.UtcNow;
            user.ModifiedAt = DateTime.UtcNow;
            user.ModifiedBy = resetBy;
            
            await _context.SaveChangesAsync();
            
            // Revocar todas las sesiones
            await LogoutAllSessionsAsync(userId, "Reset de contraseña");
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.PasswordReset, 
                AuditResult.Success,
                $"Contraseña de {user.Username} reseteada por {resetBy}",
                user.Id.ToString(), resetBy);
            
            return new AuthOperationResponse { Success = true, Message = "Contraseña reseteada exitosamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reseteando contraseña para usuario {UserId}", userId);
            return new AuthOperationResponse { Success = false, Message = "Error al resetear contraseña" };
        }
    }
    
    public async Task<List<UserListDto>> GetAllUsersAsync()
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Roles = u.UserRoles.Select(ur => ur.Role!.Name).ToList(),
                Status = u.Status,
                IsActiveDirectoryUser = u.IsActiveDirectoryUser,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();
    }
    
    public async Task<UserListDto?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.Id == userId)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Roles = u.UserRoles.Select(ur => ur.Role!.Name).ToList(),
                Status = u.Status,
                IsActiveDirectoryUser = u.IsActiveDirectoryUser,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();
    }
    
    #endregion
    
    #region Estado del Sistema
    
    public async Task<AuthSystemStatus> GetSystemStatusAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeSessions = await _context.UserSessions
            .CountAsync(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow);
        var lockedAccounts = await _context.Users
            .CountAsync(u => u.Status == UserStatus.Locked || 
                            (u.LockedUntil.HasValue && u.LockedUntil > DateTime.UtcNow));
        var lastLoginAttempt = await _context.LoginAttempts
            .OrderByDescending(l => l.Timestamp)
            .Select(l => l.Timestamp)
            .FirstOrDefaultAsync();
        
        return new AuthSystemStatus
        {
            Mode = _config.Mode,
            ActiveDirectoryEnabled = _config.EnableActiveDirectory,
            ActiveDirectoryConnected = false, // TODO: Verificar conexión AD si está habilitado
            ActiveDirectoryServer = _config.ADServer,
            ActiveDirectoryDomain = _config.ADDomain,
            TotalUsers = totalUsers,
            ActiveSessions = activeSessions,
            LockedAccounts = lockedAccounts,
            LastLoginAttempt = lastLoginAttempt == default ? null : lastLoginAttempt,
            PasswordPolicy = new PasswordPolicyInfo
            {
                MinLength = _config.PasswordMinLength,
                RequireUppercase = _config.RequireUppercase,
                RequireLowercase = _config.RequireLowercase,
                RequireNumbers = _config.RequireNumbers,
                RequireSpecialChars = _config.RequireSpecialChars,
                MaxLoginAttempts = _config.MaxLoginAttempts,
                LockoutMinutes = _config.LockoutMinutes,
                SessionTimeoutMinutes = _config.SessionTimeoutMinutes
            }
        };
    }
    
    public AuthConfiguration GetConfiguration() => _config;
    
    #endregion
    
    #region Inicialización
    
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Inicializando servicio de autenticación...");
        
        // Asegurar que la base de datos existe
        await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(_context);
        
        // Asegurar que existe usuario admin
        await EnsureAdminUserExistsAsync();
        
        // Limpiar sesiones expiradas
        await CleanupExpiredSessionsAsync();
        
        _logger.LogInformation("Servicio de autenticación inicializado correctamente");
    }
    
    public async Task EnsureAdminUserExistsAsync()
    {
        var adminExists = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AnyAsync(u => u.UserRoles.Any(ur => ur.Role!.SystemRole == SystemRole.Administrator));
        
        if (!adminExists)
        {
            _logger.LogWarning("No se encontró usuario administrador. Creando usuario admin por defecto...");
            
            // Contraseña temporal que cumple política
            var tempPassword = "Admin@Aquafrisch2024!";
            
            var adminUser = new User
            {
                Username = "admin",
                PasswordHash = HashPassword(tempPassword),
                FullName = "Administrador del Sistema",
                Email = "admin@aquafrisch.local",
                Status = UserStatus.Active,
                MustChangePassword = false, // Para desarrollo - en producción cambiar a true
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "SYSTEM"
            };
            
            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();
            
            // Asignar rol Administrator
            var adminRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.SystemRole == SystemRole.Administrator);
            
            if (adminRole != null)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = "SYSTEM"
                });
                
                await _context.SaveChangesAsync();
            }
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.AdminCreated, 
                AuditResult.Warning,
                "Usuario administrador por defecto creado. IMPORTANTE: Cambiar contraseña inmediatamente.",
                adminUser.Id.ToString(), "SYSTEM");
            
            _logger.LogWarning("Usuario admin creado con contraseña temporal: {Password}. DEBE CAMBIARLA EN EL PRIMER LOGIN.", 
                tempPassword);
        }
    }
    
    private async Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = await _context.UserSessions
            .Where(s => !s.IsRevoked && s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        
        foreach (var session in expiredSessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = "Sesión expirada";
        }
        
        if (expiredSessions.Count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Se limpiaron {Count} sesiones expiradas", expiredSessions.Count);
        }
    }
    
    #endregion
    
    #region Métodos Privados - Criptografía
    
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCRYPT_WORK_FACTOR);
    }
    
    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
    
    private string GenerateJwtToken(User user, DateTime expiry)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config.JwtSecretKey);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("full_name", user.FullName ?? user.Username),
            new("must_change_password", user.MustChangePassword.ToString().ToLower())
        };
        
        // Agregar roles
        foreach (var userRole in user.UserRoles)
        {
            if (userRole.Role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
            }
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry,
            Issuer = _config.JwtIssuer,
            Audience = _config.JwtAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
    
    private static string GenerateSecureKey(int length)
    {
        var randomBytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
    
    #endregion
    
    #region Métodos Privados - Active Directory
    
    /// <summary>
    /// Autenticación contra Active Directory (preparado pero deshabilitado por defecto)
    /// </summary>
    private async Task<bool> AuthenticateWithActiveDirectoryAsync(string username, string password)
    {
        if (!_config.EnableActiveDirectory || string.IsNullOrEmpty(_config.ADServer))
        {
            return false;
        }
        
        try
        {
            // TODO: Implementar autenticación AD real cuando se habilite
            // Usar System.DirectoryServices.Protocols para LDAP
            // 
            // Ejemplo de implementación:
            // using var connection = new LdapConnection(new LdapDirectoryIdentifier(_config.ADServer));
            // connection.Timeout = TimeSpan.FromSeconds(_config.ADTimeoutSeconds);
            // connection.Credential = new NetworkCredential(username, password, _config.ADDomain);
            // connection.AuthType = AuthType.Negotiate;
            // connection.Bind();
            // return true;
            
            _logger.LogWarning("Autenticación Active Directory no implementada. Usando fallback local si está habilitado.");
            await Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en autenticación Active Directory para usuario: {Username}", username);
            return false;
        }
    }
    
    #endregion
    
    #region Métodos Privados - Helpers
    
    private async Task LogLoginAttemptAsync(string username, bool success, AuthEventType eventType, 
        string? ipAddress, string? userAgent, string? failureReason, string? authMethod)
    {
        var attempt = new LoginAttempt
        {
            Username = username,
            Success = success,
            EventType = eventType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            FailureReason = failureReason,
            AuthMethod = authMethod,
            Timestamp = DateTime.UtcNow
        };
        
        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
    }
    
    private static UserProfileDto MapToUserProfile(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Roles = user.UserRoles.Select(ur => ur.Role?.Name ?? "Unknown").ToList(),
            Permissions = user.UserRoles
                .Where(ur => ur.Role?.PermissionsJson != null)
                .SelectMany(ur => ExtractPermissions(ur.Role!.PermissionsJson!))
                .Distinct()
                .ToList(),
            LastLoginAt = user.LastLoginAt,
            MustChangePassword = user.MustChangePassword,
            IsActiveDirectoryUser = user.IsActiveDirectoryUser
        };
    }
    
    private static List<string> ExtractPermissions(string permissionsJson)
    {
        try
        {
            var permissions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(permissionsJson);
            if (permissions == null) return new List<string>();
            
            return permissions.SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}:{v}")).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
    
    #region Session Management (Phase 3)
    
    public async Task<List<SessionInfoDto>> GetUserSessionsAsync(int userId)
    {
        var sessions = await _context.UserSessions
            .Include(s => s.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
        
        return sessions.Select(s => new SessionInfoDto
        {
            SessionId = s.Id,
            UserId = s.UserId,
            Username = s.User?.Username ?? "unknown",
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            CreatedAt = s.CreatedAt,
            ExpiresAt = s.ExpiresAt,
            LastActivityAt = s.LastActivityAt,
            IsCurrentSession = false, // Se marca en el controller comparando con token actual
            Roles = s.User?.UserRoles.Select(ur => ur.Role.Name).ToList() ?? new List<string>()
        }).ToList();
    }
    
    public async Task<List<SessionInfoDto>> GetAllActiveSessionsAsync()
    {
        var sessions = await _context.UserSessions
            .Include(s => s.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
        
        return sessions.Select(s => new SessionInfoDto
        {
            SessionId = s.Id,
            UserId = s.UserId,
            Username = s.User?.Username ?? "unknown",
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            CreatedAt = s.CreatedAt,
            ExpiresAt = s.ExpiresAt,
            LastActivityAt = s.LastActivityAt,
            IsCurrentSession = false,
            Roles = s.User?.UserRoles.Select(ur => ur.Role.Name).ToList() ?? new List<string>()
        }).ToList();
    }
    
    public async Task<AuthOperationResponse> CloseSessionAsync(int sessionId, int requestingUserId, string requestingUsername)
    {
        try
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            
            if (session == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Sesión no encontrada" };
            }
            
            // Verificar que el usuario solo puede cerrar sus propias sesiones
            if (session.UserId != requestingUserId)
            {
                return new AuthOperationResponse { Success = false, Message = "No tiene permiso para cerrar esta sesión" };
            }
            
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = $"Cerrada por el usuario desde otra sesión";
            
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.Logout, 
                AuditResult.Success,
                $"Sesión {sessionId} cerrada remotamente por {requestingUsername}",
                requestingUserId.ToString(), requestingUsername);
            
            return new AuthOperationResponse { Success = true, Message = "Sesión cerrada correctamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cerrando sesión {SessionId}", sessionId);
            return new AuthOperationResponse { Success = false, Message = "Error al cerrar sesión" };
        }
    }
    
    public async Task<AuthOperationResponse> AdminCloseSessionAsync(int sessionId, string adminUsername)
    {
        try
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            
            if (session == null)
            {
                return new AuthOperationResponse { Success = false, Message = "Sesión no encontrada" };
            }
            
            var targetUsername = session.User?.Username ?? "unknown";
            
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = $"Cerrada por administrador {adminUsername}";
            
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync(
                AuditCategory.Authentication, 
                AuditAction.LogoutAllSessions, 
                AuditResult.Warning,
                $"Sesión de {targetUsername} cerrada por administrador {adminUsername}",
                null, adminUsername);
            
            _logger.LogWarning("🔐 Admin {Admin} cerró sesión de {User} (SessionId: {SessionId})",
                adminUsername, targetUsername, sessionId);
            
            return new AuthOperationResponse { Success = true, Message = $"Sesión de {targetUsername} cerrada correctamente" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cerrando sesión {SessionId} como admin", sessionId);
            return new AuthOperationResponse { Success = false, Message = "Error al cerrar sesión" };
        }
    }
    
    #endregion
    
    private AuthConfiguration LoadConfigFromExcel(IExcelConfigService excelConfig)
    {
        var config = new AuthConfiguration();
        
        try
        {
            // Buscar archivo Excel de configuración
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExcelConfigs", "ProjectConfig.xlsm"),
                @"ExcelConfigs\ProjectConfig.xlsm"
            };
            
            var excelPath = possiblePaths.FirstOrDefault(File.Exists);
            
            if (excelPath != null)
            {
                var systemConfig = excelConfig.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
                
                // Mapear AuthMode
                if (Enum.TryParse<AuthenticationMode>(systemConfig.AuthMode, true, out var mode))
                {
                    config.Mode = mode;
                }
                
                // Active Directory
                config.EnableActiveDirectory = systemConfig.AuthEnableActiveDirectory;
                config.ADServer = systemConfig.AuthADServer;
                config.ADDomain = systemConfig.AuthADDomain;
                config.ADBaseDN = systemConfig.AuthADBaseDN;
                config.ADTimeoutSeconds = systemConfig.AuthADTimeoutSeconds;
                config.FallbackToLocal = systemConfig.AuthFallbackToLocal;
                
                // Base de datos
                config.DatabasePath = systemConfig.AuthDatabasePath;
                
                // Política de contraseñas
                config.PasswordMinLength = systemConfig.AuthPasswordMinLength;
                config.RequireUppercase = systemConfig.AuthRequireUppercase;
                config.RequireLowercase = systemConfig.AuthRequireLowercase;
                config.RequireNumbers = systemConfig.AuthRequireNumbers;
                config.RequireSpecialChars = systemConfig.AuthRequireSpecialChars;
                
                // Bloqueo de cuenta
                config.MaxLoginAttempts = systemConfig.AuthMaxLoginAttempts;
                config.LockoutMinutes = systemConfig.AuthLockoutMinutes;
                
                // Sesión
                config.SessionTimeoutMinutes = systemConfig.AuthSessionTimeoutMinutes;
                config.ForcePasswordChangeOnFirstLogin = systemConfig.AuthForcePasswordChangeOnFirstLogin;
                

                // Banner de login
                config.ShowLoginBanner = systemConfig.AuthShowLoginBanner;
                config.LoginBannerText = systemConfig.AuthLoginBannerText;
                
                // JWT
                config.JwtSecretKey = systemConfig.AuthJwtSecretKey;
                config.JwtIssuer = systemConfig.AuthJwtIssuer;
                config.JwtAudience = systemConfig.AuthJwtAudience;
                
                // ===== Gestión de Sesiones (Phase 3) =====
                config.MaxConcurrentSessions = systemConfig.AuthMaxConcurrentSessions;
                config.SingleSessionRoles = !string.IsNullOrEmpty(systemConfig.AuthSingleSessionRoles) 
                    ? systemConfig.AuthSingleSessionRoles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Trim())
                        .ToList()
                    : new List<string> { "Operator" };
                config.InactivityTimeoutMinutes = systemConfig.AuthInactivityTimeoutMinutes;
                config.TrackLastActivity = systemConfig.AuthTrackLastActivity;
                config.SingleSessionBehavior = systemConfig.AuthSingleSessionBehavior;
                
                // ===== RBAC - Role Based Access Control (Phase 4) =====
                config.DefaultRole = systemConfig.AuthDefaultRole;
                config.EnableGuestRole = systemConfig.AuthEnableGuestRole;
                config.GuestPermissions = !string.IsNullOrEmpty(systemConfig.AuthGuestPermissions)
                    ? systemConfig.AuthGuestPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToList()
                    : new List<string> { "plc:read" };
                config.RequireUserApproval = systemConfig.AuthRequireUserApproval;
                config.NotifyAdminOnNewUser = systemConfig.AuthNotifyAdminOnNewUser;
                config.OperatorExtraPermissions = !string.IsNullOrEmpty(systemConfig.AuthOperatorExtraPermissions)
                    ? systemConfig.AuthOperatorExtraPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToList()
                    : new List<string>();
                config.MaintenanceExtraPermissions = !string.IsNullOrEmpty(systemConfig.AuthMaintenanceExtraPermissions)
                    ? systemConfig.AuthMaintenanceExtraPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToList()
                    : new List<string>();
                config.RestrictedPermissions = !string.IsNullOrEmpty(systemConfig.AuthRestrictedPermissions)
                    ? systemConfig.AuthRestrictedPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToList()
                    : new List<string> { "backup:restore", "security:update" };
                config.EnableRoleHierarchy = systemConfig.AuthEnableRoleHierarchy;
                
                _logger.LogInformation("🔐 Configuración de autenticación cargada desde Excel:");
                _logger.LogInformation("  - Mode: {Mode}, AD: {AD}, Fallback: {Fallback}", 
                    config.Mode, config.EnableActiveDirectory, config.FallbackToLocal);
                _logger.LogInformation("  - Password: MinLen={Min}, Upper={U}, Lower={L}, Num={N}, Special={S}",
                    config.PasswordMinLength, config.RequireUppercase, config.RequireLowercase, 
                    config.RequireNumbers, config.RequireSpecialChars);
                _logger.LogInformation("  - Lockout: {Attempts} attempts, {Minutes} min", 
                    config.MaxLoginAttempts, config.LockoutMinutes);
                _logger.LogInformation("  - Sessions: MaxConcurrent={Max}, SingleRoles=[{Roles}], Inactivity={Inact}min",
                    config.MaxConcurrentSessions, string.Join(",", config.SingleSessionRoles), config.InactivityTimeoutMinutes);
                _logger.LogInformation("  - RBAC: DefaultRole={Role}, GuestEnabled={Guest}, ApprovalRequired={Approval}",
                    config.DefaultRole, config.EnableGuestRole, config.RequireUserApproval);
            }
            else
            {
                _logger.LogWarning("🔐 Excel no encontrado. Usando configuración de autenticación por defecto.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🔐 Error cargando configuración de autenticación desde Excel. Usando valores por defecto.");
        }
        
        return config;
    }
    
    #endregion
}
