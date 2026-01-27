using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.EtherCAT;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// 🌐 API para diagnóstico de topología EtherCAT
    /// Proporciona información de la red EtherCAT para visualización en el frontend.
    /// 
    /// OPTIMIZACIÓN: Solo lee cuando se solicita (no polling automático)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EtherCATController : ControllerBase
    {
        private readonly IEtherCATDiagnosticsService _etherCATService;
        private readonly IESIParserService _esiParser;
        private readonly ILogger<EtherCATController> _logger;

        public EtherCATController(
            IEtherCATDiagnosticsService etherCATService,
            IESIParserService esiParser,
            ILogger<EtherCATController> logger)
        {
            _etherCATService = etherCATService;
            _esiParser = esiParser;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el estado de configuración del diagnóstico EtherCAT
        /// </summary>
        [HttpGet("config")]
        public ActionResult<EtherCATConfigResponse> GetConfiguration()
        {
            var config = _etherCATService.GetConfiguration();
            
            return Ok(new EtherCATConfigResponse
            {
                IsEnabled = _etherCATService.IsEnabled,
                MasterNetId = config.EtherCATMasterNetId,
                ESIFilesPath = config.ESIFilesPath,
                UseESIFiles = config.UseESIFiles,
                TopologyReadIntervalMs = config.TopologyReadIntervalMs
            });
        }

        /// <summary>
        /// 🔍 Prueba la conexión al Master EtherCAT con diagnóstico detallado
        /// Útil para debugging cuando no se cargan datos
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<ActionResult<EtherCATConnectionDiagnostics>> TestConnection()
        {
            _logger.LogInformation("🔍 Solicitud de test de conexión EtherCAT");
            
            try
            {
                var diagnostics = await _etherCATService.TestConnectionAsync();
                return Ok(diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en test de conexión EtherCAT");
                return Ok(new EtherCATConnectionDiagnostics
                {
                    Timestamp = DateTime.Now,
                    OverallSuccess = false,
                    Summary = $"Error durante el diagnóstico: {ex.Message}",
                    DiagnosticMessages = new List<string>
                    {
                        $"❌ Excepción no controlada: {ex.Message}",
                        $"   StackTrace: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}"
                    }
                });
            }
        }

        /// <summary>
        /// 🔬 Escanea símbolos del PLC para encontrar variables EtherCAT (usando TcAdsSymbolInfoLoader)
        /// </summary>
        [HttpGet("scan-index-groups")]
        public async Task<ActionResult<object>> ScanIndexGroups([FromQuery] int? port = null, [FromQuery] string? filter = null)
        {
            var config = _etherCATService.GetConfiguration();
            
            // Probar múltiples puertos si no se especifica uno
            var portsToTry = port.HasValue 
                ? new[] { port.Value } 
                : new[] { 300, 851, 27905 }; // I/O, PLC Runtime, EtherCAT Master
            
            var results = new Dictionary<string, object>();
            var workingPort = 0;
            TwinCAT.Ads.AdsClient? workingClient = null;
            
            foreach (var testPort in portsToTry)
            {
                try
                {
                    var client = new TwinCAT.Ads.AdsClient();
                    var netId = new TwinCAT.Ads.AmsNetId(config.EtherCATMasterNetId);
                    
                    client.Connect(netId, (TwinCAT.Ads.AmsPort)testPort);
                    client.Timeout = 3000;
                    
                    var state = client.ReadState();
                    results[$"port_{testPort}"] = new { 
                        status = "✅ Connected", 
                        adsState = state.AdsState.ToString(),
                        deviceState = state.DeviceState
                    };
                    
                    if (workingClient == null)
                    {
                        workingPort = testPort;
                        workingClient = client;
                    }
                    else
                    {
                        client.Dispose();
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    results[$"port_{testPort}"] = new { status = $"❌ {ex.ErrorCode}" };
                }
                catch (Exception ex)
                {
                    results[$"port_{testPort}"] = new { status = $"❌ {ex.Message}" };
                }
            }
            
            if (workingClient == null)
            {
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = config.EtherCATMasterNetId,
                    error = "No se pudo conectar a ningún puerto",
                    portResults = results
                });
            }
            
            _logger.LogInformation("🔬 Escaneando símbolos en puerto {Port}...", workingPort);
            
            try
            {
                var client = workingClient;
                client.Timeout = 5000;
                
                // Palabras clave para buscar variables EtherCAT
                var keywords = new[] { 
                    "ethercat", "slave", "tiid", "ec_", "ecat", "devstate", 
                    "master", "topology", "box", "term", "coupler", "el", "ek",
                    "infoslave", "slavelist", "slavecount", "ecstate", "io"
                };
                
                var filterLower = filter?.ToLower() ?? "";
                
                var etherCATSymbols = new List<object>();
                var matchingSymbols = new List<object>();
                var uploadedSymbols = new List<object>();
                
                // Leer upload info
                uint symbolCount = 0;
                uint symbolSize = 0;
                string uploadError = null;
                
                try
                {
                    var uploadInfo = new byte[24];
                    var bytesRead = client.Read(0xF00F, 0, uploadInfo.AsMemory());
                    if (bytesRead >= 8)
                    {
                        symbolCount = BitConverter.ToUInt32(uploadInfo, 0);
                        symbolSize = BitConverter.ToUInt32(uploadInfo, 4);
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    uploadError = $"Upload info failed: {ex.ErrorCode}";
                }
                
                // Si hay símbolos, leerlos
                if (symbolSize > 0 && symbolSize < 50_000_000)
                {
                    try
                    {
                        var symbolBuffer = new byte[symbolSize];
                        var totalRead = client.Read(0xF00B, 0, symbolBuffer.AsMemory());
                        
                        // Parsear símbolos
                        int offset = 0;
                        int symbolsParsed = 0;
                        while (offset < totalRead && symbolsParsed < 1000)
                        {
                            try
                            {
                                if (offset + 30 > totalRead) break;
                                
                                var entryLength = BitConverter.ToUInt32(symbolBuffer, offset);
                                if (entryLength == 0 || entryLength > 10000 || offset + entryLength > totalRead) break;
                                
                                var ig = BitConverter.ToUInt32(symbolBuffer, offset + 4);
                                var io = BitConverter.ToUInt32(symbolBuffer, offset + 8);
                                var size = BitConverter.ToUInt32(symbolBuffer, offset + 12);
                                var nameLength = BitConverter.ToUInt16(symbolBuffer, offset + 24);
                                var typeLength = BitConverter.ToUInt16(symbolBuffer, offset + 26);
                                
                                var nameStart = offset + 30;
                                var typeStart = nameStart + nameLength + 1;
                                
                                if (nameStart + nameLength <= totalRead && typeStart + typeLength <= totalRead)
                                {
                                    var name = System.Text.Encoding.ASCII.GetString(symbolBuffer, nameStart, nameLength).TrimEnd('\0');
                                    var typeName = System.Text.Encoding.ASCII.GetString(symbolBuffer, typeStart, typeLength).TrimEnd('\0');
                                    
                                    var nameLower = name.ToLower();
                                    var typeLower = typeName.ToLower();
                                    
                                    var matchesKeyword = keywords.Any(k => nameLower.Contains(k) || typeLower.Contains(k));
                                    var matchesFilter = !string.IsNullOrEmpty(filterLower) && 
                                                       (nameLower.Contains(filterLower) || typeLower.Contains(filterLower));
                                    
                                    if (matchesKeyword)
                                    {
                                        etherCATSymbols.Add(new { name, type = typeName, size, ig = $"0x{ig:X}", io = $"0x{io:X}" });
                                    }
                                    else if (matchesFilter)
                                    {
                                        matchingSymbols.Add(new { name, type = typeName, size });
                                    }
                                    
                                    if (uploadedSymbols.Count < 30)
                                    {
                                        uploadedSymbols.Add(new { name, type = typeName, size });
                                    }
                                }
                                
                                offset += (int)entryLength;
                                symbolsParsed++;
                            }
                            catch { break; }
                        }
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        uploadError = $"Symbol upload failed: {ex.ErrorCode}";
                    }
                }
                
                // Probar lectura directa de Index Groups conocidos del I/O Server
                var ioTests = new List<object>();
                
                // Index Groups específicos para EtherCAT según documentación Beckhoff
                var testIGs = new (uint ig, uint io, int readSize, string desc)[]
                {
                    // ADS Index Groups para I/O Server (puerto 300)
                    (0xF020, 0, 256, "DeviceData"),
                    (0xF030, 0, 256, "DeviceDataEx"),
                    
                    // EtherCAT Master específicos (TwinCAT 3)
                    (0xF302, 0, 4, "EC Master - Slave Count"),
                    (0xF302, 1, 256, "EC Master - Slave 1 Info"),
                    
                    // IOADS Index Groups
                    (0x1000, 0, 256, "Physical Read"),
                    (0x1010, 0, 256, "Physical Write"),
                    (0x1020, 0, 256, "Bit Read"),
                    
                    // ADS State
                    (0xF100, 0, 4, "ADS State"),
                    
                    // Device Info
                    (0x2, 0, 256, "Device Info"),
                    (0x3, 0, 256, "Device Name"),
                    
                    // Task Info  
                    (0xF003, 0, 256, "Task Info"),
                    
                    // Intenta leer el número de dispositivos/esclavos
                    (0xF302, 0xFFFFFFFF, 4, "EC - Device Count"),
                };
                
                foreach (var (ig, io, readSize, desc) in testIGs)
                {
                    try
                    {
                        var buffer = new byte[readSize];
                        var bytesRead = client.Read(ig, io, buffer.AsMemory());
                        var hexData = BitConverter.ToString(buffer.Take(Math.Min(32, bytesRead)).ToArray()).Replace("-", " ");
                        
                        // Interpretar datos si es posible
                        string? interpreted = null;
                        if (bytesRead == 4)
                        {
                            var intVal = BitConverter.ToInt32(buffer, 0);
                            var uintVal = BitConverter.ToUInt32(buffer, 0);
                            interpreted = $"int32={intVal}, uint32={uintVal}";
                        }
                        else if (bytesRead >= 1 && buffer.Take(bytesRead).All(b => b >= 32 && b < 127 || b == 0))
                        {
                            interpreted = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead).TrimEnd('\0');
                            if (string.IsNullOrWhiteSpace(interpreted)) interpreted = null;
                        }
                        
                        ioTests.Add(new { 
                            ig = $"0x{ig:X4}", 
                            io = io == 0xFFFFFFFF ? "0xFFFFFFFF" : io.ToString(), 
                            desc, 
                            status = "✅ OK", 
                            bytesRead, 
                            data = hexData,
                            interpreted
                        });
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        ioTests.Add(new { 
                            ig = $"0x{ig:X4}", 
                            io = io == 0xFFFFFFFF ? "0xFFFFFFFF" : io.ToString(), 
                            desc, 
                            status = $"❌ {ex.ErrorCode}",
                            bytesRead = 0,
                            data = (string?)null,
                            interpreted = (string?)null
                        });
                    }
                }
                
                client.Dispose();
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = config.EtherCATMasterNetId,
                    connectedPort = workingPort,
                    portResults = results,
                    symbolUploadInfo = new { symbolCount, symbolSizeBytes = symbolSize, error = uploadError },
                    filter = filter ?? "(none - using EtherCAT keywords)",
                    etherCATRelatedCount = etherCATSymbols.Count,
                    etherCATSymbols = etherCATSymbols.Take(50),
                    filterMatchCount = matchingSymbols.Count,
                    filterMatches = matchingSymbols.Take(20),
                    sampleSymbols = uploadedSymbols,
                    indexGroupTests = ioTests,
                    hint = "Use ?filter=xxx para buscar, ?port=851 para forzar puerto"
                });
            }
            catch (Exception ex)
            {
                workingClient?.Dispose();
                _logger.LogError(ex, "Error escaneando símbolos");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    connectedPort = workingPort,
                    portResults = results,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 🔬 Escanea símbolos del puerto 27905 (EtherCAT Master) - Método Sample06 de Beckhoff
        /// </summary>
        [HttpGet("scan-symbols-27905")]
        public async Task<ActionResult<object>> ScanSymbols27905([FromQuery] string? filter = null, [FromQuery] int? port = null)
        {
            var config = _etherCATService.GetConfiguration();
            var targetPort = port ?? 27905;
            
            _logger.LogInformation("🔬 Escaneando símbolos en puerto {Port}...", targetPort);
            
            var symbolList = new List<object>();
            var ecatSymbols = new List<object>();
            var diagnostics = new List<string>();
            
            try
            {
                using var client = new TwinCAT.Ads.AdsClient();
                var netId = new TwinCAT.Ads.AmsNetId(config.EtherCATMasterNetId);
                
                client.Connect(netId, (TwinCAT.Ads.AmsPort)targetPort);
                client.Timeout = 5000;
                
                // Verificar conexión
                TwinCAT.Ads.StateInfo state;
                try
                {
                    state = client.ReadState();
                    diagnostics.Add($"✅ Conectado a puerto {targetPort}: AdsState={state.AdsState}, DeviceState={state.DeviceState}");
                }
                catch (Exception ex)
                {
                    return Ok(new { timestamp = DateTime.Now, port = targetPort, error = ex.Message });
                }
                
                // Leer Device Info
                try
                {
                    var deviceInfo = client.ReadDeviceInfo();
                    diagnostics.Add($"✅ Device: {deviceInfo.Name} v{deviceInfo.Version}");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ Device Info: {ex.ErrorCode}");
                }
                
                // Leer upload info (Index Group 0xF00F)
                uint symbolCount = 0;
                uint symbolSize = 0;
                
                try
                {
                    var uploadInfo = new byte[24];
                    var bytesRead = client.Read(0xF00F, 0, uploadInfo.AsMemory());
                    if (bytesRead >= 8)
                    {
                        symbolCount = BitConverter.ToUInt32(uploadInfo, 0);
                        symbolSize = BitConverter.ToUInt32(uploadInfo, 4);
                        diagnostics.Add($"✅ Upload Info: {symbolCount} símbolos, {symbolSize} bytes");
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"❌ Upload Info (0xF00F): {ex.ErrorCode}");
                }
                
                // Intentar leer símbolos con Index Group 0xF00B
                if (symbolSize > 0 && symbolSize < 100_000_000)
                {
                    try
                    {
                        var symbolBuffer = new byte[symbolSize];
                        var totalRead = client.Read(0xF00B, 0, symbolBuffer.AsMemory());
                        diagnostics.Add($"✅ Símbolos leídos: {totalRead} bytes");
                        
                        // Parsear símbolos
                        int offset = 0;
                        int parsed = 0;
                        while (offset < totalRead && parsed < 5000)
                        {
                            try
                            {
                                if (offset + 30 > totalRead) break;
                                
                                var entryLength = BitConverter.ToUInt32(symbolBuffer, offset);
                                if (entryLength == 0 || entryLength > 50000 || offset + entryLength > totalRead) break;
                                
                                var ig = BitConverter.ToUInt32(symbolBuffer, offset + 4);
                                var io = BitConverter.ToUInt32(symbolBuffer, offset + 8);
                                var size = BitConverter.ToUInt32(symbolBuffer, offset + 12);
                                var nameLength = BitConverter.ToUInt16(symbolBuffer, offset + 24);
                                var typeLength = BitConverter.ToUInt16(symbolBuffer, offset + 26);
                                
                                var nameStart = offset + 30;
                                var typeStart = nameStart + nameLength + 1;
                                
                                if (nameStart + nameLength <= totalRead && typeStart + typeLength <= totalRead)
                                {
                                    var name = System.Text.Encoding.ASCII.GetString(symbolBuffer, nameStart, nameLength).TrimEnd('\0');
                                    var typeName = System.Text.Encoding.ASCII.GetString(symbolBuffer, typeStart, typeLength).TrimEnd('\0');
                                    
                                    var matchesFilter = string.IsNullOrEmpty(filter) || 
                                                       name.ToLower().Contains(filter.ToLower()) ||
                                                       typeName.ToLower().Contains(filter.ToLower());
                                    
                                    if (matchesFilter)
                                    {
                                        var symbolInfo = new
                                        {
                                            name,
                                            type = typeName,
                                            size,
                                            indexGroup = $"0x{ig:X4}",
                                            indexOffset = $"0x{io:X}"
                                        };
                                        
                                        var nameLower = name.ToLower();
                                        if (nameLower.Contains("term") || nameLower.Contains("el") || 
                                            nameLower.Contains("ek") || nameLower.Contains("channel") ||
                                            nameLower.Contains("device") || nameLower.Contains("box") ||
                                            nameLower.Contains("status") || nameLower.Contains("value"))
                                        {
                                            ecatSymbols.Add(symbolInfo);
                                        }
                                        
                                        symbolList.Add(symbolInfo);
                                    }
                                }
                                
                                offset += (int)entryLength;
                                parsed++;
                            }
                            catch { break; }
                        }
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        diagnostics.Add($"❌ Symbol Upload (0xF00B): {ex.ErrorCode}");
                    }
                }
                
                // Si no hay símbolos, intentar leer datos directamente con Index Groups de I/O
                // Según la captura del usuario: IGrp: 0xF030, IOffs: 0x27
                var directReads = new List<object>();
                if (symbolList.Count == 0)
                {
                    diagnostics.Add("ℹ️ No se encontraron símbolos vía 0xF00B, probando lectura directa...");
                    
                    // Probar leer datos directamente con 0xF030 (según la captura)
                    var testOffsets = new uint[] { 0, 1, 0x27, 0x28, 0x29, 0x30, 0x100, 0x200 };
                    foreach (var testOffset in testOffsets)
                    {
                        try
                        {
                            var buffer = new byte[64];
                            var bytesRead = client.Read(0xF030, testOffset, buffer.AsMemory());
                            var hexData = BitConverter.ToString(buffer.Take(Math.Min(16, bytesRead)).ToArray()).Replace("-", " ");
                            directReads.Add(new { 
                                ig = "0xF030", 
                                io = $"0x{testOffset:X}", 
                                bytesRead, 
                                data = hexData,
                                status = "✅ OK"
                            });
                        }
                        catch (TwinCAT.Ads.AdsErrorException ex)
                        {
                            if (ex.ErrorCode != TwinCAT.Ads.AdsErrorCode.DeviceServiceNotSupported)
                            {
                                directReads.Add(new { ig = "0xF030", io = $"0x{testOffset:X}", status = $"❌ {ex.ErrorCode}" });
                            }
                        }
                    }
                }
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = config.EtherCATMasterNetId,
                    port = targetPort,
                    adsState = state.AdsState.ToString(),
                    deviceState = state.DeviceState,
                    diagnostics,
                    uploadInfo = new { symbolCount, symbolSizeBytes = symbolSize },
                    filter = filter ?? "(all)",
                    totalSymbolsFound = symbolList.Count,
                    ecatRelatedCount = ecatSymbols.Count,
                    ecatSymbols = ecatSymbols.Take(100),
                    allSymbols = symbolList.Take(200),
                    directReads = directReads.Count > 0 ? directReads : null,
                    hint = "Prueba con ?port=851 o ?port=300"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error escaneando puerto {Port}", targetPort);
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    port = targetPort,
                    error = ex.Message,
                    diagnostics
                });
            }
        }

        /// <summary>
        /// Obtiene el resumen rápido de la red EtherCAT (para panel compacto)
        /// Ligero y rápido, ideal para mostrar en InfoPanel
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<EtherCATSummary>> GetSummary()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Offline,
                    MasterStateText = "Disabled"
                });
            }

            try
            {
                var summary = await _etherCATService.GetSummaryAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting EtherCAT summary");
                return Ok(new EtherCATSummary
                {
                    OverallHealth = NetworkHealth.Error,
                    MasterStateText = "Error"
                });
            }
        }

        /// <summary>
        /// Obtiene la topología completa de la red EtherCAT
        /// Incluye todos los esclavos, conexiones y grafo para visualización
        /// NOTA: Esta llamada es más pesada, usar solo cuando se muestra el modal
        /// </summary>
        /// <param name="rescan">Si es true, fuerza un escaneo completo ignorando configuración guardada</param>
        [HttpGet("topology")]
        public async Task<ActionResult<EtherCATTopology>> GetTopology([FromQuery] bool rescan = false)
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = "EtherCAT diagnostics not enabled in configuration",
                    Timestamp = DateTime.Now,
                    Summary = new EtherCATSummary
                    {
                        OverallHealth = NetworkHealth.Offline,
                        MasterStateText = "Disabled"
                    }
                });
            }

            try
            {
                _logger.LogInformation("🌐 EtherCAT topology requested (rescan: {Rescan})", rescan);
                var topology = await _etherCATService.GetTopologyAsync(rescan);
                return Ok(topology);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting EtherCAT topology");
                return StatusCode(500, new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = $"Error reading topology: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// ⚡ OPTIMIZADO: Obtiene la topología guardada con estados actualizados del PLC.
        /// NO procesa ESI, NO recalcula layout - solo actualiza estados.
        /// Usar cuando ya existe configuración guardada para máximo rendimiento.
        /// Si no hay configuración guardada, retorna null y el frontend debe usar /topology normal.
        /// </summary>
        [HttpGet("topology/optimized")]
        public async Task<ActionResult<EtherCATTopology>> GetOptimizedTopology()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new EtherCATTopology
                {
                    HasCommunicationError = true,
                    ErrorMessage = "EtherCAT diagnostics not enabled",
                    Timestamp = DateTime.Now,
                    Summary = new EtherCATSummary { OverallHealth = NetworkHealth.Offline }
                });
            }

            try
            {
                _logger.LogInformation("⚡ EtherCAT optimized topology requested");
                
                // Primero verificar si hay configuración guardada
                var hasSaved = await _etherCATService.HasSavedConfigurationAsync();
                if (!hasSaved)
                {
                    // No hay config guardada - indicar al frontend que use /topology normal
                    return Ok(new { 
                        hasConfiguration = false, 
                        message = "No saved configuration. Use /topology to get full topology and save it." 
                    });
                }

                // Cargar topología guardada con estados actualizados
                var topology = await _etherCATService.GetSavedTopologyWithCurrentStatesAsync();
                
                if (topology == null)
                {
                    return Ok(new { 
                        hasConfiguration = false, 
                        message = "Error loading saved configuration" 
                    });
                }

                return Ok(topology);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized topology");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene información detallada de un esclavo específico
        /// </summary>
        [HttpGet("slave/{address:int}")]
        public async Task<ActionResult<EtherCATSlaveNode>> GetSlaveInfo(int address)
        {
            if (!_etherCATService.IsEnabled)
            {
                return NotFound("EtherCAT diagnostics not enabled");
            }

            try
            {
                var slave = await _etherCATService.GetSlaveInfoAsync((ushort)address);
                if (slave == null)
                {
                    return NotFound($"Slave at address {address} not found");
                }
                return Ok(slave);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slave info for address {Address}", address);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene solo la lista de esclavos sin el grafo completo
        /// Útil para vistas de lista/tabla
        /// </summary>
        [HttpGet("slaves")]
        public async Task<ActionResult<List<EtherCATSlaveNode>>> GetSlaves()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new List<EtherCATSlaveNode>());
            }

            try
            {
                var topology = await _etherCATService.GetTopologyAsync();
                return Ok(topology.Slaves);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slaves list");
                return StatusCode(500, new List<EtherCATSlaveNode>());
            }
        }

        /// <summary>
        /// Fuerza una nueva lectura de la topología (invalida cache)
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<EtherCATTopology>> RefreshTopology()
        {
            if (!_etherCATService.IsEnabled)
            {
                return BadRequest("EtherCAT diagnostics not enabled");
            }

            try
            {
                _logger.LogInformation("🌐 EtherCAT topology refresh requested");
                _etherCATService.InvalidateCache();
                var topology = await _etherCATService.GetTopologyAsync();
                return Ok(topology);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing EtherCAT topology");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // ===== CONFIGURACIÓN GUARDADA =====

        /// <summary>
        /// Guarda la topología actual como configuración de referencia
        /// </summary>
        [HttpPost("configuration")]
        public async Task<ActionResult<SaveConfigurationResponse>> SaveConfiguration([FromBody] SaveConfigurationRequest? request)
        {
            if (!_etherCATService.IsEnabled)
            {
                return BadRequest("EtherCAT diagnostics not enabled");
            }

            try
            {
                _logger.LogInformation("💾 EtherCAT: Saving configuration...");
                var saved = await _etherCATService.SaveConfigurationAsync(request?.Notes);
                
                return Ok(new SaveConfigurationResponse
                {
                    Success = true,
                    Message = $"Configuración guardada exitosamente ({saved.TotalSlaves} esclavos)",
                    SavedAt = saved.SavedAt,
                    TotalSlaves = saved.TotalSlaves,
                    ConfigurationHash = saved.ConfigurationHash
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving EtherCAT configuration");
                return StatusCode(500, new SaveConfigurationResponse
                {
                    Success = false,
                    Message = $"Error al guardar: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Obtiene la configuración guardada (si existe)
        /// </summary>
        [HttpGet("configuration")]
        public async Task<ActionResult<SavedConfigurationResponse>> GetSavedConfiguration()
        {
            try
            {
                var saved = await _etherCATService.GetSavedConfigurationAsync();
                
                if (saved == null)
                {
                    return Ok(new SavedConfigurationResponse
                    {
                        HasConfiguration = false
                    });
                }

                return Ok(new SavedConfigurationResponse
                {
                    HasConfiguration = true,
                    SavedAt = saved.SavedAt,
                    TotalSlaves = saved.TotalSlaves,
                    Notes = saved.Notes,
                    ConfigurationHash = saved.ConfigurationHash
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved EtherCAT configuration");
                return StatusCode(500, new SavedConfigurationResponse
                {
                    HasConfiguration = false
                });
            }
        }

        /// <summary>
        /// Elimina la configuración guardada
        /// </summary>
        [HttpDelete("configuration")]
        public async Task<ActionResult<DeleteConfigurationResponse>> DeleteConfiguration()
        {
            try
            {
                var deleted = await _etherCATService.DeleteSavedConfigurationAsync();
                
                return Ok(new DeleteConfigurationResponse
                {
                    Success = deleted,
                    Message = deleted 
                        ? "Configuración eliminada exitosamente" 
                        : "No había configuración guardada para eliminar"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting EtherCAT configuration");
                return StatusCode(500, new DeleteConfigurationResponse
                {
                    Success = false,
                    Message = $"Error al eliminar: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Compara la configuración guardada con el estado actual del sistema
        /// Devuelve información sobre esclavos faltantes, nuevos o con cambios
        /// </summary>
        [HttpGet("configuration/compare")]
        public async Task<ActionResult<EtherCATConfigurationComparison>> CompareConfiguration()
        {
            if (!_etherCATService.IsEnabled)
            {
                return BadRequest("EtherCAT diagnostics not enabled");
            }

            try
            {
                _logger.LogInformation("🔍 EtherCAT: Comparing configuration...");
                var comparison = await _etherCATService.CompareWithSavedConfigurationAsync();
                return Ok(comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing EtherCAT configuration");
                return StatusCode(500, new EtherCATConfigurationComparison
                {
                    HasSavedConfiguration = false,
                    ConfigurationMatches = false
                });
            }
        }

        /// <summary>
        /// Obtiene solo los contadores de errores de todos los esclavos
        /// Útil para vista de diagnóstico rápido
        /// </summary>
        [HttpGet("errors")]
        public async Task<ActionResult<List<SlaveErrorSummary>>> GetErrors()
        {
            if (!_etherCATService.IsEnabled)
            {
                return Ok(new List<SlaveErrorSummary>());
            }

            try
            {
                var topology = await _etherCATService.GetTopologyAsync();
                var errors = topology.Slaves
                    .Where(s => s.ErrorCounters.HasErrors || s.State.HasError())
                    .Select(s => new SlaveErrorSummary
                    {
                        Position = s.Position,
                        Address = s.ConfiguredAddress,
                        Name = s.Name,
                        State = s.State.ToShortString(),
                        StateColor = s.State.ToColorCode(),
                        ALStatusCode = s.ALStatusCode,
                        ALStatusDescription = s.ALStatusDescription,
                        CRCErrors = s.ErrorCounters.CRCErrorCount,
                        LostLinks = s.ErrorCounters.LostLinkCount,
                        TotalErrors = s.ErrorCounters.TotalErrors
                    })
                    .ToList();

                return Ok(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error counters");
                return StatusCode(500, new List<SlaveErrorSummary>());
            }
        }

        /// <summary>
        /// Obtiene estadísticas del cache de archivos ESI
        /// Muestra cuántos dispositivos/vendors están cargados desde los archivos de TwinCAT
        /// </summary>
        [HttpGet("esi/stats")]
        public ActionResult<ESICacheStats> GetESIStats()
        {
            try
            {
                var stats = _esiParser.GetCacheStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ESI cache stats");
                return StatusCode(500, new ESICacheStats { Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Fuerza recarga del cache de archivos ESI
        /// </summary>
        [HttpPost("esi/refresh")]
        public async Task<ActionResult<ESICacheStats>> RefreshESICache()
        {
            try
            {
                _logger.LogInformation("🌐 ESI cache refresh requested");
                await _esiParser.RefreshCacheAsync();
                var stats = _esiParser.GetCacheStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing ESI cache");
                return StatusCode(500, new ESICacheStats { Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Busca información de un dispositivo por VendorId y ProductCode
        /// </summary>
        [HttpGet("esi/device/{vendorId:long}/{productCode:long}")]
        public ActionResult<ESIDeviceInfo> GetESIDeviceInfo(long vendorId, long productCode)
        {
            try
            {
                var info = _esiParser.GetDeviceInfo((uint)vendorId, (uint)productCode);
                if (info == null)
                {
                    return NotFound($"Device not found: VendorId=0x{vendorId:X4}, ProductCode=0x{productCode:X8}");
                }
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ESI device info");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// 🔍 Busca información de un dispositivo por sType (ej: EL2798, EK1122-0000-0018)
        /// </summary>
        [HttpGet("esi/search/{sType}")]
        public ActionResult<object> SearchESIByType(string sType)
        {
            try
            {
                var info = _esiParser.GetDeviceInfoByType(sType);
                if (info == null)
                {
                    // Buscar tipos similares para ayudar a debuggear
                    var similarTypes = _esiParser.SearchTypes(sType.Substring(0, Math.Min(4, sType.Length)))
                        .Take(20)
                        .ToList();
                    
                    return Ok(new 
                    { 
                        found = false, 
                        searchedFor = sType,
                        message = $"No se encontró dispositivo con tipo '{sType}' en el cache ESI",
                        similarTypesInCache = similarTypes
                    });
                }
                return Ok(new
                {
                    found = true,
                    searchedFor = sType,
                    device = info
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching ESI by type");
                return StatusCode(500, ex.Message);
            }
        }
        
        /// <summary>
        /// 🔍 Lista todos los Types en el cache ESI (filtrado opcional)
        /// </summary>
        [HttpGet("esi/types")]
        public ActionResult<object> ListESITypes([FromQuery] string? filter = null, [FromQuery] int limit = 100)
        {
            try
            {
                IEnumerable<(string Type, string ProductName, string PhysicsRaw)> types;
                
                if (!string.IsNullOrEmpty(filter))
                {
                    types = _esiParser.SearchTypes(filter);
                }
                else
                {
                    types = _esiParser.GetAllCachedTypes()
                        .Select(t => 
                        {
                            var info = _esiParser.GetDeviceInfoByType(t);
                            return (t, info?.ProductName ?? "", info?.PhysicsRaw ?? "");
                        });
                }
                
                var result = types.Take(limit).ToList();
                
                return Ok(new
                {
                    totalInCache = _esiParser.GetAllCachedTypes().Count(),
                    filter = filter ?? "(none)",
                    showing = result.Count,
                    types = result.Select(t => new { type = t.Type, productName = t.ProductName, physicsRaw = t.PhysicsRaw })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing ESI types");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// 🔬 Lee símbolos usando SymbolLoaderFactory (mismo método que Sample06 de Beckhoff)
        /// Este es el método correcto y probado que funciona en Sample06
        /// </summary>
        [HttpGet("symbols-sample06")]
        public async Task<ActionResult<object>> ReadSymbolsSample06(
            [FromQuery] int port = 27905, 
            [FromQuery] string? filter = null,
            [FromQuery] int maxSymbols = 500)
        {
            var config = _etherCATService.GetConfiguration();
            var netIdStr = config.EtherCATMasterNetId;
            
            _logger.LogInformation("🔬 Sample06 Method: Leyendo símbolos de {NetId}:{Port}", netIdStr, port);
            
            var diagnostics = new List<string>();
            var allSymbols = new List<object>();
            var terminalSymbols = new List<object>();
            var inputSymbols = new List<object>();
            var outputSymbols = new List<object>();
            
            try
            {
                using var client = new TwinCAT.Ads.AdsClient();
                var netId = new TwinCAT.Ads.AmsNetId(netIdStr);
                
                client.Connect(netId, port);
                client.Timeout = 10000; // 10 segundos timeout
                
                // Verificar conexión
                var state = client.ReadState();
                diagnostics.Add($"✅ Connected: AdsState={state.AdsState}, DeviceState={state.DeviceState}");
                
                // Leer Device Info
                try
                {
                    var deviceInfo = client.ReadDeviceInfo();
                    diagnostics.Add($"✅ Device: {deviceInfo.Name} v{deviceInfo.Version}");
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"⚠️ Device Info: {ex.Message}");
                }
                
                // ====== MÉTODO: Leer símbolos con Upload Info (0xF00F) y Symbol Data (0xF00B) ======
                // Esta es la forma en que TwinCAT expone los símbolos a través de ADS
                
                uint symbolCount = 0;
                uint symbolSize = 0;
                
                try
                {
                    // Leer información de upload (número de símbolos y tamaño)
                    var uploadInfo = new byte[24];
                    var infoRead = client.Read(0xF00F, 0, uploadInfo.AsMemory());
                    if (infoRead >= 8)
                    {
                        symbolCount = BitConverter.ToUInt32(uploadInfo, 0);
                        symbolSize = BitConverter.ToUInt32(uploadInfo, 4);
                        diagnostics.Add($"✅ Upload Info: {symbolCount} símbolos, {symbolSize} bytes");
                    }
                    else
                    {
                        diagnostics.Add($"⚠️ Upload Info: Solo se leyeron {infoRead} bytes");
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"❌ Upload Info (0xF00F): {ex.ErrorCode}");
                }
                
                // Leer los datos de símbolos si hay
                if (symbolSize > 0 && symbolSize < 100_000_000)
                {
                    try
                    {
                        var symbolBuffer = new byte[symbolSize];
                        var totalRead = client.Read(0xF00B, 0, symbolBuffer.AsMemory());
                        diagnostics.Add($"✅ Symbol Data: {totalRead} bytes leídos");
                        
                        // Parsear símbolos según estructura ADS_SYMBOLENTRY
                        // Estructura: EntryLength(4) + IndexGroup(4) + IndexOffset(4) + Size(4) + DataType(4) + Flags(2) + NameLength(2) + TypeLength(2) + CommentLength(2) + Name + Type + Comment
                        int offset = 0;
                        int parsed = 0;
                        
                        while (offset < totalRead && parsed < maxSymbols)
                        {
                            try
                            {
                                if (offset + 30 > totalRead) break;
                                
                                var entryLength = BitConverter.ToUInt32(symbolBuffer, offset);
                                if (entryLength == 0 || entryLength > 50000 || offset + entryLength > totalRead) break;
                                
                                var ig = BitConverter.ToUInt32(symbolBuffer, offset + 4);
                                var io = BitConverter.ToUInt32(symbolBuffer, offset + 8);
                                var size = BitConverter.ToUInt32(symbolBuffer, offset + 12);
                                var dataType = BitConverter.ToUInt32(symbolBuffer, offset + 16);
                                var flags = BitConverter.ToUInt16(symbolBuffer, offset + 20);
                                var nameLength = BitConverter.ToUInt16(symbolBuffer, offset + 24);
                                var typeLength = BitConverter.ToUInt16(symbolBuffer, offset + 26);
                                var commentLength = BitConverter.ToUInt16(symbolBuffer, offset + 28);
                                
                                var nameStart = offset + 30;
                                var typeStart = nameStart + nameLength + 1;
                                
                                if (nameStart + nameLength <= totalRead)
                                {
                                    var name = System.Text.Encoding.ASCII.GetString(symbolBuffer, nameStart, nameLength).TrimEnd('\0');
                                    var typeName = "";
                                    if (typeStart + typeLength <= totalRead)
                                    {
                                        typeName = System.Text.Encoding.ASCII.GetString(symbolBuffer, typeStart, typeLength).TrimEnd('\0');
                                    }
                                    
                                    var nameLower = name.ToLower();
                                    
                                    // Filtrar si se especificó
                                    if (!string.IsNullOrEmpty(filter) && 
                                        !nameLower.Contains(filter.ToLower()) && 
                                        !typeName.ToLower().Contains(filter.ToLower()))
                                    {
                                        offset += (int)entryLength;
                                        parsed++;
                                        continue;
                                    }
                                    
                                    var symbolInfo = new
                                    {
                                        name,
                                        type = typeName,
                                        size,
                                        indexGroup = $"0x{ig:X4}",
                                        indexOffset = $"0x{io:X}",
                                        dataTypeId = dataType,
                                        flags
                                    };
                                    
                                    allSymbols.Add(symbolInfo);
                                    
                                    // Clasificar por tipo
                                    if (nameLower.StartsWith("inputs") || nameLower.Contains(".input"))
                                    {
                                        inputSymbols.Add(symbolInfo);
                                    }
                                    else if (nameLower.StartsWith("outputs") || nameLower.Contains(".output"))
                                    {
                                        outputSymbols.Add(symbolInfo);
                                    }
                                    
                                    // Detectar terminales EtherCAT
                                    if (nameLower.Contains("term") || nameLower.Contains("el1") || 
                                        nameLower.Contains("el2") || nameLower.Contains("el3") ||
                                        nameLower.Contains("el4") || nameLower.Contains("el5") ||
                                        nameLower.Contains("el6") || nameLower.Contains("el7") ||
                                        nameLower.Contains("el9") || nameLower.Contains("ek1") ||
                                        nameLower.Contains("channel"))
                                    {
                                        terminalSymbols.Add(symbolInfo);
                                    }
                                }
                                
                                offset += (int)entryLength;
                                parsed++;
                            }
                            catch { break; }
                        }
                        
                        diagnostics.Add($"✅ Parseados {allSymbols.Count} símbolos");
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        diagnostics.Add($"❌ Symbol Data (0xF00B): {ex.ErrorCode}");
                    }
                }
                else
                {
                    diagnostics.Add($"⚠️ No hay símbolos disponibles (size={symbolSize})");
                }
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    method = "ADS Symbol Upload (0xF00F/0xF00B)",
                    filter = filter ?? "(all)",
                    diagnostics,
                    summary = new
                    {
                        reportedSymbolCount = symbolCount,
                        totalSymbols = allSymbols.Count,
                        inputSymbols = inputSymbols.Count,
                        outputSymbols = outputSymbols.Count,
                        terminalSymbols = terminalSymbols.Count
                    },
                    terminals = terminalSymbols.Take(50),
                    inputs = inputSymbols.Take(50),
                    outputs = outputSymbols.Take(50),
                    allSymbols = allSymbols.Take(100),
                    hint = "Use ?port=851 o ?port=300 para otros puertos, ?filter=term para filtrar"
                });
            }
            catch (TwinCAT.Ads.AdsErrorException ex)
            {
                _logger.LogError(ex, "Error ADS leyendo símbolos");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    error = $"ADS Error: {ex.ErrorCode}",
                    message = ex.Message,
                    hint = "Verificar que el puerto sea correcto. Sample06 usa puerto 27905."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo símbolos");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    error = ex.Message,
                    stackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))
                });
            }
        }

        /// <summary>
        /// 🔬 Lee información de TODOS los esclavos EtherCAT directamente del Master
        /// Usa Index Groups específicos del EtherCAT Master (no depende de Create Symbols)
        /// </summary>
        [HttpGet("master-slaves")]
        public async Task<ActionResult<object>> ReadMasterSlaves([FromQuery] int port = 27905)
        {
            var config = _etherCATService.GetConfiguration();
            var netIdStr = config.EtherCATMasterNetId;
            
            _logger.LogInformation("🔬 Leyendo esclavos del EtherCAT Master {NetId}:{Port}", netIdStr, port);
            
            var diagnostics = new List<string>();
            var slaves = new List<object>();
            var indexGroupTests = new List<object>();
            
            try
            {
                using var client = new TwinCAT.Ads.AdsClient();
                var netId = new TwinCAT.Ads.AmsNetId(netIdStr);
                
                client.Connect(netId, port);
                client.Timeout = 5000;
                
                // Verificar conexión
                var state = client.ReadState();
                diagnostics.Add($"✅ Connected: AdsState={state.AdsState}, DeviceState={state.DeviceState}");
                
                // ====== Index Groups del EtherCAT Master (Documentación Beckhoff) ======
                // 0x9020 + slaveId = Slave Information Interface (CoE)
                // 0xF302 = EtherCAT Master specific
                // 0x1000 = Physical I/O
                // 0xA000 = Register Access
                
                // Intentar diferentes Index Groups para obtener información de esclavos
                var ecIndexGroups = new (uint ig, uint ioBase, int maxSlaves, string desc)[]
                {
                    // EtherCAT CoE (CANopen over EtherCAT) - Slave Object Dictionary
                    (0x9020, 0, 20, "EC CoE Slave 0-19"),
                    
                    // EtherCAT Master specific Index Groups
                    (0xF302, 0, 10, "EC Master Slave Info"),
                    
                    // Topology/Device Information
                    (0x2000, 0, 10, "Device Topology"),
                    (0xF200, 0, 10, "Device List"),
                    
                    // Box/Terminal specific
                    (0xF100, 0, 5, "Box Information"),
                };
                
                foreach (var (igBase, ioBase, maxSlaves, desc) in ecIndexGroups)
                {
                    var groupResults = new List<object>();
                    
                    for (int slaveId = 0; slaveId < maxSlaves; slaveId++)
                    {
                        try
                        {
                            // Intentar leer información básica del esclavo
                            var buffer = new byte[256];
                            
                            // Probar diferentes combinaciones de IG/IO
                            var testCases = new (uint ig, uint io)[]
                            {
                                (igBase + (uint)slaveId, 0),           // IG = base + slaveId
                                (igBase, (uint)slaveId),               // IO = slaveId
                                (igBase, ioBase + (uint)slaveId),      // IO = base + slaveId
                                (igBase + (uint)slaveId, 0x1000),      // IG + slave, IO = identity
                                (igBase + (uint)slaveId, 0x1008),      // IG + slave, IO = device name
                            };
                            
                            foreach (var (ig, io) in testCases)
                            {
                                try
                                {
                                    var bytesRead = client.Read(ig, io, buffer.AsMemory());
                                    if (bytesRead > 0)
                                    {
                                        var hexData = BitConverter.ToString(buffer.Take(Math.Min(32, bytesRead)).ToArray()).Replace("-", " ");
                                        
                                        // Intentar interpretar como string
                                        string? strValue = null;
                                        if (buffer.Take(bytesRead).All(b => b >= 32 && b < 127 || b == 0))
                                        {
                                            strValue = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead).TrimEnd('\0');
                                            if (string.IsNullOrWhiteSpace(strValue)) strValue = null;
                                        }
                                        
                                        groupResults.Add(new
                                        {
                                            slaveId,
                                            ig = $"0x{ig:X4}",
                                            io = $"0x{io:X4}",
                                            bytesRead,
                                            data = hexData,
                                            strValue
                                        });
                                        
                                        // Si encontramos datos, es un esclavo válido
                                        if (bytesRead > 4 && !slaves.Any(s => ((dynamic)s).slaveId == slaveId))
                                        {
                                            slaves.Add(new
                                            {
                                                slaveId,
                                                indexGroup = $"0x{ig:X4}",
                                                indexOffset = $"0x{io:X4}",
                                                dataSize = bytesRead,
                                                rawData = hexData,
                                                name = strValue ?? $"Slave {slaveId}"
                                            });
                                        }
                                    }
                                }
                                catch (TwinCAT.Ads.AdsErrorException)
                                {
                                    // Silenciar errores de lectura individual
                                }
                            }
                        }
                        catch
                        {
                            // Continuar con el siguiente esclavo
                        }
                    }
                    
                    if (groupResults.Count > 0)
                    {
                        indexGroupTests.Add(new
                        {
                            indexGroup = $"0x{igBase:X4}",
                            description = desc,
                            results = groupResults
                        });
                        diagnostics.Add($"✅ {desc}: {groupResults.Count} lecturas exitosas");
                    }
                }
                
                // ====== Método alternativo: Escanear rango de offsets conocidos ======
                diagnostics.Add("🔍 Escaneando offsets conocidos para detectar terminales...");
                
                // Los terminales suelen estar en offsets consecutivos
                // Term 12 está en 0x148-0x157, busquemos más
                var terminalScan = new List<object>();
                
                // Escanear un rango amplio de offsets en 0xF031 (Process Data)
                for (uint offset = 0x100; offset < 0x200; offset++)
                {
                    try
                    {
                        var buffer = new byte[1];
                        var bytesRead = client.Read(0xF031, offset, buffer.AsMemory());
                        if (bytesRead > 0)
                        {
                            terminalScan.Add(new
                            {
                                offset = $"0x{offset:X}",
                                value = buffer[0]
                            });
                        }
                    }
                    catch
                    {
                        // Ignorar errores
                    }
                }
                
                if (terminalScan.Count > 0)
                {
                    diagnostics.Add($"✅ Encontrados {terminalScan.Count} offsets válidos en 0xF031");
                }
                
                // ====== Probar Index Groups específicos de información de Master ======
                var masterInfo = new Dictionary<string, object>();
                
                var masterIGs = new (uint ig, uint io, int size, string name)[]
                {
                    (0xF302, 0x00000000, 4, "Slave Count"),
                    (0xF302, 0x00010000, 256, "Master Device Name"),
                    (0xF302, 0x00020000, 4, "Master State"),
                    (0x1000, 0, 256, "Physical Input"),
                    (0x1010, 0, 256, "Physical Output"),
                    (0xF020, 0, 256, "I/O Input Image"),
                    (0xF030, 0, 256, "I/O Output Image"),
                };
                
                foreach (var (ig, io, size, name) in masterIGs)
                {
                    try
                    {
                        var buffer = new byte[size];
                        var bytesRead = client.Read(ig, io, buffer.AsMemory());
                        
                        var hexData = BitConverter.ToString(buffer.Take(Math.Min(64, bytesRead)).ToArray()).Replace("-", " ");
                        
                        // Interpretar valores
                        object? interpreted = null;
                        if (bytesRead == 4)
                        {
                            interpreted = BitConverter.ToUInt32(buffer, 0);
                        }
                        else if (bytesRead > 0 && buffer.Take(bytesRead).All(b => b >= 32 && b < 127 || b == 0))
                        {
                            interpreted = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead).TrimEnd('\0');
                        }
                        
                        masterInfo[name] = new
                        {
                            ig = $"0x{ig:X4}",
                            io = $"0x{io:X8}",
                            bytesRead,
                            data = hexData,
                            value = interpreted
                        };
                        
                        diagnostics.Add($"✅ {name}: {bytesRead} bytes");
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        masterInfo[name] = new { error = ex.ErrorCode.ToString() };
                    }
                }
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    method = "EtherCAT Master Direct Read",
                    diagnostics,
                    slavesFound = slaves.Count,
                    slaves,
                    masterInfo,
                    terminalScanCount = terminalScan.Count,
                    terminalScan = terminalScan.Take(50),
                    indexGroupTests,
                    hint = "Esta información viene directamente del EtherCAT Master, no de los símbolos"
                });
            }
            catch (TwinCAT.Ads.AdsErrorException ex)
            {
                _logger.LogError(ex, "Error ADS leyendo esclavos del Master");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    error = $"ADS Error: {ex.ErrorCode}",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo esclavos del Master");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 🔬 Lee diagnóstico EtherCAT desde FB_EtherCATDiag del PLC (puerto 851)
        /// Usa las estructuras ST_SlaveStateInfo, ST_TopologyData, etc.
        /// </summary>
        [HttpGet("plc-diag")]
        public async Task<ActionResult<object>> ReadPLCDiagnostic([FromQuery] string? fbInstance = null)
        {
            var config = _etherCATService.GetConfiguration();
            var netIdStr = config.EtherCATMasterNetId;
            const int port = 851;
            
            // Usar el valor del Excel si no se especifica en el query
            var effectiveFbInstance = string.IsNullOrWhiteSpace(fbInstance) 
                ? config.EtherCATDiagFbInstance 
                : fbInstance;
            
            _logger.LogInformation("🔬 Leyendo FB_EtherCATDiag desde PLC {NetId}:{Port}, instancia: {FB}", netIdStr, port, effectiveFbInstance);
            
            var diagnostics = new List<string>();
            var masterState = new Dictionary<string, object>();
            var slaves = new List<object>();
            
            try
            {
                using var client = new TwinCAT.Ads.AdsClient();
                var netId = new TwinCAT.Ads.AmsNetId(netIdStr);
                
                client.Connect(netId, port);
                client.Timeout = 5000;
                
                var plcState = client.ReadState();
                diagnostics.Add($"✅ Connected to PLC: AdsState={plcState.AdsState}");
                
                // ====== Leer variables de salida del FB ======
                var outputVars = new (string name, string type, int size)[]
                {
                    ("bEtherCATOK", "BOOL", 1),
                    ("bFrameWcStateError", "BOOL", 1),
                    ("bSlaveCountError", "BOOL", 1),
                    ("bMasterDevStateError", "BOOL", 1),
                    ("bBusy", "BOOL", 1),
                    ("bError", "BOOL", 1),
                    ("iErrorID", "UDINT", 4),
                };
                
                foreach (var (varName, varType, size) in outputVars)
                {
                    try
                    {
                        var fullName = $"{effectiveFbInstance}.{varName}";
                        var handle = client.CreateVariableHandle(fullName);
                        var buffer = new byte[size];
                        var bytesRead = client.Read(handle, buffer.AsMemory());
                        client.DeleteVariableHandle(handle);
                        
                        object value = size == 1 ? buffer[0] != 0 : BitConverter.ToUInt32(buffer, 0);
                        masterState[varName] = value;
                        diagnostics.Add($"✅ {varName}: {value}");
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        diagnostics.Add($"❌ {varName}: {ex.ErrorCode}");
                    }
                }
                
                // ====== Leer stMasterDevState (ST_EcMasterDevState) ======
                try
                {
                    var handle = client.CreateVariableHandle($"{effectiveFbInstance}.stMasterDevState");
                    var buffer = new byte[32]; // Tamaño aproximado de ST_EcMasterDevState
                    var bytesRead = client.Read(handle, buffer.AsMemory());
                    client.DeleteVariableHandle(handle);
                    
                    // Parsear ST_EcMasterDevState
                    var eEcState = BitConverter.ToUInt16(buffer, 0);
                    var stateName = eEcState switch
                    {
                        0 => "UNDEFINED",
                        1 => "INIT",
                        2 => "PREOP",
                        3 => "BOOT",
                        4 => "SAFEOP",
                        8 => "OP",
                        _ => $"UNKNOWN({eEcState})"
                    };
                    
                    // Los flags están después de los reserved (offset ~8)
                    var flagsByte = buffer[8];
                    
                    masterState["stMasterDevState"] = new
                    {
                        eEcState = stateName,
                        rawState = eEcState,
                        bLinkError = (flagsByte & 0x01) != 0,
                        bResetRequired = (flagsByte & 0x02) != 0,
                        bMissFrmRedMode = (flagsByte & 0x04) != 0,
                        bWatchdogTriggerd = (flagsByte & 0x08) != 0,
                        bDriverNotFound = (flagsByte & 0x10) != 0,
                        bResetActive = (flagsByte & 0x20) != 0,
                        bAtLeastOneNotInOp = (flagsByte & 0x40) != 0,
                        bDcNotInSync = (flagsByte & 0x80) != 0,
                        rawBytes = BitConverter.ToString(buffer.Take(bytesRead).ToArray()).Replace("-", " ")
                    };
                    diagnostics.Add($"✅ stMasterDevState: {stateName}");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"❌ stMasterDevState: {ex.ErrorCode}");
                }
                
                // ====== Leer arrDiagSlaveInfo (ARRAY OF ST_SlaveStateInfo) ======
                // ST_SlaveStateInfo tiene aproximadamente:
                // nIndex: DINT (4) + sName: STRING(80) (81) + sType: STRING(80) (81) + nECAddr: UINT (2) 
                // + bDiagData: BOOL (1) + stPortCRCErrors (~20) + nSumCRCErrors: UDINT (4) + stState (~20)
                // Total aproximado: ~220 bytes por esclavo
                
                const int maxSlaves = 256; // iSLAVEADDR_ARR_SIZE = 256
                const int slaveInfoSize = 256; // Tamaño estimado de ST_SlaveStateInfo
                
                // Primero intentar leer el conteo de esclavos
                int slaveCount = 256; // iSLAVEADDR_ARR_SIZE por defecto
                
                try
                {
                    var handle = client.CreateVariableHandle($"{effectiveFbInstance}.iNumOfSlavesRead");
                    var buffer = new byte[2];
                    client.Read(handle, buffer.AsMemory());
                    client.DeleteVariableHandle(handle);
                    slaveCount = BitConverter.ToUInt16(buffer, 0);
                    diagnostics.Add($"✅ Número de esclavos: {slaveCount}");
                }
                catch
                {
                    diagnostics.Add($"⚠️ No se pudo leer iNumOfSlavesRead, usando array externo");
                }
                
                // Leer el array interno de esclavos
                try
                {
                    var handle = client.CreateVariableHandle($"{effectiveFbInstance}.arrSlaveInfo");
                    // Leer todo el array - usar un buffer grande para detectar el tamaño real
                    const int maxArrayElements = 257; // ARRAY[0..256]
                    const int maxElementSize = 320;   // Tamaño máximo estimado por elemento
                    var buffer = new byte[maxArrayElements * maxElementSize];
                    var bytesRead = client.Read(handle, buffer.AsMemory());
                    client.DeleteVariableHandle(handle);
                    
                    diagnostics.Add($"✅ arrSlaveInfo: {bytesRead} bytes leídos");
                    
                    // ⭐ MÉTODO MEJORADO: Detectar tamaño real Y offset de nECAddr dinámicamente
                    // Posibles offsets donde puede estar nECAddr:
                    // - 247/248 = con sESIfile: nIndex(4) + sName(81) + sType(81) + sESIfile(81) = 247 (con padding: 248)
                    // - 166 = sin sESIfile: nIndex(4) + sName(81) + sType(81) = 166
                    int actualSlaveSize = 0;
                    int nECAddrOffset = 248; // Default: con sESIfile y padding
                    
                    int[] possibleNECAddrOffsets = { 248, 247, 166 };
                    int[] possibleSizes = { 292, 290, 288, 296, 294, 212, 210, 208, 214 };
                    
                    bool patternFound = false;
                    foreach (var testOffset in possibleNECAddrOffsets)
                    {
                        if (patternFound) break;
                        foreach (var testSize in possibleSizes)
                        {
                            // Necesitamos al menos 3 estructuras para verificar
                            if (testOffset + (testSize * 3) > bytesRead)
                                continue;
                            
                            // Leer 3 valores de nECAddr consecutivos
                            var addr1 = BitConverter.ToUInt16(buffer, testOffset);
                            var addr2 = BitConverter.ToUInt16(buffer, testOffset + testSize);
                            var addr3 = BitConverter.ToUInt16(buffer, testOffset + (testSize * 2));
                            
                            // Verificar que son válidos (rango 1001-1256) y consecutivos
                            if (addr1 >= 1001 && addr1 <= 1256 &&
                                addr2 == addr1 + 1 &&
                                addr3 == addr1 + 2)
                            {
                                actualSlaveSize = testSize;
                                nECAddrOffset = testOffset;
                                diagnostics.Add($"✅ Patrón detectado: nECAddr en offset {nECAddrOffset}, tamaño = {actualSlaveSize} bytes");
                                diagnostics.Add($"   Valores: {addr1}, {addr2}, {addr3}");
                                patternFound = true;
                                break;
                            }
                        }
                    }
                    
                    // Fallback si ningún método funcionó
                    if (actualSlaveSize == 0)
                    {
                        actualSlaveSize = 288; // Valor más común detectado
                        nECAddrOffset = 248;
                        diagnostics.Add($"⚠️ No se pudo detectar patrón, usando defaults: size={actualSlaveSize}, offset={nECAddrOffset}");
                    }
                    
                    diagnostics.Add($"📊 Elementos estimados en array: {bytesRead / actualSlaveSize}");
                    
                    // Parsear cada esclavo usando offsets dinámicos
                    for (int i = 0; i < slaveCount && (i + 1) * actualSlaveSize <= bytesRead; i++)
                    {
                        int offset = i * actualSlaveSize;
                        
                        // Parsear ST_SlaveStateInfo
                        var nIndex = BitConverter.ToInt32(buffer, offset);
                        
                        // STRING en TwinCAT: texto directo
                        // STRING(80) ocupa 81 bytes (80 chars + null terminator)
                        // Pero TwinCAT puede añadir padding para alineación
                        var sNameOffset = offset + 4;
                        var sName = ExtractTwinCATString(buffer, sNameOffset, 81);
                        
                        // sType empieza después de sName
                        // Offset actual: 4 (nIndex) + 81 (sName) = 85
                        var sTypeOffset = sNameOffset + 81;
                        var sType = ExtractTwinCATString(buffer, sTypeOffset, 81);
                        
                        // ⭐ USAR OFFSET DETECTADO DINÁMICAMENTE para nECAddr
                        var nECAddr = BitConverter.ToUInt16(buffer, offset + nECAddrOffset);
                        var bDiagData = buffer[offset + nECAddrOffset + 2] != 0;
                        
                        // Estructura después de nECAddr:
                        // nECAddr(2) + bDiagData(1) + padding(1) + stPortCRCErrors(16) + nSumCRCErrors(4) = 24 bytes hasta stState
                        var nSumCRCErrorsOffset = offset + nECAddrOffset + 20; // 2+1+1+16 = 20
                        var nSumCRCErrors = 0u;
                        if (nSumCRCErrorsOffset + 4 <= buffer.Length)
                        {
                            nSumCRCErrors = BitConverter.ToUInt32(buffer, nSumCRCErrorsOffset);
                        }
                        
                        // Parsear stState (está en nECAddrOffset + 24)
                        var stateOffset = offset + nECAddrOffset + 24;
                        var slaveStateData = ParseSlaveState(buffer, stateOffset, buffer.Length);
                        
                        // ⭐ DEBUG: Mostrar bytes raw de stState para los primeros 3 esclavos
                        if (i < 3)
                        {
                            var rawStateBytes = new byte[16];
                            Array.Copy(buffer, stateOffset, rawStateBytes, 0, Math.Min(16, buffer.Length - stateOffset));
                            diagnostics.Add($"🔬 Slave[{i}] stState @offset={stateOffset}: {BitConverter.ToString(rawStateBytes)}");
                        }
                        
                        // ⭐ Buscar información ESI por sType para obtener Physics de puertos
                        var esiInfo = _esiParser.GetDeviceInfoByType(sType.Trim());
                        object? esiData = null;
                        if (esiInfo != null)
                        {
                            esiData = new
                            {
                                productName = esiInfo.ProductName,
                                physicsRaw = esiInfo.PhysicsRaw,
                                ports = esiInfo.PortPhysics.Select(p => new
                                {
                                    port = p.PortNumber,
                                    type = p.PhysicsType,
                                    isCable = p.IsCable,
                                    isEBus = p.IsEBus
                                }).ToList()
                            };
                        }
                        
                        slaves.Add(new
                        {
                            index = i,
                            nIndex,
                            sName = sName.Trim(),
                            sType = sType.Trim(),
                            nECAddr,
                            bDiagData,
                            nSumCRCErrors,
                            state = slaveStateData,
                            esi = esiData
                        });
                    }
                    
                    diagnostics.Add($"📊 Esclavos parseados: {slaves.Count}");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"❌ arrSlaveInfo: {ex.ErrorCode}");
                    
                    // Intentar leer el array externo arrDiagSlaveInfo
                    try
                    {
                        diagnostics.Add("⚠️ Intentando leer arrDiagSlaveInfo externo...");
                        var handle = client.CreateVariableHandle("arrDiagSlaveInfo");
                        var buffer = new byte[4 * slaveInfoSize]; // [0..3]
                        var bytesRead = client.Read(handle, buffer.AsMemory());
                        client.DeleteVariableHandle(handle);
                        
                        diagnostics.Add($"✅ arrDiagSlaveInfo: {bytesRead} bytes");
                        // Parsear igual que arriba...
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex2)
                    {
                        diagnostics.Add($"❌ arrDiagSlaveInfo: {ex2.ErrorCode}");
                    }
                }
                
                // ====== Leer Topology Data ======
                var topologyData = new List<object>();
                try
                {
                    var handle = client.CreateVariableHandle($"{effectiveFbInstance}.arrTopologyData");
                    // Leer más datos para detectar el tamaño real
                    var buffer = new byte[slaveCount * 128]; // Buffer generoso
                    var bytesRead = client.Read(handle, buffer.AsMemory());
                    client.DeleteVariableHandle(handle);
                    
                    diagnostics.Add($"✅ arrTopologyData: {bytesRead} bytes leídos para {slaveCount} esclavos");
                    
                    // Detectar el tamaño real buscando patrones
                    // El primer esclavo debería tener physicalAddr = 1001 en offset 0
                    // El segundo esclavo debería tener physicalAddr = 1002 en offset = topoSize
                    int detectedTopoSize = 64; // default
                    
                    // Buscar dónde está el segundo physicalAddr (1002)
                    for (int searchSize = 20; searchSize <= 128 && searchSize < bytesRead; searchSize += 2)
                    {
                        var testAddr = BitConverter.ToUInt16(buffer, searchSize);
                        if (testAddr == 1002)
                        {
                            detectedTopoSize = searchSize;
                            diagnostics.Add($"🔍 Detectado tamaño ST_TopologyData: {detectedTopoSize} bytes (encontrado 1002 en offset {searchSize})");
                            break;
                        }
                    }
                    
                    int topoSize = detectedTopoSize;
                    diagnostics.Add($"📊 Usando topoSize={topoSize}, bytesRead={bytesRead}, maxSlaves={bytesRead/topoSize}");
                    
                    // Mostrar los primeros bytes raw para debug
                    var rawFirst64 = BitConverter.ToString(buffer.Take(Math.Min(64, bytesRead)).ToArray()).Replace("-", " ");
                    diagnostics.Add($"📊 Raw bytes [0-63]: {rawFirst64}");
                    
                    // ⭐ Crear un diccionario de sType por physAddr para enriquecer topología
                    // IMPORTANTE: Filtrar nECAddr > 0 para evitar duplicados con Key=0
                    var slaveTypeByAddr = new Dictionary<ushort, string>();
                    foreach (var s in slaves)
                    {
                        if (s is null) continue;
                        
                        var props = s.GetType().GetProperties();
                        var nECAddrProp = props.FirstOrDefault(p => p.Name == "nECAddr");
                        var sTypeProp = props.FirstOrDefault(p => p.Name == "sType");
                        
                        if (nECAddrProp != null && sTypeProp != null)
                        {
                            var nECAddr = Convert.ToUInt16(nECAddrProp.GetValue(s));
                            var sType = sTypeProp.GetValue(s)?.ToString() ?? "";
                            
                            // Solo agregar si nECAddr > 0 y no existe ya
                            if (nECAddr > 0 && !slaveTypeByAddr.ContainsKey(nECAddr))
                            {
                                slaveTypeByAddr[nECAddr] = sType;
                            }
                        }
                    }
                    
                    for (int i = 0; i < slaveCount && (i + 1) * topoSize <= bytesRead; i++)
                    {
                        int off = i * topoSize;
                        var physAddr = BitConverter.ToUInt16(buffer, off);
                        var autoIncAddr = BitConverter.ToUInt16(buffer, off + 2);
                        
                        // ST_PortAddr (4 x UINT = 8 bytes)
                        var portAPhys = BitConverter.ToUInt16(buffer, off + 4);
                        var portBPhys = BitConverter.ToUInt16(buffer, off + 6);
                        var portCPhys = BitConverter.ToUInt16(buffer, off + 8);
                        var portDPhys = BitConverter.ToUInt16(buffer, off + 10);
                        
                        if (physAddr > 0)
                        {
                            // Debug: mostrar los primeros esclavos
                            if (i < 5 || (physAddr >= 1027 && physAddr <= 1031))
                            {
                                diagnostics.Add($"📊 Topo[{i}] @offset={off}: physAddr={physAddr}, ports=[A:{portAPhys}, B:{portBPhys}, C:{portCPhys}, D:{portDPhys}]");
                            }
                            
                            // ⭐ Obtener info ESI para este esclavo
                            var sType = slaveTypeByAddr.GetValueOrDefault(physAddr, "");
                            // Extraer tipo base: "EK1122-0000-0018" → "EK1122"
                            var sTypeBase = sType.Contains('-') ? sType.Split('-')[0] : sType;
                            var esiInfo = !string.IsNullOrEmpty(sTypeBase) ? _esiParser.GetDeviceInfoByType(sTypeBase) : null;
                            
                            // Construir info de puertos enriquecida con conector físico
                            var portPhysicsA = esiInfo?.PortPhysics.FirstOrDefault(p => p.PortNumber == 0);
                            var portPhysicsB = esiInfo?.PortPhysics.FirstOrDefault(p => p.PortNumber == 1);
                            var portPhysicsC = esiInfo?.PortPhysics.FirstOrDefault(p => p.PortNumber == 2);
                            var portPhysicsD = esiInfo?.PortPhysics.FirstOrDefault(p => p.PortNumber == 3);
                            
                            var portDetails = new[]
                            {
                                new { 
                                    port = "A", 
                                    connectedTo = portAPhys, 
                                    physics = portPhysicsA?.PhysicsType ?? "Unknown",
                                    connector = portPhysicsA?.ConnectorName ?? "",  // X1, X2, etc.
                                    isCable = portPhysicsA?.IsCable ?? false
                                },
                                new { 
                                    port = "B", 
                                    connectedTo = portBPhys, 
                                    physics = portPhysicsB?.PhysicsType ?? "Unknown",
                                    connector = portPhysicsB?.ConnectorName ?? "",
                                    isCable = portPhysicsB?.IsCable ?? false
                                },
                                new { 
                                    port = "C", 
                                    connectedTo = portCPhys, 
                                    physics = portPhysicsC?.PhysicsType ?? "Unknown",
                                    connector = portPhysicsC?.ConnectorName ?? "",
                                    isCable = portPhysicsC?.IsCable ?? false
                                },
                                new { 
                                    port = "D", 
                                    connectedTo = portDPhys, 
                                    physics = portPhysicsD?.PhysicsType ?? "Unknown",
                                    connector = portPhysicsD?.ConnectorName ?? "",
                                    isCable = portPhysicsD?.IsCable ?? false
                                }
                            };
                            
                            topologyData.Add(new
                            {
                                slaveIndex = i,
                                physicalAddr = physAddr,
                                autoIncAddr,
                                sType,
                                physicsRaw = esiInfo?.PhysicsRaw ?? "",
                                ports = new { portA = portAPhys, portB = portBPhys, portC = portCPhys, portD = portDPhys },
                                portDetails
                            });
                        }
                    }
                    diagnostics.Add($"📊 Total topologyData entries: {topologyData.Count}");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrTopologyData: {ex.ErrorCode}");
                }
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    fbInstance = effectiveFbInstance,
                    method = "FB_EtherCATDiag (Puerto 851)",
                    diagnostics,
                    masterState,
                    slavesFound = slaves.Count,
                    slaves,
                    topologyCount = topologyData.Count,
                    topology = topologyData,
                    hint = "Configurable en Excel: EtherCATDiagFbInstance"
                });
            }
            catch (TwinCAT.Ads.AdsErrorException ex)
            {
                _logger.LogError(ex, "Error leyendo FB_EtherCATDiag");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    port,
                    fbInstance = effectiveFbInstance,
                    error = $"ADS Error: {ex.ErrorCode}",
                    message = ex.Message,
                    hint = "Verificar configuración en Excel: EtherCATDiagFbInstance"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo FB_EtherCATDiag");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    error = ex.Message
                });
            }
        }
        
        // Helper: Extraer STRING de TwinCAT
        // TwinCAT STRING(n) ocupa n+1 bytes: n caracteres + null terminator
        // NO tiene byte de longitud al inicio (a diferencia de algunos otros formatos)
        private string ExtractTwinCATString(byte[] buffer, int offset, int maxLen)
        {
            if (offset >= buffer.Length) return "";
            
            try
            {
                // Leer hasta encontrar null o alcanzar maxLen
                int availableLen = Math.Min(maxLen, buffer.Length - offset);
                int len = 0;
                while (len < availableLen && buffer[offset + len] != 0)
                {
                    len++;
                }
                
                return System.Text.Encoding.ASCII.GetString(buffer, offset, len);
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// 🔬 Lee TODOS los esclavos desde FB_EtherCATDiag.arrSlaveInfo (variables internas VAR)
        /// Basado en el XML exportado del FB - Lee: arrSlaveInfo, arrTopologyData, arrSlaveStates
        /// </summary>
        [HttpGet("plc-diag-full")]
        public async Task<ActionResult<object>> ReadPLCDiagnosticFull([FromQuery] string? fbInstance = null)
        {
            var config = _etherCATService.GetConfiguration();
            var netIdStr = config.EtherCATMasterNetId;
            const int port = 851;
            
            // Usar el valor del Excel si no se especifica en el query
            var effectiveFbInstance = string.IsNullOrWhiteSpace(fbInstance) 
                ? config.EtherCATDiagFbInstance 
                : fbInstance;
            
            _logger.LogInformation("🔬 Leyendo FB_EtherCATDiag COMPLETO desde PLC {NetId}:{Port}, instancia: {FB}", netIdStr, port, effectiveFbInstance);
            
            var diagnostics = new List<string>();
            var result = new Dictionary<string, object>();
            
            try
            {
                using var client = new TwinCAT.Ads.AdsClient();
                var netId = new TwinCAT.Ads.AmsNetId(netIdStr);
                
                client.Connect(netId, port);
                client.Timeout = 10000; // 10 segundos para arrays grandes
                
                var plcState = client.ReadState();
                diagnostics.Add($"✅ Connected: AdsState={plcState.AdsState}");
                
                // ====== 1. Leer VAR_OUTPUT (estado general) ======
                var outputs = new Dictionary<string, object>();
                var outputVars = new (string name, int size, Func<byte[], object> parse)[]
                {
                    ("bEtherCATOK", 1, b => b[0] != 0),
                    ("bFrameWcStateError", 1, b => b[0] != 0),
                    ("bSlaveCountError", 1, b => b[0] != 0),
                    ("bMasterDevStateError", 1, b => b[0] != 0),
                    ("bBusy", 1, b => b[0] != 0),
                    ("bError", 1, b => b[0] != 0),
                    ("iErrorID", 4, b => BitConverter.ToUInt32(b, 0)),
                };
                
                foreach (var (varName, size, parse) in outputVars)
                {
                    try
                    {
                        var handle = client.CreateVariableHandle($"{effectiveFbInstance}.{varName}");
                        var buffer = new byte[size];
                        client.Read(handle, buffer.AsMemory());
                        client.DeleteVariableHandle(handle);
                        outputs[varName] = parse(buffer);
                    }
                    catch (TwinCAT.Ads.AdsErrorException ex)
                    {
                        outputs[varName] = $"Error: {ex.ErrorCode}";
                    }
                }
                result["outputs"] = outputs;
                diagnostics.Add($"✅ VAR_OUTPUT leídas");
                
                // ====== 2. Leer VAR_INPUT (configuración) ======
                var inputs = new Dictionary<string, object>();
                try
                {
                    // nSlaveCount (UINT)
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.nSlaveCount");
                    var buf = new byte[2];
                    client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    inputs["nSlaveCount"] = BitConverter.ToUInt16(buf, 0);
                    
                    // nSlaveCountCfg (UINT)
                    h = client.CreateVariableHandle($"{effectiveFbInstance}.nSlaveCountCfg");
                    client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    inputs["nSlaveCountCfg"] = BitConverter.ToUInt16(buf, 0);
                    
                    // nMasterDevState (WORD)
                    h = client.CreateVariableHandle($"{effectiveFbInstance}.nMasterDevState");
                    client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    inputs["nMasterDevState"] = $"0x{BitConverter.ToUInt16(buf, 0):X4}";
                }
                catch { }
                result["inputs"] = inputs;
                
                // ====== 3. Leer iNumOfSlavesRead (VAR interna) ======
                int slaveCount = 0;
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.iNumOfSlavesRead");
                    var buf = new byte[2];
                    client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    slaveCount = BitConverter.ToUInt16(buf, 0);
                    result["iNumOfSlavesRead"] = slaveCount;
                    diagnostics.Add($"✅ iNumOfSlavesRead: {slaveCount}");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"❌ iNumOfSlavesRead: {ex.ErrorCode}");
                    slaveCount = 20; // Valor por defecto para intentar
                }
                
                // ====== 4. Leer arrSlaveAddresses (ARRAY[0..256] OF UINT) ======
                var slaveAddresses = new List<ushort>();
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.arrSlaveAddresses");
                    var buf = new byte[(slaveCount + 1) * 2]; // UINT = 2 bytes
                    var bytesRead = client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    
                    for (int i = 0; i <= slaveCount && i * 2 < bytesRead; i++)
                    {
                        var addr = BitConverter.ToUInt16(buf, i * 2);
                        if (addr > 0) slaveAddresses.Add(addr);
                    }
                    diagnostics.Add($"✅ arrSlaveAddresses: {slaveAddresses.Count} direcciones");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrSlaveAddresses: {ex.ErrorCode}");
                }
                result["slaveAddresses"] = slaveAddresses;
                
                // ====== 5. Leer arrSlaveStates (ARRAY[0..256] OF ST_EcSlaveState) ======
                // ST_EcSlaveState = deviceState: WORD + linkState: WORD = 4 bytes
                var slaveStates = new List<object>();
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.arrSlaveStates");
                    var stateSize = 4; // WORD + WORD
                    var buf = new byte[(slaveCount + 1) * stateSize];
                    var bytesRead = client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    
                    for (int i = 0; i <= slaveCount && i * stateSize < bytesRead; i++)
                    {
                        var deviceState = BitConverter.ToUInt16(buf, i * stateSize);
                        var linkState = BitConverter.ToUInt16(buf, i * stateSize + 2);
                        
                        if (deviceState > 0 || linkState > 0)
                        {
                            var ecState = deviceState & 0x0F;
                            slaveStates.Add(new
                            {
                                index = i,
                                address = i < slaveAddresses.Count ? slaveAddresses[i] : 0,
                                deviceState = $"0x{deviceState:X4}",
                                linkState = $"0x{linkState:X2}",
                                ecState = ecState switch
                                {
                                    0 => "UNKNOWN",
                                    1 => "INIT",
                                    2 => "PREOP",
                                    3 => "BOOT",
                                    4 => "SAFEOP",
                                    8 => "OP",
                                    _ => $"STATE_{ecState}"
                                },
                                hasError = (deviceState & 0x10) != 0,
                                invalidVPRS = (deviceState & 0x20) != 0,
                                linkPortA = (linkState & 0x10) != 0,
                                linkPortB = (linkState & 0x20) != 0,
                                linkPortC = (linkState & 0x40) != 0,
                                linkPortD = (linkState & 0x80) != 0
                            });
                        }
                    }
                    diagnostics.Add($"✅ arrSlaveStates: {slaveStates.Count} estados");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrSlaveStates: {ex.ErrorCode}");
                }
                result["slaveStates"] = slaveStates;
                
                // ====== 6. Leer arrTopologyData (ARRAY[0..256] OF ST_TopologyData) ======
                // ST_TopologyData: iOwnPhysicalAddr(2) + iOwnAutoIncAddr(2) + stPhysicalAddr(8) + stAutoIncAddr(8) + iPortDelay[0..2](6) = ~26 bytes
                var topology = new List<object>();
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.arrTopologyData");
                    var topoSize = 32; // Aproximado, puede variar
                    var buf = new byte[(slaveCount + 1) * topoSize];
                    var bytesRead = client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    
                    // Calcular tamaño real
                    int actualTopoSize = bytesRead / Math.Max(1, slaveCount);
                    diagnostics.Add($"📊 ST_TopologyData size: ~{actualTopoSize} bytes");
                    
                    for (int i = 0; i <= slaveCount && i * actualTopoSize < bytesRead; i++)
                    {
                        int off = i * actualTopoSize;
                        var physAddr = BitConverter.ToUInt16(buf, off);
                        var autoIncAddr = BitConverter.ToUInt16(buf, off + 2);
                        
                        // ST_PortAddr (4 puertos x UINT)
                        var portAPhys = BitConverter.ToUInt16(buf, off + 4);
                        var portBPhys = BitConverter.ToUInt16(buf, off + 6);
                        var portCPhys = BitConverter.ToUInt16(buf, off + 8);
                        var portDPhys = BitConverter.ToUInt16(buf, off + 10);
                        
                        if (physAddr > 0)
                        {
                            topology.Add(new
                            {
                                index = i,
                                physicalAddr = physAddr,
                                autoIncAddr,
                                connectedPorts = new
                                {
                                    portA = portAPhys,
                                    portB = portBPhys,
                                    portC = portCPhys,
                                    portD = portDPhys
                                }
                            });
                        }
                    }
                    diagnostics.Add($"✅ arrTopologyData: {topology.Count} esclavos con topología");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrTopologyData: {ex.ErrorCode}");
                }
                result["topology"] = topology;
                
                // ====== 7. Leer arrSlaveInfo (ARRAY[0..256] OF ST_SlaveStateInfo) - Info completa ======
                var slavesInfo = new List<object>();
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.arrSlaveInfo");
                    // ST_SlaveStateInfo: nIndex(4) + sName(81) + sType(81) + nECAddr(2) + bDiagData(1) + stPortCRCErrors(~24) + nSumCRCErrors(4) + stState(~20) ≈ 220 bytes
                    var infoSize = 256; // Aproximado generoso
                    var buf = new byte[Math.Min(slaveCount + 1, 50) * infoSize]; // Limitar a 50 para no saturar
                    var bytesRead = client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    
                    int actualInfoSize = bytesRead / Math.Max(1, Math.Min(slaveCount, 50));
                    diagnostics.Add($"📊 ST_SlaveStateInfo size: ~{actualInfoSize} bytes (total: {bytesRead})");
                    
                    for (int i = 0; i < Math.Min(slaveCount, 50) && i * actualInfoSize < bytesRead; i++)
                    {
                        int off = i * actualInfoSize;
                        
                        var nIndex = BitConverter.ToInt32(buf, off);
                        var sName = ExtractTwinCATString(buf, off + 4, 81);
                        var sType = ExtractTwinCATString(buf, off + 4 + 81, 81);
                        var nECAddr = BitConverter.ToUInt16(buf, off + 4 + 81 + 81);
                        var bDiagData = buf[off + 4 + 81 + 81 + 2] != 0;
                        
                        if (!string.IsNullOrWhiteSpace(sName) || nECAddr > 0 || nIndex > 0)
                        {
                            slavesInfo.Add(new
                            {
                                arrayIndex = i,
                                nIndex,
                                sName = sName.Trim(),
                                sType = sType.Trim(),
                                nECAddr,
                                bDiagData
                            });
                        }
                    }
                    diagnostics.Add($"✅ arrSlaveInfo: {slavesInfo.Count} esclavos con info");
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrSlaveInfo: {ex.ErrorCode}");
                }
                result["slavesInfo"] = slavesInfo;
                
                // ====== 8. Leer arrDiagSlaveInfo [0..3] - Solo esclavos con diagnóstico ======
                var diagSlaves = new List<object>();
                try
                {
                    var h = client.CreateVariableHandle($"{effectiveFbInstance}.arrDiagSlaveInfo");
                    var buf = new byte[4 * 256]; // 4 elementos x ~256 bytes
                    var bytesRead = client.Read(h, buf.AsMemory());
                    client.DeleteVariableHandle(h);
                    
                    int diagSize = bytesRead / 4;
                    diagnostics.Add($"📊 arrDiagSlaveInfo: {bytesRead} bytes ({diagSize} bytes/elem)");
                    
                    for (int i = 0; i < 4; i++)
                    {
                        int off = i * diagSize;
                        var nIndex = BitConverter.ToInt32(buf, off);
                        var sName = ExtractTwinCATString(buf, off + 4, 81);
                        var sType = ExtractTwinCATString(buf, off + 4 + 81, 81);
                        var nECAddr = BitConverter.ToUInt16(buf, off + 4 + 81 + 81);
                        
                        if (!string.IsNullOrWhiteSpace(sName) || nECAddr > 0)
                        {
                            diagSlaves.Add(new
                            {
                                diagIndex = i,
                                nIndex,
                                sName = sName.Trim(),
                                sType = sType.Trim(),
                                nECAddr
                            });
                        }
                    }
                }
                catch (TwinCAT.Ads.AdsErrorException ex)
                {
                    diagnostics.Add($"⚠️ arrDiagSlaveInfo: {ex.ErrorCode}");
                }
                result["diagSlaves"] = diagSlaves;
                
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    netId = netIdStr,
                    port,
                    fbInstance = effectiveFbInstance,
                    method = "FB_EtherCATDiag FULL (basado en XML exportado)",
                    diagnostics,
                    data = result,
                    summary = new
                    {
                        slavesConfigured = inputs.ContainsKey("nSlaveCountCfg") ? inputs["nSlaveCountCfg"] : 0,
                        slavesRead = slaveCount,
                        slavesWithInfo = slavesInfo.Count,
                        slavesWithTopology = topology.Count,
                        slavesWithDiag = diagSlaves.Count
                    }
                });
            }
            catch (TwinCAT.Ads.AdsErrorException ex)
            {
                _logger.LogError(ex, "Error leyendo FB_EtherCATDiag");
                return Ok(new
                {
                    timestamp = DateTime.Now,
                    error = $"ADS Error: {ex.ErrorCode}",
                    message = ex.Message,
                    fbInstance = effectiveFbInstance,
                    diagnostics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo FB_EtherCATDiag");
                return Ok(new { timestamp = DateTime.Now, error = ex.Message, diagnostics });
            }
        }

        // Helper: Parsear ST_SlaveState
        private object ParseSlaveState(byte[] buffer, int offset, int maxLen)
        {
            if (offset + 16 > maxLen) return new { error = "insufficient data", eEcState = "UNDEFINED" };
            
            try
            {
                // ⭐ CORREGIDO: Leer solo el primer byte y aplicar máscara 0x0F
                // El enum eEcState está en los 4 bits inferiores del primer byte
                var stateValue = buffer[offset] & 0x0F;
                var stateName = stateValue switch
                {
                    1 => "INIT",
                    2 => "PREOP", 
                    3 => "BOOT",
                    4 => "SAFEOP",
                    8 => "OP",
                    _ => stateValue == 0 ? "UNDEFINED" : $"UNKNOWN({stateValue})"
                };
                
                // Offset 4: bError, bInvalidVPRS
                var bError = buffer[offset + 4] != 0;
                var bInvalidVPRS = buffer[offset + 5] != 0;
                
                // Link state flags (offset ~8)
                var linkFlags = buffer[offset + 8];
                
                // ⭐ NUEVO: Puertos activos están en offsets 12-15 dentro de ST_SlaveState
                var bPortA = buffer[offset + 12] != 0;
                var bPortB = buffer[offset + 13] != 0;
                var bPortC = buffer[offset + 14] != 0;
                var bPortD = buffer[offset + 15] != 0;
                
                return new
                {
                    eEcState = stateName,
                    rawStateValue = stateValue,
                    bError,
                    bInvalidVPRS,
                    bNoCommToSlave = (linkFlags & 0x01) != 0,
                    bLinkError = (linkFlags & 0x02) != 0,
                    bMissingLink = (linkFlags & 0x04) != 0,
                    bUnexpectedLink = (linkFlags & 0x08) != 0,
                    bPortA,
                    bPortB,
                    bPortC,
                    bPortD
                };
            }
            catch
            {
                return new { error = "parse error", eEcState = "UNDEFINED" };
            }
        }
    }

    // ===== DTOs =====

    public class EtherCATConfigResponse
    {
        public bool IsEnabled { get; set; }
        public string MasterNetId { get; set; } = "";
        public string ESIFilesPath { get; set; } = "";
        public bool UseESIFiles { get; set; }
        public int TopologyReadIntervalMs { get; set; }
    }

    public class SlaveErrorSummary
    {
        public ushort Position { get; set; }
        public ushort Address { get; set; }
        public string Name { get; set; } = "";
        public string State { get; set; } = "";
        public string StateColor { get; set; } = "";
        public ushort ALStatusCode { get; set; }
        public string ALStatusDescription { get; set; } = "";
        public uint CRCErrors { get; set; }
        public uint LostLinks { get; set; }
        public long TotalErrors { get; set; }
    }

    // ===== DTOs para Configuración Guardada =====

    public class SaveConfigurationRequest
    {
        public string? Notes { get; set; }
    }

    public class SaveConfigurationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public DateTime? SavedAt { get; set; }
        public int TotalSlaves { get; set; }
        public string? ConfigurationHash { get; set; }
    }

    public class SavedConfigurationResponse
    {
        public bool HasConfiguration { get; set; }
        public DateTime? SavedAt { get; set; }
        public int TotalSlaves { get; set; }
        public string? Notes { get; set; }
        public string? ConfigurationHash { get; set; }
    }

    public class DeleteConfigurationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }
}
