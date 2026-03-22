// ============================================================================
// DocumentsController.cs - API REST del Sistema Documental (Simplificado)
// ============================================================================
// Solo lectura + descarga de PDFs + sincronizacion desde DMS Enterprise.
// Los documentos llegan ya generados como PDF desde el servidor empresa.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly ILogger<DocumentsController> _logger;
    private readonly IDocumentService _documentService;

    public DocumentsController(
        ILogger<DocumentsController> logger,
        IDocumentService documentService)
    {
        _logger = logger;
        _documentService = documentService;
    }

    private string UserName => User.Identity?.Name ?? "anonymous";
    private string UserRole => User.Claims.FirstOrDefault(c => c.Type == "role")?.Value
                            ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? "Viewer";

    // ===================================================================
    // Lectura
    // ===================================================================

    /// <summary>
    /// Listar documentos con filtros opcionales
    /// GET /api/documents?scope=Project&amp;category=Technical&amp;page=1&amp;pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] DocumentFilter filter)
    {
        try
        {
            var (items, totalCount) = await _documentService.GetDocumentsAsync(filter, UserRole);
            return Ok(new
            {
                items,
                totalCount,
                page = filter.Page,
                pageSize = filter.PageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents");
            return StatusCode(500, new { error = "Error obteniendo documentos", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtener un documento por ID
    /// GET /api/documents/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocument(string id)
    {
        try
        {
            var doc = await _documentService.GetDocumentByIdAsync(id, UserRole);
            if (doc == null)
                return NotFound(new { message = "Documento no encontrado o sin acceso" });
            return Ok(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/{Id}", id);
            return StatusCode(500, new { error = "Error obteniendo documento", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtener un documento por slug
    /// GET /api/documents/by-slug/{slug}
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetDocumentBySlug(string slug)
    {
        try
        {
            var doc = await _documentService.GetDocumentBySlugAsync(slug, UserRole);
            if (doc == null)
                return NotFound(new { message = "Documento no encontrado" });
            return Ok(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/by-slug/{Slug}", slug);
            return StatusCode(500, new { error = "Error obteniendo documento", details = ex.Message });
        }
    }

    // ===================================================================
    // Arbol / Navegacion
    // ===================================================================

    /// <summary>
    /// Obtener arbol de documentos para navegacion lateral
    /// GET /api/documents/tree
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetDocumentTree()
    {
        try
        {
            var tree = await _documentService.GetDocumentTreeAsync(UserRole);
            return Ok(tree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/tree");
            return StatusCode(500, new { error = "Error obteniendo arbol", details = ex.Message });
        }
    }

    // ===================================================================
    // Estadisticas
    // ===================================================================

    /// <summary>
    /// Estadisticas generales del DMS
    /// GET /api/documents/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _documentService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/stats");
            return StatusCode(500, new { error = "Error obteniendo estadisticas", details = ex.Message });
        }
    }

    /// <summary>
    /// Estado de cumplimiento CRA documental
    /// GET /api/documents/cra-status
    /// </summary>
    [HttpGet("cra-status")]
    public async Task<IActionResult> GetCraStatus()
    {
        try
        {
            var status = await _documentService.GetCraStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/cra-status");
            return StatusCode(500, new { error = "Error obteniendo estado CRA", details = ex.Message });
        }
    }

    // ===================================================================
    // Descarga de ficheros (solo PDF original, sin conversion)
    // ===================================================================

    /// <summary>
    /// Descargar fichero de un documento
    /// GET /api/documents/{id}/download
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadFile(string id)
    {
        try
        {
            var result = await _documentService.DownloadFileAsync(id, UserRole);
            if (result == null || result.Value.FileStream == null)
                return NotFound(new { message = "Fichero no encontrado o sin acceso" });

            return File(result.Value.FileStream, result.Value.ContentType ?? "application/octet-stream", result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/{Id}/download", id);
            return StatusCode(500, new { error = "Error descargando fichero", details = ex.Message });
        }
    }

    // ===================================================================
    // Sincronizacion con DMS Enterprise
    // ===================================================================

    /// <summary>
    /// Sincronizar filesystem -> DB (escanea docs/ y registra ficheros nuevos)
    /// POST /api/documents/sync
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncFromFilesystem()
    {
        try
        {
            var result = await _documentService.SyncFromFilesystemAsync(UserName);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents/sync");
            return StatusCode(500, new { error = "Error en sincronizacion", details = ex.Message });
        }
    }

    /// <summary>
    /// Sincronizar solo AQSdocs_master
    /// POST /api/documents/sync/master
    /// </summary>
    [HttpPost("sync/master")]
    public async Task<IActionResult> SyncMaster()
    {
        try
        {
            var result = await _documentService.SyncMasterAsync(UserName);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents/sync/master");
            return StatusCode(500, new { error = "Error en sync master", details = ex.Message });
        }
    }

    /// <summary>
    /// Sincronizar solo AQSdocs_project
    /// POST /api/documents/sync/project
    /// </summary>
    [HttpPost("sync/project")]
    public async Task<IActionResult> SyncProject()
    {
        try
        {
            var result = await _documentService.SyncProjectAsync(UserName);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents/sync/project");
            return StatusCode(500, new { error = "Error en sync project", details = ex.Message });
        }
    }

    /// <summary>
    /// Notificacion push desde DMS Enterprise: un documento ha sido publicado/actualizado.
    /// POST /api/documents/dms-notify
    /// </summary>
    [HttpPost("dms-notify")]
    public async Task<IActionResult> DmsPublishNotify([FromBody] DmsPublishNotifyRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.File))
                return BadRequest(new { error = "El campo 'file' es obligatorio" });

            if (string.IsNullOrWhiteSpace(request.Source))
                request.Source = "DMS_Enterprise";

            var result = await _documentService.ProcessDmsNotifyAsync(request, UserName);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents/dms-notify");
            return StatusCode(500, new { error = "Error procesando notificacion DMS", details = ex.Message });
        }
    }

    // ===================================================================
    // Categorias (solo lectura)
    // ===================================================================

    /// <summary>
    /// Obtener todas las categorias de documentos
    /// GET /api/documents/categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _documentService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/categories");
            return StatusCode(500, new { error = "Error obteniendo categorias", details = ex.Message });
        }
    }
}
