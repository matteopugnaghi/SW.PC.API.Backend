using System.IO;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// 📋 EU CRA - Servicio que vigila el fichero ProjectConfig.xlsm del proyecto activo
/// y dispara recarga automática (con diff-audit) cuando se detectan cambios externos
/// (edición desde Excel, copia desde otro PC, sobrescritura via SMB, etc).
///
/// Funcionamiento:
/// 1. Crea un FileSystemWatcher sobre la carpeta de configuración del proyecto activo
/// 2. Filtra eventos por ProjectConfig.xls* (xlsm/xlsx)
/// 3. Aplica debounce de 3s (Excel dispara múltiples eventos al guardar)
/// 4. Espera a que el lock del fichero se libere (max 10 reintentos × 1s)
/// 5. Invalida la cache + fuerza recarga inmediata → diff-audit se emite desde
///    ExcelConfigService.LoadSystemConfigurationAsync
/// 6. Se re-suscribe al cambiar de proyecto activo
/// </summary>
public class ExcelConfigWatcherService : BackgroundService
{
    private readonly IProjectContextService _projectContext;
    private readonly IExcelConfigService _excelConfigService;
    private readonly ILogger<ExcelConfigWatcherService> _logger;

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new();
    private string? _watchedFilePath;

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(1);
    private const int LockRetryCount = 5;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(500);

    public ExcelConfigWatcherService(
        IProjectContextService projectContext,
        IExcelConfigService excelConfigService,
        ILogger<ExcelConfigWatcherService> logger)
    {
        _projectContext = projectContext;
        _excelConfigService = excelConfigService;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SetupWatcher();

        // Re-suscribir al cambiar de proyecto activo
        _projectContext.OnProjectChanged += (_) =>
        {
            _logger.LogInformation("📋 ExcelConfigWatcher: proyecto cambiado, reconfigurando watcher");
            SetupWatcher();
        };

        // El servicio se mantiene vivo hasta el shutdown; el watcher trabaja de forma asíncrona.
        return Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private void SetupWatcher()
    {
        lock (_lock)
        {
            // Limpiar watcher anterior si existe
            DisposeWatcher();

            try
            {
                var configFolder = _projectContext.ConfigPath;
                if (string.IsNullOrEmpty(configFolder) || !Directory.Exists(configFolder))
                {
                    _logger.LogWarning("📋 ExcelConfigWatcher: carpeta de config no existe: {Path}", configFolder);
                    return;
                }

                _watchedFilePath = _projectContext.ExcelConfigPath;

                _watcher = new FileSystemWatcher(configFolder)
                {
                    Filter = "ProjectConfig.xls*", // xlsm, xlsx
                    NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnFileEvent;
                _watcher.Created += OnFileEvent;
                _watcher.Renamed += OnFileRenamed;
                _watcher.Error += OnWatcherError;

                _logger.LogInformation("📋 ExcelConfigWatcher: vigilando {Path}", configFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📋 ExcelConfigWatcher: error configurando watcher (no fatal, continúa sin auto-reload)");
            }
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // Filtrar archivos temporales típicos de Excel (~$ProjectConfig.xlsm, ~RFxxxx.TMP)
        if (e.Name == null || e.Name.StartsWith("~") || e.Name.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase))
            return;

        ScheduleReload(e.ChangeType.ToString(), e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (e.Name == null || e.Name.StartsWith("~") || e.Name.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase))
            return;

        ScheduleReload($"Renamed from {e.OldName}", e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "📋 ExcelConfigWatcher: error interno del watcher; intentando reiniciar");
        SetupWatcher();
    }

    private void ScheduleReload(string trigger, string fullPath)
    {
        lock (_lock)
        {
            _logger.LogDebug("📋 ExcelConfigWatcher: evento {Trigger} en {Path}, debouncing {Delay}s",
                trigger, Path.GetFileName(fullPath), DebounceDelay.TotalSeconds);

            // Reiniciar el debounce timer
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(
                _ => _ = ExecuteReloadAsync(fullPath),
                null,
                DebounceDelay,
                Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ExecuteReloadAsync(string fullPath)
    {
        try
        {
            // Esperar a que el fichero se libere (Excel puede tardar en soltar el lock)
            if (!await WaitForFileUnlockAsync(fullPath))
            {
                _logger.LogWarning("📋 ExcelConfigWatcher: fichero sigue bloqueado tras {Retries} reintentos, abortando reload",
                    LockRetryCount);
                return;
            }

            _logger.LogInformation("📋 ExcelConfigWatcher: cambio detectado en {File}, invalidando cache + recarga",
                Path.GetFileName(fullPath));

            // Invalidar cache del fichero específico
            _excelConfigService.InvalidateCache(fullPath);

            // Forzar recarga inmediata → emite diff-audit si hay cambios en campos sensibles
            await _excelConfigService.LoadSystemConfigurationAsync(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "📋 ExcelConfigWatcher: error en auto-reload de {File}", Path.GetFileName(fullPath));
        }
    }

    private static async Task<bool> WaitForFileUnlockAsync(string path)
    {
        for (int i = 0; i < LockRetryCount; i++)
        {
            try
            {
                // FileShare.ReadWrite: permite leer aunque Excel mantenga el fichero abierto
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true; // Abierto sin problemas
            }
            catch (IOException)
            {
                await Task.Delay(LockRetryDelay);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(LockRetryDelay);
            }
        }
        return false;
    }

    private void DisposeWatcher()
    {
        if (_watcher != null)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileEvent;
                _watcher.Created -= OnFileEvent;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
            }
            catch { /* ignore */ }
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public override void Dispose()
    {
        lock (_lock)
        {
            DisposeWatcher();
        }
        base.Dispose();
    }
}
