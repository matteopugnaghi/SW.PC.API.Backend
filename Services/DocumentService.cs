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
    public static readonly string[] SystemRoles = { "Administrador", "Operador", "Mantenimiento", "Visualizador", "Auditor" };

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
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _globalContext = globalContext;
        _requestContext = requestContext;
        _contentRootPath = environment.ContentRootPath;
        
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

            // Filtrar por acceso del usuario
            query = ApplyAccessFilter(query, userRole);

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
            var absolutePath = Path.Combine(docsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

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
                MinimumRole = AccessLevelToRole.GetValueOrDefault(request.AccessLevel, "Viewer"),
                Version = "1.0",
                Status = DocumentStatus.Draft,
                CraRelevant = request.CraRelevant,
                CraArticle = request.CraArticle,
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
            _logger.LogError(ex, "Error creando documento: {Title}", request.Title);
            return new DocumentOperationResponse { Success = false, Message = $"Error creando documento: {ex.Message}" };
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

            // Los documentos master (Software) son de solo lectura — vienen del servidor corporativo
            if (doc.Scope == DocumentScope.Software)
                return new DocumentOperationResponse { Success = false, Message = "Los documentos AQSdocs_master son de solo lectura" };

            if (!HasAccessToDocument(doc, userRole))
                return new DocumentOperationResponse { Success = false, Message = "Sin permisos para editar este documento" };

            // Actualizar campos opcionales
            if (request.Title != null) doc.Title = request.Title;
            if (request.Description != null) doc.Description = request.Description;
            if (request.Version != null) doc.Version = request.Version;
            if (request.AccessLevel.HasValue)
            {
                doc.AccessLevel = request.AccessLevel.Value;
                doc.MinimumRole = AccessLevelToRole.GetValueOrDefault(request.AccessLevel.Value, "Viewer");
            }
            if (request.Status.HasValue) doc.Status = request.Status.Value;
            if (request.Tags != null) doc.Tags = JsonSerializer.Serialize(request.Tags);
            if (request.CraRelevant.HasValue) doc.CraRelevant = request.CraRelevant.Value;
            if (request.CraArticle != null) doc.CraArticle = request.CraArticle;

            // Actualizar contenido del fichero si se proporcionó
            if (request.Content != null)
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
            var docs = await ApplyAccessFilter(db.Documents.AsQueryable(), userRole).ToListAsync();

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
            var docsByScope = docs.GroupBy(d => d.Scope).ToDictionary(g => g.Key, g => g.AsEnumerable());

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

            var stats = new DocumentStats
            {
                TotalDocuments = docs.Count,
                TotalByScope_Software = docs.Count(d => d.Scope == DocumentScope.Software),
                TotalByScope_Project = docs.Count(d => d.Scope == DocumentScope.Project),
                CraRelevantTotal = docs.Count(d => d.CraRelevant),
                CraRelevantApproved = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Approved),
                CraRelevantPending = docs.Count(d => d.CraRelevant && d.Status != DocumentStatus.Approved),
                LastUpdated = docs.Max(d => d.UpdatedAt ?? d.CreatedAt)
            };

            stats.CraCompliancePercent = stats.CraRelevantTotal > 0 
                ? Math.Round((double)stats.CraRelevantApproved / stats.CraRelevantTotal * 100, 1) 
                : 0;

            // Por categoría
            foreach (var group in docs.GroupBy(d => d.Category))
                stats.ByCategory[group.Key.ToString()] = group.Count();

            // Por estado
            foreach (var group in docs.GroupBy(d => d.Status))
                stats.ByStatus[group.Key.ToString()] = group.Count();

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
    /// </summary>
    public async Task<DocumentOperationResponse> SyncMasterAsync(string userName)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            var globalDocsPath = Path.GetFullPath(GetGlobalDocsPath());
            int copied = 0, created = 0, updated = 0, orphaned = 0;

            // ═══ PASO 1: Copiar ficheros del master al proyecto ═══
            var masterDestPath = Path.Combine(projectDocsPath, "AQSdocs_master");
            if (Directory.Exists(globalDocsPath))
            {
                _logger.LogInformation("🔄 SyncMaster: copiando de {Src} a {Dst}", globalDocsPath, masterDestPath);
                
                var masterFiles = Directory.GetFiles(globalDocsPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                    .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
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

            // ═══ PASO 2: Escanear solo AQSdocs_master/ y registrar en DB ═══
            var existingMasterDocs = await db.Documents
                .Where(d => d.Scope == DocumentScope.Software)
                .ToListAsync();
            var existingByPath = existingMasterDocs.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(masterDestPath))
            {
                var mdFiles = Directory.GetFiles(masterDestPath, "*.md", SearchOption.AllDirectories)
                    .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                    .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                    .ToArray();

                foreach (var filePath in mdFiles)
                {
                    var relativePath = Path.GetRelativePath(projectDocsPath, filePath).Replace('\\', '/');
                    var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                    var contentBytes = Encoding.UTF8.GetBytes(content);
                    var hash = ComputeSha256(contentBytes);

                    if (existingByPath.TryGetValue(relativePath, out var existing))
                    {
                        var detectedCategory = await DetectCategoryFromPathAsync(db, relativePath);
                        if (existing.ContentHash != hash || existing.Category != detectedCategory)
                        {
                            existing.ContentHash = hash;
                            existing.FileSize = contentBytes.Length;
                            existing.SearchContent = ExtractSearchContent(content);
                            existing.Category = detectedCategory;
                            existing.Scope = DocumentScope.Software;
                            existing.UpdatedBy = userName;
                            existing.UpdatedAt = DateTime.UtcNow;
                            updated++;
                        }
                        existingByPath.Remove(relativePath);
                    }
                    else
                    {
                        var category = await DetectCategoryFromPathAsync(db, relativePath);
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
                            FileSize = contentBytes.Length,
                            Scope = DocumentScope.Software,
                            Category = category,
                            Version = "1.0",
                            Status = DocumentStatus.Draft,
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow,
                            SearchContent = ExtractSearchContent(content)
                        });
                        created++;
                    }
                }
            }

            // Eliminar huérfanos master
            foreach (var orphan in existingByPath.Values)
            {
                db.Documents.Remove(orphan);
                orphaned++;
            }

            await db.SaveChangesAsync();

            var message = $"{created} creados, {updated} actualizados, {orphaned} eliminados, {copied} copiados";
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
    /// auto-crea categorías desde las carpetas (y subcarpetas), y registra los .md en DB con scope=Project.
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

            int created = 0, updated = 0, orphaned = 0, catsCreated = 0;

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

            // ═══ PASO 2: Escanear .md dentro de AQSdocs_project/ ═══
            var existingProjectDocs = await db.Documents
                .Where(d => d.Scope == DocumentScope.Project)
                .ToListAsync();
            var existingByPath = existingProjectDocs.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);

            var mdFiles = Directory.GetFiles(projectScopePath, "*.md", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                .ToArray();

            foreach (var filePath in mdFiles)
            {
                var relativePath = Path.GetRelativePath(projectDocsPath, filePath).Replace('\\', '/');
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var hash = ComputeSha256(contentBytes);

                if (existingByPath.TryGetValue(relativePath, out var existing))
                {
                    var detectedCategory = await DetectCategoryFromPathAsync(db, relativePath);
                    if (existing.ContentHash != hash || existing.Category != detectedCategory)
                    {
                        existing.ContentHash = hash;
                        existing.FileSize = contentBytes.Length;
                        existing.SearchContent = ExtractSearchContent(content);
                        existing.Category = detectedCategory;
                        existing.Scope = DocumentScope.Project;
                        existing.UpdatedBy = userName;
                        existing.UpdatedAt = DateTime.UtcNow;
                        updated++;
                    }
                    existingByPath.Remove(relativePath);
                }
                else
                {
                    var category = await DetectCategoryFromPathAsync(db, relativePath);
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
                        FileSize = contentBytes.Length,
                        Scope = DocumentScope.Project,
                        Category = category,
                        Version = "1.0",
                        Status = DocumentStatus.Draft,
                        CreatedBy = userName,
                        CreatedAt = DateTime.UtcNow,
                        SearchContent = ExtractSearchContent(content)
                    });
                    created++;
                }
            }

            // Eliminar huérfanos project
            foreach (var orphan in existingByPath.Values)
            {
                db.Documents.Remove(orphan);
                orphaned++;
            }

            await db.SaveChangesAsync();

            var message = $"{created} creados, {updated} actualizados, {orphaned} eliminados, {catsCreated} categorías creadas";
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
        _logger.LogInformation("Categoría actualizada: {Name} (ID: {Id}) por {User}", existing.Name, id, userName);
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
            AccessLevel = doc.AccessLevel,
            Version = doc.Version,
            Status = doc.Status,
            CraRelevant = doc.CraRelevant,
            CraArticle = doc.CraArticle,
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
                detail.RawContent = await File.ReadAllTextAsync(absolutePath, Encoding.UTF8);
                detail.HtmlContent = RenderMarkdownToHtml(detail.RawContent);
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
        Version = doc.Version,
        Status = doc.Status,
        CraRelevant = doc.CraRelevant,
        CraArticle = doc.CraArticle,
        CraDeadline = doc.CraDeadline,
        CreatedBy = doc.CreatedBy,
        CreatedAt = doc.CreatedAt,
        UpdatedBy = doc.UpdatedBy,
        UpdatedAt = doc.UpdatedAt,
        FileSize = doc.FileSize
    };

    private IQueryable<Document> ApplyAccessFilter(IQueryable<Document> query, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        
        // SuperAdmin ve todo
        if (roleLevel >= 5) return query;

        // Filtrar por AccessLevel compatible con el rol del usuario (legacy)
        // TODO: Migrar a sistema de matriz categoría×rol cuando esté completamente activo
        return query.Where(d =>
            (d.AccessLevel == DocumentAccessLevel.Public) ||
            (d.AccessLevel == DocumentAccessLevel.Operator && roleLevel >= 1) ||
            (d.AccessLevel == DocumentAccessLevel.Maintenance && roleLevel >= 3) ||
            (d.AccessLevel == DocumentAccessLevel.Admin && roleLevel >= 4) ||
            (d.AccessLevel == DocumentAccessLevel.Internal && roleLevel >= 5));
    }

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

        // Obtener las categorías que este rol puede leer
        var allowedCategoryIds = await db.DocumentCategoryAccess
            .Where(a => a.RoleName == userRole && a.CanRead)
            .Select(a => a.CategoryId)
            .ToListAsync();

        // Si no hay configuración de acceso (tabla vacía), usar filtro legacy
        if (allowedCategoryIds.Count == 0)
        {
            _logger.LogWarning("No hay configuración de acceso para rol '{Role}', usando filtro legacy", userRole);
            return ApplyAccessFilter(query, userRole);
        }

        // Filtrar: solo documentos de categorías permitidas
        return query.Where(d => allowedCategoryIds.Contains(d.Category));
    }

    private bool HasAccessToDocument(Document doc, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        if (roleLevel >= 5) return true; // SuperAdmin

        var requiredLevel = RoleHierarchy.GetValueOrDefault(
            AccessLevelToRole.GetValueOrDefault(doc.AccessLevel, "Viewer"), 0);

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
        SystemDocumentCategories.Internal => "Interno",
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
        SystemDocumentCategories.Internal => "🔒",
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

    #endregion
}
