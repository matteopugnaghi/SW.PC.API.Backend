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

// ─── Hoja 4: Stats_Groups (DEC-017 base + DEC-018 cols 24-27 + DEC-020 col 28) ───
var wsGroups = wb.Worksheets.Add("Stats_Groups");
WriteHeader(wsGroups, new[]
{
    // 1-23 (base DEC-017)
    "GroupId", "GroupName", "Description", "DescriptionEN",
    "UiType", "ChartType", "TimeWindow", "MaxRows",
    "RefreshSeconds", "ReadFrequency", "GroupingKey",
    "VariablesIncluded", "VariablesExcluded",
    "ColorPalette", "Icon", "Order", "Visible",
    "ExportEnabled", "AggregationType", "DefaultPeriod",
    "LayoutWidth", "LayoutHeight", "LayoutPinned",
    // 24-27 (DEC-018)
    "CycleRunningVar", "ShowCycleStart", "ShowCycleEnd", "ShowCycleDuration",
    // 28 (DEC-020)
    "AlarmHistVar"
});

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
wsCfg.Cell(r, 2).Value = "03:00";
wsCfg.Cell(r, 3).Value = "DEC-026 — Hora job nocturno Continuous (R16 valida HH:mm 00:00-23:59). Default 03:00.";
r++;

wsCfg.Columns().AdjustToContents();

// Ajustar anchos de las hojas SMM
foreach (var ws in new[] { wsElements, wsVars, wsCons, wsGroups })
    ws.Columns().AdjustToContents();

// ClosedXML escribe .xlsx; renombrar a .xlsm es compatible (sin macros) o usar SaveAs xlsx + rename.
// Para mantener extensión .xlsm y compatibilidad con ExcelConfigService que busca .xlsm primero:
wb.SaveAs(outputFile);

System.Console.WriteLine("✅ Plantilla SMM generada correctamente.");
System.Console.WriteLine($"   Hojas: Stats_Elements (6 cols), Stats_Variables (13 cols), Stats_Consumables (6 cols), Stats_Groups (28 cols), System Config (2 params)");

static void WriteHeader(IXLWorksheet ws, string[] headers)
{
    for (int i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];
    var range = ws.Range(1, 1, 1, headers.Length);
    range.Style.Font.Bold = true;
    range.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
    ws.SheetView.FreezeRows(1);
}
