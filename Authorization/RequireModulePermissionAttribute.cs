// ============================================================================
// RequireModulePermissionAttribute.cs — Autorización por módulo/acción
// ============================================================================
// Filtro reutilizable que valida que el rol del usuario tenga el permiso
// (`view`, `create`, `edit`, `delete`, `export`, `execute`) sobre un módulo
// concreto del sistema persistido en RolePermissions.
//
// Reglas:
//   - El usuario debe estar autenticado (si no, 401).
//   - SuperAdmin: bypass automático.
//   - El resto pasa por IRolePermissionsService.HasPermissionAsync.
//
// Uso:
//   [RequireModulePermission("ExportManager", "view")]
//   [RequireModulePermission("ExportManager", "edit")]
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireModulePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _module;
    private readonly string _action;

    public RequireModulePermissionAttribute(string module, string action)
    {
        _module = module;
        _action = action;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Bypass total para SuperAdmin
        if (user.IsInRole("SuperAdmin"))
            return;

        var svc = context.HttpContext.RequestServices.GetService<IRolePermissionsService>();
        if (svc == null)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Recoge todos los roles del usuario (normalmente uno solo)
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count == 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        foreach (var role in roles)
        {
            if (await svc.HasPermissionAsync(role, _module, _action))
                return;
        }

        context.Result = new ForbidResult();
    }
}
