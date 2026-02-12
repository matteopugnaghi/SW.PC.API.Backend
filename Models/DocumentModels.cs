// ============================================================================
// DocumentModels.cs - Modelos del Sistema de Gestión Documental (DMS)
// ============================================================================
// Documentación por proyecto + documentación del SW
// Compatible con EU CRA - Trazabilidad documental completa
// Diseñado para escalabilidad a DMS Empresarial (Fase 2)
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models;

#region Enums

/// <summary>
/// Ámbito del documento — Para separar docs del SW vs docs de proyecto
/// Preparado para Fase 2 (DMS Empresarial)
/// </summary>
public enum DocumentScope
{
    /// <summary>Documentación del SW (docs/ del repo) — solo visible en desarrollo/técnicos</summary>
    Software,
    
    /// <summary>Documentación del proyecto (Projects/{id}/docs/) — va a la máquina</summary>
    Project
    
    // Fase 2 (DMS Empresarial — otro software dedicado):
    // Enterprise,   // Docs de empresa global
    // Machine,      // Docs de un modelo de máquina
    // Public        // Docs para web pública
}

/// <summary>
/// Categorías de documentos — Valores legacy del enum original.
/// Las categorías ahora son dinámicas (tabla DocumentCategories en DB).
/// Estos valores se mantienen como IDs predefinidos del sistema.
/// </summary>
public static class SystemDocumentCategories
{
    public const int Compliance = 0;
    public const int CraGeneric = 1;
    public const int UserGuide = 2;
    public const int Technical = 3;
    public const int Electrical = 4;
    public const int Maintenance = 5;
    public const int Internal = 6;
    public const int Other = 7;
}

/// <summary>
/// Configuración de una categoría de documentos (almacenada en DB).
/// Las categorías 0-7 son del sistema (IsSystem=true, no se pueden eliminar).
/// Las categorías 8+ son personalizadas (creadas por SuperAdmin).
/// </summary>
public class DocumentCategoryConfig
{
    public int Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";
    
    [MaxLength(10)]
    public string Icon { get; set; } = "📄";
    
    [MaxLength(20)]
    public string Color { get; set; } = "#888888";
    
    /// <summary>Nombre de subcarpeta en docs/ (ej: "compliance", "technical")</summary>
    [MaxLength(100)]
    public string FolderName { get; set; } = "";
    
    /// <summary>Orden de presentación en listas</summary>
    public int SortOrder { get; set; }
    
    /// <summary>True = categoría del sistema (no se puede eliminar, solo renombrar icono/color)</summary>
    public bool IsSystem { get; set; }
    
    /// <summary>ID de categoría padre (null = categoría raíz). Permite jerarquía.</summary>
    public int? ParentId { get; set; }
    
    /// <summary>Descripción breve de la categoría</summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Nivel de acceso mínimo para un documento
/// Se combina con el sistema RBAC existente (ModulePermissions.DocumentsView)
/// El acceso efectivo = canView('DocumentsView') AND userRole >= MinimumRole
/// </summary>
public enum DocumentAccessLevel
{
    /// <summary>Cualquier usuario autenticado con acceso a DocumentsView</summary>
    Public,
    
    /// <summary>Operador y roles superiores</summary>
    Operator,
    
    /// <summary>Mantenimiento y roles superiores</summary>
    Maintenance,
    
    /// <summary>Solo Admin/SuperAdmin</summary>
    Admin,
    
    /// <summary>Solo SuperAdmin (documentación interna Aquafrisch)</summary>
    Internal
}

/// <summary>
/// Estado del documento en su ciclo de vida
/// </summary>
public enum DocumentStatus
{
    /// <summary>Borrador en edición</summary>
    Draft,
    
    /// <summary>En revisión (pendiente de aprobación)</summary>
    Review,
    
    /// <summary>Aprobado para uso (nueva versión para cambios)</summary>
    Approved,
    
    /// <summary>Archivado (histórico, no eliminado)</summary>
    Archived,
    
    /// <summary>Obsoleto (reemplazado por versión más nueva)</summary>
    Obsolete
}

/// <summary>
/// Tipo de fichero del documento
/// </summary>
public enum DocumentFileType
{
    Markdown,   // .md - Fuente de verdad, versionable
    Pdf,        // .pdf - Adjunto binario (esquemas, etc.)
    Docx,       // .docx - Importado/exportado
    Image,      // .png, .jpg, .svg - Adjunto
    Json,       // .json - SBOM, configs
    Other       // Otros
}

#endregion

#region Entidades principales

/// <summary>
/// Documento gestionado por el DMS
/// Mapea a tabla 'Documents' en SQLite
/// </summary>
public class Document
{
    // === Identificación ===
    
    /// <summary>Identificador único (GUID)</summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Slug URL-friendly para rutas amigables</summary>
    [Required, MaxLength(200)]
    public string Slug { get; set; } = "";
    
    // === Contenido ===
    
    /// <summary>Título del documento</summary>
    [Required, MaxLength(500)]
    public string Title { get; set; } = "";
    
