// ============================================================================
// DmsEnterpriseModels.cs - Modelos para integración con DMS Enterprise
// ============================================================================
// Contrato: _dms_tree.json que el DMS Enterprise escribe en cada carpeta
// Mapeo de roles y estados español ↔ inglés
// ============================================================================

using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models;

#region _dms_tree.json Models

/// <summary>
/// Estructura raíz del fichero _dms_tree.json que el DMS Enterprise
/// escribe junto a los documentos publicados en cada carpeta.
/// </summary>
public class DmsTree
{
    /// <summary>Metadatos de la categoría</summary>
    [JsonPropertyName("category")]
    public DmsTreeCategory Category { get; set; } = new();

    /// <summary>Metadatos de la subcategoría (null si no tiene)</summary>
    [JsonPropertyName("subcategory")]
    public DmsTreeSubcategory? Subcategory { get; set; }

    /// <summary>Array de documentos publicados en ESTA carpeta</summary>
    [JsonPropertyName("documents")]
    public List<DmsTreeDocument> Documents { get; set; } = new();

    /// <summary>Timestamp de última actualización del manifiesto</summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Metadatos de categoría dentro de _dms_tree.json
/// </summary>
public class DmsTreeCategory
{
    /// <summary>Código de categoría: "00"-"10" o "INTERNO"</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>Nombre display de la categoría</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Emoji icono</summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "📄";
}

/// <summary>
/// Metadatos de subcategoría dentro de _dms_tree.json (solo metadato, no genera carpeta)
/// </summary>
public class DmsTreeSubcategory
{
    /// <summary>Código subcategoría (XX.Y)</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>Nombre de la subcategoría</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>
/// Un documento individual dentro del array documents de _dms_tree.json
/// </summary>
public class DmsTreeDocument
{
    /// <summary>DocumentCode completo (XX.Y-ZZ)</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>Título del documento</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>Versión actual</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>Nombre exacto del archivo copiado</summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    /// <summary>Status: Borrador|Revisión|Aprobado|Archivado|Obsoleto</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Borrador";

    /// <summary>Timestamp ISO 8601 UTC de publicación</summary>
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    /// <summary>Autor del documento</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    /// <summary>Rol mínimo requerido (en español): Visualizador|Operador|Auditor|Mantenimiento|Administrador|SuperAdmin</summary>
    [JsonPropertyName("minimumRole")]
    public string MinimumRole { get; set; } = "";
}

#endregion

#region DMS Publish Notify Request

/// <summary>
/// Payload que el DMS Enterprise envía al POST /api/documents/sync
/// cuando publica un documento individual (upsert directo sin escaneo de carpetas).
/// </summary>
public class DmsPublishNotifyRequest
{
    /// <summary>Siempre "DMS_Enterprise" — identifica el origen</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "DMS_Enterprise";

    /// <summary>Código del documento (XX.Y-ZZ)</summary>
    [JsonPropertyName("documentCode")]
    public string DocumentCode { get; set; } = "";

    /// <summary>Título del documento</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>Versión del documento</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>Nombre exacto del archivo publicado</summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    /// <summary>"AQSdocs_master" o "AQSdocs_project"</summary>
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = "";

    /// <summary>Código del proyecto (solo para AQSdocs_project)</summary>
    [JsonPropertyName("projectCode")]
    public string? ProjectCode { get; set; }

    /// <summary>Nombre del proyecto (solo para AQSdocs_project)</summary>
    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    /// <summary>Código de categoría (ej: "02")</summary>
    [JsonPropertyName("categoryCode")]
    public string CategoryCode { get; set; } = "";

    /// <summary>Nombre de categoría (ej: "SEGURIDAD")</summary>
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = "";

    /// <summary>Código de subcategoría (ej: "02.5") — puede ser null</summary>
    [JsonPropertyName("subcategoryCode")]
    public string? SubcategoryCode { get; set; }

