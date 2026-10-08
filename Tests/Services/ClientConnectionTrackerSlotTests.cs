using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Asignación de slots [0..5] para los arrays paralelos del PLC
/// (UserLogged / ClientsIdConnected / CurrentScreen / ClientsHostName):
///  • [0] reservado al cliente local (loopback); vacío si no hay kiosco.
///  • Remotos: menor índice libre en [1..5], estable mientras vivan o estén en gracia.
///  • Gracia de reconexión: el slot y la pantalla se conservan ReconnectGraceSeconds (20 s)
///    y la nueva conexión del mismo usuario@IP hereda la pantalla.
/// El estado es estático → los tests de esta clase se ejecutan en serie (xUnit por clase).
/// </summary>
[Collection("ClientConnectionTracker")]
public class ClientConnectionTrackerSlotTests : IDisposable
{
    private static readonly (string, string, string, string) Empty = ("", "", "", "");
    private DateTime _now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    public ClientConnectionTrackerSlotTests()
    {
        ClientConnectionTrackerService.ResetStateForTesting();
        ClientConnectionTrackerService.UtcNowProvider = () => _now;
    }

    public void Dispose()
    {
        ClientConnectionTrackerService.ResetStateForTesting();
        ClientConnectionTrackerService.UtcNowProvider = () => DateTime.UtcNow;
    }

    private static void Connect(string connId, string user, string ip, string screen = "", string host = "")
    {
        lock (ClientConnectionTrackerService.LockObj)
        {
            var inherited = ClientConnectionTrackerService.TakeInheritedScreen(user, ip);
            ClientConnectionTrackerService.ConnectedClients[connId] =
                (user, ip, string.IsNullOrEmpty(inherited) ? screen : inherited, host);
            ClientConnectionTrackerService.ActiveConnections++;
        }
    }

    private static void Disconnect(string connId)
    {
        lock (ClientConnectionTrackerService.LockObj)
        {
            if (ClientConnectionTrackerService.ConnectedClients.TryGetValue(connId, out var info))
            {
                ClientConnectionTrackerService.ConnectedClients.Remove(connId);
                ClientConnectionTrackerService.RememberDisconnectedClient(info);
                ClientConnectionTrackerService.ActiveConnections--;
            }
        }
    }

