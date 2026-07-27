// ============================================================================
// RequireModulePermissionAttribute.cs — Autorización por módulo/acción
// ============================================================================
// Filtro reutilizable que valida que el rol del usuario tenga el permiso
// (`view`, `create`, `edit`, `delete`, `export`, `execute`) sobre un módulo
// concreto del sistema persistido en RolePermissions.
//
// Reglas:
//   - El usuario debe estar autenticado (si no, 401).
//   - SuperAdmin: bypass automático (incluye restricciones por origen).
//   - El resto pasa por IRolePermissionsService.GetModulePermissionAsync
//     + evaluación de AllowedOrigins (restricción por IP/equipo, ver
//     OriginPermissionEvaluator). Denegación por origen → audit log (EU CRA).
//
// Uso:
//   [RequireModulePermission("ExportManager", "view")]
//   [RequireModulePermission("ExportManager", "edit")]
// ============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SW.PC.API.Backend.Models;
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

        var origin = OriginContext.FromHttpContext(context.HttpContext);
        bool grantedButBlockedByOrigin = false;

        foreach (var role in roles)
        {
            var vp = await svc.GetModulePermissionAsync(role, _module);
            if (vp == null) continue;
            if (!OriginPermissionEvaluator.GrantsAction(vp, _action)) continue;

            // El rol concede la acción → evaluar restricción por origen de la fila
            if (OriginPermissionEvaluator.IsAllowed(vp.AllowedOrigins, origin))
                return; // ✅ permitido

            grantedButBlockedByOrigin = true;
        }

        // 🔒 EU CRA — Auditar denegación por restricción de origen (el rol tenía el
        // permiso, pero la fila está limitada a otros equipos/IPs).
        if (grantedButBlockedByOrigin)
        {
            try
            {
                var audit = context.HttpContext.RequestServices.GetService<IAuditLogService>();
                if (audit != null)
                {
                    var username = user.Identity?.Name ?? "unknown";
                    await audit.LogAsync(
                        AuditCategory.Security,
                        AuditAction.PermissionDenied,
                        AuditResult.Warning,
                        details: $"Permiso '{_module}.{_action}' denegado por restricción de origen. " +
                                 $"IP={origin.RemoteIp ?? "?"}, Equipo={origin.MachineName ?? "(sin cert)"}, " +
                                 $"Roles={string.Join(",", roles)}",
                        userName: username,
                        ipAddress: origin.RemoteIp);
                }
            }
            catch { /* la auditoría nunca debe romper la respuesta */ }
        }

        context.Result = new ForbidResult();
    }
}
