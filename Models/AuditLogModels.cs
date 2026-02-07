using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// 📋 EU CRA - Categorías de auditoría
    /// Incluye todas las categorías para cumplimiento CRA/CADRA
    /// </summary>
    public enum AuditCategory
    {
        // ✅ IMPLEMENTADAS
        Integrity,      // Verificación de integridad
        Sbom,           // Generación/consulta de SBOM
        Vulnerability,  // Escaneo de vulnerabilidades
        Authentication, // Login/Logout
        Configuration,  // Cambios de configuración
        Git,            // Operaciones Git
        Certificate,    // Gestión de certificados
        System,         // Eventos del sistema
        
        // 🔴 PENDIENTES - Para implementar
        Plc,            // Operaciones PLC (conexión, desconexión)
        OtCommunication, // Comunicaciones OT externas (OPC UA, Modbus, MQTT)
        Export,         // Exportación de datos de auditoría
        Backup          // Backup y restauración
        
        // ⚠️ NOTA: Categorías operativas (Alarm, Recipe, Setpoint, Process, 
        //    Statistics, Model3D, Maintenance) van a L2 Operation Log, NO aquí
    }

    /// <summary>
    /// 📋 EU CRA - Acciones auditables
    /// Incluye todas las acciones para cumplimiento CRA/CADRA
    /// ✅ = Implementada | 🔴 = Pendiente de implementar
    /// </summary>
    public enum AuditAction
    {
        // ═══════════════════════════════════════════════════════════
        // ✅ IMPLEMENTADAS - Ya registrando en AuditLog
        // ═══════════════════════════════════════════════════════════
        
        // Integridad ✅
        IntegrityVerify,
        IntegrityAutoVerify,
        
        // SBOM ✅
        SbomGenerate,
        SbomExport,
        SbomView,
        
        // Vulnerabilidades ✅
        VulnerabilityScan,
        VulnerabilityReport,
        VulnerabilityExport,
        
        // Autenticación ✅
        Login,
        Logout,
        LoginFailed,
        SupportUnlock,          // Desbloqueo de soporte Aquafrisch exitoso
        SupportUnlockFailed,    // Desbloqueo de soporte fallido (código inválido)
        AccountLocked,
        AccountUnlocked,
        LogoutAllSessions,
        PasswordChanged,
        PasswordChangeFailed,
        PasswordReset,
        
        // Gestión de usuarios ✅
        UserCreated,
        UserUpdated,
        UserDeleted,
        AdminCreated,
        UserViewed,
        UsersListed,
        PermissionDenied,
        RoleChanged,
        PermissionUpdated,  // ⭐ Actualización de permisos de rol
        Modified,           // ⭐ Modificación genérica
        
        // Configuración ✅ (parcial)
        ConfigChange,
        ConfigLoad,
        
        // Git ✅
        GitCommit,
        GitPush,
        GitPull,
        
        // Certificados ✅
        CertificateGenerate,
        CertificateRevoke,
        
        // Sistema ✅ (parcial)
        SystemStart,
        SystemStop,
        ServiceStart,
        ServiceStop,
        
        // ═══════════════════════════════════════════════════════════
        // 🔴 PENDIENTES - Para implementar con futuras funcionalidades
        // ═══════════════════════════════════════════════════════════
        
        // PLC Operations 🔴
        PlcConnect,             // Conexión a PLC
        PlcDisconnect,          // Desconexión de PLC
        PlcConnectionLost,      // Pérdida de conexión
        PlcVariableRead,        // Lectura de variable (solo si es sensible)
        PlcVariableWrite,       // Escritura de variable - CRÍTICO
        PlcModeChange,          // Cambio de modo RUN/STOP/CONFIG
        PlcProgramDownload,     // Descarga de programa al PLC
        PlcFirmwareUpdate,      // Actualización de firmware
        
        // Exportación de Datos de Auditoría 🔴
        AuditExport,            // Exportación de logs de auditoría
        AuditArchive,           // Archivado de logs antiguos
        
        // Backup/Restore 🔴
        BackupCreate,           // Creación de backup
        BackupRestore,          // Restauración de backup - CRÍTICO
        BackupSchedule,         // Programación de backup
        BackupDelete,           // Eliminación de backup
        
        // ⚠️ NOTA: Acciones operativas (Recipe*, Process*, Statistics*, Model3D*,
        //    Maintenance*) van a L2 Operation Log, NO al Audit Log L1
        
        // Comunicaciones OT/Externas 🔴 (para servicios externos de seguridad)
        ExternalApiCall,        // Llamada a API externa
        SignalRConnect,         // Conexión SignalR
        SignalRDisconnect,      // Desconexión SignalR
        
        // OPC UA 🔴 (futuro)
        OpcUaConnect,           // Conexión a servidor OPC UA
        OpcUaDisconnect,        // Desconexión de servidor OPC UA  
        OpcUaConnectionLost,    // Pérdida de conexión OPC UA
        OpcUaSubscribe,         // Suscripción a nodos OPC UA
        OpcUaWrite,             // Escritura a nodo OPC UA - CRÍTICO
        OpcUaSecurityChange,    // Cambio de política de seguridad OPC UA
        
        // Modbus TCP 🔴 (futuro)
        ModbusConnect,          // Conexión Modbus TCP
        ModbusDisconnect,       // Desconexión Modbus TCP
        ModbusConnectionLost,   // Pérdida de conexión Modbus
        ModbusWrite,            // Escritura registro Modbus - CRÍTICO
        
        // MQTT Industrial 🔴 (futuro)
        MqttConnect,            // Conexión a broker MQTT
        MqttDisconnect,         // Desconexión MQTT
        MqttConnectionLost,     // Pérdida de conexión MQTT
        MqttPublish,            // Publicación mensaje MQTT (solo si crítico)
        
        // SCADA/ERP Remoto 🔴 (futuro)
        ScadaRemoteConnect,     // Conexión a SCADA remoto
        ScadaRemoteDisconnect,  // Desconexión SCADA remoto
        ErpIntegrationSync,     // Sincronización con ERP/MES
        
        // Excel Config 🔴 (para mejor trazabilidad)
        ExcelConfigLoad,        // Carga de configuración Excel
        ExcelConfigReload,      // Recarga de configuración
        ExcelConfigError        // Error en configuración
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
