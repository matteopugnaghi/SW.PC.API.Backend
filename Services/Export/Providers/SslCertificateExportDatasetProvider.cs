// ============================================================================
// SslCertificateExportDatasetProvider.cs — Provider tabular del certificado
// HTTPS/SSL del backend (Kestrel) — EU CRA.
// ============================================================================
// Dataset: "cra.ssl.certificate"  (Source: "integrity-certificate")
//
// Lee el certificado configurado en Kestrel:Endpoints:Https:Certificate:Path
// (mismo origen que CertificateController.LoadServerCertificate) y lo expone
// como UNA fila tabular. NO incluye la clave privada — solo metadatos
// públicos del certificado (subject, issuer, fechas, thumbprint, SAN…).
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class SslCertificateExportDatasetProvider : IExportDatasetProvider
{
    private readonly IExportTranslationLookup _translations;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SslCertificateExportDatasetProvider> _logger;

    public SslCertificateExportDatasetProvider(
        IExportTranslationLookup translations,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<SslCertificateExportDatasetProvider> logger)
    {
        _translations = translations;
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    public string DatasetId => "cra.ssl.certificate";
    public string Source => "integrity-certificate";
    public string DisplayName => "Certificado SSL/HTTPS del servidor (EU CRA)";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["subject"]         = ("ssl.export.col.subject",         "Sujeto"),
        ["issuer"]          = ("ssl.export.col.issuer",          "Emisor"),
        ["thumbprint"]      = ("ssl.export.col.thumbprint",      "Huella (SHA-1)"),
        ["serialNumber"]    = ("ssl.export.col.serialNumber",    "Nº de serie"),
        ["notBefore"]       = ("ssl.export.col.notBefore",       "Válido desde"),
        ["notAfter"]        = ("ssl.export.col.notAfter",        "Válido hasta"),
        ["daysToExpire"]    = ("ssl.export.col.daysToExpire",    "Días para expirar"),
        ["signatureAlg"]    = ("ssl.export.col.signatureAlg",    "Algoritmo de firma"),
        ["keyAlgorithm"]    = ("ssl.export.col.keyAlgorithm",    "Algoritmo de clave"),
        ["keySize"]         = ("ssl.export.col.keySize",         "Tamaño de clave (bits)"),
        ["sanList"]         = ("ssl.export.col.sanList",         "Nombres alternativos (SAN)"),
        ["isSelfSigned"]    = ("ssl.export.col.isSelfSigned",    "Auto-firmado"),
        ["hasPrivateKey"]   = ("ssl.export.col.hasPrivateKey",   "Incluye clave privada"),
        ["sourcePath"]      = ("ssl.export.col.sourcePath",      "Ruta del fichero"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "subject",        Label = "Sujeto",                     DefaultIncluded = true },
        new() { Id = "issuer",         Label = "Emisor",                     DefaultIncluded = true },
        new() { Id = "thumbprint",     Label = "Huella (SHA-1)",             DefaultIncluded = true },
        new() { Id = "serialNumber",   Label = "Nº de serie",                DefaultIncluded = false },
        new() { Id = "notBefore",      Label = "Válido desde",               DefaultIncluded = true },
        new() { Id = "notAfter",       Label = "Válido hasta",               DefaultIncluded = true },
        new() { Id = "daysToExpire",   Label = "Días para expirar",          DefaultIncluded = true },
        new() { Id = "signatureAlg",   Label = "Algoritmo de firma",         DefaultIncluded = false },
        new() { Id = "keyAlgorithm",   Label = "Algoritmo de clave",         DefaultIncluded = true },
        new() { Id = "keySize",        Label = "Tamaño de clave (bits)",     DefaultIncluded = true },
        new() { Id = "sanList",        Label = "Nombres alternativos (SAN)", DefaultIncluded = true },
        new() { Id = "isSelfSigned",   Label = "Auto-firmado",               DefaultIncluded = true },
        new() { Id = "hasPrivateKey",  Label = "Incluye clave privada",      DefaultIncluded = false },
        new() { Id = "sourcePath",     Label = "Ruta del fichero",           DefaultIncluded = false },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

    public Task<ExportDataset> GetDatasetAsync(ExportSelection selection, CancellationToken ct = default)
    {
        var (cert, sourcePath) = LoadServerCertificate();

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

        var rows = new List<object?[]>();
        if (cert != null)
        {
            rows.Add(fields.Select(f => MapField(cert, sourcePath, f)).ToArray());
        }

        var result = new ExportDataset
        {
            Columns = columns,
            ColumnIds = fields.ToList(),
            Rows = rows,
            TotalRows = rows.Count,
            Metadata = new Dictionary<string, object?>
            {
                ["dataset"]      = DatasetId,
                ["generatedAt"]  = DateTime.UtcNow,
                ["certificateFound"] = cert != null,
                ["sourcePath"]   = sourcePath ?? "",
            }
        };

        cert?.Dispose();
        return Task.FromResult(result);
    }

    private (X509Certificate2? Cert, string? SourcePath) LoadServerCertificate()
    {
        try
        {
            var certPath = _configuration["Kestrel:Endpoints:Https:Certificate:Path"];
            var certPassword = _configuration["Kestrel:Endpoints:Https:Certificate:Password"];

            if (string.IsNullOrEmpty(certPath)) return (null, null);

            if (!Path.IsPathRooted(certPath))
                certPath = Path.Combine(_env.ContentRootPath, certPath);

            if (!File.Exists(certPath))
            {
                _logger.LogWarning("⚠️ SSL certificate file not found: {Path}", certPath);
                return (null, certPath);
            }

            var cert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.EphemeralKeySet);
            return (cert, certPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not load SSL certificate");
            return (null, null);
        }
    }

    private static object? MapField(X509Certificate2 cert, string? sourcePath, string fieldId) => fieldId switch
    {
        "subject"        => cert.Subject,
        "issuer"         => cert.Issuer,
        "thumbprint"     => cert.Thumbprint,
        "serialNumber"   => cert.SerialNumber,
        "notBefore"      => cert.NotBefore,
        "notAfter"       => cert.NotAfter,
        "daysToExpire"   => (int)Math.Floor((cert.NotAfter - DateTime.Now).TotalDays),
        "signatureAlg"   => cert.SignatureAlgorithm?.FriendlyName,
        "keyAlgorithm"   => cert.GetKeyAlgorithm() switch
        {
            "1.2.840.113549.1.1.1" => "RSA",
            "1.2.840.10045.2.1"    => "ECDSA",
            "1.2.840.10040.4.1"    => "DSA",
            var oid                => oid,
        },
        "keySize"        => GetKeySize(cert),
        "sanList"        => GetSanList(cert),
        "isSelfSigned"   => string.Equals(cert.Subject, cert.Issuer, StringComparison.Ordinal),
        "hasPrivateKey"  => cert.HasPrivateKey,
        "sourcePath"     => sourcePath,
        _                => null,
    };

    private static int? GetKeySize(X509Certificate2 cert)
    {
        try
        {
            using var rsa = cert.GetRSAPublicKey();
            if (rsa != null) return rsa.KeySize;
            using var ecdsa = cert.GetECDsaPublicKey();
            if (ecdsa != null) return ecdsa.KeySize;
        }
        catch { }
        return null;
    }

    private static string GetSanList(X509Certificate2 cert)
    {
        try
        {
            var sanExt = cert.Extensions["2.5.29.17"];
            if (sanExt == null) return "";
            // Format() devuelve algo como "DNS Name=localhost, DNS Name=192.168.2.161"
            return sanExt.Format(false);
        }
        catch
        {
            return "";
        }
    }
}
