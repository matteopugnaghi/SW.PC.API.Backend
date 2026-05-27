// ============================================================================
// SbomExportDatasetProvider.cs — Provider tabular del SBOM (EU CRA)
// ============================================================================
// Dataset: "cra.sbom"  (Source: "integrity")
//
// Permite enviar el SBOM existente a través del Wizard del Export Manager
// (carpeta de red, email, automatización…). Lee el SBOM ya generado vía
// ISbomService.GetSbomAsync() y lo aplana a una fila por componente. El
// host (InfoPanel) usará hiddenSteps=[0,1] para saltar Qué/Formato.
// ============================================================================

using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class SbomExportDatasetProvider : IExportDatasetProvider
{
    private readonly ISbomService _sbomService;
    private readonly IExportTranslationLookup _translations;

    public SbomExportDatasetProvider(
        ISbomService sbomService,
        IExportTranslationLookup translations)
    {
        _sbomService = sbomService;
        _translations = translations;
    }

    public string DatasetId => "cra.sbom";
    public string Source => "sbom";
    public string DisplayName => "SBOM (componentes)";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"]      = ("sbom.export.col.name",      "Componente"),
        ["version"]   = ("sbom.export.col.version",   "Versión"),
        ["type"]      = ("sbom.export.col.type",      "Tipo"),
        ["group"]     = ("sbom.export.col.group",     "Grupo"),
        ["license"]   = ("sbom.export.col.license",   "Licencia"),
        ["purl"]      = ("sbom.export.col.purl",      "PURL"),
        ["publisher"] = ("sbom.export.col.publisher", "Origen"),
        ["scope"]     = ("sbom.export.col.scope",     "Ámbito"),
        ["hash"]      = ("sbom.export.col.hash",      "Hash"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "name",      Label = "Componente",  DefaultIncluded = true },
        new() { Id = "version",   Label = "Versión",     DefaultIncluded = true },
        new() { Id = "type",      Label = "Tipo",        DefaultIncluded = true },
        new() { Id = "group",     Label = "Grupo",       DefaultIncluded = false },
        new() { Id = "license",   Label = "Licencia",    DefaultIncluded = true },
        new() { Id = "purl",      Label = "PURL",        DefaultIncluded = false },
        new() { Id = "publisher", Label = "Origen",      DefaultIncluded = true },
        new() { Id = "scope",     Label = "Ámbito",      DefaultIncluded = false },
        new() { Id = "hash",      Label = "Hash",        DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var sbom = await _sbomService.GetSbomAsync();
        var components = sbom?.Components ?? new List<SbomComponent>();

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
                ["dataset"] = DatasetId,
                ["generatedAt"] = DateTime.UtcNow.ToString("o"),
                ["sbomVersion"] = sbom?.SpecVersion ?? "",
                ["sbomSerialNumber"] = sbom?.SerialNumber ?? "",
                ["truncated"] = selection.PreviewLimit.HasValue && components.Count > rows.Count,
            }
        };
    }

    private static object? MapField(SbomComponent c, string fieldId) => fieldId switch
    {
        "name"      => c.Name,
        "version"   => c.Version,
        "type"      => c.Type,
        "group"     => c.Group,
        "license"   => FormatLicense(c.Licenses),
        "purl"      => c.Purl,
        "publisher" => c.Publisher,
        "scope"     => c.Scope,
        "hash"      => FormatHash(c.Hashes),
        _ => null
    };

    private static string FormatLicense(List<SbomLicense>? licenses)
    {
        if (licenses == null || licenses.Count == 0) return "";
        return string.Join(", ", licenses.Select(l =>
            l.Expression
            ?? l.License?.Id
            ?? l.License?.Name
            ?? "").Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string FormatHash(List<SbomHash>? hashes)
    {
        if (hashes == null || hashes.Count == 0) return "";
        var h = hashes[0];
        return $"{h.Alg}:{h.Content}";
    }
}
