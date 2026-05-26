// ============================================================================
// SmmTemplateBuilder — One-shot tool to build/refresh ProjectConfig.xlsm
// for Projects/_template/config/ with SMM sheets + System Config.
// ----------------------------------------------------------------------------
// Decisiones FROZEN aplicadas:
//   DEC-017: Stats_Elements (6 cols), Stats_Variables (13 cols), Stats_Groups (23 cols base).
//   DEC-018: Stats_Groups +CycleRunningVar (24) +ShowCycleStart/End/Duration (25-27).
//   DEC-019: Stats_Consumables (6 cols).
//   DEC-020: Stats_Groups +AlarmHistVar (28).
//   DEC-024: System Config.SystemDeliveryDate (única fecha máquina).
//   DEC-026: System Config.ContinuousReadTime (default "03:00", regla R16 HH:mm).
// ----------------------------------------------------------------------------
// Uso:
//   cd Tools\SmmTemplateBuilder
//   dotnet run --                                      # genera en _template default
//   dotnet run -- "..\..\..\Projects\cliente-x\config" # genera en otro proyecto
// ============================================================================

using ClosedXML.Excel;

string outputDir = args.Length > 0
    ? args[0]
    : System.IO.Path.GetFullPath(System.IO.Path.Combine(
        System.AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "Projects", "_template", "config"));

System.IO.Directory.CreateDirectory(outputDir);
var outputFile = System.IO.Path.Combine(outputDir, "ProjectConfig.xlsm");

System.Console.WriteLine($"📁 Output: {outputFile}");

using var wb = new XLWorkbook();

// ─── Hoja 1: Stats_Elements (DEC-017, 6 cols) ───
var wsElements = wb.Worksheets.Add("Stats_Elements");
WriteHeader(wsElements, new[]
{
    "ElementId", "ElementName", "ComponentLocation3D",
    "SKU_Aquafrisch", "Manufacturer", "Model"
});

// ─── Hoja 2: Stats_Variables (DEC-017, 13 cols + DEC-016 Formula/Scope) ───
var wsVars = wb.Worksheets.Add("Stats_Variables");
WriteHeader(wsVars, new[]
{
    "VarName", "ElementId", "PlcVariable", "Unit",
    "ReadFrequency", "RunningBitVar", "Critical", "Warning",
    "ResetOnMaintenance", "FormulaScope", "Formula",
    "Description", "DescriptionEN"
});

// ─── Hoja 3: Stats_Consumables (DEC-019, 6 cols, 4 obligatorias) ───
var wsCons = wb.Worksheets.Add("Stats_Consumables");
WriteHeader(wsCons, new[]
{
    "ElementId", "TaskName", "PartSku", "PartDescription",
    "PartUnit", "PartDefaultQuantity"
});

// ─── Hoja 4: Stats_Groups (formato compacto leído por SmmExcelSyncService, A..O) ───
var wsGroups = wb.Worksheets.Add("Stats_Groups");
WriteHeader(wsGroups, new[]
{
    // A..D
    "GroupName", "UiType", "ReadFrequency", "CycleRunningVar",
    // E..G
    "ShowCycleStart", "ShowCycleEnd", "ShowCycleDuration",
    // H..L
    "AlarmHistVar", "LayoutWidth", "LayoutHeight", "LayoutPinned", "LayoutColor",
    // M..N (Continuous por grupo)
    "ContinuousReadIntervalSec", "ContinuousRetentionDays",
    // O (gating de grupo)
    "RunningBitVar",
    // P..Q
    "DonutMode", "ShowInMaintenance",
    // R..S (columnas opcionales en tabla on-screen y wizard de exportación)
    "ShowCycleStatus", "ShowCycleEndedReason"
});

