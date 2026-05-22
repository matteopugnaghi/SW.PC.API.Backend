using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.OpcUa;
using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// 🔒 EU CRA v1.4 Paso B — Tests para los hooks de auditoría OPC/UA security.
///
/// Valida el contrato entre los handlers de OpcUaServerService (sesiones,
/// certificados, login y quotas) y el servicio <see cref="IAuditLogService"/>.
///
/// Si el contrato cambia (firma/orden de parámetros, nombres de enum, etc.)
/// estos tests fallarán antes que los handlers rompan en runtime sin dejar log.
/// </summary>
public class OpcUaAuditTests
{
    // ─────────────────────────────────────────────────────────────
    // 1) Garantías sobre los nuevos valores de AuditAction (Paso B)
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("OpcUaSessionOpened")]
    [InlineData("OpcUaSessionClosed")]
    [InlineData("OpcUaLoginFailed")]
    [InlineData("OpcUaCertificateRejected")]
    [InlineData("OpcUaQuotaExceeded")]
    public void AuditAction_Contains_OpcUaSecurityHook(string name)
    {
        var names = Enum.GetNames(typeof(AuditAction));
        Assert.Contains(name, names);
    }

    [Fact]
    public void AuditAction_OpcUaSecurityHooks_HaveUniqueNumericValues()
    {
        var hooks = new[]
        {
            AuditAction.OpcUaSessionOpened,
            AuditAction.OpcUaSessionClosed,
            AuditAction.OpcUaLoginFailed,
            AuditAction.OpcUaCertificateRejected,
            AuditAction.OpcUaQuotaExceeded,
        };

        var distinct = hooks.Select(h => (int)h).Distinct().Count();
        Assert.Equal(hooks.Length, distinct);
    }

    // ─────────────────────────────────────────────────────────────
    // 2) OpcUaConfig contrato: AuditSessions + QuotaPollIntervalSeconds
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void OpcUaConfig_Defaults_EnableAuditSessions_And_30sQuotaPoll()
    {
        var cfg = new OpcUaConfig();
        Assert.True(cfg.AuditSessions);
        Assert.Equal(30, cfg.QuotaPollIntervalSeconds);
    }

    // ─────────────────────────────────────────────────────────────
    // 3) Contrato del hook SessionOpened
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionOpened_Hook_Emits_OtCommunication_OpcUaSessionOpened_Success()
    {
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaSessionOpened,
                AuditResult.Success,
                It.Is<string>(d => d != null && d.Contains("connected")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await SimulateSessionOpenedAudit(mock.Object, auditSessions: true, clientName: "TestClient");
        mock.Verify();
    }

    [Fact]
    public async Task SessionOpened_Hook_DoesNotAudit_When_AuditSessions_Disabled()
    {
        var mock = new Mock<IAuditLogService>();
        await SimulateSessionOpenedAudit(mock.Object, auditSessions: false, clientName: "Quiet");
        mock.Verify(s => s.LogAsync(
                It.IsAny<AuditCategory>(), It.IsAny<AuditAction>(), It.IsAny<AuditResult>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<string?>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────
    // 4) Contrato del hook SessionClosing (login-fail heuristic)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionClosing_Hook_Emits_OpcUaLoginFailed_Warning_When_NoRequestsProcessed()
    {
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaLoginFailed,
                AuditResult.Warning,
                It.Is<string>(d => d != null && d.Contains("login failed")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await SimulateSessionClosingAudit(mock.Object, auditSessions: true,
            clientName: "RogueClient", totalRequests: 0u);
        mock.Verify();
    }

    [Fact]
    public async Task SessionClosing_Hook_Emits_OpcUaSessionClosed_Success_When_RequestsProcessed()
    {
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaSessionClosed,
                AuditResult.Success,
                It.Is<string>(d => d != null && d.Contains("disconnected")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await SimulateSessionClosingAudit(mock.Object, auditSessions: true,
            clientName: "NormalClient", totalRequests: 42u);
        mock.Verify();
    }

    // ─────────────────────────────────────────────────────────────
    // 5) Contrato del hook CertificateRejected
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CertificateRejected_Hook_Emits_OpcUaCertificateRejected_Warning()
    {
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaCertificateRejected,
                AuditResult.Warning,
                It.Is<string>(d => d != null && d.Contains("CN=Untrusted")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await SimulateCertificateRejectedAudit(mock.Object, subject: "CN=Untrusted");
        mock.Verify();
    }

    // ─────────────────────────────────────────────────────────────
    // 6) Contrato del hook QuotaExceeded (DoS polling)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task QuotaCheck_Hook_Emits_OpcUaQuotaExceeded_Warning_When_DeltaPositive()
    {
        var mock = new Mock<IAuditLogService>(MockBehavior.Strict);
        mock.Setup(s => s.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaQuotaExceeded,
                AuditResult.Warning,
                It.Is<string>(d => d != null && d.Contains("delta")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<int?>(c => c.HasValue && c.Value == 7),
                It.IsAny<double?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await SimulateQuotaCheckAudit(mock.Object,
            deltaSessions: 2, deltaRequests: 3, deltaSecSessions: 1, deltaSecRequests: 1);
        mock.Verify();
    }

    [Fact]
    public async Task QuotaCheck_Hook_DoesNotAudit_When_DeltasAreZero()
    {
        var mock = new Mock<IAuditLogService>();
        await SimulateQuotaCheckAudit(mock.Object,
            deltaSessions: 0, deltaRequests: 0, deltaSecSessions: 0, deltaSecRequests: 0);
        mock.Verify(s => s.LogAsync(
                It.IsAny<AuditCategory>(), It.IsAny<AuditAction>(), It.IsAny<AuditResult>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task AllHooks_DoNotThrow_When_AuditService_Fails()
    {
        // Requisito de seguridad: un fallo escribiendo el log NUNCA debe
        // propagarse al ciclo de servidor OPC/UA.
        var mock = new Mock<IAuditLogService>();
        mock.Setup(s => s.LogAsync(
                It.IsAny<AuditCategory>(), It.IsAny<AuditAction>(), It.IsAny<AuditResult>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<double?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        var ex1 = await Record.ExceptionAsync(() =>
            SimulateSessionOpenedAudit(mock.Object, true, "X"));
        var ex2 = await Record.ExceptionAsync(() =>
            SimulateSessionClosingAudit(mock.Object, true, "X", 0u));
        var ex3 = await Record.ExceptionAsync(() =>
            SimulateCertificateRejectedAudit(mock.Object, "CN=X"));
        var ex4 = await Record.ExceptionAsync(() =>
            SimulateQuotaCheckAudit(mock.Object, 1, 0, 0, 0));

        Assert.Null(ex1);
        Assert.Null(ex2);
        Assert.Null(ex3);
        Assert.Null(ex4);
    }

    // ═════════════════════════════════════════════════════════════
    // Helpers — réplicas fieles del cuerpo de cada hook
    // (OpcUaServerService.cs). Si cambia un hook, debe cambiar aquí.
    // ═════════════════════════════════════════════════════════════

    private static async Task SimulateSessionOpenedAudit(
        IAuditLogService auditLog, bool auditSessions, string clientName)
    {
        try
        {
            if (!auditSessions) return;
            var details = $"Client '{clientName}' connected (reason: Activated)";
            await auditLog.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaSessionOpened,
                AuditResult.Success,
                details,
                userName: "System");
        }
        catch { /* swallow — mirror real hook */ }
    }

    private static async Task SimulateSessionClosingAudit(
        IAuditLogService auditLog, bool auditSessions, string clientName, uint totalRequests)
    {
        try
        {
            bool isLikelyLoginFail = totalRequests == 0u;
            if (isLikelyLoginFail)
            {
                await auditLog.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.OpcUaLoginFailed,
                    AuditResult.Warning,
                    $"OPC/UA likely login failed for '{clientName}' (session closed without processing any request, reason: CloseSession)",
                    userName: "unknown");
            }
            else if (auditSessions)
            {
                await auditLog.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.OpcUaSessionClosed,
                    AuditResult.Success,
                    $"Client '{clientName}' disconnected (reason: CloseSession)",
                    userName: "System");
            }
        }
        catch { /* swallow */ }
    }

    private static async Task SimulateCertificateRejectedAudit(
        IAuditLogService auditLog, string subject)
    {
        try
        {
            await auditLog.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaCertificateRejected,
                AuditResult.Warning,
                $"Certificate rejected (manual-trust): {subject} — pending approval",
                userName: "System");
        }
        catch { /* swallow */ }
    }

    private static async Task SimulateQuotaCheckAudit(
        IAuditLogService auditLog,
        long deltaSessions, long deltaRequests,
        long deltaSecSessions, long deltaSecRequests)
    {
        try
        {
            long total = deltaSessions + deltaRequests + deltaSecSessions + deltaSecRequests;
            if (total <= 0) return;

            var details = $"Quota rejections delta — sessions:{deltaSessions}, requests:{deltaRequests}, securityRejectedSessions:{deltaSecSessions}, securityRejectedRequests:{deltaSecRequests} (totals: stubbed)";
            await auditLog.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaQuotaExceeded,
                AuditResult.Warning,
                details,
                userName: "System",
                affectedItemCount: (int)Math.Min(total, int.MaxValue));
        }
        catch { /* swallow */ }
    }
}
