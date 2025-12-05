// 📋 SBOM Models - CycloneDX 1.5 Format
// EU CRA Compliance: Software Bill of Materials
// Ref: https://cyclonedx.org/specification/overview/

namespace SW.PC.API.Backend.Models;

/// <summary>
/// CycloneDX SBOM Document - Standard format for Software Bill of Materials
/// Required by EU Cyber Resilience Act (CRA) for software transparency
/// </summary>
public class SbomDocument
{
    /// <summary>Format identifier - always "CycloneDX"</summary>
    public string BomFormat { get; set; } = "CycloneDX";
    
    /// <summary>CycloneDX specification version</summary>
    public string SpecVersion { get; set; } = "1.5";
    
    /// <summary>Serial number (UUID) for this SBOM instance</summary>
    public string SerialNumber { get; set; } = $"urn:uuid:{Guid.NewGuid()}";
    
    /// <summary>Version of this SBOM (increments on regeneration)</summary>
    public int Version { get; set; } = 1;
    
    /// <summary>Metadata about the SBOM and main component</summary>
    public SbomMetadata Metadata { get; set; } = new();
    
    /// <summary>List of all software components/dependencies</summary>
    public List<SbomComponent> Components { get; set; } = new();
    
    /// <summary>Dependency relationships between components</summary>
    public List<SbomDependency>? Dependencies { get; set; }
}

/// <summary>
/// SBOM Metadata - Information about when/how the SBOM was generated
/// </summary>
public class SbomMetadata
{
    /// <summary>ISO 8601 timestamp of SBOM generation</summary>
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    
    /// <summary>Tools used to generate this SBOM</summary>
    public List<SbomTool> Tools { get; set; } = new()
    {
        new SbomTool 
        { 
            Vendor = "Aquafrisch", 
            Name = "SW.PC.API.Backend", 
            Version = "1.0.0" 
        }
    };
    
    /// <summary>The main component this SBOM describes</summary>
    public SbomComponent? Component { get; set; }
    
    /// <summary>Manufacturer information</summary>
    public SbomOrganization? Manufacture { get; set; }
    
    /// <summary>Supplier information</summary>
    public SbomOrganization? Supplier { get; set; }
}

/// <summary>
/// SBOM Tool - Information about the tool that generated the SBOM
/// </summary>
public class SbomTool
{
    public string? Vendor { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
}

/// <summary>
/// SBOM Organization - Company/organization information
/// </summary>
public class SbomOrganization
{
    public string? Name { get; set; }
    public List<SbomContact>? Contact { get; set; }
    public List<string>? Url { get; set; }
}

/// <summary>
/// SBOM Contact - Contact person information
/// </summary>
public class SbomContact
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

/// <summary>
/// SBOM Component - A single software component/dependency
/// </summary>
public class SbomComponent
{
    /// <summary>Type: application, library, framework, etc.</summary>
    public string Type { get; set; } = "library";
    
    /// <summary>Unique identifier for this component</summary>
    public string? BomRef { get; set; }
    
    /// <summary>Component group/namespace (e.g., "Microsoft.Extensions")</summary>
    public string? Group { get; set; }
    
    /// <summary>Component name</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Component version</summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>Component description</summary>
    public string? Description { get; set; }
    
    /// <summary>Package URL (purl) - standard format for identifying packages</summary>
    public string? Purl { get; set; }
    
    /// <summary>Licenses associated with this component</summary>
    public List<SbomLicense>? Licenses { get; set; }
    
    /// <summary>External references (repository, website, etc.)</summary>
    public List<SbomExternalReference>? ExternalReferences { get; set; }
    
    /// <summary>Hashes for integrity verification</summary>
    public List<SbomHash>? Hashes { get; set; }
    
    /// <summary>Source: "nuget", "npm", "internal"</summary>
    public string? Publisher { get; set; }
    
