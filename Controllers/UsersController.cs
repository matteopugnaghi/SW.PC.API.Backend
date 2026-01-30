// ============================================================================
// UsersController.cs - API de Gestión de Usuarios
// ============================================================================
// Endpoints para administración de usuarios según EU CRA
// - SuperAdmin: Acceso TOTAL a todos los usuarios
// - Administrator: Gestión de usuarios de su instalación (NO ve SuperAdmins)
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using System.Security.Claims;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IAuthenticationService authService,
        IAuditLogService auditLog,
        ILogger<UsersController> logger)
    {
        _authService = authService;
        _auditLog = auditLog;
        _logger = logger;
    }

    #region Helpers

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    private string GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
    }

    private bool IsSuperAdmin()
    {
        return User.IsInRole("SuperAdmin");
    }

    private bool IsAdminOrSuperAdmin()
    {
        return User.IsInRole("Administrator") || User.IsInRole("SuperAdmin");
    }

    private SystemRole GetCurrentUserRole()
    {
        if (User.IsInRole("SuperAdmin")) return SystemRole.SuperAdmin;
        if (User.IsInRole("Administrator")) return SystemRole.Administrator;
        if (User.IsInRole("Operator")) return SystemRole.Operator;
        if (User.IsInRole("Maintenance")) return SystemRole.Maintenance;
        if (User.IsInRole("Auditor")) return SystemRole.Auditor;
        return SystemRole.Viewer;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Obtiene la lista de usuarios según el rol del solicitante
    /// SuperAdmin: Ve TODOS los usuarios
    /// Administrator: Ve todos EXCEPTO SuperAdmins
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<List<UserListDto>>> GetUsers()
    {
        try
        {
            var currentRole = GetCurrentUserRole();
            var users = await _authService.GetUsersForRoleAsync(currentRole);
            
            _logger.LogInformation("Usuario {User} ({Role}) consultó lista de usuarios. Total: {Count}",
                GetCurrentUsername(), currentRole, users.Count);

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo lista de usuarios");
            return StatusCode(500, new { message = "Error al obtener usuarios" });
        }
    }

    /// <summary>
    /// Obtiene un usuario por ID
    /// Administrator no puede ver usuarios SuperAdmin
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<UserListDto>> GetUser(int id)
    {
        try
        {
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Verificar permisos: Administrator no puede ver SuperAdmins
            if (!IsSuperAdmin() && user.Roles.Contains("SuperAdmin"))
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo usuario {UserId}", id);
            return StatusCode(500, new { message = "Error al obtener usuario" });
        }
    }

    /// <summary>
    /// Crea un nuevo usuario
    /// Administrator NO puede crear usuarios SuperAdmin
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<AuthOperationResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            // Verificar que Administrator no intente crear SuperAdmin
            if (!IsSuperAdmin() && request.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.PermissionDenied,
                    AuditResult.Failure,
                    $"Usuario {GetCurrentUsername()} intentó crear un SuperAdmin sin permisos",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return Forbid();
            }

            var result = await _authService.CreateUserAsync(request, GetCurrentUsername());
            
            if (result.Success)
            {
                _logger.LogInformation("Usuario {NewUser} creado por {Creator}",
                    request.Username, GetCurrentUsername());
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando usuario");
            return StatusCode(500, new { message = "Error al crear usuario" });
        }
    }

    /// <summary>
    /// Actualiza un usuario existente
    /// Administrator NO puede modificar usuarios SuperAdmin
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<AuthOperationResponse>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            // Verificar que el usuario objetivo existe
            var targetUser = await _authService.GetUserByIdAsync(id);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Administrator no puede modificar SuperAdmins
            if (!IsSuperAdmin() && targetUser.Roles.Contains("SuperAdmin"))
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.PermissionDenied,
                    AuditResult.Failure,
                    $"Usuario {GetCurrentUsername()} intentó modificar SuperAdmin {targetUser.Username}",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Administrator no puede asignar rol SuperAdmin
            if (!IsSuperAdmin() && request.Roles?.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase) == true)
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.PermissionDenied,
                    AuditResult.Failure,
                    $"Usuario {GetCurrentUsername()} intentó asignar rol SuperAdmin sin permisos",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return Forbid();
            }

            var result = await _authService.UpdateUserAsync(id, request, GetCurrentUsername());
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando usuario {UserId}", id);
            return StatusCode(500, new { message = "Error al actualizar usuario" });
        }
    }

    /// <summary>
    /// Elimina un usuario
    /// Administrator NO puede eliminar SuperAdmins
    /// Nadie puede eliminarse a sí mismo
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<AuthOperationResponse>> DeleteUser(int id)
    {
        try
        {
            // No se puede eliminar a sí mismo
            if (id == GetCurrentUserId())
            {
                return BadRequest(new { message = "No puede eliminar su propia cuenta" });
            }

            // Verificar que el usuario existe
            var targetUser = await _authService.GetUserByIdAsync(id);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Administrator no puede eliminar SuperAdmins
            if (!IsSuperAdmin() && targetUser.Roles.Contains("SuperAdmin"))
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.PermissionDenied,
                    AuditResult.Failure,
                    $"Usuario {GetCurrentUsername()} intentó eliminar SuperAdmin {targetUser.Username}",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return NotFound(new { message = "Usuario no encontrado" });
            }

            var result = await _authService.DeleteUserAsync(id, GetCurrentUsername());
            
            if (result.Success)
            {
                _logger.LogWarning("Usuario {DeletedUser} eliminado por {Deleter}",
                    targetUser.Username, GetCurrentUsername());
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando usuario {UserId}", id);
            return StatusCode(500, new { message = "Error al eliminar usuario" });
        }
    }

    /// <summary>
    /// Desbloquea una cuenta de usuario bloqueada
    /// </summary>
    [HttpPost("{id}/unlock")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<AuthOperationResponse>> UnlockUser(int id)
    {
        try
        {
            var targetUser = await _authService.GetUserByIdAsync(id);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Administrator no puede desbloquear SuperAdmins
            if (!IsSuperAdmin() && targetUser.Roles.Contains("SuperAdmin"))
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            var result = await _authService.UnlockUserAsync(id, GetCurrentUsername());
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error desbloqueando usuario {UserId}", id);
            return StatusCode(500, new { message = "Error al desbloquear usuario" });
        }
    }

    /// <summary>
    /// Resetea la contraseña de un usuario
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<AuthOperationResponse>> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            var targetUser = await _authService.GetUserByIdAsync(id);
            if (targetUser == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // Administrator no puede resetear contraseña de SuperAdmins
            if (!IsSuperAdmin() && targetUser.Roles.Contains("SuperAdmin"))
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            var result = await _authService.ResetPasswordAsync(id, request.NewPassword, GetCurrentUsername());
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reseteando contraseña de usuario {UserId}", id);
            return StatusCode(500, new { message = "Error al resetear contraseña" });
        }
    }

    /// <summary>
    /// Obtiene los roles disponibles para asignar
    /// Administrator no ve el rol SuperAdmin
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public ActionResult<List<RoleInfoDto>> GetAvailableRoles()
    {
        var roles = new List<RoleInfoDto>
        {
            new() { Name = "Administrator", Description = "Administrador del Cliente - Gestión de usuarios de su instalación", Level = 1 },
            new() { Name = "Operator", Description = "Operador de proceso - Control de operaciones", Level = 2 },
            new() { Name = "Maintenance", Description = "Personal de mantenimiento - Configuración técnica", Level = 3 },
            new() { Name = "Viewer", Description = "Solo visualización - Acceso de solo lectura", Level = 4 },
            new() { Name = "Auditor", Description = "Auditor de seguridad - Acceso a logs y reportes", Level = 5 }
        };

        // SuperAdmin puede ver y asignar rol SuperAdmin
        if (IsSuperAdmin())
        {
            roles.Insert(0, new RoleInfoDto 
            { 
                Name = "SuperAdmin", 
                Description = "Super Administrador (Solo Fabricante) - Acceso TOTAL al sistema", 
                Level = 0 
            });
        }

        return Ok(roles);
    }

    /// <summary>
    /// Obtiene los permisos de un rol específico
    /// SuperAdmin puede obtener permisos de cualquier rol
    /// Administrator no puede obtener permisos de SuperAdmin
    /// </summary>
    [HttpGet("roles/{roleName}/permissions")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<RolePermissions>> GetRolePermissions(string roleName)
    {
        try
        {
            // Administrator no puede ver permisos de SuperAdmin
            if (!IsSuperAdmin() && roleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "Rol no encontrado" });
            }

            var permissionsService = HttpContext.RequestServices.GetRequiredService<IRolePermissionsService>();
            var permissions = await permissionsService.GetRolePermissionsAsync(roleName);

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo permisos del rol {RoleName}", roleName);
            return StatusCode(500, new { message = "Error al obtener permisos" });
        }
    }

    /// <summary>
    /// Actualiza los permisos de un rol
    /// SuperAdmin puede modificar cualquier rol
    /// Administrator puede modificar: Maintenance, Operator, Viewer, Auditor
    /// </summary>
    [HttpPut("roles/{roleName}/permissions")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<PermissionsOperationResponse>> UpdateRolePermissions(
        string roleName, 
        [FromBody] UpdateRolePermissionsRequest request)
    {
        try
        {
            // Validar que Administrator no intente modificar SuperAdmin o Administrator
            if (!IsSuperAdmin())
            {
                if (roleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                    roleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                {
                    await _auditLog.LogAsync(
                        AuditCategory.Configuration,
                        AuditAction.PermissionDenied,
                        AuditResult.Failure,
                        $"Usuario {GetCurrentUsername()} intentó modificar permisos del rol {roleName}",
                        GetCurrentUserId().ToString(),
                        GetCurrentUsername(),
                        HttpContext.Connection.RemoteIpAddress?.ToString());

                    return Forbid();
                }
            }

            var permissionsService = HttpContext.RequestServices.GetRequiredService<IRolePermissionsService>();
            var result = await permissionsService.UpdateRolePermissionsAsync(
                roleName, 
                request.Modules, 
                GetCurrentUsername());

            if (result.Success)
            {
                await _auditLog.LogAsync(
                    AuditCategory.Configuration,
                    AuditAction.Modified,
                    AuditResult.Success,
                    $"Permisos del rol {roleName} actualizados",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                _logger.LogInformation("Usuario {User} actualizó permisos del rol {RoleName}", 
                    GetCurrentUsername(), roleName);
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando permisos del rol {RoleName}", roleName);
            return StatusCode(500, new { message = "Error al actualizar permisos" });
        }
    }

    /// <summary>
    /// Restaura los permisos por defecto de un rol
    /// </summary>
    [HttpPost("roles/{roleName}/permissions/reset")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public async Task<ActionResult<PermissionsOperationResponse>> ResetRolePermissions(string roleName)
    {
        try
        {
            // Validar permisos igual que en Update
            if (!IsSuperAdmin())
            {
                if (roleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                    roleName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var permissionsService = HttpContext.RequestServices.GetRequiredService<IRolePermissionsService>();
            var result = await permissionsService.ResetToDefaultPermissionsAsync(roleName, GetCurrentUsername());

            if (result.Success)
            {
                await _auditLog.LogAsync(
                    AuditCategory.Configuration,
                    AuditAction.Modified,
                    AuditResult.Success,
                    $"Permisos del rol {roleName} restaurados a valores por defecto",
                    GetCurrentUserId().ToString(),
                    GetCurrentUsername(),
                    HttpContext.Connection.RemoteIpAddress?.ToString());
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restaurando permisos del rol {RoleName}", roleName);
            return StatusCode(500, new { message = "Error al restaurar permisos" });
        }
    }

    /// <summary>
    /// Obtiene la lista de módulos/vistas disponibles en el sistema
    /// </summary>
    [HttpGet("modules")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public ActionResult<List<ModuleInfo>> GetAvailableModules()
    {
        try
        {
            var permissionsService = HttpContext.RequestServices.GetRequiredService<IRolePermissionsService>();
            var modules = permissionsService.GetAvailableModules();
            return Ok(modules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo módulos disponibles");
            return StatusCode(500, new { message = "Error al obtener módulos" });
        }
    }

    /// <summary>
    /// Obtiene información de ayuda sobre el sistema de usuarios y roles (EU CRA)
    /// </summary>
    [HttpGet("help")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    public ActionResult<UserManagementHelpDto> GetHelp()
    {
        var help = new UserManagementHelpDto
        {
            Title = "Gestión de Usuarios - EU CRA Compliance",
            Description = "Sistema de gestión de usuarios conforme al Reglamento Europeo de Ciberresiliencia (EU CRA)",
            RoleHierarchy = new List<RoleHierarchyInfo>
            {
                new() {
                    Level = 0,
                    Name = "SuperAdmin",
                    Description = "Solo Fabricante (Aquafrisch)",
                    Capabilities = new List<string>
                    {
                        "Acceso TOTAL al sistema",
                        "Modificación de PLC/TwinCAT",
                        "Actualización de firmware",
                        "Gestión de TODOS los usuarios (incluyendo otros SuperAdmin)",
                        "Acceso al código fuente",
                        "Configuración de sistema y licencias"
                    },
                    Restrictions = new List<string>
                    {
                        "Credenciales NO se entregan al cliente",
                        "Uso exclusivo del personal de Aquafrisch"
                    },
                    VisibleToAdmin = false
                },
                new() {
                    Level = 1,
                    Name = "Administrator",
                    Description = "Responsable de Seguridad del Cliente",
                    Capabilities = new List<string>
                    {
                        "Gestión de usuarios de SU instalación",
                        "Crear, modificar, eliminar usuarios (excepto SuperAdmin)",
                        "Asignar roles (excepto SuperAdmin)",
                        "Ver logs de auditoría",
                        "Configuración operativa",
                        "Gestión de recetas y alarmas"
                    },
                    Restrictions = new List<string>
                    {
                        "NO puede ver usuarios SuperAdmin",
                        "NO puede modificar PLC/TwinCAT",
                        "NO puede actualizar firmware",
                        "NO puede acceder al código fuente"
                    },
                    VisibleToAdmin = true
                },
                new() {
                    Level = 2,
                    Name = "Operator",
                    Description = "Operador de Proceso",
                    Capabilities = new List<string>
                    {
                        "Control de operaciones de proceso",
                        "Reconocimiento de alarmas",
                        "Ejecución de recetas",
                        "Visualización de datos"
                    },
                    Restrictions = new List<string>
                    {
                        "Sin acceso a configuración",
                        "Sin gestión de usuarios"
                    },
                    VisibleToAdmin = true
                },
                new() {
                    Level = 3,
                    Name = "Maintenance",
                    Description = "Personal de Mantenimiento",
                    Capabilities = new List<string>
                    {
                        "Configuración técnica",
                        "Diagnósticos del sistema",
                        "Gestión de recetas",
                        "Calibración de equipos"
                    },
                    Restrictions = new List<string>
                    {
                        "Sin acceso a seguridad",
                        "Sin gestión de usuarios"
                    },
                    VisibleToAdmin = true
                },
                new() {
                    Level = 4,
                    Name = "Viewer",
                    Description = "Usuario de Solo Lectura",
                    Capabilities = new List<string>
                    {
                        "Visualización de datos de proceso",
                        "Lectura de alarmas",
                        "Consulta de reportes"
                    },
                    Restrictions = new List<string>
                    {
                        "Sin capacidad de modificación",
                        "Solo lectura"
                    },
                    VisibleToAdmin = true
                },
                new() {
                    Level = 5,
                    Name = "Auditor",
                    Description = "Auditor de Seguridad",
                    Capabilities = new List<string>
                    {
                        "Acceso a logs de auditoría",
                        "Exportación de reportes de seguridad",
                        "Revisión de compliance"
                    },
                    Restrictions = new List<string>
                    {
                        "Sin capacidad de modificación",
                        "Solo lectura de seguridad"
                    },
                    VisibleToAdmin = true
                }
            },
            SecurityNotes = new List<string>
            {
                "🔐 Las contraseñas deben cumplir la política de seguridad (mín. 12 caracteres, mayúsculas, minúsculas, números y caracteres especiales)",
                "⏱️ Las cuentas se bloquean automáticamente tras 6 intentos fallidos durante 15 minutos",
                "📋 Todas las acciones de usuario son registradas en el log de auditoría",
                "🔄 Los usuarios nuevos deben cambiar su contraseña en el primer inicio de sesión",
                "👁️ Los usuarios SuperAdmin son invisibles para los Administradores del cliente"
            },
            EuCraCompliance = new List<string>
            {
                "Artículo 10 - Separación de privilegios implementada",
                "Anexo I - Control de acceso basado en roles (RBAC)",
                "Trazabilidad completa de acciones de usuario",
                "Política de contraseñas conforme a mejores prácticas"
            }
        };

        // Filtrar información de SuperAdmin si el solicitante es Administrator
        if (!IsSuperAdmin())
        {
            help.RoleHierarchy = help.RoleHierarchy.Where(r => r.VisibleToAdmin).ToList();
        }

        return Ok(help);
    }

    #endregion
}

#region DTOs adicionales

// ResetPasswordRequest ya definida en AuthController.cs - usamos esa

public class RoleInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
}

public class UserManagementHelpDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RoleHierarchyInfo> RoleHierarchy { get; set; } = new();
    public List<string> SecurityNotes { get; set; } = new();
    public List<string> EuCraCompliance { get; set; } = new();
}

public class RoleHierarchyInfo
{
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public List<string> Restrictions { get; set; } = new();
    public bool VisibleToAdmin { get; set; } = true;
}

#endregion
