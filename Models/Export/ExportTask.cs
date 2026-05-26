// ============================================================================
// ExportTask.cs — Entidad persistente del Gestor de Exportaciones
// ============================================================================
// Cada paso por el Export Manager Wizard produce una ExportTask.
// Se almacena por proyecto (Projects/{id}/data/project.db).
//
// Tipos de ejecución soportados:
//   - "manual" (Fase 1): se dispara desde el botón ▶ del panel.
//   - "plc"    (Fase 2): se dispara por flanco false→true de PlcVariable.
//   - "cron"   (Fase 3): se evalúa por ExportSchedulerService.
//
// Destinos (CSV en `Destinations`): "local", "email" o "local,email".
// La regla AllowedExportFolders SOLO aplica cuando `Destinations` contiene "local".
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models.Export;

[Table("ExportTasks")]
public class ExportTask
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Identificador del proyecto al que pertenece la tarea (multi-proyecto).
    /// Se rellena al crear desde el contexto del request.
    /// </summary>
    [Required, MaxLength(100)]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Módulo anfitrión del ExportModal: "estadisticas", "mantenimiento",
    /// "alarmas", etc. Permite filtrar tareas por modal.
    /// </summary>
    [Required, MaxLength(50)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo escrito por el usuario.</summary>
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"manual" | "plc" | "cron"</summary>
    [Required, MaxLength(10)]
    public string ExecutionType { get; set; } = "manual";

    /// <summary>Expresión cron (solo si ExecutionType="cron"). Fase 3.</summary>
    [MaxLength(100)]
    public string? CronExpression { get; set; }

    /// <summary>Variable PLC booleana (solo si ExecutionType="plc"). Fase 2.</summary>
    [MaxLength(200)]
    public string? PlcVariable { get; set; }

    /// <summary>Formato de salida: "xlsx" | "csv" | "json" | "html" | "png".</summary>
    [Required, MaxLength(10)]
    public string Format { get; set; } = "xlsx";

    /// <summary>
    /// CSV de destinos activos: "local", "email" o "local,email".
    /// Al menos uno (validado en controller).
    /// </summary>
    [Required, MaxLength(50)]
    public string Destinations { get; set; } = string.Empty;

    /// <summary>
    /// JSON con la configuración de los destinos activos:
    /// {
    ///   "filename": "informe_{fecha}.xlsx",
    ///   "folder": "C:\\exports\\aquafrisch",        (solo si destinations incluye "local")
    ///   "email": { "to":[], "cc":[], "cco":[], "subject":"", "body":"" }  (solo si "email")
    /// }
    /// </summary>
    [Required]
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// DatasetId del provider que resuelve los datos al ejecutar.
    /// p.ej. "estadisticas.tabla-ciclos".
    /// </summary>
    [Required, MaxLength(100)]
    public string DatasetProvider { get; set; } = string.Empty;

    /// <summary>
    /// JSON con la selección del usuario (campos + filtros):
    /// { "fields": ["fecha","producto"], "filters": { "rangoFechas": {...}, "soloErrores": false } }
    /// Se reaplica idéntico en cada ejecución (manual/cron/plc) → tareas reproducibles.
    /// </summary>
    [Required]
    public string SelectionJson { get; set; } = "{}";

    /// <summary>Tarea activa (false = pausada, no se ejecuta automáticamente).</summary>
    public bool Enabled { get; set; } = true;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastRunAt { get; set; }

    /// <summary>"ok" | "ok (parcial)" | "error: ..." | null si nunca se ejecutó.</summary>
    [MaxLength(500)]
    public string? LastResult { get; set; }

    /// <summary>
    /// Último valor leído de PlcVariable (solo si ExecutionType="plc").
    /// Persistido para detectar flanco false→true correctamente tras reinicio
    /// del backend (evita disparar la tarea con el primer sample post-reboot).
    /// null = nunca leído; 0/1 = último estado conocido.
    /// </summary>
    public bool? PlcLastValue { get; set; }

    /// <summary>
    /// Id del perfil de carpeta (FK lógica a ExportFolderProfiles.Id).
    /// Sólo se usa cuando `Destinations` contiene "local". Si null, se cae al
    /// valor `Folder` heredado en `ConfigJson` (compatibilidad hacia atrás).
    /// </summary>
    [MaxLength(40)]
    public string? FolderProfileId { get; set; }

    /// <summary>
    /// Id del perfil SMTP (FK lógica a ExportEmailProfiles.Id).
    /// Sólo se usa cuando `Destinations` contiene "email". Si null, se cae a
    /// la configuración SMTP del Excel (compatibilidad hacia atrás).
    /// </summary>
    [MaxLength(40)]
    public string? EmailProfileId { get; set; }

    /// <summary>
    /// Destinatarios específicos de esta tarea, CSV separado por coma o ';'.
    /// Si null/empty se usan DefaultRecipients del EmailProfile.
    /// </summary>
    [MaxLength(1000)]
    public string? EmailRecipients { get; set; }
}
