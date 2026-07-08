// ============================================================================
// SecurityHeadersMiddleware.cs
// ============================================================================
// EU CRA / IEC 62443-4-1 / OWASP - Cabeceras de seguridad HTTP.
// CSP añadida v1.2 — estricta, calibrada para el supervisor SCADA en red OT
// aislada (sin CDN externo, sin eval, sin inline scripts, SignalR same-origin).
// ============================================================================

using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Middleware;

public sealed class SecurityHeadersMiddleware
{
    // CSP estricta para Production. Permite:
    //  - script-src 'self' 'wasm-unsafe-eval' (React build self-hosted + Babylon.js
    //    necesita WebAssembly para Draco/KTX2/MeshOpt decoders y compilador shader).
    //    'wasm-unsafe-eval' NO permite eval() JS clásico — solo WebAssembly.compile.
    //  - style-src 'self' 'unsafe-inline' (React inyecta estilos inline; aceptable
    //    porque no afecta scripts y los inputs de usuario no llegan a CSS).
    //  - img-src 'self' data: blob: (iconos SVG inline base64 + screenshots Babylon)
    //  - font-src 'self' (fuentes locales en /fonts/, sin Google Fonts)
    //  - connect-src 'self' blob: (API REST + SignalR same-origin + Babylon.js fetches
    //    GLB/textures vía blob: URLs). + https://login.microsoftonline.com SOLO si
    //    EntraIdEnabled=TRUE (gated, igual que OPC-UA/Modbus) — MSAL necesita este
    //    origen en connect-src para el intercambio de token SSO; inocuo/ausente si
    //    el flag está OFF (mismo comportamiento exacto que antes de Entra ID).
    //  - media-src/worker-src 'self' blob: (modelos 3D y workers internos Babylon)
    //  - frame-src 'self' blob: (visor de PDF integrado usa <iframe src="blob:...">)
    //  - object-src 'none' (sin Flash/applets — PDFs van por iframe, no por <object>)
    //  - frame-ancestors 'none' (refuerza X-Frame-Options: DENY-equiv)
    //  - base-uri 'self', form-action 'self' (anti-rebase / anti-form-hijacking)
    //  - upgrade-insecure-requests (cualquier http:// del HTML se sube a https://)
    private const string EntraIdConnectSrc = "https://login.microsoftonline.com";

    private static string BuildProductionCsp(bool entraIdEnabled)
    {
        var connectSrc = entraIdEnabled ? $"'self' blob: {EntraIdConnectSrc}" : "'self' blob:";
        return
            "default-src 'self'; " +
            "script-src 'self' 'wasm-unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self'; " +
            $"connect-src {connectSrc}; " +
            "media-src 'self' blob:; " +
            "worker-src 'self' blob:; " +
            "frame-src 'self' blob:; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'; " +
            "upgrade-insecure-requests";
    }

    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public Task InvokeAsync(HttpContext context, IEntraIdService entraIdService)
    {
        var headers = context.Response.Headers;

        // Evita MIME sniffing (OWASP).
        if (!headers.ContainsKey("X-Content-Type-Options"))
            headers["X-Content-Type-Options"] = "nosniff";

        // Bloquea framing externo (clickjacking). SAMEORIGIN permite iframes propios.
        if (!headers.ContainsKey("X-Frame-Options"))
            headers["X-Frame-Options"] = "SAMEORIGIN";

        // No filtrar la URL completa a terceros.
        if (!headers.ContainsKey("Referrer-Policy"))
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // CSP solo en Production: evita romper el hot-reload de webpack/CRA en Dev.
        // En Dev el frontend corre en puerto 3001 con webpack-dev-server (inline scripts
        // + eval para HMR), por lo que una CSP estricta lo rompería.
        if (_env.IsProduction() && !headers.ContainsKey("Content-Security-Policy"))
            headers["Content-Security-Policy"] = BuildProductionCsp(entraIdService.IsEnabled);

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
