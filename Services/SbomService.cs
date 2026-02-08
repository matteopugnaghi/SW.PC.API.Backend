// 📋 SBOM Service - Software Bill of Materials Generator
// EU CRA Compliance: Generates CycloneDX format SBOM
// Reads NuGet packages from .csproj and npm packages from package.json

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interface for SBOM generation and management
/// </summary>
public interface ISbomService
{
    /// <summary>Get current SBOM status</summary>
    Task<SbomStatus> GetStatusAsync();
    
    /// <summary>Generate new SBOM</summary>
    Task<SbomGenerateResult> GenerateAsync(SbomGenerateRequest request);
    
    /// <summary>Get full SBOM document</summary>
    Task<SbomDocument?> GetSbomAsync();
    
    /// <summary>Get SBOM as JSON string for download</summary>
    Task<string?> GetSbomJsonAsync();
}

/// <summary>
/// SBOM Service Implementation - Generates CycloneDX format SBOM
/// </summary>
public class SbomService : ISbomService
{
    private readonly ILogger<SbomService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditLogService _auditLogService;
    private readonly IProjectContextService _projectContext;
    private readonly ITwinCATService _twinCATService;
    private readonly IExcelConfigService _excelConfigService;
    
    // Paths
    private readonly string _backendProjectPath;
    private readonly string _frontendPath;
    private readonly string _contentRoot;
    
    // JSON options for CycloneDX format
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SbomService(
        ILogger<SbomService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IAuditLogService auditLogService,
        IProjectContextService projectContext,
        ITwinCATService twinCATService,
        IExcelConfigService excelConfigService)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _auditLogService = auditLogService;
        _projectContext = projectContext;
        _twinCATService = twinCATService;
        _excelConfigService = excelConfigService;
        
        // Get paths from configuration or use defaults
        _contentRoot = environment.ContentRootPath;
        _backendProjectPath = Path.Combine(_contentRoot, "SW.PC.API.Backend.csproj");
        
