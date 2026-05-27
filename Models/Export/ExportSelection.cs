// ============================================================================
// ExportSelection.cs — Selección de campos/filtros + dataset devuelto
// ============================================================================
// Modelo declarativo: cada módulo anfitrión del ExportModal declara qué
// datasets ofrece (con fields y filters que YA existen en su UI). El wizard
// expone esas opciones al usuario y persiste su elección en ExportTask.SelectionJson.
//
// IExportDatasetProvider (en el backend) consume ExportSelection y devuelve
// un ExportDataset (columnas + filas) que ExportFormatterService convierte
// al formato pedido (xlsx, csv, json, html, png).
// ============================================================================

namespace SW.PC.API.Backend.Models.Export;

/// <summary>
/// Selección del usuario en Step 0 del wizard.
/// Se serializa a JSON en ExportTask.SelectionJson y se reaplica
/// idéntica en cada ejecución (manual/cron/plc) para reproducibilidad.
/// </summary>
public class ExportSelection
{
    /// <summary>Identificadores de campos a incluir, tal como los declara el módulo.</summary>
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// Filtros aplicados (key = id declarado por el módulo,
    /// value = valor en el formato del control: string, bool, number,
    /// { from, to } para dateRange, array para multiSelect, etc.).
    /// </summary>
    public Dictionary<string, object?> Filters { get; set; } = new();

    /// <summary>
    /// Límite de filas para preview (Step 0). null en ejecución real.
    /// El provider debe respetarlo en la query.
    /// </summary>
    public int? PreviewLimit { get; set; }

    /// <summary>
    /// Idioma en que se generan las cabeceras del documento exportado
    /// (SPA, ENG, ITA, FRA). Si es null o no se encuentra traducción,
    /// el provider usa el fallback hardcodeado (típicamente español).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Metadatos volátiles por ejecución (no se persisten en SelectionJson).
    /// Ej.: pngBase64 = captura del gráfico echarts realizada en el cliente
    /// justo antes de pulsar "Ejecutar". El runner los pasa al provider.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

/// <summary>
/// Dataset devuelto por un IExportDatasetProvider y consumido por
/// ExportFormatterService.
/// </summary>
public class ExportDataset
{
    /// <summary>Cabeceras de columnas (display label, no IDs).</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// IDs estables de cada columna (alineados a Columns).
    /// Permite a la UI re-mapear el header al label traducido del lado cliente.
    /// </summary>
    public List<string> ColumnIds { get; set; } = new();

    /// <summary>
    /// Filas de datos. Cada elemento es una fila con celdas alineadas a Columns.
    /// Puede contener valores nulos.
    /// </summary>
    public List<object?[]> Rows { get; set; } = new();

    /// <summary>
    /// Metadatos opcionales (título del informe, rango temporal, totales).
    /// Los formatters HTML/XLSX pueden renderizarlos como encabezado.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();

    /// <summary>Total de filas antes de aplicar PreviewLimit (si se aplicó).</summary>
    public int TotalRows { get; set; }
}
