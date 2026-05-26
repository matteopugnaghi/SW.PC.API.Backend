// ============================================================================
// AuditExportDatasetProvider.cs — Provider de validación para Fase 1
// ============================================================================
// Dataset: "auditoria.logs"  (Source: "auditoria")
//
// Reutiliza IAuditLogService.GetLogsAsync con el AuditLogQuery construido a
// partir de ExportSelection.Filters. Sirve como caso real más sencillo para
// validar el pipeline completo (wizard → ExportTask → ExportRunner → archivo).
// ============================================================================

using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class AuditExportDatasetProvider : IExportDatasetProvider
{
    private readonly IAuditLogService _auditService;
    private readonly IRequestProjectContext _projectContext;

    public AuditExportDatasetProvider(
        IAuditLogService auditService,
        IRequestProjectContext projectContext)
    {
        _auditService = auditService;
        _projectContext = projectContext;
    }

    public string DatasetId => "auditoria.logs";
    public string Source => "auditoria";
    public string DisplayName => "Logs de auditoría";

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "timestamp",  Label = "Fecha/hora",   DefaultIncluded = true },
        new() { Id = "category",   Label = "Categoría",    DefaultIncluded = true },
        new() { Id = "action",     Label = "Acción",       DefaultIncluded = true },
        new() { Id = "result",     Label = "Resultado",    DefaultIncluded = true },
        new() { Id = "userName",   Label = "Usuario",      DefaultIncluded = true },
        new() { Id = "ipAddress",  Label = "IP",           DefaultIncluded = false },
        new() { Id = "details",    Label = "Detalles",     DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "dateRange", Label = "Rango de fechas", Type = "dateRange" },
        new()
        {
            Id = "category",
            Label = "Categoría",
            Type = "select",
            Options = Enum.GetNames<AuditCategory>()
                .Select(n => new ExportFilterOption { Value = n, Label = n })
                .ToList()
        },
        new()
        {
            Id = "result",
            Label = "Resultado",
            Type = "select",
            Options = Enum.GetNames<AuditResult>()
                .Select(n => new ExportFilterOption { Value = n, Label = n })
                .ToList()
        },
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var query = BuildQuery(selection);
        var response = await _auditService.GetLogsAsync(query, _projectContext.ProjectId);

        var fields = selection.Fields.Count > 0
            ? selection.Fields
            : AvailableFields.Where(f => f.DefaultIncluded).Select(f => f.Id).ToList();

        var columns = fields.Select(id => AvailableFields.FirstOrDefault(f => f.Id == id)?.Label ?? id).ToList();

        var rows = response.Entries.Select(e => fields.Select(id => MapField(e, id)).ToArray()).ToList();

        return new ExportDataset
        {
            Columns = columns,
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

    private static AuditLogQuery BuildQuery(ExportSelection selection)
    {
        var query = new AuditLogQuery
        {
            Skip = 0,
            Take = selection.PreviewLimit ?? 10_000,
        };

        if (selection.Filters.TryGetValue("dateRange", out var range) && range is not null)
        {
            // Acepta objeto { from, to } (Dictionary deserializado desde JSON).
            if (range is IDictionary<string, object?> dict)
            {
                if (dict.TryGetValue("from", out var f) && DateTime.TryParse(f?.ToString(), out var fromDt))
                    query.From = fromDt;
                if (dict.TryGetValue("to", out var t) && DateTime.TryParse(t?.ToString(), out var toDt))
                    query.To = toDt;
            }
        }

        if (selection.Filters.TryGetValue("category", out var cat) && cat is not null
            && Enum.TryParse<AuditCategory>(cat.ToString(), true, out var auditCat))
        {
            query.Category = auditCat;
        }

        if (selection.Filters.TryGetValue("result", out var res) && res is not null
            && Enum.TryParse<AuditResult>(res.ToString(), true, out var auditRes))
        {
            query.Result = auditRes;
        }

        if (selection.Filters.TryGetValue("userId", out var u) && u is not null)
        {
            query.UserId = u.ToString();
        }

        return query;
    }

    private static object? MapField(AuditLogEntry e, string fieldId) => fieldId switch
    {
        "timestamp" => e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        "category"  => e.Category.ToString(),
        "action"    => e.Action.ToString(),
        "result"    => e.Result.ToString(),
        "userName"  => e.UserName,
        "userId"    => e.UserId,
        "ipAddress" => e.IpAddress,
        "details"   => e.Details,
        _ => null
    };
}
