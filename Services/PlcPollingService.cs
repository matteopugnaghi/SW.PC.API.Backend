using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que monitorea continuamente variables del PLC
    /// y transmite cambios via SignalR a todos los clientes conectados.
    /// Las variables se cargan automáticamente desde el Excel.
    /// Soporta filtrado por vista activa del frontend.
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
        private List<string> _monitoredVariables;       // Todas las variables del Excel
        private List<string> _activeVariables;           // Variables filtradas por vista activa
        private DateTime _lastTaskCycleTimeUpdate;
        private bool _excelLoadedOnce = false;
        private const int TASK_CYCLE_TIME_UPDATE_SECONDS = 10;
        
        // 🔒 Semáforo para limitar lecturas concurrentes al PLC y evitar saturación/timeout
        private const int MAX_CONCURRENT_PLC_READS = 25;
        private readonly SemaphoreSlim _plcReadSemaphore = new SemaphoreSlim(MAX_CONCURRENT_PLC_READS, MAX_CONCURRENT_PLC_READS);

        // 🎯 Sistema de filtrado por vistas
        private List<VariableViewMapping> _variableViewMappings = new();
        private string _currentView = "principal";  // Vista activa del frontend
        private readonly object _viewLock = new();  // Lock para cambios de vista thread-safe
        private bool _viewFilteringEnabled = false; // Se habilita si hay hoja Variable_Views
        
        // 🔔 Último resultado de filtrado (para reenviar a nuevos clientes)
        private ViewFilterResult? _lastFilterResult = null;
        
        // 🎯 Sistema de vistas adicionales (MODEL_DETAIL, SCREEN_PANEL, etc.)
        // Estas vistas se activan/desactivan dinámicamente cuando se abren/cierran paneles
        private HashSet<string> _additionalViews = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Vista activa actual del frontend
        /// </summary>
        public string CurrentView
        {
            get { lock (_viewLock) return _currentView; }
        }

        /// <summary>
        /// 🎯 Vistas adicionales activas (MODEL_DETAIL, SCREEN_PANEL, etc.)
        /// </summary>
        public IReadOnlyCollection<string> AdditionalViews
        {
            get { lock (_viewLock) return _additionalViews.ToList(); }
        }

        /// <summary>
        /// Indica si el filtrado por vistas está activo
        /// </summary>
        public bool ViewFilteringEnabled => _viewFilteringEnabled;

        /// <summary>
        /// Número de variables actualmente monitoreadas (filtradas por vista)
        /// </summary>
        public int ActiveVariablesCount => _activeVariables?.Count ?? 0;

        /// <summary>
        /// Número total de variables en el Excel
        /// </summary>
        public int TotalVariablesCount => _monitoredVariables?.Count ?? 0;
        
        /// <summary>
        /// 🔔 Obtiene el último resultado de filtrado para enviar a nuevos clientes.
        /// Retorna null si no hay warnings pendientes.
        /// </summary>
        public ViewFilterResult? GetLastFilterResult() => _lastFilterResult;

        /// <summary>
        /// 📤 Obtiene el valor actual de una variable PLC.
        /// Usado por SignalR para enviar valor inicial al suscribirse.
        /// </summary>
        public object? GetVariableCurrentValue(string variableName)
        {
            if (_variableStates.TryGetValue(variableName, out var state))
            {
                return state.LastValue;
            }
            return null;
        }

        /// <summary>
        /// 🔔 Obtiene todos los estados de variables con sus valores actuales.
        /// Usado para enviar estados iniciales de alarmas cuando un cliente se suscribe.
        /// </summary>
        public Dictionary<string, object?> GetAllVariableStates()
        {
            var states = new Dictionary<string, object?>();
            foreach (var kvp in _variableStates)
            {
                states[kvp.Key] = kvp.Value.LastValue;
            }
            return states;
        }

        /// <summary>
        /// ⚡ Obtiene los valores actuales de todas las variables de una vista específica.
        /// Usado para enviar valores iniciales cuando se activa una vista adicional.
        /// </summary>
        public Dictionary<string, object?> GetCurrentValuesForView(string viewName)
        {
            var values = new Dictionary<string, object?>();
            
            if (!_viewFilteringEnabled || _variableViewMappings.Count == 0)
            {
                _logger.LogDebug("⚡ GetCurrentValuesForView({View}): filtrado no habilitado", viewName);
                return values;
            }
            
            using var scope = _serviceProvider.CreateScope();
            var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
            
            // Obtener variables que pertenecen a esta vista
            var viewVariables = excelConfigService.FilterVariablesForView(
                _monitoredVariables, 
                viewName, 
                _variableViewMappings
            );
            
            _logger.LogInformation("⚡ GetCurrentValuesForView({View}): {Count} variables encontradas", 
                viewName, viewVariables.Count);
            
            // Obtener valores actuales de cada variable
            foreach (var varName in viewVariables)
            {
                if (_variableStates.TryGetValue(varName, out var state) && state.LastValue != null)
                {
                    values[varName] = state.LastValue;
                    _logger.LogDebug("   ⚡ {Var} = {Value}", varName, state.LastValue);
                }
            }
            
            _logger.LogInformation("⚡ GetCurrentValuesForView({View}): {Count} valores con datos", 
                viewName, values.Count);
            
            return values;
        }

        /// <summary>
        /// 🔧 Fuerza una lectura inmediata de una variable del PLC.
        /// Útil cuando se suscribe a una variable que aún no tiene valor.
        /// Retorna el valor leído o null si hay error.
        /// </summary>
        public async Task<object?> ForceReadVariableAsync(string variableName)
        {
            try
            {
                _logger.LogInformation("🔧 Forzando lectura de {VariableName}", variableName);
                
                // Usar tipo genérico double ya que la mayoría de las variables son numéricas
                // El TwinCATService convierte automáticamente al tipo correcto
                var result = await _twinCATService.ReadVariableAsync(variableName, typeof(double));
                
                if (result != null)
                {
                    // Actualizar estado interno
                    if (!_variableStates.ContainsKey(variableName))
                    {
                        _variableStates[variableName] = new PlcVariableState
                        {
                            Name = variableName,
                            LastValue = null,
                            LastUpdate = DateTime.MinValue
                        };
                    }
                    
                    var state = _variableStates[variableName];
                    state.LastValue = result;
                    state.LastUpdate = DateTime.Now;
                    state.ReadErrorCount = 0;
                    
                    _logger.LogInformation("📤 Variable {VariableName} forzada = {Value}", 
                        variableName, result);
                    
                    return result;
                }
                
                _logger.LogWarning("⚠️ ForceRead de {VariableName} retornó null", variableName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en ForceRead de {VariableName}", variableName);
                return null;
            }
        }

        public PlcPollingService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            IProjectContextService projectContext,
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
            _activeVariables = new List<string>();
            _lastTaskCycleTimeUpdate = DateTime.MinValue;

            // 🔄 Suscribirse a cambios de proyecto para recargar variables
            projectContext.OnProjectChanged += OnProjectChanged;
        }

        /// <summary>
        /// 🔄 Maneja el cambio de proyecto: recarga variables desde el nuevo Excel.
        /// </summary>
        private void OnProjectChanged(string newProjectId)
        {
            _logger.LogInformation("🔄 PlcPollingService: Proyecto cambiado a {ProjectId} - recargando variables...", newProjectId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();

                // Recargar variables del nuevo Excel
                var newVariables = excelConfigService.GetMonitoredVariableNamesAsync(_config.ExcelFileName)
                    .GetAwaiter().GetResult();

                // Recargar mappings de vistas
                var newViewMappings = excelConfigService.LoadVariableViewsAsync(_config.ExcelFileName)
                    .GetAwaiter().GetResult();

                // Limpiar estados anteriores
                _variableStates.Clear();

                // Actualizar variables
                _monitoredVariables = newVariables;
                _variableViewMappings = newViewMappings;
                _viewFilteringEnabled = newViewMappings.Count > 0;

                // Reinicializar estados
                foreach (var varName in _monitoredVariables)
                {
                    _variableStates[varName] = new PlcVariableState
                    {
                        Name = varName,
                        LastValue = null,
                        LastUpdate = DateTime.Now
                    };
                }

                // Recalcular variables activas según vista actual
                if (_viewFilteringEnabled)
                {
                    RecalculateActiveVariables();
                }
                else
                {
                    _activeVariables = _monitoredVariables.ToList();
                }

                _logger.LogInformation("✅ PlcPollingService: Variables recargadas para proyecto {ProjectId}: {Count} variables ({Active} activas)",
                    newProjectId, _monitoredVariables.Count, _activeVariables.Count);

                var simStatus = _twinCATService.IsSimulated ? " (SIMULADO)" : "";
                _metricsService.SetPlcPollingStatus(true, true,
                    $"OK - {_activeVariables.Count}/{_monitoredVariables.Count} variables (proyecto: {newProjectId}){simStatus}",
                    _twinCATService.IsSimulated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ PlcPollingService: Error recargando variables para proyecto {ProjectId}", newProjectId);
            }
        }

        /// <summary>
        /// Cambia la vista activa del frontend. Recalcula qué variables se leen.
        /// Llamado desde ScadaHub cuando el cliente cambia de página.
        /// También notifica al PLC si CurrentScreenPlcVariable está configurado.
        /// </summary>
        public void SetActiveView(string viewName)
        {
            string newView;
            bool shouldNotifyPlc = false;
            
            lock (_viewLock)
            {
                // Permitir string vacío para indicar HMI offline
                var normalizedView = string.IsNullOrEmpty(viewName) ? "" : viewName;
                
                if (_currentView == normalizedView) return;
                
                var oldView = _currentView;
                _currentView = normalizedView;
                newView = _currentView;
                shouldNotifyPlc = true;
                
                // Recalcular variables activas si el filtrado está habilitado (solo si hay vista activa)
                if (_viewFilteringEnabled && _monitoredVariables.Count > 0 && !string.IsNullOrEmpty(newView))
                {
                    RecalculateActiveVariables();
                    _logger.LogInformation("🔄 Vista cambiada: {OldView} → {NewView}. Variables activas: {Active}/{Total}", 
                        oldView, _currentView, _activeVariables.Count, _monitoredVariables.Count);
                }
                else if (string.IsNullOrEmpty(newView))
                {
                    _logger.LogInformation("🔄 Vista cambiada: {OldView} → (vacío/offline)", oldView);
                }
            }
            
            // 📺 Notificar al PLC del cambio de pantalla (FUERA del lock para evitar deadlocks)
            if (shouldNotifyPlc)
            {
                _ = Task.Run(async () => await NotifyPlcCurrentScreenAsync(newView));
            }
        }
        
        /// <summary>
        /// 🎯 Activa una vista adicional (MODEL_DETAIL, SCREEN_PANEL, etc.)
        /// Cuando se activa, las variables asignadas a esa vista se incluyen en el polling.
        /// </summary>
        /// <param name="viewName">Nombre de la vista a activar (ej: MODEL_DETAIL)</param>
        /// <returns>True si la vista fue añadida, False si ya estaba activa</returns>
        public bool ActivateAdditionalView(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return false;
            
            lock (_viewLock)
            {
                if (_additionalViews.Add(viewName))
                {
                    _logger.LogInformation("🎯 Vista adicional ACTIVADA: {View}. Vistas activas: [{Views}]", 
                        viewName, string.Join(", ", _additionalViews));
                    
                    // Recalcular variables activas
                    if (_viewFilteringEnabled && _monitoredVariables.Count > 0)
                    {
                        RecalculateActiveVariables();
                        _logger.LogInformation("📊 Variables actualizadas: {Active}/{Total} (vista principal: {Main}, adicionales: [{Additional}])", 
                            _activeVariables.Count, _monitoredVariables.Count, _currentView, string.Join(", ", _additionalViews));
                    }
                    
                    return true;
                }
                
                _logger.LogDebug("🎯 Vista adicional {View} ya estaba activa", viewName);
                return false;
            }
        }
        
        /// <summary>
        /// 🎯 Desactiva una vista adicional (MODEL_DETAIL, SCREEN_PANEL, etc.)
        /// Cuando se desactiva, las variables exclusivas de esa vista dejan de pollearse.
        /// </summary>
        /// <param name="viewName">Nombre de la vista a desactivar</param>
        /// <returns>True si la vista fue removida, False si no estaba activa</returns>
        public bool DeactivateAdditionalView(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return false;
            
            lock (_viewLock)
            {
                if (_additionalViews.Remove(viewName))
                {
                    _logger.LogInformation("🎯 Vista adicional DESACTIVADA: {View}. Vistas activas: [{Views}]", 
                        viewName, _additionalViews.Count > 0 ? string.Join(", ", _additionalViews) : "(ninguna)");
                    
                    // Recalcular variables activas
                    if (_viewFilteringEnabled && _monitoredVariables.Count > 0)
                    {
                        RecalculateActiveVariables();
                        _logger.LogInformation("📊 Variables actualizadas: {Active}/{Total} (vista principal: {Main}, adicionales: [{Additional}])", 
                            _activeVariables.Count, _monitoredVariables.Count, _currentView, 
                            _additionalViews.Count > 0 ? string.Join(", ", _additionalViews) : "(ninguna)");
                    }
                    
                    return true;
                }
                
                _logger.LogDebug("🎯 Vista adicional {View} no estaba activa", viewName);
                return false;
            }
        }
        
        /// <summary>
        /// Fuerza la notificación al PLC de la pantalla actual, sin importar si cambió o no.
        /// Útil para shutdown del backend.
        /// </summary>
        public async Task ForceNotifyPlcScreenAsync(string screenName)
        {
            _logger.LogInformation("📺 ForceNotifyPlcScreenAsync: '{Screen}'", screenName);
            await NotifyPlcCurrentScreenAsync(screenName);
        }
        
        /// <summary>
        /// Notifica al PLC la pantalla/vista activa actual del HMI.
        /// Solo escribe si CurrentScreenPlcVariable está configurado en SystemConfig.
        /// </summary>
        private async Task NotifyPlcCurrentScreenAsync(string screenName)
        {
            try
            {
                _logger.LogInformation("📺 NotifyPlcCurrentScreenAsync llamado con screenName: '{Screen}'", screenName);;
                
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                
                var excelPath = excelConfigService.GetExcelConfigPath();
                _logger.LogDebug("📺 Cargando SystemConfig desde: {Path}", excelPath);
                
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(excelPath);
                
                if (string.IsNullOrWhiteSpace(systemConfig?.CurrentScreenPlcVariable))
                {
                    _logger.LogDebug("📺 CurrentScreenPlcVariable NO está configurado en SystemConfig - saltando notificación");
                    return;
                }
                
                var plcVariable = systemConfig.CurrentScreenPlcVariable;
                _logger.LogInformation("📺 Escribiendo al PLC: {Variable} = \"{Screen}\"", plcVariable, screenName);
                
                // Escribir la pantalla actual al PLC (variable STRING/WSTRING)
                var success = await _twinCATService.WriteVariableAsync(plcVariable, screenName, typeof(string));
                
                if (success)
                {
                    _logger.LogInformation("📺 ✅ PLC notificado exitosamente: {Variable} = \"{Screen}\"", plcVariable, screenName);
                }
                else
                {
                    _logger.LogWarning("📺 ❌ No se pudo notificar al PLC la pantalla actual: {Variable}", plcVariable);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📺 ❌ Error notificando pantalla al PLC: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Recalcula qué variables deben leerse según la vista activa y vistas adicionales
        /// </summary>
        private void RecalculateActiveVariables()
        {
            using var scope = _serviceProvider.CreateScope();
            var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
            
            // 🎯 Combinar vista principal + vistas adicionales
            var allActiveViews = new List<string>();
            
            // Vista principal
            if (!string.IsNullOrEmpty(_currentView))
            {
                allActiveViews.Add(_currentView);
            }
            
            // Vistas adicionales (MODEL_DETAIL, SCREEN_PANEL, etc.)
            allActiveViews.AddRange(_additionalViews);
            
            // Filtrar variables que pertenezcan a CUALQUIERA de las vistas activas
            _activeVariables = excelConfigService.FilterVariablesForMultipleViews(
                _monitoredVariables, 
                allActiveViews, 
                _variableViewMappings
            );
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
                        
                        // 🎯 Cargar mappings de vistas (hoja Variable_Views)
                        _variableViewMappings = await excelConfigService.LoadVariableViewsAsync(_config.ExcelFileName);
                        
                        if (_variableViewMappings.Count > 0)
                        {
                            _viewFilteringEnabled = true;
                            _logger.LogInformation("🎯 Filtrado por vistas HABILITADO: {Count} mappings cargados", _variableViewMappings.Count);
                            
                            // Calcular variables activas para la vista inicial CON ADVERTENCIAS
                            var filterResult = excelConfigService.FilterVariablesForViewWithWarnings(
                                _monitoredVariables, _currentView, _variableViewMappings);
                            
                            _activeVariables = filterResult.ActiveVariables;
                            
                            // 🔔 Guardar resultado para nuevos clientes
                            _lastFilterResult = filterResult.HasWarnings ? filterResult : null;
                            
                            _logger.LogInformation("📊 Vista inicial '{View}': {Active}/{Total} variables activas", 
                                _currentView, _activeVariables.Count, _monitoredVariables.Count);
                            
                            //  Enviar advertencias al frontend vía SignalR (si hay)
                            if (filterResult.HasWarnings)
                            {
                                _ = SendSystemWarningToFrontendAsync(filterResult);
                            }
                        }
                        else
                        {
                            _viewFilteringEnabled = false;
                            _activeVariables = _monitoredVariables.ToList();
                            _logger.LogInformation("ℹ️ Sin hoja Variable_Views - Todas las variables son GLOBAL ({Count})", _monitoredVariables.Count);
                        }
                    }
                    
                    if (_monitoredVariables.Count == 0)
                    {
                        _logger.LogWarning("⚠️ No se encontraron variables para monitorear en el Excel");
                        _metricsService.SetPlcPollingStatus(true, false, "Sin variables en Excel");
                        return;
                    }

                    // 🧾 SCG-147: emitir snapshot auditable de variables declaradas (FAT digital trace).
                    // Una sola entrada por arranque/recarga, con count + SHA256 normalizado.
                    try
                    {
                        using var snapScope = _serviceProvider.CreateScope();
                        var auditLog = snapScope.ServiceProvider.GetRequiredService<IAuditLogService>();
                        var projectContext = snapScope.ServiceProvider.GetService<IProjectContextService>();
                        var projectId = projectContext?.ActiveProjectId ?? "default";

                        var normalized = string.Join("\n",
                            _monitoredVariables
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .OrderBy(v => v, StringComparer.Ordinal));
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        var hashHex = Convert.ToHexString(
                            sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized)));

                        var details =
                            $"project={projectId}; excelFile={_config.ExcelFileName}; " +
                            $"variableCount={_monitoredVariables.Count}; " +
                            $"viewMappings={_variableViewMappings.Count}; " +
                            $"viewFilteringEnabled={_viewFilteringEnabled}; " +
                            $"sha256={hashHex}";

                        await auditLog.LogAsync(
                            AuditCategory.Plc,
                            AuditAction.PlcVariablesSnapshot,
                            AuditResult.Success,
                            details: details,
                            userId: "system",
                            userName: "PlcPollingService",
                            affectedItemCount: _monitoredVariables.Count,
                            projectId: projectId);

                        _logger.LogInformation(
                            "🧾 PLC variables snapshot audited — count: {Count}, sha256: {Hash}",
                            _monitoredVariables.Count, hashHex);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ No se pudo registrar el snapshot SCG-147 de variables PLC");
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
                    LastUpdate = DateTime.Now
                };
            }

            _logger.LogInformation("📊 Monitoreando {Count} variables PLC desde Excel", _monitoredVariables.Count);
            
            // Actualizar estado: Conectado y funcionando (indicar si es simulado)
            var simStatus = _twinCATService.IsSimulated ? " (SIMULADO)" : "";
            _metricsService.SetPlcPollingStatus(true, true, $"OK - {_monitoredVariables.Count} variables{simStatus}", _twinCATService.IsSimulated);

            // 🔔 En modo simulado, las alarmas se pollean (no hay notificaciones ADS reales)
            if (_twinCATService.IsSimulated && _config.ExcludeAlarmsFromPolling)
            {
                _logger.LogWarning("🔔 Modo SIMULADO detectado - alarmas se monitorearan por POLLING (no notificaciones ADS)");
            }
            else if (_config.ExcludeAlarmsFromPolling)
            {
                var alarmCount = _monitoredVariables.Count(v => AlarmNotificationService.IsAlarmVariable(v));
                _logger.LogInformation("🔔 Modo REAL - {Count} alarmas excluidas del polling (usan notificaciones ADS)", alarmCount);
            }

            // Marcar que Excel ya fue cargado
            _excelLoadedOnce = true;
            _logger.LogInformation("✅ Excel cargado UNA vez al inicio. No se recargará automáticamente.");

            // Loop principal de polling
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 📋 Excel solo se carga al inicio - NO se recarga periódicamente
                    // Para recargar manualmente, usar el endpoint API /api/plc/reload-config
                    
                    // Actualizar Task Cycle Time del TwinCAT periódicamente
                    if ((DateTime.Now - _lastTaskCycleTimeUpdate).TotalSeconds >= TASK_CYCLE_TIME_UPDATE_SECONDS)
                    {
                        await UpdateTwinCATTaskCycleTimeAsync();
                    }

                    await PollAllVariablesAsync(stoppingToken);
                    // Nota: UpdateTwinCATConnectionStatusAsync() se llama dentro de PollAllVariablesAsync
                    
                    // ✅ Solo marcar OK si el PLC está realmente conectado
                    if (_twinCATService.IsConnected)
                    {
                        var viewInfo = _viewFilteringEnabled ? $" (vista: {_currentView})" : "";
                        _metricsService.SetPlcPollingStatus(true, true, $"OK - {_activeVariables.Count}/{_monitoredVariables.Count} variables{viewInfo}");
                    }
                    else
                    {
                        _metricsService.SetPlcPollingStatus(true, false, "PLC desconectado");
                    }
                    
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

            // 🎯 Usar variables activas (filtradas por vista) si el filtrado está habilitado
            var variablesToPoll = _viewFilteringEnabled ? _activeVariables : _monitoredVariables;
            
            // 🔔 Excluir alarmas si están manejadas por AlarmNotificationService
            // PERO solo en modo REAL (no simulado) - en simulado las alarmas necesitan polling
            if (_config.ExcludeAlarmsFromPolling && !_twinCATService.IsSimulated)
            {
                variablesToPoll = variablesToPoll
                    .Where(v => !AlarmNotificationService.IsAlarmVariable(v))
                    .ToList();
            }
            
            // Registrar número de variables monitoreadas
            _metricsService.SetPlcMonitoredVariables(variablesToPoll.Count);

            // ✨ LECTURA EN PARALELO CON LÍMITE DE CONCURRENCIA
            // Usamos SemaphoreSlim para evitar saturar el PLC con demasiadas lecturas simultáneas
            int errorCount = 0;
            var readTasks = variablesToPoll.Select(async varName => 
            {
                // Esperar a que haya un slot disponible en el semáforo
                await _plcReadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await PollSingleVariableAsync(varName, cancellationToken);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errorCount);
                    _logger.LogError(ex, "❌ Error leyendo variable {Variable}", varName);
                    
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
                finally
                {
                    // Liberar el slot del semáforo
                    _plcReadSemaphore.Release();
                }
            }).ToList();

            // Esperar a que terminen todas las lecturas (con límite de concurrencia)
            await Task.WhenAll(readTasks);
            
            // Si hubo muchos errores, probablemente el PLC está desconectado
            if (errorCount > variablesToPoll.Count / 2)
            {
                _metricsService.SetPlcPollingStatus(true, false, $"PLC desconectado ({errorCount} errores)");
                
                // 🔴 También actualizar estado en SoftwareIntegrityService para que el frontend lo vea
                await UpdateTwinCATConnectionStatusAsync();
            }
            
            // ✅ Actualizar estado de conexión TwinCAT en cada ciclo (éxito o error)
            await UpdateTwinCATConnectionStatusAsync();
            
            // Registrar tiempo del ciclo de polling
            stopwatch.Stop();
            _metricsService.RecordPlcPollingScanTime(stopwatch.Elapsed.TotalMilliseconds);
            
            _logger.LogDebug("⏱️ Polling cycle completed in {Time}ms for {Count}/{Total} variables (view: {View})", 
                stopwatch.Elapsed.TotalMilliseconds, variablesToPoll.Count, _monitoredVariables.Count, _currentView);
        }

        private async Task PollSingleVariableAsync(string variableName, CancellationToken cancellationToken)
        {
            // 🔔 Detectar tipo de dato según el nombre de la variable
            Type dataType = typeof(int); // Por defecto int para estados de bombas, posiciones, etc.
            
            // Variables de alarma son BOOL (tanto st_alarmPc como st_alarmHistPc)
            if ((variableName.Contains("st_alarmPc[") || variableName.Contains("st_alarmHistPc[")) && 
                (variableName.EndsWith("].Alarm") || 
                 variableName.EndsWith("].Notification") || 
                 variableName.EndsWith("].Info")))
            {
                dataType = typeof(bool);
            }
            // Variables LREAL (prefijo lr_) son double - buscar .lr_ en cualquier parte antes del índice
            else if (variableName.Contains(".lr_"))
            {
                dataType = typeof(double);
            }
            // Variables REAL (prefijo r_) son float
            else if (variableName.Contains(".r_") && !variableName.Contains(".lr_"))
            {
                dataType = typeof(float);
            }
            // Variables booleanas (prefijo b_, bo_, x_)
            else if (variableName.Contains(".b_") || variableName.Contains(".bo_") || variableName.Contains(".x_"))
            {
                dataType = typeof(bool);
            }
            // Variables string (prefijo s_, str_) y WSTRING (prefijo ws_)
            else if (variableName.Contains(".s_") || variableName.Contains(".str_") || variableName.Contains(".ws_"))
            {
                dataType = typeof(string);
                _logger.LogDebug("🔤 Variable WSTRING/STRING detectada: {Var}", variableName);
            }
            
            // Leer valor actual del PLC con el tipo correcto
            var currentValue = await _twinCATService.ReadVariableAsync(variableName, dataType);

            if (currentValue == null)
            {
                // Logging reducido para performance
                // _logger.LogDebug("Variable {Variable} retornó null", variableName);
                return;
            }

            // Obtener estado previo
            var state = _variableStates[variableName];

            // Comparar con valor anterior
            // 🔧 IMPORTANTE: Si LastValue es null, es la primera lectura - NO registrar como cambio
            bool isFirstRead = state.LastValue == null;
            bool hasChanged = !isFirstRead && !currentValue.Equals(state.LastValue);

            // Siempre actualizar el estado interno (incluso en primera lectura)
            if (isFirstRead || hasChanged)
            {
                if (hasChanged)
                {
                    _logger.LogInformation("🔄 Cambio detectado: {Variable} = {OldValue} → {NewValue}", 
                        variableName, 
                        state.LastValue ?? "null", 
                        currentValue);

                    // 🔔 Forward al evento OnVariableChanged para suscriptores (Alarmas, SMM EdgeWatcher, etc.)
                    try { _twinCATService.RaiseVariableChanged(variableName, state.LastValue, currentValue); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Error forwardeando OnVariableChanged para {V}", variableName); }
                }

                // Actualizar estado interno
                state.LastValue = currentValue;
                state.LastUpdate = DateTime.Now;
                state.ReadErrorCount = 0; // Reset contador de errores

                // Transmitir cambio via SignalR (siempre, incluso primera lectura para sincronizar frontend)
                await BroadcastVariableChangeAsync(variableName, currentValue, cancellationToken);
                
                // 📋 Si es variable de historial de alarma (st_alarmHistPc), registrar en Operation Log
                // SOLO si es un cambio real (no primera lectura) para evitar spam de logs al iniciar
                if (hasChanged && variableName.Contains("st_alarmHistPc["))
                {
                    await LogAlarmHistoryAsync(variableName, currentValue);
                }
            }
        }
        
        /// <summary>
        /// Registrar cambio de alarma histórica en Operation Log
        /// </summary>
        private async Task LogAlarmHistoryAsync(string variableName, object value)
        {
            try
            {
                // Convertir valor a bool (tolerante a byte/sbyte/short/ushort/uint/long/ulong/string).
                // Antes solo aceptaba bool/int → cualquier otro tipo caía a false y se registraba
                // siempre como "Deactivated", llenando el historial de eventos falsos.
                bool isActive = AlarmNotificationService.ConvertPlcValueToBool(value);
                
                // Obtener servicio de operaciones
                using var scope = _serviceProvider.CreateScope();
                var operationLogService = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
                
                // Registrar la alarma
                await operationLogService.LogPlcAlarmHistoryAsync(variableName, isActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando alarma histórica: {Variable}", variableName);
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
                    timestamp = DateTime.Now
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
        /// Recarga la configuración de variables desde el Excel.
        /// NOTA: Ya NO se llama automáticamente cada X segundos.
        /// Solo se carga al inicio del servicio o manualmente via API.
        /// </summary>
        public async Task ReloadExcelConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("🔄 Recargando configuración desde Excel (llamada manual)...");

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
                                LastUpdate = DateTime.Now
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
                        _logger.LogInformation("✅ Sin cambios en configuración Excel");
                    }
                }
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
                
                _lastTaskCycleTimeUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ No se pudo actualizar Task Cycle Time del TwinCAT");
                _lastTaskCycleTimeUpdate = DateTime.Now; // Evitar reintentos constantes
            }
        }

        /// <summary>
        /// Actualiza el estado de conexión de TwinCAT en SoftwareIntegrityService
        /// Se llama en cada ciclo de polling para detectar desconexiones en tiempo real
        /// </summary>
        private async Task UpdateTwinCATConnectionStatusAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var integrityService = scope.ServiceProvider.GetRequiredService<ISoftwareIntegrityService>();
                var twinCatInfo = _twinCATService.GetVersionInfo();
                
                // Actualizar estado real de conexión
                integrityService.UpdateTwinCATRuntimeInfo(
                    twinCatInfo.RuntimeVersion,
                    twinCatInfo.AdsVersion,
                    twinCatInfo.IsConnected,  // ✅ Esto ahora verifica _adsClient.IsConnected
                    twinCatInfo.IsSimulated,
                    twinCatInfo.TaskCycleTimeMs
                );
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "⚠️ No se pudo actualizar estado de conexión TwinCAT");
            }
        }

        /// <summary>
        /// 🔔 Envía advertencias de configuración al frontend vía SignalR
        /// Aparecerán en el Session Event Log
        /// </summary>
        private async Task SendSystemWarningToFrontendAsync(ViewFilterResult filterResult)
        {
            try
            {
                // Esperar un poco para que los clientes se conecten
                await Task.Delay(3000);
                
                var warning = filterResult.ToSystemWarning();
                if (warning != null)
                {
                    _logger.LogInformation("🔔 Enviando SystemWarning al frontend: {Count} variables sin patrón", 
                        filterResult.UnmatchedVariables.Count);
                    
                    await _hubContext.Clients.All.SendAsync("SystemWarning", warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ No se pudo enviar SystemWarning al frontend");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Deteniendo PlcPollingService...");
            
            // 📺 Notificar al PLC que el HMI se está cerrando (pantalla vacía)
            try
            {
                _logger.LogInformation("📺 Backend cerrándose - notificando al PLC que HMI está offline");
                
                // Usar timeout para asegurar que no bloqueamos el shutdown indefinidamente
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var notifyTask = ForceNotifyPlcScreenAsync("");
                
                if (await Task.WhenAny(notifyTask, Task.Delay(3000, cts.Token)) == notifyTask)
                {
                    _logger.LogInformation("📺 ✅ PLC notificado correctamente que HMI está offline");
                }
                else
                {
                    _logger.LogWarning("📺 ⚠️ Timeout notificando al PLC - continuando con shutdown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ No se pudo notificar al PLC que HMI está offline");
            }
            
            await base.StopAsync(cancellationToken);
        }
    }
}
