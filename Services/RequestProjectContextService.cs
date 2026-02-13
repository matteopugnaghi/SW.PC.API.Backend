using System.Text.Json;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio Scoped que proporciona el contexto del proyecto para cada request.
    /// En desarrollo: permite seleccionar proyecto via header X-Project-Id
    /// En producción: siempre usa el proyecto de active-project.json
    /// </summary>
    public interface IRequestProjectContext
    {
        /// <summary>
        /// ID del proyecto para este request
        /// </summary>
        string ProjectId { get; }
        
        /// <summary>
        /// Si está en modo multi-proyecto (proyecto específico, no default)
        /// </summary>
        bool IsMultiProjectMode { get; }
        
        /// <summary>
        /// Ruta base del proyecto
        /// </summary>
        string ProjectBasePath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de configuración
        /// </summary>
        string ConfigPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de modelos 3D
        /// </summary>
        string ModelsPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de datos
        /// </summary>
        string DataPath { get; }
        
        /// <summary>
        /// Ruta a la carpeta de backups
        /// </summary>
        string BackupsPath { get; }
        
        /// <summary>
        /// Ruta al archivo Excel de configuración
        /// </summary>
        string ExcelConfigPath { get; }
        
        /// <summary>
        /// Ruta a la base de datos SQLite
        /// </summary>
        string DatabasePath { get; }

        /// <summary>
        /// Ruta a la carpeta de traducciones (i18n)
        /// </summary>
        string TranslationsPath { get; }

        /// <summary>
        /// Ruta a la carpeta de documentación del proyecto (DMS)
        /// </summary>
        string DocsPath { get; }
        
        /// <summary>
        /// Configura el proyecto para este request
        /// </summary>
        void SetProject(string projectId);
    }

    public class RequestProjectContextService : IRequestProjectContext
    {
        private readonly IProjectContextService _globalContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<RequestProjectContextService> _logger;
        
        private string _projectId;
        private bool _isMultiProjectMode;
        private string _projectBasePath = "";
        private string _configPath = "";
        private string _modelsPath = "";
        private string _dataPath = "";
        private string _backupsPath = "";
        private string _excelConfigPath = "";
        private string _databasePath = "";
        private string _translationsPath = "";
        private string _docsPath = "";
        
        // Rutas legacy
        private readonly string _contentRootPath;
        private readonly string _projectsRootPath;
        private readonly string _legacyExcelConfigPath;
        private readonly string _legacyModelsPath;
        private readonly string _legacyDataPath;

        public RequestProjectContextService(
            IProjectContextService globalContext,
            IWebHostEnvironment environment,
            ILogger<RequestProjectContextService> logger)
        {
            _globalContext = globalContext;
            _environment = environment;
            _logger = logger;
            
            _contentRootPath = environment.ContentRootPath;
            _projectsRootPath = Path.Combine(_contentRootPath, "Projects");
            _legacyExcelConfigPath = Path.Combine(_contentRootPath, "ExcelConfigs");
            _legacyModelsPath = Path.Combine(environment.WebRootPath ?? _contentRootPath, "models");
            _legacyDataPath = Path.Combine(_contentRootPath, "Data");
            
            // Por defecto, usar el proyecto global
            _projectId = globalContext.ActiveProjectId;
            _isMultiProjectMode = globalContext.IsMultiProjectMode;
            
            // Inicializar rutas con el proyecto por defecto
            CalculatePaths(_projectId);
        }

        public string ProjectId => _projectId;
        public bool IsMultiProjectMode => _isMultiProjectMode;
        public string ProjectBasePath => _projectBasePath;
        public string ConfigPath => _configPath;
        public string ModelsPath => _modelsPath;
        public string DataPath => _dataPath;
        public string BackupsPath => _backupsPath;
        public string ExcelConfigPath => _excelConfigPath;
        public string DatabasePath => _databasePath;
        public string TranslationsPath => _translationsPath;
        public string DocsPath => _docsPath;

        /// <summary>
        /// Configura el proyecto para este request específico
        /// </summary>
        public void SetProject(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                projectId = _globalContext.ActiveProjectId;
            }
            
            // Validar que el proyecto existe
            if (projectId != "default" && !_globalContext.ProjectExists(projectId))
            {
                _logger.LogWarning("⚠️ Project '{ProjectId}' not found, using global default: {Default}", 
                    projectId, _globalContext.ActiveProjectId);
                projectId = _globalContext.ActiveProjectId;
            }
            
            _projectId = projectId;
            _isMultiProjectMode = projectId != "default";
            
            CalculatePaths(projectId);
            
            _logger.LogDebug("📁 Request project set to: {ProjectId} (MultiProject: {IsMulti})", 
                _projectId, _isMultiProjectMode);
        }

        private void CalculatePaths(string projectId)
        {
            if (projectId == "default")
            {
                // Modo legacy
                _projectBasePath = _contentRootPath;
                _configPath = _legacyExcelConfigPath;
                _modelsPath = _legacyModelsPath;
                _dataPath = _legacyDataPath;
                _backupsPath = Path.Combine(_contentRootPath, "backups");
                _excelConfigPath = Path.Combine(_legacyExcelConfigPath, "ProjectConfig.xlsm");
                _databasePath = Path.Combine(_legacyDataPath, "Aquafrisch.db");
                _translationsPath = Path.Combine(_contentRootPath, "translations");
                _docsPath = Path.Combine(_contentRootPath, "docs");
            }
            else
            {
                // Modo multi-proyecto
                _projectBasePath = Path.Combine(_projectsRootPath, projectId);
                _configPath = Path.Combine(_projectBasePath, "config");
                _modelsPath = Path.Combine(_projectBasePath, "models");
                _dataPath = Path.Combine(_projectBasePath, "data");
                _backupsPath = Path.Combine(_projectBasePath, "backups");
                _translationsPath = Path.Combine(_projectBasePath, "translations");
                _docsPath = Path.Combine(_projectBasePath, "docs");
                
                // Buscar archivo Excel en config
                _excelConfigPath = FindExcelConfig(_configPath);
                _databasePath = Path.Combine(_dataPath, "project.db");
            }
        }

        private string FindExcelConfig(string configDir)
        {
            if (Directory.Exists(configDir))
            {
                var excelFiles = Directory.GetFiles(configDir, "*.xlsm")
                    .Concat(Directory.GetFiles(configDir, "*.xlsx"))
                    .ToArray();
                
                if (excelFiles.Length > 0)
                {
                    // Priorizar ProjectConfig.xlsm
                    var projectConfig = excelFiles.FirstOrDefault(f => 
                        Path.GetFileName(f).Equals("ProjectConfig.xlsm", StringComparison.OrdinalIgnoreCase));
                    
                    return projectConfig ?? excelFiles[0];
                }
            }
            
            // Fallback a legacy
            return Path.Combine(_legacyExcelConfigPath, "ProjectConfig.xlsm");
        }
    }
}
