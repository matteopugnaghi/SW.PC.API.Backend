// ============================================================================
// MaintenanceInterventionsExportDatasetProvider.cs — Historial de intervenciones
// ============================================================================
// Dataset: "maintenance.interventions"  (Source: "maintenance")
//
// Una fila por intervención registrada en SMM_Interventions. Incluye un
// campo agregado `partsUsed` que concatena los consumibles asociados.
//
// Filtros declarados:
//   dateRange    : { from, to } absoluto (PerformedAt)
//   healthStatus : "all" | "critical" | "warning" | "ok"
//                  (filtra por estado actual del elemento — usa heurística:
//                   intervención de elemento cuyo estado de salud actual coincide).
// ============================================================================

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class MaintenanceInterventionsExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IExportTranslationLookup _translations;

    public MaintenanceInterventionsExportDatasetProvider(
        IRequestProjectContext projectContext,
        IProjectDbContextFactory dbFactory,
        IExportTranslationLookup translations)
    {
        _projectContext = projectContext;
        _dbFactory = dbFactory;
        _translations = translations;
    }

    public string DatasetId => "maintenance.interventions";
    public string Source => "maintenance";
    public string DisplayName => "Historial de intervenciones";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["performedAt"]     = ("maintenance.export.intervention.col.performedAt",     "Fecha"),
        ["elementName"]     = ("maintenance.export.intervention.col.elementName",     "Elemento"),
        ["taskName"]        = ("maintenance.export.intervention.col.taskName",        "Tarea"),
        ["interventionType"]= ("maintenance.export.intervention.col.interventionType","Tipo"),
        ["performedByUser"] = ("maintenance.export.intervention.col.performedByUser", "Operador"),
        ["performedByRole"] = ("maintenance.export.intervention.col.performedByRole", "Rol"),
        ["workOrderRef"]    = ("maintenance.export.intervention.col.workOrderRef",    "Orden de trabajo"),
        ["accumulatedValue"]= ("maintenance.export.intervention.col.accumulatedValue","Valor acumulado"),
        ["partsUsed"]       = ("maintenance.export.intervention.col.partsUsed",       "Consumibles"),
        ["notes"]           = ("maintenance.export.intervention.col.notes",           "Notas"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "performedAt",      Label = "Fecha",            DefaultIncluded = true },
        new() { Id = "elementName",      Label = "Elemento",         DefaultIncluded = true },
        new() { Id = "taskName",         Label = "Tarea",            DefaultIncluded = true },
        new() { Id = "interventionType", Label = "Tipo",             DefaultIncluded = true },
        new() { Id = "performedByUser",  Label = "Operador",         DefaultIncluded = true },
        new() { Id = "performedByRole",  Label = "Rol",              DefaultIncluded = false },
        new() { Id = "workOrderRef",     Label = "Orden de trabajo", DefaultIncluded = false },
        new() { Id = "accumulatedValue", Label = "Valor acumulado",  DefaultIncluded = false },
        new() { Id = "partsUsed",        Label = "Consumibles",      DefaultIncluded = true },
        new() { Id = "notes",            Label = "Notas",            DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "dateRange",    Label = "Rango de fechas",   Type = "dateRange" },
        new() { Id = "healthStatus", Label = "Estado de salud",   Type = "select" },
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

        // dateRange
        selection.Filters.TryGetValue("dateRange", out var rangeRaw);
        var (fromUtc, toUtc) = DateRangeFilterValue.Resolve(rangeRaw);
        if (fromUtc is not null) ds.Metadata["dateFrom"] = fromUtc.Value.ToString("o");
        if (toUtc   is not null) ds.Metadata["dateTo"]   = toUtc.Value.ToString("o");

        // healthStatus
        string healthFilter = "all";
        if (selection.Filters.TryGetValue("healthStatus", out var rawHs) && rawHs is not null)
        {
            healthFilter = (rawHs switch
            {
                System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
                _ => rawHs.ToString(),
            } ?? "all").ToLowerInvariant();
        }

        using var db = _dbFactory.CreateDbContext();

        // Si se filtra por healthStatus, primero obtenemos elementIds que coinciden.
        HashSet<int>? elementIdsFilter = null;
        if (healthFilter != "all")
        {
            elementIdsFilter = await ComputeElementsByStatusAsync(db, healthFilter, ct);
            ds.Metadata["healthStatus"] = healthFilter;
            if (elementIdsFilter.Count == 0) return ds;
        }

        var q = db.SmmInterventions.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue) q = q.Where(i => i.PerformedAt >= fromUtc.Value);
        if (toUtc.HasValue)   q = q.Where(i => i.PerformedAt <= toUtc.Value);
        if (elementIdsFilter != null)
        {
            var ids = elementIdsFilter.ToList();
            q = q.Where(i => ids.Contains(i.ElementId));
        }

        var ints = await q
            .OrderByDescending(i => i.PerformedAt)
            .Select(i => new
            {
                i.Id, i.ElementId, i.TaskName, i.InterventionType,
                i.PerformedAt, i.PerformedByUser, i.PerformedByRole,
                i.WorkOrderRef, i.AccumulatedValueAtMaintenance, i.Notes,
            })
            .ToListAsync(ct);

        if (ints.Count == 0) return ds;

        var elementIds = ints.Select(i => i.ElementId).Distinct().ToList();
        var elementNames = await db.SmmElements.AsNoTracking()
            .Where(e => elementIds.Contains(e.Id))
            .Select(e => new { e.Id, e.ElementName })
            .ToDictionaryAsync(e => e.Id, e => e.ElementName, ct);

        // Consumibles agrupados por intervención.
        var intIds = ints.Select(i => i.Id).ToList();
        var usages = await db.SmmConsumableUsage.AsNoTracking()
            .Where(c => intIds.Contains(c.InterventionId))
            .Select(c => new { c.InterventionId, c.PartSku, c.PartDescription, c.PartUnit, c.Quantity })
            .ToListAsync(ct);
        var usagesByInt = usages.GroupBy(u => u.InterventionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<object?[]>(ints.Count);
        foreach (var i in ints)
        {
            string? parts = null;
            if (usagesByInt.TryGetValue(i.Id, out var us) && us.Count > 0)
            {
                parts = string.Join("; ", us.Select(u =>
                    $"{u.PartSku} ({u.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} {u.PartUnit})"
                    + (string.IsNullOrEmpty(u.PartDescription) ? "" : " - " + u.PartDescription)));
            }

            var row = new object?[fields.Count];
            for (int k = 0; k < fields.Count; k++)
            {
                row[k] = fields[k] switch
                {
                    "performedAt"      => i.PerformedAt,
                    "elementName"      => elementNames.TryGetValue(i.ElementId, out var n) ? n : ("#" + i.ElementId),
                    "taskName"         => i.TaskName,
                    "interventionType" => i.InterventionType,
                    "performedByUser"  => i.PerformedByUser,
                    "performedByRole"  => i.PerformedByRole,
                    "workOrderRef"     => i.WorkOrderRef,
                    "accumulatedValue" => i.AccumulatedValueAtMaintenance,
                    "partsUsed"        => parts,
                    "notes"            => i.Notes,
                    _                  => null,
                };
            }
            rows.Add(row);
        }

        ds.Rows = rows;
        ds.TotalRows = rows.Count;
        return ds;
    }

    // Reusa la misma heurística que el provider de elements.health.
    private static async Task<HashSet<int>> ComputeElementsByStatusAsync(
        AquafrischDbContext db, string status, CancellationToken ct)
    {
        var vars = await db.SmmVariables.AsNoTracking()
            .Where(v => v.ElementId != null && v.Critical != null && v.Critical > 0)
            .Select(v => new
            {
                v.Id, v.ElementId, v.VarName, v.Warning, v.Critical,
                v.ResetOnMaintenance, v.ScaleFactor,
            })
            .ToListAsync(ct);

        var varIds = vars.Select(v => v.Id).ToList();
        var elementIds = vars.Select(v => v.ElementId!.Value).Distinct().ToList();

        var latest = await db.SmmReadings.AsNoTracking()
            .Where(r => varIds.Contains(r.VariableId) && !r.IsError && r.Value != null)
            .GroupBy(r => r.VariableId)
            .Select(g => new
            {
                VariableId = g.Key,
                Value = g.OrderByDescending(r => r.Timestamp).Select(r => r.Value).FirstOrDefault(),
            })
            .ToDictionaryAsync(x => x.VariableId, x => x.Value, ct);

        var interventions = await db.SmmInterventions.AsNoTracking()
            .Where(i => elementIds.Contains(i.ElementId))
            .OrderByDescending(i => i.PerformedAt)
            .Select(i => new { i.ElementId, i.TaskName, i.InterventionType, i.AccumulatedValueAtMaintenance })
            .ToListAsync(ct);
        var intByElement = interventions.GroupBy(i => i.ElementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var matching = new HashSet<int>();
        foreach (var v in vars)
        {
            if (!latest.TryGetValue(v.Id, out var raw) || raw == null) continue;
            double scale = v.ScaleFactor.HasValue && v.ScaleFactor.Value > 0 ? v.ScaleFactor.Value : 1.0;
            double current = raw.Value * scale;

            double baseline = 0;
            var ints = intByElement.TryGetValue(v.ElementId!.Value, out var li) ? li : new();
            if (!v.ResetOnMaintenance)
            {
                var lastRep = ints.FirstOrDefault(i =>
                    string.Equals(i.InterventionType, "Replacement", StringComparison.OrdinalIgnoreCase));
                if (lastRep is { AccumulatedValueAtMaintenance: not null })
                    baseline = lastRep.AccumulatedValueAtMaintenance.Value;
            }
            else
            {
                var lastTask = ints.FirstOrDefault(i =>
                    string.Equals(i.TaskName, v.VarName, StringComparison.OrdinalIgnoreCase));
                if (lastTask is { AccumulatedValueAtMaintenance: not null })
                    baseline = lastTask.AccumulatedValueAtMaintenance.Value;
            }
            double consumed = Math.Max(0, current - baseline);
            string s;
            if (consumed >= v.Critical!.Value) s = "critical";
            else if (v.Warning.HasValue && v.Warning.Value > 0 && consumed >= v.Warning.Value) s = "warning";
            else s = "ok";

            if (string.Equals(s, status, StringComparison.OrdinalIgnoreCase))
                matching.Add(v.ElementId.Value);
        }
        return matching;
    }
}
