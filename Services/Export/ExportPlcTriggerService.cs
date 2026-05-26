// ============================================================================
// ExportPlcTriggerService.cs — Disparador de ExportTasks por flanco PLC (Fase 2)
// ============================================================================
// HostedService Singleton que:
//   1. Se suscribe a ITwinCATService.OnVariableChanged
//   2. Mantiene un mapa  PlcVariable → [taskIds]  (recargado desde BD cada 30s)
//   3. En flanco FALSE→TRUE de la variable: ejecuta cada tarea suscrita vía
//      ExportService.RunTaskAsync (en background, sin bloquear el handler PLC)
//   4. Persiste el último valor leído (PlcLastValue) para sobrevivir reinicios
//      sin disparar la tarea con el primer sample post-boot.
//
// Solo procesa tareas con ExecutionType="plc" Y Enabled=true.
// Multi-proyecto: trabaja con el proyecto activo (active-project.json). Los
// triggers de otros proyectos quedan inertes hasta que el proyecto sea activo.
//
// Si quieres forzar recarga (p.ej. tras crear/editar una tarea PLC sin esperar
// 30 s), llama a RefreshAsync() — disponible vía la interfaz pública.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Export;
using SW.PC.API.Backend.Services;
using System.Collections.Concurrent;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportPlcTriggerService
{
    /// <summary>Recarga el mapa de variables→tareas desde BD (idempotente).</summary>
    Task RefreshAsync(CancellationToken ct = default);
}

public class ExportPlcTriggerService : BackgroundService, IExportPlcTriggerService
{
    private readonly IServiceProvider _services;
    private readonly ITwinCATService _twincat;
    private readonly ILogger<ExportPlcTriggerService> _logger;

    /// <summary>PlcVariable (case-insensitive) → set de taskIds suscritos.</summary>
    private readonly ConcurrentDictionary<string, HashSet<int>> _tasksByVariable
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>taskId → último valor bool conocido (null si nunca leído).</summary>
    private readonly ConcurrentDictionary<int, bool?> _lastValueByTask = new();

