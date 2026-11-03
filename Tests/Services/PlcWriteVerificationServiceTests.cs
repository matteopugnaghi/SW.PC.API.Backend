using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// PLC simulado en memoria para tests: Write guarda, Read devuelve lo guardado.
/// Permite forzar rechazos (false) y valores "corruptos" en relectura.
/// </summary>
internal sealed class FakePlc
{
    public readonly Dictionary<string, object> Memory = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> RejectWrites = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> ThrowOnRead = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, object> ReadOverrides = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<(string Var, object Value, Type Type)> Writes = new();

    public Mock<ITwinCATService> Mock { get; }

    public FakePlc()
    {
        Mock = new Mock<ITwinCATService>(MockBehavior.Loose);
        Mock.SetupGet(p => p.IsConnected).Returns(true);
        Mock.Setup(p => p.WriteVariableAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Type>()))
            .Returns<string, object, Type>((v, val, t) =>
            {
                Writes.Add((v, val, t));
                if (RejectWrites.Contains(v)) return Task.FromResult(false);
                Memory[v] = val;
                return Task.FromResult(true);
            });
        Mock.Setup(p => p.ReadVariableAsync(It.IsAny<string>(), It.IsAny<Type>()))
            .Returns<string, Type>((v, t) =>
            {
                if (ThrowOnRead.Contains(v)) throw new InvalidOperationException($"ADS error leyendo {v}");
                if (ReadOverrides.TryGetValue(v, out var o)) return Task.FromResult<object?>(o);
                return Task.FromResult<object?>(Memory.TryGetValue(v, out var m) ? m : null);
            });
    }

    public ITwinCATService Service => Mock.Object;
}

public class PlcValueComparerTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, (byte)0, true)]
    public void Bool(object expected, object actual, bool eq)
        => Assert.Equal(eq, PlcValueComparer.AreEqual(expected, actual, typeof(bool)));

    [Theory]
    [InlineData(1.5, 1.5, true)]
    [InlineData(1.5, 1.5000000001, true)]
    [InlineData(1.5, 1.6, false)]
    [InlineData(1000000.0, 1000000.0005, true)]
    [InlineData(0.0, 0.0, true)]
    public void Double(double expected, double actual, bool eq)
        => Assert.Equal(eq, PlcValueComparer.AreEqual(expected, actual, typeof(double)));

    [Fact]
    public void Double_AcceptsFloatActual()
        => Assert.True(PlcValueComparer.AreEqual(2.0, 2.0f, typeof(double)));

    [Theory]
    [InlineData(7, 7, true)]
    [InlineData(7, (short)7, true)]
    [InlineData((sbyte)3, 3, true)]
    [InlineData(7, 8, false)]
    public void Integers(object expected, object actual, bool eq)
        => Assert.Equal(eq, PlcValueComparer.AreEqual(expected, actual, typeof(int)));

    [Fact]
    public void SByte_FunctionType()
        => Assert.True(PlcValueComparer.AreEqual((sbyte)5, (sbyte)5, typeof(sbyte)));

    [Theory]
    [InlineData("Tren A", "Tren A", true)]
    [InlineData("Tren A", "Tren A\0\0", true)]
    [InlineData("Tren A", " Tren A ", true)]
    [InlineData("Tren A", "Tren B", false)]
    public void Strings(string expected, string actual, bool eq)
        => Assert.Equal(eq, PlcValueComparer.AreEqual(expected, actual, typeof(string)));

    [Fact]
    public void NullActual_IsMismatch()
        => Assert.False(PlcValueComparer.AreEqual(1.0, null, typeof(double)));

    [Fact]
    public void Format_IsInvariant()
    {
        Assert.Equal("1.5", PlcValueComparer.Format(1.5));
        Assert.Equal("true", PlcValueComparer.Format(true));
        Assert.Equal("null", PlcValueComparer.Format(null));
    }
}

