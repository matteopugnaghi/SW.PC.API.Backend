// ============================================================================
// ExportProviderDefinitions.cs — Metadatos declarados por cada IExportDatasetProvider
// ============================================================================
// El wizard (Step 0 — Qué) los consume vía GET /api/export/datasets/{source}
// para construir la UI dinámica de campos y filtros. NO se inventan controles:
// el módulo anfitrión solo declara los campos y filtros que YA muestra en su UI.
// ============================================================================

namespace SW.PC.API.Backend.Models.Export;

/// <summary>
/// Definición de un campo (columna) que el dataset puede emitir.
/// </summary>
public class ExportFieldDefinition
{
    /// <summary>Identificador estable usado en ExportSelection.Fields.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Etiqueta visible al usuario (i18n: clave o texto).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Si true, viene marcado por defecto en el wizard.</summary>
    public bool DefaultIncluded { get; set; } = true;
}

/// <summary>
/// Definición de un filtro disponible. El tipo determina el control que renderiza
/// el frontend. La forma del valor en ExportSelection.Filters[id] debe coincidir
/// con lo que ese control emite (string, bool, number, array, { from, to }).
/// </summary>
public class ExportFilterDefinition
{
    /// <summary>Identificador estable usado en ExportSelection.Filters.</summary>
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// "dateRange" | "select" | "multiSelect" | "text" | "bool" | "number".
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>Opciones (solo para "select" / "multiSelect").</summary>
    public List<ExportFilterOption>? Options { get; set; }
}

public class ExportFilterOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
