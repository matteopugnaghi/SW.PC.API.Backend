// ============================================================================
// NxLogFileService.cs - Escritura de logs JSONL para NxLog
// ============================================================================
// Servicio dedicado a escribir logs en formato JSONL (JSON Lines) para que
// NxLog los recoja y envíe al SOC PIVOT TISSEO.
//
// Formato: Una línea JSON por evento (append-only)
// Ubicación: Projects/{projectId}/logs/
//   - audit_YYYY-MM-DD.log      → L1 Audit events (seguridad)
//   - operations_YYYY-MM-DD.log → L2 Operation events (alarmas PLC, recetas)
//
// Exigencias TISSEO:
//   - TLS_M3_ALS_EXI_CYB_SYS_00510: Generar eventos de seguridad → SOC PIVOT
//   - TLS_M3_ALS_EXI_CYB_SYS_00516: Formato GELF + Syslog SNARE via NxLog
//   - TLS_M3_ALS_EXI_CYB_SYS_00514: Retención local 30 días
//   - TLS_M3_ALS_EXI_CYB_SYS_00505: Journaliser événements cybersécurité
//   - TLS_M3_ALS_EXI_CYB_SYS_00513: No incluir datos confidenciales en logs
// ============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interface para el servicio de escritura JSONL para NxLog
/// </summary>
public interface INxLogFileService
{
    /// <summary>
    /// Escribir un evento L1 (Audit) al fichero JSONL
    /// </summary>
    Task WriteAuditEventAsync(NxLogAuditEntry entry, string? projectId = null);
    
    /// <summary>
    /// Escribir un evento L2 (Operation) al fichero JSONL
    /// </summary>
    Task WriteOperationEventAsync(NxLogOperationEntry entry, string? projectId = null);
    
    /// <summary>
    /// Indica si el servicio NxLog está habilitado (configurable desde Excel)
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Nombre de la fuente configurado (ej: "MAL-EQI")
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// Días de retención configurados
    /// </summary>
    int RetentionDays { get; }
    
    /// <summary>
    /// Obtener la ruta de la carpeta logs/ del proyecto
    /// </summary>
    string GetLogsPath(string? projectId = null);
    
    /// <summary>
    /// Limpiar ficheros JSONL antiguos (retención configurable)
    /// </summary>
    Task CleanupOldLogsAsync(int retentionDays = 30);
    
    /// <summary>
    /// Obtener estadísticas de los ficheros JSONL
    /// </summary>
    NxLogStats GetStats(string? projectId = null);
}

/// <summary>
/// Entrada L1 Audit para JSONL (formato limpio para NxLog, sin datos confidenciales - TLS_..00513)
/// </summary>
public class NxLogAuditEntry
{
    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";
    
    [JsonPropertyName("source")]
    public string Source { get; set; } = "MAL-EQI";
    
    [JsonPropertyName("log_type")]
    public string LogType { get; set; } = "L1_AUDIT";
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
    
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
    
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";
    
    [JsonPropertyName("user")]
    public string? User { get; set; }
    
    [JsonPropertyName("ip")]
    public string? IpAddress { get; set; }
    
    [JsonPropertyName("details")]
    public string? Details { get; set; }
    
    [JsonPropertyName("duration_ms")]
    public double? DurationMs { get; set; }
    
    [JsonPropertyName("affected_count")]
    public int? AffectedItemCount { get; set; }
}

/// <summary>
/// Entrada L2 Operation para JSONL (formato limpio para NxLog)
/// </summary>
public class NxLogOperationEntry
{
    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";
    
    [JsonPropertyName("source")]
    public string Source { get; set; } = "MAL-EQI";
    
    [JsonPropertyName("log_type")]
    public string LogType { get; set; } = "L2_OPERATION";
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
    
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Info";
    
    [JsonPropertyName("user")]
    public string? User { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("plc_variable")]
    public string? PlcVariable { get; set; }
    
    [JsonPropertyName("alarm_code")]
    public string? AlarmCode { get; set; }
    
    [JsonPropertyName("alarm_type")]
    public string? AlarmType { get; set; }
    
