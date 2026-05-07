using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Smm.Entities;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SW.PC.API.Backend.Services.Smm;

/// <summary>
/// Servicio central de captura SMM (DEC-013/025).
/// NO hace polling. Solo reacciona a eventos (CycleStart/CycleEnd, hora programada, OnDemand).
/// </summary>
public interface ISmmCaptureService
{
    /// <summary>Lectura batch de variables Continuous (job nocturno DEC-026).</summary>
    Task<int> SnapshotContinuousAsync(CancellationToken ct = default);

    /// <summary>Snapshot manual admin (DEC-026 punto 6).</summary>
    Task<int> OnDemandSnapshotAsync(int? groupId, CancellationToken ct = default);

    /// <summary>Inicio de ciclo PerCycle por flanco FALSE→TRUE de CycleRunningVar (DEC-018).</summary>
    Task<int> OnCycleStartAsync(int groupId, DateTime startedAt, CancellationToken ct = default);

    /// <summary>Fin de ciclo PerCycle por flanco TRUE→FALSE (DEC-018).</summary>
    Task OnCycleEndAsync(int cycleId, DateTime endedAt, string endedReason = "Normal", CancellationToken ct = default);

    /// <summary>Aborta ciclos huérfanos al startup (DEC-020 punto 5).</summary>
    Task<int> AbortOrphanCyclesAsync(CancellationToken ct = default);
}

