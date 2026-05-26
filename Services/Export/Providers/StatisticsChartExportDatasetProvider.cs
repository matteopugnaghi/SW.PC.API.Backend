// ============================================================================
// StatisticsChartExportDatasetProvider.cs — Captura PNG de gráficos ECharts
// ============================================================================
// Dataset: "statistics.chart"  (Source: "statistics")
//
// Provider passthrough: no consulta BD. Espera que el frontend capture el
// gráfico con `chart.getDataURL({...})` y lo envíe en runtimeMetadata.pngBase64
// al pulsar "Ejecutar". El ExportFormatterService.FormatPng lo decodifica.
//
// Solo válido para format="png". Ejecuciones programadas (cron/PLC) no pueden
// usar este provider porque no hay DOM disponible — se ignora o falla con
// mensaje claro si pngBase64 está ausente.
// ============================================================================

using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class StatisticsChartExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;

    public StatisticsChartExportDatasetProvider(IRequestProjectContext projectContext)
    {
        _projectContext = projectContext;
    }

    public string DatasetId => "statistics.chart";
    public string Source => "statistics";
    public string DisplayName => "Gráfico de estadísticas (PNG)";

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>();
    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

    public Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var ds = new ExportDataset
        {
            Columns = new List<string>(),
            Rows = new List<object?[]>(),
            TotalRows = 1,
            Metadata =
            {
                ["dataset"] = DatasetId,
                ["projectId"] = _projectContext.ProjectId ?? "",
                ["generatedAt"] = DateTime.UtcNow.ToString("o"),
                ["captureMode"] = "echarts",
            }
        };

        if (selection.Metadata.TryGetValue("pngBase64", out var png) && png is not null)
        {
            ds.Metadata["pngBase64"] = png;
        }

        if (selection.Metadata.TryGetValue("chartTitle", out var title) && title is not null)
        {
            ds.Metadata["chartTitle"] = title;
        }

        return Task.FromResult(ds);
    }
}
