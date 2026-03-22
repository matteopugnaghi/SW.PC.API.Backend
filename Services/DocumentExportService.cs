// ============================================================================
// DocumentExportService.cs - Servicio de exportacion documental (SIMPLIFICADO)
// ============================================================================
// Sistema simplificado: solo sirve PDFs ya generados desde DMS Enterprise.
// Ya no se generan PDF/DOCX - QuestPDF y OpenXml para docs eliminados.
// Se mantiene la interfaz minima por compatibilidad con DI.
// ============================================================================

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interfaz minima de exportacion documental.
/// El sistema DMS ahora solo sirve PDFs pregenerados - no se necesita conversion.
/// </summary>
public interface IDocumentExportService
{
}

/// <summary>
/// Implementacion stub - no se necesitan conversiones.
/// Los documentos llegan ya en PDF desde DMS Enterprise.
/// </summary>
public class DocumentExportService : IDocumentExportService
{
    public DocumentExportService(ILogger<DocumentExportService> logger)
    {
    }
}
