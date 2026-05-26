// ============================================================================
// ExportConfig.cs — DTO deserializado desde ExportTask.ConfigJson
// ============================================================================
// Estructura usada por IExportRunner (LocalFileRunner / EmailRunner) para
// ejecutar el envío al destino correspondiente.
// ============================================================================

namespace SW.PC.API.Backend.Models.Export;

public class ExportConfig
{
    /// <summary>
    /// Plantilla del nombre de archivo (puede incluir tokens dinámicos
    /// como {fecha}, {hora}, {ciclo}, {plc}, {linea}, {turno}, {producto}).
    /// Los tokens se resuelven en ExportService antes de pasar el filename
    /// al IExportRunner.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// Carpeta local o UNC donde guardar (solo si destinos incluye "local").
    /// Debe estar dentro de AllowedExportFolders del proyecto activo.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>Configuración de email (solo si destinos incluye "email").</summary>
    public ExportEmailConfig? Email { get; set; }
}

public class ExportEmailConfig
{
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Cco { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
