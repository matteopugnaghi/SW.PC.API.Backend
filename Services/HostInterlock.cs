// ============================================================================
// HostInterlock.cs — HostName Interlocking (control exclusivo de la máquina por equipo)
// ============================================================================
// Algunos clientes exigen que, mientras alguien trabaja desde una pantalla, nadie
// más pueda trabajar en la máquina; otros quieren además que determinados equipos
// (p. ej. el ordenador principal) puedan forzar la toma de control.
//
// Tres niveles de equipo (Usuarios → Equipos):
//   · Normal
//   · Interlocking
//   · Interlocking + Override
//
//   Equipo que entra        | Solo equipos normales conectados | Hay un equipo con interlocking conectado
//   ------------------------+----------------------------------+------------------------------------------
//   Normal                  | Entra                            | BLOQUEADO
//   Interlocking            | Entra y expulsa a los demás      | BLOQUEADO
//   Interlocking + Override | Entra y expulsa a los demás      | Puede FORZAR → entra y expulsa a todos
//   SuperAdmin (cualquiera) | Según su equipo                  | Puede FORZAR (break-glass)
//
// · Solo tiene efecto con MtlsRequireRegisteredMachine=TRUE: el nombre de equipo es una
//   identidad VERIFICADA (certificado, token o loopback) y solo los equipos registrados
//   pueden iniciar sesión. Con el flag en FALSE no se evalúa nada.
// · "Conectado" = sesión válida + conexión SignalR viva (o dentro de la gracia de
//   reconexión). Una sesión colgada (navegador cerrado sin logout) no bloquea a nadie,
//   pero se revoca cuando entra otro equipo para que no pueda reaparecer sin pasar por
//   el login.
// · Un equipo nunca se bloquea a sí mismo (cambio de usuario en la misma pantalla).
//
// Estático y sin estado propio, igual que MtlsState / TokenDeviceRegistry.
// ============================================================================

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>Origen verificado de un intento de login.</summary>
/// <param name="MachineName">Nombre del equipo ("" si no está identificado).</param>
/// <param name="IsLoopback">true si la petición llega desde el propio servidor (kiosco local).</param>
/// <param name="RemoteIp">IP normalizada (solo para mostrar cuando no hay nombre de equipo).</param>
public sealed record LoginOrigin(string MachineName, bool IsLoopback, string? RemoteIp = null);

public sealed record HostInterlockFlags(bool Interlocking, bool OverrideInterlocking)
{
    public static readonly HostInterlockFlags None = new(false, false);
}

public enum HostInterlockDecision
{
    Allow,
    Blocked,
    OverrideAvailable
}

/// <summary>Resultado de la regla pura de interlocking.</summary>
/// <param name="Decision">Permitido, bloqueado o bloqueado con opción de forzar.</param>
/// <param name="BlockingHosts">Equipos con interlocking conectados que impiden (o condicionan) el login.</param>
/// <param name="ExpelAllOthers">El equipo que entra tiene interlocking: se expulsa a todos los demás equipos.</param>
/// <param name="HostsToExpel">Equipos conectados a expulsar (vacío si no se expulsa a nadie).</param>
public sealed record HostInterlockEvaluation(
    HostInterlockDecision Decision,
    IReadOnlyList<string> BlockingHosts,
    bool ExpelAllOthers,
    IReadOnlyList<string> HostsToExpel);

/// <summary>Toma de control aplicada en un login: a quién hay que avisar por SignalR.</summary>
public sealed class HostInterlockTakeover
{
    /// <summary>Nombre del equipo que toma el control ("" si no está identificado).</summary>
    public string ByMachineName { get; init; } = "";
    /// <summary>Nombre a mostrar del equipo que toma el control (nombre de equipo o, si no hay, IP).</summary>
    public string ByHost { get; init; } = "";
    public string ByUser { get; init; } = "";
    public bool ExpelAllOthers { get; init; }
    public IReadOnlyList<string> ExpelledHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExpelledUsers { get; init; } = Array.Empty<string>();
    public bool Forced { get; init; }
}

