using SW.PC.API.Backend.Models.EtherCAT;
using SW.PC.API.Backend.Data;
using Microsoft.EntityFrameworkCore;
using TwinCAT.Ads;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🌐 Servicio de diagnóstico de topología EtherCAT
    /// Lee información del Master EtherCAT via ADS para visualización de red.
    /// 
    /// OPTIMIZACIÓN: Solo lee cuando se solicita (no polling continuo)
    /// Soporta configuración guardada vs escaneo en tiempo real
    /// </summary>
    public interface IEtherCATDiagnosticsService
    {
        /// <summary>Obtiene la configuración actual</summary>
        EtherCATConfiguration GetConfiguration();

        /// <summary>Verifica si el diagnóstico EtherCAT está habilitado</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Lee la topología. Si hay configuración guardada, la usa como base.
        /// Si rescan=true, fuerza un escaneo completo ignorando config guardada.
        /// </summary>
        Task<EtherCATTopology> GetTopologyAsync(bool rescan = false);

        /// <summary>Obtiene solo el resumen (para panel compacto, más ligero)</summary>
        Task<EtherCATSummary> GetSummaryAsync();

        /// <summary>Obtiene información de un esclavo específico</summary>
        Task<EtherCATSlaveNode?> GetSlaveInfoAsync(ushort slaveAddress);

        /// <summary>Fuerza una nueva lectura (invalida cache)</summary>
        void InvalidateCache();

        // === Métodos para configuración guardada ===

        /// <summary>Guarda la topología actual como configuración de referencia</summary>
        Task<EtherCATSavedConfiguration> SaveConfigurationAsync(string? notes = null);

        /// <summary>Obtiene la configuración guardada (si existe)</summary>
        Task<EtherCATSavedConfiguration?> GetSavedConfigurationAsync();

        /// <summary>Elimina la configuración guardada</summary>
        Task<bool> DeleteSavedConfigurationAsync();

        /// <summary>Compara la configuración guardada con el estado actual</summary>
        Task<EtherCATConfigurationComparison> CompareWithSavedConfigurationAsync();

        /// <summary>Verifica si existe configuración guardada</summary>
        Task<bool> HasSavedConfigurationAsync();

        /// <summary>
        /// ⭐ OPTIMIZADO: Obtiene la topología guardada con estados actualizados del PLC.
        /// NO procesa ESI, NO recalcula layout - solo actualiza estados.
        /// Usar cuando ya existe configuración guardada para máximo rendimiento.
        /// </summary>
        Task<EtherCATTopology?> GetSavedTopologyWithCurrentStatesAsync();

        /// <summary>Prueba la conexión al Master EtherCAT y retorna diagnóstico detallado</summary>
        Task<EtherCATConnectionDiagnostics> TestConnectionAsync();
    }

    public class EtherCATDiagnosticsService : IEtherCATDiagnosticsService, IDisposable
    {
        private readonly ILogger<EtherCATDiagnosticsService> _logger;
        private readonly IESIParserService _esiParser;
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectContextService _projectContext;
        private readonly EtherCATConfiguration _config;
        private readonly bool _useSimulatedPlc;
        private AdsClient? _masterClient;
        private bool _isInitialized = false;

        /// <summary>
        /// Indica si usar modo simulado (leído de Excel: UseSimulatedPlc)
        /// </summary>
        private bool IsSimulatedMode => _useSimulatedPlc;

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

        // ADS Index Groups para EtherCAT Master (TwinCAT 3)
        // Documentación: https://infosys.beckhoff.com/
        // Puerto: AmsPort.R0_IO (300) - Acceso directo I/O
        private static class EcAdsIndexGroups
        {
            // ========================================
            // TwinCAT 3 - Acceso directo al EtherCAT Master via I/O (Puerto 300)
            // ========================================
            
            /// <summary>
            /// Base del Index Group para EtherCAT Master
            /// Fórmula: 0xF302 + (DeviceId * 0x10000)
            /// Ejemplo: DeviceId=2 → 0x0002F302
            /// </summary>
            public const uint EC_MASTER_BASE = 0xF302;
            
            // Index Offsets para el Master (usados con EC_MASTER_BASE + DeviceId*0x10000)
            public const uint EC_MASTER_INFO = 0x00;           // Información del Master
            public const uint EC_MASTER_SLAVECOUNT = 0x01;     // Número de esclavos (UINT16)
            public const uint EC_SLAVE_IDENTITY_BASE = 0x02;   // Base para identidad de esclavos
            public const uint EC_SLAVE_STATE_BASE = 0x100;     // Base para estado AL de esclavos
            public const uint EC_SLAVE_IDENTITY_SIZE = 16;     // Tamaño ST_EcSlaveIdentity (4x UINT32)
            
            // ========================================
            // Métodos de cálculo de Index Groups
            // ========================================
            
            /// <summary>
            /// Calcula el Index Group base para un EtherCAT Master específico
            /// </summary>
            public static uint GetMasterIndexGroup(int deviceId) 
                => EC_MASTER_BASE + ((uint)deviceId * 0x10000);
            
            /// <summary>
            /// Calcula el Index Offset para leer la identidad de un esclavo
            /// </summary>
            public static uint GetSlaveIdentityOffset(ushort slaveIndex)
                => EC_SLAVE_IDENTITY_BASE + (uint)(slaveIndex * EC_SLAVE_IDENTITY_SIZE);
            
            /// <summary>
            /// Calcula el Index Offset para leer el estado AL de un esclavo
            /// </summary>
            public static uint GetSlaveStateOffset(ushort slaveIndex)
                => EC_SLAVE_STATE_BASE + slaveIndex;
            
            // ========================================
            // Alternativa: Index Groups legados (por si los anteriores no funcionan)
            // ========================================
            public const uint ECMASTER_LEGACY_SLAVECOUNT = 0x0F020001;
            public const uint ECMASTER_LEGACY_SLAVEIDENTITY = 0x0F020002;
        }

        public EtherCATDiagnosticsService(
            ILogger<EtherCATDiagnosticsService> logger,
            IExcelConfigService excelConfig,
            IProjectContextService projectContext,
            IESIParserService esiParser,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _esiParser = esiParser;
            _serviceProvider = serviceProvider;
            _projectContext = projectContext;

            // Cargar configuración desde Excel (incluye UseSimulatedPlc)
            var (config, useSimulated) = LoadConfigurationFromExcel(excelConfig, projectContext);
            _config = config;
            _useSimulatedPlc = useSimulated;

            if (_config.EnableEtherCATTopology)
            {
                _logger.LogInformation("🌐 EtherCAT Diagnostics enabled - Master: {NetId}, FB: {FB}, UseSimulatedPlc: {Simulated}",
                    _config.EtherCATMasterNetId, _config.EtherCATDiagFbInstance, _useSimulatedPlc);
            }
            else
            {
                _logger.LogInformation("🌐 EtherCAT Diagnostics disabled (not configured in Excel)");
            }
        }

        private (EtherCATConfiguration config, bool useSimulatedPlc) LoadConfigurationFromExcel(
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
                    return (new EtherCATConfiguration(), true); // Default: simulado
                }

                // Cargar configuración del sistema que incluye EtherCAT y UseSimulatedPlc
                var systemConfig = excelConfig.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
                
                if (systemConfig == null)
                {
                    _logger.LogWarning("📊 EtherCAT: SystemConfig es null");
                    return (new EtherCATConfiguration(), true); // Default: simulado
                }
                
                // ⭐ Usar UseSimulatedPlc del Excel (igual que TwinCATService)
                var useSimulatedPlc = systemConfig.UseSimulatedPlc;
                _logger.LogInformation("📊 EtherCAT: UseSimulatedPlc leído desde Excel: {Value}", useSimulatedPlc);

                // Mapear a EtherCATConfiguration
                var config = new EtherCATConfiguration
                {
                    EnableEtherCATTopology = systemConfig.EnableEtherCATTopology,
                    EtherCATMasterNetId = systemConfig.EtherCATMasterNetId,
                    EtherNETIdTwincat = systemConfig.EtherNETIdTwincat,
                    ESIFilesPath = systemConfig.ESIFilesPath,
                    TopologyReadIntervalMs = systemConfig.EtherCATTopologyReadIntervalMs,
                    UseESIFiles = systemConfig.UseEtherCATESIFiles,
                    EtherCATDiagFbInstance = systemConfig.EtherCATDiagFbInstance
                };

                _logger.LogInformation("📊 EtherCAT: Config cargada - NetId: {NetId}, FB: {FB}, IP: {IP}",
                    config.EtherCATMasterNetId, config.EtherCATDiagFbInstance, config.EtherNETIdTwincat);

                return (config, useSimulatedPlc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando configuración EtherCAT desde Excel");
                return (new EtherCATConfiguration(), true); // Default: simulado por seguridad
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

        /// <summary>
        /// 🔍 Prueba la conexión al Master EtherCAT con diagnóstico detallado
        /// </summary>
        public async Task<EtherCATConnectionDiagnostics> TestConnectionAsync()
        {
            var diag = new EtherCATConnectionDiagnostics
            {
                Timestamp = DateTime.Now,
                IsEnabled = IsEnabled,
                IsSimulatedMode = IsSimulatedMode,
                ConfiguredNetId = _config.EtherCATMasterNetId
            };

            _logger.LogInformation("🔍 Iniciando diagnóstico de conexión EtherCAT...");

            // Paso 1: Verificar si está habilitado
            if (!IsEnabled)
            {
                diag.DiagnosticMessages.Add("❌ EtherCAT diagnóstico NO está habilitado en Excel");
                diag.DiagnosticMessages.Add($"   - EnableEtherCATTopology: {_config.EnableEtherCATTopology}");
                diag.DiagnosticMessages.Add($"   - EtherCATMasterNetId: '{_config.EtherCATMasterNetId}'");
                diag.Summary = "EtherCAT diagnóstico no está habilitado. Configure EnableEtherCATTopology=true y EtherCATMasterNetId en Excel.";
                return diag;
            }

            diag.DiagnosticMessages.Add("✅ EtherCAT diagnóstico habilitado en Excel");
            diag.DiagnosticMessages.Add($"   - UseSimulatedPlc: {_useSimulatedPlc}");
            diag.DiagnosticMessages.Add($"   - NetId: {_config.EtherCATMasterNetId}");
            diag.DiagnosticMessages.Add($"   - EtherNETIdTwincat (IP): {_config.EtherNETIdTwincat}");
            diag.DiagnosticMessages.Add($"   - FB_EtherCATDiag: {_config.EtherCATDiagFbInstance}");

            // Determinar IP a usar: EtherNETIdTwincat o extraer de NetId
            string targetIpAddress;
            if (!string.IsNullOrWhiteSpace(_config.EtherNETIdTwincat))
            {
                targetIpAddress = _config.EtherNETIdTwincat;
                diag.DiagnosticMessages.Add($"   → Usando IP de EtherNETIdTwincat: {targetIpAddress}");
            }
            else
            {
                // Extraer IP de los primeros 4 octetos del NetId
                var ipParts = _config.EtherCATMasterNetId.Split('.');
                if (ipParts.Length >= 4)
                {
                    targetIpAddress = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.{ipParts[3]}";
                    diag.DiagnosticMessages.Add($"   → IP extraída del NetId: {targetIpAddress}");
                }
                else
                {
                    diag.DiagnosticMessages.Add("❌ No se pudo determinar la IP de destino");
                    diag.Summary = "Configure EtherNETIdTwincat en Excel con la IP del PC TwinCAT";
                    return diag;
                }
            }

            // Paso 2: Verificar conectividad de red (ping)
            diag.DiagnosticMessages.Add($"⏳ Verificando ping a {targetIpAddress}...");
            
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(targetIpAddress, 2000);
                
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    diag.DiagnosticMessages.Add($"✅ Ping exitoso a {targetIpAddress} ({reply.RoundtripTime}ms)");
                }
                else
                {
                    diag.DiagnosticMessages.Add($"❌ Ping falló a {targetIpAddress}: {reply.Status}");
                    diag.DiagnosticMessages.Add("   💡 Verifique:");
                    diag.DiagnosticMessages.Add("      - Que el PC industrial esté encendido");
                    diag.DiagnosticMessages.Add("      - Conectividad de red (cable, switch)");
                    diag.DiagnosticMessages.Add("      - Firewall no bloquea ICMP");
                }
            }
            catch (Exception pingEx)
            {
                diag.DiagnosticMessages.Add($"⚠️ No se pudo hacer ping: {pingEx.Message}");
            }

            // Paso 3: Verificar TwinCAT.Ads
            try
            {
                var testType = typeof(TwinCAT.Ads.AdsClient);
                diag.TwinCATAdsInstalled = true;
                diag.DiagnosticMessages.Add("✅ TwinCAT.Ads library disponible");
            }
            catch (Exception ex)
            {
                diag.TwinCATAdsInstalled = false;
                diag.DiagnosticMessages.Add($"❌ TwinCAT.Ads library no disponible: {ex.Message}");
                diag.Summary = "TwinCAT.Ads library no está disponible. Instale TwinCAT XAE.";
                return diag;
            }

            // Paso 4: Parsear NetId
            TwinCAT.Ads.AmsNetId netId;
            try
            {
                netId = new TwinCAT.Ads.AmsNetId(_config.EtherCATMasterNetId);
                diag.NetIdValid = true;
                diag.DiagnosticMessages.Add($"✅ NetId parseado correctamente: {netId}");
            }
            catch (Exception ex)
            {
                diag.NetIdValid = false;
                diag.NetIdParseError = ex.Message;
                diag.DiagnosticMessages.Add($"❌ NetId inválido '{_config.EtherCATMasterNetId}': {ex.Message}");
                diag.DiagnosticMessages.Add("   Formato esperado: x.x.x.x.x.x (ej: 192.168.1.151.1.1)");
                diag.Summary = $"NetId inválido: {_config.EtherCATMasterNetId}";
                return diag;
            }

            // Paso 5: Probar múltiples puertos ADS
            var portsToTest = new[]
            {
                (AmsPort.PlcRuntime_851, "PLC Runtime 1 (851)"),
                (AmsPort.PlcRuntime_852, "PLC Runtime 2 (852)"),
                (AmsPort.R0_IO, "I/O (R0_IO)"),
                ((AmsPort)10000, "TC3 Router (10000)"),
            };

            diag.DiagnosticMessages.Add("");
            diag.DiagnosticMessages.Add($"🔍 Probando conexión ADS a NetId {netId} (IP: {targetIpAddress})...");
            diag.DiagnosticMessages.Add($"   ⚠️ NOTA: Requiere ruta ADS configurada en TwinCAT Router");

            AdsClient? workingClient = null;
            AmsPort workingPort = AmsPort.PlcRuntime_851;
            
            foreach (var (port, portName) in portsToTest)
            {
                AdsClient? testClient = null;
                try
                {
                    testClient = new AdsClient();
                    
                    // Conectar usando NetId + Puerto (requiere ruta ADS configurada)
                    testClient.Connect(netId, port);
                    
                    // El Connect() no falla aunque no haya ruta, hay que intentar leer algo
                    var state = testClient.ReadState();
                    
                    diag.DiagnosticMessages.Add($"   ✅ Puerto {portName}: CONECTADO (State: {state.AdsState})");
                    diag.ConnectionSuccessful = true;
                    diag.StateReadSuccessful = true;
                    diag.AdsState = state.AdsState.ToString();
                    diag.DeviceState = state.DeviceState.ToString();
                    
                    workingClient = testClient;
                    workingPort = port;
                    testClient = null; // No dispose, lo usamos
                    break;
                }
                catch (AdsErrorException adsEx)
                {
                    var errorInfo = adsEx.ErrorCode == AdsErrorCode.TargetMachineNotFound 
                        ? "Target no encontrado" 
                        : adsEx.ErrorCode == AdsErrorCode.TargetPortNotFound
                            ? "Puerto no existe"
                            : adsEx.ErrorCode.ToString();
                    diag.DiagnosticMessages.Add($"   ❌ Puerto {portName}: {errorInfo}");
                }
                catch (Exception ex)
                {
                    diag.DiagnosticMessages.Add($"   ❌ Puerto {portName}: {ex.Message}");
                }
                finally
                {
                    testClient?.Dispose();
                }
            }

            diag.DiagnosticMessages.Add("");

            if (workingClient != null)
            {
                diag.AdsClientCreated = true;
                diag.DiagnosticMessages.Add($"✅ Conexión exitosa en puerto {workingPort}");

                // Leer info del dispositivo
                try
                {
                    var deviceInfo = workingClient.ReadDeviceInfo();
                    diag.DeviceName = deviceInfo.Name;
                    diag.DeviceVersion = $"{deviceInfo.Version.Version}.{deviceInfo.Version.Revision}.{deviceInfo.Version.Build}";
                    diag.DiagnosticMessages.Add($"✅ Dispositivo: {deviceInfo.Name} v{diag.DeviceVersion}");
                }
                catch (Exception ex)
                {
                    diag.DiagnosticMessages.Add($"⚠️ No se pudo leer info del dispositivo: {ex.Message}");
                }

                workingClient.Dispose();
                
                diag.OverallSuccess = true;
                diag.Summary = $"✅ Conexión exitosa a {_config.EtherCATMasterNetId} puerto {workingPort}. Estado: {diag.AdsState}";
            }
            else
            {
                diag.AdsClientCreated = true;
                diag.ConnectionSuccessful = false;
                diag.ConnectionError = "No se pudo conectar a ningún puerto ADS";
                
                diag.DiagnosticMessages.Add("❌ NO se pudo conectar a ningún puerto ADS");
                diag.DiagnosticMessages.Add("");
                diag.DiagnosticMessages.Add("💡 POSIBLES CAUSAS:");
                diag.DiagnosticMessages.Add("   1. La RUTA ADS no está configurada en este PC");
                diag.DiagnosticMessages.Add("      → Abra TwinCAT XAE → System → Routes → Add Route");
                diag.DiagnosticMessages.Add($"      → Añada ruta a {_config.EtherCATMasterNetId}");
                diag.DiagnosticMessages.Add("");
                diag.DiagnosticMessages.Add("   2. TwinCAT NO está corriendo en el PC remoto");
                diag.DiagnosticMessages.Add("      → Verifique que TwinCAT esté en RUN en el PC industrial");
                diag.DiagnosticMessages.Add("");
                diag.DiagnosticMessages.Add("   3. El PC remoto NO tiene ruta de vuelta");
                diag.DiagnosticMessages.Add("      → En el PC industrial, añada ruta ADS hacia este PC");
                diag.DiagnosticMessages.Add("");
                diag.DiagnosticMessages.Add("   4. Firewall bloqueando puerto ADS (48898 TCP/UDP)");
                diag.DiagnosticMessages.Add("      → Verifique reglas de firewall en ambos PCs");
                
                diag.OverallSuccess = false;
                diag.Summary = "❌ No hay comunicación ADS. Verifique rutas ADS y estado de TwinCAT.";
            }

            _logger.LogInformation("🔍 Diagnóstico completado: {Summary}", diag.Summary);
            return diag;
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
                var topology = await GetTopologyAsync(rescan: false);
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

        public async Task<EtherCATTopology> GetTopologyAsync(bool rescan = false)
        {
            if (!IsEnabled)
            {
                return new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = "EtherCAT diagnostics not enabled in Excel configuration",
                    Timestamp = DateTime.Now,
                    IsSimulated = _useSimulatedPlc
                };
            }

            // Si no es rescan, verificar cache primero
            if (!rescan)
            {
                lock (_cacheLock)
                {
                    if (_cachedTopology != null &&
                        (DateTime.Now - _cacheTimestamp).TotalMilliseconds < _config.TopologyReadIntervalMs)
                    {
                        _logger.LogDebug("🌐 Returning cached EtherCAT topology");
                        return _cachedTopology;
                    }
                }
            }
            else
            {
                // Si es rescan, invalidar cache
                InvalidateCache();
            }

            _logger.LogInformation("🌐 Reading EtherCAT topology from Master... (UseSimulatedPlc: {Simulated}, Rescan: {Rescan})", _useSimulatedPlc, rescan);

            try
            {
                // Inicializar cliente ADS si es necesario
                bool connected = await EnsureConnectedAsync();
                
                if (!connected)
                {
                    // ⭐ Si UseSimulatedPlc=false, NO simular, mostrar error real
                    if (!IsSimulatedMode)
                    {
                        _logger.LogWarning("🌐 UseSimulatedPlc=FALSE: No hay conexión con EtherCAT Master - NO se usarán datos simulados");
                        return CreateErrorTopology($"No hay comunicación con el Master EtherCAT ({_config.EtherCATMasterNetId}). Verifique la conexión de red y el estado del PLC.");
                    }
                    
                    // UseSimulatedPlc=true: usar datos simulados
                    _logger.LogWarning("🌐 UseSimulatedPlc=TRUE: No hay conexión - Usando datos SIMULADOS");
                    return await CreateSimulatedTopologyAsync();
                }

                var topology = new EtherCATTopology
                {
                    Timestamp = DateTime.Now,
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
                IsSimulated = true,  // ⚠️ Marcamos como SIMULADO
                HasCommunicationError = false
            };

            // Master simulado
            topology.Master = new EtherCATMaster
            {
                NetId = _config.EtherCATMasterNetId,
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
                if (IsSimulatedMode)
                {
                    _logger.LogWarning("⚠️ UseSimulatedPlc=TRUE: No hay esclavos reales, usando datos SIMULADOS");
                    slaves = GenerateSimulatedSlaves();
                    isSimulated = true;
                }
                else
                {
                    _logger.LogWarning("⚠️ UseSimulatedPlc=FALSE: No se detectaron esclavos EtherCAT en el bus");
                    // En modo real, retornamos lista vacía - no simulamos
                    slaves = new List<EtherCATSlaveNode>();
                    isSimulated = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading slaves");
                
                if (IsSimulatedMode)
                {
                    _logger.LogWarning("⚠️ UseSimulatedPlc=TRUE: Error de lectura, usando datos SIMULADOS");
                    slaves = GenerateSimulatedSlaves();
                    isSimulated = true;
                }
                else
                {
                    _logger.LogError("✕ UseSimulatedPlc=FALSE: Error de comunicación con bus EtherCAT");
                    throw; // Re-lanzar en modo real
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
                
                // Determinar IP para logging
                string targetIpAddress = !string.IsNullOrWhiteSpace(_config.EtherNETIdTwincat)
                    ? _config.EtherNETIdTwincat
                    : "(extraer de NetId)";
                
                // ⭐ ESTRATEGIA: Leer desde el PLC Runtime (puerto 851) usando FB_EtherCATDiag
                // En lugar de conectar directamente al EtherCAT Master (puerto 27905)
                const int PlcRuntimePort = 851;
                
                // Conectar al PLC Runtime donde está instanciado FB_EtherCATDiag
                _logger.LogInformation("🌐 Conectando a PLC {NetId}:{Port} (IP: {IP}), FB: {FB}...", 
                    netId, PlcRuntimePort, targetIpAddress, _config.EtherCATDiagFbInstance);
                _masterClient.Connect(netId, (AmsPort)PlcRuntimePort);

                // Verificar conexión
                var state = _masterClient.ReadState();
                _isInitialized = state.AdsState == AdsState.Run || state.AdsState == AdsState.Config;

                _logger.LogInformation("🌐 Conectado al PLC {NetId}:{Port}, State: {State}",
                    _config.EtherCATMasterNetId, PlcRuntimePort, state.AdsState);

                return _isInitialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot connect to EtherCAT Master at {NetId}. Verifique que existe ruta ADS configurada.",
                    _config.EtherCATMasterNetId);
                return false;
            }
        }

        private async Task<EtherCATMaster> ReadMasterInfoAsync()
        {
            var master = new EtherCATMaster
            {
                NetId = _config.EtherCATMasterNetId,
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
        // que controla correctamente la simulación según UseSimulatedPlc

        /// <summary>
        /// Lee esclavos desde FB_EtherCATDiag.arrSlaveInfo vía variables PLC (puerto 851)
        /// Esta es la estrategia correcta: usar el FB de Beckhoff en lugar de Index Groups directos
        /// </summary>
        private async Task<List<EtherCATSlaveNode>> TryReadSlavesFromPlcVariablesAsync()
        {
            var slaves = new List<EtherCATSlaveNode>();

            if (_masterClient == null || !_masterClient.IsConnected)
                return slaves;

            var fbInstance = _config.EtherCATDiagFbInstance;
            _logger.LogInformation("🔍 Leyendo esclavos desde {FB}.arrSlaveInfo (puerto 851)", fbInstance);

            try
            {
                // =====================================================
                // ESTRATEGIA: Leer desde FB_EtherCATDiag.arrSlaveInfo
                // El FB de Beckhoff ya hace todo el trabajo de diagnóstico
                // Solo necesitamos leer sus variables de salida
                // =====================================================

                // Primero verificar si el FB está activo
                bool fbOk = false;
                try
                {
                    var handle = _masterClient.CreateVariableHandle($"{fbInstance}.bEtherCATOK");
                    var buffer = new byte[1];
                    _masterClient.Read(handle, buffer.AsMemory());
                    _masterClient.DeleteVariableHandle(handle);
                    fbOk = buffer[0] != 0;
                    _logger.LogDebug("✅ {FB}.bEtherCATOK = {Value}", fbInstance, fbOk);
                }
                catch (AdsErrorException ex)
                {
                    _logger.LogWarning("❌ No se pudo leer {FB}.bEtherCATOK: {Error}", fbInstance, ex.ErrorCode);
                    _logger.LogWarning("   Verifique que existe la instancia '{FB}' en el PLC", fbInstance);
                    return slaves;
                }

                // Leer número de esclavos
                ushort slaveCount = 0;
                try
                {
                    var handle = _masterClient.CreateVariableHandle($"{fbInstance}.iNumOfSlavesRead");
                    var buffer = new byte[2];
                    _masterClient.Read(handle, buffer.AsMemory());
                    _masterClient.DeleteVariableHandle(handle);
                    slaveCount = BitConverter.ToUInt16(buffer, 0);
                    _logger.LogInformation("✅ {FB}.iNumOfSlavesRead = {Count}", fbInstance, slaveCount);
                }
                catch (AdsErrorException ex)
                {
                    _logger.LogDebug("⚠️ No se pudo leer iNumOfSlavesRead: {Error}, usando tamaño de array", ex.ErrorCode);
                    slaveCount = 256; // Tamaño máximo del array por defecto
                }

                if (slaveCount == 0)
                {
                    _logger.LogInformation("ℹ️ No hay esclavos reportados por FB_EtherCATDiag");
                    return slaves;
                }

                // =====================================================
                // Leer el array arrSlaveInfo (ARRAY[0..256] OF ST_SlaveStateInfo)
                // =====================================================
                const int maxArrayElements = 257; // ARRAY[0..256]
                const int maxElementSize = 300;   // Tamaño máximo estimado por elemento
                
                try
                {
                    var handle = _masterClient.CreateVariableHandle($"{fbInstance}.arrSlaveInfo");
                    var buffer = new byte[maxArrayElements * maxElementSize];
                    var bytesRead = _masterClient.Read(handle, buffer.AsMemory());
                    _masterClient.DeleteVariableHandle(handle);

                    _logger.LogDebug("✅ Leídos {Bytes} bytes de arrSlaveInfo", bytesRead);

                    // Detectar tamaño real de cada elemento buscando nECAddr consecutivos
                    // ST_SlaveStateInfo tiene nECAddr en offset ~247-248 (después de nIndex + sName + sType + sESIfile)
                    var (actualSlaveSize, detectedNECAddrOffset) = DetectSlaveInfoSize(buffer, bytesRead);
                    
                    if (actualSlaveSize == 0)
                    {
                        _logger.LogWarning("⚠️ No se pudo detectar el tamaño de ST_SlaveStateInfo");
                        return slaves;
                    }

                    _logger.LogDebug("📏 Tamaño detectado de ST_SlaveStateInfo: {Size} bytes, nECAddr offset: {Offset}", 
                        actualSlaveSize, detectedNECAddrOffset);

                    // Parsear cada esclavo
                    for (int i = 0; i < slaveCount && i < 100; i++)
                    {
                        var offset = i * actualSlaveSize;
                        if (offset + actualSlaveSize > bytesRead)
                            break;

                        var slave = ParseSlaveStateInfo(buffer, offset, i, actualSlaveSize, detectedNECAddrOffset);
                        if (slave != null)
                        {
                            // Enriquecer con datos de ESI si está habilitado
                            // ⭐ Ahora pasa también el ESIFileName del PLC
                            EnrichSlaveFromESI(slave);
                            slaves.Add(slave);
                        }
                    }

                    _logger.LogInformation("✅ Parseados {Count} esclavos desde {FB}.arrSlaveInfo", slaves.Count, fbInstance);
                }
                catch (AdsErrorException ex)
                {
                    _logger.LogWarning("❌ Error leyendo arrSlaveInfo: {Error} (Code: {Code})", ex.Message, ex.ErrorCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error en TryReadSlavesFromPlcVariablesAsync: {Error}", ex.Message);
            }

            return slaves;
        }

        /// <summary>
        /// Extrae un string terminado en nulo desde un array de bytes.
        /// Trunca en el primer byte nulo encontrado.
        /// </summary>
        private static string ExtractNullTerminatedString(byte[] bytes)
        {
            int nullIndex = Array.IndexOf(bytes, (byte)0);
            int length = nullIndex >= 0 ? nullIndex : bytes.Length;
            return System.Text.Encoding.ASCII.GetString(bytes, 0, length).Trim();
        }

        /// <summary>
        /// Detecta el tamaño real de ST_SlaveStateInfo buscando nECAddr consecutivos
        /// ⭐ ESTRATEGIA: Busca 3 valores consecutivos de nECAddr (N, N+1, N+2) para confirmar el tamaño
        /// ⭐ Devuelve (size, nECAddrOffset) - el offset detectado de nECAddr dentro de cada estructura
        /// </summary>
        private (int Size, int NECAddrOffset) DetectSlaveInfoSize(byte[] buffer, int bytesRead)
        {
            _logger.LogDebug("🔍 Detectando tamaño de ST_SlaveStateInfo... (buffer: {Bytes} bytes)", bytesRead);
            
            // Posibles offsets donde podría estar nECAddr dentro de la estructura:
            // - 247 = con sESIfile: nIndex(4) + sName(81) + sType(81) + sESIfile(81) = 247
            // - 166 = sin sESIfile: nIndex(4) + sName(81) + sType(81) = 166
            int[] possibleNECAddrOffsets = { 247, 166 };
            
            // Posibles tamaños de estructura a probar
            int[] possibleSizes = { 292, 290, 288, 296, 294, 212, 210, 208, 214 };
            
            foreach (var nECAddrOffset in possibleNECAddrOffsets)
            {
                foreach (var testSize in possibleSizes)
                {
                    // Necesitamos al menos 3 estructuras para verificar
                    if (nECAddrOffset + (testSize * 3) > bytesRead)
                        continue;
                    
                    // Leer 3 valores de nECAddr consecutivos
                    var addr1 = BitConverter.ToUInt16(buffer, nECAddrOffset);
                    var addr2 = BitConverter.ToUInt16(buffer, nECAddrOffset + testSize);
                    var addr3 = BitConverter.ToUInt16(buffer, nECAddrOffset + (testSize * 2));
                    
                    // Verificar que son válidos (rango 1001-1256) y consecutivos
                    if (addr1 >= 1001 && addr1 <= 1256 &&
                        addr2 == addr1 + 1 &&
                        addr3 == addr1 + 2)
                    {
                        _logger.LogInformation("✅ Patrón detectado: nECAddr en offset {Offset}, tamaño estructura = {Size} bytes",
                            nECAddrOffset, testSize);
                        _logger.LogDebug("   Valores: {A1}, {A2}, {A3}", addr1, addr2, addr3);
                        return (testSize, nECAddrOffset);
                    }
                }
            }
            
            // Si no encontramos el patrón, hacer un escaneo más exhaustivo
            _logger.LogWarning("⚠️ Patrón estándar no encontrado, haciendo escaneo exhaustivo...");
            
            // Buscar cualquier secuencia de 3 valores consecutivos en rango 1001-1256
            for (int startOffset = 0; startOffset < Math.Min(500, bytesRead - 6); startOffset += 2)
            {
                var val = BitConverter.ToUInt16(buffer, startOffset);
                if (val >= 1001 && val <= 1256)
                {
                    // Encontramos un posible nECAddr, buscar el siguiente
                    for (int testSize = 200; testSize <= 320; testSize += 2)
                    {
                        if (startOffset + (testSize * 2) + 2 > bytesRead)
                            break;
                            
                        var val2 = BitConverter.ToUInt16(buffer, startOffset + testSize);
                        var val3 = BitConverter.ToUInt16(buffer, startOffset + (testSize * 2));
                        
                        if (val2 == val + 1 && val3 == val + 2)
                        {
                            _logger.LogInformation("✅ Escaneo: nECAddr encontrado en offset {Offset}, tamaño = {Size} bytes",
                                startOffset, testSize);
                            return (testSize, startOffset);
                        }
                    }
                }
            }
            
            // Último recurso: usar el tamaño por defecto
            _logger.LogWarning("⚠️ No se pudo detectar tamaño, usando valores por defecto: Size={Size}, Offset={Offset}", 
                ST_SlaveStateInfo_Parsed.Size, ST_SlaveStateInfo_Parsed.Offset_nECAddr);
            return (ST_SlaveStateInfo_Parsed.Size, ST_SlaveStateInfo_Parsed.Offset_nECAddr); // 290 bytes, offset 247
        }

        /// <summary>
        /// Parsea un ST_SlaveStateInfo desde el buffer
        /// ⭐ Estructura según XML del PLC:
        ///    nIndex, sName, sType, sESIfile, nECAddr, bDiagData, stPortCRCErrors, nSumCRCErrors, stState
        /// </summary>
        /// <param name="nECAddrOffset">Offset detectado dinámicamente de nECAddr dentro de la estructura</param>
        private EtherCATSlaveNode? ParseSlaveStateInfo(byte[] buffer, int offset, int index, int slaveSize, int nECAddrOffset)
        {
            try
            {
                // Estructura ST_SlaveStateInfo según PLC:
                // nIndex:         DINT (4 bytes)        → offset 0
                // sName:          STRING(80) (81 bytes) → offset 4
                // sType:          STRING(80) (81 bytes) → offset 85
                // sESIfile:       STRING(80) (81 bytes) → offset 166 ⭐ 
                // nECAddr:        UINT (2 bytes)        → offset 247
                // bDiagData:      BOOL (1 byte)         → offset 249
                // stPortCRCErrors: ST_EcCrcErrorEx (16) → offset ~252
                // nSumCRCErrors:  UDINT (4 bytes)       → offset ~268
                // stState:        ST_SlaveState (16)    → offset ~272

                var nIndex = BitConverter.ToInt32(buffer, offset + ST_SlaveStateInfo_Parsed.Offset_nIndex);
                
                // Leer sName (STRING 80 + null terminator)
                var nameBytes = new byte[81];
                Array.Copy(buffer, offset + ST_SlaveStateInfo_Parsed.Offset_sName, nameBytes, 0, 81);
                var name = ExtractNullTerminatedString(nameBytes);

                // Leer sType (STRING 80 + null terminator)  
                var typeBytes = new byte[81];
                Array.Copy(buffer, offset + ST_SlaveStateInfo_Parsed.Offset_sType, typeBytes, 0, 81);
                var deviceType = ExtractNullTerminatedString(typeBytes);

                // ⭐ Leer sESIfile (STRING 80 + null terminator) - offset 166
                string esiFileName = "";
                if (offset + ST_SlaveStateInfo_Parsed.Offset_sESIfile + 81 <= buffer.Length)
                {
                    var esiBytes = new byte[81];
                    Array.Copy(buffer, offset + ST_SlaveStateInfo_Parsed.Offset_sESIfile, esiBytes, 0, 81);
                    esiFileName = ExtractNullTerminatedString(esiBytes);
                }

                // nECAddr en offset 247
                // ⭐ Usar el offset detectado dinámicamente, no el hardcodeado
                var nECAddr = BitConverter.ToUInt16(buffer, offset + nECAddrOffset);
                
                // 🔍 DEBUG: Log detallado para diagnóstico de posiciones faltantes
                _logger.LogDebug("  [{Index}] Parseando: offset={Offset}, nECAddr={ECAddr}, name='{Name}', type='{Type}'", 
                    index, offset, nECAddr, name, deviceType);
                
                // Si nECAddr es 0, saltar este esclavo (entrada vacía en el array)
                // NOTA: No filtrar por < 1001 porque dispositivos como YASKAWA pueden tener direcciones bajas (1024+)
                if (nECAddr == 0)
                {
                    _logger.LogWarning("  [{Index}] ⚠️ Saltado: nECAddr=0 (entrada vacía). Nombre='{Name}', Tipo='{Type}'", 
                        index, name, deviceType);
                    return null;
                }

                // ⭐ Calcular offsets relativos basándose en el offset detectado de nECAddr
                // Los offsets después de nECAddr son relativos a su posición:
                // nECAddr(2) + bDiagData(1) + padding(2) = 5 bytes hasta stPortCRCErrors
                // stPortCRCErrors(16) + nSumCRCErrors(4) = 20 bytes hasta stState
                int bDiagDataOffset = nECAddrOffset + 2;              // nECAddr es 2 bytes
                int stPortCRCErrorsOffset = nECAddrOffset + 5;        // +2 (nECAddr) +1 (bDiagData) +2 (padding)
                int nSumCRCErrorsOffset = nECAddrOffset + 21;         // +5 + 16 (stPortCRCErrors)
                int stStateOffset = nECAddrOffset + 25;               // +21 + 4 (nSumCRCErrors)

                // bDiagData - ahora con offset dinámico
                var bDiagData = buffer[offset + bDiagDataOffset] != 0;

                // nSumCRCErrors - ahora con offset dinámico
                uint nSumCRCErrors = 0;
                if (offset + nSumCRCErrorsOffset + 4 <= buffer.Length)
                {
                    nSumCRCErrors = BitConverter.ToUInt32(buffer, offset + nSumCRCErrorsOffset);
                }

                // stState - ahora con offset dinámico
                EtherCATState state = EtherCATState.Init;
                bool portAActive = false, portBActive = false, portCActive = false, portDActive = false;
                int stateOffsetAbs = offset + stStateOffset;
                if (stateOffsetAbs + ST_SlaveState.Size <= buffer.Length)
                {
                    var stateValue = buffer[stateOffsetAbs] & 0x0F;
                    state = stateValue switch
                    {
                        1 => EtherCATState.Init,
                        2 => EtherCATState.PreOp,
                        3 => EtherCATState.Bootstrap,
                        4 => EtherCATState.SafeOp,
                        8 => EtherCATState.Operational,
                        _ => EtherCATState.Unknown
                    };
                    
                    // ⭐ Extraer flags de puertos activos de stState (offsets 12-15 dentro de ST_SlaveState)
                    portAActive = buffer[stateOffsetAbs + 12] != 0;  // bPortA
                    portBActive = buffer[stateOffsetAbs + 13] != 0;  // bPortB
                    portCActive = buffer[stateOffsetAbs + 14] != 0;  // bPortC
                    portDActive = buffer[stateOffsetAbs + 15] != 0;  // bPortD
                }

                // ⭐ Determinar tipo físico y número de puertos basado en deviceType
                var (physicalType, portCount) = DetermineDevicePhysicalType(deviceType);
                
                // ⭐ Generar información de puertos basada en datos reales del PLC
                // IMPORTANTE: Pasar physicalType para generar correctamente el tipo de conector
                var ports = GeneratePortsFromPLCData(portCount, portAActive, portBActive, portCActive, portDActive, physicalType);
                byte activePortsBitmap = (byte)(
                    (portAActive ? 0x01 : 0) |
                    (portBActive ? 0x02 : 0) |
                    (portCActive ? 0x04 : 0) |
                    (portDActive ? 0x08 : 0)
                );

                var slave = new EtherCATSlaveNode
                {
                    Position = (ushort)(index + 1),
                    ConfiguredAddress = nECAddr,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Slave {nECAddr}" : name,
                    DeviceType = deviceType,
                    State = state,
                    Health = state == EtherCATState.Operational ? NodeHealth.Healthy : 
                             state == EtherCATState.SafeOp ? NodeHealth.Warning : NodeHealth.Error,
                    DiagnosticsAvailable = bDiagData,
                    ErrorCount = (int)nSumCRCErrors,
                    ESIFileName = esiFileName,  // ⭐ Nombre del archivo ESI (para no-Beckhoff)
                    // ⭐ Información de puertos
                    Ports = ports,
                    ActivePortsBitmap = activePortsBitmap,
                    ActivePortCount = ports.Count(p => p.HasCommunication),
                    PhysicalType = physicalType
                };

                // Log con info de ESI si está especificado
                if (!string.IsNullOrWhiteSpace(esiFileName))
                {
                    _logger.LogDebug("  [{Index}] {Name} (ECAddr:{Addr}, State:{State}, ESI:'{ESI}')", 
                        index, slave.Name, nECAddr, state, esiFileName);
                }
                else
                {
                    _logger.LogDebug("  [{Index}] {Name} (ECAddr:{Addr}, State:{State})", 
                        index, slave.Name, nECAddr, state);
                }

                return slave;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error parseando esclavo {Index}: {Error}", index, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Enriquece datos del esclavo con información de ESI files
        /// ⭐ MEJORADO: Si el esclavo tiene ESIFileName especificado, busca en ese archivo primero
        ///    Esto es útil para dispositivos no-Beckhoff (ej: variadores Yaskawa)
        /// </summary>
        private void EnrichSlaveFromESI(EtherCATSlaveNode slave)
        {
            if (!_config.UseESIFiles)
                return;

            try
            {
                ESIDeviceInfo? esiInfo = null;
                
                // ⭐ NUEVO: Si el PLC especificó un archivo ESI (sESIfile), buscarlo ahí primero
                if (!string.IsNullOrWhiteSpace(slave.ESIFileName))
                {
                    _logger.LogDebug("🔍 Buscando ESI para '{Name}' en archivo especificado: '{ESIFile}'", 
                        slave.Name, slave.ESIFileName);
                    
                    esiInfo = _esiParser.GetDeviceInfoFromESIFile(slave.ESIFileName, slave.DeviceType);
                    
                    if (esiInfo != null)
                    {
                        _logger.LogInformation("✅ ESI encontrado para '{Name}' en '{ESIFile}': {ProductName} ({VendorName})", 
                            slave.Name, slave.ESIFileName, esiInfo.ProductName, esiInfo.VendorName);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No se encontró ESI en archivo '{ESIFile}' para '{Name}' (type: {Type})", 
                            slave.ESIFileName, slave.Name, slave.DeviceType);
                    }
                }
                
                // Si no se encontró por ESIFileName, intentar por VendorId/ProductCode (Beckhoff y otros)
                if (esiInfo == null && slave.VendorId != 0)
                {
                    esiInfo = _esiParser.GetDeviceInfo(slave.VendorId, slave.ProductCode);
                }
                
                // Si aún no se encontró, intentar por DeviceType (sType)
                if (esiInfo == null && !string.IsNullOrWhiteSpace(slave.DeviceType))
                {
                    esiInfo = _esiParser.GetDeviceInfoByType(slave.DeviceType);
                }
                
                // Aplicar información del ESI si se encontró
                if (esiInfo != null)
                {
                    if (string.IsNullOrWhiteSpace(slave.Name) || slave.Name.StartsWith("Slave "))
                        slave.Name = esiInfo.ProductName;
                    if (string.IsNullOrWhiteSpace(slave.Description))
                        slave.Description = esiInfo.Description;
                    if (string.IsNullOrWhiteSpace(slave.VendorName))
                        slave.VendorName = esiInfo.VendorName;
                    if (slave.VendorId == 0)
                        slave.VendorId = esiInfo.VendorId;
                    if (slave.ProductCode == 0)
                        slave.ProductCode = esiInfo.ProductCode;
                    if (string.IsNullOrWhiteSpace(slave.ImageUrl) && !string.IsNullOrWhiteSpace(esiInfo.ImageFile))
                        slave.ImageUrl = esiInfo.ImageFile;
                    
                    // ⭐ NUEVO: Aplicar información de puertos del ESI
                    if (esiInfo.PortPhysics != null && esiInfo.PortPhysics.Count > 0)
                    {
                        EnrichPortsFromESI(slave, esiInfo);
                        _logger.LogDebug("  🔌 Puertos ESI para '{Name}': {Physics} ({Count} puertos definidos)", 
                            slave.Name, esiInfo.PhysicsRaw, esiInfo.PortPhysics.Count(p => p.PhysicsType != "NotImplemented"));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error obteniendo ESI para esclavo {Name}: {Error}", slave.Name, ex.Message);
            }
        }

        /// <summary>
        /// ⭐ Enriquece los puertos del esclavo con información del archivo ESI
        /// Esto permite determinar correctamente qué tipo de conector tiene cada puerto
        /// </summary>
        private void EnrichPortsFromESI(EtherCATSlaveNode slave, ESIDeviceInfo esiInfo)
        {
            if (esiInfo.PortPhysics == null || esiInfo.PortPhysics.Count == 0)
                return;

            // Si el slave no tiene puertos definidos, crearlos basándose en el ESI
            if (slave.Ports == null || slave.Ports.Count == 0)
            {
                slave.Ports = new List<EtherCATPort>();
                for (int i = 0; i < esiInfo.PortPhysics.Count; i++)
                {
                    var portPhysics = esiInfo.PortPhysics[i];
                    if (portPhysics.PhysicsType == "NotImplemented")
                        continue;
                    
                    slave.Ports.Add(new EtherCATPort
                    {
                        PortNumber = (byte)i,
                        Type = portPhysics.PhysicsType == "MII" ? PortType.MII : PortType.EBUS,
                        Physics = portPhysics.PhysicsType switch
                        {
                            "EBUS" => PortPhysics.EBus,
                            "MII" => PortPhysics.Ethernet,
                            "LVDS" => PortPhysics.LVDS,
                            _ => PortPhysics.Unknown
                        },
                        HasCommunication = false,
                        LinkUp = false,
                        IsOpen = false,
                        Health = LinkHealth.Unknown
                    });
                }
            }
            else
            {
                // Actualizar física de puertos existentes con info del ESI
                foreach (var port in slave.Ports)
                {
                    if (port.PortNumber < esiInfo.PortPhysics.Count)
                    {
                        var esiPort = esiInfo.PortPhysics[port.PortNumber];
                        port.Physics = esiPort.PhysicsType switch
                        {
                            "EBUS" => PortPhysics.EBus,
                            "MII" => PortPhysics.Ethernet,
                            "LVDS" => PortPhysics.LVDS,
                            _ => port.Physics  // Mantener el valor actual si no se reconoce
                        };
                        port.Type = esiPort.PhysicsType == "MII" ? PortType.MII : PortType.EBUS;
                    }
                }
            }

            // Determinar PhysicalType del esclavo basándose en los puertos
            var hasEBus = slave.Ports.Any(p => p.Physics == PortPhysics.EBus);
            var hasEthernet = slave.Ports.Any(p => p.Physics == PortPhysics.Ethernet);
            
            if (hasEBus && hasEthernet)
                slave.PhysicalType = PhysicalType.Mixed;
            else if (hasEthernet)
                slave.PhysicalType = PhysicalType.EthernetOnly;
            else if (hasEBus)
                slave.PhysicalType = PhysicalType.EBusOnly;
        }

        /// <summary>
        /// Lee información de un esclavo por su índice usando Index Groups TwinCAT 3
        /// (Método legacy, mantenido como fallback)
        /// </summary>
        /// </summary>
        private async Task<EtherCATSlaveNode?> ReadSlaveInfoByIndexAsync(ushort slaveIndex, uint masterIndexGroup)
        {
            if (_masterClient == null) return null;

            try
            {
                // Calcular offset para la identidad del esclavo
                var identityOffset = EcAdsIndexGroups.GetSlaveIdentityOffset(slaveIndex);
                
                var infoBuffer = new byte[16]; // ST_EcSlaveIdentity: 4x UINT32
                
                _masterClient.Read(masterIndexGroup, identityOffset, infoBuffer.AsMemory());
                
                // Parsear ST_EcSlaveIdentity
                var vendorId = BitConverter.ToUInt32(infoBuffer, 0);
                var productCode = BitConverter.ToUInt32(infoBuffer, 4);
                var revisionNumber = BitConverter.ToUInt32(infoBuffer, 8);
                var serialNumber = BitConverter.ToUInt32(infoBuffer, 12);
                
                // Si todos son 0, puede ser un esclavo no válido
                if (vendorId == 0 && productCode == 0)
                {
                    _logger.LogDebug("Esclavo {Index}: identidad vacía, ignorando", slaveIndex);
                    return null;
                }

                var slave = new EtherCATSlaveNode
                {
                    ConfiguredAddress = (ushort)(1001 + slaveIndex),
                    VendorId = vendorId,
                    ProductCode = productCode,
                    RevisionNumber = revisionNumber,
                    SerialNumber = serialNumber,
                    State = EtherCATState.Operational,
                    Health = NodeHealth.Healthy
                };

                // Intentar leer estado AL del esclavo
                try
                {
                    var stateOffset = EcAdsIndexGroups.GetSlaveStateOffset(slaveIndex);
                    var stateBuffer = new byte[2];
                    _masterClient.Read(masterIndexGroup, stateOffset, stateBuffer.AsMemory());
                    slave.State = (EtherCATState)(stateBuffer[0] & 0x0F);
                    
                    // Determinar salud basada en estado
                    slave.Health = slave.State switch
                    {
                        EtherCATState.Operational => NodeHealth.Healthy,
                        EtherCATState.SafeOp => NodeHealth.Warning,
                        EtherCATState.PreOp => NodeHealth.Warning,
                        EtherCATState.Init => NodeHealth.Warning,
                        _ => NodeHealth.Error
                    };
                }
                catch
                {
                    // Si no podemos leer estado, asumimos OP
                }

                // Obtener nombre desde ESI
                var esiInfo = _esiParser.GetDeviceInfo(vendorId, productCode);
                if (esiInfo != null)
                {
                    slave.Name = esiInfo.ProductName;
                    slave.Description = esiInfo.Description;
                    slave.DeviceType = esiInfo.Type;
                    slave.VendorName = esiInfo.VendorName;
                }
                else
                {
                    slave.Name = GetGenericDeviceName(vendorId, productCode);
                    slave.Description = $"VID: 0x{vendorId:X8}, PID: 0x{productCode:X8}";
                }

                _logger.LogDebug("✓ Esclavo {Index}: {Name} (VID:0x{VID:X4} PID:0x{PID:X8})", 
                    slaveIndex, slave.Name, vendorId, productCode);
                
                return slave;
            }
            catch (AdsErrorException adsEx)
            {
                _logger.LogDebug("Error ADS leyendo esclavo {Index}: {Error}", slaveIndex, adsEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error leyendo esclavo {Index}: {Error}", slaveIndex, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Lee información de un esclavo usando Index Groups legados
        /// </summary>
        private async Task<EtherCATSlaveNode?> ReadSlaveInfoByIndexLegacyAsync(ushort slaveIndex)
        {
            if (_masterClient == null) return null;

            try
            {
                var infoBuffer = new byte[16];
                _masterClient.Read(EcAdsIndexGroups.ECMASTER_LEGACY_SLAVEIDENTITY, slaveIndex, infoBuffer.AsMemory());
                
                var vendorId = BitConverter.ToUInt32(infoBuffer, 0);
                var productCode = BitConverter.ToUInt32(infoBuffer, 4);
                var revisionNumber = BitConverter.ToUInt32(infoBuffer, 8);
                var serialNumber = BitConverter.ToUInt32(infoBuffer, 12);
                
                if (vendorId == 0 && productCode == 0)
                    return null;

                var slave = new EtherCATSlaveNode
                {
                    ConfiguredAddress = (ushort)(1001 + slaveIndex),
                    VendorId = vendorId,
                    ProductCode = productCode,
                    RevisionNumber = revisionNumber,
                    SerialNumber = serialNumber,
                    State = EtherCATState.Operational,
                    Health = NodeHealth.Healthy
                };

                var esiInfo = _esiParser.GetDeviceInfo(vendorId, productCode);
                if (esiInfo != null)
                {
                    slave.Name = esiInfo.ProductName;
                    slave.Description = esiInfo.Description;
                    slave.DeviceType = esiInfo.Type;
                }
                else
                {
                    slave.Name = GetGenericDeviceName(vendorId, productCode);
                    slave.Description = $"VID: 0x{vendorId:X8}, PID: 0x{productCode:X8}";
                }

                return slave;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Método alternativo: Leer esclavos via Device Info del sistema TwinCAT
        /// </summary>
        private async Task<List<EtherCATSlaveNode>> TryReadSlavesViaDeviceInfoAsync()
        {
            var slaves = new List<EtherCATSlaveNode>();

            if (_masterClient == null) return slaves;

            try
            {
                _logger.LogDebug("🔍 Intentando lectura via Device Info...");

                // Obtener lista de dispositivos I/O configurados
                // Index Group 0x2100 = IO Device Info
                // Este método funciona en la mayoría de configuraciones TwinCAT 3
                
                var deviceCountBuffer = new byte[4];
                try
                {
                    // ADSIGRP_IODEVICE_COUNT = 0x00002001
                    _masterClient.Read(0x2001, 0, deviceCountBuffer.AsMemory());
                    var deviceCount = BitConverter.ToUInt16(deviceCountBuffer, 0);
                    _logger.LogDebug("Dispositivos I/O encontrados: {Count}", deviceCount);
                }
                catch
                {
                    _logger.LogDebug("No se pudo leer conteo de dispositivos I/O");
                }

                // Intentar escanear direcciones conocidas de esclavos EtherCAT
                // Las direcciones típicas van de 1001 en adelante
                ushort position = 1;
                int consecutiveFailures = 0;

                for (ushort addr = 1001; addr <= 1100 && consecutiveFailures < 5; addr++)
                {
                    try
                    {
                        // Intentar leer estado del esclavo usando Index Group específico
                        // 0x0F02XXYY donde XX = DeviceId (usamos 1 por defecto), YY = operación
                        // NOTA: Este método es legacy y no debería ejecutarse con la nueva estrategia FB_EtherCATDiag
                        var stateBuffer = new byte[2];
                        
                        // ADSIGRP_ECAT_SLAVECNT (obtener si existe esclavo)
                        uint indexGroup = 0x0F020000 | (1u << 16); // DeviceId = 1 (fijo)
                        
                        try
                        {
                            _masterClient.Read(indexGroup, addr, stateBuffer.AsMemory());
                            
                            // Si llegamos aquí, el esclavo existe
                            var slave = new EtherCATSlaveNode
                            {
                                Position = position++,
                                ConfiguredAddress = addr,
                                State = (EtherCATState)stateBuffer[0],
                                Name = $"EtherCAT Slave {addr}",
                                Health = NodeHealth.Healthy
                            };

                            slaves.Add(slave);
                            consecutiveFailures = 0;
                            _logger.LogDebug("✓ Encontrado esclavo en dirección {Addr}", addr);
                        }
                        catch (AdsErrorException adsEx) when (adsEx.ErrorCode == AdsErrorCode.DeviceSymbolNotFound ||
                                                              adsEx.ErrorCode == AdsErrorCode.DeviceInvalidOffset)
                        {
                            consecutiveFailures++;
                        }
                    }
                    catch
                    {
                        consecutiveFailures++;
                    }
                }

                if (slaves.Count > 0)
                {
                    _logger.LogInformation("🔍 Encontrados {Count} esclavos via escaneo de direcciones", slaves.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error en TryReadSlavesViaDeviceInfoAsync: {Error}", ex.Message);
            }

            return slaves;
        }

        /// <summary>
        /// Obtiene un nombre genérico para el dispositivo basado en VendorId/ProductCode
        /// </summary>
        private string GetGenericDeviceName(uint vendorId, uint productCode)
        {
            // Beckhoff = 0x00000002
            if (vendorId == 0x00000002)
            {
                // Extraer tipo de terminal del ProductCode
                // Los ProductCodes de Beckhoff suelen tener el número de terminal codificado
                var terminalType = (productCode >> 16) & 0xFFFF;
                
                // Algunos tipos conocidos
                return terminalType switch
                {
                    0x044C => "EK1100 Coupler",
                    0x0456 => "EK1110 Extension",
                    0x0462 => "EK1122 Junction",
                    0x03F0 => "EL1xxx Digital Input",
                    0x07D8 => "EL2xxx Digital Output",
                    0x0BF6 => "EL3xxx Analog Input",
                    0x0FA0 => "EL4xxx Analog Output",
                    0x1771 => "EL6xxx Communication",
                    0x1B81 => "EL7xxx Motion",
                    _ => $"Beckhoff 0x{productCode:X8}"
                };
            }

            // Otros vendors conocidos
            return vendorId switch
            {
                0x00000001 => $"EtherCAT Device 0x{productCode:X8}",
                _ => $"Vendor 0x{vendorId:X4} Product 0x{productCode:X8}"
            };
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
            if (_masterClient == null || !_masterClient.IsConnected)
                return null;

            try
            {
                // Intentar leer información del esclavo usando diferentes Index Groups
                // TwinCAT provee varios métodos dependiendo de la versión/configuración
                
                // Método 1: ADSIGRP_ECAT_SLAVE_IDENTITY = 0x0F020002
                var identityBuffer = new byte[24];
                try
                {
                    // Index Group para obtener identidad de esclavo por dirección
                    _masterClient.Read(0x0F020002, address, identityBuffer.AsMemory());
                    
                    var vendorId = BitConverter.ToUInt32(identityBuffer, 0);
                    var productCode = BitConverter.ToUInt32(identityBuffer, 4);
                    var revisionNumber = BitConverter.ToUInt32(identityBuffer, 8);
                    var serialNumber = BitConverter.ToUInt32(identityBuffer, 12);
                    
                    // Si todos son 0, probablemente el esclavo no existe
                    if (vendorId == 0 && productCode == 0)
                        return null;

                    var slave = new EtherCATSlaveNode
                    {
                        ConfiguredAddress = address,
                        VendorId = vendorId,
                        ProductCode = productCode,
                        RevisionNumber = revisionNumber,
                        SerialNumber = serialNumber,
                        State = EtherCATState.Operational,
                        Health = NodeHealth.Healthy
                    };

                    slave.Name = GetGenericDeviceName(vendorId, productCode);
                    slave.Description = $"Addr: {address}, VID: 0x{vendorId:X4}, PID: 0x{productCode:X8}";

                    _logger.LogDebug("✓ Esclavo encontrado en {Addr}: {Name}", address, slave.Name);
                    return slave;
                }
                catch (AdsErrorException adsEx) when (adsEx.ErrorCode == AdsErrorCode.DeviceSymbolNotFound ||
                                                      adsEx.ErrorCode == AdsErrorCode.DeviceInvalidOffset ||
                                                      adsEx.ErrorCode == AdsErrorCode.DeviceInvalidData)
                {
                    // Esclavo no existe en esta dirección
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error leyendo esclavo en dirección {Addr}: {Error}", address, ex.Message);
                return null;
            }
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

        /// <summary>
        /// ⭐ Determina el tipo físico y número de puertos basado en el deviceType (sType del PLC)
        /// </summary>
        private (PhysicalType physicalType, int portCount) DetermineDevicePhysicalType(string deviceType)
        {
            if (string.IsNullOrWhiteSpace(deviceType))
                return (PhysicalType.EBusOnly, 2);
            
            var dt = deviceType.ToUpperInvariant();
            
            // Extraer modelo base (ej: "EK1100-0000-0018" → "EK1100")
            var modelBase = dt.Split('-')[0];
            
            // === Couplers EtherCAT (entrada Ethernet, salida E-Bus) ===
            // EK1100 = Coupler estándar (1 puerto Ethernet IN, E-Bus OUT)
            if (modelBase.StartsWith("EK1100"))
                return (PhysicalType.Mixed, 2);  // Puerto 0=Ethernet IN, Puerto 1=E-Bus
            
            // EK1101 = Coupler con ID switch
            if (modelBase.StartsWith("EK1101"))
                return (PhysicalType.Mixed, 2);
            
            // === Junction/Splitters (múltiples puertos Ethernet) ===
            // EK1122 = Junction con 2 puertos downstream adicionales
            if (modelBase.StartsWith("EK1122"))
                return (PhysicalType.Mixed, 4);  // Puerto 0=E-Bus IN, 1=E-Bus OUT, 2=Ethernet OUT, 3=Ethernet OUT
            
            // EK1521 = Junction con fibra óptica
            if (modelBase.StartsWith("EK1521") || modelBase.StartsWith("EK1541"))
                return (PhysicalType.Mixed, 4);
            
            // === Extension (Ethernet a Ethernet, cable largo) ===
            // EK1110 = Extension terminal (permite cable Ethernet hasta 100m)
            if (modelBase.StartsWith("EK1110"))
                return (PhysicalType.EthernetOnly, 2);  // Ethernet IN → Ethernet OUT
            
            // === Box modules (IP67, todo Ethernet) ===
            if (modelBase.StartsWith("EP") || modelBase.StartsWith("ER") || modelBase.StartsWith("EQ"))
                return (PhysicalType.EthernetOnly, 4);  // Generalmente 4 puertos Ethernet
            
            // === Drives y Motion ===
            // AX5xxx, AX8xxx = Drives Beckhoff
            if (modelBase.StartsWith("AX5") || modelBase.StartsWith("AX8"))
                return (PhysicalType.EthernetOnly, 2);  // Ethernet IN/OUT
            
            // === Variadores externos (YASKAWA, etc.) ===
            if (dt.Contains("YASKAWA") || dt.Contains("GA500") || dt.Contains("GA700"))
                return (PhysicalType.EthernetOnly, 2);  // Ethernet IN/OUT típicamente
            
            // === Terminales E-Bus estándar ===
            // EL/ES/EM series = Terminales de carril
            if (modelBase.StartsWith("EL") || modelBase.StartsWith("ES") || modelBase.StartsWith("EM"))
                return (PhysicalType.EBusOnly, 2);  // E-Bus IN/OUT (conector plano)
            
            // === Bus End / System ===
            if (modelBase.StartsWith("EL9") || dt.Contains("END"))
                return (PhysicalType.EBusOnly, 1);  // Solo entrada, termina el bus
            
            // Default: Terminal E-Bus estándar con 2 puertos
            return (PhysicalType.EBusOnly, 2);
        }

        /// <summary>
        /// ⭐ Genera lista de puertos con información real del PLC
        /// </summary>
        private List<EtherCATPort> GeneratePortsFromPLCData(int portCount, bool portA, bool portB, bool portC, bool portD, PhysicalType physicalType)
        {
            var ports = new List<EtherCATPort>();
            bool[] activeFlags = { portA, portB, portC, portD };
            string[] portNames = { "Port A (X1/IN)", "Port B (X2/OUT)", "Port C (X3/Branch)", "Port D (X4/Branch)" };
            
            for (int i = 0; i < portCount && i < 4; i++)
            {
                // Determinar tipo de puerto según PhysicalType del dispositivo
                PortType portType;
                PortPhysics portPhysics;
                
                switch (physicalType)
                {
                    case PhysicalType.EthernetOnly:
                        // Dispositivos solo Ethernet (drives, YASKAWA, Box modules)
                        // Todos los puertos son RJ45 Ethernet (MII)
                        portType = PortType.MII;
                        portPhysics = PortPhysics.Ethernet;
                        break;
                        
                    case PhysicalType.Mixed:
                        // Dispositivos mixtos - depende del número de puertos:
                        // - 2 puertos (EK1100 coupler): Puerto 0 = Ethernet IN, Puerto 1 = E-Bus OUT
                        // - 4 puertos (EK1122 junction): Puerto 0-1 = E-Bus, Puerto 2-3 = Ethernet
                        if (portCount == 2)
                        {
                            // Coupler: P0=Ethernet (upstream), P1=E-Bus (downstream)
                            if (i == 0)
                            {
                                portType = PortType.MII;
                                portPhysics = PortPhysics.Ethernet;
                            }
                            else
                            {
                                portType = PortType.EBUS;
                                portPhysics = PortPhysics.EBus;
                            }
                        }
                        else
                        {
                            // Junction (4 puertos): P0-1=E-Bus, P2-3=Ethernet
                            if (i < 2)
                            {
                                portType = PortType.EBUS;
                                portPhysics = PortPhysics.EBus;
                            }
                            else
                            {
                                portType = PortType.MII;
                                portPhysics = PortPhysics.Ethernet;
                            }
                        }
                        break;
                        
                    case PhysicalType.EBusOnly:
                    default:
                        // Terminales estándar (EL series)
                        // Todos los puertos son E-Bus (conector plano)
                        portType = PortType.EBUS;
                        portPhysics = PortPhysics.EBus;
                        break;
                }
                
                ports.Add(new EtherCATPort
                {
                    PortNumber = (byte)i,
                    Type = portType,
                    Physics = portPhysics,
                    HasCommunication = activeFlags[i],
                    LinkUp = activeFlags[i],
                    IsOpen = activeFlags[i],
                    ConnectedToSlaveIndex = -1,  // Se calcula después en la topología
                    Health = activeFlags[i] ? LinkHealth.Good : LinkHealth.Unknown
                });
            }
            
            return ports;
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
                Summary = new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Offline,
                    MasterStateText = IsSimulatedMode ? "Error (SIM)" : "Error - Sin comunicación"
                }
            };
        }

        // ===== MÉTODOS PARA CONFIGURACIÓN GUARDADA =====

        /// <summary>
        /// Guarda la topología actual como configuración de referencia en la base de datos
        /// </summary>
        public async Task<EtherCATSavedConfiguration> SaveConfigurationAsync(string? notes = null)
        {
            var topology = await GetTopologyAsync(rescan: true);
            
            if (topology.HasCommunicationError)
            {
                throw new InvalidOperationException($"No se puede guardar configuración: {topology.ErrorMessage}");
            }

            var projectId = _projectContext.ActiveProjectId;
            var topologyJson = JsonSerializer.Serialize(topology, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var configHash = ComputeConfigurationHash(topology);

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();

            // Buscar configuración existente para este proyecto
            var existing = await dbContext.EtherCATSavedConfigurations
                .FirstOrDefaultAsync(c => c.ProjectId == projectId);

            if (existing != null)
            {
                // Actualizar existente
                existing.TopologyJson = topologyJson;
                existing.TotalSlaves = topology.Slaves.Count;
                existing.SavedAt = DateTime.Now;
                existing.Notes = notes;
                existing.ConfigurationHash = configHash;
                
                _logger.LogInformation("💾 EtherCAT: Configuración actualizada para proyecto {ProjectId} ({Count} esclavos)", 
                    projectId, topology.Slaves.Count);
            }
            else
            {
                // Crear nueva
                existing = new EtherCATSavedConfiguration
                {
                    ProjectId = projectId,
                    TopologyJson = topologyJson,
                    TotalSlaves = topology.Slaves.Count,
                    SavedAt = DateTime.Now,
                    Notes = notes,
                    ConfigurationHash = configHash
                };
                dbContext.EtherCATSavedConfigurations.Add(existing);
                
                _logger.LogInformation("💾 EtherCAT: Nueva configuración guardada para proyecto {ProjectId} ({Count} esclavos)", 
                    projectId, topology.Slaves.Count);
            }

            await dbContext.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Obtiene la configuración guardada para el proyecto activo
        /// </summary>
        public async Task<EtherCATSavedConfiguration?> GetSavedConfigurationAsync()
        {
            var projectId = _projectContext.ActiveProjectId;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();

            return await dbContext.EtherCATSavedConfigurations
                .FirstOrDefaultAsync(c => c.ProjectId == projectId);
        }

        /// <summary>
        /// Elimina la configuración guardada del proyecto activo
        /// </summary>
        public async Task<bool> DeleteSavedConfigurationAsync()
        {
            var projectId = _projectContext.ActiveProjectId;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();

            var existing = await dbContext.EtherCATSavedConfigurations
                .FirstOrDefaultAsync(c => c.ProjectId == projectId);

            if (existing == null)
            {
                return false;
            }

            dbContext.EtherCATSavedConfigurations.Remove(existing);
            await dbContext.SaveChangesAsync();
            
            _logger.LogInformation("🗑️ EtherCAT: Configuración eliminada para proyecto {ProjectId}", projectId);
            return true;
        }

        /// <summary>
        /// Verifica si existe configuración guardada
        /// </summary>
        public async Task<bool> HasSavedConfigurationAsync()
        {
            var projectId = _projectContext.ActiveProjectId;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();

            return await dbContext.EtherCATSavedConfigurations
                .AnyAsync(c => c.ProjectId == projectId);
        }

        /// <summary>
        /// ⭐ OPTIMIZADO: Obtiene la topología guardada con estados actualizados del PLC.
        /// NO procesa ESI, NO recalcula layout - solo lee estados actuales y los aplica.
        /// Usar cuando ya existe configuración guardada para máximo rendimiento.
        /// </summary>
        public async Task<EtherCATTopology?> GetSavedTopologyWithCurrentStatesAsync()
        {
            var projectId = _projectContext.ActiveProjectId;
            _logger.LogInformation("⚡ EtherCAT: Cargando topología guardada con estados actuales (modo optimizado)");

            // 1. Cargar configuración guardada
            var savedConfig = await GetSavedConfigurationAsync();
            if (savedConfig == null)
            {
                _logger.LogWarning("⚠️ No hay configuración guardada para proyecto {ProjectId}", projectId);
                return null;
            }

            // 2. Deserializar topología guardada
            EtherCATTopology? savedTopology;
            try
            {
                savedTopology = JsonSerializer.Deserialize<EtherCATTopology>(savedConfig.TopologyJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializando configuración guardada");
                return null;
            }

            if (savedTopology == null || savedTopology.Slaves.Count == 0)
            {
                _logger.LogWarning("⚠️ Topología guardada vacía o inválida");
                return null;
            }

            // 3. Leer SOLO estados actuales del PLC (ligero, sin ESI)
            var currentStates = await ReadSlaveStatesOnlyAsync();
            
            if (currentStates.Count == 0)
            {
                _logger.LogWarning("⚠️ No se pudieron leer estados del PLC, devolviendo topología guardada sin actualizar");
                savedTopology.Timestamp = DateTime.Now;
                savedTopology.Summary.MasterStateText = "States unavailable";
                return savedTopology;
            }

            // 4. Actualizar estados en la topología guardada
            int updatedCount = 0;
            foreach (var slave in savedTopology.Slaves)
            {
                // Buscar estado actual por ConfiguredAddress (nECAddr)
                if (currentStates.TryGetValue(slave.ConfiguredAddress, out var currentState))
                {
                    slave.State = currentState.State;
                    slave.Health = currentState.Health;
                    slave.ErrorCounters = currentState.ErrorCounters;
                    slave.DiagnosticsAvailable = currentState.DiagnosticsAvailable;
                    slave.ErrorCount = currentState.ErrorCount;
                    
                    // Actualizar estado de puertos si disponible
                    if (currentState.PortsActive != null && slave.Ports != null)
                    {
                        for (int i = 0; i < slave.Ports.Count && i < 4; i++)
                        {
                            slave.Ports[i].HasCommunication = currentState.PortsActive[i];
                            slave.Ports[i].LinkUp = currentState.PortsActive[i];
                        }
                    }
                    updatedCount++;
                }
            }

            // 5. Actualizar resumen
            savedTopology.Timestamp = DateTime.Now;
            savedTopology.Summary.OperationalSlaveCount = savedTopology.Slaves.Count(s => s.State == EtherCATState.Operational);
            savedTopology.Summary.SlavesWithErrors = savedTopology.Slaves.Count(s => s.Health == NodeHealth.Error || s.ErrorCounters.HasErrors);
            savedTopology.Summary.TotalCRCErrors = savedTopology.Slaves.Sum(s => s.ErrorCounters.CRCErrorCount);
            savedTopology.Summary.OverallHealth = DetermineOverallHealth(savedTopology.Slaves);
            savedTopology.Summary.MasterStateText = savedTopology.Master.State.ToString();

            _logger.LogInformation("⚡ EtherCAT: Topología optimizada cargada - {Updated}/{Total} esclavos actualizados, {Op} en OP", 
                updatedCount, savedTopology.Slaves.Count, savedTopology.Summary.OperationalSlaveCount);

            return savedTopology;
        }

        /// <summary>
        /// Lee SOLO los estados de los esclavos desde el PLC (sin ESI, sin layout).
        /// Retorna diccionario [ConfiguredAddress → SlaveStateInfo]
        /// </summary>
        private async Task<Dictionary<ushort, SlaveCurrentState>> ReadSlaveStatesOnlyAsync()
        {
            var states = new Dictionary<ushort, SlaveCurrentState>();

            if (!IsEnabled || _masterClient == null)
            {
                return states;
            }

            try
            {
                await EnsureConnectedAsync();

                // Leer arrSlaveInfo pero solo extraer estado y errores
                var fbInstance = _config.EtherCATDiagFbInstance ?? "Diagnostic.fbEtherCATDiag";
                
                // Primero obtener número de esclavos
                var slaveCountHandle = _masterClient.CreateVariableHandle($"{fbInstance}.iNumOfSlavesRead");
                var slaveCount = _masterClient.ReadAny<short>(slaveCountHandle);
                _masterClient.DeleteVariableHandle(slaveCountHandle);

                if (slaveCount <= 0)
                {
                    return states;
                }

                // Leer array completo
                int maxArrayElements = 100;
                int maxElementSize = 320;
                var handle = _masterClient.CreateVariableHandle($"{fbInstance}.arrSlaveInfo");
                var buffer = new byte[maxArrayElements * maxElementSize];
                var bytesRead = _masterClient.Read(handle, buffer.AsMemory());
                _masterClient.DeleteVariableHandle(handle);

                // Detectar tamaño y offset
                var (actualSlaveSize, nECAddrOffset) = DetectSlaveInfoSize(buffer, bytesRead);
                if (actualSlaveSize == 0) return states;

                // Calcular offsets relativos
                int bDiagDataOffset = nECAddrOffset + 2;
                int nSumCRCErrorsOffset = nECAddrOffset + 21;
                int stStateOffset = nECAddrOffset + 25;

                // Parsear solo estados
                for (int i = 0; i < slaveCount && i < 100; i++)
                {
                    var offset = i * actualSlaveSize;
                    if (offset + actualSlaveSize > bytesRead) break;

                    var nECAddr = BitConverter.ToUInt16(buffer, offset + nECAddrOffset);
                    if (nECAddr == 0) continue;

                    var bDiagData = buffer[offset + bDiagDataOffset] != 0;
                    
                    uint nSumCRCErrors = 0;
                    if (offset + nSumCRCErrorsOffset + 4 <= buffer.Length)
                    {
                        nSumCRCErrors = BitConverter.ToUInt32(buffer, offset + nSumCRCErrorsOffset);
                    }

                    // Estado y puertos activos
                    var state = EtherCATState.Unknown;
                    bool[] portsActive = new bool[4];
                    int stateOffsetAbs = offset + stStateOffset;
                    if (stateOffsetAbs + 16 <= buffer.Length)
                    {
                        var stateValue = buffer[stateOffsetAbs] & 0x0F;
                        state = stateValue switch
                        {
                            1 => EtherCATState.Init,
                            2 => EtherCATState.PreOp,
                            3 => EtherCATState.Bootstrap,
                            4 => EtherCATState.SafeOp,
                            8 => EtherCATState.Operational,
                            _ => EtherCATState.Unknown
                        };
                        
                        portsActive[0] = buffer[stateOffsetAbs + 12] != 0;
                        portsActive[1] = buffer[stateOffsetAbs + 13] != 0;
                        portsActive[2] = buffer[stateOffsetAbs + 14] != 0;
                        portsActive[3] = buffer[stateOffsetAbs + 15] != 0;
                    }

                    states[nECAddr] = new SlaveCurrentState
                    {
                        State = state,
                        Health = state == EtherCATState.Operational ? NodeHealth.Healthy :
                                 state == EtherCATState.SafeOp ? NodeHealth.Warning : NodeHealth.Error,
                        DiagnosticsAvailable = bDiagData,
                        ErrorCount = (int)nSumCRCErrors,
                        PortsActive = portsActive,
                        ErrorCounters = new SlaveErrorCounters { CRCErrorCount = nSumCRCErrors }
                    };
                }

                _logger.LogDebug("⚡ Leídos {Count} estados de esclavos (modo ligero)", states.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error leyendo estados: {Error}", ex.Message);
            }

            return states;
        }

        /// <summary>
        /// Estado actual de un esclavo (datos mínimos para actualización)
        /// </summary>
        private class SlaveCurrentState
        {
            public EtherCATState State { get; set; }
            public NodeHealth Health { get; set; }
            public bool DiagnosticsAvailable { get; set; }
            public int ErrorCount { get; set; }
            public bool[]? PortsActive { get; set; }
            public SlaveErrorCounters ErrorCounters { get; set; } = new();
        }

        /// <summary>
        /// Determina la salud general de la red
        /// </summary>
        private static NetworkHealth DetermineOverallHealth(List<EtherCATSlaveNode> slaves)
        {
            if (slaves.Count == 0) return NetworkHealth.Offline;
            
            var errorCount = slaves.Count(s => s.Health == NodeHealth.Error);
            var warningCount = slaves.Count(s => s.Health == NodeHealth.Warning);
            var opCount = slaves.Count(s => s.State == EtherCATState.Operational);
            
            if (errorCount > 0) return NetworkHealth.Error;
            if (warningCount > 0 || opCount < slaves.Count) return NetworkHealth.Warning;
            return NetworkHealth.Healthy;
        }

        /// <summary>
        /// Compara la configuración guardada con el estado actual del sistema
        /// </summary>
        public async Task<EtherCATConfigurationComparison> CompareWithSavedConfigurationAsync()
        {
            var comparison = new EtherCATConfigurationComparison();
            
            // Obtener configuración guardada
            var savedConfig = await GetSavedConfigurationAsync();
            
            if (savedConfig == null)
            {
                comparison.HasSavedConfiguration = false;
                return comparison;
            }

            comparison.HasSavedConfiguration = true;
            comparison.SavedAt = savedConfig.SavedAt;
            comparison.SavedNotes = savedConfig.Notes;
            comparison.SavedSlaveCount = savedConfig.TotalSlaves;

            // Deserializar topología guardada
            EtherCATTopology? savedTopology;
            try
            {
                savedTopology = JsonSerializer.Deserialize<EtherCATTopology>(savedConfig.TopologyJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializando configuración guardada");
                comparison.ConfigurationMatches = false;
                return comparison;
            }

            if (savedTopology == null)
            {
                comparison.ConfigurationMatches = false;
                return comparison;
            }

            // Obtener topología actual (forzar rescan)
            var currentTopology = await GetTopologyAsync(rescan: true);
            comparison.CurrentSlaveCount = currentTopology.Slaves.Count;

            // Comparar esclavos
            var savedSlaves = savedTopology.Slaves.ToDictionary(s => s.ConfiguredAddress);
            var currentSlaves = currentTopology.Slaves.ToDictionary(s => s.ConfiguredAddress);

            // Encontrar esclavos faltantes (estaban en config guardada pero no están ahora)
            foreach (var saved in savedTopology.Slaves)
            {
                if (!currentSlaves.ContainsKey(saved.ConfiguredAddress))
                {
                    comparison.MissingSlaves.Add(new MissingSlaveInfo
                    {
                        Position = saved.Position,
                        ConfiguredAddress = saved.ConfiguredAddress,
                        Name = saved.Name,
                        VendorId = saved.VendorId,
                        ProductCode = saved.ProductCode
                    });
                }
            }

            // Encontrar esclavos nuevos (no estaban en config guardada)
            foreach (var current in currentTopology.Slaves)
            {
                if (!savedSlaves.ContainsKey(current.ConfiguredAddress))
                {
                    comparison.NewSlaves.Add(new NewSlaveInfo
                    {
                        Position = current.Position,
                        ConfiguredAddress = current.ConfiguredAddress,
                        Name = current.Name,
                        VendorId = current.VendorId,
                        ProductCode = current.ProductCode
                    });
                }
            }

            // Encontrar diferencias en esclavos que existen en ambos
            foreach (var saved in savedTopology.Slaves)
            {
                if (currentSlaves.TryGetValue(saved.ConfiguredAddress, out var current))
                {
                    // Comparar VendorId + ProductCode (hardware diferente)
                    if (saved.VendorId != current.VendorId || saved.ProductCode != current.ProductCode)
                    {
                        comparison.Differences.Add(new SlaveConfigDifference
                        {
                            Position = saved.Position,
                            SlaveName = saved.Name,
                            Field = "Hardware",
                            SavedValue = $"0x{saved.VendorId:X8}:{saved.ProductCode:X8}",
                            CurrentValue = $"0x{current.VendorId:X8}:{current.ProductCode:X8}"
                        });
                    }

                    // Comparar posición en el bus
                    if (saved.Position != current.Position)
                    {
                        comparison.Differences.Add(new SlaveConfigDifference
                        {
                            Position = saved.Position,
                            SlaveName = saved.Name,
                            Field = "Position",
                            SavedValue = saved.Position.ToString(),
                            CurrentValue = current.Position.ToString()
                        });
                    }
                }
            }

            // La configuración coincide si no hay diferencias significativas
            comparison.ConfigurationMatches = 
                comparison.MissingSlaves.Count == 0 && 
                comparison.NewSlaves.Count == 0 && 
                comparison.Differences.Count == 0;

            _logger.LogInformation("🔍 EtherCAT: Comparación - Guardados: {Saved}, Actuales: {Current}, Match: {Match}, Faltantes: {Missing}, Nuevos: {New}", 
                comparison.SavedSlaveCount, comparison.CurrentSlaveCount, comparison.ConfigurationMatches,
                comparison.MissingSlaves.Count, comparison.NewSlaves.Count);

            return comparison;
        }

        /// <summary>
        /// Calcula un hash de la configuración para detectar cambios rápidamente
        /// </summary>
        private static string ComputeConfigurationHash(EtherCATTopology topology)
        {
            // Hash basado en: número de esclavos + sus VendorId:ProductCode + posiciones
            var hashInput = string.Join("|", topology.Slaves
                .OrderBy(s => s.Position)
                .Select(s => $"{s.Position}:{s.VendorId}:{s.ProductCode}:{s.ConfiguredAddress}"));

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
            return Convert.ToHexString(hashBytes);
        }

        public void Dispose()
        {
            _masterClient?.Dispose();
            _masterClient = null;
        }
    }
}
