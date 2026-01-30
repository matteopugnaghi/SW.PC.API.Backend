using System.Xml.Linq;
using SW.PC.API.Backend.Models.EtherCAT;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Servicio para parsear archivos ESI (EtherCAT Slave Information) de TwinCAT.
/// Los ESI files contienen información detallada de los dispositivos EtherCAT:
/// - Nombre del producto
/// - Descripción
/// - Vendor ID → Nombre del fabricante
/// - Capabilities
/// 
/// Ruta típica TwinCAT 3: C:\TwinCAT\3.1\Config\Io\EtherCAT\
/// </summary>
public interface IESIParserService
{
    /// <summary>
    /// Obtiene información de un dispositivo por Vendor ID y Product Code
    /// </summary>
    ESIDeviceInfo? GetDeviceInfo(uint vendorId, uint productCode);
    
    /// <summary>
    /// Obtiene información de un dispositivo por el sType del PLC (ej: "EK1122-0000-0018" o "EL2798")
    /// Este es el método principal para correlacionar datos del FB_EtherCATDiag con ESI
    /// </summary>
    ESIDeviceInfo? GetDeviceInfoByType(string sType);
    
    /// <summary>
    /// Busca dispositivo por nombre de archivo ESI (para fabricantes no-Beckhoff que SÍ envían nombre)
    /// </summary>
    ESIDeviceInfo? GetDeviceInfoByFileName(string fileName);
    
    /// <summary>
    /// ⭐ NUEVO: Obtiene información buscando específicamente en un archivo ESI
    /// Usado para dispositivos no-Beckhoff donde el PLC especifica el nombre del archivo ESI (sESIfile)
    /// </summary>
    /// <param name="esiFileName">Nombre del archivo ESI (ej: "Yaskawa Sigma-7.xml")</param>
    /// <param name="sType">Tipo del dispositivo para búsqueda dentro del archivo (opcional)</param>
    ESIDeviceInfo? GetDeviceInfoFromESIFile(string esiFileName, string? sType = null);
    
    /// <summary>
    /// Obtiene el nombre del vendor por su ID
    /// </summary>
    string GetVendorName(uint vendorId);
    
    /// <summary>
    /// Recarga el cache de ESI files
    /// </summary>
    Task RefreshCacheAsync();
    
    /// <summary>
    /// Obtiene estadísticas del cache
    /// </summary>
    ESICacheStats GetCacheStats();
    
    /// <summary>
    /// Lista todos los Types cargados en el cache (para debug)
    /// </summary>
    IEnumerable<string> GetAllCachedTypes();
    
    /// <summary>
    /// Busca Types que contengan el texto especificado (para debug)
    /// </summary>
    IEnumerable<(string Type, string ProductName, string PhysicsRaw)> SearchTypes(string searchText);
}

/// <summary>
/// Información de un dispositivo extraída de ESI files
/// </summary>
public class ESIDeviceInfo
{
    public uint VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public uint ProductCode { get; set; }
    public string ProductName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";  // Ej: "EL2008"
    public string GroupType { get; set; } = "";  // Ej: "DigOut", "Coupler"
    public string ImageFile { get; set; } = "";  // Ruta a imagen si existe
    public List<string> Capabilities { get; set; } = new();
    
    /// <summary>
    /// Nombre del archivo ESI de origen (para buscar dispositivos por sESIfile del PLC)
    /// IMPORTANTE: Beckhoff/TwinCAT NO envía esto, otros fabricantes SÍ
    /// </summary>
    public string SourceFileName { get; set; } = "";
    
    /// <summary>
    /// Física de cada puerto (0-3). Valores: "Y"=EBUS, "K"=MII (100BASE-TX), " "=no implementado
    /// Extraído del campo Physics del ESI (ej: "YY  " = Port0 EBUS, Port1 EBUS, Port2/3 no implementados)
    /// </summary>
    public List<ESIPortPhysics> PortPhysics { get; set; } = new(4);
    
    /// <summary>
    /// Cadena original de Physics del ESI (ej: "YY  ", "K  K", "YKYY")
    /// </summary>
    public string PhysicsRaw { get; set; } = "";
    
    // ═══════════════════════════════════════════════════════════════════════════
    // ⭐ NUEVAS PROPIEDADES CALCULADAS PARA SISTEMA MODULAR (sin hardcoding frontend)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Categoría del dispositivo: coupler, junction, terminal, drive, encoder, gateway, power, unknown
    /// Calculado desde GroupType + análisis de Physics
    /// </summary>
    public string DeviceCategory { get; set; } = "unknown";
    
    /// <summary>
    /// Tipo de conexión: ebus-only, ethernet-only, mixed
    /// Calculado desde Physics (todos Y = ebus-only, todos K = ethernet-only, mix = mixed)
    /// </summary>
    public string ConnectionType { get; set; } = "unknown";
    
    /// <summary>
    /// Es un junction (dispositivo con 2+ salidas Ethernet como EK1122)
    /// Calculado contando puertos MII de salida (port 1-3)
    /// </summary>
    public bool IsJunction { get; set; } = false;
    
    /// <summary>
    /// Número de puertos implementados (no NotImplemented)
    /// </summary>
    public int PortCount { get; set; } = 0;
}

/// <summary>
/// Información física de un puerto según el ESI
/// </summary>
public class ESIPortPhysics
{
    /// <summary>Número de puerto (0-3)</summary>
    public int PortNumber { get; set; }
    
