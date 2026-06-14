using SW.PC.API.Backend.Models.TwinCAT;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using TwinCAT.Ads;
using System.Runtime.InteropServices;

namespace SW.PC.API.Backend.Services
{
    public interface ITwinCATService
    {
        Task<bool> ConnectAsync();
        Task<bool> DisconnectAsync();
        bool IsConnected { get; }
        bool IsSimulated { get; }
        Task<PlcDataSnapshot> ReadAllVariablesAsync(List<string> variableNames);
        Task<object?> ReadVariableAsync(string variableName, Type dataType);
        Task<bool> WriteVariableAsync(string variableName, object value, Type dataType);
        Task<PlcState> GetPlcStateAsync();
        TwinCATVersionInfo GetVersionInfo();
        Task<double> GetTaskCycleTimeAsync();
        event EventHandler<PlcNotification>? OnVariableChanged;

        /// <summary>
        /// Dispara manualmente el evento OnVariableChanged. Usado por PlcPollingService
        /// para reenviar cambios detectados por polling a los suscriptores (alarmas, SMM, etc).
        /// </summary>
        void RaiseVariableChanged(string variableName, object? oldValue, object? newValue);

        // 🔔 ADS Notifications API - Push notifications from PLC
        /// <summary>
        /// Register a single variable for ADS notifications (push on change).
        /// Returns the notification handle, or 0 if failed.
        /// </summary>
        Task<uint> RegisterNotificationAsync(string variableName, Type dataType, int cycleTimeMs = 100);
        
        /// <summary>
        /// Register multiple variables for ADS notifications in batch.
        /// Returns dictionary of variableName -> notificationHandle (0 if failed).
        /// </summary>
        Task<Dictionary<string, uint>> RegisterMultipleNotificationsAsync(
            IEnumerable<string> variableNames, Type dataType, int cycleTimeMs = 100);
        
        /// <summary>
        /// Unregister a notification by handle.
        /// </summary>
        Task<bool> UnregisterNotificationAsync(uint notificationHandle);
        
        /// <summary>
        /// Unregister all active notifications.
        /// </summary>
        Task UnregisterAllNotificationsAsync();
        
        /// <summary>
        /// Number of active notification registrations.
        /// </summary>
        int ActiveNotificationCount { get; }

        /// <summary>
        /// Reconfigura la conexión TwinCAT con nuevos parámetros del proyecto activo.
        /// Desconecta del PLC actual, actualiza AMS Net ID/Port, y reconecta.
        /// </summary>
        Task<bool> ReconfigureAsync(string newNetId, int newPort, bool useSimulatedPlc);
    }
    
    public class TwinCATService : ITwinCATService, IDisposable
    {
        private readonly ILogger<TwinCATService> _logger;
        private readonly IAuditLogService _auditLog;
        private AdsConfiguration _config;
        private AdsClient? _adsClient;  // ✅ CLASE CORRECTA de Beckhoff 6.x
        private bool _isConnected;
        private bool _isSimulatedMode = false;  // ⚡ Por defecto FALSE - simulación es opcional
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _simulatedVariables = new();
        private readonly Random _random = new();
        
        // 🔴 Cache de variables que fallan - evitar reintentar constantemente
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _failedVariables = new();
        private readonly TimeSpan _failedVariableRetryInterval = TimeSpan.FromMinutes(1); // Reintentar cada minuto
        
        // 🔴 Contador de errores de timeout consecutivos para detectar desconexión
        private int _consecutiveTimeoutErrors = 0;
        private const int MAX_TIMEOUT_ERRORS_BEFORE_DISCONNECT = 3; // 3 errores consecutivos = desconectado
        
        // ⚡ Cache de handles ADS para evitar crear/destruir en cada operación
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, uint> _handleCache = new();
        private readonly object _handleLock = new object();
        
        // 🔔 ADS Notifications - Push notifications from PLC
        private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, NotificationRegistration> _notificationRegistrations = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _lastNotifiedValues = new();
        
        public event EventHandler<PlcNotification>? OnVariableChanged;

