namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Background service that runs SFTP certificate sync on a timer.
    /// SyncInterval = 0 → disabled (manual only via UI buttons).
    /// SyncInterval > 0 → runs every N seconds.
    /// </summary>
    public class OpcUaSftpSyncService : BackgroundService
    {
        private readonly ILogger<OpcUaSftpSyncService> _logger;
        private readonly IOpcUaSftpService _sftpService;
        private readonly IOpcUaCertificateService _certService;

        public OpcUaSftpSyncService(
            ILogger<OpcUaSftpSyncService> logger,
            IOpcUaSftpService sftpService,
            IOpcUaCertificateService certService)
        {
            _logger = logger;
            _sftpService = sftpService;
            _certService = certService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for other services to initialize (OPC UA server loads config from Excel)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var interval = _sftpService.SyncIntervalSeconds;
            if (interval <= 0)
            {
                _logger.LogInformation("🔐 [SFTP-SYNC] Auto-sync DISABLED (SyncInterval=0). Use manual buttons in UI.");
                return;
            }

            _logger.LogInformation("🔐 [SFTP-SYNC] Auto-sync ENABLED — every {Interval}s ({Hours}h)",
                interval, interval / 3600.0);

            // Run first sync immediately after startup delay
            await RunOnce(stoppingToken);

            // Then loop on interval
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
                    await RunOnce(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🔐 [SFTP-SYNC] Unexpected error in sync loop");
                    // Wait a bit before retrying
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("🔐 [SFTP-SYNC] Service stopped");
        }

        private async Task RunOnce(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var result = await _sftpService.RunSyncCycleAsync(_certService);
                if (result.Success)
                {
                    _logger.LogInformation("🔐 [SFTP-SYNC] Cycle complete: {Uploaded} uploaded, {Imported} imported, {Skipped} skipped",
                        result.FilesUploaded, result.FilesImported, result.FilesSkipped);
                }
                else
                {
                    _logger.LogWarning("🔐 [SFTP-SYNC] Cycle had issues: {Details}",
                        string.Join("; ", result.Details));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔐 [SFTP-SYNC] Sync cycle failed");
            }
        }
    }
}
