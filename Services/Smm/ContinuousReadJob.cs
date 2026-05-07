using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Smm;

/// <summary>
/// Job de captura Continuous (DEC-026, ampliado por-grupo).
/// Cada grupo (SMM_Groups con ReadFrequency='Continuous') tiene su propia frecuencia y retención:
///   - ContinuousReadIntervalSec: si null/0/&gt;=86400 → modo DIARIO (1/día a SystemConfig.ContinuousReadTime).
///                                si 1..86399 → modo CÍCLICO: snapshot cada N segundos.
///   - ContinuousRetentionDays: tras cada snapshot del grupo, borra filas Continuous viejas del grupo.
/// - Sin catchup si PC apagado.
/// - Sin retry tras fallo ADS.
/// - Aborta ciclos huérfanos al startup (DEC-020).
/// </summary>
public class ContinuousReadJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ContinuousReadJob> _logger;

    // Estado por grupo (clave = GroupId)
    private readonly Dictionary<int, DateTime> _lastCyclicFireUtc = new();
    private readonly Dictionary<int, string> _lastDailyFiredKey = new();

    public ContinuousReadJob(IServiceProvider services, ILogger<ContinuousReadJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1) Abort orphan cycles at startup (DEC-020 punto 5)
        try
        {
            using var scope = _services.CreateScope();
            var capture = scope.ServiceProvider.GetRequiredService<ISmmCaptureService>();
            await capture.AbortOrphanCyclesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ContinuousReadJob: AbortOrphanCycles falló (continuamos)");
        }

        // 2) Loop principal: tick rápido para soportar grupos con intervalo pequeño.
        //    Sleep dinámico = min de los próximos disparos pendientes (clamped 1..60s).
        while (!stoppingToken.IsCancellationRequested)
        {
            int sleepSec = 60;
            try
            {
                sleepSec = await TickAllGroupsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ContinuousReadJob tick error");
            }

            if (sleepSec < 1) sleepSec = 1;
            if (sleepSec > 60) sleepSec = 60;
            try { await Task.Delay(TimeSpan.FromSeconds(sleepSec), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<int> TickAllGroupsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var excelService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
        var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<Data.IProjectDbContextFactory>();
        var capture = scope.ServiceProvider.GetRequiredService<ISmmCaptureService>();

        // Hora del snapshot diario global (default 23:59) — se usa para grupos en modo DIARIO.
        string dailyTargetTime = "23:59";
        try
        {
            var sys = await excelService.LoadSystemConfigurationAsync(projectContext.ExcelConfigPath);
            if (!string.IsNullOrWhiteSpace(sys.ContinuousReadTime)) dailyTargetTime = sys.ContinuousReadTime;
        }
        catch { /* default */ }

        // Grupos Continuous con sus parámetros
        List<(int Id, string Name, int? IntervalSec, int? RetentionDays)> groups;
        using (var db = dbFactory.CreateDbContext())
        {
            groups = await db.SmmGroups
                .Where(g => g.ReadFrequency == "Continuous")
                .Select(g => new ValueTuple<int, string, int?, int?>(
                    g.Id, g.GroupName, g.ContinuousReadIntervalSec, g.ContinuousRetentionDays))
                .ToListAsync(ct);
        }

        if (groups.Count == 0) return 60;

        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now;
        var nowKey = nowLocal.ToString("yyyy-MM-dd HH:mm");
        var nowHm = nowLocal.ToString("HH:mm");

        int nextSleep = 60;

        foreach (var g in groups)
        {
            bool cyclic = g.IntervalSec.HasValue && g.IntervalSec.Value > 0 && g.IntervalSec.Value < 86400;
            bool fire = false;
            int sleepHint = 60;

            if (cyclic)
            {
                _lastCyclicFireUtc.TryGetValue(g.Id, out var last);
                var elapsed = (nowUtc - last).TotalSeconds;
                if (elapsed >= g.IntervalSec!.Value)
                {
                    fire = true;
                    _lastCyclicFireUtc[g.Id] = nowUtc;
                    sleepHint = Math.Min(g.IntervalSec.Value, 60);
                }
                else
                {
                    sleepHint = (int)Math.Ceiling(g.IntervalSec.Value - elapsed);
                }
            }
            else
            {
                // Modo DIARIO
                if (nowHm == dailyTargetTime)
                {
                    _lastDailyFiredKey.TryGetValue(g.Id, out var lastKey);
                    if (lastKey != nowKey)
                    {
                        fire = true;
                        _lastDailyFiredKey[g.Id] = nowKey;
                    }
                }
                sleepHint = 60;
            }

            if (fire)
            {
                _logger.LogInformation("⏰ Continuous snapshot grupo '{Name}' (id={Id}, mode={Mode})",
                    g.Name, g.Id, cyclic ? $"cyclic/{g.IntervalSec}s" : $"daily@{dailyTargetTime}");
                try
                {
                    var n = await capture.SnapshotContinuousGroupAsync(g.Id, ct);
                    _logger.LogInformation("✅ Grupo '{Name}' → {N} readings", g.Name, n);

                    // Retención por grupo
                    if (g.RetentionDays.HasValue && g.RetentionDays.Value > 0)
                    {
                        try
                        {
                            var purged = await capture.PurgeOldContinuousGroupAsync(g.Id, g.RetentionDays.Value, ct);
                            if (purged > 0)
                                _logger.LogInformation("🧹 Grupo '{Name}' retención {Days}d → {N} filas borradas",
                                    g.Name, g.RetentionDays.Value, purged);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Retención grupo {Name} falló (continuamos)", g.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "❌ Snapshot grupo '{Name}' falló (sin retry)", g.Name);
                }
            }

            if (sleepHint < nextSleep) nextSleep = sleepHint;
        }

        return nextSleep;
    }
}
