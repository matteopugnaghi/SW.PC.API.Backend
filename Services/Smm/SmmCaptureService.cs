using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Smm.Entities;
using System.Collections.Concurrent;
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

    /// <summary>Snapshot Continuous de un único grupo (DEC-026 ampliado por-grupo).</summary>
    Task<int> SnapshotContinuousGroupAsync(int groupId, CancellationToken ct = default);

    /// <summary>Snapshot manual admin (DEC-026 punto 6).</summary>
    Task<int> OnDemandSnapshotAsync(int? groupId, CancellationToken ct = default);

    /// <summary>Inicio de ciclo PerCycle por flanco FALSE→TRUE de CycleRunningVar (DEC-018).</summary>
    Task<int> OnCycleStartAsync(int groupId, DateTime startedAt, CancellationToken ct = default);

    /// <summary>Fin de ciclo PerCycle por flanco TRUE→FALSE (DEC-018).</summary>
    Task OnCycleEndAsync(int cycleId, DateTime endedAt, string endedReason = "Normal", CancellationToken ct = default);

    /// <summary>Aborta ciclos huérfanos al startup (DEC-020 punto 5).</summary>
    Task<int> AbortOrphanCyclesAsync(CancellationToken ct = default);

    /// <summary>Borra snapshots Continuous (CycleId IS NULL) anteriores a UtcNow - retentionDays (todos los grupos).</summary>
    Task<int> PurgeOldContinuousAsync(int retentionDays, CancellationToken ct = default);

    /// <summary>Borra snapshots Continuous antiguos de un único grupo.</summary>
    Task<int> PurgeOldContinuousGroupAsync(int groupId, int retentionDays, CancellationToken ct = default);
}

