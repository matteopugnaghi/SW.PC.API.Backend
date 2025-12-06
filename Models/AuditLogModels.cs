using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// 📋 EU CRA - Categorías de auditoría
    /// </summary>
    public enum AuditCategory
    {
        Integrity,      // Verificación de integridad
        Sbom,           // Generación/consulta de SBOM
        Vulnerability,  // Escaneo de vulnerabilidades
        Authentication, // Login/Logout
        Configuration,  // Cambios de configuración
        Git,            // Operaciones Git
        Certificate,    // Gestión de certificados
        System          // Eventos del sistema
    }

    /// <summary>
    /// 📋 EU CRA - Acciones auditables
    /// </summary>
    public enum AuditAction
    {
        // Integridad
        IntegrityVerify,
        IntegrityAutoVerify,
        
        // SBOM
        SbomGenerate,
        SbomExport,
        SbomView,
        
        // Vulnerabilidades
        VulnerabilityScan,
        VulnerabilityReport,
        VulnerabilityExport,
        
        // Autenticación
        Login,
        Logout,
        LoginFailed,
        AccountLocked,
        AccountUnlocked,
        LogoutAllSessions,
        PasswordChanged,
        PasswordChangeFailed,
        PasswordReset,
        
        // Gestión de usuarios
        UserCreated,
        UserUpdated,
        UserDeleted,
        AdminCreated,
        
        // Configuración
        ConfigChange,
        ConfigLoad,
        
        // Git
        GitCommit,
        GitPush,
        GitPull,
        
        // Certificados
        CertificateGenerate,
        CertificateRevoke,
        
        // Sistema
        SystemStart,
        SystemStop,
        ServiceStart,
        ServiceStop
    }

    /// <summary>
    /// 📋 EU CRA - Resultado de acción
    /// </summary>
    public enum AuditResult
    {
        Success,
        Warning,
        Failure,
        Error
    }

    /// <summary>
    /// 📋 EU CRA - Entrada de log de auditoría
    /// </summary>
    public class AuditLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public AuditCategory Category { get; set; }
        public AuditAction Action { get; set; }
        public AuditResult Result { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
        public string? AdditionalData { get; set; }
        public int? AffectedItemCount { get; set; }
        public double? DurationMs { get; set; }
        
        /// <summary>
        /// Firma SHA256 del contenido del log para garantizar integridad (CADRA/CRA)
        /// </summary>
        public string? Signature { get; set; }
        
        /// <summary>
        /// Hash del log anterior para crear cadena de integridad
        /// </summary>
        public string? PreviousHash { get; set; }
    }

    /// <summary>
    /// 📋 Estado del sistema de auditoría
    /// </summary>
    public class AuditLogStatus
    {
        public bool IsEnabled { get; set; }
        public int TotalEntries { get; set; }
        public DateTime? OldestEntry { get; set; }
        public DateTime? NewestEntry { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public long StorageSizeBytes { get; set; }
        public Dictionary<string, int> EntriesByCategory { get; set; } = new();
        public Dictionary<string, int> EntriesByResult { get; set; } = new();
        
        // 📋 Configuración (desde Excel)
        public int RetentionDays { get; set; } = 30;
        public bool SignatureEnabled { get; set; } = true;
        public bool ExternalEnabled { get; set; } = false;
        public string? ExternalUrl { get; set; }
        public int MaxEntriesPerFile { get; set; } = 10000;
        
        // 📋 Estado de envío externo
        public DateTime? LastExternalSendTime { get; set; }
        public int ExternalSendFailures { get; set; }
    }

    /// <summary>
    /// 📋 Query para filtrar logs
    /// </summary>
    public class AuditLogQuery
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AuditCategory? Category { get; set; }
        public AuditResult? Result { get; set; }
        public string? UserId { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 100;
    }

    /// <summary>
    /// 📋 Respuesta paginada de logs
    /// </summary>
    public class AuditLogResponse
    {
        public List<AuditLogEntry> Entries { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// 📋 Resumen de auditoría
    /// </summary>
    public class AuditSummary
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalEntries { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> ByResult { get; set; } = new();
        public Dictionary<string, int> ByDay { get; set; } = new();
        public List<AuditLogEntry> RecentFailures { get; set; } = new();
    }
}
