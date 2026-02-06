using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Models.TwinCAT;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🔔 Servicio de notificaciones ADS para alarmas.
    /// Usa notificaciones push del PLC en lugar de polling para las variables de alarma.
    /// Esto es mucho más eficiente: solo hay tráfico cuando cambia una alarma.
    /// 
    /// Variables monitoreadas:
    /// - st_alarmPc[x].Alarm / .Notification / .Info (alarmas activas)
    /// - st_alarmHistPc[x].Alarm / .Notification / .Info (historial de alarmas)
    /// </summary>
    public class AlarmNotificationService : BackgroundService
    {
        private readonly ITwinCATService _twinCATService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<AlarmNotificationService> _logger;
        private readonly AlarmNotificationConfiguration _config;
        
        // Variables de alarma registradas para notificaciones
        private List<string> _alarmVariables = new();
        private Dictionary<string, uint> _notificationHandles = new();
        private bool _notificationsRegistered = false;
        
        // � Variable WSTRING para recibir logs/mensajes desde el PLC
        private string? _logFromTwincatVariable = null;
        private string _lastLogFromTwincatValue = ""; // Para detectar cambios
        
        // �🔥 Warm-up: Ignorar notificaciones iniciales de ADS (solo al primer arranque)
        private DateTime _warmupEndTime = DateTime.MaxValue;
        private bool _isInWarmupPeriod = true;
        private bool _warmupAlreadyCompleted = false;  // Para evitar reiniciar warm-up en reconexiones
        
        // Estado actual de las alarmas (para enviar a nuevos clientes)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _alarmStates = new();
        
        public AlarmNotificationService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            IOptions<AlarmNotificationConfiguration> config,
            ILogger<AlarmNotificationService> logger)
        {
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _metricsService = metricsService;
            _logger = logger;
            _config = config.Value;
        }
        
        /// <summary>
        /// Obtiene el estado actual de todas las alarmas (para nuevos clientes)
        /// </summary>
        public IReadOnlyDictionary<string, bool> GetCurrentAlarmStates() => _alarmStates;
        
        /// <summary>
        /// Número de variables de alarma monitoreadas por notificaciones
        /// </summary>
        public int MonitoredAlarmCount => _alarmVariables.Count;
        
        /// <summary>
        /// Indica si las notificaciones están activas
        /// </summary>
        public bool NotificationsActive => _notificationsRegistered;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔔 AlarmNotificationService iniciando...");
            
            if (!_config.Enabled)
            {
                _logger.LogWarning("⚠️ AlarmNotificationService deshabilitado en configuración");
                return;
            }
            
            // Esperar a que el PLC esté conectado
            await WaitForPlcConnectionAsync(stoppingToken);
            
            if (stoppingToken.IsCancellationRequested)
                return;
            
            // Cargar variables de alarma desde Excel
            await LoadAlarmVariablesFromExcelAsync();
            
            if (_alarmVariables.Count == 0)
            {
                _logger.LogWarning("⚠️ No se encontraron variables de alarma para monitorear");
                return;
            }
            
            // Suscribirse al evento OnVariableChanged del TwinCATService
            _twinCATService.OnVariableChanged += OnAlarmChanged;
            
            // Registrar notificaciones para todas las variables de alarma
            await RegisterAlarmNotificationsAsync();
            
            // Mantener el servicio vivo y manejar reconexiones
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Verificar que las notificaciones siguen activas
                    if (!_twinCATService.IsConnected && _notificationsRegistered)
                    {
                        _logger.LogWarning("⚠️ PLC desconectado - notificaciones pueden necesitar re-registro");
                        _notificationsRegistered = false;
                    }
                    
                    // Si se reconectó y no tenemos notificaciones, re-registrar
                    if (_twinCATService.IsConnected && !_notificationsRegistered)
                    {
                        _logger.LogInformation("🔄 PLC reconectado - re-registrando notificaciones de alarma...");
                        await RegisterAlarmNotificationsAsync();
                    }
                    
                    await Task.Delay(5000, stoppingToken); // Check cada 5 segundos
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error en loop de AlarmNotificationService");
                    await Task.Delay(5000, stoppingToken);
                }
            }
            
            // Cleanup
            _twinCATService.OnVariableChanged -= OnAlarmChanged;
            await _twinCATService.UnregisterAllNotificationsAsync();
            
            _logger.LogInformation("🔔 AlarmNotificationService detenido");
        }
        
        private async Task WaitForPlcConnectionAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔔 Esperando conexión con PLC...");
            
            int attempts = 0;
            while (!_twinCATService.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                attempts++;
                if (attempts % 10 == 0) // Log cada 10 intentos (10 segundos)
                {
                    _logger.LogDebug("⏳ Esperando conexión PLC (intento {Attempt})...", attempts);
                }
                await Task.Delay(1000, stoppingToken);
            }
            
            if (_twinCATService.IsConnected)
            {
                _logger.LogInformation("✅ PLC conectado - continuando con registro de notificaciones");
            }
        }
        
        private async Task LoadAlarmVariablesFromExcelAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                
                // Obtener todas las variables del Excel
                var allVariables = await excelConfigService.GetMonitoredVariableNamesAsync(_config.ExcelFileName);
                
                // Filtrar solo las variables de alarma
                _alarmVariables = allVariables
                    .Where(v => IsAlarmVariable(v))
                    .ToList();
                
                // Contar por tipo
                var alarmPcCount = _alarmVariables.Count(v => v.Contains("st_alarmPc["));
                var alarmHistCount = _alarmVariables.Count(v => v.Contains("st_alarmHistPc["));
                
                _logger.LogInformation("🔔 Variables de alarma encontradas: {Total} (activas: {Active}, historial: {History})",
                    _alarmVariables.Count, alarmPcCount, alarmHistCount);
                
                // 📝 Cargar variable LogFromTwincat desde SystemConfiguration
                try
                {
                    var excelPath = excelConfigService.GetExcelConfigPath();
                    var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    
                    if (!string.IsNullOrWhiteSpace(systemConfig.LogFromTwincatPlcVariable))
                    {
                        _logFromTwincatVariable = systemConfig.LogFromTwincatPlcVariable;
                        _logger.LogInformation("📝 LogFromTwincat habilitado: Variable PLC = {Variable}", _logFromTwincatVariable);
                    }
                    else
                    {
                        _logger.LogInformation("📝 LogFromTwincat deshabilitado (celda vacía en Excel SystemConfig)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error cargando LogFromTwincat desde SystemConfiguration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cargando variables de alarma desde Excel");
                _alarmVariables = new List<string>();
            }
        }
        
        /// <summary>
        /// Determina si una variable es de tipo alarma (para usar notificaciones en vez de polling)
        /// </summary>
        public static bool IsAlarmVariable(string variableName)
        {
            return (variableName.Contains("st_alarmPc[") || variableName.Contains("st_alarmHistPc[")) &&
                   (variableName.EndsWith("].Alarm") || 
                    variableName.EndsWith("].Notification") || 
                    variableName.EndsWith("].Info"));
        }
        
        private async Task RegisterAlarmNotificationsAsync()
        {
            if (_alarmVariables.Count == 0)
            {
                _logger.LogWarning("⚠️ No hay variables de alarma para registrar");
                return;
            }
            
            // 🔥 IMPORTANTE: Iniciar warm-up SOLO en el primer arranque (no en reconexiones)
            // ADS envía notificaciones iniciales inmediatamente después de registrar
            if (!_warmupAlreadyCompleted)
            {
                _warmupEndTime = DateTime.Now.AddMilliseconds(_config.WarmupPeriodMs);
                _isInWarmupPeriod = true;
                _logger.LogInformation("🔥 Warm-up iniciado: ignorando Operation Log durante {Ms}ms (hasta {EndTime:HH:mm:ss.fff})", 
                    _config.WarmupPeriodMs, _warmupEndTime);
            }
            else
            {
                _logger.LogInformation("🔄 Re-registro de notificaciones (sin warm-up, ya completado anteriormente)");
            }
            
            _logger.LogInformation("🔔 Registrando {Count} notificaciones de alarma (cycle: {Cycle}ms)...",
                _alarmVariables.Count, _config.CycleTimeMs);
            
            var startTime = DateTime.Now;
            
            // Registrar todas las notificaciones en batch
            _notificationHandles = await _twinCATService.RegisterMultipleNotificationsAsync(
                _alarmVariables,
                typeof(bool),  // Todas las alarmas son BOOL
                _config.CycleTimeMs
            );
            
            var successCount = _notificationHandles.Values.Count(h => h > 0);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            
            _notificationsRegistered = successCount > 0;
            
            _logger.LogInformation("🔔 Notificaciones registradas: {Success}/{Total} en {Time}ms",
                successCount, _alarmVariables.Count, elapsed);
            
            if (successCount < _alarmVariables.Count)
            {
                var failed = _alarmVariables.Where(v => !_notificationHandles.ContainsKey(v) || _notificationHandles[v] == 0).Take(5);
                _logger.LogWarning("⚠️ Algunas notificaciones fallaron. Ejemplos: {Failed}", 
                    string.Join(", ", failed));
            }
            
            // 📝 Registrar también LogFromTwincat (WSTRING) si está configurado
            if (!string.IsNullOrWhiteSpace(_logFromTwincatVariable))
            {
                try
                {
                    var handle = await _twinCATService.RegisterNotificationAsync(
                        _logFromTwincatVariable,
                        typeof(string),  // WSTRING → string
                        _config.CycleTimeMs
                    );
                    
                    if (handle > 0)
                    {
                        _notificationHandles[_logFromTwincatVariable] = handle;
                        _logger.LogInformation("📝 LogFromTwincat registrado: {Variable} (handle: {Handle})", 
                            _logFromTwincatVariable, handle);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No se pudo registrar LogFromTwincat: {Variable}", _logFromTwincatVariable);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error registrando LogFromTwincat: {Variable}", _logFromTwincatVariable);
                }
            }
            
            // Actualizar métricas
            _metricsService.SetAlarmNotificationStatus(
                _config.Enabled, 
                _notificationsRegistered, 
                $"OK - {successCount} notificaciones activas"
            );
        }
        
        /// <summary>
        /// Handler cuando cambia una variable de alarma (notificación push del PLC)
        /// </summary>
        private void OnAlarmChanged(object? sender, PlcNotification notification)
        {
            // 📝 Verificar si es LogFromTwincat (WSTRING de mensajes del PLC)
            if (!string.IsNullOrWhiteSpace(_logFromTwincatVariable) && 
                notification.VariableName == _logFromTwincatVariable)
            {
                _ = HandleLogFromTwincatAsync(notification);
                return;
            }
            
            // Verificar que es una variable de alarma
            if (!IsAlarmVariable(notification.VariableName))
            {
                return;
            }
            
            try
            {
                // Convertir valor a bool
                bool newState = notification.NewValue switch
                {
                    bool b => b,
                    int i => i != 0,
                    _ => false
                };
                
                // Actualizar estado local
                _alarmStates[notification.VariableName] = newState;
                
                // ✅ SIEMPRE transmitir a clientes SignalR (el frontend necesita conocer el estado actual)
                _ = BroadcastAlarmChangeAsync(notification.VariableName, newState, notification.Timestamp);
                
                // 📋 Registrar en Operation Log si es variable de historial (st_alarmHistPc)
                // Solo registrar DESPUÉS del período de warm-up (ignora notificaciones iniciales)
                if (notification.VariableName.Contains("st_alarmHistPc["))
                {
                    // Verificar warm-up
                    if (_isInWarmupPeriod)
                    {
                        if (DateTime.Now >= _warmupEndTime)
                        {
                            _isInWarmupPeriod = false;
                            _warmupAlreadyCompleted = true;
                            _logger.LogInformation("🔥 Warm-up completado - ahora registrando alarmas históricas en Operation Log");
                        }
                        else
                        {
                            // Aún en warm-up: NO registrar en Operation Log (son notificaciones iniciales)
                            return;
                        }
                    }
                    
                    // Fuera de warm-up: registrar en Operation Log
                    _logger.LogInformation("📋 Registrando alarma histórica en Operation Log: {Var} = {State}", 
                        notification.VariableName, newState);
                    _ = LogAlarmHistoryAsync(notification.VariableName, newState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando cambio de alarma: {Var}", notification.VariableName);
            }
        }
        
        /// <summary>
        /// Registrar cambio de alarma histórica en Operation Log
        /// </summary>
        private async Task LogAlarmHistoryAsync(string variableName, bool isActive)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var operationLogService = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
                
                var result = await operationLogService.LogPlcAlarmHistoryAsync(variableName, isActive);
                
                if (result != null)
                {
                    _logger.LogInformation("📋 Alarma histórica registrada en Operation Log: {Var} = {State}", 
                        variableName, isActive ? "ACTIVA" : "INACTIVA");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error registrando alarma histórica en Operation Log: {Var}", variableName);
            }
        }
        
        /// <summary>
        /// Manejar mensaje recibido desde variable LogFromTwincat (WSTRING)
        /// </summary>
        private async Task HandleLogFromTwincatAsync(PlcNotification notification)
        {
            try
            {
                // Extraer el mensaje (WSTRING → string)
                string message = notification.NewValue?.ToString() ?? "";
                
                // Ignorar mensajes vacíos o iguales al anterior (warm-up o sin cambio real)
                if (string.IsNullOrWhiteSpace(message) || message == _lastLogFromTwincatValue)
                {
                    return;
                }
                
                _lastLogFromTwincatValue = message;
                
                // Ignorar durante warm-up
                if (_isInWarmupPeriod)
                {
                    if (DateTime.Now >= _warmupEndTime)
                    {
                        _isInWarmupPeriod = false;
                        _warmupAlreadyCompleted = true;
                        _logger.LogInformation("🔥 Warm-up completado - LogFromTwincat activo");
                    }
                    else
                    {
                        _logger.LogDebug("📝 [LogFromTwincat] Ignorando mensaje durante warm-up: {Message}", message);
                        return;
                    }
                }
                
                _logger.LogInformation("📝 [LogFromTwincat] Mensaje recibido: {Message}", message);
                
                // Registrar en Operation Log
                using var scope = _serviceProvider.CreateScope();
                var operationLogService = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
                
                var result = await operationLogService.LogPlcMessageAsync(message);
                
                if (result != null)
                {
                    _logger.LogInformation("📝 Mensaje PLC registrado en Operation Log: {Message}", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando LogFromTwincat: {Value}", notification.NewValue);
            }
        }
        
        private async Task BroadcastAlarmChangeAsync(string variableName, bool value, DateTime timestamp)
        {
            try
            {
                // ✅ USAR EL MISMO EVENTO QUE PlcPollingService para compatibilidad con frontend
                var updateData = new
                {
                    variableName = variableName,
                    value = value,
                    timestamp = timestamp
                };
                
                await _hubContext.Clients.All.SendAsync("PlcVariableUpdated", updateData);
                
                _logger.LogInformation("📡 Alarma broadcast enviado: {Var} = {Value}", variableName, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error broadcasting alarm change: {Var}", variableName);
            }
        }
        
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Deteniendo AlarmNotificationService...");
            
            // Desregistrar todas las notificaciones
            await _twinCATService.UnregisterAllNotificationsAsync();
            _notificationsRegistered = false;
            
            await base.StopAsync(cancellationToken);
        }
    }
    
    /// <summary>
    /// Configuración para AlarmNotificationService
    /// </summary>
    public class AlarmNotificationConfiguration
    {
        /// <summary>
        /// Habilitar/deshabilitar el servicio de notificaciones de alarma
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Tiempo de ciclo mínimo para notificaciones (ms)
        /// Valor más bajo = más responsivo pero más tráfico
        /// Recomendado: 100-500ms para alarmas
        /// </summary>
        public int CycleTimeMs { get; set; } = 100;
        
        /// <summary>
        /// Período de warm-up en milisegundos.
        /// Durante este tiempo después de registrar las notificaciones, se ignoran
        /// las notificaciones iniciales de ADS (que envía el valor actual, no un cambio real).
        /// Recomendado: 2000-5000ms dependiendo del número de variables.
        /// </summary>
        public int WarmupPeriodMs { get; set; } = 3000;
        
        /// <summary>
        /// Nombre del archivo Excel con la configuración
        /// </summary>
        public string ExcelFileName { get; set; } = "ProjectConfig.xlsm";
    }
}