    /// <summary>Variables ya suscritas vía ADS por este servicio (case-insensitive).</summary>
    private readonly HashSet<string> _subscribedVars = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    public ExportPlcTriggerService(
        IServiceProvider services,
        ITwinCATService twincat,
        ILogger<ExportPlcTriggerService> logger)
    {
        _services = services;
        _twincat = twincat;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _twincat.OnVariableChanged += OnPlcVariableChanged;
        _logger.LogInformation("📤🎯 ExportPlcTriggerService iniciado — escuchando triggers PLC de tareas de exportación");

        // Defensivo: asegurar que la tabla ExportTasks existe en el proyecto activo.
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureExportTasksTableAsync(db);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ExportPlcTrigger] EnsureExportTasksTableAsync de arranque falló");
        }

        await RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(RefreshInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try { await RefreshAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "[ExportPlcTrigger] Refresh periódico falló"); }
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

            var plcTasks = await db.ExportTasks
                .Where(t => t.ExecutionType == "plc"
                            && t.Enabled
                            && t.PlcVariable != null
                            && t.PlcVariable != "")
                .Select(t => new { t.Id, t.PlcVariable, t.PlcLastValue })
                .ToListAsync(ct);

            // Reconstrucción atómica del mapa.
            var nextMap = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var activeIds = new HashSet<int>();
            foreach (var t in plcTasks)
            {
                activeIds.Add(t.Id);
                var v = t.PlcVariable!.Trim();
                if (!nextMap.TryGetValue(v, out var set))
                    nextMap[v] = set = new HashSet<int>();
                set.Add(t.Id);

                // Hidrata cache con valor persistido (si existe).
                _lastValueByTask.TryAdd(t.Id, t.PlcLastValue);
            }

            _tasksByVariable.Clear();
            foreach (var kv in nextMap) _tasksByVariable[kv.Key] = kv.Value;

            // Limpia cache de tareas que ya no son plc/enabled.
            foreach (var staleId in _lastValueByTask.Keys.Where(id => !activeIds.Contains(id)).ToList())
                _lastValueByTask.TryRemove(staleId, out _);

            // Asegura suscripción ADS para cada variable trigger que no estuviera
            // ya suscrita (no requiere que la variable esté en PLC_Variables.xlsm).
            foreach (var varName in nextMap.Keys)
            {
                if (_subscribedVars.Contains(varName)) continue;
                try
                {
                    var handle = await _twincat.RegisterNotificationAsync(varName, typeof(bool), 100);
                    if (handle != 0)
                    {
                        _subscribedVars.Add(varName);
                        _logger.LogInformation("📤🎯 Trigger PLC suscrito: {Var} (handle={Handle})", varName, handle);
                    }
                    else
                    {
                        _logger.LogWarning("[ExportPlcTrigger] No se pudo suscribir variable trigger '{Var}' (PLC no conectado o variable inexistente)", varName);
                    }
                }
                catch (Exception exSub)
                {
                    _logger.LogWarning(exSub, "[ExportPlcTrigger] Error suscribiendo variable trigger '{Var}'", varName);
                }
            }

            _logger.LogDebug("[ExportPlcTrigger] Refresh: {VarCount} variables vigiladas, {TaskCount} tareas activas",
                _tasksByVariable.Count, activeIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExportPlcTrigger] RefreshAsync falló");
        }
    }

    private void OnPlcVariableChanged(object? sender, Models.TwinCAT.PlcNotification e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.VariableName)) return;
            if (!_tasksByVariable.TryGetValue(e.VariableName, out var taskIds) || taskIds.Count == 0) return;
            if (!TryAsBool(e.NewValue, out var newBool)) return;

            // Copia defensiva por si el set cambia durante la iteración.
            int[] snapshot;
            lock (taskIds) snapshot = taskIds.ToArray();

            foreach (var taskId in snapshot)
            {
                var prev = _lastValueByTask.TryGetValue(taskId, out var p) ? p : null;
                _lastValueByTask[taskId] = newBool;

                // Persistir asíncronamente sin bloquear (best effort).
                _ = PersistLastValueAsync(taskId, newBool);

                // Flanco FALSE→TRUE: ejecutar.
                if (prev == false && newBool)
                {
                    _ = TriggerTaskAsync(taskId, e.VariableName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExportPlcTrigger] OnPlcVariableChanged falló para {Var}", e.VariableName);
        }
    }

    private async Task TriggerTaskAsync(int taskId, string variableName)
    {
        try
        {
            using var scope = _services.CreateScope();
            var exportService = scope.ServiceProvider.GetRequiredService<IExportService>();
            _logger.LogInformation("📤⚡ Trigger PLC: ejecutando ExportTask #{TaskId} (var={Var}, flanco false→true)",
                taskId, variableName);
            var result = await exportService.RunTaskAsync(taskId, runtimeMetadata: null);
            _logger.LogInformation("📤⚡ ExportTask #{TaskId} resultado: success={Success}, summary={Summary}",
                taskId, result.Success, result.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ExportPlcTrigger] TriggerTaskAsync falló para taskId={TaskId}", taskId);
        }
    }

    private async Task PersistLastValueAsync(int taskId, bool value)
    {
        try
        {
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IProjectDbContextFactory>();
            using var db = dbFactory.CreateDbContext();
            var task = await db.ExportTasks.FindAsync(taskId);
            if (task is null) return;
            if (task.PlcLastValue == value) return;
            task.PlcLastValue = value;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ExportPlcTrigger] PersistLastValueAsync(task={TaskId}) ignorado", taskId);
        }
    }

    private static bool TryAsBool(object? raw, out bool value)
    {
        switch (raw)
        {
            case bool b: value = b; return true;
            case byte by: value = by != 0; return true;
            case sbyte sb: value = sb != 0; return true;
            case short s: value = s != 0; return true;
            case ushort us: value = us != 0; return true;
            case int i: value = i != 0; return true;
            case uint ui: value = ui != 0; return true;
            case long l: value = l != 0; return true;
            case ulong ul: value = ul != 0; return true;
            case string str:
                if (bool.TryParse(str, out value)) return true;
                value = str == "1"; return true;
            default:
                value = false; return false;
        }
    }
}
