// ============================================================================
// ExportSmtpSettings.cs — Configuración SMTP cargada desde Excel (Commit 6)
// ============================================================================
// Hoy se rellena desde appsettings.json o se inyecta como null si el cliente
// aún no ha configurado SMTP. En el Commit 6 ExcelConfigService leerá los
// campos SMTP:* del SystemConfig y poblará este DTO.
// ============================================================================

namespace SW.PC.API.Backend.Models.Export;

public class ExportSmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
