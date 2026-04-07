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
    /// <summary>Recetas</summary>
    Recipe = 2,
    
    /// <summary>Control de proceso</summary>
    Process = 3,
    
    /// <summary>Estadísticas</summary>
    Statistics = 5,
    
    /// <summary>Configuración de máquina</summary>
    Configuration = 8,
    
    /// <summary>Órdenes/comandos recibidos desde el PLC (cambio lavado automático, etc.)</summary>
    PlcCommand = 9,
    
    /// <summary>Historial de alarmas PLC (st_alarmHistPc)</summary>
    PlcAlarmHistory = 10,

    /// <summary>Comunicación OPC/UA (escrituras, lecturas, cambios)</summary>
    OpcUa = 11
}

/// <summary>
/// Acción específica de la operación
/// </summary>
public enum OperationAction
{
    // Recipe / Wash Types (30-39)
    WashTypeCreate = 33,        // Crear tipo de lavado
    WashTypeEdit = 34,          // Editar tipo de lavado
    WashTypeDelete = 35,        // Eliminar tipo de lavado
    WashTypeWritePlc = 36,      // Escribir tipo de lavado al PLC (desde lista)
    TrainTypeCreate = 37,       // Crear tipo de tren
    TrainTypeEdit = 38,         // Editar tipo de tren
    TrainTypeDelete = 39,       // Eliminar tipo de tren
    
    // Process (40-49) - Modos manuales/semiautomáticos
    SemiautomaticToggle = 46,   // Toggle elemento en panel semiauto
    ManualModeToggle = 47,      // Toggle elemento en modo manual
    
    // Statistics (60-69)
    StatisticsView = 60,
    StatisticsExport = 61,
    ReportGenerate = 62,
    ReportExport = 63,
    
    // Configuration / Machine Settings (90-99)
    ConfigChange = 91,          // Cambio de configuración de máquina
    ConfigWritePlc = 92,        // Escribir configuración al PLC
    FastConfigWritePlc = 95,    // Escribir configuración rápida al PLC
    FastConfigChange = 96,      // Cambio de configuración rápida
    
    // PLC Alarm History (100-109)
    PlcAlarmActivated = 100,
    PlcAlarmDeactivated = 101,
    PlcNotificationActivated = 102,
    PlcNotificationDeactivated = 103,
    PlcInfoActivated = 104,
    PlcInfoDeactivated = 105,
    
    // PLC Commands - Órdenes recibidas desde el PLC (110-119)
    PlcCommandWashChange = 110,      // Cambio de lavado desde PLC (automático)
    PlcCommandTrainChange = 111,     // Cambio de tipo de tren desde PLC (automático)
    
    // PLC Messages - Logs recibidos desde el PLC (120)
    PlcLogReceived = 120,            // Log recibido desde PLC
    
    // Train Type specific (130-139)
    TrainTypeLoad = 130,             // Seleccionar tipo de tren
    TrainTypeWritePlc = 131,         // Escribir tipo de tren al PLC (desde lista)
    TrainTypeSaveFromPlc = 133,      // Guardar tipo de tren en DB desde PLC
    TrainTypeInterpolationWrite = 134, // Escribir tabla de interpolación al PLC
    TrainTypeWritePlcFromEditor = 135, // Escribir tipo de tren al PLC (desde editor)
    
    // Wash Type specific (140-149)
    WashTypeLoad = 140,              // Seleccionar tipo de lavado
    WashTypeSaveFromPlc = 142,       // Guardar tipo de lavado en DB desde PLC
    WashTypeWritePlcFromEditor = 143, // Escribir tipo de lavado al PLC (desde editor)
    
    // OPC/UA (150-159)
    OpcUaNodeWrite = 150,             // Escritura desde cliente OPC UA → ADS
    OpcUaValueChange = 151,           // Cambio de valor ADS → OPC UA
    OpcUaAlarmChange = 152,           // Cambio de estado de alarma OPC UA
    OpcUaClientConnect = 153,         // Cliente OPC UA conectado
    OpcUaClientDisconnect = 154,      // Cliente OPC UA desconectado
    
    // ❌ System (200+) - ELIMINADO: Los eventos de sistema (Startup, Shutdown, Error)
    //    ya se registran en L1 (Audit Log) con AuditAction.SystemStart/SystemStop
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
    
    /// <summary>
    /// Datos adicionales (JSON deserializado)
    /// Para Manual/Semiautomático incluye: PlcVariable, ElementId, Value
    /// </summary>
    public object? Details { get; set; }
    
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
        // Deserializar DetailsJson si existe
        object? details = null;
        if (!string.IsNullOrEmpty(entity.DetailsJson))
        {
            try
            {
                details = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(entity.DetailsJson);
            }
            catch
            {
                // Si falla, devolver el JSON como string
                details = entity.DetailsJson;
            }
        }
        
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
            Details = details, // JSON deserializado
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
