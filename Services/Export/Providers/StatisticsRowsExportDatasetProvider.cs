// ============================================================================
// StatisticsRowsExportDatasetProvider.cs — Lectura directa desde SQLite
// ============================================================================
// Dataset: "statistics.rows"  (Source: "statistics")
//
// Construye las filas que el usuario vería en pantalla (DashboardTab) leyendo
// directamente de SQLite. De esta forma manual y programado producen el MISMO
// documento — no depende de DOM/runtimeMetadata.
//
// Entrada (selection.Filters):
//   groupId   : int  (obligatorio)  Id del grupo SMM a exportar.
//   dateRange : { from,to } absoluto  o  { mode:"relative", value, unit }.
//
// Para 'PerCycle': construye filas con startedAt/completedAt/duration + vars.
// Para 'Continuous'/'OnDemand': filas por snapshot (timestamp único) + vars.
//
// selection.Fields filtra las columnas. IDs estables:
//   PerCycle:    "startedAt","completedAt","durationSec","status","endedReason",
//                "alarmsCount","alarmTimeSec","hadAlarms", <varName>...
//   Continuous:  "timestamp", <varName>...
// ============================================================================

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class StatisticsRowsExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IExcelConfigService _excelConfigService;
    private readonly IExportTranslationLookup _translations;

    public StatisticsRowsExportDatasetProvider(
        IRequestProjectContext projectContext,
        IProjectDbContextFactory dbFactory,
        IExcelConfigService excelConfigService,
        IExportTranslationLookup translations)
    {
        _projectContext = projectContext;
        _dbFactory = dbFactory;
        _excelConfigService = excelConfigService;
        _translations = translations;
    }

    public string DatasetId => "statistics.rows";
    public string Source => "statistics";
    public string DisplayName => "Filas de estadísticas";

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>();

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "dateRange", Label = "Rango de fechas", Type = "dateRange" },
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
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

        // ── groupId ──
        if (!TryGetInt(selection.Filters, "groupId", out var groupId))
        {
            ds.Metadata["error"] = "Falta filtro 'groupId' en la selección.";
            return ds;
        }

        // ── dateRange (acepta absoluto o relativo) ──
        selection.Filters.TryGetValue("dateRange", out var rangeRaw);
        var (fromUtc, toUtc) = DateRangeFilterValue.Resolve(rangeRaw);
        if (fromUtc is not null) ds.Metadata["dateFrom"] = fromUtc.Value.ToString("o");
        if (toUtc is not null) ds.Metadata["dateTo"] = toUtc.Value.ToString("o");

        using var db = _dbFactory.CreateDbContext();

        var group = await db.SmmGroups.AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.Id, g.GroupName, g.UiType, g.ReadFrequency })
            .FirstOrDefaultAsync(ct);
        if (group is null)
        {
            ds.Metadata["error"] = $"Grupo SMM {groupId} no encontrado.";
            return ds;
        }

        ds.Metadata["groupId"] = group.Id;
        ds.Metadata["groupName"] = group.GroupName;
        ds.Metadata["uiType"] = group.UiType;
        ds.Metadata["readFrequency"] = group.ReadFrequency;

        // Variables del grupo (orden estable)
        var vars = await db.SmmVariables.AsNoTracking()
            .Where(v => v.GroupId == groupId)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.VarName)
            .Select(v => new VarMeta(v.Id, v.VarName, v.Unit, v.ScaleFactor ?? 1.0))
            .ToListAsync(ct);

        var freq = (group.ReadFrequency ?? "").Trim();
        var isPerCycle = string.Equals(freq, "PerCycle", StringComparison.OrdinalIgnoreCase);

        if (isPerCycle)
        {
            await BuildPerCycleAsync(db, ds, selection, groupId, vars, fromUtc, toUtc, _excelConfigService, ct);
        }
        else
        {
            await BuildContinuousAsync(db, ds, selection, groupId, vars, fromUtc, toUtc, ct);
        }

        // Traducir cabeceras al idioma pedido (selection.Language). Fallback al
        // label hardcodeado (español) si no hay clave/idioma o lookup vacío.
        TranslateColumnHeaders(ds, selection.Language);

        return ds;
    }

    // Mapa labelKey base → (claveI18n, fallbackEs). Las claves coinciden con
    // las que el frontend usa para el checklist "Campos a incluir", de modo
    // que preview y archivo exportado muestren EXACTAMENTE el mismo texto.
    private static readonly Dictionary<string, (string Key, string Es)> BaseColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["timestamp"]    = ("statistics.column.fecha",          "Fecha/hora"),
        ["startedAt"]    = ("statistics.column.inicio",         "Inicio"),
        ["completedAt"]  = ("statistics.column.fin",            "Fin"),
        ["durationSec"]  = ("statistics.column.duracion",       "Duración (s)"),
        ["status"]       = ("statistics.column.estado",         "Estado"),
        ["endedReason"]  = ("statistics.column.razon_fin",      "Razón fin"),
        ["alarmsCount"]  = ("statistics.column.alarmas",        "Alarmas"),
        ["alarmTimeSec"] = ("statistics.column.tiempo_alarma",  "Tiempo alarma (s)"),
        ["hadAlarms"]    = ("statistics.column.con_alarmas",    "Con alarmas"),
        ["alarmNames"]   = ("statistics.column.nombre_alarmas", "Nombre alarmas"),
    };

    private void TranslateColumnHeaders(ExportDataset ds, string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return;
        if (ds.ColumnIds.Count == 0 || ds.Columns.Count != ds.ColumnIds.Count) return;

        for (int i = 0; i < ds.ColumnIds.Count; i++)
        {
            var id = ds.ColumnIds[i];
            if (BaseColumnI18n.TryGetValue(id, out var meta))
            {
                ds.Columns[i] = _translations.GetLabel(meta.Key, lang, meta.Es);
            }
            // Variables OPC/UA: no hay clave i18n → se deja el VarName tal cual.
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // PerCycle: una fila por ciclo + valores de variables capturadas al cierre
    // ─────────────────────────────────────────────────────────────────────
    private static async Task BuildPerCycleAsync(
        AquafrischDbContext db, ExportDataset ds, ExportSelection selection,
        int groupId, List<VarMeta> vars, DateTime? from, DateTime? to,
        IExcelConfigService excelConfigService,
        CancellationToken ct)
    {
        var cycleQ = db.SmmCycles.AsNoTracking()
            .Where(c => c.GroupId == groupId && !c.IsDeleted);
        if (from.HasValue) cycleQ = cycleQ.Where(c => c.StartedAt >= from.Value);
        if (to.HasValue) cycleQ = cycleQ.Where(c => c.StartedAt <= to.Value);

        var limit = selection.PreviewLimit ?? 100_000;
        var cycles = await cycleQ
            .OrderByDescending(c => c.StartedAt)
            .Take(limit)
            .Select(c => new
            {
                c.Id, c.StartedAt, c.CompletedAt, c.Status, c.EndedReason,
                c.AlarmsCount, c.AlarmTime_s, c.HadAlarms
            })
            .ToListAsync(ct);

        var cycleIds = cycles.Select(c => c.Id).ToList();
        var readings = await db.SmmReadings.AsNoTracking()
            .Where(r => r.CycleId != null && cycleIds.Contains(r.CycleId!.Value))
            .Select(r => new { CycleId = r.CycleId!.Value, r.VariableId, r.Value, r.StringValue, r.IsError })
            .ToListAsync(ct);
        var readingsByCycle = readings.GroupBy(r => r.CycleId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.VariableId, x => x));

        // Alarmas por ciclo — para emitir nombres legibles en el export
        var cycleAlarms = await db.SmmCycleAlarms.AsNoTracking()
            .Where(a => cycleIds.Contains(a.CycleId))
            .Select(a => new { a.CycleId, a.AlarmCode, a.AlarmText })
            .ToListAsync(ct);

        // Resolver textos desde Excel. Si el usuario seleccionó un idioma distinto
        // de SPA, siempre intentamos la resolución por Excel (que devuelve el texto
        // en el idioma pedido); el AlarmText guardado en BD está siempre en SPA y
        // se usa como fallback final.
        var alarmLang = string.IsNullOrWhiteSpace(selection.Language) ? "SPA" : selection.Language!;
        var isNonSpanish = !string.Equals(alarmLang, "SPA", StringComparison.OrdinalIgnoreCase);
        SW.PC.API.Backend.Models.Excel.AlarmConfiguration? alarmsCfg = null;
        try
        {
            var needsLookup = isNonSpanish
                || cycleAlarms.Any(a => string.IsNullOrWhiteSpace(a.AlarmText) || a.AlarmText == a.AlarmCode);
            if (needsLookup)
            {
                var excelPath = excelConfigService.GetExcelConfigPath();
                alarmsCfg = await excelConfigService.LoadAlarmsAsync(excelPath);
            }
        }
        catch { /* fallback silencioso al AlarmCode */ }

        string ResolveText(string code, string? text)
        {
            // En idiomas distintos de SPA, priorizamos siempre la traducción del Excel.
            if (isNonSpanish)
            {
                var translated = SW.PC.API.Backend.Services.Smm.SmmAlarmTextResolver.Resolve(alarmsCfg, code, alarmLang);
                if (!string.IsNullOrWhiteSpace(translated)) return translated!;
            }
            if (!string.IsNullOrWhiteSpace(text) && text != code) return text!;
            var resolved = SW.PC.API.Backend.Services.Smm.SmmAlarmTextResolver.Resolve(alarmsCfg, code, alarmLang);
            return !string.IsNullOrWhiteSpace(resolved) ? resolved : (text ?? code);
        }

        var alarmTextsByCycle = cycleAlarms
            .GroupBy(a => a.CycleId)
            .ToDictionary(g => g.Key, g => string.Join(", ",
                g.Select(x => ResolveText(x.AlarmCode, x.AlarmText))
                 .Where(s => !string.IsNullOrWhiteSpace(s))
                 .Distinct()));

        var baseCols = new List<ColDef>
        {
            new("startedAt",    "Inicio"),
            new("completedAt",  "Fin"),
            new("durationSec",  "Duración (s)"),
            new("status",       "Estado"),
            new("endedReason",  "Motivo fin"),
            new("alarmsCount",  "Alarmas"),
            new("alarmTimeSec", "Tiempo alarma (s)"),
            new("hadAlarms",    "Con alarmas"),
            new("alarmNames",   "Nombre alarmas"),
        };
        var varCols = vars.Select(v => new ColDef(v.VarName,
            string.IsNullOrEmpty(v.Unit) ? v.VarName : $"{v.VarName} ({v.Unit})")).ToList();

        var (selectedBase, selectedVars, columnKeys, columnLabels) =
            ApplyFieldSelection(EnsureAlarmNamesSelected(selection.Fields), baseCols, varCols, vars);
        ds.Columns = columnLabels;
        ds.ColumnIds = columnKeys;
        ds.Metadata["columnKeys"] = columnKeys;

        foreach (var c in cycles)
        {
            var row = new List<object?>(columnLabels.Count);
            foreach (var col in selectedBase)
            {
                row.Add(col.Id switch
                {
                    "startedAt"    => DateTime.SpecifyKind(c.StartedAt, DateTimeKind.Utc),
                    "completedAt"  => c.CompletedAt.HasValue
                                        ? (object?)DateTime.SpecifyKind(c.CompletedAt.Value, DateTimeKind.Utc)
                                        : null,
                    "durationSec"  => c.CompletedAt.HasValue
                                        ? Math.Round((c.CompletedAt.Value - c.StartedAt).TotalSeconds, 1)
                                        : (object?)null,
                    "status"       => c.Status,
                    "endedReason"  => c.EndedReason,
                    "alarmsCount"  => c.AlarmsCount,
                    "alarmTimeSec" => Math.Round(c.AlarmTime_s, 1),
                    "hadAlarms"    => c.HadAlarms,
                    "alarmNames"   => alarmTextsByCycle.TryGetValue(c.Id, out var atxt) ? atxt : null,
                    _ => null,
                });
            }

            readingsByCycle.TryGetValue(c.Id, out var rmap);
            foreach (var v in selectedVars)
            {
                if (rmap is not null && rmap.TryGetValue(v.VariableId, out var r))
                {
                    if (r.IsError) row.Add("ERR");
                    else if (r.Value.HasValue) row.Add(Math.Round(r.Value.Value * v.ScaleFactor, 3));
                    else row.Add(r.StringValue);
                }
                else row.Add(null);
            }
            ds.Rows.Add(row.ToArray());
        }
        ds.TotalRows = ds.Rows.Count;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Continuous/OnDemand: una fila por snapshot (timestamp) + valores vars
    // ─────────────────────────────────────────────────────────────────────
    private static async Task BuildContinuousAsync(
        AquafrischDbContext db, ExportDataset ds, ExportSelection selection,
        int groupId, List<VarMeta> vars, DateTime? from, DateTime? to,
        CancellationToken ct)
    {
        var rQ = db.SmmReadings.AsNoTracking()
            .Where(r => r.GroupId == groupId && r.CycleId == null);
        if (from.HasValue) rQ = rQ.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue) rQ = rQ.Where(r => r.Timestamp <= to.Value);

        var limit = selection.PreviewLimit ?? 100_000;
        var recentTs = await rQ
            .Select(r => r.Timestamp)
            .Distinct()
            .OrderByDescending(t => t)
            .Take(limit)
            .ToListAsync(ct);
        if (recentTs.Count == 0)
        {
            var only = BuildContinuousColumnsOnly(selection.Fields, vars);
            ds.Columns = only.Labels;
            ds.ColumnIds = only.Keys;
            ds.Metadata["columnKeys"] = only.Keys;
            return;
        }

        var minTs = recentTs.Min();
        var maxTs = recentTs.Max();
        var readings = await db.SmmReadings.AsNoTracking()
            .Where(r => r.GroupId == groupId && r.CycleId == null
                        && r.Timestamp >= minTs && r.Timestamp <= maxTs)
            .Select(r => new { r.Timestamp, r.VariableId, r.Value, r.StringValue, r.IsError })
            .ToListAsync(ct);
        var byTs = readings.GroupBy(r => r.Timestamp)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.VariableId, x => x));

        var baseCols = new List<ColDef> { new("timestamp", "Fecha/hora") };
        var varCols = vars.Select(v => new ColDef(v.VarName,
            string.IsNullOrEmpty(v.Unit) ? v.VarName : $"{v.VarName} ({v.Unit})")).ToList();

        var (selectedBase, selectedVars, columnKeys, columnLabels) =
            ApplyFieldSelection(selection.Fields, baseCols, varCols, vars);
        ds.Columns = columnLabels;
        ds.ColumnIds = columnKeys;
        ds.Metadata["columnKeys"] = columnKeys;

        foreach (var ts in recentTs)
        {
            var row = new List<object?>(columnLabels.Count);
            foreach (var col in selectedBase)
            {
                if (col.Id == "timestamp") row.Add(DateTime.SpecifyKind(ts, DateTimeKind.Utc));
                else row.Add(null);
            }
            byTs.TryGetValue(ts, out var rmap);
            foreach (var v in selectedVars)
            {
                if (rmap is not null && rmap.TryGetValue(v.VariableId, out var r))
                {
                    if (r.IsError) row.Add("ERR");
                    else if (r.Value.HasValue) row.Add(Math.Round(r.Value.Value * v.ScaleFactor, 3));
                    else row.Add(r.StringValue);
                }
                else row.Add(null);
            }
            ds.Rows.Add(row.ToArray());
        }
        ds.TotalRows = ds.Rows.Count;
    }

    private static (List<string> Keys, List<string> Labels) BuildContinuousColumnsOnly(
        List<string> selectedIds, List<VarMeta> vars)
    {
        var baseCols = new List<ColDef> { new("timestamp", "Fecha/hora") };
        var varCols = vars.Select(v => new ColDef(v.VarName,
            string.IsNullOrEmpty(v.Unit) ? v.VarName : $"{v.VarName} ({v.Unit})")).ToList();
        var (_, _, keys, labels) = ApplyFieldSelection(selectedIds, baseCols, varCols, vars);
        return (keys, labels);
    }

    // Garantiza que la columna `alarmNames` está incluida en una selección no vacía.
    // Las tareas cron/legacy guardadas antes de añadir la columna no la tienen.
    private static List<string> EnsureAlarmNamesSelected(List<string> selected)
    {
        if (selected is null || selected.Count == 0) return selected!; // vacío = incluir todo
        if (selected.Contains("alarmNames", StringComparer.Ordinal)) return selected;
        var copy = new List<string>(selected.Count + 1);
        copy.AddRange(selected);
        // Insertar después de "hadAlarms" si existe, para mantener orden coherente
        var idx = copy.FindIndex(s => string.Equals(s, "hadAlarms", StringComparison.Ordinal));
        if (idx >= 0) copy.Insert(idx + 1, "alarmNames"); else copy.Add("alarmNames");
        return copy;
    }

    // Filtra base+vars según selection.Fields; si está vacío, incluye todo.
    private static (List<ColDef> SelBase, List<VarMeta> SelVars,
                    List<string> ColKeys, List<string> ColLabels)
        ApplyFieldSelection(List<string> selected, List<ColDef> baseCols, List<ColDef> varCols,
                            List<VarMeta> varMetas)
    {
        var includeAll = selected is null || selected.Count == 0;
        var selSet = includeAll ? null : new HashSet<string>(selected, StringComparer.Ordinal);

        var selBase = baseCols.Where(c => includeAll || selSet!.Contains(c.Id)).ToList();

        var selVars = new List<VarMeta>();
        foreach (var vc in varCols)
        {
            if (includeAll || selSet!.Contains(vc.Id))
            {
                var meta = varMetas.FirstOrDefault(v => v.VarName == vc.Id);
                if (meta is not null) selVars.Add(meta);
            }
        }

        var keys = new List<string>();
        var labels = new List<string>();
        foreach (var c in selBase) { keys.Add(c.Id); labels.Add(c.Label); }
        foreach (var v in selVars)
        {
            keys.Add(v.VarName);
            labels.Add(string.IsNullOrEmpty(v.Unit) ? v.VarName : $"{v.VarName} ({v.Unit})");
        }
        return (selBase, selVars, keys, labels);
    }

    private static bool TryGetInt(IDictionary<string, object?> filters, string key, out int value)
    {
        value = 0;
        if (!filters.TryGetValue(key, out var raw) || raw is null) return false;
        if (raw is int i) { value = i; return true; }
        if (raw is long l) { value = (int)l; return true; }
        if (raw is double d) { value = (int)d; return true; }
        if (raw is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var n)) { value = n; return true; }
            if (je.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(je.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ns)) { value = ns; return true; }
        }
        return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private sealed record ColDef(string Id, string Label);
    private sealed record VarMeta(int VariableId, string VarName, string? Unit, double ScaleFactor);
}
