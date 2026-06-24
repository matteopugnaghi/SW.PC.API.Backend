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

        // 🛡️ Variables declaradas en Excel (case-insensitive). Se usa para detectar
        // bits PLC activados en sufijos NO declarados (Alarm/Notification/Info), lo que
        // indica un error de configuración entre TwinCAT y el Excel.
        private HashSet<string> _declaredAlarmKeys = new(StringComparer.OrdinalIgnoreCase);

        // Anti-spam: limita cuántas veces se loguea el mismo mismatch (clave → último timestamp)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _mismatchLogCooldown = new();
        private static readonly TimeSpan MismatchLogCooldown = TimeSpan.FromMinutes(5);
        
        public AlarmNotificationService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            IProjectContextService projectContext,
            IOptions<AlarmNotificationConfiguration> config,
            ILogger<AlarmNotificationService> logger)
        {
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _metricsService = metricsService;
            _logger = logger;
            _config = config.Value;

            // 🔄 Suscribirse a cambios de proyecto para re-registrar notificaciones
            projectContext.OnProjectChanged += OnProjectChanged;
        }

        /// <summary>
        /// 🔄 Maneja el cambio de proyecto: recarga variables de alarma y re-registra notificaciones.
        /// </summary>
        private void OnProjectChanged(string newProjectId)
        {
            _logger.LogInformation("🔄 AlarmNotificationService: Proyecto cambiado a {ProjectId} - recargando alarmas...", newProjectId);

            try
            {
                // 1. Desregistrar notificaciones del PLC anterior
                if (_notificationsRegistered)
                {
                    _twinCATService.UnregisterAllNotificationsAsync().GetAwaiter().GetResult();
                    _notificationHandles.Clear();
                    _notificationsRegistered = false;
                }

                // 2. Limpiar estados de alarma anteriores
                _alarmStates.Clear();

                // 3. Recargar variables de alarma del nuevo Excel
                LoadAlarmVariablesFromExcelAsync().GetAwaiter().GetResult();

                // 3.b Tras recargar, resetear cualquier alarma huérfana en clientes
                ResetStaleActiveAlarmsAfterReload();

                // 4. Re-registrar notificaciones si hay variables y el PLC está conectado
                if (_alarmVariables.Count > 0 && _twinCATService.IsConnected)
                {
                    RegisterAlarmNotificationsAsync().GetAwaiter().GetResult();
                }

                _logger.LogInformation("✅ AlarmNotificationService: Alarmas recargadas para proyecto {ProjectId}: {Count} variables",
                    newProjectId, _alarmVariables.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ AlarmNotificationService: Error recargando alarmas para proyecto {ProjectId}", newProjectId);
            }
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
        /// True si <c>st_alarmPc[plcIndex].suffix</c> está entre las alarmas DECLARADAS
        /// (monitorizadas, hoja Alarms). Usado por otros drivers (p.ej. Modbus) para avisar
        /// si referencian un índice que el sistema central no vigila.
        /// </summary>
        public bool IsAlarmDeclared(int plcIndex, string suffix) =>
            _declaredAlarmKeys.Any(k => k.Contains($"st_alarmPc[{plcIndex}].{suffix}", StringComparison.OrdinalIgnoreCase));

        /// <summary>Número de claves de alarma declaradas (0 hasta que se carga el Excel).</summary>
        public int DeclaredAlarmKeyCount => _declaredAlarmKeys.Count;
        
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

                        // 🔍 Rescanear el valor actual de TODAS las alarmas declaradas para detectar
                        // cambios que ocurrieron mientras el PLC estaba desconectado (p.ej. el
                        // operador limpió un bit). Sin esto, las alarmas activas en caché que
                        // ya no están activas en el PLC quedan colgadas en el frontend.
                        await RescanAllAlarmsAsync();
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
                var declaredAlarmVars = allVariables
                    .Where(v => IsAlarmVariable(v))
                    .ToList();

                // 📌 Snapshot de variables declaradas en Excel (para detectar misconfigs).
                // No incluimos st_alarmHistPc porque son auto-generadas a partir de st_alarmPc.
                _declaredAlarmKeys = new HashSet<string>(
                    declaredAlarmVars.Where(v => v.Contains("st_alarmPc[")),
                    StringComparer.OrdinalIgnoreCase);

                // 🛡️ Para CADA índice de st_alarmPc[] usado en Excel, suscribir a los 3
                // sufijos (.Alarm, .Notification, .Info) aunque no estén declarados, para
                // poder DETECTAR (no mostrar) si un técnico activa por error el bit equivocado.
                var expanded = new HashSet<string>(declaredAlarmVars, StringComparer.OrdinalIgnoreCase);
                var suffixes = new[] { "Alarm", "Notification", "Info" };
                var indexRegex = new System.Text.RegularExpressions.Regex(
                    @"^(?<prefix>.*\.st_alarmPc\[\d+\])\.(Alarm|Notification|Info)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (var v in declaredAlarmVars.Where(v => v.Contains("st_alarmPc[")))
                {
                    var m = indexRegex.Match(v);
                    if (!m.Success) continue;
                    var prefix = m.Groups["prefix"].Value;
                    foreach (var suffix in suffixes)
                    {
                        expanded.Add($"{prefix}.{suffix}");
                    }
                }

                _alarmVariables = expanded.ToList();
                
                // Contar por tipo
                var alarmPcCount = _alarmVariables.Count(v => v.Contains("st_alarmPc["));
                var alarmHistCount = _alarmVariables.Count(v => v.Contains("st_alarmHistPc["));
                var watchdogAdded = _alarmVariables.Count - declaredAlarmVars.Count;
                
                _logger.LogInformation("🔔 Variables de alarma encontradas: {Total} (activas: {Active}, historial: {History}, watchdog suffix-coverage: +{Watchdog})",
                    _alarmVariables.Count, alarmPcCount, alarmHistCount, watchdogAdded);
                
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

        /// <summary>
        /// Convierte un valor PLC arbitrario a bool de forma tolerante.
        /// El driver TwinCAT puede devolver bool/byte/sbyte/short/ushort/int/uint/long/ulong/string.
        /// Antes solo se aceptaba bool/int y cualquier otro tipo caía silenciosamente a false,
        /// lo que generaba registros falsos de "Deactivated" en el historial de alarmas.
        /// </summary>
        internal static bool ConvertPlcValueToBool(object? value)
        {
            switch (value)
            {
                case null: return false;
                case bool b: return b;
                case string s:
                    if (string.IsNullOrWhiteSpace(s)) return false;
                    if (bool.TryParse(s, out var parsed)) return parsed;
                    return s.Trim() != "0";
                default:
                    try { return Convert.ToInt64(value) != 0; }
                    catch { return false; }
            }
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

            // ⏱️ Transición pasiva del warm-up por tiempo (si no se ha disparado por
            // st_alarmHistPc o LogFromTwincat). Garantiza que el detector de misconfig
            // y el log de historial se activen aunque solo haya variables st_alarmPc.
            if (_isInWarmupPeriod && DateTime.Now >= _warmupEndTime)
            {
                _isInWarmupPeriod = false;
                _warmupAlreadyCompleted = true;
                _logger.LogInformation("🔥 Warm-up completado (transición pasiva por tiempo).");
            }
            
            try
            {
                // Convertir valor a bool (tolerante a byte/sbyte/short/ushort/uint/long/ulong/string)
                // ⚠️ Antes solo aceptaba bool/int → cualquier otro tipo caía a false y se
                // registraba como "Deactivated" creando ruido masivo en el historial.
                bool newState = ConvertPlcValueToBool(notification.NewValue);

                // 🔁 Deduplicación para variables de historial de alarma (st_alarmHistPc):
                // si el estado no cambia frente al último conocido, no transmitir ni loguear.
                // Las notificaciones push del PLC pueden repetirse y antes generaban duplicados.
                if (notification.VariableName.Contains("st_alarmHistPc[")
                    && _alarmStates.TryGetValue(notification.VariableName, out var prevState)
                    && prevState == newState)
                {
                    return;
                }

                // Actualizar estado local
                _alarmStates[notification.VariableName] = newState;

                // 🛡️ Variables PLC no declaradas en Excel (suscritas como "watchdog" a los 3
                // sufijos del índice): NUNCA se transmiten al frontend para evitar alarmas
                // fantasma. Sólo se loguean al Registro del Sistema cuando van a TRUE
                // (probable error de configuración entre TwinCAT y Excel) y fuera del warm-up.
                bool isStAlarmPc = notification.VariableName.Contains("st_alarmPc[");
                bool isDeclared = _declaredAlarmKeys.Contains(notification.VariableName);

                if (isStAlarmPc && !isDeclared)
                {
                    if (newState && !_isInWarmupPeriod)
                    {
                        LogConfigurationMismatch(notification.VariableName);
                    }
                    return;
                }

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
        /// Registra una misconfiguración: el PLC ha activado un bit cuyo sufijo
        /// (Alarm/Notification/Info) no está declarado en el Excel para ese índice.
        /// Aplica anti-spam para no inundar el log.
        /// </summary>
        private void LogConfigurationMismatch(string variableName)
        {
            var now = DateTime.Now;
            if (_mismatchLogCooldown.TryGetValue(variableName, out var last) &&
                now - last < MismatchLogCooldown)
            {
                return; // Aún en cooldown
            }
            _mismatchLogCooldown[variableName] = now;

            // Nota: este log entra automáticamente en el Registro del Sistema (no retentivo)
            // a través de SystemLogBufferProvider. NO se escribe en Operation Log (retentivo).
            _logger.LogWarning(
                "⚠️ [ConfigMismatch] Bit PLC activado SIN definición Excel: {Variable}. " +
                "Posible causa: sufijo (Alarm/Notification/Info) en TwinCAT no coincide con el declarado en Excel para ese índice. " +
                "Revise la hoja 'Alarms' del ProjectConfig.xlsm.",
                variableName);
        }

        /// <summary>
        /// Lee el valor actual de TODAS las variables de alarma declaradas directamente del PLC
        /// y reconcilia el estado en caché. Si encuentra diferencias respecto a <see cref="_alarmStates"/>,
        /// actualiza la caché y emite el broadcast SignalR correspondiente.
        ///
        /// Se invoca en dos escenarios:
        ///  • Tras (re)registrar las notificaciones cuando el PLC vuelve a conectarse.
        ///  • Como red de seguridad por si ADS no entrega un valor inicial al re-suscribir.
        ///
        /// Sólo procesa las claves "originales" (Alarm/Notification/Info) declaradas en Excel,
        /// nunca las suscripciones watchdog adicionales.
        /// </summary>
        private async Task RescanAllAlarmsAsync()
        {
            if (!_twinCATService.IsConnected)
            {
                _logger.LogDebug("⏭️ RescanAllAlarmsAsync omitido: PLC no conectado");
                return;
            }

            if (_declaredAlarmKeys.Count == 0)
            {
                _logger.LogDebug("⏭️ RescanAllAlarmsAsync omitido: no hay alarmas declaradas");
                return;
            }

            _logger.LogInformation("🔍 Rescaneando estado actual de {Count} alarmas declaradas tras reconexión...",
                _declaredAlarmKeys.Count);

            int diffsActivated = 0;
            int diffsCleared = 0;
            int readErrors = 0;
            var startTime = DateTime.Now;

            foreach (var variable in _declaredAlarmKeys.ToList())
            {
                try
                {
                    var raw = await _twinCATService.ReadVariableAsync(variable, typeof(bool));
                    if (raw is not bool plcValue)
                    {
                        readErrors++;
                        continue;
                    }

                    bool cachedValue = _alarmStates.TryGetValue(variable, out var s) && s;
                    if (plcValue == cachedValue)
                        continue;

                    _alarmStates[variable] = plcValue;
                    if (plcValue) diffsActivated++; else diffsCleared++;

                    _logger.LogInformation(
                        "🔄 [Rescan] Estado divergente detectado: {Var} caché={Cached} → PLC={Plc}",
                        variable, cachedValue, plcValue);

                    await BroadcastAlarmChangeAsync(variable, plcValue, DateTime.Now);
                }
                catch (Exception ex)
                {
                    readErrors++;
                    _logger.LogDebug(ex, "⚠️ [Rescan] Error leyendo {Var}", variable);
                }
            }

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            if (diffsActivated + diffsCleared + readErrors > 0)
            {
                _logger.LogInformation(
                    "✅ Rescan completado en {Ms:F0}ms: {Activated} activada(s), {Cleared} reseteada(s), {Errors} error(es) de lectura",
                    elapsed, diffsActivated, diffsCleared, readErrors);
            }
            else
            {
                _logger.LogDebug("✅ Rescan completado en {Ms:F0}ms sin cambios", elapsed);
            }
        }

        /// <summary>
        /// Tras recargar el Excel, resetea en los clientes cualquier alarma que estuviera ACTIVA
        /// y cuya variable ya no está declarada en la nueva configuración. Esto evita el caso
        /// "cambio el Excel de Alarm a Info y la Alarm activa nunca se resetea".
        /// </summary>
        private void ResetStaleActiveAlarmsAfterReload()
        {
            try
            {
                var stale = _alarmStates
                    .Where(kvp => kvp.Value
                                  && kvp.Key.Contains("st_alarmPc[")
                                  && !_declaredAlarmKeys.Contains(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var v in stale)
                {
                    _alarmStates[v] = false;
                    // Log al Registro del Sistema (no retentivo) vía SystemLogBufferProvider.
                    _logger.LogWarning(
                        "♻️ [ConfigReload] Alarma activa sin definición tras recargar Excel: {Variable}. " +
                        "Forzando reset en clientes.", v);
                    _ = BroadcastAlarmChangeAsync(v, false, DateTime.Now);
                }

                if (stale.Count > 0)
                {
                    _logger.LogInformation("♻️ {Count} alarma(s) huérfana(s) reseteada(s) tras recarga de configuración.", stale.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en ResetStaleActiveAlarmsAfterReload");
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
