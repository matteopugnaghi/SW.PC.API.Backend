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
    
    /// <summary>Procesar notificación push del DMS Enterprise (upsert directo de un documento)</summary>
    Task<DocumentOperationResponse> ProcessDmsNotifyAsync(DmsPublishNotifyRequest request, string userName);
    
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

    // === Niveles de Clasificación (ISO 27001 A.8.2) ===

    /// <summary>Obtener todos los niveles de clasificación</summary>
    Task<List<DocumentClassificationLevel>> GetClassificationLevelsAsync();

    /// <summary>Crear un nuevo nivel de clasificación</summary>
    Task<DocumentClassificationLevel> CreateClassificationLevelAsync(DocumentClassificationLevel level, string userName);

    /// <summary>Actualizar un nivel de clasificación existente</summary>
    Task<DocumentClassificationLevel?> UpdateClassificationLevelAsync(int id, DocumentClassificationLevel level, string userName);

    /// <summary>Eliminar un nivel de clasificación (no del sistema)</summary>
    Task<bool> DeleteClassificationLevelAsync(int id);

    // === Matriz de Acceso: Categoría × Rol (ISO 27001 A.9.1) ===

    /// <summary>Obtener toda la matriz de acceso</summary>
    Task<List<DocumentCategoryAccess>> GetCategoryAccessMatrixAsync();

    /// <summary>Obtener accesos de una categoría específica</summary>
    Task<List<DocumentCategoryAccess>> GetCategoryAccessAsync(int categoryId);

    /// <summary>Actualizar acceso de un rol a una categoría</summary>
    Task<DocumentCategoryAccess> SetCategoryAccessAsync(int categoryId, string roleName, bool canRead, string userName);

    /// <summary>Actualizar toda la matriz de acceso de una categoría de golpe</summary>
    Task<List<DocumentCategoryAccess>> SetCategoryAccessBulkAsync(int categoryId, Dictionary<string, bool> roleAccess, string userName);

    /// <summary>Resetear la matriz de acceso a defaults ISO 27001 (menor privilegio)</summary>
    Task<int> ResetCategoryAccessToDefaultsAsync(string userName);

    // === Upload / Download de ficheros ===

    /// <summary>Subir un fichero (PDF, DOCX, imagen, etc.) al DMS</summary>
    Task<DocumentOperationResponse> UploadFileAsync(Stream fileStream, string fileName, long fileSize, int category, string? description, string? minimumRole, int? classificationId, string userName, string userRole);

    /// <summary>Obtener el stream de un fichero para descarga (exportFormat: null=original, "pdf", "docx")</summary>
    Task<(Stream? FileStream, string? ContentType, string? FileName)?> DownloadFileAsync(string documentId, string userRole, string? exportFormat = null);

    /// <summary>Previsualización de un documento Markdown como HTML con estilo PDF o Word</summary>
    Task<string?> PreviewAsFormatAsync(string documentId, string userRole, string format);

    /// <summary>Importar un fichero a un documento existente (DOCX→MD, o reemplazar fichero)</summary>
    Task<DocumentOperationResponse> ImportFileAsync(string documentId, Stream fileStream, string fileName, long fileSize, string userName, string userRole);
}