    [JsonPropertyName("old_value")]
    public string? OldValue { get; set; }
    
    [JsonPropertyName("new_value")]
    public string? NewValue { get; set; }
    
    [JsonPropertyName("ip")]
    public string? IpAddress { get; set; }
}

/// <summary>
/// Estadísticas de los ficheros JSONL
/// </summary>
public class NxLogStats
{
    public string LogsPath { get; set; } = "";
    public int AuditFileCount { get; set; }
    public int OperationFileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime? OldestFile { get; set; }
    public DateTime? NewestFile { get; set; }
}

/// <summary>
/// Servicio Singleton para escribir logs JSONL append-only para NxLog
/// </summary>
public class NxLogFileService : INxLogFileService
{
    private readonly IProjectContextService _projectContext;
    private readonly IExcelConfigService _excelConfigService;
    private readonly ILogger<NxLogFileService> _logger;
    private readonly string _contentRoot;
    private readonly SemaphoreSlim _auditWriteLock = new(1, 1);
    private readonly SemaphoreSlim _operationWriteLock = new(1, 1);
    
    // Config leída desde Excel (cacheada tras primera lectura)
    private bool _configLoaded = false;
    private bool _enabled = false;
    private int _retentionDays = 30;
    private string _sourceName = "MAL-EQI";
    private readonly object _configLock = new();
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NxLogFileService(
        IProjectContextService projectContext,
        IExcelConfigService excelConfigService,
        IWebHostEnvironment environment,
        ILogger<NxLogFileService> logger)
    {
        _projectContext = projectContext;
        _excelConfigService = excelConfigService;
        _contentRoot = environment.ContentRootPath;
        _logger = logger;
        
        _logger.LogInformation("📋 NxLogFileService initialized (config will be loaded on first use)");

        // 🔄 Suscribirse a cambios de proyecto para recargar configuración
        projectContext.OnProjectChanged += (newProjectId) =>
        {
            lock (_configLock)
            {
                _configLoaded = false; // Forzar recarga de configuración NxLog
            }
            _logger.LogInformation("🔄 NxLogFileService: Config reset por cambio de proyecto a {ProjectId}", newProjectId);
        };
    }
    
    /// <summary>
    /// Carga la configuración NxLog desde Excel (una sola vez, cacheada)
    /// </summary>
    private void EnsureConfigLoaded()
    {
        if (_configLoaded) return;
        
        lock (_configLock)
        {
            if (_configLoaded) return;
            
            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (!string.IsNullOrEmpty(excelPath) && File.Exists(excelPath))
                {
                    var config = _excelConfigService.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
                    _enabled = config.NxLogEnabled;
                    _retentionDays = config.NxLogRetentionDays;
                    _sourceName = config.NxLogSourceName;
                }
                
                _logger.LogInformation("📋 NxLog config loaded: Enabled={Enabled}, Retention={Days}d, Source={Source}",
                    _enabled, _retentionDays, _sourceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error loading NxLog config from Excel, defaulting to disabled");
                _enabled = false;
            }
            
            _configLoaded = true;
        }
    }
    
