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
    
    // Paths
    private readonly string _backendProjectPath;
    private readonly string _frontendPath;
    private readonly string _sbomOutputPath;
    
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
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        
        // Get paths from configuration or use defaults
        var contentRoot = environment.ContentRootPath;
        _backendProjectPath = Path.Combine(contentRoot, "SW.PC.API.Backend.csproj");
        
        // Frontend path - relative to backend
        var frontendRelativePath = configuration["Paths:FrontendPath"] ?? "../SW.PC.REACT.Frontend/my-3d-app";
        _frontendPath = Path.GetFullPath(Path.Combine(contentRoot, frontendRelativePath));
        
        // SBOM output directory
        _sbomOutputPath = Path.Combine(contentRoot, "wwwroot", "sbom");
        
        // Ensure output directory exists
        Directory.CreateDirectory(_sbomOutputPath);
    }

    /// <summary>
    /// Get current SBOM status
    /// </summary>
    public async Task<SbomStatus> GetStatusAsync()
    {
        var status = new SbomStatus();
        
        try
        {
            var sbomFilePath = Path.Combine(_sbomOutputPath, "sbom-combined.json");
            
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
                    status.IsUpToDate = (DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalHours < 24;
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
    /// Generate new SBOM document
    /// </summary>
    public async Task<SbomGenerateResult> GenerateAsync(SbomGenerateRequest request)
    {
        var result = new SbomGenerateResult();
        
        try
        {
            _logger.LogInformation("🔄 Generating SBOM... Requested by: {RequestedBy}", request.RequestedBy);
            
            var sbom = new SbomDocument
            {
                Metadata = new SbomMetadata
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Tools = new List<SbomTool>
                    {
                        new() 
                        { 
                            Vendor = "Aquafrisch",
                            Name = "SW.PC.API.Backend SBOM Generator",
                            Version = "1.0.0"
                        }
                    },
                    Component = new SbomComponent
                    {
                        Type = "application",
                        Name = "SW.PC.SUPERVISOR.System",
                        Version = "1.0.0",
                        Description = "Industrial Supervisor System"
                    },
                    Manufacture = new SbomOrganization
                    {
                        Name = "Aquafrisch"
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
            
            // Serialize and save
            var sbomJson = JsonSerializer.Serialize(sbom, JsonOptions);
            
            // Save combined SBOM
            var combinedPath = Path.Combine(_sbomOutputPath, "sbom-combined.json");
            await File.WriteAllTextAsync(combinedPath, sbomJson);
            
            // Save timestamped version for history
            var historyPath = Path.Combine(_sbomOutputPath, "history");
            Directory.CreateDirectory(historyPath);
            var timestampedPath = Path.Combine(historyPath, $"sbom-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json");
            await File.WriteAllTextAsync(timestampedPath, sbomJson);
            
            result.Success = true;
            result.Message = $"SBOM generated successfully with {sbom.Components.Count} components";
            result.GeneratedAt = DateTime.UtcNow;
            result.DownloadUrl = "/sbom/sbom-combined.json";
            result.Status = await GetStatusAsync();
            
            _logger.LogInformation("✅ SBOM generated: {Count} total components", sbom.Components.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating SBOM");
            result.Success = false;
            result.Message = $"Error generating SBOM: {ex.Message}";
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
            var sbomFilePath = Path.Combine(_sbomOutputPath, "sbom-combined.json");
            
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
            var sbomFilePath = Path.Combine(_sbomOutputPath, "sbom-combined.json");
            
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
}
