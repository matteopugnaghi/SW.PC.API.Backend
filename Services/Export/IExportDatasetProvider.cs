// ============================================================================
// IExportDatasetProvider.cs — Contrato para cada origen de datos exportable
// ============================================================================
// Cada módulo anfitrión del ExportModal (auditoría, alarmas, estadísticas,
// mantenimiento…) registra uno o más providers. Cada provider:
//   - Declara qué campos y filtros expone (consumido por el wizard).
//   - Resuelve los datos cuando una ExportTask se ejecuta.
//
// Providers son SCOPED en DI (necesitan IRequestProjectContext, DbContext, etc.).
// IExportDatasetRegistry los indexa por DatasetId.
// ============================================================================

using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportDatasetProvider
{
    /// <summary>Identificador único global del dataset. Ej: "auditoria.logs".</summary>
    string DatasetId { get; }

    /// <summary>
    /// Módulo anfitrión del ExportModal al que pertenece este dataset.
    /// Debe coincidir con ExportTask.Source. Ej: "auditoria".
    /// </summary>
    string Source { get; }

    /// <summary>Nombre legible del dataset (mostrado en Step 0 del wizard).</summary>
    string DisplayName { get; }

    /// <summary>Campos disponibles para que el usuario seleccione.</summary>
    IReadOnlyList<ExportFieldDefinition> AvailableFields { get; }

    /// <summary>Filtros disponibles, idénticos a los que la vista del módulo muestra.</summary>
    IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; }

    /// <summary>
    /// Resuelve el dataset aplicando la selección del usuario.
    /// Debe respetar selection.PreviewLimit si está presente.
    /// </summary>
    Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default);
}