    /// <summary>
    /// Indica si NxLog JSONL export está habilitado
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            EnsureConfigLoaded();
            return _enabled;
        }
    }
    
    /// <summary>
    /// Nombre de la fuente configurado
    /// </summary>
    public string SourceName
    {
        get
        {
            EnsureConfigLoaded();
            return _sourceName;
        }
    }
    
    /// <summary>
    /// Días de retención configurados
    /// </summary>
    public int RetentionDays
    {
        get
        {
            EnsureConfigLoaded();
            return _retentionDays;
        }
    }

    /// <summary>
    /// Obtener la ruta de la carpeta logs/ del proyecto.
    /// Multi-proyecto: Projects/{projectId}/logs/
    /// Legacy: wwwroot/logs/
    /// </summary>
    public string GetLogsPath(string? projectId = null)
    {
        var effectiveProjectId = projectId ?? _projectContext.ActiveProjectId;
        
        if (effectiveProjectId != "default")
        {
            return Path.Combine(_contentRoot, "Projects", effectiveProjectId, "logs");
        }
        else
        {
            return Path.Combine(_contentRoot, "wwwroot", "logs");
        }
    }

    /// <summary>
    /// Asegurar que la carpeta logs/ existe
    /// </summary>
    private string EnsureLogsPath(string? projectId = null)
    {
        var path = GetLogsPath(projectId);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogInformation("📁 Created NxLog directory: {Path}", path);
        }
        return path;
    }

    /// <summary>
    /// Escribir un evento L1 (Audit) al fichero JSONL append-only
    /// </summary>
    public async Task WriteAuditEventAsync(NxLogAuditEntry entry, string? projectId = null)
    {
        EnsureConfigLoaded();
        if (!_enabled) return;
        
        // Aplicar source name desde config
        entry.Source = _sourceName;
        
        try
        {
            var logsPath = EnsureLogsPath(projectId);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(logsPath, $"audit_{today}.log");
            
            var jsonLine = JsonSerializer.Serialize(entry, JsonOptions);
            
            await _auditWriteLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(filePath, jsonLine + Environment.NewLine);
            }
            finally
            {
                _auditWriteLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error writing NxLog audit event: {Action}", entry.Action);
        }
    }

    /// <summary>
    /// Escribir un evento L2 (Operation) al fichero JSONL append-only
    /// </summary>
    public async Task WriteOperationEventAsync(NxLogOperationEntry entry, string? projectId = null)
    {
        EnsureConfigLoaded();
        if (!_enabled) return;
        
        // Aplicar source name desde config
        entry.Source = _sourceName;
        
        try
        {
            var logsPath = EnsureLogsPath(projectId);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(logsPath, $"operations_{today}.log");
            
            var jsonLine = JsonSerializer.Serialize(entry, JsonOptions);
            
            await _operationWriteLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(filePath, jsonLine + Environment.NewLine);
            }
            finally
            {
                _operationWriteLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error writing NxLog operation event: {Action}", entry.Action);
        }
    }

    /// <summary>
    /// Limpiar ficheros JSONL antiguos (retención por defecto: 30 días - TLS_..00514)
    /// </summary>
    public async Task CleanupOldLogsAsync(int retentionDays = 30)
    {
        EnsureConfigLoaded();
        if (!_enabled) return;
        
        // Usar retención desde config si no se especifica explícitamente
        var effectiveRetention = retentionDays == 30 ? _retentionDays : retentionDays;
        
        try
        {
            var logsPath = GetLogsPath();
            if (!Directory.Exists(logsPath)) return;

            var cutoff = DateTime.Now.AddDays(-effectiveRetention);
            var files = Directory.GetFiles(logsPath, "*.log");
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Formato: audit_YYYY-MM-DD o operations_YYYY-MM-DD
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && DateTime.TryParse(parts[^1], out var fileDate))
                    {
                        if (fileDate < cutoff)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error deleting old NxLog file: {File}", file);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("📋 NxLog cleanup: {Count} old files deleted (retention: {Days} days)",
                    deletedCount, effectiveRetention);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during NxLog cleanup");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Obtener estadísticas de los ficheros JSONL
    /// </summary>
    public NxLogStats GetStats(string? projectId = null)
    {
        var stats = new NxLogStats
        {
            LogsPath = GetLogsPath(projectId)
        };

        if (!Directory.Exists(stats.LogsPath))
            return stats;

        var files = Directory.GetFiles(stats.LogsPath, "*.log");
        
        foreach (var file in files)
        {
            var fi = new FileInfo(file);
            stats.TotalSizeBytes += fi.Length;
            
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.StartsWith("audit_"))
                stats.AuditFileCount++;
            else if (fileName.StartsWith("operations_"))
                stats.OperationFileCount++;
            
            if (stats.OldestFile == null || fi.CreationTime < stats.OldestFile)
                stats.OldestFile = fi.CreationTime;
            if (stats.NewestFile == null || fi.LastWriteTime > stats.NewestFile)
                stats.NewestFile = fi.LastWriteTime;
        }

        return stats;
    }
}
