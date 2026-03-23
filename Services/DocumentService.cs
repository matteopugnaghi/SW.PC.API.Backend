// ============================================================================
// DocumentService.cs - Servicio Documental Simplificado (Solo Lectura + Sync)
// ============================================================================
// Los documentos (PDF) llegan ya generados desde DMS Enterprise.
// Solo lectura, descarga, árbol, estadísticas y sincronización.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly string _contentRootPath;

    // Orden de roles de menor a mayor privilegio
    private static readonly Dictionary<string, int> RoleHierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Viewer", 0 }, { "Visualizador", 0 },
        { "Operator", 1 }, { "Operador", 1 },
        { "Auditor", 2 },
        { "Maintenance", 3 }, { "Mantenimiento", 3 },
        { "Administrator", 4 }, { "Administrador", 4 },
        { "SuperAdmin", 5 }
    };

    // Roles del sistema para la matriz de acceso
    public static readonly string[] SystemRoles = { "Visualizador", "Operador", "Auditor", "Mantenimiento", "Administrador" };

    // Mapeo JWT (inglés) → Matriz (español)
    private static readonly Dictionary<string, string> RoleToMatrixName = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Viewer", "Visualizador" },
        { "Operator", "Operador" },
        { "Auditor", "Auditor" },
        { "Maintenance", "Mantenimiento" },
        { "Administrator", "Administrador" },
        { "Visualizador", "Visualizador" },
        { "Operador", "Operador" },
        { "Mantenimiento", "Mantenimiento" },
        { "Administrador", "Administrador" },
        { "SuperAdmin", "SuperAdmin" }
    };

    private static string NormalizeRoleForMatrix(string jwtRole)
        => RoleToMatrixName.GetValueOrDefault(jwtRole, jwtRole);

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
    }

    private string GetGlobalDocsPath()
    {
        return Path.Combine(_contentRootPath, "docs");
    }

    private string? ResolveDocFilePath(string relativePath)
    {
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        
        var projectDocsPath = _requestContext.DocsPath;
        if (!string.IsNullOrEmpty(projectDocsPath))
        {
            var projectPath = Path.Combine(projectDocsPath, normalizedRelative);
            if (File.Exists(projectPath)) return projectPath;
        }

        var globalDocsPath = GetGlobalDocsPath();
        var globalPath = Path.GetFullPath(Path.Combine(globalDocsPath, normalizedRelative));
        if (File.Exists(globalPath)) return globalPath;

        return null;
    }

    #region Lectura

    public async Task<(List<DocumentInfo> Items, int TotalCount)> GetDocumentsAsync(DocumentFilter filter, string userRole)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.Documents.AsQueryable();

            query = await ApplyAccessFilterWithMatrixAsync(db, query, userRole);

            if (filter.Scope.HasValue)
                query = query.Where(d => d.Scope == filter.Scope.Value);
            
            if (filter.Category.HasValue)
                query = query.Where(d => d.Category == filter.Category.Value);

            if (!string.IsNullOrWhiteSpace(filter.FolderName))
            {
                var folder = filter.FolderName;
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

            return BuildDocumentDetail(doc);
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

            return BuildDocumentDetail(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo documento por slug {Slug}", slug);
            return null;
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

            var allCategories = await db.DocumentCategories
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            var catMap = allCategories.ToDictionary(c => c.Id);

            var tree = new List<DocumentTreeNode>();

            string? GetRealFolder(string filePath)
            {
                var parts = filePath.Split('/');
                int startIdx = 0;
                if (parts.Length > 0 && (parts[0].Equals("AQSdocs_master", StringComparison.OrdinalIgnoreCase)
                    || parts[0].Equals("AQSdocs_project", StringComparison.OrdinalIgnoreCase)))
                {
                    startIdx = 1;
                }
                if (parts.Length >= startIdx + 2)
                    return parts[startIdx];
                return null;
            }

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

            List<DocumentTreeNode> BuildCategoryNodes(IEnumerable<Document> scopeDocs)
            {
                var catNodes = new List<DocumentTreeNode>();
                var docsByCategory = scopeDocs.GroupBy(d => d.Category).ToDictionary(g => g.Key, g => g.ToList());

                var usedCatIds = docsByCategory.Keys.ToHashSet();
                var rootCatIds = new HashSet<int>();

                foreach (var catId in usedCatIds)
                {
                    var config = catMap.GetValueOrDefault(catId);
                    if (config?.ParentId != null)
                        rootCatIds.Add(config.ParentId.Value);
                    else
                        rootCatIds.Add(catId);
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
                    Children = BuildFolderNodes(scopeDocs)
                };

                tree.Add(scopeNode);
            }

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
                TotalDocuments = docs.Count,
                TotalByScope_Software = docs.Count(d => d.Scope == DocumentScope.Software),
                TotalByScope_Project = docs.Count(d => d.Scope == DocumentScope.Project),
                TotalSizeBytes = docs.Sum(d => d.FileSize),
                LastUpdated = lastDoc != null ? (lastDoc.UpdatedAt ?? lastDoc.CreatedAt) : null,
                LastUpdatedDocument = lastDoc?.Title,
                GeneratedAt = DateTime.UtcNow,

                CraRelevantTotal = docs.Count(d => d.CraRelevant),
                CraRelevantApproved = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Approved),
                CraRelevantPending = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Review),
                CraRelevantDraft = docs.Count(d => d.CraRelevant && d.Status == DocumentStatus.Draft),

                Iso27001RelevantTotal = docs.Count(d => d.Iso27001Relevant),
                Iso27001RelevantApproved = docs.Count(d => d.Iso27001Relevant && d.Status == DocumentStatus.Approved),
                Iso27001RelevantPending = docs.Count(d => d.Iso27001Relevant && d.Status != DocumentStatus.Approved),

                Iec62443RelevantTotal = docs.Count(d => d.Iec62443Relevant),
                Iec62443RelevantApproved = docs.Count(d => d.Iec62443Relevant && d.Status == DocumentStatus.Approved),
                Iec62443RelevantPending = docs.Count(d => d.Iec62443Relevant && d.Status != DocumentStatus.Approved),

                DocsWithTags = docs.Count(d => !string.IsNullOrEmpty(d.Tags)),
                DocsWithClassification = docs.Count(d => d.ClassificationId > 0),
            };

            stats.CraCompliancePercent = stats.CraRelevantTotal > 0
                ? Math.Round((double)stats.CraRelevantApproved / stats.CraRelevantTotal * 100, 1)
                : 0;
            stats.Iso27001CompliancePercent = stats.Iso27001RelevantTotal > 0
                ? Math.Round((double)stats.Iso27001RelevantApproved / stats.Iso27001RelevantTotal * 100, 1)
                : 0;
            stats.Iec62443CompliancePercent = stats.Iec62443RelevantTotal > 0
                ? Math.Round((double)stats.Iec62443RelevantApproved / stats.Iec62443RelevantTotal * 100, 1)
                : 0;

            var catMap = categories.ToDictionary(c => c.Id, c => $"{c.Icon} {c.Name}");
            foreach (var group in docs.GroupBy(d => d.Category))
            {
                var label = catMap.TryGetValue(group.Key, out var name) ? name : $"Cat {group.Key}";
                stats.ByCategory[label] = group.Count();
            }

            var statusLabels = new Dictionary<string, string> {
                ["Draft"] = "📝 Borrador", ["Review"] = "🔍 En Revisión", ["Approved"] = "✅ Aprobado",
                ["Obsolete"] = "⚠️ Obsoleto", ["Archived"] = "📦 Archivado"
            };
            foreach (var group in docs.GroupBy(d => d.Status))
            {
                var label = statusLabels.TryGetValue(group.Key.ToString(), out var name) ? name : group.Key.ToString();
                stats.ByStatus[label] = group.Count();
            }

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

            foreach (var group in docs.GroupBy(d => d.MinimumRole ?? "Visualizador"))
                stats.ByMinimumRole[group.Key] = group.Count();

            var classMap = classifications.ToDictionary(c => c.Id, c => $"{c.Icon} {c.Name}");
            classMap[0] = "🌐 Público";
            foreach (var group in docs.GroupBy(d => d.ClassificationId))
            {
                var label = classMap.TryGetValue(group.Key, out var name) ? name : $"Nivel {group.Key}";
                stats.ByClassification[label] = group.Count();
            }

            foreach (var group in docs.Where(d => d.CraRelevant && !string.IsNullOrEmpty(d.CraArticle)).GroupBy(d => d.CraArticle!))
                stats.CraByArticle[group.Key] = group.Count();

            foreach (var group in docs.Where(d => d.Iso27001Relevant && !string.IsNullOrEmpty(d.Iso27001Article)).GroupBy(d => d.Iso27001Article!))
                stats.Iso27001ByArticle[group.Key] = group.Count();

            foreach (var group in docs.Where(d => d.Iec62443Relevant && !string.IsNullOrEmpty(d.Iec62443Article)).GroupBy(d => d.Iec62443Article!))
                stats.Iec62443ByArticle[group.Key] = group.Count();

            var versionCounts = await db.DocumentHistories.GroupBy(v => v.DocumentId).Select(g => new { DocId = g.Key, Count = g.Count() }).ToListAsync();
            stats.DocsWithVersionHistory = versionCounts.Count;
            stats.TotalVersionEntries = versionCounts.Sum(v => v.Count);

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

    #region Sincronización filesystem → DB

    public async Task<DocumentOperationResponse> SyncFromFilesystemAsync(string userName, bool skipGlobalCopy = true)
    {
        var masterResult = await SyncMasterAsync(userName, skipGlobalCopy);
        var projectResult = await SyncProjectAsync(userName);
        
        var combined = $"MASTER: {masterResult.Message} | PROJECT: {projectResult.Message}";
        return new DocumentOperationResponse 
        { 
            Success = masterResult.Success && projectResult.Success, 
            Message = combined 
        };
    }

    public async Task<DocumentOperationResponse> SyncMasterAsync(string userName, bool skipGlobalCopy = true)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            var masterScopePath = Path.Combine(projectDocsPath, "AQSdocs_master");
            if (!Directory.Exists(masterScopePath))
            {
                Directory.CreateDirectory(masterScopePath);
                _logger.LogInformation("📁 Creada carpeta AQSdocs_master en {Path}", masterScopePath);
            }

            int created = 0, updated = 0, orphaned = 0, dmsUpdated = 0, copied = 0;

            // Si existe {contentRoot}/docs/ como fuente global, copiar archivos NUEVOS o actualizados
            // (NO borra lo existente en AQSdocs_master/ — respeta restauraciones de backup)
            // skipGlobalCopy=true después de un restore para no re-introducir ficheros fantasma
            var globalDocsPath = Path.GetFullPath(GetGlobalDocsPath());
            if (!skipGlobalCopy && Directory.Exists(globalDocsPath))
            {
                var masterFiles = Directory.GetFiles(globalDocsPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Replace('\\', '/').Contains("/node_modules/"))
                    .Where(f => !f.Replace('\\', '/').Contains("/.git/"))
                    .Where(f => !Path.GetFileName(f).Equals("_dms_tree.json", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var srcFile in masterFiles)
                {
                    var relPath = Path.GetRelativePath(globalDocsPath, srcFile);
                    var dstFile = Path.Combine(masterScopePath, relPath);
                    
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
                
                if (copied > 0)
                    _logger.LogInformation("🔄 SyncMaster: {Copied} archivos copiados/actualizados desde docs/ global", copied);
            }

            // Cargar _dms_tree.json para metadatos DMS Enterprise
            var dmsMetadataByFile = new Dictionary<string, (DmsTreeDocument doc, DmsTreeCategory cat, DmsTreeSubcategory? subcat)>(StringComparer.OrdinalIgnoreCase);
            var treePaths = Directory.GetFiles(masterScopePath, "_dms_tree.json", SearchOption.AllDirectories);
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

            // Cargar registros master existentes de DB (para update en vez de purge+create)
            var existingMaster = await db.Documents
                .Where(d => d.Scope == DocumentScope.Software)
                .ToListAsync();
            var existingByPath = existingMaster.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);

            // Escanear AQSdocs_master/ y registrar/actualizar ficheros en DB
            var allFiles = Directory.GetFiles(masterScopePath, "*.*", SearchOption.AllDirectories)
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
                    // Actualizar registro existente
                    bool changed = existing.ContentHash != hash;

                    if (hasDmsMetadata)
                    {
                        var category = await DetectCategoryFromDmsCodeAsync(db, dmsMeta.cat.Code, dmsMeta.cat.Name, dmsMeta.cat.Icon);
                        existing.Title = dmsMeta.doc.Title;
                        existing.ContentHash = hash;
                        existing.FileSize = fileBytes.Length;
                        existing.FileType = DetectFileType(ext);
                        existing.Category = category;
                        existing.MinimumRole = DmsEnterpriseMappings.MapRole(dmsMeta.doc.MinimumRole, existing.MinimumRole ?? "SuperAdmin");
                        existing.Status = DmsEnterpriseMappings.MapStatus(dmsMeta.doc.Status);
                        existing.Version = dmsMeta.doc.Version;
                        existing.Source = "DMS_Enterprise";
                        existing.DocumentCode = dmsMeta.doc.Code;
                        existing.DmsSubcategoryCode = dmsMeta.subcat?.Code;
                        existing.DmsSubcategoryName = dmsMeta.subcat?.Name;
                        existing.DmsAuthor = dmsMeta.doc.Author;
                        existing.DmsPublishedAt = dmsMeta.doc.PublishedAt;
                        existing.Scope = DocumentScope.Software;
                        existing.UpdatedBy = userName;
                        existing.UpdatedAt = DateTime.UtcNow;
                        if (ext == ".md")
                            existing.SearchContent = ExtractSearchContent(Encoding.UTF8.GetString(fileBytes));
                        dmsUpdated++;
                    }
                    else if (changed || existing.Category == null)
                    {
                        existing.ContentHash = hash;
                        existing.FileSize = fileBytes.Length;
                        existing.Scope = DocumentScope.Software;
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
                    // Crear nuevo registro
                    if (hasDmsMetadata)
                    {
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
                    else
                    {
                        var title = ext == ".md"
                            ? (ExtractTitleFromContent(Encoding.UTF8.GetString(fileBytes)) ?? Path.GetFileNameWithoutExtension(filePath))
                            : Path.GetFileNameWithoutExtension(filePath);
                        var slug = GenerateUniqueSlug(db, title);
                        
                        db.Documents.Add(new Document
                        {
                            Id = Guid.NewGuid().ToString(),
                            Slug = slug,
                            Title = title,
                            FilePath = relativePath,
                            FileType = DetectFileType(ext),
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
                            SearchContent = ext == ".md" ? ExtractSearchContent(Encoding.UTF8.GetString(fileBytes)) : null
                        });
                        created++;
                    }
                }
            }

            // Eliminar huérfanos (registros DB sin archivo en disco)
            foreach (var orphan in existingByPath.Values)
            {
                if (orphan.Source == "DMS_Enterprise")
                {
                    var absPath = Path.Combine(projectDocsPath, orphan.FilePath.Replace('/', '\\'));
                    if (File.Exists(absPath))
                        continue;
                }
                db.Documents.Remove(orphan);
                orphaned++;
            }

            await db.SaveChangesAsync();

            var message = $"{created} creados, {updated} actualizados, {dmsUpdated} DMS actualizados, {orphaned} eliminados, {copied} copiados desde docs/";
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

            int created = 0, updated = 0, orphaned = 0, dmsUpdated = 0;

            // Cargar _dms_tree.json para metadatos
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

            // Escanear ficheros
            var existingProjectDocs = await db.Documents
                .Where(d => d.Scope == DocumentScope.Project)
                .ToListAsync();
            var existingByPath = existingProjectDocs.ToDictionary(d => d.FilePath, StringComparer.OrdinalIgnoreCase);

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
                    bool changed = existing.ContentHash != hash;
                    
                    if (hasDmsMetadata)
                    {
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
                    if (hasDmsMetadata)
                    {
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
                    else
                    {
                        var category = await DetectCategoryFromPathAsync(db, relativePath);
                        var title = ext == ".md"
                            ? (ExtractTitleFromContent(Encoding.UTF8.GetString(fileBytes)) ?? Path.GetFileNameWithoutExtension(filePath))
                            : Path.GetFileNameWithoutExtension(filePath);
                        var slug = GenerateUniqueSlug(db, title);

                        db.Documents.Add(new Document
                        {
                            Id = Guid.NewGuid().ToString(),
                            Slug = slug,
                            Title = title,
                            FilePath = relativePath,
                            FileType = DetectFileType(ext),
                            ContentHash = hash,
                            FileSize = fileBytes.Length,
                            Scope = DocumentScope.Project,
                            Category = category,
                            Version = "1.0",
                            Status = DocumentStatus.Draft,
                            Source = "local",
                            CreatedBy = userName,
                            CreatedAt = DateTime.UtcNow,
                            SearchContent = ext == ".md" ? ExtractSearchContent(Encoding.UTF8.GetString(fileBytes)) : null
                        });
                        created++;
                    }
                }
            }

            // Eliminar huérfanos (excepto DMS Enterprise cuyo archivo aún existe)
            foreach (var orphan in existingByPath.Values)
            {
                if (orphan.Source == "DMS_Enterprise")
                {
                    var absPath = Path.Combine(projectDocsPath, orphan.FilePath.Replace('/', '\\'));
                    if (File.Exists(absPath))
                        continue;
                }
                db.Documents.Remove(orphan);
                orphaned++;
            }

            await db.SaveChangesAsync();

            var message = $"{created} creados, {updated} actualizados, {dmsUpdated} DMS actualizados, {orphaned} eliminados";
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

    public async Task<DocumentOperationResponse> ProcessDmsNotifyAsync(DmsPublishNotifyRequest request, string userName)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(db);

            var projectDocsPath = _requestContext.DocsPath;
            if (string.IsNullOrEmpty(projectDocsPath))
                return new DocumentOperationResponse { Success = false, Message = "No se encontró carpeta docs/ del proyecto" };

            var folder = request.Folder?.Trim('/') ?? "AQSdocs_project";
            var scope = folder.StartsWith("AQSdocs_master", StringComparison.OrdinalIgnoreCase)
                ? DocumentScope.Software
                : DocumentScope.Project;

            var relativePath = $"{folder}/{request.File}".Replace('\\', '/');
            var absolutePath = Path.Combine(projectDocsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

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

            var categoryId = !string.IsNullOrWhiteSpace(request.CategoryCode)
                ? await DetectCategoryFromDmsCodeAsync(db, request.CategoryCode, request.CategoryName ?? request.CategoryCode, null)
                : SystemDocumentCategories.Other;

            var minimumRole = DmsEnterpriseMappings.MapRole(request.MinimumRole, scope == DocumentScope.Software ? "SuperAdmin" : "Operator");
            var status = DmsEnterpriseMappings.MapStatus(request.Status);

            var existingDoc = await db.Documents.FirstOrDefaultAsync(d => d.FilePath == relativePath);
            if (existingDoc == null && !string.IsNullOrWhiteSpace(request.DocumentCode))
                existingDoc = await db.Documents.FirstOrDefaultAsync(d => d.DocumentCode == request.DocumentCode && d.Source == "DMS_Enterprise");

            if (existingDoc != null)
            {
                existingDoc.Title = request.Title ?? existingDoc.Title;
                existingDoc.FilePath = relativePath;
                existingDoc.ContentHash = hash;
                existingDoc.FileSize = fileBytes.Length;
                existingDoc.FileType = DetectFileType(ext);
                existingDoc.Category = categoryId;
                existingDoc.MinimumRole = minimumRole;
                existingDoc.Status = status;
                existingDoc.Version = request.Version ?? existingDoc.Version;
                existingDoc.Source = request.Source ?? "DMS_Enterprise";
                existingDoc.DocumentCode = request.DocumentCode ?? existingDoc.DocumentCode;
                existingDoc.DmsSubcategoryCode = request.SubcategoryCode;
                existingDoc.DmsSubcategoryName = request.SubcategoryName;
                existingDoc.DmsAuthor = request.Author;
                existingDoc.DmsPublishedAt = request.PublishedAt;
                existingDoc.Scope = scope;
                existingDoc.UpdatedBy = userName;
                existingDoc.UpdatedAt = DateTime.UtcNow;
                if (ext == ".md")
                    existingDoc.SearchContent = ExtractSearchContent(Encoding.UTF8.GetString(fileBytes));

                await db.SaveChangesAsync();
                _logger.LogInformation("📤 DMS Notify: actualizado '{Title}' ({Code})", existingDoc.Title, existingDoc.DocumentCode);
                return new DocumentOperationResponse { Success = true, Message = $"Documento actualizado: {existingDoc.Title}" };
            }
            else
            {
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

    #region Categorías (solo lectura)

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

    #endregion

    #region Descarga

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

    public async Task<(Stream? FileStream, string? ContentType, string? FileName)?> DownloadFileAsync(string documentId, string userRole)
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
            var contentType = ExtToMime.GetValueOrDefault(ext, "application/octet-stream");
            var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, contentType, originalFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando fichero {DocumentId}", documentId);
            return null;
        }
    }

    #endregion

    #region Helpers privados

    /// <summary>
    /// Construye el detalle de un documento (solo metadatos, sin renderizar contenido).
    /// Los documentos se descargan directamente como PDF.
    /// </summary>
    private static DocumentDetail BuildDocumentDetail(Document doc)
    {
        return new DocumentDetail
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
                : null,
            // No se carga contenido — el frontend descarga directamente el PDF
            HtmlContent = null,
            RawContent = null
        };
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

    private Task<IQueryable<Document>> ApplyAccessFilterWithMatrixAsync(
        AquafrischDbContext db, IQueryable<Document> query, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        
        if (roleLevel >= 5) return Task.FromResult(query);

        // Filtrar por MinimumRole: excluir documentos que requieren un rol superior al del usuario
        var excludedRoles = RoleHierarchy
            .Where(kv => kv.Value > roleLevel)
            .Select(kv => kv.Key)
            .ToList();

        var filtered = query
            .Where(d => string.IsNullOrEmpty(d.MinimumRole) || !excludedRoles.Contains(d.MinimumRole));

        return Task.FromResult(filtered);
    }

    private bool HasAccessToDocument(Document doc, string userRole)
    {
        var roleLevel = RoleHierarchy.GetValueOrDefault(userRole, 0);
        if (roleLevel >= 5) return true;

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
        var text = Regex.Replace(markdownContent, @"```[\s\S]*?```", " ");
        text = Regex.Replace(text, @"[#*_~`\[\]\(\)>|]", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string? ExtractTitleFromContent(string content)
    {
        var match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private async Task<int> DetectCategoryFromPathAsync(AquafrischDbContext db, string relativePath)
    {
        var parts = relativePath.Split('/');
        
        int startIdx = 0;
        if (parts.Length > 0 && (parts[0].Equals("AQSdocs_master", StringComparison.OrdinalIgnoreCase) 
            || parts[0].Equals("AQSdocs_project", StringComparison.OrdinalIgnoreCase)))
        {
            startIdx = 1;
        }
        
        if (parts.Length < startIdx + 2) return SystemDocumentCategories.Other;

        var folderName = parts[startIdx];
        
        var allCategories = await db.DocumentCategories.ToListAsync();
        
        var matchedCat = allCategories.FirstOrDefault(c => 
            string.Equals(c.FolderName, folderName, StringComparison.OrdinalIgnoreCase));
        
        if (matchedCat != null) return matchedCat.Id;
        
        if (parts.Length >= startIdx + 3)
        {
            var subFolderName = parts[startIdx + 1];
            var matchedSub = allCategories.FirstOrDefault(c => 
                string.Equals(c.FolderName, subFolderName, StringComparison.OrdinalIgnoreCase) && c.ParentId != null);
            if (matchedSub != null) return matchedSub.Id;
        }

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

    private async Task<int> DetectCategoryFromDmsCodeAsync(AquafrischDbContext db, string code, string name, string? icon)
    {
        var allCategories = await db.DocumentCategories.ToListAsync();

        var existing = allCategories.FirstOrDefault(c =>
            string.Equals(c.FolderName, code, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        existing = allCategories.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

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
}
