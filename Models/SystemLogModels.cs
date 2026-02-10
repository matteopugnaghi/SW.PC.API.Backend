// Models/SystemLogModels.cs
// L3 - System Log Models for in-memory diagnostic log buffer
// These logs are NOT persisted - they exist only in RAM for real-time diagnostics

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Log severity levels matching Microsoft.Extensions.Logging.LogLevel
    /// Only Warning, Error, and Critical are captured in the buffer
    /// </summary>
    public enum SystemLogLevel
    {
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Source of the log entry
    /// </summary>
    public enum SystemLogSource
    {
        /// <summary>Backend .NET runtime, services, controllers</summary>
        Backend,
        /// <summary>Frontend React app (selective errors sent via POST)</summary>
        Frontend
    }

    /// <summary>
    /// A single system log entry stored in the in-memory circular buffer
    /// </summary>
    public class SystemLogEntry
    {
        /// <summary>Unique ID for frontend keying</summary>
        public long Id { get; set; }

        /// <summary>When the log was created</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Warning, Error, or Critical</summary>
        public SystemLogLevel Level { get; set; }

        /// <summary>Backend or Frontend</summary>
        public SystemLogSource Source { get; set; }

        /// <summary>Category/logger name (e.g. "TwinCATService", "ScadaHub", "SignalR")</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>The log message</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Exception details if available (type + message, NOT full stack trace)</summary>
        public string? Exception { get; set; }
    }

    /// <summary>
    /// DTO for frontend to send client-side logs to the buffer
    /// </summary>
    public class ClientLogRequest
    {
        /// <summary>Warning or Error</summary>
        public SystemLogLevel Level { get; set; } = SystemLogLevel.Error;

        /// <summary>Source category (e.g. "SignalR", "API", "3DLoader")</summary>
        public string Category { get; set; } = "Frontend";

        /// <summary>Error/warning message</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query parameters for filtering system logs
    /// </summary>
    public class SystemLogQuery
    {
        /// <summary>Minimum log level to return (default: Warning = show all)</summary>
        public SystemLogLevel? MinLevel { get; set; }

        /// <summary>Filter by source (Backend/Frontend/null=all)</summary>
        public SystemLogSource? Source { get; set; }

        /// <summary>Filter by category (partial match, case-insensitive)</summary>
        public string? Category { get; set; }

        /// <summary>Max entries to return (default: 200)</summary>
        public int Take { get; set; } = 200;
    }

    /// <summary>
    /// Summary statistics for the compact card
    /// </summary>
    public class SystemLogSummary
    {
        public int TotalEntries { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int CriticalCount { get; set; }
        public int BackendCount { get; set; }
        public int FrontendCount { get; set; }
        public int BufferCapacity { get; set; }
        public DateTime? OldestEntry { get; set; }
        public DateTime? NewestEntry { get; set; }
    }
}
