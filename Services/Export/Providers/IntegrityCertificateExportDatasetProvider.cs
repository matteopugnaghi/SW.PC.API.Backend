// ============================================================================
// IntegrityCertificateExportDatasetProvider.cs — Certificado de integridad EU CRA
// ============================================================================
// Dataset: "cra.integrity.certificate"  (Source: "integrity")
//
// Genera bajo demanda un IntegrityCertificate (firmado) y lo expone como
// dataset tabular: una fila por componente (Backend / Frontend / TwinCAT),
// con las cabeceras del certificado (machineId, operador, hash, firma…)
// dentro de Metadata. El Wizard saltará Steps 0/1 vía hiddenSteps.
// ============================================================================

using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class IntegrityCertificateExportDatasetProvider : IExportDatasetProvider
{
    private readonly ISoftwareIntegrityService _integrityService;
    private readonly IExportTranslationLookup _translations;

    public IntegrityCertificateExportDatasetProvider(
        ISoftwareIntegrityService integrityService,
        IExportTranslationLookup translations)
    {
        _integrityService = integrityService;
        _translations = translations;
    }

    public string DatasetId => "cra.integrity.certificate";
    public string Source => "integrity";
    public string DisplayName => "Certificado de integridad";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"]            = ("integrity.export.col.component",  "Componente"),
        ["version"]         = ("integrity.export.col.version",    "Versión"),
        ["commitShort"]     = ("integrity.export.col.commit",     "Commit"),
        ["branch"]          = ("integrity.export.col.branch",     "Rama"),
        ["commitDate"]      = ("integrity.export.col.commitDate", "Fecha commit"),
        ["commitAuthor"]    = ("integrity.export.col.author",     "Autor"),
        ["integrity"]       = ("integrity.export.col.integrity",  "Integridad"),
        ["isSigned"]        = ("integrity.export.col.signed",     "Firmado"),
        ["signatureStatus"] = ("integrity.export.col.sigStatus",  "Estado firma"),
        ["modifiedFiles"]   = ("integrity.export.col.modified",   "Ficheros modificados"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "name",            Label = "Componente",            DefaultIncluded = true },
        new() { Id = "version",         Label = "Versión",               DefaultIncluded = true },
        new() { Id = "commitShort",     Label = "Commit",                DefaultIncluded = true },
        new() { Id = "branch",          Label = "Rama",                  DefaultIncluded = true },
        new() { Id = "commitDate",      Label = "Fecha commit",          DefaultIncluded = false },
        new() { Id = "commitAuthor",    Label = "Autor",                 DefaultIncluded = false },
        new() { Id = "integrity",       Label = "Integridad",            DefaultIncluded = true },
        new() { Id = "isSigned",        Label = "Firmado",               DefaultIncluded = true },
        new() { Id = "signatureStatus", Label = "Estado firma",          DefaultIncluded = false },
        new() { Id = "modifiedFiles",   Label = "Ficheros modificados",  DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "machineId",    Label = "Machine ID", Type = "text" },
        new() { Id = "operatorName", Label = "Operador",   Type = "text" },
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var machineId = selection.Filters.TryGetValue("machineId", out var m) ? m?.ToString() ?? "" : "";
        var operatorName = selection.Filters.TryGetValue("operatorName", out var o) ? o?.ToString() ?? "system" : "system";

        if (string.IsNullOrWhiteSpace(machineId))
            machineId = Environment.MachineName;

        var cert = await _integrityService.GenerateIntegrityCertificateAsync(machineId, operatorName);

        var fields = selection.Fields.Count > 0
            ? selection.Fields
            : AvailableFields.Where(f => f.DefaultIncluded).Select(f => f.Id).ToList();

        var lang = string.IsNullOrWhiteSpace(selection.Language) ? "SPA" : selection.Language!;

        var columns = fields.Select(id =>
        {
            if (ColumnI18n.TryGetValue(id, out var meta))
                return _translations.GetLabel(meta.Key, lang, meta.Es);
            return AvailableFields.FirstOrDefault(f => f.Id == id)?.Label ?? id;
        }).ToList();

        var components = cert.Components ?? new List<CertificateComponent>();
        var limit = selection.PreviewLimit ?? int.MaxValue;
        var rows = components
            .Take(limit)
            .Select(c => fields.Select(id => MapField(c, id)).ToArray())
            .ToList();

        return new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields.ToList(),
            Rows = rows,
            TotalRows = components.Count,
            Metadata =
            {
                ["dataset"]            = DatasetId,
                ["certificateId"]      = cert.CertificateId,
                ["certificateVersion"] = cert.Version,
                ["machineId"]          = cert.MachineId,
                ["machineName"]        = cert.MachineName,
                ["operatorName"]       = cert.OperatorName,
                ["operatingSystem"]    = cert.OperatingSystem,
                ["overallStatus"]      = cert.OverallStatus,
                ["contentHash"]        = cert.ContentHash,
                ["signature"]          = cert.Signature,
                ["signatureAlgorithm"] = cert.SignatureAlgorithm,
                ["signedAt"]           = cert.SignedAt,
                ["generatedAt"]        = cert.GeneratedAt,
                ["twinCatVersion"]     = cert.RuntimeInfo?.TwinCatVersion ?? "",
                ["twinCatStatus"]      = cert.RuntimeInfo?.TwinCatStatus ?? "",
                ["databaseStatus"]     = cert.RuntimeInfo?.DatabaseStatus ?? "",
                ["truncated"]          = selection.PreviewLimit.HasValue && components.Count > rows.Count,
            }
        };
    }

    private static object? MapField(CertificateComponent c, string fieldId) => fieldId switch
    {
        "name"            => c.Name,
        "version"         => c.Version,
        "commitShort"     => c.CommitShort,
        "branch"          => c.Branch,
        "commitDate"      => c.CommitDate,
        "commitAuthor"    => c.CommitAuthor,
        "integrity"       => c.Integrity,
        "isSigned"        => c.IsSigned,
        "signatureStatus" => c.SignatureStatus,
        "modifiedFiles"   => c.ModifiedFiles,
        _ => null
    };
}