    /// <summary>Descripción/resumen breve</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    /// <summary>Ruta relativa al fichero dentro de docs/ (ej: "user-guides/manual-operador.md")</summary>
    [Required, MaxLength(500)]
    public string FilePath { get; set; } = "";
    
    /// <summary>Tipo de fichero</summary>
    public DocumentFileType FileType { get; set; } = DocumentFileType.Markdown;
    
    /// <summary>SHA256 del contenido actual — para detectar cambios e integridad</summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }
    
    /// <summary>Tamaño del fichero en bytes</summary>
    public long FileSize { get; set; }
    
    // === Categorización ===
    
    /// <summary>Ámbito: Software (docs/ del repo) o Project (Projects/{id}/docs/)</summary>
    public DocumentScope Scope { get; set; } = DocumentScope.Project;
    
    /// <summary>Categoría principal del documento (ID de DocumentCategoryConfig)</summary>
    public int Category { get; set; } = SystemDocumentCategories.Other;
    
    /// <summary>Sub-categoría libre (opcional)</summary>
    [MaxLength(100)]
    public string? SubCategory { get; set; }
    
    /// <summary>Tags en formato JSON array: ["cra","seguridad","plc"]</summary>
    [MaxLength(1000)]
    public string? Tags { get; set; }
    
    // === Control de acceso ===
    
    /// <summary>Nivel de acceso mínimo requerido</summary>
    public DocumentAccessLevel AccessLevel { get; set; } = DocumentAccessLevel.Public;
    
    /// <summary>Rol mínimo del sistema requerido (complementa AccessLevel)</summary>
    [MaxLength(50)]
    public string MinimumRole { get; set; } = "Viewer";
    
    // === Versionado ===
    
    /// <summary>Versión semántica del documento (controlada por el autor)</summary>
    [Required, MaxLength(20)]
    public string Version { get; set; } = "1.0";
    
    /// <summary>Estado en el ciclo de vida</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    
    // === CRA / Compliance ===
    
    /// <summary>¿Es relevante para cumplimiento CRA?</summary>
    public bool CraRelevant { get; set; }
    
    /// <summary>Artículo CRA aplicable (ej: "Art. 13.2", "Anexo VII")</summary>
    [MaxLength(100)]
    public string? CraArticle { get; set; }
    
    /// <summary>Plazo CRA para tener este documento listo</summary>
    public DateTime? CraDeadline { get; set; }
    
    /// <summary>Usuario que aprobó el documento</summary>
    [MaxLength(100)]
    public string? ApprovedBy { get; set; }
    
    /// <summary>Fecha de aprobación</summary>
    public DateTime? ApprovedAt { get; set; }
    
    // === Auditoría ===
    
    /// <summary>Usuario que creó el documento</summary>
    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = "System";
    
    /// <summary>Fecha de creación</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Último usuario que modificó el documento</summary>
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
    
    /// <summary>Fecha de última modificación</summary>
    public DateTime? UpdatedAt { get; set; }
    
    // === Relaciones ===
    
    /// <summary>ID del documento padre (para jerarquía/secciones)</summary>
    [MaxLength(36)]
    public string? ParentDocId { get; set; }
    
    /// <summary>IDs de documentos relacionados en JSON array</summary>
    [MaxLength(1000)]
    public string? RelatedDocIds { get; set; }
    
    // === Búsqueda ===
    
    /// <summary>Contenido indexable en texto plano (para FTS)</summary>
    public string? SearchContent { get; set; }
}

/// <summary>
/// Historial de cambios de un documento
/// Mapea a tabla 'DocumentHistory' en SQLite
/// </summary>
public class DocumentHistory
{
    /// <summary>Identificador único</summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>ID del documento al que pertenece</summary>
    [Required]
    public string DocumentId { get; set; } = "";
    
    /// <summary>Versión del documento en este punto</summary>
    [Required, MaxLength(20)]
    public string Version { get; set; } = "";
    
    /// <summary>Acción realizada</summary>
    [Required, MaxLength(50)]
    public string Action { get; set; } = "";  // created, edited, approved, exported, imported, archived
    
    /// <summary>Usuario que realizó el cambio</summary>
    [Required, MaxLength(100)]
    public string ChangedBy { get; set; } = "";
    
    /// <summary>Fecha del cambio</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Hash del commit Git (si se commiteó)</summary>
    [MaxLength(40)]
    public string? CommitHash { get; set; }
    
    /// <summary>SHA256 del contenido en ese momento</summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }
    
    /// <summary>Nota/descripción del cambio</summary>
    [MaxLength(1000)]
    public string? ChangeNote { get; set; }
}

#endregion

#region DTOs - Request/Response

/// <summary>
/// Request para crear un nuevo documento
/// </summary>
public class CreateDocumentRequest
{
    [Required, MaxLength(500)]
    public string Title { get; set; } = "";
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [Required]
    public int Category { get; set; }
    
    public DocumentScope Scope { get; set; } = DocumentScope.Project;
    
    public DocumentAccessLevel AccessLevel { get; set; } = DocumentAccessLevel.Public;
    
    /// <summary>Contenido Markdown inicial (opcional)</summary>
    public string? Content { get; set; }
    
