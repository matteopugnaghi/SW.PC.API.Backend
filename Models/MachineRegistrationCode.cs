// ============================================================================
// MachineRegistrationCode.cs — Códigos de registro de equipo (mTLS enrollment)
// ============================================================================
// Código de un solo uso generado por Administrador/SuperAdmin desde la pantalla
// Usuarios. El script ClientSetup lo presenta junto con un CSR; el backend
// valida el código, firma el certificado de máquina (CN=%COMPUTERNAME%) con la
// Machine CA y quema el código. Solo se persiste el HASH (SHA256) del código.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models;

public class MachineRegistrationCode
{
    public int Id { get; set; }

    /// <summary>SHA256 hex del código en claro (el código NUNCA se almacena).</summary>
    [Required, MaxLength(64)]
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Usuario que generó el código.</summary>
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Caducidad (por defecto 24h desde la creación).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Momento de uso (null = pendiente). Un código usado no es reutilizable.</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>Fecha de caducidad del certificado emitido (por defecto +5 años desde el enrolamiento).</summary>
    public DateTime? CertExpiresAt { get; set; }

    /// <summary>Nombre del equipo registrado (CN del CSR), rellenado al usarse.</summary>
    [MaxLength(100)]
    public string? MachineName { get; set; }
}

/// <summary>Body de POST /api/certificate/enroll.</summary>
public class MachineEnrollRequest
{
    /// <summary>Código de registro de un solo uso (formato XXXX-XXXX-XXXX).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>CSR PKCS#10 en PEM o base64/DER (generado por certreq en el cliente).</summary>
    public string Csr { get; set; } = string.Empty;
}
