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
/// Permisos organizados por módulo/vista - 42 permisos granulares
/// </summary>
public class ModulePermissions
{
    // ═══════════════════════════════════════════════════════════════════
    // 🏠 PÁGINA PRINCIPAL - TOPBAR (10 elementos)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>Tour Virtual / Calibración</summary>
    public ViewPermission MainView_Tour { get; set; } = new();
    
    /// <summary>Etiquetas (Labels)</summary>
    public ViewPermission MainView_Labels { get; set; } = new();
    
    /// <summary>Capas (Layers)</summary>
    public ViewPermission MainView_Layers { get; set; } = new();
    
    /// <summary>Herramientas del Sistema</summary>
    public ViewPermission MainView_SystemTools { get; set; } = new();
    
    /// <summary>Modo Semiautomático</summary>
    public ViewPermission MainView_SemiAuto { get; set; } = new();
    
    /// <summary>Configuración Rápida</summary>
    public ViewPermission MainView_FastConfig { get; set; } = new();
    
    /// <summary>Idiomas</summary>
    public ViewPermission MainView_Language { get; set; } = new();
    
    /// <summary>Stats Modelo (DEV)</summary>
    public ViewPermission MainView_ModelStats { get; set; } = new();
    
    /// <summary>Screen Display</summary>
    public ViewPermission MainView_ScreenDisplay { get; set; } = new();
    
    /// <summary>Model Label</summary>
    public ViewPermission MainView_ModelLabel { get; set; } = new();
    
    /// <summary>Display Model Label</summary>
    public ViewPermission MainView_DisplayModelLabel { get; set; } = new();
    
    // ═══════════════════════════════════════════════════════════════════
    // 📊 INFO PANEL (17 elementos)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>Botón Expandir/Compacto</summary>
    public ViewPermission InfoPanel_Button { get; set; } = new();
    
    // --- Info Panel Compacto ---
    
    /// <summary>Info General Servicios</summary>
    public ViewPermission InfoPanel_ServicesInfo { get; set; } = new();
    
    /// <summary>Info PLC</summary>
    public ViewPermission InfoPanel_PLCInfo { get; set; } = new();
    
    /// <summary>Alarmas (Compacto)</summary>
    public ViewPermission InfoPanel_AlarmsCompact { get; set; } = new();
    
    /// <summary>Integridad Software (Compacto)</summary>
    public ViewPermission InfoPanel_IntegrityCompact { get; set; } = new();
    
    /// <summary>Cumplimiento y Seguridad (Compacto)</summary>
    public ViewPermission InfoPanel_ComplianceCompact { get; set; } = new();
    
    // --- Info Panel Expandido ---
    
    /// <summary>Servicios Internos del Backend</summary>
    public ViewPermission InfoPanel_InternalServices { get; set; } = new();
    
    /// <summary>Servicios Externos del Backend</summary>
    public ViewPermission InfoPanel_ExternalServices { get; set; } = new();
    
    /// <summary>Alarmas (Expandido)</summary>
    public ViewPermission InfoPanel_AlarmsExpanded { get; set; } = new();
    
    /// <summary>Integridad del Software (Expandido)</summary>
    public ViewPermission InfoPanel_IntegrityExpanded { get; set; } = new();
    
    /// <summary>Info Hardware PC</summary>
    public ViewPermission InfoPanel_HardwareInfo { get; set; } = new();
    
    /// <summary>SBOM</summary>
    public ViewPermission InfoPanel_SBOM { get; set; } = new();
    
    /// <summary>Escáner de Vulnerabilidades</summary>
    public ViewPermission InfoPanel_VulnScanner { get; set; } = new();
    
    /// <summary>Reporte de Vulnerabilidades</summary>
    public ViewPermission InfoPanel_VulnReport { get; set; } = new();
    
    /// <summary>Registro de Auditoría</summary>
    public ViewPermission InfoPanel_AuditLog { get; set; } = new();
    
