// ============================================================================
// ExportCronSchedulerService.cs — Scheduler de ExportTasks por cron (Fase 3)
// ============================================================================
// HostedService Singleton que cada 30 s:
//   1. Recarga la lista de tareas con ExecutionType="cron" y Enabled=true.
//   2. Para cada tarea, parsea CronExpression (cacheado por expresión).
//   3. Si la expresión es "due" en el minuto actual (hora LOCAL del servidor)
//      Y el último disparo (LastRunAt) NO fue dentro del minuto actual,
//      lanza la tarea vía IExportService.RunTaskAsync.
//
// Notas de diseño:
//   - Granularidad = 1 minuto (suficiente para cron clásico).
//   - Hora LOCAL del servidor industrial (no UTC) — coincide con la expectativa
//     del operador ("a las 8 de la mañana").
//   - La deduplicación usa LastRunAt y compara contra el inicio del minuto
//     actual; tolerante a reinicios o ticks ligeramente desplazados.
//   - Multi-proyecto: trabaja sobre el proyecto activo (active-project.json).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;
using System.Collections.Concurrent;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportCronSchedulerService
{
    /// <summary>Fuerza un tick inmediato (útil tras crear/editar tarea cron).</summary>
    Task TickNowAsync(CancellationToken ct = default);
}

public class ExportCronSchedulerService : BackgroundService, IExportCronSchedulerService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ExportCronSchedulerService> _logger;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    /// <summary>Cache de CronExpression → evaluator parseado.</summary>
    private readonly ConcurrentDictionary<string, CronExpressionEvaluator> _evCache
        = new(StringComparer.Ordinal);

    public ExportCronSchedulerService(
        IServiceProvider services,
        ILogger<ExportCronSchedulerService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📤🕒 ExportCronSchedulerService iniciado (tick={Tick}s)", (int)TickInterval.TotalSeconds);
        // Defensivo: asegurar que la tabla ExportTasks existe en el proyecto activo
        // antes del primer tick (evita SqliteException 'no such table' si el lazy-init
        // del factory se saltó esta tabla por un fallo previo).
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureExportTasksTableAsync(db);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ExportCron] EnsureExportTasksTableAsync de arranque falló (se reintentará en cada tick)");
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExportCron] Tick falló");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public Task TickNowAsync(CancellationToken ct = default) => TickAsync(ct);

    private async Task TickAsync(CancellationToken ct)
    {
        // Snapshot del minuto actual (hora local del servidor)
        var nowLocal = DateTime.Now;
        var minuteStart = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day,
                                       nowLocal.Hour, nowLocal.Minute, 0, DateTimeKind.Local);

        List<ExportTask> due;
        using (var scope = _services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();

            // Auto-heal: garantiza la tabla en cada tick (idempotente y barato)
            await AquafrischDbContextFactory.EnsureExportTasksTableAsync(db);

            var candidates = await db.ExportTasks
                .Where(t => t.ExecutionType == "cron"
                            && t.Enabled
                            && t.CronExpression != null
                            && t.CronExpression != "")
                .ToListAsync(ct);

            due = new List<ExportTask>();
            foreach (var t in candidates)
            {
                var ev = GetOrCreateEvaluator(t.CronExpression!);
                if (ev is null) continue;
                if (!ev.IsDue(nowLocal)) continue;

                // Dedupe: si LastRunAt cae dentro del minuto actual, ya se disparó.
                if (t.LastRunAt is DateTime last)
                {
                    var lastLocal = last.Kind == DateTimeKind.Utc
                        ? last.ToLocalTime()
                        : DateTime.SpecifyKind(last, DateTimeKind.Local);
                    if (lastLocal >= minuteStart) continue;
                }
                due.Add(t);
            }
        }

        foreach (var t in due)
        {
            _ = TriggerAsync(t.Id, t.Name, t.CronExpression!);
        }
    }

    private CronExpressionEvaluator? GetOrCreateEvaluator(string expression)
    {
        if (_evCache.TryGetValue(expression, out var existing)) return existing;
        var (ok, error, ev) = CronExpressionEvaluator.TryParse(expression);
        if (!ok || ev is null)
        {
            _logger.LogWarning("[ExportCron] Cron inválido ignorado: '{Expr}' → {Err}", expression, error);
            return null;
        }
        _evCache[expression] = ev;
        return ev;
    }

    private async Task TriggerAsync(int taskId, string name, string expr)
    {
        try
        {
            using var scope = _services.CreateScope();
            var exportService = scope.ServiceProvider.GetRequiredService<IExportService>();
            _logger.LogInformation("📤🕒 Trigger CRON: ejecutando ExportTask #{Id} ('{Name}') por expresión '{Expr}'",
                taskId, name, expr);
            var result = await exportService.RunTaskAsync(taskId, runtimeMetadata: null);
            _logger.LogInformation("📤🕒 ExportTask #{Id} resultado: success={Success}, summary={Summary}",
                taskId, result.Success, result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExportCron] TriggerAsync falló para taskId={Id}", taskId);
        }
    }
}
