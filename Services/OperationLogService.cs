// 📋 OPERATION LOG SERVICE - Nivel 2 (Acciones de Operador)
// Registra acciones operativas de usuarios (alarmas, recetas, setpoints, etc.)
// Complementa el Audit Log (Nivel 1) que es obligatorio por EU CRA

using System.Collections.Concurrent;
using System.Text.Json;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 📋 Categorías de logs de operación (Nivel 2)
    /// </summary>
    public enum OperationCategory
    {
        // Navegación
        Navigation,
        
        // Alarmas
        Alarm,
        
        // Recetas
        Recipe,
        
        // Control de proceso
        Process,
        
        // Setpoints
        Setpoint,
        
        // Estadísticas
        Statistics,
        
        // Exportaciones
        Export,
        
        // Backup
        Backup
    }

    /// <summary>
    /// 📋 Acciones de operación
    /// </summary>
    public enum OperationAction
    {
        // Navigation
        ViewChange,
        MenuOpen,
        MenuClose,
        
        // Alarm
        AlarmView,
        AlarmAcknowledge,
        AlarmReset,
        AlarmSilence,
        AlarmExport,
        
        // Recipe
        RecipeView,
        RecipeCreate,
        RecipeEdit,
        RecipeDelete,
        RecipeLoad,
        RecipeExecute,
        RecipePause,
        RecipeResume,
        RecipeAbort,
        RecipeExport,
        RecipeImport,
        
        // Process
        ProcessStart,
        ProcessStop,
        ProcessPause,
        ProcessResume,
        ProcessModeChange,
        CommandExecute,
        
        // Setpoint
        SetpointView,
        SetpointChange,
        SetpointOverride,
        LimitChange,
        
        // Statistics
        StatisticsView,
        StatisticsExport,
        ReportGenerate,
        ReportExport,
        
        // Export
        DataExport,
        
        // Backup
        BackupCreate,
        BackupRestore,
        BackupDelete
    }

    /// <summary>
    /// 📋 Entrada de log de operación
    /// </summary>
    public class OperationLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public OperationCategory Category { get; set; }
        public OperationAction Action { get; set; }
        public string User { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object>? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// 📋 Query para filtrar logs
    /// </summary>
    public class OperationLogQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public OperationCategory? Category { get; set; }
        public OperationAction? Action { get; set; }
        public string? User { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Search { get; set; }
    }

    /// <summary>
    /// 📋 Respuesta paginada
    /// </summary>
    public class OperationLogResponse
    {
        public List<OperationLogEntry> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// 📋 Información de ayuda
    /// </summary>
    public class OperationLogHelp
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<HelpSection> Sections { get; set; } = new();
        public ComplianceInfo? Compliance { get; set; }
    }

    public class HelpSection
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class ComplianceInfo
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// 📋 Interface del servicio de logs de operación
    /// </summary>
    public interface IOperationLogService
    {
        Task LogAsync(OperationCategory category, OperationAction action, 
            string description, string? user = null, 
            Dictionary<string, object>? details = null,
            string? ipAddress = null, string? sessionId = null);
        
        Task<OperationLogResponse> GetLogsAsync(OperationLogQuery query);
        Task<List<OperationLogEntry>> GetRecentLogsAsync(int count = 50);
        Task<OperationLogHelp> GetHelpAsync(string language = "es");
        Task CleanupOldLogsAsync();
    }

    /// <summary>
    /// 📋 Servicio de logging de operaciones (Nivel 2)
    /// </summary>
    public class OperationLogService : IOperationLogService
    {
        private readonly ILogger<OperationLogService> _logger;
        private readonly string _logPath;
        private readonly ConcurrentQueue<OperationLogEntry> _cache = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private DateTime _lastFlush = DateTime.UtcNow;
        
        // Configuración
        private int _retentionDays = 365; // 1 año por defecto
        private int _maxEntriesPerFile = 10000;
        private const int MAX_CACHE_SIZE = 50;
        private const int FLUSH_INTERVAL_SECONDS = 60;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public OperationLogService(ILogger<OperationLogService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _logPath = Path.Combine(env.WebRootPath ?? "wwwroot", "logs", "operations");
            
            // No crear directorios en el constructor - solo crearlos cuando sea necesario escribir
            // Esto evita crear carpetas innecesarias en producción al inicio
            
            // Iniciar tarea de limpieza periódica
            _ = StartCleanupTaskAsync();
            
            _logger.LogInformation("📋 OperationLogService initialized - Path: {Path}", _logPath);
        }
        
        /// <summary>
        /// Asegurar que el directorio de logs existe (llamar antes de escribir)
        /// </summary>
        private void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
                _logger.LogInformation("📋 Created operation log directory: {Path}", _logPath);
            }
        }

        /// <summary>
        /// 📝 Registrar una operación
        /// </summary>
        public async Task LogAsync(OperationCategory category, OperationAction action, 
            string description, string? user = null, 
            Dictionary<string, object>? details = null,
            string? ipAddress = null, string? sessionId = null)
        {
            try
            {
                var entry = new OperationLogEntry
                {
                    Category = category,
                    Action = action,
                    Description = description,
                    User = user ?? "system",
                    Details = details,
                    IpAddress = ipAddress,
                    SessionId = sessionId
                };

                _cache.Enqueue(entry);
                
                _logger.LogDebug("📋 Operation logged: {Category}.{Action} by {User}: {Description}",
                    category, action, user, description);

                // Flush si cache llena o tiempo excedido
                if (_cache.Count >= MAX_CACHE_SIZE || 
                    (DateTime.UtcNow - _lastFlush).TotalSeconds >= FLUSH_INTERVAL_SECONDS)
                {
                    await FlushCacheAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging operation: {Action}", action);
            }
        }

        /// <summary>
        /// 📊 Obtener logs con filtros y paginación
        /// </summary>
        public async Task<OperationLogResponse> GetLogsAsync(OperationLogQuery query)
        {
            await FlushCacheAsync(); // Asegurar datos actualizados
            
            var allLogs = new List<OperationLogEntry>();
            
            // Leer archivos de log
            var logFiles = Directory.GetFiles(_logPath, "operations_*.json")
                .OrderByDescending(f => f)
                .Take(30); // Últimos 30 archivos

            foreach (var file in logFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var entries = JsonSerializer.Deserialize<List<OperationLogEntry>>(json, JsonOptions);
                    if (entries != null)
                    {
                        allLogs.AddRange(entries);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading log file: {File}", file);
                }
            }

            // Añadir logs en cache
            allLogs.AddRange(_cache);

            // Aplicar filtros
            var filtered = allLogs.AsQueryable();

            if (query.Category.HasValue)
                filtered = filtered.Where(l => l.Category == query.Category.Value);

            if (query.Action.HasValue)
                filtered = filtered.Where(l => l.Action == query.Action.Value);

            if (!string.IsNullOrEmpty(query.User))
                filtered = filtered.Where(l => l.User.Contains(query.User, StringComparison.OrdinalIgnoreCase));

            if (query.StartDate.HasValue)
                filtered = filtered.Where(l => l.Timestamp >= query.StartDate.Value);

            if (query.EndDate.HasValue)
                filtered = filtered.Where(l => l.Timestamp <= query.EndDate.Value.AddDays(1));

            if (!string.IsNullOrEmpty(query.Search))
                filtered = filtered.Where(l => 
                    l.Description.Contains(query.Search, StringComparison.OrdinalIgnoreCase));

            // Ordenar por fecha descendente
            var sorted = filtered.OrderByDescending(l => l.Timestamp);

            // Paginación
            var totalCount = sorted.Count();
            var items = sorted
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new OperationLogResponse
            {
                Items = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        /// <summary>
        /// 📋 Obtener logs recientes
        /// </summary>
        public async Task<List<OperationLogEntry>> GetRecentLogsAsync(int count = 50)
        {
            var result = await GetLogsAsync(new OperationLogQuery { Page = 1, PageSize = count });
            return result.Items;
        }

        /// <summary>
        /// ❓ Obtener información de ayuda
        /// </summary>
        public Task<OperationLogHelp> GetHelpAsync(string language = "es")
        {
            var help = language == "en" ? GetEnglishHelp() : GetSpanishHelp();
            return Task.FromResult(help);
        }

        private OperationLogHelp GetSpanishHelp() => new()
        {
            Title = "Registro de Operaciones (Nivel 2)",
            Description = "Esta vista muestra todas las acciones realizadas por los operadores en la aplicación. " +
                "A diferencia del Audit Log (Nivel 1) que registra eventos de seguridad, " +
                "el Operation Log registra las acciones operativas del día a día.",
            Sections = new List<HelpSection>
            {
                new() {
                    Title = "¿Qué se registra?",
                    Content = "• Cambios de vista en la aplicación\n" +
                        "• Reconocimiento de alarmas\n" +
                        "• Carga y ejecución de recetas\n" +
                        "• Cambios de setpoints\n" +
                        "• Exportaciones de datos\n" +
                        "• Acciones sobre el proceso"
                },
                new() {
                    Title = "Retención de datos",
                    Content = "Los logs de operación se conservan durante 1-5 años según la configuración del sistema. " +
                        "Este período es configurable en el archivo Excel de configuración."
                },
                new() {
                    Title = "Funcionalidades futuras",
                    Content = "Actualmente esta vista muestra datos de ejemplo. " +
                        "Cuando se implementen las vistas de Alarmas, Recetas y Estadísticas, " +
                        "se añadirán automáticamente los logs correspondientes."
                }
            },
            Compliance = new ComplianceInfo
            {
                Title = "Cumplimiento Normativo",
                Content = "El Operation Log (Nivel 2) complementa al Audit Log (Nivel 1) que es obligatorio por EU CRA. " +
                    "Mientras el Audit Log registra eventos de seguridad, el Operation Log proporciona " +
                    "trazabilidad operativa útil para diagnóstico y mejora continua."
            }
        };

        private OperationLogHelp GetEnglishHelp() => new()
        {
            Title = "Operation Logs (Level 2)",
            Description = "This view shows all actions performed by operators in the application. " +
                "Unlike the Audit Log (Level 1) which records security events, " +
                "the Operation Log records day-to-day operational actions.",
            Sections = new List<HelpSection>
            {
                new() {
                    Title = "What is logged?",
                    Content = "• View changes in the application\n" +
                        "• Alarm acknowledgments\n" +
                        "• Recipe loading and execution\n" +
                        "• Setpoint changes\n" +
                        "• Data exports\n" +
                        "• Process actions"
                },
                new() {
                    Title = "Data retention",
                    Content = "Operation logs are kept for 1-5 years according to system configuration. " +
                        "This period is configurable in the Excel configuration file."
                },
                new() {
                    Title = "Future features",
                    Content = "Currently this view shows example data. " +
                        "When the Alarms, Recipes, and Statistics views are implemented, " +
                        "the corresponding logs will be automatically added."
                }
            },
            Compliance = new ComplianceInfo
            {
                Title = "Regulatory Compliance",
                Content = "The Operation Log (Level 2) complements the Audit Log (Level 1) which is mandatory under EU CRA. " +
                    "While the Audit Log records security events, the Operation Log provides " +
                    "operational traceability useful for diagnostics and continuous improvement."
            }
        };

        /// <summary>
        /// 💾 Escribir cache a disco
        /// </summary>
        private async Task FlushCacheAsync()
        {
            if (_cache.IsEmpty) return;

            await _writeLock.WaitAsync();
            try
            {
                var entries = new List<OperationLogEntry>();
                while (_cache.TryDequeue(out var entry))
                {
                    entries.Add(entry);
                }

                if (entries.Count == 0) return;

                // Asegurar que el directorio existe antes de escribir
                EnsureLogDirectoryExists();

                var fileName = $"operations_{DateTime.UtcNow:yyyyMMdd}.json";
                var filePath = Path.Combine(_logPath, fileName);

                List<OperationLogEntry> existingEntries = new();
                if (File.Exists(filePath))
                {
                    var existingJson = await File.ReadAllTextAsync(filePath);
                    existingEntries = JsonSerializer.Deserialize<List<OperationLogEntry>>(existingJson, JsonOptions) 
                        ?? new List<OperationLogEntry>();
                }

                existingEntries.AddRange(entries);

                // Si excede el máximo, crear archivo nuevo
                if (existingEntries.Count > _maxEntriesPerFile)
                {
                    fileName = $"operations_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    filePath = Path.Combine(_logPath, fileName);
                    existingEntries = entries;
                }

                var json = JsonSerializer.Serialize(existingEntries, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);

                _lastFlush = DateTime.UtcNow;
                _logger.LogDebug("📋 Flushed {Count} operation logs to {File}", entries.Count, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing operation logs to disk");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 🗑️ Limpiar logs antiguos
        /// </summary>
        public async Task CleanupOldLogsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_logPath, "operations_*.json");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        _logger.LogInformation("🗑️ Deleted old operation log: {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old operation logs");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Iniciar tarea de limpieza periódica
        /// </summary>
        private async Task StartCleanupTaskAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(24));
                await CleanupOldLogsAsync();
            }
        }
    }
}