    /// <summary>Tags como array de strings</summary>
    public List<string>? Tags { get; set; }
    
    /// <summary>¿Es relevante para CRA?</summary>
    public bool CraRelevant { get; set; }
    
    /// <summary>Artículo CRA aplicable</summary>
    [MaxLength(100)]
    public string? CraArticle { get; set; }
    
    /// <summary>Nombre del fichero (opcional — si no se da, se genera del título)</summary>
    [MaxLength(200)]
    public string? FileName { get; set; }
}

/// <summary>
/// Request para actualizar un documento existente
/// </summary>
public class UpdateDocumentRequest
{
    [MaxLength(500)]
    public string? Title { get; set; }
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    /// <summary>Nuevo contenido Markdown</summary>
    public string? Content { get; set; }
    
    /// <summary>Nota del cambio (para historial)</summary>
    [MaxLength(1000)]
    public string? ChangeNote { get; set; }
    
    /// <summary>Nueva versión (si el autor quiere incrementarla)</summary>
    [MaxLength(20)]
    public string? Version { get; set; }
    
    public DocumentAccessLevel? AccessLevel { get; set; }
    
    public DocumentStatus? Status { get; set; }
    
    public List<string>? Tags { get; set; }
    
    public bool? CraRelevant { get; set; }
    
    [MaxLength(100)]
    public string? CraArticle { get; set; }
}

/// <summary>
/// Filtros para listar documentos
/// </summary>
public class DocumentFilter
{
    public DocumentScope? Scope { get; set; }
    public int? Category { get; set; }
    /// <summary>
    /// Filtrar por nombre de carpeta real (para docs master agrupados por carpeta del filesystem).
    /// Ej: "architecture", "changelog", "compliance"
    /// </summary>
    public string? FolderName { get; set; }
    public DocumentStatus? Status { get; set; }
    public DocumentAccessLevel? MaxAccessLevel { get; set; }
    public bool? CraRelevant { get; set; }
    public string? SearchQuery { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Información básica de un documento (para listados)
/// </summary>
public class DocumentInfo
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string FilePath { get; set; } = "";
    public DocumentFileType FileType { get; set; }
    public DocumentScope Scope { get; set; }
    public int Category { get; set; }
    public string? SubCategory { get; set; }
    public List<string> Tags { get; set; } = new();
    public DocumentAccessLevel AccessLevel { get; set; }
    public string Version { get; set; } = "";
    public DocumentStatus Status { get; set; }
    public bool CraRelevant { get; set; }
    public string? CraArticle { get; set; }
    public DateTime? CraDeadline { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long FileSize { get; set; }
}

/// <summary>
/// Detalle completo de un documento (incluye contenido)
/// </summary>
public class DocumentDetail : DocumentInfo
{
    /// <summary>Contenido Markdown renderizado como HTML</summary>
    public string? HtmlContent { get; set; }
    
    /// <summary>Contenido Markdown raw</summary>
    public string? RawContent { get; set; }
    
    /// <summary>Hash de integridad SHA256</summary>
    public string? ContentHash { get; set; }
    
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ParentDocId { get; set; }
    public List<string>? RelatedDocIds { get; set; }
}

/// <summary>
/// Nodo del árbol de documentos (para navegación)
/// </summary>
public class DocumentTreeNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public string Type { get; set; } = "folder";  // "folder" | "document"
    public int? Category { get; set; }
    public DocumentScope? Scope { get; set; }
    public DocumentAccessLevel? AccessLevel { get; set; }
    public string? DocumentId { get; set; }
    public DocumentStatus? Status { get; set; }
    public bool CraRelevant { get; set; }
    public int DocumentCount { get; set; }
    public List<DocumentTreeNode> Children { get; set; } = new();
}

/// <summary>
/// Estadísticas del sistema documental
/// </summary>
public class DocumentStats
{
    public int TotalDocuments { get; set; }
    public int TotalByScope_Software { get; set; }
    public int TotalByScope_Project { get; set; }
    public Dictionary<string, int> ByCategory { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public int CraRelevantTotal { get; set; }
    public int CraRelevantApproved { get; set; }
    public int CraRelevantPending { get; set; }
    public double CraCompliancePercent { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Estado de cumplimiento CRA documental
/// </summary>
public class CraDocumentStatus
{
    public double CompliancePercent { get; set; }
    public List<CraDocumentItem> RequiredDocuments { get; set; } = new();
    public int TotalRequired { get; set; }
    public int TotalApproved { get; set; }
    public int TotalDraft { get; set; }
    public int TotalPending { get; set; }
    public DateTime? NextDeadline { get; set; }
    public string? NextDeadlineDocument { get; set; }
}

/// <summary>
/// Un documento requerido por CRA con su estado
/// </summary>
public class CraDocumentItem
{
    public string? DocumentId { get; set; }
    public string Title { get; set; } = "";
    public string CraArticle { get; set; } = "";
    public DocumentStatus Status { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Version { get; set; }
    public bool Exists { get; set; }
}

/// <summary>
/// Respuesta genérica de operación documental
/// </summary>
public class DocumentOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public DocumentInfo? Document { get; set; }
}

#endregion
