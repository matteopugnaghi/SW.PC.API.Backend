// 📋 AUDIT LOG SERVICE - EU CRA Compliance (CADRA/Alstom)
// Proporciona logging de auditoría con firma SHA256, envío externo y retención
// Los audit logs se guardan en Projects/{projectId}/audit/ (multi-proyecto)

using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 📋 EU CRA - Interface del servicio de auditoría
    /// </summary>
    public interface IAuditLogService
    {
        Task LogAsync(AuditCategory category, AuditAction action, AuditResult result, 
            string? details = null, string? userId = null, string? userName = null, 
            string? ipAddress = null, int? affectedItemCount = null, double? durationMs = null);
        
        Task<AuditLogStatus> GetStatusAsync();
        Task<List<AuditLogEntry>> GetRecentLogsAsync(int count = 50);
        Task<AuditLogResponse> GetLogsAsync(AuditLogQuery query);
        Task<string> ExportLogsAsync(DateTime? from = null, DateTime? to = null);
        Task<AuditSummary> GetSummaryAsync(int days = 7);
        Task<bool> VerifyLogIntegrityAsync(string logId);
        Task CleanupOldLogsAsync();
        
        /// <summary>
        /// Forzar escritura del cache a disco (útil antes de backups)
        /// </summary>
        Task FlushAsync();
    }

    /// <summary>
    /// 📋 EU CRA - Servicio de logging de auditoría
    /// Almacena logs en archivos JSON con firma SHA256 para cumplimiento CADRA/Alstom
    /// Los logs se guardan en Projects/{projectId}/audit/ para multi-proyecto
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly ILogger<AuditLogService> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProjectContextService _projectContext;
        private readonly string _contentRoot;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ConcurrentQueue<AuditLogEntry> _cache = new();
        private readonly ConcurrentQueue<AuditLogEntry> _externalQueue = new();
        private DateTime _lastFlush = DateTime.Now;
        private string? _lastLogHash = null;
        
        // Configuración (cargada desde Excel)
        private bool _isEnabled = true;
        private int _retentionDays = 30;
        private bool _signatureEnabled = true;
        private bool _externalEnabled = false;
        private string _externalUrl = "";
        private int _maxEntriesPerFile = 10000;
        private const int MAX_CACHE_SIZE = 100;
        private const int FLUSH_INTERVAL_SECONDS = 30;
        
        // Estadísticas de envío externo
        private DateTime? _lastExternalSendTime;
        private int _externalSendFailures = 0;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // 📋 Serializar enums como strings para legibilidad (Git, Authentication, etc.)
            Converters = { new JsonStringEnumConverter() }
        };

        public AuditLogService(
            ILogger<AuditLogService> logger, 
            IWebHostEnvironment env,
            IExcelConfigService excelConfigService,
            IHttpClientFactory httpClientFactory,
            IProjectContextService projectContext)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _httpClientFactory = httpClientFactory;
            _projectContext = projectContext;
            _contentRoot = env.ContentRootPath;
            
            // NO crear directorio aquí - se crea dinámicamente cuando se necesite escribir
            // Solo loggeamos la ruta que se usará (sin crearla)
            var auditPath = GetAuditPathWithoutCreate();
            _logger.LogInformation("📋 AuditLogService initialized - Path will be: {Path}", auditPath);
            
            // Cargar configuración en background
            _ = LoadConfigurationAsync();
            
            // Iniciar tarea de envío externo en background
            _ = StartExternalSenderAsync();
            
            // Iniciar tarea de limpieza periódica
            _ = StartCleanupTaskAsync();
        }
        
        /// <summary>
        /// Obtener la ruta de audit logs SIN crear el directorio.
        /// Usar para logging y consultas que no requieren escritura.
        /// </summary>
        private string GetAuditPathWithoutCreate()
        {
            var projectId = _projectContext.ActiveProjectId;
            
            if (projectId != "default")
            {
                return Path.Combine(_contentRoot, "Projects", projectId, "audit");
            }
            else
            {
                return Path.Combine(_contentRoot, "wwwroot", "audit");
            }
        }
        
        /// <summary>
        /// Obtener la ruta de audit logs Y crear el directorio si no existe.
        /// Usar cuando se necesita escribir archivos.
        /// Multi-proyecto: Projects/{projectId}/audit/
        /// Legacy: wwwroot/audit/
        /// </summary>
        private string GetAuditPath()
        {
            var path = GetAuditPathWithoutCreate();
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("📁 Created audit directory: {Path}", path);
            }
            
            return path;
        }

        /// <summary>
        /// Cargar configuración desde Excel
        /// </summary>
        private async Task LoadConfigurationAsync()
        {
            try
            {
                var possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExcelConfigs", "ProjectConfig.xlsm"),
                    @"C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\ExcelConfigs\ProjectConfig.xlsm"
                };
                
                var excelPath = possiblePaths.FirstOrDefault(File.Exists);
                
                if (excelPath != null)
                {
                    var config = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    
                    _isEnabled = config.AuditLogEnabled;
                    _retentionDays = config.AuditLogRetentionDays;
                    _signatureEnabled = config.AuditLogSignatureEnabled;
                    _externalEnabled = config.AuditLogExternalEnabled;
                    _externalUrl = config.AuditLogExternalUrl;
                    _maxEntriesPerFile = config.AuditLogMaxEntriesPerFile;
                    
                    _logger.LogInformation("📋 AuditLog config loaded: Enabled={Enabled}, Retention={Days}d, Signature={Sig}, External={Ext}", 
                        _isEnabled, _retentionDays, _signatureEnabled, _externalEnabled);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "📋 Could not load audit config from Excel, using defaults");
            }
        }

        /// <summary>
        /// Registrar evento de auditoría
        /// </summary>
        public async Task LogAsync(AuditCategory category, AuditAction action, AuditResult result,
            string? details = null, string? userId = null, string? userName = null,
            string? ipAddress = null, int? affectedItemCount = null, double? durationMs = null)
        {
            if (!_isEnabled) return;

            var entry = new AuditLogEntry
            {
                Category = category,
                Action = action,
                Result = result,
                Details = details,
                UserId = userId,
                UserName = userName,
                IpAddress = ipAddress,
                AffectedItemCount = affectedItemCount,
                DurationMs = durationMs
            };

            // Añadir firma SHA256 si está habilitada
            if (_signatureEnabled)
            {
                entry.PreviousHash = _lastLogHash;
                entry.Signature = ComputeSignature(entry);
                _lastLogHash = entry.Signature;
            }

            _cache.Enqueue(entry);
            
            // Añadir a cola de envío externo si está habilitado
            if (_externalEnabled && !string.IsNullOrEmpty(_externalUrl))
            {
                _externalQueue.Enqueue(entry);
            }

            // Flush si el cache está lleno o ha pasado el intervalo
            if (_cache.Count >= MAX_CACHE_SIZE || 
                (DateTime.Now - _lastFlush).TotalSeconds > FLUSH_INTERVAL_SECONDS)
            {
                await FlushCacheAsync();
            }

            // Log a consola
            LogToConsole(entry);
        }

        /// <summary>
        /// Calcular firma SHA256 del log
        /// </summary>
        private string ComputeSignature(AuditLogEntry entry)
        {
            var data = $"{entry.Id}|{entry.Timestamp:O}|{entry.Category}|{entry.Action}|{entry.Result}|{entry.Details}|{entry.UserId}|{entry.PreviousHash}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verificar integridad de un log específico
        /// </summary>
        public async Task<bool> VerifyLogIntegrityAsync(string logId)
        {
            if (!_signatureEnabled) return true;

            var allEntries = await GetAllEntriesAsync();
            var entry = allEntries.FirstOrDefault(e => e.Id == logId);
            
            if (entry == null) return false;
            
            // Recalcular firma
            var originalSignature = entry.Signature;
            entry.Signature = null;
            var expectedSignature = ComputeSignature(entry);
            entry.Signature = originalSignature;
            
            return originalSignature == expectedSignature;
        }

        /// <summary>
        /// Log a consola con emoji según resultado
        /// </summary>
        private void LogToConsole(AuditLogEntry entry)
        {
            var emoji = entry.Result switch
            {
                AuditResult.Success => "✅",
                AuditResult.Warning => "⚠️",
                AuditResult.Failure => "❌",
                AuditResult.Error => "🔥",
                _ => "📋"
            };

            _logger.LogInformation("{Emoji} AUDIT: {Category}/{Action} = {Result} - {Details}",
                emoji, entry.Category, entry.Action, entry.Result, entry.Details ?? "No details");
        }

        /// <summary>
        /// Tarea de envío a URL externa (SOC)
        /// </summary>
        private async Task StartExternalSenderAsync()
        {
            await Task.Delay(5000); // Esperar inicialización
            
            while (true)
            {
                try
                {
                    if (_externalEnabled && !string.IsNullOrEmpty(_externalUrl) && !_externalQueue.IsEmpty)
                    {
                        var entries = new List<AuditLogEntry>();
                        while (_externalQueue.TryDequeue(out var entry) && entries.Count < 50)
                        {
                            entries.Add(entry);
                        }

                        if (entries.Count > 0)
                        {
                            await SendToExternalAsync(entries);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "📋 Error in external sender task");
                }

                await Task.Delay(10000); // Cada 10 segundos
            }
        }

        /// <summary>
        /// Enviar logs a URL externa (SOC PIVOT TISSEO)
        /// </summary>
        private async Task SendToExternalAsync(List<AuditLogEntry> entries)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AuditExternal");
                client.Timeout = TimeSpan.FromSeconds(30);

                var payload = new
                {
                    source = "AquafrischSupervisor",
                    timestamp = DateTime.Now,
                    entries = entries
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(_externalUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    _lastExternalSendTime = DateTime.Now;
                    _externalSendFailures = 0;
                    _logger.LogInformation("📋 Sent {Count} audit logs to external SOC", entries.Count);
                }
                else
                {
                    _externalSendFailures++;
                    _logger.LogWarning("📋 External SOC returned {StatusCode}", response.StatusCode);
                    
                    // Re-encolar si falló
                    foreach (var entry in entries)
                    {
                        _externalQueue.Enqueue(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                _externalSendFailures++;
                _logger.LogWarning(ex, "📋 Failed to send to external SOC");
                
                // Re-encolar
                foreach (var entry in entries)
                {
                    _externalQueue.Enqueue(entry);
                }
            }
        }

        /// <summary>
        /// Tarea de limpieza periódica
        /// </summary>
        private async Task StartCleanupTaskAsync()
        {
            await Task.Delay(60000); // Esperar 1 minuto
            
            while (true)
            {
                try
                {
                    await CleanupOldLogsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "📋 Error in cleanup task");
                }

                await Task.Delay(TimeSpan.FromHours(1)); // Cada hora
            }
        }

        /// <summary>
        /// Limpiar logs antiguos según retención configurada
        /// </summary>
        public async Task CleanupOldLogsAsync()
        {
            var auditPath = GetAuditPath();
            if (!Directory.Exists(auditPath)) return;

            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var files = Directory.GetFiles(auditPath, "audit_*.json");
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Formato: audit_2025-12-06 o audit_2025-12-06_123456
                    var datePart = fileName.Replace("audit_", "").Split('_')[0];
                    
                    if (DateTime.TryParse(datePart, out var fileDate) && fileDate < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "📋 Error deleting old audit file: {File}", file);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("📋 Cleaned up {Count} old audit files (retention: {Days} days)", 
                    deletedCount, _retentionDays);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Escribir cache a disco
        /// </summary>
        private async Task FlushCacheAsync()
        {
            if (_cache.IsEmpty) return;

            await _writeLock.WaitAsync();
            try
            {
                var entries = new List<AuditLogEntry>();
                while (_cache.TryDequeue(out var entry))
                {
                    entries.Add(entry);
                }

                if (entries.Count == 0) return;

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var auditPath = GetAuditPath();
                var filePath = Path.Combine(auditPath, $"audit_{today}.json");

                List<AuditLogEntry> existingEntries = new();
                
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        // Solo deserializar si el archivo tiene contenido válido
                        if (!string.IsNullOrWhiteSpace(json) && json.Trim().StartsWith("["))
                        {
                            existingEntries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json, JsonOptions) ?? new();
                        }
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("⚠️ Corrupted audit file detected during flush, will overwrite: {File}", filePath);
                        existingEntries = new List<AuditLogEntry>();
                    }
                }

                existingEntries.AddRange(entries);

                // Rotar archivo si excede el límite
                if (existingEntries.Count > _maxEntriesPerFile)
                {
                    var archivePath = Path.Combine(auditPath, $"audit_{today}_{DateTime.Now:HHmmss}.json");
                    await File.WriteAllTextAsync(archivePath, JsonSerializer.Serialize(existingEntries, JsonOptions));
                    existingEntries = new List<AuditLogEntry>();
                }

                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(existingEntries, JsonOptions));

                _lastFlush = DateTime.Now;
                _logger.LogDebug("📋 Flushed {Count} audit entries to {File}", entries.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error flushing audit cache");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Forzar escritura del cache a disco (método público para uso externo, ej: antes de backup)
        /// </summary>
        public async Task FlushAsync()
        {
            _logger.LogInformation("📋 Forzando flush de audit logs a disco...");
            await FlushCacheAsync();
            _logger.LogInformation("📋 Flush de audit logs completado");
        }

        /// <summary>
        /// Obtener estado del sistema de auditoría
        /// </summary>
        public async Task<AuditLogStatus> GetStatusAsync()
        {
            await FlushCacheAsync();

            var auditPath = GetAuditPathWithoutCreate();
            var status = new AuditLogStatus
            {
                IsEnabled = _isEnabled,
                StoragePath = auditPath,
                RetentionDays = _retentionDays,
                SignatureEnabled = _signatureEnabled,
                ExternalEnabled = _externalEnabled,
                ExternalUrl = _externalEnabled ? _externalUrl : null,
                MaxEntriesPerFile = _maxEntriesPerFile,
                LastExternalSendTime = _lastExternalSendTime,
                ExternalSendFailures = _externalSendFailures
            };

            try
            {
                var allEntries = await GetAllEntriesAsync();
                status.TotalEntries = allEntries.Count;
                
                // Calcular entradas de hoy
                var today = DateTime.Now.Date;
                status.TodayEntries = allEntries.Count(e => e.Timestamp.Date == today);
                
                if (allEntries.Any())
                {
                    status.OldestEntry = allEntries.Min(e => e.Timestamp);
                    status.NewestEntry = allEntries.Max(e => e.Timestamp);
                    
                    status.EntriesByCategory = allEntries
                        .GroupBy(e => e.Category.ToString())
                        .ToDictionary(g => g.Key, g => g.Count());
                    
                    status.EntriesByResult = allEntries
                        .GroupBy(e => e.Result.ToString())
                        .ToDictionary(g => g.Key, g => g.Count());
                }

                if (Directory.Exists(auditPath))
                {
                    var files = Directory.GetFiles(auditPath, "*.json");
                    status.StorageSizeBytes = files.Sum(f => new FileInfo(f).Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting audit status");
            }

            return status;
        }

        /// <summary>
        /// Obtener logs recientes
        /// </summary>
        public async Task<List<AuditLogEntry>> GetRecentLogsAsync(int count = 50)
        {
            await FlushCacheAsync();
            
            var allEntries = await GetAllEntriesAsync();
            return allEntries
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Obtener logs con filtros
        /// </summary>
        public async Task<AuditLogResponse> GetLogsAsync(AuditLogQuery query)
        {
            await FlushCacheAsync();
            
            var allEntries = await GetAllEntriesAsync();
            
            IEnumerable<AuditLogEntry> filtered = allEntries;

            if (query.From.HasValue)
                filtered = filtered.Where(e => e.Timestamp >= query.From.Value);
            
            if (query.To.HasValue)
                filtered = filtered.Where(e => e.Timestamp <= query.To.Value);
            
            if (query.Category.HasValue)
                filtered = filtered.Where(e => e.Category == query.Category.Value);
            
            if (query.Result.HasValue)
                filtered = filtered.Where(e => e.Result == query.Result.Value);
            
            if (!string.IsNullOrEmpty(query.UserId))
                filtered = filtered.Where(e => e.UserId == query.UserId);

            var totalCount = filtered.Count();
            var entries = filtered
                .OrderByDescending(e => e.Timestamp)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToList();

            return new AuditLogResponse
            {
                Entries = entries,
                TotalCount = totalCount,
                Page = query.Skip / query.Take + 1,
                PageSize = query.Take,
                HasMore = query.Skip + entries.Count < totalCount
            };
        }

        /// <summary>
        /// Exportar logs a JSON
        /// </summary>
        public async Task<string> ExportLogsAsync(DateTime? from = null, DateTime? to = null)
        {
            await FlushCacheAsync();
            
            var allEntries = await GetAllEntriesAsync();
            
            if (from.HasValue)
                allEntries = allEntries.Where(e => e.Timestamp >= from.Value).ToList();
            
            if (to.HasValue)
                allEntries = allEntries.Where(e => e.Timestamp <= to.Value).ToList();

            var exportData = new
            {
                ExportedAt = DateTime.Now,
                ExportedBy = "AquafrischSupervisor",
                From = from,
                To = to,
                TotalEntries = allEntries.Count,
                SignatureEnabled = _signatureEnabled,
                Entries = allEntries.OrderByDescending(e => e.Timestamp).ToList()
            };

            return JsonSerializer.Serialize(exportData, JsonOptions);
        }

        /// <summary>
        /// Obtener resumen de auditoría
        /// </summary>
        public async Task<AuditSummary> GetSummaryAsync(int days = 7)
        {
            await FlushCacheAsync();
            
            var allEntries = await GetAllEntriesAsync();
            var cutoff = DateTime.Now.AddDays(-days);
            var periodEntries = allEntries.Where(e => e.Timestamp >= cutoff).ToList();

            return new AuditSummary
            {
                TotalEntries = periodEntries.Count,
                PeriodStart = cutoff,
                PeriodEnd = DateTime.Now,
                ByCategory = periodEntries
                    .GroupBy(e => e.Category.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByResult = periodEntries
                    .GroupBy(e => e.Result.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByDay = periodEntries
                    .GroupBy(e => e.Timestamp.Date.ToString("yyyy-MM-dd"))
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentFailures = periodEntries
                    .Where(e => e.Result == AuditResult.Failure || e.Result == AuditResult.Error)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(10)
                    .ToList()
            };
        }

        /// <summary>
        /// Leer todos los logs de archivos
        /// </summary>
        private async Task<List<AuditLogEntry>> GetAllEntriesAsync()
        {
            var allEntries = new List<AuditLogEntry>();
            var auditPath = GetAuditPath();

            if (!Directory.Exists(auditPath))
                return allEntries;

            var files = Directory.GetFiles(auditPath, "audit_*.json")
                .OrderByDescending(f => f);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    
                    // Verificar si el archivo está vacío o contiene solo whitespace
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogWarning("⚠️ Audit file is empty, initializing: {File}", file);
                        // Inicializar archivo vacío con array JSON válido
                        await File.WriteAllTextAsync(file, "[]");
                        continue;
                    }
                    
                    // Verificar que empiece con [ para ser un array JSON válido
                    var trimmed = json.Trim();
                    if (!trimmed.StartsWith("["))
                    {
                        _logger.LogWarning("⚠️ Audit file is not a valid JSON array, backing up and reinitializing: {File}", file);
                        // Backup del archivo corrupto
                        var backupPath = file + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Move(file, backupPath);
                        await File.WriteAllTextAsync(file, "[]");
                        continue;
                    }
                    
                    var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json, JsonOptions);
                    if (entries != null)
                        allEntries.AddRange(entries);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("⚠️ Invalid JSON in audit file, backing up and reinitializing: {File} - {Error}", file, jsonEx.Message);
                    try
                    {
                        // Backup del archivo corrupto
                        var backupPath = file + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Move(file, backupPath);
                        await File.WriteAllTextAsync(file, "[]");
                    }
                    catch (Exception moveEx)
                    {
                        _logger.LogError(moveEx, "❌ Could not backup corrupted audit file: {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error reading audit file: {File}", file);
                }
            }

            return allEntries;
        }
    }
}
