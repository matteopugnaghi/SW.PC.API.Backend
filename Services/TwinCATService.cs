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
    }
    
    public class TwinCATService : ITwinCATService, IDisposable
    {
        private readonly ILogger<TwinCATService> _logger;
        private readonly AdsConfiguration _config;
        private AdsClient? _adsClient;  // ✅ CLASE CORRECTA de Beckhoff 6.x
        private bool _isConnected;
        private bool _isSimulatedMode = false;  // ⚡ Por defecto FALSE - simulación es opcional
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _simulatedVariables = new();
        private readonly Random _random = new();
        
        // 🔴 Cache de variables que fallan - evitar reintentar constantemente
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _failedVariables = new();
        private readonly TimeSpan _failedVariableRetryInterval = TimeSpan.FromMinutes(1); // Reintentar cada minuto
        
        public event EventHandler<PlcNotification>? OnVariableChanged;
        
        public bool IsConnected => _isConnected;
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
                    
                    // Debug: mostrar todos los campos disponibles
                    _logger.LogInformation("🔍 DeviceInfo.Name: {Name}", deviceInfo.Name);
                    _logger.LogInformation("🔍 DeviceInfo.Version.Version (Major): {Major}", deviceInfo.Version.Version);
                    _logger.LogInformation("🔍 DeviceInfo.Version.Revision (Minor): {Minor}", deviceInfo.Version.Revision);
                    _logger.LogInformation("🔍 DeviceInfo.Version.Build: {Build}", deviceInfo.Version.Build);
                    
                    // Formato: "TwinCAT 3.1 Build 4024" o similar
                    // Version=Major, Revision=Minor, Build=Build number
                    info.RuntimeVersion = $"TwinCAT {deviceInfo.Version.Version}.{deviceInfo.Version.Revision} Build {deviceInfo.Version.Build}";
                    info.MajorVersion = deviceInfo.Version.Version;
                    info.MinorVersion = deviceInfo.Version.Revision;
                    info.BuildNumber = deviceInfo.Version.Build;
                    info.DeviceName = deviceInfo.Name;
                    
                    // Añadir Task Cycle Time si está disponible
                    info.TaskCycleTimeMs = _cachedTaskCycleTimeMs;
                    
                    _logger.LogInformation("🔧 TwinCAT Runtime: {Version} ({Name})", info.RuntimeVersion, deviceInfo.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read TwinCAT device info");
                    info.RuntimeVersion = "TwinCAT 3.x (version unknown)";
                }
            }
            else
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

            return info;
        }
        
        /// <summary>
        /// 🕐 Obtener el Task Cycle Time real del PLC TwinCAT
        /// Lee la variable de sistema TwinCAT que contiene el cycle time configurado
        /// </summary>
        public async Task<double> GetTaskCycleTimeAsync()
        {
            // Cache de 5 segundos - el cycle time no cambia frecuentemente
            if ((DateTime.UtcNow - _lastTaskCycleTimeUpdate).TotalSeconds < 5 && _cachedTaskCycleTimeMs > 0)
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
                _lastTaskCycleTimeUpdate = DateTime.UtcNow;
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
                        _lastTaskCycleTimeUpdate = DateTime.UtcNow;
                        
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
                        _lastTaskCycleTimeUpdate = DateTime.UtcNow;
                        
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
                _lastTaskCycleTimeUpdate = DateTime.UtcNow;
                
                return _cachedTaskCycleTimeMs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading TwinCAT Task Cycle Time");
                return 0;
            }
        }
        
        private readonly bool _forceSimulatedMode = false; // Forzar modo simulado desde Excel
        
        public TwinCATService(IConfiguration configuration, ILogger<TwinCATService> logger, IExcelConfigService excelConfig, IProjectContextService projectContext)
        {
            _logger = logger;
            
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
                    // ✅ API CORRECTO Beckhoff 6.x - Basado en ejemplos oficiales
                    _adsClient = new AdsClient();
                    
                    // Parse AmsNetId string to AmsNetId object
                    AmsNetId targetNetId = new AmsNetId(_config.NetId);
                    
                    // Conectar al PLC
                    _adsClient.Connect(targetNetId, _config.Port);
                    
                    _isConnected = true;
                    _isSimulatedMode = false;
                    
                    _logger.LogInformation("✅ Successfully connected to REAL TwinCAT PLC at {NetId}:{Port}", 
                        _config.NetId, _config.Port);
                    
                    return true;
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
            if (_adsClient != null)
            {
                _adsClient.Dispose();
                _adsClient = null;
                _logger.LogInformation("✅ Disconnected from REAL TwinCAT PLC");
            }
            
            _isConnected = false;
            _isSimulatedMode = false;
            _logger.LogInformation("✅ Disconnected from TwinCAT service");
            return await Task.FromResult(true);
        }
        
        public async Task<PlcDataSnapshot> ReadAllVariablesAsync(List<string> variableNames)
        {
            var snapshot = new PlcDataSnapshot
            {
                Timestamp = DateTime.UtcNow,
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
        
        public async Task<object?> ReadVariableAsync(string variableName, Type dataType)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }
            
            // 🔴 Si esta variable falló recientemente, saltar para evitar spam de errores
            if (_failedVariables.TryGetValue(variableName, out var failedAt))
            {
                if (DateTime.UtcNow - failedAt < _failedVariableRetryInterval)
                {
                    // Devolver null silenciosamente - la variable no existe o falla
                    return null;
                }
                // Ya pasó el tiempo de espera, quitar del cache y reintentar
                _failedVariables.TryRemove(variableName, out _);
            }
            
            // Si está en modo REAL (no simulado), intentar leer del PLC real
            if (!_isSimulatedMode && _adsClient != null)
            {
                try
                {
                    // ✅ API CORRECTO Beckhoff 6.x - Basado en Form1.cs ejemplo oficial
                    // Paso 1: Crear handle a la variable
                    uint handle = _adsClient.CreateVariableHandle(variableName);
                    
                    try
                    {
                        object? result = null;
                        
                        // Paso 2: Leer según el tipo de dato
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
                        
                        return result;
                    }
                    finally
                    {
                        // Paso 3: Liberar handle
                        _adsClient.DeleteVariableHandle(handle);
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex) when ((int)ex.ErrorCode == 1808)
                {
                    // Variable no existe en PLC - Código 1808 = ADS_E_SYMBOLNOTFOUND
                    // Agregar al cache de variables fallidas para no reintentar
                    _failedVariables[variableName] = DateTime.UtcNow;
                    
                    // Log solo una vez (se silenciará por 1 minuto)
                    _logger.LogWarning("⚠️ Variable NO EXISTE en PLC: {Var} - Silenciando por 1 minuto", variableName);
                    return null;
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    // Agregar al cache de variables fallidas
                    _failedVariables[variableName] = DateTime.UtcNow;
                    
                    // Solo loguear UNA VEZ por variable diferente (para no spamear)
                    _logger.LogWarning("⚠️ ADS Error en {Var}: Code={ErrorCode} ({ErrorName}) - Silenciando por 1 minuto", 
                        variableName, (int)ex.ErrorCode, ex.ErrorCode.ToString());
                    return null;
                }
                catch (Exception ex)
                {
                    // Agregar al cache de variables fallidas
                    _failedVariables[variableName] = DateTime.UtcNow;
                    
                    _logger.LogError(ex, "❌ Error leyendo {Var} del PLC - Silenciando por 1 minuto", variableName);
                    return null;
                }
            }
            
            // Modo SIMULADO (fallback)
            if (_simulatedVariables.ContainsKey(variableName))
            {
                var value = _simulatedVariables[variableName];
                // Logging reducido para performance
                // _logger.LogDebug("📖 Read from SIMULATED PLC: {Var} = {Value}", variableName, value);
                
                // Retorno directo sin Task wrapper innecesario
                return value;
            }
            
            // ⭐ Auto-generar valor simulado para variables no definidas
            var autoValue = GenerateSimulatedValue(variableName, dataType);
            _simulatedVariables[variableName] = autoValue; // Cache para futuras lecturas
            _logger.LogDebug("🎮 Auto-generated simulated value for {Var}: {Value}", variableName, autoValue);
            return autoValue;
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
            
            // Default: entero entre 0 y 10
            return _random.Next(0, 10);
        }
        
        public async Task<bool> WriteVariableAsync(string variableName, object value, Type dataType)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }
            
            // Si está en modo REAL (no simulado), escribir al PLC real
            if (!_isSimulatedMode && _adsClient != null)
            {
                try
                {
                    // ✅ API CORRECTO Beckhoff 6.x
                    uint handle = _adsClient.CreateVariableHandle(variableName);
                    
                    try
                    {
                        byte[] buffer;
                        
                        if (dataType == typeof(int))
                        {
                            // ✅ Escribir INT de TwinCAT (16 bits = 2 bytes, signed)
                            buffer = new byte[2];  // INT = 16 bits (Int16)
                            using var stream = new MemoryStream(buffer);
                            using var writer = new BinaryWriter(stream);
                            writer.Write((short)value);  // Convertir a Int16
                            
                            _adsClient.Write(handle, buffer.AsMemory());
                        }
                        else if (dataType == typeof(bool))
                        {
                            buffer = new byte[1];
                            buffer[0] = (bool)value ? (byte)1 : (byte)0;
                            _adsClient.Write(handle, buffer.AsMemory());
                        }
                        else if (dataType == typeof(float))
                        {
                            buffer = new byte[4];
                            using var stream = new MemoryStream(buffer);
                            using var writer = new BinaryWriter(stream);
                            writer.Write((float)value);
                            
                            _adsClient.Write(handle, buffer.AsMemory());
                        }
                        
                        _logger.LogDebug("✍️ Wrote to REAL PLC: {Var} = {Value}", variableName, value);
                        return true;
                    }
                    finally
                    {
                        _adsClient.DeleteVariableHandle(handle);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error writing variable {Var} to REAL PLC", variableName);
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
            _adsClient?.Dispose();
        }
    }
}
