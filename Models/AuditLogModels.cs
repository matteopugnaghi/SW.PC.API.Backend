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
        
        // 🔴 PENDIENTES - Para implementar con futuras funcionalidades
        Plc,            // Operaciones PLC (conexión, escritura, modo)
        Alarm,          // Gestión de alarmas (reconocimiento, silencio)
        Recipe,         // Gestión de recetas (carga, ejecución, guardado)
        Setpoint,       // Cambios de consignas/setpoints
        Process,        // Acciones de proceso (start, stop, modo)
        Statistics,     // Acceso a estadísticas/reportes
        Export,         // Exportación de datos
        Backup,         // Backup y restauración
        Model3D,        // Carga/configuración de modelos 3D
        Maintenance     // Acciones de mantenimiento
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
        
        // Alarmas 🔴
        AlarmTriggered,         // Alarma activada (para log, no audit)
        AlarmAcknowledge,       // Reconocimiento de alarma - CRÍTICO
        AlarmReset,             // Reset de alarma
        AlarmSilence,           // Silenciar alarmas
        AlarmSilenceEnd,        // Fin de silencio
        AlarmConfigChange,      // Cambio en configuración de alarmas
        AlarmHistoryExport,     // Exportación de histórico
        
        // Recetas 🔴
        RecipeCreate,           // Crear receta
        RecipeUpdate,           // Modificar receta
        RecipeDelete,           // Eliminar receta
        RecipeLoad,             // Cargar receta en máquina - CRÍTICO
        RecipeUnload,           // Descargar receta
        RecipeExecute,          // Ejecutar/iniciar receta - CRÍTICO
        RecipePause,            // Pausar receta
        RecipeResume,           // Reanudar receta
        RecipeAbort,            // Abortar receta - CRÍTICO
        RecipeComplete,         // Receta completada
        RecipeExport,           // Exportar receta
        RecipeImport,           // Importar receta
        
        // Setpoints/Consignas 🔴
        SetpointChange,         // Cambio de setpoint - CRÍTICO
        SetpointOverride,       // Override manual de setpoint
        SetpointReset,          // Reset a valor default
        LimitChange,            // Cambio de límites
        
        // Control de Proceso 🔴
        ProcessStart,           // Arranque de proceso
        ProcessStop,            // Parada de proceso
        ProcessPause,           // Pausa de proceso
        ProcessResume,          // Reanudación de proceso
        ProcessEmergencyStop,   // Parada de emergencia - CRÍTICO
        ProcessModeChange,      // Cambio AUTO/MANUAL/etc
        ProcessPhaseChange,     // Cambio de fase
        CommandExecute,         // Ejecución de comando manual
        
        // Estadísticas/Reportes 🔴
        StatisticsView,         // Visualización de estadísticas
        StatisticsExport,       // Exportación de estadísticas
        ReportGenerate,         // Generación de reporte
        ReportExport,           // Exportación de reporte
        ReportSchedule,         // Programación de reporte
        
        // Exportación de Datos 🔴
        DataExport,             // Exportación genérica
        DataExportScheduled,    // Exportación programada
        TrendExport,            // Exportación de tendencias
        HistorianQuery,         // Consulta a histórico
        
        // Backup/Restore 🔴
        BackupCreate,           // Creación de backup
        BackupRestore,          // Restauración de backup - CRÍTICO
        BackupSchedule,         // Programación de backup
        BackupDelete,           // Eliminación de backup
        
        // Modelos 3D 🔴
        Model3DLoad,            // Carga de modelo 3D
        Model3DConfigChange,    // Cambio de configuración de modelo
        Model3DBindingChange,   // Cambio de binding PLC
        
        // Mantenimiento 🔴
        MaintenanceStart,       // Inicio de mantenimiento
        MaintenanceEnd,         // Fin de mantenimiento
        MaintenanceSchedule,    // Programación de mantenimiento
        CalibrationStart,       // Inicio de calibración
        CalibrationComplete,    // Calibración completada
        
        // Comunicaciones 🔴
        ExternalApiCall,        // Llamada a API externa
        SignalRConnect,         // Conexión SignalR
        SignalRDisconnect,      // Desconexión SignalR
        
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