    /// <summary>Nombre de subcategoría (ej: "Asset/Risk Management")</summary>
    [JsonPropertyName("subcategoryName")]
    public string? SubcategoryName { get; set; }

    /// <summary>Timestamp de publicación (ISO 8601 UTC)</summary>
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    /// <summary>Status en español: Borrador|Revisión|Aprobado|Archivado|Obsoleto</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Borrador";

    /// <summary>Rol mínimo en español: Visualizador|Operador|Auditor|Mantenimiento|Administrador|SuperAdmin</summary>
    [JsonPropertyName("minimumRole")]
    public string MinimumRole { get; set; } = "";

    /// <summary>Autor del documento</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }
}

#endregion

#region Mapeo DMS Enterprise → Supervisor

/// <summary>
/// Mapeos estáticos entre los valores en español del DMS Enterprise
/// y los enums/valores internos del Supervisor.
/// </summary>
public static class DmsEnterpriseMappings
{
    /// <summary>
    /// Mapeo de roles: español (DMS Enterprise) → inglés (Supervisor)
    /// </summary>
    public static readonly Dictionary<string, string> RoleSpanishToEnglish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Visualizador"] = "Viewer",
        ["Operador"] = "Operator",
        ["Auditor"] = "Auditor",
        ["Mantenimiento"] = "Maintenance",
        ["Administrador"] = "Administrator",
        ["SuperAdmin"] = "SuperAdmin",
    };

    /// <summary>
    /// Mapeo de roles: inglés (Supervisor) → español (DMS Enterprise)
    /// </summary>
    public static readonly Dictionary<string, string> RoleEnglishToSpanish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Viewer"] = "Visualizador",
        ["Operator"] = "Operador",
        ["Auditor"] = "Auditor",
        ["Maintenance"] = "Mantenimiento",
        ["Administrator"] = "Administrador",
        ["SuperAdmin"] = "SuperAdmin",
    };

    /// <summary>
    /// Mapeo de status: español (DMS Enterprise) → enum DocumentStatus
    /// </summary>
    public static readonly Dictionary<string, DocumentStatus> StatusSpanishToEnum = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Borrador"] = DocumentStatus.Draft,
        ["Revisión"] = DocumentStatus.Review,
        ["Aprobado"] = DocumentStatus.Approved,
        ["Archivado"] = DocumentStatus.Archived,
        ["Obsoleto"] = DocumentStatus.Obsolete,
    };

    /// <summary>
    /// Mapeo de status: enum DocumentStatus → español (DMS Enterprise)
    /// </summary>
    public static readonly Dictionary<DocumentStatus, string> StatusEnumToSpanish = new()
    {
        [DocumentStatus.Draft] = "Borrador",
        [DocumentStatus.Review] = "Revisión",
        [DocumentStatus.Approved] = "Aprobado",
        [DocumentStatus.Archived] = "Archivado",
        [DocumentStatus.Obsolete] = "Obsoleto",
    };

    /// <summary>
    /// Convierte un rol en español del DMS a rol inglés del Supervisor.
    /// Si no se reconoce, devuelve el default proporcionado.
    /// </summary>
    public static string MapRole(string? spanishRole, string defaultRole = "Viewer")
    {
        if (string.IsNullOrWhiteSpace(spanishRole)) return defaultRole;
        return RoleSpanishToEnglish.TryGetValue(spanishRole, out var english) ? english : defaultRole;
    }

    /// <summary>
    /// Convierte un status en español del DMS a enum DocumentStatus.
    /// Si no se reconoce, devuelve Draft.
    /// </summary>
    public static DocumentStatus MapStatus(string? spanishStatus)
    {
        if (string.IsNullOrWhiteSpace(spanishStatus)) return DocumentStatus.Draft;
        return StatusSpanishToEnum.TryGetValue(spanishStatus, out var status) ? status : DocumentStatus.Draft;
    }
}

#endregion
