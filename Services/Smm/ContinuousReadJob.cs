using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Smm;

/// <summary>
/// Job nocturno Continuous (DEC-026): timer 60s, dispara captura cuando HH:mm coincide
/// con SystemConfiguration.ContinuousReadTime del proyecto activo.
/// - Sin catchup si PC apagado.
/// - Sin retry tras fallo ADS.
/// - 1 ejecución por día (DST tolerada).
/// - Aborta ciclos huérfanos al startup (DEC-020).
/// </summary>
public class ContinuousReadJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ContinuousReadJob> _logger;
    private string _lastFiredKey = string.Empty; // "yyyy-MM-dd HH:mm" del último disparo

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

        // 2) Loop: cada 60s comprobar si HH:mm coincide con ContinuousReadTime
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndFireAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ContinuousReadJob tick error");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckAndFireAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var excelService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
        var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();

        // Default 23:59 — captura el "cierre del día lógico" (00:00–23:59).
        // El usuario puede sobreescribir en Excel SystemConfig.ContinuousReadTime
        // (formato HH:mm). Si la máquina tiene turno noche y los contadores no se
        // resetean a medianoche, capturar a las 23:59 da el total acumulado del día.
        string targetTime = "23:59";
        try
        {
            var sys = await excelService.LoadSystemConfigurationAsync(projectContext.ExcelConfigPath);
            if (!string.IsNullOrWhiteSpace(sys.ContinuousReadTime)) targetTime = sys.ContinuousReadTime;
        }
        catch
        {
            // Excel inaccesible: usar default
        }

        var now = DateTime.Now;
        var nowKey = now.ToString("yyyy-MM-dd HH:mm");
        var nowHm = now.ToString("HH:mm");
        if (nowHm != targetTime) return;
        if (nowKey == _lastFiredKey) return; // dedup en el mismo minuto

        _lastFiredKey = nowKey;
        _logger.LogInformation("⏰ ContinuousReadJob disparo {Time} (target={Target})", nowHm, targetTime);

        var capture = scope.ServiceProvider.GetRequiredService<ISmmCaptureService>();
        try
        {
            var n = await capture.SnapshotContinuousAsync(ct);
            _logger.LogInformation("✅ ContinuousReadJob persistió {N} readings", n);
        }
        catch (Exception ex)
        {
            // DEC-026 punto 4: sin retry, skip silencioso (warning, NO IsError=1)
            _logger.LogWarning(ex, "❌ ContinuousReadJob: snapshot falló (skip 1 día, sin retry)");
        }
    }
}
