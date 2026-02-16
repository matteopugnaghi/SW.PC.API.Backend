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

    /// <summary>
    /// Known license mappings for NuGet packages that use file-based licenses
    /// (their .nuspec has license type="file" pointing to a .md, which is useless in SBOM)
    /// ⚠️ Packages with commercial/restrictive licenses are flagged here
    /// </summary>
    private static readonly Dictionary<string, (string Id, string? Name, string? Url, bool IsCommercial)> KnownNuGetLicenses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BCrypt.Net-Next"]                         = ("MIT", null, "https://github.com/BcryptNet/bcrypt.net/blob/main/licence.txt", false),
        ["Beckhoff.TwinCAT.Ads"]                    = ("LicenseRef-Beckhoff", "Beckhoff Proprietary License", "https://download.beckhoff.com/download/Document/automation/twincat3/TwinCAT3LicenseTerms.pdf", true),
        ["ClosedXML"]                               = ("MIT", null, "https://github.com/ClosedXML/ClosedXML/blob/develop/LICENSE", false),
        ["QuestPDF"]                                = ("LicenseRef-QuestPDF-Community", "QuestPDF Community License (free <$1M revenue)", "https://www.questpdf.com/license/", true),
        ["Microsoft.AspNetCore.SignalR"]             = ("Apache-2.0", null, "https://licenses.nuget.org/Apache-2.0", false),
        ["DocumentFormat.OpenXml"]                  = ("MIT", null, null, false),
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
                var nugetWithLicense = nugetComponents.Count(c => c.Licenses?.Count > 0);
                _logger.LogInformation("📦 Added {Count} NuGet packages ({Licensed} with license info)", 
                    nugetComponents.Count, nugetWithLicense);
            }
            
            // Generate Frontend (npm) components
            if (request.IncludeFrontend)
            {
                var npmComponents = await GetNpmComponentsAsync(request.IncludeDevDependencies);
                sbom.Components.AddRange(npmComponents);
                var npmWithLicense = npmComponents.Count(c => c.Licenses?.Count > 0);
                _logger.LogInformation("📦 Added {Count} npm packages ({Licensed} with license info)", 
                    npmComponents.Count, npmWithLicense);
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
                
                // Extract license from NuGet cache (.nuspec)
                var licenses = await GetNuGetLicenseAsync(name, version);
                
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
                    Licenses = licenses,
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
                    
                    // Extract license from node_modules
                    var licenses = GetNpmLicense(name);
                    
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
                        Licenses = licenses,
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
                    
                    // Extract license from node_modules
                    var devLicenses = GetNpmLicense(name);
                    
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
                        Licenses = devLicenses,
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
                Licenses = new List<SbomLicense>
                {
                    new() { License = new SbomLicenseInfo { Id = "LicenseRef-Beckhoff", Name = "Beckhoff Proprietary License", Url = "https://download.beckhoff.com/download/Document/automation/twincat3/TwinCAT3LicenseTerms.pdf" } }
                },
                ExternalReferences = new List<SbomExternalReference>
                {
                    new() { Type = "website", Url = "https://www.beckhoff.com/twincat3/" }
                }
            });
            
            // 2. TwinCAT ADS - SKIP: already included from NuGet PackageReference scan
            //    (avoids duplicate with different version numbers)
            
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
                Licenses = new List<SbomLicense>
                {
                    new() { License = new SbomLicenseInfo { Id = "MIT", Url = "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT" } }
                },
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
    
    // ============================================
    // 📜 License Extraction Methods
    // ============================================

    /// <summary>
    /// Extract license information from NuGet global package cache (.nuspec files)
    /// Path: %USERPROFILE%\.nuget\packages\{name}\{version}\{name}.nuspec
    /// </summary>
    private async Task<List<SbomLicense>?> GetNuGetLicenseAsync(string packageName, string version)
    {
        try
        {
            var nugetCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", packageName.ToLowerInvariant(), version);
            
            if (!Directory.Exists(nugetCachePath))
                return null;
            
            var nuspecPath = Directory.GetFiles(nugetCachePath, "*.nuspec").FirstOrDefault();
            if (nuspecPath == null || !File.Exists(nuspecPath))
                return null;
            
            var nuspecXml = await File.ReadAllTextAsync(nuspecPath);
            var nuspecDoc = XDocument.Parse(nuspecXml);
            var ns = nuspecDoc.Root?.Name.Namespace ?? XNamespace.None;
            var metadata = nuspecDoc.Root?.Element(ns + "metadata");
            
            if (metadata == null)
                return null;
            
            // Try <license type="expression">MIT</license> (modern format)
            var licenseElement = metadata.Element(ns + "license");
            if (licenseElement != null)
            {
                var licenseType = licenseElement.Attribute("type")?.Value;
                var licenseValue = licenseElement.Value.Trim();
                
                if (licenseType == "expression")
                {
                    return new List<SbomLicense>
                    {
                        new() { Expression = licenseValue }
                    };
                }
                
                // type="file" → just a filename (e.g. "license.md") — try known licenses fallback
                if (licenseType == "file" || licenseValue.EndsWith(".md", StringComparison.OrdinalIgnoreCase) 
                    || licenseValue.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    var fallback = GetKnownLicenseFallback(packageName);
                    if (fallback != null) return fallback;
                }
                
                return new List<SbomLicense>
                {
                    new() { License = new SbomLicenseInfo { Name = licenseValue } }
                };
            }
            
            // Try <licenseUrl> (legacy format)
            var licenseUrl = metadata.Element(ns + "licenseUrl")?.Value;
            if (!string.IsNullOrEmpty(licenseUrl) && licenseUrl != "https://aka.ms/deprecateLicenseUrl")
            {
                var spdxId = DetectSpdxFromUrl(licenseUrl);
                
                // If URL detection failed, try known licenses fallback
                if (spdxId == null)
                {
                    var fallback = GetKnownLicenseFallback(packageName);
                    if (fallback != null) return fallback;
                }
                
                return new List<SbomLicense>
                {
                    new()
                    {
                        License = new SbomLicenseInfo
                        {
                            Id = spdxId,
                            Name = spdxId ?? "See license URL",
                            Url = licenseUrl
                        }
                    }
                };
            }
            
            // Final fallback: check known licenses dictionary
            var knownFallback = GetKnownLicenseFallback(packageName);
            if (knownFallback != null) return knownFallback;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not extract NuGet license for {Package}@{Version}: {Error}",
                packageName, version, ex.Message);
        }
        
        return null;
    }

    /// <summary>
    /// Extract license information from npm node_modules package.json
    /// Reads: node_modules/{packageName}/package.json → "license" or "licenses" field
    /// </summary>
    private List<SbomLicense>? GetNpmLicense(string packageName)
    {
        try
        {
            var packageJsonPath = Path.Combine(_frontendPath, "node_modules", packageName, "package.json");
            if (!File.Exists(packageJsonPath))
                return null;
            
            var json = File.ReadAllText(packageJsonPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Try "license" string (modern format, SPDX expression)
            if (root.TryGetProperty("license", out var license))
            {
                if (license.ValueKind == JsonValueKind.String)
                {
                    var licenseValue = license.GetString();
                    if (!string.IsNullOrEmpty(licenseValue) && licenseValue != "UNLICENSED")
                    {
                        // Compound SPDX expressions like "MIT OR Apache-2.0"
                        if (licenseValue.Contains(" OR ") || licenseValue.Contains(" AND ") || licenseValue.Contains("("))
                        {
                            return new List<SbomLicense>
                            {
                                new() { Expression = licenseValue }
                            };
                        }
                        
                        return new List<SbomLicense>
                        {
                            new() { License = new SbomLicenseInfo { Id = licenseValue } }
                        };
                    }
                }
                else if (license.ValueKind == JsonValueKind.Object)
                {
                    // Object format: { "type": "MIT", "url": "..." }
                    var type = license.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var url = license.TryGetProperty("url", out var u) ? u.GetString() : null;
                    if (!string.IsNullOrEmpty(type))
                    {
                        return new List<SbomLicense>
                        {
                            new() { License = new SbomLicenseInfo { Id = type, Url = url } }
                        };
                    }
                }
            }
            
            // Try "licenses" array (deprecated but still used by some packages)
            if (root.TryGetProperty("licenses", out var licenses) && licenses.ValueKind == JsonValueKind.Array)
            {
                var result = new List<SbomLicense>();
                foreach (var lic in licenses.EnumerateArray())
                {
                    var type = lic.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var url = lic.TryGetProperty("url", out var u) ? u.GetString() : null;
                    
                    if (!string.IsNullOrEmpty(type))
                    {
                        result.Add(new SbomLicense
                        {
                            License = new SbomLicenseInfo { Id = type, Url = url }
                        });
                    }
                }
                return result.Count > 0 ? result : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not extract npm license for {Package}: {Error}",
                packageName, ex.Message);
        }
        
        return null;
    }

    /// <summary>
    /// Detect SPDX license identifier from a license URL
    /// </summary>
    private static string? DetectSpdxFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var lower = url.ToLowerInvariant();
        
        if (lower.Contains("apache") && lower.Contains("2")) return "Apache-2.0";
        if (lower.Contains("/mit")) return "MIT";
        if (lower.Contains("bsd-3") || lower.Contains("bsd/3")) return "BSD-3-Clause";
        if (lower.Contains("bsd-2") || lower.Contains("bsd/2")) return "BSD-2-Clause";
        if (lower.Contains("lgpl-3")) return "LGPL-3.0-only";
        if (lower.Contains("lgpl-2.1")) return "LGPL-2.1-only";
        if (lower.Contains("gpl-3")) return "GPL-3.0-only";
        if (lower.Contains("gpl-2")) return "GPL-2.0-only";
        if (lower.Contains("mpl-2")) return "MPL-2.0";
        if (lower.Contains("isc")) return "ISC";
        if (lower.Contains("unlicense")) return "Unlicense";
        if (lower.Contains("ms-pl")) return "MS-PL";
        if (lower.Contains("ms-rl")) return "MS-RL";
        
        // GitHub raw LICENSE files (e.g., aspnet/AspNetCore)
        if (lower.Contains("github") && lower.Contains("license"))
        {
            if (lower.Contains("aspnet") || lower.Contains("dotnet")) return "Apache-2.0";
        }
        
        return null;
    }

    /// <summary>
    /// Get license from known licenses dictionary (fallback for file-based or unknown licenses)
    /// </summary>
    private static List<SbomLicense>? GetKnownLicenseFallback(string packageName)
    {
        if (KnownNuGetLicenses.TryGetValue(packageName, out var known))
        {
            return new List<SbomLicense>
            {
                new()
                {
                    License = new SbomLicenseInfo
                    {
                        Id = known.Id,
                        Name = known.Name ?? known.Id,
                        Url = known.Url
                    }
                }
            };
        }
        return null;
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
