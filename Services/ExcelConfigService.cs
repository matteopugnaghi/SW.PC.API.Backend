using OfficeOpenXml;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public interface IExcelConfigService
    {
        Task<ProjectConfiguration> LoadProjectConfigurationAsync(string filePath);
        Task<bool> SaveProjectConfigurationAsync(ProjectConfiguration config, string filePath);
        Task<List<PlcVariable>> LoadPlcVariablesAsync(string filePath);
        Task<List<HMIScreen>> LoadHMIScreensAsync(string filePath);
        Task<List<StateColorConfig>> LoadStateColorsAsync(string filePath);
        Task<List<string>> GetMonitoredVariableNamesAsync(string filePath);
        Task<SystemConfiguration> LoadSystemConfigurationAsync(string filePath);
        void InvalidateCache(string? filePath = null); // ✅ MÉTODO PARA FORZAR RECARGA (opcional: por archivo)
        Task<List<Model3DConfig>> Load3DModelsAsync(string filePath);
        
        // 🔔 Sistema de Alarmas Multilenguaje
        Task<AlarmConfiguration> LoadAlarmsAsync(string filePath);
        
        // ⚙️ Sistema de Settings de Máquina
        Task<SettingsPageConfiguration> LoadSettingsPageAsync(string filePath);
        
        // 🚿 Sistema de Recetas de Lavado
        Task<WashRecipeEditorConfiguration> LoadWashRecipeConfigAsync(string filePath);
        
        // 🚂 Sistema de Tipos de Tren
        Task<TrainRecipeConfiguration> LoadTrainRecipeConfigAsync(string filePath);
        
        // � Sistema de Modo Manual/Mantenimiento
        Task<ManualPageExcelConfiguration> LoadManualPageAsync(string filePath);
        
        // �📁 Soporte Multi-Proyecto
        void SetProjectContext(IProjectContextService projectContext);
        string GetExcelConfigPath();
        
        // 🎯 Sistema de filtrado de variables por vista
        Task<List<VariableViewMapping>> LoadVariableViewsAsync(string filePath);
        List<string> GetViewsForVariable(string variableName, List<VariableViewMapping> mappings);
        List<string> FilterVariablesForView(IEnumerable<string> allVariables, string currentView, List<VariableViewMapping> mappings);
        
        /// <summary>
        /// Filtra variables para múltiples vistas activas (vista principal + vistas adicionales)
        /// </summary>
        List<string> FilterVariablesForMultipleViews(IEnumerable<string> allVariables, IEnumerable<string> activeViews, List<VariableViewMapping> mappings);
        
        /// <summary>
        /// Filtra variables y devuelve advertencias de configuración para enviar al frontend
        /// </summary>
        ViewFilterResult FilterVariablesForViewWithWarnings(IEnumerable<string> allVariables, string currentView, List<VariableViewMapping> mappings);
        
        // 🎛️ Configuración de visualización de info en elementos 3D
        Task<List<ElementInfoSettingConfig>> Load3DElementsInfoSettingAsync(string filePath);
        
        // ⚡ Sistema de Modo Semiautomático
        Task<SemiautomaticConfiguration> LoadSemiautomaticConfigAsync(string filePath);
        
        // ⚡ Sistema de Configuración Rápida (Fast Configuration)
        Task<FastConfigurationPageConfiguration> LoadFastConfigurationAsync(string filePath);
        
        // 📟 Sistema de PLC Info Panel (Variables WSTRING desde PLC)
        Task<PlcInfoPanelConfig> LoadPlcInfoPanelAsync(string filePath);
    }

    /// <summary>
    /// Resultado del filtrado de variables con advertencias para el frontend
    /// </summary>
    public class ViewFilterResult
    {
        public List<string> ActiveVariables { get; set; } = new();
        public List<string> ExcludedVariables { get; set; } = new();
        public List<string> UnmatchedVariables { get; set; } = new();
        public bool HasWarnings => UnmatchedVariables.Count > 0;
        
        /// <summary>Genera un SystemWarning para enviar al frontend si hay problemas</summary>
        public object? ToSystemWarning()
        {
            if (!HasWarnings) return null;
            
            return new
            {
                type = "warning",
                title = "Variable_Views: Configuración incompleta",
                message = $"{UnmatchedVariables.Count} variables no tienen patrón configurado en Excel",
                details = UnmatchedVariables.Take(10).Select(v => $"Sin patrón: {v}").ToList(),
                suggestion = "Revise la hoja 'Variable_Views' del Excel y agregue patrones para estas variables"
            };
        }
    }
    
    public class ExcelConfigService : IExcelConfigService
    {
        private readonly ILogger<ExcelConfigService> _logger;
        private readonly IMetricsService _metricsService;
        private readonly IWebHostEnvironment _environment;
        private string _configFolder;
        
        // ✅ CACHÉ por archivo/proyecto para evitar recargar Excel constantemente
        // Diccionarios keyed por filePath absoluto normalizado
        private readonly Dictionary<string, (SystemConfiguration Config, DateTime Timestamp)> _systemConfigCache = new();
        private readonly Dictionary<string, (List<StateColorConfig> Colors, DateTime Timestamp)> _stateColorsCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5); // Cache válido por 5 minutos
        private readonly object _cacheLock = new(); // Thread-safety
        
        // 📁 Soporte Multi-Proyecto
        private IProjectContextService? _projectContext;
        
        public ExcelConfigService(
            IWebHostEnvironment environment, 
            ILogger<ExcelConfigService> logger,
            IMetricsService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _environment = environment;
            _configFolder = Path.Combine(environment.ContentRootPath, "ExcelConfigs");
            
            // Solo crear carpeta ExcelConfigs en desarrollo - en producción debe ya existir
            // o el proyecto usa multi-proyecto con Projects/{id}/config/
            if (environment.IsDevelopment() && !Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
                _logger.LogInformation("📁 ExcelConfigService: Created ExcelConfigs folder (development mode)");
            }
            
            // Configurar licencia EPPlus (NonCommercial o Commercial)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }
        
        /// <summary>
        /// Configura el servicio de contexto de proyecto para soporte multi-proyecto.
        /// Debe llamarse después de la inicialización del DI container.
        /// </summary>
        public void SetProjectContext(IProjectContextService projectContext)
        {
            _projectContext = projectContext;
            
            // Actualizar carpeta de configuración según el proyecto activo
            if (_projectContext != null && _projectContext.IsMultiProjectMode)
            {
                _configFolder = _projectContext.ConfigPath;
                _logger.LogInformation("📁 ExcelConfigService: Config folder updated to {Path}", _configFolder);
                
                // Asegurar que existe la carpeta
                if (!Directory.Exists(_configFolder))
                {
                    Directory.CreateDirectory(_configFolder);
                }
                
                // Invalidar caché para forzar recarga desde nueva ubicación
                InvalidateCache();
            }
        }
        
        /// <summary>
        /// Obtiene la ruta completa al archivo Excel de configuración.
        /// Usa el contexto de proyecto si está disponible.
        /// </summary>
        public string GetExcelConfigPath()
        {
            if (_projectContext != null && _projectContext.IsMultiProjectMode)
            {
                return _projectContext.ExcelConfigPath;
            }
            
            return Path.Combine(_configFolder, "ProjectConfig.xlsm");
        }
        
        public async Task<ProjectConfiguration> LoadProjectConfigurationAsync(string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Excel file not found: {fullPath}");
                }
                
                var config = new ProjectConfiguration();
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    // Leer hoja de información general
                    var generalSheet = package.Workbook.Worksheets["General"];
                    if (generalSheet != null)
                    {
                        config.ProjectName = generalSheet.Cells["B1"].Text;
                        config.ProjectCode = generalSheet.Cells["B2"].Text;
                        config.Customer = generalSheet.Cells["B3"].Text;
                        
                        if (DateTime.TryParse(generalSheet.Cells["B4"].Text, out var date))
                        {
                            config.CreatedDate = date;
                        }
                    }
                    
                    // Leer variables PLC
                    config.PlcVariables = await LoadPlcVariablesFromSheetAsync(package);
                    
                    // Leer pantallas HMI
                    config.Screens = await LoadHMIScreensFromSheetAsync(package);
                    
                    // Leer modelos 3D desde Excel
                    config.Models3D = await LoadModels3DFromSheetAsync(package);
                }
                
                _logger.LogInformation("Project configuration loaded successfully from {FilePath}", fullPath);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project configuration from {FilePath}", filePath);
                throw;
            }
        }
        
        public async Task<bool> SaveProjectConfigurationAsync(ProjectConfiguration config, string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                using (var package = new ExcelPackage())
                {
                    // Crear hoja General
                    var generalSheet = package.Workbook.Worksheets.Add("General");
                    generalSheet.Cells["A1"].Value = "Project Name:";
                    generalSheet.Cells["B1"].Value = config.ProjectName;
                    generalSheet.Cells["A2"].Value = "Project Code:";
                    generalSheet.Cells["B2"].Value = config.ProjectCode;
                    generalSheet.Cells["A3"].Value = "Customer:";
                    generalSheet.Cells["B3"].Value = config.Customer;
                    generalSheet.Cells["A4"].Value = "Created Date:";
                    generalSheet.Cells["B4"].Value = config.CreatedDate;
                    
                    // Crear hoja de Variables PLC
                    var plcSheet = package.Workbook.Worksheets.Add("PLC_Variables");
                    plcSheet.Cells["A1"].Value = "Variable Name";
                    plcSheet.Cells["B1"].Value = "Symbol Path";
                    plcSheet.Cells["C1"].Value = "Data Type";
                    plcSheet.Cells["D1"].Value = "Access Mode";
                    plcSheet.Cells["E1"].Value = "Update Rate (ms)";
                    plcSheet.Cells["F1"].Value = "Description";
                    
                    int row = 2;
                    foreach (var variable in config.PlcVariables)
                    {
                        plcSheet.Cells[$"A{row}"].Value = variable.VariableName;
                        plcSheet.Cells[$"B{row}"].Value = variable.SymbolPath;
                        plcSheet.Cells[$"C{row}"].Value = variable.DataType;
                        plcSheet.Cells[$"D{row}"].Value = variable.AccessMode;
                        plcSheet.Cells[$"E{row}"].Value = variable.UpdateRateMs;
                        plcSheet.Cells[$"F{row}"].Value = variable.Description;
                        row++;
                    }
                    
                    // Crear hoja de Pantallas HMI
                    var hmiSheet = package.Workbook.Worksheets.Add("HMI_Screens");
                    hmiSheet.Cells["A1"].Value = "Screen ID";
                    hmiSheet.Cells["B1"].Value = "Screen Name";
                    hmiSheet.Cells["C1"].Value = "Title";
                    hmiSheet.Cells["D1"].Value = "Display Order";
                    hmiSheet.Cells["E1"].Value = "Is Enabled";
                    hmiSheet.Cells["F1"].Value = "Icon Name";
                    
                    row = 2;
                    foreach (var screen in config.Screens)
                    {
                        hmiSheet.Cells[$"A{row}"].Value = screen.ScreenId;
                        hmiSheet.Cells[$"B{row}"].Value = screen.ScreenName;
                        hmiSheet.Cells[$"C{row}"].Value = screen.Title;
                        hmiSheet.Cells[$"D{row}"].Value = screen.DisplayOrder;
                        hmiSheet.Cells[$"E{row}"].Value = screen.IsEnabled;
                        hmiSheet.Cells[$"F{row}"].Value = screen.IconName;
                        row++;
                    }
                    
                    // Crear hoja de Modelos 3D
                    var modelsSheet = package.Workbook.Worksheets.Add("3D_Models");
                    modelsSheet.Cells["A1"].Value = "Model ID";
                    modelsSheet.Cells["B1"].Value = "Model Name";
                    modelsSheet.Cells["C1"].Value = "File Name";
                    modelsSheet.Cells["D1"].Value = "File Type";
                    modelsSheet.Cells["E1"].Value = "Description";
                    modelsSheet.Cells["F1"].Value = "Category";
                    modelsSheet.Cells["G1"].Value = "Associated Screen";
                    modelsSheet.Cells["H1"].Value = "Is Enabled";
                    modelsSheet.Cells["I1"].Value = "Display Order";
                    
                    row = 2;
                    foreach (var model in config.Models3D)
                    {
                        modelsSheet.Cells[$"A{row}"].Value = model.ModelId;
                        modelsSheet.Cells[$"B{row}"].Value = model.ModelName;
                        modelsSheet.Cells[$"C{row}"].Value = model.FileName;
                        modelsSheet.Cells[$"D{row}"].Value = model.FileType;
                        modelsSheet.Cells[$"E{row}"].Value = model.Description;
                        modelsSheet.Cells[$"F{row}"].Value = model.Category;
                        modelsSheet.Cells[$"G{row}"].Value = model.AssociatedScreen;
                        modelsSheet.Cells[$"H{row}"].Value = model.IsEnabled;
                        modelsSheet.Cells[$"I{row}"].Value = model.DisplayOrder;
                        row++;
                    }
                    
                    // Guardar archivo
                    await package.SaveAsAsync(new FileInfo(fullPath));
                }
                
                _logger.LogInformation("Project configuration saved successfully to {FilePath}", fullPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project configuration to {FilePath}", filePath);
                return false;
            }
        }
        
        public async Task<List<PlcVariable>> LoadPlcVariablesAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            
            // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(stream))
            {
                return await LoadPlcVariablesFromSheetAsync(package);
            }
        }
        
        public async Task<List<HMIScreen>> LoadHMIScreensAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            
            // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var package = new ExcelPackage(stream))
            {
                return await LoadHMIScreensFromSheetAsync(package);
            }
        }
        
        /*public async Task<List<Model3DConfig>> LoadModels3DAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            
            using (var package = new ExcelPackage(new FileInfo(fullPath)))
            {
                return await LoadModels3DFromSheetAsync(package);
            }
        }*/
        
        private async Task<List<PlcVariable>> LoadPlcVariablesFromSheetAsync(ExcelPackage package)
        {
            var variables = new List<PlcVariable>();
            var sheet = package.Workbook.Worksheets["PLC_Variables"];
            
            if (sheet == null)
            {
                _logger.LogWarning("PLC_Variables sheet not found in Excel file");
                return variables;
            }
            
            // Leer desde la fila 2 (la 1 es encabezado)
            int row = 2;
            while (!string.IsNullOrEmpty(sheet.Cells[$"A{row}"].Text))
            {
                var variable = new PlcVariable
                {
                    VariableName = sheet.Cells[$"A{row}"].Text,
                    SymbolPath = sheet.Cells[$"B{row}"].Text,
                    DataType = sheet.Cells[$"C{row}"].Text,
                    AccessMode = sheet.Cells[$"D{row}"].Text,
                    UpdateRateMs = int.TryParse(sheet.Cells[$"E{row}"].Text, out var rate) ? rate : 1000,
                    Description = sheet.Cells[$"F{row}"].Text,
                    Unit = sheet.Cells[$"G{row}"].Text,
                    LogToDatabase = sheet.Cells[$"H{row}"].Text.ToLower() == "true" || sheet.Cells[$"H{row}"].Text == "1"
                };
                
                variables.Add(variable);
                row++;
            }
            
            _logger.LogInformation("Loaded {Count} PLC variables from Excel", variables.Count);
            return await Task.FromResult(variables);
        }
        
        private async Task<List<HMIScreen>> LoadHMIScreensFromSheetAsync(ExcelPackage package)
        {
            var screens = new List<HMIScreen>();
            var sheet = package.Workbook.Worksheets["HMI_Screens"];
            
            if (sheet == null)
            {
                _logger.LogWarning("HMI_Screens sheet not found in Excel file");
                return screens;
            }
            
            // Leer desde la fila 2
            int row = 2;
            while (!string.IsNullOrEmpty(sheet.Cells[$"A{row}"].Text))
            {
                var screen = new HMIScreen
                {
                    ScreenId = sheet.Cells[$"A{row}"].Text,
                    ScreenName = sheet.Cells[$"B{row}"].Text,
                    Title = sheet.Cells[$"C{row}"].Text,
                    DisplayOrder = int.TryParse(sheet.Cells[$"D{row}"].Text, out var order) ? order : 0,
                    IsEnabled = sheet.Cells[$"E{row}"].Text.ToLower() != "false" && sheet.Cells[$"E{row}"].Text != "0",
                    IconName = sheet.Cells[$"F{row}"].Text
                };
                
                screens.Add(screen);
                row++;
            }
            
            _logger.LogInformation("Loaded {Count} HMI screens from Excel", screens.Count);
            return await Task.FromResult(screens);
        }
        
        private async Task<List<Model3DConfig>> LoadModels3DFromSheetAsync(ExcelPackage package)
        {
            var models = new List<Model3DConfig>();
            var sheet = package.Workbook.Worksheets["3D_Models"];
            
            if (sheet == null)
            {
                _logger.LogWarning("3D_Models sheet not found in Excel file");
                return models;
            }
            
            // Leer desde la fila 2 (la 1 es encabezado)
            // Columnas: Model ID | Model Name | File Name | File Type | Description | Category | Associated Screen | Is Enabled | Display Order
            int row = 2;
            while (!string.IsNullOrEmpty(sheet.Cells[$"A{row}"].Text))
            {
                var model = new Model3DConfig
                {
                    ModelId = sheet.Cells[$"A{row}"].Text,
                    ModelName = sheet.Cells[$"B{row}"].Text,
                    FileName = sheet.Cells[$"C{row}"].Text,
                    FileType = sheet.Cells[$"D{row}"].Text,
                    Description = sheet.Cells[$"E{row}"].Text,
                    Category = sheet.Cells[$"F{row}"].Text,
                    AssociatedScreen = sheet.Cells[$"G{row}"].Text,
                    IsEnabled = sheet.Cells[$"H{row}"].Text.ToLower() != "false" && sheet.Cells[$"H{row}"].Text != "0",
                    DisplayOrder = int.TryParse(sheet.Cells[$"I{row}"].Text, out var order) ? order : 0,
                    // Campos de animación del padre
                    AnimationType = sheet.Cells[$"U{row}"].Text,
                    AnimationSpeed = double.TryParse(sheet.Cells[$"V{row}"].Text, out var animSpeed) ? animSpeed : 1.0,
                    AnimateOnlyWhenOn = sheet.Cells[$"W{row}"].Text.ToLower() == "true" || sheet.Cells[$"W{row}"].Text == "1",
                    AnimationPlcVariable = sheet.Cells[$"AD{row}"].Text,
                    AnimationMinValue = double.TryParse(sheet.Cells[$"AE{row}"].Text, out var animMin) ? animMin : 0.0,
                    AnimationMaxValue = double.TryParse(sheet.Cells[$"AF{row}"].Text, out var animMax) ? animMax : 1000.0,
                    AnimationAxis = sheet.Cells[$"AG{row}"].Text,
                    AnimationScaleFactor = double.TryParse(sheet.Cells[$"AH{row}"].Text, out var animScale) ? animScale : 0.1
                };
                
                // ✅ LEER CHILDREN (5 hijos posibles, 21 columnas cada uno)
                model.Children = LoadChildrenForModel(sheet, row, model.ModelName);
                
                models.Add(model);
                row++;
            }
            
            _logger.LogInformation("Loaded {Count} 3D models from Excel", models.Count);
            return await Task.FromResult(models);
        }
        
        /// <summary>
        /// Lee las 21 columnas × 5 hijos (Child1-Child5) desde columnas AI-EI del Excel
        /// </summary>
        private List<ChildModel3DConfig> LoadChildrenForModel(ExcelWorksheet sheet, int row, string parentName)
        {
            var children = new List<ChildModel3DConfig>();
            
            // Child1: AI-BC (columnas 35-55)
            // Child2: BD-BX (columnas 56-76)
            // Child3: BY-CS (columnas 77-97)
            // Child4: CT-DN (columnas 98-118)
            // Child5: DO-EI (columnas 119-139)
            
            var childColumns = new[]
            {
                ("Child1", 35),  // AI = columna 35
                ("Child2", 56),  // BD = columna 56
                ("Child3", 77),  // BY = columna 77
                ("Child4", 98),  // CT = columna 98
                ("Child5", 119)  // DO = columna 119
            };
            
            foreach (var (childLabel, startCol) in childColumns)
            {
                // Leer Name (columna 0)
                var name = sheet.Cells[row, startCol].Text?.Trim();
                
                // Si no hay nombre, este hijo no está definido
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogDebug("  ⏭️ {Label} vacío para modelo {Parent} (fila {Row})", childLabel, parentName, row);
                    continue;
                }
                
                var child = new ChildModel3DConfig
                {
                    Name = name,
                    ParentName = sheet.Cells[row, startCol + 1].Text?.Trim() ?? string.Empty,
                    FileName = sheet.Cells[row, startCol + 2].Text?.Trim() ?? string.Empty,
                    AnimationType = sheet.Cells[row, startCol + 3].Text?.Trim() ?? string.Empty,
                    AnimationSpeed = double.TryParse(sheet.Cells[row, startCol + 4].Text, out var speed) ? speed : 1.0,
                    AnimateOnlyWhenOn = sheet.Cells[row, startCol + 5].Text?.Trim().ToLower() != "false",
                    PlcVariable = sheet.Cells[row, startCol + 6].Text?.Trim() ?? string.Empty,
                    Axis = sheet.Cells[row, startCol + 7].Text?.Trim() ?? "Y",
                    MinValue = double.TryParse(sheet.Cells[row, startCol + 8].Text, out var min) ? min : 0.0,
                    MaxValue = double.TryParse(sheet.Cells[row, startCol + 9].Text, out var max) ? max : 1000.0,
                    ScaleFactor = double.TryParse(sheet.Cells[row, startCol + 10].Text, out var scale) ? scale : 0.1,
                    ScaleX = double.TryParse(sheet.Cells[row, startCol + 11].Text, out var sx) ? (double?)sx : null,
                    ScaleY = double.TryParse(sheet.Cells[row, startCol + 12].Text, out var sy) ? (double?)sy : null,
                    ScaleZ = double.TryParse(sheet.Cells[row, startCol + 13].Text, out var sz) ? (double?)sz : null,
                    ColorOn = sheet.Cells[row, startCol + 14].Text?.Trim() ?? string.Empty,
                    ColorOff = sheet.Cells[row, startCol + 15].Text?.Trim() ?? string.Empty,
                    ColorDisabled = sheet.Cells[row, startCol + 16].Text?.Trim() ?? string.Empty,
                    ColorAlarm = sheet.Cells[row, startCol + 17].Text?.Trim() ?? string.Empty,
                    OffsetX = double.TryParse(sheet.Cells[row, startCol + 18].Text, out var ox) ? ox : 0.0,
                    OffsetY = double.TryParse(sheet.Cells[row, startCol + 19].Text, out var oy) ? oy : 0.0,
                    OffsetZ = double.TryParse(sheet.Cells[row, startCol + 20].Text, out var oz) ? oz : 0.0
                };
                
                children.Add(child);
                _logger.LogInformation("  ✅ {Label} cargado: {Name} (parent: {Parent}, file: {File}, anim: {AnimType}, plc: {Plc})", 
                    childLabel, child.Name, child.ParentName, child.FileName, child.AnimationType, child.PlcVariable);
            }
            
            return children;
        }

        #region Variable Views Mapping

        /// <summary>
        /// Caché para Variable Views mappings por archivo Excel
        /// </summary>
        private readonly Dictionary<string, (List<VariableViewMapping> Mappings, DateTime Timestamp)> _variableViewsCache = new();

        /// <summary>
        /// Carga los mappings de Variable_Views desde la hoja del Excel.
        /// Define qué variables se leen en cada vista del frontend.
        /// </summary>
        public async Task<List<VariableViewMapping>> LoadVariableViewsAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            var cacheKey = fullPath.ToLowerInvariant();

            // Verificar caché
            lock (_cacheLock)
            {
                if (_variableViewsCache.TryGetValue(cacheKey, out var cached))
                {
                    var cacheAge = DateTime.Now - cached.Timestamp;
                    if (cacheAge < _cacheExpiration)
                    {
                        _logger.LogDebug("📦 Usando Variable_Views desde CACHÉ ({Count} mappings)", cached.Mappings.Count);
                        return cached.Mappings;
                    }
                }
            }

            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Variable_Views no disponible.", fullPath);
                    return new List<VariableViewMapping>();
                }

                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var package = new ExcelPackage(stream);
                
                var mappings = await LoadVariableViewsFromSheetAsync(package);

                // Guardar en caché
                lock (_cacheLock)
                {
                    _variableViewsCache[cacheKey] = (mappings, DateTime.Now);
                }

                _logger.LogInformation("✅ Cargados {Count} mappings de Variable_Views desde Excel", mappings.Count);
                return mappings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando Variable_Views: {Message}", ex.Message);
                return new List<VariableViewMapping>();
            }
        }

        /// <summary>
        /// Lee la hoja Variable_Views del Excel y compila los patrones regex
        /// </summary>
        private async Task<List<VariableViewMapping>> LoadVariableViewsFromSheetAsync(ExcelPackage package)
        {
            var mappings = new List<VariableViewMapping>();
            var sheet = package.Workbook.Worksheets["Variable_Views"];

            if (sheet == null)
            {
                _logger.LogWarning("⚠️ Hoja 'Variable_Views' no encontrada en Excel. Todas las variables serán GLOBAL.");
                return mappings;
            }

            // Leer desde fila 2 (fila 1 = encabezados)
            int row = 2;
            while (!string.IsNullOrWhiteSpace(sheet.Cells[$"A{row}"].Text))
            {
                var pattern = sheet.Cells[$"A{row}"].Text?.Trim();
                var viewsText = sheet.Cells[$"B{row}"].Text?.Trim();
                var description = sheet.Cells[$"C{row}"].Text?.Trim();

                if (!string.IsNullOrEmpty(pattern) && !string.IsNullOrEmpty(viewsText))
                {
                    // Parsear vistas (separadas por coma)
                    var views = viewsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim().ToUpper())
                        .Where(v => PlcViewIds.AllViews.Contains(v))
                        .ToList();

                    if (views.Count > 0)
                    {
                        var mapping = new VariableViewMapping
                        {
                            VariablePattern = pattern,
                            Views = views,
                            Description = description,
                            CompiledPattern = CompileWildcardPattern(pattern)
                        };
                        mappings.Add(mapping);
                        _logger.LogDebug("📋 Variable mapping: {Pattern} → [{Views}]", pattern, string.Join(", ", views));
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Fila {Row}: vistas inválidas '{ViewsText}'", row, viewsText);
                    }
                }
                row++;
            }

            // Ordenar por especificidad (más específicos primero)
            mappings = mappings.OrderByDescending(m => m.Specificity).ToList();

            // Log detallado de los mappings cargados
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");
            _logger.LogInformation("📊 Variable_Views: {Count} mappings cargados desde Excel", mappings.Count);
            _logger.LogInformation("────────────────────────────────────────────────────────────────────");
            foreach (var m in mappings)
            {
                var example = GetPatternMatchExample(m.VariablePattern);
                _logger.LogInformation("   📋 '{Pattern}' → [{Views}]", m.VariablePattern, string.Join(", ", m.Views));
                _logger.LogInformation("      Coincidiría con: {Example}", example);
            }
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");
            
            return await Task.FromResult(mappings);
        }

        /// <summary>
        /// Genera un ejemplo de qué coincidiría con un patrón dado
        /// </summary>
        private string GetPatternMatchExample(string pattern)
        {
            // Reemplazar * con ejemplos concretos para ayudar a entender
            if (pattern.EndsWith("[*]"))
            {
                // st_XXX[*] → "st_XXX[1]", "st_XXX[2]", etc.
                var baseName = pattern.Substring(0, pattern.Length - 3);
                return $"'{baseName}[1]', '{baseName}[2]', etc.";
            }
            else if (pattern.EndsWith(".*"))
            {
                // st_XXX.* → "st_XXX.propiedad", "st_XXX.otra[1]", etc.
                var baseName = pattern.Substring(0, pattern.Length - 2);
                return $"'{baseName}.cualquier_propiedad', '{baseName}.i_State[1]', etc.";
            }
            else if (pattern.EndsWith("*"))
            {
                // st_XXX* → "st_XXX", "st_XXX[1]", "st_XXX.algo", etc.
                var baseName = pattern.Substring(0, pattern.Length - 1);
                return $"'{baseName}', '{baseName}[1]', '{baseName}.algo', etc.";
            }
            else if (!pattern.Contains("*"))
            {
                // Patrón exacto
                return $"SOLO '{pattern}' (match exacto)";
            }
            else
            {
                return $"Variables que coincidan con el patrón";
            }
        }

        /// <summary>
        /// Convierte un patrón con wildcards (*) a una expresión regular
        /// </summary>
        private System.Text.RegularExpressions.Regex CompileWildcardPattern(string pattern)
        {
            // Escapar caracteres especiales de regex excepto *
            var regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace(@"\*", ".*")  // * → .*
                .Replace(@"\?", ".");  // ? → . (opcional, si quieres soportar ?)

            return new System.Text.RegularExpressions.Regex(
                $"^{regexPattern}$",
                System.Text.RegularExpressions.RegexOptions.Compiled | 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        /// <summary>
        /// Determina a qué vistas pertenece una variable según los mappings.
        /// Si no hay match, devuelve GLOBAL (comportamiento seguro por defecto).
        /// </summary>
        public List<string> GetViewsForVariable(string variableName, List<VariableViewMapping> mappings)
        {
            return GetViewsForVariable(variableName, mappings, out _);
        }

        /// <summary>
        /// Determina a qué vistas pertenece una variable según los mappings.
        /// Si no hay match, devuelve GLOBAL (comportamiento seguro por defecto).
        /// </summary>
        private List<string> GetViewsForVariable(string variableName, List<VariableViewMapping> mappings, out bool hadMatch, out string matchedPattern)
        {
            hadMatch = false;
            matchedPattern = string.Empty;
            
            // Buscar primer match (ya están ordenados por especificidad)
            foreach (var mapping in mappings)
            {
                if (mapping.CompiledPattern?.IsMatch(variableName) == true)
                {
                    hadMatch = true;
                    matchedPattern = mapping.VariablePattern;
                    return mapping.Views;
                }
            }

            // Sin match = GLOBAL (siempre se lee)
            return new List<string> { PlcViewIds.GLOBAL };
        }
        
        // Overload para mantener compatibilidad
        private List<string> GetViewsForVariable(string variableName, List<VariableViewMapping> mappings, out bool hadMatch)
        {
            return GetViewsForVariable(variableName, mappings, out hadMatch, out _);
        }

        /// <summary>
        /// Sugiere un patrón correcto para una variable que no tiene match.
        /// Ayuda a los usuarios a corregir el Excel.
        /// </summary>
        private string SuggestPatternForVariable(string variableName)
        {
            // Extraer el prefijo de la estructura (ej: "MAIN.fbMachine.st_MainForm")
            // y sugerir agregar ".*" o "*" al final
            
            // Buscar el último componente que parece ser una estructura
            var parts = variableName.Split('.');
            if (parts.Length < 2) return string.Empty;

            // Caso: MAIN.fbMachine.st_MainForm.i_StateGantry[1]
            // Sugerir: MAIN.fbMachine.st_MainForm.*
            
            // Buscar el patrón "st_" que indica estructura
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i].StartsWith("st_", StringComparison.OrdinalIgnoreCase))
                {
                    // Si tiene algo después (propiedad o índice), sugerir wildcard
                    if (i < parts.Length - 1)
                    {
                        var basePath = string.Join(".", parts.Take(i + 1));
                        return $"{basePath}.*";
                    }
                    else if (parts[i].Contains('['))
                    {
                        // st_XXX[1] → st_XXX*
                        var baseStruct = parts[i].Substring(0, parts[i].IndexOf('['));
                        var basePath = string.Join(".", parts.Take(i)) + "." + baseStruct;
                        return $"{basePath}*";
                    }
                }
            }

            // Fallback: sugerir el prefijo con wildcard
            if (parts.Length >= 3)
            {
                return string.Join(".", parts.Take(3)) + "*";
            }

            return string.Empty;
        }

        /// <summary>
        /// Filtra una lista de variables para obtener solo las que deben leerse en la vista actual.
        /// Siempre incluye las variables GLOBAL.
        /// </summary>
        public List<string> FilterVariablesForView(
            IEnumerable<string> allVariables, 
            string currentView, 
            List<VariableViewMapping> mappings)
        {
            var targetView = PlcViewIds.FromFrontendView(currentView);
            var result = new List<string>();
            var excluded = new List<string>();
            var globalByDefault = new List<string>(); // Variables sin match (tratadas como GLOBAL)
            var matchedByPattern = new Dictionary<string, List<string>>(); // Patrón → variables que coinciden

            _logger.LogInformation("🔍 FilterVariablesForView: vista={View} → targetView={Target}, {MappingCount} mappings, {VarCount} variables",
                currentView, targetView, mappings?.Count ?? 0, allVariables.Count());

            foreach (var varName in allVariables)
            {
                var views = GetViewsForVariable(varName, mappings, out bool hadMatch, out string matchedPattern);
                
                // Registrar qué patrón coincidió
                if (hadMatch)
                {
                    if (!matchedByPattern.ContainsKey(matchedPattern))
                        matchedByPattern[matchedPattern] = new List<string>();
                    matchedByPattern[matchedPattern].Add(varName);
                }
                
                // Incluir si es GLOBAL o pertenece a la vista actual
                if (views.Contains(PlcViewIds.GLOBAL) || views.Contains(targetView))
                {
                    result.Add(varName);
                    if (!hadMatch)
                    {
                        globalByDefault.Add(varName);
                    }
                }
                else
                {
                    excluded.Add(varName);
                }
            }

            // SIEMPRE mostrar diagnóstico de patrones
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");
            _logger.LogInformation("📊 DIAGNÓSTICO DE FILTRADO - Vista: {View}", targetView);
            _logger.LogInformation("────────────────────────────────────────────────────────────────────");
            if (matchedByPattern.Count > 0)
            {
                _logger.LogInformation("📋 Patrones que coincidieron:");
                foreach (var kvp in matchedByPattern.OrderByDescending(x => x.Value.Count))
                {
                    var mapping = mappings?.FirstOrDefault(m => m.VariablePattern == kvp.Key);
                    var viewsList = mapping != null ? string.Join(",", mapping.Views) : "?";
                    _logger.LogInformation("   - '{Pattern}' → [{Views}] = {Count} variables", 
                        kvp.Key, viewsList, kvp.Value.Count);
                }
            }
            
            // Log de diagnóstico DETALLADO para variables sin match
            if (globalByDefault.Count > 0)
            {
                _logger.LogWarning("────────────────────────────────────────────────────────────────────");
                _logger.LogWarning("⚠️ PROBLEMA: {Count} variables NO COINCIDEN con ningún patrón", globalByDefault.Count);
                _logger.LogWarning("   Estas variables serán tratadas como GLOBAL (se leen en TODAS las vistas)");
                _logger.LogWarning("❌ Variables sin match (primeras 10):");
                foreach (var v in globalByDefault.Take(10))
                {
                    _logger.LogWarning("   - {Variable}", v);
                    var suggested = SuggestPatternForVariable(v);
                    if (!string.IsNullOrEmpty(suggested))
                    {
                        _logger.LogWarning("     💡 Sugerencia: usar patrón '{Suggested}'", suggested);
                    }
                }
                if (globalByDefault.Count > 10)
                {
                    _logger.LogWarning("   ... y {More} variables más sin match", globalByDefault.Count - 10);
                }
            }
            _logger.LogInformation("────────────────────────────────────────────────────────────────────");
            _logger.LogInformation("📈 RESUMEN: {Active} incluidas, {Excluded} excluidas, {NoMatch} sin patrón",
                result.Count, excluded.Count, globalByDefault.Count);
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");

            return result;
        }

        /// <summary>
        /// 🎯 Filtra variables para múltiples vistas activas simultáneas.
        /// Usado cuando hay vistas adicionales activas (MODEL_DETAIL, SCREEN_PANEL, etc.)
        /// junto con la vista principal.
        /// </summary>
        public List<string> FilterVariablesForMultipleViews(
            IEnumerable<string> allVariables, 
            IEnumerable<string> activeViews, 
            List<VariableViewMapping> mappings)
        {
            // Convertir nombres de vistas frontend a IDs internos
            var targetViews = activeViews
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => PlcViewIds.FromFrontendView(v))
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Siempre incluir GLOBAL
            targetViews.Add(PlcViewIds.GLOBAL);
            
            var result = new List<string>();
            
            _logger.LogInformation("🎯 FilterVariablesForMultipleViews: vistas activas=[{Views}], {VarCount} variables totales",
                string.Join(", ", targetViews), allVariables.Count());

            foreach (var varName in allVariables)
            {
                var variableViews = GetViewsForVariable(varName, mappings, out bool hadMatch, out _);
                
                // Incluir si la variable pertenece a CUALQUIERA de las vistas activas
                // O si no tuvo match (se trata como GLOBAL)
                if (!hadMatch || variableViews.Any(v => targetViews.Contains(v)))
                {
                    result.Add(varName);
                }
            }
            
            _logger.LogInformation("📊 Filtrado múltiple: {Active}/{Total} variables activas para vistas [{Views}]",
                result.Count, allVariables.Count(), string.Join(", ", targetViews.Where(v => v != PlcViewIds.GLOBAL)));

            return result;
        }

        /// <summary>
        /// Filtra variables y devuelve advertencias para enviar al frontend vía SignalR
        /// </summary>
        public ViewFilterResult FilterVariablesForViewWithWarnings(
            IEnumerable<string> allVariables, 
            string currentView, 
            List<VariableViewMapping> mappings)
        {
            var targetView = PlcViewIds.FromFrontendView(currentView);
            var result = new ViewFilterResult();

            foreach (var varName in allVariables)
            {
                var views = GetViewsForVariable(varName, mappings, out bool hadMatch);
                
                if (views.Contains(PlcViewIds.GLOBAL) || views.Contains(targetView))
                {
                    result.ActiveVariables.Add(varName);
                    if (!hadMatch)
                    {
                        result.UnmatchedVariables.Add(varName);
                    }
                }
                else
                {
                    result.ExcludedVariables.Add(varName);
                }
            }

            // Logging (más compacto, el detallado ya está en FilterVariablesForView)
            if (result.HasWarnings)
            {
                _logger.LogWarning("⚠️ Variable_Views: {Unmatched} variables sin patrón (de {Total} total)",
                    result.UnmatchedVariables.Count, allVariables.Count());
            }

            return result;
        }

        #endregion

        #region 3D Elements Info Setting

        /// <summary>
        /// Caché para configuración de visualización de elementos 3D
        /// </summary>
        private readonly Dictionary<string, (List<ElementInfoSettingConfig> Configs, DateTime Timestamp)> _elementsInfoSettingCache = new();

        /// <summary>
        /// Carga la configuración de visualización de información en elementos 3D.
        /// Hoja: "3D_Elements_Info_Setting"
        /// Estructura: ModelName, DisplayType, ScreenPosition, ModelPosition, OffsetX, OffsetY, ModelIcon
        ///             + 5 botones (PlcVar, Desc, Icon) 
        ///             + 10 slots (Type, PlcVar, Desc, Unit, Format, Min, Max, Warning, Critical, History, TextOn, TextOff, Icon)
        /// </summary>
        public async Task<List<ElementInfoSettingConfig>> Load3DElementsInfoSettingAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            var cacheKey = fullPath.ToLowerInvariant();

            // Verificar caché
            lock (_cacheLock)
            {
                if (_elementsInfoSettingCache.TryGetValue(cacheKey, out var cached))
                {
                    var cacheAge = DateTime.Now - cached.Timestamp;
                    if (cacheAge < _cacheExpiration)
                    {
                        _logger.LogDebug("📦 Usando 3D_Elements_Info_Setting desde CACHÉ ({Count} configuraciones)", cached.Configs.Count);
                        return cached.Configs;
                    }
                }
            }

            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. 3D_Elements_Info_Setting no disponible.", fullPath);
                    return new List<ElementInfoSettingConfig>();
                }

                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var package = new ExcelPackage(stream);
                
                var configs = await LoadElementsInfoSettingFromSheetAsync(package);

                // Guardar en caché
                lock (_cacheLock)
                {
                    _elementsInfoSettingCache[cacheKey] = (configs, DateTime.Now);
                }

                _logger.LogInformation("✅ Cargadas {Count} configuraciones de 3D_Elements_Info_Setting desde Excel", configs.Count);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando 3D_Elements_Info_Setting: {Message}", ex.Message);
                return new List<ElementInfoSettingConfig>();
            }
        }

        /// <summary>
        /// Lee la hoja 3D_Elements_Info_Setting del Excel
        /// </summary>
        private async Task<List<ElementInfoSettingConfig>> LoadElementsInfoSettingFromSheetAsync(ExcelPackage package)
        {
            var configs = new List<ElementInfoSettingConfig>();
            var sheet = package.Workbook.Worksheets["3D_Elements_Info_Setting"];

            if (sheet == null)
            {
                _logger.LogWarning("⚠️ Hoja '3D_Elements_Info_Setting' no encontrada en Excel. Sin configuración de info para elementos 3D.");
                return configs;
            }

            _logger.LogInformation("🎛️ Cargando configuración de info para elementos 3D...");

            // Columnas base: A-L (12 columnas)
            // Botones: M en adelante (5 botones × 3 columnas = 15 columnas)
            // Slots: AB en adelante (10 slots × 13 columnas = 130 columnas)
            
            // 🔍 DEBUG: Mostrar dimensiones reales de la hoja
            _logger.LogInformation("📐 Dimensiones de hoja '3D_Elements_Info_Setting': Rows={Rows}, Cols={Cols}",
                sheet.Dimension?.Rows ?? 0, sheet.Dimension?.Columns ?? 0);
            
            // 🔍 DEBUG: Leer directamente la celda AC2 para verificar
            var directAC2 = sheet.Cells["AC2"].Text;
            var directAC3 = sheet.Cells["AC3"].Text;
            _logger.LogInformation("📌 LECTURA DIRECTA - AC2='{AC2}', AC3='{AC3}'", directAC2, directAC3);
            
            // Leer desde fila 2 (fila 1 = encabezados)
            // ⚠️ NO usar while con columna A vacía - puede haber filas con datos en otras columnas
            int row = 2;
            int emptyRowCount = 0;
            int maxRows = sheet.Dimension?.Rows ?? 100;
            
            while (row <= maxRows && emptyRowCount < 5)
            {
                try
                {
                    // Leer nombre del modelo desde columna A
                    var modelName = sheet.Cells[$"A{row}"].Text?.Trim() ?? string.Empty;
                    
                    // Si la fila no tiene nombre de modelo, saltar pero continuar buscando
                    if (string.IsNullOrWhiteSpace(modelName))
                    {
                        emptyRowCount++;
                        row++;
                        continue;
                    }
                    
                    // Reset contador de filas vacías al encontrar una con datos
                    emptyRowCount = 0;
                    
                    // Parsear DisplayType con posible sufijo :N para CompactSlots
                    var (displayType, compactSlots) = ElementDisplayTypeParser.ParseWithCompact(sheet.Cells[$"B{row}"].Text);
                    
                    var config = new ElementInfoSettingConfig
                    {
                        ExcelRowIndex = row,
                        
                        // Columnas base (A-L)
                        ModelName = modelName,  // Ya lo leímos arriba
                        DisplayType = displayType,
                        CompactSlots = compactSlots,
                        ScreenPosition = GetCellText(sheet, $"C{row}"),
                        ModelPosition = GetCellText(sheet, $"D{row}") ?? "top",
                        OffsetX = GetCellDouble(sheet, $"E{row}"),
                        OffsetY = GetCellDouble(sheet, $"F{row}"),
                        OffsetZ = GetCellDouble(sheet, $"G{row}"),
                        ModelIcon = GetCellText(sheet, $"H{row}"),
                        LabelWidth = GetCellDoubleWithDefault(sheet, $"I{row}", 0.6),
                        LabelHeight = GetCellDoubleWithDefault(sheet, $"J{row}", 0.2),
                        LabelScale = GetCellDoubleWithDefault(sheet, $"K{row}", 1.0),
                        ShortName = GetCellText(sheet, $"L{row}")
                    };

                    // Cargar 5 controles (columnas M en adelante, 3 columnas por control)
                    // FORMATO NUEVO (3 columnas por control):
                    //   M=13: Ctrl_1_Var (Variable PLC)
                    //   N=14: Ctrl_1_Config (formato: icon|behaviorType|dataType|enableVar o solo icon.png)
                    //   O=15: Ctrl_1_Text (Descripción)
                    // COMPATIBILIDAD: Si Config contiene solo nombre de archivo (sin |), se trata como icono simple
                    int buttonCol = 13; // Columna M
                    for (int btn = 1; btn <= 5; btn++)
                    {
                        var plcVariable = GetCellText(sheet, row, buttonCol);
                        var configString = GetCellText(sheet, row, buttonCol + 1);
                        var description = GetCellText(sheet, row, buttonCol + 2);
                        
                        // Parsear formato compuesto: icon|behaviorType|dataType|enableVar
                        // Si no contiene |, se trata como icono simple (formato antiguo compatible)
                        var (icon, buttonType, dataType, enableVar) = ControlConfigParser.Parse(configString);
                        
                        var button = new InfoSettingButton
                        {
                            Index = btn,
                            PlcVariable = plcVariable,
                            Description = description,
                            Icon = icon,
                            ButtonType = buttonType,
                            DataType = dataType,
                            EnableVariable = enableVar
                        };
                        
                        if (button.IsConfigured)
                        {
                            config.Buttons.Add(button);
                            // 🔍 DEBUG: Log del botón parseado
                            _logger.LogInformation("   🔘 Botón {Btn} parseado: PlcVar={PlcVar}, Type={Type}, DataType={DataType}, Config='{Config}'",
                                btn, plcVariable, buttonType, dataType, configString);
                        }
                        
                        buttonCol += 3; // 3 columnas por control (mantiene compatibilidad M-AA)
                    }

                    // Cargar 10 slots (columnas AB en adelante, 13 columnas por slot)
                    // AB=28: Slot_1_Type, AC=29: Slot_1_PlcVar, etc.
                    int slotCol = 28; // Columna AB
                    
                    for (int slot = 1; slot <= 10; slot++)
                    {
                        var slotTypeText = GetCellText(sheet, row, slotCol);
                        var plcVar = GetCellText(sheet, row, slotCol + 1);
                        
                        // 🔍 DEBUG: Mostrar qué hay en las columnas del slot
                        if (!string.IsNullOrWhiteSpace(plcVar) || !string.IsNullOrWhiteSpace(slotTypeText))
                        {
                            _logger.LogInformation("   🔍 Row {Row}, Slot {Slot}: Type='{Type}' (col {TypeCol}), PlcVar='{PlcVar}' (col {VarCol})", 
                                row, slot, slotTypeText, slotCol, plcVar, slotCol + 1);
                        }
                        
                        var slotConfig = new InfoSettingSlot
                        {
                            Index = slot,
                            Type = SlotDisplayTypeParser.Parse(slotTypeText),
                            PlcVariable = plcVar,
                            Description = GetCellText(sheet, row, slotCol + 2),
                            Unit = GetCellText(sheet, row, slotCol + 3),
                            Format = GetCellText(sheet, row, slotCol + 4),
                            Min = GetCellNullableDouble(sheet, row, slotCol + 5),
                            Max = GetCellNullableDouble(sheet, row, slotCol + 6),
                            WarningThreshold = GetCellNullableDouble(sheet, row, slotCol + 7),
                            CriticalThreshold = GetCellNullableDouble(sheet, row, slotCol + 8),
                            HistorySize = GetCellNullableInt(sheet, row, slotCol + 9),
                            TextOn = GetCellText(sheet, row, slotCol + 10),
                            TextOff = GetCellText(sheet, row, slotCol + 11),
                            Icon = GetCellText(sheet, row, slotCol + 12)
                        };
                        
                        if (slotConfig.IsConfigured)
                        {
                            config.Slots.Add(slotConfig);
                        }
                        
                        slotCol += 13;
                    }

                    // Solo añadir si tiene nombre de modelo válido (ya validado arriba)
                    configs.Add(config);
                    
                    // Log detallado (usando Information para debug)
                    _logger.LogInformation("   📋 {Model}: {DisplayType}, {ButtonCount} botones, {SlotCount} slots",
                        config.ModelName, config.DisplayType, config.Buttons.Count, config.Slots.Count);
                    
                    // Si no hay slots, mostrar warning
                    if (config.Slots.Count == 0 && config.Buttons.Count == 0)
                    {
                        _logger.LogWarning("   ⚠️ {Model}: Sin botones ni slots configurados", config.ModelName);
                    }
                    
                    // Listar variables PLC para integración con Variable_Views
                    var plcVars = config.GetAllPlcVariables();
                    if (plcVars.Count > 0)
                    {
                        _logger.LogDebug("      Variables PLC: [{Variables}]", string.Join(", ", plcVars));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Error parseando fila {Row} de 3D_Elements_Info_Setting: {Error}", row, ex.Message);
                }
                
                row++;
            }

            // Resumen
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");
            _logger.LogInformation("📊 3D_Elements_Info_Setting: {Count} elementos configurados", configs.Count);
            _logger.LogInformation("────────────────────────────────────────────────────────────────────");
            
            var byDisplayType = configs.GroupBy(c => c.DisplayType);
            foreach (var group in byDisplayType)
            {
                _logger.LogInformation("   {DisplayType}: {Count} elementos", group.Key, group.Count());
            }
            
            var totalButtons = configs.Sum(c => c.Buttons.Count);
            var totalSlots = configs.Sum(c => c.Slots.Count);
            var totalVars = configs.SelectMany(c => c.GetAllPlcVariables()).Distinct().Count();
            
            _logger.LogInformation("────────────────────────────────────────────────────────────────────");
            _logger.LogInformation("   Total botones: {Buttons}, Total slots: {Slots}, Variables únicas: {Vars}", 
                totalButtons, totalSlots, totalVars);
            _logger.LogInformation("════════════════════════════════════════════════════════════════════");
            
            return await Task.FromResult(configs);
        }

        // Helpers para lectura de celdas por índice de columna
        private string? GetCellText(ExcelWorksheet sheet, int row, int col)
        {
            var text = sheet.Cells[row, col].Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private string? GetCellText(ExcelWorksheet sheet, string address)
        {
            var text = sheet.Cells[address].Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private double GetCellDouble(ExcelWorksheet sheet, string address)
        {
            var cell = sheet.Cells[address];
            if (cell.Value is double d) return d;
            if (cell.Value is int i) return i;
            if (double.TryParse(cell.Text?.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0;
        }

        private double GetCellDoubleWithDefault(ExcelWorksheet sheet, string address, double defaultValue)
        {
            var cell = sheet.Cells[address];
            if (cell.Value == null || string.IsNullOrWhiteSpace(cell.Text)) return defaultValue;
            if (cell.Value is double d) return d;
            if (cell.Value is int i) return i;
            if (double.TryParse(cell.Text?.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        private double? GetCellNullableDouble(ExcelWorksheet sheet, int row, int col)
        {
            var cell = sheet.Cells[row, col];
            if (cell.Value == null || string.IsNullOrWhiteSpace(cell.Text)) return null;
            if (cell.Value is double d) return d;
            if (cell.Value is int i) return i;
            if (double.TryParse(cell.Text?.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return null;
        }

        private int? GetCellNullableInt(ExcelWorksheet sheet, int row, int col)
        {
            var cell = sheet.Cells[row, col];
            if (cell.Value == null || string.IsNullOrWhiteSpace(cell.Text)) return null;
            if (cell.Value is int i) return i;
            if (cell.Value is double d) return (int)d;
            if (int.TryParse(cell.Text, out var result))
            {
                return result;
            }
            return null;
        }

        #endregion
        
        #region ⚡ Semiautomatic Mode
        
        /// <summary>
        /// Carga la configuración del modo semiautomático desde la hoja "Semiautomatic_Mode"
        /// Columnas: A=MainVar(solo A2), B=Descripción, C=Variable PLC, D=Modo Visibilidad (0/1/2)
        /// </summary>
        public async Task<SemiautomaticConfiguration> LoadSemiautomaticConfigAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Semiautomatic_Mode no disponible.", fullPath);
                    return new SemiautomaticConfiguration();
                }

                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var package = new ExcelPackage(stream);
                
                var sheet = package.Workbook.Worksheets["Semiautomatic_Mode"];
                
                if (sheet == null)
                {
                    _logger.LogWarning("⚠️ Hoja 'Semiautomatic_Mode' no encontrada en Excel.");
                    
                    // En desarrollo, devolver datos de prueba
                    if (_environment.IsDevelopment())
                    {
                        _logger.LogInformation("🔧 [DEV] Usando datos de prueba para Semiautomatic_Mode");
                        return new SemiautomaticConfiguration
                        {
                            MainPlcVariable = "GVL.bSemiMode",
                            Elements = new List<SemiautomaticElement>
                            {
                                new SemiautomaticElement { Description = "Bomba Test 1", PlcVariable = "GVL.bPump1", VisibilityMode = 1 },
                                new SemiautomaticElement { Description = "Bomba Test 2", PlcVariable = "GVL.bPump2", VisibilityMode = 1 },
                                new SemiautomaticElement { Description = "Motor Solo Semi", PlcVariable = "GVL.bMotor1", VisibilityMode = 2 }
                            }
                        };
                    }
                    
                    return new SemiautomaticConfiguration();
                }

                _logger.LogInformation("⚡ Cargando configuración de Modo Semiautomático...");

                var config = new SemiautomaticConfiguration();
                
                // A2 = Variable PLC principal para activar modo semiautomático
                config.MainPlcVariable = sheet.Cells["A2"].Text?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(config.MainPlcVariable))
                {
                    _logger.LogWarning("⚠️ Semiautomatic_Mode: No se encontró variable principal en A2");
                }
                else
                {
                    _logger.LogInformation("⚡ Variable principal semiautomático: {Var}", config.MainPlcVariable);
                }

                // Leer elementos desde fila 2 (fila 1 = encabezados)
                int row = 2;
                int emptyRowCount = 0;
                int maxRows = sheet.Dimension?.Rows ?? 100;
                
                while (row <= maxRows && emptyRowCount < 5)
                {
                    try
                    {
                        var description = sheet.Cells[$"B{row}"].Text?.Trim() ?? string.Empty;
                        var plcVariable = sheet.Cells[$"C{row}"].Text?.Trim() ?? string.Empty;
                        var visibilityText = sheet.Cells[$"D{row}"].Text?.Trim() ?? "1";
                        
                        // Si no hay descripción y variable PLC, saltar
                        if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(plcVariable))
                        {
                            emptyRowCount++;
                            row++;
                            continue;
                        }
                        
                        // Reset contador de filas vacías
                        emptyRowCount = 0;
                        
                        // Parsear modo de visibilidad
                        int visibilityMode = 1; // Default: siempre visible
                        if (int.TryParse(visibilityText, out var parsedVis))
                        {
                            visibilityMode = Math.Max(0, Math.Min(parsedVis, 2)); // Clamp 0-2
                        }
                        
                        // Solo añadir si tiene variable PLC
                        if (!string.IsNullOrWhiteSpace(plcVariable))
                        {
                            config.Elements.Add(new SemiautomaticElement
                            {
                                Description = description,
                                PlcVariable = plcVariable,
                                VisibilityMode = visibilityMode
                            });
                            
                            _logger.LogDebug("   ⚡ Fila {Row}: '{Desc}' -> {Var} (vis:{Vis})", 
                                row, description, plcVariable, visibilityMode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error parseando fila {Row} de Semiautomatic_Mode: {Error}", row, ex.Message);
                    }
                    
                    row++;
                }

                _logger.LogInformation("✅ Semiautomatic_Mode: {Count} elementos cargados", config.Elements.Count);
                
                // Resumen por tipo de visibilidad
                var vis0 = config.Elements.Count(e => e.VisibilityMode == 0);
                var vis1 = config.Elements.Count(e => e.VisibilityMode == 1);
                var vis2 = config.Elements.Count(e => e.VisibilityMode == 2);
                _logger.LogInformation("   Visibilidad: 0(oculto)={V0}, 1(siempre)={V1}, 2(solo semi)={V2}", vis0, vis1, vis2);
                
                return await Task.FromResult(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando Semiautomatic_Mode: {Message}", ex.Message);
                return new SemiautomaticConfiguration();
            }
        }
        
        #endregion
        
        /// <summary>
        /// Carga la configuración de colores por estado desde la hoja PLC_State_Colors
        /// </summary>
        public async Task<List<StateColorConfig>> LoadStateColorsAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            var cacheKey = fullPath.ToLowerInvariant(); // Normalizar para comparación
            
            // ✅ VERIFICAR CACHÉ POR ARCHIVO
            lock (_cacheLock)
            {
                if (_stateColorsCache.TryGetValue(cacheKey, out var cached))
                {
                    var cacheAge = DateTime.Now - cached.Timestamp;
                    if (cacheAge < _cacheExpiration)
                    {
                        _logger.LogInformation("📦 Usando state colors desde CACHÉ para {Path} (edad: {Age:F1}s, {Count} configs)", 
                            Path.GetFileName(fullPath), cacheAge.TotalSeconds, cached.Colors.Count);
                        _metricsService.RecordExcelLoadTime(0.1); // ✅ Cache hit = casi 0ms
                        return cached.Colors;
                    }
                    else
                    {
                        _logger.LogInformation("⏰ Caché de state colors expirado para {Path}, recargando", Path.GetFileName(fullPath));
                    }
                }
                else
                {
                    _logger.LogInformation("🔍 No hay caché de state colors para {Path}, cargando desde Excel", Path.GetFileName(fullPath));
                }
            }
            
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Returning empty state colors.", fullPath);
                    var emptyList = new List<StateColorConfig>();
                    
                    // ✅ CACHEAR LISTA VACÍA TAMBIÉN
                    lock (_cacheLock)
                    {
                        _stateColorsCache[cacheKey] = (emptyList, DateTime.Now);
                    }
                    
                    return emptyList;
                }
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    var stateColors = await LoadStateColorsFromSheetAsync(package);
                    
                    // ✅ GUARDAR EN CACHÉ POR ARCHIVO
                    lock (_cacheLock)
                    {
                        _stateColorsCache[cacheKey] = (stateColors, DateTime.Now);
                    }
                    _logger.LogDebug("💾 State colors guardados en caché para {Path} ({Count} configs)", Path.GetFileName(fullPath), stateColors.Count);
                    
                    return stateColors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading state colors from Excel: {Message}", ex.Message);
                var errorList = new List<StateColorConfig>();
                
                // ✅ CACHEAR LISTA VACÍA EN ERROR
                lock (_cacheLock)
                {
                    _stateColorsCache[cacheKey] = (errorList, DateTime.Now);
                }
                
                return errorList;
            }
        }
        
        private async Task<List<StateColorConfig>> LoadStateColorsFromSheetAsync(ExcelPackage package)
        {
            var stateColors = new List<StateColorConfig>();
            
            // Buscar hoja con colores (orden de prioridad - actualizado para nueva estructura)
            var sheet = package.Workbook.Worksheets["3D Elements"]      // ← Nueva estructura
                     ?? package.Workbook.Worksheets["1) Pumps"]         // ← Fallback legacy
                     ?? package.Workbook.Worksheets["Pumps"]
                     ?? package.Workbook.Worksheets["PLC_State_Colors"] 
                     ?? package.Workbook.Worksheets["PumpElements"]
                     ?? package.Workbook.Worksheets["3D_Models"]
                     ?? package.Workbook.Worksheets.FirstOrDefault();
            
            if (sheet == null)
            {
                _logger.LogWarning("❌ No worksheets found in Excel file");
                return stateColors;
            }
            
            _logger.LogInformation("📊 Reading state colors from sheet: '{SheetName}'", sheet.Name);
            _logger.LogInformation("📋 Estructura: J=On(2), K=Off(1), L=Disabled(0), M=Alarm(3)");
            
            // Leer desde la fila 2 (la 1 es encabezado)
            // ESTRUCTURA REAL:
            // Columna J = Color para estado ON (2)
            // Columna K = Color para estado OFF (1)
            // Columna L = Color para estado DISABLED (0)
            // Columna M = Color para estado ALARM (3)
            int row = 2;
            int loadedCount = 0;
            int emptyRowCount = 0;
            
            _logger.LogInformation("🔍 Iniciando lectura de filas desde row 2...");
            
            while (row < 1000) // Límite de seguridad
            {
                // LEER NOMBRE COMPLETO DE LA VARIABLE DESDE COLUMNA G
                // (La columna A solo tiene el número total en A2, las demás filas A3,A4,... están vacías)
                var fullVariableName = sheet.Cells[$"G{row}"].Text;
                
                _logger.LogInformation($"📋 Fila {row}: G='{fullVariableName}'");
                
                // Verificar si la columna G tiene datos (esto indica que la fila tiene configuración)
                if (string.IsNullOrWhiteSpace(fullVariableName))
                {
                    emptyRowCount++;
                    _logger.LogDebug($"⚠️ Fila {row} vacía en columna G (contador: {emptyRowCount})");
                    
                    // Si encontramos 10 filas vacías consecutivas en columna G, terminar
                    if (emptyRowCount >= 10)
                    {
                        _logger.LogInformation($"🛑 Terminando lectura: {emptyRowCount} filas vacías consecutivas en columna G");
                        break;
                    }
                    row++;
                    continue;
                }
                
                emptyRowCount = 0; // Reset contador al encontrar fila con datos en columna G
                
                try
                {
                    // Usar el nombre completo de la variable (ej: "MAIN.fbMachine.st_MainForm.i_StatePumps[1]")
                    string variablePattern = fullVariableName.Trim();
                    _logger.LogDebug("✅ Variable encontrada en fila {Row}: {Variable}", row, variablePattern);
                    
                    // Leer colores de las 4 columnas (pueden estar en diferentes formatos)
                    var colorOn = sheet.Cells[$"H{row}"].Text;       // Estado 2 (On)
                    var colorOff = sheet.Cells[$"I{row}"].Text;      // Estado 1 (Off)
                    var colorDisabled = sheet.Cells[$"J{row}"].Text; // Estado 0 (Disabled)
                    var colorAlarm = sheet.Cells[$"K{row}"].Text;    // Estado 3 (Alarm)
                    
                    // ✅ LEER COLORES DE LOS 5 HIJOS (si existen)
                    // Child1: AW/AX/AY/AZ, Child2: BR/BS/BT/BU, Child3: CM/CN/CO/CP, Child4: DH/DI/DJ/DK, Child5: EC/ED/EE/EF
                    var child1ColorOn = sheet.Cells[$"AW{row}"].Text;
                    var child1ColorOff = sheet.Cells[$"AX{row}"].Text;
                    var child1ColorDisabled = sheet.Cells[$"AY{row}"].Text;
                    var child1ColorAlarm = sheet.Cells[$"AZ{row}"].Text;
                    
                    var child2ColorOn = sheet.Cells[$"BR{row}"].Text;
                    var child2ColorOff = sheet.Cells[$"BS{row}"].Text;
                    var child2ColorDisabled = sheet.Cells[$"BT{row}"].Text;
                    var child2ColorAlarm = sheet.Cells[$"BU{row}"].Text;
                    
                    var child3ColorOn = sheet.Cells[$"CM{row}"].Text;
                    var child3ColorOff = sheet.Cells[$"CN{row}"].Text;
                    var child3ColorDisabled = sheet.Cells[$"CO{row}"].Text;
                    var child3ColorAlarm = sheet.Cells[$"CP{row}"].Text;
                    
                    var child4ColorOn = sheet.Cells[$"DH{row}"].Text;
                    var child4ColorOff = sheet.Cells[$"DI{row}"].Text;
                    var child4ColorDisabled = sheet.Cells[$"DJ{row}"].Text;
                    var child4ColorAlarm = sheet.Cells[$"DK{row}"].Text;
                    
                    var child5ColorOn = sheet.Cells[$"EC{row}"].Text;
                    var child5ColorOff = sheet.Cells[$"ED{row}"].Text;
                    var child5ColorDisabled = sheet.Cells[$"EE{row}"].Text;
                    var child5ColorAlarm = sheet.Cells[$"EF{row}"].Text;
                    
                    // ✅ TODOS (padre + hijos) usan la MISMA variable de columna C (variablePattern)
                    // pero cada uno tiene sus propias columnas de colores
                    var colorConfigs = new[]
                    {
                        // PADRE (Parent model colors - columnas H/I/J/K)
                        new { State = 2, Name = "On", Color = colorOn, Column = "H", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = colorOff, Column = "I", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = colorDisabled, Column = "J", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = colorAlarm, Column = "K", VariablePattern = variablePattern },
                        
                        // CHILD 1 (usa MISMA variable C, colores AW/AX/AY/AZ)
                        new { State = 2, Name = "On", Color = child1ColorOn, Column = "AW", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = child1ColorOff, Column = "AX", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = child1ColorDisabled, Column = "AY", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = child1ColorAlarm, Column = "AZ", VariablePattern = variablePattern },
                        
                        // CHILD 2 (usa MISMA variable C, colores BR/BS/BT/BU)
                        new { State = 2, Name = "On", Color = child2ColorOn, Column = "BR", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = child2ColorOff, Column = "BS", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = child2ColorDisabled, Column = "BT", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = child2ColorAlarm, Column = "BU", VariablePattern = variablePattern },
                        
                        // CHILD 3 (usa MISMA variable C, colores CM/CN/CO/CP)
                        new { State = 2, Name = "On", Color = child3ColorOn, Column = "CM", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = child3ColorOff, Column = "CN", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = child3ColorDisabled, Column = "CO", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = child3ColorAlarm, Column = "CP", VariablePattern = variablePattern },
                        
                        // CHILD 4 (usa MISMA variable C, colores DH/DI/DJ/DK)
                        new { State = 2, Name = "On", Color = child4ColorOn, Column = "DH", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = child4ColorOff, Column = "DI", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = child4ColorDisabled, Column = "DJ", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = child4ColorAlarm, Column = "DK", VariablePattern = variablePattern },
                        
                        // CHILD 5 (usa MISMA variable C, colores EC/ED/EE/EF)
                        new { State = 2, Name = "On", Color = child5ColorOn, Column = "EC", VariablePattern = variablePattern },
                        new { State = 1, Name = "Off", Color = child5ColorOff, Column = "ED", VariablePattern = variablePattern },
                        new { State = 0, Name = "Disabled", Color = child5ColorDisabled, Column = "EE", VariablePattern = variablePattern },
                        new { State = 3, Name = "Alarm", Color = child5ColorAlarm, Column = "EF", VariablePattern = variablePattern }
                    };
                    
                    foreach (var config in colorConfigs)
                    {
                        // ✅ Saltar si el color o la variable están vacíos
                        if (string.IsNullOrWhiteSpace(config.Color) || string.IsNullOrWhiteSpace(config.VariablePattern)) continue;
                        
                        // Parsear color (puede ser hex #RRGGBB o RGB separado)
                        var rgb = ParseColorValue(config.Color);
                        
                        if (rgb.HasValue)
                        {
                            var stateColor = new StateColorConfig
                            {
                                VariablePattern = config.VariablePattern,  // ✅ Usar la variable específica (padre o hijo)
                                StateValue = config.State,
                                StateName = config.Name,
                                ColorR = rgb.Value.R,
                                ColorG = rgb.Value.G,
                                ColorB = rgb.Value.B,
                                Description = $"{config.VariablePattern} - {config.Name} (Fila {row}, Col {config.Column})"
                            };
                            
                            stateColors.Add(stateColor);
                            loadedCount++;
                            _logger.LogDebug("✅ Row {Row}, Col {Col}: {Pattern} state={State}({Name}) RGB=({R},{G},{B}) Hex={Hex}", 
                                row, config.Column, stateColor.VariablePattern, config.State, config.Name,
                                rgb.Value.R, rgb.Value.G, rgb.Value.B, stateColor.ColorHex);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ No se pudo parsear color en fila {Row}, columna {Col}: '{Color}'", 
                                row, config.Column, config.Color);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "❌ Error parsing row {Row}: {Message}", row, ex.Message);
                }
                
                row++;
            }
            
            _logger.LogInformation("✅ Loaded {Count} state color configurations from Excel (sheet: {SheetName})", 
                stateColors.Count, sheet.Name);
            
            // Mostrar resumen por estado
            var summary = stateColors.GroupBy(c => c.StateValue).Select(g => new { State = g.Key, Count = g.Count() });
            foreach (var s in summary)
            {
                var stateName = s.State switch { 0 => "Disabled", 1 => "Off", 2 => "On", 3 => "Alarm", _ => "Unknown" };
                _logger.LogInformation("   Estado {State} ({Name}): {Count} configuraciones", s.State, stateName, s.Count);
            }
            
            return await Task.FromResult(stateColors);
        }
        
        /// <summary>
        /// Parsea un valor de color desde Excel (soporta nombres CSS, hex #RRGGBB o RGB separado)
        /// </summary>
        private (int R, int G, int B)? ParseColorValue(string colorValue)
        {
            if (string.IsNullOrWhiteSpace(colorValue)) return null;
            
            colorValue = colorValue.Trim();
            
            // 1. Intentar parsear como nombre de color CSS/HTML
            var namedColor = ConvertNamedColorToRgb(colorValue);
            if (namedColor.HasValue) return namedColor;
            
            // 2. Formato hexadecimal: #RRGGBB o RRGGBB
            if (colorValue.StartsWith("#"))
            {
                colorValue = colorValue.Substring(1);
            }
            
            if (colorValue.Length == 6 && colorValue.All(c => "0123456789ABCDEFabcdef".Contains(c)))
            {
                try
                {
                    int r = Convert.ToInt32(colorValue.Substring(0, 2), 16);
                    int g = Convert.ToInt32(colorValue.Substring(2, 2), 16);
                    int b = Convert.ToInt32(colorValue.Substring(4, 2), 16);
                    return (r, g, b);
                }
                catch
                {
                    return null;
                }
            }
            
            // 3. Formato RGB separado: "255,0,0" o "255 0 0"
            var parts = colorValue.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int r) && 
                    int.TryParse(parts[1], out int g) && 
                    int.TryParse(parts[2], out int b))
                {
                    return (Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
                }
            }
            
            // 4. Intentar parsear como número decimal (valor RGB único para todos)
            if (int.TryParse(colorValue, out int grayValue))
            {
                grayValue = Math.Clamp(grayValue, 0, 255);
                return (grayValue, grayValue, grayValue);
            }
            
            return null;
        }
        
        /// <summary>
        /// Convierte nombres de colores CSS/HTML a valores RGB
        /// </summary>
        private (int R, int G, int B)? ConvertNamedColorToRgb(string colorName)
        {
            var colors = new Dictionary<string, (int R, int G, int B)>(StringComparer.OrdinalIgnoreCase)
            {
                { "AliceBlue", (240, 248, 255) }, { "AntiqueWhite", (250, 235, 215) }, { "Aqua", (0, 255, 255) },
                { "Aquamarine", (127, 255, 212) }, { "Azure", (240, 255, 255) }, { "Beige", (245, 245, 220) },
                { "Bisque", (255, 228, 196) }, { "Black", (0, 0, 0) }, { "BlanchedAlmond", (255, 235, 205) },
                { "Blue", (0, 0, 255) }, { "BlueViolet", (138, 43, 226) }, { "Brown", (165, 42, 42) },
                { "BurlyWood", (222, 184, 135) }, { "CadetBlue", (95, 158, 160) }, { "Chartreuse", (127, 255, 0) },
                { "Chocolate", (210, 105, 30) }, { "Coral", (255, 127, 80) }, { "CornflowerBlue", (100, 149, 237) },
                { "Cornsilk", (255, 248, 220) }, { "Crimson", (220, 20, 60) }, { "Cyan", (0, 255, 255) },
                { "DarkBlue", (0, 0, 139) }, { "DarkCyan", (0, 139, 139) }, { "DarkGoldenrod", (184, 134, 11) },
                { "DarkGray", (169, 169, 169) }, { "DarkGreen", (0, 100, 0) }, { "DarkKhaki", (189, 183, 107) },
                { "DarkMagenta", (139, 0, 139) }, { "DarkMagena", (139, 0, 139) }, { "DarkOliveGreen", (85, 107, 47) },
                { "DarkOrange", (255, 140, 0) }, { "DarkOrchid", (153, 50, 204) }, { "DarkRed", (139, 0, 0) },
                { "DarkSalmon", (233, 150, 122) }, { "DarkSeaGreen", (143, 188, 143) }, { "DarkSlateBlue", (72, 61, 139) },
                { "DarkSlateGray", (47, 79, 79) }, { "DarkTurquoise", (0, 206, 209) }, { "DarkViolet", (148, 0, 211) },
                { "DeepPink", (255, 20, 147) }, { "DeepSkyBlue", (0, 191, 255) }, { "DimGray", (105, 105, 105) },
                { "DodgerBlue", (30, 144, 255) }, { "Firebrick", (178, 34, 34) }, { "FloralWhite", (255, 250, 240) },
                { "ForestGreen", (34, 139, 34) }, { "Fuchsia", (255, 0, 255) }, { "Fuschia", (255, 0, 255) },
                { "Gainsboro", (220, 220, 220) }, { "GhostWhite", (248, 248, 255) }, { "Gold", (255, 215, 0) },
                { "Goldenrod", (218, 165, 32) }, { "Gray", (128, 128, 128) }, { "Green", (0, 128, 0) },
                { "GreenYellow", (173, 255, 47) }, { "Honeydew", (240, 255, 240) }, { "HotPink", (255, 105, 180) },
                { "IndianRed", (205, 92, 92) }, { "Indigo", (75, 0, 130) }, { "Ivory", (255, 255, 240) },
                { "Khaki", (240, 230, 140) }, { "Lavender", (230, 230, 250) }, { "LavenderBlush", (255, 240, 245) },
                { "LawnGreen", (124, 252, 0) }, { "LemonChiffon", (255, 250, 205) }, { "LightBlue", (173, 216, 230) },
                { "LightCoral", (240, 128, 128) }, { "LightCyan", (224, 255, 255) }, { "LightGoldenrodYellow", (250, 250, 210) },
                { "LightGreen", (144, 238, 144) }, { "LightGray", (211, 211, 211) }, { "LightPink", (255, 182, 193) },
                { "LightSalmon", (255, 160, 122) }, { "LightSeaGreen", (32, 178, 170) }, { "LightSkyBlue", (135, 206, 250) },
                { "LightSlateGray", (119, 136, 153) }, { "LightSteelBlue", (176, 196, 222) }, { "LightYellow", (255, 255, 224) },
                { "Lime", (0, 255, 0) }, { "LimeGreen", (50, 205, 50) }, { "Linen", (250, 240, 230) },
                { "Magenta", (255, 0, 255) }, { "Maroon", (128, 0, 0) }, { "MediumAquamarine", (102, 205, 170) },
                { "MediumBlue", (0, 0, 205) }, { "MediumOrchid", (186, 85, 211) }, { "MediumPurple", (147, 112, 219) },
                { "MediumSeaGreen", (60, 179, 113) }, { "MediumSlateBlue", (123, 104, 238) }, { "MediumSpringGreen", (0, 250, 154) },
                { "MediumTurquoise", (72, 209, 204) }, { "MediumVioletRed", (199, 21, 133) }, { "MidnightBlue", (25, 25, 112) },
                { "MintCream", (245, 255, 250) }, { "MistyRose", (255, 228, 225) }, { "Moccasin", (255, 228, 181) },
                { "NavajoWhite", (255, 222, 173) }, { "Navy", (0, 0, 128) }, { "OldLace", (253, 245, 230) },
                { "Olive", (128, 128, 0) }, { "OliveDrab", (107, 142, 35) }, { "Orange", (255, 165, 0) },
                { "OrangeRed", (255, 69, 0) }, { "Orchid", (218, 112, 214) }, { "PaleGoldenrod", (238, 232, 170) },
                { "PaleGreen", (152, 251, 152) }, { "PaleTurquoise", (175, 238, 238) }, { "PaleVioletRed", (219, 112, 147) },
                { "PapayaWhip", (255, 239, 213) }, { "PeachPuff", (255, 218, 185) }, { "Peru", (205, 133, 63) },
                { "Pink", (255, 192, 203) }, { "Plum", (221, 160, 221) }, { "PowderBlue", (176, 224, 230) },
                { "Purple", (128, 0, 128) }, { "Red", (255, 0, 0) }, { "RosyBrown", (188, 143, 143) },
                { "RoyalBlue", (65, 105, 225) }, { "SaddleBrown", (139, 69, 19) }, { "Salmon", (250, 128, 114) },
                { "SandyBrown", (244, 164, 96) }, { "SeaGreen", (46, 139, 87) }, { "Seashell", (255, 245, 238) },
                { "Sienna", (160, 82, 45) }, { "Silver", (192, 192, 192) }, { "SkyBlue", (135, 206, 235) },
                { "SlateBlue", (106, 90, 205) }, { "SlateGray", (112, 128, 144) }, { "Snow", (255, 250, 250) },
                { "SpringGreen", (0, 255, 127) }, { "SteelBlue", (70, 130, 180) }, { "Tan", (210, 180, 140) },
                { "Teal", (0, 128, 128) }, { "Thistle", (216, 191, 216) }, { "Tomato", (255, 99, 71) },
                { "Turquoise", (64, 224, 208) }, { "Violet", (238, 130, 238) }, { "Wheat", (245, 222, 179) },
                { "White", (255, 255, 255) }, { "WhiteSmoke", (245, 245, 245) }, { "Yellow", (255, 255, 0) },
                { "YellowGreen", (154, 205, 50) }
            };
            
            if (colors.TryGetValue(colorName, out var rgb))
            {
                return rgb;
            }
            
            return null;
        }

        /// <summary>
        /// Obtiene la lista de nombres de variables PLC únicas que deben ser monitoreadas
        /// desde la configuración de StateColors en el Excel
        /// </summary>
        /// <summary>
        /// 🔍 ESCANEO AUTOMÁTICO: Busca TODAS las variables PLC en TODAS las hojas del Excel.
        /// Cualquier celda que contenga "MAIN.fbMachine" se considera una variable PLC.
        /// NO requiere código específico para nuevas hojas - es completamente automático.
        /// </summary>
        public async Task<List<string>> GetMonitoredVariableNamesAsync(string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {FilePath}", fullPath);
                    return new List<string>();
                }

                using (var package = new ExcelPackage(new FileInfo(fullPath)))
                {
                    var variableNames = new HashSet<string>(); // Usar HashSet para evitar duplicados
                    var variablesBySheet = new Dictionary<string, int>(); // Para logging
                    
                    _logger.LogInformation("════════════════════════════════════════════════════════════════════");
                    _logger.LogInformation("🔍 ESCANEO AUTOMÁTICO DE VARIABLES PLC");
                    _logger.LogInformation("   Buscando 'MAIN.fbMachine' en TODAS las hojas del Excel...");
                    _logger.LogInformation("────────────────────────────────────────────────────────────────────");

                    // 🔄 Escanear TODAS las hojas del Excel
                    foreach (var worksheet in package.Workbook.Worksheets)
                    {
                        if (worksheet.Dimension == null) continue;
                        
                        // ⚠️ EXCLUIR hoja Variable_Views - contiene PATRONES, no variables reales
                        if (worksheet.Name.Equals("Variable_Views", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("   ⏭️ Saltando hoja 'Variable_Views' (contiene patrones, no variables)");
                            continue;
                        }
                        
                        int sheetVarCount = 0;
                        int rowCount = worksheet.Dimension.Rows;
                        // ⚠️ worksheet.Dimension.Columns puede no incluir columnas lejanas si hay vacías intermedias
                        // Usar un rango fijo amplio para asegurar que llegamos hasta AC (col 29) y más allá
                        int colCount = Math.Max(worksheet.Dimension.Columns, 200); // Al menos 200 columnas (hasta columna GR)
                        
                        _logger.LogDebug("   🔍 Escaneando hoja '{SheetName}': {Rows} filas x {Cols} columnas", 
                            worksheet.Name, rowCount, colCount);
                        
                        // Escanear todas las celdas de la hoja
                        for (int row = 1; row <= rowCount; row++)
                        {
                            for (int col = 1; col <= colCount; col++)
                            {
                                var cellValue = worksheet.Cells[row, col].Text?.Trim();
                                
                                // Si la celda contiene una variable PLC (empieza con MAIN.fbMachine)
                                if (!string.IsNullOrWhiteSpace(cellValue) && 
                                    cellValue.StartsWith("MAIN.fbMachine", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Limpiar la variable (quitar espacios, comillas, etc.)
                                    var cleanVar = cellValue.Trim().Trim('"').Trim('\'');
                                    
                                    // ⚠️ EXCLUIR patrones con wildcards (* o ?) - son para filtrado, no variables reales
                                    if (cleanVar.Contains('*') || cleanVar.Contains('?'))
                                    {
                                        continue; // Es un patrón, no una variable
                                    }
                                    
                                    // Verificar que sea una variable válida (no contiene espacios ni caracteres raros)
                                    if (!cleanVar.Contains(' ') && !cleanVar.Contains('\n') && cleanVar.Length < 200)
                                    {
                                        if (variableNames.Add(cleanVar))
                                        {
                                            sheetVarCount++;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (sheetVarCount > 0)
                        {
                            variablesBySheet[worksheet.Name] = sheetVarCount;
                            _logger.LogInformation("   📋 {SheetName}: {Count} variables encontradas", 
                                worksheet.Name, sheetVarCount);
                        }
                    }
                    
                    // 🔔 CASO ESPECIAL: Variables de historial de alarmas (st_alarmHistPc)
                    // Por cada st_alarmPc[x] añadir automáticamente st_alarmHistPc[x]
                    var alarmVars = variableNames.Where(v => v.Contains("st_alarmPc[")).ToList();
                    int histVarsAdded = 0;
                    foreach (var alarmVar in alarmVars)
                    {
                        var histVar = alarmVar.Replace("st_alarmPc[", "st_alarmHistPc[");
                        if (variableNames.Add(histVar))
                        {
                            histVarsAdded++;
                        }
                    }
                    if (histVarsAdded > 0)
                    {
                        _logger.LogInformation("   🔔 Alarmas historial: +{Count} variables (st_alarmHistPc)", histVarsAdded);
                    }
                    
                    // 🎯 CASO ESPECIAL: Variables de 3D_Elements_Info_Setting (paneles de información 3D)
                    // Estas variables están en columnas específicas de slots y pueden no detectarse con el escaneo general
                    try
                    {
                        var infoSettingConfigs = await LoadElementsInfoSettingFromSheetAsync(package);
                        int infoVarsAdded = 0;
                        foreach (var config in infoSettingConfigs)
                        {
                            // Variables de slots
                            if (config.Slots != null)
                            {
                                foreach (var slot in config.Slots)
                                {
                                    if (!string.IsNullOrWhiteSpace(slot.PlcVariable) && 
                                        slot.PlcVariable.StartsWith("MAIN.fbMachine", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (variableNames.Add(slot.PlcVariable))
                                        {
                                            infoVarsAdded++;
                                        }
                                    }
                                }
                            }
                            // Variables de botones (si existen)
                            if (config.Buttons != null)
                            {
                                foreach (var btn in config.Buttons)
                                {
                                    // Variable principal del botón
                                    if (!string.IsNullOrWhiteSpace(btn.PlcVariable) && 
                                        btn.PlcVariable.StartsWith("MAIN.fbMachine", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (variableNames.Add(btn.PlcVariable))
                                        {
                                            infoVarsAdded++;
                                        }
                                    }
                                    // 🔑 TAMBIÉN añadir EnableVariable para monitoreo (habilita/deshabilita botón)
                                    if (!string.IsNullOrWhiteSpace(btn.EnableVariable) && 
                                        btn.EnableVariable.StartsWith("MAIN.fbMachine", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (variableNames.Add(btn.EnableVariable))
                                        {
                                            infoVarsAdded++;
                                            _logger.LogDebug("   🔑 EnableVariable añadida: {Var}", btn.EnableVariable);
                                        }
                                    }
                                }
                            }
                        }
                        if (infoVarsAdded > 0)
                        {
                            _logger.LogInformation("   🎯 3D_Elements_Info_Setting: +{Count} variables (slots/buttons)", infoVarsAdded);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error extrayendo variables de 3D_Elements_Info_Setting: {Message}", ex.Message);
                    }
                    
                    // 📟 CASO ESPECIAL: Variables del PLC Info Panel (WSTRING)
                    // Estas variables se leen desde la hoja Plc_InfoPanel y deben monitorearse
                    try
                    {
                        var plcInfoPanelConfig = await LoadPlcInfoPanelAsync(filePath);
                        int infoPanelVarsAdded = 0;
                        foreach (var plcVar in plcInfoPanelConfig.AllVariables)
                        {
                            if (!string.IsNullOrWhiteSpace(plcVar) && 
                                plcVar.StartsWith("MAIN.fbMachine", StringComparison.OrdinalIgnoreCase))
                            {
                                if (variableNames.Add(plcVar))
                                {
                                    infoPanelVarsAdded++;
                                }
                            }
                        }
                        if (infoPanelVarsAdded > 0)
                        {
                            _logger.LogInformation("   📟 Plc_InfoPanel: +{Count} variables añadidas explícitamente", infoPanelVarsAdded);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error extrayendo variables de Plc_InfoPanel: {Message}", ex.Message);
                    }
                    
                    // 🔔 CASO ESPECIAL: Variables de ALARMAS desde hoja "Alarms"
                    // Estas variables NO se detectan automáticamente porque la hoja tiene formato especial
                    // Necesitamos agregarlas explícitamente para que el polling las monitoree
                    try
                    {
                        var alarmConfig = await LoadAlarmsAsync(filePath);
                        int alarmVarsAdded = 0;
                        
                        // Agregar variables de Alarm
                        foreach (var alarm in alarmConfig.Alarms)
                        {
                            if (!string.IsNullOrWhiteSpace(alarm.PlcVariable))
                            {
                                if (variableNames.Add(alarm.PlcVariable))
                                {
                                    alarmVarsAdded++;
                                }
                            }
                        }
                        
                        // Agregar variables de Notification
                        foreach (var notification in alarmConfig.Notifications)
                        {
                            if (!string.IsNullOrWhiteSpace(notification.PlcVariable))
                            {
                                if (variableNames.Add(notification.PlcVariable))
                                {
                                    alarmVarsAdded++;
                                }
                            }
                        }
                        
                        // Agregar variables de Info
                        foreach (var info in alarmConfig.Infos)
                        {
                            if (!string.IsNullOrWhiteSpace(info.PlcVariable))
                            {
                                if (variableNames.Add(info.PlcVariable))
                                {
                                    alarmVarsAdded++;
                                }
                            }
                        }
                        
                        if (alarmVarsAdded > 0)
                        {
                            _logger.LogInformation("   🔔 Alarms: +{Count} variables de alarma añadidas (st_alarmPc[x].Alarm/Notification/Info)", alarmVarsAdded);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error extrayendo variables de Alarms: {Message}", ex.Message);
                    }
                    
                    // 🔔 REGENERAR variables de historial de alarmas (st_alarmHistPc)
                    // Ahora que tenemos TODAS las variables st_alarmPc (incluyendo las de hoja Alarms),
                    // generamos las correspondientes st_alarmHistPc
                    var allAlarmVars = variableNames.Where(v => v.Contains("st_alarmPc[")).ToList();
                    int histVarsAddedFinal = 0;
                    foreach (var alarmVar in allAlarmVars)
                    {
                        var histVar = alarmVar.Replace("st_alarmPc[", "st_alarmHistPc[");
                        if (variableNames.Add(histVar))
                        {
                            histVarsAddedFinal++;
                        }
                    }
                    if (histVarsAddedFinal > 0)
                    {
                        _logger.LogInformation("   🔔 Alarmas historial: +{Count} variables (st_alarmHistPc) generadas desde st_alarmPc", histVarsAddedFinal);
                    }
                    
                    _logger.LogInformation("────────────────────────────────────────────────────────────────────");
                    _logger.LogInformation("✅ TOTAL: {Count} variables PLC únicas encontradas en {SheetCount} hojas",
                        variableNames.Count, variablesBySheet.Count);
                    _logger.LogInformation("════════════════════════════════════════════════════════════════════");

                    var variableList = variableNames.ToList();
                    return variableList;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo variables monitoreadas desde Excel");
                return new List<string>();
            }
        }

        /// <summary>
        /// Carga la configuración del sistema desde la hoja "System Config" del Excel
        /// </summary>
        public async Task<SystemConfiguration> LoadSystemConfigurationAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            var cacheKey = fullPath.ToLowerInvariant(); // Normalizar para comparación
            
            // ✅ VERIFICAR CACHÉ POR ARCHIVO
            lock (_cacheLock)
            {
                if (_systemConfigCache.TryGetValue(cacheKey, out var cached))
                {
                    var cacheAge = DateTime.Now - cached.Timestamp;
                    if (cacheAge < _cacheExpiration)
                    {
                        _logger.LogInformation("📦 Usando configuración del sistema desde CACHÉ para {Path} (edad: {Age:F1}s)", 
                            Path.GetFileName(fullPath), cacheAge.TotalSeconds);
                        _metricsService.RecordExcelLoadTime(0.1); // ✅ Cache hit = casi 0ms
                        return cached.Config;
                    }
                    else
                    {
                        _logger.LogInformation("⏰ Caché expirado para {Path} (edad: {Age:F1}min), recargando desde Excel", 
                            Path.GetFileName(fullPath), cacheAge.TotalMinutes);
                    }
                }
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Returning default system configuration.", fullPath);
                    var defaultConfig = new SystemConfiguration();
                    
                    // ✅ CACHEAR CONFIG POR DEFECTO TAMBIÉN
                    lock (_cacheLock)
                    {
                        _systemConfigCache[cacheKey] = (defaultConfig, DateTime.Now);
                    }
                    
                    return defaultConfig;
                }
                
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    // Buscar hoja "System Config" (varios nombres posibles)
                    var sheet = package.Workbook.Worksheets["System Config"]
                             ?? package.Workbook.Worksheets["SystemConfig"]
                             ?? package.Workbook.Worksheets["Config"]
                             ?? package.Workbook.Worksheets["Settings"];
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("⚠️ No se encontró hoja 'System Config' en Excel. Usando configuración por defecto.");
                        
                        // En desarrollo, activar funcionalidades para testing
                        if (_environment.IsDevelopment())
                        {
                            _logger.LogInformation("🔧 [DEV] Activando SemiautomaticEnabled para testing");
                            return new SystemConfiguration { SemiautomaticEnabled = true };
                        }
                        
                        return new SystemConfiguration();
                    }

                    _logger.LogInformation("📊 Leyendo configuración del sistema desde hoja: '{SheetName}'", sheet.Name);

                    var config = new SystemConfiguration();

                    // Leer configuración en formato Clave-Valor
                    // Columna A = Nombre del parámetro
                    // Columna B = Valor
                    // Formato esperado:
                    // Row 1: Encabezados (Parameter | Value)
                    // Row 2+: Datos

                    int row = 2; // Empezar desde fila 2 (1 es encabezado)
                    while (row < 1000)
                    {
                        var paramName = sheet.Cells[$"A{row}"].Text.Trim();
                        var paramValue = sheet.Cells[$"B{row}"].Text.Trim();

                        if (string.IsNullOrWhiteSpace(paramName))
                        {
                            row++;
                            continue; // Fila vacía
                        }

                        // Mapear parámetros a propiedades de SystemConfiguration
                        switch (paramName.ToLowerInvariant())
                        {
                            // SERVICIOS
                            case "enableplcpolling":
                            case "enable_plc_polling":
                                config.EnablePlcPolling = ParseBool(paramValue, true);
                                break;
                            
                            case "plcpollinginterval":
                            case "plc_polling_interval":
                                config.PlcPollingInterval = ParseInt(paramValue, 1000);
                                break;
                            
                            case "enablesignalr":
                            case "enable_signalr":
                                config.EnableSignalR = ParseBool(paramValue, true);
                                break;
                            
                            case "enableverboselogging":
                            case "enable_verbose_logging":
                                config.EnableVerboseLogging = ParseBool(paramValue, false);
                                break;

                            // TWINCAT/PLC
                            case "usesimulatedplc":
                            case "use_simulated_plc":
                                config.UseSimulatedPlc = ParseBool(paramValue, true);
                                break;
                            
                            case "plcamsnetid":
                            case "plc_ams_net_id":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.PlcAmsNetId = paramValue;
                                break;
                            
                            case "plcadsport":
                            case "plc_ads_port":
                                config.PlcAdsPort = ParseInt(paramValue, 851);
                                break;
                            
                            case "currentscreenplcvariable":
                            case "current_screen_plc_variable":
                            case "hmicurrentscreenvariable":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.CurrentScreenPlcVariable = paramValue;
                                break;

                            // BASE DE DATOS SQLite
                            case "enabledatabase":
                            case "enable_database":
                                config.EnableDatabase = ParseBool(paramValue, true); // Default true para SQLite
                                break;
                            
                            case "databaseconnectionstring":
                            case "database_connection_string":
                            case "databasepath":
                            case "database_path":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.DatabaseConnectionString = paramValue;
                                break;

                            // API/WEB
                            case "apiport":
                            case "api_port":
                                config.ApiPort = ParseInt(paramValue, 5000);
                                break;
                            
                            case "enablecors":
                            case "enable_cors":
                                config.EnableCors = ParseBool(paramValue, true);
                                break;
                            
                            case "corsorigins":
                            case "cors_origins":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.CorsOrigins = paramValue;
                                break;

                            // EXCEL/ARCHIVOS
                            case "excelconfigfilename":
                            case "excel_config_file_name":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.ExcelConfigFileName = paramValue;
                                break;
                            
                            case "configfolder":
                            case "config_folder":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.ConfigFolder = paramValue;
                                break;
                            
                            case "modelsfolder":
                            case "models_folder":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.ModelsFolder = paramValue;
                                break;

                            // CACHE/PERFORMANCE
                            case "configcacheseconds":
                            case "config_cache_seconds":
                                config.ConfigCacheSeconds = ParseInt(paramValue, 300);
                                break;
                            
                            case "maxsignalrconnections":
                            case "max_signalr_connections":
                                config.MaxSignalRConnections = ParseInt(paramValue, 100);
                                break;

                            // 🔐 GIT REPOSITORIES (Cybersecurity)
                            case "gitrepobackend":
                            case "git_repo_backend":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.GitRepoBackend = paramValue;
                                break;
                            
                            case "gitrepofrontend":
                            case "git_repo_frontend":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.GitRepoFrontend = paramValue;
                                break;
                            
                            case "gitrepotwincatplc":
                            case "git_repo_twincat_plc":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.GitRepoTwinCatPlc = paramValue;
                                break;

                            // 🔐 MODO DE ENTORNO (EU CRA Compliance)
                            case "environmentmode":
                            case "environment_mode":
                                if (!string.IsNullOrWhiteSpace(paramValue))
                                    config.EnvironmentMode = paramValue.ToLower();
                                break;

                            // 🛡️ VULNERABILITY SCANNER (EU CRA Compliance)
                            case "vulnscan_apiurl":
                            case "vulnscanapiurl":
                                config.VulnScanApiUrl = paramValue ?? "";
                                break;
                            case "vulnscan_apitype":
                            case "vulnscanapitype":
                                config.VulnScanApiType = paramValue ?? "OSV";
                                break;
                            case "vulnscan_autoscanintervalhours":
                            case "vulnscanautoscanintervalhours":
                            case "vulnscan_intervalhours":
                            case "vulnscanintervalhours":
                                if (int.TryParse(paramValue, out var vulnInterval))
                                    config.VulnScanIntervalHours = vulnInterval;
                                break;
                            case "vulnscan_alertoncritical":
                            case "vulnscanalertoncritical":
                                config.VulnScanAlertOnCritical = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "vulnscan_apikey":
                            case "vulnscanapikey":
                                config.VulnScanApiKey = paramValue ?? "";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 📤 VULNERABILITY REPORT - EU CRA Art. 14
                            // ═══════════════════════════════════════════════════════════════
                            case "vulnreportenabled":
                            case "vulnreport_enabled":
                                config.VulnReportEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "vulnreportapiurl":
                            case "vulnreport_apiurl":
                            case "vulnreport_api_url":
                                config.VulnReportApiUrl = paramValue ?? "";
                                break;
                            case "vulnreportapitype":
                            case "vulnreport_apitype":
                            case "vulnreport_api_type":
                                config.VulnReportApiType = paramValue ?? "SOC_SIEM";
                                break;
                            case "vulnreportautosendoncritical":
                            case "vulnreport_autosendoncritical":
                            case "vulnreport_autosend_on_critical":
                                config.VulnReportAutoSendOnCritical = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 💻 IPC HARDWARE INFO - EU CRA Compliance
                            // ═══════════════════════════════════════════════════════════════
                            case "ipcinfoenabled":
                            case "ipcinfo_enabled":
                                config.IpcInfoEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "ipcinfoquickpollseconds":
                            case "ipcinfo_quickpollseconds":
                            case "ipcinfo_quickpoll_seconds":
                                if (int.TryParse(paramValue, out int quickPoll))
                                    config.IpcInfoQuickPollSeconds = quickPoll;
                                break;
                            case "ipcinfofullpollminutes":
                            case "ipcinfo_fullpollminutes":
                            case "ipcinfo_fullpoll_minutes":
                                if (int.TryParse(paramValue, out int fullPoll))
                                    config.IpcInfoFullPollMinutes = fullPoll;
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 📋 AUDIT LOG - EU CRA Compliance (CADRA/Alstom)
                            // ═══════════════════════════════════════════════════════════════
                            case "auditlogenabled":
                            case "auditlog_enabled":
                                config.AuditLogEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "auditlogretentiondays":
                            case "auditlog_retentiondays":
                            case "auditlog_retention_days":
                                if (int.TryParse(paramValue, out int retDays))
                                    config.AuditLogRetentionDays = Math.Max(7, retDays); // Mínimo 7 días
                                break;
                            case "auditlogexternalurl":
                            case "auditlog_externalurl":
                            case "auditlog_external_url":
                                config.AuditLogExternalUrl = paramValue ?? "";
                                break;
                            case "auditlogexternalenabled":
                            case "auditlog_externalenabled":
                            case "auditlog_external_enabled":
                                config.AuditLogExternalEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "auditlogsignatureenabled":
                            case "auditlog_signatureenabled":
                            case "auditlog_signature_enabled":
                                config.AuditLogSignatureEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "auditlogmaxentriesperfile":
                            case "auditlog_maxentriesperfile":
                            case "auditlog_max_entries_per_file":
                                if (int.TryParse(paramValue, out int maxEntries))
                                    config.AuditLogMaxEntriesPerFile = Math.Max(100, maxEntries);
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🔐 AUTHENTICATION - EU CRA Compliance (CADRA/Alstom Phase 2)
                            // ═══════════════════════════════════════════════════════════════
                            case "authmode":
                            case "auth_mode":
                                config.AuthMode = paramValue ?? "Local";
                                break;
                            case "authenableactivedirectory":
                            case "auth_enableactivedirectory":
                            case "auth_enable_active_directory":
                                config.AuthEnableActiveDirectory = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authadserver":
                            case "auth_adserver":
                            case "auth_ad_server":
                                config.AuthADServer = paramValue ?? "";
                                break;
                            case "authaddomain":
                            case "auth_addomain":
                            case "auth_ad_domain":
                                config.AuthADDomain = paramValue ?? "";
                                break;
                            case "authadbasedn":
                            case "auth_adbasedn":
                            case "auth_ad_basedn":
                                config.AuthADBaseDN = paramValue ?? "";
                                break;
                            case "authadtimeoutseconds":
                            case "auth_adtimeoutseconds":
                            case "auth_ad_timeout_seconds":
                                if (int.TryParse(paramValue, out int adTimeout))
                                    config.AuthADTimeoutSeconds = Math.Max(5, adTimeout);
                                break;
                            case "authfallbacktolocal":
                            case "auth_fallbacktolocal":
                            case "auth_fallback_to_local":
                                config.AuthFallbackToLocal = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authdatabasepath":
                            case "auth_databasepath":
                            case "auth_database_path":
                                config.AuthDatabasePath = paramValue ?? "Data/Aquafrisch.db";
                                break;
                            case "authpasswordminlength":
                            case "auth_passwordminlength":
                            case "auth_password_min_length":
                                if (int.TryParse(paramValue, out int minLen))
                                    config.AuthPasswordMinLength = Math.Max(4, minLen); // Mínimo 4 para desarrollo
                                break;
                            case "authrequireuppercase":
                            case "auth_requireuppercase":
                            case "auth_require_uppercase":
                                config.AuthRequireUppercase = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authrequirelowercase":
                            case "auth_requirelowercase":
                            case "auth_require_lowercase":
                                config.AuthRequireLowercase = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authrequirenumbers":
                            case "auth_requirenumbers":
                            case "auth_require_numbers":
                                config.AuthRequireNumbers = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authrequirespecialchars":
                            case "auth_requirespecialchars":
                            case "auth_require_special_chars":
                                config.AuthRequireSpecialChars = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authmaxloginattempts":
                            case "auth_maxloginattempts":
                            case "auth_max_login_attempts":
                                if (int.TryParse(paramValue, out int maxAttempts))
                                    config.AuthMaxLoginAttempts = Math.Max(3, maxAttempts);
                                break;
                            case "authlockoutminutes":
                            case "auth_lockoutminutes":
                            case "auth_lockout_minutes":
                                if (int.TryParse(paramValue, out int lockoutMin))
                                    config.AuthLockoutMinutes = Math.Max(5, lockoutMin);
                                break;
                            case "authsessiontimeoutminutes":
                            case "auth_sessiontimeoutminutes":
                            case "auth_session_timeout_minutes":
                                if (int.TryParse(paramValue, out int sessionTimeout))
                                    config.AuthSessionTimeoutMinutes = Math.Max(5, sessionTimeout);
                                break;
                            case "authforcepasswordchangeonfirstlogin":
                            case "auth_forcepasswordchangeonfirstlogin":
                            case "auth_force_password_change_on_first_login":
                                config.AuthForcePasswordChangeOnFirstLogin = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authshowloginbanner":
                            case "auth_showloginbanner":
                            case "auth_show_login_banner":
                                config.AuthShowLoginBanner = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authjwtsecretkey":
                            case "auth_jwtsecretkey":
                            case "auth_jwt_secret_key":
                                config.AuthJwtSecretKey = paramValue ?? "";
                                break;
                            case "authjwtissuer":
                            case "auth_jwtissuer":
                            case "auth_jwt_issuer":
                                config.AuthJwtIssuer = paramValue ?? "AquafrischSupervisor";
                                break;
                            case "authjwtaudience":
                            case "auth_jwtaudience":
                            case "auth_jwt_audience":
                                config.AuthJwtAudience = paramValue ?? "AquafrischClients";
                                break;

                            // ===== 🔐 SESSION MANAGEMENT (Phase 3) =====
                            case "authmaxconcurrentsessions":
                            case "auth_maxconcurrentsessions":
                            case "auth_max_concurrent_sessions":
                                config.AuthMaxConcurrentSessions = int.TryParse(paramValue, out var maxSessions) ? maxSessions : 2;
                                break;
                            case "authsinglesessionroles":
                            case "auth_singlesessionroles":
                            case "auth_single_session_roles":
                                config.AuthSingleSessionRoles = paramValue ?? "Operator";
                                break;
                            case "authinactivitytimeoutminutes":
                            case "auth_inactivitytimeoutminutes":
                            case "auth_inactivity_timeout_minutes":
                                config.AuthInactivityTimeoutMinutes = int.TryParse(paramValue, out var inactivity) ? inactivity : 15;
                                break;
                            case "authtracklastactivity":
                            case "auth_tracklastactivity":
                            case "auth_track_last_activity":
                                config.AuthTrackLastActivity = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authsinglesessionbehavior":
                            case "auth_singlesessionbehavior":
                            case "auth_single_session_behavior":
                                config.AuthSingleSessionBehavior = paramValue?.ToLower() == "force" ? "force" : "reject";
                                break;

                            // ===== 🔐 RBAC - Role Based Access Control (Phase 4) =====
                            case "authdefaultrole":
                            case "auth_defaultrole":
                            case "auth_default_role":
                                config.AuthDefaultRole = paramValue ?? "Viewer";
                                break;
                            case "authenableguestrole":
                            case "auth_enableguestrole":
                            case "auth_enable_guest_role":
                                config.AuthEnableGuestRole = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authguestpermissions":
                            case "auth_guestpermissions":
                            case "auth_guest_permissions":
                                config.AuthGuestPermissions = paramValue ?? "plc:read";
                                break;
                            case "authrequireuserapproval":
                            case "auth_requireuserapproval":
                            case "auth_require_user_approval":
                                config.AuthRequireUserApproval = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authnotifyadminonnewuser":
                            case "auth_notifyadminonnewuser":
                            case "auth_notify_admin_on_new_user":
                                config.AuthNotifyAdminOnNewUser = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "authoperatorextrapermissions":
                            case "auth_operatorextrapermissions":
                            case "auth_operator_extra_permissions":
                                config.AuthOperatorExtraPermissions = paramValue ?? "";
                                break;
                            case "authmaintenanceextrapermissions":
                            case "auth_maintenanceextrapermissions":
                            case "auth_maintenance_extra_permissions":
                                config.AuthMaintenanceExtraPermissions = paramValue ?? "";
                                break;
                            case "authrestrictedpermissions":
                            case "auth_restrictedpermissions":
                            case "auth_restricted_permissions":
                                config.AuthRestrictedPermissions = paramValue ?? "backup:restore,security:update";
                                break;
                            case "authenablerolehierarchy":
                            case "auth_enablerolehierarchy":
                            case "auth_enable_role_hierarchy":
                                config.AuthEnableRoleHierarchy = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🖥️ KIOSK MODE - Herramientas del Sistema (IPCs industriales)
                            // ═══════════════════════════════════════════════════════════════
                            case "kioskmodeenabled":
                            case "kiosk_mode_enabled":
                                config.KioskModeEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "installationid":
                            case "installation_id":
                                config.InstallationId = paramValue ?? "AQF-DEFAULT-001";
                                break;
                            case "allowedsystemtoolsroles":
                            case "allowed_system_tools_roles":
                                config.AllowedSystemToolsRoles = paramValue ?? "SuperAdmin,Administrator,Maintenance";
                                break;
                            case "windowslogoutenabled":
                            case "windows_logout_enabled":
                                config.WindowsLogoutEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "apprestartenabled":
                            case "app_restart_enabled":
                                config.AppRestartEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "networkdiagnosticenabled":
                            case "network_diagnostic_enabled":
                                config.NetworkDiagnosticEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "gatewayip":
                            case "gateway_ip":
                                config.GatewayIP = paramValue ?? "192.168.1.1";
                                break;
                            case "kioskbrowserpath":
                            case "kiosk_browser_path":
                                config.KioskBrowserPath = paramValue ?? "";
                                break;
                            case "kioskbrowserargs":
                            case "kiosk_browser_args":
                                config.KioskBrowserArgs = paramValue ?? "--kiosk http://localhost:3001";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🖥️ TEAMVIEWER - Soporte Remoto
                            // ═══════════════════════════════════════════════════════════════
                            case "teamviewerenabled":
                            case "teamviewer_enabled":
                                config.TeamViewerEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "teamviewerpath":
                            case "teamviewer_path":
                                config.TeamViewerPath = paramValue ?? "";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🔌 CUSTOM TOOLS - Software Adicional Configurable
                            // ═══════════════════════════════════════════════════════════════
                            // --- HERRAMIENTA 1 ---
                            case "customtool1enabled":
                            case "custom_tool_1_enabled":
                                config.CustomTool1Enabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "customtool1name":
                            case "custom_tool_1_name":
                                config.CustomTool1Name = paramValue ?? "";
                                break;
                            case "customtool1path":
                            case "custom_tool_1_path":
                                config.CustomTool1Path = paramValue ?? "";
                                break;
                            case "customtool1args":
                            case "custom_tool_1_args":
                                config.CustomTool1Args = paramValue ?? "";
                                break;
                            case "customtool1icon":
                            case "custom_tool_1_icon":
                                config.CustomTool1Icon = paramValue ?? "🔧";
                                break;

                            // --- HERRAMIENTA 2 ---
                            case "customtool2enabled":
                            case "custom_tool_2_enabled":
                                config.CustomTool2Enabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "customtool2name":
                            case "custom_tool_2_name":
                                config.CustomTool2Name = paramValue ?? "";
                                break;
                            case "customtool2path":
                            case "custom_tool_2_path":
                                config.CustomTool2Path = paramValue ?? "";
                                break;
                            case "customtool2args":
                            case "custom_tool_2_args":
                                config.CustomTool2Args = paramValue ?? "";
                                break;
                            case "customtool2icon":
                            case "custom_tool_2_icon":
                                config.CustomTool2Icon = paramValue ?? "⚙️";
                                break;

                            // --- HERRAMIENTA 3 ---
                            case "customtool3enabled":
                            case "custom_tool_3_enabled":
                                config.CustomTool3Enabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "customtool3name":
                            case "custom_tool_3_name":
                                config.CustomTool3Name = paramValue ?? "";
                                break;
                            case "customtool3path":
                            case "custom_tool_3_path":
                                config.CustomTool3Path = paramValue ?? "";
                                break;
                            case "customtool3args":
                            case "custom_tool_3_args":
                                config.CustomTool3Args = paramValue ?? "";
                                break;
                            case "customtool3icon":
                            case "custom_tool_3_icon":
                                config.CustomTool3Icon = paramValue ?? "🔌";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 📞 SOPORTE AQUAFRISCH - "Llamar a Aquafrisch"
                            // ═══════════════════════════════════════════════════════════════
                            case "supportunlockenabled":
                            case "support_unlock_enabled":
                                config.SupportUnlockEnabled = paramValue?.ToLower() == "true" || paramValue == "1";
                                break;
                            case "supportphonenumber":
                            case "support_phone_number":
                                config.SupportPhoneNumber = paramValue ?? "+34 900 123 456";
                                break;
                            case "supportemail":
                            case "support_email":
                                config.SupportEmail = paramValue ?? "soporte@aquafrisch.com";
                                break;
                            case "supportunlockdurationminutes":
                            case "support_unlock_duration_minutes":
                                if (int.TryParse(paramValue, out int unlockMin))
                                    config.SupportUnlockDurationMinutes = Math.Max(5, Math.Min(unlockMin, 120)); // 5-120 min
                                break;
                            case "supportendyear":
                            case "support_end_year":
                                if (int.TryParse(paramValue, out int supportYear))
                                    config.SupportEndYear = Math.Max(2025, Math.Min(supportYear, 2100)); // 2025-2100
                                break;
                            
                            // NOTA: SupportChallengeSecret NO se lee de Excel
                            // Está hardcodeado en SupportController.cs (igual que RecoveryCodeService)

                            // ═══════════════════════════════════════════════════════════════
                            // 🚿 WASH RECIPE - Tipos de Lavado
                            // ═══════════════════════════════════════════════════════════════
                            case "washrecipeenabled":
                            case "wash_recipe_enabled":
                                // Aceptar: true/false, 1/0, on/off, si/no
                                var washValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.WashRecipeEnabled = washValue == "true" || washValue == "1" || washValue == "on" || washValue == "si" || washValue == "yes";
                                _logger.LogDebug("🚿 WashRecipeEnabled raw value: '{RawValue}' -> {Parsed}", paramValue, config.WashRecipeEnabled);
                                break;
                            case "washrecipeautoloadvar":
                            case "wash_recipe_autoload_var":
                            case "washrecipeenabled_varautoload":
                                config.WashRecipeAutoLoadVar = paramValue ?? "";
                                break;
                            case "washrecipeautoloadvar2":
                            case "wash_recipe_autoload_var_2":
                            case "washrecipeenabled_varautoload_2":
                                config.WashRecipeAutoLoadVar2 = paramValue ?? "";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🚆 TRAIN RECIPE - Tipos de Tren
                            // ═══════════════════════════════════════════════════════════════
                            case "trainrecipeenabled":
                            case "train_recipe_enabled":
                                // Aceptar: true/false, 1/0, on/off, si/no
                                var trainValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.TrainRecipeEnabled = trainValue == "true" || trainValue == "1" || trainValue == "on" || trainValue == "si" || trainValue == "yes";
                                _logger.LogDebug("🚆 TrainRecipeEnabled raw value: '{RawValue}' -> {Parsed}", paramValue, config.TrainRecipeEnabled);
                                break;
                            case "trainrecipeautoloadvar":
                            case "train_recipe_autoload_var":
                            case "trainrecipeenabled_varautoload":
                                config.TrainRecipeAutoLoadVar = paramValue ?? "";
                                break;
                            case "trainrecipeautoloadvar2":
                            case "train_recipe_autoload_var_2":
                            case "trainrecipeenabled_varautoload_2":
                                config.TrainRecipeAutoLoadVar2 = paramValue ?? "";
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // ⚡ SEMIAUTOMATIC MODE - Modo Semiautomático
                            // ═══════════════════════════════════════════════════════════════
                            case "semiautomaticenabled":
                            case "semiautomatic_enabled":
                                var semiValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.SemiautomaticEnabled = semiValue == "true" || semiValue == "1" || semiValue == "on" || semiValue == "si" || semiValue == "yes";
                                _logger.LogDebug("⚡ SemiautomaticEnabled raw value: '{RawValue}' -> {Parsed}", paramValue, config.SemiautomaticEnabled);
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // ⚡ FAST CONFIGURATION - Configuración Rápida
                            // ═══════════════════════════════════════════════════════════════
                            case "fastconfigurationenabled":
                            case "fastconfiguration_enabled":
                            case "fast_configuration_enabled":
                                var fastConfigValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.FastConfigurationEnabled = fastConfigValue == "true" || fastConfigValue == "1" || fastConfigValue == "on" || fastConfigValue == "si" || fastConfigValue == "yes";
                                _logger.LogDebug("⚡ FastConfigurationEnabled raw value: '{RawValue}' -> {Parsed}", paramValue, config.FastConfigurationEnabled);
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🌐 ETHERCAT TOPOLOGY - Diagnóstico de Red Industrial
                            // ═══════════════════════════════════════════════════════════════
                            case "enableethercattopology":
                            case "enable_ethercat_topology":
                            case "ethercattopologyenabled":
                            case "ethercat_topology_enabled":
                                var ecatValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.EnableEtherCATTopology = ecatValue == "true" || ecatValue == "1" || ecatValue == "on" || ecatValue == "si" || ecatValue == "yes";
                                _logger.LogDebug("🌐 EnableEtherCATTopology raw value: '{RawValue}' -> {Parsed}", paramValue, config.EnableEtherCATTopology);
                                break;
                            
                            case "ethercatmasternetid":
                            case "ethercat_master_netid":
                            case "ethercat_master_net_id":
                            case "ecatmasternetid":
                                config.EtherCATMasterNetId = paramValue?.Trim() ?? "";
                                _logger.LogDebug("🌐 EtherCATMasterNetId raw value: '{RawValue}'", paramValue);
                                break;
                            
                            case "ethercatmasterdeviceid":
                            case "ethercat_master_deviceid":
                            case "ethercat_master_device_id":
                            case "ecatmasterdeviceid":
                                if (int.TryParse(paramValue?.Trim(), out int ecatDeviceId))
                                    config.EtherCATMasterDeviceId = ecatDeviceId;
                                _logger.LogDebug("🌐 EtherCATMasterDeviceId raw value: '{RawValue}' -> {Parsed}", paramValue, config.EtherCATMasterDeviceId);
                                break;
                            
                            case "esifilespath":
                            case "esi_files_path":
                            case "ethercatesipath":
                            case "ethercat_esi_path":
                                config.ESIFilesPath = paramValue?.Trim() ?? "";
                                _logger.LogDebug("🌐 ESIFilesPath raw value: '{RawValue}'", paramValue);
                                break;
                            
                            case "useethercatesifiles":
                            case "use_ethercat_esi_files":
                            case "useethercat_esifiles":
                            case "use_esi_files":
                                var esiValue = paramValue?.ToLower()?.Trim() ?? "";
                                config.UseEtherCATESIFiles = esiValue == "true" || esiValue == "1" || esiValue == "on" || esiValue == "si" || esiValue == "yes";
                                _logger.LogDebug("🌐 UseEtherCATESIFiles raw value: '{RawValue}' -> {Parsed}", paramValue, config.UseEtherCATESIFiles);
                                break;
                            
                            case "ethercattopologyreadintervalms":
                            case "ethercat_topology_read_interval_ms":
                            case "ecattopologyinterval":
                            case "ethercat_read_interval":
                                if (int.TryParse(paramValue?.Trim(), out int ecatInterval))
                                    config.EtherCATTopologyReadIntervalMs = Math.Max(500, ecatInterval); // Mínimo 500ms
                                _logger.LogDebug("🌐 EtherCATTopologyReadIntervalMs raw value: '{RawValue}' -> {Parsed}", paramValue, config.EtherCATTopologyReadIntervalMs);
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 📷 3D SCENE / CAMERA - Configuración de escena 3D
                            // ═══════════════════════════════════════════════════════════════
                            case "camerazoomfactor":
                            case "camera_zoom_factor":
                            case "camera_zoomfactor":
                                if (double.TryParse(paramValue?.Trim()?.Replace(",", "."), 
                                    System.Globalization.NumberStyles.Float, 
                                    System.Globalization.CultureInfo.InvariantCulture, 
                                    out double zoomFactor))
                                {
                                    config.CameraZoomFactor = zoomFactor;
                                }
                                _logger.LogDebug("📷 CameraZoomFactor raw value: '{RawValue}' -> {Parsed}", paramValue, config.CameraZoomFactor);
                                break;

                            // ═══════════════════════════════════════════════════════════════
                            // 🌐 INTERNATIONALIZATION (i18n) - Sistema de traducciones
                            // ═══════════════════════════════════════════════════════════════
                            case "defaultlanguage":
                            case "default_language":
                            case "idioma":
                            case "language":
                                config.DefaultLanguage = paramValue?.Trim()?.ToUpperInvariant() ?? "SPA";
                                _logger.LogDebug("🌐 DefaultLanguage raw value: '{RawValue}' -> {Parsed}", paramValue, config.DefaultLanguage);
                                break;

                            case "exposelabelids":
                            case "expose_label_ids":
                            case "debug_labels":
                            case "show_label_ids":
                                var exposeLabelValue = paramValue?.Trim()?.ToLowerInvariant() ?? "false";
                                config.ExposeLabelIds = exposeLabelValue == "true" || exposeLabelValue == "1" || exposeLabelValue == "on" || exposeLabelValue == "si" || exposeLabelValue == "yes";
                                _logger.LogDebug("🌐 ExposeLabelIds raw value: '{RawValue}' -> {Parsed}", paramValue, config.ExposeLabelIds);
                                break;

                            default:
                                _logger.LogDebug("⚠️ Parámetro desconocido en System Config: {Param}", paramName);
                                break;
                        }

                        row++;
                    }

                    _logger.LogInformation("✅ Configuración del sistema cargada desde Excel:");
                    _logger.LogInformation("  - PlcPolling: {Enabled} ({Interval}ms)", config.EnablePlcPolling, config.PlcPollingInterval);
                    _logger.LogInformation("  - SignalR: {Enabled}", config.EnableSignalR);
                    _logger.LogInformation("  - Simulated PLC: {Enabled}", config.UseSimulatedPlc);
                    _logger.LogInformation("  - Database: {Enabled}", config.EnableDatabase);
                    _logger.LogInformation("  - 🔐 EnvironmentMode: {Mode}", config.EnvironmentMode);
                    _logger.LogInformation("  - 💻 IpcInfo: {Enabled} (Quick: {Quick}s, Full: {Full}m)", 
                        config.IpcInfoEnabled, config.IpcInfoQuickPollSeconds, config.IpcInfoFullPollMinutes);
                    _logger.LogInformation("  - 🚿 WashRecipe: {Enabled} (AutoLoad: {Var1}, AutoLoad2: {Var2})", 
                        config.WashRecipeEnabled, 
                        string.IsNullOrEmpty(config.WashRecipeAutoLoadVar) ? "N/A" : config.WashRecipeAutoLoadVar,
                        string.IsNullOrEmpty(config.WashRecipeAutoLoadVar2) ? "N/A" : config.WashRecipeAutoLoadVar2);
                    _logger.LogInformation("  - 🚆 TrainRecipe: {Enabled} (AutoLoad: {Var1}, AutoLoad2: {Var2})", 
                        config.TrainRecipeEnabled, 
                        string.IsNullOrEmpty(config.TrainRecipeAutoLoadVar) ? "N/A" : config.TrainRecipeAutoLoadVar,
                        string.IsNullOrEmpty(config.TrainRecipeAutoLoadVar2) ? "N/A" : config.TrainRecipeAutoLoadVar2);
                    _logger.LogInformation("  - ⚡ Semiautomatic: {Enabled}", config.SemiautomaticEnabled);
                    _logger.LogInformation("  - ⚡ FastConfiguration: {Enabled}", config.FastConfigurationEnabled);
                    _logger.LogInformation("  - 📷 CameraZoomFactor: {Factor}", config.CameraZoomFactor);
                    _logger.LogInformation("  - 🌐 i18n: DefaultLanguage={Lang}, ExposeLabelIds={Expose}", 
                        config.DefaultLanguage, config.ExposeLabelIds);

                    stopwatch.Stop();
                    _metricsService.RecordExcelLoadTime(stopwatch.Elapsed.TotalMilliseconds);
                    _logger.LogDebug("⏱️ System configuration loaded in {Time}ms", stopwatch.Elapsed.TotalMilliseconds);
                    
                    // ✅ GUARDAR EN CACHÉ POR ARCHIVO
                    lock (_cacheLock)
                    {
                        _systemConfigCache[cacheKey] = (config, DateTime.Now);
                    }
                    _logger.LogDebug("💾 Configuración guardada en caché para {Path} (válida por {Minutes} minutos)", 
                        Path.GetFileName(fullPath), _cacheExpiration.TotalMinutes);
                    
                    return config;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading system configuration from Excel: {Message}", ex.Message);
                
                var errorConfig = new SystemConfiguration();
                
                // ✅ CACHEAR CONFIG POR DEFECTO TAMBIÉN EN ERROR
                lock (_cacheLock)
                {
                    _systemConfigCache[cacheKey] = (errorConfig, DateTime.Now);
                }
                
                return errorConfig;
            }
        }

        // Métodos helper para parsing
        private bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            
            value = value.ToLowerInvariant();
            if (value == "true" || value == "1" || value == "yes" || value == "si" || value == "sí" || value == "enabled")
                return true;
            if (value == "false" || value == "0" || value == "no" || value == "disabled")
                return false;
            
            return defaultValue;
        }

        private int ParseInt(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Carga modelos 3D con children desde Excel (hoja "3D_Models")
        /// </summary>
        public async Task<List<Model3DConfig>> Load3DModelsAsync(string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}", fullPath);
                    return new List<Model3DConfig>();
                }
                
                _logger.LogInformation("📂 Loading 3D models from Excel: {Path}", fullPath);
                
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    var models = await LoadModels3DFromSheetAsync(package);
                    _logger.LogInformation("✅ Loaded {Count} 3D models (with {ChildCount} total children)", 
                        models.Count, models.Sum(m => m.Children.Count));
                    return models;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading 3D models from Excel");
                return new List<Model3DConfig>();
            }
        }

        #region Alarm System Loading
        
        /// <summary>
        /// Carga la configuración de alarmas desde la hoja "Alarms" del Excel.
        /// Estructura esperada (ISO 639-2 de 3 letras):
        /// - Columna A: Index (1-based, corresponde al array PLC)
        /// - Columna B: Alarm_SPA (texto español para Alarm[Index])
        /// - Columna C: Notification_SPA (texto español para Notification[Index])
        /// - Columna D: Info_SPA (texto español para Info[Index])
        /// - Columna E: Alarm_ENG (texto inglés para Alarm[Index])
        /// - Columna F: Notification_ENG (texto inglés para Notification[Index])
        /// - Columna G: Info_ENG (texto inglés para Info[Index])
        /// - Columnas H-M: Más idiomas (ITA, FRA, RUS, etc.) si existen
        /// Variables PLC generadas: MAIN.fbMachine.st_alarmPc.{Type}[Index]
        /// </summary>
        public async Task<AlarmConfiguration> LoadAlarmsAsync(string filePath)
        {
            var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
            var config = new AlarmConfiguration
            {
                SourceFile = fullPath,
                LoadedAt = DateTime.Now
            };
            
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("🔔 Excel file not found: {Path}. Returning empty alarm configuration.", fullPath);
                    return config;
                }
                
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var package = new ExcelPackage(stream);
                
                // 📋 Listar todas las hojas disponibles para debugging
                var availableSheets = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
                _logger.LogInformation("🔔 Hojas disponibles en Excel: [{Sheets}]", string.Join(", ", availableSheets));
                
                // Buscar hoja "Alarms" (varios nombres posibles)
                var sheet = package.Workbook.Worksheets["Alarms"]
                         ?? package.Workbook.Worksheets["ALARMS"]
                         ?? package.Workbook.Worksheets["Alarmas"]
                         ?? package.Workbook.Worksheets["2) Alarms"];
                
                if (sheet == null)
                {
                    _logger.LogWarning("🔔 No se encontró hoja 'Alarms' en Excel {Path}. Hojas disponibles: [{Sheets}]", fullPath, string.Join(", ", availableSheets));
                    return config;
                }
                
                _logger.LogInformation("🔔 Cargando alarmas desde hoja: '{SheetName}'", sheet.Name);
                
                // Detectar idiomas disponibles desde encabezados (fila 1)
                var columnMapping = DetectAlarmColumnMapping(sheet);
                config.AvailableLanguages = columnMapping.Keys.ToList();
                _logger.LogInformation("🔔 Idiomas detectados: {Languages}", string.Join(", ", config.AvailableLanguages));
                
                // Leer definiciones de alarmas (fila 2 en adelante)
                int row = 2;
                int emptyRows = 0;
                const int maxEmptyRows = 10;
                const int maxRow = 500; // Límite de seguridad
                
                _logger.LogInformation("🔔 Iniciando lectura de alarmas desde fila 2...");
                
                while (row <= maxRow && emptyRows < maxEmptyRows)
                {
                    var indexCell = sheet.Cells[$"A{row}"].Text.Trim();
                    
                    // Debug: mostrar las primeras 5 filas
                    if (row <= 6)
                    {
                        _logger.LogInformation("🔔 Fila {Row}: A='{IndexCell}', B='{ColB}', C='{ColC}', D='{ColD}'", 
                            row, indexCell, 
                            sheet.Cells[$"B{row}"].Text.Trim(),
                            sheet.Cells[$"C{row}"].Text.Trim(),
                            sheet.Cells[$"D{row}"].Text.Trim());
                    }
                    
                    if (string.IsNullOrWhiteSpace(indexCell))
                    {
                        emptyRows++;
                        row++;
                        continue;
                    }
                    
                    emptyRows = 0; // Reset contador de filas vacías
                    
                    if (!int.TryParse(indexCell, out int index))
                    {
                        _logger.LogWarning("🔔 Fila {Row}: índice inválido '{Index}', saltando", row, indexCell);
                        row++;
                        continue;
                    }
                    
                    // Leer textos de Alarm para cada idioma
                    var alarmTexts = ReadAlarmTextsForType(sheet, row, "Alarm", columnMapping);
                    if (alarmTexts.Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                    {
                        config.Alarms.Add(new AlarmDefinition
                        {
                            Index = index,
                            Type = AlarmType.Alarm,
                            PlcVariable = $"MAIN.fbMachine.st_alarmPc[{index}].Alarm",
                            Texts = alarmTexts
                        });
                    }
                    
                    // Leer textos de Notification para cada idioma
                    var notificationTexts = ReadAlarmTextsForType(sheet, row, "Notification", columnMapping);
                    if (notificationTexts.Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                    {
                        config.Notifications.Add(new AlarmDefinition
                        {
                            Index = index,
                            Type = AlarmType.Notification,
                            PlcVariable = $"MAIN.fbMachine.st_alarmPc[{index}].Notification",
                            Texts = notificationTexts
                        });
                    }
                    
                    // Leer textos de Info para cada idioma
                    var infoTexts = ReadAlarmTextsForType(sheet, row, "Info", columnMapping);
                    if (infoTexts.Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                    {
                        config.Infos.Add(new AlarmDefinition
                        {
                            Index = index,
                            Type = AlarmType.Info,
                            PlcVariable = $"MAIN.fbMachine.st_alarmPc[{index}].Info",
                            Texts = infoTexts
                        });
                    }
                    
                    row++;
                }
                
                _logger.LogInformation("🔔 Alarmas cargadas: {Alarms} Alarm, {Notifications} Notification, {Infos} Info (Total: {Total})",
                    config.Alarms.Count, config.Notifications.Count, config.Infos.Count, config.TotalCount);
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error loading alarms from Excel: {Path}", fullPath);
                return config;
            }
        }
        
        /// <summary>
        /// Detecta el mapeo de columnas por idioma desde los encabezados (ISO 639-2: SPA, ENG, ITA, FRA, etc.)
        /// Estructura: Alarm_SPA, Notification_SPA, Info_SPA, Alarm_ENG, Notification_ENG, Info_ENG, ...
        /// </summary>
        private Dictionary<string, AlarmColumnSet> DetectAlarmColumnMapping(ExcelWorksheet sheet)
        {
            var mapping = new Dictionary<string, AlarmColumnSet>();
            
            // Escanear encabezados desde columna B hasta que estén vacíos
            int col = 2; // B = 2
            const int maxCol = 50; // Límite de seguridad
            
            while (col <= maxCol)
            {
                var header = sheet.Cells[1, col].Text.Trim();
                
                if (string.IsNullOrWhiteSpace(header))
                {
                    col++;
                    continue;
                }
                
                // Quitar paréntesis si existen: (Alarm_SPA) → Alarm_SPA
                header = header.TrimStart('(').TrimEnd(')');
                
                // Parsear encabezado (formato: Type_LANG, ej: Alarm_SPA, Notification_ENG)
                var parts = header.Split('_');
                if (parts.Length >= 2)
                {
                    var alarmType = parts[0]; // Alarm, Notification, Info
                    var langCode = parts[^1].ToUpperInvariant(); // SPA, ENG, ITA, etc.
                    
                    // Crear entrada para el idioma si no existe
                    if (!mapping.ContainsKey(langCode))
                    {
                        mapping[langCode] = new AlarmColumnSet { Language = langCode };
                    }
                    
                    // Asignar columna según tipo
                    switch (alarmType.ToLower())
                    {
                        case "alarm":
                            mapping[langCode].AlarmColumn = col;
                            break;
                        case "notification":
                            mapping[langCode].NotificationColumn = col;
                            break;
                        case "info":
                            mapping[langCode].InfoColumn = col;
                            break;
                    }
                }
                
                col++;
            }
            
            // Si no se detectaron idiomas, usar valores por defecto (B, C, D = SPA; E, F, G = ENG)
            if (mapping.Count == 0)
            {
                _logger.LogWarning("🔔 No se detectaron encabezados de idioma, usando valores por defecto");
                mapping["SPA"] = new AlarmColumnSet { Language = "SPA", AlarmColumn = 2, NotificationColumn = 3, InfoColumn = 4 };
                mapping["ENG"] = new AlarmColumnSet { Language = "ENG", AlarmColumn = 5, NotificationColumn = 6, InfoColumn = 7 };
            }
            
            // Log de mapeo detectado
            foreach (var kvp in mapping)
            {
                _logger.LogDebug("🔔 Idioma {Lang}: Alarm=Col{A}, Notification=Col{N}, Info=Col{I}",
                    kvp.Key, kvp.Value.AlarmColumn, kvp.Value.NotificationColumn, kvp.Value.InfoColumn);
            }
            
            return mapping;
        }
        
        /// <summary>
        /// Estructura para almacenar columnas por idioma
        /// </summary>
        private class AlarmColumnSet
        {
            public string Language { get; set; } = string.Empty;
            public int AlarmColumn { get; set; }
            public int NotificationColumn { get; set; }
            public int InfoColumn { get; set; }
        }
        
        /// <summary>
        /// Lee los textos multilenguaje de un tipo de alarma específico
        /// </summary>
        private Dictionary<string, string> ReadAlarmTextsForType(
            ExcelWorksheet sheet, 
            int row, 
            string alarmType, 
            Dictionary<string, AlarmColumnSet> columnMapping)
        {
            var texts = new Dictionary<string, string>();
            
            foreach (var kvp in columnMapping)
            {
                var langCode = kvp.Key;
                var columns = kvp.Value;
                
                int col = alarmType.ToLower() switch
                {
                    "alarm" => columns.AlarmColumn,
                    "notification" => columns.NotificationColumn,
                    "info" => columns.InfoColumn,
                    _ => 0
                };
                
                if (col > 0)
                {
                    var text = sheet.Cells[row, col].Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        texts[langCode] = text;
                    }
                }
            }
            
            // Fallback: si falta un idioma principal, copiar del otro
            if (!texts.ContainsKey("ENG") && texts.ContainsKey("SPA"))
            {
                texts["ENG"] = texts["SPA"];
            }
            if (!texts.ContainsKey("SPA") && texts.ContainsKey("ENG"))
            {
                texts["SPA"] = texts["ENG"];
            }
            
            return texts;
        }
        
        #endregion
        
        #region Settings Page (Machine Configuration Parameters)
        
        /// <summary>
        /// Carga la configuración de parámetros de máquina desde la hoja "setting page" del Excel.
        /// Lee parámetros Bool (B, C, D), Int (F, G, H) y LongReal (J, K, L) a partir de la fila 2.
        /// </summary>
        /// <param name="filePath">Ruta al archivo Excel</param>
        /// <returns>Configuración de settings con los tres tipos de parámetros</returns>
        public async Task<SettingsPageConfiguration> LoadSettingsPageAsync(string filePath)
        {
            var config = new SettingsPageConfiguration();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("⚙️ Excel file not found for settings page: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("⚙️ Loading machine settings from Excel: {Path}", fullPath);
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    // Buscar hoja "setting page" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("setting page", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("⚙️ Sheet 'setting page' not found in Excel file");
                        return config;
                    }
                    
                    // === Leer títulos de secciones desde las celdas A2, E2, L2 ===
                    var boolTitle = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(boolTitle))
                    {
                        config.BoolSectionTitle = boolTitle;
                        _logger.LogDebug("⚙️ Bool section title from A2: {Title}", boolTitle);
                    }
                    
                    var intTitle = sheet.Cells["E2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(intTitle))
                    {
                        config.IntSectionTitle = intTitle;
                        _logger.LogDebug("⚙️ Int section title from E2: {Title}", intTitle);
                    }
                    
                    var longRealTitle = sheet.Cells["L2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(longRealTitle))
                    {
                        config.LongRealSectionTitle = longRealTitle;
                        _logger.LogDebug("⚙️ LongReal section title from L2: {Title}", longRealTitle);
                    }
                    
                    var longReal2Title = sheet.Cells["T2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(longReal2Title))
                    {
                        config.LongReal2SectionTitle = longReal2Title;
                        _logger.LogDebug("⚙️ LongReal2 section title from T2: {Title}", longReal2Title);
                    }
                    
                    // Leer datos desde la fila 2 (títulos y datos en la misma fila)
                    int row = 2;
                    int maxEmptyRows = 5; // Permitir hasta 5 filas vacías consecutivas
                    int consecutiveEmpty = 0;
                    
                    while (consecutiveEmpty < maxEmptyRows)
                    {
                        bool hasData = false;
                        
                        // === BOOL: Columnas B (Nombre), C (Imagen), D (Variable PLC) ===
                        var boolName = sheet.Cells[$"B{row}"].Text?.Trim();
                        var boolImage = sheet.Cells[$"C{row}"].Text?.Trim();
                        var boolPlcVar = sheet.Cells[$"D{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(boolName) && !string.IsNullOrEmpty(boolPlcVar))
                        {
                            config.BoolSettings.Add(new ExcelBoolSetting
                            {
                                Name = boolName,
                                ImagePath = string.IsNullOrEmpty(boolImage) ? null : boolImage,
                                PlcVariable = boolPlcVar,
                                RowIndex = row - 1 // 0-based index
                            });
                            hasData = true;
                            _logger.LogDebug("⚙️ Bool setting [{Row}]: {Name} -> {PlcVar}", row, boolName, boolPlcVar);
                        }
                        
                        // === INT: Columnas F (Nombre), G (Imagen), H (Variable PLC), I (Min), J (Max), K (Unidad) ===
                        var intName = sheet.Cells[$"F{row}"].Text?.Trim();
                        var intImage = sheet.Cells[$"G{row}"].Text?.Trim();
                        var intPlcVar = sheet.Cells[$"H{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(intName) && !string.IsNullOrEmpty(intPlcVar))
                        {
                            var intSetting = new ExcelIntSetting
                            {
                                Name = intName,
                                ImagePath = string.IsNullOrEmpty(intImage) ? null : intImage,
                                PlcVariable = intPlcVar,
                                RowIndex = row - 1
                            };
                            
                            // Leer Min (columna I)
                            var minVal = sheet.Cells[$"I{row}"].Text?.Trim();
                            if (int.TryParse(minVal, out var minInt))
                                intSetting.MinValue = minInt;
                            
                            // Leer Max (columna J)
                            var maxVal = sheet.Cells[$"J{row}"].Text?.Trim();
                            if (int.TryParse(maxVal, out var maxInt))
                                intSetting.MaxValue = maxInt;
                            
                            // Leer Unidad (columna K)
                            var unit = sheet.Cells[$"K{row}"].Text?.Trim();
                            if (!string.IsNullOrEmpty(unit))
                                intSetting.Unit = unit;
                            
                            config.IntSettings.Add(intSetting);
                            hasData = true;
                            _logger.LogDebug("⚙️ Int setting [{Row}]: {Name} -> {PlcVar} (Min:{Min}, Max:{Max}, Unit:{Unit})", 
                                row, intName, intPlcVar, intSetting.MinValue, intSetting.MaxValue, intSetting.Unit);
                        }
                        
                        // === LONGREAL: Columnas M (Nombre), N (Imagen), O (Variable PLC), P (Min), Q (Max), R (Decimales), S (Unidad) ===
                        var lrealName = sheet.Cells[$"M{row}"].Text?.Trim();
                        var lrealImage = sheet.Cells[$"N{row}"].Text?.Trim();
                        var lrealPlcVar = sheet.Cells[$"O{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(lrealName) && !string.IsNullOrEmpty(lrealPlcVar))
                        {
                            var lrealSetting = new ExcelLongRealSetting
                            {
                                Name = lrealName,
                                ImagePath = string.IsNullOrEmpty(lrealImage) ? null : lrealImage,
                                PlcVariable = lrealPlcVar,
                                RowIndex = row - 1
                            };
                            
                            // Leer min/max/decimals/unit desde columnas P, Q, R, S
                            var lrealMin = sheet.Cells[$"P{row}"].Text?.Trim();
                            var lrealMax = sheet.Cells[$"Q{row}"].Text?.Trim();
                            var lrealDecimals = sheet.Cells[$"R{row}"].Text?.Trim();
                            var lrealUnit = sheet.Cells[$"S{row}"].Text?.Trim();
                            
                            if (double.TryParse(lrealMin, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var minDbl))
                                lrealSetting.MinValue = minDbl;
                            if (double.TryParse(lrealMax, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var maxDbl))
                                lrealSetting.MaxValue = maxDbl;
                            if (int.TryParse(lrealDecimals, out var dec))
                                lrealSetting.DecimalPlaces = dec;
                            lrealSetting.Unit = string.IsNullOrEmpty(lrealUnit) ? null : lrealUnit;
                            
                            config.LongRealSettings.Add(lrealSetting);
                            hasData = true;
                            _logger.LogDebug("⚙️ LongReal setting [{Row}]: {Name} -> {PlcVar} (Min:{Min}, Max:{Max}, Dec:{Decimals}, Unit:{Unit})", 
                                row, lrealName, lrealPlcVar, lrealSetting.MinValue, lrealSetting.MaxValue, lrealSetting.DecimalPlaces, lrealSetting.Unit);
                        }
                        
                        // === LONGREAL2: Columnas U (Nombre), V (Imagen), W (Variable PLC), X (Min), Y (Max), Z (Decimales), AA (Unidad) ===
                        var lreal2Name = sheet.Cells[$"U{row}"].Text?.Trim();
                        var lreal2Image = sheet.Cells[$"V{row}"].Text?.Trim();
                        var lreal2PlcVar = sheet.Cells[$"W{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(lreal2Name) && !string.IsNullOrEmpty(lreal2PlcVar))
                        {
                            var lreal2Setting = new ExcelLongRealSetting
                            {
                                Name = lreal2Name,
                                ImagePath = string.IsNullOrEmpty(lreal2Image) ? null : lreal2Image,
                                PlcVariable = lreal2PlcVar,
                                RowIndex = row - 1
                            };
                            
                            // Leer min/max/decimals/unit desde columnas X, Y, Z, AA
                            var lreal2Min = sheet.Cells[$"X{row}"].Text?.Trim();
                            var lreal2Max = sheet.Cells[$"Y{row}"].Text?.Trim();
                            var lreal2Decimals = sheet.Cells[$"Z{row}"].Text?.Trim();
                            var lreal2Unit = sheet.Cells[$"AA{row}"].Text?.Trim();
                            
                            if (double.TryParse(lreal2Min, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var min2Dbl))
                                lreal2Setting.MinValue = min2Dbl;
                            if (double.TryParse(lreal2Max, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var max2Dbl))
                                lreal2Setting.MaxValue = max2Dbl;
                            if (int.TryParse(lreal2Decimals, out var dec2))
                                lreal2Setting.DecimalPlaces = dec2;
                            lreal2Setting.Unit = string.IsNullOrEmpty(lreal2Unit) ? null : lreal2Unit;
                            
                            config.LongReal2Settings.Add(lreal2Setting);
                            hasData = true;
                            _logger.LogDebug("⚙️ LongReal2 setting [{Row}]: {Name} -> {PlcVar} (Min:{Min}, Max:{Max}, Dec:{Decimals}, Unit:{Unit})", 
                                row, lreal2Name, lreal2PlcVar, lreal2Setting.MinValue, lreal2Setting.MaxValue, lreal2Setting.DecimalPlaces, lreal2Setting.Unit);
                        }
                        
                        // Controlar filas vacías consecutivas
                        if (hasData)
                        {
                            consecutiveEmpty = 0;
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                }
                
                _logger.LogInformation("⚙️ Settings page loaded: {BoolCount} bool, {IntCount} int, {LRealCount} longreal, {LReal2Count} longreal2 parameters",
                    config.BoolSettings.Count, config.IntSettings.Count, config.LongRealSettings.Count, config.LongReal2Settings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚙️ Error loading settings page from Excel: {Path}", filePath);
            }
            
            return await Task.FromResult(config);
        }
        
        #endregion
        
        /// <summary>
        /// Invalida el caché de configuración del sistema para forzar una recarga desde Excel.
        /// Si se proporciona filePath, solo invalida el cache de ese archivo.
        /// Si no se proporciona, invalida todo el cache.
        /// </summary>
        public void InvalidateCache(string? filePath = null)
        {
            lock (_cacheLock)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // Invalidar todo el cache
                    _systemConfigCache.Clear();
                    _stateColorsCache.Clear();
                    _variableViewsCache.Clear();
                    _elementsInfoSettingCache.Clear();
                    _logger.LogInformation("🔄 Todo el caché invalidado - se recargará en la próxima petición");
                }
                else
                {
                    // Invalidar solo el archivo específico
                    var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                    var cacheKey = fullPath.ToLowerInvariant();
                    
                    _systemConfigCache.Remove(cacheKey);
                    _stateColorsCache.Remove(cacheKey);
                    _variableViewsCache.Remove(cacheKey);
                    _elementsInfoSettingCache.Remove(cacheKey);
                    _logger.LogInformation("🔄 Caché invalidado para {Path} - se recargará en la próxima petición", Path.GetFileName(fullPath));
                }
            }
        }
        
        #region Wash Recipe Configuration
        
        /// <summary>
        /// Carga la configuración del editor de recetas de lavado desde la hoja "WashRecipe" del Excel.
        /// Estructura de columnas por fila (cada fila = una estación):
        /// - A2: Descripción nombre lavado (solo fila 2, título general)
        /// - A3: Variable PLC para nombre de receta (WSTRING)
        /// - B: Nombre de la estación
        /// - C: Imagen de la estación
        /// - D,E / F,G / ... / V,W: 10 pares de Variable PLC BOOL + Descripción
        /// - X,Y,Z,AA,AB / AC,AD,AE,AF,AG / ...: 10 grupos de Variable INT + Desc + Min + Max + Unidad
        /// </summary>
        public async Task<WashRecipeEditorConfiguration> LoadWashRecipeConfigAsync(string filePath)
        {
            var config = new WashRecipeEditorConfiguration();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("🚿 Excel file not found for wash recipe: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("🚿 Loading wash recipe configuration from Excel: {Path}", fullPath);
                
                await Task.Run(() =>
                {
                    using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var package = new ExcelPackage(stream);
                    
                    // Buscar hoja "WashRecipe" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("WashRecipe", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("🚿 Sheet 'WashRecipe' not found in Excel file");
                        return;
                    }
                    
                    // Leer descripción del nombre de lavado desde A2
                    var recipeDesc = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(recipeDesc))
                    {
                        config.RecipeNameDescription = recipeDesc;
                        _logger.LogDebug("🚿 Recipe name description from A2: {Desc}", recipeDesc);
                    }
                    
                    // Leer variable PLC del nombre de receta desde A3
                    var recipeNamePlcVar = sheet.Cells["A3"].Text?.Trim();
                    if (!string.IsNullOrEmpty(recipeNamePlcVar))
                    {
                        config.RecipeNamePlcVariable = recipeNamePlcVar;
                        _logger.LogDebug("🚿 Recipe name PLC variable from A3: {Var}", recipeNamePlcVar);
                    }
                    
                    // Leer variable PLC de la línea/número de receta desde A4
                    var recipeLinePlcVar = sheet.Cells["A4"].Text?.Trim();
                    if (!string.IsNullOrEmpty(recipeLinePlcVar))
                    {
                        config.RecipeLineNumberPlcVariable = recipeLinePlcVar;
                        _logger.LogDebug("🚿 Recipe line number PLC variable from A4: {Var}", recipeLinePlcVar);
                    }
                    
                    // Leer configuración de escritura alternativa desde A13 (ON/OFF)
                    var alternateWriteEnabled = sheet.Cells["A13"].Text?.Trim()?.ToUpperInvariant();
                    if (alternateWriteEnabled == "ON")
                    {
                        config.AlternateWriteEnabled = true;
                        _logger.LogDebug("🚿 Alternate write enabled from A13: ON");
                        
                        // Leer prefijo PLC alternativo desde A14
                        var alternatePlcPrefix = sheet.Cells["A14"].Text?.Trim();
                        if (!string.IsNullOrEmpty(alternatePlcPrefix))
                        {
                            config.AlternateWritePlcPrefix = alternatePlcPrefix;
                            _logger.LogDebug("🚿 Alternate PLC prefix from A14: {Prefix}", alternatePlcPrefix);
                        }
                    }
                    
                    // Definir columnas para parámetros BOOL (10 pares: Variable, Descripción)
                    // D-E, F-G, H-I, J-K, L-M, N-O, P-Q, R-S, T-U, V-W
                    var boolColumns = new (string VarCol, string DescCol)[]
                    {
                        ("D", "E"), ("F", "G"), ("H", "I"), ("J", "K"), ("L", "M"),
                        ("N", "O"), ("P", "Q"), ("R", "S"), ("T", "U"), ("V", "W")
                    };
                    
                    // Definir columnas para parámetros INT (10 grupos de 5: Variable, Descripción, Min, Max, Unidad)
                    // INT 1: X, Y, Z, AA, AB
                    // INT 2: AC, AD, AE, AF, AG
                    // INT 3: AH, AI, AJ, AK, AL
                    // INT 4: AM, AN, AO, AP, AQ
                    // INT 5: AR, AS, AT, AU, AV
                    // INT 6: AW, AX, AY, AZ, BA
                    // INT 7: BB, BC, BD, BE, BF
                    // INT 8: BG, BH, BI, BJ, BK
                    // INT 9: BL, BM, BN, BO, BP
                    // INT 10: BQ, BR, BS, BT, BU
                    var intColumns = new (string VarCol, string DescCol, string MinCol, string MaxCol, string UnitCol)[]
                    {
                        ("X", "Y", "Z", "AA", "AB"),
                        ("AC", "AD", "AE", "AF", "AG"),
                        ("AH", "AI", "AJ", "AK", "AL"),
                        ("AM", "AN", "AO", "AP", "AQ"),
                        ("AR", "AS", "AT", "AU", "AV"),
                        ("AW", "AX", "AY", "AZ", "BA"),
                        ("BB", "BC", "BD", "BE", "BF"),
                        ("BG", "BH", "BI", "BJ", "BK"),
                        ("BL", "BM", "BN", "BO", "BP"),
                        ("BQ", "BR", "BS", "BT", "BU")
                    };
                    
                    // Leer estaciones desde fila 2 en adelante
                    int row = 2;
                    int maxEmptyRows = 5;
                    int consecutiveEmpty = 0;
                    int stationIndex = 0;
                    
                    while (consecutiveEmpty < maxEmptyRows && stationIndex < 50) // Máximo 50 estaciones
                    {
                        var stationName = sheet.Cells[$"B{row}"].Text?.Trim();
                        var stationImage = sheet.Cells[$"C{row}"].Text?.Trim();
                        
                        // Verificar si hay algo en esta fila (nombre o al menos una variable)
                        bool hasData = !string.IsNullOrEmpty(stationName);
                        
                        // También verificar si hay variables configuradas aunque no haya nombre
                        if (!hasData)
                        {
                            foreach (var (varCol, _) in boolColumns)
                            {
                                if (!string.IsNullOrEmpty(sheet.Cells[$"{varCol}{row}"].Text?.Trim()))
                                {
                                    hasData = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!hasData)
                        {
                            foreach (var (varCol, _, _, _, _) in intColumns)
                            {
                                if (!string.IsNullOrEmpty(sheet.Cells[$"{varCol}{row}"].Text?.Trim()))
                                {
                                    hasData = true;
                                    break;
                                }
                            }
                        }
                        
                        if (hasData)
                        {
                            consecutiveEmpty = 0;
                            
                            var station = new WashRecipeStation
                            {
                                Index = stationIndex,
                                ExcelRow = row,
                                Name = stationName ?? $"Estación {stationIndex + 1}",
                                ImagePath = string.IsNullOrEmpty(stationImage) ? null : stationImage
                            };
                            
                            // Leer parámetros BOOL
                            for (int i = 0; i < boolColumns.Length; i++)
                            {
                                var (varCol, descCol) = boolColumns[i];
                                var plcVar = sheet.Cells[$"{varCol}{row}"].Text?.Trim();
                                var desc = sheet.Cells[$"{descCol}{row}"].Text?.Trim();
                                
                                station.BoolParameters.Add(new WashRecipeBoolParam
                                {
                                    Index = i,
                                    PlcVariable = plcVar ?? string.Empty,
                                    Description = desc ?? $"Bool {i + 1}",
                                    Value = false // Valor inicial, se leerá del PLC
                                });
                            }
                            
                            // Leer parámetros INT (ahora con 5 columnas: Var, Desc, Min, Max, Unit)
                            for (int i = 0; i < intColumns.Length; i++)
                            {
                                var (varCol, descCol, minCol, maxCol, unitCol) = intColumns[i];
                                var plcVar = sheet.Cells[$"{varCol}{row}"].Text?.Trim();
                                var desc = sheet.Cells[$"{descCol}{row}"].Text?.Trim();
                                var minText = sheet.Cells[$"{minCol}{row}"].Text?.Trim();
                                var maxText = sheet.Cells[$"{maxCol}{row}"].Text?.Trim();
                                var unit = sheet.Cells[$"{unitCol}{row}"].Text?.Trim();
                                
                                var intParam = new WashRecipeIntParam
                                {
                                    Index = i,
                                    PlcVariable = plcVar ?? string.Empty,
                                    Description = desc ?? $"Int {i + 1}",
                                    Value = 0, // Valor inicial, se leerá del PLC
                                    Unit = string.IsNullOrEmpty(unit) ? null : unit
                                };
                                
                                // Parsear Min
                                if (int.TryParse(minText, out var minVal))
                                    intParam.MinValue = minVal;
                                
                                // Parsear Max
                                if (int.TryParse(maxText, out var maxVal))
                                    intParam.MaxValue = maxVal;
                                
                                station.IntParameters.Add(intParam);
                            }
                            
                            config.Stations.Add(station);
                            _logger.LogDebug("🚿 Station [{Index}] '{Name}': {BoolCount} bool params, {IntCount} int params",
                                stationIndex, station.Name, 
                                station.BoolParameters.Count(p => p.IsConfigured),
                                station.IntParameters.Count(p => p.IsConfigured));
                            
                            stationIndex++;
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                    
                    config.LoadedAt = DateTime.Now;
                    _logger.LogInformation("🚿 Loaded {Count} wash recipe stations from Excel", config.Stations.Count);
                });
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚿 Error loading wash recipe configuration from {FilePath}", filePath);
                return config;
            }
        }
        
        /// <summary>
        /// Cargar configuración de TrainRecipe desde Excel
        /// 
        /// Estructura de la hoja "TrainRecipe":
        /// - A2: Título/descripción del nombre del tren
        /// - A3: Variable PLC para nombre del tipo de tren
        /// - A4: Número de línea
        /// - B2-E2: Parámetros booleanos (nombre en fila 2, variable PLC en fila 3)
        /// - F2-M2: Parámetros decimales (nombre en fila 2, variable PLC en fila 3, min/max/unidad en filas 4-6)
        /// </summary>
        public async Task<TrainRecipeConfiguration> LoadTrainRecipeConfigAsync(string filePath)
        {
            var config = new TrainRecipeConfiguration();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("🚆 Excel file not found for train recipe: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("🚆 Loading train recipe configuration from Excel: {Path}", fullPath);
                
                await Task.Run(() =>
                {
                    using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var package = new ExcelPackage(stream);
                    
                    // Buscar hoja "TrainRecipe" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("TrainRecipe", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("🚆 Sheet 'TrainRecipe' not found in Excel file");
                        return;
                    }
                    
                    // Leer descripción/título del nombre del tren desde A2
                    var titleLabel = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(titleLabel))
                    {
                        config.TitleLabel = titleLabel;
                        _logger.LogDebug("🚆 Train title label from A2: {Label}", titleLabel);
                    }
                    
                    // ============================================
                    // Leer nombres de secciones desde B2, F2, N2
                    // ============================================
                    var sectionBoolName = sheet.Cells["B2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(sectionBoolName))
                    {
                        config.SectionBoolName = sectionBoolName;
                        _logger.LogDebug("🚆 Section BOOL name from B2: {Name}", sectionBoolName);
                    }
                    
                    // Imagen de sección BOOL desde D2
                    var sectionBoolImage = sheet.Cells["D2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(sectionBoolImage))
                    {
                        config.SectionBoolImage = sectionBoolImage;
                        _logger.LogDebug("🚆 Section BOOL image from D2: {Image}", sectionBoolImage);
                    }
                    
                    var sectionDecimalName = sheet.Cells["F2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(sectionDecimalName))
                    {
                        config.SectionDecimalName = sectionDecimalName;
                        _logger.LogDebug("🚆 Section DECIMAL name from F2: {Name}", sectionDecimalName);
                    }
                    
                    // Imagen de sección DECIMAL desde H2
                    var sectionDecimalImage = sheet.Cells["H2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(sectionDecimalImage))
                    {
                        config.SectionDecimalImage = sectionDecimalImage;
                        _logger.LogDebug("🚆 Section DECIMAL image from H2: {Image}", sectionDecimalImage);
                    }
                    
                    var sectionGantryName = sheet.Cells["N2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(sectionGantryName))
                    {
                        config.SectionGantryName = sectionGantryName;
                        _logger.LogDebug("🚆 Section GANTRY name from N2: {Name}", sectionGantryName);
                    }
                    
                    // Leer variable PLC para el número de tablas activas del Gantry desde W2
                    // Valor 1 = 4 tablas (TAB1_*), Valor 2 = 8 tablas (TAB1_* + TAB2_*)
                    var gantryTableCountPlcVar = sheet.Cells["W2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(gantryTableCountPlcVar))
                    {
                        config.GantryTableCountPlcVariable = gantryTableCountPlcVar;
                        _logger.LogDebug("🚆 Gantry table count PLC variable from W2: {Var}", gantryTableCountPlcVar);
                    }
                    
                    // ============================================
                    // Leer plantillas de variables PLC para tablas de interpolación del Gantry (fila 2)
                    // Cada tabla tiene 4 columnas: Position_X, Position_Y, Speed_Y, FunctionType
                    // Las variables deben tener {index} como placeholder para el índice del array st_Points
                    // Ejemplo: "MAIN.fbMachine.st_TrainRecipe[1].st_Points[{index}].Position_X"
                    // ============================================
                    // TAB1_FW_UP:   AC=Position_X, AD=Position_Y, AE=Speed_Y, AF=FunctionType
                    // TAB1_FW_DOWN: AG=Position_X, AH=Position_Y, AI=Speed_Y, AJ=FunctionType
                    // TAB1_BW_UP:   AK=Position_X, AL=Position_Y, AM=Speed_Y, AN=FunctionType
                    // TAB1_BW_DOWN: AO=Position_X, AP=Position_Y, AQ=Speed_Y, AR=FunctionType
                    // TAB2_FW_UP:   AS=Position_X, AT=Position_Y, AU=Speed_Y, AV=FunctionType
                    // TAB2_FW_DOWN: AW=Position_X, AX=Position_Y, AY=Speed_Y, AZ=FunctionType
                    // TAB2_BW_UP:   BA=Position_X, BB=Position_Y, BC=Speed_Y, BD=FunctionType
                    // TAB2_BW_DOWN: BE=Position_X, BF=Position_Y, BG=Speed_Y, BH=FunctionType
                    // ============================================
                    
                    // Definición de tablas con columnas para variables de interpolación, número de líneas, min_height y max_height
                    // Columnas BI-BP: LineCount, BQ-BX: MinHeight, BY-CF: MaxHeight
                    var tableDefinitions = new[]
                    {
                        //  TableId         PosX   PosY   SpeedY FuncType LineCount MinHeight MaxHeight
                        ("TAB1_FW_UP",   "AC", "AD", "AE", "AF", "BI", "BQ", "BY"),
                        ("TAB1_FW_DOWN", "AG", "AH", "AI", "AJ", "BJ", "BR", "BZ"),
                        ("TAB1_BW_UP",   "AK", "AL", "AM", "AN", "BK", "BS", "CA"),
                        ("TAB1_BW_DOWN", "AO", "AP", "AQ", "AR", "BL", "BT", "CB"),
                        ("TAB2_FW_UP",   "AS", "AT", "AU", "AV", "BM", "BU", "CC"),
                        ("TAB2_FW_DOWN", "AW", "AX", "AY", "AZ", "BN", "BV", "CD"),
                        ("TAB2_BW_UP",   "BA", "BB", "BC", "BD", "BO", "BW", "CE"),
                        ("TAB2_BW_DOWN", "BE", "BF", "BG", "BH", "BP", "BX", "CF"),
                    };
                    
                    for (int i = 0; i < tableDefinitions.Length; i++)
                    {
                        var (tableId, colPosX, colPosY, colSpeedY, colFuncType, colLineCount, colMinHeight, colMaxHeight) = tableDefinitions[i];
                        
                        var posXTemplate = sheet.Cells[$"{colPosX}2"].Text?.Trim();
                        var posYTemplate = sheet.Cells[$"{colPosY}2"].Text?.Trim();
                        var speedYTemplate = sheet.Cells[$"{colSpeedY}2"].Text?.Trim();
                        var funcTypeTemplate = sheet.Cells[$"{colFuncType}2"].Text?.Trim();
                        var lineCountPlcVar = sheet.Cells[$"{colLineCount}2"].Text?.Trim();
                        var minHeightPlcVar = sheet.Cells[$"{colMinHeight}2"].Text?.Trim();
                        var maxHeightPlcVar = sheet.Cells[$"{colMaxHeight}2"].Text?.Trim();
                        
                        var table = new GantryInterpolationTable
                        {
                            TableId = tableId,
                            TableIndex = i,
                            PositionXPlcTemplate = posXTemplate ?? string.Empty,
                            PositionYPlcTemplate = posYTemplate ?? string.Empty,
                            SpeedYPlcTemplate = speedYTemplate ?? string.Empty,
                            FunctionTypePlcTemplate = funcTypeTemplate ?? string.Empty,
                            LineCountPlcVariable = lineCountPlcVar ?? string.Empty,
                            MinHeightPlcVariable = minHeightPlcVar ?? string.Empty,
                            MaxHeightPlcVariable = maxHeightPlcVar ?? string.Empty,
                        };
                        
                        config.GantryInterpolationTables.Add(table);
                        
                        if (table.IsConfigured)
                        {
                            _logger.LogDebug("🚆 Interpolation table {TableId}: PosX={PosX}, PosY={PosY}, Speed={Speed}, Func={Func}, LineCount={LineCount}, MinH={MinH}, MaxH={MaxH}",
                                tableId, posXTemplate, posYTemplate, speedYTemplate, funcTypeTemplate, lineCountPlcVar, minHeightPlcVar, maxHeightPlcVar);
                        }
                    }
                    
                    _logger.LogDebug("🚆 Loaded {Count} Gantry interpolation tables ({Configured} configured)",
                        config.GantryInterpolationTables.Count, 
                        config.GantryInterpolationTables.Count(t => t.IsConfigured));
                    
                    // Leer variable PLC del nombre del tren desde A3
                    var trainNamePlcVar = sheet.Cells["A3"].Text?.Trim();
                    if (!string.IsNullOrEmpty(trainNamePlcVar))
                    {
                        config.TrainNamePlcVariable = trainNamePlcVar;
                        _logger.LogDebug("🚆 Train name PLC variable from A3: {Var}", trainNamePlcVar);
                    }
                    
                    // Leer variable PLC de la línea/número desde A4
                    var lineNumberPlcVar = sheet.Cells["A4"].Text?.Trim();
                    if (!string.IsNullOrEmpty(lineNumberPlcVar))
                    {
                        config.LineNumberPlcVariable = lineNumberPlcVar;
                        _logger.LogDebug("🚆 Line number PLC variable from A4: {Var}", lineNumberPlcVar);
                    }
                    
                    // Leer variable PLC del trigger de escritura desde A5
                    // (Se pone en TRUE cuando escribimos al PLC, el PLC la pone en FALSE al recibir)
                    var writeTriggerPlcVar = sheet.Cells["A5"].Text?.Trim();
                    if (!string.IsNullOrEmpty(writeTriggerPlcVar))
                    {
                        config.WriteTriggerPlcVariable = writeTriggerPlcVar;
                        _logger.LogDebug("🚆 Write trigger PLC variable from A5: {Var}", writeTriggerPlcVar);
                    }
                    
                    // ============================================
                    // ESTRUCTURA POR FILAS (cada fila = un parámetro)
                    // ============================================
                    // BOOL: C=Nombre, D=Imagen, E=Variable PLC
                    // DECIMAL: G=Nombre, H=Imagen, I=Variable PLC, J=Min, K=Max, L=Decimales, M=Unidad
                    // ============================================
                    
                    int row = 2;
                    int maxEmptyRows = 5;
                    int consecutiveEmpty = 0;
                    
                    while (consecutiveEmpty < maxEmptyRows && row < 100) // Máximo 100 filas
                    {
                        // Leer parámetro BOOL de esta fila (columnas C, D, E)
                        var boolName = sheet.Cells[row, 3].Text?.Trim();      // C = columna 3
                        var boolImage = sheet.Cells[row, 4].Text?.Trim();     // D = columna 4
                        var boolPlcVar = sheet.Cells[row, 5].Text?.Trim();    // E = columna 5
                        
                        // Leer parámetro DECIMAL de esta fila (columnas G, H, I, J, K, L, M)
                        var decName = sheet.Cells[row, 7].Text?.Trim();       // G = columna 7
                        var decImage = sheet.Cells[row, 8].Text?.Trim();      // H = columna 8
                        var decPlcVar = sheet.Cells[row, 9].Text?.Trim();     // I = columna 9
                        var decMin = sheet.Cells[row, 10].Text?.Trim();       // J = columna 10
                        var decMax = sheet.Cells[row, 11].Text?.Trim();       // K = columna 11
                        var decDecimals = sheet.Cells[row, 12].Text?.Trim();  // L = columna 12
                        var decUnit = sheet.Cells[row, 13].Text?.Trim();      // M = columna 13
                        
                        bool hasData = false;
                        
                        // Agregar parámetro BOOL si tiene datos
                        if (!string.IsNullOrEmpty(boolName) || !string.IsNullOrEmpty(boolPlcVar))
                        {
                            config.BoolParameters.Add(new TrainRecipeParameter
                            {
                                Name = boolName ?? $"Bool_{row - 1}",
                                Image = boolImage,
                                PlcVariable = boolPlcVar,
                                DataType = "BOOL",
                                RowIndex = row
                            });
                            _logger.LogDebug("🚆 Bool param row {Row}: {Name} -> {Var}", row, boolName, boolPlcVar);
                            hasData = true;
                        }
                        
                        // Agregar parámetro DECIMAL si tiene datos
                        if (!string.IsNullOrEmpty(decName) || !string.IsNullOrEmpty(decPlcVar))
                        {
                            var param = new TrainRecipeParameter
                            {
                                Name = decName ?? $"Decimal_{row - 1}",
                                Image = decImage,
                                PlcVariable = decPlcVar,
                                DataType = "LREAL",
                                RowIndex = row
                            };
                            
                            // Parsear Min
                            if (!string.IsNullOrEmpty(decMin) && double.TryParse(decMin, 
                                System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var min))
                            {
                                param.MinValue = min;
                            }
                            
                            // Parsear Max
                            if (!string.IsNullOrEmpty(decMax) && double.TryParse(decMax, 
                                System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var max))
                            {
                                param.MaxValue = max;
                            }
                            
                            // Parsear Decimales (precisión)
                            if (!string.IsNullOrEmpty(decDecimals) && int.TryParse(decDecimals, out var decimals))
                            {
                                param.Decimals = decimals;
                            }
                            
                            // Unidad
                            if (!string.IsNullOrEmpty(decUnit))
                            {
                                param.Unit = decUnit;
                            }
                            
                            config.DecimalParameters.Add(param);
                            _logger.LogDebug("🚆 Decimal param row {Row}: {Name} -> {Var} ({Min}-{Max} {Unit})", 
                                row, decName, decPlcVar, param.MinValue, param.MaxValue, param.Unit);
                            hasData = true;
                        }
                        
                        // ============================================
                        // Leer parámetro GANTRY CONFIG de esta fila (columnas O, P, Q, R, S, T, U, V)
                        // O=Nombre, P=Icono, Q=Variable PLC, R=Min, S=Max, T=Decimales, U=Unidad, V=Visibilidad
                        // ============================================
                        var gantryName = sheet.Cells[row, 15].Text?.Trim();      // O = columna 15
                        var gantryImage = sheet.Cells[row, 16].Text?.Trim();     // P = columna 16
                        var gantryPlcVar = sheet.Cells[row, 17].Text?.Trim();    // Q = columna 17
                        var gantryMin = sheet.Cells[row, 18].Text?.Trim();       // R = columna 18
                        var gantryMax = sheet.Cells[row, 19].Text?.Trim();       // S = columna 19
                        var gantryDecimals = sheet.Cells[row, 20].Text?.Trim();  // T = columna 20
                        var gantryUnit = sheet.Cells[row, 21].Text?.Trim();      // U = columna 21
                        var gantryVisibility = sheet.Cells[row, 22].Text?.Trim(); // V = columna 22
                        
                        // Agregar parámetro GANTRY CONFIG si tiene nombre Y visibilidad
                        if (!string.IsNullOrEmpty(gantryName) && !string.IsNullOrEmpty(gantryVisibility))
                        {
                            var gantryParam = new GantryConfigParameter
                            {
                                Index = config.GantryConfigParameters.Count,
                                RowIndex = row,
                                Name = gantryName,
                                Image = gantryImage,
                                PlcVariable = gantryPlcVar ?? string.Empty,
                                Visibility = gantryVisibility
                            };
                            
                            // Parsear Min
                            if (!string.IsNullOrEmpty(gantryMin) && double.TryParse(gantryMin, 
                                System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var gMin))
                            {
                                gantryParam.MinValue = gMin;
                            }
                            
                            // Parsear Max
                            if (!string.IsNullOrEmpty(gantryMax) && double.TryParse(gantryMax, 
                                System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var gMax))
                            {
                                gantryParam.MaxValue = gMax;
                            }
                            
                            // Parsear Decimales
                            if (!string.IsNullOrEmpty(gantryDecimals) && int.TryParse(gantryDecimals, out var gDec))
                            {
                                gantryParam.Decimals = gDec;
                            }
                            
                            // Unidad
                            if (!string.IsNullOrEmpty(gantryUnit))
                            {
                                gantryParam.Unit = gantryUnit;
                            }
                            
                            config.GantryConfigParameters.Add(gantryParam);
                            _logger.LogDebug("🚆 Gantry config param row {Row}: {Name} -> {Var} (Visibility: {Vis})", 
                                row, gantryName, gantryPlcVar, gantryVisibility);
                            hasData = true;
                        }
                        
                        if (hasData)
                        {
                            consecutiveEmpty = 0;
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                    
                    // Leer prefijo alternativo para PLC2 desde A14 (igual que WashRecipe)
                    var alternatePlcPrefix = sheet.Cells["A14"].Text?.Trim();
                    if (!string.IsNullOrEmpty(alternatePlcPrefix))
                    {
                        config.AlternatePlcPrefix = alternatePlcPrefix;
                        _logger.LogDebug("🚆 Alternate PLC prefix from A14: {Prefix}", alternatePlcPrefix);
                    }
                    
                    _logger.LogInformation("🚆 Loaded TrainRecipe config: {BoolCount} bool params, {DecimalCount} decimal params, {GantryCount} gantry config params, {TableCount} interpolation tables ({TableConfigured} configured)", 
                        config.BoolParameters.Count, config.DecimalParameters.Count, config.GantryConfigParameters.Count, 
                        config.GantryInterpolationTables.Count, config.GantryInterpolationTables.Count(t => t.IsConfigured));
                });
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚆 Error loading train recipe configuration from {FilePath}", filePath);
                return config;
            }
        }
        
        #endregion
        
        #region Manual Mode Page (Modo Manual/Mantenimiento)
        
        /// <summary>
        /// Carga la configuración del modo manual desde la hoja "Manual" del Excel.
        /// Estructura:
        ///   A2: Título de la vista
        ///   B2+: Descripción del elemento
        ///   C2+: Imagen del elemento
        ///   D2+: Variable PLC (BOOL)
        /// </summary>
        /// <param name="filePath">Ruta al archivo Excel</param>
        /// <returns>Configuración del modo manual con elementos controlables</returns>
        public async Task<ManualPageExcelConfiguration> LoadManualPageAsync(string filePath)
        {
            var config = new ManualPageExcelConfiguration();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("🔧 Excel file not found for manual page: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("🔧 Loading manual mode config from Excel: {Path}", fullPath);
                
                await Task.Run(() =>
                {
                    // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                    using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var package = new ExcelPackage(stream);
                    
                    // Buscar hoja "Manual" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("Manual", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("🔧 Sheet 'Manual' not found in Excel file");
                        return;
                    }
                    
                    // === Leer título de la vista desde A2 ===
                    var viewTitle = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(viewTitle))
                    {
                        config.ViewTitle = viewTitle;
                        _logger.LogDebug("🔧 Manual view title from A2: {Title}", viewTitle);
                    }
                    
                    // Leer elementos desde la fila 2
                    int row = 2;
                    int maxEmptyRows = 5; // Permitir hasta 5 filas vacías consecutivas
                    int consecutiveEmpty = 0;
                    
                    while (consecutiveEmpty < maxEmptyRows)
                    {
                        // === Columnas B (Descripción), C (Imagen), D (Variable PLC) ===
                        var description = sheet.Cells[$"B{row}"].Text?.Trim();
                        var imagePath = sheet.Cells[$"C{row}"].Text?.Trim();
                        var plcVariable = sheet.Cells[$"D{row}"].Text?.Trim();
                        
                        // Solo añadir si tiene descripción Y variable PLC
                        if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(plcVariable))
                        {
                            config.Elements.Add(new ManualElementSetting
                            {
                                Description = description,
                                ImagePath = string.IsNullOrEmpty(imagePath) ? null : imagePath,
                                PlcVariable = plcVariable,
                                RowIndex = row
                            });
                            consecutiveEmpty = 0;
                            _logger.LogDebug("🔧 Manual element [{Row}]: {Desc} -> {PlcVar}", row, description, plcVariable);
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                    
                    _logger.LogInformation("🔧 Manual mode config loaded: {Title} with {Count} elements", 
                        config.ViewTitle, config.Elements.Count);
                });
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔧 Error loading manual mode configuration from {FilePath}", filePath);
                return config;
            }
        }
        
        #endregion
        
        #region ⚡ FAST CONFIGURATION - Panel de Configuración Rápida
        
        /// <summary>
        /// Carga la configuración del panel de configuración rápida desde la hoja "Fast_Configuration"
        /// </summary>
        /// <param name="filePath">Ruta al archivo Excel</param>
        /// <returns>Configuración con parámetros BOOL, INT y LREAL</returns>
        public async Task<FastConfigurationPageConfiguration> LoadFastConfigurationAsync(string filePath)
        {
            var config = new FastConfigurationPageConfiguration();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("⚡ Excel file not found for fast configuration: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("⚡ Loading fast configuration from Excel: {Path}", fullPath);
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    // Buscar hoja "Fast_Configuration" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("Fast_Configuration", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogWarning("⚡ Sheet 'Fast_Configuration' not found in Excel file");
                        return config;
                    }
                    
                    // === Leer títulos desde las celdas A2, B2, F2, M2 ===
                    var pageTitle = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(pageTitle))
                    {
                        config.PageTitle = pageTitle;
                        _logger.LogDebug("⚡ Page title from A2: {Title}", pageTitle);
                    }
                    
                    var boolTitle = sheet.Cells["B2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(boolTitle))
                    {
                        config.BoolSectionTitle = boolTitle;
                        _logger.LogDebug("⚡ Bool section title from B2: {Title}", boolTitle);
                    }
                    
                    var intTitle = sheet.Cells["F2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(intTitle))
                    {
                        config.IntSectionTitle = intTitle;
                        _logger.LogDebug("⚡ Int section title from F2: {Title}", intTitle);
                    }
                    
                    var lrealTitle = sheet.Cells["M2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(lrealTitle))
                    {
                        config.LRealSectionTitle = lrealTitle;
                        _logger.LogDebug("⚡ LReal section title from M2: {Title}", lrealTitle);
                    }
                    
                    // Leer datos desde la fila 2 (títulos y datos en la misma fila)
                    int row = 2;
                    int maxEmptyRows = 5; // Permitir hasta 5 filas vacías consecutivas
                    int consecutiveEmpty = 0;
                    
                    while (consecutiveEmpty < maxEmptyRows)
                    {
                        bool hasData = false;
                        
                        // === BOOL: Columnas C (Descripción), D (Imagen), E (Variable PLC) ===
                        var boolDesc = sheet.Cells[$"C{row}"].Text?.Trim();
                        var boolImage = sheet.Cells[$"D{row}"].Text?.Trim();
                        var boolPlcVar = sheet.Cells[$"E{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(boolDesc) && !string.IsNullOrEmpty(boolPlcVar))
                        {
                            config.BoolSettings.Add(new FastConfigBoolSetting
                            {
                                Description = boolDesc,
                                ImagePath = string.IsNullOrEmpty(boolImage) ? null : boolImage,
                                PlcVariable = boolPlcVar,
                                RowIndex = row - 1 // 0-based index
                            });
                            hasData = true;
                            _logger.LogDebug("⚡ Fast Bool setting [{Row}]: {Desc} -> {PlcVar}", row, boolDesc, boolPlcVar);
                        }
                        
                        // === INT: Columnas G (Descripción), H (Imagen), I (Variable PLC), J (Min), K (Max), L (Unidad) ===
                        var intDesc = sheet.Cells[$"G{row}"].Text?.Trim();
                        var intImage = sheet.Cells[$"H{row}"].Text?.Trim();
                        var intPlcVar = sheet.Cells[$"I{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(intDesc) && !string.IsNullOrEmpty(intPlcVar))
                        {
                            var intSetting = new FastConfigIntSetting
                            {
                                Description = intDesc,
                                ImagePath = string.IsNullOrEmpty(intImage) ? null : intImage,
                                PlcVariable = intPlcVar,
                                RowIndex = row - 1
                            };
                            
                            // Leer Min (columna J)
                            var minVal = sheet.Cells[$"J{row}"].Text?.Trim();
                            if (int.TryParse(minVal, out var minInt))
                                intSetting.MinValue = minInt;
                            
                            // Leer Max (columna K)
                            var maxVal = sheet.Cells[$"K{row}"].Text?.Trim();
                            if (int.TryParse(maxVal, out var maxInt))
                                intSetting.MaxValue = maxInt;
                            
                            // Leer Unidad (columna L)
                            var unit = sheet.Cells[$"L{row}"].Text?.Trim();
                            if (!string.IsNullOrEmpty(unit))
                                intSetting.Unit = unit;
                            
                            config.IntSettings.Add(intSetting);
                            hasData = true;
                            _logger.LogDebug("⚡ Fast Int setting [{Row}]: {Desc} -> {PlcVar} (Min:{Min}, Max:{Max}, Unit:{Unit})", 
                                row, intDesc, intPlcVar, intSetting.MinValue, intSetting.MaxValue, intSetting.Unit);
                        }
                        
                        // === LREAL: Columnas N (Descripción), O (Imagen), P (Variable PLC), Q (Min), R (Max), S (Decimales), T (Unidad) ===
                        var lrealDesc = sheet.Cells[$"N{row}"].Text?.Trim();
                        var lrealImage = sheet.Cells[$"O{row}"].Text?.Trim();
                        var lrealPlcVar = sheet.Cells[$"P{row}"].Text?.Trim();
                        
                        if (!string.IsNullOrEmpty(lrealDesc) && !string.IsNullOrEmpty(lrealPlcVar))
                        {
                            var lrealSetting = new FastConfigLRealSetting
                            {
                                Description = lrealDesc,
                                ImagePath = string.IsNullOrEmpty(lrealImage) ? null : lrealImage,
                                PlcVariable = lrealPlcVar,
                                RowIndex = row - 1
                            };
                            
                            // Leer Min (columna Q)
                            var lrealMin = sheet.Cells[$"Q{row}"].Text?.Trim();
                            if (double.TryParse(lrealMin, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var minDbl))
                                lrealSetting.MinValue = minDbl;
                            
                            // Leer Max (columna R)
                            var lrealMax = sheet.Cells[$"R{row}"].Text?.Trim();
                            if (double.TryParse(lrealMax, System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, out var maxDbl))
                                lrealSetting.MaxValue = maxDbl;
                            
                            // Leer Decimales (columna S)
                            var lrealDecimals = sheet.Cells[$"S{row}"].Text?.Trim();
                            if (int.TryParse(lrealDecimals, out var dec))
                                lrealSetting.DecimalPlaces = dec;
                            
                            // Leer Unidad (columna T)
                            var lrealUnit = sheet.Cells[$"T{row}"].Text?.Trim();
                            lrealSetting.Unit = string.IsNullOrEmpty(lrealUnit) ? null : lrealUnit;
                            
                            config.LRealSettings.Add(lrealSetting);
                            hasData = true;
                            _logger.LogDebug("⚡ Fast LReal setting [{Row}]: {Desc} -> {PlcVar} (Min:{Min}, Max:{Max}, Dec:{Dec}, Unit:{Unit})", 
                                row, lrealDesc, lrealPlcVar, lrealSetting.MinValue, lrealSetting.MaxValue, lrealSetting.DecimalPlaces, lrealSetting.Unit);
                        }
                        
                        // Controlar filas vacías consecutivas
                        if (hasData)
                        {
                            consecutiveEmpty = 0;
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                }
                
                _logger.LogInformation("⚡ Fast Configuration loaded: {PageTitle} with {BoolCount} bool, {IntCount} int, {LRealCount} lreal parameters",
                    config.PageTitle, config.BoolSettings.Count, config.IntSettings.Count, config.LRealSettings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚡ Error loading fast configuration from Excel: {Path}", filePath);
            }
            
            return await Task.FromResult(config);
        }
        
        #endregion
        
        #region PLC Info Panel Configuration

        /// <summary>
        /// Carga la configuración de la card PLC Info Panel desde la hoja "Plc_InfoPanel".
        /// Las variables PLC son WSTRING de solo lectura.
        /// 
        /// Estructura de la hoja:
        /// - A2: Título de la card (solo 1 celda)
        /// - B2: Icono del título (solo 1 celda, opcional)
        /// - C2: Contenido del botón de ayuda (solo 1 celda)
        /// - D2...Dn: Nombre/descripción de cada línea
        /// - E2...En: Icono de cada línea (opcional)
        /// - F2...Fn: Variable PLC (WSTRING, requerida)
        /// </summary>
        public async Task<PlcInfoPanelConfig> LoadPlcInfoPanelAsync(string filePath)
        {
            var config = new PlcInfoPanelConfig();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("📟 Excel file not found for PLC Info Panel: {Path}", fullPath);
                    return config;
                }
                
                _logger.LogInformation("📟 Loading PLC Info Panel configuration from Excel: {Path}", fullPath);
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    // Buscar hoja "Plc_InfoPanel" (case-insensitive)
                    var sheet = package.Workbook.Worksheets
                        .FirstOrDefault(ws => ws.Name.Equals("Plc_InfoPanel", StringComparison.OrdinalIgnoreCase));
                    
                    if (sheet == null)
                    {
                        _logger.LogInformation("📟 Sheet 'Plc_InfoPanel' not found in Excel - PLC Info Panel disabled");
                        return config;
                    }
                    
                    // === Leer configuración de la card (solo de fila 2) ===
                    var title = sheet.Cells["A2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(title))
                    {
                        config.Title = title;
                        _logger.LogDebug("📟 Card title from A2: {Title}", title);
                    }
                    
                    var titleIcon = sheet.Cells["B2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(titleIcon))
                    {
                        config.TitleIcon = titleIcon;
                        _logger.LogDebug("📟 Card icon from B2: {Icon}", titleIcon);
                    }
                    
                    var helpContent = sheet.Cells["C2"].Text?.Trim();
                    if (!string.IsNullOrEmpty(helpContent))
                    {
                        config.HelpContent = helpContent;
                        _logger.LogDebug("📟 Help content from C2: {Length} chars", helpContent.Length);
                    }
                    
                    // === Leer líneas de datos desde fila 2 ===
                    int row = 2;
                    int maxEmptyRows = 3; // Permitir hasta 3 filas vacías consecutivas
                    int consecutiveEmpty = 0;
                    
                    _logger.LogInformation("📟 Scanning Plc_InfoPanel sheet starting from row 2...");
                    
                    while (consecutiveEmpty < maxEmptyRows)
                    {
                        // Columna F (Variable PLC) es requerida
                        var plcVariable = sheet.Cells[$"F{row}"].Text?.Trim();
                        
                        // DEBUG: Log every row we check
                        _logger.LogDebug("📟 Row {Row}: F={PlcVar}, D={LineName}", 
                            row, plcVariable ?? "(empty)", sheet.Cells[$"D{row}"].Text?.Trim() ?? "(empty)");
                        
                        if (!string.IsNullOrEmpty(plcVariable))
                        {
                            var lineName = sheet.Cells[$"D{row}"].Text?.Trim();
                            var lineIcon = sheet.Cells[$"E{row}"].Text?.Trim();
                            
                            // Solo agregar si tiene nombre y variable PLC
                            if (!string.IsNullOrEmpty(lineName))
                            {
                                var line = new PlcInfoPanelLine
                                {
                                    Name = lineName,
                                    Icon = string.IsNullOrEmpty(lineIcon) ? null : lineIcon,
                                    PlcVariable = plcVariable
                                };
                                
                                config.Lines.Add(line);
                                _logger.LogDebug("📟 Line [{Row}]: {Name} ({Icon}) -> {PlcVar}", 
                                    row, lineName, lineIcon ?? "no icon", plcVariable);
                            }
                            
                            consecutiveEmpty = 0;
                        }
                        else
                        {
                            consecutiveEmpty++;
                        }
                        
                        row++;
                    }
                    
                    // Marcar como habilitado si hay líneas configuradas
                    config.IsEnabled = config.Lines.Count > 0;
                }
                
                if (config.IsEnabled)
                {
                    _logger.LogInformation("📟 PLC Info Panel loaded: '{Title}' with {LineCount} lines. Variables: [{Variables}]",
                        config.Title, config.Lines.Count, string.Join(", ", config.AllVariables));
                }
                else
                {
                    _logger.LogInformation("📟 PLC Info Panel disabled (no lines configured)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📟 Error loading PLC Info Panel configuration from Excel: {Path}", filePath);
            }
            
            return await Task.FromResult(config);
        }
        
        #endregion
    }
}