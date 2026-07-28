// ============================================================================
// OriginPermissionEvaluator.cs — Evaluación de restricciones de permisos por origen
// ============================================================================
// Implementa la semántica de ViewPermission.AllowedOrigins:
//   - Lista null/vacía            → SIN restricción (cualquier origen).
//   - Entrada loopback            → se evalúa SIEMPRE (::1 ≡ 127.0.0.1 ≡ localhost).
//   - Entrada IP remota           → solo se evalúa cuando MtlsEnabled=FALSE
//                                   (con mTLS activo la identidad de confianza es el cert).
//   - Entrada nombre-de-equipo    → solo se evalúa cuando MtlsEnabled=TRUE,
//                                   comparando contra el CN del certificado cliente
//                                   (validado por Kestrel contra nuestra CA de máquinas).
//   - Entrada no verificable      → se IGNORA (no concede ni deniega).
//   - Ninguna entrada coincide    → fila DENEGADA en este origen.
//
// La restricción aplica a la fila completa (todas las acciones del módulo).
// SuperAdmin hace bypass de todo esto (se controla en los llamadores).
//
// Normalización de IPs: "::1" ≡ "127.0.0.1"; "::ffff:x.x.x.x" ≡ "x.x.x.x".
// ============================================================================

using System.Net;
using System.Security.Cryptography.X509Certificates;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Estado global de mTLS (identidad de máquina por certificado cliente).
/// Se inicializa UNA vez en Program.cs a partir del flag Excel
/// `System Config → MtlsEnabled` (requiere reinicio para cambiar),
/// siguiendo el mismo patrón que OpcUaEnabled/ModbusEnabled.
/// </summary>
public static class MtlsState
{
    /// <summary>Flag Excel MtlsEnabled leído al arranque.</summary>
    public static bool Enabled { get; set; } = false;

    /// <summary>
    /// Flag Excel MtlsRequireRegisteredMachine (modo ESTRICTO, opt-in).
    /// TRUE → el login se RECHAZA (403) para conexiones remotas sin certificado
    /// de máquina válido. Exenciones: loopback/kiosco local (siempre permitido)
    /// y SuperAdmin (break-glass). Solo tiene efecto con MtlsEnabled=TRUE.
    /// </summary>
    public static bool RequireRegisteredMachine { get; set; } = false;

    /// <summary>
    /// CA raíz de máquinas (Aquafrisch Machine CA) usada para firmar y
    /// validar certificados cliente. null si mTLS deshabilitado o CA no cargada.
    /// </summary>
    public static X509Certificate2? MachineCa { get; set; }

