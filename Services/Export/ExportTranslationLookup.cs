// ============================================================================
// ExportTranslationLookup.cs — Lookup ligero de traducciones para exports
// ============================================================================
// Lee Projects/{id}/translations/translations.json del proyecto activo y
// expone un GetLabel(key, lang, fallback). Cache por proyecto en memoria
// con expiración corta (5 min) — alineado con TranslationsController.
// ============================================================================

using System.Text.Json;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportTranslationLookup
{
    /// <summary>
    /// Devuelve la traducción del labelId en el idioma indicado.
    /// Si lang es null/vacío o no encuentra entrada → devuelve fallback.
    /// </summary>
    string GetLabel(string labelId, string? lang, string fallback);
}

public class ExportTranslationLookup : IExportTranslationLookup
{
    private readonly IRequestProjectContext _projectContext;
    private readonly ILogger<ExportTranslationLookup> _logger;

    // Cache estática compartida (key = projectId)
    private static readonly Dictionary<string, (Dictionary<string, Dictionary<string, string>> Map, DateTime LoadedAt)> _cache
        = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly object _cacheLock = new();

    public ExportTranslationLookup(
        IRequestProjectContext projectContext,
        ILogger<ExportTranslationLookup> logger)
    {
        _projectContext = projectContext;
        _logger = logger;
    }

    public string GetLabel(string labelId, string? lang, string fallback)
    {
        if (string.IsNullOrWhiteSpace(lang)) return fallback;
        var normalized = lang.Trim().ToUpperInvariant();

        try
        {
            var map = LoadOrGetCached();
            if (map.TryGetValue(labelId, out var langs))
            {
                if (langs.TryGetValue(normalized, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ExportTranslationLookup: fallback para '{Key}'", labelId);
        }

        return fallback;
    }

    private Dictionary<string, Dictionary<string, string>> LoadOrGetCached()
    {
        var projectId = _projectContext.ProjectId ?? "default";
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(projectId, out var cached)
                && DateTime.Now - cached.LoadedAt < _cacheExpiration)
            {
                return cached.Map;
            }
        }

        var path = Path.Combine(_projectContext.TranslationsPath, "translations.json");
        if (!File.Exists(path))
        {
            var empty = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            lock (_cacheLock) { _cache[projectId] = (empty, DateTime.Now); }
            return empty;
        }

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // El archivo del proyecto tiene la forma:
        //   { "metadata": {...}, "pages": {...}, "translations": { labelId: { LANG: text } } }
        // Por compatibilidad aceptamos también labels colgando de la raíz
        // (formato plano que usaron versiones tempranas).
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("translations", out var trEl) && trEl.ValueKind == JsonValueKind.Object)
        {
            FillFromLabels(trEl, result);
        }
        else
        {
            FillFromLabels(root, result, skipReservedRoots: true);
        }

        lock (_cacheLock) { _cache[projectId] = (result, DateTime.Now); }
        return result;
    }

    private static void FillFromLabels(
        JsonElement labelsObj,
        Dictionary<string, Dictionary<string, string>> target,
        bool skipReservedRoots = false)
    {
        foreach (var prop in labelsObj.EnumerateObject())
        {
            if (skipReservedRoots)
            {
                if (prop.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Name.Equals("pages", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Name.Equals("translations", StringComparison.OrdinalIgnoreCase)) continue;
            }
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;

            var inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var langProp in prop.Value.EnumerateObject())
            {
                if (langProp.Value.ValueKind == JsonValueKind.String)
                    inner[langProp.Name] = langProp.Value.GetString() ?? "";
            }
            if (inner.Count > 0) target[prop.Name] = inner;
        }
    }
}