public class SmmCaptureService : ISmmCaptureService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly ITwinCATService _twincat;
    private readonly ILogger<SmmCaptureService> _logger;

    // ─── Auto-recovery del deadlock PlcReset ───
    // Cuando la heurística de wrap detecta un "PlcReset" (raw mucho menor que LastRawValue
    // y reconDelta > 50% MaxValue) NO actualiza LastRawValue → si el PLC realmente fue
    // reiniciado físicamente, todas las lecturas siguientes se quedan atrapadas en el
    // mismo error indefinidamente. Para romper el bucle: si vemos N detecciones
    // consecutivas para la misma variable, asumimos reset físico real y aceptamos el
    // raw actual como nueva baseline.
    private const int PlcResetConfirmThreshold = 3;
    // STATIC: SmmCaptureService está registrado como Scoped, cada snapshot crea una
    // instancia nueva. Si el dictionary fuera de instancia, el contador se reiniciaría
    // a 0 cada lectura y NUNCA llegaría a PlcResetConfirmThreshold → loop infinito de
    // PlcReset. Static garantiza persistencia entre instancias en el mismo proceso.
    private static readonly ConcurrentDictionary<int, int> _consecutivePlcResets = new();

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
        // Itera todos los grupos Continuous y suma sus readings.
        // Mantenido para compatibilidad (snapshot manual / OnDemand sin groupId).
        using var db = _dbFactory.CreateDbContext();
        var groupIds = await db.SmmGroups
            .Where(g => g.ReadFrequency == "Continuous")
            .Select(g => g.Id)
            .ToListAsync(ct);
        int total = 0;
        foreach (var gid in groupIds)
        {
            try { total += await SnapshotContinuousGroupAsync(gid, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Snapshot grupo {Id} falló (continuamos)", gid); }
        }
        return total;
    }

    public async Task<int> SnapshotContinuousGroupAsync(int groupId, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();

        // ─── 0) GATING a NIVEL DE GRUPO (RunningBitVar del grupo) ───
        // Si el grupo define un bit "máquina/módulo en marcha" y vale FALSE
        // (o falla la lectura), saltamos TODO el snapshot del grupo.
        // Esto evita generar filas de "ruido" cuando el equipo está parado y
        // ahorra memoria masivamente. Se evalúa ANTES del gating per-variable.
        var group = await db.SmmGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group != null && !string.IsNullOrWhiteSpace(group.RunningBitVar))
        {
            try
            {
                var raw = await _twincat.ReadVariableAsync(group.RunningBitVar, typeof(bool));
                bool? running = raw switch
                {
                    bool b => b,
                    null => (bool?)null,
                    _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
                };
                if (running != true)
                {
                    _logger.LogDebug("SMM Continuous grupo {Id}: gating grupo OFF ({Bit}={Val}) → snapshot omitido",
                        groupId, group.RunningBitVar, running?.ToString() ?? "ERROR");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMM Continuous grupo {Id}: error leyendo RunningBitVar de grupo {Bit} → snapshot omitido",
                    groupId, group.RunningBitVar);
                return 0;
            }
        }

        var vars = await db.SmmVariables
            .Where(v => v.GroupId == groupId && v.PlcVariable != null)
            .ToListAsync(ct);

        // Variables con Formula (derivadas) — se evalúan al final usando los valores PLC ya leídos.
        // Aceptamos FormulaScope IN (null/empty/Continuous/OnRead/Daily) — todos significan
        // "evaluar en cada snapshot Continuous" en el modelo actual.
        var formulaVars = await db.SmmVariables
            .Where(v => v.GroupId == groupId
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

        // Si el grupo define RunningBitVar a nivel de GRUPO (col O Excel) y aquí estamos
        // → significa que vale TRUE → ignoramos completamente el gating per-variable (col L).
        // O sea: O en blanco → manda L; O rellena → L se ignora (regla simple).
        bool groupGatingActive = group != null && !string.IsNullOrWhiteSpace(group.RunningBitVar);

        // Leer cada variable con su tipo CLR correcto (no usar ReadAllVariablesAsync porque asume int).
        var rawValues = new Dictionary<int, (object? raw, bool isError, string? err, bool gatedOff)>(vars.Count);
        foreach (var v in vars)
        {
            // Gating per-variable SOLO si el grupo NO tiene gating de grupo
            if (!groupGatingActive && !string.IsNullOrWhiteSpace(v.RunningBitVar))
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

            // ─── 1.5) WRAP-AROUND DETECTION BIDIRECCIONAL (Continuous) ───
            // Aplica a variables PLC numéricas con MaxValue definido (contadores HW:
            // revoluciones, ciclos UINT16/UDINT32, caudalímetros bidireccionales, etc.).
            //
            // Modelo: el contador es CIRCULAR de período P = MaxValue+1. Entre dos
            // lecturas consecutivas asumimos que NO se ha movido más de medio rango
            // (premisa física razonable: muestreamos cada ~5s; un UDINT tendría que
            // contar >2 mil millones de pulsos/segundo para violarla → imposible).
            //
            // Bajo esa premisa elegimos siempre la interpretación de MENOR magnitud:
            //   diff = raw - last
            //   · diff >  P/2  → en realidad fue BACKWARD wrap → WrapCount-- ; signedDelta = diff - P (negativo)
            //   · diff < -P/2  → en realidad fue FORWARD  wrap → WrapCount++ ; signedDelta = diff + P (positivo)
            //   · |diff| ≤ P/2 → step normal (puede ser positivo O negativo: backflow)
            //
            // Esto cubre los 4 escenarios automáticamente sin flags:
            //   (a) Forward step              (caso clásico, contador subiendo)
            //   (b) Forward wrap              (raw < last cerca del máximo)
            //   (c) Backward step / backflow  (raw < last lejos del wrap, agua retornando)
            //   (d) Backward wrap             (raw > last cerca del cero, retorno cruzando 0)
            //
            // Valor normalizado guardado en SMM_Readings.Value:
            //     normalized = raw + WrapCount * P     (WrapCount ahora puede ser negativo)
            //
            // Sanity: si |signedDelta| > 40% P el delta es físicamente improbable
            // (cerca de la zona ambigua P/2) → tratamos como reset PLC con streak.
            if (!isError && value.HasValue && v.MaxValue.HasValue && v.MaxValue.Value > 0)
            {
                var rawNum = value.Value;
                var period = v.MaxValue.Value + 1;
                bool detectedReset = false;

                if (v.LastRawValue.HasValue)
                {
                    var diff = rawNum - v.LastRawValue.Value;
                    double signedDelta;
                    int wrapDelta;
                    if (diff > period * 0.5)
                    {
                        signedDelta = diff - period;
                        wrapDelta = -1; // backward wrap
                    }
                    else if (diff < -period * 0.5)
                    {
                        signedDelta = diff + period;
                        wrapDelta = +1; // forward wrap
                    }
                    else
                    {
                        signedDelta = diff;
                        wrapDelta = 0;  // step normal (forward o backward)
                    }

                    if (Math.Abs(signedDelta) > period * 0.4)
                    {
                        // Zona ambigua: probable reset PLC físico. Aplicamos streak.
                        var consecutive = _consecutivePlcResets.AddOrUpdate(v.Id, 1, (_, c) => c + 1);
                        if (consecutive >= PlcResetConfirmThreshold)
                        {
                            _logger.LogInformation(
                                "[SMM Continuous] PLC reset CONFIRMADO var '{V}' (id={Id}) tras {N} detecciones: aceptamos raw={R} como nueva baseline (last={L}, WrapCount conservado={W}).",
                                v.PlcVariable, v.Id, consecutive, rawNum, v.LastRawValue.Value, v.WrapCount);
                            v.LastRawValue = rawNum;
                            value = rawNum + v.WrapCount * period;
                            _consecutivePlcResets.TryRemove(v.Id, out _);
                        }
                        else
                        {
                            isError = true;
                            errReason = $"PlcReset(last={v.LastRawValue.Value.ToString("0.###", CultureInfo.InvariantCulture)},raw={rawNum.ToString("0.###", CultureInfo.InvariantCulture)},period={period.ToString("0.###", CultureInfo.InvariantCulture)},signedDelta={signedDelta.ToString("0.###", CultureInfo.InvariantCulture)},streak={consecutive}/{PlcResetConfirmThreshold})";
                            _logger.LogWarning(
                                "[SMM Continuous] Probable reset PLC var '{V}' (id={Id}): last={L} → raw={R} | period={P} | signedDelta={D} (>40% P, streak {S}/{T}).",
                                v.PlcVariable, v.Id, v.LastRawValue.Value, rawNum, period, signedDelta, consecutive, PlcResetConfirmThreshold);
                            detectedReset = true;
                        }
                    }
                    else
                    {
                        _consecutivePlcResets.TryRemove(v.Id, out _);
                        if (wrapDelta != 0)
                        {
                            v.WrapCount += wrapDelta;
                            _logger.LogInformation(
                                "[SMM Continuous] Wrap {Dir} var '{V}' (id={Id}): last={L} → raw={R} | period={P} | signedDelta={D} | wrapCount={W}",
                                wrapDelta > 0 ? "FORWARD" : "BACKWARD", v.PlcVariable, v.Id,
                                v.LastRawValue.Value, rawNum, period, signedDelta, v.WrapCount);
                        }
                    }
                }
                else
                {
                    _consecutivePlcResets.TryRemove(v.Id, out _);
                }

                if (!isError && !detectedReset)
                {
                    v.LastRawValue = rawNum;
                    // Valor normalizado: raw + offset acumulado de wraps (WrapCount puede ser negativo)
                    value = rawNum + v.WrapCount * period;
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
                        var result = FormulaEvaluator.Evaluate(expanded);
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
        _logger.LogInformation("📊 SMM Continuous snapshot grupo {GroupId}: {Count} readings ({Plc} PLC + {Fx} fórmulas)",
            groupId, readings.Count, vars.Count - readings.Count(r => r.Source == "Formula"), formulaVars.Count);
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

    public async Task<int> PurgeOldContinuousAsync(int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;
        using var db = _dbFactory.CreateDbContext();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        // FIX: SQLite almacena DateTime como TEXT en formato "yyyy-MM-dd HH:mm:ss.FFFFFFF" (con ESPACIO).
        // Si usamos ISO "o" (con 'T'), la comparación lexicográfica falla:
        // " " (0x20) < "T" (0x54) → todos los timestamps del MISMO día con espacio se consideran < cutoff con T
        // y se borran erróneamente. Usar formato nativo SQLite para que la comparación sea correcta.
        var cutoffStr = cutoff.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
        // Borrar sólo snapshots Continuous (CycleId IS NULL); los readings PerCycle se preservan.
        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM SMM_Readings WHERE CycleId IS NULL AND Timestamp < {0}", cutoffStr);
        return deleted;
    }

    public async Task<int> PurgeOldContinuousGroupAsync(int groupId, int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;
        using var db = _dbFactory.CreateDbContext();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        // FIX: ver comentário en PurgeOldContinuousAsync. SQLite usa "yyyy-MM-dd HH:mm:ss.FFFFFFF".
        var cutoffStr = cutoff.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM SMM_Readings WHERE GroupId = {0} AND CycleId IS NULL AND Timestamp < {1}",
            groupId, cutoffStr);
        return deleted;
    }
}
