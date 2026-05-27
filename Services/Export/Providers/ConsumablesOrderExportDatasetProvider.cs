// ============================================================================
// ConsumablesOrderExportDatasetProvider.cs — Pedido de consumibles
// ============================================================================
// Dataset: "orders.consumables"  (Source: "orders")
//
// Una fila por línea de pedido. Mismas columnas que la versión imprimible
// del pedido (PrintPreviewModal): SKU, Descripción, Cantidad, Unidad, Elemento.
//
// Los items NO se persisten en la tarea: viajan en `selection.Metadata["items"]`
// vía `getRuntimeMetadata` desde ConsumablesOrderModal (frontend) en cada
// ejecución manual. Si llega vacío, devuelve dataset vacío con mensaje.
//
// Metadatos esperados en selection.Metadata (todos opcionales salvo items):
//   - items         : List<{ partSku, partDescription?, partUnit?, quantity, elementName? }>
//   - customerName  : string  (suele ser el elementName del modal)
//   - elementName   : string  (cuando todas las líneas son del mismo elemento)
//   - notes         : string
//   - orderRef      : string  (si no viene, se genera ORD-{yyyyMMddHHmmss})
// ============================================================================

using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Services.Export.Providers;

public class ConsumablesOrderExportDatasetProvider : IExportDatasetProvider
{
    private readonly IRequestProjectContext _projectContext;
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IExportTranslationLookup _translations;

    public ConsumablesOrderExportDatasetProvider(
        IRequestProjectContext projectContext,
        IProjectDbContextFactory dbFactory,
        IExportTranslationLookup translations)
    {
        _projectContext = projectContext;
        _dbFactory = dbFactory;
        _translations = translations;
    }

    public string DatasetId => "orders.consumables";
    public string Source => "orders";
    public string DisplayName => "Pedido de consumibles";

