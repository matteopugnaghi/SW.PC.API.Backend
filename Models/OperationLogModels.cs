// ============================================================================
// OperationLogModels.cs - Modelos para Registro de Operaciones (SQLite)
// ============================================================================
// Tabla de base de datos para almacenar registros de operaciones del sistema:
// - Historial de alarmas del PLC (st_alarmHistPc)
// - Acciones de usuario (navegación, reconocimiento alarmas, recetas, etc.)
// - Eventos del sistema
// EU CRA Compliance: Trazabilidad de operaciones
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models;

/// <summary>
/// Categoría de la operación registrada
/// </summary>
public enum OperationCategory
{
    /// <summary>Navegación en la aplicación</summary>
    Navigation = 0,
    
    /// <summary>Alarmas de usuario (reconocimiento, etc.)</summary>
    Alarm = 1,
    
    /// <summary>Recetas</summary>
    Recipe = 2,
    
    /// <summary>Control de proceso</summary>
    Process = 3,
    
    /// <summary>Setpoints</summary>
    Setpoint = 4,
    
    /// <summary>Estadísticas</summary>
    Statistics = 5,
    
    /// <summary>Exportaciones</summary>
    Export = 6,
    
    /// <summary>Backup</summary>
    Backup = 7,
    
    /// <summary>Historial de alarmas PLC (st_alarmHistPc)</summary>
    PlcAlarmHistory = 10,
    
    /// <summary>Evento del sistema</summary>
    System = 20
}

/// <summary>
/// Acción específica de la operación
/// </summary>
public enum OperationAction
{
    // Navigation (0-9)
    ViewChange = 0,
    MenuOpen = 1,
    MenuClose = 2,
    
    // Alarm UI (10-19)
    AlarmView = 10,
    AlarmAcknowledge = 11,
    AlarmReset = 12,
    AlarmSilence = 13,
    AlarmExport = 14,
    
    // Recipe (20-39)
    RecipeView = 20,
    RecipeCreate = 21,
    RecipeEdit = 22,
    RecipeDelete = 23,
    RecipeLoad = 24,
    RecipeExecute = 25,
    RecipePause = 26,
    RecipeResume = 27,
    RecipeAbort = 28,
    RecipeExport = 29,
    RecipeImport = 30,
    
    // Process (40-49)
    ProcessStart = 40,
    ProcessStop = 41,
    ProcessPause = 42,
    ProcessResume = 43,
    ProcessModeChange = 44,
    CommandExecute = 45,
    
    // Setpoint (50-59)
    SetpointView = 50,
    SetpointChange = 51,
    SetpointOverride = 52,
    LimitChange = 53,
    
    // Statistics (60-69)
    StatisticsView = 60,
    StatisticsExport = 61,
    ReportGenerate = 62,
    ReportExport = 63,
    
    // Export (70-79)
    DataExport = 70,
    
    // Backup (80-89)
    BackupCreate = 80,
    BackupRestore = 81,
    BackupDelete = 82,
    
    // PLC Alarm History (100-109)
    PlcAlarmActivated = 100,
    PlcAlarmDeactivated = 101,
    PlcNotificationActivated = 102,
    PlcNotificationDeactivated = 103,
    PlcInfoActivated = 104,
    PlcInfoDeactivated = 105,
    
    // System (200+)
    SystemStartup = 200,
    SystemShutdown = 201,
    SystemError = 202,
    ConfigChange = 203
}

/// <summary>
/// Severidad del registro de operación
/// </summary>
public enum OperationSeverity
{
    /// <summary>Información normal</summary>
    Info = 0,
    
    /// <summary>Notificación (atención recomendada)</summary>
    Notice = 1,
    
    /// <summary>Advertencia</summary>
    Warning = 2,
    
    /// <summary>Error</summary>
    Error = 3,
    
    /// <summary>Crítico</summary>
    Critical = 4
}

/// <summary>
/// Registro de operación del sistema SCADA (tabla SQLite).
/// Almacena historial de alarmas PLC, acciones de usuario, eventos del sistema.
/// </summary>
[Table("OperationLogs")]
public class OperationLog
{
    /// <summary>ID único del registro</summary>
    [Key]
    public int Id { get; set; }
    
