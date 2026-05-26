// ============================================================================
// SmmAlarmTextResolver.cs — Helper común para resolver textos legibles
// ============================================================================
// El EdgeWatcher recibe notificaciones de variables `st_alarmHistPc[N].Type`
// (historial), pero el Excel `PLC_Alarms` define las variables ACTIVAS
// `st_alarmPc.Type[N]`. Por tanto FindByPlcVariable() falla siempre.
//
// Este helper parsea el nombre PLC histórico, extrae Index+Type y resuelve
// el texto via AlarmConfiguration.Alarms/Notifications/Infos por Index.
// ============================================================================

using System.Text.RegularExpressions;
using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Services.Smm;

internal static class SmmAlarmTextResolver
{
    // Acepta tanto st_alarmHistPc[N].Type como st_alarmPc.Type[N]
    private static readonly Regex HistRegex = new(
        @"st_alarmHistPc\[(\d+)\]\.(Alarm|Notification|Info)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ActiveRegex = new(
        @"st_alarmPc\.(Alarm|Notification|Info)\[(\d+)\]$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Resuelve el texto legible (en idioma SPA por defecto) para una variable PLC
    /// de alarma. Soporta `st_alarmHistPc[N].Type` y `st_alarmPc.Type[N]`.
    /// Devuelve null si no se encuentra match.
    /// </summary>
    public static string? Resolve(AlarmConfiguration? cfg, string plcVariable, string language = "SPA")
    {
        if (cfg == null || string.IsNullOrWhiteSpace(plcVariable)) return null;

        int index; string type;
        var m = HistRegex.Match(plcVariable);
        if (m.Success) { index = int.Parse(m.Groups[1].Value); type = m.Groups[2].Value; }
        else
        {
            m = ActiveRegex.Match(plcVariable);
            if (!m.Success) return null;
            type = m.Groups[1].Value;
            index = int.Parse(m.Groups[2].Value);
        }

        var list = type.ToLowerInvariant() switch
        {
            "alarm"        => cfg.Alarms,
            "notification" => cfg.Notifications,
            "info"         => cfg.Infos,
            _              => null
        };
        if (list == null) return null;

        var def = list.FirstOrDefault(d => d.Index == index);
        if (def == null) return null;

        var txt = def.GetText(language);
        return string.IsNullOrWhiteSpace(txt) ? null : txt;
    }
}