    /// <summary>Registro de Operaciones</summary>
    public ViewPermission InfoPanel_OperationLog { get; set; } = new();
    
    /// <summary>Registro de Eventos de Sesión</summary>
    public ViewPermission InfoPanel_SessionLog { get; set; } = new();
    
    /// <summary>Info PLC Expandido - Variables WSTRING desde PLC</summary>
    public ViewPermission InfoPanel_PlcInfoExpanded { get; set; } = new();
    
    // ═══════════════════════════════════════════════════════════════════
    // 📱 SIDE MENU - VISTAS (7 elementos)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>Vista de Alarmas</summary>
    public ViewPermission AlarmsView { get; set; } = new();
    
    /// <summary>Vista de Estadísticas</summary>
    public ViewPermission StatisticsView { get; set; } = new();
    
    /// <summary>Vista de Recetas</summary>
    public ViewPermission RecipesView { get; set; } = new();
    
    /// <summary>Vista de Configuración</summary>
    public ViewPermission SettingsView { get; set; } = new();
    
    /// <summary>Vista de Gestión de Usuarios</summary>
    public ViewPermission UsersView { get; set; } = new();
    
    /// <summary>Vista de Tipos de Tren</summary>
    public ViewPermission TrainTypesView { get; set; } = new();
    
    /// <summary>Vista de Tipos de Lavado</summary>
    public ViewPermission WashTypesView { get; set; } = new();
    
    /// <summary>Vista de Modo Manual</summary>
    public ViewPermission ManualModeView { get; set; } = new();
    
    /// <summary>Vista de Documentación (DMS)</summary>
    public ViewPermission DocumentsView { get; set; } = new();
    
    // ═══════════════════════════════════════════════════════════════════
    // 🚂 TIPOS DE TREN - ACCIONES (4 elementos)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>Editor de Tipos de Tren</summary>
    public ViewPermission TrainTypes_Editor { get; set; } = new();
    
    /// <summary>Guardar Tipos de Tren</summary>
    public ViewPermission TrainTypes_Save { get; set; } = new();
    
    /// <summary>Escribir PLC 1 (Tipos de Tren)</summary>
    public ViewPermission TrainTypes_WritePLC1 { get; set; } = new();
    
    /// <summary>Escribir PLC 2 (Tipos de Tren)</summary>
    public ViewPermission TrainTypes_WritePLC2 { get; set; } = new();
    
    // ═══════════════════════════════════════════════════════════════════
    // 🧼 TIPOS DE LAVADO - ACCIONES (4 elementos)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>Editor de Tipos de Lavado</summary>
    public ViewPermission WashTypes_Editor { get; set; } = new();
    
    /// <summary>Guardar Tipos de Lavado</summary>
    public ViewPermission WashTypes_Save { get; set; } = new();
    
    /// <summary>Escribir PLC 1 (Tipos de Lavado)</summary>
    public ViewPermission WashTypes_WritePLC1 { get; set; } = new();
    
    /// <summary>Escribir PLC 2 (Tipos de Lavado)</summary>
    public ViewPermission WashTypes_WritePLC2 { get; set; } = new();
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
                // 🏠 Página Principal - TODO habilitado
                MainView_Tour = AllPermissions(),
                MainView_ModelStats = AllPermissions(),
                MainView_Labels = AllPermissions(),
                MainView_Layers = AllPermissions(),
                MainView_SystemTools = AllPermissions(),
                MainView_SemiAuto = AllPermissions(),
                MainView_FastConfig = AllPermissions(),
                MainView_Language = AllPermissions(),
                MainView_ScreenDisplay = AllPermissions(),
                MainView_ModelLabel = AllPermissions(),
                MainView_DisplayModelLabel = AllPermissions(),
                
