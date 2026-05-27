// ============================================================================
// RolePermissionsService.cs - Implementación del Servicio de Permisos
// ============================================================================
// Gestión de permisos almacenados en PermissionsJson de la tabla Roles
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using System.Text.Json;

namespace SW.PC.API.Backend.Services;

// Opciones JSON consistentes con el resto de la API (camelCase)
public static class PermissionsJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}

/// <summary>
/// Servicio de gestión de permisos por rol
/// Almacena permisos en PermissionsJson de la tabla Roles
/// </summary>
public class RolePermissionsService : IRolePermissionsService
{
    private readonly IDbContextFactory<AquafrischDbContext> _dbFactory;
    private readonly ILogger<RolePermissionsService> _logger;
    private readonly IRequestProjectContext _projectContext;

    public RolePermissionsService(
        IDbContextFactory<AquafrischDbContext> dbFactory,
        ILogger<RolePermissionsService> logger,
        IRequestProjectContext projectContext)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _projectContext = projectContext;
    }

    public async Task<RolePermissions> GetRolePermissionsAsync(string roleName)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null)
            {
                _logger.LogWarning("Rol {RoleName} no encontrado, devolviendo permisos por defecto", roleName);
                return GetDefaultPermissionsForRole(roleName);
            }

            // Si no tiene permisos configurados, devolver defaults
            if (string.IsNullOrEmpty(role.PermissionsJson))
            {
                var systemRole = Enum.TryParse<SystemRole>(roleName, out var sr) ? sr : SystemRole.Viewer;
                return DefaultRolePermissions.GetDefaultPermissions(systemRole);
            }

            // Deserializar permisos guardados
            try
            {
                var permissions = JsonSerializer.Deserialize<ModulePermissions>(
                    role.PermissionsJson, 
                    PermissionsJsonOptions.Options);

                // Obtener el conjunto de claves realmente presentes en el JSON guardado.
                // Esto nos permite distinguir entre "módulo nuevo que nunca se guardó"
                // (debe heredar el default) y "módulo guardado explícitamente con todo en false"
                // (el usuario lo desmarcó a propósito y debe respetarse).
                var savedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var doc = JsonDocument.Parse(role.PermissionsJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in doc.RootElement.EnumerateObject())
                        {
                            savedKeys.Add(p.Name);
                        }
                    }
                }
                catch (JsonException) { /* fallback: savedKeys vacío => se comporta como antes */ }

                // Mergear con defaults: si un módulo nuevo no existía en el JSON guardado,
                // hereda los permisos por defecto del rol (evita que módulos nuevos queden en false)
                var mergedPermissions = MergeWithDefaults(permissions ?? new ModulePermissions(), roleName, savedKeys);
                
                return new RolePermissions
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Modules = mergedPermissions
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializando permisos del rol {RoleName}", roleName);
                return GetDefaultPermissionsForRole(roleName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo permisos del rol {RoleName}", roleName);
            return GetDefaultPermissionsForRole(roleName);
        }
    }

    public async Task<RolePermissions?> GetRolePermissionsByIdAsync(int roleId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            var role = await context.Roles.FindAsync(roleId);
            if (role == null) return null;

            return await GetRolePermissionsAsync(role.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo permisos del rol ID {RoleId}", roleId);
            return null;
        }
    }

    public async Task<PermissionsOperationResponse> UpdateRolePermissionsAsync(
        string roleName, 
        ModulePermissions permissions, 
        string updatedBy)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null)
            {
                return new PermissionsOperationResponse
                {
                    Success = false,
                    Message = $"Rol '{roleName}' no encontrado"
                };
            }

            // Serializar permisos a JSON (con camelCase para consistencia)
            var permissionsJson = JsonSerializer.Serialize(permissions, PermissionsJsonOptions.Options);

            role.PermissionsJson = permissionsJson;
            await context.SaveChangesAsync();

            _logger.LogInformation("Permisos del rol {RoleName} actualizados por {UpdatedBy}", 
                roleName, updatedBy);

            return new PermissionsOperationResponse
            {
                Success = true,
                Message = $"Permisos del rol '{roleName}' actualizados correctamente",
                Data = new RolePermissions
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Modules = permissions,
                    LastUpdated = DateTime.Now,
                    UpdatedBy = updatedBy
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando permisos del rol {RoleName}", roleName);
            return new PermissionsOperationResponse
            {
                Success = false,
                Message = $"Error al actualizar permisos: {ex.Message}"
            };
        }
    }

    public List<ModuleInfo> GetAvailableModules()
    {
        return new List<ModuleInfo>
        {
            new() { 
                Key = "MainView", 
                Name = "Vista Principal 3D", 
                Description = "Control SCADA con visualización 3D del proceso",
                Icon = "🏠",
                Category = "Operación"
            },
            new() { 
                Key = "AlarmsView", 
                Name = "Alarmas", 
                Description = "Gestión y reconocimiento de alarmas del sistema",
                Icon = "⚠️",
                Category = "Operación"
            },
            new() { 
                Key = "StatisticsView", 
                Name = "Estadísticas", 
                Description = "Gráficos y reportes estadísticos de producción",
                Icon = "📊",
                Category = "Reportes"
            },
            new() { 
                Key = "RecipesView", 
                Name = "Recetas", 
                Description = "Gestión de recetas de lavado y producción",
                Icon = "🧪",
                Category = "Configuración"
            },
            new() { 
                Key = "SettingsView", 
                Name = "Configuración", 
                Description = "Configuración general del sistema",
                Icon = "⚙️",
                Category = "Configuración"
            },
            new() { 
                Key = "UsersView", 
                Name = "Gestión de Usuarios", 
                Description = "Administración de usuarios y roles",
                Icon = "👥",
                Category = "Administración"
            },
            new() { 
                Key = "OperationLogsView", 
                Name = "Logs de Operación", 
                Description = "Historial de operaciones del sistema",
                Icon = "📈",
                Category = "Reportes"
            },
            new() { 
                Key = "TrainTypesView", 
                Name = "Tipos de Tren", 
                Description = "Configuración de tipos de tren",
                Icon = "🚂",
                Category = "Configuración"
            },
            new() { 
                Key = "WashTypesView", 
                Name = "Tipos de Lavado", 
                Description = "Configuración de tipos de lavado",
                Icon = "🧼",
                Category = "Configuración"
            },
            new() { 
                Key = "EtherCATView", 
                Name = "Topología EtherCAT", 
                Description = "Visualización y diagnóstico de EtherCAT",
                Icon = "🔌",
                Category = "Mantenimiento"
            },
            new() { 
                Key = "AuditView", 
                Name = "Auditoría", 
                Description = "Logs de auditoría y seguridad del sistema",
                Icon = "🛡️",
                Category = "Administración"
            },
            new() { 
                Key = "BackupView", 
                Name = "Backup y Restauración", 
                Description = "Gestión de copias de seguridad",
                Icon = "💾",
                Category = "Administración"
            },
            new() { 
                Key = "ManualModeView", 
                Name = "Modo Manual", 
                Description = "Control manual de elementos del sistema",
                Icon = "🎮",
                Category = "Operación"
            },
            new() { 
                Key = "DocumentsView", 
                Name = "Documentación", 
                Description = "Gestión documental del proyecto (DMS)",
                Icon = "📄",
                Category = "Documentación"
            },
            new() {
                Key = "ExportManager",
                Name = "Gestor de Exportaciones",
                Description = "Lista, programación y ejecución de tareas de exportación (CanView = lista + ejecutar manual; CanEdit = crear/editar/eliminar tareas y perfiles)",
                Icon = "📤",
                Category = "Administración"
            }
        };
    }

    public async Task<bool> HasPermissionAsync(string roleName, string module, string action)
    {
        try
        {
            var permissions = await GetRolePermissionsAsync(roleName);
            
            // Obtener el módulo específico usando reflexión
            var moduleProperty = typeof(ModulePermissions).GetProperty(module);
            if (moduleProperty == null) return false;

            var viewPermission = moduleProperty.GetValue(permissions.Modules) as ViewPermission;
            if (viewPermission == null) return false;

            // Verificar la acción específica
            return action.ToLower() switch
            {
                "view" => viewPermission.CanView,
                "create" => viewPermission.CanCreate,
                "edit" => viewPermission.CanEdit,
                "delete" => viewPermission.CanDelete,
                "export" => viewPermission.CanExport,
                "execute" => viewPermission.CanExecute,
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando permiso {Action} en módulo {Module} para rol {RoleName}", 
                action, module, roleName);
            return false;
        }
    }

    public async Task<PermissionsOperationResponse> ResetToDefaultPermissionsAsync(string roleName, string updatedBy)
    {
        try
        {
            var systemRole = Enum.TryParse<SystemRole>(roleName, out var sr) ? sr : SystemRole.Viewer;
            var defaultPermissions = DefaultRolePermissions.GetDefaultPermissions(systemRole);

            return await UpdateRolePermissionsAsync(roleName, defaultPermissions.Modules, updatedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restaurando permisos por defecto del rol {RoleName}", roleName);
            return new PermissionsOperationResponse
            {
                Success = false,
                Message = $"Error al restaurar permisos: {ex.Message}"
            };
        }
    }

    #region Helpers

    private RolePermissions GetDefaultPermissionsForRole(string roleName)
    {
        var systemRole = Enum.TryParse<SystemRole>(roleName, out var sr) ? sr : SystemRole.Viewer;
        return DefaultRolePermissions.GetDefaultPermissions(systemRole);
    }

    /// <summary>
    /// Mergea permisos deserializados con los defaults del rol.
    /// Si un módulo nuevo fue añadido al código pero no existía en el JSON guardado en DB,
    /// hereda los permisos por defecto del rol en lugar de quedar todo en false.
    /// </summary>
    private ModulePermissions MergeWithDefaults(ModulePermissions saved, string roleName, HashSet<string>? savedKeys = null)
    {
        var defaults = GetDefaultPermissionsForRole(roleName).Modules;
        if (defaults == null) return saved;

        // Usar reflection para detectar propiedades ViewPermission que quedaron vacías
        var props = typeof(ModulePermissions).GetProperties()
            .Where(p => p.PropertyType == typeof(ViewPermission));

        foreach (var prop in props)
        {
            // Si tenemos información de las claves realmente presentes en el JSON guardado,
            // sólo aplicar el default cuando la propiedad NO estaba en el JSON
            // (módulo nuevo añadido al sistema después de haber guardado los permisos).
            // Si la propiedad estaba presente, respetar lo que el usuario guardó (incluso si es todo false).
            if (savedKeys != null && savedKeys.Count > 0)
            {
                // Comparar tanto en camelCase como en el nombre original PascalCase
                var camel = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                if (savedKeys.Contains(prop.Name) || savedKeys.Contains(camel))
                {
                    continue; // El usuario guardó este módulo explícitamente: no tocar
                }

                var defaultVal = (ViewPermission?)prop.GetValue(defaults);
                if (defaultVal != null)
                {
                    prop.SetValue(saved, defaultVal);
                }
                continue;
            }

            // Fallback (no se pudo parsear el JSON original): heurística antigua basada en "todo false"
            var savedVal = (ViewPermission?)prop.GetValue(saved);
            if (savedVal != null && !savedVal.CanView && !savedVal.CanCreate && !savedVal.CanEdit 
                && !savedVal.CanDelete && !savedVal.CanExport && !savedVal.CanExecute)
            {
                var defaultVal = (ViewPermission?)prop.GetValue(defaults);
                if (defaultVal != null && (defaultVal.CanView || defaultVal.CanCreate || defaultVal.CanEdit 
                    || defaultVal.CanDelete || defaultVal.CanExport || defaultVal.CanExecute))
                {
                    prop.SetValue(saved, defaultVal);
                }
            }
        }

        return saved;
    }

    #endregion
}