    private static readonly Dictionary<string, (string Key, string Es)> ColumnI18n = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lineNumber"]  = ("orders.export.col.lineNumber",  "Línea"),
        ["sku"]         = ("orders.export.col.sku",         "SKU"),
        ["description"] = ("orders.export.col.description", "Descripción"),
        ["quantity"]    = ("orders.export.col.quantity",    "Cantidad"),
        ["unit"]        = ("orders.export.col.unit",        "Unidad"),
        ["elementName"] = ("orders.export.col.elementName", "Elemento"),
    };

    public IReadOnlyList<ExportFieldDefinition> AvailableFields { get; } = new List<ExportFieldDefinition>
    {
        new() { Id = "lineNumber",  Label = "Línea",       DefaultIncluded = false },
        new() { Id = "sku",         Label = "SKU",         DefaultIncluded = true  },
        new() { Id = "description", Label = "Descripción", DefaultIncluded = true  },
        new() { Id = "quantity",    Label = "Cantidad",    DefaultIncluded = true  },
        new() { Id = "unit",        Label = "Unidad",      DefaultIncluded = true  },
        new() { Id = "elementName", Label = "Elemento",    DefaultIncluded = true  },
    };

    public IReadOnlyList<ExportFilterDefinition> AvailableFilters { get; } = new List<ExportFilterDefinition>();

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

        var orderRef = ReadString(selection.Metadata, "orderRef")
                        ?? $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var customer = ReadString(selection.Metadata, "customerName");
        var elementName = ReadString(selection.Metadata, "elementName");
        var notes = ReadString(selection.Metadata, "notes");

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
                ["orderRef"] = orderRef,
            }
        };
        if (!string.IsNullOrWhiteSpace(customer)) ds.Metadata["customerName"] = customer;
        if (!string.IsNullOrWhiteSpace(elementName)) ds.Metadata["elementName"] = elementName;
        if (!string.IsNullOrWhiteSpace(notes)) ds.Metadata["notes"] = notes;

        var items = ReadItems(selection.Metadata);
        if (items.Count == 0)
        {
            ds.Metadata["warning"] = "No se recibieron líneas de pedido (selection.Metadata['items'] vacío).";
            return ds;
        }

        // Enriquecer descripciones/unidades/elemento desde catálogo (SmmConsumables)
        // cuando falten en los items recibidos.
        var skus = items.Select(i => i.PartSku)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

        Dictionary<string, (string? Description, string? Unit, string? ElementName)> catalog = new(StringComparer.OrdinalIgnoreCase);
        if (skus.Count > 0)
        {
            using var db = _dbFactory.CreateDbContext();
            var rows = await db.SmmConsumables.AsNoTracking()
                .Where(c => skus.Contains(c.PartSku))
                .Select(c => new
                {
                    c.PartSku,
                    c.PartDescription,
                    c.PartUnit,
                    ElementName = db.SmmElements.Where(e => e.Id == c.ElementId).Select(e => e.ElementName).FirstOrDefault()
                })
                .ToListAsync(ct);
            foreach (var r in rows)
            {
                if (!catalog.ContainsKey(r.PartSku))
                    catalog[r.PartSku] = (r.PartDescription, r.PartUnit, r.ElementName);
            }
        }

        var defaultUnit = _translations.GetLabel("statistics.order.qr.label.unit", lang, "ud");

        var resultRows = new List<object?[]>(items.Count);
        for (int idx = 0; idx < items.Count; idx++)
        {
            var it = items[idx];
            catalog.TryGetValue(it.PartSku ?? string.Empty, out var c);
            var description = !string.IsNullOrWhiteSpace(it.PartDescription) ? it.PartDescription : c.Description;
            var unit = !string.IsNullOrWhiteSpace(it.PartUnit) ? it.PartUnit : (c.Unit ?? defaultUnit);
            var elName = !string.IsNullOrWhiteSpace(it.ElementName) ? it.ElementName : (c.ElementName ?? elementName);

            var row = new object?[fields.Count];
            for (int f = 0; f < fields.Count; f++)
            {
                row[f] = fields[f] switch
                {
                    "lineNumber"  => idx + 1,
                    "sku"         => it.PartSku,
                    "description" => description,
                    "quantity"    => it.Quantity,
                    "unit"        => unit,
                    "elementName" => elName,
                    _             => null,
                };
            }
            resultRows.Add(row);
        }

        ds.Rows = resultRows;
        ds.TotalRows = resultRows.Count;
        return ds;
    }

    // ────────────────────────────────────────────────────────────────────────
    private static string? ReadString(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var raw) || raw is null) return null;
        return raw switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => raw.ToString(),
        };
    }

    private static List<OrderLineDto> ReadItems(Dictionary<string, object?> dict)
    {
        if (!dict.TryGetValue("items", out var raw) || raw is null) return new();

        // Caso 1: lista nativa.
        if (raw is IEnumerable<object?> list && raw is not string)
        {
            var output = new List<OrderLineDto>();
            foreach (var el in list)
            {
                var dto = ParseLine(el);
                if (dto != null) output.Add(dto);
            }
            return output;
        }

        // Caso 2: JsonElement (lo más habitual cuando viene del frontend).
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var output = new List<OrderLineDto>(je.GetArrayLength());
            foreach (var el in je.EnumerateArray())
            {
                var dto = ParseLine(el);
                if (dto != null) output.Add(dto);
            }
            return output;
        }

        return new();
    }

    private static OrderLineDto? ParseLine(object? raw)
    {
        if (raw is null) return null;
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            return new OrderLineDto
            {
                PartSku         = GetStr(je, "partSku") ?? GetStr(je, "sku"),
                PartDescription = GetStr(je, "partDescription") ?? GetStr(je, "description"),
                PartUnit        = GetStr(je, "partUnit") ?? GetStr(je, "unit"),
                Quantity        = GetDouble(je, "quantity") ?? 0,
                ElementName     = GetStr(je, "elementName"),
            };
        }
        // Fallback genérico via reflection / IDictionary
        if (raw is IDictionary<string, object?> d)
        {
            return new OrderLineDto
            {
                PartSku         = DictStr(d, "partSku") ?? DictStr(d, "sku"),
                PartDescription = DictStr(d, "partDescription") ?? DictStr(d, "description"),
                PartUnit        = DictStr(d, "partUnit") ?? DictStr(d, "unit"),
                Quantity        = DictDouble(d, "quantity") ?? 0,
                ElementName     = DictStr(d, "elementName"),
            };
        }
        return null;
    }

    private static string? GetStr(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Null ? null : v.ToString();
    }

    private static double? GetDouble(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetDouble(out var d) ? d : null,
            JsonValueKind.String => double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ => null,
        };
    }

    private static string? DictStr(IDictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v != null ? v.ToString() : null;

    private static double? DictDouble(IDictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is null) return null;
        if (v is double dd) return dd;
        if (v is int i) return i;
        if (v is long l) return l;
        if (v is decimal m) return (double)m;
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    private class OrderLineDto
    {
        public string? PartSku { get; set; }
        public string? PartDescription { get; set; }
        public string? PartUnit { get; set; }
        public double Quantity { get; set; }
        public string? ElementName { get; set; }
    }
}
