// ============================================================================
// AuthorizedSigningKeysExportDatasetProvider.cs — Provider tabular de las
// claves SSH/GPG autorizadas para firmar commits/releases (EU CRA).
// ============================================================================
// Dataset: "cra.signing.keys"  (Source: "integrity-certificate")
//
// Lee authorized_signing_keys.json del BaseDirectory (el mismo fichero que
// usa GitOperationsService para verificar firmas cross-server) y lo expone
// como filas tabulares al Wizard de exportación. NO incluye claves privadas.
// ============================================================================

using System.Text.Json;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class AuthorizedSigningKeysExportDatasetProvider : IExportDatasetProvider
{
    private readonly IExportTranslationLookup _translations;
    private readonly ILogger<AuthorizedSigningKeysExportDatasetProvider> _logger;

    private static readonly string AuthorizedKeysFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");

    public AuthorizedSigningKeysExportDatasetProvider(
        IExportTranslationLookup translations,
        ILogger<AuthorizedSigningKeysExportDatasetProvider> logger)
    {
        _translations = translations;
        _logger = logger;
    }

    public string DatasetId => "cra.signing.keys";
    public string Source => "integrity-certificate";
    public string DisplayName => "Claves SSH autorizadas para firmar (EU CRA)";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fingerprint"]    = ("signingkeys.export.col.fingerprint",    "Huella (Fingerprint)"),
        ["ownerName"]      = ("signingkeys.export.col.ownerName",      "Propietario"),
        ["ownerEmail"]     = ("signingkeys.export.col.ownerEmail",     "Email"),
        ["machineName"]    = ("signingkeys.export.col.machineName",    "Máquina"),
        ["authorizedAt"]   = ("signingkeys.export.col.authorizedAt",   "Autorizada el"),
        ["authorizedBy"]   = ("signingkeys.export.col.authorizedBy",   "Autorizada por"),
        ["publicKey"]      = ("signingkeys.export.col.publicKey",      "Clave pública"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "fingerprint",    Label = "Huella (Fingerprint)", DefaultIncluded = true },
        new() { Id = "ownerName",      Label = "Propietario",          DefaultIncluded = true },
        new() { Id = "ownerEmail",     Label = "Email",                DefaultIncluded = true },
        new() { Id = "machineName",    Label = "Máquina",              DefaultIncluded = true },
        new() { Id = "authorizedAt",   Label = "Autorizada el",        DefaultIncluded = true },
        new() { Id = "authorizedBy",   Label = "Autorizada por",       DefaultIncluded = true },
        new() { Id = "publicKey",      Label = "Clave pública",        DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

    public async Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var keys = await LoadAsync(ct);

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
        var slice = preview.HasValue ? keys.Take(preview.Value).ToList() : keys;

        var rows = slice.Select(k => fields.Select(f => MapField(k, f)).ToArray()).ToList();

        return new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields.ToList(),
            Rows = rows,
            TotalRows = keys.Count,
            Metadata = new Dictionary<string, object?>
            {
                ["dataset"]     = DatasetId,
                ["generatedAt"] = DateTime.UtcNow,
                ["truncated"]   = preview.HasValue && keys.Count > rows.Count,
            }
        };
    }

    private async Task<List<AuthorizedSigningKey>> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(AuthorizedKeysFilePath)) return new List<AuthorizedSigningKey>();
            var json = await File.ReadAllTextAsync(AuthorizedKeysFilePath, ct);
            if (string.IsNullOrWhiteSpace(json)) return new List<AuthorizedSigningKey>();
            return JsonSerializer.Deserialize<List<AuthorizedSigningKey>>(json) ?? new List<AuthorizedSigningKey>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not load authorized_signing_keys.json");
            return new List<AuthorizedSigningKey>();
        }
    }

    private static object? MapField(AuthorizedSigningKey k, string fieldId) => fieldId switch
    {
        "fingerprint"    => k.Fingerprint,
        "ownerName"      => k.OwnerName,
        "ownerEmail"     => k.OwnerEmail,
        "machineName"    => k.MachineName,
        "authorizedAt"   => k.AuthorizedAt,
        "authorizedBy"   => k.AuthorizedBy,
        "publicKey"      => k.PublicKey,
        _                => null,
    };

    // Modelo local — sólo lectura del JSON, sin claves privadas.
    private class AuthorizedSigningKey
    {
        public string? Fingerprint { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerEmail { get; set; }
        public DateTime? AuthorizedAt { get; set; }
        public string? AuthorizedBy { get; set; }
        public string? PublicKey { get; set; }
        public string? MachineName { get; set; }
    }
}
