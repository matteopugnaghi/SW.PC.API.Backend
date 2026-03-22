using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio que gestiona el contexto del proyecto activo.
    /// Lee el proyecto activo desde active-project.json y proporciona
    /// las rutas correctas para config, models, data y backups.
    /// </summary>
    public interface IProjectContextService
    {
        /// <summary>
        /// ID del proyecto activo (ej: "AQF-ALSTOM-001")
        /// </summary>
        string ActiveProjectId { get; }
        
        /// <summary>
        /// Ruta base del proyecto activo (ej: "Projects/AQF-ALSTOM-001")
        /// </summary>
        string ProjectBasePath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de configuración del proyecto (Excel, etc.)
        /// </summary>
        string ConfigPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de modelos 3D del proyecto
        /// </summary>
        string ModelsPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de datos del proyecto (SQLite, etc.)
        /// </summary>
        string DataPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de backups del proyecto
        /// </summary>
        string BackupsPath { get; }
        
        /// <summary>
        /// Ruta completa al archivo Excel de configuración del proyecto
        /// </summary>
        string ExcelConfigPath { get; }
        
        /// <summary>
        /// Ruta completa a la base de datos SQLite del proyecto
        /// </summary>
        string DatabasePath { get; }
        
        /// <summary>
        /// Indica si el proyecto está usando la estructura multi-proyecto
        /// o la estructura legacy (ExcelConfigs/, wwwroot/models/, Data/)
        /// </summary>
        bool IsMultiProjectMode { get; }

        /// <summary>
        /// Ruta a la carpeta de documentación del proyecto (DMS)
        /// </summary>
        string DocsPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de logs JSONL para NxLog (SOC PIVOT TISSEO)
        /// </summary>
        string LogsPath { get; }

        /// <summary>
        /// Ruta raíz de la carpeta Projects/ (respeta ProjectsRootPath de config)
        /// </summary>
        string ProjectsRootPath { get; }
        
        /// <summary>
        /// Lista todos los proyectos disponibles en la carpeta Projects/
        /// </summary>
        IEnumerable<ProjectInfo> GetAvailableProjects();
        
        /// <summary>
        /// Verifica si existe un proyecto con el ID especificado
        /// </summary>
        bool ProjectExists(string projectId);
        
        /// <summary>
        /// Crea la estructura de carpetas para un nuevo proyecto
        /// </summary>
        Task<bool> CreateProjectStructureAsync(string projectId);
        
        /// <summary>
        /// Recarga la configuración del proyecto activo desde active-project.json
        /// </summary>
        void ReloadActiveProject();

        /// <summary>
        /// Cambia el proyecto activo escribiendo active-project.json y recargando.
        /// Solo debe usarse en Development.
        /// </summary>
        bool SetActiveProject(string projectId);

        /// <summary>
        /// Evento disparado cuando el proyecto activo cambia.
        /// Los servicios Singleton deben suscribirse para recargar su configuración.
        /// El parámetro es el nuevo projectId.
        /// </summary>
        event Action<string>? OnProjectChanged;
    }
    
    /// <summary>
    /// Información básica de un proyecto
    /// </summary>
    public class ProjectInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool HasConfig { get; set; }
        public bool HasModels { get; set; }
        public bool HasDatabase { get; set; }
        public DateTime? LastModified { get; set; }
        public bool IsActive { get; set; }
    }
    
    public class ProjectContextService : IProjectContextService
    {
        private readonly ILogger<ProjectContextService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _contentRootPath;
        private readonly string _projectsRootPath;
        private readonly string _activeProjectFilePath;

        /// <inheritdoc />
        public event Action<string>? OnProjectChanged;
        
        private string _activeProjectId = "default";
        private bool _isMultiProjectMode = false;
        
        // Rutas legacy (compatibilidad hacia atrás)
        private readonly string _legacyExcelConfigPath;
        private readonly string _legacyModelsPath;
        private readonly string _legacyDataPath;
        
        public ProjectContextService(
            IWebHostEnvironment environment,
            ILogger<ProjectContextService> logger,
            IServiceProvider serviceProvider,
            IConfiguration? configuration = null)
        {
            _environment = environment;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _contentRootPath = environment.ContentRootPath;
            var customProjectsPath = configuration?.GetValue<string>("ProjectsRootPath");
            _projectsRootPath = !string.IsNullOrEmpty(customProjectsPath) ? customProjectsPath : Path.Combine(_contentRootPath, "Projects");
            _activeProjectFilePath = Path.Combine(_contentRootPath, "active-project.json");
            
            // Rutas legacy
            _legacyExcelConfigPath = Path.Combine(_contentRootPath, "ExcelConfigs");
            _legacyModelsPath = Path.Combine(environment.WebRootPath, "models");
            _legacyDataPath = Path.Combine(_contentRootPath, "Data");
            
            // Cargar proyecto activo al iniciar
            LoadActiveProject();
            
            // Asegurar que existe la estructura base
            EnsureProjectsDirectoryExists();
        }
        
        public string ActiveProjectId => _activeProjectId;
        
        public string ProjectBasePath => _isMultiProjectMode 
            ? Path.Combine(_projectsRootPath, _activeProjectId)
            : _contentRootPath;
        
        public string ConfigPath => _isMultiProjectMode
            ? Path.Combine(ProjectBasePath, "config")
            : _legacyExcelConfigPath;
        
        public string ModelsPath => _isMultiProjectMode
            ? Path.Combine(ProjectBasePath, "models")
            : _legacyModelsPath;
        
        public string DataPath => _isMultiProjectMode
            ? Path.Combine(ProjectBasePath, "data")
            : _legacyDataPath;
        
        public string BackupsPath => Path.Combine(
            _isMultiProjectMode ? ProjectBasePath : _contentRootPath, 
            "backups");
        
        public string ProjectsRootPath => _projectsRootPath;
        
        public string ExcelConfigPath
        {
            get
            {
                if (_isMultiProjectMode)
                {
                    // Buscar archivos Excel en la carpeta config del proyecto
                    var configDir = ConfigPath;
                    if (Directory.Exists(configDir))
                    {
                        var excelFiles = Directory.GetFiles(configDir, "*.xlsm")
                            .Concat(Directory.GetFiles(configDir, "*.xlsx"))
                            .Concat(Directory.GetFiles(configDir, "*.xls"))
                            .ToArray();
                        
                        if (excelFiles.Length > 0)
                        {
                            // Priorizar ProjectConfig.xlsm, luego .xlsx, luego .xls
                            var projectConfig = excelFiles.FirstOrDefault(f => 
                                Path.GetFileName(f).Equals("ProjectConfig.xlsm", StringComparison.OrdinalIgnoreCase))
                                ?? excelFiles.FirstOrDefault(f => 
                                Path.GetFileName(f).Equals("ProjectConfig.xlsx", StringComparison.OrdinalIgnoreCase))
                                ?? excelFiles.FirstOrDefault(f => 
                                Path.GetFileName(f).Equals("ProjectConfig.xls", StringComparison.OrdinalIgnoreCase));
                            
                            return projectConfig ?? excelFiles[0];
                        }
                    }
                }
                
                // Legacy: buscar en ExcelConfigs/
                return Path.Combine(_legacyExcelConfigPath, "ProjectConfig.xlsm");
            }
        }
        
        public string DatabasePath => _isMultiProjectMode
            ? Path.Combine(DataPath, "project.db")
            : Path.Combine(_legacyDataPath, "Aquafrisch.db");
        
        public string DocsPath => _isMultiProjectMode
            ? Path.Combine(ProjectBasePath, "docs")
            : Path.Combine(_contentRootPath, "docs");
        
        /// <summary>
        /// Ruta a la carpeta de logs JSONL para NxLog (SOC PIVOT TISSEO)
        /// </summary>
        public string LogsPath => _isMultiProjectMode
            ? Path.Combine(ProjectBasePath, "logs")
            : Path.Combine(_contentRootPath, "wwwroot", "logs");
        
        public bool IsMultiProjectMode => _isMultiProjectMode;
        
        public IEnumerable<ProjectInfo> GetAvailableProjects()
        {
            var projects = new List<ProjectInfo>();
            
            // Añadir proyecto "default" (legacy mode)
            projects.Add(new ProjectInfo
            {
                Id = "default",
                Name = "Default (Legacy Mode)",
                Path = _contentRootPath,
                HasConfig = Directory.Exists(_legacyExcelConfigPath) && 
                           Directory.GetFiles(_legacyExcelConfigPath, "*.xls*").Any(),
                HasModels = Directory.Exists(_legacyModelsPath) &&
                           Directory.GetFiles(_legacyModelsPath, "*.glb").Any(),
                HasDatabase = File.Exists(Path.Combine(_legacyDataPath, "Aquafrisch.db")),
                LastModified = Directory.Exists(_legacyExcelConfigPath) 
                    ? new DirectoryInfo(_legacyExcelConfigPath).LastWriteTime 
                    : null,
                IsActive = _activeProjectId == "default"
            });
            
            // Escanear carpeta Projects/
            if (Directory.Exists(_projectsRootPath))
            {
                foreach (var projectDir in Directory.GetDirectories(_projectsRootPath))
                {
                    var projectId = Path.GetFileName(projectDir);
                    
                    // Ignorar carpetas especiales
                    if (projectId.StartsWith("_") || projectId.StartsWith("."))
                        continue;
                    
                    var configPath = Path.Combine(projectDir, "config");
                    var modelsPath = Path.Combine(projectDir, "models");
                    var dataPath = Path.Combine(projectDir, "data");
                    
                    projects.Add(new ProjectInfo
                    {
                        Id = projectId,
                        Name = projectId.Replace("-", " ").Replace("_", " "),
                        Path = projectDir,
                        HasConfig = Directory.Exists(configPath) && 
                                   Directory.GetFiles(configPath, "*.xls*").Any(),
                        HasModels = Directory.Exists(modelsPath) &&
                                   Directory.EnumerateFiles(modelsPath, "*.glb", SearchOption.AllDirectories).Any(),
                        HasDatabase = File.Exists(Path.Combine(dataPath, "project.db")),
                        LastModified = new DirectoryInfo(projectDir).LastWriteTime,
                        IsActive = _activeProjectId == projectId
                    });
                }
            }
            
            return projects.OrderBy(p => p.Id == "default" ? 0 : 1).ThenBy(p => p.Name);
        }
        
        public bool ProjectExists(string projectId)
        {
            if (projectId == "default")
                return true;
            
            var projectPath = Path.Combine(_projectsRootPath, projectId);
            return Directory.Exists(projectPath);
        }
        
        public async Task<bool> CreateProjectStructureAsync(string projectId)
        {
            try
            {
                var projectPath = Path.Combine(_projectsRootPath, projectId);
                
                if (Directory.Exists(projectPath))
                {
                    _logger.LogWarning("Project {ProjectId} already exists", projectId);
                    return false;
                }
                
                // Crear estructura de carpetas
                Directory.CreateDirectory(projectPath);
                Directory.CreateDirectory(Path.Combine(projectPath, "config"));
                Directory.CreateDirectory(Path.Combine(projectPath, "models"));
                Directory.CreateDirectory(Path.Combine(projectPath, "data"));
                Directory.CreateDirectory(Path.Combine(projectPath, "backups"));
                
                // NxLog: Carpeta para ficheros JSONL (SOC PIVOT TISSEO)
                Directory.CreateDirectory(Path.Combine(projectPath, "logs"));
                
                // DMS: Estructura documental del proyecto
                var docsPath = Path.Combine(projectPath, "docs");
                Directory.CreateDirectory(docsPath);
                Directory.CreateDirectory(Path.Combine(docsPath, "compliance"));
                Directory.CreateDirectory(Path.Combine(docsPath, "cra-generic"));
                Directory.CreateDirectory(Path.Combine(docsPath, "user-guides"));
                Directory.CreateDirectory(Path.Combine(docsPath, "technical"));
                Directory.CreateDirectory(Path.Combine(docsPath, "electrical"));
                Directory.CreateDirectory(Path.Combine(docsPath, "maintenance"));
                Directory.CreateDirectory(Path.Combine(docsPath, "_attachments"));
                
                // Crear archivo README
                var readmePath = Path.Combine(projectPath, "README.md");
                await File.WriteAllTextAsync(readmePath, $@"# Proyecto: {projectId}

## Estructura de carpetas

- **config/**: Archivos de configuración Excel (ProjectConfig.xlsm)
- **models/**: Modelos 3D (.glb, .gltf)
- **data/**: Base de datos SQLite (project.db)
- **backups/**: Copias de seguridad automáticas y manuales
- **docs/**: Documentación del proyecto (DMS)
  - compliance/ - Documentación CRA específica de esta instalación
  - cra-generic/ - Documentación CRA genérica del SW (copia por versión)
  - user-guides/ - Manuales de usuario y operador
  - technical/ - Documentación técnica del proyecto
  - electrical/ - Esquemas eléctricos (wrappers MD + PDFs adjuntos)
  - maintenance/ - Procedimientos de mantenimiento
  - _attachments/ - Archivos binarios adjuntos (PDF, imágenes)

## Creado

- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
- Por: Sistema Multi-Proyecto SW.PC.API.Backend

## Notas

Copiar el archivo ProjectConfig.xlsm a la carpeta config/ y configurar según necesidades.
");
                
                _logger.LogInformation("✅ Project structure created: {ProjectId}", projectId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project structure for {ProjectId}", projectId);
                return false;
            }
        }
        
        public void ReloadActiveProject()
        {
            LoadActiveProject();
        }

        public bool SetActiveProject(string projectId)
        {
            try
            {
                // Validar que el proyecto existe (o es "default")
                if (projectId != "default" && !ProjectExists(projectId))
                {
                    _logger.LogWarning("Cannot set active project: {ProjectId} does not exist", projectId);
                    return false;
                }

                var config = new
                {
                    activeProject = projectId,
                    description = "Identificador del proyecto activo. Debe coincidir con una carpeta en Projects/"
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_activeProjectFilePath, json);

                _logger.LogInformation("\u2705 active-project.json updated to: {ProjectId}", projectId);

                // Recargar para que el singleton refleje el cambio
                LoadActiveProject();

                // Re-detectar rutas TwinCAT para el nuevo proyecto
                try
                {
                    var integrity = _serviceProvider?.GetService<ISoftwareIntegrityService>();
                    integrity?.RedetectPaths();
                }
                catch (Exception redetectEx)
                {
                    _logger.LogWarning(redetectEx, "Could not re-detect TwinCAT paths after project change");
                }

                // 🔄 Notificar a todos los servicios Singleton del cambio de proyecto
                try
                {
                    _logger.LogInformation("🔄 Disparando OnProjectChanged para proyecto: {ProjectId}", projectId);
                    OnProjectChanged?.Invoke(projectId);
                }
                catch (Exception eventEx)
                {
                    _logger.LogWarning(eventEx, "⚠️ Error en handler de OnProjectChanged");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active project to {ProjectId}", projectId);
                return false;
            }
        }
        
        private void LoadActiveProject()
        {
            try
            {
                // 🔧 PRIORIDAD 1: Variable de entorno (para multi-instancia en servidor empresa)
                var envProject = Environment.GetEnvironmentVariable("SUPERVISOR_PROJECT");
                if (!string.IsNullOrWhiteSpace(envProject))
                {
                    _activeProjectId = envProject;
                    var projectPath = Path.Combine(_projectsRootPath, _activeProjectId);
                    _isMultiProjectMode = _activeProjectId != "default" && Directory.Exists(projectPath);
                    
                    _logger.LogInformation("📁 Active project from SUPERVISOR_PROJECT env: {ProjectId} (mode: {Mode})", 
                        _activeProjectId, _isMultiProjectMode ? "Multi-Project" : "Legacy");
                    if (_isMultiProjectMode)
                    {
                        _logger.LogInformation("   Config: {Path}", ConfigPath);
                        _logger.LogInformation("   Models: {Path}", ModelsPath);
                        _logger.LogInformation("   Data: {Path}", DataPath);
                    }
                    return;
                }

                // PRIORIDAD 2: active-project.json
                if (!File.Exists(_activeProjectFilePath))
                {
                    _logger.LogInformation("📁 active-project.json not found, using default (legacy mode)");
                    _activeProjectId = "default";
                    _isMultiProjectMode = false;
                    return;
                }
                
                var json = File.ReadAllText(_activeProjectFilePath);
                var config = JsonSerializer.Deserialize<ActiveProjectConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (config == null || string.IsNullOrWhiteSpace(config.ActiveProject))
                {
                    _logger.LogWarning("⚠️ Invalid active-project.json, using default");
                    _activeProjectId = "default";
                    _isMultiProjectMode = false;
                    return;
                }
                
                _activeProjectId = config.ActiveProject;
                
                // Determinar si es modo multi-proyecto o legacy
                if (_activeProjectId == "default")
                {
                    _isMultiProjectMode = false;
                    _logger.LogInformation("📁 Active project: default (legacy mode)");
                }
                else
                {
                    var projectPath = Path.Combine(_projectsRootPath, _activeProjectId);
                    _isMultiProjectMode = Directory.Exists(projectPath);
                    
                    if (_isMultiProjectMode)
                    {
                        _logger.LogInformation("📁 Active project: {ProjectId} (multi-project mode)", _activeProjectId);
                        _logger.LogInformation("   Config: {Path}", ConfigPath);
                        _logger.LogInformation("   Models: {Path}", ModelsPath);
                        _logger.LogInformation("   Data: {Path}", DataPath);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Project folder not found: {Path}, falling back to legacy mode", projectPath);
                        _activeProjectId = "default";
                        _isMultiProjectMode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading active project configuration");
                _activeProjectId = "default";
                _isMultiProjectMode = false;
            }
        }
        
        private void EnsureProjectsDirectoryExists()
        {
            try
            {
                // Solo crear estructura en desarrollo, no en producción
                var isDevelopment = _environment.IsDevelopment();
                
                if (!Directory.Exists(_projectsRootPath))
                {
                    // Solo crear Projects/ si estamos en desarrollo
                    if (isDevelopment)
                    {
                        Directory.CreateDirectory(_projectsRootPath);
                        _logger.LogInformation("📁 Created Projects directory: {Path}", _projectsRootPath);
                    }
                    else
                    {
                        _logger.LogWarning("📁 Projects directory does not exist in production: {Path}", _projectsRootPath);
                        return; // No crear nada en producción
                    }
                }
                
                // Solo crear _template en desarrollo
                if (isDevelopment)
                {
                    var templatePath = Path.Combine(_projectsRootPath, "_template");
                    if (!Directory.Exists(templatePath))
                    {
                        Directory.CreateDirectory(templatePath);
                        Directory.CreateDirectory(Path.Combine(templatePath, "config"));
                        Directory.CreateDirectory(Path.Combine(templatePath, "models"));
                        Directory.CreateDirectory(Path.Combine(templatePath, "data"));
                        Directory.CreateDirectory(Path.Combine(templatePath, "backups"));
                        
                        // DMS: Estructura documental template
                        var templateDocsPath = Path.Combine(templatePath, "docs");
                        Directory.CreateDirectory(templateDocsPath);
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "compliance"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "cra-generic"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "user-guides"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "technical"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "electrical"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "maintenance"));
                        Directory.CreateDirectory(Path.Combine(templateDocsPath, "_attachments"));
                        
                        // Crear README en template
                        File.WriteAllText(Path.Combine(templatePath, "README.md"), @"# Plantilla de Proyecto

Esta carpeta sirve como plantilla para crear nuevos proyectos.

## Para crear un nuevo proyecto:

1. Copiar toda esta carpeta con el nombre del nuevo proyecto (ej: `AQF-CLIENTE-001`)
2. Añadir el archivo Excel de configuración en `config/ProjectConfig.xlsm`
3. Añadir los modelos 3D en `models/`
4. Modificar `active-project.json` en la raíz del backend con el nuevo ID

## Estructura:

```
{PROJECT_ID}/
├── config/
│   └── ProjectConfig.xlsm    ← Configuración Excel del proyecto
├── models/
│   └── *.glb                 ← Modelos 3D
├── data/
│   └── project.db            ← Base de datos SQLite (auto-generada)
└── backups/
    └── *.zip                 ← Backups automáticos y manuales
```
");
                        
                        _logger.LogInformation("📁 Created _template directory: {Path}", templatePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring Projects directory exists");
            }
        }
        
        private class ActiveProjectConfig
        {
            public string ActiveProject { get; set; } = "default";
            public string? Description { get; set; }
        }
    }
}
