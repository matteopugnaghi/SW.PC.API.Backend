using SW.PC.API.Backend.Models.Smm.Entities;

namespace SW.PC.API.Backend.Services.Smm;

public static class SmmReadingQueries
{
    /// <summary>
    /// Fechas de los últimos <paramref name="take"/> snapshots Continuous/OnDemand de un grupo
    /// (CycleId IS NULL; cada snapshot escribe la misma fecha en todas sus variables), de más
    /// reciente a más antiguo.
    /// </summary>
    /// <remarks>
    /// Se agrupa por fecha en lugar de usar Distinct(): con Distinct().OrderBy().Take() EF genera
    /// una subconsulta que SQLite resuelve leyendo y ordenando TODAS las lecturas del grupo. Con
    /// GROUP BY recorre IX_SMM_Readings_Group_Cycle_Time hacia atrás y se detiene al llegar a
    /// <paramref name="take"/>, sea cual sea el tamaño de la tabla.
    /// </remarks>
    public static IQueryable<DateTime> RecentSnapshotTimestamps(
        IQueryable<SmmReading> readings, int groupId, int take, DateTime? from = null, DateTime? to = null)
    {
        var q = readings.Where(r => r.GroupId == groupId && r.CycleId == null);
        if (from.HasValue) q = q.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(r => r.Timestamp <= to.Value);

        return q
            .GroupBy(r => r.Timestamp)
            .Select(g => g.Key)
            .OrderByDescending(t => t)
            .Take(take);
    }
}