// Fila 2 = descripciones (no leídas por el sync, que arranca buscando datos en la fila 2
// SOLO si A2 no está vacío; aquí se escribe en notas/comentario para no romper el sync).
// Para no interferir con el parser, dejamos la fila 2 vacía y añadimos las descripciones
// como comentario (cell note) en la cabecera.
var groupHeaderNotes = new (string Col, string Note)[]
{
    ("A1", "Nombre único del grupo (clave de upsert)."),
    ("B1", "Tipo de UI en la card del dashboard:\n  • Table        = tabla snapshots × variables (sin gráfico ni KPIs)\n  • Kpi          = TODAS las variables numéricas como KPIs grandes en rejilla (sin gráfico)\n  • Stat         = TODAS las variables como KPIs compactos + LineChart debajo (combo, default recomendado)\n  • LineChart    = solo gráfico de líneas (sustituye al antiguo 'Chart'; Excels antiguos con 'Chart' se mapean automáticamente aquí)\n  • BarChart     = solo gráfico de barras\n  • GaugeChart   = solo gauge (1 valor con umbrales warn/crit)\n  • DonutChart   = solo donut (proporciones)\n  • ScatterChart = solo scatter (correlación 2 variables)\n  • HeatmapChart = solo heatmap (densidad temporal)"),
    ("C1", "Modo lectura: Continuous (snapshot diario o cíclico) | PerCycle (lectura por flanco) | OnDemand."),
    ("D1", "Variable PLC BOOL que delimita un ciclo PerCycle (flanco FALSE→TRUE arranca, TRUE→FALSE cierra)."),
    ("E1", "Mostrar columna 'Inicio ciclo' en la UI (true/false). Solo PerCycle."),
    ("F1", "Mostrar columna 'Fin ciclo' en la UI (true/false). Solo PerCycle."),
    ("G1", "Mostrar columna 'Duración ciclo' en la UI (true/false). Solo PerCycle."),
    ("H1", "Variable PLC con histórico de alarmas asociado al grupo (DEC-020)."),
    ("I1", "Ancho de la card en el dashboard (grid units, 1-12)."),
    ("J1", "Alto de la card en el dashboard (grid units)."),
    ("K1", "Card pinned (true/false): siempre arriba, no se mueve en el layout."),
    ("L1", "Color hex de la card (#RRGGBB) para acento visual (KPIs, gráfico, badges)."),
    ("M1", "Continuous: intervalo de lectura cíclico en segundos.\n  • Vacío / 0 / ≥86400 → modo DIARIO usando System Config.ContinuousReadTime\n  • 1..86399 → modo CÍCLICO (snapshot cada N segundos)\n  Ej: 10 = cada 10s; 60 = cada minuto; 300 = cada 5 min."),
    ("N1", "Continuous: retención en días. Tras cada snapshot del grupo se borran los snapshots Continuous (CycleId IS NULL) más viejos que UtcNow - N días.\n  • Vacío / 0 → sin retención (acumula indefinidamente, ¡cuidado con el tamaño de la BD!)\n  • Recomendado: 30-90 días para datos cíclicos rápidos, 365+ para diarios."),
    ("O1", "Gating a NIVEL DE GRUPO. Variable PLC BOOL opcional (típicamente bit 'máquina/módulo en marcha').\n  REGLA SIMPLE: si esta columna está rellena, IGNORA completamente la columna L (RunningBitVar) de Stats_Variables.\n  • Vacío → sin gating de grupo (cada variable usa su propio L si lo tiene).\n  • TRUE → captura todas las variables del grupo (sin mirar L).\n  • FALSE o error de lectura → SKIP TODO el snapshot del grupo (cero filas en BD).\n  Útil cuando todas las variables de un grupo dependen del mismo bit (caso típico).")
};
foreach (var (col, note) in groupHeaderNotes)
    wsGroups.Cell(col).GetComment().AddText(note);

// ─── Hoja 5: System Config (DEC-024 SystemDeliveryDate + DEC-026 ContinuousReadTime) ───
var wsCfg = wb.Worksheets.Add("System Config");
wsCfg.Cell(1, 1).Value = "Parameter";
wsCfg.Cell(1, 2).Value = "Value";
wsCfg.Cell(1, 3).Value = "Description";
wsCfg.Range(1, 1, 1, 3).Style.Font.Bold = true;
wsCfg.Range(1, 1, 1, 3).Style.Fill.BackgroundColor = XLColor.LightGray;

int r = 2;
wsCfg.Cell(r, 1).Value = "SystemDeliveryDate";
wsCfg.Cell(r, 2).Value = "";
wsCfg.Cell(r, 3).Value = "DEC-024 — Fecha de puesta en marcha (única para toda la máquina). Formato YYYY-MM-DD o DD/MM/YYYY.";
r++;

wsCfg.Cell(r, 1).Value = "ContinuousReadTime";
wsCfg.Cell(r, 2).Value = "23:59";
wsCfg.Cell(r, 3).Value = "DEC-026 — Hora del snapshot diario Continuous (HH:mm 00:00-23:59). Default 23:59 (cierre día lógico). Sólo se usa para grupos en modo DIARIO (sin ContinuousReadIntervalSec en Stats_Groups).";
r++;

wsCfg.Columns().AdjustToContents();

// Ajustar anchos de las hojas SMM
foreach (var ws in new[] { wsElements, wsVars, wsCons, wsGroups })
    ws.Columns().AdjustToContents();

// ClosedXML escribe .xlsx; renombrar a .xlsm es compatible (sin macros) o usar SaveAs xlsx + rename.
// Para mantener extensión .xlsm y compatibilidad con ExcelConfigService que busca .xlsm primero:
wb.SaveAs(outputFile);

System.Console.WriteLine("✅ Plantilla SMM generada correctamente.");
System.Console.WriteLine($"   Hojas: Stats_Elements (6 cols), Stats_Variables (13 cols), Stats_Consumables (6 cols), Stats_Groups (14 cols A..N), System Config (2 params)");

static void WriteHeader(IXLWorksheet ws, string[] headers)
{
    for (int i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];
    var range = ws.Range(1, 1, 1, headers.Length);
    range.Style.Font.Bold = true;
    range.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
    ws.SheetView.FreezeRows(1);
}
