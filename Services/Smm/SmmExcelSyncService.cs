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
    Task<SmmSyncResult> SyncFromExcelAsync(string excelPath, bool purgeMissing = false, CancellationToken ct = default);
}

public class SmmSyncResult
{
    public int GroupsAdded { get; set; }
    public int GroupsUpdated { get; set; }
    public int GroupsDeleted { get; set; }
    public int ElementsAdded { get; set; }
    public int ElementsUpdated { get; set; }
    public int ElementsDeleted { get; set; }
    public int VariablesAdded { get; set; }
    public int VariablesUpdated { get; set; }
    public int VariablesDeleted { get; set; }
    public int ConsumablesAdded { get; set; }
    public int ConsumablesUpdated { get; set; }
    public int ConsumablesDeleted { get; set; }
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

    public async Task<SmmSyncResult> SyncFromExcelAsync(string excelPath, bool purgeMissing = false, CancellationToken ct = default)
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

            // Sets de claves naturales presentes en el Excel — usados para purgar lo ausente.
            var excelGroupNames    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var excelElementNames  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var excelVariableKeys  = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "GroupName::VarName"
            var excelConsumableKeys= new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "ElementName::TaskName::PartSku"

            // 1) Groups (debe ir primero — Variables refieren GroupName)
            await SyncGroupsAsync(workbook, ctx, result, excelGroupNames, ct);

            // 2) Elements (antes de Variables y Consumables)
            await SyncElementsAsync(workbook, ctx, result, excelElementNames, ct);

            // 3) Variables (link por GroupName + ElementName)
            await SyncVariablesAsync(workbook, ctx, result, excelVariableKeys, ct);

            // 4) Consumables (link por ElementName)
            await SyncConsumablesAsync(workbook, ctx, result, excelConsumableKeys, ct);

            await ctx.SaveChangesAsync(ct);

            // 5) Purga opcional — DEC-013-PURGE: borra lo que ya no está en el Excel.
            //    SQLite cascadea: borrar Group → Variables, Cycles, CycleAlarms, Readings.
            //                     borrar Element → Lifecycles, Interventions, Consumables, ConsumableUsage.
            //                     borrar Variable → Readings.
            //                     borrar Consumable → ConsumableUsage.
            if (purgeMissing)
            {
                await PurgeMissingAsync(ctx, result, excelGroupNames, excelElementNames, excelVariableKeys, excelConsumableKeys, ct);
                await ctx.SaveChangesAsync(ct);
            }

            result.Success = true;

