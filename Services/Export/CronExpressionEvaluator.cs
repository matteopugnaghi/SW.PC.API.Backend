// ============================================================================
// CronExpressionEvaluator.cs — Parser/evaluador cron de 5 campos (sin libs)
// ============================================================================
// Formato: "minuto hora dia mes dia-semana"
//   minuto      0-59
//   hora        0-23
//   día (mes)   1-31
//   mes         1-12
//   día semana  0-6  (0 = domingo, 7 también acepta domingo)
//
// Sintaxis por campo:
//   *              cualquier valor
//   N              valor exacto
//   N1,N2,N3       lista
//   N1-N2          rango (inclusivo)
//   */N            cada N (paso desde el min del campo)
//   N1-N2/N        rango con paso
//
// Limitaciones a propósito (.NET 8, sin libs externas):
//   - No soporta nombres ("JAN", "MON")
//   - No soporta "L", "W", "?", "#"
//   - Cuando se especifican AMBOS día-mes y día-semana, se aplica AND
//     (en cron clásico de Vixie es OR; nuestra implementación es estricta
//     para evitar disparos inesperados). Si quieres "todos los lunes" usa
//     "0 7 * * 1" con día-mes = "*".
// ============================================================================

using System.Globalization;

namespace SW.PC.API.Backend.Services.Export;

public sealed class CronExpressionEvaluator
{
    private readonly bool[] _minutes  = new bool[60];
    private readonly bool[] _hours    = new bool[24];
    private readonly bool[] _days     = new bool[32]; // 1..31
    private readonly bool[] _months   = new bool[13]; // 1..12
    private readonly bool[] _weekdays = new bool[7];  // 0..6 (0=domingo)

    public string Expression { get; }

    private CronExpressionEvaluator(string expression) { Expression = expression; }

    /// <summary>Devuelve (ok, error). Si ok=true, evaluator no es null.</summary>
    public static (bool ok, string? error, CronExpressionEvaluator? evaluator) TryParse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return (false, "Expresión cron vacía", null);

        var parts = expression.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            return (false, $"Se esperaban 5 campos separados por espacios, se recibieron {parts.Length}.", null);

        var ev = new CronExpressionEvaluator(expression);

        try
        {
            FillField(parts[0], 0, 59, ev._minutes);
            FillField(parts[1], 0, 23, ev._hours);
            FillField(parts[2], 1, 31, ev._days);
            FillField(parts[3], 1, 12, ev._months);
            FillWeekdayField(parts[4], ev._weekdays);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }

        return (true, null, ev);
    }

    /// <summary>Indica si el minuto representado por <paramref name="when"/> coincide con la expresión.</summary>
    public bool IsDue(DateTime when)
    {
        // weekday: DayOfWeek.Sunday == 0
        var dow = (int)when.DayOfWeek;
        return _minutes[when.Minute]
            && _hours[when.Hour]
            && _days[when.Day]
            && _months[when.Month]
            && _weekdays[dow];
    }

    // ─────────────────────── parsing helpers ───────────────────────

    private static void FillField(string raw, int min, int max, bool[] set)
    {
        // Limpiar todo
        for (int i = 0; i < set.Length; i++) set[i] = false;

        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            ParseToken(token.Trim(), min, max, set);
        }
    }

    private static void FillWeekdayField(string raw, bool[] set)
    {
        // Limpiar
        for (int i = 0; i < set.Length; i++) set[i] = false;

        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            // Aceptar 0..7 (7 == domingo == 0)
            ParseToken(token.Trim(), 0, 7, set, weekdayWrap: true);
        }
    }

    private static void ParseToken(string token, int min, int max, bool[] set, bool weekdayWrap = false)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Campo cron vacío");

        int step = 1;
        string rangePart = token;

        // separa paso */N o N-N/N
        var slash = token.IndexOf('/');
        if (slash >= 0)
        {
            rangePart = token[..slash];
            var stepStr = token[(slash + 1)..];
            if (!int.TryParse(stepStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out step) || step <= 0)
                throw new ArgumentException($"Paso inválido en '{token}'");
        }

        int from, to;
        if (rangePart == "*")
        {
            from = min; to = max;
        }
        else if (rangePart.Contains('-'))
        {
            var dash = rangePart.IndexOf('-');
            if (!int.TryParse(rangePart[..dash], NumberStyles.Integer, CultureInfo.InvariantCulture, out from)
                || !int.TryParse(rangePart[(dash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out to))
                throw new ArgumentException($"Rango inválido en '{token}'");
        }
        else
        {
            if (!int.TryParse(rangePart, NumberStyles.Integer, CultureInfo.InvariantCulture, out from))
                throw new ArgumentException($"Valor inválido en '{token}'");
            to = from;
        }

        if (from < min || to > max || from > to)
            throw new ArgumentException($"Rango fuera de límites [{min}..{max}] en '{token}'");

        for (int v = from; v <= to; v += step)
        {
            int idx = v;
            if (weekdayWrap && idx == 7) idx = 0;
            set[idx] = true;
        }
    }
}