public class PlcWriteVerificationServiceTests
{
    private static (PlcWriteVerificationService Svc, Mock<IOperationLogService> OpLog, FakePlc Plc) Create()
    {
        var plc = new FakePlc();
        var opLog = new Mock<IOperationLogService>();
        opLog.Setup(o => o.LogAsync(It.IsAny<OperationCategory>(), It.IsAny<OperationAction>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new OperationLog());
        var svc = new PlcWriteVerificationService(plc.Service, opLog.Object, NullLogger<PlcWriteVerificationService>.Instance);
        return (svc, opLog, plc);
    }

    [Fact]
    public async Task Batch_WriteAsync_ThrowsWhenDriverReturnsFalse_AndRecordsFailure()
    {
        var (svc, _, plc) = Create();
        plc.RejectWrites.Add("MAIN.x");
        var batch = svc.CreateBatch("test");

        var ex = await Assert.ThrowsAsync<PlcWriteException>(() => batch.WriteAsync("MAIN.x", 1.0, typeof(double)));

        Assert.Equal("MAIN.x", ex.Variable);
        Assert.Empty(batch.Records);
        Assert.Single(batch.Failures);
    }

    [Fact]
    public async Task Batch_WriteAsync_RecordsOnSuccess()
    {
        var (svc, _, plc) = Create();
        var batch = svc.CreateBatch("test");

        await batch.WriteAsync("MAIN.x", 1.0, typeof(double));
        await batch.WriteAsync("MAIN.b", true, typeof(bool));

        Assert.Equal(2, batch.Records.Count);
        Assert.Empty(batch.Failures);
        Assert.Equal(1.0, plc.Memory["MAIN.x"]);
    }

    [Fact]
    public async Task Verify_AllMatch_Success_NoOperationLog()
    {
        var (svc, opLog, _) = Create();
        var batch = svc.CreateBatch("ctx");
        await batch.WriteAsync("MAIN.x", 1.0, typeof(double));
        await batch.WriteAsync("MAIN.s", "Tren A", typeof(string));

        var result = await svc.VerifyAsync(batch, OperationCategory.Recipe, "user");

        Assert.True(result.Success);
        Assert.Equal(2, result.Verified);
        Assert.Empty(result.Mismatches);
        opLog.Verify(o => o.LogAsync(It.IsAny<OperationCategory>(), It.IsAny<OperationAction>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Mismatch_LogsRetentiveL2_AndReportsVariable()
    {
        var (svc, opLog, plc) = Create();
        var batch = svc.CreateBatch("Tipo de tren 'A' → PLC");
        await batch.WriteAsync("MAIN.fbMachine.st_TrainRecipe[1].r_TypeOfwash_012", 1.0, typeof(double));
        // El PLC "conserva" otro valor (lo que vio el técnico en campo)
        plc.ReadOverrides["MAIN.fbMachine.st_TrainRecipe[1].r_TypeOfwash_012"] = 2.0;

        var result = await svc.VerifyAsync(batch, OperationCategory.Recipe, "tecnico");

        Assert.False(result.Success);
        var m = Assert.Single(result.Mismatches);
        Assert.Equal("1", m.Expected);
        Assert.Equal("2", m.Actual);
        Assert.Contains("r_TypeOfwash_012", result.ToErrorStrings()[0]);

        opLog.Verify(o => o.LogAsync(OperationCategory.Recipe, OperationAction.PlcWriteVerifyFailed,
            It.Is<string>(d => d.Contains("Tipo de tren 'A'")), "tecnico",
            It.Is<Dictionary<string, object>?>(d => d != null && d.ContainsKey("mismatches")),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Verify_ReadError_IsReported()
    {
        var (svc, _, plc) = Create();
        var batch = svc.CreateBatch("ctx");
        await batch.WriteAsync("MAIN.x", 1.0, typeof(double));
        plc.ThrowOnRead.Add("MAIN.x");

        var result = await svc.VerifyAsync(batch, OperationCategory.Configuration, null);

        Assert.False(result.Success);
        Assert.Single(result.ReadErrors);
        Assert.Equal(0, result.Verified);
    }

    [Fact]
    public async Task Verify_WriteFailureInBatch_MakesResultFail()
    {
        var (svc, _, plc) = Create();
        plc.RejectWrites.Add("MAIN.bad");
        var batch = svc.CreateBatch("ctx");
        await batch.WriteAsync("MAIN.ok", 1, typeof(int));
        await Assert.ThrowsAsync<PlcWriteException>(() => batch.WriteAsync("MAIN.bad", 1, typeof(int)));

        var result = await svc.VerifyAsync(batch, OperationCategory.Recipe, null);

        Assert.False(result.Success);
        Assert.Equal(1, result.Verified);
        Assert.Single(result.WriteFailures);
    }

    [Fact]
    public async Task IntegrityWarnings_Empty_DoesNothing()
    {
        var (svc, opLog, _) = Create();
        await svc.LogIntegrityWarningsAsync(OperationCategory.Recipe, "ctx", Array.Empty<string>(), null);
        opLog.Verify(o => o.LogAsync(It.IsAny<OperationCategory>(), It.IsAny<OperationAction>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task IntegrityWarnings_LogsL2WithWarningAction()
    {
        var (svc, opLog, _) = Create();
        await svc.LogIntegrityWarningsAsync(OperationCategory.Recipe, "Slot 3", new[] { "falta X", "huérfano Y" }, "u");
        opLog.Verify(o => o.LogAsync(OperationCategory.Recipe, OperationAction.IntegrityWarning,
            It.Is<string>(d => d.Contains("Slot 3") && d.Contains("2 aviso")), "u",
            It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void NewOperationActions_HaveNonInfoSeverity()
    {
        Assert.Equal(OperationSeverity.Error, OperationLog.GetSeverityFromAction(OperationAction.PlcWriteVerifyFailed));
        Assert.Equal(OperationSeverity.Warning, OperationLog.GetSeverityFromAction(OperationAction.IntegrityWarning));
    }
}