                // 📊 Info Panel - TODO habilitado
                InfoPanel_Button = AllPermissions(),
                InfoPanel_ServicesInfo = AllPermissions(),
                InfoPanel_PLCInfo = AllPermissions(),
                InfoPanel_AlarmsCompact = AllPermissions(),
                InfoPanel_IntegrityCompact = AllPermissions(),
                InfoPanel_ComplianceCompact = AllPermissions(),
                InfoPanel_InternalServices = AllPermissions(),
                InfoPanel_ExternalServices = AllPermissions(),
                InfoPanel_AlarmsExpanded = AllPermissions(),
                InfoPanel_IntegrityExpanded = AllPermissions(),
                InfoPanel_HardwareInfo = AllPermissions(),
                InfoPanel_SBOM = AllPermissions(),
                InfoPanel_VulnScanner = AllPermissions(),
                InfoPanel_VulnReport = AllPermissions(),
                InfoPanel_AuditLog = AllPermissions(),
                InfoPanel_OperationLog = AllPermissions(),
                InfoPanel_SessionLog = AllPermissions(),
                InfoPanel_PlcInfoExpanded = AllPermissions(),
                
                // 📱 Side Menu - TODO habilitado
                AlarmsView = AllPermissions(),
                StatisticsView = AllPermissions(),
                RecipesView = AllPermissions(),
                SettingsView = AllPermissions(),
                UsersView = AllPermissions(),
                TrainTypesView = AllPermissions(),
                WashTypesView = AllPermissions(),
                ManualModeView = AllPermissions(),
                DocumentsView = AllPermissions(),
                
                // 🚂 Tipos de Tren - TODO habilitado
                TrainTypes_Editor = AllPermissions(),
                TrainTypes_Save = AllPermissions(),
                TrainTypes_WritePLC1 = AllPermissions(),
                TrainTypes_WritePLC2 = AllPermissions(),
                
                // 🧼 Tipos de Lavado - TODO habilitado
                WashTypes_Editor = AllPermissions(),
                WashTypes_Save = AllPermissions(),
                WashTypes_WritePLC1 = AllPermissions(),
                WashTypes_WritePLC2 = AllPermissions()
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
                // 🏠 Página Principal
                MainView_Tour = AllPermissions(),
                MainView_ModelStats = AllPermissions(),
                MainView_Labels = AllPermissions(),
                MainView_Layers = AllPermissions(),
                MainView_SystemTools = AllPermissions(),
                MainView_SemiAuto = AllPermissions(),
                MainView_FastConfig = AllPermissions(),
                MainView_Language = AllPermissions(),
                MainView_ScreenDisplay = AllPermissions(),
                MainView_ModelLabel = AllPermissions(),
                MainView_DisplayModelLabel = AllPermissions(),
                
                // 📊 Info Panel
                InfoPanel_Button = AllPermissions(),
                InfoPanel_ServicesInfo = AllPermissions(),
                InfoPanel_PLCInfo = AllPermissions(),
                InfoPanel_AlarmsCompact = AllPermissions(),
                InfoPanel_IntegrityCompact = AllPermissions(),
                InfoPanel_ComplianceCompact = AllPermissions(),
                InfoPanel_InternalServices = AllPermissions(),
                InfoPanel_ExternalServices = AllPermissions(),
                InfoPanel_AlarmsExpanded = AllPermissions(),
                InfoPanel_IntegrityExpanded = AllPermissions(),
                InfoPanel_HardwareInfo = AllPermissions(),
                InfoPanel_SBOM = AllPermissions(),
                InfoPanel_VulnScanner = AllPermissions(),
                InfoPanel_VulnReport = AllPermissions(),
                InfoPanel_AuditLog = AllPermissions(),
                InfoPanel_OperationLog = AllPermissions(),
                InfoPanel_SessionLog = AllPermissions(),
                InfoPanel_PlcInfoExpanded = AllPermissions(),
                
                // 📱 Side Menu
                AlarmsView = AllPermissions(),
                StatisticsView = AllPermissions(),
                RecipesView = AllPermissions(),
                SettingsView = AllPermissions(),
                UsersView = AllPermissions(),
                TrainTypesView = AllPermissions(),
                WashTypesView = AllPermissions(),
                ManualModeView = AllPermissions(),
                DocumentsView = AllPermissions(),
                
