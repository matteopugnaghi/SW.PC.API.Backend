using OfficeOpenXml;
using SW.PC.API.Backend.Models.Excel;

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
        void InvalidateCache(); // ✅ MÉTODO PARA FORZAR RECARGA
        Task<List<Model3DConfig>> Load3DModelsAsync(string filePath);
    }
    
    public class ExcelConfigService : IExcelConfigService
    {
        private readonly ILogger<ExcelConfigService> _logger;
        private readonly IMetricsService _metricsService;
        private readonly string _configFolder;
        
        // ✅ CACHÉ para evitar recargar Excel constantemente
        private SystemConfiguration? _cachedSystemConfig;
        private List<StateColorConfig>? _cachedStateColors;
        private DateTime? _systemConfigCacheTimestamp;
        private DateTime? _stateColorsCacheTimestamp;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5); // Cache válido por 5 minutos
        
        public ExcelConfigService(
            IWebHostEnvironment environment, 
            ILogger<ExcelConfigService> logger,
            IMetricsService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _configFolder = Path.Combine(environment.ContentRootPath, "ExcelConfigs");
            
            // Asegurar que existe la carpeta
            if (!Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
            }
            
            // Configurar licencia EPPlus (NonCommercial o Commercial)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
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
        
        /// <summary>
        /// Carga la configuración de colores por estado desde la hoja PLC_State_Colors
        /// </summary>
        public async Task<List<StateColorConfig>> LoadStateColorsAsync(string filePath)
        {
            // ✅ VERIFICAR CACHÉ PRIMERO
            if (_cachedStateColors != null && _stateColorsCacheTimestamp.HasValue)
            {
                var cacheAge = DateTime.UtcNow - _stateColorsCacheTimestamp.Value;
                if (cacheAge < _cacheExpiration)
                {
                    _logger.LogInformation("📦 Usando state colors desde CACHÉ (edad: {Age:F1}s, {Count} configs)", cacheAge.TotalSeconds, _cachedStateColors.Count);
                    _metricsService.RecordExcelLoadTime(0.1); // ✅ Cache hit = casi 0ms
                    return _cachedStateColors;
                }
                else
                {
                    _logger.LogInformation("⏰ Caché de state colors expirado, recargando");
                }
            }
            else
            {
                _logger.LogInformation("🔍 No hay caché de state colors, cargando desde Excel (cachedStateColors null: {IsNull}, timestamp null: {TimestampNull})", 
                    _cachedStateColors == null, !_stateColorsCacheTimestamp.HasValue);
            }
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Returning empty state colors.", fullPath);
                    var emptyList = new List<StateColorConfig>();
                    
                    // ✅ CACHEAR LISTA VACÍA TAMBIÉN
                    _cachedStateColors = emptyList;
                    _stateColorsCacheTimestamp = DateTime.UtcNow;
                    
                    return emptyList;
                }
                
                // Abrir archivo en modo solo lectura para permitir que esté abierto en Excel
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var package = new ExcelPackage(stream))
                {
                    var stateColors = await LoadStateColorsFromSheetAsync(package);
                    
                    // ✅ GUARDAR EN CACHÉ
                    _cachedStateColors = stateColors;
                    _stateColorsCacheTimestamp = DateTime.UtcNow;
                    _logger.LogDebug("💾 State colors guardados en caché ({Count} configs)", stateColors.Count);
                    
                    return stateColors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading state colors from Excel: {Message}", ex.Message);
                var errorList = new List<StateColorConfig>();
                
                // ✅ CACHEAR LISTA VACÍA EN ERROR
                _cachedStateColors = errorList;
                _stateColorsCacheTimestamp = DateTime.UtcNow;
                
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
                    
                    // 1. Variables de StateColors (colores según estado PLC)
                    var stateColors = await LoadStateColorsFromSheetAsync(package);
                    foreach (var sc in stateColors)
                    {
                        if (!string.IsNullOrWhiteSpace(sc.VariablePattern))
                        {
                            variableNames.Add(sc.VariablePattern);
                        }
                    }

                    // 2. Variables de animación (padre + hijos)
                    // Buscar en la misma hoja "3D Elements" donde están los StateColors
                    var worksheet = package.Workbook.Worksheets["3D Elements"];
                    
                    if (worksheet != null)
                    {
                        _logger.LogInformation("  🔍 Buscando variables de animación en hoja: {SheetName}", worksheet.Name);
                        int rowCount = worksheet.Dimension?.Rows ?? 0;
                        _logger.LogInformation("  📊 Total de filas en hoja: {RowCount}", rowCount);
                        
                        int parentAnimCount = 0;
                        int childAnimCount = 0;
                        
                        for (int row = 2; row <= rowCount; row++) // Empezar en fila 2 (saltar header)
                        {
                            // PADRE: Columnas U (AnimationType) y AD (AnimationPlcVariable)
                            var animationType = worksheet.Cells[$"U{row}"].Text?.Trim().ToUpper();
                            var animationVariable = worksheet.Cells[$"AD{row}"].Text?.Trim();
                            
                            if (!string.IsNullOrWhiteSpace(animationType) && animationType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(animationVariable))
                            {
                                variableNames.Add(animationVariable);
                                parentAnimCount++;
                                _logger.LogInformation("  ✅ Parent animation variable: {Variable}", animationVariable);
                            }
                            
                            // CHILD 1: Columnas AL (AnimationType) y AO (PlcVariable)
                            var child1AnimType = worksheet.Cells[$"AL{row}"].Text?.Trim().ToUpper();
                            var child1PlcVar = worksheet.Cells[$"AO{row}"].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(child1AnimType) && child1AnimType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(child1PlcVar))
                            {
                                variableNames.Add(child1PlcVar);
                                childAnimCount++;
                                _logger.LogInformation("  ✅ Child1 animation variable: {Variable}", child1PlcVar);
                            }
                            
                            // CHILD 2: Columnas BG (AnimationType) y BJ (PlcVariable)
                            var child2AnimType = worksheet.Cells[$"BG{row}"].Text?.Trim().ToUpper();
                            var child2PlcVar = worksheet.Cells[$"BJ{row}"].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(child2AnimType) && child2AnimType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(child2PlcVar))
                            {
                                variableNames.Add(child2PlcVar);
                                childAnimCount++;
                                _logger.LogInformation("  ✅ Child2 animation variable: {Variable}", child2PlcVar);
                            }
                            
                            // CHILD 3: Columnas CB (AnimationType) y CE (PlcVariable)
                            var child3AnimType = worksheet.Cells[$"CB{row}"].Text?.Trim().ToUpper();
                            var child3PlcVar = worksheet.Cells[$"CE{row}"].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(child3AnimType) && child3AnimType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(child3PlcVar))
                            {
                                variableNames.Add(child3PlcVar);
                                childAnimCount++;
                                _logger.LogInformation("  ✅ Child3 animation variable: {Variable}", child3PlcVar);
                            }
                            
                            // CHILD 4: Columnas CW (AnimationType) y CZ (PlcVariable)
                            var child4AnimType = worksheet.Cells[$"CW{row}"].Text?.Trim().ToUpper();
                            var child4PlcVar = worksheet.Cells[$"CZ{row}"].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(child4AnimType) && child4AnimType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(child4PlcVar))
                            {
                                variableNames.Add(child4PlcVar);
                                childAnimCount++;
                                _logger.LogInformation("  ✅ Child4 animation variable: {Variable}", child4PlcVar);
                            }
                            
                            // CHILD 5: Columnas DR (AnimationType) y DU (PlcVariable)
                            var child5AnimType = worksheet.Cells[$"DR{row}"].Text?.Trim().ToUpper();
                            var child5PlcVar = worksheet.Cells[$"DU{row}"].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(child5AnimType) && child5AnimType.Contains("REF PLC") && !string.IsNullOrWhiteSpace(child5PlcVar))
                            {
                                variableNames.Add(child5PlcVar);
                                childAnimCount++;
                                _logger.LogInformation("  ✅ Child5 animation variable: {Variable}", child5PlcVar);
                            }
                        }
                        
                        _logger.LogInformation("  📋 Animation variables found - Parent: {ParentCount}, Children: {ChildCount}, Total: {TotalCount}", 
                            parentAnimCount, childAnimCount, parentAnimCount + childAnimCount);
                    }
                    else
                    {
                        _logger.LogWarning("  ⚠️ No se encontró hoja '3D Elements' para variables de animación");
                    }

                    var variableList = variableNames.ToList();
                    _logger.LogInformation("📋 Variables a monitorear desde Excel: {Count}", variableList.Count);
                    foreach (var varName in variableList)
                    {
                        _logger.LogDebug("  - {Variable}", varName);
                    }

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
            // ✅ VERIFICAR CACHÉ PRIMERO
            if (_cachedSystemConfig != null && _systemConfigCacheTimestamp.HasValue)
            {
                var cacheAge = DateTime.UtcNow - _systemConfigCacheTimestamp.Value;
                if (cacheAge < _cacheExpiration)
                {
                    _logger.LogInformation("📦 Usando configuración del sistema desde CACHÉ (edad: {Age:F1}s)", cacheAge.TotalSeconds);
                    _metricsService.RecordExcelLoadTime(0.1); // ✅ Cache hit = casi 0ms
                    return _cachedSystemConfig;
                }
                else
                {
                    _logger.LogInformation("⏰ Caché expirado (edad: {Age:F1}min), recargando desde Excel", cacheAge.TotalMinutes);
                }
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Excel file not found: {Path}. Returning default system configuration.", fullPath);
                    var defaultConfig = new SystemConfiguration();
                    
                    // ✅ CACHEAR CONFIG POR DEFECTO TAMBIÉN
                    _cachedSystemConfig = defaultConfig;
                    _systemConfigCacheTimestamp = DateTime.UtcNow;
                    
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

                            // BASE DE DATOS
                            case "enabledatabase":
                            case "enable_database":
                                config.EnableDatabase = ParseBool(paramValue, false);
                                break;
                            
                            case "databaseconnectionstring":
                            case "database_connection_string":
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

                    stopwatch.Stop();
                    _metricsService.RecordExcelLoadTime(stopwatch.Elapsed.TotalMilliseconds);
                    _logger.LogDebug("⏱️ System configuration loaded in {Time}ms", stopwatch.Elapsed.TotalMilliseconds);
                    
                    // ✅ GUARDAR EN CACHÉ
                    _cachedSystemConfig = config;
                    _systemConfigCacheTimestamp = DateTime.UtcNow;
                    _logger.LogDebug("💾 Configuración guardada en caché (válida por {Minutes} minutos)", _cacheExpiration.TotalMinutes);
                    
                    return config;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading system configuration from Excel: {Message}", ex.Message);
                
                var errorConfig = new SystemConfiguration();
                
                // ✅ CACHEAR CONFIG POR DEFECTO TAMBIÉN EN ERROR
                _cachedSystemConfig = errorConfig;
                _systemConfigCacheTimestamp = DateTime.UtcNow;
                
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
        
        /// <summary>
        /// Invalida el caché de configuración del sistema para forzar una recarga desde Excel
        /// </summary>
        public void InvalidateCache()
        {
            _cachedSystemConfig = null;
            _cachedStateColors = null;
            _systemConfigCacheTimestamp = null;
            _stateColorsCacheTimestamp = null;
            _logger.LogInformation("🔄 Caché invalidado - se recargará en la próxima petición");
        }
    }
}