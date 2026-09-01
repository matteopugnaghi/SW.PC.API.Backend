using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>Elemento simulable de la hoja "3D Elements" (padre o hijo)</summary>
    public class SimulationTargetDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>Nombre del padre si es un hijo; null para elementos raíz</summary>
        public string? Parent { get; set; }
        public bool HasState { get; set; }
        public string? StateVariable { get; set; }
        public List<string> Colors { get; set; } = new();
        public bool HasTranslation { get; set; }
        public string? TranslationVariable { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string? Axis { get; set; }
        public int StateIntervalMs { get; set; }
        public int PeriodMs { get; set; }
        public bool StateActive { get; set; }
        public bool TranslationActive { get; set; }
    }

    public class SimulationSettingsDto
    {
        public int StateIntervalMs { get; set; }
        public int TranslationPeriodMs { get; set; }
    }

    /// <summary>Ajustes persistidos por elemento (recorrido/tiempos propios + estado de activación)</summary>
    public class SimulationElementOverride
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
        public int? PeriodMs { get; set; }
        public int? StateIntervalMs { get; set; }
        public bool StateActive { get; set; }
        public bool TranslationActive { get; set; }
    }

    public class SimulationPersistedConfig
    {
        public int StateIntervalMs { get; set; } = 3000;
        public int TranslationPeriodMs { get; set; } = 8000;
        public Dictionary<string, SimulationElementOverride> Elements { get; set; } = new();
    }

    public interface ISimulationDriverService
    {
        bool IsSimulated { get; }
        Task<List<SimulationTargetDto>> GetTargetsAsync();
        SimulationSettingsDto GetSettings();
        void UpdateSettings(int? stateIntervalMs, int? translationPeriodMs);
        Task<bool> SetEnabledAsync(string key, string kind, bool enabled);
        Task UpdateElementConfigAsync(string key, double? min, double? max, int? periodMs, int? stateIntervalMs);
        Task DisableAllAsync();
        int ActiveCount { get; }
    }

    /// <summary>
    /// Motor de simulación de elementos 3D para demos (VR y escritorio).
    /// SOLO opera cuando TwinCAT está en modo simulado (UseSimulatedPlc=TRUE en Excel):
    /// con PLC real este servicio no escribe NADA (cero impacto en producción).
    /// Empuja valores por el MISMO pipeline que un PLC real: WriteVariableAsync (diccionario
    /// simulado) + RaiseVariableChanged (evento global → SignalR → colores/animaciones 3D).
    /// </summary>
    public class SimulationDriverService : BackgroundService, ISimulationDriverService
    {
        private readonly ITwinCATService _twincat;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IProjectContextService _projectContext;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly ILogger<SimulationDriverService> _logger;

        // Ciclo de estados: 2=On, 1=Off, 3=Alarma, 0=Deshabilitado (colores Excel H,I,J,K)
        private static readonly int[] StateCycle = { 2, 1, 3, 0 };

        private volatile int _stateIntervalMs = 3000;
        private volatile int _translationPeriodMs = 8000;

        private class ActiveSim
        {
            public string Kind = "";              // "state" | "translation"
            public string Variable = "";
            public double Min;
            public double Max;
            public int? PeriodMs;                 // override por elemento (null = global)
            public int? StateIntervalMs;          // override por elemento (null = global)
            public int StateIndex = -1;
            public DateTime LastStateChange = DateTime.MinValue;
            public DateTime LastPush = DateTime.MinValue;
            public long StartTick = Environment.TickCount64;
            public object? LastValue;
        }

        private readonly ConcurrentDictionary<string, ActiveSim> _active = new(); // key = "{targetKey}|{kind}"

        // 💾 Persistencia en Projects/{id}/data/simulation-config.json
        private SimulationPersistedConfig _config = new();
        private bool _configLoaded;
        private readonly object _configLock = new();
        private string ConfigFilePath => Path.Combine(_projectContext.DataPath, "simulation-config.json");

        private void EnsureConfigLoaded()
        {
            if (_configLoaded) return;
            lock (_configLock)
            {
                if (_configLoaded) return;
                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        var json = File.ReadAllText(ConfigFilePath);
                        _config = System.Text.Json.JsonSerializer.Deserialize<SimulationPersistedConfig>(json) ?? new();
                        _stateIntervalMs = Math.Max(500, _config.StateIntervalMs);
                        _translationPeriodMs = Math.Max(1000, _config.TranslationPeriodMs);
                        _logger.LogInformation("🎮 [Sim] Configuración cargada: {Count} elementos con ajustes", _config.Elements.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🎮 [Sim] No se pudo cargar simulation-config.json — se usan defaults");
                    _config = new();
                }
                _configLoaded = true;
            }
        }

        private void SaveConfig()
        {
            lock (_configLock)
            {
                try
                {
                    _config.StateIntervalMs = _stateIntervalMs;
                    _config.TranslationPeriodMs = _translationPeriodMs;
                    var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    // Escritura atómica (temp + move) — mismo patrón que el audit log
                    var tmp = ConfigFilePath + ".tmp";
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, ConfigFilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🎮 [Sim] No se pudo guardar simulation-config.json");
                }
            }
        }

        private SimulationElementOverride GetOrCreateOverride(string key)
        {
            if (!_config.Elements.TryGetValue(key, out var ov))
            {
                ov = new SimulationElementOverride();
                _config.Elements[key] = ov;
            }
            return ov;
        }

        public SimulationDriverService(
            ITwinCATService twincat,
            IServiceScopeFactory scopeFactory,
            IProjectContextService projectContext,
            IHubContext<ScadaHub> hubContext,
            ILogger<SimulationDriverService> logger)
        {
            _twincat = twincat;
            _scopeFactory = scopeFactory;
            _projectContext = projectContext;
            _hubContext = hubContext;
            _logger = logger;
        }

        public bool IsSimulated => _twincat.IsSimulated;
        public int ActiveCount => _active.Count;

        public SimulationSettingsDto GetSettings() => new()
        {
            StateIntervalMs = _stateIntervalMs,
            TranslationPeriodMs = _translationPeriodMs
        };

        public void UpdateSettings(int? stateIntervalMs, int? translationPeriodMs)
        {
            EnsureConfigLoaded();
            if (stateIntervalMs.HasValue) _stateIntervalMs = Math.Max(500, stateIntervalMs.Value);
            if (translationPeriodMs.HasValue) _translationPeriodMs = Math.Max(1000, translationPeriodMs.Value);
            SaveConfig();
            _logger.LogInformation("🎮 [Sim] Ajustes: ciclo estado={State}ms, periodo traslación={Trans}ms",
                _stateIntervalMs, _translationPeriodMs);
        }

        public async Task UpdateElementConfigAsync(string key, double? min, double? max, int? periodMs, int? stateIntervalMs)
        {
            EnsureConfigLoaded();
            var ov = GetOrCreateOverride(key);
            if (min.HasValue) ov.Min = min;
            if (max.HasValue) ov.Max = max;
            if (periodMs.HasValue) ov.PeriodMs = Math.Max(1000, periodMs.Value);
            if (stateIntervalMs.HasValue) ov.StateIntervalMs = Math.Max(500, stateIntervalMs.Value);
            SaveConfig();

            // Aplicar en vivo si la simulación está activa
            if (_active.TryGetValue($"{key}|translation", out var trans))
            {
                if (ov.Min.HasValue) trans.Min = ov.Min.Value;
                if (ov.Max.HasValue) trans.Max = ov.Max.Value;
                trans.PeriodMs = ov.PeriodMs;
            }
            if (_active.TryGetValue($"{key}|state", out var st))
            {
                st.StateIntervalMs = ov.StateIntervalMs;
            }
            _logger.LogInformation("🎮 [Sim] Config de '{Key}': min={Min} max={Max} periodo={Period}ms cicloEstado={State}ms",
                key, ov.Min, ov.Max, ov.PeriodMs, ov.StateIntervalMs);
            await Task.CompletedTask;
        }

        public async Task<List<SimulationTargetDto>> GetTargetsAsync()
        {
            EnsureConfigLoaded();
            var result = new List<SimulationTargetDto>();
            using var scope = _scopeFactory.CreateScope();
            var pumpService = scope.ServiceProvider.GetRequiredService<IPumpElementService>();
            var elements = await pumpService.LoadPumpElementsAsync(_projectContext.ExcelConfigPath);

            foreach (var el in elements)
            {
                if (string.IsNullOrWhiteSpace(el.Name)) continue;

                var target = new SimulationTargetDto
                {
                    Key = el.Name,
                    Name = el.Name,
                    HasState = !string.IsNullOrWhiteSpace(el.PlcMainPageReference),
                    StateVariable = el.PlcMainPageReference,
                    Colors = new List<string> { el.ColorElementOn, el.ColorElementOff, el.ColorElementDisabled, el.ColorElementAlarm },
                    HasTranslation = IsRefPlc(el.AnimationType) && !string.IsNullOrWhiteSpace(el.AnimationPlcVariable),
                    TranslationVariable = el.AnimationPlcVariable,
                    MinValue = el.AnimationMinValue,
                    MaxValue = el.AnimationMaxValue,
                    Axis = el.AnimationAxis
                };
                if (target.HasState || target.HasTranslation)
                {
                    MarkActive(target);
                    result.Add(target);
                }

                // Hijos 1..5: solo traslación (su variable PLC controla el movimiento)
                AddChild(result, el.Name, 1, el.Child1_Name, el.Child1_AnimationType, el.Child1_PlcVariable, el.Child1_MinValue, el.Child1_MaxValue, el.Child1_Axis, el.Child1_ColorOn, el.Child1_ColorOff, el.Child1_ColorDisabled, el.Child1_ColorAlarm);
                AddChild(result, el.Name, 2, el.Child2_Name, el.Child2_AnimationType, el.Child2_PlcVariable, el.Child2_MinValue, el.Child2_MaxValue, el.Child2_Axis, el.Child2_ColorOn, el.Child2_ColorOff, el.Child2_ColorDisabled, el.Child2_ColorAlarm);
                AddChild(result, el.Name, 3, el.Child3_Name, el.Child3_AnimationType, el.Child3_PlcVariable, el.Child3_MinValue, el.Child3_MaxValue, el.Child3_Axis, el.Child3_ColorOn, el.Child3_ColorOff, el.Child3_ColorDisabled, el.Child3_ColorAlarm);
                AddChild(result, el.Name, 4, el.Child4_Name, el.Child4_AnimationType, el.Child4_PlcVariable, el.Child4_MinValue, el.Child4_MaxValue, el.Child4_Axis, el.Child4_ColorOn, el.Child4_ColorOff, el.Child4_ColorDisabled, el.Child4_ColorAlarm);
                AddChild(result, el.Name, 5, el.Child5_Name, el.Child5_AnimationType, el.Child5_PlcVariable, el.Child5_MinValue, el.Child5_MaxValue, el.Child5_Axis, el.Child5_ColorOn, el.Child5_ColorOff, el.Child5_ColorDisabled, el.Child5_ColorAlarm);
            }

            return result;
        }

        private void AddChild(List<SimulationTargetDto> result, string parentName, int index,
            string? name, string animType, string? plcVar, double min, double max, string? axis,
            string colorOn, string colorOff, string colorDisabled, string colorAlarm)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var hasTranslation = IsRefPlc(animType) && !string.IsNullOrWhiteSpace(plcVar);
            if (!hasTranslation) return;

            var target = new SimulationTargetDto
            {
                Key = $"{parentName}::Child{index}",
                Name = name,
                Parent = parentName,
                HasState = false,
                Colors = new List<string> { colorOn, colorOff, colorDisabled, colorAlarm },
                HasTranslation = true,
                TranslationVariable = plcVar,
                MinValue = min,
                MaxValue = max,
                Axis = axis
            };
            MarkActive(target);
            result.Add(target);
        }

        private void MarkActive(SimulationTargetDto target)
        {
            target.StateActive = _active.ContainsKey($"{target.Key}|state");
            target.TranslationActive = _active.ContainsKey($"{target.Key}|translation");
            // Aplicar overrides persistidos (recorrido/tiempos propios)
            if (_config.Elements.TryGetValue(target.Key, out var ov))
            {
                if (ov.Min.HasValue) target.MinValue = ov.Min.Value;
                if (ov.Max.HasValue) target.MaxValue = ov.Max.Value;
                target.PeriodMs = ov.PeriodMs ?? _translationPeriodMs;
                target.StateIntervalMs = ov.StateIntervalMs ?? _stateIntervalMs;
            }
            else
            {
                target.PeriodMs = _translationPeriodMs;
                target.StateIntervalMs = _stateIntervalMs;
            }
        }

        private static bool IsRefPlc(string? animationType) =>
            !string.IsNullOrWhiteSpace(animationType) &&
            animationType.Trim().StartsWith("REF PLC", StringComparison.OrdinalIgnoreCase);

        public async Task<bool> SetEnabledAsync(string key, string kind, bool enabled)
        {
            if (!IsSimulated)
            {
                _logger.LogWarning("🎮 [Sim] Ignorado toggle de '{Key}' — TwinCAT NO está en modo simulado", key);
                return false;
            }

            var dictKey = $"{key}|{kind}";
            if (!enabled)
            {
                if (_active.TryRemove(dictKey, out var removed))
                {
                    // Estado de reposo al desactivar: estado 0 (color "deshabilitado") / posición original 0
                    await ResetToRestAsync(removed);
                }
                PersistActiveFlag(key, kind, false);
                _logger.LogInformation("🎮 [Sim] Desactivado {Kind} de '{Key}' ({Count} activas)", kind, key, _active.Count);
                return true;
            }

            var targets = await GetTargetsAsync();
            var target = targets.FirstOrDefault(t => t.Key == key);
            if (target == null) return false;

            if (kind == "state" && target.HasState && !string.IsNullOrWhiteSpace(target.StateVariable))
            {
                _active[dictKey] = new ActiveSim
                {
                    Kind = "state",
                    Variable = target.StateVariable!,
                    StateIntervalMs = _config.Elements.TryGetValue(key, out var ovS) ? ovS.StateIntervalMs : null
                };
            }
            else if (kind == "translation" && target.HasTranslation && !string.IsNullOrWhiteSpace(target.TranslationVariable))
            {
                _active[dictKey] = new ActiveSim
                {
                    Kind = "translation",
                    Variable = target.TranslationVariable!,
                    Min = target.MinValue,
                    Max = target.MaxValue,
                    PeriodMs = _config.Elements.TryGetValue(key, out var ovT) ? ovT.PeriodMs : null
                };
            }
            else
            {
                return false;
            }

            PersistActiveFlag(key, kind, true);
            _logger.LogInformation("🎮 [Sim] Activado {Kind} de '{Key}' → {Var} ({Count} activas)",
                kind, key, _active[dictKey].Variable, _active.Count);
            return true;
        }

        private void PersistActiveFlag(string key, string kind, bool active)
        {
            var ov = GetOrCreateOverride(key);
            if (kind == "state") ov.StateActive = active; else ov.TranslationActive = active;
            SaveConfig();
        }

        public async Task DisableAllAsync()
        {
            var sims = _active.Values.ToList();
            _active.Clear();
            foreach (var sim in sims)
            {
                await ResetToRestAsync(sim);
            }
            EnsureConfigLoaded();
            foreach (var ov in _config.Elements.Values)
            {
                ov.StateActive = false;
                ov.TranslationActive = false;
            }
            SaveConfig();
            _logger.LogInformation("🎮 [Sim] Todas las simulaciones desactivadas ({Count} reseteadas)", sims.Count);
        }

        /// <summary>Devuelve la variable a su valor de reposo: estado=0, posición=0</summary>
        private async Task ResetToRestAsync(ActiveSim sim)
        {
            try
            {
                if (sim.Kind == "state")
                    await PushValueAsync(sim, 0, typeof(int), CancellationToken.None);
                else
                    await PushValueAsync(sim, 0.0, typeof(double), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🎮 [Sim] No se pudo resetear {Var}", sim.Variable);
            }
        }

        /// <summary>
        /// Escribe el valor simulado y lo difunde por los DOS canales: evento global
        /// (suscriptores internos: SMM, alarmas…) y SignalR directo al frontend.
        /// En modo simulado el polling NO lee (ReadVariableAsync lanza a propósito),
        /// así que sin este broadcast el 3D nunca vería los cambios.
        /// </summary>
        private async Task PushValueAsync(ActiveSim sim, object newValue, Type dataType, CancellationToken ct)
        {
            await _twincat.WriteVariableAsync(sim.Variable, newValue, dataType);
            _twincat.RaiseVariableChanged(sim.Variable, sim.LastValue, newValue);
            await _hubContext.Clients.All.SendAsync("PlcVariableUpdated", new
            {
                variableName = sim.Variable,
                value = newValue,
                timestamp = DateTime.Now
            }, ct);
            sim.LastValue = newValue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🎮 SimulationDriverService iniciado (solo opera con UseSimulatedPlc=TRUE)");
            // Re-armar simulaciones persistidas (solo en modo simulado): tras un reinicio
            // en la feria no hay que volver a marcar los checks uno a uno
            try
            {
                if (_twincat.IsSimulated)
                {
                    await Task.Delay(5000, stoppingToken); // esperar a que el Excel/proyecto esté cargado
                    EnsureConfigLoaded();
                    var persisted = _config.Elements.Where(kv => kv.Value.StateActive || kv.Value.TranslationActive).ToList();
                    foreach (var (key, ov) in persisted)
                    {
                        if (ov.StateActive) await SetEnabledAsync(key, "state", true);
                        if (ov.TranslationActive) await SetEnabledAsync(key, "translation", true);
                    }
                    if (persisted.Count > 0)
                        _logger.LogInformation("🎮 [Sim] Re-armadas {Count} simulaciones persistidas", _active.Count);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🎮 [Sim] No se pudieron re-armar las simulaciones persistidas");
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(150, stoppingToken);
                    // Guardia dura: con PLC real jamás se escribe nada
                    if (!_twincat.IsSimulated || _active.IsEmpty) continue;

                    var now = DateTime.UtcNow;
                    foreach (var (dictKey, sim) in _active)
                    {
                        if (sim.Kind == "state")
                        {
                            var interval = sim.StateIntervalMs ?? _stateIntervalMs;
                            if ((now - sim.LastStateChange).TotalMilliseconds < interval) continue;
                            sim.LastStateChange = now;
                            sim.StateIndex = (sim.StateIndex + 1) % StateCycle.Length;
                            var newValue = StateCycle[sim.StateIndex];
                            await PushValueAsync(sim, newValue, typeof(int), stoppingToken);
                        }
                        else // translation: onda triangular Min → Max → Min
                        {
                            // Push cada 500ms: la extrapolación del frontend necesita updates espaciados
                            // >300ms (como el PLC real) para calcular velocidad y mover LINEALMENTE.
                            // Más rápido = la anula y el movimiento se ve a tirones (stop-go).
                            if ((now - sim.LastPush).TotalMilliseconds < 500) continue;
                            sim.LastPush = now;
                            var period = Math.Max(1000, sim.PeriodMs ?? _translationPeriodMs);
                            var elapsed = (Environment.TickCount64 - sim.StartTick) % period;
                            var phase = elapsed / (double)period * 2.0;   // 0..2
                            var t = phase <= 1.0 ? phase : 2.0 - phase;   // 0→1→0
                            var newValue = Math.Round(sim.Min + (sim.Max - sim.Min) * t, 2);
                            await PushValueAsync(sim, newValue, typeof(double), stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🎮 [Sim] Error en el bucle de simulación");
                }
            }
        }
    }
}