    private static (string Username, string IPAddress, string CurrentScreen, string HostName)[] Snapshot()
    {
        lock (ClientConnectionTrackerService.LockObj)
        {
            return ClientConnectionTrackerService.GetPlcClientsSnapshot();
        }
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("127.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    public void LocalClient_AlwaysSlot0_EvenIfRemoteConnectedFirst(string loopback)
    {
        Connect("r1", "matteo", "192.168.2.50", "alarmas");
        Connect("k1", "operador", loopback, "manual");

        var s = Snapshot();

        Assert.Equal("operador", s[0].Username);
        Assert.Equal("manual", s[0].CurrentScreen);
        Assert.Equal("matteo", s[1].Username);
    }

    [Fact]
    public void NoLocalClient_Slot0_StaysEmpty()
    {
        Connect("r1", "matteo", "192.168.2.50", "alarmas");
        Connect("r2", "tecnico", "192.168.2.51", "manual");

        var s = Snapshot();

        Assert.Equal(Empty, s[0]);
        Assert.Equal("matteo", s[1].Username);
        Assert.Equal("tecnico", s[2].Username);
    }

    [Fact]
    public void RemoteSlots_AreStable_WhenAnotherRemoteDisconnects()
    {
        Connect("k1", "operador", "::1", "principal");
        Connect("r1", "a", "10.0.0.1", "alarmas");
        Connect("r2", "b", "10.0.0.2", "manual");
        Connect("r3", "c", "10.0.0.3", "recetas");
        Assert.Equal("b", Snapshot()[2].Username);

        Disconnect("r1");
        _now = _now.AddSeconds(30); // a expira de la gracia

        var s = Snapshot();

        Assert.Equal(Empty, s[1]);                 // hueco liberado, nadie se desplaza
        Assert.Equal("b", s[2].Username);
        Assert.Equal("manual", s[2].CurrentScreen);
        Assert.Equal("c", s[3].Username);

        Connect("r4", "d", "10.0.0.4");
        Assert.Equal("d", Snapshot()[1].Username); // el nuevo ocupa el menor hueco libre
        Assert.Equal("b", Snapshot()[2].Username);
    }

    [Fact]
    public void Grace_KeepsSlotAndScreen_ForRemoteReconnect_AnyIndex()
    {
        Connect("k1", "operador", "::1", "principal");
        Connect("r1", "a", "10.0.0.1", "alarmas");
        Connect("r2", "tecnico", "10.0.0.2", "manual");
        Assert.Equal("tecnico", Snapshot()[2].Username);

        Disconnect("r2");
        _now = _now.AddSeconds(5);

        var during = Snapshot();
        Assert.Equal("tecnico", during[2].Username);  // sigue en [2] durante la gracia
        Assert.Equal("manual", during[2].CurrentScreen);

        Connect("r2b", "tecnico", "10.0.0.2");         // reconecta sin pantalla todavía
        var after = Snapshot();
        Assert.Equal("tecnico", after[2].Username);    // mismo índice
        Assert.Equal("manual", after[2].CurrentScreen); // pantalla heredada → el PLC nunca ve ""
    }

    [Fact]
    public void Grace_KeepsSlot0AndScreen_ForLocalReconnect()
    {
        Connect("k1", "operador", "::1", "manual");
        Connect("r1", "a", "10.0.0.1", "alarmas");

        Disconnect("k1");
        _now = _now.AddSeconds(3);

        var during = Snapshot();
        Assert.Equal("operador", during[0].Username);
        Assert.Equal("manual", during[0].CurrentScreen);
        Assert.Equal("a", during[1].Username);

        Connect("k2", "operador", "::1");
        var after = Snapshot();
        Assert.Equal("operador", after[0].Username);
        Assert.Equal("manual", after[0].CurrentScreen);
    }

    [Fact]
    public void Grace_Expires_AfterTimeout_SlotCleared()
    {
        Connect("k1", "operador", "::1", "manual");
        Disconnect("k1");

        _now = _now.AddSeconds(19);
        Assert.Equal("operador", Snapshot()[0].Username);

        _now = _now.AddSeconds(2); // 21 s > 20 s
        Assert.Equal(Empty, Snapshot()[0]);
    }

    [Fact]
    public void LocalReLoginWithDifferentUser_TakesSlot0_PreviousLocalInGraceIsDropped()
    {
        Connect("k1", "operador", "::1", "manual");
        Disconnect("k1");
        Connect("k2", "jefe", "::1", "principal");

        var s = Snapshot();

        Assert.Equal("jefe", s[0].Username);
        Assert.DoesNotContain(s, x => x.Username == "operador");
    }

    [Fact]
    public void DuplicateConnectionsSameUser_SingleSlot()
    {
        Connect("k1", "operador", "::1", "manual");   // conexión zombi aún no detectada
        Connect("k2", "operador", "::1", "manual");   // nueva conexión

        var s = Snapshot();

        Assert.Equal("operador", s[0].Username);
        Assert.Equal(1, s.Count(x => x.Username == "operador"));
    }

    [Fact]
    public void MoreThanFiveRemotes_ExtraNotRepresented_NoShift()
    {
        for (int i = 1; i <= 6; i++)
            Connect($"r{i}", $"u{i}", $"10.0.0.{i}");

        var s = Snapshot();

        Assert.Equal(Empty, s[0]);
        for (int i = 1; i <= 5; i++) Assert.Equal($"u{i}", s[i].Username);
        Assert.DoesNotContain(s, x => x.Username == "u6");
    }
}
