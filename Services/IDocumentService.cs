// ============================================================================
// IDocumentService.cs - Interface del Servicio Documental (DMS Simplificado)
// ============================================================================
// Solo lectura + sincronización desde DMS Enterprise.
// Los documentos (PDF) llegan ya generados desde el servidor empresa.
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

public interface IDocumentService
{
    // === Lectura ===

    /// <summary>Obtener lista de documentos con filtros</summary>
    Task<(List<DocumentInfo> Items, int TotalCount)> GetDocumentsAsync(DocumentFilter filter, string userRole);

    /// <summary>Obtener detalle de un documento (metadatos)</summary>
    Task<DocumentDetail?> GetDocumentByIdAsync(string id, string userRole);

    /// <summary>Obtener detalle por slug</summary>
    Task<DocumentDetail?> GetDocumentBySlugAsync(string slug, string userRole);

    // === Árbol de navegación ===

    /// <summary>Obtener árbol de documentos para navegación</summary>
    Task<List<DocumentTreeNode>> GetDocumentTreeAsync(string userRole);

    // === Estadísticas ===

    /// <summary>Obtener estadísticas del DMS</summary>
    Task<DocumentStats> GetStatsAsync();

    /// <summary>Obtener estado de cumplimiento CRA documental</summary>
    Task<CraDocumentStatus> GetCraStatusAsync();

    // === Sincronización ===

    /// <summary>Escanear docs/ del filesystem y sincronizar con DB</summary>
    Task<DocumentOperationResponse> SyncFromFilesystemAsync(string userName);

    /// <summary>Sincronizar solo AQSdocs_master</summary>
    Task<DocumentOperationResponse> SyncMasterAsync(string userName);

    /// <summary>Sincronizar solo AQSdocs_project</summary>
    Task<DocumentOperationResponse> SyncProjectAsync(string userName);

    /// <summary>Procesar notificación push del DMS Enterprise</summary>
    Task<DocumentOperationResponse> ProcessDmsNotifyAsync(DmsPublishNotifyRequest request, string userName);

    // === Descarga ===

    /// <summary>Obtener el stream de un fichero para descarga (solo formato original)</summary>
    Task<(Stream? FileStream, string? ContentType, string? FileName)?> DownloadFileAsync(string documentId, string userRole);

    // === Categorías (solo lectura) ===

    /// <summary>Obtener todas las categorías de documentos</summary>
    Task<List<DocumentCategoryConfig>> GetCategoriesAsync();
}
