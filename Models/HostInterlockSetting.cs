// ============================================================================
// HostInterlockSetting.cs — HostName Interlocking (control exclusivo por equipo)
// ============================================================================
// Configuración por NOMBRE DE EQUIPO (CN del certificado mTLS, nombre del
// dispositivo por token o nombre del equipo local/kiosco). Se edita desde
// Usuarios → Equipos (Administrator/SuperAdmin).
//   - Interlocking:         mientras el equipo está conectado, ningún otro
//                           equipo puede iniciar sesión; al iniciar sesión
//                           expulsa a los demás.
//   - OverrideInterlocking: puede forzar la toma de control aunque haya un
//                           equipo con interlocking conectado. Exige Interlocking.
// Solo tiene efecto con MtlsRequireRegisteredMachine=TRUE (identidad verificada).
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models;

public class HostInterlockSetting
{
    public int Id { get; set; }

    /// <summary>Nombre del equipo (se compara sin distinguir mayúsculas).</summary>
    [Required, MaxLength(100)]
    public string HostName { get; set; } = string.Empty;

    public bool Interlocking { get; set; } = false;

    public bool OverrideInterlocking { get; set; } = false;

    [MaxLength(100)]
    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>Body de PUT /api/host-interlock.</summary>
public class HostInterlockUpdateRequest
{
    public string HostName { get; set; } = string.Empty;
    public bool Interlocking { get; set; }
    public bool OverrideInterlocking { get; set; }
}
