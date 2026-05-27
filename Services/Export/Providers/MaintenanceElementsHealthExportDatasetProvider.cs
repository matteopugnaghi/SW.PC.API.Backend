// ============================================================================
// MaintenanceElementsHealthExportDatasetProvider.cs — Vista Mantenimiento
// ============================================================================
// Dataset: "maintenance.elements.health"  (Source: "maintenance")
//
// Una fila por (Elemento, Variable de vida/mantenimiento). Cada fila resume
// el estado de salud actual de ese contador:
//   - Elemento (nombre, SKU, fabricante, modelo)
//   - Variable (nombre, unidad, tipo)
//   - Valor PLC actual, baseline, consumido
//   - Umbrales (warning/critical)
//   - HealthPct = 100 - (consumido / critical) × 100
//   - Status: Critical | Warning | OK
//   - Última intervención (fecha)
//
// Filtros declarados:
//   healthStatus : "all" | "critical" | "warning" | "ok"
//
// NO requiere lectura del PLC (todo desde SQLite). Manual y programado
// producen exactamente el mismo documento. Para mantener el dataset puro
// (consistente entre llamadas), se usan los últimos valores persistidos
// en SMM_Readings, no una lectura síncrona del PLC.
// ============================================================================

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class MaintenanceElementsHealthExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IExportTranslationLookup _translations;

    public MaintenanceElementsHealthExportDatasetProvider(
        IRequestProjectContext projectContext,
        IProjectDbContextFactory dbFactory,
        IExportTranslationLookup translations)
    {
        _projectContext = projectContext;
        _dbFactory = dbFactory;
        _translations = translations;
    }

    public string DatasetId => "maintenance.elements.health";
    public string Source => "maintenance";
    public string DisplayName => "Estado de salud de elementos";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["elementName"]      = ("maintenance.export.col.elementName",      "Elemento"),
        ["sku"]              = ("maintenance.export.col.sku",              "SKU"),
        ["manufacturer"]     = ("maintenance.export.col.manufacturer",     "Fabricante"),
        ["model"]            = ("maintenance.export.col.model",            "Modelo"),
        ["varName"]          = ("maintenance.export.col.varName",          "Variable"),
        ["unit"]             = ("maintenance.export.col.unit",             "Unidad"),
        ["taskType"]         = ("maintenance.export.col.taskType",         "Tipo"),
        ["currentValue"]     = ("maintenance.export.col.currentValue",     "Valor actual"),
        ["baseline"]         = ("maintenance.export.col.baseline",         "Baseline"),
        ["consumed"]         = ("maintenance.export.col.consumed",         "Consumido"),
        ["warning"]          = ("maintenance.export.col.warning",          "Umbral atención"),
        ["critical"]         = ("maintenance.export.col.critical",         "Umbral crítico"),
        ["healthPct"]        = ("maintenance.export.col.healthPct",        "Salud %"),
        ["status"]           = ("maintenance.export.col.status",           "Estado"),
        ["lastInterventionAt"] = ("maintenance.export.col.lastInterventionAt", "Última intervención"),
        ["lastReadingAt"]    = ("maintenance.export.col.lastReadingAt",    "Última lectura"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "elementName",        Label = "Elemento",            DefaultIncluded = true },
        new() { Id = "sku",                Label = "SKU",                 DefaultIncluded = true },
        new() { Id = "manufacturer",       Label = "Fabricante",          DefaultIncluded = true },
        new() { Id = "model",              Label = "Modelo",              DefaultIncluded = false },
        new() { Id = "varName",            Label = "Variable",            DefaultIncluded = true },
        new() { Id = "unit",               Label = "Unidad",              DefaultIncluded = true },
        new() { Id = "taskType",           Label = "Tipo",                DefaultIncluded = true },
        new() { Id = "currentValue",       Label = "Valor actual",        DefaultIncluded = true },
        new() { Id = "baseline",           Label = "Baseline",            DefaultIncluded = false },
        new() { Id = "consumed",           Label = "Consumido",           DefaultIncluded = true },
        new() { Id = "warning",            Label = "Umbral atención",     DefaultIncluded = false },
        new() { Id = "critical",           Label = "Umbral crítico",      DefaultIncluded = true },
        new() { Id = "healthPct",          Label = "Salud %",             DefaultIncluded = true },
        new() { Id = "status",             Label = "Estado",              DefaultIncluded = true },
        new() { Id = "lastInterventionAt", Label = "Última intervención", DefaultIncluded = false },
        new() { Id = "lastReadingAt",      Label = "Última lectura",      DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "healthStatus", Label = "Estado de salud", Type = "select" },
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var lang = string.IsNullOrWhiteSpace(selection.Language) ? "SPA" : selection.Language!;

        var fields = selection.Fields.Count > 0
            ? selection.Fields.ToList()
            : AvailableFields.Where(f => f.DefaultIncluded).Select(f => f.Id).ToList();

        var columns = fields.Select(id =>
        {
            if (ColumnI18n.TryGetValue(id, out var meta))
                return _translations.GetLabel(meta.Key, lang, meta.Es);
            return AvailableFields.FirstOrDefault(f => f.Id == id)?.Label ?? id;
        }).ToList();

        var ds = new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields,
            Rows = new List<object?[]>(),
            TotalRows = 0,
            Metadata =
            {
                ["dataset"] = DatasetId,
                ["projectId"] = _projectContext.ProjectId ?? "",
                ["generatedAt"] = DateTime.UtcNow.ToString("o"),
            }
        };

        // Filtro healthStatus
        string healthFilter = "all";
        if (selection.Filters.TryGetValue("healthStatus", out var rawHs) && rawHs is not null)
        {
            healthFilter = (rawHs switch
            {
                System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
                _ => rawHs.ToString(),
            } ?? "all").ToLowerInvariant();
        }
        if (healthFilter != "all") ds.Metadata["healthStatus"] = healthFilter;

        using var db = _dbFactory.CreateDbContext();

        // Variables con umbral crítico válido y elemento asociado.
        var vars = await db.SmmVariables.AsNoTracking()
            .Where(v => v.ElementId != null && v.Critical != null && v.Critical > 0)
            .OrderBy(v => v.ElementId).ThenBy(v => v.VarName)
            .Select(v => new
            {
                v.Id, v.ElementId, v.VarName, v.Unit, v.Warning, v.Critical,
                v.ResetOnMaintenance, v.ScaleFactor,
            })
            .ToListAsync(ct);

        if (vars.Count == 0) return ds;

        var elementIds = vars.Select(v => v.ElementId!.Value).Distinct().ToList();
        var elements = await db.SmmElements.AsNoTracking()
            .Where(e => elementIds.Contains(e.Id))
            .Select(e => new { e.Id, e.ElementName, e.SkuAquafrisch, e.Manufacturer, e.Model })
            .ToDictionaryAsync(e => e.Id, ct);

        // Última lectura por variable (no errores).
        var varIds = vars.Select(v => v.Id).ToList();
        var latestReadings = await db.SmmReadings.AsNoTracking()
            .Where(r => varIds.Contains(r.VariableId) && !r.IsError && r.Value != null)
            .GroupBy(r => r.VariableId)
            .Select(g => new
            {
                VariableId = g.Key,
                Last = g.OrderByDescending(r => r.Timestamp).Select(r => new { r.Value, r.Timestamp }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var latestByVar = latestReadings
            .Where(x => x.Last != null)
            .ToDictionary(x => x.VariableId, x => x.Last!);

        // Intervenciones del proyecto, agrupadas por ElementId.
        var interventions = await db.SmmInterventions.AsNoTracking()
            .Where(i => elementIds.Contains(i.ElementId))
            .OrderByDescending(i => i.PerformedAt)
            .Select(i => new { i.ElementId, i.TaskName, i.InterventionType, i.PerformedAt, i.AccumulatedValueAtMaintenance })
            .ToListAsync(ct);
        var intervByElement = interventions
            .GroupBy(i => i.ElementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<object?[]>();
        foreach (var v in vars)
        {
            if (!elements.TryGetValue(v.ElementId!.Value, out var el)) continue;

            var elInts = intervByElement.TryGetValue(v.ElementId.Value, out var li) ? li : new();

            // Baseline:
            //  - Vida útil (ResetOnMaintenance=FALSE): última Replacement.
            //  - Recurrente (ResetOnMaintenance=TRUE): última intervención cuya TaskName=VarName.
            double baseline = 0;
            DateTime? lastIntAt = null;
            if (!v.ResetOnMaintenance)
            {
                var lastRep = elInts.FirstOrDefault(i =>
                    string.Equals(i.InterventionType, "Replacement", StringComparison.OrdinalIgnoreCase));
                if (lastRep is { AccumulatedValueAtMaintenance: not null })
                {
                    baseline = lastRep.AccumulatedValueAtMaintenance.Value;
                    lastIntAt = lastRep.PerformedAt;
                }
            }
            else
            {
                var lastTask = elInts.FirstOrDefault(i =>
                    string.Equals(i.TaskName, v.VarName, StringComparison.OrdinalIgnoreCase));
                if (lastTask is { AccumulatedValueAtMaintenance: not null })
                {
                    baseline = lastTask.AccumulatedValueAtMaintenance.Value;
                    lastIntAt = lastTask.PerformedAt;
                }
            }

            double? rawValue = null;
            DateTime? readingAt = null;
            if (latestByVar.TryGetValue(v.Id, out var lr))
            {
                rawValue = lr.Value;
                readingAt = lr.Timestamp;
            }

            // Aplicar ScaleFactor coherente con el DTO de SmmController.
            double scale = v.ScaleFactor.HasValue && v.ScaleFactor.Value > 0 ? v.ScaleFactor.Value : 1.0;
            double? currentValue = rawValue.HasValue ? rawValue.Value * scale : (double?)null;

            double? consumed = currentValue.HasValue ? Math.Max(0, currentValue.Value - baseline) : (double?)null;
            double critical = v.Critical!.Value;
            double? healthPct = consumed.HasValue
                ? Math.Max(0, 100.0 - (consumed.Value / critical) * 100.0)
                : (double?)null;

            string status = ComputeStatus(consumed, v.Warning, critical, healthPct);

            if (healthFilter != "all" && !string.Equals(status, healthFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var row = new object?[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                row[i] = fields[i] switch
                {
                    "elementName"        => el.ElementName,
                    "sku"                => el.SkuAquafrisch,
                    "manufacturer"       => el.Manufacturer,
                    "model"              => el.Model,
                    "varName"            => v.VarName,
                    "unit"               => v.Unit,
                    "taskType"           => v.ResetOnMaintenance
                        ? _translations.GetLabel("maintenance.export.value.taskType.maintenance", lang, "Mantenimiento")
                        : _translations.GetLabel("maintenance.export.value.taskType.lifetime",    lang, "Vida útil"),
                    "currentValue"       => currentValue,
                    "baseline"           => baseline,
                    "consumed"           => consumed,
                    "warning"            => v.Warning,
                    "critical"           => critical,
                    "healthPct"          => healthPct.HasValue ? Math.Round(healthPct.Value, 1) : (double?)null,
                    "status"             => status,
                    "lastInterventionAt" => lastIntAt,
                    "lastReadingAt"      => readingAt,
                    _                    => null,
                };
            }
            rows.Add(row);
        }

        ds.Rows = rows;
        ds.TotalRows = rows.Count;
        return ds;
    }

    private static string ComputeStatus(double? consumed, double? warning, double critical, double? healthPct)
    {
        if (consumed == null) return "unknown";
        if (consumed.Value >= critical) return "critical";
        if (warning.HasValue && warning.Value > 0 && consumed.Value >= warning.Value) return "warning";
        return "ok";
    }
}
