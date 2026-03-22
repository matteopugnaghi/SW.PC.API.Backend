// ============================================================================
// DocumentService.cs - Implementación del Servicio de Gestión Documental
// ============================================================================
// CRUD + filesystem sync + Markdown rendering + búsqueda + estadísticas
// Scoped service — usa IRequestProjectContext para multi-proyecto
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IProjectContextService _globalContext;
    private readonly IRequestProjectContext _requestContext;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly IDocumentExportService _exportService;
    private readonly string _contentRootPath;

    // (CategoryFolders estático eliminado — las carpetas se resuelven dinámicamente desde la DB)

    // Orden de roles de menor a mayor privilegio
    // Incluye nombres en español (sistema actual) + inglés (legacy)
    private static readonly Dictionary<string, int> RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Viewer", 0 }, { "Visualizador", 0 },
        { "Operator", 1 }, { "Operador", 1 },
        { "Auditor", 2 },
        { "Maintenance", 3 }, { "Mantenimiento", 3 },
        { "Administrator", 4 }, { "Administrador", 4 },
        { "SuperAdmin", 5 }
    };

    // Roles del sistema para la matriz de acceso (sin SuperAdmin — tiene acceso implícito)
    // Orden: menor → mayor privilegio, alineado con RoleHierarchy
    public static readonly string[] SystemRoles = { "Visualizador", "Operador", "Auditor", "Mantenimiento", "Administrador" };

    // Mapeo JWT (inglés) → Matriz (español). La Matriz usa nombres en español (SystemRoles),
    // pero el JWT almacena nombres en inglés. Este mapa normaliza para las consultas de acceso.
    private static readonly Dictionary<string, string> RoleToMatrixName = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Viewer", "Visualizador" },
        { "Operator", "Operador" },
        { "Auditor", "Auditor" },
        { "Maintenance", "Mantenimiento" },
        { "Administrator", "Administrador" },
        // Si ya vienen en español, se mapean a sí mismos
        { "Visualizador", "Visualizador" },
        { "Operador", "Operador" },
        { "Mantenimiento", "Mantenimiento" },
        { "Administrador", "Administrador" },
        { "SuperAdmin", "SuperAdmin" }
    };

    /// <summary>
    /// Normaliza el nombre de rol del JWT (inglés) al nombre usado en la Matriz de Acceso (español).
    /// </summary>
    private static string NormalizeRoleForMatrix(string jwtRole)
        => RoleToMatrixName.GetValueOrDefault(jwtRole, jwtRole);

    // Mapeo AccessLevel → rol mínimo requerido (legacy, mantener para compatibilidad)
    private static readonly Dictionary<DocumentAccessLevel, string> AccessLevelToRole = new()
    {
        { DocumentAccessLevel.Public, "Visualizador" },
        { DocumentAccessLevel.Operator, "Operador" },
        { DocumentAccessLevel.Maintenance, "Mantenimiento" },
        { DocumentAccessLevel.Admin, "Administrador" },
        { DocumentAccessLevel.Internal, "SuperAdmin" }
    };

    public DocumentService(
        ILogger<DocumentService> logger,
        IProjectDbContextFactory dbFactory,
        IProjectContextService globalContext,
        IRequestProjectContext requestContext,
        IWebHostEnvironment environment,
        IDocumentExportService exportService)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _globalContext = globalContext;
        _requestContext = requestContext;
        _contentRootPath = environment.ContentRootPath;
        _exportService = exportService;
        
        // Configurar pipeline de Markdig con extensiones comunes
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseAutoLinks()
            .Build();
    }

    /// <summary>
    /// Obtiene la ruta de la carpeta docs/ global del backend (no del proyecto)
    /// </summary>
    private string GetGlobalDocsPath()
    {
        return Path.Combine(_contentRootPath, "docs");
    }

    /// <summary>
    /// Resuelve la ruta absoluta de un fichero de documento.
    /// Todo vive dentro de docs/ del proyecto (incluido AQSdocs_master/).
    /// Fallback a docs/ global del backend solo en desarrollo.
    /// </summary>
    private string? ResolveDocFilePath(string relativePath)
    {
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        
        // 1. Buscar en docs/ del proyecto activo (aquí están tanto master como project)
        var projectDocsPath = _requestContext.DocsPath;
        if (!string.IsNullOrEmpty(projectDocsPath))
        {
            var projectPath = Path.Combine(projectDocsPath, normalizedRelative);
            if (File.Exists(projectPath)) return projectPath;
        }

        // 2. Fallback: docs/ global del backend (solo en desarrollo, antes del primer Sync)
        var globalDocsPath = GetGlobalDocsPath();
        var globalPath = Path.GetFullPath(Path.Combine(globalDocsPath, normalizedRelative));
        if (File.Exists(globalPath)) return globalPath;

        return null;
    }

    #region CRUD

    public async Task<(List<DocumentInfo> Items, int TotalCount)> GetDocumentsAsync(DocumentFilter filter, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.Documents.AsQueryable();

            // Filtrar por acceso del usuario (matriz categoría×rol)
            query = await ApplyAccessFilterWithMatrixAsync(db, query, userRole);

            // Filtros opcionales
            if (filter.Scope.HasValue)
                query = query.Where(d => d.Scope == filter.Scope.Value);
            
            if (filter.Category.HasValue)
                query = query.Where(d => d.Category == filter.Category.Value);

            // Filtro por carpeta real (para docs master: architecture, changelog, etc.)
            if (!string.IsNullOrWhiteSpace(filter.FolderName))
            {
                var folder = filter.FolderName;
                // FilePath tiene formato: "AQSdocs_master/architecture/FILE.md"
                // Buscamos docs cuyo FilePath contenga "/{folderName}/" después del prefijo
                query = query.Where(d => d.FilePath.Contains("/" + folder + "/")
                    || d.FilePath.Contains("\\" + folder + "\\"));
            }
            
            if (filter.Status.HasValue)
                query = query.Where(d => d.Status == filter.Status.Value);
            
            if (filter.CraRelevant.HasValue)
                query = query.Where(d => d.CraRelevant == filter.CraRelevant.Value);
            
            if (!string.IsNullOrWhiteSpace(filter.Tag))
                query = query.Where(d => d.Tags != null && d.Tags.Contains(filter.Tag));
            
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var search = filter.SearchQuery.ToLower();
                query = query.Where(d => 
                    d.Title.ToLower().Contains(search) ||
                    (d.Description != null && d.Description.ToLower().Contains(search)) ||
                    (d.SearchContent != null && d.SearchContent.ToLower().Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(d => MapToInfo(d))
                .ToListAsync();

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo lista de documentos");
            return (new List<DocumentInfo>(), 0);
        }
    }

    public async Task<DocumentDetail?> GetDocumentByIdAsync(string id, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return null;
            
            if (!HasAccessToDocument(doc, userRole)) return null;

            return await BuildDocumentDetail(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo documento {Id}", id);
            return null;
        }
    }

    public async Task<DocumentDetail?> GetDocumentBySlugAsync(string slug, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Slug == slug);
            if (doc == null) return null;
            
            if (!HasAccessToDocument(doc, userRole)) return null;

            return await BuildDocumentDetail(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo documento por slug {Slug}", slug);
            return null;
        }
    }

    public async Task<DocumentOperationResponse> CreateDocumentAsync(CreateDocumentRequest request, string userName, string userRole)
    {
        string absolutePath = "";
        try
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(request.Title))
                return new DocumentOperationResponse { Success = false, Message = "El título es obligatorio" };

            var slug = GenerateSlug(request.Title);
            var fileName = !string.IsNullOrWhiteSpace(request.FileName) 
                ? request.FileName 
                : $"{slug}.md";

            // Determinar ruta del fichero — resolver carpeta desde la jerarquía de categorías en DB
            using var dbForPath = _dbFactory.CreateDbContext();
            var categoryFolder = await BuildCategoryFolderPathAsync(dbForPath, request.Category);
            
            // Prefijo según scope: Project → AQSdocs_project/, Software → AQSdocs_master/
            var scopePrefix = request.Scope == DocumentScope.Project ? "AQSdocs_project" : "AQSdocs_master";
            
            // Construir ruta relativa: {scopePrefix}/{categoryFolder}/{filename}
            string relativePath;
            if (string.IsNullOrEmpty(categoryFolder))
                relativePath = $"{scopePrefix}/{fileName}";
            else
                relativePath = $"{scopePrefix}/{categoryFolder}/{fileName}".Replace('\\', '/');

            var docsPath = _requestContext.DocsPath;
            absolutePath = Path.Combine(docsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Verificar que no exista ya
            if (File.Exists(absolutePath))
                return new DocumentOperationResponse { Success = false, Message = $"Ya existe un fichero en: {relativePath}" };

            // Crear directorio si no existe
            var dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Contenido inicial
            var content = request.Content ?? BuildInitialContent(request);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var hash = ComputeSha256(contentBytes);

            // Escribir fichero
            await File.WriteAllTextAsync(absolutePath, content, Encoding.UTF8);

            // Crear registro en DB
            var doc = new Document
            {
                Id = Guid.NewGuid().ToString(),
                Slug = slug,
                Title = request.Title,
                Description = request.Description,
                FilePath = relativePath,
                FileType = DocumentFileType.Markdown,
                ContentHash = hash,
                FileSize = contentBytes.Length,
                Scope = request.Scope,
                Category = request.Category,
                Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
                AccessLevel = request.AccessLevel,
                MinimumRole = request.MinimumRole ?? "Administrador", // Restrictivo por defecto hasta configuración explícita
                ClassificationId = request.ClassificationId ?? 0,
                Version = "1.0",
                Status = DocumentStatus.Draft,
                CraRelevant = request.CraRelevant,
                CraArticle = request.CraArticle,
                Iso27001Relevant = request.Iso27001Relevant,
                Iso27001Article = request.Iso27001Article,
                Iec62443Relevant = request.Iec62443Relevant,
                Iec62443Article = request.Iec62443Article,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                SearchContent = ExtractSearchContent(content)
            };

            using var db = _dbFactory.CreateDbContext();
            db.Documents.Add(doc);
            
            // Historial
            db.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = doc.Id,
                Version = doc.Version,
                Action = "created",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                ContentHash = hash,
                ChangeNote = $"Documento creado: {doc.Title}"
            });
            
            await db.SaveChangesAsync();

            _logger.LogInformation("📄 Documento creado: {Title} por {User} en {Path}", doc.Title, userName, relativePath);

            return new DocumentOperationResponse
            {
                Success = true,
                Message = $"Documento '{doc.Title}' creado correctamente",
                Document = MapToInfo(doc)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando documento: {Title}. Inner: {Inner}", request.Title, ex.InnerException?.Message);
            // Limpiar fichero huérfano si se creó en disco pero falló la DB
            try { if (File.Exists(absolutePath)) File.Delete(absolutePath); } catch { }
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            return new DocumentOperationResponse { Success = false, Message = $"Error creando documento: {innerMsg}" };
        }
    }

    public async Task<DocumentOperationResponse> UpdateDocumentAsync(string id, UpdateDocumentRequest request, string userName, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null)
                return new DocumentOperationResponse { Success = false, Message = "Documento no encontrado" };

            // Documentos master (Software): solo se permiten cambios de metadatos de acceso/clasificación
            // El contenido, título y archivo son de solo lectura (vienen del servidor corporativo)
            var isMasterDoc = doc.Scope == DocumentScope.Software;
            if (isMasterDoc)
            {
                // Bloquear cambios de contenido en master
                if (request.Title != null || request.Content != null || request.Description != null || request.Version != null)
                    return new DocumentOperationResponse { Success = false, Message = "Los documentos AQSdocs_master son de solo lectura. Solo se pueden modificar metadatos de acceso (rol mínimo, clasificación, tags, normativas)." };
            }

            if (!HasAccessToDocument(doc, userRole))
                return new DocumentOperationResponse { Success = false, Message = "Sin permisos para editar este documento" };

            // Actualizar campos opcionales (en master: solo metadatos de acceso)
            if (!isMasterDoc)
            {
                if (request.Title != null) doc.Title = request.Title;
                if (request.Description != null) doc.Description = request.Description;
                if (request.Version != null) doc.Version = request.Version;
            }

            // Metadatos de acceso/clasificación — permitidos siempre (incluido master)
            if (request.MinimumRole != null) doc.MinimumRole = request.MinimumRole;
            if (request.ClassificationId.HasValue) doc.ClassificationId = request.ClassificationId.Value;
            if (request.AccessLevel.HasValue)
            {
                doc.AccessLevel = request.AccessLevel.Value;
                // Solo sobrescribir MinimumRole desde AccessLevel si no se proporcionó MinimumRole explícitamente
                if (request.MinimumRole == null)
                    doc.MinimumRole = AccessLevelToRole.GetValueOrDefault(request.AccessLevel.Value, "Visualizador");
            }
            if (request.Status.HasValue) doc.Status = request.Status.Value;
            if (request.Tags != null) doc.Tags = JsonSerializer.Serialize(request.Tags);
            if (request.CraRelevant.HasValue) doc.CraRelevant = request.CraRelevant.Value;
            if (request.CraArticle != null) doc.CraArticle = request.CraArticle;
            if (request.Iso27001Relevant.HasValue) doc.Iso27001Relevant = request.Iso27001Relevant.Value;
            if (request.Iso27001Article != null) doc.Iso27001Article = request.Iso27001Article;
            if (request.Iec62443Relevant.HasValue) doc.Iec62443Relevant = request.Iec62443Relevant.Value;
            if (request.Iec62443Article != null) doc.Iec62443Article = request.Iec62443Article;

            // Actualizar contenido del fichero si se proporcionó (solo para docs de proyecto)
            if (request.Content != null && !isMasterDoc)
            {
                var docsPath = _requestContext.DocsPath;
                var absolutePath = Path.Combine(docsPath, doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
                var contentBytes = Encoding.UTF8.GetBytes(request.Content);
                
                await File.WriteAllTextAsync(absolutePath, request.Content, Encoding.UTF8);
                
                doc.ContentHash = ComputeSha256(contentBytes);
                doc.FileSize = contentBytes.Length;
                doc.SearchContent = ExtractSearchContent(request.Content);
            }

            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            // Historial
            db.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = doc.Id,
                Version = doc.Version,
                Action = "edited",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                ContentHash = doc.ContentHash,
                ChangeNote = request.ChangeNote ?? $"Documento actualizado por {userName}"
            });

            await db.SaveChangesAsync();

            _logger.LogInformation("📝 Documento actualizado: {Title} por {User}", doc.Title, userName);

            return new DocumentOperationResponse
            {
                Success = true,
                Message = $"Documento '{doc.Title}' actualizado correctamente",
                Document = MapToInfo(doc)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando documento {Id}", id);
            return new DocumentOperationResponse { Success = false, Message = $"Error actualizando documento: {ex.Message}" };
        }
    }

    public async Task<DocumentOperationResponse> DeleteDocumentAsync(string id, string userName, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null)
                return new DocumentOperationResponse { Success = false, Message = "Documento no encontrado" };

            // Los documentos master (Software) son de solo lectura — vienen del servidor corporativo
            if (doc.Scope == DocumentScope.Software)
                return new DocumentOperationResponse { Success = false, Message = "Los documentos AQSdocs_master no se pueden eliminar" };

            if (!HasAccessToDocument(doc, userRole))
                return new DocumentOperationResponse { Success = false, Message = "Sin permisos para eliminar este documento" };

            // Eliminar fichero del filesystem
            var docsPath = _requestContext.DocsPath;
            var absolutePath = Path.Combine(docsPath, doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
                _logger.LogInformation("🗑️ Fichero eliminado: {Path}", absolutePath);
            }

            // Historial antes de eliminar
            db.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = doc.Id,
                Version = doc.Version,
                Action = "deleted",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                ChangeNote = $"Documento eliminado: {doc.Title}"
            });

            // Eliminar de DB
            db.Documents.Remove(doc);
            await db.SaveChangesAsync();

            _logger.LogInformation("🗑️ Documento eliminado de DB: {Title} por {User}", doc.Title, userName);

            return new DocumentOperationResponse
            {
                Success = true,
                Message = $"Documento '{doc.Title}' eliminado correctamente"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando documento {Id}", id);
            return new DocumentOperationResponse { Success = false, Message = $"Error eliminando documento: {ex.Message}" };
        }
    }

    #endregion

    #region Árbol de navegación

    public async Task<List<DocumentTreeNode>> GetDocumentTreeAsync(string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var docs = await ApplyAccessFilterWithMatrixAsync(db, db.Documents.AsQueryable(), userRole);
            var docList = await docs.ToListAsync();

            // Cargar categorías para nombres/iconos/colores y jerarquía
            var allCategories = await db.DocumentCategories
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            var catMap = allCategories.ToDictionary(c => c.Id);

            var tree = new List<DocumentTreeNode>();

            // Helper: extraer la carpeta real del primer nivel del FilePath
            // Ej: "AQSdocs_master/architecture/SOFTWARE_INTEGRITY.md" → "architecture"
            //     "AQSdocs_master/README.md" → null (raíz, sin carpeta)
            string? GetRealFolder(string filePath)
            {
                var parts = filePath.Split('/');
                int startIdx = 0;
                if (parts.Length > 0 && (parts[0].Equals("AQSdocs_master", StringComparison.OrdinalIgnoreCase)
                    || parts[0].Equals("AQSdocs_project", StringComparison.OrdinalIgnoreCase)))
                {
                    startIdx = 1;
                }
                // Si hay al menos 2 partes después del prefijo → la primera es la carpeta
                if (parts.Length >= startIdx + 2)
                    return parts[startIdx];
                return null; // fichero en la raíz
            }

            // Helper: construir nodos de carpeta real dentro de un scope
            List<DocumentTreeNode> BuildFolderNodes(IEnumerable<Document> scopeDocs)
            {
                var folderNodes = new List<DocumentTreeNode>();
                var docsByFolder = scopeDocs
                    .GroupBy(d => GetRealFolder(d.FilePath) ?? "__root__")
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var folderGroup in docsByFolder)
                {
                    var folderName = folderGroup.Key;
                    var docsInFolder = folderGroup.OrderBy(d => d.Title).ToList();

                    if (folderName == "__root__")
                    {
                        // Ficheros en la raíz del scope (sin subcarpeta) → directamente como hijos
                        foreach (var d in docsInFolder)
                        {
                            folderNodes.Add(new DocumentTreeNode
                            {
                                Id = d.Id, Name = d.Title,
                                Icon = GetFileTypeIcon(d.FileType), Type = "document",
                                DocumentId = d.Id, Category = d.Category, Scope = d.Scope,
                                AccessLevel = d.AccessLevel, Status = d.Status, CraRelevant = d.CraRelevant
                            });
                        }
                        continue;
                    }

                    var folderNode = new DocumentTreeNode
                    {
                        Id = $"folder-{folderName}",
                        Name = folderName,
                        Icon = "📁",
                        Type = "folder",
                        DocumentCount = docsInFolder.Count,
                        Children = docsInFolder.Select(d => new DocumentTreeNode
                        {
                            Id = d.Id, Name = d.Title,
                            Icon = GetFileTypeIcon(d.FileType), Type = "document",
                            DocumentId = d.Id, Category = d.Category, Scope = d.Scope,
                            AccessLevel = d.AccessLevel, Status = d.Status, CraRelevant = d.CraRelevant
                        }).ToList()
                    };
                    folderNodes.Add(folderNode);
                }

                return folderNodes;
            }

            // Helper: construir nodos de categoría (para AQSdocs_project)
            List<DocumentTreeNode> BuildCategoryNodes(IEnumerable<Document> scopeDocs)
            {
                var catNodes = new List<DocumentTreeNode>();
                var docsByCategory = scopeDocs.GroupBy(d => d.Category).ToDictionary(g => g.Key, g => g.ToList());

                // Obtener IDs de categorías raíz que tienen docs (directos o via subcategorías)
                var usedCatIds = docsByCategory.Keys.ToHashSet();
                var rootCatIds = new HashSet<int>();

                foreach (var catId in usedCatIds)
                {
                    var config = catMap.GetValueOrDefault(catId);
                    if (config?.ParentId != null)
                        rootCatIds.Add(config.ParentId.Value); // padre de subcategoría
                    else
                        rootCatIds.Add(catId); // categoría raíz directa
                }

                foreach (var rootCatId in rootCatIds.OrderBy(id => catMap.GetValueOrDefault(id)?.SortOrder ?? 999))
                {
                    var rootConfig = catMap.GetValueOrDefault(rootCatId);
                    var catNode = new DocumentTreeNode
                    {
                        Id = $"cat-{rootCatId}",
                        Name = rootConfig?.Name ?? GetCategoryDisplayName(rootCatId),
                        Icon = rootConfig?.Icon ?? GetCategoryIcon(rootCatId),
                        Type = "folder",
                        Category = rootCatId,
                        DocumentCount = 0,
                        Children = new List<DocumentTreeNode>()
                    };

                    // Subcategorías hijas
                    var childCatIds = allCategories
                        .Where(c => c.ParentId == rootCatId)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => c.Id)
                        .ToList();

                    foreach (var subId in childCatIds)
                    {
                        if (!docsByCategory.ContainsKey(subId)) continue;
                        var subConfig = catMap.GetValueOrDefault(subId);
                        var subDocs = docsByCategory[subId].OrderBy(d => d.Title).ToList();
                        var subNode = new DocumentTreeNode
                        {
                            Id = $"cat-{subId}",
                            Name = subConfig?.Name ?? $"Categoría {subId}",
                            Icon = subConfig?.Icon ?? "📄",
                            Type = "folder",
                            Category = subId,
                            DocumentCount = subDocs.Count,
                            Children = subDocs.Select(d => new DocumentTreeNode
                            {
                                Id = d.Id, Name = d.Title,
                                Icon = GetFileTypeIcon(d.FileType), Type = "document",
                                DocumentId = d.Id, Category = d.Category, Scope = d.Scope,
                                AccessLevel = d.AccessLevel, Status = d.Status, CraRelevant = d.CraRelevant
                            }).ToList()
                        };
                        catNode.Children.Add(subNode);
                        catNode.DocumentCount += subDocs.Count;
                    }

                    // Docs directos de la categoría raíz
                    if (docsByCategory.ContainsKey(rootCatId))
                    {
                        foreach (var d in docsByCategory[rootCatId].OrderBy(d => d.Title))
                        {
                            catNode.Children.Add(new DocumentTreeNode
                            {
                                Id = d.Id, Name = d.Title,
                                Icon = GetFileTypeIcon(d.FileType), Type = "document",
                                DocumentId = d.Id, Category = d.Category, Scope = d.Scope,
                                AccessLevel = d.AccessLevel, Status = d.Status, CraRelevant = d.CraRelevant
                            });
                            catNode.DocumentCount++;
                        }
                    }

                    if (catNode.DocumentCount > 0)
                        catNodes.Add(catNode);
                }

                return catNodes;
            }

            // Agrupar por Scope:
            //   - Software (master) → carpetas reales del filesystem (solo lectura)
            //   - Project → categorías gestionables
            // Siempre mostrar ambos scopes, incluso si están vacíos
            var docsByScope = docList.GroupBy(d => d.Scope).ToDictionary(g => g.Key, g => g.AsEnumerable());

            foreach (var scope in new[] { DocumentScope.Software, DocumentScope.Project })
            {
                var isMaster = scope == DocumentScope.Software;
                var scopeDocs = docsByScope.GetValueOrDefault(scope, Enumerable.Empty<Document>());
                var scopeNode = new DocumentTreeNode
                {
                    Id = $"scope-{scope}",
                    Name = isMaster ? "AQSdocs_master" : "AQSdocs_project",
                    Icon = isMaster ? "🖥️" : "📦",
                    Type = "folder",
                    Scope = scope,
                    DocumentCount = scopeDocs.Count(),
                    Children = isMaster 
                        ? BuildFolderNodes(scopeDocs)      // Master: carpetas reales
                        : BuildCategoryNodes(scopeDocs)    // Project: categorías gestionables 
                };

                tree.Add(scopeNode);
            }

            // Ordenar: Software primero, Proyecto después
            tree.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            return tree;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error construyendo árbol de documentos");
            return new List<DocumentTreeNode>();
        }
    }

    #endregion

    #region Estadísticas

    public async Task<DocumentStats> GetStatsAsync()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var docs = await db.Documents.ToListAsync();
            var classifications = await db.DocumentClassificationLevels.ToListAsync();
            var categories = await db.DocumentCategories.ToListAsync();

            var lastDoc = docs.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).FirstOrDefault();

            var stats = new DocumentStats
            {
                // ═══ General ═══
                TotalDocuments = docs.Count,
                TotalByScope_Software = docs.Count(d => d.Scope == DocumentScope.Software),
                TotalByScope_Project = docs.Count(d => d.Scope == DocumentScope.Project),
                TotalSizeBytes = docs.Sum(d => d.FileSize),
                LastUpdated = lastDoc != null ? (lastDoc.UpdatedAt ?? lastDoc.CreatedAt) : null,
                LastUpdatedDocument = lastDoc?.Title,
                GeneratedAt = DateTime.UtcNow,

                // ═══ EU CRA 2024/2847 ═══
                CraRelevantTotal = docs.Count(d => d.CraRelevant),
                CraRelevantApproved = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Approved),
                CraRelevantPending = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Review),
                CraRelevantDraft = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Draft),

                // ═══ ISO 27001 ═══
                Iso27001RelevantTotal = docs.Count(d => d.Iso27001Relevant),
                Iso27001RelevantApproved = docs.Count(d => d.Iso27001Relevant && d.Status == DocumentStatus.Approved),
                Iso27001RelevantPending = docs.Count(d => d.Iso27001Relevant && d.Status != DocumentStatus.Approved),

                // ═══ IEC 62443 ═══
                Iec62443RelevantTotal = docs.Count(d => d.Iec62443Relevant),
                Iec62443RelevantApproved = docs.Count(d => d.Iec62443Relevant && d.Status == DocumentStatus.Approved),
                Iec62443RelevantPending = docs.Count(d => d.Iec62443Relevant && d.Status != DocumentStatus.Approved),

                // ═══ Auditoría ═══
                DocsWithTags = docs.Count(d => !string.IsNullOrEmpty(d.Tags)),
                DocsWithClassification = docs.Count(d => d.ClassificationId > 0),
            };

            // Compliance percentages
            stats.CraCompliancePercent = stats.CraRelevantTotal > 0
                ? Math.Round((double)stats.CraRelevantApproved / stats.CraRelevantTotal * 100, 1)
                : 0;
            stats.Iso27001CompliancePercent = stats.Iso27001RelevantTotal > 0
                ? Math.Round((double)stats.Iso27001RelevantApproved / stats.Iso27001RelevantTotal * 100, 1)
                : 0;
            stats.Iec62443CompliancePercent = stats.Iec62443RelevantTotal > 0
                ? Math.Round((double)stats.Iec62443RelevantApproved / stats.Iec62443RelevantTotal * 100, 1)
                : 0;

            // ═══ Por categoría (usando nombre de la categoría) ═══
            var catMap = categories.ToDictionary(c => c.Id, c => $"{c.Icon} {c.Name}");
            foreach (var group in docs.GroupBy(d => d.Category))
            {
                var label = catMap.TryGetValue(group.Key, out var name) ? name : $"Cat {group.Key}";
                stats.ByCategory[label] = group.Count();
            }

            // ═══ Por estado ═══
            var statusLabels = new Dictionary<string, string> {
                ["Draft"] = "📝 Borrador", ["Review"] = "🔍 En Revisión", ["Approved"] = "✅ Aprobado",
                ["Obsolete"] = "⚠️ Obsoleto", ["Archived"] = "📦 Archivado"
            };
            foreach (var group in docs.GroupBy(d => d.Status))
            {
                var label = statusLabels.TryGetValue(group.Key.ToString(), out var name) ? name : group.Key.ToString();
                stats.ByStatus[label] = group.Count();
            }

            // ═══ Por tipo de fichero ═══
            var ftLabels = new Dictionary<string, string> {
                ["Markdown"] = "📝 Markdown", ["PDF"] = "📕 PDF", ["Image"] = "🖼️ Imagen",
                ["Binary"] = "📦 Binario", ["Excel"] = "📊 Excel", ["Word"] = "📘 Word",
                ["Text"] = "📄 Texto", ["Unknown"] = "❓ Otro"
            };
            foreach (var group in docs.GroupBy(d => d.FileType))
            {
                var label = ftLabels.TryGetValue(group.Key.ToString(), out var name) ? name : group.Key.ToString();
                stats.ByFileType[label] = group.Count();
            }

            // ═══ Por MinimumRole ═══
            foreach (var group in docs.GroupBy(d => d.MinimumRole ?? "Visualizador"))
                stats.ByMinimumRole[group.Key] = group.Count();

            // ═══ Por clasificación ISO 27001 ═══
            var classMap = classifications.ToDictionary(c => c.Id, c => $"{c.Icon} {c.Name}");
            classMap[0] = "🌐 Público";
            foreach (var group in docs.GroupBy(d => d.ClassificationId))
            {
                var label = classMap.TryGetValue(group.Key, out var name) ? name : $"Nivel {group.Key}";
                stats.ByClassification[label] = group.Count();
            }

            // ═══ CRA por artículo ═══
            foreach (var group in docs.Where(d => d.CraRelevant && !string.IsNullOrEmpty(d.CraArticle)).GroupBy(d => d.CraArticle!))
                stats.CraByArticle[group.Key] = group.Count();

            // ═══ ISO 27001 por artículo ═══
            foreach (var group in docs.Where(d => d.Iso27001Relevant && !string.IsNullOrEmpty(d.Iso27001Article)).GroupBy(d => d.Iso27001Article!))
                stats.Iso27001ByArticle[group.Key] = group.Count();

            // ═══ IEC 62443 por artículo ═══
            foreach (var group in docs.Where(d => d.Iec62443Relevant && !string.IsNullOrEmpty(d.Iec62443Article)).GroupBy(d => d.Iec62443Article!))
                stats.Iec62443ByArticle[group.Key] = group.Count();

            // ═══ Historial de versiones ═══
            var versionCounts = await db.DocumentHistories.GroupBy(v => v.DocumentId).Select(g => new { DocId = g.Key, Count = g.Count() }).ToListAsync();
            stats.DocsWithVersionHistory = versionCounts.Count;
            stats.TotalVersionEntries = versionCounts.Sum(v => v.Count);

            // ═══ Actividad reciente (últimos 10 docs modificados) ═══
            stats.RecentActivity = docs
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .Take(10)
                .Select(d => new RecentDocActivity
                {
                    Id = d.Id,
                    Title = d.Title,
                    Status = d.Status.ToString(),
                    Date = d.UpdatedAt ?? d.CreatedAt,
                    Author = d.CreatedBy,
                    Version = d.Version
                }).ToList();

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas DMS");
            return new DocumentStats();
        }
    }

    public async Task<CraDocumentStatus> GetCraStatusAsync()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var craDocs = await db.Documents.Where(d => d.CraRelevant).ToListAsync();

            var status = new CraDocumentStatus
            {
                TotalRequired = craDocs.Count,
                TotalApproved = craDocs.Count(d => d.Status == DocumentStatus.Approved),
                TotalDraft = craDocs.Count(d => d.Status == DocumentStatus.Draft),
                TotalPending = craDocs.Count(d => d.Status == DocumentStatus.Review),
                RequiredDocuments = craDocs.Select(d => new CraDocumentItem
                {
                    DocumentId = d.Id,
                    Title = d.Title,
                    CraArticle = d.CraArticle ?? "",
                    Status = d.Status,
                    Deadline = d.CraDeadline,
                    Version = d.Version,
                    Exists = true
                }).OrderBy(d => d.Deadline).ToList()
            };

            status.CompliancePercent = status.TotalRequired > 0
                ? Math.Round((double)status.TotalApproved / status.TotalRequired * 100, 1)
                : 0;

            var nextDeadlineDoc = craDocs
                .Where(d => d.CraDeadline.HasValue && d.CraDeadline > DateTime.UtcNow && d.Status != DocumentStatus.Approved)
                .OrderBy(d => d.CraDeadline)
                .FirstOrDefault();

            if (nextDeadlineDoc != null)
            {
                status.NextDeadline = nextDeadlineDoc.CraDeadline;
                status.NextDeadlineDocument = nextDeadlineDoc.Title;
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estado CRA");
            return new CraDocumentStatus();
        }
    }

    #endregion

    #region Contenido

    public async Task<string?> GetRawContentAsync(string id, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null || !HasAccessToDocument(doc, userRole)) return null;

            var absolutePath = ResolveDocFilePath(doc.FilePath);
            if (absolutePath == null) return null;
            return await File.ReadAllTextAsync(absolutePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo contenido raw de {Id}", id);
            return null;
        }
    }

    public string RenderMarkdownToHtml(string markdown)
    {
        return Markdig.Markdown.ToHtml(markdown, _markdownPipeline);
    }

    #endregion

    #region Sincronización filesystem → DB

    /// <summary>
    /// Sync completo (master + project). Mantiene compatibilidad con el endpoint existente.
    /// </summary>
    public async Task<DocumentOperationResponse> SyncFromFilesystemAsync(string userName)
    {
        var masterResult = await SyncMasterAsync(userName);
        var projectResult = await SyncProjectAsync(userName);
        
        var combined = $"MASTER: {masterResult.Message} | PROJECT: {projectResult.Message}";
        return new DocumentOperationResponse 
        { 
            Success = masterResult.Success && projectResult.Success, 
            Message = combined 
        };
    }

    /// <summary>
    /// Sincronizar AQSdocs_master: copia docs del código fuente (backend/docs/) al proyecto
    /// y registra los .md en DB con scope=Software.
    /// Documentos con Source="DMS_Enterprise" NO se purgan — solo se re-crean los locales.
    /// Si existe _dms_tree.json en una carpeta, usa sus metadatos para los docs de esa carpeta.
    /// </summary>
    public async Task<DocumentOperationResponse> SyncMasterAsync(string userName)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            // Purgar SOLO los master docs con Source="local" (NO tocar los del DMS Enterprise)
            var existingMaster = await db.Documents
                .Where(d => d.Scope == DocumentScope.Software)
                .ToListAsync();
            
            var localMasterDocs = existingMaster.Where(d => d.Source != "DMS_Enterprise").ToList();
            var dmsMasterDocs = existingMaster.Where(d => d.Source == "DMS_Enterprise").ToList();
            
            int purged = localMasterDocs.Count;
            int dmsPreserved = dmsMasterDocs.Count;
            db.Documents.RemoveRange(localMasterDocs);
            await db.SaveChangesAsync();
            _logger.LogInformation("🗑️ SyncMaster: {Purged} locales purgados, {Preserved} DMS_Enterprise preservados", purged, dmsPreserved);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            var globalDocsPath = Path.GetFullPath(GetGlobalDocsPath());
            int copied = 0, created = 0, dmsUpdated = 0;

            // ═══ PASO 1: Copiar ficheros del master al proyecto ═══
            var masterDestPath = Path.Combine(projectDocsPath, "AQSdocs_master");
            if (Directory.Exists(globalDocsPath))
            {
                _logger.LogInformation("🔄 SyncMaster: copiando de {Src} a {Dst}", globalDocsPath, masterDestPath);
                
                var masterFiles = Directory.GetFiles(globalDocsPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                    .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                    .Where(f => !Path.GetFileName(f).Equals("_dms_tree.json", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var srcFile in masterFiles)
                {
                    var relPath = Path.GetRelativePath(globalDocsPath, srcFile);
                    var dstFile = Path.Combine(masterDestPath, relPath);
                    
                    var dstDir = Path.GetDirectoryName(dstFile);
                    if (dstDir != null && !Directory.Exists(dstDir))
                        Directory.CreateDirectory(dstDir);
                    
                    bool shouldCopy = !File.Exists(dstFile);
                    if (!shouldCopy)
                    {
                        var srcBytes = await File.ReadAllBytesAsync(srcFile);
                        var dstBytes = await File.ReadAllBytesAsync(dstFile);
                        shouldCopy = ComputeSha256(srcBytes) != ComputeSha256(dstBytes);
                    }
                    
                    if (shouldCopy)
                    {
                        File.Copy(srcFile, dstFile, overwrite: true);
                        copied++;
                    }
                }
            }

            // ═══ PASO 2: Cargar todos los _dms_tree.json de AQSdocs_master para metadatos ═══
            var dmsMetadataByFile = new Dictionary<string, (DmsTreeDocument doc, DmsTreeCategory cat, DmsTreeSubcategory? subcat)>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(masterDestPath))
            {
                var treePaths = Directory.GetFiles(masterDestPath, "_dms_tree.json", SearchOption.AllDirectories);
                foreach (var treePath in treePaths)
                {
                    var tree = await ReadDmsTreeAsync(treePath);
                    if (tree != null)
                    {
                        var treeDir = Path.GetDirectoryName(treePath)!;
                        foreach (var dmsDoc in tree.Documents)
                        {
                            // La clave es la ruta relativa del archivo desde projectDocsPath
                            var fileAbsPath = Path.Combine(treeDir, dmsDoc.File);
                            if (File.Exists(fileAbsPath))
                            {
                                var relKey = Path.GetRelativePath(projectDocsPath, fileAbsPath).Replace('\\', '/');
                                dmsMetadataByFile[relKey] = (dmsDoc, tree.Category, tree.Subcategory);
                            }
                        }
                    }
                }
            }

            // ═══ PASO 3: Escanear AQSdocs_master/ y registrar ficheros en DB ═══
            // Indexar los DMS Enterprise docs existentes por FilePath para upsert
            var dmsExistingByPath = dmsMasterDocs.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);
            
            if (Directory.Exists(masterDestPath))
            {
                // Escanear todos los ficheros (no solo .md — el DMS puede publicar .pdf, .docx, etc.)
                var allFiles = Directory.GetFiles(masterDestPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                    .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                    .Where(f => !Path.GetFileName(f).Equals("_dms_tree.json", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var filePath in allFiles)
                {
                    var relativePath = Path.GetRelativePath(projectDocsPath, filePath).Replace('\\', '/');
                    var fileBytes = await File.ReadAllBytesAsync(filePath);
                    var hash = ComputeSha256(fileBytes);
                    var ext = Path.GetExtension(filePath).ToLower();

                    // ¿Tiene metadatos del DMS Enterprise?
                    var hasDmsMetadata = dmsMetadataByFile.TryGetValue(relativePath, out var dmsMeta);

                    if (hasDmsMetadata)
                    {
                        // Documento del DMS Enterprise → upsert con metadatos del _dms_tree.json
                        var category = await DetectCategoryFromDmsCodeAsync(db, dmsMeta.cat.Code, dmsMeta.cat.Name, dmsMeta.cat.Icon);
                        
                        if (dmsExistingByPath.TryGetValue(relativePath, out var existingDms))
                        {
                            // Actualizar doc DMS existente
                            existingDms.Title = dmsMeta.doc.Title;
                            existingDms.ContentHash = hash;
                            existingDms.FileSize = fileBytes.Length;
                            existingDms.FileType = DetectFileType(ext);
                            existingDms.Category = category;
                            existingDms.MinimumRole = DmsEnterpriseMappings.MapRole(dmsMeta.doc.MinimumRole, "SuperAdmin");
                            existingDms.Status = DmsEnterpriseMappings.MapStatus(dmsMeta.doc.Status);
                            existingDms.Version = dmsMeta.doc.Version;
                            existingDms.DocumentCode = dmsMeta.doc.Code;
                            existingDms.DmsSubcategoryCode = dmsMeta.subcat?.Code;
                            existingDms.DmsSubcategoryName = dmsMeta.subcat?.Name;
                            existingDms.DmsAuthor = dmsMeta.doc.Author;
                            existingDms.DmsPublishedAt = dmsMeta.doc.PublishedAt;
                            existingDms.UpdatedBy = userName;
                            existingDms.UpdatedAt = DateTime.UtcNow;
                            if (ext == ".md")
                            {
                                var content = Encoding.UTF8.GetString(fileBytes);
                                existingDms.SearchContent = ExtractSearchContent(content);
                            }
                            dmsUpdated++;
                        }
                        else
                        {
                            // Crear nuevo doc DMS
                            var title = dmsMeta.doc.Title;
                            if (string.IsNullOrWhiteSpace(title) && ext == ".md")
                                title = ExtractTitleFromContent(Encoding.UTF8.GetString(fileBytes)) ?? Path.GetFileNameWithoutExtension(filePath);

                            db.Documents.Add(new Document
                            {
                                Id = Guid.NewGuid().ToString(),
                                Slug = GenerateUniqueSlug(db, title ?? Path.GetFileNameWithoutExtension(filePath)),
                                Title = title ?? Path.GetFileNameWithoutExtension(filePath),
                                FilePath = relativePath,
                                FileType = DetectFileType(ext),
                                ContentHash = hash,
                                FileSize = fileBytes.Length,
                                Scope = DocumentScope.Software,
                                Category = category,
                                MinimumRole = DmsEnterpriseMappings.MapRole(dmsMeta.doc.MinimumRole, "SuperAdmin"),
                                Version = dmsMeta.doc.Version,
                                Status = DmsEnterpriseMappings.MapStatus(dmsMeta.doc.Status),
                                Source = "DMS_Enterprise",
                                DocumentCode = dmsMeta.doc.Code,
                                DmsSubcategoryCode = dmsMeta.subcat?.Code,
                                DmsSubcategoryName = dmsMeta.subcat?.Name,
                                DmsAuthor = dmsMeta.doc.Author,
                                DmsPublishedAt = dmsMeta.doc.PublishedAt,
                                CreatedBy = userName,
                                CreatedAt = DateTime.UtcNow,
                                SearchContent = ext == ".md" ? ExtractSearchContent(Encoding.UTF8.GetString(fileBytes)) : null
                            });
                            created++;
                        }
                    }
                    else if (ext == ".md")
                    {
                        // Documento local sin DMS → comportamiento original (solo .md)
                        var content = Encoding.UTF8.GetString(fileBytes);
                        var title = ExtractTitleFromContent(content) ?? Path.GetFileNameWithoutExtension(filePath);
                        var slug = GenerateUniqueSlug(db, title);
                        
                        db.Documents.Add(new Document
                        {
                            Id = Guid.NewGuid().ToString(),
                            Slug = slug,
                            Title = title,
                            FilePath = relativePath,
                            FileType = DocumentFileType.Markdown,
                            ContentHash = hash,
                            FileSize = fileBytes.Length,
                            Scope = DocumentScope.Software,
                            Category = SystemDocumentCategories.Other,
                            MinimumRole = "SuperAdmin",
                            Version = "1.0",
                            Status = DocumentStatus.Draft,
                            Source = "local",
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow,
                            SearchContent = ExtractSearchContent(content)
                        });
                        created++;
                    }
                    // Non-.md files without DMS metadata are skipped (no raw binaries without context)
                }
            }

            await db.SaveChangesAsync();

            var message = $"PURGE: {purged} locales eliminados, {dmsPreserved} DMS preservados → {created} creados, {dmsUpdated} DMS actualizados, {copied} copiados";
            _logger.LogInformation("📦 SyncMaster: {Message}", message);
            return new DocumentOperationResponse { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "";
            _logger.LogError(ex, "Error en SyncMaster: {Message} | Inner: {Inner}", ex.Message, innerMsg);
            return new DocumentOperationResponse { Success = false, Message = $"Error: {ex.Message} {innerMsg}" };
        }
    }

    /// <summary>
    /// Sincronizar AQSdocs_project: escanea la carpeta AQSdocs_project/,
    /// auto-crea categorías desde las carpetas (y subcarpetas), y registra docs en DB con scope=Project.
    /// Si existe _dms_tree.json en una carpeta, usa sus metadatos.
    /// Documentos DMS_Enterprise NO se eliminan como huérfanos si su archivo sigue existiendo.
    /// </summary>
    public async Task<DocumentOperationResponse> SyncProjectAsync(string userName)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            var projectScopePath = Path.Combine(projectDocsPath, "AQSdocs_project");
            if (!Directory.Exists(projectScopePath))
            {
                Directory.CreateDirectory(projectScopePath);
                _logger.LogInformation("📁 Creada carpeta AQSdocs_project en {Path}", projectScopePath);
            }

            int created = 0, updated = 0, orphaned = 0, catsCreated = 0, dmsUpdated = 0;

            // ═══ PASO 1: Auto-crear categorías desde carpetas ═══
            var allCategories = await db.DocumentCategories.ToListAsync();
            var catByFolder = allCategories.ToDictionary(c => c.FolderName?.ToLower() ?? "", c => c, StringComparer.OrdinalIgnoreCase);

            // Recorrer carpetas de primer nivel en AQSdocs_project/
            var topDirs = Directory.GetDirectories(projectScopePath);
            foreach (var topDir in topDirs)
            {
                var folderName = Path.GetFileName(topDir);
                if (!catByFolder.ContainsKey(folderName.ToLower()))
                {
                    // No existe categoría con este FolderName → crear
                    var maxId = allCategories.Count > 0 ? allCategories.Max(c => c.Id) : -1;
                    var newCat = new DocumentCategoryConfig
                    {
                        Id = maxId + 1,
                        Name = folderName.Replace("-", " ").Replace("_", " "),
                        FolderName = folderName,
                        Icon = "📁",
                        Color = "#6b7280",
                        SortOrder = maxId + 1,
                        IsSystem = false,
                        CreatedBy = userName,
                        CreatedAt = DateTime.UtcNow
                    };
                    // Capitalizar primera letra de cada palabra
                    newCat.Name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(newCat.Name);
                    
                    db.DocumentCategories.Add(newCat);
                    allCategories.Add(newCat);
                    catByFolder[folderName.ToLower()] = newCat;
                    catsCreated++;
                    _logger.LogInformation("🏷️ Categoría auto-creada desde carpeta: {Name} (folder: {Folder})", newCat.Name, folderName);
                }

                // Subcarpetas → subcategorías
                var parentCat = catByFolder[folderName.ToLower()];
                var subDirs = Directory.GetDirectories(topDir);
                foreach (var subDir in subDirs)
                {
                    var subFolderName = Path.GetFileName(subDir);
                    if (!catByFolder.ContainsKey(subFolderName.ToLower()))
                    {
                        var maxSubId = allCategories.Count > 0 ? allCategories.Max(c => c.Id) : -1;
                        var subCat = new DocumentCategoryConfig
                        {
                            Id = maxSubId + 1,
                            Name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                                subFolderName.Replace("-", " ").Replace("_", " ")),
                            FolderName = subFolderName,
                            Icon = "📂",
                            Color = parentCat.Color,
                            SortOrder = maxSubId + 1,
                            IsSystem = false,
                            ParentId = parentCat.Id,
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.DocumentCategories.Add(subCat);
                        allCategories.Add(subCat);
                        catByFolder[subFolderName.ToLower()] = subCat;
                        catsCreated++;
                        _logger.LogInformation("🏷️ Subcategoría auto-creada: {Name} (parent: {Parent})", subCat.Name, parentCat.Name);
                    }
                }
            }

            if (catsCreated > 0)
                await db.SaveChangesAsync(); // Guardar categorías antes de asignarlas a docs

            // ═══ PASO 2: Cargar todos los _dms_tree.json de AQSdocs_project para metadatos ═══
            var dmsMetadataByFile = new Dictionary<string, (DmsTreeDocument doc, DmsTreeCategory cat, DmsTreeSubcategory? subcat)>(StringComparer.OrdinalIgnoreCase);
            var treePaths = Directory.GetFiles(projectScopePath, "_dms_tree.json", SearchOption.AllDirectories);
            foreach (var treePath in treePaths)
            {
                var tree = await ReadDmsTreeAsync(treePath);
                if (tree != null)
                {
                    var treeDir = Path.GetDirectoryName(treePath)!;
                    foreach (var dmsDoc in tree.Documents)
                    {
                        var fileAbsPath = Path.Combine(treeDir, dmsDoc.File);
                        if (File.Exists(fileAbsPath))
                        {
                            var relKey = Path.GetRelativePath(projectDocsPath, fileAbsPath).Replace('\\', '/');
                            dmsMetadataByFile[relKey] = (dmsDoc, tree.Category, tree.Subcategory);
                        }
                    }
                }
            }

            // ═══ PASO 3: Escanear ficheros dentro de AQSdocs_project/ ═══
            var existingProjectDocs = await db.Documents
                .Where(d => d.Scope == DocumentScope.Project)
                .ToListAsync();
            var existingByPath = existingProjectDocs.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);

            // Escanear TODOS los ficheros (no solo .md — el DMS puede publicar .pdf, .docx, etc.)
            var allFiles = Directory.GetFiles(projectScopePath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                .Where(f => !Path.GetFileName(f).Equals("_dms_tree.json", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(projectDocsPath, filePath).Replace('\\', '/');
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var hash = ComputeSha256(fileBytes);
                var ext = Path.GetExtension(filePath).ToLower();

                var hasDmsMetadata = dmsMetadataByFile.TryGetValue(relativePath, out var dmsMeta);

                if (existingByPath.TryGetValue(relativePath, out var existing))
                {
                    // Documento ya existe → actualizar
                    bool changed = existing.ContentHash != hash;
                    
                    if (hasDmsMetadata)
                    {
                        // Actualizar con metadatos DMS
                        var category = await DetectCategoryFromDmsCodeAsync(db, dmsMeta.cat.Code, dmsMeta.cat.Name, dmsMeta.cat.Icon);
                        existing.Title = dmsMeta.doc.Title;
                        existing.ContentHash = hash;
                        existing.FileSize = fileBytes.Length;
                        existing.FileType = DetectFileType(ext);
                        existing.Category = category;
                        existing.MinimumRole = DmsEnterpriseMappings.MapRole(dmsMeta.doc.MinimumRole, existing.MinimumRole ?? "Operator");
                        existing.Status = DmsEnterpriseMappings.MapStatus(dmsMeta.doc.Status);
                        existing.Version = dmsMeta.doc.Version;
                        existing.Source = "DMS_Enterprise";
                        existing.DocumentCode = dmsMeta.doc.Code;
                        existing.DmsSubcategoryCode = dmsMeta.subcat?.Code;
                        existing.DmsSubcategoryName = dmsMeta.subcat?.Name;
                        existing.DmsAuthor = dmsMeta.doc.Author;
                        existing.DmsPublishedAt = dmsMeta.doc.PublishedAt;
                        existing.Scope = DocumentScope.Project;
                        existing.UpdatedBy = userName;
                        existing.UpdatedAt = DateTime.UtcNow;
                        if (ext == ".md")
                            existing.SearchContent = ExtractSearchContent(Encoding.UTF8.GetString(fileBytes));
                        dmsUpdated++;
                    }
                    else if (changed || existing.Category == null)
                    {
                        // Actualizar doc local (solo si cambió hash o no tiene categoría)
                        var detectedCategory = await DetectCategoryFromPathAsync(db, relativePath);
                        existing.ContentHash = hash;
                        existing.FileSize = fileBytes.Length;
                        existing.Category = detectedCategory;
                        existing.Scope = DocumentScope.Project;
                        existing.UpdatedBy = userName;
                        existing.UpdatedAt = DateTime.UtcNow;
                        if (ext == ".md")
                            existing.SearchContent = ExtractSearchContent(Encoding.UTF8.GetString(fileBytes));
                        updated++;
                    }
                    existingByPath.Remove(relativePath);
                }
                else
                {
                    // Documento nuevo
                    if (hasDmsMetadata)
                    {
                        // Nuevo doc DMS Enterprise
                        var category = await DetectCategoryFromDmsCodeAsync(db, dmsMeta.cat.Code, dmsMeta.cat.Name, dmsMeta.cat.Icon);
                        var title = dmsMeta.doc.Title;
                        if (string.IsNullOrWhiteSpace(title) && ext == ".md")
                            title = ExtractTitleFromContent(Encoding.UTF8.GetString(fileBytes)) ?? Path.GetFileNameWithoutExtension(filePath);

                        db.Documents.Add(new Document
                        {
                            Id = Guid.NewGuid().ToString(),
                            Slug = GenerateUniqueSlug(db, title ?? Path.GetFileNameWithoutExtension(filePath)),
                            Title = title ?? Path.GetFileNameWithoutExtension(filePath),
                            FilePath = relativePath,
                            FileType = DetectFileType(ext),
                            ContentHash = hash,
                            FileSize = fileBytes.Length,
                            Scope = DocumentScope.Project,
                            Category = category,
                            MinimumRole = DmsEnterpriseMappings.MapRole(dmsMeta.doc.MinimumRole, "Operator"),
                            Version = dmsMeta.doc.Version,
                            Status = DmsEnterpriseMappings.MapStatus(dmsMeta.doc.Status),
                            Source = "DMS_Enterprise",
                            DocumentCode = dmsMeta.doc.Code,
                            DmsSubcategoryCode = dmsMeta.subcat?.Code,
                            DmsSubcategoryName = dmsMeta.subcat?.Name,
                            DmsAuthor = dmsMeta.doc.Author,
                            DmsPublishedAt = dmsMeta.doc.PublishedAt,
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow,
                            SearchContent = ext == ".md" ? ExtractSearchContent(Encoding.UTF8.GetString(fileBytes)) : null
                        });
                        created++;
                    }
                    else if (ext == ".md")
                    {
                        // Nuevo doc local (solo .md sin DMS)
                        var category = await DetectCategoryFromPathAsync(db, relativePath);
                        var content = Encoding.UTF8.GetString(fileBytes);
                        var title = ExtractTitleFromContent(content) ?? Path.GetFileNameWithoutExtension(filePath);
                        var slug = GenerateUniqueSlug(db, title);

                        db.Documents.Add(new Document
                        {
                            Id = Guid.NewGuid().ToString(),
                            Slug = slug,
                            Title = title,
                            FilePath = relativePath,
                            FileType = DocumentFileType.Markdown,
                            ContentHash = hash,
                            FileSize = fileBytes.Length,
                            Scope = DocumentScope.Project,
                            Category = category,
                            Version = "1.0",
                            Status = DocumentStatus.Draft,
                            Source = "local",
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow,
                            SearchContent = ExtractSearchContent(content)
                        });
                        created++;
                    }
                    // Non-.md files without DMS metadata are skipped
                }
            }

            // Eliminar huérfanos project — PERO NO los DMS Enterprise cuyo archivo sigue en disco
            foreach (var orphan in existingByPath.Values)
            {
                if (orphan.Source == "DMS_Enterprise")
                {
                    // Verificar si el archivo ya no existe en disco
                    var absPath = Path.Combine(projectDocsPath, orphan.FilePath.Replace('/', '\\'));
                    if (File.Exists(absPath))
                        continue; // El archivo existe → preservar (el DMS lo mantiene)
                }
                db.Documents.Remove(orphan);
                orphaned++;
            }

            await db.SaveChangesAsync();

            var message = $"{created} creados, {updated} actualizados, {dmsUpdated} DMS actualizados, {orphaned} eliminados, {catsCreated} categorías creadas";
            _logger.LogInformation("📦 SyncProject: {Message}", message);
            return new DocumentOperationResponse { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "";
            _logger.LogError(ex, "Error en SyncProject: {Message} | Inner: {Inner}", ex.Message, innerMsg);
            return new DocumentOperationResponse { Success = false, Message = $"Error: {ex.Message} {innerMsg}" };
        }
    }

    /// <summary>
    /// Notificación push del DMS Enterprise: upsert directo de un documento.
    /// Se espera que el DMS ya haya copiado el archivo al filesystem antes de llamar.
    /// </summary>
    public async Task<DocumentOperationResponse> ProcessDmsNotifyAsync(DmsPublishNotifyRequest request, string userName)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            // Determinar scope y ruta
            var folder = request.Folder?.Trim('/') ?? "AQSdocs_project";
            var scope = folder.StartsWith("AQSdocs_master", StringComparison.OrdinalIgnoreCase)
                ? DocumentScope.Software
                : DocumentScope.Project;

            // Construir ruta relativa: {folder}/{file}
            var relativePath = $"{folder}/{request.File}".Replace('\\', '/');
            var absolutePath = Path.Combine(projectDocsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Verificar que el archivo existe en disco
            if (!File.Exists(absolutePath))
            {
                return new DocumentOperationResponse
                {
                    Success = false,
                    Message = $"El archivo no existe en disco: {relativePath}. El DMS Enterprise debe copiar el archivo ANTES de notificar."
                };
            }

            var fileBytes = await File.ReadAllBytesAsync(absolutePath);
            var hash = ComputeSha256(fileBytes);
            var ext = Path.GetExtension(absolutePath).ToLower();

            // Detectar/crear categoría desde el código DMS
            var categoryId = !string.IsNullOrWhiteSpace(request.CategoryCode)
                ? await DetectCategoryFromDmsCodeAsync(db, request.CategoryCode, request.CategoryName ?? request.CategoryCode, null)
                : SystemDocumentCategories.Other;

            // Mapear rol y estado
            var minimumRole = DmsEnterpriseMappings.MapRole(request.MinimumRole, scope == DocumentScope.Software ? "SuperAdmin" : "Operator");
            var status = DmsEnterpriseMappings.MapStatus(request.Status);

            // Buscar documento existente por FilePath o DocumentCode
            var existing = await db.Documents.FirstOrDefaultAsync(d => d.FilePath == relativePath);
            if (existing == null && !string.IsNullOrWhiteSpace(request.DocumentCode))
                existing = await db.Documents.FirstOrDefaultAsync(d => d.DocumentCode == request.DocumentCode && d.Source == "DMS_Enterprise");

            if (existing != null)
            {
                // Actualizar
                existing.Title = request.Title ?? existing.Title;
                existing.FilePath = relativePath;
                existing.ContentHash = hash;
                existing.FileSize = fileBytes.Length;
                existing.FileType = DetectFileType(ext);
                existing.Category = categoryId;
                existing.MinimumRole = minimumRole;
                existing.Status = status;
                existing.Version = request.Version ?? existing.Version;
                existing.Source = request.Source ?? "DMS_Enterprise";
                existing.DocumentCode = request.DocumentCode ?? existing.DocumentCode;
                existing.DmsSubcategoryCode = request.SubcategoryCode;
                existing.DmsSubcategoryName = request.SubcategoryName;
                existing.DmsAuthor = request.Author;
                existing.DmsPublishedAt = request.PublishedAt;
                existing.Scope = scope;
                existing.UpdatedBy = userName;
                existing.UpdatedAt = DateTime.UtcNow;
                if (ext == ".md")
                    existing.SearchContent = ExtractSearchContent(Encoding.UTF8.GetString(fileBytes));

                await db.SaveChangesAsync();
                _logger.LogInformation("📤 DMS Notify: actualizado '{Title}' ({Code})", existing.Title, existing.DocumentCode);
                return new DocumentOperationResponse { Success = true, Message = $"Documento actualizado: {existing.Title}" };
            }
            else
            {
                // Crear nuevo
                var title = request.Title;
                if (string.IsNullOrWhiteSpace(title) && ext == ".md")
                    title = ExtractTitleFromContent(Encoding.UTF8.GetString(fileBytes));
                title ??= Path.GetFileNameWithoutExtension(request.File);

                var newDoc = new Document
                {
                    Id = Guid.NewGuid().ToString(),
                    Slug = GenerateUniqueSlug(db, title),
                    Title = title,
                    FilePath = relativePath,
                    FileType = DetectFileType(ext),
                    ContentHash = hash,
                    FileSize = fileBytes.Length,
                    Scope = scope,
                    Category = categoryId,
                    MinimumRole = minimumRole,
                    Version = request.Version ?? "1.0",
                    Status = status,
                    Source = request.Source ?? "DMS_Enterprise",
                    DocumentCode = request.DocumentCode,
                    DmsSubcategoryCode = request.SubcategoryCode,
                    DmsSubcategoryName = request.SubcategoryName,
                    DmsAuthor = request.Author,
                    DmsPublishedAt = request.PublishedAt,
                    CreatedBy = userName,
                    CreatedAt = DateTime.UtcNow,
                    SearchContent = ext == ".md" ? ExtractSearchContent(Encoding.UTF8.GetString(fileBytes)) : null
                };

                db.Documents.Add(newDoc);
                await db.SaveChangesAsync();
                _logger.LogInformation("📤 DMS Notify: creado '{Title}' ({Code})", newDoc.Title, newDoc.DocumentCode);
                return new DocumentOperationResponse { Success = true, Message = $"Documento creado: {newDoc.Title}" };
            }
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "";
            _logger.LogError(ex, "Error en ProcessDmsNotify: {Message} | Inner: {Inner}", ex.Message, innerMsg);
            return new DocumentOperationResponse { Success = false, Message = $"Error: {ex.Message} {innerMsg}" };
        }
    }

    #endregion

    #region Historial

    public async Task<List<DocumentHistory>> GetDocumentHistoryAsync(string documentId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentHistories
                .Where(h => h.DocumentId == documentId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo historial de {DocumentId}", documentId);
            return new List<DocumentHistory>();
        }
    }

    // === Categorías dinámicas ===

    public async Task<List<DocumentCategoryConfig>> GetCategoriesAsync()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categorías");
            return new List<DocumentCategoryConfig>();
        }
    }

    public async Task<DocumentCategoryConfig?> GetCategoryByIdAsync(int id)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentCategories.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categoría {Id}", id);
            return null;
        }
    }

    public async Task<DocumentCategoryConfig> CreateCategoryAsync(DocumentCategoryConfig category, string userName)
    {
        using var db = _dbFactory.CreateDbContext();
        
        // Asignar siguiente ID disponible
        var maxId = await db.DocumentCategories.MaxAsync(c => (int?)c.Id) ?? -1;
        category.Id = maxId + 1;
        category.IsSystem = false;
        category.CreatedBy = userName;
        category.CreatedAt = DateTime.UtcNow;
        
        // Validar ParentId si se proporcionó
        if (category.ParentId.HasValue)
        {
            var parent = await db.DocumentCategories.FindAsync(category.ParentId.Value);
            if (parent == null)
                throw new ArgumentException($"Categoría padre {category.ParentId.Value} no existe");
        }
        
        // Generar FolderName (slug) a partir del nombre
        if (string.IsNullOrWhiteSpace(category.FolderName))
        {
            category.FolderName = GenerateFolderName(category.Name);
        }

        db.DocumentCategories.Add(category);
        await db.SaveChangesAsync();
        
        // Crear carpeta física en docs/ del proyecto
        try
        {
            await EnsureCategoryFolderAsync(db, category.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo crear la carpeta para categoría {Name}, se creará al guardar el primer documento", category.Name);
        }
        
        _logger.LogInformation("📁 Categoría creada: {Name} (ID: {Id}, Folder: {Folder}, Parent: {ParentId}) por {User}", 
            category.Name, category.Id, category.FolderName, category.ParentId, userName);
        return category;
    }

    public async Task<DocumentCategoryConfig?> UpdateCategoryAsync(int id, DocumentCategoryConfig updated, string userName)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.DocumentCategories.FindAsync(id);
        if (existing == null) return null;

        // Detectar si cambió ParentId o FolderName → necesitamos mover carpeta + actualizar FilePaths
        var parentChanged = existing.ParentId != updated.ParentId;
        var folderNameChanged = !existing.IsSystem && !string.IsNullOrWhiteSpace(updated.FolderName) && existing.FolderName != updated.FolderName;
        
        // Calcular ruta VIEJA antes de cambiar nada en DB
        string oldFolderPath = "";
        if (parentChanged || folderNameChanged)
        {
            oldFolderPath = await BuildCategoryFolderPathAsync(db, id);
        }

        // Actualizar campos permitidos
        existing.Name = updated.Name;
        existing.Icon = updated.Icon;
        existing.Color = updated.Color;
        existing.Description = updated.Description;
        existing.SortOrder = updated.SortOrder;
        existing.ParentId = updated.ParentId;
        existing.DefaultClassificationId = updated.DefaultClassificationId;
        existing.DefaultMinimumRole = updated.DefaultMinimumRole;
        
        // Solo permitir cambiar FolderName si no es categoría del sistema
        if (!existing.IsSystem && !string.IsNullOrWhiteSpace(updated.FolderName))
        {
            existing.FolderName = updated.FolderName;
        }

        await db.SaveChangesAsync();

        // Si cambió la jerarquía, reorganizar carpetas y ficheros
        if (parentChanged || folderNameChanged)
        {
            await ReorganizeCategoryFilesAsync(db, id, oldFolderPath);
        }

        _logger.LogInformation("Categoría actualizada: {Name} (ID: {Id}, Parent: {Parent}) por {User}", 
            existing.Name, id, existing.ParentId, userName);
        return existing;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        var category = await db.DocumentCategories.FindAsync(id);
        if (category == null) return false;
        
        // No permitir eliminar la categoría "Otros" (7) que es el destino de reasignación
        if (id == SystemDocumentCategories.Other)
        {
            _logger.LogWarning("No se puede eliminar la categoría 'Otros' (ID: {Id}) porque es el destino de reasignación", id);
            return false;
        }

        // Reasignar documentos de esta categoría a "Otros" (7)
        var docsToReassign = await db.Documents.Where(d => d.Category == id).ToListAsync();
        foreach (var doc in docsToReassign)
        {
            doc.Category = SystemDocumentCategories.Other;
        }

        // Eliminar también todas las subcategorías hijas y reasignar sus documentos
        var childCategories = await db.DocumentCategories.Where(c => c.ParentId == id).ToListAsync();
        foreach (var child in childCategories)
        {
            var childDocs = await db.Documents.Where(d => d.Category == child.Id).ToListAsync();
            foreach (var childDoc in childDocs)
            {
                childDoc.Category = SystemDocumentCategories.Other;
            }
            docsToReassign.AddRange(childDocs);
            db.DocumentCategories.Remove(child);
        }

        db.DocumentCategories.Remove(category);
        await db.SaveChangesAsync();
        
        _logger.LogInformation("Categoría eliminada: {Name} (ID: {Id}), {Count} documentos reasignados", 
            category.Name, id, docsToReassign.Count);
        return true;
    }

    #endregion

    #region Helpers de carpetas (Categoría = Carpeta)

    /// <summary>
    /// Construye la ruta completa de carpeta para una categoría, recorriendo la jerarquía de padres.
    /// Ej: Si "Planos Eléctricos" (folder: planos-electricos) es hijo de "Planos" (folder: planos)
    ///     → devuelve "planos/planos-electricos"
    /// </summary>
    private async Task<string> BuildCategoryFolderPathAsync(AquafrischDbContext db, int categoryId)
    {
        var segments = new List<string>();
        var visited = new HashSet<int>(); // Protección contra ciclos
        var currentId = (int?)categoryId;

        while (currentId.HasValue)
        {
            if (!visited.Add(currentId.Value)) break; // Ciclo detectado
            
            var cat = await db.DocumentCategories.FindAsync(currentId.Value);
            if (cat == null) break;

            if (!string.IsNullOrWhiteSpace(cat.FolderName))
                segments.Insert(0, cat.FolderName);
            
            currentId = cat.ParentId;
        }

        return string.Join("/", segments);
    }

    /// <summary>
    /// Genera un FolderName (slug) a partir del nombre de categoría.
    /// </summary>
    private static string GenerateFolderName(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("ñ", "n").Replace("ü", "u")
            .Replace(".", "").Replace(",", "").Replace("(", "").Replace(")", "")
            .Replace(":", "").Replace(";", "");
    }

    /// <summary>
    /// Asegura que la carpeta física para una categoría existe en docs/ del proyecto.
    /// Las categorías custom (no system) se crean bajo AQSdocs_project/.
    /// Las categorías del sistema ya existen bajo AQSdocs_master/.
    /// </summary>
    private async Task EnsureCategoryFolderAsync(AquafrischDbContext db, int categoryId)
    {
        var folderPath = await BuildCategoryFolderPathAsync(db, categoryId);
        if (string.IsNullOrEmpty(folderPath)) return;

        var docsPath = _requestContext.DocsPath;
        if (string.IsNullOrEmpty(docsPath)) return;

        // Categorías custom van bajo AQSdocs_project/
        var cat = await db.DocumentCategories.FindAsync(categoryId);
        var scopePrefix = (cat != null && !cat.IsSystem) ? "AQSdocs_project" : "";
        
        var fullPath = string.IsNullOrEmpty(scopePrefix)
            ? Path.Combine(docsPath, folderPath.Replace('/', Path.DirectorySeparatorChar))
            : Path.Combine(docsPath, scopePrefix, folderPath.Replace('/', Path.DirectorySeparatorChar));
        
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            _logger.LogInformation("📁 Carpeta creada para categoría: {Path}", fullPath);
        }
    }

    /// <summary>
    /// Reorganiza las carpetas y ficheros cuando una categoría cambia de padre o de FolderName.
    /// 1. Calcula la nueva ruta de carpeta
    /// 2. Mueve los ficheros de la carpeta vieja a la nueva
    /// 3. Actualiza FilePath en la DB para todos los documentos afectados (categoría + subcategorías)
    /// 4. Limpia la carpeta vieja si quedó vacía
    /// </summary>
    private async Task ReorganizeCategoryFilesAsync(AquafrischDbContext db, int categoryId, string oldFolderPath)
    {
        try
        {
            var docsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(docsPath)) return;

            // Calcular nueva ruta con la jerarquía ya actualizada en DB
            var newFolderPath = await BuildCategoryFolderPathAsync(db, categoryId);
            
            _logger.LogDebug("📦 Reorganizando categoría {Id}: '{OldPath}' → '{NewPath}'", categoryId, oldFolderPath, newFolderPath);

            // Si las rutas son iguales, no hay nada que mover
            if (oldFolderPath == newFolderPath) return;

            const string scopePrefix = "AQSdocs_project";
            
            // Crear la nueva carpeta
            if (!string.IsNullOrEmpty(newFolderPath))
            {
                var newAbsPath = Path.Combine(docsPath, scopePrefix, newFolderPath.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(newAbsPath))
                    Directory.CreateDirectory(newAbsPath);
            }

            // Mover ficheros de esta categoría
            await MoveDocumentsForCategoryAsync(db, categoryId, oldFolderPath, newFolderPath, scopePrefix, docsPath);

            // Mover ficheros de subcategorías hijas (recursivo)
            var childCategories = await db.DocumentCategories.Where(c => c.ParentId == categoryId).ToListAsync();
            foreach (var child in childCategories)
            {
                // La ruta vieja de la hija era: oldFolderPath/childFolderName
                var childOldPath = string.IsNullOrEmpty(oldFolderPath) 
                    ? child.FolderName 
                    : $"{oldFolderPath}/{child.FolderName}";
                await ReorganizeCategoryFilesAsync(db, child.Id, childOldPath);
            }

            // Limpiar carpeta vieja si quedó vacía
            if (!string.IsNullOrEmpty(oldFolderPath))
            {
                var oldAbsPath = Path.Combine(docsPath, scopePrefix, oldFolderPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(oldAbsPath) && !Directory.EnumerateFileSystemEntries(oldAbsPath).Any())
                {
                    Directory.Delete(oldAbsPath, false);
                    _logger.LogInformation("🗑️ Carpeta vacía eliminada: {Path}", oldAbsPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Error reorganizando ficheros de categoría {Id}. Los documentos pueden necesitar re-sync.", categoryId);
        }
    }

    /// <summary>
    /// Mueve los ficheros físicos de una categoría y actualiza su FilePath en DB.
    /// </summary>
    private async Task MoveDocumentsForCategoryAsync(AquafrischDbContext db, int categoryId, 
        string oldFolderPath, string newFolderPath, string scopePrefix, string docsPath)
    {
        var docs = await db.Documents.Where(d => d.Category == categoryId && d.Scope == DocumentScope.Project).ToListAsync();
        
        foreach (var doc in docs)
        {
            try
            {
                // Construir nueva ruta relativa
                var fileName = Path.GetFileName(doc.FilePath);
                var newRelativePath = string.IsNullOrEmpty(newFolderPath)
                    ? $"{scopePrefix}/{fileName}"
                    : $"{scopePrefix}/{newFolderPath}/{fileName}";

                var oldAbsPath = Path.Combine(docsPath, doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
                var newAbsPath = Path.Combine(docsPath, newRelativePath.Replace('/', Path.DirectorySeparatorChar));

                // Crear directorio destino si no existe
                var destDir = Path.GetDirectoryName(newAbsPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                // Mover fichero físico
                if (File.Exists(oldAbsPath) && oldAbsPath != newAbsPath)
                {
                    File.Move(oldAbsPath, newAbsPath, overwrite: false);
                    _logger.LogInformation("📄 Movido: {Old} → {New}", doc.FilePath, newRelativePath);
                }

                // Actualizar ruta en DB
                doc.FilePath = newRelativePath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ No se pudo mover fichero de doc {Id}: {Path}", doc.Id, doc.FilePath);
            }
        }

        await db.SaveChangesAsync();
    }

    #endregion

    #region Helpers privados

    private async Task<DocumentDetail> BuildDocumentDetail(Document doc)
    {
        var detail = new DocumentDetail
        {
            Id = doc.Id,
            Slug = doc.Slug,
            Title = doc.Title,
            Description = doc.Description,
            FilePath = doc.FilePath,
            FileType = doc.FileType,
            Scope = doc.Scope,
            Category = doc.Category,
            SubCategory = doc.SubCategory,
            Tags = ParseTags(doc.Tags),
            ClassificationId = doc.ClassificationId,
            AccessLevel = doc.AccessLevel,
            MinimumRole = doc.MinimumRole,
            Version = doc.Version,
            Status = doc.Status,
            CraRelevant = doc.CraRelevant,
            CraArticle = doc.CraArticle,
            Iso27001Relevant = doc.Iso27001Relevant,
            Iso27001Article = doc.Iso27001Article,
            Iec62443Relevant = doc.Iec62443Relevant,
            Iec62443Article = doc.Iec62443Article,
            CraDeadline = doc.CraDeadline,
            CreatedBy = doc.CreatedBy,
            CreatedAt = doc.CreatedAt,
            UpdatedBy = doc.UpdatedBy,
            UpdatedAt = doc.UpdatedAt,
            FileSize = doc.FileSize,
            ContentHash = doc.ContentHash,
            ApprovedBy = doc.ApprovedBy,
            ApprovedAt = doc.ApprovedAt,
            ParentDocId = doc.ParentDocId,
            RelatedDocIds = doc.RelatedDocIds != null 
                ? JsonSerializer.Deserialize<List<string>>(doc.RelatedDocIds) 
                : null
        };

        // Leer contenido del fichero (proyecto activo → global fallback)
        try
        {
            var absolutePath = ResolveDocFilePath(doc.FilePath);
            if (absolutePath != null)
            {
                switch (doc.FileType)
                {
                    case DocumentFileType.Markdown:
                    case DocumentFileType.Json:
                    case DocumentFileType.Other:
                        // Ficheros de texto: leer y renderizar como Markdown/HTML
                        detail.RawContent = await File.ReadAllTextAsync(absolutePath, Encoding.UTF8);
                        detail.HtmlContent = RenderMarkdownToHtml(detail.RawContent);
                        break;

                    case DocumentFileType.Docx:
                        // DOCX: convertir a HTML para previsualización inline
                        detail.HtmlContent = _exportService.ConvertDocxToHtml(absolutePath);
                        detail.RawContent = null; // binario — no tiene rawContent
                        break;

                    case DocumentFileType.Pdf:
                    case DocumentFileType.Image:
                        // PDF/Image: no leer contenido — el frontend usa el endpoint de descarga
                        detail.HtmlContent = null;
                        detail.RawContent = null;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el contenido de {FilePath}", doc.FilePath);
        }

        return detail;
    }

    private static DocumentInfo MapToInfo(Document doc) => new()
    {
        Id = doc.Id,
        Slug = doc.Slug,
        Title = doc.Title,
        Description = doc.Description,
        FilePath = doc.FilePath,
        FileType = doc.FileType,
        Scope = doc.Scope,
        Category = doc.Category,
        SubCategory = doc.SubCategory,
        Tags = ParseTags(doc.Tags),
        ClassificationId = doc.ClassificationId,
        AccessLevel = doc.AccessLevel,
        MinimumRole = doc.MinimumRole,
        Version = doc.Version,
        Status = doc.Status,
        CraRelevant = doc.CraRelevant,
        CraArticle = doc.CraArticle,
        Iso27001Relevant = doc.Iso27001Relevant,
        Iso27001Article = doc.Iso27001Article,
        Iec62443Relevant = doc.Iec62443Relevant,
        Iec62443Article = doc.Iec62443Article,
        CraDeadline = doc.CraDeadline,
        CreatedBy = doc.CreatedBy,
        CreatedAt = doc.CreatedAt,
        UpdatedBy = doc.UpdatedBy,
        UpdatedAt = doc.UpdatedAt,
        FileSize = doc.FileSize
    };

    /// <summary>
    /// Filtrar documentos usando la matriz de acceso categoría×rol (ISO 27001 A.9.1).
    /// Usa la tabla DocumentCategoryAccess para determinar qué categorías puede ver el rol.
    /// </summary>
    private async Task<IQueryable<Document>> ApplyAccessFilterWithMatrixAsync(
        AquafrischDbContext db, IQueryable<Document> query, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        
        // SuperAdmin ve todo
        if (roleLevel >= 5) return query;

        // Normalizar el rol del JWT (inglés) al nombre de la Matriz (español)
        var matrixRole = NormalizeRoleForMatrix(userRole);

        // Obtener las categorías que este rol puede leer
        var allowedCategoryIds = await db.DocumentCategoryAccess
            .Where(a => a.RoleName == matrixRole && a.CanRead)
            .Select(a => a.CategoryId)
            .ToListAsync();

        _logger.LogInformation(
            "[Matriz Acceso] JWT role='{JwtRole}' → matrix='{MatrixRole}' → {Count} categorías permitidas: [{Ids}]",
            userRole, matrixRole, allowedCategoryIds.Count, string.Join(", ", allowedCategoryIds));

        // Filtrar: solo documentos de categorías explícitamente permitidas en la Matriz.
        // Si el rol no tiene NINGUNA categoría con CanRead=true, no ve nada (solo SuperAdmin ve todo).
        // Además filtrar por MinimumRole: excluir docs cuyo rol mínimo es superior al nivel del usuario.
        // Construir lista de roles que el usuario NO puede ver (nivel superior al suyo)
        var excludedRoles = RoleHierarchy
            .Where(kv => kv.Value > roleLevel)
            .Select(kv => kv.Key)
            .ToList();

        return query
            .Where(d => allowedCategoryIds.Contains(d.Category))
            .Where(d => string.IsNullOrEmpty(d.MinimumRole) || !excludedRoles.Contains(d.MinimumRole));
    }

    private bool HasAccessToDocument(Document doc, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        if (roleLevel >= 5) return true; // SuperAdmin

        // Verificar MinimumRole del documento (default: Visualizador si no está configurado)
        var requiredRole = !string.IsNullOrEmpty(doc.MinimumRole) ? doc.MinimumRole : "Visualizador";
        var requiredLevel = RoleHierarchy.GetValueOrDefault(requiredRole, 0);
        return roleLevel >= requiredLevel;
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLower()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("ñ", "n").Replace("ü", "u");
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

    /// <summary>
    /// Genera un slug único, añadiendo sufijo numérico si ya existe en DB o en el ChangeTracker (inserts pendientes)
    /// </summary>
    private static string GenerateUniqueSlug(AquafrischDbContext db, string title)
    {
        var baseSlug = GenerateSlug(title);
        var slug = baseSlug;
        var counter = 1;

        while (db.Documents.Any(d => d.Slug == slug) ||
               db.ChangeTracker.Entries<Document>().Any(e => e.Entity.Slug == slug && e.State == EntityState.Added))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }

    private static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLower();
    }

    private static List<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string ExtractSearchContent(string markdownContent)
    {
        // Limpiar Markdown para indexar solo texto
        var text = Regex.Replace(markdownContent, @"```[\s\S]*?```", " "); // code blocks
        text = Regex.Replace(text, @"[#*_~`\[\]\(\)>|]", " ");           // markdown syntax
        text = Regex.Replace(text, @"\s+", " ");                          // whitespace
        return text.Trim();
    }

    private static string? ExtractTitleFromContent(string content)
    {
        // Buscar primer heading H1
        var match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Detecta la categoría de un documento a partir de su ruta relativa.
    /// Busca primero en la DB (FolderName de categorías), luego fallback estático para carpetas legacy del master.
    /// </summary>
    private async Task<int> DetectCategoryFromPathAsync(AquafrischDbContext db, string relativePath)
    {
        var parts = relativePath.Split('/');
        
        // Saltar prefijo AQSdocs_master/ o AQSdocs_project/ si existe
        int startIdx = 0;
        if (parts.Length > 0 && (parts[0].Equals("AQSdocs_master", StringComparison.OrdinalIgnoreCase) 
            || parts[0].Equals("AQSdocs_project", StringComparison.OrdinalIgnoreCase)))
        {
            startIdx = 1;
        }
        
        if (parts.Length < startIdx + 2) return SystemDocumentCategories.Other;

        var folderName = parts[startIdx];
        
        // 1. Buscar en DB: ¿hay una categoría cuyo FolderName coincida?
        var allCategories = await db.DocumentCategories.ToListAsync();
        
        // Intentar match exacto por FolderName
        var matchedCat = allCategories.FirstOrDefault(c => 
            string.Equals(c.FolderName, folderName, StringComparison.OrdinalIgnoreCase));
        
        if (matchedCat != null) return matchedCat.Id;
        
        // Si hay subcarpeta, intentar match jerárquico (padre/hijo)
        if (parts.Length >= startIdx + 3)
        {
            var subFolderName = parts[startIdx + 1];
            // Buscar subcategoría con ese FolderName
            var matchedSub = allCategories.FirstOrDefault(c => 
                string.Equals(c.FolderName, subFolderName, StringComparison.OrdinalIgnoreCase) && c.ParentId != null);
            if (matchedSub != null) return matchedSub.Id;
        }

        // 2. Fallback estático para carpetas legacy del master que no tienen su propia categoría en DB
        return folderName.ToLower() switch
        {
            "architecture" => SystemDocumentCategories.Technical,
            "configuration" => SystemDocumentCategories.Technical,
            "excel configuration" => SystemDocumentCategories.Technical,
            "development" => SystemDocumentCategories.Technical,
            "deployment" => SystemDocumentCategories.Maintenance,
            "changelog" => SystemDocumentCategories.Other,
            "user-guide" => SystemDocumentCategories.UserGuide,
            "especificaciones_clientes" => SystemDocumentCategories.Compliance,
            "presentacion" => SystemDocumentCategories.Other,
            _ => SystemDocumentCategories.Other
        };
    }

    /// <summary>
    /// Lee y parsea un _dms_tree.json de disco. Devuelve null si no existe o falla el parsing.
    /// </summary>
    private async Task<DmsTree?> ReadDmsTreeAsync(string treePath)
    {
        try
        {
            if (!File.Exists(treePath)) return null;
            var json = await File.ReadAllTextAsync(treePath, Encoding.UTF8);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var tree = JsonSerializer.Deserialize<DmsTree>(json, options);
            if (tree?.Category == null || tree.Documents == null || tree.Documents.Count == 0)
            {
                _logger.LogWarning("⚠️ _dms_tree.json inválido o vacío: {Path}", treePath);
                return null;
            }
            _logger.LogInformation("📋 _dms_tree.json leído: {Path} → {Count} docs, categoría: {Cat}",
                treePath, tree.Documents.Count, tree.Category.Code);
            return tree;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Error leyendo _dms_tree.json: {Path}", treePath);
            return null;
        }
    }

    /// <summary>
    /// Busca o crea una categoría de documento a partir del código DMS.
    /// Si no existe en DB, la crea con el nombre e icono proporcionados.
    /// </summary>
    private async Task<int> DetectCategoryFromDmsCodeAsync(AquafrischDbContext db, string code, string name, string? icon)
    {
        var allCategories = await db.DocumentCategories.ToListAsync();

        // Buscar por FolderName == code (el DMS usa FolderName como clave)
        var existing = allCategories.FirstOrDefault(c =>
            string.Equals(c.FolderName, code, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        // Buscar por nombre exacto
        existing = allCategories.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        // No existe → crear
        var maxId = allCategories.Count > 0 ? allCategories.Max(c => c.Id) : -1;
        var newCat = new DocumentCategoryConfig
        {
            Id = maxId + 1,
            Name = name,
            FolderName = code,
            Icon = icon ?? "📁",
            Color = "#6b7280",
            SortOrder = maxId + 1,
            IsSystem = false,
            CreatedBy = "DMS_Enterprise",
            CreatedAt = DateTime.UtcNow
        };
        db.DocumentCategories.Add(newCat);
        await db.SaveChangesAsync();
        _logger.LogInformation("🏷️ Categoría auto-creada desde DMS: {Name} (code: {Code})", name, code);
        return newCat.Id;
    }

    /// <summary>
    /// Detecta el DocumentFileType a partir de la extensión del archivo.
    /// </summary>
    private static DocumentFileType DetectFileType(string extensionWithDot)
    {
        return extensionWithDot.ToLower() switch
        {
            ".md" => DocumentFileType.Markdown,
            ".pdf" => DocumentFileType.Pdf,
            ".docx" or ".doc" => DocumentFileType.Docx,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".bmp" or ".webp" => DocumentFileType.Image,
            ".json" => DocumentFileType.Json,
            _ => DocumentFileType.Other
        };
    }

    private static string BuildInitialContent(CreateDocumentRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {request.Title}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            sb.AppendLine($"> {request.Description}");
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"- **Categoría**: {request.Category}");
        sb.AppendLine($"- **Versión**: 1.0");
        sb.AppendLine($"- **Estado**: Borrador");
        if (request.CraRelevant)
            sb.AppendLine($"- **CRA**: {request.CraArticle ?? "Sí"}");
        sb.AppendLine();
        sb.AppendLine("## Contenido");
        sb.AppendLine();
        sb.AppendLine("*Pendiente de redactar...*");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetCategoryDisplayName(int category) => category switch
    {
        SystemDocumentCategories.Compliance => "Compliance CRA",
        SystemDocumentCategories.CraGeneric => "CRA Genérico (SW)",
        SystemDocumentCategories.UserGuide => "Manuales de Usuario",
        SystemDocumentCategories.Technical => "Documentación Técnica",
        SystemDocumentCategories.Electrical => "Esquemas Eléctricos",
        SystemDocumentCategories.Maintenance => "Mantenimiento",
        SystemDocumentCategories.Other => "Otros",
        _ => $"Categoría {category}"
    };

    private static string GetCategoryIcon(int category) => category switch
    {
        SystemDocumentCategories.Compliance => "📋",
        SystemDocumentCategories.CraGeneric => "🇪🇺",
        SystemDocumentCategories.UserGuide => "📖",
        SystemDocumentCategories.Technical => "🔧",
        SystemDocumentCategories.Electrical => "⚡",
        SystemDocumentCategories.Maintenance => "🔩",
        SystemDocumentCategories.Other => "📄",
        _ => "📄"
    };

    private static string GetFileTypeIcon(DocumentFileType fileType) => fileType switch
    {
        DocumentFileType.Markdown => "📝",
        DocumentFileType.Pdf => "📕",
        DocumentFileType.Docx => "📘",
        DocumentFileType.Image => "🖼️",
        DocumentFileType.Json => "📊",
        _ => "📄"
    };

    #endregion

    #region Clasificación + Acceso (ISO 27001)

    // ═══ Niveles de Clasificación (ISO 27001 A.8.2) ═══

    public async Task<List<DocumentClassificationLevel>> GetClassificationLevelsAsync()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentClassificationLevels
                .OrderBy(l => l.SortOrder)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo niveles de clasificación");
            return new List<DocumentClassificationLevel>();
        }
    }

    public async Task<DocumentClassificationLevel> CreateClassificationLevelAsync(DocumentClassificationLevel level, string userName)
    {
        using var db = _dbFactory.CreateDbContext();

        // Auto-generar ID (siguiente disponible)
        var maxId = await db.DocumentClassificationLevels.MaxAsync(l => (int?)l.Id) ?? -1;
        level.Id = maxId + 1;
        level.IsSystem = false;
        level.CreatedBy = userName;
        level.CreatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(level.Code))
            level.Code = GenerateFolderName(level.Name);

        db.DocumentClassificationLevels.Add(level);
        await db.SaveChangesAsync();

        _logger.LogInformation("Nivel de clasificación '{Name}' creado por {User}", level.Name, userName);
        return level;
    }

    public async Task<DocumentClassificationLevel?> UpdateClassificationLevelAsync(int id, DocumentClassificationLevel level, string userName)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.DocumentClassificationLevels.FindAsync(id);
        if (existing == null) return null;

        existing.Name = level.Name;
        existing.Icon = level.Icon;
        existing.Color = level.Color;
        existing.Description = level.Description;
        existing.Level = level.Level;
        existing.SortOrder = level.SortOrder;

        if (!existing.IsSystem && !string.IsNullOrWhiteSpace(level.Code))
            existing.Code = level.Code;

        await db.SaveChangesAsync();
        _logger.LogInformation("Nivel de clasificación '{Name}' (ID={Id}) actualizado por {User}", existing.Name, id, userName);
        return existing;
    }

    public async Task<bool> DeleteClassificationLevelAsync(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        var level = await db.DocumentClassificationLevels.FindAsync(id);
        if (level == null || level.IsSystem) return false;

        db.DocumentClassificationLevels.Remove(level);
        await db.SaveChangesAsync();
        _logger.LogInformation("Nivel de clasificación '{Name}' (ID={Id}) eliminado", level.Name, id);
        return true;
    }

    // ═══ Matriz de Acceso: Categoría × Rol (ISO 27001 A.9.1) ═══

    public async Task<List<DocumentCategoryAccess>> GetCategoryAccessMatrixAsync()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentCategoryAccess
                .OrderBy(a => a.CategoryId)
                .ThenBy(a => a.RoleName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo matriz de acceso");
            return new List<DocumentCategoryAccess>();
        }
    }

    public async Task<List<DocumentCategoryAccess>> GetCategoryAccessAsync(int categoryId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return await db.DocumentCategoryAccess
                .Where(a => a.CategoryId == categoryId)
                .OrderBy(a => a.RoleName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo accesos de categoría {CategoryId}", categoryId);
            return new List<DocumentCategoryAccess>();
        }
    }

    public async Task<DocumentCategoryAccess> SetCategoryAccessAsync(int categoryId, string roleName, bool canRead, string userName)
    {
        using var db = _dbFactory.CreateDbContext();

        var existing = await db.DocumentCategoryAccess
            .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.RoleName == roleName);

        if (existing != null)
        {
            existing.CanRead = canRead;
            existing.UpdatedBy = userName;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new DocumentCategoryAccess
            {
                CategoryId = categoryId,
                RoleName = roleName,
                CanRead = canRead,
                UpdatedBy = userName,
                UpdatedAt = DateTime.UtcNow
            };
            db.DocumentCategoryAccess.Add(existing);
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Acceso categoría {CatId} / rol '{Role}' = CanRead:{CanRead} por {User}",
            categoryId, roleName, canRead, userName);
        return existing;
    }

    public async Task<List<DocumentCategoryAccess>> SetCategoryAccessBulkAsync(
        int categoryId, Dictionary<string, bool> roleAccess, string userName)
    {
        using var db = _dbFactory.CreateDbContext();
        var result = new List<DocumentCategoryAccess>();

        foreach (var (roleName, canRead) in roleAccess)
        {
            var existing = await db.DocumentCategoryAccess
                .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.RoleName == roleName);

            if (existing != null)
            {
                existing.CanRead = canRead;
                existing.UpdatedBy = userName;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                existing = new DocumentCategoryAccess
                {
                    CategoryId = categoryId,
                    RoleName = roleName,
                    CanRead = canRead,
                    UpdatedBy = userName,
                    UpdatedAt = DateTime.UtcNow
                };
                db.DocumentCategoryAccess.Add(existing);
            }
            result.Add(existing);
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Acceso bulk categoría {CatId}: {Count} roles actualizados por {User}",
            categoryId, roleAccess.Count, userName);
        return result;
    }

    /// <summary>
    /// Resetear la matriz de acceso a los defaults ISO 27001 (principio de menor privilegio).
    /// Sobrescribe TODOS los valores existentes en DocumentCategoryAccess.
    /// </summary>
    public async Task<int> ResetCategoryAccessToDefaultsAsync(string userName)
    {
        using var db = _dbFactory.CreateDbContext();

        // Defaults restrictivos (ISO 27001 A.9.1 — menor privilegio)
        var defaultAccess = new Dictionary<string, HashSet<int>>
        {
            { "Administrador", new() { 0, 1, 2, 3, 4, 5, 7 } },
            { "Mantenimiento", new() { 2, 3, 4, 5, 7 } },
            { "Auditor", new() { 0, 1, 2, 3, 7 } },
            { "Operador", new() { 2, 7 } },
            { "Visualizador", new() { 2, 7 } },
        };

        // Obtener todas las categorías
        var allCategories = await db.DocumentCategories.Select(c => c.Id).ToListAsync();
        int updated = 0;

        foreach (var catId in allCategories)
        {
            foreach (var (role, allowedCats) in defaultAccess)
            {
                bool canRead = allowedCats.Contains(catId);
                var existing = await db.DocumentCategoryAccess
                    .FirstOrDefaultAsync(a => a.CategoryId == catId && a.RoleName == role);

                if (existing != null)
                {
                    existing.CanRead = canRead;
                    existing.UpdatedBy = userName;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.DocumentCategoryAccess.Add(new DocumentCategoryAccess
                    {
                        CategoryId = catId,
                        RoleName = role,
                        CanRead = canRead,
                        UpdatedBy = userName,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                updated++;
            }
        }

        await db.SaveChangesAsync();
        _logger.LogWarning("🔒 Matriz de acceso reseteada a defaults ISO 27001 por {User}: {Count} entradas actualizadas",
            userName, updated);
        return updated;
    }

    #endregion

    #region Upload / Download de ficheros

    // Extensión → DocumentFileType
    private static readonly Dictionary<string, DocumentFileType> ExtToFileType = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".md", DocumentFileType.Markdown },
        { ".pdf", DocumentFileType.Pdf },
        { ".docx", DocumentFileType.Docx },
        { ".doc", DocumentFileType.Docx },
        { ".png", DocumentFileType.Image },
        { ".jpg", DocumentFileType.Image },
        { ".jpeg", DocumentFileType.Image },
        { ".gif", DocumentFileType.Image },
        { ".svg", DocumentFileType.Image },
        { ".webp", DocumentFileType.Image },
        { ".bmp", DocumentFileType.Image },
        { ".json", DocumentFileType.Json },
    };

    // Extensión → Content-Type MIME
    private static readonly Dictionary<string, string> ExtToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".md", "text/markdown" },
        { ".pdf", "application/pdf" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".doc", "application/msword" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".webp", "image/webp" },
        { ".bmp", "image/bmp" },
        { ".json", "application/json" },
        { ".txt", "text/plain" },
        { ".csv", "text/csv" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".xls", "application/vnd.ms-excel" },
    };

    // Tamaño máximo permitido (50 MB)
    private const long MaxFileSize = 50 * 1024 * 1024;

    public async Task<DocumentOperationResponse> UploadFileAsync(
        Stream fileStream, string fileName, long fileSize,
        int category, string? description, string? minimumRole, int? classificationId,
        string userName, string userRole)
    {
        string absolutePath = "";
        try
        {
            if (fileSize > MaxFileSize)
                return new DocumentOperationResponse { Success = false, Message = $"El fichero excede el tamaño máximo permitido ({MaxFileSize / 1024 / 1024} MB)" };

            var ext = Path.GetExtension(fileName).ToLower();
            var fileType = ExtToFileType.GetValueOrDefault(ext, DocumentFileType.Other);
            var title = Path.GetFileNameWithoutExtension(fileName);
            var slug = GenerateSlug(title);

            // Nombre único (evitar colisiones)
            var safeFileName = $"{slug}{ext}";

            // Resolver carpeta de categoría
            using var dbForPath = _dbFactory.CreateDbContext();
            var categoryFolder = await BuildCategoryFolderPathAsync(dbForPath, category);

            var scopePrefix = "AQSdocs_project";
            string relativePath;
            if (string.IsNullOrEmpty(categoryFolder))
                relativePath = $"{scopePrefix}/{safeFileName}";
            else
                relativePath = $"{scopePrefix}/{categoryFolder}/{safeFileName}".Replace('\\', '/');

            var docsPath = _requestContext.DocsPath;
            absolutePath = Path.Combine(docsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Verificar que no exista ya
            if (File.Exists(absolutePath))
            {
                // Agregar timestamp para unicidad
                safeFileName = $"{slug}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                if (string.IsNullOrEmpty(categoryFolder))
                    relativePath = $"{scopePrefix}/{safeFileName}";
                else
                    relativePath = $"{scopePrefix}/{categoryFolder}/{safeFileName}".Replace('\\', '/');
                absolutePath = Path.Combine(docsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            // Crear directorio si no existe
            var dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Escribir fichero a disco
            using var memStream = new MemoryStream();
            await fileStream.CopyToAsync(memStream);
            var fileBytes = memStream.ToArray();

            // Validar que se recibió contenido real (proteción contra uploads corruptos)
            if (fileBytes.Length == 0 || fileBytes.Length < fileSize * 0.5)
            {
                _logger.LogWarning("Upload corrupto: se esperaban {Expected} bytes, se recibieron {Actual}", fileSize, fileBytes.Length);
                return new DocumentOperationResponse
                {
                    Success = false,
                    Message = $"Error: fichero corrupto o vacío. Se esperaban {fileSize / 1024} KB, se recibieron {fileBytes.Length / 1024} KB. Reintente la subida."
                };
            }

            await File.WriteAllBytesAsync(absolutePath, fileBytes);

            var hash = ComputeSha256(fileBytes);

            // Crear registro en DB
            using var db2 = _dbFactory.CreateDbContext();
            slug = GenerateUniqueSlug(db2, title);

            var doc = new Document
            {
                Id = Guid.NewGuid().ToString(),
                Slug = slug,
                Title = title,
                Description = description,
                FilePath = relativePath,
                FileType = fileType,
                ContentHash = hash,
                FileSize = fileBytes.Length,
                Scope = DocumentScope.Project,
                Category = category,
                AccessLevel = DocumentAccessLevel.Public,
                MinimumRole = minimumRole ?? "Administrador", // Restrictivo por defecto
                ClassificationId = classificationId ?? 0,
                Version = "1.0",
                Status = DocumentStatus.Draft,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                SearchContent = $"{title} {description ?? ""} {fileName}"
            };

            db2.Documents.Add(doc);

            db2.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = doc.Id,
                Version = doc.Version,
                Action = "created",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                ContentHash = hash,
                ChangeNote = $"Fichero subido: {fileName} ({fileBytes.Length / 1024} KB)"
            });

            await db2.SaveChangesAsync();

            _logger.LogInformation("📎 Fichero subido: {FileName} ({Size} KB) por {User} → {Path}",
                fileName, fileBytes.Length / 1024, userName, relativePath);

            return new DocumentOperationResponse
            {
                Success = true,
                Message = $"Fichero '{fileName}' subido correctamente ({fileBytes.Length / 1024} KB)",
                Document = MapToInfo(doc)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo fichero {FileName}", fileName);
            try { if (File.Exists(absolutePath)) File.Delete(absolutePath); } catch { }
            return new DocumentOperationResponse { Success = false, Message = $"Error subiendo fichero: {ex.Message}" };
        }
    }

    /// <summary>
    /// Importar un fichero a un documento existente.
    /// - Si doc es Markdown y fichero es .docx → convierte DOCX→MD y reemplaza contenido
    /// - Si doc es Markdown y fichero es .md → reemplaza contenido directamente
    /// - Otros: reemplaza fichero en disco y actualiza metadatos
    /// </summary>
    public async Task<DocumentOperationResponse> ImportFileAsync(
        string documentId, Stream fileStream, string fileName, long fileSize,
        string userName, string userRole)
    {
        try
        {
            if (fileSize > MaxFileSize)
                return new DocumentOperationResponse { Success = false, Message = $"El fichero excede {MaxFileSize / 1024 / 1024} MB" };

            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null)
                return new DocumentOperationResponse { Success = false, Message = "Documento no encontrado" };

            if (!HasAccessToDocument(doc, userRole))
                return new DocumentOperationResponse { Success = false, Message = "Sin acceso al documento" };

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var importFileType = ExtToFileType.GetValueOrDefault(ext, DocumentFileType.Other);

            // Leer fichero a memoria
            using var memStream = new MemoryStream();
            await fileStream.CopyToAsync(memStream);
            var fileBytes = memStream.ToArray();

            string importNote;

            if (doc.FileType == DocumentFileType.Markdown)
            {
                // ── Importar contenido a documento Markdown ──
                string newContent;
                if (importFileType == DocumentFileType.Docx)
                {
                    // DOCX → Markdown
                    using var docxStream = new MemoryStream(fileBytes);
                    newContent = _exportService.ConvertDocxToMarkdown(docxStream);
                    importNote = $"Importado desde DOCX: {fileName}";
                }
                else if (importFileType == DocumentFileType.Markdown || ext == ".txt")
                {
                    // MD/TXT → directo
                    newContent = System.Text.Encoding.UTF8.GetString(fileBytes);
                    importNote = $"Importado desde: {fileName}";
                }
                else
                {
                    return new DocumentOperationResponse
                    {
                        Success = false,
                        Message = $"No se puede importar '{ext}' a un documento Markdown. Formatos válidos: .docx, .md, .txt"
                    };
                }

                // Guardar contenido Markdown al fichero existente
                var absolutePath = ResolveDocFilePath(doc.FilePath);
                if (absolutePath == null)
                    return new DocumentOperationResponse { Success = false, Message = "No se pudo resolver la ruta del documento" };

                await File.WriteAllTextAsync(absolutePath, newContent, System.Text.Encoding.UTF8);
                doc.ContentHash = ComputeSha256(System.Text.Encoding.UTF8.GetBytes(newContent));
                doc.FileSize = new System.IO.FileInfo(absolutePath).Length;
            }
            else
            {
                // ── Reemplazar fichero binario (PDF, DOCX, imagen, etc.) ──
                if (importFileType != doc.FileType)
                    return new DocumentOperationResponse
                    {
                        Success = false,
                        Message = $"Tipo de fichero incompatible: se esperaba {doc.FileType}, se recibió {importFileType} ({ext})"
                    };

                var absolutePath = ResolveDocFilePath(doc.FilePath);
                if (absolutePath == null)
                    return new DocumentOperationResponse { Success = false, Message = "No se pudo resolver la ruta del documento" };

                await File.WriteAllBytesAsync(absolutePath, fileBytes);
                doc.ContentHash = ComputeSha256(fileBytes);
                doc.FileSize = fileBytes.Length;
                importNote = $"Fichero reemplazado: {fileName} ({fileBytes.Length / 1024} KB)";
            }

            // Incrementar versión
            var currentVersion = doc.Version ?? "1.0";
            if (Version.TryParse(currentVersion, out var ver))
                doc.Version = $"{ver.Major}.{ver.Minor + 1}";
            else
                doc.Version = currentVersion + ".1";

            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;
            doc.Status = DocumentStatus.Draft;

            // Historial
            db.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = doc.Id,
                Version = doc.Version,
                Action = "imported",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                ContentHash = doc.ContentHash,
                ChangeNote = importNote
            });

            await db.SaveChangesAsync();

            _logger.LogInformation("📥 Importado {FileName} → documento {DocId} v{Version} por {User}",
                fileName, doc.Id, doc.Version, userName);

            return new DocumentOperationResponse
            {
                Success = true,
                Message = $"Importado '{fileName}' correctamente → v{doc.Version}",
                Document = MapToInfo(doc)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importando fichero {FileName} a documento {DocId}", fileName, documentId);
            return new DocumentOperationResponse { Success = false, Message = $"Error importando: {ex.Message}" };
        }
    }

    public async Task<(Stream? FileStream, string? ContentType, string? FileName)?> DownloadFileAsync(string documentId, string userRole, string? exportFormat = null)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return null;

            if (!HasAccessToDocument(doc, userRole)) return null;

            var absolutePath = ResolveDocFilePath(doc.FilePath);
            if (absolutePath == null || !File.Exists(absolutePath))
            {
                _logger.LogWarning("Fichero no encontrado en disco: {FilePath}", doc.FilePath);
                return null;
            }

            var ext = Path.GetExtension(doc.FilePath).ToLower();
            var originalFileName = Path.GetFileName(doc.FilePath);
            var docTitle = doc.Title ?? Path.GetFileNameWithoutExtension(doc.FilePath);

            // ── Conversión de formato (solo para fuentes Markdown) ──
            if (!string.IsNullOrEmpty(exportFormat) && ext == ".md")
            {
                var mdContent = await File.ReadAllTextAsync(absolutePath);

                switch (exportFormat.ToLower())
                {
                    case "pdf":
                        var pdfStream = _exportService.ExportToPdf(mdContent, docTitle);
                        var pdfName = Path.ChangeExtension(originalFileName, ".pdf");
                        return (pdfStream, "application/pdf", pdfName);

                    case "docx":
                        var docxStream = _exportService.ExportToDocx(mdContent, docTitle);
                        var docxName = Path.ChangeExtension(originalFileName, ".docx");
                        return (docxStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxName);
                }
            }

            // ── Fichero original (sin conversión) ──
            var contentType = ExtToMime.GetValueOrDefault(ext, "application/octet-stream");
            var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, contentType, originalFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando fichero {DocumentId} (formato: {Format})", documentId, exportFormat ?? "original");
            return null;
        }
    }

    /// <summary>
    /// Genera una previsualización HTML de un documento Markdown convertido a DOCX.
    /// Cadena: MD → DOCX (en memoria) → HTML
    /// </summary>
    public async Task<string?> PreviewAsFormatAsync(string documentId, string userRole, string format)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return null;
            if (!HasAccessToDocument(doc, userRole)) return null;

            // Solo soportamos preview para ficheros Markdown
            if (doc.FileType != DocumentFileType.Markdown) return null;

            var absolutePath = ResolveDocFilePath(doc.FilePath);
            if (absolutePath == null || !File.Exists(absolutePath)) return null;

            var mdContent = await File.ReadAllTextAsync(absolutePath, Encoding.UTF8);
            var title = doc.Title ?? Path.GetFileNameWithoutExtension(doc.FilePath);

            if (format.Equals("docx", StringComparison.OrdinalIgnoreCase))
            {
                // MD → DOCX (memory stream) → HTML
                using var docxStream = _exportService.ExportToDocx(mdContent, title);
                return _exportService.ConvertDocxToHtml(docxStream);
            }

            // Formato no soportado para preview
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preview {Format} para {DocumentId}", format, documentId);
            return null;
        }
    }

    #endregion
}
