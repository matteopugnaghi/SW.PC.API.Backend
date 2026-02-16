using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests for MetricsService — parameterless constructor, pure in-memory state tracking.
/// Circular buffer of 100 samples, thread-safe via lock.
/// </summary>
public class MetricsServiceTests
{
    private readonly MetricsService _sut = new();

    [Fact]
    public void GetCurrentMetrics_ReturnsValidDefaults()
    {
        var metrics = _sut.GetCurrentMetrics();

        Assert.NotNull(metrics);
        Assert.NotNull(metrics.ServerUptime);
        Assert.Equal(0, metrics.PlcPollingScanTime);
        Assert.Equal(0, metrics.PlcPollingAvgScanTime);
        Assert.Equal(0, metrics.PlcMonitoredVariables);
        Assert.Equal(0, metrics.SignalRActiveConnections);
    }

    [Fact]
    public void RecordPlcPollingScanTime_TracksLastAndAverage()
    {
        _sut.RecordPlcPollingScanTime(10.0);
        _sut.RecordPlcPollingScanTime(20.0);
        _sut.RecordPlcPollingScanTime(30.0);

        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(20.0, metrics.PlcPollingAvgScanTime, 1);
        Assert.Equal(30.0, metrics.PlcPollingScanTime, 1);
    }

    [Fact]
    public void SetPlcMonitoredVariables_ReflectsInMetrics()
    {
        _sut.SetPlcMonitoredVariables(42);
        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(42, metrics.PlcMonitoredVariables);
    }

    [Fact]
    public void RecordSignalRBroadcastTime_TracksLastAndAverage()
    {
        _sut.RecordSignalRBroadcastTime(5.0);
        _sut.RecordSignalRBroadcastTime(15.0);

        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(10.0, metrics.SignalRAvgBroadcastTime, 1);
        Assert.Equal(15.0, metrics.SignalRLastBroadcastTime, 1);
    }

    [Fact]
    public void SetSignalRActiveConnections_ReflectsInMetrics()
    {
        _sut.SetSignalRActiveConnections(7);
        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(7, metrics.SignalRActiveConnections);
    }

    [Fact]
    public void RecordExcelLoadTime_ReflectsInMetrics()
    {
        _sut.RecordExcelLoadTime(1500.0);
        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(1500.0, metrics.ExcelLastLoadTime, 1);
    }

    [Fact]
    public void SetPlcPollingStatus_UpdatesServicesStatus()
    {
        _sut.SetPlcPollingStatus(true, true, "Connected to PLC", true);
        var metrics = _sut.GetCurrentMetrics();

        Assert.True(metrics.ServicesStatus.PlcPollingEnabled);
        Assert.True(metrics.ServicesStatus.PlcPollingConnected);
        Assert.True(metrics.ServicesStatus.PlcIsSimulated);
        Assert.Equal("Connected to PLC", metrics.ServicesStatus.PlcPollingStatus);
    }

    [Fact]
    public void SetSignalRStatus_UpdatesServicesStatus()
    {
        _sut.SetSignalRStatus(true, false, "Disconnected");
        var metrics = _sut.GetCurrentMetrics();

        Assert.True(metrics.ServicesStatus.SignalREnabled);
        Assert.False(metrics.ServicesStatus.SignalRConnected);
        Assert.Equal("Disconnected", metrics.ServicesStatus.SignalRStatus);
    }

    [Fact]
    public void SetDatabaseStatus_UpdatesServicesStatus()
    {
        _sut.SetDatabaseStatus(true, true, "SQLite OK");
        var metrics = _sut.GetCurrentMetrics();

        Assert.True(metrics.ServicesStatus.DatabaseEnabled);
        Assert.True(metrics.ServicesStatus.DatabaseConnected);
        Assert.Equal("SQLite OK", metrics.ServicesStatus.DatabaseStatus);
    }

    [Fact]
    public void CircularBuffer_CapsAt100Samples()
    {
        // Record 150 samples — buffer should only keep last 100
        for (int i = 0; i < 150; i++)
            _sut.RecordPlcPollingScanTime(i);

        var metrics = _sut.GetCurrentMetrics();
        // Average of 50..149 = 99.5
        Assert.Equal(99.5, metrics.PlcPollingAvgScanTime, 0);
        Assert.Equal(149.0, metrics.PlcPollingScanTime, 1);
    }

    [Fact]
    public void SetUseSimulatedPlc_ReflectsInMetrics()
    {
        _sut.SetUseSimulatedPlc(true);
        var metrics = _sut.GetCurrentMetrics();
        Assert.True(metrics.ServicesStatus.UseSimulatedPlc);
    }

    [Fact]
    public void SetAlarmNotificationStatus_UpdatesServicesStatus()
    {
        _sut.SetAlarmNotificationStatus(true, true, "Active monitoring");
        var metrics = _sut.GetCurrentMetrics();

        Assert.True(metrics.ServicesStatus.AlarmNotificationEnabled);
        Assert.True(metrics.ServicesStatus.AlarmNotificationActive);
        Assert.Equal("Active monitoring", metrics.ServicesStatus.AlarmNotificationStatus);
    }

    [Fact]
    public void ServerUptime_FormatsCorrectly()
    {
        // Just verify it's a non-empty formatted string
        var metrics = _sut.GetCurrentMetrics();
        Assert.False(string.IsNullOrWhiteSpace(metrics.ServerUptime));
        Assert.Contains(":", metrics.ServerUptime);
    }

    [Fact]
    public void ValuesAreRoundedToTwoDecimals()
    {
        _sut.RecordPlcPollingScanTime(1.23456789);
        var metrics = _sut.GetCurrentMetrics();
        Assert.Equal(1.23, metrics.PlcPollingScanTime);
    }
}