                // 🚂 Tipos de Tren
                TrainTypes_Editor = AllPermissions(),
                TrainTypes_Save = AllPermissions(),
                TrainTypes_WritePLC1 = AllPermissions(),
                TrainTypes_WritePLC2 = AllPermissions(),
                
                // 🧼 Tipos de Lavado
                WashTypes_Editor = AllPermissions(),
                WashTypes_Save = AllPermissions(),
                WashTypes_WritePLC1 = AllPermissions(),
                WashTypes_WritePLC2 = AllPermissions()
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
                // 🏠 Página Principal - Operación básica
                MainView_Tour = ReadOnlyPermission(),
                MainView_ModelStats = NoPermission(),
                MainView_Labels = ReadOnlyPermission(),
                MainView_Layers = ReadOnlyPermission(),
                MainView_SystemTools = NoPermission(),
                MainView_SemiAuto = new ViewPermission { CanView = true, CanExecute = true },
                MainView_FastConfig = NoPermission(),
                MainView_Language = ReadOnlyPermission(),
                MainView_ScreenDisplay = ReadOnlyPermission(),
                MainView_ModelLabel = ReadOnlyPermission(),
                MainView_DisplayModelLabel = ReadOnlyPermission(),
                
                // 📊 Info Panel - Solo lectura
                InfoPanel_Button = ReadOnlyPermission(),
                InfoPanel_ServicesInfo = ReadOnlyPermission(),
                InfoPanel_PLCInfo = ReadOnlyPermission(),
                InfoPanel_AlarmsCompact = ReadOnlyPermission(),
                InfoPanel_IntegrityCompact = NoPermission(),
                InfoPanel_ComplianceCompact = NoPermission(),
                InfoPanel_InternalServices = NoPermission(),
                InfoPanel_ExternalServices = NoPermission(),
                InfoPanel_AlarmsExpanded = ReadOnlyPermission(),
                InfoPanel_IntegrityExpanded = NoPermission(),
                InfoPanel_HardwareInfo = NoPermission(),
                InfoPanel_SBOM = NoPermission(),
                InfoPanel_VulnScanner = NoPermission(),
                InfoPanel_VulnReport = NoPermission(),
                InfoPanel_AuditLog = NoPermission(),
                InfoPanel_OperationLog = ReadOnlyPermission(),
                InfoPanel_SessionLog = NoPermission(),
                InfoPanel_PlcInfoExpanded = ReadOnlyPermission(),
                
                // 📱 Side Menu
                AlarmsView = new ViewPermission { CanView = true, CanEdit = true },
                StatisticsView = ReadOnlyPermission(),
                RecipesView = new ViewPermission { CanView = true, CanExecute = true },
                SettingsView = NoPermission(),
                UsersView = NoPermission(),
                TrainTypesView = ReadOnlyPermission(),
                WashTypesView = ReadOnlyPermission(),
                ManualModeView = NoPermission(),
                DocumentsView = ReadOnlyPermission(),
                
                // 🚂 Tipos de Tren - Solo ver
                TrainTypes_Editor = NoPermission(),
                TrainTypes_Save = NoPermission(),
                TrainTypes_WritePLC1 = NoPermission(),
                TrainTypes_WritePLC2 = NoPermission(),
                
                // 🧼 Tipos de Lavado - Solo ver
                WashTypes_Editor = NoPermission(),
                WashTypes_Save = NoPermission(),
                WashTypes_WritePLC1 = NoPermission(),
                WashTypes_WritePLC2 = NoPermission()
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
                // 🏠 Página Principal - Acceso técnico
                MainView_Tour = AllPermissions(),
                MainView_ModelStats = AllPermissions(),
                MainView_Labels = AllPermissions(),
                MainView_Layers = AllPermissions(),
                MainView_SystemTools = AllPermissions(),
                MainView_SemiAuto = AllPermissions(),
                MainView_FastConfig = AllPermissions(),
                MainView_Language = AllPermissions(),
                MainView_ScreenDisplay = AllPermissions(),
                MainView_ModelLabel = AllPermissions(),
                MainView_DisplayModelLabel = AllPermissions(),
                
