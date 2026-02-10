// Services/SystemLogService.cs
// L3 - In-Memory System Log Service with circular buffer
// Captures Warning/Error/Critical from ILogger pipeline
// Provides real-time push via SignalR when clients are subscribed

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    // ═══════════════════════════════════════════════════════════════
    //  SERVICE INTERFACE
    // ═══════════════════════════════════════════════════════════════

    public interface ISystemLogService
    {
        /// <summary>Add a log entry to the buffer (called by ILoggerProvider or client endpoint)</summary>
        void AddEntry(SystemLogEntry entry);

        /// <summary>Get filtered entries from the buffer</summary>
        IReadOnlyList<SystemLogEntry> GetEntries(SystemLogQuery? query = null);

        /// <summary>Get summary statistics</summary>
        SystemLogSummary GetSummary();

        /// <summary>Clear all entries from the buffer</summary>
        void Clear();

        /// <summary>Current entry count</summary>
        int Count { get; }

        /// <summary>Buffer capacity</summary>
        int Capacity { get; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SERVICE IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════

    public class SystemLogService : ISystemLogService
    {
        private readonly ConcurrentQueue<SystemLogEntry> _buffer = new();
        private readonly IHubContext<ScadaHub>? _hubContext;
        private long _nextId = 1;
        private const int DEFAULT_CAPACITY = 1000;

        // Categories to EXCLUDE (too noisy, not useful for operators)
        private static readonly HashSet<string> _excludedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.AspNetCore.Hosting.Diagnostics",
            "Microsoft.AspNetCore.Routing.EndpointMiddleware",
            "Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware",
            "Microsoft.AspNetCore.Cors.Infrastructure.CorsMiddleware",
            "Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler",
            "Microsoft.AspNetCore.Authorization.DefaultAuthorizationService",
            "Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker",
            "Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor",
            "Microsoft.EntityFrameworkCore.Database.Command",
            "Microsoft.EntityFrameworkCore.Infrastructure",
            "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher"
        };

        public int Capacity => DEFAULT_CAPACITY;
        public int Count => _buffer.Count;

        public SystemLogService(IHubContext<ScadaHub>? hubContext = null)
        {
            _hubContext = hubContext;
        }

        public void AddEntry(SystemLogEntry entry)
        {
            // Assign ID and ensure timestamp
            entry.Id = Interlocked.Increment(ref _nextId);
            if (entry.Timestamp == default)
                entry.Timestamp = DateTime.Now;

            // Trim message if excessively long
            if (entry.Message?.Length > 500)
                entry.Message = entry.Message[..497] + "...";

            if (entry.Exception?.Length > 300)
                entry.Exception = entry.Exception[..297] + "...";

            // Enqueue
            _buffer.Enqueue(entry);

            // Evict oldest if over capacity
            while (_buffer.Count > DEFAULT_CAPACITY)
                _buffer.TryDequeue(out _);

            // Push to SignalR group (fire-and-forget, don't block ILogger pipeline)
            if (_hubContext != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _hubContext.Clients.Group("system_logs")
                            .SendAsync("SystemLogEntry", entry);
                    }
                    catch
                    {
                        // Silently ignore - don't cause recursive logging
                    }
                });
            }
        }

        public IReadOnlyList<SystemLogEntry> GetEntries(SystemLogQuery? query = null)
        {
            var entries = _buffer.ToArray().AsEnumerable();

            if (query != null)
            {
                if (query.ExactLevel.HasValue)
                    entries = entries.Where(e => e.Level == query.ExactLevel.Value);
                else if (query.MinLevel.HasValue)
                    entries = entries.Where(e => e.Level >= query.MinLevel.Value);

                if (query.Source.HasValue)
                    entries = entries.Where(e => e.Source == query.Source.Value);

                if (!string.IsNullOrWhiteSpace(query.Category))
                    entries = entries.Where(e =>
                        e.Category.Contains(query.Category, StringComparison.OrdinalIgnoreCase));

                entries = entries.TakeLast(query.Take);
            }

            return entries.OrderByDescending(e => e.Timestamp).ToList();
        }

        public SystemLogSummary GetSummary()
        {
            var entries = _buffer.ToArray();
            return new SystemLogSummary
            {
                TotalEntries = entries.Length,
                WarningCount = entries.Count(e => e.Level == SystemLogLevel.Warning),
                ErrorCount = entries.Count(e => e.Level == SystemLogLevel.Error),
                CriticalCount = entries.Count(e => e.Level == SystemLogLevel.Critical),
                BackendCount = entries.Count(e => e.Source == SystemLogSource.Backend),
                FrontendCount = entries.Count(e => e.Source == SystemLogSource.Frontend),
                BufferCapacity = DEFAULT_CAPACITY,
                OldestEntry = entries.Length > 0 ? entries.First().Timestamp : null,
                NewestEntry = entries.Length > 0 ? entries.Last().Timestamp : null
            };
        }

        public void Clear()
        {
            while (_buffer.TryDequeue(out _)) { }
        }

        /// <summary>Check if a category should be excluded from capture</summary>
        public static bool ShouldExcludeCategory(string category)
        {
            return _excludedCategories.Contains(category) ||
                   category.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase) ||
                   category.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.OrdinalIgnoreCase) ||
                   category.StartsWith("Microsoft.Extensions.Http.", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CUSTOM ILogger PROVIDER - Captures Warning+ into the buffer
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Custom ILoggerProvider that feeds Warning/Error/Critical entries
    /// from the .NET logging pipeline into the SystemLogService buffer.
    /// </summary>
    [ProviderAlias("SystemLogBuffer")]
    public class SystemLogBufferProvider : ILoggerProvider
    {
        private readonly ISystemLogService _logService;
        private readonly ConcurrentDictionary<string, SystemLogBufferLogger> _loggers = new();

        public SystemLogBufferProvider(ISystemLogService logService)
        {
            _logService = logService;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName,
                name => new SystemLogBufferLogger(name, _logService));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Individual logger instance for each category that filters
    /// and forwards Warning+ entries to the buffer service.
    /// </summary>
    public class SystemLogBufferLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ISystemLogService _logService;
        // Simplify category for display (remove namespace prefix)
        private readonly string _shortCategory;

        public SystemLogBufferLogger(string categoryName, ISystemLogService logService)
        {
            _categoryName = categoryName;
            _logService = logService;

            // "SW.PC.API.Backend.Services.TwinCATService" → "TwinCATService"
            var lastDot = categoryName.LastIndexOf('.');
            _shortCategory = lastDot >= 0 ? categoryName[(lastDot + 1)..] : categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            // Only capture Warning, Error, Critical
            return logLevel >= LogLevel.Warning && logLevel < LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Skip noisy framework categories
            if (SystemLogService.ShouldExcludeCategory(_categoryName))
                return;

            // Skip our own service to prevent recursive logging
            if (_categoryName.Contains("SystemLog", StringComparison.OrdinalIgnoreCase))
                return;

            var entry = new SystemLogEntry
            {
                Timestamp = DateTime.Now,
                Level = logLevel switch
                {
                    LogLevel.Warning => SystemLogLevel.Warning,
                    LogLevel.Error => SystemLogLevel.Error,
                    LogLevel.Critical => SystemLogLevel.Critical,
                    _ => SystemLogLevel.Warning
                },
                Source = SystemLogSource.Backend,
                Category = _shortCategory,
                Message = formatter(state, exception),
                Exception = exception != null
                    ? $"{exception.GetType().Name}: {exception.Message}"
                    : null
            };

            _logService.AddEntry(entry);
        }
    }
}
