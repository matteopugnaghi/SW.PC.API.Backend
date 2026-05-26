// ============================================================================
// DateRangeFilterValue.cs — Resolutor de rangos de fechas (absoluto/relativo)
// ============================================================================
// Acepta como entrada lo que el wizard guarda en selection.Filters["dateRange"]:
//
//   Absoluto:  { "from": "2026-05-01", "to": "2026-05-26" }
//              { "mode": "absolute", "from": "...", "to": "..." }
//
//   Relativo:  { "mode": "relative", "value": 24, "unit": "h" }
//              { "mode": "relative", "value": 7,  "unit": "d", "anchor": "now" }
//
// Unidades soportadas: "m" (min), "h" (horas), "d" (días).
// Anchor: "now" (default).
//
// Resolve(now) devuelve (from, to) en UTC. Si la entrada está vacía/invalida,
// devuelve (null, null) y el provider decide qué hacer (típicamente no filtra).
// ============================================================================

using System.Globalization;
using System.Text.Json;

namespace SW.PC.API.Backend.Models.Export;

public static class DateRangeFilterValue
{
    public static (DateTime? From, DateTime? To) Resolve(object? raw, DateTime? nowUtc = null)
    {
        if (raw is null) return (null, null);
        var now = nowUtc ?? DateTime.UtcNow;

        // Normaliza a diccionario string→object
        IDictionary<string, object?>? dict = raw switch
        {
            IDictionary<string, object?> d => d,
            JsonElement je when je.ValueKind == JsonValueKind.Object => JsonElementToDict(je),
            _ => null,
        };
        if (dict is null) return (null, null);

        var mode = (GetString(dict, "mode") ?? "absolute").ToLowerInvariant();

        if (mode == "relative")
        {
            if (!TryGetDouble(dict, "value", out var value)) return (null, null);
            var unit = (GetString(dict, "unit") ?? "h").ToLowerInvariant();
            // value puede llegar como negativo o positivo: tratamos siempre como magnitud
            value = Math.Abs(value);
            if (value <= 0) return (null, null);

            TimeSpan span = unit switch
            {
                "m" or "min" or "minutes" => TimeSpan.FromMinutes(value),
                "h" or "hour" or "hours" => TimeSpan.FromHours(value),
                "d" or "day" or "days" => TimeSpan.FromDays(value),
                _ => TimeSpan.FromHours(value),
            };
            var to = now;
            var from = now - span;
            return (from, to);
        }

        // Absoluto
        var fromStr = GetString(dict, "from");
        var toStr = GetString(dict, "to");
        DateTime? fromDt = TryParseUtc(fromStr);
        DateTime? toDt = TryParseUtc(toStr, endOfDay: true);
        return (fromDt, toDt);
    }

    private static IDictionary<string, object?> JsonElementToDict(JsonElement je)
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in je.EnumerateObject())
        {
            d[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetDouble(out var dn) ? dn : (object?)prop.Value.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText(),
            };
        }
        return d;
    }

    private static string? GetString(IDictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is null) return null;
        return v.ToString();
    }

    private static bool TryGetDouble(IDictionary<string, object?> d, string key, out double value)
    {
        value = 0;
        if (!d.TryGetValue(key, out var v) || v is null) return false;
        if (v is double dv) { value = dv; return true; }
        if (v is float fv) { value = fv; return true; }
        if (v is int iv) { value = iv; return true; }
        if (v is long lv) { value = lv; return true; }
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static DateTime? TryParseUtc(string? s, bool endOfDay = false)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return null;
        }
        // Si la cadena venía sin parte horaria y es endOfDay, llevamos a 23:59:59
        if (endOfDay && dt.TimeOfDay == TimeSpan.Zero && s.Length <= 10)
        {
            dt = dt.AddDays(1).AddSeconds(-1);
        }
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
