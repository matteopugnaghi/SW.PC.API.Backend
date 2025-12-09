using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller para gestión de proyectos multi-proyecto.
    /// Permite listar proyectos, ver el activo, crear backups, etc.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectContextService _projectContext;
        private readonly ILogger<ProjectsController> _logger;
        
        public ProjectsController(
            IProjectContextService projectContext,
            ILogger<ProjectsController> logger)
        {
            _projectContext = projectContext;
            _logger = logger;
        }
        
        /// <summary>
        /// Lista todos los proyectos disponibles
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<ProjectInfo>> GetProjects()
        {
            try
            {
                var projects = _projectContext.GetAvailableProjects();
                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available projects");
                return StatusCode(500, new { error = "Error retrieving projects", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene información del proyecto activo
        /// </summary>
        [HttpGet("active")]
        public ActionResult<object> GetActiveProject()
        {
            try
            {
                return Ok(new
                {
                    projectId = _projectContext.ActiveProjectId,
                    isMultiProjectMode = _projectContext.IsMultiProjectMode,
                    paths = new
                    {
                        basePath = _projectContext.ProjectBasePath,
                        configPath = _projectContext.ConfigPath,
                        modelsPath = _projectContext.ModelsPath,
                        dataPath = _projectContext.DataPath,
                        backupsPath = _projectContext.BackupsPath,
                        excelConfigPath = _projectContext.ExcelConfigPath,
                        databasePath = _projectContext.DatabasePath
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active project");
                return StatusCode(500, new { error = "Error retrieving active project", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Verifica si existe un proyecto
        /// </summary>
        [HttpGet("{projectId}/exists")]
        public ActionResult<object> ProjectExists(string projectId)
        {
            try
            {
                var exists = _projectContext.ProjectExists(projectId);
                return Ok(new { projectId, exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if project exists: {ProjectId}", projectId);
                return StatusCode(500, new { error = "Error checking project", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Crea la estructura de carpetas para un nuevo proyecto
        /// </summary>
        [HttpPost("{projectId}/create")]
        public async Task<ActionResult<object>> CreateProject(string projectId)
        {
            try
            {
                // Validar ID de proyecto
                if (string.IsNullOrWhiteSpace(projectId))
                {
                    return BadRequest(new { error = "Project ID cannot be empty" });
                }
                
                // Validar caracteres permitidos
                if (!System.Text.RegularExpressions.Regex.IsMatch(projectId, @"^[a-zA-Z0-9_-]+$"))
                {
                    return BadRequest(new { error = "Project ID can only contain letters, numbers, hyphens and underscores" });
                }
                
                if (_projectContext.ProjectExists(projectId))
                {
                    return Conflict(new { error = $"Project '{projectId}' already exists" });
                }
                
                var success = await _projectContext.CreateProjectStructureAsync(projectId);
                
                if (success)
                {
                    _logger.LogInformation("✅ Project created: {ProjectId}", projectId);
                    return Ok(new
                    {
                        success = true,
                        message = $"Project '{projectId}' created successfully",
                        projectId,
                        nextSteps = new[]
                        {
                            $"1. Copy ProjectConfig.xlsm to Projects/{projectId}/config/",
                            $"2. Copy 3D models to Projects/{projectId}/models/",
                            $"3. Update active-project.json with: {{\"activeProject\": \"{projectId}\"}}",
                            "4. Restart the backend"
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new { error = "Failed to create project structure" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project: {ProjectId}", projectId);
                return StatusCode(500, new { error = "Error creating project", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene instrucciones para cambiar de proyecto
        /// </summary>
        [HttpGet("{projectId}/switch-instructions")]
        public ActionResult<object> GetSwitchInstructions(string projectId)
        {
            try
            {
                if (!_projectContext.ProjectExists(projectId))
                {
                    return NotFound(new { error = $"Project '{projectId}' not found" });
                }
                
                return Ok(new
                {
                    currentProject = _projectContext.ActiveProjectId,
                    targetProject = projectId,
                    instructions = new[]
                    {
                        "To switch projects, follow these steps:",
                        "",
                        "1. Edit active-project.json in the backend root folder",
                        $"2. Change 'activeProject' value to: \"{projectId}\"",
                        "3. Save the file",
                        "4. Restart the backend service",
                        "",
                        "PowerShell command:",
                        $"  '{{\"activeProject\": \"{projectId}\"}}' | Out-File active-project.json -Encoding UTF8"
                    },
                    note = "A backend restart is required to switch projects. This is by design to ensure data integrity."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting switch instructions for: {ProjectId}", projectId);
                return StatusCode(500, new { error = "Error getting instructions", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Recarga la configuración del proyecto (sin cambiar de proyecto)
        /// </summary>
        [HttpPost("reload")]
        public ActionResult<object> ReloadProjectContext()
        {
            try
            {
                var previousProject = _projectContext.ActiveProjectId;
                _projectContext.ReloadActiveProject();
                var currentProject = _projectContext.ActiveProjectId;
                
                return Ok(new
                {
                    success = true,
                    previousProject,
                    currentProject,
                    changed = previousProject != currentProject,
                    note = currentProject != previousProject 
                        ? "Project changed - some services may need restart to reload configuration"
                        : "Configuration reloaded"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading project context");
                return StatusCode(500, new { error = "Error reloading project", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Lista los backups disponibles del proyecto activo
        /// </summary>
        [HttpGet("backups")]
        public ActionResult<object> GetBackups()
        {
            try
            {
                var backupsPath = _projectContext.BackupsPath;
                
                if (!Directory.Exists(backupsPath))
                {
                    return Ok(new
                    {
                        projectId = _projectContext.ActiveProjectId,
                        backupsPath,
                        backups = Array.Empty<object>()
                    });
                }
                
                var backupFiles = Directory.GetFiles(backupsPath, "*.zip")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Select(f => new
                    {
                        fileName = f.Name,
                        path = f.FullName,
                        sizeBytes = f.Length,
                        sizeMB = Math.Round(f.Length / 1024.0 / 1024.0, 2),
                        createdAt = f.CreationTime,
                        modifiedAt = f.LastWriteTime
                    })
                    .ToList();
                
                return Ok(new
                {
                    projectId = _projectContext.ActiveProjectId,
                    backupsPath,
                    count = backupFiles.Count,
                    backups = backupFiles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backups");
                return StatusCode(500, new { error = "Error retrieving backups", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Crea un backup del proyecto activo
        /// </summary>
        [HttpPost("backup")]
        public async Task<ActionResult<object>> CreateBackup([FromQuery] bool includeDatabase = true, [FromQuery] bool includeModels = true)
        {
            try
            {
                var projectId = _projectContext.ActiveProjectId;
                var backupsPath = _projectContext.BackupsPath;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                var backupFileName = $"{projectId}_backup_{timestamp}.zip";
                var backupFilePath = Path.Combine(backupsPath, backupFileName);
                
                // Asegurar que existe la carpeta de backups
                if (!Directory.Exists(backupsPath))
                {
                    Directory.CreateDirectory(backupsPath);
                }
                
                // Crear archivo ZIP temporal
                var tempDir = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    var includedItems = new List<string>();
                    
                    // Copiar config
                    var configSource = _projectContext.ConfigPath;
                    if (Directory.Exists(configSource))
                    {
                        var configDest = Path.Combine(tempDir, "config");
                        CopyDirectory(configSource, configDest);
                        includedItems.Add("config");
                    }
                    
                    // Copiar modelos (opcional)
                    if (includeModels)
                    {
                        var modelsSource = _projectContext.ModelsPath;
                        if (Directory.Exists(modelsSource))
                        {
                            var modelsDest = Path.Combine(tempDir, "models");
                            CopyDirectory(modelsSource, modelsDest);
                            includedItems.Add("models");
                        }
                    }
                    
                    // Copiar database (opcional)
                    if (includeDatabase)
                    {
                        var dbSource = _projectContext.DatabasePath;
                        if (System.IO.File.Exists(dbSource))
                        {
                            var dataDest = Path.Combine(tempDir, "data");
                            Directory.CreateDirectory(dataDest);
                            System.IO.File.Copy(dbSource, Path.Combine(dataDest, Path.GetFileName(dbSource)));
                            includedItems.Add("data");
                        }
                    }
                    
                    // Crear manifest
                    var manifest = new
                    {
                        projectId,
                        backupDate = DateTime.Now,
                        backupType = "full",
                        includedItems,
                        includeDatabase,
                        includeModels,
                        version = "1.0"
                    };
                    
                    var manifestPath = Path.Combine(tempDir, "manifest.json");
                    await System.IO.File.WriteAllTextAsync(manifestPath, 
                        System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    
                    // Crear ZIP
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, backupFilePath);
                    
                    var fileInfo = new FileInfo(backupFilePath);
                    
                    _logger.LogInformation("✅ Backup created: {FileName} ({Size:F2} MB)", backupFileName, fileInfo.Length / 1024.0 / 1024.0);
                    
                    return Ok(new
                    {
                        success = true,
                        projectId,
                        fileName = backupFileName,
                        filePath = backupFilePath,
                        sizeBytes = fileInfo.Length,
                        sizeMB = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2),
                        includedItems,
                        createdAt = DateTime.Now
                    });
                }
                finally
                {
                    // Limpiar carpeta temporal
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                return StatusCode(500, new { error = "Error creating backup", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Descarga un backup específico
        /// </summary>
        [HttpGet("backup/{fileName}/download")]
        public ActionResult DownloadBackup(string fileName)
        {
            try
            {
                var backupsPath = _projectContext.BackupsPath;
                var filePath = Path.Combine(backupsPath, fileName);
                
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { error = $"Backup file not found: {fileName}" });
                }
                
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading backup: {FileName}", fileName);
                return StatusCode(500, new { error = "Error downloading backup", details = ex.Message });
            }
        }
        
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                System.IO.File.Copy(file, destFile);
            }
            
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
