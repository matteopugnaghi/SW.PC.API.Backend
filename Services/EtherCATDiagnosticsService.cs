using SW.PC.API.Backend.Models.EtherCAT;
using TwinCAT.Ads;
using System.Runtime.InteropServices;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🌐 Servicio de diagnóstico de topología EtherCAT
    /// Lee información del Master EtherCAT via ADS para visualización de red.
    /// 
    /// OPTIMIZACIÓN: Solo lee cuando se solicita (no polling continuo)
    /// </summary>
    public interface IEtherCATDiagnosticsService
    {
        /// <summary>Obtiene la configuración actual</summary>
        EtherCATConfiguration GetConfiguration();

        /// <summary>Verifica si el diagnóstico EtherCAT está habilitado</summary>
        bool IsEnabled { get; }

        /// <summary>Lee la topología completa del Master EtherCAT</summary>
        Task<EtherCATTopology> GetTopologyAsync();

        /// <summary>Obtiene solo el resumen (para panel compacto, más ligero)</summary>
        Task<EtherCATSummary> GetSummaryAsync();

        /// <summary>Obtiene información de un esclavo específico</summary>
        Task<EtherCATSlaveNode?> GetSlaveInfoAsync(ushort slaveAddress);

        /// <summary>Fuerza una nueva lectura (invalida cache)</summary>
        void InvalidateCache();
    }

    public class EtherCATDiagnosticsService : IEtherCATDiagnosticsService, IDisposable
    {
        private readonly ILogger<EtherCATDiagnosticsService> _logger;
        private readonly IESIParserService _esiParser;
        private readonly EtherCATConfiguration _config;
        private readonly string _environmentMode;
        private AdsClient? _masterClient;
        private bool _isInitialized = false;

        /// <summary>
        /// Indica si estamos en modo desarrollo (permite simulación)
        /// </summary>
        private bool IsDevelopmentMode => _environmentMode.Equals("development", StringComparison.OrdinalIgnoreCase);

        // Cache para evitar lecturas excesivas
        private EtherCATTopology? _cachedTopology;
        private DateTime _cacheTimestamp = DateTime.MinValue;
        private readonly object _cacheLock = new();

        // Registros EtherCAT ESC (EtherCAT Slave Controller)
        private static class ESCRegisters
        {
            public const ushort Type = 0x0000;              // 1 byte - Device Type
            public const ushort Revision = 0x0001;          // 1 byte - Revision
            public const ushort Build = 0x0002;             // 2 bytes - Build
            public const ushort FMMUCount = 0x0004;         // 1 byte - FMMU count
            public const ushort SMCount = 0x0005;           // 1 byte - SyncManager count
            public const ushort RAMSize = 0x0006;           // 1 byte - RAM size
            public const ushort PortDescriptor = 0x0007;    // 1 byte - Port descriptor
            public const ushort ESCFeatures = 0x0008;       // 2 bytes - ESC features
            public const ushort ConfiguredAddress = 0x0010; // 2 bytes - Configured station address
            public const ushort AliasAddress = 0x0012;      // 2 bytes - Station alias
            public const ushort DLControl = 0x0100;         // 4 bytes - DL Control
            public const ushort DLStatus = 0x0110;          // 2 bytes - DL Status (topology!)
            public const ushort ALControl = 0x0120;         // 2 bytes - AL Control
            public const ushort ALStatus = 0x0130;          // 2 bytes - AL Status (state!)
            public const ushort ALStatusCode = 0x0134;      // 2 bytes - AL Status Code (error!)
            
            // Error counters
            public const ushort RxErrorCounter = 0x0300;    // 8 bytes (2 per port)
            public const ushort ForwardedError = 0x0308;    // 8 bytes
            public const ushort ProcessUnitError = 0x030C;  // 1 byte
            public const ushort PDIError = 0x030D;          // 1 byte
            public const ushort LostLinkCounter = 0x0310;   // 4 bytes (1 per port)
            
            // DC timestamps
            public const ushort DCReceiveTime0 = 0x0900;    // 4 bytes - Port 0
            public const ushort DCReceiveTime1 = 0x0904;    // 4 bytes - Port 1
            public const ushort DCReceiveTime2 = 0x0908;    // 4 bytes - Port 2
            public const ushort DCReceiveTime3 = 0x090C;    // 4 bytes - Port 3
        }

        // SII (Slave Information Interface) - EEPROM
        private static class SIIOffsets
        {
            public const ushort VendorId = 0x0008;          // 4 bytes
            public const ushort ProductCode = 0x000A;       // 4 bytes
            public const ushort RevisionNumber = 0x000C;    // 4 bytes
            public const ushort SerialNumber = 0x000E;      // 4 bytes
        }

        // ADS Index Groups para EtherCAT Master
        private static class EcAdsIndexGroups
        {
            // Para acceso al estado del dispositivo I/O
            public const uint IODEVICESTATE_BASE = 0x5000;
            public const uint DEVICE_COUNT = 0x02;
            public const uint DEVICE_IDS = 0x01;
            public const uint DEVICE_NAME = 0x01;
            public const uint DEVICE_NETID = 0x05;
            public const uint DEVICE_TYPE = 0x07;
            
            // Comandos EtherCAT
            public const uint ECMASTER_GETSLAVECOUNT = 0x0F020010;
            public const uint ECMASTER_GETSLAVEINFO = 0x0F020011;
            public const uint ECMASTER_GETSLAVESTATE = 0x0F020012;
            
            // Acceso a registros de esclavos
            public const uint ECMASTER_READSLAVEREGISTER = 0x0F020020;
            public const uint ECMASTER_READSLAVEEEEPROM = 0x0F020021;
        }

        public EtherCATDiagnosticsService(
            ILogger<EtherCATDiagnosticsService> logger,
            IExcelConfigService excelConfig,
            IProjectContextService projectContext,
            IESIParserService esiParser)
        {
            _logger = logger;
            _esiParser = esiParser;

            // Cargar configuración desde Excel (incluye EnvironmentMode)
            var (config, envMode) = LoadConfigurationFromExcel(excelConfig, projectContext);
            _config = config;
            _environmentMode = envMode;

            if (_config.EnableEtherCATTopology)
            {
                _logger.LogInformation("🌐 EtherCAT Diagnostics enabled - Master: {NetId}, Device: {DeviceId}, Mode: {Mode}",
                    _config.EtherCATMasterNetId, _config.EtherCATMasterDeviceId, _environmentMode);
            }
            else
            {
                _logger.LogInformation("🌐 EtherCAT Diagnostics disabled (not configured in Excel)");
            }
        }

        private (EtherCATConfiguration config, string environmentMode) LoadConfigurationFromExcel(
            IExcelConfigService excelConfig,
            IProjectContextService projectContext)
        {
            try
            {
                // Obtener ruta del Excel del proyecto activo
                var projectExcelPath = Path.Combine(projectContext.ConfigPath, "ProjectConfig.xlsm");
                var possiblePaths = new[]
                {
                    projectExcelPath,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
                    @"ExcelConfigs\ProjectConfig.xlsm"
                };

                var excelPath = possiblePaths.FirstOrDefault(File.Exists);
                if (excelPath == null)
                {
                    _logger.LogWarning("📊 EtherCAT: No se encontró ProjectConfig.xlsm");
                    return (new EtherCATConfiguration(), "development");
                }

                // Cargar configuración del sistema que incluye EtherCAT y EnvironmentMode
                var systemConfig = excelConfig.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
                var environmentMode = systemConfig?.EnvironmentMode?.ToLower() ?? "development";

                // Mapear a EtherCATConfiguration
                var config = new EtherCATConfiguration
                {
                    EnableEtherCATTopology = systemConfig.EnableEtherCATTopology,
                    EtherCATMasterNetId = systemConfig.EtherCATMasterNetId,
                    EtherCATMasterDeviceId = systemConfig.EtherCATMasterDeviceId,
                    ESIFilesPath = systemConfig.ESIFilesPath,
                    TopologyReadIntervalMs = systemConfig.EtherCATTopologyReadIntervalMs,
                    UseESIFiles = systemConfig.UseEtherCATESIFiles
                };

                return (config, environmentMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando configuración EtherCAT desde Excel");
                return (new EtherCATConfiguration(), "development");
            }
        }

        public EtherCATConfiguration GetConfiguration() => _config;

        public bool IsEnabled => _config.EnableEtherCATTopology && 
                                 !string.IsNullOrEmpty(_config.EtherCATMasterNetId);

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedTopology = null;
                _cacheTimestamp = DateTime.MinValue;
            }
            _logger.LogDebug("🌐 EtherCAT topology cache invalidated");
        }

        public async Task<EtherCATSummary> GetSummaryAsync()
        {
            if (!IsEnabled)
            {
                return new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Offline,
                    MasterStateText = "Disabled"
                };
            }

            try
            {
                // Si tenemos cache reciente, usar el summary de ahí
                lock (_cacheLock)
                {
                    if (_cachedTopology != null &&
                        (DateTime.Now - _cacheTimestamp).TotalMilliseconds < _config.TopologyReadIntervalMs)
                    {
                        return _cachedTopology.Summary;
                    }
                }

                // Lectura rápida solo del summary (sin topología completa)
                var topology = await GetTopologyAsync();
                return topology.Summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo summary EtherCAT");
                return new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Error,
                    MasterStateText = "Error"
                };
            }
        }

        public async Task<EtherCATTopology> GetTopologyAsync()
        {
            if (!IsEnabled)
            {
                return new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = "EtherCAT diagnostics not enabled in Excel configuration",
                    Timestamp = DateTime.Now,
                    EnvironmentMode = _environmentMode
                };
            }

            // Verificar cache
            lock (_cacheLock)
            {
                if (_cachedTopology != null &&
                    (DateTime.Now - _cacheTimestamp).TotalMilliseconds < _config.TopologyReadIntervalMs)
                {
                    _logger.LogDebug("🌐 Returning cached EtherCAT topology");
                    return _cachedTopology;
                }
            }

            _logger.LogInformation("🌐 Reading EtherCAT topology from Master... (Mode: {Mode})", _environmentMode);

            try
            {
                // Inicializar cliente ADS si es necesario
                bool connected = await EnsureConnectedAsync();
                
                if (!connected)
                {
                    // ⚠️ IMPORTANTE: En producción NO simular, mostrar error real
                    if (!IsDevelopmentMode)
                    {
                        _logger.LogWarning("🌐 PRODUCCIÓN: No hay conexión con EtherCAT Master - NO se usarán datos simulados");
                        return CreateErrorTopology($"No hay comunicación con el Master EtherCAT ({_config.EtherCATMasterNetId}). Verifique la conexión de red y el estado del PLC.");
                    }
                    
                    // En desarrollo, usar datos simulados
                    _logger.LogWarning("🌐 DESARROLLO: No hay conexión - Usando datos SIMULADOS");
                    return await CreateSimulatedTopologyAsync();
                }

                var topology = new EtherCATTopology
                {
                    Timestamp = DateTime.Now,
                    EnvironmentMode = _environmentMode,
                    IsSimulated = false
                };

                // 1. Leer información del Master
                topology.Master = await ReadMasterInfoAsync();

                // 2. Leer todos los esclavos (puede retornar simulados en dev si falla)
                var (slaves, isSimulated) = await ReadAllSlavesWithSimulationCheckAsync();
                topology.Slaves = slaves;
                topology.IsSimulated = isSimulated;

                // 3. Construir relaciones de topología (parent/child)
                BuildTopologyRelations(topology.Slaves);

                // 4. Calcular layout para visualización
                CalculateLayout(topology);

                // 5. Generar grafo para frontend
                topology.Graph = BuildTopologyGraph(topology);

                // 6. Detectar tipo de topología
                topology.DetectedTopology = DetectTopologyType(topology.Slaves);

                // 7. Calcular summary
                topology.Summary = CalculateSummary(topology);

                // Actualizar cache
                lock (_cacheLock)
                {
                    _cachedTopology = topology;
                    _cacheTimestamp = DateTime.Now;
                }

                _logger.LogInformation("🌐 EtherCAT topology read: {SlaveCount} slaves, type: {Type}, health: {Health}",
                    topology.Slaves.Count, topology.DetectedTopology, topology.Summary.OverallHealth);

                return topology;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading EtherCAT topology");
                return CreateErrorTopology($"Error: {ex.Message}");
            }
        }

        public async Task<EtherCATSlaveNode?> GetSlaveInfoAsync(ushort slaveAddress)
        {
            var topology = await GetTopologyAsync();
            return topology.Slaves.FirstOrDefault(s => s.ConfiguredAddress == slaveAddress);
        }

        /// <summary>
        /// Crea una topología completamente simulada (solo para modo desarrollo)
        /// </summary>
        private async Task<EtherCATTopology> CreateSimulatedTopologyAsync()
        {
            var topology = new EtherCATTopology
            {
                Timestamp = DateTime.Now,
                EnvironmentMode = _environmentMode,
                IsSimulated = true,  // ⚠️ Marcamos como SIMULADO
                HasCommunicationError = false
            };

            // Master simulado
            topology.Master = new EtherCATMaster
            {
                NetId = _config.EtherCATMasterNetId,
                DeviceId = _config.EtherCATMasterDeviceId,
                Name = "EtherCAT Master (SIMULATED)",
                IsConnected = false,
                State = EtherCATState.Operational,
                DeviceName = "TwinCAT 3 EtherCAT Master (Simulated)",
                RuntimeVersion = "3.1.4024"
            };

            // Esclavos simulados
            topology.Slaves = GenerateSimulatedSlaves();
            
            // Construir topología
            BuildTopologyRelations(topology.Slaves);
            CalculateLayout(topology);
            topology.Graph = BuildTopologyGraph(topology);
            topology.DetectedTopology = DetectTopologyType(topology.Slaves);
            topology.Summary = CalculateSummary(topology);

            // Actualizar cache
            lock (_cacheLock)
            {
                _cachedTopology = topology;
                _cacheTimestamp = DateTime.Now;
            }

            _logger.LogWarning("🌐 SIMULACIÓN: Topología generada con {Count} esclavos simulados", topology.Slaves.Count);
            
            return topology;
        }

        /// <summary>
        /// Lee esclavos con control de simulación según el modo de entorno
        /// </summary>
        private async Task<(List<EtherCATSlaveNode> slaves, bool isSimulated)> ReadAllSlavesWithSimulationCheckAsync()
        {
            var slaves = new List<EtherCATSlaveNode>();
            bool isSimulated = false;

            try
            {
                // Intentar métodos de lectura real
                slaves = await TryReadSlavesFromPlcVariablesAsync();
                
                if (slaves.Count > 0)
                {
                    _logger.LogInformation("✓ Read {Count} slaves from PLC variables (REAL DATA)", slaves.Count);
                    return (slaves, false);
                }

                slaves = await TryScanSlavesByAddressAsync();
                
                if (slaves.Count > 0)
                {
                    _logger.LogInformation("✓ Scanned {Count} slaves by address (REAL DATA)", slaves.Count);
                    return (slaves, false);
                }

                // No se encontraron esclavos reales
                if (IsDevelopmentMode)
                {
                    _logger.LogWarning("⚠️ DESARROLLO: No hay esclavos reales, usando datos SIMULADOS");
                    slaves = GenerateSimulatedSlaves();
                    isSimulated = true;
                }
                else
                {
                    _logger.LogWarning("⚠️ PRODUCCIÓN: No se detectaron esclavos EtherCAT en el bus");
                    // En producción, retornamos lista vacía - no simulamos
                    slaves = new List<EtherCATSlaveNode>();
                    isSimulated = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading slaves");
                
                if (IsDevelopmentMode)
                {
                    _logger.LogWarning("⚠️ DESARROLLO: Error de lectura, usando datos SIMULADOS");
                    slaves = GenerateSimulatedSlaves();
                    isSimulated = true;
                }
                else
                {
                    _logger.LogError("✕ PRODUCCIÓN: Error de comunicación con bus EtherCAT");
                    throw; // Re-lanzar en producción
                }
            }

            return (slaves, isSimulated);
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (_masterClient != null && _masterClient.IsConnected)
            {
                return true;
            }

            try
            {
                _masterClient?.Dispose();
                _masterClient = new AdsClient();

                var netId = new AmsNetId(_config.EtherCATMasterNetId);
                
                // Puerto 0xFFFF para comandos del sistema, o puerto específico del Master
                // Típicamente puerto 851 para PLC, pero para EtherCAT Master directo usamos otro
                _masterClient.Connect(netId, AmsPort.R0_IO);

                // Verificar conexión
                var state = _masterClient.ReadState();
                _isInitialized = state.AdsState == AdsState.Run || state.AdsState == AdsState.Config;

                _logger.LogInformation("🌐 Connected to EtherCAT Master at {NetId}, State: {State}",
                    _config.EtherCATMasterNetId, state.AdsState);

                return _isInitialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot connect to EtherCAT Master at {NetId}",
                    _config.EtherCATMasterNetId);
                return false;
            }
        }

        private async Task<EtherCATMaster> ReadMasterInfoAsync()
        {
            var master = new EtherCATMaster
            {
                NetId = _config.EtherCATMasterNetId,
                DeviceId = _config.EtherCATMasterDeviceId,
                Name = "EtherCAT Master",
                IsConnected = _masterClient?.IsConnected ?? false
            };

            try
            {
                if (_masterClient == null) return master;

                // Leer info del dispositivo
                var deviceInfo = _masterClient.ReadDeviceInfo();
                master.DeviceName = deviceInfo.Name;
                master.RuntimeVersion = $"{deviceInfo.Version.Version}.{deviceInfo.Version.Revision}.{deviceInfo.Version.Build}";

                // Leer estado
                var state = _masterClient.ReadState();
                master.State = state.AdsState == AdsState.Run ? EtherCATState.Operational : EtherCATState.PreOp;

                // Intentar leer número de esclavos
                // Esto depende de cómo esté configurado el acceso en TwinCAT
                await TryReadSlaveCountAsync(master);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading Master info, using defaults");
            }

            return master;
        }

        private async Task TryReadSlaveCountAsync(EtherCATMaster master)
        {
            try
            {
                // Método 1: Leer variable global si existe
                var variableNames = new[]
                {
                    "TwinCAT_SystemInfoVarList._EcMasterInfo.SlaveCount",
                    "GVL_EtherCAT.nSlaveCount",
                    "MAIN.nEcSlaveCount"
                };

                foreach (var varName in variableNames)
                {
                    try
                    {
                        var handle = _masterClient!.CreateVariableHandle(varName);
                        var count = _masterClient.ReadAny<ushort>(handle);
                        _masterClient.DeleteVariableHandle(handle);

                        master.ConfiguredSlaveCount = count;
                        master.ActualSlaveCount = count;
                        _logger.LogDebug("Slave count read from {Variable}: {Count}", varName, count);
                        return;
                    }
                    catch
                    {
                        // Variable no existe, probar siguiente
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not read slave count directly: {Error}", ex.Message);
            }
        }

        // NOTA: ReadAllSlavesAsync reemplazada por ReadAllSlavesWithSimulationCheckAsync
        // que controla correctamente la simulación según EnvironmentMode

        private async Task<List<EtherCATSlaveNode>> TryReadSlavesFromPlcVariablesAsync()
        {
            var slaves = new List<EtherCATSlaveNode>();

            // Estructura típica en TwinCAT para exponer info de esclavos
            // GVL_EtherCAT.aSlaveInfo[1..n] : ARRAY OF ST_EcSlaveInfo
            var arrayNames = new[]
            {
                "GVL_EtherCAT.aSlaveInfo",
                "GVL.aEcSlaves",
                "MAIN.aEtherCATSlaves"
            };

            // Por ahora retornamos vacío - implementación completa requiere
            // conocer la estructura exacta de variables en tu PLC
            return slaves;
        }

        private async Task<List<EtherCATSlaveNode>> TryScanSlavesByAddressAsync()
        {
            var slaves = new List<EtherCATSlaveNode>();

            try
            {
                // Escanear direcciones típicas (1001-1100)
                for (ushort addr = 1001; addr < 1100; addr++)
                {
                    try
                    {
                        // Intentar leer AL Status del esclavo
                        // Si responde, existe
                        var slave = await TryReadSlaveAtAddressAsync(addr);
                        if (slave != null)
                        {
                            slave.Position = (ushort)(slaves.Count + 1);
                            slaves.Add(slave);
                        }
                        else
                        {
                            // Si falla 3 consecutivos, probablemente no hay más esclavos
                            if (slaves.Count > 0 && addr > slaves.Last().ConfiguredAddress + 10)
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Esclavo no existe en esta dirección
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Address scan failed: {Error}", ex.Message);
            }

            return slaves;
        }

        private async Task<EtherCATSlaveNode?> TryReadSlaveAtAddressAsync(ushort address)
        {
            // Esta implementación requiere acceso directo a registros ESC
            // via ADS o comandos específicos de TwinCAT
            // Por ahora retornamos null - se implementará con acceso real
            return null;
        }

        /// <summary>
        /// Genera esclavos simulados para desarrollo/demostración
        /// Topología representativa de una instalación industrial Beckhoff
        /// con múltiples grupos conectados via E-Bus y Cable Ethernet
        /// </summary>
        private List<EtherCATSlaveNode> GenerateSimulatedSlaves()
        {
            var slaves = new List<EtherCATSlaveNode>();
            var random = new Random(42); // Semilla fija para consistencia

            // Topología realista de planta industrial:
            // GRUPO 1: Panel principal (Coupler + terminales E-Bus)
            // GRUPO 2: Extension via cable Ethernet (EK1110 coupler) + terminales + Junction
            // GRUPO 3: Rama A del Junction (EK1100 + terminales)
            // GRUPO 4: Rama B del Junction (EK1100 + terminales)
            // 
            // Topología:
            // Master → Cable → EK1100 (Grupo 1) → E-Bus terminales
            //                       ↓ (El EK1100 tiene puerto de salida X2 que conecta)
            //                  Cable → EK1110 (Grupo 2) → E-Bus terminales → EK1122 (Junction)
            //                                                                   ↓ X2     ↓ X3
            //                                                            Cable → EK1100  Cable → EK1100
            //                                                            (Grupo 3)       (Grupo 4)

            var simulatedDevices = new[]
            {
                // === GRUPO 1: Panel Principal ===
                (VendorId: 0x00000002u, ProductCode: 0x044C2C52u, Name: "EK1100-0030", Desc: "EtherCAT Coupler", Type: "Coupler"),
                (VendorId: 0x00000002u, ProductCode: 0x03F03052u, Name: "EL1008", Desc: "8x Digital Input 24V", Type: "Digital Input"),
                (VendorId: 0x00000002u, ProductCode: 0x03F43052u, Name: "EL1012", Desc: "2x Digital Input 24V", Type: "Digital Input"),
                (VendorId: 0x00000002u, ProductCode: 0x07D83052u, Name: "EL2008", Desc: "8x Digital Output 24V", Type: "Digital Output"),
                (VendorId: 0x00000002u, ProductCode: 0x07DC3052u, Name: "EL2024", Desc: "4x Digital Output 24V 2A", Type: "Digital Output"),
                (VendorId: 0x00000002u, ProductCode: 0x0BF63052u, Name: "EL3062", Desc: "2x Analog Input 0-10V", Type: "Analog Input"),
                
                // === GRUPO 2: Extension via Ethernet (hasta 100m de cable) ===
                // EK1110 actúa como COUPLER que recibe cable Ethernet
                (VendorId: 0x00000002u, ProductCode: 0x04562C52u, Name: "EK1110-0044", Desc: "EtherCAT Extension", Type: "Extension"),
                (VendorId: 0x00000002u, ProductCode: 0x03F03052u, Name: "EL1008", Desc: "8x Digital Input 24V", Type: "Digital Input"),
                (VendorId: 0x00000002u, ProductCode: 0x07D83052u, Name: "EL2008", Desc: "8x Digital Output 24V", Type: "Digital Output"),
                (VendorId: 0x00000002u, ProductCode: 0x17713052u, Name: "EL6001", Desc: "Serial RS232", Type: "Communication"),
                (VendorId: 0x00000002u, ProductCode: 0x1B813052u, Name: "EL7041", Desc: "Stepper Motor Terminal", Type: "Motion"),
                (VendorId: 0x00000002u, ProductCode: 0x07D84052u, Name: "EL2889", Desc: "16x DO 24V 0.5A", Type: "Digital Output"),
                // Junction Box con 2 puertos Ethernet de salida (es un TERMINAL especial)
                (VendorId: 0x00000002u, ProductCode: 0x04622C52u, Name: "EK1122", Desc: "EtherCAT Junction (2 ports)", Type: "Junction"),
                
                // === GRUPO 3: Rama A del Junction (Puerto X2) ===
                (VendorId: 0x00000002u, ProductCode: 0x044C2C52u, Name: "EK1100-0030", Desc: "EtherCAT Coupler", Type: "Coupler"),
                (VendorId: 0x00000002u, ProductCode: 0x03F03052u, Name: "EL1008", Desc: "8x Digital Input 24V", Type: "Digital Input"),
                (VendorId: 0x00000002u, ProductCode: 0x07D83052u, Name: "EL2008", Desc: "8x Digital Output 24V", Type: "Digital Output"),
                (VendorId: 0x00000002u, ProductCode: 0x0C1E3052u, Name: "EL3102", Desc: "2x Analog Input ±10V", Type: "Analog Input"),
                
                // === GRUPO 4: Rama B del Junction (Puerto X3) ===
                (VendorId: 0x00000002u, ProductCode: 0x044C2C52u, Name: "EK1100-0030", Desc: "EtherCAT Coupler", Type: "Coupler"),
                (VendorId: 0x00000002u, ProductCode: 0x03F03052u, Name: "EL1008", Desc: "8x Digital Input 24V", Type: "Digital Input"),
                (VendorId: 0x00000002u, ProductCode: 0x07D83052u, Name: "EL2008", Desc: "8x Digital Output 24V", Type: "Digital Output"),
                (VendorId: 0x00000002u, ProductCode: 0x24023052u, Name: "EL9011", Desc: "Bus End Cover", Type: "System"),
            };

            for (int i = 0; i < simulatedDevices.Length; i++)
            {
                var dev = simulatedDevices[i];
                var state = i < simulatedDevices.Length - 2 
                    ? EtherCATState.Operational 
                    : (random.NextDouble() > 0.7 ? EtherCATState.SafeOp : EtherCATState.Operational);

                // 🌐 Enriquecer con información de ESI files si está disponible
                var esiInfo = _esiParser.GetDeviceInfo(dev.VendorId, dev.ProductCode);
                
                // Usar ESI solo si encontró el dispositivo real (no el fallback con ProductCode)
                var esiFoundDevice = esiInfo != null && !esiInfo.Type.StartsWith("0x");
                var deviceName = esiFoundDevice ? esiInfo!.Type : dev.Name;
                var deviceDesc = esiFoundDevice ? esiInfo!.ProductName : dev.Desc;
                var vendorName = esiInfo?.VendorName ?? _esiParser.GetVendorName(dev.VendorId);
                var imageUrl = esiFoundDevice ? esiInfo!.ImageFile : "";

                var slave = new EtherCATSlaveNode
                {
                    Position = (ushort)(i + 1),
                    ConfiguredAddress = (ushort)(1001 + i),
                    AliasAddress = 0,
                    VendorId = dev.VendorId,
                    ProductCode = dev.ProductCode,
                    RevisionNumber = 0x00120000,
                    SerialNumber = (uint)(1000000 + i),
                    Name = deviceName,
                    VendorName = vendorName,
                    Description = deviceDesc,
                    DeviceType = esiFoundDevice ? esiInfo!.GroupType : dev.Type,
                    ImageUrl = imageUrl,  // Imagen del ESI si existe
                    State = state,
                    ALStatusCode = state == EtherCATState.Operational ? (ushort)0 : (ushort)0x001A,
                    ALStatusDescription = state == EtherCATState.Operational ? "No error" : "Synchronization error",
                    Health = state == EtherCATState.Operational ? NodeHealth.Healthy : NodeHealth.Warning,
                    PhysicalType = dev.Type switch 
                    {
                        "Coupler" or "Extension" or "Junction" => PhysicalType.Mixed,
                        "Box" => PhysicalType.EthernetOnly,
                        _ => PhysicalType.EBusOnly
                    },
                    HasDC = dev.Type == "Motion" || dev.Type == "Drive",
                    PropagationDelayNs = (i + 1) * 150,
                    SupportsCoE = true,
                    SupportsFoE = dev.Type == "Drive",
                    Ports = GenerateSimulatedPorts(i, dev.Type, simulatedDevices.Length),
                    ErrorCounters = new SlaveErrorCounters
                    {
                        CRCErrorCount = (uint)(random.NextDouble() > 0.8 ? random.Next(1, 15) : 0),
                        LostLinkCount = (uint)(random.NextDouble() > 0.9 ? random.Next(1, 5) : 0),
                        RxErrorCount = (uint)(random.NextDouble() > 0.85 ? random.Next(1, 10) : 0)
                    }
                };

                slave.ActivePortCount = slave.Ports.Count(p => p.HasCommunication);
                slave.ActivePortsBitmap = (byte)slave.Ports
                    .Where(p => p.HasCommunication)
                    .Aggregate(0, (acc, p) => acc | (1 << p.PortNumber));

                slaves.Add(slave);
            }

            return slaves;
        }

        private List<EtherCATPort> GenerateSimulatedPorts(int slaveIndex, string deviceType, int totalSlaves)
        {
            var ports = new List<EtherCATPort>();
            var isCoupler = deviceType == "Coupler";
            var isExtension = deviceType == "Extension";
            var isJunction = deviceType == "Junction";
            var isBox = deviceType == "Box";
            
            // Determinar número de puertos según tipo
            int portCount = (isCoupler || isExtension || isJunction || isBox) ? 4 : 2;

            for (byte p = 0; p < portCount; p++)
            {
                var isFirst = slaveIndex == 0;
                var isLast = slaveIndex == totalSlaves - 1;

                // Lógica de puertos:
                // - Puerto 0: Siempre entrada (desde master o esclavo anterior)
                // - Puerto 1: Salida E-Bus al siguiente terminal del stack
                // - Puerto 2/3: Puertos Ethernet para ramificaciones (solo couplers/extensions/boxes)
                
                bool hasComm;
                PortPhysics physics;
                PortType portType;
                
                if (p == 0)
                {
                    // Puerto 0: Entrada - siempre conectado
                    hasComm = true;
                    physics = isBox ? PortPhysics.Ethernet : (isFirst ? PortPhysics.Ethernet : PortPhysics.EBus);
                    portType = physics == PortPhysics.Ethernet ? PortType.MII : PortType.EBUS;
                }
                else if (p == 1)
                {
                    // Puerto 1: Salida E-Bus al siguiente terminal
                    hasComm = !isLast && !isExtension && !isBox;
                    physics = PortPhysics.EBus;
                    portType = PortType.EBUS;
                }
                else if (p == 2)
                {
                    // Puerto 2: Salida Ethernet (extension/junction) o E-Bus adicional
                    hasComm = isExtension || isJunction || isBox;
                    physics = (isExtension || isJunction || isBox) ? PortPhysics.Ethernet : PortPhysics.EBus;
                    portType = physics == PortPhysics.Ethernet ? PortType.MII : PortType.EBUS;
                }
                else
                {
                    // Puerto 3: Raramente usado
                    hasComm = false;
                    physics = PortPhysics.Unknown;
                    portType = PortType.NotImplemented;
                }

                ports.Add(new EtherCATPort
                {
                    PortNumber = p,
                    Type = portType,
                    Physics = physics,
                    IsOpen = hasComm,
                    HasCommunication = hasComm,
                    LinkUp = hasComm,
                    IsLoop = false,
                    Health = hasComm ? LinkHealth.Good : LinkHealth.Unknown
                });
            }

            return ports;
        }

        private void BuildTopologyRelations(List<EtherCATSlaveNode> slaves)
        {
            // Construir relaciones parent/child basándose en puertos activos
            // En EtherCAT, el orden de escaneo indica la conexión física
            
            for (int i = 0; i < slaves.Count; i++)
            {
                var slave = slaves[i];

                if (i == 0)
                {
                    // Primer esclavo conectado al Master
                    slave.ParentSlaveIndex = -1;
                    slave.ParentPort = null;
                    slave.EntryPort = 0;
                    slave.TreeLevel = 0;
                }
                else
                {
                    // Buscar padre basándose en topología
                    var parent = FindParent(slaves, i);
                    slave.ParentSlaveIndex = parent.index;
                    slave.ParentPort = parent.port;
                    slave.EntryPort = 0;
                    slave.TreeLevel = parent.index >= 0 
                        ? slaves[parent.index].TreeLevel + 1 
                        : 0;

                    // Registrar como hijo en el padre
                    if (parent.index >= 0)
                    {
                        slaves[parent.index].ChildSlaveIndices.Add(i);
                    }
                }
            }
        }

        private (int index, byte? port) FindParent(List<EtherCATSlaveNode> slaves, int currentIndex)
        {
            // Lógica simplificada: el padre es el esclavo anterior
            // En topología de árbol, habría que analizar puertos
            if (currentIndex > 0)
            {
                return (currentIndex - 1, (byte)1);
            }
            return (-1, null);
        }

        private void CalculateLayout(EtherCATTopology topology)
        {
            // Layout jerárquico simple
            const int nodeWidth = 180;
            const int nodeHeight = 80;
            const int horizontalGap = 40;
            const int verticalGap = 100;

            // Master en la parte superior
            var masterNode = new TopologyNode
            {
                Id = "master",
                Label = topology.Master.Name,
                Type = "master",
                State = topology.Master.State,
                VendorName = "Beckhoff",
                ProductName = topology.Master.DeviceName,
                Health = topology.Master.IsConnected ? NodeHealth.Healthy : NodeHealth.Offline,
                X = 400,
                Y = 50,
                Width = nodeWidth,
                Height = nodeHeight
            };

            // Agrupar esclavos por nivel
            var levelGroups = topology.Slaves
                .GroupBy(s => s.TreeLevel)
                .OrderBy(g => g.Key)
                .ToList();

            int y = 150 + verticalGap;
            foreach (var level in levelGroups)
            {
                var slavesInLevel = level.ToList();
                int totalWidth = slavesInLevel.Count * (nodeWidth + horizontalGap);
                int startX = (800 - totalWidth) / 2 + nodeWidth / 2;

                for (int i = 0; i < slavesInLevel.Count; i++)
                {
                    var slave = slavesInLevel[i];
                    slave.LayoutX = startX + i * (nodeWidth + horizontalGap);
                    slave.LayoutY = y;
                }

                y += nodeHeight + verticalGap;
            }
        }

        private TopologyGraph BuildTopologyGraph(EtherCATTopology topology)
        {
            var graph = new TopologyGraph();

            // Nodo Master
            graph.Nodes.Add(new TopologyNode
            {
                Id = "master",
                Label = "EtherCAT Master",
                Type = "master",
                State = topology.Master.State,
                VendorName = "Beckhoff",
                ProductName = topology.Master.DeviceName,
                Health = topology.Master.IsConnected ? NodeHealth.Healthy : NodeHealth.Offline,
                X = 400,
                Y = 50,
                Width = 180,
                Height = 80
            });

            // Nodos esclavos
            foreach (var slave in topology.Slaves)
            {
                graph.Nodes.Add(new TopologyNode
                {
                    Id = $"slave_{slave.Position}",
                    Label = slave.Name,
                    Type = "slave",
                    SlaveIndex = slave.Position,
                    State = slave.State,
                    VendorName = slave.VendorName,
                    ProductName = slave.Description,
                    Health = slave.Health,
                    X = slave.LayoutX,
                    Y = slave.LayoutY,
                    Width = 180,
                    Height = 80
                });
            }

            // Aristas
            foreach (var slave in topology.Slaves)
            {
                var sourceId = slave.ParentSlaveIndex < 0 
                    ? "master" 
                    : $"slave_{topology.Slaves[slave.ParentSlaveIndex].Position}";
                var targetId = $"slave_{slave.Position}";

                graph.Edges.Add(new TopologyEdge
                {
                    Id = $"edge_{sourceId}_{targetId}",
                    SourceNodeId = sourceId,
                    SourcePort = slave.ParentPort ?? 0,
                    TargetNodeId = targetId,
                    TargetPort = slave.EntryPort ?? 0,
                    HasErrors = slave.ErrorCounters.HasErrors,
                    Health = slave.ErrorCounters.HasErrors ? LinkHealth.Degraded : LinkHealth.Good,
                    ErrorCount = (uint)slave.ErrorCounters.TotalErrors
                });
            }

            return graph;
        }

        private TopologyType DetectTopologyType(List<EtherCATSlaveNode> slaves)
        {
            if (slaves.Count == 0) return TopologyType.Unknown;

            // Verificar si hay ramificaciones
            var hasMultipleChildren = slaves.Any(s => s.ChildSlaveIndices.Count > 1);
            var maxLevel = slaves.Max(s => s.TreeLevel);

            if (hasMultipleChildren)
            {
                return maxLevel > 2 ? TopologyType.Tree : TopologyType.Star;
            }

            return TopologyType.Line;
        }

        private EtherCATSummary CalculateSummary(EtherCATTopology topology)
        {
            var summary = new EtherCATSummary
            {
                ConfiguredSlaveCount = topology.Slaves.Count,
                OperationalSlaveCount = topology.Slaves.Count(s => s.State == EtherCATState.Operational),
                SlavesWithErrors = topology.Slaves.Count(s => s.State.HasError() || s.ErrorCounters.HasErrors),
                TotalCRCErrors = topology.Slaves.Sum(s => s.ErrorCounters.CRCErrorCount),
                TotalLostLinks = topology.Slaves.Sum(s => s.ErrorCounters.LostLinkCount),
                MasterStateText = topology.Master.State.ToShortString()
            };

            // Calcular salud general
            if (topology.HasCommunicationError || !topology.Master.IsConnected)
            {
                summary.OverallHealth = NetworkHealth.Offline;
            }
            else if (summary.SlavesWithErrors > 0 || summary.OperationalSlaveCount < summary.ConfiguredSlaveCount * 0.9)
            {
                summary.OverallHealth = summary.OperationalSlaveCount < summary.ConfiguredSlaveCount * 0.5
                    ? NetworkHealth.Error
                    : NetworkHealth.Warning;
            }
            else
            {
                summary.OverallHealth = NetworkHealth.Healthy;
            }

            return summary;
        }

        private EtherCATTopology CreateErrorTopology(string errorMessage)
        {
            return new EtherCATTopology
            {
                HasCommunicationError = true,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now,
                IsSimulated = false,  // No es simulación, es error real
                EnvironmentMode = _environmentMode,
                Summary = new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Offline,
                    MasterStateText = IsDevelopmentMode ? "Error (DEV)" : "Error - Sin comunicación"
                }
            };
        }

        public void Dispose()
        {
            _masterClient?.Dispose();
            _masterClient = null;
        }
    }
}
