// ============================================================================
// ExportFolderProfile.cs — Perfil de carpeta destino reutilizable
// ============================================================================
// Configurable desde la UI (sin Excel, sin whitelist). Cada ExportTask puede
// referenciar un perfil por Id, lo que permite cambiar la ruta en un único
// punto sin tocar las tareas.
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models.Export;

[Table("ExportFolderProfiles")]
public class ExportFolderProfile
{
    /// <summary>GUID en formato N (32 hex chars).</summary>
    [Key, MaxLength(40)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nombre legible (ej. "NAS Producción").</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Ruta base local o UNC (ej. C:\Exports o \\NAS\Reports).</summary>
    [Required, MaxLength(500)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Subcarpeta opcional dentro de Path (acepta tokens {fecha}, {source}).</summary>
    [MaxLength(200)]
    public string? Subfolder { get; set; }

    /// <summary>Descripción opcional.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}
