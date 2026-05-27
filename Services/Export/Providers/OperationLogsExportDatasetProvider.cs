// ============================================================================
// OperationLogsExportDatasetProvider.cs — Provider de validación end-to-end
// ============================================================================
// Dataset: "operationlogs.entries"  (Source: "operationlogs")
//
// Reutiliza IOperationLogService.GetLogsAsync con el OperationLogFilter
// construido a partir de ExportSelection.Filters. Sirve para validar la
// integración del Wizard desde el módulo OperationLogsModal del frontend.
// ============================================================================

using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class OperationLogsExportDatasetProvider : IExportDatasetProvider
{
    private readonly IOperationLogService _opService;
    private readonly IRequestProjectContext _projectContext;
    private readonly IExportTranslationLookup _translations;

    public OperationLogsExportDatasetProvider(
        IOperationLogService opService,
        IRequestProjectContext projectContext,
        IExportTranslationLookup translations)
    {
        _opService = opService;
        _projectContext = projectContext;
        _translations = translations;
    }

    public string DatasetId => "operationlogs.entries";
    public string Source => "operationlogs";
    public string DisplayName => "Registro de operaciones";

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "timestamp",   Label = "Fecha/hora",   DefaultIncluded = true },
        new() { Id = "category",    Label = "Categoría",    DefaultIncluded = true },
        new() { Id = "action",      Label = "Acción",       DefaultIncluded = true },
        new() { Id = "severity",    Label = "Severidad",    DefaultIncluded = true },
        new() { Id = "user",        Label = "Usuario",      DefaultIncluded = true },
        new() { Id = "description", Label = "Descripción",  DefaultIncluded = true },
        new() { Id = "message",     Label = "Mensaje",      DefaultIncluded = false },
        new() { Id = "plcVariable", Label = "Variable PLC", DefaultIncluded = false },
        new() { Id = "alarmCode",   Label = "Código alarma", DefaultIncluded = false },
        new() { Id = "acknowledged",Label = "Reconocido",   DefaultIncluded = false },
        new() { Id = "acknowledgedBy", Label = "Reconocido por", DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "dateRange", Label = "Rango de fechas", Type = "dateRange" },
        new()
        {
            Id = "category",
            Label = "Categoría",
            Type = "select",
            Options = Enum.GetNames<OperationCategory>()
                .Select(n => new ExportFilterOption { Value = n, Label = n })
                .ToList()
        },
        new()
        {
            Id = "severity",
            Label = "Severidad mínima",
            Type = "select",
            Options = Enum.GetNames<OperationSeverity>()
                .Select(n => new ExportFilterOption { Value = n, Label = n })
                .ToList()
        },
        new() { Id = "onlyPlcAlarms",     Label = "Solo alarmas PLC", Type = "boolean" },
        new() { Id = "onlyUnacknowledged",Label = "Solo no reconocidos", Type = "boolean" },
        new() { Id = "user",              Label = "Usuario",          Type = "text" },
        new() { Id = "searchText",        Label = "Buscar texto",     Type = "text" },
    };

    // Mapa id-columna → (claveI18n, fallbackEs). Las claves se añaden al
    // translations.json del proyecto para que el header del XLSX/HTML respete
    // el idioma elegido en el wizard.
    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["timestamp"]      = ("operationLogs.export.col.timestamp",     "Fecha/hora"),
        ["category"]       = ("operationLogs.export.col.category",      "Categoría"),
        ["action"]         = ("operationLogs.export.col.action",        "Acción"),
        ["severity"]       = ("operationLogs.export.col.severity",      "Severidad"),
        ["user"]           = ("operationLogs.export.col.user",          "Usuario"),
        ["description"]    = ("operationLogs.export.col.description",   "Descripción"),
        ["message"]        = ("operationLogs.export.col.message",       "Mensaje"),
        ["plcVariable"]    = ("operationLogs.export.col.plcVariable",   "Variable PLC"),
        ["alarmCode"]      = ("operationLogs.export.col.alarmCode",     "Código alarma"),
        ["acknowledged"]   = ("operationLogs.export.col.acknowledged",  "Reconocido"),
        ["acknowledgedBy"] = ("operationLogs.export.col.acknowledgedBy","Reconocido por"),
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var filter = BuildFilter(selection);
        var response = await _opService.GetLogsAsync(filter);

        var fields = selection.Fields.Count > 0
            ? selection.Fields
            : AvailableFields.Where(f => f.DefaultIncluded).Select(f => f.Id).ToList();

        var lang = string.IsNullOrWhiteSpace(selection.Language) ? "SPA" : selection.Language!;

        // Cabeceras traducidas según selection.Language (fallback español).
        var columns = fields
            .Select(id =>
            {
                if (ColumnI18n.TryGetValue(id, out var meta))
                    return _translations.GetLabel(meta.Key, lang, meta.Es);
                // Fallback al label hardcodeado del field si no hay clave i18n.
                return AvailableFields.FirstOrDefault(f => f.Id == id)?.Label ?? id;
            })
            .ToList();

        var rows = response.Items
            .Select(e => fields.Select(id => MapField(e, id, lang)).ToArray())
            .ToList();

        return new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields.ToList(),
            Rows = rows,
            TotalRows = response.TotalCount,
            Metadata =
            {
                ["dataset"] = DatasetId,
                ["projectId"] = _projectContext.ProjectId ?? "",
                ["generatedAt"] = DateTime.UtcNow.ToString("o"),
                ["truncated"] = selection.PreviewLimit.HasValue && response.TotalCount > rows.Count,
            }
        };
    }

    private static OperationLogFilter BuildFilter(ExportSelection selection)
    {
        var f = new OperationLogFilter
        {
            Page = 1,
            PageSize = selection.PreviewLimit ?? 10_000,
            // Idioma para resolver Message (texto de alarma desde Excel) en el lenguaje elegido.
            Language = string.IsNullOrWhiteSpace(selection.Language) ? "SPA" : selection.Language!,
        };

        if (selection.Filters.TryGetValue("dateRange", out var range) && range is IDictionary<string, object?> dict)
        {
            if (dict.TryGetValue("from", out var from) && DateTime.TryParse(from?.ToString(), out var fromDt))
                f.FromDate = fromDt;
            if (dict.TryGetValue("to", out var to) && DateTime.TryParse(to?.ToString(), out var toDt))
                f.ToDate = toDt;
        }

        if (selection.Filters.TryGetValue("category", out var cat) && cat is not null
            && Enum.TryParse<OperationCategory>(cat.ToString(), true, out var opCat))
        {
            f.Category = opCat;
        }

        if (selection.Filters.TryGetValue("severity", out var sev) && sev is not null
            && Enum.TryParse<OperationSeverity>(sev.ToString(), true, out var opSev))
        {
            f.MinSeverity = opSev;
        }

        if (selection.Filters.TryGetValue("onlyPlcAlarms", out var pa) && pa is not null
            && bool.TryParse(pa.ToString(), out var pab))
        {
            f.OnlyPlcAlarms = pab;
        }

        if (selection.Filters.TryGetValue("onlyUnacknowledged", out var ack) && ack is not null
            && bool.TryParse(ack.ToString(), out var ackb))
        {
            f.OnlyUnacknowledged = ackb;
        }

        if (selection.Filters.TryGetValue("user", out var u) && u is not null)
            f.User = u.ToString();

        if (selection.Filters.TryGetValue("searchText", out var s) && s is not null)
            f.SearchText = s.ToString();

        return f;
    }

    private object? MapField(OperationLogDto e, string fieldId, string lang) => fieldId switch
    {
        "timestamp"      => e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        // Convención del proyecto: operationLogs.category.{camelCase} (la clave en
        // translations.json es singular y la primera letra del enum va en minúscula).
        "category"       => _translations.GetLabel($"operationLogs.category.{ToCamel(e.Category)}", lang, e.Category),
        // Acción: operationLogs.action.{PascalCase} (mismo nombre del enum OperationAction).
        "action"         => _translations.GetLabel($"operationLogs.action.{e.Action}", lang, e.Action),
        // Severidad: no existe traducción dedicada en translations.json — fallback al enum.
        "severity"       => e.Severity,
        "user"           => e.User,
        "description"    => e.Description,
        // Message ya viene traducido por OperationLogService.ConvertToDtosWithAlarmTextAsync
        // usando filter.Language (que arriba fijamos a selection.Language).
        "message"        => e.Message,
        "plcVariable"    => e.PlcVariable,
        "alarmCode"      => e.AlarmCode,
        "acknowledged"   => e.IsAcknowledged,
        "acknowledgedBy" => e.AcknowledgedBy,
        _ => null
    };

    private static string ToCamel(string? s)
        => string.IsNullOrEmpty(s) ? (s ?? "") : char.ToLowerInvariant(s![0]) + s.Substring(1);
}
