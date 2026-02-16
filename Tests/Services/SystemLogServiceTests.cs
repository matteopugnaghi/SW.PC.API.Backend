using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests for SystemLogService — ConcurrentQueue circular buffer, capacity 1000.
/// Constructor accepts optional IHubContext (pass null for tests).
/// </summary>
public class SystemLogServiceTests
{
    private readonly SystemLogService _sut = new(hubContext: null);

    private static SystemLogEntry MakeEntry(
        SystemLogLevel level = SystemLogLevel.Warning,
        SystemLogSource source = SystemLogSource.Backend,
        string category = "Test",
        string message = "Test message") => new()
    {
        Level = level,
        Source = source,
        Category = category,
        Message = message
    };

    // ===== Basic Operations =====

    [Fact]
    public void AddEntry_IncrementsCount()
    {
        _sut.AddEntry(MakeEntry());
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void AddEntry_AssignsAutoIncrementId()
    {
        var e1 = MakeEntry();
        var e2 = MakeEntry();
        _sut.AddEntry(e1);
        _sut.AddEntry(e2);

        Assert.True(e1.Id > 0);
        Assert.True(e2.Id > e1.Id);
    }

    [Fact]
    public void AddEntry_AssignsTimestampIfDefault()
    {
        var entry = MakeEntry();
        entry.Timestamp = default;
        _sut.AddEntry(entry);

        Assert.NotEqual(default, entry.Timestamp);
    }

    [Fact]
    public void AddEntry_TruncatesLongMessage()
    {
        var longMsg = new string('X', 600);
        var entry = MakeEntry(message: longMsg);
        _sut.AddEntry(entry);

        Assert.Equal(500, entry.Message.Length);
        Assert.EndsWith("...", entry.Message);
    }

    [Fact]
    public void AddEntry_TruncatesLongException()
    {
        var entry = MakeEntry();
        entry.Exception = new string('E', 400);
        _sut.AddEntry(entry);

        Assert.Equal(300, entry.Exception.Length);
        Assert.EndsWith("...", entry.Exception);
    }

    [Fact]
    public void Capacity_Returns1000()
    {
        Assert.Equal(1000, _sut.Capacity);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _sut.AddEntry(MakeEntry());
        _sut.AddEntry(MakeEntry());
        _sut.Clear();

        Assert.Equal(0, _sut.Count);
    }

    // ===== Circular Buffer Eviction =====

    [Fact]
    public void CircularBuffer_EvictsOldestBeyondCapacity()
    {
        // Add 1010 entries — should keep last 1000
        for (int i = 0; i < 1010; i++)
            _sut.AddEntry(MakeEntry(message: $"msg-{i}"));

        Assert.True(_sut.Count <= 1000);
    }

    // ===== GetEntries Filtering =====

    [Fact]
    public void GetEntries_ReturnsAllWhenNoQuery()
    {
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Warning));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Error));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Critical));

        var entries = _sut.GetEntries();
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void GetEntries_FiltersByExactLevel()
    {
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Warning));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Error));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Critical));

        var query = new SystemLogQuery { ExactLevel = SystemLogLevel.Error };
        var entries = _sut.GetEntries(query);

        Assert.Single(entries);
        Assert.Equal(SystemLogLevel.Error, entries[0].Level);
    }

    [Fact]
    public void GetEntries_FiltersByMinLevel()
    {
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Warning));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Error));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Critical));

        var query = new SystemLogQuery { MinLevel = SystemLogLevel.Error };
        var entries = _sut.GetEntries(query);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.True(e.Level >= SystemLogLevel.Error));
    }

    [Fact]
    public void GetEntries_FiltersBySource()
    {
        _sut.AddEntry(MakeEntry(source: SystemLogSource.Backend));
        _sut.AddEntry(MakeEntry(source: SystemLogSource.Frontend));
        _sut.AddEntry(MakeEntry(source: SystemLogSource.Backend));

        var query = new SystemLogQuery { Source = SystemLogSource.Frontend };
        var entries = _sut.GetEntries(query);

        Assert.Single(entries);
        Assert.Equal(SystemLogSource.Frontend, entries[0].Source);
    }

    [Fact]
    public void GetEntries_FiltersByCategory()
    {
        _sut.AddEntry(MakeEntry(category: "PLC.Connection"));
        _sut.AddEntry(MakeEntry(category: "SignalR.Hub"));
        _sut.AddEntry(MakeEntry(category: "PLC.Variables"));

        var query = new SystemLogQuery { Category = "PLC" };
        var entries = _sut.GetEntries(query);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Contains("PLC", e.Category));
    }

    [Fact]
    public void GetEntries_RespectsQueryTake()
    {
        for (int i = 0; i < 10; i++)
            _sut.AddEntry(MakeEntry());

        var query = new SystemLogQuery { Take = 3 };
        var entries = _sut.GetEntries(query);

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void GetEntries_OrderedByTimestampDescending()
    {
        _sut.AddEntry(MakeEntry(message: "first"));
        Thread.Sleep(10);
        _sut.AddEntry(MakeEntry(message: "second"));

        var entries = _sut.GetEntries();
        Assert.True(entries[0].Timestamp >= entries[1].Timestamp);
    }

    // ===== GetSummary =====

    [Fact]
    public void GetSummary_CountsByLevelAndSource()
    {
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Warning, source: SystemLogSource.Backend));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Error, source: SystemLogSource.Backend));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Critical, source: SystemLogSource.Frontend));
        _sut.AddEntry(MakeEntry(level: SystemLogLevel.Warning, source: SystemLogSource.Frontend));

        var summary = _sut.GetSummary();

        Assert.Equal(4, summary.TotalEntries);
        Assert.Equal(2, summary.WarningCount);
        Assert.Equal(1, summary.ErrorCount);
        Assert.Equal(1, summary.CriticalCount);
        Assert.Equal(2, summary.BackendCount);
        Assert.Equal(2, summary.FrontendCount);
        Assert.Equal(1000, summary.BufferCapacity);
    }

    [Fact]
    public void GetSummary_ReportsOldestAndNewest()
    {
        _sut.AddEntry(MakeEntry());
        Thread.Sleep(10);
        _sut.AddEntry(MakeEntry());

        var summary = _sut.GetSummary();

        Assert.NotNull(summary.OldestEntry);
        Assert.NotNull(summary.NewestEntry);
        Assert.True(summary.NewestEntry >= summary.OldestEntry);
    }

    [Fact]
    public void GetSummary_EmptyBuffer_ReturnsNullDates()
    {
        var summary = _sut.GetSummary();

        Assert.Equal(0, summary.TotalEntries);
        Assert.Null(summary.OldestEntry);
        Assert.Null(summary.NewestEntry);
    }

    // ===== ShouldExcludeCategory =====

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics", true)]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware", true)]
    [InlineData("Microsoft.AspNetCore.SomeNew.Thing", true)]       // Starts with Microsoft.AspNetCore.
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", true)]
    [InlineData("Microsoft.Extensions.Http.SomeHandler", true)]
    [InlineData("SW.PC.API.Backend.Services.TwinCATService", false)]
    [InlineData("MyCustomCategory", false)]
    public void ShouldExcludeCategory_CorrectlyFilters(string category, bool expected)
    {
        Assert.Equal(expected, SystemLogService.ShouldExcludeCategory(category));
    }
}