    /// <summary>Timestamp UTC del evento</summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>Categoría de la operación</summary>
    [Required]
    public OperationCategory Category { get; set; }
    
    /// <summary>Acción específica</summary>
    [Required]
    public OperationAction Action { get; set; }
    
    /// <summary>Severidad del evento</summary>
    public OperationSeverity Severity { get; set; } = OperationSeverity.Info;
    
    /// <summary>Usuario que realizó la acción (o "PLC" para alarmas automáticas)</summary>
    [MaxLength(100)]
    public string User { get; set; } = "System";
    
    /// <summary>Descripción legible del evento (idioma por defecto: español)</summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Variable PLC de origen (ej: MAIN.fbMachine.st_alarmHistPc[1].Alarm)
    /// </summary>
    [MaxLength(200)]
    public string? PlcVariable { get; set; }
    
    /// <summary>
    /// Índice de la alarma (1-based, del array st_alarmHistPc[X])
    /// </summary>
    public int? AlarmIndex { get; set; }
    
    /// <summary>
    /// Código de la alarma (ej: "ALM001", "NTF002", "INF003")
    /// </summary>
    [MaxLength(50)]
    public string? AlarmCode { get; set; }
    
    /// <summary>
    /// Tipo de alarma PLC: "Alarm", "Notification", "Info"
    /// </summary>
    [MaxLength(20)]
    public string? AlarmType { get; set; }
    
    /// <summary>
    /// Clave de acción para traducción i18n en frontend (ej: "action.process.start")
    /// Para Category != PlcAlarmHistory, el frontend traduce esta clave.
    /// Para PlcAlarmHistory, se ignora y el texto viene del Excel según AlarmCode.
    /// </summary>
    [MaxLength(100)]
    public string? ActionKey { get; set; }
    
    /// <summary>
    /// Valor anterior (para cambios de estado, setpoints, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? OldValue { get; set; }
    
    /// <summary>
    /// Valor nuevo (para cambios de estado, setpoints, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? NewValue { get; set; }
    
    /// <summary>
    /// IP del cliente (si aplica)
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// ID de sesión (para trazabilidad)
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Datos adicionales en formato JSON
    /// </summary>
    public string? DetailsJson { get; set; }
    
    /// <summary>
    /// Si el evento ha sido reconocido/confirmado
    /// </summary>
    public bool IsAcknowledged { get; set; } = false;
    
    /// <summary>
    /// Usuario que reconoció el evento
    /// </summary>
    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }
    
    /// <summary>
    /// Timestamp del reconocimiento
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
    
    // ============================================
    // Métodos de utilidad
    // ============================================
    
    /// <summary>
    /// Obtiene la clave de acción para traducciones i18n
    /// Si no hay ActionKey, genera una basada en Category y Action
    /// NOTA: Para PlcAlarmHistory NO genera clave porque el texto viene del Excel
    /// </summary>
    public string GetActionKey()
    {
        if (!string.IsNullOrEmpty(ActionKey))
            return ActionKey;
        
        // Para PlcAlarmHistory, el texto viene del Excel, no de i18n
        // Devolver vacío para que el frontend use el campo Message
        if (Category == OperationCategory.PlcAlarmHistory)
            return string.Empty;
        
        // Generar clave por defecto basada en Category.Action
        var category = Category.ToString().ToLowerInvariant();
        var action = Action.ToString().ToLowerInvariant();
        return $"operationLogs.actions.{category}.{action}";
    }
    
    /// <summary>
    /// Calcula severidad automática según la acción
    /// </summary>
    public static OperationSeverity GetSeverityFromAction(OperationAction action)
    {
        return action switch
        {
            OperationAction.PlcAlarmActivated => OperationSeverity.Error,
            OperationAction.PlcAlarmDeactivated => OperationSeverity.Notice,
            OperationAction.PlcNotificationActivated => OperationSeverity.Warning,
            OperationAction.PlcNotificationDeactivated => OperationSeverity.Info,
            OperationAction.PlcInfoActivated => OperationSeverity.Info,
            OperationAction.PlcInfoDeactivated => OperationSeverity.Info,
            OperationAction.SystemError => OperationSeverity.Critical,
            OperationAction.AlarmAcknowledge => OperationSeverity.Notice,
            _ => OperationSeverity.Info
        };
    }
}