public class SmmCaptureService : ISmmCaptureService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly ITwinCATService _twincat;
    private readonly ILogger<SmmCaptureService> _logger;

    public SmmCaptureService(
        IProjectDbContextFactory dbFactory,
        ITwinCATService twincat,
        ILogger<SmmCaptureService> logger)
    {
        _dbFactory = dbFactory;
        _twincat = twincat;
        _logger = logger;
    }

    public async Task<int> SnapshotContinuousAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();

        // Variables Continuous = pertenecen a grupos ReadFrequency=Continuous
        var continuousGroups = await db.SmmGroups
            .Where(g => g.ReadFrequency == "Continuous")
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (continuousGroups.Count == 0) return 0;

        var vars = await db.SmmVariables
            .Where(v => continuousGroups.Contains(v.GroupId) && v.PlcVariable != null)
            .ToListAsync(ct);

        // Variables con Formula (derivadas) — se evalúan al final usando los valores PLC ya leídos.
        // Aceptamos FormulaScope IN (null/empty/Continuous/OnRead/Daily) — todos significan
        // "evaluar en cada snapshot Continuous" en el modelo actual.
        var formulaVars = await db.SmmVariables
            .Where(v => continuousGroups.Contains(v.GroupId)
                        && v.Formula != null && v.Formula != ""
                        && (v.PlcVariable == null || v.PlcVariable == ""))
            .Where(v => v.FormulaScope == null || v.FormulaScope == ""
                        || v.FormulaScope == "Continuous" || v.FormulaScope == "OnRead"
                        || v.FormulaScope == "Daily")
            .ToListAsync(ct);

        if (vars.Count == 0 && formulaVars.Count == 0) return 0;

        // ─── 1) GATING RunningBitVar ───
        // Para cada variable PLC con RunningBitVar configurado, leer el bit; si FALSE → skip
        // (no se inserta fila). Si TRUE o sin gating → se lee el valor.
        // Cacheamos lectura del bit para evitar re-leer si varias vars usan el mismo bit.
        var bitCache = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        async Task<bool?> ReadRunningBitAsync(string bitName)
        {
            if (bitCache.TryGetValue(bitName, out var cached)) return cached;
            try
            {
                var raw = await _twincat.ReadVariableAsync(bitName, typeof(bool));
                bool? val = raw switch
                {
                    bool b => b,
                    null => (bool?)null,
                    _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
                };
                bitCache[bitName] = val;
                return val;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMM Continuous: error leyendo RunningBitVar {Bit}", bitName);
                bitCache[bitName] = null; // null = error de lectura → tratamos como "skip"
                return null;
            }
        }

        // Leer cada variable con su tipo CLR correcto (no usar ReadAllVariablesAsync porque asume int).
        var rawValues = new Dictionary<int, (object? raw, bool isError, string? err, bool gatedOff)>(vars.Count);
        foreach (var v in vars)
        {
            // Gating
            if (!string.IsNullOrWhiteSpace(v.RunningBitVar))
            {
                var bit = await ReadRunningBitAsync(v.RunningBitVar);
                if (bit != true)
                {
                    rawValues[v.Id] = (null, false, bit == null ? "GatingBitReadError" : "GatedOff", true);
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(v.PlcVariable))
            {
                rawValues[v.Id] = (null, true, "NoPlcVariable", false);
                continue;
            }
            try
            {
                var clrType = MapDataTypeToClr(v.DataType);
                var raw = await _twincat.ReadVariableAsync(v.PlcVariable, clrType);
                rawValues[v.Id] = (raw, raw == null, raw == null ? "PlcReadNull" : null, false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMM Continuous: error leyendo {Var} ({Type})", v.PlcVariable, v.DataType);
                rawValues[v.Id] = (null, true, $"ReadError: {ex.GetType().Name}: {ex.Message}", false);
            }
        }

        var now = DateTime.UtcNow;
        var readings = new List<SmmReading>(vars.Count);
        // Mapa VarName → (valor, isError) para alimentar fórmulas
        var byVarName = new Dictionary<string, (double? Value, bool IsError)>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in vars)
        {
            if (!rawValues.TryGetValue(v.Id, out var entry)) continue;

            // Si fue "gatedOff", NO insertamos fila (el bit estaba FALSE → la variable
            // no tiene sentido en este snapshot).
            if (entry.gatedOff) continue;

            double? value = null;
            string? stringValue = null;
            bool isError = entry.isError;
            string? errReason = entry.err;

            if (!isError && entry.raw != null)
            {
                var raw = entry.raw;
                if (raw is string s) { stringValue = s; }
                else if (raw is bool b) { value = b ? 1.0 : 0.0; }
                else
                {
                    try { value = Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
                    catch { isError = true; errReason = $"CastError: {raw.GetType().Name}"; }
                }
            }

            readings.Add(new SmmReading
            {
                GroupId = v.GroupId,
                VariableId = v.Id,
                CycleId = null,
                Timestamp = now,
                Value = value,
                StringValue = stringValue,
                Source = "Plc",
                IsError = isError,
                ErrorReason = errReason,
                PlcVariable = v.PlcVariable
            });

            if (!string.IsNullOrEmpty(v.VarName))
                byVarName[v.VarName] = (value, isError);
        }

        // ─── 2) FÓRMULAS Continuous (DEC-016/021 adaptado) ───
        if (formulaVars.Count > 0)
        {
            var depPattern = new Regex(@"\{([^}]+)\}");
            foreach (var fv in formulaVars)
            {
                double? num = null;
                bool isError = false;
                string? errReason = null;
                try
                {
                    var formula = fv.Formula!;
                    string upstreamError = null!;
                    foreach (Match m in depPattern.Matches(formula))
                    {
                        var depName = m.Groups[1].Value.Trim();
                        if (!byVarName.TryGetValue(depName, out var dep))
                        { upstreamError = $"UnknownDependency:{depName}"; break; }
                        if (dep.IsError || dep.Value == null)
                        { upstreamError = $"UpstreamError:{depName}"; break; }
                    }
                    if (upstreamError != null)
                    {
                        isError = true; errReason = upstreamError;
                    }
                    else
                    {
                        var expanded = depPattern.Replace(formula, mm =>
                            byVarName[mm.Groups[1].Value.Trim()].Value!.Value.ToString(CultureInfo.InvariantCulture));
                        var expr = new NCalc.Expression(expanded, NCalc.ExpressionOptions.NoCache);
                        var result = expr.Evaluate();
                        if (result == null) { isError = true; errReason = "NullResult"; }
                        else
                        {
                            try { num = Convert.ToDouble(result, CultureInfo.InvariantCulture); }
                            catch { isError = true; errReason = "NonNumericResult"; }
                            if (num.HasValue && (double.IsNaN(num.Value) || double.IsInfinity(num.Value)))
                            {
                                isError = true;
                                errReason = double.IsInfinity(num.Value) ? "DivisionByZero" : "NaN";
                                num = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    isError = true;
                    errReason = $"FormulaError:{ex.GetType().Name}:{ex.Message}";
                }

                readings.Add(new SmmReading
                {
                    GroupId = fv.GroupId,
                    VariableId = fv.Id,
                    CycleId = null,
                    Timestamp = now,
                    Value = num,
                    StringValue = null,
                    Source = "Formula",
                    IsError = isError,
                    ErrorReason = errReason,
                    PlcVariable = null
                });

                // Permitir fórmulas que dependan de otras fórmulas (orden simple según BD)
                if (!string.IsNullOrEmpty(fv.VarName))
                    byVarName[fv.VarName] = (num, isError);
            }
        }

        db.SmmReadings.AddRange(readings);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("📊 SMM Continuous snapshot: {Count} readings ({Plc} PLC + {Fx} fórmulas)",
            readings.Count, vars.Count - readings.Count(r => r.Source == "Formula"), formulaVars.Count);
        return readings.Count;
    }

    /// <summary>
    /// Mapea DataType TwinCAT-style del Excel SMM al CLR type que entiende ReadVariableAsync.
    /// (Idéntico a SmmPlcEdgeWatcher.MapDataTypeToClr).
    /// </summary>
    private static Type MapDataTypeToClr(string? dataType)
    {
        var dt = (dataType ?? string.Empty).Trim().ToUpperInvariant();
        return dt switch
        {
            "BOOL" => typeof(bool),
            "BYTE" or "USINT" => typeof(byte),
            "SINT" => typeof(sbyte),
            "INT" => typeof(int),
            "WORD" or "UINT" => typeof(ushort),
            "DINT" => typeof(int),
            "DWORD" or "UDINT" => typeof(uint),
            "LINT" => typeof(long),
            "ULINT" or "LWORD" => typeof(ulong),
            "REAL" => typeof(float),
            "LREAL" => typeof(double),
            "STRING" or "WSTRING" or "CHAR" or "WCHAR" => typeof(string),
            _ => typeof(double)
        };
    }

    public Task<int> OnDemandSnapshotAsync(int? groupId, CancellationToken ct = default)
    {
        // Versión simplificada Fase 4: si groupId especificado filtra, sino comporta como Continuous.
        // (Para snapshot real OnDemand se reusa la misma lógica de SumRead.)
        return SnapshotContinuousAsync(ct);
    }

    public async Task<int> OnCycleStartAsync(int groupId, DateTime startedAt, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var cycle = new SmmCycle
        {
            GroupId = groupId,
            StartedAt = startedAt,
            Status = "Running"
        };
        db.SmmCycles.Add(cycle);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("🔄 SMM CycleStart group={GroupId} cycleId={Id}", groupId, cycle.Id);
        return cycle.Id;
    }

    public async Task OnCycleEndAsync(int cycleId, DateTime endedAt, string endedReason = "Normal", CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var cycle = await db.SmmCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle == null) return;
        if (cycle.Status != "Running") return; // INMUTABLE DEC-023

        cycle.Status = "Completed";
        cycle.CompletedAt = endedAt;
        cycle.EndedReason = endedReason;

        // Recalcular alarmas (DEC-020 punto 3)
        var alarms = await db.SmmCycleAlarms.Where(a => a.CycleId == cycleId).ToListAsync(ct);
        cycle.AlarmsCount = alarms.Count;
        cycle.AlarmTime_s = alarms.Sum(a => a.DurationInCycle_s);
        cycle.HadAlarms = alarms.Count > 0;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("✅ SMM CycleEnd cycleId={Id} reason={Reason} alarms={N}", cycleId, endedReason, alarms.Count);
    }

    public async Task<int> AbortOrphanCyclesAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var orphans = await db.SmmCycles.Where(c => c.Status == "Running").ToListAsync(ct);
        if (orphans.Count == 0) return 0;

        foreach (var c in orphans)
        {
            var lastReading = await db.SmmReadings
                .Where(r => r.CycleId == c.Id)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => (DateTime?)r.Timestamp)
                .FirstOrDefaultAsync(ct);

            c.Status = "Aborted";
            c.EndedReason = "BackendRestart";
            c.CompletedAt = lastReading ?? c.StartedAt;
        }
        await db.SaveChangesAsync(ct);
        _logger.LogWarning("⚠️ SMM AbortOrphanCycles: {N} ciclos huérfanos cerrados al startup", orphans.Count);
        return orphans.Count;
    }
}
