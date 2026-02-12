// ============================================================================
// DocumentsController.cs - API REST del Sistema de Gestión Documental
// ============================================================================
// Endpoints CRUD + búsqueda + árbol + estadísticas + sync + CRA status
// Requiere autenticación JWT — permisos vía DocumentsView RBAC
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
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
    private readonly IRequestProjectContext _requestContext;
    private readonly IProjectDbContextFactory _dbFactory;

    public DocumentsController(
        ILogger<DocumentsController> logger,
        IDocumentService documentService,
        IRequestProjectContext requestContext,
        IProjectDbContextFactory dbFactory)
    {
        _logger = logger;
        _documentService = documentService;
        _requestContext = requestContext;
        _dbFactory = dbFactory;
    }

    private string UserName => User.Identity?.Name ?? "anonymous";
    private string UserRole => User.Claims.FirstOrDefault(c => c.Type == "role")?.Value 
                            ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value 
                            ?? "Viewer";

    // ═══════════════════════════════════════════════════════════════════
    // CRUD
    // ═══════════════════════════════════════════════════════════════════

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
    /// Obtener un documento por ID (incluye contenido Markdown + HTML)
    /// GET /api/documents/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocument(string id)
    {
        try
        {
            var doc = await _documentService.GetDocumentByIdAsync(id, UserRole);
            if (doc == null)
                return NotFound(new { message = $"Documento no encontrado o sin acceso: {id}" });
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
                return NotFound(new { message = $"Documento no encontrado: {slug}" });
            return Ok(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/by-slug/{Slug}", slug);
            return StatusCode(500, new { error = "Error obteniendo documento", details = ex.Message });
        }
    }

    /// <summary>
    /// Obtener contenido Markdown raw
    /// GET /api/documents/{id}/raw
    /// </summary>
    [HttpGet("{id}/raw")]
    public async Task<IActionResult> GetRawContent(string id)
    {
        try
        {
            var content = await _documentService.GetRawContentAsync(id, UserRole);
            if (content == null)
                return NotFound(new { message = "Documento no encontrado o sin acceso" });
            return Content(content, "text/markdown; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/{Id}/raw", id);
            return StatusCode(500, new { error = "Error obteniendo contenido", details = ex.Message });
        }
    }

    /// <summary>
    /// Crear un nuevo documento
    /// POST /api/documents
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _documentService.CreateDocumentAsync(request, UserName, UserRole);
            
            if (!result.Success)
                return BadRequest(result);
            
            return CreatedAtAction(nameof(GetDocument), new { id = result.Document?.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents");
            return StatusCode(500, new { error = "Error creando documento", details = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar un documento existente
    /// PUT /api/documents/{id}
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDocument(string id, [FromBody] UpdateDocumentRequest request)
    {
        try
        {
            var result = await _documentService.UpdateDocumentAsync(id, request, UserName, UserRole);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en PUT /api/documents/{Id}", id);
            return StatusCode(500, new { error = "Error actualizando documento", details = ex.Message });
        }
    }

    /// <summary>
    /// Eliminar un documento
    /// DELETE /api/documents/{id}
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(string id)
    {
        try
        {
            var result = await _documentService.DeleteDocumentAsync(id, UserName, UserRole);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DELETE /api/documents/{Id}", id);
            return StatusCode(500, new { error = "Error eliminando documento", details = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Árbol / Navegación
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtener árbol de documentos para navegación lateral
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
            return StatusCode(500, new { error = "Error obteniendo árbol", details = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Estadísticas / CRA
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Estadísticas generales del DMS
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
            return StatusCode(500, new { error = "Error obteniendo estadísticas", details = ex.Message });
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

    // ═══════════════════════════════════════════════════════════════════
    // Sincronización
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sincronizar filesystem → DB (escanea docs/ y registra ficheros nuevos)
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
            return StatusCode(500, new { error = "Error en sincronización", details = ex.Message });
        }
    }

    /// <summary>
    /// Sincronizar solo AQSdocs_master (copia docs del código fuente al proyecto)
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
    /// Sincronizar solo AQSdocs_project (escanea carpetas, auto-crea categorías, registra docs)
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

    // ═══════════════════════════════════════════════════════════════════
    // Historial
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Historial de cambios de un documento
    /// GET /api/documents/{id}/history
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetDocumentHistory(string id)
    {
        try
        {
            var history = await _documentService.GetDocumentHistoryAsync(id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET /api/documents/{Id}/history", id);
            return StatusCode(500, new { error = "Error obteniendo historial", details = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Utilidades
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Renderizar Markdown a HTML (preview)
    /// POST /api/documents/render-markdown
    /// </summary>
    [HttpPost("render-markdown")]
    public IActionResult RenderMarkdown([FromBody] MarkdownRenderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Markdown))
                return BadRequest(new { error = "Markdown vacío" });
            
            var html = _documentService.RenderMarkdownToHtml(request.Markdown);
            return Ok(new { html });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renderizando Markdown");
            return StatusCode(500, new { error = "Error renderizando Markdown", details = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Categorías dinámicas
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtener todas las categorías de documentos
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
            return StatusCode(500, new { error = "Error obteniendo categorías", details = ex.Message });
        }
    }

    /// <summary>
    /// Crear nueva categoría (solo SuperAdmin)
    /// POST /api/documents/categories
    /// </summary>
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] DocumentCategoryConfig category)
    {
        try
        {
            if (UserRole != "SuperAdmin")
                return Forbid();

            if (string.IsNullOrWhiteSpace(category.Name))
                return BadRequest(new { error = "El nombre de la categoría es obligatorio" });

            var created = await _documentService.CreateCategoryAsync(category, UserName);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en POST /api/documents/categories");
            return StatusCode(500, new { error = "Error creando categoría", details = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar una categoría (solo SuperAdmin)
    /// PUT /api/documents/categories/{id}
    /// </summary>
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] DocumentCategoryConfig category)
    {
        try
        {
            if (UserRole != "SuperAdmin")
                return Forbid();

            var updated = await _documentService.UpdateCategoryAsync(id, category, UserName);
            if (updated == null)
                return NotFound(new { error = $"Categoría {id} no encontrada" });

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en PUT /api/documents/categories/{Id}", id);
            return StatusCode(500, new { error = "Error actualizando categoría", details = ex.Message });
        }
    }

    /// <summary>
    /// Eliminar una categoría personalizada (solo SuperAdmin, no del sistema)
    /// DELETE /api/documents/categories/{id}
    /// </summary>
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            if (UserRole != "SuperAdmin")
                return Forbid();

            var deleted = await _documentService.DeleteCategoryAsync(id);
            if (!deleted)
                return BadRequest(new { error = "No se puede eliminar la categoría. Puede ser del sistema o no existir." });

            return Ok(new { message = "Categoría eliminada correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DELETE /api/documents/categories/{Id}", id);
            return StatusCode(500, new { error = "Error eliminando categoría", details = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Diagnóstico (TEMPORAL — quitar en producción)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Diagnóstico de categorías: muestra distribución de documentos por categoría
    /// GET /api/documents/diag/categories
    /// </summary>
    [HttpGet("diag/categories")]
    [AllowAnonymous]
    public async Task<IActionResult> DiagCategories()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var docs = await db.Documents.ToListAsync();
            
            var categories = await db.DocumentCategories.ToListAsync();
            var catNames = categories.ToDictionary(c => c.Id, c => c.Name);

            var grouped = docs.GroupBy(d => d.Category)
                .Select(g => new { 
                    CategoryId = g.Key, 
                    CategoryName = catNames.GetValueOrDefault(g.Key, "UNKNOWN"),
                    Count = g.Count(),
                    SamplePaths = g.Take(3).Select(d => d.FilePath).ToList()
                })
                .OrderBy(g => g.CategoryId)
                .ToList();

            return Ok(new { 
                totalDocs = docs.Count,
                categories = grouped,
                allCategories = categories.Select(c => new { c.Id, c.Name, c.ParentId }).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request para preview de renderizado MD→HTML
/// </summary>
public class MarkdownRenderRequest
{
    public string Markdown { get; set; } = "";
}
