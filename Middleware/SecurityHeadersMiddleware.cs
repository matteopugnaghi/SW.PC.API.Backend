// ============================================================================
// SecurityHeadersMiddleware.cs
// ============================================================================
// EU CRA / IEC 62443-4-1 / OWASP - Cabeceras de seguridad HTTP básicas.
// Política intencionadamente CONSERVADORA para no romper la UI Babylon.js /
// SignalR existente. No se añade CSP (requiere análisis dedicado del front).
// ============================================================================

namespace SW.PC.API.Backend.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
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

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
