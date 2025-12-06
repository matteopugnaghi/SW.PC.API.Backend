// ============================================================================
// AuthModels.cs - Modelos de Autenticación y Autorización
// ============================================================================
// Sistema de autenticación compatible con EU CRA y requisitos CADRA/Alstom
// Soporte para Active Directory (deshabilitado por defecto) + Autenticación Local
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models;

#region Enumeraciones

/// <summary>
/// Roles predefinidos del sistema según CADRA Cyber SYS
/// </summary>
public enum SystemRole
{
    /// <summary>Administrador del sistema - Acceso total</summary>
    Administrator = 1,
    
    /// <summary>Operador - Control de proceso, sin configuración</summary>
    Operator = 2,
    
    /// <summary>Mantenimiento - Configuración técnica, sin seguridad</summary>
    Maintenance = 3,
    
    /// <summary>Visualización - Solo lectura</summary>
    Viewer = 4,
    
    /// <summary>Auditor - Acceso a logs y reportes de seguridad</summary>
    Auditor = 5
}

/// <summary>
/// Modo de autenticación del sistema
/// </summary>
public enum AuthenticationMode
{
    /// <summary>Solo autenticación local (base de datos SQLite)</summary>
    Local = 1,
    
    /// <summary>Solo Active Directory</summary>
    ActiveDirectory = 2,
    
    /// <summary>Active Directory con fallback a local si AD no disponible</summary>
    Hybrid = 3
}

/// <summary>
/// Estado de la cuenta de usuario
/// </summary>
public enum UserStatus
{
    /// <summary>Cuenta activa</summary>
    Active = 1,
    
    /// <summary>Cuenta bloqueada por intentos fallidos</summary>
    Locked = 2,
    
    /// <summary>Cuenta deshabilitada por administrador</summary>
    Disabled = 3,
    
    /// <summary>Cuenta pendiente de activación</summary>
    Pending = 4,
    
    /// <summary>Cuenta expirada</summary>
    Expired = 5
}

/// <summary>
/// Tipo de evento de autenticación para auditoría
/// </summary>
public enum AuthEventType
{
    LoginSuccess,
    LoginFailed,
    LoginBlocked,
    Logout,
    PasswordChanged,
    PasswordReset,
    AccountLocked,
    AccountUnlocked,
    AccountCreated,
    AccountModified,
    AccountDeleted,
    SessionExpired,
    SessionRefreshed,
    RoleAssigned,
    RoleRevoked,
    PermissionDenied
}

#endregion

#region Entidades de Base de Datos

/// <summary>
/// Entidad de Usuario - Almacenada en SQLite
/// </summary>
[Table("Users")]
public class User
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Nombre de usuario único (login)</summary>
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>Hash BCrypt de la contraseña (null si usuario AD)</summary>
    [MaxLength(255)]
    public string? PasswordHash { get; set; }
    
    /// <summary>Nombre completo del usuario</summary>
    [MaxLength(200)]
    public string? FullName { get; set; }
    
    /// <summary>Email del usuario</summary>
    [MaxLength(200)]
    public string? Email { get; set; }
    
    /// <summary>Usuario es de Active Directory</summary>
    public bool IsActiveDirectoryUser { get; set; } = false;
    
    /// <summary>Distinguished Name en AD (si aplica)</summary>
    [MaxLength(500)]
    public string? ActiveDirectoryDN { get; set; }
    
    /// <summary>Estado actual de la cuenta</summary>
    public UserStatus Status { get; set; } = UserStatus.Active;
    
    /// <summary>Número de intentos de login fallidos consecutivos</summary>
    public int FailedLoginAttempts { get; set; } = 0;
    
    /// <summary>Fecha/hora del último intento fallido</summary>
    public DateTime? LastFailedLoginAt { get; set; }
    
    /// <summary>Fecha/hora hasta cuando está bloqueada la cuenta</summary>
    public DateTime? LockedUntil { get; set; }
    
    /// <summary>Fecha/hora del último login exitoso</summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>IP del último login exitoso</summary>
    [MaxLength(50)]
    public string? LastLoginIp { get; set; }
    
    /// <summary>Requiere cambio de contraseña en próximo login</summary>
    public bool MustChangePassword { get; set; } = true;
    
    /// <summary>Fecha/hora del último cambio de contraseña</summary>
    public DateTime? PasswordChangedAt { get; set; }
    
    /// <summary>Fecha de creación de la cuenta</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Usuario que creó esta cuenta</summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    /// <summary>Fecha de última modificación</summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>Usuario que modificó por última vez</summary>
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
    
    /// <summary>Notas o comentarios sobre el usuario</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    // Navegación
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}