            _logger.LogInformation(
                "[SMM-Sync] OK. Groups: +{GA}/~{GU}/-{GD} | Elements: +{EA}/~{EU}/-{ED} | Vars: +{VA}/~{VU}/-{VD} | Cons: +{CA}/~{CU}/-{CD} | Purge: {P} | Warns: {W}",
                result.GroupsAdded, result.GroupsUpdated, result.GroupsDeleted,
                result.ElementsAdded, result.ElementsUpdated, result.ElementsDeleted,
                result.VariablesAdded, result.VariablesUpdated, result.VariablesDeleted,
                result.ConsumablesAdded, result.ConsumablesUpdated, result.ConsumablesDeleted,
                purgeMissing, result.Warnings.Count);
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
    //   H=AlarmHistVar  I=LayoutWidth  J=LayoutHeight  K=LayoutPinned  L=LayoutColor
    private async Task SyncGroupsAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, HashSet<string> excelKeys, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Groups");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Groups no encontrada — sin grupos"); return; }

        var existing = await ctx.SmmGroups.ToDictionaryAsync(g => g.GroupName, g => g, StringComparer.OrdinalIgnoreCase, ct);
        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)))
        {
            var name = Cell(sh, "A", row);
            excelKeys.Add(name);
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
            g.LayoutColor       = string.IsNullOrEmpty(Cell(sh, "L", row)) ? null : Cell(sh, "L", row);
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
    private async Task SyncElementsAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, HashSet<string> excelKeys, CancellationToken ct)
    {
        var sh = FindSheet(wb, "Stats_Elements");
        if (sh == null) { res.Warnings.Add("Hoja Stats_Elements no encontrada — sin elementos"); return; }

        var existing = await ctx.SmmElements.ToDictionaryAsync(e => e.ElementName, e => e, StringComparer.OrdinalIgnoreCase, ct);
        int row = 2;
        while (!string.IsNullOrEmpty(Cell(sh, "A", row)))
        {
            var name = Cell(sh, "A", row);
            excelKeys.Add(name);
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
    private async Task SyncVariablesAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, HashSet<string> excelKeys, CancellationToken ct)
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

            // Recolectar clave natural para purga (siempre, aunque luego se salte por validaciones)
            excelKeys.Add($"{groupName}::{varName}");

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
            // M = CaptureMode (Snapshot|Delta) — DEC-028
            var capMode = Cell(sh, "M", row);
            v.CaptureMode        = string.IsNullOrEmpty(capMode) ? "Snapshot"
                                  : (capMode.Equals("Delta", StringComparison.OrdinalIgnoreCase) ? "Delta" : "Snapshot");

            row++;
        }
    }

    // ── Stats_Consumables ────────────────────────────────────────────────────
    // Columnas: A=ElementName B=TaskName C=PartSku D=PartDescription E=PartUnit F=PartDefaultQuantity
    private async Task SyncConsumablesAsync(XLWorkbook wb, AquafrischDbContext ctx, SmmSyncResult res, HashSet<string> excelKeys, CancellationToken ct)
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

            // Recolectar clave natural para purga
            excelKeys.Add($"{elementName}::{taskName}::{sku}");

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

    // ── PURGA OPCIONAL ──────────────────────────────────────────────────────
    // DEC-013-PURGE: borra entidades que ya no están en el Excel.
    // Cascada SQLite (definida en CREATE TABLE):
    //   Group   → Variables, Cycles, CycleAlarms, Readings (cascade)
    //   Element → Lifecycles, Interventions, Consumables, ConsumableUsage (cascade)
    //   Variable→ Readings (cascade)
    //   Consumable → ConsumableUsage (cascade)
    // ⚠️ Operación destructiva — solo se invoca si purgeMissing=true.
    private async Task PurgeMissingAsync(
        AquafrischDbContext ctx,
        SmmSyncResult res,
        HashSet<string> excelGroupNames,
        HashSet<string> excelElementNames,
        HashSet<string> excelVariableKeys,
        HashSet<string> excelConsumableKeys,
        CancellationToken ct)
    {
        // 1) Consumibles (más profundo → menos riesgo)
        var elementNamesById = await ctx.SmmElements
            .ToDictionaryAsync(e => e.Id, e => e.ElementName, ct);
        var allConsumables = await ctx.SmmConsumables.ToListAsync(ct);
        var consumablesToDelete = allConsumables.Where(c =>
        {
            elementNamesById.TryGetValue(c.ElementId, out var elementName);
            var key = $"{elementName ?? ""}::{c.TaskName}::{c.PartSku}";
            return !excelConsumableKeys.Contains(key);
        }).ToList();
        if (consumablesToDelete.Count > 0)
        {
            ctx.SmmConsumables.RemoveRange(consumablesToDelete);
            res.ConsumablesDeleted = consumablesToDelete.Count;
            _logger.LogInformation("[SMM-Sync-Purge] Eliminando {N} consumibles ausentes del Excel", consumablesToDelete.Count);
        }

        // 2) Variables
        var groupNamesById = await ctx.SmmGroups
            .ToDictionaryAsync(g => g.Id, g => g.GroupName, ct);
        var allVariables = await ctx.SmmVariables.ToListAsync(ct);
        var variablesToDelete = allVariables.Where(v =>
        {
            groupNamesById.TryGetValue(v.GroupId, out var groupName);
            var key = $"{groupName ?? ""}::{v.VarName}";
            return !excelVariableKeys.Contains(key);
        }).ToList();
        if (variablesToDelete.Count > 0)
        {
            ctx.SmmVariables.RemoveRange(variablesToDelete);
            res.VariablesDeleted = variablesToDelete.Count;
            _logger.LogInformation("[SMM-Sync-Purge] Eliminando {N} variables ausentes del Excel (cascada → readings)", variablesToDelete.Count);
        }

        // 3) Elementos — solo borrar si no tienen intervenciones registradas (preserva histórico mantenimiento)
        var allElements = await ctx.SmmElements.ToListAsync(ct);
        var elementsToConsider = allElements.Where(e => !excelElementNames.Contains(e.ElementName)).ToList();
        if (elementsToConsider.Count > 0)
        {
            var elementIdsWithInterventions = await ctx.SmmInterventions
                .Where(i => elementsToConsider.Select(e => e.Id).Contains(i.ElementId))
                .Select(i => i.ElementId)
                .Distinct()
                .ToListAsync(ct);

            var elementsToDelete = elementsToConsider.Where(e => !elementIdsWithInterventions.Contains(e.Id)).ToList();
            var elementsKept = elementsToConsider.Where(e => elementIdsWithInterventions.Contains(e.Id)).ToList();

            if (elementsToDelete.Count > 0)
            {
                ctx.SmmElements.RemoveRange(elementsToDelete);
                res.ElementsDeleted = elementsToDelete.Count;
                _logger.LogInformation("[SMM-Sync-Purge] Eliminando {N} elementos ausentes del Excel", elementsToDelete.Count);
            }
            foreach (var ek in elementsKept)
            {
                res.Warnings.Add($"Elemento '{ek.ElementName}' no está en el Excel pero tiene intervenciones registradas — conservado");
            }
        }

        // 4) Grupos
        var allGroups = await ctx.SmmGroups.ToListAsync(ct);
        var groupsToDelete = allGroups.Where(g => !excelGroupNames.Contains(g.GroupName)).ToList();
        if (groupsToDelete.Count > 0)
        {
            // Avisar si arrastra ciclos (histórico que se pierde por cascada)
            var groupIdsWithCycles = await ctx.SmmCycles
                .Where(cy => groupsToDelete.Select(g => g.Id).Contains(cy.GroupId))
                .Select(cy => cy.GroupId)
                .Distinct()
                .ToListAsync(ct);
            foreach (var g in groupsToDelete.Where(x => groupIdsWithCycles.Contains(x.Id)))
            {
                res.Warnings.Add($"Grupo '{g.GroupName}' borrado: se eliminó histórico de ciclos por cascada");
            }
            ctx.SmmGroups.RemoveRange(groupsToDelete);
            res.GroupsDeleted = groupsToDelete.Count;
            _logger.LogInformation("[SMM-Sync-Purge] Eliminando {N} grupos ausentes del Excel (cascada → variables, cycles, alarms, readings)", groupsToDelete.Count);
        }
    }
}
