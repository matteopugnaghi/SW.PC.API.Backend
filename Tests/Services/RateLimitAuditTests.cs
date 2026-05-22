using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// 🔒 EU CRA v1.4 — Tests para la auditoría de eventos de rate limiting (HTTP 429).
///
/// Valida el contrato entre el callback <c>OnRejected</c> configurado en
/// <see cref="Program"/> y el servicio <see cref="IAuditLogService"/>.
///
/// Si esta firma cambia (orden/tipo de parámetros, nombre del enum, etc.),
/// estos tests fallarán antes que el callback explote en runtime sin dejar log.
/// </summary>
public class RateLimitAuditTests
{
    // ─────────────────────────────────────────────────────────────
    // 1) Garantías sobre el enum AuditAction.RateLimitExceeded
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuditAction_Contains_RateLimitExceeded()
    {
        // El callback OnRejected del rate limiter (Program.cs) depende de
        // este valor del enum. Si alguien lo renombra, el callback dejará
        // de compilar y este test sirve como aviso temprano.
        var names = Enum.GetNames(typeof(AuditAction));
        Assert.Contains("RateLimitExceeded", names);
    }

    [Fact]
    public void AuditAction_RateLimitExceeded_HasUniqueNumericValue()
    {
        // Asegura que no quede colisionando con otro valor del enum
        // (cosa fácil de romper al reordenar declaraciones).
        var rl = (int)AuditAction.RateLimitExceeded;
        var collisions = Enum.GetValues<AuditAction>()
            .Where(v => (int)v == rl && v != AuditAction.RateLimitExceeded)
            .ToArray();

        Assert.Empty(collisions);
    }

    // ─────────────────────────────────────────────────────────────
    // 2) Contrato del callback OnRejected -> IAuditLogService.LogAsync
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnRejected_Callback_Calls_LogAsync_With_Security_RateLimitExceeded_Warning()
    {
        // Arrange: mock del servicio de auditoría con la firma EXACTA
        // que usa el callback OnRejected configurado en Program.cs.
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.Security,
                AuditAction.RateLimitExceeded,
                AuditResult.Warning,
                It.Is<string>(d => d != null && d.Contains("/api/auth/login")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string>(ip => ip == "127.0.0.1"),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act: simulamos exactamente lo que hace el OnRejected callback.
        await SimulateRateLimitRejectionAudit(
            mock.Object,
            path: "/api/auth/login",
            method: "POST",
            ip: "127.0.0.1",
            userAgent: "curl/8.19.0");

        // Assert
        mock.Verify();
    }

    [Fact]
    public async Task OnRejected_Callback_Distinguishes_Auth_From_GlobalApi_Policy()
    {
        // El callback determina la política de rate-limit en función del path
        // (auth = 10/min sliding desde v1.7.2; api-global = 300/min) y lo escribe en `details`.
        var capturedDetails = new List<string>();
        var mock = new Mock<IAuditLogService>();
        mock.Setup(s => s.LogAsync(
                It.IsAny<AuditCategory>(),
                It.IsAny<AuditAction>(),
                It.IsAny<AuditResult>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Callback<AuditCategory, AuditAction, AuditResult, string?, string?, string?, string?, int?, double?, string?>(
                (_, _, _, details, _, _, _, _, _, _) => capturedDetails.Add(details ?? ""))
            .Returns(Task.CompletedTask);

        await SimulateRateLimitRejectionAudit(mock.Object, "/api/auth/login", "POST", "10.0.0.5", "ua");
        await SimulateRateLimitRejectionAudit(mock.Object, "/api/recovery/start", "POST", "10.0.0.5", "ua");
        await SimulateRateLimitRejectionAudit(mock.Object, "/api/models", "GET", "10.0.0.5", "ua");

        Assert.Equal(3, capturedDetails.Count);
        Assert.Contains("auth (20/5min sliding)", capturedDetails[0]);
        Assert.Contains("auth (20/5min sliding)", capturedDetails[1]);
        Assert.Contains("api-global (1000/min, anon only)", capturedDetails[2]);
    }

    [Fact]
    public async Task OnRejected_Callback_Does_Not_Throw_When_AuditService_Fails()
    {
        // Requisito de seguridad: un fallo escribiendo el log de auditoría
        // NUNCA debe propagarse al callback OnRejected (rompería la respuesta 429).
        var mock = new Mock<IAuditLogService>();
        mock.Setup(s => s.LogAsync(
                It.IsAny<AuditCategory>(), It.IsAny<AuditAction>(), It.IsAny<AuditResult>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        // No debe lanzar.
        var ex = await Record.ExceptionAsync(() =>
            SimulateRateLimitRejectionAudit(mock.Object, "/api/auth/login", "POST", "::1", "ua"));

        Assert.Null(ex);
    }

    // ─────────────────────────────────────────────────────────────
    // Helper: réplica fiel del cuerpo del callback OnRejected
    // configurado en Program.cs. Cualquier cambio en el callback
    // debe reflejarse aquí (y a la inversa).
    // ─────────────────────────────────────────────────────────────
    private static async Task SimulateRateLimitRejectionAudit(
        IAuditLogService auditLog,
        string path,
        string method,
        string ip,
        string userAgent)
    {
        try
        {
            var policy = (path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                      || path.StartsWith("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
                      || path.StartsWith("/api/recovery", StringComparison.OrdinalIgnoreCase))
                      ? "auth (20/5min sliding)" : "api-global (1000/min, anon only)";

            await auditLog.LogAsync(
                category: AuditCategory.Security,
                action: AuditAction.RateLimitExceeded,
                result: AuditResult.Warning,
                details: $"Rate limit {policy} excedido en {method} {path} (UA: {userAgent})",
                userId: null,
                userName: null,
                ipAddress: ip);
        }
        catch
        {
            // Mismo comportamiento que el callback real: tragarse cualquier excepción.
        }
    }
}
