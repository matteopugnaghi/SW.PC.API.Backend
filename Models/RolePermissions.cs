// ============================================================================
// RolePermissions.cs - Modelo de Permisos por Rol
// ============================================================================
// Sistema de permisos granulares para control de acceso a vistas/módulos
// Compatible con EU CRA - Principio de mínimo privilegio
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models;

#region Permisos por Módulo/Vista

/// <summary>
/// Definición de permisos para cada módulo/vista del sistema
/// </summary>
public class RolePermissions
{
    /// <summary>ID del rol al que pertenecen estos permisos</summary>
    public int RoleId { get; set; }
    
    /// <summary>Nombre del rol</summary>
    public string RoleName { get; set; } = string.Empty;
    
    /// <summary>Permisos por módulo</summary>
    public ModulePermissions Modules { get; set; } = new();
    
    /// <summary>Fecha de última actualización</summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    
    /// <summary>Usuario que realizó la última actualización</summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Permisos organizados por módulo/vista
/// </summary>
public class ModulePermissions
{
    /// <summary>Vista Principal 3D - Control SCADA</summary>
    public ViewPermission MainView { get; set; } = new();
    
    /// <summary>Vista de Alarmas</summary>
    public ViewPermission AlarmsView { get; set; } = new();
    
    /// <summary>Vista de Estadísticas</summary>
    public ViewPermission StatisticsView { get; set; } = new();
    
    /// <summary>Vista de Recetas</summary>
    public ViewPermission RecipesView { get; set; } = new();
    
    /// <summary>Vista de Configuración General</summary>
    public ViewPermission SettingsView { get; set; } = new();
    
    /// <summary>Vista de Gestión de Usuarios</summary>
    public ViewPermission UsersView { get; set; } = new();
    
    /// <summary>Vista de Logs de Operación</summary>
    public ViewPermission OperationLogsView { get; set; } = new();
    
    /// <summary>Vista de Tipos de Tren</summary>
    public ViewPermission TrainTypesView { get; set; } = new();
    
    /// <summary>Vista de Tipos de Lavado</summary>
    public ViewPermission WashTypesView { get; set; } = new();
    
    /// <summary>Vista de Topología EtherCAT</summary>
    public ViewPermission EtherCATView { get; set; } = new();
    
    /// <summary>Vista de Auditoría (Logs de sistema)</summary>
    public ViewPermission AuditView { get; set; } = new();
    
    /// <summary>Vista de Backup y Restauración</summary>
    public ViewPermission BackupView { get; set; } = new();
    
    /// <summary>Vista de Modo Manual</summary>
    public ViewPermission ManualModeView { get; set; } = new();
}

/// <summary>
/// Permisos para una vista específica
/// </summary>
public class ViewPermission
{
    /// <summary>Puede acceder a la vista</summary>
    public bool CanView { get; set; } = false;
    
    /// <summary>Puede crear/agregar elementos</summary>
    public bool CanCreate { get; set; } = false;
    
    /// <summary>Puede editar/modificar elementos</summary>
    public bool CanEdit { get; set; } = false;
    
    /// <summary>Puede eliminar elementos</summary>
    public bool CanDelete { get; set; } = false;
    
    /// <summary>Puede exportar datos</summary>
    public bool CanExport { get; set; } = false;
    
    /// <summary>Puede ejecutar acciones críticas (ej: comandos PLC)</summary>
    public bool CanExecute { get; set; } = false;
}

#endregion

#region Permisos Predefinidos por Rol

/// <summary>
/// Factory para generar permisos predefinidos por rol
/// Compatible con EU CRA - Principio de mínimo privilegio
/// </summary>
public static class DefaultRolePermissions
{
    /// <summary>
    /// Obtiene los permisos por defecto para un rol específico
    /// </summary>
    public static RolePermissions GetDefaultPermissions(SystemRole role)
    {
        return role switch
        {
            SystemRole.SuperAdmin => GetSuperAdminPermissions(),
            SystemRole.Administrator => GetAdministratorPermissions(),
            SystemRole.Operator => GetOperatorPermissions(),
            SystemRole.Maintenance => GetMaintenancePermissions(),
            SystemRole.Viewer => GetViewerPermissions(),
            SystemRole.Auditor => GetAuditorPermissions(),
            _ => GetViewerPermissions() // Por defecto, mínimo privilegio
        };
    }

    private static RolePermissions GetSuperAdminPermissions()
    {
        return new RolePermissions
        {
            RoleName = "SuperAdmin",
            Modules = new ModulePermissions
            {
                MainView = AllPermissions(),
                AlarmsView = AllPermissions(),
                StatisticsView = AllPermissions(),
                RecipesView = AllPermissions(),
                SettingsView = AllPermissions(),
                UsersView = AllPermissions(),
                OperationLogsView = AllPermissions(),
                TrainTypesView = AllPermissions(),
                WashTypesView = AllPermissions(),
                EtherCATView = AllPermissions(),
                AuditView = AllPermissions(),
                BackupView = AllPermissions(),
                ManualModeView = AllPermissions()
            }
        };
    }

