using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// HostName Interlocking: control exclusivo de la máquina por equipo.
///  • Normal / Interlocking / Interlocking+Override (ver cabecera de HostInterlock.cs).
///  • "Conectado" = sesión válida + conexión SignalR viva (o en gracia).
/// Usa el estado estático de ClientConnectionTrackerService y MtlsState → misma colección
/// que los tests de slots para ejecutarse en serie.
/// </summary>
[Collection("ClientConnectionTracker")]
public class HostInterlockTests : IDisposable
{
    private const string Panel = "PANEL-1";      // interlocking
    private const string Master = "MASTER-PC";   // interlocking + override
    private const string Tablet = "TABLET-1";    // normal
    private const string Tablet2 = "TABLET-2";   // normal

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"interlock-test-{Guid.NewGuid():N}.db");
    private readonly bool _mtlsEnabled = MtlsState.Enabled;
    private readonly bool _mtlsStrict = MtlsState.RequireRegisteredMachine;

    public HostInterlockTests()
    {
        ClientConnectionTrackerService.ResetStateForTesting();
        MtlsState.Enabled = true;
        MtlsState.RequireRegisteredMachine = true;
    }

    public void Dispose()
    {
        ClientConnectionTrackerService.ResetStateForTesting();
        MtlsState.Enabled = _mtlsEnabled;
        MtlsState.RequireRegisteredMachine = _mtlsStrict;
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* fichero temporal */ }
    }

    // ───────────────────────── Regla pura ─────────────────────────

    private static HostInterlockFlags Flags(string host) => host.ToUpperInvariant() switch
    {
        Panel => new HostInterlockFlags(true, false),
        Master => new HostInterlockFlags(true, true),
        "OVERRIDE-ONLY" => new HostInterlockFlags(false, true),
        _ => HostInterlockFlags.None
    };

    private static HostInterlockEvaluation Evaluate(string host, string[] active, bool superAdmin = false, bool force = false) =>
        HostInterlock.Evaluate(host, superAdmin, force, active, Flags);

    [Theory]
    [InlineData(Tablet)]            // normal
    [InlineData(Panel + "-B")]      // otro equipo sin niveles
    [InlineData("OVERRIDE-ONLY")]   // override sin interlocking no concede nada
    [InlineData("")]                // sin identificar
    public void InterlockingHostConnected_BlocksHostsWithoutOverride(string host)
    {
        var result = Evaluate(host, new[] { Panel, Tablet2 }, force: true);

        Assert.Equal(HostInterlockDecision.Blocked, result.Decision);
        Assert.Equal(new[] { Panel }, result.BlockingHosts);
        Assert.Empty(result.HostsToExpel);
    }

    [Fact]
    public void InterlockingHost_IsAlsoBlockedByAnotherInterlockingHost()
    {
        var result = HostInterlock.Evaluate("PANEL-2", false, true, new[] { Panel },
            h => h is Panel or "PANEL-2" ? new HostInterlockFlags(true, false) : HostInterlockFlags.None);

        Assert.Equal(HostInterlockDecision.Blocked, result.Decision);
    }

    [Fact]
    public void OverrideHost_MustConfirmBeforeTakingControl()
    {
        var asked = Evaluate(Master, new[] { Panel, Tablet });
        Assert.Equal(HostInterlockDecision.OverrideAvailable, asked.Decision);
        Assert.Equal(new[] { Panel }, asked.BlockingHosts);
        Assert.Empty(asked.HostsToExpel);

        var forced = Evaluate(Master, new[] { Panel, Tablet }, force: true);
        Assert.Equal(HostInterlockDecision.Allow, forced.Decision);
        Assert.True(forced.ExpelAllOthers);
        Assert.Equal(new[] { Panel, Tablet }, forced.HostsToExpel);
    }

    [Fact]
    public void SuperAdminFromNormalHost_ForcesOutOnlyTheBlockingHosts()
    {
        Assert.Equal(HostInterlockDecision.OverrideAvailable,
            Evaluate(Tablet, new[] { Panel, Tablet2 }, superAdmin: true).Decision);

        var forced = Evaluate(Tablet, new[] { Panel, Tablet2 }, superAdmin: true, force: true);
        Assert.Equal(HostInterlockDecision.Allow, forced.Decision);
        Assert.False(forced.ExpelAllOthers);
        Assert.Equal(new[] { Panel }, forced.HostsToExpel);
    }

    [Fact]
    public void InterlockingHost_EntersAndExpelsNormalHosts_WithoutConfirmation()
    {
        var result = Evaluate(Panel, new[] { Tablet, Tablet2, "" });

        Assert.Equal(HostInterlockDecision.Allow, result.Decision);
        Assert.True(result.ExpelAllOthers);
        Assert.Equal(new[] { Tablet, Tablet2, "" }, result.HostsToExpel);
    }

    [Fact]
    public void NormalHosts_DoNotAffectEachOther()
    {
        var result = Evaluate(Tablet, new[] { Tablet2 });

        Assert.Equal(HostInterlockDecision.Allow, result.Decision);
        Assert.False(result.ExpelAllOthers);
        Assert.Empty(result.HostsToExpel);
    }

    [Fact]
    public void Host_NeverBlocksItself_IgnoringCase()
    {
        var result = Evaluate("panel-1", new[] { Panel });

        Assert.Equal(HostInterlockDecision.Allow, result.Decision);
        Assert.Empty(result.HostsToExpel);
    }

    // ───────────────────── Evaluación contra la BD ─────────────────────

    private AquafrischDbContext CreateDb()
    {
        var db = AquafrischDbContextFactory.Create(_dbPath);
        db.Database.EnsureCreated();
        return db;
    }

    private static void Register(AquafrischDbContext db, string host, bool interlocking = false, bool overrideInterlocking = false)
    {
        db.TokenDevices.Add(new TokenDevice { TokenHash = Guid.NewGuid().ToString("N"), Name = host, CreatedBy = "test" });
        if (interlocking || overrideInterlocking)
        {
            db.HostInterlockSettings.Add(new HostInterlockSetting
            {
                HostName = host, Interlocking = interlocking, OverrideInterlocking = overrideInterlocking
            });
        }
    }

    private static UserSession AddSession(AquafrischDbContext db, string username, string? host, int ageMinutes = 5)
    {
        var user = new User { Username = username };
        db.Users.Add(user);
        var session = new UserSession
        {
            User = user,
            Token = Guid.NewGuid().ToString("N"),
            MachineName = host,
            CreatedAt = DateTime.Now.AddMinutes(-ageMinutes),
            ExpiresAt = DateTime.Now.AddMinutes(30)
        };
        db.UserSessions.Add(session);
        return session;
    }

    private static void ConnectSignalR(string connectionId, string username, string host)
    {
        lock (ClientConnectionTrackerService.LockObj)
        {
            ClientConnectionTrackerService.ConnectedClients[connectionId] = (username, "192.168.1.50", "", host);
        }
    }

    private static LoginOrigin Remote(string host) => new(host, false, "192.168.1.60");

    [Fact]
    public async Task ConnectedInterlockingHost_BlocksLogin_ButAStaleSessionDoesNot()
    {
        await using var db = CreateDb();
        Register(db, Panel, interlocking: true);
        Register(db, Tablet);
        var panelSession = AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();

        // Sesión válida pero sin conexión viva (navegador cerrado sin logout): no bloquea,
        // y se revoca al completarse el login del otro equipo para que no pueda reaparecer.
        var stale = await HostInterlock.EvaluateLoginAsync(db, Remote(Tablet), isSuperAdmin: false, force: false);
        Assert.True(stale.IsAllowed);
        var takeover = stale.Apply("mantenimiento");
        Assert.NotNull(takeover);
        Assert.True(panelSession.IsRevoked);
        Assert.True(HostInterlock.TryParseRevokedReason(panelSession.RevokedReason, out var byHost, out var byUser));
        Assert.Equal(Tablet, byHost);
        Assert.Equal("mantenimiento", byUser);

        // Con el panel conectado, el mismo login queda bloqueado.
        panelSession.IsRevoked = false;
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "operador", Panel);

        var blocked = await HostInterlock.EvaluateLoginAsync(db, Remote(Tablet), isSuperAdmin: false, force: true);
        Assert.Equal(HostInterlockDecision.Blocked, blocked.Decision);
        Assert.Equal(HostInterlock.ErrorBlocked, blocked.ErrorCode);
        Assert.Equal(Panel, blocked.BlockingHostsText);
        Assert.Null(blocked.Apply("mantenimiento"));
        Assert.False(panelSession.IsRevoked);
    }

    [Fact]
    public async Task SessionJustCreated_CountsAsConnected_BeforeSignalRConnects()
    {
        await using var db = CreateDb();
        Register(db, Panel, interlocking: true);
        Register(db, Tablet);
        AddSession(db, "operador", Panel, ageMinutes: 0);
        await db.SaveChangesAsync();

        var result = await HostInterlock.EvaluateLoginAsync(db, Remote(Tablet), isSuperAdmin: false, force: false);

        Assert.Equal(HostInterlockDecision.Blocked, result.Decision);
    }

    [Fact]
    public async Task InterlockingLogin_RevokesEveryOtherHost_ButNotItsOwnSessions()
    {
        await using var db = CreateDb();
        Register(db, Panel, interlocking: true);
        Register(db, Tablet);
        var own = AddSession(db, "turno-anterior", Panel);
        var tablet = AddSession(db, "mantenimiento", Tablet);
        var legacy = AddSession(db, "antiguo", null);
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "mantenimiento", Tablet);

        var check = await HostInterlock.EvaluateLoginAsync(db, Remote(Panel), isSuperAdmin: false, force: false);
        Assert.True(check.IsAllowed);
        var takeover = check.Apply("operador");

        Assert.NotNull(takeover);
        Assert.True(takeover!.ExpelAllOthers);
        Assert.False(takeover.Forced);
        Assert.Equal(Panel, takeover.ByHost);
        Assert.True(tablet.IsRevoked);
        Assert.True(legacy.IsRevoked);
        Assert.False(own.IsRevoked);
    }

    [Fact]
    public async Task OverrideHost_ForcesOutTheInterlockingHost_OnlyAfterConfirmation()
    {
        await using var db = CreateDb();
        Register(db, Panel, interlocking: true);
        Register(db, Master, interlocking: true, overrideInterlocking: true);
        var panel = AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "operador", Panel);

        var asked = await HostInterlock.EvaluateLoginAsync(db, Remote(Master), isSuperAdmin: false, force: false);
        Assert.Equal(HostInterlock.ErrorOverrideAvailable, asked.ErrorCode);
        Assert.False(panel.IsRevoked);

        var forced = await HostInterlock.EvaluateLoginAsync(db, Remote(Master), isSuperAdmin: false, force: true);
        var takeover = forced.Apply("jefe");

        Assert.True(panel.IsRevoked);
        Assert.True(takeover!.Forced);
        Assert.Equal(new[] { Panel }, takeover.ExpelledHosts);
        Assert.Equal(new[] { "operador" }, takeover.ExpelledUsers);
    }

    [Fact]
    public async Task RevokedRegistration_NoLongerBlocksNorExpels()
    {
        await using var db = CreateDb();
        // Configuración de interlocking que sobrevive a un registro revocado/eliminado.
        db.HostInterlockSettings.Add(new HostInterlockSetting { HostName = Panel, Interlocking = true });
        Register(db, Tablet);
        var tablet = AddSession(db, "mantenimiento", Tablet);
        AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "operador", Panel);
        ConnectSignalR("c2", "mantenimiento", Tablet);

        // El equipo revocado sigue conectado pero ya no bloquea...
        var fromOther = await HostInterlock.EvaluateLoginAsync(db, Remote(Tablet2), isSuperAdmin: true, force: false);
        Assert.True(fromOther.IsAllowed);

        // ...y un login desde él no expulsa a nadie (lo rechaza el mTLS estricto del controlador).
        var fromPanel = await HostInterlock.EvaluateLoginAsync(db, Remote(Panel), isSuperAdmin: false, force: false);
        Assert.True(fromPanel.IsAllowed);
        Assert.Null(fromPanel.Apply("operador"));
        Assert.False(tablet.IsRevoked);
    }

    [Fact]
    public async Task WithoutStrictMtls_InterlockingIsNotEvaluated()
    {
        MtlsState.RequireRegisteredMachine = false;
        await using var db = CreateDb();
        Register(db, Panel, interlocking: true);
        Register(db, Tablet);
        AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "operador", Panel);

        var result = await HostInterlock.EvaluateLoginAsync(db, Remote(Tablet), isSuperAdmin: false, force: false);

        Assert.True(result.IsAllowed);
        Assert.Null(result.Apply("mantenimiento"));
    }

    [Fact]
    public async Task LocalKiosk_UsesTheServerMachineNameRow()
    {
        await using var db = CreateDb();
        db.HostInterlockSettings.Add(new HostInterlockSetting
        {
            HostName = HostInterlock.LocalHostName, Interlocking = true, OverrideInterlocking = true
        });
        Register(db, Panel, interlocking: true);
        AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();
        ConnectSignalR("c1", "operador", Panel);

        var kiosk = new LoginOrigin(HostInterlock.LocalHostName, true, "127.0.0.1");
        var result = await HostInterlock.EvaluateLoginAsync(db, kiosk, isSuperAdmin: false, force: false);

        Assert.Equal(HostInterlockDecision.OverrideAvailable, result.Decision);
    }

    // ───────────────────── Login completo (AuthenticationService) ─────────────────────

    private const string Password = "Clave-Segura-1";

    private AuthenticationService CreateAuthService()
    {
        var dbFactory = new Mock<IProjectDbContextFactory>();
        dbFactory.Setup(f => f.CreateDbContext()).Returns(() => AquafrischDbContextFactory.Create(_dbPath));
        var projectContext = new Mock<IProjectContextService>();
        projectContext.SetupGet(p => p.ExcelConfigPath).Returns("");

        return new AuthenticationService(
            dbFactory.Object,
            new Mock<IAuditLogService>().Object,
            NullLogger<AuthenticationService>.Instance,
            new Mock<IExcelConfigService>().Object,
            projectContext.Object,
            new ConfigurationBuilder().Build());
    }

    private static void AddLoginUser(AquafrischDbContext db, string username, string roleName)
    {
        var user = new User { Username = username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4) };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { User = user, RoleId = db.Roles.Single(r => r.Name == roleName).Id });
    }

    [Fact]
    public async Task Login_BlockedByInterlocking_CreatesNoSession_AndExpelsNobody()
    {
        await using (var db = CreateDb())
        {
            Register(db, Panel, interlocking: true);
            Register(db, Tablet);
            AddLoginUser(db, "mantenimiento", "Maintenance");
            AddSession(db, "operador", Panel);
            await db.SaveChangesAsync();
        }
        ConnectSignalR("c1", "operador", Panel);
        using var auth = CreateAuthService();

        var response = await auth.LoginAsync(
            new LoginRequest { Username = "mantenimiento", Password = Password, ForceInterlock = true },
            "192.168.1.60", "test", null, Remote(Tablet));

        Assert.False(response.Success);
        Assert.Equal(HostInterlock.ErrorBlocked, response.ErrorCode);
        Assert.Equal(Panel, response.InterlockHost);
        Assert.Null(response.Token);

        await using var check = CreateDb();
        var sessions = await check.UserSessions.Include(s => s.User).ToListAsync();
        var panelSession = Assert.Single(sessions);
        Assert.Equal("operador", panelSession.User!.Username);
        Assert.False(panelSession.IsRevoked);
    }

    [Fact]
    public async Task Login_FromOverrideHost_TakesControlOnlyWhenForced()
    {
        await using (var db = CreateDb())
        {
            Register(db, Panel, interlocking: true);
            Register(db, Master, interlocking: true, overrideInterlocking: true);
            AddLoginUser(db, "jefe", "Maintenance");
            AddSession(db, "operador", Panel);
            await db.SaveChangesAsync();
        }
        ConnectSignalR("c1", "operador", Panel);

        using (var auth = CreateAuthService())
        {
            var asked = await auth.LoginAsync(new LoginRequest { Username = "jefe", Password = Password },
                "192.168.1.60", "test", null, Remote(Master));
            Assert.False(asked.Success);
            Assert.Equal(HostInterlock.ErrorOverrideAvailable, asked.ErrorCode);
        }

        using (var auth = CreateAuthService())
        {
            var forced = await auth.LoginAsync(
                new LoginRequest { Username = "jefe", Password = Password, ForceInterlock = true },
                "192.168.1.60", "test", null, Remote(Master));

            Assert.True(forced.Success);
            Assert.NotNull(forced.InterlockTakeover);
            Assert.True(forced.InterlockTakeover!.Forced);
            Assert.Equal(Master, forced.InterlockTakeover.ByHost);
            Assert.Equal(new[] { Panel }, forced.InterlockTakeover.ExpelledHosts);
        }

        await using var check = CreateDb();
        var sessions = await check.UserSessions.Include(s => s.User).ToListAsync();
        var panelSession = sessions.Single(s => s.User!.Username == "operador");
        var masterSession = sessions.Single(s => s.User!.Username == "jefe");
        Assert.True(panelSession.IsRevoked);
        Assert.True(HostInterlock.TryParseRevokedReason(panelSession.RevokedReason, out var byHost, out var byUser));
        Assert.Equal((Master, "jefe"), (byHost, byUser));
        Assert.False(masterSession.IsRevoked);
        Assert.Equal(Master, masterSession.MachineName);
    }

    // ───────────────────── Esquema en BD existentes ─────────────────────

    [Fact]
    public async Task EnsureSchema_UpgradesAnExistingDatabase_AndIsIdempotent()
    {
        await using var db = CreateDb();
        // BD anterior a la función: sin la tabla de niveles ni la columna de equipo en sesiones.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE HostInterlockSettings");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSessions DROP COLUMN MachineName");

        await AquafrischDbContextFactory.EnsureHostInterlockSchemaAsync(db);
        await AquafrischDbContextFactory.EnsureHostInterlockSchemaAsync(db);

        Register(db, Panel, interlocking: true);
        AddSession(db, "operador", Panel);
        await db.SaveChangesAsync();

        Assert.Equal(Panel, await db.UserSessions.Select(s => s.MachineName).SingleAsync());
        Assert.True(await db.HostInterlockSettings.AnyAsync(s => s.HostName == Panel && s.Interlocking));
    }
}
