// ============================================================================
// StatisticsRowsExportDatasetProvider.cs — Passthrough de filas en pantalla
// ============================================================================
// Dataset: "statistics.rows"  (Source: "statistics")
//
// Provider passthrough: no consulta BD. El frontend envía en runtimeMetadata:
//   - columns : List<string>             (cabeceras visibles)
//   - rows    : List<List<object?>>      (filas formateadas tal cual en pantalla)
//   - totalRows (opcional)
//
// Pensado para exportar lo que el usuario está viendo en la tabla de Estadísticas
// (mismo patrón que StatisticsChartExportDatasetProvider con pngBase64).
// Ejecuciones programadas (cron/PLC) no pueden usar este provider porque no hay
// DOM disponible; el dataset llegará vacío.
// ============================================================================

using System.Text.Json;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class StatisticsRowsExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;

    public StatisticsRowsExportDatasetProvider(IRequestProjectContext projectContext)
    {
        _projectContext = projectContext;
    }

    public string DatasetId => "statistics.rows";
    public string Source => "statistics";
    public string DisplayName => "Filas de estadísticas (tabla en pantalla)";

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>();
    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

    public Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var ds = new ExportDataset
        {
            Columns = new List<string>(),
            Rows = new List<object?[]>(),
            TotalRows = 0,
            Metadata =
            {
                ["dataset"] = DatasetId,
                ["projectId"] = _projectContext.ProjectId ?? "",
                ["generatedAt"] = DateTime.UtcNow.ToString("o"),
            }
        };

        // columns
        if (selection.Metadata.TryGetValue("columns", out var colsObj) && colsObj is not null)
        {
            ds.Columns = ToStringList(colsObj);
        }

        // columnKeys (alineados con columns; permiten al formatter resolver
        // summaryColumns por id/clave en vez de por etiqueta).
        if (selection.Metadata.TryGetValue("columnKeys", out var keysObj) && keysObj is not null)
        {
            ds.Metadata["columnKeys"] = ToStringList(keysObj);
        }

        // rows: lista de listas (cada fila = lista de celdas)
        if (selection.Metadata.TryGetValue("rows", out var rowsObj) && rowsObj is not null)
        {
            ds.Rows = ToRowsList(rowsObj);
            ds.TotalRows = ds.Rows.Count;
        }

        if (selection.Metadata.TryGetValue("totalRows", out var totalObj) && totalObj is not null)
        {
            if (int.TryParse(totalObj.ToString(), out var n)) ds.TotalRows = n;
        }

        if (selection.Metadata.TryGetValue("chartTitle", out var title) && title is not null)
        {
            ds.Metadata["chartTitle"] = title;
        }

        return Task.FromResult(ds);
    }

    private static List<string> ToStringList(object value)
    {
        var list = new List<string>();
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
                list.Add(item.ValueKind == JsonValueKind.String ? (item.GetString() ?? "") : item.ToString());
            return list;
        }
        if (value is System.Collections.IEnumerable en && value is not string)
        {
            foreach (var item in en) list.Add(item?.ToString() ?? "");
        }
        return list;
    }

    private static List<object?[]> ToRowsList(object value)
    {
        var result = new List<object?[]>();
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var rowEl in je.EnumerateArray())
            {
                if (rowEl.ValueKind != JsonValueKind.Array) continue;
                var cells = new List<object?>();
                foreach (var cell in rowEl.EnumerateArray())
                {
                    cells.Add(cell.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => cell.GetString(),
                        JsonValueKind.Number => cell.TryGetInt64(out var l) ? l : cell.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => cell.ToString()
                    });
                }
                result.Add(cells.ToArray());
            }
            return result;
        }
        if (value is System.Collections.IEnumerable en && value is not string)
        {
            foreach (var rowObj in en)
            {
                if (rowObj is System.Collections.IEnumerable rEn && rowObj is not string)
                {
                    var cells = new List<object?>();
                    foreach (var c in rEn) cells.Add(c);
                    result.Add(cells.ToArray());
                }
            }
        }
        return result;
    }
}