        // Frontend path - relative to backend
        var frontendRelativePath = configuration["Paths:FrontendPath"] ?? "../SW.PC.REACT.Frontend/my-3d-app";
        _frontendPath = Path.GetFullPath(Path.Combine(_contentRoot, frontendRelativePath));
    }
    
    /// <summary>
    /// Get SBOM output path based on active project
    /// In production: Projects/{projectId}/sbom/
    /// In development: wwwroot/sbom/ (for legacy compatibility)
    /// NOTE: This method does NOT create the directory - call EnsureSbomDirectoryExists() before writing
    /// </summary>
    private string GetSbomOutputPath()
    {
        var projectId = _projectContext.ActiveProjectId;
        
        if (projectId != "default")
        {
            // Multi-proyecto: Projects/{projectId}/sbom/
            return Path.Combine(_contentRoot, "Projects", projectId, "sbom");
        }
        else
        {
            // Legacy: wwwroot/sbom/
            return Path.Combine(_contentRoot, "wwwroot", "sbom");
        }
    }
    
    /// <summary>
    /// Ensure SBOM output directory exists (call before writing files)
    /// </summary>
    private void EnsureSbomDirectoryExists(string sbomPath)
    {
        if (!Directory.Exists(sbomPath))
        {
            Directory.CreateDirectory(sbomPath);
            _logger.LogInformation("📁 Created SBOM directory: {Path}", sbomPath);
        }
    }

    /// <summary>
    /// Get current SBOM status
    /// </summary>
    public async Task<SbomStatus> GetStatusAsync()
    {
        var status = new SbomStatus();
        
        try
        {
            var sbomOutputPath = GetSbomOutputPath();
            var sbomFilePath = Path.Combine(sbomOutputPath, "sbom-combined.json");
            
            if (File.Exists(sbomFilePath))
            {
                var fileInfo = new FileInfo(sbomFilePath);
                status.Exists = true;
                status.GeneratedAt = fileInfo.LastWriteTimeUtc;
                status.FilePath = sbomFilePath;
                status.FileSizeBytes = fileInfo.Length;
                
                // Read SBOM to get component counts
                var sbomJson = await File.ReadAllTextAsync(sbomFilePath);
                var sbom = JsonSerializer.Deserialize<SbomDocument>(sbomJson, JsonOptions);
                
                if (sbom != null)
                {
                    status.TotalComponents = sbom.Components.Count;
                    status.BackendComponents = sbom.Components.Count(c => c.Purl?.StartsWith("pkg:nuget") == true);
                    status.FrontendComponents = sbom.Components.Count(c => c.Purl?.StartsWith("pkg:npm") == true);
                    status.SpecVersion = sbom.SpecVersion;
                    
                    // Check if SBOM is up-to-date (within last 24 hours)
                    status.IsUpToDate = (DateTime.Now - fileInfo.LastWriteTimeUtc).TotalHours < 24;
                    status.Status = status.IsUpToDate ? "valid" : "outdated";
                    
                    // Try to get who generated it from metadata
                    if (sbom.Metadata?.Tools?.FirstOrDefault() is { } tool)
                    {
                        status.GeneratedBy = tool.Name;
                    }
                }
            }
            else
            {
                status.Exists = false;
                status.Status = "missing";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SBOM status");
            status.Status = "error";
            status.ErrorMessage = ex.Message;
        }
        
        return status;
    }

    /// <summary>
    /// Check if we're running in production mode (no source files available)
    /// </summary>
    private bool IsProductionMode()
    {
        // In production, the .csproj file won't exist - only compiled DLLs
        return !File.Exists(_backendProjectPath);
    }

    /// <summary>
    /// Generate new SBOM document
    /// </summary>
    public async Task<SbomGenerateResult> GenerateAsync(SbomGenerateRequest request)
    {
        var result = new SbomGenerateResult();
        
        try
        {
            // 🏭 PRODUCTION CHECK: Cannot generate new SBOM without source files
            if (IsProductionMode())
            {
                _logger.LogWarning("⚠️ Cannot generate SBOM in production mode - source files not available");
                
                // Check if pre-generated SBOM exists
                var existingStatus = await GetStatusAsync();
                if (existingStatus.Exists)
                {
                    result.Success = false;
                    result.Message = "🏭 Modo Producción: El SBOM fue pre-generado durante el despliegue. " +
                                   $"Contiene {existingStatus.TotalComponents} componentes. " +
                                   "Para regenerar, hazlo desde el entorno de desarrollo.";
                    result.Status = existingStatus;
                }
                else
                {
                    result.Success = false;
                    result.Message = "🏭 Modo Producción: No hay SBOM disponible. " +
                                   "El SBOM debe generarse en desarrollo y desplegarse con Deploy-Manual-Remote.ps1";
                }
                
                return result;
            }
            
            _logger.LogInformation("🔄 Generating SBOM... Requested by: {RequestedBy}", request.RequestedBy);
            
            // 📦 Load product info from Excel configuration
            var productName = "SW.PC.SUPERVISOR.System";
            var productVersion = "1.0.0";
            var productDescription = "Industrial Supervisor System";
            var productManufacturer = "Aquafrisch";
            
            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (!string.IsNullOrEmpty(excelPath) && File.Exists(excelPath))
                {
                    var sysConfig = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    if (!string.IsNullOrWhiteSpace(sysConfig.ProductName))
                        productName = sysConfig.ProductName;
                    if (!string.IsNullOrWhiteSpace(sysConfig.ProductVersion))
                        productVersion = sysConfig.ProductVersion;
                    if (!string.IsNullOrWhiteSpace(sysConfig.ProductDescription))
                        productDescription = sysConfig.ProductDescription;
                    if (!string.IsNullOrWhiteSpace(sysConfig.ProductManufacturer))
                        productManufacturer = sysConfig.ProductManufacturer;
                    
                    _logger.LogInformation("📦 Product info from Excel: {Name} v{Version} by {Manufacturer}", 
                        productName, productVersion, productManufacturer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Could not load product info from Excel: {Message}. Using defaults.", ex.Message);
            }
            
            var sbom = new SbomDocument
            {
                Metadata = new SbomMetadata
                {
                    Timestamp = DateTime.Now.ToString("o"),
                    Tools = new List<SbomTool>
                    {
                        new() 
                        { 
                            Vendor = productManufacturer,
                            Name = "SW.PC.API.Backend SBOM Generator",
                            Version = "1.0.0"
                        }
                    },
                    Component = new SbomComponent
                    {
                        Type = "application",
                        Name = productName,
                        Version = productVersion,
                        Description = productDescription
                    },
                    Manufacture = new SbomOrganization
                    {
                        Name = productManufacturer
                    }
                },
                Components = new List<SbomComponent>()
            };
            
            // Generate Backend (NuGet) components
            if (request.IncludeBackend)
            {
                var nugetComponents = await GetNuGetComponentsAsync();
                sbom.Components.AddRange(nugetComponents);
                _logger.LogInformation("📦 Added {Count} NuGet packages", nugetComponents.Count);
            }
            
            // Generate Frontend (npm) components
            if (request.IncludeFrontend)
            {
                var npmComponents = await GetNpmComponentsAsync(request.IncludeDevDependencies);
                sbom.Components.AddRange(npmComponents);
                _logger.LogInformation("📦 Added {Count} npm packages", npmComponents.Count);
            }
            
            // Generate OT (Operational Technology) components - TwinCAT, IPC, etc.
            var otComponents = await GetOtComponentsAsync();
            sbom.Components.AddRange(otComponents);
            _logger.LogInformation("🏭 Added {Count} OT components (TwinCAT, IPC, Excel)", otComponents.Count);
            
            // Serialize and save
            var sbomJson = JsonSerializer.Serialize(sbom, JsonOptions);
            
            // Save combined SBOM
            var sbomOutputPath = GetSbomOutputPath();
            EnsureSbomDirectoryExists(sbomOutputPath); // Crear directorio solo cuando vamos a escribir
            var combinedPath = Path.Combine(sbomOutputPath, "sbom-combined.json");
            await File.WriteAllTextAsync(combinedPath, sbomJson);
            
            // Save timestamped version for history
            var historyPath = Path.Combine(sbomOutputPath, "history");
            EnsureSbomDirectoryExists(historyPath); // Crear subdirectorio history
            var timestampedPath = Path.Combine(historyPath, $"sbom-{DateTime.Now:yyyy-MM-dd-HHmmss}.json");
            await File.WriteAllTextAsync(timestampedPath, sbomJson);
            
            result.Success = true;
            result.Message = $"SBOM generated successfully with {sbom.Components.Count} components";
            result.GeneratedAt = DateTime.Now;
            result.DownloadUrl = "/sbom/sbom-combined.json";
            result.Status = await GetStatusAsync();
            
            _logger.LogInformation("✅ SBOM generated: {Count} total components", sbom.Components.Count);
            
            // 📋 Audit Log - EU CRA Compliance
            await _auditLogService.LogAsync(
                AuditCategory.Sbom,
                AuditAction.SbomGenerate,
                AuditResult.Success,
                $"Generated SBOM with {sbom.Components.Count} components (Backend: {sbom.Components.Count(c => c.Purl?.StartsWith("pkg:nuget") == true)}, Frontend: {sbom.Components.Count(c => c.Purl?.StartsWith("pkg:npm") == true)})",
                request.RequestedBy,
                affectedItemCount: sbom.Components.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating SBOM");
            result.Success = false;
            result.Message = $"Error generating SBOM: {ex.Message}";
            
            // 📋 Audit Log - Error
            await _auditLogService.LogAsync(
                AuditCategory.Sbom,
                AuditAction.SbomGenerate,
                AuditResult.Error,
                $"Failed to generate SBOM: {ex.Message}",
                request.RequestedBy
            );
        }
        
        return result;
    }

    /// <summary>
    /// Get full SBOM document
    /// </summary>
    public async Task<SbomDocument?> GetSbomAsync()
    {
        try
        {
            var sbomFilePath = Path.Combine(GetSbomOutputPath(), "sbom-combined.json");
            
            if (!File.Exists(sbomFilePath))
            {
                return null;
            }
            
            var sbomJson = await File.ReadAllTextAsync(sbomFilePath);
            return JsonSerializer.Deserialize<SbomDocument>(sbomJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading SBOM");
            return null;
        }
    }

    /// <summary>
    /// Get SBOM as JSON string for download
    /// </summary>
    public async Task<string?> GetSbomJsonAsync()
    {
        try
        {
            var sbomFilePath = Path.Combine(GetSbomOutputPath(), "sbom-combined.json");
            
            if (!File.Exists(sbomFilePath))
            {
                return null;
            }
            
            return await File.ReadAllTextAsync(sbomFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading SBOM JSON");
            return null;
        }
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    /// <summary>
    /// Parse NuGet packages from .csproj file
    /// </summary>
    private async Task<List<SbomComponent>> GetNuGetComponentsAsync()
    {
        var components = new List<SbomComponent>();
        
        try
        {
            if (!File.Exists(_backendProjectPath))
            {
                _logger.LogWarning("Backend project file not found: {Path}", _backendProjectPath);
                return components;
            }
            
            var projectXml = await File.ReadAllTextAsync(_backendProjectPath);
            var doc = XDocument.Parse(projectXml);
            
            // Find all PackageReference elements
            var packageReferences = doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .ToList();
            
            foreach (var packageRef in packageReferences)
            {
                var name = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Attribute("Version")?.Value 
                    ?? packageRef.Element(XName.Get("Version", packageRef.Name.NamespaceName))?.Value
                    ?? "unknown";
                
                if (string.IsNullOrEmpty(name)) continue;
                
                // Parse group from package name (e.g., "Microsoft.Extensions.Logging" -> "Microsoft.Extensions")
                var nameParts = name.Split('.');
                var group = nameParts.Length > 2 
                    ? string.Join(".", nameParts.Take(nameParts.Length - 1))
                    : nameParts.FirstOrDefault();
                
                components.Add(new SbomComponent
                {
                    Type = "library",
                    BomRef = $"pkg:nuget/{name}@{version}",
                    Group = group,
                    Name = name,
                    Version = version,
                    Purl = $"pkg:nuget/{name}@{version}",
                    Publisher = "NuGet",
                    Scope = "required",
                    ExternalReferences = new List<SbomExternalReference>
                    {
                        new()
                        {
                            Type = "website",
                            Url = $"https://www.nuget.org/packages/{name}/{version}"
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing NuGet packages from .csproj");
        }
        
        return components;
    }

    /// <summary>
    /// Parse npm packages from package.json
    /// </summary>
    private async Task<List<SbomComponent>> GetNpmComponentsAsync(bool includeDevDependencies)
    {
        var components = new List<SbomComponent>();
        
        try
        {
            var packageJsonPath = Path.Combine(_frontendPath, "package.json");
            
            if (!File.Exists(packageJsonPath))
            {
                _logger.LogWarning("Frontend package.json not found: {Path}", packageJsonPath);
                return components;
            }
            
            var packageJson = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = JsonDocument.Parse(packageJson);
            var root = doc.RootElement;
            
            // Parse dependencies
            if (root.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dep in dependencies.EnumerateObject())
                {
                    var name = dep.Name;
                    var version = dep.Value.GetString()?.TrimStart('^', '~') ?? "unknown";
                    
                    // Parse scope from package name (e.g., "@babel/core" -> "@babel")
                    var group = name.StartsWith("@") 
                        ? name.Split('/').FirstOrDefault()
                        : null;
                    
                    components.Add(new SbomComponent
                    {
                        Type = "library",
                        BomRef = $"pkg:npm/{name}@{version}",
                        Group = group,
                        Name = name,
                        Version = version,
                        Purl = $"pkg:npm/{name}@{version}",
                        Publisher = "npm",
                        Scope = "required",
                        ExternalReferences = new List<SbomExternalReference>
                        {
                            new()
                            {
                                Type = "website",
                                Url = $"https://www.npmjs.com/package/{name}/v/{version}"
                            }
                        }
                    });
                }
            }
            
            // Parse devDependencies if requested
            if (includeDevDependencies && root.TryGetProperty("devDependencies", out var devDependencies))
            {
                foreach (var dep in devDependencies.EnumerateObject())
                {
                    var name = dep.Name;
                    var version = dep.Value.GetString()?.TrimStart('^', '~') ?? "unknown";
                    var group = name.StartsWith("@") ? name.Split('/').FirstOrDefault() : null;
                    
                    components.Add(new SbomComponent
                    {
                        Type = "library",
                        BomRef = $"pkg:npm/{name}@{version}",
                        Group = group,
                        Name = name,
                        Version = version,
                        Purl = $"pkg:npm/{name}@{version}",
                        Publisher = "npm",
                        Scope = "optional", // devDependencies are optional
                        ExternalReferences = new List<SbomExternalReference>
                        {
                            new()
                            {
                                Type = "website",
                                Url = $"https://www.npmjs.com/package/{name}/v/{version}"
                            }
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing npm packages from package.json");
        }
        
        return components;
    }
    
    /// <summary>
    /// Get OT (Operational Technology) components - TwinCAT, IPC, Excel config, etc.
    /// These are hardware/firmware components in the industrial system
    /// </summary>
    private async Task<List<SbomComponent>> GetOtComponentsAsync()
    {
        var components = new List<SbomComponent>();
        
        try
        {
            // 1. TwinCAT Runtime (automático)
            var tcVersion = _twinCATService.GetVersionInfo();
            
            components.Add(new SbomComponent
            {
                Type = "firmware",
                BomRef = $"pkg:ot/beckhoff/twincat-runtime@{tcVersion.RuntimeVersion}",
                Group = "OT-PLC",
                Name = "TwinCAT Runtime",
                Version = tcVersion.RuntimeVersion,
                Description = $"Beckhoff TwinCAT PLC Runtime - Device: {tcVersion.DeviceName}",
                Publisher = "Beckhoff Automation",
                Purl = $"pkg:ot/beckhoff/twincat-runtime@{tcVersion.MajorVersion}.{tcVersion.MinorVersion}.{tcVersion.BuildNumber}",
                ExternalReferences = new List<SbomExternalReference>
                {
                    new() { Type = "website", Url = "https://www.beckhoff.com/twincat3/" }
                }
            });
            
            // 2. TwinCAT ADS Client Library
            components.Add(new SbomComponent
            {
                Type = "library",
                BomRef = $"pkg:nuget/Beckhoff.TwinCAT.Ads@{tcVersion.AdsVersion}",
                Group = "OT-PLC",
                Name = "TwinCAT.Ads",
                Version = tcVersion.AdsVersion,
                Description = "Beckhoff TwinCAT ADS Communication Library",
                Publisher = "Beckhoff Automation",
                Purl = $"pkg:nuget/Beckhoff.TwinCAT.Ads@{tcVersion.AdsVersion}",
                ExternalReferences = new List<SbomExternalReference>
                {
                    new() { Type = "website", Url = "https://www.nuget.org/packages/Beckhoff.TwinCAT.Ads" }
                }
            });
            
            // 3. IPC / Host System
            var osVersion = Environment.OSVersion;
            var machineName = Environment.MachineName;
            
            components.Add(new SbomComponent
            {
                Type = "operating-system",
                BomRef = $"pkg:ot/microsoft/windows@{osVersion.Version}",
                Group = "OT-IPC",
                Name = "Windows IPC",
                Version = osVersion.VersionString,
                Description = $"Industrial PC Operating System - Machine: {machineName}",
                Publisher = "Microsoft",
                Purl = $"pkg:generic/microsoft/windows@{osVersion.Version}",
                ExternalReferences = new List<SbomExternalReference>
                {
                    new() { Type = "website", Url = "https://www.microsoft.com/windows" }
                }
            });
            
            // 4. .NET Runtime
            components.Add(new SbomComponent
            {
                Type = "framework",
                BomRef = $"pkg:ot/microsoft/dotnet@{Environment.Version}",
                Group = "OT-IPC",
                Name = ".NET Runtime",
                Version = Environment.Version.ToString(),
                Description = "Microsoft .NET Runtime for Backend Application",
                Publisher = "Microsoft",
                Purl = $"pkg:generic/microsoft/dotnet@{Environment.Version}",
                ExternalReferences = new List<SbomExternalReference>
                {
                    new() { Type = "website", Url = "https://dotnet.microsoft.com/" }
                }
            });
            
            _logger.LogDebug("🏭 OT Components: TwinCAT {TcVersion}, ADS {AdsVersion}, Windows {WinVersion}, .NET {NetVersion}",
                tcVersion.RuntimeVersion, tcVersion.AdsVersion, osVersion.VersionString, Environment.Version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting automatic OT components - some may be unavailable");
        }
        
        // 5. Componentes OT desde Excel (Firewall, Switches, etc.)
        try
        {
            var excelPath = _excelConfigService.GetExcelConfigPath();
            if (!string.IsNullOrEmpty(excelPath) && File.Exists(excelPath))
            {
                var excelOtComponents = await _excelConfigService.LoadOtComponentsAsync(excelPath);
                
                foreach (var ot in excelOtComponents)
                {
                    var purl = $"pkg:ot/{ot.Manufacturer.ToLower().Replace(" ", "-")}/{ot.Model.ToLower().Replace(" ", "-")}@{ot.Version}";
                    
                    components.Add(new SbomComponent
                    {
                        Type = MapOtTypeToSbomType(ot.Type),
                        BomRef = purl,
                        Group = $"OT-{ot.Type.ToUpper()}",
                        Name = $"{ot.Manufacturer} {ot.Model}",
                        Version = ot.Version,
                        Description = ot.Description ?? $"{ot.Type}: {ot.Manufacturer} {ot.Model}" + 
                                     (string.IsNullOrEmpty(ot.Location) ? "" : $" @ {ot.Location}"),
                        Publisher = ot.Manufacturer,
                        Purl = purl,
                        ExternalReferences = string.IsNullOrEmpty(ot.SupportUrl) ? null : new List<SbomExternalReference>
                        {
                            new() { Type = "website", Url = ot.SupportUrl }
                        }
                    });
                }
                
                if (excelOtComponents.Count > 0)
                {
                    _logger.LogInformation("🏭 Added {Count} OT components from Excel (manual config)", excelOtComponents.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading OT components from Excel");
        }
        
        return components;
    }
    
    /// <summary>
    /// Mapea tipos OT del Excel a tipos SBOM estándar
    /// </summary>
    private static string MapOtTypeToSbomType(string otType)
    {
        return otType.ToLower() switch
        {
            "firewall" => "device",
            "switch" => "device",
            "router" => "device",
            "gateway" => "device",
            "plc" => "firmware",
            "hmi" => "device",
            "sensor" => "device",
            "drive" => "firmware",
            "ups" => "device",
            _ => "device"
        };
    }
}
