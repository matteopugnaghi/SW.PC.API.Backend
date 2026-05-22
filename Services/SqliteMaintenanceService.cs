// ============================================================================
// SqliteMaintenanceService.cs — SCG-113 (EU CRA)
// ============================================================================
// Background service que ejecuta VACUUM + PRAGMA integrity_check periódicamente
// sobre la base de datos del proyecto activo y registra el resultado en el
// audit log L1 (AuditCategory.System / AuditAction.DatabaseMaintenance).
//
// Frecuencia por defecto: 7 días (configurable vía Maintenance:DatabaseIntervalDays).
// Primera ejecución: 60 segundos después del arranque.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public class SqliteMaintenanceService : BackgroundService
    {
        private readonly ILogger<SqliteMaintenanceService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _intervalDays;
        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(60);

        public SqliteMaintenanceService(
            ILogger<SqliteMaintenanceService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _intervalDays = configuration.GetValue<int>("Maintenance:DatabaseIntervalDays", 7);

            _logger.LogInformation(
                "🧹 SqliteMaintenanceService initialized — interval: {Days} days, initial delay: {Delay}s",
                _intervalDays, (int)_initialDelay.TotalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(_initialDelay, stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunMaintenanceCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ SqliteMaintenanceService: unexpected error during maintenance cycle");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromDays(_intervalDays), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("🧹 SqliteMaintenanceService stopped");
        }

        private async Task RunMaintenanceCycleAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var projectContext = scope.ServiceProvider.GetService<IProjectContextService>();

            var projectId = projectContext?.ActiveProjectId ?? "default";
            var dbPath = factory.GetCurrentDatabasePath();

            _logger.LogInformation("🧹 Running database maintenance (project: {Project}, db: {Db})", projectId, dbPath);

            var started = DateTime.Now;
            long sizeBeforeBytes = 0, sizeAfterBytes = 0;
            string integrityResult = "unknown";
            bool ok = false;
            string? errorMessage = null;

            try
            {
                if (File.Exists(dbPath))
                    sizeBeforeBytes = new FileInfo(dbPath).Length;

                await using (var ctx = factory.CreateDbContext())
                {
                    // 1) PRAGMA integrity_check
                    try
                    {
                        var conn = ctx.Database.GetDbConnection();
                        if (conn.State != System.Data.ConnectionState.Open)
                            await conn.OpenAsync(ct);

                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA integrity_check;";
                        var scalar = await cmd.ExecuteScalarAsync(ct);
                        integrityResult = scalar?.ToString() ?? "unknown";
                    }
                    catch (Exception ex)
                    {
                        integrityResult = $"error: {ex.Message}";
                    }

                    // 2) VACUUM (rebuild + defrag)
                    await ctx.Database.ExecuteSqlRawAsync("VACUUM;", ct);
                }

                if (File.Exists(dbPath))
                    sizeAfterBytes = new FileInfo(dbPath).Length;

                ok = integrityResult.Equals("ok", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogError(ex, "❌ Database maintenance failed for project {Project}", projectId);
            }

            var durationMs = (DateTime.Now - started).TotalMilliseconds;
            var bytesReclaimed = Math.Max(0, sizeBeforeBytes - sizeAfterBytes);
            var details =
                $"project={projectId}; db={Path.GetFileName(dbPath)}; integrity={integrityResult}; " +
                $"sizeBeforeBytes={sizeBeforeBytes}; sizeAfterBytes={sizeAfterBytes}; " +
                $"reclaimedBytes={bytesReclaimed}" +
                (errorMessage is null ? "" : $"; error={errorMessage}");

            var result = errorMessage is not null
                ? AuditResult.Failure
                : (ok ? AuditResult.Success : AuditResult.Warning);

            try
            {
                await auditLog.LogAsync(
                    AuditCategory.System,
                    AuditAction.DatabaseMaintenance,
                    result,
                    details: details,
                    userId: "system",
                    userName: "SqliteMaintenanceService",
                    durationMs: durationMs,
                    projectId: projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to write DatabaseMaintenance audit entry");
            }

            _logger.LogInformation(
                "🧹 Database maintenance finished — result: {Result}, integrity: {Integrity}, " +
                "reclaimed: {Reclaimed} bytes, duration: {Duration:N0} ms",
                result, integrityResult, bytesReclaimed, durationMs);
        }
    }
}
