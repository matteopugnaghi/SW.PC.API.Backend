// ============================================================================
// ExportTaskDtos.cs — DTOs de entrada/salida del ExportTasksController
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models.Export;

/// <summary>Payload de POST /api/export/tasks (crear) y PUT /api/export/tasks/{id}.</summary>
public class ExportTaskRequest
{
    [Required, MaxLength(50)]
    public string Source { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string ExecutionType { get; set; } = "manual";

    [MaxLength(100)]
    public string? CronExpression { get; set; }

    [MaxLength(200)]
    public string? PlcVariable { get; set; }

    [Required, MaxLength(10)]
    public string Format { get; set; } = "xlsx";

    /// <summary>Array de destinos: ["local","email"]. Se serializa a CSV en la entidad.</summary>
    [Required]
    public List<string> Destinations { get; set; } = new();

    [Required, MaxLength(100)]
    public string DatasetProvider { get; set; } = string.Empty;

    /// <summary>Configuración por destino. Se serializa a JSON.</summary>
    [Required]
    public ExportConfig Config { get; set; } = new();

    /// <summary>Selección de campos + filtros. Se serializa a JSON.</summary>
    [Required]
    public ExportSelection Selection { get; set; } = new();

    public bool Enabled { get; set; } = true;

    /// <summary>Id de perfil de carpeta (si destinos incluye "local").</summary>
    [MaxLength(40)] public string? FolderProfileId { get; set; }

    /// <summary>Id de perfil SMTP (si destinos incluye "email").</summary>
    [MaxLength(40)] public string? EmailProfileId { get; set; }

    /// <summary>Destinatarios específicos (CSV). Si vacío usa DefaultRecipients del perfil.</summary>
    [MaxLength(1000)] public string? EmailRecipients { get; set; }
}

/// <summary>Payload de POST /api/export/preview.</summary>
public class ExportPreviewRequest
{
    [Required]
    public string DatasetProvider { get; set; } = string.Empty;

    [Required]
    public ExportSelection Selection { get; set; } = new();
}

/// <summary>Respuesta resumida de una ExportTask (no expone JSON crudos).</summary>
public class ExportTaskResponse
{
    public int Id { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExecutionType { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public string? PlcVariable { get; set; }
    public string Format { get; set; } = string.Empty;
    public List<string> Destinations { get; set; } = new();
    public string DatasetProvider { get; set; } = string.Empty;
    public ExportConfig Config { get; set; } = new();
    public ExportSelection Selection { get; set; } = new();
    public bool Enabled { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastResult { get; set; }
    public string? FolderProfileId { get; set; }
    public string? EmailProfileId { get; set; }
    public string? EmailRecipients { get; set; }
}

/// <summary>Respuesta de POST /api/export/tasks/{id}/run.</summary>
public class ExportRunResponse
{
    public int TaskId { get; set; }
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<ExportResult> Results { get; set; } = new();
}