                // 📊 Info Panel - Diagnóstico completo
                InfoPanel_Button = AllPermissions(),
                InfoPanel_ServicesInfo = AllPermissions(),
                InfoPanel_PLCInfo = AllPermissions(),
                InfoPanel_AlarmsCompact = AllPermissions(),
                InfoPanel_IntegrityCompact = AllPermissions(),
                InfoPanel_ComplianceCompact = ReadOnlyPermission(),
                InfoPanel_InternalServices = AllPermissions(),
                InfoPanel_ExternalServices = AllPermissions(),
                InfoPanel_AlarmsExpanded = AllPermissions(),
                InfoPanel_IntegrityExpanded = AllPermissions(),
                InfoPanel_HardwareInfo = AllPermissions(),
                InfoPanel_SBOM = ReadOnlyPermission(),
                InfoPanel_VulnScanner = ReadOnlyPermission(),
                InfoPanel_VulnReport = ReadOnlyPermission(),
                InfoPanel_AuditLog = ReadOnlyPermission(),
                InfoPanel_OperationLog = AllPermissions(),
                InfoPanel_SessionLog = ReadOnlyPermission(),
                InfoPanel_PlcInfoExpanded = AllPermissions(),
                
                // 📱 Side Menu
                AlarmsView = AllPermissions(),
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = AllPermissions(),
                SettingsView = new ViewPermission { CanView = true, CanEdit = true },
                UsersView = NoPermission(),
                TrainTypesView = AllPermissions(),
                WashTypesView = AllPermissions(),
                ManualModeView = AllPermissions(),
                DocumentsView = AllPermissions(),
                
                // 🚂 Tipos de Tren - Control total
                TrainTypes_Editor = AllPermissions(),
                TrainTypes_Save = AllPermissions(),
                TrainTypes_WritePLC1 = AllPermissions(),
                TrainTypes_WritePLC2 = AllPermissions(),
                
                // 🧼 Tipos de Lavado - Control total
                WashTypes_Editor = AllPermissions(),
                WashTypes_Save = AllPermissions(),
                WashTypes_WritePLC1 = AllPermissions(),
                WashTypes_WritePLC2 = AllPermissions()
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
                // 🏠 Página Principal - Solo ver
                MainView_Tour = ReadOnlyPermission(),
                MainView_ModelStats = NoPermission(),
                MainView_Labels = ReadOnlyPermission(),
                MainView_Layers = ReadOnlyPermission(),
                MainView_SystemTools = NoPermission(),
                MainView_SemiAuto = NoPermission(),
                MainView_FastConfig = NoPermission(),
                MainView_Language = ReadOnlyPermission(),
                MainView_ScreenDisplay = ReadOnlyPermission(),
                MainView_ModelLabel = ReadOnlyPermission(),
                MainView_DisplayModelLabel = ReadOnlyPermission(),
                
                // 📊 Info Panel - Solo básico
                InfoPanel_Button = ReadOnlyPermission(),
                InfoPanel_ServicesInfo = ReadOnlyPermission(),
                InfoPanel_PLCInfo = ReadOnlyPermission(),
                InfoPanel_AlarmsCompact = ReadOnlyPermission(),
                InfoPanel_IntegrityCompact = NoPermission(),
                InfoPanel_ComplianceCompact = NoPermission(),
                InfoPanel_InternalServices = NoPermission(),
                InfoPanel_ExternalServices = NoPermission(),
                InfoPanel_AlarmsExpanded = ReadOnlyPermission(),
                InfoPanel_IntegrityExpanded = NoPermission(),
                InfoPanel_HardwareInfo = NoPermission(),
                InfoPanel_SBOM = NoPermission(),
                InfoPanel_VulnScanner = NoPermission(),
                InfoPanel_VulnReport = NoPermission(),
                InfoPanel_AuditLog = NoPermission(),
                InfoPanel_OperationLog = NoPermission(),
                InfoPanel_SessionLog = NoPermission(),
                InfoPanel_PlcInfoExpanded = NoPermission(),
                