    /// <summary>Tipo físico: EBUS, MII (cable Ethernet), o NotImplemented</summary>
    public string PhysicsType { get; set; } = "NotImplemented";
    
    /// <summary>Nombre descriptivo del conector (X1, X2, E-Bus IN, E-Bus OUT, etc.)</summary>
    public string? ConnectorName { get; set; }
    
    /// <summary>Es un puerto de cable Ethernet (MII/100BASE-TX)</summary>
    public bool IsCable => PhysicsType == "MII" || PhysicsType == "K";
    
    /// <summary>Es un puerto E-Bus (backplane)</summary>
    public bool IsEBus => PhysicsType == "EBUS" || PhysicsType == "Y";
}

/// <summary>
/// Estadísticas del cache de ESI
/// </summary>
public class ESICacheStats
{
    public int TotalFiles { get; set; }
    public int TotalDevices { get; set; }
    public int TotalVendors { get; set; }
    public DateTime LastRefresh { get; set; }
    public string ESIPath { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<string> LoadedFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ESIParserService : IESIParserService
{
    private readonly ILogger<ESIParserService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    // Cache de dispositivos: Key = "VendorId_ProductCode"
    private readonly Dictionary<string, ESIDeviceInfo> _deviceCache = new();
    
    // Cache de dispositivos por Type: Key = tipo normalizado (ej: "EL2798", "EK1122")
    private readonly Dictionary<string, ESIDeviceInfo> _deviceByTypeCache = new(StringComparer.OrdinalIgnoreCase);
    
    // Cache de vendors: Key = VendorId
    private readonly Dictionary<uint, string> _vendorCache = new();
    
    // Estadísticas
    private ESICacheStats _stats = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _cacheLoaded = false;
    
    // ⭐ NUEVO: Señal para indicar que el caché está listo
    private readonly TaskCompletionSource<bool> _cacheReadySignal = new();
    
    // Vendors conocidos (fallback si no hay ESI)
    // VendorIds según ETG (EtherCAT Technology Group)
    private static readonly Dictionary<uint, string> KnownVendors = new()
    {
        { 0x00000001, "EtherCAT Technology Group" },
        { 0x00000002, "Beckhoff Automation GmbH" },
        { 0x00000022, "Hilscher GmbH" },
        { 0x0000001D, "Festo" },            // ⭐ VendorId CORRECTO de Festo (29 decimal = 0x1D)
        { 0x00000092, "ifm electronic" },   // ⭐ VendorId de ifm (146 decimal = 0x92)
        { 0x000000E8, "Omron Corporation" },
        { 0x00000156, "Kollmorgen" },
        { 0x000001DD, "Delta Electronics" },
        { 0x00000539, "Yaskawa" },          // VendorId Yaskawa (1337 decimal)
        { 0x000005A3, "Mitsubishi Electric" },
        { 0x00000B95, "SMC Corporation" },
        { 0x00001000, "Siemens AG" },
        { 0x00001A05, "Lenze SE" }
    };

    public ESIParserService(
        ILogger<ESIParserService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Iniciar carga de ESI en background (no bloqueante)
        Task.Run(async () => 
        {
            try
            {
                await Task.Delay(1000); // Reducido de 2s a 1s - esperar a que el sistema arranque
                await RefreshCacheAsync();
                _cacheReadySignal.TrySetResult(true); // ⭐ Señalizar que el caché está listo
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error en carga inicial de ESI files (continuando sin ESI)");
                _cacheReadySignal.TrySetResult(false); // Señalizar aunque haya error para no bloquear indefinidamente
            }
        });
    }
    
    /// <summary>
    /// ⭐ Espera a que el caché ESI esté completamente cargado (máximo 10 segundos)
    /// </summary>
    public async Task EnsureCacheLoadedAsync(int timeoutMs = 10000)
    {
        if (_cacheLoaded)
            return;
            
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await _cacheReadySignal.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⚠️ Timeout esperando carga de ESI cache ({TimeoutMs}ms)", timeoutMs);
        }
    }
    
    /// <summary>
    /// ⭐ Versión síncrona para esperar el caché (usa con cuidado, puede bloquear)
    /// </summary>
    private void EnsureCacheLoaded(int timeoutMs = 5000)
    {
        if (_cacheLoaded)
            return;
            
        try
        {
            _cacheReadySignal.Task.Wait(timeoutMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ Error esperando carga de ESI cache: {Error}", ex.Message);
        }
    }

    public ESIDeviceInfo? GetDeviceInfo(uint vendorId, uint productCode)
    {
        // ⭐ CRÍTICO: Esperar a que el caché esté cargado antes de buscar
        EnsureCacheLoaded();
        
        var key = $"{vendorId}_{productCode}";
        if (_deviceCache.TryGetValue(key, out var info))
        {
            return info;
        }
        
        // ⭐ CAMBIO: Devolver null si no encontramos en cache
        // Esto permite que la búsqueda por Type entre como fallback
        // El llamador decidirá qué hacer si es null
        return null;
    }

    public ESIDeviceInfo? GetDeviceInfoByType(string sType)
    {
        if (string.IsNullOrWhiteSpace(sType))
            return null;
        
        // ⭐ CRÍTICO: Esperar a que el caché esté cargado antes de buscar
        EnsureCacheLoaded();
        
        // sType del PLC puede ser:
        // - "EK1122-0000-0018" (tipo-variante-revision)
        // - "EL2798-0000-0018"
        // - "EL2798" (solo tipo)
        
        // Extraer el tipo base (EK1122, EL2798, etc.)
        var typeBase = ExtractTypeBase(sType);
        
        // Buscar en cache por tipo base
        if (_deviceByTypeCache.TryGetValue(typeBase, out var info))
        {
            return info;
        }
        
        // Intentar búsqueda parcial si no hay match exacto
        // Por ejemplo "EL2798" podría matchear "EL2798-xxxx"
        foreach (var kvp in _deviceByTypeCache)
        {
            if (kvp.Key.StartsWith(typeBase, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        
        // No encontrado - devolver null (el llamador decidirá qué hacer)
        return null;
    }
    
    /// <summary>
    /// ⭐ Busca dispositivo por nombre de archivo ESI
    /// IMPORTANTE: Beckhoff/TwinCAT NO envía nombre de fichero, otros fabricantes SÍ
    /// </summary>
    public ESIDeviceInfo? GetDeviceInfoByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        
        // Buscar en todos los dispositivos cacheados
        foreach (var kvp in _deviceCache.Values)
        {
            if (string.Equals(kvp.SourceFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp;
            }
        }
        
        // Intentar búsqueda por nombre parcial
        var fileNameLower = fileName.ToLowerInvariant();
        foreach (var kvp in _deviceCache.Values)
        {
            if (kvp.SourceFileName.ToLowerInvariant().Contains(fileNameLower) ||
                fileNameLower.Contains(kvp.SourceFileName.ToLowerInvariant()))
            {
                return kvp;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Extrae el tipo base de un sType del PLC
    /// "EK1122-0000-0018" → "EK1122"
    /// "EL2798" → "EL2798"
    /// </summary>
    private static string ExtractTypeBase(string sType)
    {
        if (string.IsNullOrWhiteSpace(sType))
            return "";
        
        // Si contiene guión, tomar solo la primera parte
        var dashIndex = sType.IndexOf('-');
        if (dashIndex > 0)
        {
            return sType.Substring(0, dashIndex).Trim();
        }
        
        // Si no hay guión, devolver todo (puede ser "EL2798" directamente)
        return sType.Trim();
    }

    /// <summary>
    /// ⭐ NUEVO: Busca información de un dispositivo en un archivo ESI específico
    /// Usado para dispositivos no-Beckhoff donde el PLC especifica el nombre del archivo ESI (sESIfile)
    /// </summary>
    public ESIDeviceInfo? GetDeviceInfoFromESIFile(string esiFileName, string? sType = null)
    {
        if (string.IsNullOrWhiteSpace(esiFileName))
            return null;
        
        // ⭐ CRÍTICO: Esperar a que el caché esté cargado antes de buscar
        EnsureCacheLoaded();
        
        // Normalizar nombre de archivo (quitar extensión si la tiene)
        var fileNameLower = esiFileName.ToLowerInvariant();
        if (!fileNameLower.EndsWith(".xml"))
            fileNameLower += ".xml";
        
        // Extraer nombre base sin extensión para comparación más flexible
        var fileNameBase = Path.GetFileNameWithoutExtension(esiFileName).ToLowerInvariant();
        
        _logger.LogDebug("🔍 GetDeviceInfoFromESIFile: Buscando archivo '{FileName}' (base: '{Base}'), sType='{Type}'", 
            esiFileName, fileNameBase, sType);
        
        // Buscar en cache por archivos que matchean
        var matchingDevices = new List<ESIDeviceInfo>();
        var partialMatches = new List<(string SourceFile, string Type)>();
        
        // ⭐ NUEVO: Buscar directamente por SourceFileName guardado en cada dispositivo
        foreach (var kvp in _deviceByTypeCache)
        {
            var device = kvp.Value;
            if (string.IsNullOrEmpty(device.SourceFileName))
                continue;
                
            var sourceNameLower = device.SourceFileName.ToLowerInvariant();
            var sourceBase = Path.GetFileNameWithoutExtension(device.SourceFileName).ToLowerInvariant();
            
            // Comparar: nombre exacto, nombre sin extensión, o contenido parcial
            if (sourceNameLower == fileNameLower ||
                sourceBase == fileNameBase ||
                sourceNameLower.Contains(fileNameBase) ||
                fileNameBase.Contains(sourceBase))
            {
                matchingDevices.Add(device);
                _logger.LogDebug("  📁 Match: SourceFile='{Source}' → Type='{Type}', ProductName='{Name}'", 
                    device.SourceFileName, device.Type, device.ProductName);
            }
            // ⭐ DEBUG: Mostrar archivos similares para ayudar a diagnosticar
            else if (sourceBase.Contains("ifm") || sourceBase.Contains("festo") || 
                     fileNameBase.Contains("ifm") || fileNameBase.Contains("festo"))
            {
                // Solo para debug de ifm/festo
                if ((fileNameBase.Contains("ifm") && sourceBase.Contains("ifm")) ||
                    (fileNameBase.Contains("festo") && sourceBase.Contains("festo")))
                {
                    partialMatches.Add((device.SourceFileName, device.Type));
                }
            }
        }
        
        // Log de archivos relacionados si no hay match exacto
        if (matchingDevices.Count == 0 && partialMatches.Count > 0)
        {
            _logger.LogWarning("  ⚠️ No match exacto pero archivos relacionados encontrados:");
            foreach (var pm in partialMatches.Take(10))
            {
                _logger.LogWarning("    - '{SourceFile}' contiene Type='{Type}'", pm.SourceFile, pm.Type);
            }
        }
        
        if (matchingDevices.Count == 0)
        {
            _logger.LogDebug("⚠️ No se encontraron dispositivos del archivo ESI: {FileName}", esiFileName);
            
            // Intentar buscar por type si se proporcionó
            if (!string.IsNullOrWhiteSpace(sType))
            {
                _logger.LogDebug("  🔄 Intentando fallback por sType: '{Type}'", sType);
                return GetDeviceInfoByType(sType);
            }
            return null;
        }
        
        _logger.LogDebug("  📦 Encontrados {Count} dispositivos en archivo ESI '{File}'", matchingDevices.Count, esiFileName);
        
        // Si se proporcionó sType, buscar coincidencia específica
        if (!string.IsNullOrWhiteSpace(sType))
        {
            var typeBase = ExtractTypeBase(sType);
            _logger.LogDebug("  🔍 Buscando Type que coincida con '{TypeBase}' (extraído de '{FullType}')", typeBase, sType);
            
            // ⭐ MEJORADO: Buscar de forma más flexible
            var exactMatch = matchingDevices.FirstOrDefault(d => 
                d.Type.Equals(typeBase, StringComparison.OrdinalIgnoreCase));
            
            if (exactMatch == null)
            {
                // Intentar con StartsWith
                exactMatch = matchingDevices.FirstOrDefault(d => 
                    d.Type.StartsWith(typeBase, StringComparison.OrdinalIgnoreCase));
            }
            
            if (exactMatch == null)
            {
                // ⭐ NUEVO: Intentar que el Type del ESI contenga parte del sType
                exactMatch = matchingDevices.FirstOrDefault(d => 
                    d.Type.Contains(typeBase, StringComparison.OrdinalIgnoreCase) ||
                    typeBase.Contains(d.Type, StringComparison.OrdinalIgnoreCase));
            }
            
            if (exactMatch == null)
            {
                // Log de todos los Types disponibles para diagnóstico
                _logger.LogWarning("  ⚠️ No se encontró Type '{TypeBase}' en el archivo. Types disponibles:", typeBase);
                foreach (var dev in matchingDevices.Take(10))
                {
                    _logger.LogWarning("    - Type='{Type}', ProductName='{Name}'", dev.Type, dev.ProductName);
                }
            }
            
            if (exactMatch != null)
            {
                _logger.LogDebug("✅ Encontrado en ESI '{File}': {Type} → {Name}", 
                    esiFileName, sType, exactMatch.ProductName);
                return exactMatch;
            }
        }
        
        // Devolver el primer dispositivo del archivo ESI
        var firstDevice = matchingDevices.FirstOrDefault();
        if (firstDevice != null)
        {
            _logger.LogDebug("✅ Usando primer dispositivo de ESI '{File}': {Name} (Type={Type})", 
                esiFileName, firstDevice.ProductName, firstDevice.Type);
        }
        
        return firstDevice;
    }

    public string GetVendorName(uint vendorId)
    {
        // NO bloquear - usar lo que haya en cache
        if (_vendorCache.TryGetValue(vendorId, out var name))
        {
            return name;
        }
        
        if (KnownVendors.TryGetValue(vendorId, out var knownName))
        {
            return knownName;
        }
        
        return $"Vendor 0x{vendorId:X4}";
    }

    public ESICacheStats GetCacheStats()
    {
        return _stats;
    }
    
    public IEnumerable<string> GetAllCachedTypes()
    {
        return _deviceByTypeCache.Keys.OrderBy(k => k);
    }
    
    public IEnumerable<(string Type, string ProductName, string PhysicsRaw)> SearchTypes(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Enumerable.Empty<(string, string, string)>();
        
        return _deviceByTypeCache
            .Where(kvp => kvp.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => (kvp.Key, kvp.Value.ProductName, kvp.Value.PhysicsRaw))
            .OrderBy(x => x.Key);
    }

    public async Task RefreshCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            await LoadESIFilesAsync();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task LoadESIFilesAsync()
    {
        _deviceCache.Clear();
        _deviceByTypeCache.Clear();
        _vendorCache.Clear();
        _stats = new ESICacheStats { LastRefresh = DateTime.UtcNow };
        
        // Obtener configuración
        string esiPath = "";
        bool useEsi = false;
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var excelService = scope.ServiceProvider.GetService<IExcelConfigService>();
            var projectContext = scope.ServiceProvider.GetService<IProjectContextService>();
            
            if (excelService != null && projectContext != null)
            {
                var excelPath = projectContext.ExcelConfigPath;
                if (!string.IsNullOrEmpty(excelPath) && File.Exists(excelPath))
                {
                    var systemConfig = await excelService.LoadSystemConfigurationAsync(excelPath);
                    if (systemConfig != null)
                    {
                        esiPath = systemConfig.ESIFilesPath;
                        useEsi = systemConfig.UseEtherCATESIFiles;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ No se pudo obtener configuración ESI del Excel");
        }
        
        _stats.ESIPath = esiPath;
        _stats.IsEnabled = useEsi;
        
        // Agregar vendors conocidos al cache
        foreach (var kv in KnownVendors)
        {
            _vendorCache[kv.Key] = kv.Value;
        }
        _stats.TotalVendors = _vendorCache.Count;
        
        if (!useEsi)
        {
            _logger.LogInformation("🌐 ESI Parser: Deshabilitado por configuración");
            _cacheLoaded = true;
            return;
        }
        
        // Determinar ruta ESI
        _logger.LogInformation("🌐 ESI Parser: ESIFilesPath del Excel = '{EsiPath}'", esiPath ?? "(vacío)");
        
        if (string.IsNullOrWhiteSpace(esiPath))
        {
            // Buscar rutas comunes de TwinCAT
            var commonPaths = new[]
            {
                @"C:\Program Files (x86)\Beckhoff\TwinCAT\3.1\Config\Io\EtherCAT",
                @"C:\TwinCAT\3.1\Config\Io\EtherCAT",
                @"C:\TwinCAT\3.0\Config\Io\EtherCAT",
                @"D:\TwinCAT\3.1\Config\Io\EtherCAT",
                @"C:\Program Files\TwinCAT\3.1\Config\Io\EtherCAT"
            };
            
            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    esiPath = path;
                    _logger.LogInformation("🌐 ESI Parser: Ruta auto-detectada: {Path}", path);
                    break;
                }
            }
        }
        
        if (string.IsNullOrWhiteSpace(esiPath) || !Directory.Exists(esiPath))
        {
            _logger.LogWarning("⚠️ ESI Parser: Ruta no encontrada: {Path}", esiPath);
            _stats.Errors.Add($"Ruta ESI no encontrada: {esiPath}");
            _cacheLoaded = true;
            return;
        }
        
        _stats.ESIPath = esiPath;
        _logger.LogInformation("🌐 ESI Parser: Cargando archivos de {Path}", esiPath);
        
        // Cargar todos los archivos XML
        var xmlFiles = Directory.GetFiles(esiPath, "*.xml", SearchOption.AllDirectories);
        _stats.TotalFiles = xmlFiles.Length;
        
        foreach (var file in xmlFiles)
        {
            try
            {
                await ParseESIFileAsync(file);
                _stats.LoadedFiles.Add(Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Error parseando ESI file {File}: {Error}", 
                    Path.GetFileName(file), ex.Message);
                _stats.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        
        _stats.TotalDevices = _deviceCache.Count;
        _stats.TotalVendors = _vendorCache.Count;
        
        _logger.LogInformation("🌐 ESI Parser: Cargados {Devices} dispositivos de {Files} archivos, {Vendors} vendors",
            _stats.TotalDevices, _stats.LoadedFiles.Count, _stats.TotalVendors);
        
        _cacheLoaded = true;
    }

    private async Task ParseESIFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var doc = XDocument.Parse(content);
        
        // Namespace típico de ESI
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? "";
        
        // Obtener información del Vendor a nivel raíz (EtherCATInfo/Vendor)
        var vendorElement = doc.Root?.Element(ns + "Vendor") 
                          ?? doc.Root?.Element("Vendor")
                          ?? doc.Descendants(ns + "Vendor").FirstOrDefault() 
                          ?? doc.Descendants("Vendor").FirstOrDefault();
        
        uint vendorId = 0;
        string vendorName = "";
        
        if (vendorElement != null)
        {
            var idElement = vendorElement.Element(ns + "Id") ?? vendorElement.Element("Id");
            var nameElement = vendorElement.Element(ns + "Name") ?? vendorElement.Element("Name");
            
            if (idElement != null)
            {
                var idText = idElement.Value.Trim();
                vendorId = ParseHexOrDecimal(idText);
                _logger.LogDebug("📦 ESI '{File}': VendorId parsed = 0x{Id:X4} from '{Raw}'", 
                    Path.GetFileName(filePath), vendorId, idText);
            }
            
            if (nameElement != null)
            {
                vendorName = nameElement.Value.Trim();
                _logger.LogDebug("📦 ESI '{File}': VendorName = '{Name}'", 
                    Path.GetFileName(filePath), vendorName);
                    
                if (vendorId > 0 && !string.IsNullOrEmpty(vendorName))
                {
                    _vendorCache[vendorId] = vendorName;
                    _logger.LogInformation("📦 ESI '{File}': Cacheado Vendor 0x{Id:X4} = '{Name}'", 
                        Path.GetFileName(filePath), vendorId, vendorName);
                }
            }
        }
        else
        {
            _logger.LogDebug("⚠️ ESI '{File}': No se encontró elemento Vendor a nivel raíz", 
                Path.GetFileName(filePath));
        }
        
        // Buscar dispositivos
        var devices = doc.Descendants(ns + "Device").Concat(doc.Descendants("Device"));
        
        foreach (var device in devices)
        {
            try
            {
                ParseDevice(device, ns, vendorId, vendorName, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogTrace("Error parseando dispositivo en {File}: {Error}", 
                    Path.GetFileName(filePath), ex.Message);
            }
        }
    }

    private void ParseDevice(XElement device, XNamespace ns, uint defaultVendorId, string defaultVendorName, string filePath)
    {
        // Tipo (ej: EL2008)
        var typeElement = device.Element(ns + "Type") ?? device.Element("Type");
        if (typeElement == null) return;
        
        var type = typeElement.Value.Trim();
        
        // Product Code del atributo
        var productCodeAttr = typeElement.Attribute("ProductCode");
        if (productCodeAttr == null) return;
        
        var productCode = ParseHexOrDecimal(productCodeAttr.Value);
        if (productCode == 0) return;
        
        // ⭐ NUEVO: Buscar Vendor específico del Device (algunos ESI lo tienen a nivel de Device)
        uint deviceVendorId = defaultVendorId;
        string deviceVendorName = defaultVendorName;
        
        // Buscar VendorId en atributo del Type o en elemento Vendor dentro del Device
        var vendorIdAttr = typeElement.Attribute("VendorId");
        if (vendorIdAttr != null)
        {
            deviceVendorId = ParseHexOrDecimal(vendorIdAttr.Value);
        }
        
        // Buscar Vendor element dentro del Device
        var deviceVendor = device.Element(ns + "Vendor") ?? device.Element("Vendor");
        if (deviceVendor != null)
        {
            var vendorIdEl = deviceVendor.Element(ns + "Id") ?? deviceVendor.Element("Id");
            var vendorNameEl = deviceVendor.Element(ns + "Name") ?? deviceVendor.Element("Name");
            
            if (vendorIdEl != null)
                deviceVendorId = ParseHexOrDecimal(vendorIdEl.Value);
            if (vendorNameEl != null)
                deviceVendorName = vendorNameEl.Value.Trim();
        }
        
        // ⭐ FALLBACK: Si no hay VendorName, intentar obtenerlo del cache por VendorId
        if (string.IsNullOrWhiteSpace(deviceVendorName) && deviceVendorId > 0)
        {
            if (_vendorCache.TryGetValue(deviceVendorId, out var cachedName))
            {
                deviceVendorName = cachedName;
            }
            else
            {
                // Usar GetVendorName que tiene tabla de vendors conocidos
                deviceVendorName = GetVendorName(deviceVendorId);
            }
        }
        
        // ⭐ FALLBACK FINAL: Detectar por nombre de archivo ESI
        if (string.IsNullOrWhiteSpace(deviceVendorName) || deviceVendorName == "Unknown")
        {
            var fileName = Path.GetFileName(filePath).ToUpperInvariant();
            if (fileName.Contains("FESTO") || fileName.Contains("CMMT"))
                deviceVendorName = "Festo";
            else if (fileName.Contains("IFM"))
                deviceVendorName = "ifm";
            else if (fileName.Contains("YASKAWA") || fileName.Contains("SIGMA"))
                deviceVendorName = "YASKAWA";
            else if (fileName.Contains("SICK"))
                deviceVendorName = "SICK AG";
        }
        
        // Nombre
        var nameElement = device.Element(ns + "Name") ?? device.Element("Name");
        var productName = nameElement?.Value.Trim() ?? type;
        
        // También puede estar en Name con atributo LcId
        if (nameElement == null)
        {
            var names = device.Elements(ns + "Name").Concat(device.Elements("Name"));
            nameElement = names.FirstOrDefault(n => n.Attribute("LcId")?.Value == "1033") // Inglés
                       ?? names.FirstOrDefault();
            if (nameElement != null)
            {
                productName = nameElement.Value.Trim();
            }
        }
        
        // Group Type (Digital I/O, Drives, etc.)
        var groupType = "";
        var groupElement = device.Element(ns + "GroupType") ?? device.Element("GroupType");
        if (groupElement != null)
        {
            groupType = groupElement.Value.Trim();
        }
        
        // Imagen
        var imageFile = "";
        var imageElement = device.Element(ns + "ImageFile16x14") ?? device.Element("ImageFile16x14")
                        ?? device.Element(ns + "Image") ?? device.Element("Image");
        if (imageElement != null)
        {
            imageFile = imageElement.Value.Trim();
        }
        
        // Capabilities (CoE, FoE, etc.)
        var capabilities = new List<string>();
        var mailbox = device.Element(ns + "Mailbox") ?? device.Element("Mailbox");
        if (mailbox != null)
        {
            if (mailbox.Element(ns + "CoE") != null || mailbox.Element("CoE") != null)
                capabilities.Add("CoE");
            if (mailbox.Element(ns + "FoE") != null || mailbox.Element("FoE") != null)
                capabilities.Add("FoE");
            if (mailbox.Element(ns + "EoE") != null || mailbox.Element("EoE") != null)
                capabilities.Add("EoE");
            if (mailbox.Element(ns + "SoE") != null || mailbox.Element("SoE") != null)
                capabilities.Add("SoE");
            if (mailbox.Element(ns + "VoE") != null || mailbox.Element("VoE") != null)
                capabilities.Add("VoE");
        }
        
        // DC Support
        var dc = device.Element(ns + "Dc") ?? device.Element("Dc");
        if (dc != null)
        {
            capabilities.Add("DC");
        }
        
        // ⭐ PHYSICS - Tipo físico de cada puerto (CRÍTICO para topología)
        // Hay TRES formatos posibles en ESI:
        // 1. <Physics>KYKY</Physics> - Formato compacto (elemento)
        // 2. <Device Physics="YY">  - Formato compacto (atributo) - usado por Yaskawa, ifm, Festo
        // 3. <Info><Port><Type>MII</Type></Port>...</Info> - Formato detallado (Beckhoff)
        
        var physicsRaw = "";
        var portPhysics = new List<ESIPortPhysics>();
        
        // MÉTODO 0: ⭐ NUEVO - Buscar atributo Physics en <Device Physics="YY">
        // Usado por fabricantes como Yaskawa, ifm, Festo
        var physicsAttr = device.Attribute("Physics")?.Value ?? "";
        if (!string.IsNullOrWhiteSpace(physicsAttr))
        {
            physicsRaw = physicsAttr;
            _logger.LogDebug("📦 Physics encontrado como ATRIBUTO para {Type}: '{Physics}'", type, physicsRaw);
        }
        
        // MÉTODO 1: Buscar elemento <Physics> directo (si no se encontró atributo)
        if (string.IsNullOrWhiteSpace(physicsRaw))
        {
            var physicsElement = device.Element(ns + "Physics") ?? device.Element("Physics");
            if (physicsElement == null)
            {
                physicsElement = device.Descendants(ns + "Physics").FirstOrDefault()
                              ?? device.Descendants("Physics").FirstOrDefault();
            }
            
            if (physicsElement != null && !string.IsNullOrWhiteSpace(physicsElement.Value))
            {
                physicsRaw = physicsElement.Value;
                _logger.LogDebug("📦 Physics encontrado como ELEMENTO para {Type}: '{Physics}'", type, physicsRaw);
            }
        }
        
        // Si encontramos Physics (atributo o elemento), parsear los puertos
        if (!string.IsNullOrWhiteSpace(physicsRaw))
        {
            for (int i = 0; i < 4; i++)
            {
                var physChar = i < physicsRaw.Length ? physicsRaw[i] : ' ';
                // ⭐ Según especificación EtherCAT ESI:
                // K = E-Bus (contactos internos del bus)
                // Y = MII/100BASE-TX (puerto Ethernet RJ45)
                // H = LVDS (alta velocidad)
                // L = LVDS
                var physType = physChar switch
                {
                    'K' => "EBUS",      // ⭐ K = E-Bus (Beckhoff terminals)
                    'Y' => "MII",       // ⭐ Y = MII/Ethernet (RJ45 connectors)
                    'H' => "MII",       // H también es cable (100BASE-TX)
                    'L' => "LVDS",
                    ' ' => "NotImplemented",
                    _ => "Unknown"
                };
                
                portPhysics.Add(new ESIPortPhysics
                {
                    PortNumber = i,
                    PhysicsType = physType
                });
            }
        }
        else
        {
            // MÉTODO 2: Buscar en <Info><Port><Type>...</Type></Port></Info>
            var infoElement = device.Element(ns + "Info") ?? device.Element("Info");
            var portElements = infoElement?.Elements(ns + "Port").ToList() 
                            ?? infoElement?.Elements("Port").ToList()
                            ?? new List<XElement>();
            
            if (portElements.Count > 0)
            {
                var portTypes = new List<string>();
                foreach (var portEl in portElements)
                {
                    var portTypeEl = portEl.Element(ns + "Type") ?? portEl.Element("Type");
                    var portType = portTypeEl?.Value?.Trim() ?? "";
                    var labelEl = portEl.Element(ns + "Label") ?? portEl.Element("Label");
                    var label = labelEl?.Value?.Trim() ?? "";
                    
                    // Convertir tipo de puerto
                    var physType = portType.ToUpperInvariant() switch
                    {
                        "EBUS" => "EBUS",
                        "MII" => "MII",
                        "100BASE-TX" => "MII",
                        "LVDS" => "LVDS",
                        _ => "Unknown"
                    };
                    
                    portPhysics.Add(new ESIPortPhysics
                    {
                        PortNumber = portPhysics.Count,
                        PhysicsType = physType,
                        ConnectorName = !string.IsNullOrEmpty(label) ? label : null
                    });
                    
                    // Construir physicsRaw equivalente
                    portTypes.Add(physType == "MII" ? "K" : (physType == "EBUS" ? "Y" : " "));
                }
                
                physicsRaw = string.Join("", portTypes);
                _logger.LogTrace("📦 ESI Physics (from Info/Port) for {Type}: '{Physics}'", type, physicsRaw);
                
                // Rellenar hasta 4 puertos
                while (portPhysics.Count < 4)
                {
                    portPhysics.Add(new ESIPortPhysics
                    {
                        PortNumber = portPhysics.Count,
                        PhysicsType = "NotImplemented"
                    });
                }
            }
            else
            {
                _logger.LogTrace("📦 ESI Physics NOT found for {Type}, using default EBUS", type);
                // Sin Physics en ESI - asumir 2 puertos EBUS (típico para terminales)
                for (int i = 0; i < 4; i++)
                {
                    portPhysics.Add(new ESIPortPhysics
                    {
                        PortNumber = i,
                        PhysicsType = i < 2 ? "EBUS" : "NotImplemented"
                    });
                }
            }
        }
        
        var info = new ESIDeviceInfo
        {
            VendorId = deviceVendorId,
            VendorName = deviceVendorName,
            ProductCode = productCode,
            ProductName = productName,
            Description = productName,
            Type = type,
            GroupType = groupType,
            ImageFile = imageFile,
            Capabilities = capabilities,
            PortPhysics = portPhysics,
            PhysicsRaw = physicsRaw,
            SourceFileName = Path.GetFileName(filePath)  // ⭐ Guardar nombre del archivo ESI
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ⭐ CALCULAR PROPIEDADES PARA SISTEMA MODULAR (sin hardcoding frontend)
        // ═══════════════════════════════════════════════════════════════════════════
        CalculateDeviceProperties(info);
        
        var key = $"{deviceVendorId}_{productCode}";
        _deviceCache[key] = info;
        
        // ⭐ También añadir al cache por Type para búsqueda por sType del PLC
        // Ej: "EK1122" -> ESIDeviceInfo
        // IMPORTANTE: Preferir versiones que tienen Physics definido
        if (!string.IsNullOrEmpty(type))
        {
            if (!_deviceByTypeCache.ContainsKey(type))
            {
                // Primera vez que vemos este tipo - guardar
                _deviceByTypeCache[type] = info;
            }
            else if (!string.IsNullOrEmpty(physicsRaw) && string.IsNullOrEmpty(_deviceByTypeCache[type].PhysicsRaw))
            {
                // Ya existe pero SIN Physics, y esta versión SÍ tiene Physics - actualizar
                _deviceByTypeCache[type] = info;
                _logger.LogDebug("📦 ESI: Actualizado {Type} con Physics: '{Physics}'", type, physicsRaw);
            }
        }
        
        _logger.LogTrace("📦 ESI: {Type} ({ProductName}) - Physics: '{Physics}' - VendorId: 0x{VendorId:X4}, ProductCode: 0x{ProductCode:X8}",
            type, productName, physicsRaw, defaultVendorId, productCode);
    }
    
    /// <summary>
    /// ⭐ Calcula propiedades automáticas para el sistema modular (sin hardcoding en frontend)
    /// </summary>
    private void CalculateDeviceProperties(ESIDeviceInfo info)
    {
        // 1. Contar puertos implementados
        info.PortCount = info.PortPhysics.Count(p => p.PhysicsType != "NotImplemented");
        
        // 2. Determinar ConnectionType (ebus-only, ethernet-only, mixed)
        var ebusCount = info.PortPhysics.Count(p => p.IsEBus);
        var miiCount = info.PortPhysics.Count(p => p.IsCable);
        
        if (ebusCount > 0 && miiCount > 0)
            info.ConnectionType = "mixed";
        else if (miiCount > 0 && ebusCount == 0)
            info.ConnectionType = "ethernet-only";
        else if (ebusCount > 0 && miiCount == 0)
            info.ConnectionType = "ebus-only";
        else
            info.ConnectionType = "unknown";
        
        // 3. Detectar Junction: 2+ puertos MII de SALIDA (puertos 1-3, el 0 es entrada)
        var miiOutputPorts = info.PortPhysics
            .Where(p => p.PortNumber > 0 && p.IsCable)
            .ToList();
        info.IsJunction = miiOutputPorts.Count >= 2;
        
        // 4. Determinar DeviceCategory desde GroupType + análisis
        info.DeviceCategory = DetermineDeviceCategory(info);
    }
    
    /// <summary>
    /// ⭐ Determina la categoría del dispositivo basándose en GroupType y Physics
    /// </summary>
    private string DetermineDeviceCategory(ESIDeviceInfo info)
    {
        var groupType = info.GroupType?.ToLowerInvariant() ?? "";
        var type = info.Type?.ToUpperInvariant() ?? "";
        
        // 1. Junction: dispositivos con 2+ salidas Ethernet
        if (info.IsJunction)
            return "junction";
        
        // 2. Coupler: GroupType = "Coupler" o nombres conocidos
        if (groupType.Contains("coupler") || 
            type.StartsWith("EK1") || type.StartsWith("BK1") ||
            type.StartsWith("CX") || type.StartsWith("CU"))
            return "coupler";
        
        // 3. Drive: GroupType contiene "drive" o series AX, AL
        if (groupType.Contains("drive") || groupType.Contains("servo") ||
            type.StartsWith("EL7") || type.StartsWith("AX") || type.StartsWith("AL"))
            return "drive";
        
        // 4. Encoder: GroupType contiene "encoder" o serie EL5
        if (groupType.Contains("encoder") || groupType.Contains("positioning") ||
            type.StartsWith("EL5"))
            return "encoder";
        
        // 5. Gateway/Communication: GroupType contiene "gateway" o serie EL6
        if (groupType.Contains("gateway") || groupType.Contains("communication") ||
            type.StartsWith("EL6"))
            return "gateway";
        
        // 6. Power/System: serie EL9
        if (type.StartsWith("EL9"))
            return "power";
        
        // 7. Terminal: todo lo demás (EL1xxx, EL2xxx, EL3xxx, EL4xxx, etc.)
        if (type.StartsWith("EL") || type.StartsWith("EP") || type.StartsWith("EQ"))
            return "terminal";
        
        // 8. Si es Ethernet-Only y no es ninguna categoría anterior, probablemente es Drive/Device externo
        if (info.ConnectionType == "ethernet-only")
            return "drive"; // Drives externos, YASKAWA, SICK, etc.
        
        return "terminal"; // Default
    }

    private static uint ParseHexOrDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        
        value = value.Trim();
        
        try
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToUInt32(value.Substring(2), 16);
            }
            else if (value.StartsWith("#"))
            {
                return Convert.ToUInt32(value.Substring(1), 16);
            }
            else if (value.All(c => char.IsDigit(c)))
            {
                return Convert.ToUInt32(value, 10);
            }
            else
            {
                // Intentar como hex sin prefijo
                return Convert.ToUInt32(value, 16);
            }
        }
        catch
        {
            return 0;
        }
    }
}
