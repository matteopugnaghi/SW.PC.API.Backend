// ============================================================================
// SmmPlcEdgeWatcher.cs — Detector de flancos de CycleRunningVar (DEC-018)
// ============================================================================
// HostedService Singleton que:
//   1. Se suscribe a ITwinCATService.OnVariableChanged
//   2. Mantiene un mapa CycleRunningVar → groupId (recargado desde BD)
//   3. En flanco FALSE→TRUE: abre ciclo (SmmCaptureService.OnCycleStartAsync)
//   4. En flanco TRUE→FALSE: hace snapshot de TODAS las variables PLC del grupo
//      con el cycleId activo, y cierra el ciclo (OnCycleEndAsync)
//
// Refresh del mapa: al startup, cada 60s y vía RefreshAsync() pública (llamada
// tras "Sincronizar Excel" desde SmmExcelSyncService).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.TwinCAT;
using SW.PC.API.Backend.Models.Smm.Entities;
using SW.PC.API.Backend.Services;
using System.Collections.Concurrent;
using System.Globalization;

namespace SW.PC.API.Backend.Services.Smm;

public interface ISmmPlcEdgeWatcher
{
    /// <summary>Recarga el mapa de CycleRunningVar→groupId desde BD (llamar tras sync Excel).</summary>
    Task RefreshAsync(CancellationToken ct = default);
}

public class SmmPlcEdgeWatcher : BackgroundService, ISmmPlcEdgeWatcher
{
    private readonly IServiceProvider _services;
    private readonly ITwinCATService _twincat;
    private readonly ILogger<SmmPlcEdgeWatcher> _logger;

    /// <summary>CycleRunningVar PLC name → groupId (case-insensitive)</summary>
    private readonly ConcurrentDictionary<string, int> _watchVarToGroupId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>groupId → cycleId activo (Running). null si no hay ciclo abierto.</summary>
    private readonly ConcurrentDictionary<int, int> _activeCycleByGroup = new();

    /// <summary>Último valor bool conocido por variable, para detectar flanco.</summary>
    private readonly ConcurrentDictionary<string, bool> _lastBoolValue = new(StringComparer.OrdinalIgnoreCase);

    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    public SmmPlcEdgeWatcher(
        IServiceProvider services,
        ITwinCATService twincat,
        ILogger<SmmPlcEdgeWatcher> logger)
    {
        _services = services;
        _twincat = twincat;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _twincat.OnVariableChanged += OnPlcVariableChanged;
        _logger.LogInformation("🎯 SmmPlcEdgeWatcher iniciado — escuchando flancos de CycleRunningVar");

        // Carga inicial + recargas periódicas
        await RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(RefreshInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try { await RefreshAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "[SMM-EdgeWatcher] Error en refresh periódico"); }
        }

        _twincat.OnVariableChanged -= OnPlcVariableChanged;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();

            // Recolectar grupos PerCycle con CycleRunningVar definida
            var groups = await db.SmmGroups
                .Where(g => g.ReadFrequency == "PerCycle" && g.CycleRunningVar != null && g.CycleRunningVar != "")
                .Select(g => new { g.Id, g.CycleRunningVar, g.GroupName })
                .ToListAsync(ct);

            // Reconstruir mapa
            _watchVarToGroupId.Clear();
            foreach (var g in groups)
            {
                _watchVarToGroupId[g.CycleRunningVar!] = g.Id;
            }

            // Sincronizar activeCycleByGroup con BD (recoger ciclos Running existentes)
            var runningCycles = await db.SmmCycles
                .Where(c => c.Status == "Running")
                .Select(c => new { c.GroupId, c.Id })
                .ToListAsync(ct);
            _activeCycleByGroup.Clear();
            foreach (var rc in runningCycles)
            {
                _activeCycleByGroup[rc.GroupId] = rc.Id;
            }

