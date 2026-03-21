using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller para gestión de proyectos multi-proyecto.
    /// Permite listar proyectos, ver el activo, crear backups, etc.
    /// 
    /// En Development: Soporta multi-tenant via header X-Project-Id
    /// En Production: Siempre usa el proyecto de active-project.json
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectContextService _globalContext;
        private readonly IRequestProjectContext _requestContext;
        private readonly IExcelConfigService _excelConfigService;
        private readonly ITwinCATService _twinCATService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProjectsController> _logger;
        
        public ProjectsController(
            IProjectContextService globalContext,
            IRequestProjectContext requestContext,
            IExcelConfigService excelConfigService,
            ITwinCATService twinCATService,
            IWebHostEnvironment environment,
            ILogger<ProjectsController> logger)
        {
            _globalContext = globalContext;
            _requestContext = requestContext;
            _excelConfigService = excelConfigService;
            _twinCATService = twinCATService;
            _environment = environment;
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
                var projects = _globalContext.GetAvailableProjects().ToList();
                
                // Marcar el proyecto activo del request actual (en development puede diferir del global)
                foreach (var project in projects)
                {
                    project.IsActive = project.Id == _requestContext.ProjectId;
                }
                
                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available projects");
                return StatusCode(500, new { error = "Error retrieving projects", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene información del proyecto activo para este request
        /// 
        /// allowProjectSelection: viene de ASPNETCORE_ENVIRONMENT (Development = true)
        ///   → Controla si el frontend muestra el selector de proyectos
        ///   → El servidor empresa usa --environment Development para permitirlo
        /// 
        /// environmentMode: viene del Excel (System Config → EnvironmentMode)
        ///   → Controla el Git panel (production = solo TwinCAT editable)
        /// </summary>
        [HttpGet("active")]
        public ActionResult<object> GetActiveProject()
        {
            try
            {
                // Leer EnvironmentMode del Excel (System Config) → controla Git panel
                var systemConfig = _excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm").GetAwaiter().GetResult();
                var environmentMode = systemConfig?.EnvironmentMode?.ToLower() ?? "development";
                
                // ASPNETCORE_ENVIRONMENT → controla si se permite seleccionar proyectos
                // El servidor empresa usa --environment Development para multi-tenant
                var allowProjectSelection = _environment.IsDevelopment();
                
                return Ok(new
                {
                    projectId = _requestContext.ProjectId,
                    isMultiProjectMode = _requestContext.IsMultiProjectMode,
                    isDevelopmentMode = environmentMode == "development", // Mantener por compatibilidad
                    allowProjectSelection = allowProjectSelection, // NUEVO: basado en ASPNETCORE_ENVIRONMENT
                    environmentMode = environmentMode,
                    globalProjectId = _globalContext.ActiveProjectId,
                    paths = new
                    {
                        basePath = _requestContext.ProjectBasePath,
                        configPath = _requestContext.ConfigPath,
                        modelsPath = _requestContext.ModelsPath,
                        dataPath = _requestContext.DataPath,
                        backupsPath = _requestContext.BackupsPath,
                        excelConfigPath = _requestContext.ExcelConfigPath,
                        databasePath = _requestContext.DatabasePath
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
                var exists = _globalContext.ProjectExists(projectId);
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
                
                if (_globalContext.ProjectExists(projectId))
                {
                    return Conflict(new { error = $"Project '{projectId}' already exists" });
                }
                
                var success = await _globalContext.CreateProjectStructureAsync(projectId);
                
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
                if (!_globalContext.ProjectExists(projectId))
                {
                    return NotFound(new { error = $"Project '{projectId}' not found" });
                }
                
                return Ok(new
                {
                    currentProject = _globalContext.ActiveProjectId,
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
        /// Activa un proyecto escribiendo active-project.json y recargando el contexto.
        /// SOLO disponible en Development — en Production devuelve 403.
        /// </summary>
        [HttpPost("{projectId}/activate")]
        public async Task<ActionResult<object>> ActivateProject(string projectId)
        {
            try
            {
                if (!_environment.IsDevelopment())
                {
                    return StatusCode(403, new { error = "Project activation via API is only available in Development mode. Edit active-project.json manually and restart." });
                }
                
                var previousProject = _globalContext.ActiveProjectId;
                
                var success = _globalContext.SetActiveProject(projectId);
                if (!success)
                {
                    return BadRequest(new { error = $"Failed to activate project '{projectId}'. Does it exist?" });
                }
                
                // ⭐ Reconfigurar TwinCAT con el AMS Net ID del nuevo proyecto
                string plcReconfigResult = "not attempted";
                try
                {
                    var excelPath = _globalContext.ExcelConfigPath;
                    if (System.IO.File.Exists(excelPath))
                    {
                        var systemConfig = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                        if (systemConfig != null)
                        {
                            var reconfigured = await _twinCATService.ReconfigureAsync(
                                systemConfig.PlcAmsNetId,
                                systemConfig.PlcAdsPort,
                                systemConfig.UseSimulatedPlc);
                            plcReconfigResult = reconfigured 
                                ? $"Connected to {systemConfig.PlcAmsNetId}:{systemConfig.PlcAdsPort}" 
                                : $"Failed to connect to {systemConfig.PlcAmsNetId}:{systemConfig.PlcAdsPort}";
                            if (systemConfig.UseSimulatedPlc)
                                plcReconfigResult = "Simulated mode";
                        }
                        else
                        {
                            plcReconfigResult = "No SystemConfiguration in Excel";
                        }
                    }
                    else
                    {
                        plcReconfigResult = $"Excel not found: {excelPath}";
                    }
                }
                catch (Exception plcEx)
                {
                    _logger.LogWarning(plcEx, "⚠️ Could not reconfigure TwinCAT for project {ProjectId}", projectId);
                    plcReconfigResult = $"Error: {plcEx.Message}";
                }
                
                return Ok(new
                {
                    success = true,
                    previousProject,
                    activeProject = _globalContext.ActiveProjectId,
                    message = $"Project switched from '{previousProject}' to '{projectId}'. active-project.json updated.",
                    plcConnection = plcReconfigResult,
                    note = "TwinCAT connection reconfigured for new project."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating project: {ProjectId}", projectId);
                return StatusCode(500, new { error = "Error activating project", details = ex.Message });
            }
        }
        
        /// <summary>
        /// Recarga la configuración del proyecto (sin cambiar de proyecto)
        /// </summary>
        [HttpPost("reload")]
        public async Task<ActionResult<object>> ReloadProjectContext()
        {
            try
            {
                var previousProject = _globalContext.ActiveProjectId;
                _globalContext.ReloadActiveProject();
                var currentProject = _globalContext.ActiveProjectId;
                
                // ⭐ Si el proyecto cambió, reconfigurar TwinCAT
                string plcReconfigResult = "no change";
                if (previousProject != currentProject)
                {
                    try
                    {
                        var excelPath = _globalContext.ExcelConfigPath;
                        if (System.IO.File.Exists(excelPath))
                        {
                            var systemConfig = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                            if (systemConfig != null)
                            {
                                var reconfigured = await _twinCATService.ReconfigureAsync(
                                    systemConfig.PlcAmsNetId,
                                    systemConfig.PlcAdsPort,
                                    systemConfig.UseSimulatedPlc);
                                plcReconfigResult = reconfigured 
                                    ? $"Connected to {systemConfig.PlcAmsNetId}:{systemConfig.PlcAdsPort}" 
                                    : $"Failed to connect to {systemConfig.PlcAmsNetId}:{systemConfig.PlcAdsPort}";
                                if (systemConfig.UseSimulatedPlc)
                                    plcReconfigResult = "Simulated mode";
                            }
                        }
                    }
                    catch (Exception plcEx)
                    {
                        _logger.LogWarning(plcEx, "⚠️ Could not reconfigure TwinCAT after reload");
                        plcReconfigResult = $"Error: {plcEx.Message}";
                    }
                }
                
                return Ok(new
                {
                    success = true,
                    previousProject,
                    currentProject,
                    changed = previousProject != currentProject,
                    plcConnection = plcReconfigResult,
                    note = currentProject != previousProject 
                        ? "Project changed - TwinCAT connection reconfigured"
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
        /// Lista los backups disponibles del proyecto activo (según request)
        /// </summary>
        [HttpGet("backups")]
        public ActionResult<object> GetBackups()
        {
            try
            {
                var projectId = _requestContext.ProjectId;
                var backupsPath = _requestContext.BackupsPath;
                
                if (!Directory.Exists(backupsPath))
                {
                    return Ok(new
                    {
                        projectId,
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
                    projectId,
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
        /// Crea un backup del proyecto activo (según request)
        /// </summary>
        [HttpPost("backup")]
        public async Task<ActionResult<object>> CreateBackup([FromQuery] bool includeDatabase = true, [FromQuery] bool includeModels = true)
        {
            try
            {
                var projectId = _requestContext.ProjectId;
                var backupsPath = _requestContext.BackupsPath;
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
                    var configSource = _requestContext.ConfigPath;
                    if (Directory.Exists(configSource))
                    {
                        var configDest = Path.Combine(tempDir, "config");
                        CopyDirectory(configSource, configDest);
                        includedItems.Add("config");
                    }
                    
                    // Copiar modelos (opcional)
                    if (includeModels)
                    {
                        var modelsSource = _requestContext.ModelsPath;
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
                        var dbSource = _requestContext.DatabasePath;
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
        /// Descarga un backup específico (del proyecto del request)
        /// </summary>
        [HttpGet("backup/{fileName}/download")]
        public ActionResult DownloadBackup(string fileName)
        {
            try
            {
                var backupsPath = _requestContext.BackupsPath;
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