                // 📱 Side Menu - Solo ver
                AlarmsView = ReadOnlyPermission(),
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = ReadOnlyPermission(),
                SettingsView = NoPermission(),
                UsersView = NoPermission(),
                TrainTypesView = ReadOnlyPermission(),
                WashTypesView = ReadOnlyPermission(),
                ManualModeView = NoPermission(),
                DocumentsView = ReadOnlyPermission(),
                
                // 🚂 Tipos de Tren - Sin acceso
                TrainTypes_Editor = NoPermission(),
                TrainTypes_Save = NoPermission(),
                TrainTypes_WritePLC1 = NoPermission(),
                TrainTypes_WritePLC2 = NoPermission(),
                
                // 🧼 Tipos de Lavado - Sin acceso
                WashTypes_Editor = NoPermission(),
                WashTypes_Save = NoPermission(),
                WashTypes_WritePLC1 = NoPermission(),
                WashTypes_WritePLC2 = NoPermission()
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
                // 🏠 Página Principal - Solo ver
                MainView_Tour = ReadOnlyPermission(),
                MainView_ModelStats = NoPermission(),
                MainView_Labels = ReadOnlyPermission(),
                MainView_Layers = ReadOnlyPermission(),
                MainView_SystemTools = NoPermission(),
                MainView_SemiAuto = NoPermission(),
                MainView_FastConfig = NoPermission(),
                MainView_Language = ReadOnlyPermission(),
                MainView_ScreenDisplay = ReadOnlyPermission(),
                MainView_ModelLabel = ReadOnlyPermission(),
                MainView_DisplayModelLabel = ReadOnlyPermission(),
                
                // 📊 Info Panel - Acceso auditoría
                InfoPanel_Button = ReadOnlyPermission(),
                InfoPanel_ServicesInfo = ReadOnlyPermission(),
                InfoPanel_PLCInfo = ReadOnlyPermission(),
                InfoPanel_AlarmsCompact = ReadOnlyPermission(),
                InfoPanel_IntegrityCompact = ReadOnlyPermission(),
                InfoPanel_ComplianceCompact = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_InternalServices = ReadOnlyPermission(),
                InfoPanel_ExternalServices = ReadOnlyPermission(),
                InfoPanel_AlarmsExpanded = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_IntegrityExpanded = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_HardwareInfo = ReadOnlyPermission(),
                InfoPanel_SBOM = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_VulnScanner = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_VulnReport = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_AuditLog = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_OperationLog = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_SessionLog = new ViewPermission { CanView = true, CanExport = true },
                InfoPanel_PlcInfoExpanded = new ViewPermission { CanView = true, CanExport = true },
                
                // 📱 Side Menu - Solo auditoría
                AlarmsView = new ViewPermission { CanView = true, CanExport = true },
                StatisticsView = new ViewPermission { CanView = true, CanExport = true },
                RecipesView = ReadOnlyPermission(),
                SettingsView = ReadOnlyPermission(),
                UsersView = ReadOnlyPermission(),
                TrainTypesView = ReadOnlyPermission(),
                WashTypesView = ReadOnlyPermission(),
                ManualModeView = NoPermission(),
                DocumentsView = new ViewPermission { CanView = true, CanExport = true },
                
                // 🚂 Tipos de Tren - Sin acceso
                TrainTypes_Editor = NoPermission(),
                TrainTypes_Save = NoPermission(),
                TrainTypes_WritePLC1 = NoPermission(),
                TrainTypes_WritePLC2 = NoPermission(),
                
                // 🧼 Tipos de Lavado - Sin acceso
                WashTypes_Editor = NoPermission(),
                WashTypes_Save = NoPermission(),
                WashTypes_WritePLC1 = NoPermission(),
                WashTypes_WritePLC2 = NoPermission()
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