    private static RolePermissions GetAdministratorPermissions()
    {
        return new RolePermissions
        {
            RoleName = "Administrator",
            Modules = new ModulePermissions
            {
                MainView = new ViewPermission { CanView = true, CanEdit = true, CanExecute = true },
                AlarmsView = new ViewPermission { CanView = true, CanEdit = true, CanCreate = true, CanDelete = true },
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = new ViewPermission { CanView = true, CanCreate = true, CanEdit = true, CanDelete = true },
                SettingsView = new ViewPermission { CanView = true, CanEdit = true },
                UsersView = AllPermissions(), // Gestión total de usuarios
                OperationLogsView = new ViewPermission { CanView = true, CanExport = true },
                TrainTypesView = new ViewPermission { CanView = true, CanCreate = true, CanEdit = true, CanDelete = true },
                WashTypesView = new ViewPermission { CanView = true, CanCreate = true, CanEdit = true, CanDelete = true },
                EtherCATView = new ViewPermission { CanView = true },
                AuditView = new ViewPermission { CanView = true, CanExport = true },
                BackupView = AllPermissions(),
                ManualModeView = new ViewPermission { CanView = true, CanExecute = true }
            }
        };
    }

    private static RolePermissions GetOperatorPermissions()
    {
        return new RolePermissions
        {
            RoleName = "Operator",
            Modules = new ModulePermissions
            {
                MainView = new ViewPermission { CanView = true, CanExecute = true }, // Control de proceso
                AlarmsView = new ViewPermission { CanView = true, CanEdit = true }, // Reconocer alarmas
                StatisticsView = new ViewPermission { CanView = true },
                RecipesView = new ViewPermission { CanView = true, CanExecute = true }, // Ejecutar recetas
                SettingsView = ReadOnlyPermission(), // Solo lectura
                UsersView = NoPermission(), // Sin gestión de usuarios
                OperationLogsView = new ViewPermission { CanView = true },
                TrainTypesView = new ViewPermission { CanView = true },
                WashTypesView = new ViewPermission { CanView = true },
                EtherCATView = ReadOnlyPermission(),
                AuditView = NoPermission(),
                BackupView = NoPermission(),
                ManualModeView = new ViewPermission { CanView = true, CanExecute = true }
            }
        };
    }

    private static RolePermissions GetMaintenancePermissions()
    {
        return new RolePermissions
        {
            RoleName = "Maintenance",
            Modules = new ModulePermissions
            {
                MainView = new ViewPermission { CanView = true, CanEdit = true, CanExecute = true },
                AlarmsView = new ViewPermission { CanView = true, CanEdit = true, CanCreate = true },
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = AllPermissions(), // Gestión completa de recetas
                SettingsView = new ViewPermission { CanView = true, CanEdit = true }, // Configuración técnica
                UsersView = NoPermission(), // Sin gestión de usuarios
                OperationLogsView = new ViewPermission { CanView = true, CanExport = true },
                TrainTypesView = AllPermissions(),
                WashTypesView = AllPermissions(),
                EtherCATView = new ViewPermission { CanView = true, CanEdit = true }, // Diagnóstico
                AuditView = ReadOnlyPermission(),
                BackupView = new ViewPermission { CanView = true, CanCreate = true }, // Puede hacer backups
                ManualModeView = AllPermissions() // Control manual completo
            }
        };
    }

    private static RolePermissions GetViewerPermissions()
    {
        return new RolePermissions
        {
            RoleName = "Viewer",
            Modules = new ModulePermissions
            {
                MainView = ReadOnlyPermission(),
                AlarmsView = ReadOnlyPermission(),
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = ReadOnlyPermission(),
                SettingsView = ReadOnlyPermission(),
                UsersView = NoPermission(),
                OperationLogsView = ReadOnlyPermission(),
                TrainTypesView = ReadOnlyPermission(),
                WashTypesView = ReadOnlyPermission(),
                EtherCATView = ReadOnlyPermission(),
                AuditView = NoPermission(),
                BackupView = NoPermission(),
                ManualModeView = NoPermission() // Sin control manual
            }
        };
    }

    private static RolePermissions GetAuditorPermissions()
    {
        return new RolePermissions
        {
            RoleName = "Auditor",
            Modules = new ModulePermissions
            {
                MainView = ReadOnlyPermission(),
                AlarmsView = new ViewPermission { CanView = true, CanExport = true },
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = ReadOnlyPermission(),
                SettingsView = ReadOnlyPermission(),
                UsersView = ReadOnlyPermission(), // Puede ver usuarios (auditoría)
                OperationLogsView = new ViewPermission { CanView = true, CanExport = true },
                TrainTypesView = ReadOnlyPermission(),
                WashTypesView = ReadOnlyPermission(),
                EtherCATView = ReadOnlyPermission(),
                AuditView = new ViewPermission { CanView = true, CanExport = true }, // Acceso total a auditoría
                BackupView = ReadOnlyPermission(),
                ManualModeView = NoPermission()
            }
        };
    }

    // Helpers para permisos comunes
    private static ViewPermission AllPermissions() =>
        new() { CanView = true, CanCreate = true, CanEdit = true, CanDelete = true, CanExport = true, CanExecute = true };

    private static ViewPermission ReadOnlyPermission() =>
        new() { CanView = true };

    private static ViewPermission NoPermission() =>
        new() { CanView = false };
}

#endregion

#region DTOs

/// <summary>
/// DTO para actualización de permisos de un rol
/// </summary>
public class UpdateRolePermissionsRequest
{
    [Required]
    public ModulePermissions Modules { get; set; } = new();
}

/// <summary>
/// DTO para respuesta de operación de permisos
/// </summary>
public class PermissionsOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public RolePermissions? Data { get; set; }
}

/// <summary>
/// DTO para listado de módulos disponibles
/// </summary>
public class ModuleInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

#endregion
