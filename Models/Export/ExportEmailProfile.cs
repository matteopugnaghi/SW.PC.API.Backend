// ============================================================================
// ExportEmailProfile.cs — Perfil SMTP reutilizable (gestionado desde la UI)
// ============================================================================
// La contraseña se almacena cifrada con ASP.NET Core Data Protection
// (DPAPI bajo Windows). Nunca se devuelve al frontend.
// CRA: secret-at-rest cifrado por el OS, auditoría obligatoria en cambios.
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models.Export;

[Table("ExportEmailProfiles")]
public class ExportEmailProfile
{
    /// <summary>GUID en formato N (32 hex chars).</summary>
    [Key, MaxLength(40)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nombre legible (ej. "Gmail Reports").</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    [MaxLength(200)]
    public string? Username { get; set; }

    /// <summary>
    /// Contraseña cifrada con IDataProtector (purpose "Aquafrisch.Export.SmtpPassword.v1").
    /// Si está vacío/null, se asume autenticación sin password (servidor SMTP abierto).
    /// </summary>
    public string? PasswordEncrypted { get; set; }

    public bool UseSsl { get; set; } = true;

    [Required, MaxLength(200)]
    public string FromAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FromName { get; set; }

    /// <summary>Destinatarios por defecto (CSV) — opcional.</summary>
    [MaxLength(1000)]
    public string? DefaultRecipients { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}