#region DTOs para API

/// <summary>
/// DTO para respuesta de log de operación
/// </summary>
public class OperationLogDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Mensaje traducido (para PlcAlarmHistory viene del Excel, para otros está vacío)
    /// El frontend usa ActionKey para traducir si Message está vacío
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Clave i18n para traducción en frontend (ej: "operationLogs.actions.process.start")
    /// Para PlcAlarmHistory se puede ignorar ya que Message viene del Excel
    /// </summary>
    public string ActionKey { get; set; } = string.Empty;
    
    public string? PlcVariable { get; set; }
    public int? AlarmIndex { get; set; }
    public string? AlarmCode { get; set; }
    public string? AlarmType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool IsAcknowledged { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    
    /// <summary>
    /// Crea DTO desde entidad. 
    /// Para PlcAlarmHistory, el mensaje debe ser proporcionado externamente (desde Excel).
    /// </summary>
    public static OperationLogDto FromEntity(OperationLog entity, string? alarmMessage = null)
    {
        return new OperationLogDto
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            Category = entity.Category.ToString(),
            Action = entity.Action.ToString(),
            Severity = entity.Severity.ToString(),
            User = entity.User,
            Description = entity.Description,
            Message = alarmMessage, // Para PlcAlarmHistory viene del Excel
            ActionKey = entity.GetActionKey(),
            PlcVariable = entity.PlcVariable,
            AlarmIndex = entity.AlarmIndex,
            AlarmCode = entity.AlarmCode,
            AlarmType = entity.AlarmType,
            OldValue = entity.OldValue,
            NewValue = entity.NewValue,
            IsAcknowledged = entity.IsAcknowledged,
            AcknowledgedBy = entity.AcknowledgedBy,
            AcknowledgedAt = entity.AcknowledgedAt
        };
    }
}

/// <summary>
/// Filtros para consultar registros de operación
/// </summary>
public class OperationLogFilter
{
    /// <summary>Fecha inicio (UTC)</summary>
    public DateTime? FromDate { get; set; }
    
    /// <summary>Fecha fin (UTC)</summary>
    public DateTime? ToDate { get; set; }
    
    /// <summary>Filtrar por categoría</summary>
    public OperationCategory? Category { get; set; }
    
    /// <summary>Filtrar por acción</summary>
    public OperationAction? Action { get; set; }
    
    /// <summary>Filtrar por severidad mínima</summary>
    public OperationSeverity? MinSeverity { get; set; }
    
    /// <summary>Filtrar solo alarmas PLC (PlcAlarmHistory)</summary>
    public bool? OnlyPlcAlarms { get; set; }
    
    /// <summary>Filtrar por usuario</summary>
    public string? User { get; set; }
    
    /// <summary>Filtrar solo no reconocidos</summary>
    public bool? OnlyUnacknowledged { get; set; }
    
    /// <summary>Buscar en mensajes/descripción</summary>
    public string? SearchText { get; set; }
    
    /// <summary>Número de página (1-based)</summary>
    public int Page { get; set; } = 1;
    
    /// <summary>Registros por página</summary>
    public int PageSize { get; set; } = 50;
    
    /// <summary>Idioma para mensajes (SPA, ENG)</summary>
    public string Language { get; set; } = "SPA";
}

/// <summary>
/// Respuesta paginada de registros de operación
/// </summary>
public class OperationLogPagedResponse
{
    public List<OperationLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Resumen de operaciones para dashboard
/// </summary>
public class OperationLogSummary
{
    public int TotalToday { get; set; }
    public int AlarmsToday { get; set; }
    public int NotificationsToday { get; set; }
    public int InfosToday { get; set; }
    public int UnacknowledgedCount { get; set; }
    public int CriticalCount { get; set; }
    public OperationLogDto? LastAlarm { get; set; }
    public DateTime? LastUpdate { get; set; }
}

#endregion