        /// <summary>
        /// Dispara manualmente el evento OnVariableChanged (usado por PlcPollingService
        /// para forwardear cambios detectados por polling).
        /// </summary>
        public void RaiseVariableChanged(string variableName, object? oldValue, object? newValue)
        {
            try
            {
                OnVariableChanged?.Invoke(this, new PlcNotification
                {
                    VariableName = variableName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Timestamp = DateTime.Now,
                    NotificationHandle = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invocando OnVariableChanged para {Var}", variableName);
            }
        }
        
        /// <summary>
        /// Number of active ADS notification registrations
        /// </summary>
        public int ActiveNotificationCount => _notificationRegistrations.Count;
        
        public bool IsConnected 
        {
            get 
            {
                // En modo simulado, usar solo el flag interno
                if (_isSimulatedMode)
                {
                    return _isConnected;
                }
                
                // En modo REAL, verificar también el estado real del cliente ADS
                if (_adsClient != null && !_adsClient.IsConnected)
                {
                    // El cliente ADS detectó una desconexión - actualizar nuestro estado
                    if (_isConnected)
                    {
                        _logger.LogWarning("⚠️ PLC desconectado detectado por AdsClient.IsConnected = false");
                        _isConnected = false;
                        // 🧹 Invalidar handles cacheados: tras una reconexión del PLC los handles antiguos
                        // ya no son válidos en el lado del PLC y el primer Write fallaría silenciosamente.
                        ClearHandleCache();
                    }
                }
                
                return _isConnected;
            }
        }
        public bool IsSimulated => _isSimulatedMode;
        
        // Cache del Task Cycle Time (se actualiza periódicamente)
        private double _cachedTaskCycleTimeMs = 0;
        private DateTime _lastTaskCycleTimeUpdate = DateTime.MinValue;

        /// <summary>
        /// 🔐 Obtener información de versión de TwinCAT para ciberseguridad
        /// </summary>
        public TwinCATVersionInfo GetVersionInfo()
        {
            var info = new TwinCATVersionInfo
            {
                TargetNetId = _config.NetId,
                IsConnected = _isConnected,
                IsSimulated = _isSimulatedMode,
                DeviceState = _isConnected ? (_isSimulatedMode ? "Simulated" : "Connected") : "Disconnected"
            };

            if (_adsClient != null && _isConnected && !_isSimulatedMode)
            {
                try
                {
                    // Obtener versión del ADS Client (librería Beckhoff)
                    var adsVersion = typeof(AdsClient).Assembly.GetName().Version;
                    info.AdsVersion = adsVersion?.ToString() ?? "Unknown";
                    
                    // Leer información del dispositivo PLC
                    var deviceInfo = _adsClient.ReadDeviceInfo();
                    
                    // Formato: "TwinCAT 3.1 Build 4024" o similar
                    info.RuntimeVersion = $"TwinCAT {deviceInfo.Version.Version}.{deviceInfo.Version.Revision} Build {deviceInfo.Version.Build}";
                    info.MajorVersion = deviceInfo.Version.Version;
                    info.MinorVersion = deviceInfo.Version.Revision;
                    info.BuildNumber = deviceInfo.Version.Build;
                    info.DeviceName = deviceInfo.Name;
                    
                    // Añadir Task Cycle Time si está disponible
                    info.TaskCycleTimeMs = _cachedTaskCycleTimeMs;
                    
                    // Solo log de debug - no spamear en cada ciclo
                    _logger.LogDebug("🔧 TwinCAT Runtime: {Version} ({Name})", info.RuntimeVersion, deviceInfo.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read TwinCAT device info");
                    info.RuntimeVersion = "TwinCAT 3.x (version unknown)";
                }
            }
            else if (_isSimulatedMode)
            {
                // Modo simulado - usar versión genérica y cycle time simulado (10ms típico)
                info.RuntimeVersion = "TwinCAT 3.1.4024 (Simulated)";
                info.AdsVersion = typeof(AdsClient).Assembly.GetName().Version?.ToString() ?? "6.x";
                info.MajorVersion = 3;
                info.MinorVersion = 1;
                info.BuildNumber = 4024;
                info.TaskCycleTimeMs = 10.0; // 10ms típico en simulación
                info.TaskName = "PlcTask (Simulated)";
            }
            else
            {
                // 🔴 DESCONECTADO - No simulado, simplemente no hay conexión
                info.RuntimeVersion = "Disconnected";
                info.AdsVersion = typeof(AdsClient).Assembly.GetName().Version?.ToString() ?? "6.x";
                info.MajorVersion = 0;
                info.MinorVersion = 0;
                info.BuildNumber = 0;
                info.TaskCycleTimeMs = 0;
                info.TaskName = "N/A";
                info.DeviceState = "Disconnected";
            }

            return info;
        }
        
        /// <summary>
        /// 🕐 Obtener el Task Cycle Time real del PLC TwinCAT
        /// Lee la variable de sistema TwinCAT que contiene el cycle time configurado
        /// </summary>
        public async Task<double> GetTaskCycleTimeAsync()
        {
            // Cache de 5 segundos - el cycle time no cambia frecuentemente
            if ((DateTime.Now - _lastTaskCycleTimeUpdate).TotalSeconds < 5 && _cachedTaskCycleTimeMs > 0)
            {
                return _cachedTaskCycleTimeMs;
            }
            
            if (!_isConnected)
            {
                return 0;
            }
            
            if (_isSimulatedMode)
            {
                // Simulación: cycle time típico de 10ms
                _cachedTaskCycleTimeMs = 10.0;
                _lastTaskCycleTimeUpdate = DateTime.Now;
                return _cachedTaskCycleTimeMs;
            }
            
            if (_adsClient == null)
            {
                return 0;
            }
            
            try
            {
                // Lista de posibles rutas para el CycleTime en diferentes versiones/configuraciones de TwinCAT
                // El CycleTime en TwinCAT está en unidades de 100ns (10000 = 1ms)
                string[] possiblePaths = new[]
                {
                    // Tu configuración específica
                    "In_Out.TaskInfo.CycleTime",
                    
                    // Rutas con PlcTask (tu tarea)
                    "PlcTask.Info.CycleTime",
                    "PlcTask._TaskInfo.CycleTime",
                    
                    // Rutas de sistema TwinCAT
                    "TwinCAT_SystemInfoVarList._TaskInfo[1].CycleTime",
                    "_TaskInfo[1].CycleTime",
                    
                    // Variables globales comunes
                    "GVL._TaskInfo.CycleTime",
                    "GVL_System._TaskInfo.CycleTime",
                    "MAIN._TaskInfo.CycleTime",
                    
                    // PlcTaskSystemInfo
                    "PlcTaskSystemInfo.CycleTime",
                    "TcSystemInfo.PlcTask.CycleTime"
                };
                
                foreach (var path in possiblePaths)
                {
                    try
                    {
                        var handle = _adsClient.CreateVariableHandle(path);
                        var cycleTime100ns = _adsClient.ReadAny<uint>(handle);
                        _adsClient.DeleteVariableHandle(handle);
                        
                        // Convertir de 100ns a milisegundos
                        _cachedTaskCycleTimeMs = cycleTime100ns / 10000.0;
                        _lastTaskCycleTimeUpdate = DateTime.Now;
                        
                        _logger.LogInformation("🕐 TwinCAT Task Cycle Time: {CycleTime}ms (from: {Path}, raw: {Raw} x 100ns)", 
                            _cachedTaskCycleTimeMs, path, cycleTime100ns);
                        
                        return _cachedTaskCycleTimeMs;
                    }
                    catch (AdsErrorException)
                    {
                        // Path no encontrado, intentar siguiente
                        continue;
                    }
                }
                
                // Si ningún path funciona, intentar leer la configuración del Task via índice de grupo
                try
                {
                    // ADS Index Group 0x4020 = Task Info, Offset 0 = configuración de la primera tarea
                    // Leer cycle time directamente del sistema (offset 4 = CycleTime en UDINT)
                    byte[] buffer = new byte[4];
                    _adsClient.Read(0x4020, 0x4, buffer.AsMemory());
                    var cycleTime100ns = BitConverter.ToUInt32(buffer, 0);
                    
                    if (cycleTime100ns > 0 && cycleTime100ns < 100000000) // Sanity check: < 10 segundos
                    {
                        _cachedTaskCycleTimeMs = cycleTime100ns / 10000.0;
                        _lastTaskCycleTimeUpdate = DateTime.Now;
                        
                        _logger.LogInformation("🕐 TwinCAT Task Cycle Time: {CycleTime}ms (from ADS Index Group 0x4020)", 
                            _cachedTaskCycleTimeMs);
                        
                        return _cachedTaskCycleTimeMs;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not read via ADS Index Group: {Error}", ex.Message);
                }
                
                // Si todo falla, usar valor por defecto
                _logger.LogWarning("⚠️ Could not read TwinCAT Task Cycle Time - using default 10ms. Add '_TaskInfo : PlcTaskSystemInfo' to your PLC project GVL.");
                _cachedTaskCycleTimeMs = 10.0;
                _lastTaskCycleTimeUpdate = DateTime.Now;
                
                return _cachedTaskCycleTimeMs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading TwinCAT Task Cycle Time");
                return 0;
            }
        }
        
        private bool _forceSimulatedMode = false; // Forzar modo simulado desde Excel
        
        public TwinCATService(IConfiguration configuration, ILogger<TwinCATService> logger, IExcelConfigService excelConfig, IProjectContextService projectContext, IAuditLogService auditLog)
        {
            _logger = logger;
            _auditLog = auditLog;
            
            // Cargar configuración desde Excel del PROYECTO ACTIVO (prioridad) o legacy/fallback
            SystemConfiguration? systemConfig = null;
            try
            {
                // ⭐ PRIMERO: Usar el sistema multi-proyecto para obtener la ruta correcta
                var projectExcelPath = Path.Combine(projectContext.ConfigPath, "ProjectConfig.xlsm");
                
                // Fallback: rutas legacy si el proyecto es "default" o no existe
                var possiblePaths = new[]
                {
                    projectExcelPath, // ⭐ Ruta del proyecto activo (PRIORIDAD)
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExcelConfigs", "ProjectConfig.xlsm"),
                    @"ExcelConfigs\ProjectConfig.xlsm"
                };
                
                var excelPath = possiblePaths.FirstOrDefault(File.Exists);
                if (excelPath != null)
                {
                    _logger.LogInformation("📂 TwinCATService cargando configuración desde: {Path} (proyecto: {Project})", 
                        excelPath, projectContext.ActiveProjectId);
                    systemConfig = excelConfig.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
                }
                else
                {
                    _logger.LogWarning("⚠️ No se encontró ProjectConfig.xlsm en ninguna ubicación");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ No se pudo cargar configuración de Excel: {Error}", ex.Message);
            }
            
            // Usar valores de Excel si están disponibles, sino fallback a appsettings.json
            _config = new AdsConfiguration
            {
                NetId = systemConfig?.PlcAmsNetId ?? configuration["TwinCAT:NetId"] ?? "192.168.1.151.1.1",
                Port = systemConfig?.PlcAdsPort ?? int.Parse(configuration["TwinCAT:Port"] ?? "851"),
                Timeout = int.Parse(configuration["TwinCAT:Timeout"] ?? "5000")
            };
            
            // ⭐ IMPORTANTE: Leer UseSimulatedPlc desde Excel
            _forceSimulatedMode = systemConfig?.UseSimulatedPlc ?? false;
            _isSimulatedMode = _forceSimulatedMode; // Inicializar con el valor de Excel
            
            // 📊 Log detallado del valor leído
            _logger.LogInformation("📊 UseSimulatedPlc leído desde Excel: {Value} (systemConfig null: {IsNull})", 
                _forceSimulatedMode, systemConfig == null);
            
            if (_forceSimulatedMode)
            {
                _logger.LogWarning("🎮 TwinCATService en MODO SIMULADO (configurado en Excel: UseSimulatedPlc=TRUE)");
                _isConnected = true; // En modo simulado, siempre "conectado"
            }
            else
            {
                _logger.LogInformation("🔧 TwinCATService initialized - Target: {NetId}:{Port} - UseSimulatedPlc=FALSE", _config.NetId, _config.Port);
            }
            
            // Inicializar variables simuladas (fallback)
            InitializeSimulatedVariables();
        }
        
        private void InitializeSimulatedVariables()
        {
            // Variables de bombas - Estado: 0=Disabled, 1=Off, 2=On, 3=Alarm
            _simulatedVariables["MAIN.fbMachine.st_MainForm.i_StatePumps[1]"] = 1; // Bomba 1 Off
            _simulatedVariables["MAIN.fbMachine.st_MainForm.i_StatePumps[2]"] = 0; // Bomba 2 Disabled
            _simulatedVariables["MAIN.fbMachine.st_MainForm.i_StatePumps[3]"] = 3; // Bomba 3 Alarm
            
            // Otras variables estándar
            _simulatedVariables["MAIN.bStart"] = false;
            _simulatedVariables["MAIN.nCounter"] = 0;
            _simulatedVariables["MAIN.fTemperature"] = 25.5f;
            
            // ⚡ Variables del modo semiautomático (para desarrollo)
            _simulatedVariables["MAIN.fbMachine.st_SemiAutomatic.b_WpInSemiAutomatic"] = false;
            _simulatedVariables["MAIN.fbMachine.st_SemiAutomatic.b_StartSemiAutomatic_1"] = false;
            _simulatedVariables["MAIN.fbMachine.st_SemiAutomatic.b_StartSemiAutomatic_2"] = false;
            _simulatedVariables["GVL.bPump1"] = false;
            _simulatedVariables["GVL.bPump2"] = false;
            _simulatedVariables["GVL.bMotor1"] = false;
        }
        
        public async Task<bool> ConnectAsync()
        {
            try
            {
                // ⭐ Si está forzado modo simulado desde Excel, NO intentar conectar al PLC real
                if (_forceSimulatedMode)
                {
                    _logger.LogInformation("🎮 Modo SIMULADO forzado desde Excel (UseSimulatedPlc=TRUE) - NO se conectará al PLC real");
                    _isConnected = true;
                    _isSimulatedMode = true;
                    return true;
                }
                
                _logger.LogInformation("🔌 Attempting to connect to REAL TwinCAT PLC at {NetId}:{Port}", 
                    _config.NetId, _config.Port);
                
                try 
                {
                    // 🧹 Si había un AdsClient previo (p.ej. tras desconexión por timeout o port-not-found),
                    // descartarlo limpiamente. Reseteamos también el estado de notificaciones porque los
                    // handles ADS pertenecen a la sesión anterior y el handler de eventos quedaría
                    // suscrito a un cliente muerto → el nuevo cliente nunca dispararía OnAdsNotification
                    // y el reconocimiento automático de alarmas dejaría de funcionar hasta reiniciar.
                    if (_adsClient != null)
                    {
                        try
                        {
                            if (_notificationEventAttached)
                            {
                                _adsClient.AdsNotification -= OnAdsNotification;
                            }
                            _adsClient.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            _logger.LogDebug(disposeEx, "Ignorando error al disponer AdsClient previo durante reconexión");
                        }
                        _adsClient = null;
                    }
                    _notificationEventAttached = false;
                    _notificationRegistrations.Clear();
                    _lastNotifiedValues.Clear();

                    // ✅ API CORRECTO Beckhoff 6.x - Basado en ejemplos oficiales
                    _adsClient = new AdsClient();
                    
                    // Parse AmsNetId string to AmsNetId object
                    AmsNetId targetNetId = new AmsNetId(_config.NetId);
                    
                    // Conectar al PLC (esto solo abre el socket, NO verifica si el PLC responde)
                    _adsClient.Connect(targetNetId, _config.Port);
                    
                    // 🔴 IMPORTANTE: Verificar que el PLC está en RUN y puerto 851 abierto
                    try
                    {
                        // Paso 1: Verificar que TwinCAT responde
                        var deviceInfo = _adsClient.ReadDeviceInfo();
                        _logger.LogInformation("📡 TwinCAT responde - Device: {Name}, Version: {Version}.{Revision}.{Build}", 
                            deviceInfo.Name, deviceInfo.Version.Version, deviceInfo.Version.Revision, deviceInfo.Version.Build);
                        
                        // Paso 2: Verificar estado del PLC (DEBE estar en RUN)
                        var state = _adsClient.ReadState();
                        _logger.LogInformation("📊 PLC State: AdsState={AdsState}, DeviceState={DeviceState}", 
                            state.AdsState, state.DeviceState);
                        
                        // 🔴 SOLO conectado si está en RUN - Config/Stop/etc NO cuenta
                        if (state.AdsState != TwinCAT.Ads.AdsState.Run)
                        {
                            _logger.LogWarning("🔴 PLC NO está en RUN (State={State}) - Marcando como DESCONECTADO", state.AdsState);
                            _isConnected = false;
                            _isSimulatedMode = false;
                            
                            // 📋 L1 Audit Log: PLC Connect Failed - Not in RUN
                            await _auditLog.LogAsync(
                                AuditCategory.Plc,
                                AuditAction.PlcConnect,
                                AuditResult.Failure,
                                $"PLC at {_config.NetId}:{_config.Port} not in RUN state: {state.AdsState}");
                            
                            return false;
                        }
                        
                        _logger.LogInformation("✅ PLC en RUN - Conexión establecida");
                        // 🧹 Limpiar cache de handles: tras una reconexión los handles previos pertenecen
                        // a la sesión ADS anterior y son inválidos. Si no se limpian, el primer Write tras
                        // un Connect (típicamente el escribir st_InfoUserLogged en el primer login) falla
                        // y no se reintenta hasta el siguiente login.
                        ClearHandleCache();
                        _isConnected = true;
                        _isSimulatedMode = false;
                        _consecutiveTimeoutErrors = 0;
                        
                        // 📋 L1 Audit Log: PLC Connect Success
                        await _auditLog.LogAsync(
                            AuditCategory.Plc,
                            AuditAction.PlcConnect,
                            AuditResult.Success,
                            $"Connected to PLC at {_config.NetId}:{_config.Port} - Device: {deviceInfo.Name}");
                        
                        return true;
                    }
                    catch (TwinCAT.Ads.AdsErrorException verifyEx)
                    {
                        // El PLC no responde - NO está realmente conectado
                        _logger.LogWarning("🔴 PLC NO RESPONDE - Error {Code}: {Message}", 
                            (int)verifyEx.ErrorCode, verifyEx.Message);
                        
                        _isConnected = false;
                        _isSimulatedMode = false;
                        
                        // Cerrar el cliente ADS que no sirve
                        _adsClient?.Dispose();
                        _adsClient = null;
                        
                        // 📋 L1 Audit Log: PLC Connect Failed - No response
                        await _auditLog.LogAsync(
                            AuditCategory.Plc,
                            AuditAction.PlcConnect,
                            AuditResult.Failure,
                            $"PLC at {_config.NetId}:{_config.Port} not responding - Error: {verifyEx.ErrorCode}");
                        
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Cannot connect to REAL TwinCAT PLC at {NetId}:{Port}", 
                        _config.NetId, _config.Port);
                    
                    // ⛔ NO hacer fallback a simulado si UseSimulatedPlc=FALSE
                    // El usuario configuró explícitamente que quiere PLC real
                    _isConnected = false;
                    _isSimulatedMode = false;
                    _logger.LogError("⛔ UseSimulatedPlc=FALSE - NO se usará modo simulado. Verifique la conexión al PLC.");
                    
                    // 📋 L1 Audit Log: PLC Connect Failed - Exception
                    await _auditLog.LogAsync(
                        AuditCategory.Plc,
                        AuditAction.PlcConnect,
                        AuditResult.Error,
                        $"Cannot connect to PLC at {_config.NetId}:{_config.Port} - {ex.Message}");
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Critical error in ConnectAsync");
                _isConnected = false;
                return false;
            }
        }
        
        public async Task<bool> DisconnectAsync()
        {
            // ⚡ Limpiar cache de handles antes de desconectar
            ClearHandleCache();
            
            var wasConnected = _isConnected && !_isSimulatedMode;
            
            if (_adsClient != null)
            {
                _adsClient.Dispose();
                _adsClient = null;
                _logger.LogInformation("✅ Disconnected from REAL TwinCAT PLC");
            }
            
            _isConnected = false;
            _isSimulatedMode = false;
            _logger.LogInformation("✅ Disconnected from TwinCAT service");
            
            // 📋 L1 Audit Log: PLC Disconnect (solo si estaba realmente conectado)
            if (wasConnected)
            {
                await _auditLog.LogAsync(
                    AuditCategory.Plc,
                    AuditAction.PlcDisconnect,
                    AuditResult.Success,
                    $"Disconnected from PLC at {_config.NetId}:{_config.Port}");
            }
            
            return true;
        }

        /// <summary>
        /// Reconfigura la conexión TwinCAT: desconecta del PLC actual, actualiza AMS Net ID/Port, y reconecta.
        /// Se invoca cuando cambia el proyecto activo para conectar al PLC correcto.
        /// </summary>
        public async Task<bool> ReconfigureAsync(string newNetId, int newPort, bool useSimulatedPlc)
        {
            var previousNetId = _config.NetId;
            var previousPort = _config.Port;

            _logger.LogInformation("🔄 TwinCAT ReconfigureAsync: {OldNetId}:{OldPort} → {NewNetId}:{NewPort} (Simulated: {Simulated})",
                previousNetId, previousPort, newNetId, newPort, useSimulatedPlc);

            // 1. Desconectar del PLC actual (limpia handles, notifications, etc.)
            await UnregisterAllNotificationsAsync();
            await DisconnectAsync();

            // 2. Limpiar cache de variables fallidas (nuevo PLC puede tener variables diferentes)
            _failedVariables.Clear();
            _consecutiveTimeoutErrors = 0;

            // 3. Actualizar configuración
            _config.NetId = newNetId;
            _config.Port = newPort;

            // 4. Actualizar modo simulado
            _forceSimulatedMode = useSimulatedPlc;
            _isSimulatedMode = useSimulatedPlc;

            if (useSimulatedPlc)
            {
                _logger.LogWarning("🎮 TwinCAT reconfigurado en MODO SIMULADO (UseSimulatedPlc=TRUE)");
                _isConnected = true;

                await _auditLog.LogAsync(
                    AuditCategory.Plc,
                    AuditAction.PlcConnect,
                    AuditResult.Success,
                    $"TwinCAT reconfigured: {previousNetId}:{previousPort} → SIMULATED MODE");

                return true;
            }

            // 5. Reconectar al nuevo PLC
            _logger.LogInformation("🔌 Reconectando a nuevo PLC: {NetId}:{Port}", newNetId, newPort);
            var connected = await ConnectAsync();

            await _auditLog.LogAsync(
                AuditCategory.Plc,
                connected ? AuditAction.PlcConnect : AuditAction.PlcConnect,
                connected ? AuditResult.Success : AuditResult.Failure,
                $"TwinCAT reconfigured: {previousNetId}:{previousPort} → {newNetId}:{newPort} - {(connected ? "Connected" : "Failed")}");

            return connected;
        }
        
        public async Task<PlcDataSnapshot> ReadAllVariablesAsync(List<string> variableNames)
        {
            var snapshot = new PlcDataSnapshot
            {
                Timestamp = DateTime.Now,
                Variables = new Dictionary<string, object>()
            };

            foreach (var varName in variableNames)
            {
                try
                {
                    var value = await ReadVariableAsync(varName, typeof(int)); // Asumir int por defecto
                    if (value != null)
                    {
                        snapshot.Variables[varName] = value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading variable {VariableName}", varName);
                }
            }

            return snapshot;
        }
        
        /// <summary>
        /// 🔠 Resuelve el tipo CLR de una variable PLC a partir de su nombre (convención de prefijos).
        /// FUENTE ÚNICA DE VERDAD usada por el polling, el ForceRead y el endpoint de lectura puntual,
        /// para que TODOS lean el mismo número de bytes (LREAL=8, REAL=4, INT=2, BOOL=1…). Antes el
        /// endpoint puntual forzaba typeof(int) (2 bytes) y leía mal las LREAL de traslación: los
        /// elementos no se posicionaban en el estado inicial tras login/reinicio.
        /// </summary>
        public static Type ResolveDataTypeFromName(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return typeof(int);

            // Variables de alarma son BOOL (tanto st_alarmPc como st_alarmHistPc)
            if ((variableName.Contains("st_alarmPc[") || variableName.Contains("st_alarmHistPc[")) &&
                (variableName.EndsWith("].Alarm") ||
                 variableName.EndsWith("].Notification") ||
                 variableName.EndsWith("].Info")))
            {
                return typeof(bool);
            }
            // Variables LREAL (prefijo lr_) son double
            if (variableName.Contains(".lr_"))
                return typeof(double);
            // Variables REAL (prefijo r_) son float
            if (variableName.Contains(".r_") && !variableName.Contains(".lr_"))
                return typeof(float);
            // Variables booleanas (prefijo b_, bo_, x_)
            if (variableName.Contains(".b_") || variableName.Contains(".bo_") || variableName.Contains(".x_"))
                return typeof(bool);
            // Variables string (prefijo s_, str_) y WSTRING (prefijo ws_)
            if (variableName.Contains(".s_") || variableName.Contains(".str_") || variableName.Contains(".ws_"))
                return typeof(string);

            // Por defecto int (estados de bombas, posiciones DINT, contadores…)
            return typeof(int);
        }

        public async Task<object?> ReadVariableAsync(string variableName, Type dataType)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }
            
            // ⚠️ NOTA: Ya no silenciamos errores de variables fallidas
            // Cada intento de lectura reportará el error si la variable no existe
            
            // Si está en modo REAL (no simulado), intentar leer del PLC real
            if (!_isSimulatedMode && _adsClient != null)
            {
                try
                {
                    // ⚡ Usar handle cacheado para mejor rendimiento
                    uint handle = GetOrCreateHandle(variableName);
                    
                    object? result = null;
                    
                    // Leer según el tipo de dato
                    if (dataType == typeof(int))
                    {
                        // ✅ Leer INT de TwinCAT (16 bits = 2 bytes, signed)
                        byte[] buffer = new byte[2];  // INT = 16 bits (Int16)
                        int bytesRead = _adsClient.Read(handle, buffer.AsMemory());
                        
                        using var stream = new MemoryStream(buffer);
                        using var reader = new BinaryReader(stream);
                        result = (int)reader.ReadInt16();  // Leer como Int16 y convertir a int
                        
                        _logger.LogDebug("📖 Read from REAL PLC: {Var} = {Value} (INT/Int16)", variableName, result);
                    }
                    else if (dataType == typeof(short))
                    {
                        // ✅ Leer INT de TwinCAT como short (16 bits = 2 bytes, signed)
                        byte[] buffer = new byte[2];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = BitConverter.ToInt16(buffer, 0);
                    }
                    else if (dataType == typeof(ushort))
                    {
                        // ✅ Leer UINT de TwinCAT (16 bits = 2 bytes, unsigned)
                        byte[] buffer = new byte[2];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = BitConverter.ToUInt16(buffer, 0);
                    }
                    else if (dataType == typeof(uint))
                    {
                        // ✅ Leer UDINT/DWORD de TwinCAT (32 bits = 4 bytes, unsigned)
                        byte[] buffer = new byte[4];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = BitConverter.ToUInt32(buffer, 0);
                        _logger.LogDebug("📖 Read UDINT from REAL PLC: {Var} = {Value}", variableName, result);
                    }
                    else if (dataType == typeof(long))
                    {
                        // ✅ Leer LINT de TwinCAT (64 bits = 8 bytes, signed)
                        byte[] buffer = new byte[8];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = BitConverter.ToInt64(buffer, 0);
                    }
                    else if (dataType == typeof(ulong))
                    {
                        // ✅ Leer ULINT/LWORD de TwinCAT (64 bits = 8 bytes, unsigned)
                        byte[] buffer = new byte[8];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = BitConverter.ToUInt64(buffer, 0);
                    }
                    else if (dataType == typeof(byte))
                    {
                        // ✅ Leer BYTE/USINT de TwinCAT (8 bits = 1 byte, unsigned)
                        byte[] buffer = new byte[1];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = buffer[0];
                    }
                    else if (dataType == typeof(sbyte))
                    {
                        // ✅ Leer SINT de TwinCAT (8 bits = 1 byte, signed)
                        byte[] buffer = new byte[1];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = unchecked((sbyte)buffer[0]);
                        _logger.LogDebug("📖 Read from REAL PLC: {Var} = {Value} (SINT)", variableName, result);
                    }
                    else if (dataType == typeof(bool))
                    {
                        byte[] buffer = new byte[1];
                        _adsClient.Read(handle, buffer.AsMemory());
                        result = buffer[0] != 0;
                    }
                    else if (dataType == typeof(float))
                    {
                        byte[] buffer = new byte[4];
                        _adsClient.Read(handle, buffer.AsMemory());
                        
                        using var stream = new MemoryStream(buffer);
                        using var reader = new BinaryReader(stream);
                        result = reader.ReadSingle();
                    }
                    else if (dataType == typeof(double))
                    {
                        byte[] buffer = new byte[8];
                        _adsClient.Read(handle, buffer.AsMemory());
                        
                        using var stream = new MemoryStream(buffer);
                        using var reader = new BinaryReader(stream);
                        result = reader.ReadDouble();
                    }
                    else if (dataType == typeof(string))
                    {
                        // WSTRING en TwinCAT: 162 bytes por defecto (80 chars * 2 bytes + 2 bytes terminador)
                        // WSTRING usa Unicode UTF-16 Little Endian
                        byte[] buffer = new byte[162];
                        _adsClient.Read(handle, buffer.AsMemory());
                        
                        // Decodificar UTF-16 LE y buscar terminador null (2 bytes: 0x00 0x00)
                        string fullString = System.Text.Encoding.Unicode.GetString(buffer);
                        int nullIndex = fullString.IndexOf('\0');
                        result = nullIndex >= 0 ? fullString.Substring(0, nullIndex) : fullString.TrimEnd();
                        
                        _logger.LogDebug("📖 Read WSTRING from REAL PLC: {Var} = {Value}", variableName, result);
                    }
                    
                    // ✅ Lectura exitosa - resetear contador de errores de timeout
                    if (_consecutiveTimeoutErrors > 0)
                    {
                        _consecutiveTimeoutErrors = 0;
                        if (!_isConnected)
                        {
                            _logger.LogInformation("🟢 PLC RECONECTADO - Lectura exitosa después de errores de timeout");
                            _isConnected = true;
                        }
                    }

                    if (result == null)
                    {
                        _logger.LogWarning("⚠️ ReadVariableAsync({Var}): tipo CLR no soportado '{Type}'. Devolviendo null. Añadir rama en TwinCATService.ReadVariableAsync.",
                            variableName, dataType.Name);
                    }

                    return result;
                }
                catch (TwinCAT.Ads.AdsErrorException ex) when ((int)ex.ErrorCode == 1808)
                {
                    // Variable no existe en PLC - Código 1808 = ADS_E_SYMBOLNOTFOUND
                    // Quitar del cache por si acaso
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogError("❌ Variable NO EXISTE en PLC: {Var}", variableName);
                    throw new InvalidOperationException($"Variable '{variableName}' no existe en el PLC. Verifique que el programa PLC esté cargado y la variable exista.", ex);
                }
                catch (TwinCAT.Ads.AdsErrorException ex) when ((int)ex.ErrorCode == 6)
                {
                    // 🔴 ERROR 6 = Target port could not be found - El PLC/TwinCAT no está corriendo
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogWarning("🔴 PLC DESCONECTADO - Target port not found (PLC apagado o TwinCAT no corriendo)");
                    _isConnected = false;
                    
                    throw new InvalidOperationException($"PLC desconectado (port not found): {ex.Message}", ex);
                }
                catch (TwinCAT.Ads.AdsErrorException ex) when ((int)ex.ErrorCode == 1861)
                {
                    // 🔴 ERROR 1861 = ClientSyncTimeOut - El PLC no responde = DESCONECTADO
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogWarning("🔴 PLC DESCONECTADO - Timeout al leer {Var}", variableName);
                    _isConnected = false;
                    _consecutiveTimeoutErrors++;
                    
                    throw new InvalidOperationException($"PLC desconectado (timeout): {ex.Message}", ex);
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    // Quitar del cache en caso de error
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogError("❌ ADS Error en {Var}: Code={ErrorCode} ({ErrorName})", 
                        variableName, (int)ex.ErrorCode, ex.ErrorCode.ToString());
                    throw new InvalidOperationException($"Error ADS al leer '{variableName}': {ex.ErrorCode} - {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogError(ex, "❌ Error leyendo {Var} del PLC", variableName);
                    throw new InvalidOperationException($"Error al leer '{variableName}' del PLC: {ex.Message}", ex);
                }
            }
            
            // ❌ NO hay modo simulado para lectura de variables críticas
            // Si llegamos aquí, el PLC no está conectado y no hay datos reales
            _logger.LogError("❌ No se puede leer '{Var}' - PLC no conectado y no hay modo simulado", variableName);
            throw new InvalidOperationException($"No se puede leer '{variableName}': El PLC no está conectado. No se generan datos simulados.");
        }
        
        /// <summary>
        /// Genera valores simulados automáticamente basándose en el nombre de la variable
        /// </summary>
        private object GenerateSimulatedValue(string variableName, Type dataType)
        {
            var lowerName = variableName.ToLower();
            
            // Detectar tipo de variable por nombre y generar valor apropiado
            if (lowerName.Contains("state") || lowerName.Contains("status"))
            {
                // Estados: 0=Disabled, 1=Off, 2=On, 3=Alarm - rotar entre valores
                return _random.Next(0, 3);
            }
            else if (lowerName.Contains("position"))
            {
                // Posiciones: valor entre 0 y 1000
                return (float)(_random.NextDouble() * 1000);
            }
            else if (lowerName.Contains("temperature") || lowerName.Contains("temp"))
            {
                // Temperatura: entre 15 y 35 grados
                return (float)(15 + _random.NextDouble() * 20);
            }
            else if (lowerName.Contains("pressure"))
            {
                // Presión: entre 0 y 10 bar
                return (float)(_random.NextDouble() * 10);
            }
            else if (lowerName.Contains("counter") || lowerName.Contains("count"))
            {
                return _random.Next(0, 1000);
            }
            else if (lowerName.Contains("alarm") || lowerName.Contains("error"))
            {
                return false; // Sin alarmas por defecto
            }
            else if (dataType == typeof(bool))
            {
                return _random.Next(2) == 1;
            }
            else if (dataType == typeof(int) || dataType == typeof(short))
            {
                return _random.Next(0, 100);
            }
            else if (dataType == typeof(float) || dataType == typeof(double))
            {
                return (float)(_random.NextDouble() * 100);
            }
            else if (dataType == typeof(string))
            {
                // Generar nombre de receta simulado
                if (lowerName.Contains("recipe") || lowerName.Contains("receta") || lowerName.Contains("name") || lowerName.Contains("nombre"))
                {
                    string[] recipeNames = { "Lavado Normal", "Lavado Intensivo", "Lavado Eco", "Lavado Rapido", "Pre-Lavado" };
                    return recipeNames[_random.Next(recipeNames.Length)];
                }
                return $"SimValue_{_random.Next(1000)}";
            }
            
            // Default: entero entre 0 y 10
            return _random.Next(0, 10);
        }
        
        /// <summary>
        /// Obtiene o crea un handle ADS cacheado para una variable
        /// </summary>
        private uint GetOrCreateHandle(string variableName)
        {
            if (_adsClient == null)
                throw new InvalidOperationException("ADS client not initialized");
                
            return _handleCache.GetOrAdd(variableName, name => 
            {
                var handle = _adsClient.CreateVariableHandle(name);
                _logger.LogDebug("🔧 Created handle for {Var}: {Handle}", name, handle);
                return handle;
            });
        }
        
        /// <summary>
        /// Invalida el cache de handles (llamar al desconectar)
        /// </summary>
        private void ClearHandleCache()
        {
            foreach (var handle in _handleCache.Values)
            {
                try
                {
                    _adsClient?.DeleteVariableHandle(handle);
                }
                catch { /* Ignorar errores al limpiar */ }
            }
            _handleCache.Clear();
            _logger.LogDebug("🧹 Handle cache cleared");
        }
        
        public async Task<bool> WriteVariableAsync(string variableName, object value, Type dataType)
        {
            _logger.LogInformation("🔘 WriteVariableAsync: Variable={Var}, Value={Value}, DataType={Type}, IsSimulated={Sim}, IsConnected={Conn}", 
                variableName, value, dataType.Name, _isSimulatedMode, IsConnected);
            
            if (!IsConnected)
            {
                _logger.LogError("❌ WriteVariableAsync: Not connected to PLC");
                throw new InvalidOperationException("Not connected to PLC");
            }
            
            // Si está en modo REAL (no simulado), escribir al PLC real
            if (!_isSimulatedMode && _adsClient != null)
            {
                try
                {
                    // ⚡ Usar handle cacheado para mejor rendimiento
                    uint handle = GetOrCreateHandle(variableName);
                    _logger.LogInformation("🔘 WriteVariableAsync: Got handle={Handle} for {Var}", handle, variableName);
                    
                    byte[] buffer;
                    
                    if (dataType == typeof(int))
                    {
                        // ✅ Escribir INT de TwinCAT (16 bits = 2 bytes, signed)
                        buffer = new byte[2];  // INT = 16 bits (Int16)
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        // Convertir de forma segura a Int16
                        short shortValue = Convert.ToInt16(value);
                        writer.Write(shortValue);
                        
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote INT to PLC: {Var} = {Value} (as Int16, 2 bytes)", variableName, shortValue);
                    }
                    else if (dataType == typeof(short))
                    {
                        // ✅ Escribir INT de TwinCAT (16 bits = 2 bytes, signed) - alias de int
                        buffer = new byte[2];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToInt16(value));
                        
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote INT (short) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(sbyte))
                    {
                        // ✅ Escribir SINT de TwinCAT (8 bits = 1 byte, signed)
                        buffer = new byte[1];
                        buffer[0] = unchecked((byte)Convert.ToSByte(value));
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote SINT to PLC: {Var} = {Value} (as SINT, 1 byte)", variableName, value);
                    }
                    else if (dataType == typeof(bool))
                    {
                        buffer = new byte[1];
                        buffer[0] = Convert.ToBoolean(value) ? (byte)1 : (byte)0;
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote BOOL to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(ushort))
                    {
                        // ✅ UINT de TwinCAT (16 bits = 2 bytes, unsigned)
                        buffer = new byte[2];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToUInt16(value));
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote UINT (ushort) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(uint))
                    {
                        // ✅ UDINT de TwinCAT (32 bits = 4 bytes, unsigned)
                        buffer = new byte[4];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToUInt32(value));
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote UDINT (uint) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(long))
                    {
                        // ✅ LINT de TwinCAT (64 bits = 8 bytes, signed)
                        buffer = new byte[8];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToInt64(value));
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote LINT (long) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(float))
                    {
                        buffer = new byte[4];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToSingle(value));
                        
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote REAL (float) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(double))
                    {
                        // ✅ LREAL de TwinCAT (64 bits = 8 bytes)
                        buffer = new byte[8];
                        using var stream = new MemoryStream(buffer);
                        using var writer = new BinaryWriter(stream);
                        writer.Write(Convert.ToDouble(value));
                        
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogDebug("✍️ Wrote LREAL (double) to PLC: {Var} = {Value}", variableName, value);
                    }
                    else if (dataType == typeof(string))
                    {
                        string strValue = value?.ToString() ?? string.Empty;
                        
                        // Obtener información del símbolo para saber el tamaño real
                        var symbolInfo = _adsClient.ReadSymbol(variableName);
                        int symbolSize = symbolInfo.Size;
                        
                        _logger.LogDebug("📏 Symbol {Var}: Size={Size}, DataType={Type}", 
                            variableName, symbolSize, symbolInfo.TypeName);
                        
                        // Detectar si es STRING (ASCII) o WSTRING (Unicode) basado en el tipo
                        bool isWString = symbolInfo.TypeName?.StartsWith("WSTRING", StringComparison.OrdinalIgnoreCase) == true;
                        
                        if (isWString)
                        {
                            // WSTRING: UTF-16 Little Endian (2 bytes por carácter + 2 bytes terminador)
                            int maxChars = (symbolSize - 2) / 2;
                            if (strValue.Length > maxChars)
                                strValue = strValue.Substring(0, maxChars);
                            
                            buffer = new byte[symbolSize];
                            byte[] strBytes = System.Text.Encoding.Unicode.GetBytes(strValue);
                            Array.Copy(strBytes, buffer, Math.Min(strBytes.Length, symbolSize - 2));
                            
                            _logger.LogDebug("✍️ Writing WSTRING to PLC: {Var} = \"{Value}\" (size={Size})", 
                                variableName, strValue, symbolSize);
                        }
                        else
                        {
                            // STRING: ASCII/Latin-1 (1 byte por carácter + 1 byte terminador)
                            int maxChars = symbolSize - 1;
                            if (strValue.Length > maxChars)
                                strValue = strValue.Substring(0, maxChars);
                            
                            buffer = new byte[symbolSize];
                            byte[] strBytes = System.Text.Encoding.Latin1.GetBytes(strValue);
                            Array.Copy(strBytes, buffer, Math.Min(strBytes.Length, symbolSize - 1));
                            
                            _logger.LogDebug("✍️ Writing STRING to PLC: {Var} = \"{Value}\" (size={Size})", 
                                variableName, strValue, symbolSize);
                        }
                        
                        _adsClient.Write(handle, buffer.AsMemory());
                        _logger.LogInformation("✍️ Wrote string to PLC: {Var} = \"{Value}\"", variableName, strValue);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Unsupported data type {Type} for variable {Var}", dataType.Name, variableName);
                        return false;
                    }
                    
                    return true;
                }
                catch (AdsErrorException ex) when (ex.ErrorCode == AdsErrorCode.ClientSyncTimeOut)
                {
                    // Si hay timeout, el handle puede estar inválido - quitarlo del cache
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogError(ex, "❌ Timeout writing variable {Var} - handle removed from cache", variableName);
                    return false;
                }
                catch (Exception ex)
                {
                    // Si hay otro error, también quitar del cache por si el handle es inválido
                    _handleCache.TryRemove(variableName, out _);
                    _logger.LogError(ex, "❌ Error writing variable {Var} (type: {Type}) to PLC", variableName, dataType.Name);
                    return false;
                }
            }
            
            // Modo SIMULADO (fallback)
            _simulatedVariables[variableName] = value;
            _logger.LogDebug("✍️ Wrote to SIMULATED PLC: {Var} = {Value}", variableName, value);
            return await Task.FromResult(true);
        }
        
        public async Task<PlcState> GetPlcStateAsync()
        {
            if (!IsConnected)
            {
                return PlcState.Invalid;
            }
            
            if (!_isSimulatedMode && _adsClient != null)
            {
                try
                {
                    // ✅ API CORRECTO Beckhoff 6.x - ReadState
                    var stateInfo = _adsClient.ReadState();
                    
                    // Mapear el AdsState al enum PlcState
                    return stateInfo.AdsState == TwinCAT.Ads.AdsState.Run ? PlcState.Run : PlcState.Stop;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading PLC state");
                    return PlcState.Invalid;
                }
            }
            
            // Simulated mode
            return await Task.FromResult(PlcState.Run);
        }
        
        public void Dispose()
        {
            // 🔔 Unregister all notifications before disposing
            UnregisterAllNotificationsAsync().GetAwaiter().GetResult();
            _adsClient?.Dispose();
        }
        
        #region ADS Notifications Implementation
        
        /// <summary>
        /// Internal class to track notification registrations
        /// </summary>
        private class NotificationRegistration
        {
            public uint NotificationHandle { get; set; }
            public uint VariableHandle { get; set; }
            public string VariableName { get; set; } = string.Empty;
            public Type DataType { get; set; } = typeof(bool);
            public DateTime RegisteredAt { get; set; } = DateTime.Now;
        }
        
        /// <summary>
        /// Register a single variable for ADS notifications (push on change).
        /// </summary>
        public async Task<uint> RegisterNotificationAsync(string variableName, Type dataType, int cycleTimeMs = 100)
        {
            if (!IsConnected || _adsClient == null)
            {
                _logger.LogWarning("🔔 Cannot register notification - PLC not connected: {Var}", variableName);
                return 0;
            }
            
            if (_isSimulatedMode)
            {
                // En modo simulado, simular el registro pero no hacer nada real
                var fakeHandle = (uint)(variableName.GetHashCode() & 0x7FFFFFFF);
                _logger.LogDebug("🔔 [SIMULATED] Notification registered: {Var} → Handle {Handle}", variableName, fakeHandle);
                return fakeHandle;
            }
            
            try
            {
                // Get or create variable handle
                var varHandle = GetOrCreateHandle(variableName);
                
                // Determine data size based on type
                int dataSize = GetDataSize(dataType);
                
                // Configure notification settings
                // TransMode.OnChange = notify only when value changes
                // cycleTimeMs = minimum time between notifications (in 100ns units for ADS)
                var notificationSettings = new NotificationSettings(
                    AdsTransMode.OnChange,
                    cycleTimeMs,      // Cycle time in ms
                    0                 // Max delay (0 = immediate)
                );
                
                // Register the notification
                var notifHandle = _adsClient.AddDeviceNotification(
                    variableName,
                    dataSize,
                    notificationSettings,
                    null  // User data (not needed, we track by handle)
                );
                
                // Store registration info
                _notificationRegistrations[notifHandle] = new NotificationRegistration
                {
                    NotificationHandle = notifHandle,
                    VariableHandle = varHandle,
                    VariableName = variableName,
                    DataType = dataType
                };
                
                _logger.LogInformation("🔔 ADS Notification registered: {Var} → Handle {Handle} (cycle: {Cycle}ms)", 
                    variableName, notifHandle, cycleTimeMs);
                
                return await Task.FromResult(notifHandle);
            }
            catch (AdsErrorException ex)
            {
                _logger.LogError(ex, "❌ Failed to register notification for {Var}: ADS Error {Code}", 
                    variableName, ex.ErrorCode);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to register notification for {Var}", variableName);
                return 0;
            }
        }
        
        /// <summary>
        /// Register multiple variables for ADS notifications in batch.
        /// </summary>
        public async Task<Dictionary<string, uint>> RegisterMultipleNotificationsAsync(
            IEnumerable<string> variableNames, Type dataType, int cycleTimeMs = 100)
        {
            var results = new Dictionary<string, uint>();
            var variableList = variableNames.ToList();
            
            _logger.LogInformation("🔔 Registering {Count} ADS notifications (type: {Type}, cycle: {Cycle}ms)...", 
                variableList.Count, dataType.Name, cycleTimeMs);
            
            // Setup notification event handler ONCE if not already done
            if (_adsClient != null && !_notificationEventAttached)
            {
                _adsClient.AdsNotification += OnAdsNotification;
                _notificationEventAttached = true;
                _logger.LogInformation("🔔✅ ADS Notification event handler ATTACHED to AdsClient");
            }
            
            int successCount = 0;
            int failCount = 0;
            
            foreach (var varName in variableList)
            {
                var handle = await RegisterNotificationAsync(varName, dataType, cycleTimeMs);
                results[varName] = handle;
                
                if (handle > 0)
                    successCount++;
                else
                    failCount++;
            }
            
            // 🔍 DEBUG: Listar handles de st_alarmHistPc
            var histHandles = results.Where(r => r.Key.Contains("st_alarmHistPc") && r.Value > 0)
                                     .OrderBy(r => r.Key)
                                     .Take(10)
                                     .Select(r => $"{r.Key.Split('[')[1].Split(']')[0]}→{r.Value}");
            _logger.LogInformation("🔔🔍 st_alarmHistPc handles (primeros 10): {Handles}", 
                string.Join(", ", histHandles));
            
            var histTotal = results.Count(r => r.Key.Contains("st_alarmHistPc") && r.Value > 0);
            _logger.LogInformation("🔔🔍 Total st_alarmHistPc registradas: {Count}", histTotal);
            
            _logger.LogInformation("🔔 Notification registration complete: {Success} OK, {Failed} failed (total active: {Total})", 
                successCount, failCount, _notificationRegistrations.Count);
            
            return results;
        }
        
        private bool _notificationEventAttached = false;
        
        // 🔍 DEBUG: Contador para diagnóstico
        private int _totalNotificationsReceived = 0;
        
        /// <summary>
        /// Event handler for ADS notifications - called by TwinCAT when a value changes
        /// </summary>
        private void OnAdsNotification(object? sender, AdsNotificationEventArgs e)
        {
            try
            {
                _totalNotificationsReceived++;
                
                // 🔍 DEBUG: Log TODAS las notificaciones cada 100
                if (_totalNotificationsReceived % 100 == 0)
                {
                    _logger.LogInformation("🔔📊 Total notifications received so far: {Count}", _totalNotificationsReceived);
                }
                
                _logger.LogInformation("🔔📥 ADS Notification RECEIVED! Handle: {Handle}, DataLength: {Len}", 
                    e.Handle, e.Data.Length);
                
                // Find the registration for this notification
                if (!_notificationRegistrations.TryGetValue(e.Handle, out var registration))
                {
                    _logger.LogDebug("🔔 Received notification for unknown handle: {Handle} (stale/residual)", e.Handle);
                    return;
                }
                
                // 🔍 DEBUG: Log específico para st_alarmHistPc
                if (registration.VariableName.Contains("st_alarmHistPc"))
                {
                    _logger.LogInformation("🔔🔍 HIST ALARM notification: {Var}, Handle: {Handle}", 
                        registration.VariableName, e.Handle);
                }
                
                // Read the new value from the notification data
                object? newValue = ReadValueFromNotification(e.Data, registration.DataType);
                
                _logger.LogInformation("🔔📥 Notification parsed: {Var} = {Value}", 
                    registration.VariableName, newValue ?? "null");
                
                // Get old value (if any)
                _lastNotifiedValues.TryGetValue(registration.VariableName, out var oldValue);
                
                // Update last known value
                _lastNotifiedValues[registration.VariableName] = newValue;
                
                // Only fire event if value actually changed
                if (!Equals(oldValue, newValue))
                {
                    _logger.LogInformation("🔔🔄 Notification CHANGED: {Var} = {OldValue} → {NewValue}", 
                        registration.VariableName, oldValue ?? "null", newValue ?? "null");
                    
                    // Fire the OnVariableChanged event
                    var hasSubscribers = OnVariableChanged != null;
                    _logger.LogInformation("🔔📤 Firing OnVariableChanged event (subscribers: {HasSub})", hasSubscribers);
                    
                    OnVariableChanged?.Invoke(this, new PlcNotification
                    {
                        VariableName = registration.VariableName,
                        OldValue = oldValue,
                        NewValue = newValue,
                        Timestamp = DateTime.Now,
                        NotificationHandle = e.Handle
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing ADS notification for handle {Handle}", e.Handle);
            }
        }
        
        /// <summary>
        /// Read value from notification data buffer based on data type
        /// </summary>
        private object? ReadValueFromNotification(ReadOnlyMemory<byte> data, Type dataType)
        {
            var span = data.Span;
            
            if (dataType == typeof(bool))
            {
                return span.Length > 0 && span[0] != 0;
            }
            else if (dataType == typeof(int) || dataType == typeof(Int16))
            {
                if (span.Length >= 2)
                    return BitConverter.ToInt16(span);
                return 0;
            }
            else if (dataType == typeof(Int32))
            {
                if (span.Length >= 4)
                    return BitConverter.ToInt32(span);
                return 0;
            }
            else if (dataType == typeof(float))
            {
                if (span.Length >= 4)
                    return BitConverter.ToSingle(span);
                return 0f;
            }
            else if (dataType == typeof(double))
            {
                if (span.Length >= 8)
                    return BitConverter.ToDouble(span);
                return 0.0;
            }
            else if (dataType == typeof(uint) || dataType == typeof(UInt32))
            {
                if (span.Length >= 4)
                    return BitConverter.ToUInt32(span);
                return 0u;
            }
            else if (dataType == typeof(UInt16))
            {
                if (span.Length >= 2)
                    return BitConverter.ToUInt16(span);
                return (ushort)0;
            }
            else if (dataType == typeof(string))
            {
                // WSTRING en TwinCAT: Unicode (2 bytes por caracter), terminado en null
                try
                {
                    // Buscar el terminador null (2 bytes 0x00 0x00)
                    int length = 0;
                    for (int i = 0; i < span.Length - 1; i += 2)
                    {
                        if (span[i] == 0 && span[i + 1] == 0)
                            break;
                        length += 2;
                    }
                    
                    if (length > 0)
                    {
                        var stringBytes = span.Slice(0, length).ToArray();
                        return System.Text.Encoding.Unicode.GetString(stringBytes);
                    }
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔔 Error parsing WSTRING from notification");
                    return null;
                }
            }
            
            _logger.LogWarning("🔔 Unknown data type for notification: {Type}", dataType.Name);
            return null;
        }
        
        /// <summary>
        /// Get data size in bytes for a given type
        /// </summary>
        private int GetDataSize(Type dataType)
        {
            if (dataType == typeof(bool)) return 1;
            if (dataType == typeof(byte) || dataType == typeof(sbyte)) return 1;
            if (dataType == typeof(Int16) || dataType == typeof(UInt16)) return 2;
            if (dataType == typeof(int) || dataType == typeof(Int32) || dataType == typeof(UInt32)) return 4;
            if (dataType == typeof(float)) return 4;
            if (dataType == typeof(double) || dataType == typeof(Int64) || dataType == typeof(UInt64)) return 8;
            if (dataType == typeof(string)) return 512; // WSTRING(255) = 255*2 bytes + algunos extras
            
            // Default for unknown types
            return 4;
        }
        
        /// <summary>
        /// Unregister a notification by handle.
        /// </summary>
        public async Task<bool> UnregisterNotificationAsync(uint notificationHandle)
        {
            if (_adsClient == null || notificationHandle == 0)
                return false;
            
            try
            {
                if (_notificationRegistrations.TryRemove(notificationHandle, out var registration))
                {
                    if (!_isSimulatedMode)
                    {
                        _adsClient.DeleteDeviceNotification(notificationHandle);
                    }
                    
                    _logger.LogDebug("🔔 Notification unregistered: {Var} (handle: {Handle})", 
                        registration.VariableName, notificationHandle);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error unregistering notification {Handle}", notificationHandle);
                return await Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Unregister all active notifications.
        /// </summary>
        public async Task UnregisterAllNotificationsAsync()
        {
            var count = _notificationRegistrations.Count;
            if (count == 0)
                return;
            
            _logger.LogInformation("🔔 Unregistering all {Count} notifications...", count);
            
            foreach (var handle in _notificationRegistrations.Keys.ToList())
            {
                await UnregisterNotificationAsync(handle);
            }
            
            _notificationRegistrations.Clear();
            _lastNotifiedValues.Clear();
            
            _logger.LogInformation("🔔 All notifications unregistered");
        }
        
        #endregion
    }
}
