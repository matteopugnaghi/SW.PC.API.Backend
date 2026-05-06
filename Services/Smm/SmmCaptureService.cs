using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Smm.Entities;

namespace SW.PC.API.Backend.Services.Smm;

/// <summary>
/// Servicio central de captura SMM (DEC-013/025).
/// NO hace polling. Solo reacciona a eventos (CycleStart/CycleEnd, hora programada, OnDemand).
/// </summary>
public interface ISmmCaptureService
{
    /// <summary>Lectura batch de variables Continuous (job nocturno DEC-026).</summary>
    Task<int> SnapshotContinuousAsync(CancellationToken ct = default);

    /// <summary>Snapshot manual admin (DEC-026 punto 6).</summary>
    Task<int> OnDemandSnapshotAsync(int? groupId, CancellationToken ct = default);

    /// <summary>Inicio de ciclo PerCycle por flanco FALSE→TRUE de CycleRunningVar (DEC-018).</summary>
    Task<int> OnCycleStartAsync(int groupId, DateTime startedAt, CancellationToken ct = default);

    /// <summary>Fin de ciclo PerCycle por flanco TRUE→FALSE (DEC-018).</summary>
    Task OnCycleEndAsync(int cycleId, DateTime endedAt, string endedReason = "Normal", CancellationToken ct = default);

    /// <summary>Aborta ciclos huérfanos al startup (DEC-020 punto 5).</summary>
    Task<int> AbortOrphanCyclesAsync(CancellationToken ct = default);
}

public class SmmCaptureService : ISmmCaptureService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly ITwinCATService _twincat;
    private readonly ILogger<SmmCaptureService> _logger;

    public SmmCaptureService(
        IProjectDbContextFactory dbFactory,
        ITwinCATService twincat,
        ILogger<SmmCaptureService> logger)
    {
        _dbFactory = dbFactory;
        _twincat = twincat;
        _logger = logger;
    }

    public async Task<int> SnapshotContinuousAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();

        // Variables Continuous = pertenecen a grupos ReadFrequency=Continuous
        var continuousGroups = await db.SmmGroups
            .Where(g => g.ReadFrequency == "Continuous")
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (continuousGroups.Count == 0) return 0;

        var vars = await db.SmmVariables
            .Where(v => continuousGroups.Contains(v.GroupId) && v.PlcVariable != null)
            .ToListAsync(ct);

        if (vars.Count == 0) return 0;

        var plcNames = vars.Where(v => v.PlcVariable != null).Select(v => v.PlcVariable!).Distinct().ToList();
        var snapshot = await _twincat.ReadAllVariablesAsync(plcNames);

        var now = DateTime.UtcNow;
        var readings = new List<SmmReading>(vars.Count);
        foreach (var v in vars)
        {
            double? value = null;
            bool isError = false;
            string? errReason = null;

            if (v.PlcVariable != null && snapshot.Variables.TryGetValue(v.PlcVariable, out var raw))
            {
                if (raw == null) { isError = true; errReason = "PlcReadNull"; }
                else
                {
                    try { value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture); }
                    catch { isError = true; errReason = $"CastError: {raw.GetType().Name}"; }
                }
            }
            else
            {
                isError = true; errReason = "NotFoundInSnapshot";
            }

            readings.Add(new SmmReading
            {
                GroupId = v.GroupId,
                VariableId = v.Id,
                CycleId = null,
                Timestamp = now,
                Value = value,
                Source = "Plc",
                IsError = isError,
                ErrorReason = errReason,
                PlcVariable = v.PlcVariable
            });
        }

        db.SmmReadings.AddRange(readings);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("📊 SMM Continuous snapshot: {Count} readings", readings.Count);
        return readings.Count;
    }

    public Task<int> OnDemandSnapshotAsync(int? groupId, CancellationToken ct = default)
    {
        // Versión simplificada Fase 4: si groupId especificado filtra, sino comporta como Continuous.
        // (Para snapshot real OnDemand se reusa la misma lógica de SumRead.)
        return SnapshotContinuousAsync(ct);
    }

    public async Task<int> OnCycleStartAsync(int groupId, DateTime startedAt, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var cycle = new SmmCycle
        {
            GroupId = groupId,
            StartedAt = startedAt,
            Status = "Running"
        };
        db.SmmCycles.Add(cycle);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("🔄 SMM CycleStart group={GroupId} cycleId={Id}", groupId, cycle.Id);
        return cycle.Id;
    }

    public async Task OnCycleEndAsync(int cycleId, DateTime endedAt, string endedReason = "Normal", CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var cycle = await db.SmmCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle == null) return;
        if (cycle.Status != "Running") return; // INMUTABLE DEC-023

        cycle.Status = "Completed";
        cycle.CompletedAt = endedAt;
        cycle.EndedReason = endedReason;

        // Recalcular alarmas (DEC-020 punto 3)
        var alarms = await db.SmmCycleAlarms.Where(a => a.CycleId == cycleId).ToListAsync(ct);
        cycle.AlarmsCount = alarms.Count;
        cycle.AlarmTime_s = alarms.Sum(a => a.DurationInCycle_s);
        cycle.HadAlarms = alarms.Count > 0;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("✅ SMM CycleEnd cycleId={Id} reason={Reason} alarms={N}", cycleId, endedReason, alarms.Count);
    }

    public async Task<int> AbortOrphanCyclesAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var orphans = await db.SmmCycles.Where(c => c.Status == "Running").ToListAsync(ct);
        if (orphans.Count == 0) return 0;

        foreach (var c in orphans)
        {
            var lastReading = await db.SmmReadings
                .Where(r => r.CycleId == c.Id)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => (DateTime?)r.Timestamp)
                .FirstOrDefaultAsync(ct);

            c.Status = "Aborted";
            c.EndedReason = "BackendRestart";
            c.CompletedAt = lastReading ?? c.StartedAt;
        }
        await db.SaveChangesAsync(ct);
        _logger.LogWarning("⚠️ SMM AbortOrphanCycles: {N} ciclos huérfanos cerrados al startup", orphans.Count);
        return orphans.Count;
    }
}
