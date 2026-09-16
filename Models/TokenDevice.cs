// ============================================================================
// TokenDevice.cs — Dispositivos identificados por token de URL (sin certificado)
// ============================================================================
// Sistema hermano de mTLS para hardware SIN almacén de certificados (paneles
// HMI Weintek cMT, tablets, visores). El admin genera el token en Usuarios →
// Equipos y se configura UNA vez en la URL de inicio del dispositivo
// (?deviceKey=...). Solo se persiste el HASH (SHA256); el token en claro se
// muestra una única vez. Compensating countermeasure IEC 62443 SR 1.2 —
// identifica el DISPOSITIVO, nunca sustituye el login de usuario.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models;

public class TokenDevice
{
    public int Id { get; set; }

    /// <summary>SHA256 hex del token en claro (el token NUNCA se almacena).</summary>
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Nombre lógico del dispositivo (p.ej. HMI-CABINA-1). Equivale al CN de un cert mTLS.</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Usuario que registró el dispositivo.</summary>
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Último uso del token (actualización throttled, no por petición).</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Revocado: el token deja de valer al instante; la fila se conserva como histórico.</summary>
    public bool Revoked { get; set; } = false;
}

/// <summary>Body de POST /api/certificate/token-devices.</summary>
public class TokenDeviceCreateRequest
{
    public string Name { get; set; } = string.Empty;
}