            _lastRefreshUtc = DateTime.UtcNow;
            _logger.LogInformation("[SMM-EdgeWatcher] Refresh OK — {N} CycleRunningVar vigiladas, {R} ciclos Running",
                _watchVarToGroupId.Count, _activeCycleByGroup.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMM-EdgeWatcher] Error refrescando mapa");
        }
    }

    private void OnPlcVariableChanged(object? sender, PlcNotification notification)
    {
        // 🔔 Tracking de alarmas durante ciclos abiertos (DEC-018)
        if (AlarmNotificationService.IsAlarmVariable(notification.VariableName))
        {
            if (TryToBool(notification.NewValue, out var alarmActive))
                _ = Task.Run(() => HandleAlarmChangeAsync(notification.VariableName, alarmActive));
            return;
        }

        // Filtrado rápido — la mayoría de notificaciones no son nuestras
        if (!_watchVarToGroupId.TryGetValue(notification.VariableName, out var groupId))
            return;

        // Convertir NewValue → bool
        if (!TryToBool(notification.NewValue, out var newBool))
        {
            _logger.LogWarning("[SMM-EdgeWatcher] CycleRunningVar '{V}' valor no convertible a bool: {Val}",
                notification.VariableName, notification.NewValue);
            return;
        }

        // Determinar valor previo: prioriza OldValue de la notificación; si es null,
        // usa el último valor cacheado por nosotros (puede no haber si es el primer evento).
        bool? prevBool = null;
        if (TryToBool(notification.OldValue, out var oldFromNotif))
            prevBool = oldFromNotif;
        else if (_lastBoolValue.TryGetValue(notification.VariableName, out var cachedPrev))
            prevBool = cachedPrev;

        _lastBoolValue[notification.VariableName] = newBool;

        // Si no podemos determinar el previo, inferir por el estado del ciclo:
        //  - newBool=true y sin ciclo activo  => flanco UP implícito (abrir)
        //  - newBool=false y con ciclo activo => flanco DOWN implícito (cerrar)
        if (prevBool == null)
        {
            var hasActive = _activeCycleByGroup.ContainsKey(groupId);
            if (newBool && !hasActive) prevBool = false;
            else if (!newBool && hasActive) prevBool = true;
            else
            {
                _logger.LogDebug("[SMM-EdgeWatcher] '{V}' estado inicial = {B} (sin acción)", notification.VariableName, newBool);
                return;
            }
        }

        if (prevBool == newBool) return;

        // Flanco detectado — fire-and-forget (el evento no debe bloquear al PLC)
        _ = Task.Run(async () =>
        {
            try
            {
                if (!prevBool.Value && newBool)
                    await HandleCycleStartAsync(groupId, notification.VariableName);
                else // true → false
                    await HandleCycleEndAsync(groupId, notification.VariableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SMM-EdgeWatcher] Error procesando flanco {V} {Prev}→{New}",
                    notification.VariableName, prevBool, newBool);
            }
        });
    }

    private async Task HandleCycleStartAsync(int groupId, string varName)
    {
        // Si ya hay ciclo Running para ese grupo (caso raro: backend reinició), lo respetamos.
        if (_activeCycleByGroup.ContainsKey(groupId))
        {
            _logger.LogWarning("[SMM-EdgeWatcher] Flanco UP en {V} pero grupo {G} ya tiene ciclo Running — ignorado", varName, groupId);
            return;
        }

        using var scope = _services.CreateScope();
        var capture = scope.ServiceProvider.GetRequiredService<ISmmCaptureService>();
        var cycleId = await capture.OnCycleStartAsync(groupId, DateTime.UtcNow);
        _activeCycleByGroup[groupId] = cycleId;
        _logger.LogInformation("🟢 [SMM-EdgeWatcher] Ciclo abierto group={G} cycle={C} (trigger={V})", groupId, cycleId, varName);
    }

    private async Task HandleCycleEndAsync(int groupId, string varName)
    {
        if (!_activeCycleByGroup.TryRemove(groupId, out var cycleId))
        {
            _logger.LogWarning("[SMM-EdgeWatcher] Flanco DOWN en {V} pero grupo {G} sin ciclo activo — ignorado", varName, groupId);
            return;
        }

        // 1) Snapshot de variables del grupo asociadas al ciclo
        await CaptureCycleSnapshotAsync(groupId, cycleId);

        // 2) Cerrar alarmas abiertas todavía activas asociadas a este ciclo
        await CloseOpenAlarmsForCycleAsync(cycleId, DateTime.UtcNow);

        // 3) Cerrar ciclo (recalcula AlarmsCount/AlarmTime_s)
        using var scope = _services.CreateScope();
        var capture = scope.ServiceProvider.GetRequiredService<ISmmCaptureService>();
        await capture.OnCycleEndAsync(cycleId, DateTime.UtcNow, "Normal");
        _logger.LogInformation("🔴 [SMM-EdgeWatcher] Ciclo cerrado group={G} cycle={C} (trigger={V})", groupId, cycleId, varName);
    }

    /// <summary>
    /// Asocia el cambio de una alarma a TODOS los ciclos activos:
    /// - Activación → crea SmmCycleAlarm para cada ciclo abierto.
    /// - Desactivación → cierra las alarmas abiertas con el mismo AlarmCode.
    /// </summary>
    private async Task HandleAlarmChangeAsync(string alarmVarName, bool active)
    {
        try
        {
            // Snapshot de ciclos activos (puede haber 0..N grupos PerCycle abiertos)
            var openCycles = _activeCycleByGroup.Values.ToList();
            if (openCycles.Count == 0) return;

            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();
            var now = DateTime.UtcNow;

            if (active)
            {
                foreach (var cid in openCycles)
                {
                    db.SmmCycleAlarms.Add(new SmmCycleAlarm
                    {
                        CycleId = cid,
                        AlarmCode = alarmVarName,
                        AlarmText = alarmVarName,
                        Severity = 0,
                        RaisedAt = now
                    });
                }
                await db.SaveChangesAsync();
                _logger.LogInformation("⚠️ [SMM-EdgeWatcher] Alarma '{A}' ACTIVA en {N} ciclo(s)", alarmVarName, openCycles.Count);
            }
            else
            {
                var open = await db.SmmCycleAlarms
                    .Where(a => openCycles.Contains(a.CycleId)
                                && a.AlarmCode == alarmVarName
                                && a.ClearedAt == null)
                    .ToListAsync();
                foreach (var a in open)
                {
                    a.ClearedAt = now;
                    a.DurationInCycle_s = (now - a.RaisedAt).TotalSeconds;
                }
                if (open.Count > 0)
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation("✅ [SMM-EdgeWatcher] Alarma '{A}' INACTIVA, cerrada en {N} ciclo(s)", alarmVarName, open.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMM-EdgeWatcher] Error procesando alarma {A}", alarmVarName);
        }
    }

    private async Task CloseOpenAlarmsForCycleAsync(int cycleId, DateTime now)
    {
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();
            var open = await db.SmmCycleAlarms
                .Where(a => a.CycleId == cycleId && a.ClearedAt == null)
                .ToListAsync();
            foreach (var a in open)
            {
                a.ClearedAt = now;
                a.DurationInCycle_s = (now - a.RaisedAt).TotalSeconds;
            }
            if (open.Count > 0) await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SMM-EdgeWatcher] Error cerrando alarmas abiertas del ciclo {C}", cycleId);
        }
    }

    private async Task CaptureCycleSnapshotAsync(int groupId, int cycleId)
    {
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();

            var vars = await db.SmmVariables
                .Where(v => v.GroupId == groupId && v.PlcVariable != null && v.PlcVariable != "")
                .ToListAsync();
            if (vars.Count == 0) return;

            // Leer cada variable con su tipo correcto (no usar ReadAllVariablesAsync que asume int)
            var snapshotMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in vars)
            {
                if (string.IsNullOrEmpty(v.PlcVariable)) continue;
                if (snapshotMap.ContainsKey(v.PlcVariable)) continue;
                try
                {
                    var clrType = MapDataTypeToClr(v.DataType);
                    var raw = await _twincat.ReadVariableAsync(v.PlcVariable, clrType);
                    snapshotMap[v.PlcVariable] = raw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SMM-EdgeWatcher] Error leyendo {V}", v.PlcVariable);
                    snapshotMap[v.PlcVariable] = null;
                }
            }
            var now = DateTime.UtcNow;

            var readings = new List<SmmReading>(vars.Count);
            foreach (var v in vars)
            {
                double? num = null;
                string? str = null;
                bool isError = false;
                string? errReason = null;

                if (v.PlcVariable != null && snapshotMap.TryGetValue(v.PlcVariable, out var raw))
                {
                    if (raw == null) { isError = true; errReason = "PlcReadNull"; }
                    else
                    {
                        var dt = (v.DataType ?? string.Empty).Trim().ToUpperInvariant();
                        var isStringType = dt.Contains("STRING") || dt.Contains("CHAR");
                        if (isStringType || raw is string)
                        {
                            str = NormalizePlcString(raw);
                        }
                        else
                        {
                            try { num = Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
                            catch
                            {
                                str = raw.ToString();
                            }
                        }
                    }
                }
                else
                {
                    isError = true; errReason = "NotFoundInSnapshot";
                }

                readings.Add(new SmmReading
                {
                    GroupId = groupId,
                    VariableId = v.Id,
                    CycleId = cycleId,
                    Timestamp = now,
                    Value = num,
                    StringValue = str,
                    Source = "Plc",
                    IsError = isError,
                    ErrorReason = errReason,
                    PlcVariable = v.PlcVariable
                });
            }

            db.SmmReadings.AddRange(readings);
            await db.SaveChangesAsync();
            _logger.LogInformation("📸 [SMM-EdgeWatcher] Snapshot ciclo {C}: {N} readings (group={G})",
                cycleId, readings.Count, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMM-EdgeWatcher] Error en snapshot ciclo {C} group {G}", cycleId, groupId);
        }
    }

    private static bool TryToBool(object? raw, out bool value)
    {
        value = false;
        if (raw == null) return false;
        if (raw is bool b) { value = b; return true; }
        if (raw is byte by) { value = by != 0; return true; }
        if (raw is sbyte sb) { value = sb != 0; return true; }
        if (raw is short sh) { value = sh != 0; return true; }
        if (raw is int i) { value = i != 0; return true; }
        if (raw is long l) { value = l != 0; return true; }
        if (raw is uint ui) { value = ui != 0; return true; }
        if (raw is ulong ul) { value = ul != 0; return true; }
        var s = raw.ToString();
        if (string.IsNullOrEmpty(s)) return false;
        if (bool.TryParse(s, out value)) return true;
        if (s == "1") { value = true; return true; }
        if (s == "0") { value = false; return true; }
        return false;
    }

    /// <summary>
    /// Normaliza un valor "string" del PLC. TwinCAT puede devolver:
    /// - string directo
    /// - byte[] (STRING ASCII) — null-terminated
    /// - char[] o ushort[] (WSTRING UTF-16) — null-terminated
    /// </summary>
    private static string NormalizePlcString(object raw)
    {
        switch (raw)
        {
            case string s: return TrimNull(s);
            case char[] ca: return TrimNull(new string(ca));
            case byte[] ba:
                {
                    // ASCII / STRING — cortar en primer 0
                    int len = Array.IndexOf(ba, (byte)0);
                    if (len < 0) len = ba.Length;
                    return System.Text.Encoding.ASCII.GetString(ba, 0, len);
                }
            case ushort[] wa:
                {
                    // WSTRING (UTF-16 LE) — cortar en primer 0
                    int len = Array.IndexOf(wa, (ushort)0);
                    if (len < 0) len = wa.Length;
                    var bytes = new byte[len * 2];
                    Buffer.BlockCopy(wa, 0, bytes, 0, bytes.Length);
                    return System.Text.Encoding.Unicode.GetString(bytes);
                }
            case short[] sa:
                {
                    int len = Array.IndexOf(sa, (short)0);
                    if (len < 0) len = sa.Length;
                    var bytes = new byte[len * 2];
                    Buffer.BlockCopy(sa, 0, bytes, 0, bytes.Length);
                    return System.Text.Encoding.Unicode.GetString(bytes);
                }
            default: return TrimNull(raw.ToString() ?? string.Empty);
        }
    }

    private static string TrimNull(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int idx = s.IndexOf('\0');
        return idx >= 0 ? s.Substring(0, idx) : s;
    }

    /// <summary>
    /// Mapea el DataType del Excel SMM (TwinCAT-style) al CLR type que entiende ReadVariableAsync.
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
            _ => typeof(double) // fallback genérico para numéricos desconocidos
        };
    }
}
