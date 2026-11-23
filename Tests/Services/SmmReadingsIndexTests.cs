using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Services.Smm;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Incidente de campo: Estadísticas tardaba segundos por petición y saturaba el backend.
/// readings/recent y la purga de retención recorrían TODAS las lecturas del grupo (más de
/// 1 M de filas con lectura cada 1 s y 1 día de retención) porque ningún índice ordenaba
/// los snapshots de un grupo por fecha. El plan de ambas consultas debe apoyarse en
/// IX_SMM_Readings_Group_Cycle_Time y no ordenar ni deduplicar en un B-tree temporal.
/// </summary>
public class SmmReadingsIndexTests : IDisposable
{
    private const string Index = "IX_SMM_Readings_Group_Cycle_Time";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"smm-index-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* fichero temporal */ }
    }

    [Fact]
    public async Task Recent_snapshot_timestamps_of_a_group_are_read_from_the_index_in_order()
    {
        await using var db = AquafrischDbContextFactory.Create(_dbPath);
        await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

        var query = SmmReadingQueries.RecentSnapshotTimestamps(
            db.SmmReadings, groupId: 11, take: 1, from: new DateTime(2026, 1, 1), to: new DateTime(2026, 12, 31));

        await db.Database.OpenConnectionAsync();
        await using var command = query.CreateDbCommand();
        var plan = await ExplainAsync(command);

        Assert.Contains(Index, plan);
        Assert.DoesNotContain("TEMP B-TREE", plan);
    }

    [Fact]
    public async Task Recent_snapshot_timestamps_are_distinct_newest_first_and_only_for_the_group_snapshots()
    {
        await using var db = AquafrischDbContextFactory.Create(_dbPath);
        await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO SMM_Readings (GroupId, VariableId, CycleId, Timestamp, Value, Source, IsError) VALUES
                (11, 1, NULL, '2026-11-23 10:00:01', 1, 'Plc', 0), (11, 2, NULL, '2026-11-23 10:00:01', 2, 'Plc', 0),
                (11, 1, NULL, '2026-11-23 10:00:02', 1, 'Plc', 0), (11, 2, NULL, '2026-11-23 10:00:02', 2, 'Plc', 0),
                (11, 1, NULL, '2026-11-23 10:00:03', 1, 'Plc', 0), (11, 2, NULL, '2026-11-23 10:00:03', 2, 'Plc', 0),
                (11, 1, 7,    '2026-11-23 10:00:09', 1, 'Plc', 0),
                (12, 3, NULL, '2026-11-23 10:00:08', 1, 'Plc', 0)");

        var newestTwo = await SmmReadingQueries.RecentSnapshotTimestamps(db.SmmReadings, 11, take: 2).ToListAsync();
        var untilSecondTwo = await SmmReadingQueries
            .RecentSnapshotTimestamps(db.SmmReadings, 11, take: 10, to: new DateTime(2026, 11, 23, 10, 0, 2))
            .ToListAsync();

        Assert.Equal(new[] { new DateTime(2026, 11, 23, 10, 0, 3), new DateTime(2026, 11, 23, 10, 0, 2) }, newestTwo);
        Assert.Equal(new[] { new DateTime(2026, 11, 23, 10, 0, 2), new DateTime(2026, 11, 23, 10, 0, 1) }, untilSecondTwo);
    }

    [Fact]
    public async Task Retention_purge_of_a_group_seeks_the_cutoff_in_the_index()
    {
        await using var db = AquafrischDbContextFactory.Create(_dbPath);
        await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        // Misma sentencia que SmmCaptureService.PurgeOldContinuousGroupAsync
        command.CommandText = "DELETE FROM SMM_Readings WHERE GroupId = 11 AND CycleId IS NULL AND Timestamp < '2026-01-01 00:00:00'";
        var plan = await ExplainAsync(command);

        Assert.Contains(Index, plan);
        Assert.Contains("Timestamp<?", plan);
    }

    private static async Task<string> ExplainAsync(DbCommand command)
    {
        command.CommandText = "EXPLAIN QUERY PLAN " + command.CommandText;
        var lines = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(3));
        return string.Join(" | ", lines);
    }
}
