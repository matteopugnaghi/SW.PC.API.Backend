// ============================================================================
// IRolePermissionsService.cs - Servicio de Gestión de Permisos de Roles
// ============================================================================
// Gestión persistente de permisos por rol con soporte multi-proyecto
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interfaz del servicio de gestión de permisos por rol
/// </summary>
public interface IRolePermissionsService
{
    /// <summary>
    /// Obtiene los permisos de un rol específico
    /// Si no existen, devuelve permisos por defecto
    /// </summary>
    Task<RolePermissions> GetRolePermissionsAsync(string roleName);
    
    /// <summary>
    /// Obtiene los permisos de un rol por su ID
    /// </summary>
    Task<RolePermissions?> GetRolePermissionsByIdAsync(int roleId);
    
    /// <summary>
    /// Actualiza los permisos de un rol
    /// </summary>
    Task<PermissionsOperationResponse> UpdateRolePermissionsAsync(
        string roleName, 
        ModulePermissions permissions, 
        string updatedBy);
    
    /// <summary>
    /// Obtiene todos los módulos/vistas disponibles con sus descripciones
    /// </summary>
    List<ModuleInfo> GetAvailableModules();
    
    /// <summary>
    /// Verifica si un rol tiene un permiso específico
    /// </summary>
    Task<bool> HasPermissionAsync(string roleName, string module, string action);
    
    /// <summary>
    /// Obtiene la ViewPermission completa de un módulo para un rol
    /// (incluye AllowedOrigins para evaluación de restricción por origen).
    /// null si el módulo no existe.
    /// </summary>
    Task<ViewPermission?> GetModulePermissionAsync(string roleName, string module);
    
    /// <summary>
    /// Restaura los permisos por defecto de un rol
    /// </summary>
    Task<PermissionsOperationResponse> ResetToDefaultPermissionsAsync(string roleName, string updatedBy);
}
