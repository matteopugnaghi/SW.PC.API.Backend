// ==================================================================
// Models/BackupModels.cs
// DATA MANAGEMENT - Modelos para Sistema de Backup/Restore
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Información básica de un backup
    /// </summary>
    public class BackupInfo
    {
        /// <summary>Identificador único del backup (formato: backup_{projectId}_{timestamp})</summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>ID del proyecto al que pertenece</summary>
        public string ProjectId { get; set; } = string.Empty;
        
        /// <summary>Nombre descriptivo del backup</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Descripción opcional del backup</summary>
        public string? Description { get; set; }
        
        /// <summary>Fecha y hora de creación (UTC)</summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>Usuario que creó el backup</summary>
        public string CreatedBy { get; set; } = "system";
        
        /// <summary>Tipo de backup: manual, scheduled, pre-update</summary>
        public BackupType Type { get; set; } = BackupType.Manual;
        
        /// <summary>Tamaño del backup en bytes</summary>
        public long SizeBytes { get; set; }
        
        /// <summary>Tamaño formateado (ej: "15.2 MB")</summary>
        public string SizeFormatted => FormatSize(SizeBytes);
        
        /// <summary>Ruta completa del archivo ZIP</summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>Indica si el backup está firmado con certificado</summary>
        public bool IsSigned { get; set; }
        
        /// <summary>Estado de verificación del certificado</summary>
        public CertificateStatus CertificateStatus { get; set; } = CertificateStatus.NotSigned;
        
        /// <summary>Hash SHA256 del backup completo</summary>
        public string? Hash { get; set; }
        
        /// <summary>Versión de la aplicación cuando se creó el backup</summary>
        public string? AppVersion { get; set; }
        
        /// <summary>Contenido del backup</summary>
        public BackupContents Contents { get; set; } = new();

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Tipo de backup
    /// </summary>
    public enum BackupType
    {
        /// <summary>Backup manual creado por usuario</summary>
        Manual,
        /// <summary>Backup automático programado</summary>
        Scheduled,
        /// <summary>Backup automático antes de actualización</summary>
        PreUpdate,
        /// <summary>Backup antes de restauración</summary>
        PreRestore,
        /// <summary>Backup importado desde archivo externo</summary>
        Imported
    }

    /// <summary>
    /// Estado del certificado de backup
    /// </summary>
    public enum CertificateStatus
    {
        /// <summary>Backup no firmado</summary>
        NotSigned,
        /// <summary>Certificado válido y verificado</summary>
        Valid,
        /// <summary>Certificado inválido o manipulado</summary>
        Invalid,
        /// <summary>Certificado expirado</summary>
        Expired,
        /// <summary>Error al verificar</summary>
        Error
    }

    /// <summary>
    /// Contenido incluido en el backup
    /// </summary>
    public class BackupContents
    {
        /// <summary>Incluye configuración Excel</summary>
        public bool HasConfig { get; set; }
        
        /// <summary>Incluye modelos 3D</summary>
        public bool HasModels { get; set; }
        
        /// <summary>Incluye base de datos</summary>
        public bool HasDatabase { get; set; }
        
        /// <summary>Incluye repositorio TwinCAT PLC</summary>
        public bool HasTwinCAT { get; set; }
        
        /// <summary>Número de archivos de configuración</summary>
        public int ConfigFilesCount { get; set; }
        
        /// <summary>Número de modelos 3D</summary>
        public int ModelsCount { get; set; }
        
        /// <summary>Tamaño de la base de datos</summary>
        public long DatabaseSizeBytes { get; set; }
        
        /// <summary>Número de archivos TwinCAT</summary>
        public int TwinCatFilesCount { get; set; }
        
        /// <summary>Incluye documentación DMS</summary>
        public bool HasDocs { get; set; }
        
        /// <summary>Número de archivos de documentación</summary>
        public int DocsFilesCount { get; set; }
    }

    /// <summary>
    /// Manifest del backup (incluido en el ZIP)
    /// </summary>
    public class BackupManifest
    {
        /// <summary>Versión del formato de manifest</summary>
        public string ManifestVersion { get; set; } = "1.0";
        
        /// <summary>Información del backup</summary>
        public BackupInfo BackupInfo { get; set; } = new();
        
        /// <summary>Lista de archivos con sus checksums</summary>
        public List<BackupFileEntry> Files { get; set; } = new();
        
        /// <summary>Metadata adicional</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
        
        /// <summary>Timestamp de creación del manifest</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Entrada de archivo en el manifest
    /// </summary>
    public class BackupFileEntry
    {
        /// <summary>Ruta relativa dentro del ZIP</summary>
        public string RelativePath { get; set; } = string.Empty;
        
        /// <summary>Hash SHA256 del archivo</summary>
        public string Hash { get; set; } = string.Empty;
        
        /// <summary>Tamaño en bytes</summary>
        public long SizeBytes { get; set; }
        
        /// <summary>Fecha de última modificación</summary>
        public DateTime ModifiedAt { get; set; }
    }

    /// <summary>
    /// Certificado de firma del backup (CRA Compliance)
    /// </summary>
    public class BackupCertificate
    {
        /// <summary>Versión del formato de certificado</summary>
        public string CertificateVersion { get; set; } = "1.0";
        
        /// <summary>ID del backup al que pertenece</summary>
        public string BackupId { get; set; } = string.Empty;
        
        /// <summary>Hash SHA256 del manifest</summary>
        public string ManifestHash { get; set; } = string.Empty;
        
        /// <summary>Hash SHA256 de todos los archivos combinados</summary>
        public string ContentHash { get; set; } = string.Empty;
        
        /// <summary>Firma combinada (ManifestHash + ContentHash)</summary>
        public string Signature { get; set; } = string.Empty;
        
        /// <summary>Timestamp de firma (UTC)</summary>
        public DateTime SignedAt { get; set; }
        
        /// <summary>Identificador del sistema que firmó</summary>
        public string SignedBy { get; set; } = string.Empty;
        
        /// <summary>Hash del certificado anterior (cadena de integridad)</summary>
        public string? PreviousCertificateHash { get; set; }
        
        /// <summary>Número de secuencia en la cadena</summary>
        public int SequenceNumber { get; set; }
        
        /// <summary>Metadata de cumplimiento</summary>
        public ComplianceMetadata Compliance { get; set; } = new();
    }

    /// <summary>
    /// Metadata de cumplimiento normativo
    /// </summary>
    public class ComplianceMetadata
    {
        /// <summary>Estándar de cumplimiento</summary>
        public string Standard { get; set; } = "EU-CRA-2024";
        
        /// <summary>Referencia de requisito</summary>
        public string Requirement { get; set; } = "Anexo I, Parte I, 2f";
        
        /// <summary>Descripción del requisito</summary>
        public string Description { get; set; } = "Protección de integridad de datos almacenados, transmitidos o procesados";
        
        /// <summary>Algoritmo de hash utilizado</summary>
        public string HashAlgorithm { get; set; } = "SHA256";
    }

    /// <summary>
    /// Configuración de backup desde Excel
    /// </summary>
    public class BackupConfig
    {
        /// <summary>Backup habilitado</summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>Intervalo de backup automático en horas (0 = deshabilitado)</summary>
        public int IntervalHours { get; set; } = 24;
        
        /// <summary>Días de retención de backups (0 = sin límite)</summary>
        public int RetentionDays { get; set; } = 30;
        
        /// <summary>Firma de backups habilitada</summary>
        public bool SignEnabled { get; set; } = true;
        
        /// <summary>Backup remoto habilitado</summary>
        public bool RemoteEnabled { get; set; } = false;
        
        /// <summary>URL del servidor remoto de backup</summary>
        public string? RemoteUrl { get; set; }
        
        /// <summary>API Key para servidor remoto</summary>
        public string? RemoteApiKey { get; set; }
        
        /// <summary>Crear backup antes de restauración</summary>
        public bool BackupBeforeRestore { get; set; } = true;
        
        /// <summary>Máximo número de backups a mantener (0 = sin límite)</summary>
        public int MaxBackups { get; set; } = 10;
    }

    // ==================== DTOs para API ====================

    /// <summary>
    /// Request para crear backup
    /// </summary>
    public class CreateBackupRequest
    {
        /// <summary>Nombre opcional del backup</summary>
        public string? Name { get; set; }
        
        /// <summary>Descripción opcional</summary>
        public string? Description { get; set; }
        
        /// <summary>Incluir configuración Excel</summary>
        public bool IncludeConfig { get; set; } = true;
        
        /// <summary>Incluir modelos 3D</summary>
        public bool IncludeModels { get; set; } = true;
        
        /// <summary>Incluir base de datos</summary>
        public bool IncludeDatabase { get; set; } = true;
        
        /// <summary>Incluir repositorio TwinCAT PLC</summary>
        public bool IncludeTwinCAT { get; set; } = true;
        
        /// <summary>Incluir documentación DMS</summary>
        public bool IncludeDocs { get; set; } = true;
        
        /// <summary>Tipo de backup (Manual, Scheduled, PreUpdate, PreRestore). Default: Manual</summary>
        public BackupType Type { get; set; } = BackupType.Manual;
    }

    /// <summary>
    /// Request para restaurar backup
    /// </summary>
    public class RestoreBackupRequest
    {
        /// <summary>ID del backup a restaurar</summary>
        public string BackupId { get; set; } = string.Empty;
        
        /// <summary>Crear backup antes de restaurar</summary>
        public bool CreateBackupFirst { get; set; } = true;
        
        /// <summary>Restaurar configuración</summary>
        public bool RestoreConfig { get; set; } = true;
        
        /// <summary>Restaurar modelos 3D</summary>
        public bool RestoreModels { get; set; } = true;
        
        /// <summary>Restaurar base de datos</summary>
        public bool RestoreDatabase { get; set; } = true;
        
        /// <summary>Restaurar repositorio TwinCAT PLC</summary>
        public bool RestoreTwinCAT { get; set; } = true;
        
        /// <summary>Restaurar documentación DMS</summary>
        public bool RestoreDocs { get; set; } = true;
    }

    /// <summary>
    /// Response de operación de backup
    /// </summary>
    public class BackupOperationResponse
    {
        /// <summary>Operación exitosa</summary>
        public bool Success { get; set; }
        
        /// <summary>Mensaje descriptivo</summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>Información del backup (si aplica)</summary>
        public BackupInfo? BackupInfo { get; set; }
        
        /// <summary>Errores encontrados</summary>
        public List<string> Errors { get; set; } = new();
        
        /// <summary>Advertencias</summary>
        public List<string> Warnings { get; set; } = new();
        
        /// <summary>Timestamp de la operación</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Response de lista de backups
    /// </summary>
    public class BackupListResponse
    {
        /// <summary>Lista de backups</summary>
        public List<BackupInfo> Backups { get; set; } = new();
        
        /// <summary>Total de backups</summary>
        public int TotalCount { get; set; }
        
        /// <summary>Espacio total utilizado</summary>
        public long TotalSizeBytes { get; set; }
        
        /// <summary>Espacio formateado</summary>
        public string TotalSizeFormatted => FormatSize(TotalSizeBytes);
        
        /// <summary>Configuración actual de backup</summary>
        public BackupConfig Config { get; set; } = new();

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Response de verificación de backup
    /// </summary>
    public class BackupVerificationResponse
    {
        /// <summary>Backup válido</summary>
        public bool IsValid { get; set; }
        
        /// <summary>Estado del certificado</summary>
        public CertificateStatus CertificateStatus { get; set; }
        
        /// <summary>Detalles de verificación</summary>
        public List<VerificationDetail> Details { get; set; } = new();
        
        /// <summary>Timestamp de verificación</summary>
        public DateTime VerifiedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Detalle de verificación
    /// </summary>
    public class VerificationDetail
    {
        /// <summary>Componente verificado</summary>
        public string Component { get; set; } = string.Empty;
        
        /// <summary>Verificación exitosa</summary>
        public bool IsValid { get; set; }
        
        /// <summary>Hash esperado</summary>
        public string? ExpectedHash { get; set; }
        
        /// <summary>Hash actual</summary>
        public string? ActualHash { get; set; }
        
        /// <summary>Mensaje</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Estado del sistema de backup
    /// </summary>
    public class BackupSystemStatus
    {
        /// <summary>Sistema habilitado</summary>
        public bool Enabled { get; set; }
        
        /// <summary>Último backup</summary>
        public BackupInfo? LastBackup { get; set; }
        
        /// <summary>Próximo backup programado</summary>
        public DateTime? NextScheduledBackup { get; set; }
        
        /// <summary>Total de backups</summary>
        public int TotalBackups { get; set; }
        
        /// <summary>Espacio utilizado</summary>
        public long UsedSpaceBytes { get; set; }
        
        /// <summary>Configuración actual</summary>
        public BackupConfig Config { get; set; } = new();
        
        /// <summary>Estado de salud del sistema</summary>
        public string HealthStatus { get; set; } = "OK";
        
        /// <summary>Mensajes de estado</summary>
        public List<string> StatusMessages { get; set; } = new();
    }
}
