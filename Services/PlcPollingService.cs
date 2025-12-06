using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que monitorea continuamente variables del PLC
    /// y transmite cambios via SignalR a todos los clientes conectados.
    /// Las variables se cargan automáticamente desde el Excel.
    /// </summary>
    public class PlcPollingService : BackgroundService
    {
        private readonly ITwinCATService _twinCATService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<PlcPollingService> _logger;
        private readonly PlcPollingConfiguration _config;
        private readonly Dictionary<string, PlcVariableState> _variableStates;
        private List<string> _monitoredVariables;
        private DateTime _lastExcelReload;
        private DateTime _lastTaskCycleTimeUpdate;
        private const int EXCEL_RELOAD_INTERVAL_SECONDS = 30; // Recargar Excel cada 30 segundos
        private const int TASK_CYCLE_TIME_UPDATE_SECONDS = 10; // Actualizar Task Cycle Time cada 10 segundos

        public PlcPollingService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            IOptions<PlcPollingConfiguration> config,
            ILogger<PlcPollingService> logger)
        {
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _metricsService = metricsService;
            _logger = logger;
            _config = config.Value;
            _variableStates = new Dictionary<string, PlcVariableState>();
            _monitoredVariables = new List<string>();
            _lastExcelReload = DateTime.MinValue;
            _lastTaskCycleTimeUpdate = DateTime.MinValue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 PlcPollingService iniciado - Intervalo: {Interval}ms", _config.PollingIntervalMs);

            if (!_config.Enabled)
            {
                _logger.LogWarning("⚠️ PlcPollingService deshabilitado en configuración");
                _metricsService.SetPlcPollingStatus(false, false, "Deshabilitado en configuración");
                return;
            }

            // Registrar que el servicio está habilitado
            _metricsService.SetPlcPollingStatus(true, false, "Iniciando...");

            // Cargar variables desde Excel si está habilitado
            if (_config.AutoLoadFromExcel)
            {
                try
                {
                    _logger.LogInformation("📂 Cargando variables desde Excel: {FileName}", _config.ExcelFileName);
                    
                    // Crear un scope para resolver servicios scoped
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                        _monitoredVariables = await excelConfigService.GetMonitoredVariableNamesAsync(_config.ExcelFileName);
                    }
                    
                    if (_monitoredVariables.Count == 0)
                    {
                        _logger.LogWarning("⚠️ No se encontraron variables para monitorear en el Excel");
                        _metricsService.SetPlcPollingStatus(true, false, "Sin variables en Excel");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error cargando variables desde Excel");
                    _metricsService.SetPlcPollingStatus(true, false, $"Error: {ex.Message}");
                    return;
                }
            }
            else
            {
                _logger.LogWarning("⚠️ AutoLoadFromExcel deshabilitado - No hay variables para monitorear");
                _metricsService.SetPlcPollingStatus(true, false, "AutoLoadFromExcel deshabilitado");
                return;
            }

            // Inicializar estados de variables
            foreach (var varName in _monitoredVariables)
            {
                _variableStates[varName] = new PlcVariableState
                {
                    Name = varName,
                    LastValue = null,
                    LastUpdate = DateTime.UtcNow
                };
            }

            _logger.LogInformation("📊 Monitoreando {Count} variables PLC desde Excel", _monitoredVariables.Count);
            _lastExcelReload = DateTime.UtcNow;
            
            // Actualizar estado: Conectado y funcionando (indicar si es simulado)
            var simStatus = _twinCATService.IsSimulated ? " (SIMULADO)" : "";
            _metricsService.SetPlcPollingStatus(true, true, $"OK - {_monitoredVariables.Count} variables{simStatus}", _twinCATService.IsSimulated);

            // Loop principal de polling
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Verificar si es hora de recargar el Excel
                    if ((DateTime.UtcNow - _lastExcelReload).TotalSeconds >= EXCEL_RELOAD_INTERVAL_SECONDS)
                    {
                        await ReloadExcelConfigurationAsync();
                    }
                    
                    // Actualizar Task Cycle Time del TwinCAT periódicamente
                    if ((DateTime.UtcNow - _lastTaskCycleTimeUpdate).TotalSeconds >= TASK_CYCLE_TIME_UPDATE_SECONDS)
                    {
                        await UpdateTwinCATTaskCycleTimeAsync();
                    }

                    await PollAllVariablesAsync(stoppingToken);
                    
                    // Actualizar estado a OK después de un ciclo exitoso
                    _metricsService.SetPlcPollingStatus(true, true, $"OK - {_monitoredVariables.Count} variables");
                    
                    await Task.Delay(_config.PollingIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 PlcPollingService detenido");
                    _metricsService.SetPlcPollingStatus(true, false, "Servicio detenido");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error en ciclo de polling");
                    _metricsService.SetPlcPollingStatus(true, false, $"Error: {ex.Message}");
                    await Task.Delay(5000, stoppingToken); // Esperar antes de reintentar
                }
            }
        }

        private async Task PollAllVariablesAsync(CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Verificar conexión PLC
            if (!_twinCATService.IsConnected)
            {
                _logger.LogWarning("⚠️ PLC no conectado, intentando reconexión...");
                _metricsService.SetPlcPollingStatus(true, false, "PLC desconectado - reconectando...");
                
                // Intentar reconectar
                try
                {
                    var reconnected = await _twinCATService.ConnectAsync();
                    if (!reconnected)
                    {
                        _metricsService.SetPlcPollingStatus(true, false, "PLC desconectado");
                        return;
                    }
                    _logger.LogInformation("✅ PLC reconectado exitosamente");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error al reconectar con PLC");
                    _metricsService.SetPlcPollingStatus(true, false, "Error reconexión PLC");
                    return;
                }
            }

            // Registrar número de variables monitoreadas
            _metricsService.SetPlcMonitoredVariables(_monitoredVariables.Count);

            // ✨ LECTURA EN PARALELO - Mucho más rápido que secuencial
            int errorCount = 0;
            var readTasks = _monitoredVariables.Select(varName => 
                PollSingleVariableAsync(varName, cancellationToken)
                    .ContinueWith(t => 
                    {
                        if (t.IsFaulted)
                        {
                            Interlocked.Increment(ref errorCount);
                            _logger.LogError(t.Exception, "❌ Error leyendo variable {Variable}", varName);
                            
                            // Incrementar contador de errores
                            if (_variableStates.TryGetValue(varName, out var state))
                            {
                                state.ReadErrorCount++;
                                if (state.ReadErrorCount > 10)
                                {
                                    _logger.LogWarning("⚠️ Variable {Variable} tiene {Count} errores consecutivos", 
                                        varName, state.ReadErrorCount);
                                }
                            }
                        }
                    }, cancellationToken)
            ).ToList();

            // Esperar a que terminen todas las lecturas en paralelo
            await Task.WhenAll(readTasks);
            
            // Si hubo muchos errores, probablemente el PLC está desconectado
            if (errorCount > _monitoredVariables.Count / 2)
            {
                _metricsService.SetPlcPollingStatus(true, false, $"PLC desconectado ({errorCount} errores)");
            }
            
            // Registrar tiempo del ciclo de polling
            stopwatch.Stop();
            _metricsService.RecordPlcPollingScanTime(stopwatch.Elapsed.TotalMilliseconds);
            
            _logger.LogDebug("⏱️ Polling cycle completed in {Time}ms for {Count} variables", 
                stopwatch.Elapsed.TotalMilliseconds, _monitoredVariables.Count);
        }

        private async Task PollSingleVariableAsync(string variableName, CancellationToken cancellationToken)
        {
            // Leer valor actual del PLC (asumimos Int32 para estados de bombas)
            var currentValue = await _twinCATService.ReadVariableAsync(variableName, typeof(int));

            if (currentValue == null)
            {
                // Logging reducido para performance
                // _logger.LogDebug("Variable {Variable} retornó null", variableName);
                return;
            }

            // Obtener estado previo
            var state = _variableStates[variableName];

            // Comparar con valor anterior
            bool hasChanged = state.LastValue == null || !currentValue.Equals(state.LastValue);

            if (hasChanged)
            {
                _logger.LogInformation("🔄 Cambio detectado: {Variable} = {OldValue} → {NewValue}", 
                    variableName, 
                    state.LastValue ?? "null", 
                    currentValue);

                // Actualizar estado interno
                state.LastValue = currentValue;
                state.LastUpdate = DateTime.UtcNow;
                state.ReadErrorCount = 0; // Reset contador de errores

                // Transmitir cambio via SignalR
                await BroadcastVariableChangeAsync(variableName, currentValue, cancellationToken);
            }
        }

        private async Task BroadcastVariableChangeAsync(string variableName, object value, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var updateData = new
                {
                    variableName = variableName,
                    value = value,
                    timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("PlcVariableUpdated", updateData, cancellationToken);

                stopwatch.Stop();
                _metricsService.RecordSignalRBroadcastTime(stopwatch.Elapsed.TotalMilliseconds);
                
                // Logging reducido para performance - solo en verbose mode
                // _logger.LogDebug("📡 SignalR broadcast enviado: {Variable} = {Value} ({Time}ms)", 
                //     variableName, value, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error enviando broadcast SignalR para {Variable}", variableName);
            }
        }

        /// <summary>
        /// Recarga la configuración de variables desde el Excel sin reiniciar el servicio
        /// </summary>
        private async Task ReloadExcelConfigurationAsync()
        {
            try
            {
                _logger.LogDebug("🔄 Recargando configuración desde Excel...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                    var newVariables = await excelConfigService.GetMonitoredVariableNamesAsync(_config.ExcelFileName);

                    // Comparar con variables actuales
                    var addedVariables = newVariables.Except(_monitoredVariables).ToList();
                    var removedVariables = _monitoredVariables.Except(newVariables).ToList();

                    if (addedVariables.Any() || removedVariables.Any())
                    {
                        _logger.LogInformation("📝 Detectados cambios en Excel:");
                        
                        foreach (var addedVar in addedVariables)
                        {
                            _logger.LogInformation("  ➕ Nueva variable: {Variable}", addedVar);
                            _variableStates[addedVar] = new PlcVariableState
                            {
                                Name = addedVar,
                                LastValue = null,
                                LastUpdate = DateTime.UtcNow
                            };
                        }

                        foreach (var removedVar in removedVariables)
                        {
                            _logger.LogInformation("  ➖ Variable eliminada: {Variable}", removedVar);
                            _variableStates.Remove(removedVar);
                        }

                        _monitoredVariables = newVariables;
                        _logger.LogInformation("✅ Configuración actualizada. Monitoreando {Count} variables", _monitoredVariables.Count);
                    }
                    else
                    {
                        _logger.LogDebug("✅ Sin cambios en configuración Excel");
                    }
                }

                _lastExcelReload = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error recargando configuración desde Excel");
            }
        }
        
        /// <summary>
        /// Actualiza el Task Cycle Time real del TwinCAT en el servicio de integridad
        /// </summary>
        private async Task UpdateTwinCATTaskCycleTimeAsync()
        {
            try
            {
                // Obtener Task Cycle Time real del PLC
                var taskCycleTimeMs = await _twinCATService.GetTaskCycleTimeAsync();
                
                if (taskCycleTimeMs > 0)
                {
                    // Actualizar en el SoftwareIntegrityService
                    using var scope = _serviceProvider.CreateScope();
                    var integrityService = scope.ServiceProvider.GetRequiredService<ISoftwareIntegrityService>();
                    var twinCatInfo = _twinCATService.GetVersionInfo();
                    
                    integrityService.UpdateTwinCATRuntimeInfo(
                        twinCatInfo.RuntimeVersion,
                        twinCatInfo.AdsVersion,
                        twinCatInfo.IsConnected,
                        twinCatInfo.IsSimulated,
                        taskCycleTimeMs
                    );
                    
                    _logger.LogDebug("🕐 TwinCAT Task Cycle Time actualizado: {CycleTime}ms", taskCycleTimeMs);
                }
                
                _lastTaskCycleTimeUpdate = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ No se pudo actualizar Task Cycle Time del TwinCAT");
                _lastTaskCycleTimeUpdate = DateTime.UtcNow; // Evitar reintentos constantes
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Deteniendo PlcPollingService...");
            await base.StopAsync(cancellationToken);
        }
    }
}
