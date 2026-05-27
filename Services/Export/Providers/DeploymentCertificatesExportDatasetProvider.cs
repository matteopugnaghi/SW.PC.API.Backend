// ============================================================================
// DeploymentCertificatesExportDatasetProvider.cs — Provider tabular de los
// certificados de despliegue (EU CRA) generados en cada push/commit.
// ============================================================================
// Dataset: "cra.deployment.certificates"  (Source: "integrity-certificate")
//
// Lee deployment_certificates.json del BaseDirectory (el mismo fichero que
// alimenta la vista GitController.GetDeploymentCertificates) y lo expone
// como filas tabulares al Wizard de exportación.
// ============================================================================

using System.Text.Json;
using SW.PC.API.Backend.Controllers;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class DeploymentCertificatesExportDatasetProvider : IExportDatasetProvider
{
    private readonly IExportTranslationLookup _translations;
    private readonly ILogger<DeploymentCertificatesExportDatasetProvider> _logger;

    // Mismo path que GitController.DeploymentLogPath.
    private static readonly string DeploymentLogPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deployment_certificates.json");

    public DeploymentCertificatesExportDatasetProvider(
        IExportTranslationLookup translations,
        ILogger<DeploymentCertificatesExportDatasetProvider> logger)
    {
        _translations = translations;
        _logger = logger;
    }

    public string DatasetId => "cra.deployment.certificates";
    public string Source => "integrity-certificate";
    public string DisplayName => "Certificados de despliegue (EU CRA)";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["certificateId"]  = ("deployment.export.col.certificateId",  "ID Certificado"),
        ["timestamp"]      = ("deployment.export.col.timestamp",      "Fecha/Hora"),
        ["repository"]     = ("deployment.export.col.repository",     "Repositorio"),
        ["machineId"]      = ("deployment.export.col.machineId",      "Máquina"),
        ["operatorName"]   = ("deployment.export.col.operatorName",   "Operador"),
        ["commitHash"]     = ("deployment.export.col.commitHash",     "Commit"),
        ["branch"]         = ("deployment.export.col.branch",         "Rama"),
        ["action"]         = ("deployment.export.col.action",         "Acción"),
        ["description"]    = ("deployment.export.col.description",    "Descripción"),
        ["integrityHash"]  = ("deployment.export.col.integrityHash",  "Hash Integridad"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "certificateId",  Label = "ID Certificado",   DefaultIncluded = true },
        new() { Id = "timestamp",      Label = "Fecha/Hora",       DefaultIncluded = true },
        new() { Id = "repository",     Label = "Repositorio",      DefaultIncluded = true },
        new() { Id = "machineId",      Label = "Máquina",          DefaultIncluded = true },
        new() { Id = "operatorName",   Label = "Operador",         DefaultIncluded = true },
        new() { Id = "commitHash",     Label = "Commit",           DefaultIncluded = true },
        new() { Id = "branch",         Label = "Rama",             DefaultIncluded = false },
        new() { Id = "action",         Label = "Acción",           DefaultIncluded = true },
        new() { Id = "description",    Label = "Descripción",      DefaultIncluded = true },
        new() { Id = "integrityHash",  Label = "Hash Integridad",  DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>
    {
        new() { Id = "repository", Label = "Repositorio", Type = "text" },
    };

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var certs = await LoadAsync(ct);

        var repoFilter = selection.Filters.TryGetValue("repository", out var rv) ? rv?.ToString() : null;
        if (!string.IsNullOrWhiteSpace(repoFilter))
        {
            certs = certs.Where(c => string.Equals(c.Repository, repoFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        certs = certs.OrderByDescending(c => c.Timestamp).ToList();

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

        var preview = selection.PreviewLimit;
        var slice = preview.HasValue ? certs.Take(preview.Value).ToList() : certs;

        var rows = slice.Select(c => fields.Select(f => MapField(c, f)).ToArray()).ToList();

        return new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields.ToList(),
            Rows = rows,
            TotalRows = certs.Count,
            Metadata = new Dictionary<string, object?>
            {
                ["dataset"]     = DatasetId,
                ["generatedAt"] = DateTime.UtcNow,
                ["repository"]  = repoFilter ?? "",
                ["truncated"]   = preview.HasValue && certs.Count > rows.Count,
            }
        };
    }

    private async Task<List<DeploymentCertificate>> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(DeploymentLogPath)) return new List<DeploymentCertificate>();
            var json = await File.ReadAllTextAsync(DeploymentLogPath, ct);
            if (string.IsNullOrWhiteSpace(json)) return new List<DeploymentCertificate>();
            return JsonSerializer.Deserialize<List<DeploymentCertificate>>(json) ?? new List<DeploymentCertificate>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not load deployment_certificates.json");
            return new List<DeploymentCertificate>();
        }
    }

    private static object? MapField(DeploymentCertificate c, string fieldId) => fieldId switch
    {
        "certificateId"  => c.CertificateId,
        "timestamp"      => c.Timestamp,
        "repository"     => c.Repository,
        "machineId"      => c.MachineId,
        "operatorName"   => c.OperatorName,
        "commitHash"     => c.CommitHash,
        "branch"         => c.Branch,
        "action"         => c.Action,
        "description"    => c.Description,
        "integrityHash"  => c.IntegrityHash,
        _                => null,
    };
}