    /// <summary>
    /// Valida que un certificado cliente fue emitido por nuestra Machine CA
    /// y está dentro de su periodo de validez. Usado por Kestrel
    /// (ClientCertificateValidation) — si devuelve false el handshake TLS
    /// con cert se rechaza (los clientes SIN cert pasan igualmente porque
    /// el modo es AllowCertificate, no RequireCertificate).
    /// </summary>
    public static bool ValidateClientCertificate(X509Certificate2 clientCert)
    {
        var ca = MachineCa;
        if (ca == null) return false;

        var now = DateTime.Now;
        if (now < clientCert.NotBefore || now > clientCert.NotAfter) return false;

        try
        {
            // La Machine CA está registrada en LocalMachine\Root al arrancar (Program.cs),
            // así chain.Build() funciona con el store del sistema en Windows.
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags =
                X509VerificationFlags.IgnoreEndRevocationUnknown |
                X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown;
            if (!chain.Build(clientCert)) return false;

            // La raíz de la cadena debe ser EXACTAMENTE nuestra CA
            var root = chain.ChainElements[^1].Certificate;
            return root.Thumbprint.Equals(ca.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Modo estricto (MtlsRequireRegisteredMachine): ¿debe RECHAZARSE el login de
    /// esta petición? true = conexión remota SIN certificado de máquina válido y
    /// el usuario NO es SuperAdmin. Loopback/kiosco local siempre permitido.
    /// Se evalúa DESPUÉS de credenciales válidas (para poder eximir a SuperAdmin
    /// sin revelar al atacante si las credenciales eran correctas: misma respuesta 403).
    /// </summary>
    public static bool ShouldBlockLogin(HttpContext http, IEnumerable<string>? userRoles)
    {
        if (!Enabled || !RequireRegisteredMachine) return false;
        var origin = OriginContext.FromHttpContext(http);
        if (origin.RemoteIp == "127.0.0.1") return false;            // kiosco/local: siempre permitido
        if (!string.IsNullOrEmpty(origin.MachineName)) return false; // equipo registrado (cert válido)
        if (userRoles?.Contains("SuperAdmin") == true) return false; // break-glass
        return true;
    }
}

/// <summary>
/// Contexto de origen de una petición: IP normalizada + identidad de máquina
/// (CN del certificado cliente validado, solo presente con mTLS).
/// </summary>
public sealed record OriginContext(string? RemoteIp, string? MachineName)
{
    /// <summary>Extrae el contexto de origen de una petición HTTP.</summary>
    public static OriginContext FromHttpContext(HttpContext http)
    {
        var ip = OriginPermissionEvaluator.NormalizeIp(http.Connection.RemoteIpAddress?.ToString());
        string? machine = null;
        if (MtlsState.Enabled)
        {
            // Kestrel solo expone ClientCertificate si pasó ClientCertificateValidation
            // Kestrel ya validó el cert con ValidateClientCertificate durante el handshake TLS.
            // Solo leemos el CN — no revalidamos (evita doble chain.Build innecesario).
            var cert = http.Connection.ClientCertificate;
            if (cert != null)
                machine = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        }
        return new OriginContext(ip, machine);
    }
}

/// <summary>
/// Evaluador estático de AllowedOrigins (sin estado, thread-safe).
/// </summary>
public static class OriginPermissionEvaluator
{
    /// <summary>
    /// Normaliza una IP: ::1 → 127.0.0.1, ::ffff:x.x.x.x → x.x.x.x.
    /// Devuelve null si la entrada es null/vacía.
    /// </summary>
    public static string? NormalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        ip = ip.Trim();

        if (!IPAddress.TryParse(ip, out var parsed)) return ip;

        // IPv4-mapped IPv6 (::ffff:192.168.1.5) → IPv4 pura
        if (parsed.IsIPv4MappedToIPv6)
            parsed = parsed.MapToIPv4();

        // Loopback canónico: ::1 y 127.x.x.x → 127.0.0.1
        if (IPAddress.IsLoopback(parsed))
            return "127.0.0.1";

        return parsed.ToString();
    }

    /// <summary>true si la entrada de la lista es una IP (vs nombre de equipo).</summary>
    public static bool LooksLikeIp(string entry) => IPAddress.TryParse(entry, out _);

    /// <summary>true si la entrada representa loopback (::1, 127.x, "localhost"
    /// o el nombre del propio host local, p.ej. "C07").</summary>
    public static bool IsLoopbackEntry(string entry)
    {
        if (entry.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        // Nombre del host local (kiosco): equivale a loopback para uniformidad
        // con los nombres de equipo mTLS en AllowedOrigins.
        if (entry.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(entry, out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>
    /// Evalúa si el origen actual satisface la lista AllowedOrigins de una fila.
    /// </summary>
    /// <param name="allowedOrigins">Lista de la fila (null/vacía = sin restricción).</param>
    /// <param name="origin">Contexto de la petición (IP normalizada + CN mTLS).</param>
    /// <returns>true = la fila aplica en este origen.</returns>
    public static bool IsAllowed(List<string>? allowedOrigins, OriginContext origin)
    {
        // Sin restricción → cualquier origen
        if (allowedOrigins == null || allowedOrigins.Count == 0) return true;

        var remoteIsLoopback = origin.RemoteIp == "127.0.0.1";

        foreach (var raw in allowedOrigins)
        {
            var entry = raw?.Trim();
            if (string.IsNullOrEmpty(entry)) continue;

            // 1) Loopback: se evalúa SIEMPRE (kiosk local, independiente de mTLS)
            if (IsLoopbackEntry(entry))
            {
                if (remoteIsLoopback) return true;
                continue;
            }

            // 2) IP remota: solo verificable SIN mTLS (con mTLS la IP no es identidad)
            if (LooksLikeIp(entry))
            {
                if (MtlsState.Enabled) continue; // no verificable en este modo → IGNORAR
                var normEntry = NormalizeIp(entry);
                if (normEntry != null && normEntry.Equals(origin.RemoteIp, StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            // 3) Nombre de equipo: solo verificable CON mTLS (CN del cert validado)
            if (!MtlsState.Enabled) continue; // no verificable en este modo → IGNORAR
            if (!string.IsNullOrEmpty(origin.MachineName) &&
                entry.Equals(origin.MachineName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false; // había restricción y ninguna entrada coincidió
    }

    /// <summary>
    /// Comprueba si una ViewPermission concede una acción concreta
    /// (view/create/edit/delete/export/execute). Sin evaluar orígenes.
    /// </summary>
    public static bool GrantsAction(ViewPermission vp, string action) => action.ToLowerInvariant() switch
    {
        "view" => vp.CanView,
        "create" => vp.CanCreate,
        "edit" => vp.CanEdit,
        "delete" => vp.CanDelete,
        "export" => vp.CanExport,
        "execute" => vp.CanExecute,
        _ => false
    };
}
