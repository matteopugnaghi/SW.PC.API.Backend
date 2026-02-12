// ============================================================================
// IDocumentService.cs - Interface del Servicio de Gestión Documental (DMS)
// ============================================================================
// CRUD completo + búsqueda + árbol + renderizado MD→HTML
// Compatible con sistema multi-proyecto
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

public interface IDocumentService
{
    // === CRUD ===
    
    /// <summary>Obtener lista de documentos con filtros</summary>
    Task<(List<DocumentInfo> Items, int TotalCount)> GetDocumentsAsync(DocumentFilter filter, string userRole);
    
    /// <summary>Obtener detalle completo de un documento (incluye contenido MD + HTML)</summary>
    Task<DocumentDetail?> GetDocumentByIdAsync(string id, string userRole);
    
    /// <summary>Obtener detalle por slug</summary>
    Task<DocumentDetail?> GetDocumentBySlugAsync(string slug, string userRole);
    
    /// <summary>Crear un nuevo documento (crea fichero .md + registro en DB)</summary>
    Task<DocumentOperationResponse> CreateDocumentAsync(CreateDocumentRequest request, string userName, string userRole);
    
    /// <summary>Actualizar un documento existente</summary>
    Task<DocumentOperationResponse> UpdateDocumentAsync(string id, UpdateDocumentRequest request, string userName, string userRole);
    
    /// <summary>Eliminar un documento (archivo + registro en DB)</summary>
    Task<DocumentOperationResponse> DeleteDocumentAsync(string id, string userName, string userRole);
    
    // === Árbol de navegación ===
    
    /// <summary>Obtener árbol de documentos para navegación</summary>
    Task<List<DocumentTreeNode>> GetDocumentTreeAsync(string userRole);
    
    // === Estadísticas ===
    
    /// <summary>Obtener estadísticas del DMS</summary>
    Task<DocumentStats> GetStatsAsync();
    
    /// <summary>Obtener estado de cumplimiento CRA documental</summary>
    Task<CraDocumentStatus> GetCraStatusAsync();
    
    // === Contenido ===
    
    /// <summary>Obtener contenido Markdown raw de un documento</summary>
    Task<string?> GetRawContentAsync(string id, string userRole);
    
    /// <summary>Renderizar Markdown a HTML</summary>
    string RenderMarkdownToHtml(string markdown);
    
    // === Sincronización ===
    
    /// <summary>Escanear docs/ del filesystem y sincronizar con DB</summary>
    Task<DocumentOperationResponse> SyncFromFilesystemAsync(string userName);
    
    /// <summary>Sincronizar solo AQSdocs_master (copia docs del código fuente al proyecto)</summary>
    Task<DocumentOperationResponse> SyncMasterAsync(string userName);
    
    /// <summary>Sincronizar solo AQSdocs_project (escanea carpetas, auto-crea categorías, registra docs)</summary>
    Task<DocumentOperationResponse> SyncProjectAsync(string userName);
    
    // === Historial ===
    
    /// <summary>Obtener historial de cambios de un documento</summary>
    Task<List<DocumentHistory>> GetDocumentHistoryAsync(string documentId);

    // === Categorías dinámicas ===
    
    /// <summary>Obtener todas las categorías de documentos</summary>
    Task<List<DocumentCategoryConfig>> GetCategoriesAsync();
    
    /// <summary>Obtener una categoría por ID</summary>
    Task<DocumentCategoryConfig?> GetCategoryByIdAsync(int id);
    
    /// <summary>Crear una nueva categoría personalizada</summary>
    Task<DocumentCategoryConfig> CreateCategoryAsync(DocumentCategoryConfig category, string userName);
    
    /// <summary>Actualizar una categoría existente</summary>
    Task<DocumentCategoryConfig?> UpdateCategoryAsync(int id, DocumentCategoryConfig category, string userName);
    
    /// <summary>Eliminar una categoría personalizada (no del sistema)</summary>
    Task<bool> DeleteCategoryAsync(int id);
}