/// <summary>
/// Resultado de evaluar el interlocking para un login concreto. Las revocaciones NO se aplican
/// al evaluar: solo cuando el login se va a completar (<see cref="Apply"/>), en el mismo
/// SaveChanges que crea la nueva sesión.
/// </summary>
public sealed class HostInterlockLoginCheck
{
    public static readonly HostInterlockLoginCheck NotApplicable = new();

    public HostInterlockDecision Decision { get; init; } = HostInterlockDecision.Allow;
    public IReadOnlyList<string> BlockingHosts { get; init; } = Array.Empty<string>();

    internal LoginOrigin? Origin { get; init; }
    internal bool ExpelAllOthers { get; init; }
    internal bool Forced { get; init; }
    internal List<UserSession> SessionsToRevoke { get; init; } = new();

    public bool IsAllowed => Decision == HostInterlockDecision.Allow;

    public string? ErrorCode => Decision switch
    {
        HostInterlockDecision.Blocked => HostInterlock.ErrorBlocked,
        HostInterlockDecision.OverrideAvailable => HostInterlock.ErrorOverrideAvailable,
        _ => null
    };

    public string BlockingHostsText => string.Join(", ", BlockingHosts);

    /// <summary>
    /// Marca como revocadas las sesiones de los equipos expulsados (entidades rastreadas por el
    /// DbContext del login; las persiste el SaveChanges del llamador). Devuelve la toma de
    /// control a notificar, o null si no se expulsó a nadie.
    /// </summary>
    public HostInterlockTakeover? Apply(string byUser)
    {
        if (Origin == null) return null;

        var byHost = HostInterlock.DisplayName(Origin);
        var reason = HostInterlock.BuildRevokedReason(byHost, byUser);
        var now = DateTime.Now;
        var revoked = new List<UserSession>();

        foreach (var session in SessionsToRevoke)
        {
            if (session.IsRevoked) continue; // ya cerrada por otra regla del mismo login
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = reason;
            revoked.Add(session);
        }

        if (revoked.Count == 0) return null;

        return new HostInterlockTakeover
        {
            ByMachineName = Origin.MachineName,
            ByHost = byHost,
            ByUser = byUser,
            ExpelAllOthers = ExpelAllOthers,
            Forced = Forced,
            ExpelledHosts = revoked.Select(s => s.MachineName ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ExpelledUsers = revoked.Select(s => s.User?.Username ?? $"#{s.UserId}")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}

public static class HostInterlock
{
    public const string ErrorBlocked = "INTERLOCK_BLOCKED";
    public const string ErrorOverrideAvailable = "INTERLOCK_OVERRIDE_AVAILABLE";

    /// <summary>Evento SignalR que recibe un equipo expulsado: { byHost, byUser }.</summary>
    public const string TerminatedEvent = "InterlockSessionTerminated";

    private const string RevokedReasonPrefix = "Interlocking: control tomado por ";
    private static readonly Regex RevokedReasonRegex =
        new(@"^Interlocking: control tomado por (?<host>.*) \((?<user>.*)\)$", RegexOptions.Compiled);

    /// <summary>
    /// Margen entre el login (sesión creada) y la conexión SignalR del cliente: durante este
    /// tiempo la sesión cuenta como "conectada" aunque todavía no haya conexión viva.
    /// </summary>
    private static readonly TimeSpan LoginToConnectGrace = TimeSpan.FromSeconds(30);

    /// <summary>El interlocking solo se evalúa con identidad de equipo obligatoria (mTLS estricto).</summary>
    public static bool Enabled => MtlsState.Enabled && MtlsState.RequireRegisteredMachine;

    public static bool IsInterlockError(string? errorCode) =>
        errorCode == ErrorBlocked || errorCode == ErrorOverrideAvailable;

    /// <summary>Nombre del equipo local (kiosco): el que ven las conexiones loopback.</summary>
    public static string LocalHostName => Environment.MachineName;

    /// <summary>
    /// Origen verificado de la petición, con el mismo criterio que ScadaHub.OnConnectedAsync:
    /// CN del certificado o nombre del dispositivo por token; el nombre del servidor si es
    /// loopback; "" en cualquier otro caso.
    /// </summary>
    public static LoginOrigin GetLoginOrigin(HttpContext http)
    {
        var origin = OriginContext.FromHttpContext(http);
        var isLoopback = origin.RemoteIp == "127.0.0.1";
        var machine = !string.IsNullOrEmpty(origin.MachineName)
            ? origin.MachineName
            : (isLoopback ? LocalHostName : "");
        return new LoginOrigin(machine, isLoopback, origin.RemoteIp);
    }

    public static string DisplayName(LoginOrigin origin) =>
        !string.IsNullOrEmpty(origin.MachineName) ? origin.MachineName : (origin.RemoteIp ?? "");

    private static bool SameHost(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    // ────────────────────────────────────────────────────────────────────────
    // Regla pura (sin BD ni estado): ver la tabla de la cabecera.
    // ────────────────────────────────────────────────────────────────────────
    /// <param name="attemptingHost">Equipo que intenta iniciar sesión ("" = sin identificar).</param>
    /// <param name="canAlwaysOverride">SuperAdmin: puede forzar desde cualquier equipo.</param>
    /// <param name="force">El usuario ya confirmó en el popup que quiere forzar.</param>
    /// <param name="activeHosts">Equipos conectados (puede incluir al propio equipo).</param>
    /// <param name="flagsOf">Niveles configurados de un equipo.</param>
    public static HostInterlockEvaluation Evaluate(
        string attemptingHost,
        bool canAlwaysOverride,
        bool force,
        IEnumerable<string> activeHosts,
        Func<string, HostInterlockFlags> flagsOf)
    {
        var others = activeHosts
            .Select(h => h ?? "")
            .Where(h => !SameHost(h, attemptingHost))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var blockers = others.Where(h => flagsOf(h).Interlocking).ToList();
        var mine = flagsOf(attemptingHost ?? "");
        var none = (IReadOnlyList<string>)Array.Empty<string>();

        if (blockers.Count > 0)
        {
            // Override exige Interlocking (un override "suelto" no concede nada).
            var canOverride = canAlwaysOverride || (mine.Interlocking && mine.OverrideInterlocking);
            if (!canOverride)
                return new HostInterlockEvaluation(HostInterlockDecision.Blocked, blockers, false, none);
            if (!force)
                return new HostInterlockEvaluation(HostInterlockDecision.OverrideAvailable, blockers, false, none);

            // Forzado: un equipo con interlocking se queda solo; un SuperAdmin desde un equipo
            // normal solo desaloja a quienes le bloqueaban.
            return new HostInterlockEvaluation(HostInterlockDecision.Allow, blockers, mine.Interlocking,
                mine.Interlocking ? others : blockers);
        }

        return new HostInterlockEvaluation(HostInterlockDecision.Allow, none, mine.Interlocking,
            mine.Interlocking ? others : none);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Evaluación de un login contra la BD del proyecto + conexiones vivas.
    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Evalúa el interlocking para un login con credenciales ya validadas. No modifica nada:
    /// las sesiones a revocar se devuelven rastreadas por <paramref name="db"/> y se marcan con
    /// <see cref="HostInterlockLoginCheck.Apply"/> cuando el login se completa.
    /// </summary>
    public static async Task<HostInterlockLoginCheck> EvaluateLoginAsync(
        AquafrischDbContext db, LoginOrigin? origin, bool isSuperAdmin, bool force)
    {
        if (!Enabled || origin == null) return HostInterlockLoginCheck.NotApplicable;

        // Ningún equipo con interlocking configurado → el login se comporta como siempre.
        var settings = await db.HostInterlockSettings.AsNoTracking()
            .Where(s => s.Interlocking)
            .ToListAsync();
        if (settings.Count == 0) return HostInterlockLoginCheck.NotApplicable;

        var registered = await GetRegisteredHostNamesAsync(db);
        var host = origin.MachineName ?? "";

        // Equipo no reconocido (sin identidad o con el registro revocado) y usuario no
        // SuperAdmin: el controlador rechaza este login por mTLS estricto justo después.
        var recognised = origin.IsLoopback || (host.Length > 0 && registered.Contains(host));
        if (!recognised && !isSuperAdmin) return HostInterlockLoginCheck.NotApplicable;

        var flagsByHost = new Dictionary<string, HostInterlockFlags>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in settings)
            flagsByHost[s.HostName] = new HostInterlockFlags(s.Interlocking, s.OverrideInterlocking);

        // Los niveles solo valen para equipos que siguen registrados (un registro revocado
        // deja de bloquear y de expulsar aunque conserve su configuración).
        HostInterlockFlags FlagsOf(string h) =>
            h.Length > 0 && registered.Contains(h) && flagsByHost.TryGetValue(h, out var f)
                ? f
                : HostInterlockFlags.None;

        var now = DateTime.Now;
        var otherSessions = (await db.UserSessions
                .Include(s => s.User)
                .Where(s => !s.IsRevoked && s.ExpiresAt > now)
                .ToListAsync())
            .Where(s => !SameHost(s.MachineName, host))
            .ToList();

        var liveHosts = ClientConnectionTrackerService.GetLiveHostNames();
        bool IsConnected(UserSession s) =>
            liveHosts.Contains(s.MachineName ?? "") || (now - s.CreatedAt) <= LoginToConnectGrace;

        var activeHosts = otherSessions.Where(IsConnected).Select(s => s.MachineName ?? "");
        var evaluation = Evaluate(host, isSuperAdmin, force, activeHosts, FlagsOf);

        if (evaluation.Decision != HostInterlockDecision.Allow)
        {
            return new HostInterlockLoginCheck
            {
                Decision = evaluation.Decision,
                BlockingHosts = evaluation.BlockingHosts,
                Origin = origin
            };
        }

        List<UserSession> toRevoke;
        if (evaluation.ExpelAllOthers)
        {
            toRevoke = otherSessions; // conectadas o colgadas: el equipo se queda solo
        }
        else
        {
            var expel = new HashSet<string>(evaluation.HostsToExpel, StringComparer.OrdinalIgnoreCase);
            toRevoke = otherSessions
                .Where(s => expel.Contains(s.MachineName ?? "")
                         // Sesión colgada de un equipo con interlocking: se revoca para que no
                         // reaparezca (reconexión de red) saltándose el login.
                         || (FlagsOf(s.MachineName ?? "").Interlocking && !IsConnected(s)))
                .ToList();
        }

        return new HostInterlockLoginCheck
        {
            Decision = HostInterlockDecision.Allow,
            BlockingHosts = evaluation.BlockingHosts,
            Origin = origin,
            ExpelAllOthers = evaluation.ExpelAllOthers,
            Forced = force && evaluation.BlockingHosts.Count > 0,
            SessionsToRevoke = toRevoke
        };
    }

    /// <summary>
    /// Equipos que pueden tener niveles de interlocking: el equipo local, los equipos con
    /// certificado enrolado y los dispositivos por token no revocados.
    /// </summary>
    public static async Task<HashSet<string>> GetRegisteredHostNamesAsync(AquafrischDbContext db)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { LocalHostName };

        var machines = await db.MachineRegistrationCodes.AsNoTracking()
            .Where(c => c.UsedAt != null && c.MachineName != null)
            .Select(c => c.MachineName!)
            .ToListAsync();
        foreach (var m in machines)
            if (!string.IsNullOrWhiteSpace(m)) names.Add(m);

        var devices = await db.TokenDevices.AsNoTracking()
            .Where(d => !d.Revoked)
            .Select(d => d.Name)
            .ToListAsync();
        foreach (var d in devices)
            if (!string.IsNullOrWhiteSpace(d)) names.Add(d);

        return names;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Motivo de revocación (legible en el listado de sesiones y reconocible al reconectar)
    // ────────────────────────────────────────────────────────────────────────
    public static string BuildRevokedReason(string byHost, string byUser)
    {
        var reason = $"{RevokedReasonPrefix}{byHost} ({byUser})";
        return reason.Length <= 200 ? reason : reason[..200];
    }

    public static bool TryParseRevokedReason(string? reason, out string byHost, out string byUser)
    {
        byHost = "";
        byUser = "";
        if (string.IsNullOrEmpty(reason) || !reason.StartsWith(RevokedReasonPrefix, StringComparison.Ordinal))
            return false;

        var match = RevokedReasonRegex.Match(reason);
        if (match.Success)
        {
            byHost = match.Groups["host"].Value;
            byUser = match.Groups["user"].Value;
        }
        return true; // motivo de interlocking aunque esté truncado
    }

    // ────────────────────────────────────────────────────────────────────────
    // Avisos SignalR a los equipos expulsados
    // ────────────────────────────────────────────────────────────────────────
    /// <summary>Avisa a las conexiones vivas de los equipos expulsados. Nunca lanza.</summary>
    public static async Task NotifyTakeoverAsync(IServiceProvider services, HostInterlockTakeover? takeover, ILogger logger)
    {
        if (takeover == null) return;
        try
        {
            var expelled = new HashSet<string>(takeover.ExpelledHosts, StringComparer.OrdinalIgnoreCase);
            var connectionIds = ClientConnectionTrackerService.GetConnectionIdsByHost(h =>
                takeover.ExpelAllOthers ? !SameHost(h, takeover.ByMachineName) : expelled.Contains(h));
            if (connectionIds.Count == 0) return;

            var hub = services.GetRequiredService<IHubContext<ScadaHub>>();
            await hub.Clients.Clients(connectionIds)
                .SendAsync(TerminatedEvent, new { byHost = takeover.ByHost, byUser = takeover.ByUser });

            logger.LogWarning("🔒 Interlocking: {Count} conexión(es) avisadas de la toma de control por {Host} ({User})",
                connectionIds.Count, takeover.ByHost, takeover.ByUser);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Interlocking: no se pudo avisar a los equipos expulsados");
        }
    }

    /// <summary>
    /// Si la sesión de esta conexión SignalR fue cerrada por una toma de control, devuelve el
    /// aviso { byHost, byUser } a enviar al cliente; null en cualquier otro caso. Cubre al
    /// cliente que estaba desconectado cuando se envió el aviso (micro-corte de red) y
    /// reconecta con un JWT que sigue siendo válido. Nunca lanza.
    /// </summary>
    public static async Task<object?> GetTakeoverNoticeAsync(IServiceProvider services, HttpContext? http, ILogger logger)
    {
        if (!Enabled || http == null) return null;
        try
        {
            string? token = http.Request.Query["access_token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                var header = http.Request.Headers.Authorization.FirstOrDefault();
                if (header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    token = header["Bearer ".Length..].Trim();
            }
            if (string.IsNullOrEmpty(token)) return null;

            var factory = services.GetRequiredService<IProjectDbContextFactory>();
            await using var db = factory.CreateDbContext();
            var session = await db.UserSessions.AsNoTracking()
                .Where(s => s.Token == token)
                .Select(s => new { s.IsRevoked, s.RevokedReason })
                .FirstOrDefaultAsync();

            if (session == null || !session.IsRevoked) return null;
            if (!TryParseRevokedReason(session.RevokedReason, out var byHost, out var byUser)) return null;
            return new { byHost, byUser };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "⚠️ Interlocking: no se pudo comprobar la sesión de la conexión SignalR");
            return null;
        }
    }
}
