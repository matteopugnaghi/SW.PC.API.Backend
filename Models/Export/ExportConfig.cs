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

    /// <summary>
    /// Estrategia cuando el archivo destino ya existe (solo destino "local"):
    ///   - "overwrite" (default): sobreescribe el archivo existente.
    ///   - "rename": añade sufijo numérico incremental (_001, _002, …) hasta encontrar un nombre libre.
    ///   - "skip": no escribe nada y devuelve Success=true con mensaje informativo.
    /// </summary>
    public string? OnFileExists { get; set; } = "overwrite";

    /// <summary>
    /// Diseño del informe (cabecera, logo, resumen, pie). Solo aplica a
    /// formatos enriquecidos: <c>xlsx</c> y <c>html</c>. Si es null el
    /// formatter usa el layout básico (sin marca/empresa/resumen).
    /// </summary>
    public ReportDesignConfig? Report { get; set; }
}

public class ExportEmailConfig
{
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Cco { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

// ============================================================================
// ReportDesignConfig — Personalización del informe (UI-driven, por tarea)
// ============================================================================
// Todos los campos son OPCIONALES. El frontend (ExportManagerWizard Step 3)
// rellena lo que el usuario quiera mostrar. El logo se guarda como base64
// dentro de esta config (sin tocar el disco). Aplica a XLSX y HTML.
// ============================================================================

public class ReportDesignConfig
{
    // ─── Cabecera ───
    public bool IncludeHeader { get; set; } = true;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? CompanyName { get; set; }
    /// <summary>Logo en base64. Acepta data-URI ("data:image/png;base64,...") o el base64 desnudo.</summary>
    public string? LogoBase64 { get; set; }
    public bool ShowDate { get; set; } = true;
    public bool ShowProject { get; set; } = false;

    // ─── Filtros aplicados ───
    public bool IncludeFilters { get; set; } = true;

    // ─── Resumen (totales) ───
    /// <summary>"off" | "auto" | "manual".</summary>
    public string SummaryMode { get; set; } = "off";
    /// <summary>Solo si SummaryMode="manual". IDs de columnas (por nombre) a resumir.</summary>
    public List<string> SummaryColumns { get; set; } = new();
    /// <summary>Subconjunto de: "sum", "avg", "min", "max", "count".</summary>
    public List<string> SummaryAggregations { get; set; } = new() { "sum", "avg" };

    // ─── Pie ───
    public bool IncludeFooter { get; set; } = false;
    public string? FooterText { get; set; }

    // ─── Estilo ───
    public string HeaderColor { get; set; } = "#17A2B8";
    public string AccentColor { get; set; } = "#0B5566";
}