/// <summary>
/// Entidad de Rol - Roles predefinidos del sistema
/// </summary>
[Table("Roles")]
public class Role
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Nombre del rol</summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Descripción del rol</summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>Rol del sistema (enum)</summary>
    public SystemRole SystemRole { get; set; }
    
    /// <summary>Permisos en formato JSON</summary>
    [MaxLength(4000)]
    public string? PermissionsJson { get; set; }
    
    /// <summary>Es rol del sistema (no se puede eliminar)</summary>
    public bool IsSystemRole { get; set; } = true;
    
    // Navegación
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>
/// Relación Usuario-Rol (muchos a muchos)
/// </summary>
[Table("UserRoles")]
public class UserRole
{
    [Key]
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public int RoleId { get; set; }
    
    /// <summary>Fecha de asignación del rol</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Usuario que asignó el rol</summary>
    [MaxLength(100)]
    public string? AssignedBy { get; set; }
    
    // Navegación
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
    
    [ForeignKey("RoleId")]
    public virtual Role? Role { get; set; }
}

/// <summary>
/// Sesión de usuario activa
/// </summary>
[Table("UserSessions")]
public class UserSession
{
    [Key]
    public int Id { get; set; }
    
    public int UserId { get; set; }
    
    /// <summary>Token JWT único de la sesión</summary>
    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;
    
    /// <summary>Refresh token para renovar sesión</summary>
    [MaxLength(500)]
    public string? RefreshToken { get; set; }
    
    /// <summary>IP desde donde se inició la sesión</summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    /// <summary>User-Agent del cliente</summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>Fecha/hora de inicio de sesión</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Fecha/hora de expiración</summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>Fecha/hora de última actividad</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Sesión revocada (logout o forzado)</summary>
    public bool IsRevoked { get; set; } = false;
    
    /// <summary>Fecha/hora de revocación</summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>Motivo de revocación</summary>
    [MaxLength(200)]
    public string? RevokedReason { get; set; }
    
    // Navegación
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}

/// <summary>
/// Registro de intentos de login para auditoría y análisis
/// </summary>
[Table("LoginAttempts")]
public class LoginAttempt
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Nombre de usuario intentado</summary>
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>Login exitoso o fallido</summary>
    public bool Success { get; set; }
    
    /// <summary>Tipo de evento</summary>
    public AuthEventType EventType { get; set; }
    
    /// <summary>IP del intento</summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    /// <summary>User-Agent del cliente</summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>Motivo de fallo (si aplica)</summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }
    
    /// <summary>Método de autenticación usado</summary>
    [MaxLength(50)]
    public string? AuthMethod { get; set; }
    
    /// <summary>Fecha/hora del intento</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region DTOs de Request/Response

/// <summary>
/// Request de login
/// </summary>
public class LoginRequest
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
    
    /// <summary>Recordar sesión (token de mayor duración)</summary>
    public bool RememberMe { get; set; } = false;
}

/// <summary>
/// Response de login exitoso
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserProfileDto? User { get; set; }
    public string? Message { get; set; }
    public bool MustChangePassword { get; set; }
    public int? RemainingAttempts { get; set; }
    public int? LockoutMinutes { get; set; }
}

/// <summary>
/// Perfil de usuario para respuestas
/// </summary>
public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsActiveDirectoryUser { get; set; }
}

/// <summary>
/// Request de cambio de contraseña
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    
    [Required]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response de cambio de contraseña
/// </summary>
public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string>? ValidationErrors { get; set; }
}

