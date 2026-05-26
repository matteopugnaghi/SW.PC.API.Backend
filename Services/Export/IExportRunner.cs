// ============================================================================
// IExportRunner.cs — Contrato genérico para cada destino de exportación
// ============================================================================
// Fase 1: LocalFileRunner ("local") y EmailRunner ("email").
// ExportService invoca un runner por cada destino activo en ExportTask.Destinations.
// Cada runner produce un ExportResult independiente.
// ============================================================================

using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportRunner
{
    /// <summary>Identificador del destino: "local" | "email" | (futuros).</summary>
    string DestinationType { get; }

    Task<ExportResult> ExecuteAsync(ExportRunContext context, CancellationToken ct = default);
}

/// <summary>
/// Contexto inmutable preparado por ExportService antes de invocar a cada runner.
/// </summary>
public class ExportRunContext
{
    public ExportTask Task { get; init; } = null!;
    public ExportConfig Config { get; init; } = null!;
    public FormattedExport File { get; init; } = null!;

    /// <summary>Nombre final del archivo (con tokens ya resueltos + extensión).</summary>
    public string Filename { get; init; } = string.Empty;

    /// <summary>
    /// Lista de carpetas autorizadas para escritura local (Excel SystemConfig).
    /// Solo usada por LocalFileRunner. Si está vacía y el destino es local → 400.
    /// </summary>
    public IReadOnlyList<string> AllowedFolders { get; init; } = Array.Empty<string>();

    /// <summary>Configuración SMTP. Si null o no configurada y destino es email → 400.</summary>
    public ExportSmtpSettings? Smtp { get; init; }
}
