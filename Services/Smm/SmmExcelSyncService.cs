// ============================================================================
// SmmExcelSyncService.cs — Sincronización Excel → tablas SMM_*
// ============================================================================
// DEC-013 — Lee 4 hojas del ProjectConfig.xlsm y hace UPSERT idempotente:
//   • Stats_Groups       → SMM_Groups
//   • Stats_Elements     → SMM_Elements
//   • Stats_Variables    → SMM_Variables  (link por GroupName y ElementName)
//   • Stats_Consumables  → SMM_Consumables (link por ElementName)
//
// Estrategia de UPSERT: por nombre natural (clave funcional), no por Id.
// Borrado: NO elimina nada — añade/actualiza para preservar histórico de readings.
// ============================================================================

using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Smm.Entities;

namespace SW.PC.API.Backend.Services.Smm;

public interface ISmmExcelSyncService
{
    Task<SmmSyncResult> SyncFromExcelAsync(string excelPath, CancellationToken ct = default);
}

public class SmmSyncResult
{
    public int GroupsAdded { get; set; }
    public int GroupsUpdated { get; set; }
    public int ElementsAdded { get; set; }
    public int ElementsUpdated { get; set; }
    public int VariablesAdded { get; set; }
    public int VariablesUpdated { get; set; }
    public int ConsumablesAdded { get; set; }
    public int ConsumablesUpdated { get; set; }
    public List<string> Warnings { get; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class SmmExcelSyncService : ISmmExcelSyncService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly ILogger<SmmExcelSyncService> _logger;

    public SmmExcelSyncService(IProjectDbContextFactory dbFactory, ILogger<SmmExcelSyncService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<SmmSyncResult> SyncFromExcelAsync(string excelPath, CancellationToken ct = default)
    {
        var result = new SmmSyncResult();

        if (!File.Exists(excelPath))
        {
            result.Error = $"Excel no encontrado: {excelPath}";
            _logger.LogWarning("[SMM-Sync] {Error}", result.Error);
            return result;
        }

        try
        {
            using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = new XLWorkbook(stream);
            using var ctx = _dbFactory.CreateDbContext();

            // 1) Groups (debe ir primero — Variables refieren GroupName)
            await SyncGroupsAsync(workbook, ctx, result, ct);

            // 2) Elements (antes de Variables y Consumables)
            await SyncElementsAsync(workbook, ctx, result, ct);

            // 3) Variables (link por GroupName + ElementName)
            await SyncVariablesAsync(workbook, ctx, result, ct);

            // 4) Consumables (link por ElementName)
            await SyncConsumablesAsync(workbook, ctx, result, ct);

            await ctx.SaveChangesAsync(ct);
            result.Success = true;

            _logger.LogInformation(
                "[SMM-Sync] OK. Groups: +{GA}/~{GU} | Elements: +{EA}/~{EU} | Vars: +{VA}/~{VU} | Cons: +{CA}/~{CU} | Warns: {W}",
                result.GroupsAdded, result.GroupsUpdated,
                result.ElementsAdded, result.ElementsUpdated,
                result.VariablesAdded, result.VariablesUpdated,
                result.ConsumablesAdded, result.ConsumablesUpdated,
                result.Warnings.Count);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "[SMM-Sync] Error sincronizando {Path}", excelPath);
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IXLWorksheet? FindSheet(XLWorkbook wb, string name)
        => wb.TryGetWorksheet(name, out var ws) ? ws : null;

    private static string Cell(IXLWorksheet sh, string col, int row)
        => sh.Cell($"{col}{row}").GetString().Trim();

    private static int? CellInt(IXLWorksheet sh, string col, int row)
    {
        var s = Cell(sh, col, row);
        return int.TryParse(s, out var v) ? v : (int?)null;
    }

    private static double? CellDouble(IXLWorksheet sh, string col, int row)
    {
        var s = Cell(sh, col, row).Replace(',', '.');
        return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : (double?)null;
    }

    private static bool CellBool(IXLWorksheet sh, string col, int row)
    {
        var s = Cell(sh, col, row).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes" || s == "sí" || s == "si" || s == "x";
    }

    // ── Stats_Groups ─────────────────────────────────────────────────────────
    // Columnas esperadas:
    //   A=GroupName  B=UiType  C=ReadFrequency  D=CycleRunningVar
    //   E=ShowCycleStart  F=ShowCycleEnd  G=ShowCycleDuration
    //   H=AlarmHistVar  I=LayoutWidth  J=LayoutHeight  K=LayoutPinned
    private async Task SyncGroupsAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Groups");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Groups no encontrada — sin grupos"); return; }

        var existing = await ctx.SmmGroups.ToDictionaryAsync(g => g.GroupName, g => g, StringComparer.OrdinalIgnoreCase, ct);
        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)))
        {
            var name = Cell(sh, "A", row);
            if (!existing.TryGetValue(name, out var g))
            {
                g = new SmmGroup { GroupName = name };
                ctx.SmmGroups.Add(g);
                res.GroupsAdded++;
            }
            else
            {
                res.GroupsUpdated++;
            }

            g.UiType            = Cell(sh, "B", row).Length > 0 ? Cell(sh, "B", row) : "Table";
            g.ReadFrequency     = Cell(sh, "C", row).Length > 0 ? Cell(sh, "C", row) : "Continuous";
            g.CycleRunningVar   = string.IsNullOrEmpty(Cell(sh, "D", row)) ? null : Cell(sh, "D", row);
            g.ShowCycleStart    = string.IsNullOrEmpty(Cell(sh, "E", row)) ? true  : CellBool(sh, "E", row);
            g.ShowCycleEnd      = string.IsNullOrEmpty(Cell(sh, "F", row)) ? true  : CellBool(sh, "F", row);
            g.ShowCycleDuration = CellBool(sh, "G", row);
            g.AlarmHistVar      = string.IsNullOrEmpty(Cell(sh, "H", row)) ? null : Cell(sh, "H", row);
            g.LayoutWidth       = CellInt(sh, "I", row);
            g.LayoutHeight      = CellInt(sh, "J", row);
            g.LayoutPinned      = CellBool(sh, "K", row);
            g.UpdatedAt         = DateTime.UtcNow;

            // Validación DEC-018: PerCycle requiere CycleRunningVar
            if (string.Equals(g.ReadFrequency, "PerCycle", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(g.CycleRunningVar))
            {
                res.Warnings.Add($"Grupo '{name}': ReadFrequency=PerCycle requiere CycleRunningVar");
            }

            row++;
        }
    }

    // ── Stats_Elements ───────────────────────────────────────────────────────
    // Columnas: A=ElementName B=ComponentLocation3D C=SkuAquafrisch D=Manufacturer E=Model F=Notes
    private async Task SyncElementsAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Elements");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Elements no encontrada — sin elementos"); return; }

        var existing = await ctx.SmmElements.ToDictionaryAsync(e => e.ElementName, e => e, StringComparer.OrdinalIgnoreCase, ct);
        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)))
        {
            var name = Cell(sh, "A", row);
            if (!existing.TryGetValue(name, out var e))
            {
                e = new SmmElement { ElementName = name };
                ctx.SmmElements.Add(e);
                res.ElementsAdded++;
            }
            else
            {
                res.ElementsUpdated++;
            }

            e.ComponentLocation3D = string.IsNullOrEmpty(Cell(sh, "B", row)) ? null : Cell(sh, "B", row);
            e.SkuAquafrisch       = string.IsNullOrEmpty(Cell(sh, "C", row)) ? null : Cell(sh, "C", row);
            e.Manufacturer        = string.IsNullOrEmpty(Cell(sh, "D", row)) ? null : Cell(sh, "D", row);
            e.Model               = string.IsNullOrEmpty(Cell(sh, "E", row)) ? null : Cell(sh, "E", row);
            e.Notes               = string.IsNullOrEmpty(Cell(sh, "F", row)) ? null : Cell(sh, "F", row);

            row++;
        }
    }

    // ── Stats_Variables ──────────────────────────────────────────────────────
    // Columnas: A=GroupName B=VarName C=PlcVariable D=Unit E=DataType F=Formula
    //           G=FormulaScope H=Warning I=Critical J=ResetOnMaintenance
    //           K=ElementName L=RunningBitVar
    private async Task SyncVariablesAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Variables");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Variables no encontrada — sin variables"); return; }

        // SaveChanges parcial para resolver IDs de Groups/Elements antes de variables
        await ctx.SaveChangesAsync(ct);

        var groups   = await ctx.SmmGroups.ToDictionaryAsync(g => g.GroupName, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);
        var elements = await ctx.SmmElements.ToDictionaryAsync(e => e.ElementName, e => e.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existing = await ctx.SmmVariables.ToListAsync(ct);
        var existingByKey = existing.ToDictionary(v => $"{v.GroupId}::{v.VarName}", v => v, StringComparer.OrdinalIgnoreCase);

        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)) || !string.IsNullOrEmpty(Cell(sh, "B", row)))
        {
            var groupName = Cell(sh, "A", row);
            var varName   = Cell(sh, "B", row);
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(varName))
            {
                row++;
                continue;
            }

            if (!groups.TryGetValue(groupName, out var groupId))
            {
                res.Warnings.Add($"Variable '{varName}' fila {row}: grupo '{groupName}' no existe — saltada");
                row++;
                continue;
            }

            var plc      = Cell(sh, "C", row);
            var formula  = Cell(sh, "F", row);
            if (string.IsNullOrEmpty(plc) && string.IsNullOrEmpty(formula))
            {
                res.Warnings.Add($"Variable '{varName}' fila {row}: requiere PlcVariable o Formula — saltada");
                row++;
                continue;
            }
            if (!string.IsNullOrEmpty(plc) && !string.IsNullOrEmpty(formula))
            {
                res.Warnings.Add($"Variable '{varName}' fila {row}: PlcVariable y Formula son mutuamente excluyentes — usando Formula");
                plc = "";
            }

            int? elementId = null;
            var elementName = Cell(sh, "K", row);
            if (!string.IsNullOrEmpty(elementName))
            {
                if (elements.TryGetValue(elementName, out var eid)) elementId = eid;
                else res.Warnings.Add($"Variable '{varName}': elemento '{elementName}' no existe — vinculación omitida");
            }

            var key = $"{groupId}::{varName}";
            if (!existingByKey.TryGetValue(key, out var v))
            {
                v = new SmmVariable { GroupId = groupId, VarName = varName };
                ctx.SmmVariables.Add(v);
                existingByKey[key] = v;
                res.VariablesAdded++;
            }
            else
            {
                res.VariablesUpdated++;
            }

            v.PlcVariable        = string.IsNullOrEmpty(plc) ? null : plc;
            v.Unit               = string.IsNullOrEmpty(Cell(sh, "D", row)) ? null : Cell(sh, "D", row);
            v.DataType           = Cell(sh, "E", row).Length > 0 ? Cell(sh, "E", row) : "REAL";
            v.Formula            = string.IsNullOrEmpty(formula) ? null : formula;
            v.FormulaScope       = string.IsNullOrEmpty(Cell(sh, "G", row)) ? null : Cell(sh, "G", row);
            v.Warning            = CellDouble(sh, "H", row);
            v.Critical           = CellDouble(sh, "I", row);
            v.ResetOnMaintenance = CellBool(sh, "J", row);
            v.ElementId          = elementId;
            v.RunningBitVar      = string.IsNullOrEmpty(Cell(sh, "L", row)) ? null : Cell(sh, "L", row);

            row++;
        }
    }

    // ── Stats_Consumables ────────────────────────────────────────────────────
    // Columnas: A=ElementName B=TaskName C=PartSku D=PartDescription E=PartUnit F=PartDefaultQuantity
    private async Task SyncConsumablesAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Consumables");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Consumables no encontrada — sin consumibles"); return; }

        await ctx.SaveChangesAsync(ct);

        var elements = await ctx.SmmElements.ToDictionaryAsync(e => e.ElementName, e => e.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existing = await ctx.SmmConsumables.ToListAsync(ct);
        var existingByKey = existing.ToDictionary(
            c => $"{c.ElementId}::{c.TaskName}::{c.PartSku}",
            c => c,
            StringComparer.OrdinalIgnoreCase);

        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)))
        {
            var elementName = Cell(sh, "A", row);
            var taskName    = Cell(sh, "B", row);
            var sku         = Cell(sh, "C", row);

            if (string.IsNullOrEmpty(elementName) || string.IsNullOrEmpty(taskName) || string.IsNullOrEmpty(sku))
            {
                res.Warnings.Add($"Consumible fila {row}: ElementName/TaskName/PartSku son obligatorios — saltado");
                row++;
                continue;
            }
            if (!elements.TryGetValue(elementName, out var elementId))
            {
                res.Warnings.Add($"Consumible fila {row}: elemento '{elementName}' no existe — saltado");
                row++;
                continue;
            }

            var key = $"{elementId}::{taskName}::{sku}";
            if (!existingByKey.TryGetValue(key, out var c))
            {
                c = new SmmConsumable { ElementId = elementId, TaskName = taskName, PartSku = sku };
                ctx.SmmConsumables.Add(c);
                existingByKey[key] = c;
                res.ConsumablesAdded++;
            }
            else
            {
                res.ConsumablesUpdated++;
            }

            c.PartDescription      = string.IsNullOrEmpty(Cell(sh, "D", row)) ? sku : Cell(sh, "D", row);
            c.PartUnit             = Cell(sh, "E", row).Length > 0 ? Cell(sh, "E", row) : "ud";
            c.PartDefaultQuantity  = CellDouble(sh, "F", row) ?? 1.0;

            row++;
        }
    }
}