    /// <summary>Scope: "required", "optional", "dev"</summary>
    public string? Scope { get; set; }
}

/// <summary>
/// SBOM License - License information for a component
/// </summary>
public class SbomLicense
{
    public SbomLicenseInfo? License { get; set; }
    public string? Expression { get; set; }
}

/// <summary>
/// SBOM License Info - Detailed license information
/// </summary>
public class SbomLicenseInfo
{
    /// <summary>SPDX License ID (e.g., "MIT", "Apache-2.0")</summary>
    public string? Id { get; set; }
    
    /// <summary>License name if not SPDX standard</summary>
    public string? Name { get; set; }
    
    /// <summary>URL to license text</summary>
    public string? Url { get; set; }
}

/// <summary>
/// SBOM External Reference - Links to external resources
/// </summary>
public class SbomExternalReference
{
    /// <summary>Type: vcs, website, documentation, etc.</summary>
    public string Type { get; set; } = "website";
    
    /// <summary>URL to the resource</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>Comment about this reference</summary>
    public string? Comment { get; set; }
}

/// <summary>
/// SBOM Hash - Integrity hash for a component
/// </summary>
public class SbomHash
{
    /// <summary>Algorithm: SHA-256, SHA-512, etc.</summary>
    public string Alg { get; set; } = "SHA-256";
    
    /// <summary>Hash value</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// SBOM Dependency - Relationship between components
/// </summary>
public class SbomDependency
{
    /// <summary>Reference to the component</summary>
    public string Ref { get; set; } = string.Empty;
    
    /// <summary>Components this depends on</summary>
    public List<string>? DependsOn { get; set; }
}

// ============================================
// 📊 SBOM Status Models - For API responses
// ============================================

/// <summary>
/// SBOM Status - Current state of SBOM generation
/// </summary>
public class SbomStatus
{
    /// <summary>Whether SBOM files exist</summary>
    public bool Exists { get; set; }
    
    /// <summary>Last generation timestamp</summary>
    public DateTime? GeneratedAt { get; set; }
    
    /// <summary>Who triggered the generation</summary>
    public string? GeneratedBy { get; set; }
    
    /// <summary>Total component count</summary>
    public int TotalComponents { get; set; }
    
    /// <summary>Backend (NuGet) component count</summary>
    public int BackendComponents { get; set; }
    
    /// <summary>Frontend (npm) component count</summary>
    public int FrontendComponents { get; set; }
    
    /// <summary>Is the SBOM up-to-date with current dependencies?</summary>
    public bool IsUpToDate { get; set; }
    
    /// <summary>Path to the SBOM file</summary>
    public string? FilePath { get; set; }
    
    /// <summary>File size in bytes</summary>
    public long? FileSizeBytes { get; set; }
    
    /// <summary>CycloneDX format version</summary>
    public string SpecVersion { get; set; } = "1.5";
    
    /// <summary>Validation status</summary>
    public string Status { get; set; } = "unknown"; // valid, outdated, missing, error
    
    /// <summary>Error message if generation failed</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// SBOM Generation Request - Parameters for generating SBOM
/// </summary>
public class SbomGenerateRequest
{
    /// <summary>Who is requesting the generation</summary>
    public string? RequestedBy { get; set; } = "System";
    
    /// <summary>Include backend (NuGet) dependencies</summary>
    public bool IncludeBackend { get; set; } = true;
    
    /// <summary>Include frontend (npm) dependencies</summary>
    public bool IncludeFrontend { get; set; } = true;
    
    /// <summary>Include dev dependencies</summary>
    public bool IncludeDevDependencies { get; set; } = false;
    
    /// <summary>Force regeneration even if up-to-date</summary>
    public bool Force { get; set; } = false;
}

/// <summary>
/// SBOM Generation Result - Response after generating SBOM
/// </summary>
public class SbomGenerateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SbomStatus? Status { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? DownloadUrl { get; set; }
}