/// <summary>
/// Request para crear usuario
/// </summary>
public class CreateUserRequest
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Password { get; set; }
    
    [MaxLength(200)]
    public string? FullName { get; set; }
    
    [MaxLength(200)]
    public string? Email { get; set; }
    
    public List<string> Roles { get; set; } = new();
    
    public bool IsActiveDirectoryUser { get; set; } = false;
    
    [MaxLength(500)]
    public string? ActiveDirectoryDN { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request para actualizar usuario
/// </summary>
public class UpdateUserRequest
{
    [MaxLength(200)]
    public string? FullName { get; set; }
    
    [MaxLength(200)]
    public string? Email { get; set; }
    
    public List<string>? Roles { get; set; }
    
    public UserStatus? Status { get; set; }
    
    public bool? MustChangePassword { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Response genérica de operación
/// </summary>
public class AuthOperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}

/// <summary>
/// DTO para listar usuarios
/// </summary>
public class UserListDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public UserStatus Status { get; set; }
    public bool IsActiveDirectoryUser { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Estado del sistema de autenticación
/// </summary>
public class AuthSystemStatus
{
    public AuthenticationMode Mode { get; set; }
    public bool ActiveDirectoryEnabled { get; set; }
    public bool ActiveDirectoryConnected { get; set; }
    public string? ActiveDirectoryServer { get; set; }
    public string? ActiveDirectoryDomain { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveSessions { get; set; }
    public int LockedAccounts { get; set; }
    public DateTime? LastLoginAttempt { get; set; }
    public PasswordPolicyInfo PasswordPolicy { get; set; } = new();
}

/// <summary>
/// Información de política de contraseñas
/// </summary>
public class PasswordPolicyInfo
{
    public int MinLength { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireNumbers { get; set; }
    public bool RequireSpecialChars { get; set; }
    public int MaxLoginAttempts { get; set; }
    public int LockoutMinutes { get; set; }
    public int SessionTimeoutMinutes { get; set; }
}

/// <summary>
/// Resultado de validación de contraseña
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

#endregion

#region Configuración de Autenticación (desde Excel)

/// <summary>
/// Configuración de autenticación cargada desde Excel
/// </summary>
public class AuthConfiguration
{
    /// <summary>Modo de autenticación: Local, ActiveDirectory, Hybrid</summary>
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.Local;
    
    /// <summary>Habilitar Active Directory</summary>
    public bool EnableActiveDirectory { get; set; } = false;
    
    /// <summary>Servidor AD (LDAP://server:port)</summary>
    public string? ADServer { get; set; }
    
    /// <summary>Dominio AD</summary>
    public string? ADDomain { get; set; }
    
    /// <summary>Base DN para búsquedas AD</summary>
    public string? ADBaseDN { get; set; }
    
    /// <summary>Timeout de conexión AD en segundos</summary>
    public int ADTimeoutSeconds { get; set; } = 10;
    
    /// <summary>Si AD falla, usar autenticación local</summary>
    public bool FallbackToLocal { get; set; } = true;
    
    /// <summary>Ruta a la base de datos SQLite</summary>
    public string DatabasePath { get; set; } = "Data/Aquafrisch.db";
    
    // Política de contraseñas
    public int PasswordMinLength { get; set; } = 12;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireNumbers { get; set; } = true;
    public bool RequireSpecialChars { get; set; } = true;
    
    // Bloqueo de cuenta
    public int MaxLoginAttempts { get; set; } = 6;
    public int LockoutMinutes { get; set; } = 15;
    
    // Sesión
    public int SessionTimeoutMinutes { get; set; } = 30;
    public bool ForcePasswordChangeOnFirstLogin { get; set; } = true;
    
    // Banner de login
    public bool ShowLoginBanner { get; set; } = true;
    public string LoginBannerText { get; set; } = "ACCESO RESTRINGIDO - Solo personal autorizado. Todas las actividades son monitoreadas y registradas.";
    
    // JWT
    public string JwtSecretKey { get; set; } = string.Empty; // Se genera si está vacío
    public string JwtIssuer { get; set; } = "AquafrischSupervisor";
    public string JwtAudience { get; set; } = "AquafrischClients";
    
    // ===== Gestión de Sesiones (Phase 3) =====
    /// <summary>Máximo sesiones concurrentes por usuario (0=ilimitado)</summary>
    public int MaxConcurrentSessions { get; set; } = 2;
    
    /// <summary>Roles con sesión única (solo 1 activo a la vez)</summary>
    public List<string> SingleSessionRoles { get; set; } = new() { "Operator" };
    
    /// <summary>Timeout por inactividad en minutos (0=deshabilitado)</summary>
    public int InactivityTimeoutMinutes { get; set; } = 15;
    
    /// <summary>Rastrear última actividad</summary>
    public bool TrackLastActivity { get; set; } = true;
    
    /// <summary>Comportamiento de sesión única: "reject" o "force"</summary>
    public string SingleSessionBehavior { get; set; } = "reject";
    
    // ===== RBAC - Role Based Access Control (Phase 4) =====
    /// <summary>Rol por defecto para nuevos usuarios</summary>
    public string DefaultRole { get; set; } = "Viewer";
    
    /// <summary>Habilitar rol de invitado (usuario anónimo)</summary>
    public bool EnableGuestRole { get; set; } = false;
    
    /// <summary>Permisos para rol invitado</summary>
    public List<string> GuestPermissions { get; set; } = new() { "plc:read" };
    
    /// <summary>Requerir aprobación de admin para nuevos usuarios</summary>
    public bool RequireUserApproval { get; set; } = true;
    
    /// <summary>Notificar a admins cuando se crea nuevo usuario</summary>
    public bool NotifyAdminOnNewUser { get; set; } = true;
    
    /// <summary>Permisos extra para Operator (desde Excel)</summary>
    public List<string> OperatorExtraPermissions { get; set; } = new();
    
    /// <summary>Permisos extra para Maintenance (desde Excel)</summary>
    public List<string> MaintenanceExtraPermissions { get; set; } = new();
    
    /// <summary>Permisos restringidos (solo Administrator)</summary>
    public List<string> RestrictedPermissions { get; set; } = new() { "backup:restore", "security:update" };
    
    /// <summary>Habilitar jerarquía de roles</summary>
    public bool EnableRoleHierarchy { get; set; } = false;
}

/// <summary>
/// DTO de información de sesión activa (Phase 3)
/// </summary>
public class SessionInfoDto
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsCurrentSession { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// DTO de configuración de sesiones para frontend (Phase 3)
/// </summary>
public class SessionConfigDto
{
    public int MaxConcurrentSessions { get; set; }
    public List<string> SingleSessionRoles { get; set; } = new();
    public int InactivityTimeoutMinutes { get; set; }
    public int SessionTimeoutMinutes { get; set; }
}

#endregion
