// ============================================================================
// SmmDashboardLayoutSerializer — formato de SMM_UserDashboardLayouts.LayoutJson
// ============================================================================
// DEC-005: layout del dashboard SMM personalizable por usuario.
// Hoy solo se persiste el ORDEN de las tarjetas:
//     { "groupOrder": ["GroupName A", "GroupName B", ...] }
// Se guarda el NOMBRE del grupo (clave del Excel) y no su Id: "Sincronizar + purgar"
// puede recrear los Ids, pero los nombres se conservan. El JSON es un objeto para
// poder añadir más propiedades (tamaños, visibilidad) sin romper lo ya guardado.
// ============================================================================

using System.Text.Json;

namespace SW.PC.API.Backend.Services.Smm;

public static class SmmDashboardLayoutSerializer
{
    public const int MaxGroups = 500;
    public const int MaxGroupNameLength = 100; // = SmmGroup.GroupName

    /// <summary>Lee el orden guardado. JSON vacío, corrupto o de otro formato → lista vacía (orden por defecto).</summary>
    public static List<string> ReadGroupOrder(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson)) return new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(layoutJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("groupOrder", out var order)
                || order.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }
            return NormalizeGroupOrder(order.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()));
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>Descarta vacíos, nombres imposibles y duplicados, conservando el orden recibido.</summary>
    public static List<string> NormalizeGroupOrder(IEnumerable<string?>? names)
    {
        var result = new List<string>();
        if (names == null) return result;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxGroupNameLength) continue;
            if (!seen.Add(name)) continue;
            result.Add(name);
            if (result.Count >= MaxGroups) break;
        }
        return result;
    }

    public static string Serialize(IEnumerable<string> groupOrder) =>
        JsonSerializer.Serialize(new { groupOrder = groupOrder.ToList() });
}
