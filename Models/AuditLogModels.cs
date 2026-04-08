using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// 📋 EU CRA - Categorías de auditoría L1
    /// Solo categorías implementadas y en uso
    /// </summary>
    public enum AuditCategory
    {
        // ✅ IMPLEMENTADAS
        Authentication, // Login/Logout/Password
        Integrity,      // Verificación de integridad del software
        Sbom,           // Generación/exportación de SBOM
        Vulnerability,  // Escaneo de vulnerabilidades
        Git,            // Operaciones Git (commit, push, release)
        Certificate,    // Certificados de integridad
        Security,       // SSH signing, permisos de roles
        Backup,         // Backup y restauración
        Plc,            // Conexión/desconexión PLC
        System,         // Acciones del sistema (restart, TeamViewer)
        Export,         // Exportación de datos de auditoría
        
        // ✅ OT COMMUNICATION (OPC UA, Modbus, MQTT)
        OtCommunication
    }

    /// <summary>
    /// 📋 EU CRA - Acciones auditables L1
    /// Solo acciones implementadas y en uso
    /// </summary>
    public enum AuditAction
    {
        // ═══════════════════════════════════════════════════════════
        // AUTENTICACIÓN
        // ═══════════════════════════════════════════════════════════
        Login,
        Logout,
        LoginFailed,
        LogoutAllSessions,
        AccountLocked,
        AccountUnlocked,
        PasswordChanged,
        PasswordChangeFailed,
        PasswordReset,
        SupportUnlock,
        SupportUnlockFailed,
        
        // ═══════════════════════════════════════════════════════════
        // GESTIÓN DE USUARIOS
        // ═══════════════════════════════════════════════════════════
        UserCreated,
        UserUpdated,
        UserDeleted,
        AdminCreated,
        RoleChanged,
        PermissionUpdated,
        PermissionDenied,
        
        // ═══════════════════════════════════════════════════════════
        // INTEGRIDAD Y SBOM
        // ═══════════════════════════════════════════════════════════
        IntegrityVerify,
        IntegrityAutoVerify,
        SbomGenerate,
        SbomExport,
        VulnerabilityScan,
        
        // ═══════════════════════════════════════════════════════════
        // CERTIFICADOS
        // ═══════════════════════════════════════════════════════════
        CertificateGenerate,
        CertificateDownload,
        CertificateVerify,
        
        // ═══════════════════════════════════════════════════════════
        // SSH SIGNING (EU CRA - Firma de código)
        // ═══════════════════════════════════════════════════════════
        SshKeyGenerate,
        SshKeyDelete,
        SshKeyExport,
        SshKeyImport,
        SshSigningEnable,
        SshSigningDisable,
        SshKeyAuthorize,
        SshKeyRevoke,
        
        // ═══════════════════════════════════════════════════════════
        // GIT
        // ═══════════════════════════════════════════════════════════
        GitCommit,
        GitPush,
        GitBackupExport,
        GitRelease,
        GitDiscard,
        GitRevert,
        GitAccessControl,
        
        // ═══════════════════════════════════════════════════════════
        // CONFIGURACIÓN
        // ═══════════════════════════════════════════════════════════
        ConfigChange,
        
        // ═══════════════════════════════════════════════════════════
        // BACKUP
        // ═══════════════════════════════════════════════════════════
        BackupCreate,
        BackupRestore,
        BackupDelete,
        
        // ═══════════════════════════════════════════════════════════
        // PLC
        // ═══════════════════════════════════════════════════════════
        PlcConnect,
        PlcDisconnect,
        
        // ═══════════════════════════════════════════════════════════
        // SISTEMA
        // ═══════════════════════════════════════════════════════════
        SystemStart,    // Inicio de aplicación (Program.cs)
        SystemStop,     // Detención de aplicación (Program.cs)
        ServiceStart,   // TeamViewer, restart-app, custom-tool
        ServiceStop,    // Detención de servicio (TeamViewer, etc.)
        
        // ═══════════════════════════════════════════════════════════
        // EXPORTACIÓN
        // ═══════════════════════════════════════════════════════════
        AuditExport,            // Exportación de logs de auditoría
        AlarmHistoryExport,     // Exportación de histórico de alarmas
        OperationLogExport,     // Exportación de logs de operación (L2)
        StatisticsExport,       // Exportación de estadísticas
        RecipeExport,           // Exportación de recetas
        ConfigurationExport,    // Exportación de configuraciones
        
        // ═══════════════════════════════════════════════════════════
        // OPC/UA (OT COMMUNICATION)
        // ═══════════════════════════════════════════════════════════
        OpcUaServerStart,
        OpcUaServerStop,
        OpcUaClientConnect,
        OpcUaClientDisconnect,
        OpcUaNodeRead,          // ⚠️ DEPRECATED in L1 - Variable operations solo en L2 (OperationLog)
        OpcUaNodeWrite,         // ⚠️ DEPRECATED in L1 - Variable operations solo en L2 (OperationLog)
        OpcUaValueChange,       // ⚠️ DEPRECATED in L1 - Variable status solo en L2 (OperationLog)
        OpcUaAlarmChange,       // ⚠️ DEPRECATED in L1 - Alarm status solo en L2 (OperationLog)
        OpcUaSubscriptionCreate,
        OpcUaSubscriptionDelete,
        OpcUaSecurityReject,
        
        // ═══════════════════════════════════════════════════════════
        // OPC/UA CERTIFICATE MANAGEMENT (Phase 1 - Manual .DER exchange)
        // ═══════════════════════════════════════════════════════════
        CertificateImport,      // Trusted certificate imported
        CertificateRemove,      // Certificate removed from store
        CertificateApprove,     // Rejected certificate approved → trusted

        // ═══════════════════════════════════════════════════════════
        // OPC/UA SFTP CERTIFICATE SYNC (Phase 2 - Automatic exchange)
        // ═══════════════════════════════════════════════════════════
        SftpSync,               // SFTP certificate sync operation
        OpcUaConfigWarning      // Excel configuration error/warning
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
        public DateTime Timestamp { get; set; } = DateTime.Now;
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
        /// ID del proyecto donde se guardará este log (interno, no se serializa al JSON)
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string? TargetProjectId { get; set; }
        
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
        public int TodayEntries { get; set; }
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
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int TotalEntries { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> ByResult { get; set; } = new();
        public Dictionary<string, int> ByDay { get; set; } = new();
        public List<AuditLogEntry> RecentFailures { get; set; } = new();
    }
}
