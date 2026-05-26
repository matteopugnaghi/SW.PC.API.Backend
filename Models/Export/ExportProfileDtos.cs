// ============================================================================
// ExportProfileDtos.cs — DTOs de FolderProfile y EmailProfile (Wizard UI)
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models.Export;

// ───────────────────────── FOLDER PROFILES ─────────────────────────

public class ExportFolderProfileRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Path { get; set; } = string.Empty;
    [MaxLength(200)] public string? Subfolder { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
}

public class ExportFolderProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Subfolder { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

// ───────────────────────── EMAIL PROFILES ─────────────────────────

public class ExportEmailProfileRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 587;
    [MaxLength(200)] public string? Username { get; set; }
    /// <summary>Solo en POST/PUT. En PUT, null/empty = no cambiar password existente.</summary>
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
    [Required, MaxLength(200), EmailAddress] public string FromAddress { get; set; } = string.Empty;
    [MaxLength(100)] public string? FromName { get; set; }
    [MaxLength(1000)] public string? DefaultRecipients { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
}

public class ExportEmailProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    /// <summary>true si hay password almacenada (la propia password nunca se devuelve).</summary>
    public bool HasPassword { get; set; }
    public bool UseSsl { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string? DefaultRecipients { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class ExportEmailTestRequest
{
    [Required, EmailAddress] public string To { get; set; } = string.Empty;
}

public class ExportEmailTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
