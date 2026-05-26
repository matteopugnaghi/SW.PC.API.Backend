// ============================================================================
// ExportResult.cs — Resultado de la ejecución de un IExportRunner
// ============================================================================
// Cada destino (local, email, futuros) produce un ExportResult independiente.
// ExportService combina los resultados para actualizar LastResult de la tarea.
// ============================================================================

namespace SW.PC.API.Backend.Models.Export;

public class ExportResult
{
    /// <summary>Destino que produjo este resultado: "local" | "email" | ...</summary>
    public string DestinationType { get; set; } = string.Empty;

    public bool Success { get; set; }

    /// <summary>Ruta final del archivo guardado (LocalFileRunner) o null.</summary>
    public string? Path { get; set; }

    /// <summary>Tamaño del archivo en bytes (cuando aplica).</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Mensaje de error legible (solo si Success=false).</summary>
    public string? ErrorMessage { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
