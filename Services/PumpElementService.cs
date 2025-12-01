using OfficeOpenXml;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public interface IPumpElementService
    {
        Task<List<PumpElement3D>> LoadPumpElementsAsync(string filePath);
        Task<bool> SavePumpElementsAsync(List<PumpElement3D> elements, string filePath);
    }

    public class PumpElementService : IPumpElementService
    {
        private readonly ILogger<PumpElementService> _logger;
        private readonly string _configFolder;

        public PumpElementService(IWebHostEnvironment environment, ILogger<PumpElementService> logger)
        {
            _logger = logger;
            _configFolder = Path.Combine(environment.ContentRootPath, "ExcelConfigs");

            if (!Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<List<PumpElement3D>> LoadPumpElementsAsync(string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Excel file not found: {fullPath}");
                }

                var elements = new List<PumpElement3D>();

                using (var package = new ExcelPackage(new FileInfo(fullPath)))
                {
                    var sheet = package.Workbook.Worksheets["3D Elements"];

                    if (sheet == null)
                    {
                        _logger.LogWarning("Sheet '3D Elements' not found in Excel file");
                        return elements;
                    }

                    // Leer número total de elementos desde A2
                    var totalElementsText = sheet.Cells["A2"].Text;
                    if (!int.TryParse(totalElementsText, out var totalElements))
                    {
                        _logger.LogWarning("Could not read total elements count from A2");
                        return elements;
                    }

                    _logger.LogInformation("Reading {TotalElements} 3D elements from Excel", totalElements);

                    // Leer elementos desde fila 2 hasta fila (2 + totalElements - 1)
                    for (int i = 0; i < totalElements; i++)
                    {
                        int row = 2 + i; // Fila 2 = primer elemento, fila 3 = segundo, etc.

                        var element = new PumpElement3D
                        {
                            ExcelRowIndex = row,
                            
                            // A: Num 3D elements
                            TotalElements = i == 0 ? totalElements : null,
                            
                            // B: Name
                            Name = sheet.Cells[$"B{row}"].Text,
                            
                            // C: File Name
                            FileName = sheet.Cells[$"C{row}"].Text,

                            // D-F: Offset file X/Y/Z
                            OffsetX = ParseDouble(sheet.Cells[$"D{row}"].Text),
                            OffsetY = ParseDouble(sheet.Cells[$"E{row}"].Text),
                            OffsetZ = ParseDouble(sheet.Cells[$"F{row}"].Text),

                            // G: PLC(main page reference)
                            PlcMainPageReference = sheet.Cells[$"G{row}"].Text,
                            PlcManualPageReference = "",  // No existe en nueva estructura
                            PlcConfigPageReference = "",  // No existe en nueva estructura

                            // H-K: Color element on/off/disabled/alarm (ORDEN CORRECTO)
                            ColorElementOn = sheet.Cells[$"H{row}"].Text,
                            ColorElementOff = sheet.Cells[$"I{row}"].Text,
                            ColorElementDisabled = sheet.Cells[$"J{row}"].Text,
                            ColorElementAlarm = sheet.Cells[$"K{row}"].Text,

                            // L: Element name descript.
                            ElementNameDescription = sheet.Cells[$"L{row}"].Text,
                            
                            // M-O: Rotation X/Y/Z (grados)
                            RotationX = ParseDouble(sheet.Cells[$"M{row}"].Text),
                            RotationY = ParseDouble(sheet.Cells[$"N{row}"].Text),
                            RotationZ = ParseDouble(sheet.Cells[$"O{row}"].Text),
                            
                            // P-R: Scale X/Y/Z
                            ScaleX = ParseDouble(sheet.Cells[$"P{row}"].Text, 1.0),
                            ScaleY = ParseDouble(sheet.Cells[$"Q{row}"].Text, 1.0),
                            ScaleZ = ParseDouble(sheet.Cells[$"R{row}"].Text, 1.0),
                            
                            // S: Is Clickable
                            IsClickable = ParseBool(sheet.Cells[$"S{row}"].Text, true),
                            
                            // T: Show Tooltip
                            ShowTooltip = ParseBool(sheet.Cells[$"T{row}"].Text, true),
                            
                            // U: Animation Type
                            AnimationType = ParseString(sheet.Cells[$"U{row}"].Text, "none"),
                            
                            // V: Animation Speed
                            AnimationSpeed = ParseDouble(sheet.Cells[$"V{row}"].Text, 1.0),
                            
                            // W: Animate Only When On
                            AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"W{row}"].Text, true),
                            
                            // AD: Animation PLC Variable (variable que controla la animación en mm)
                            AnimationPlcVariable = ParseString(sheet.Cells[$"AD{row}"].Text, string.Empty),
                            
                            // AE: Animation Min Value (valor mínimo en mm)
                            AnimationMinValue = ParseDouble(sheet.Cells[$"AE{row}"].Text, 0.0),
                            
                            // AF: Animation Max Value (valor máximo en mm)
                            AnimationMaxValue = ParseDouble(sheet.Cells[$"AF{row}"].Text, 1000.0),
                            
                            // AG: Animation Axis (X, Y o Z)
                            AnimationAxis = ParseString(sheet.Cells[$"AG{row}"].Text, "Y"),
                            
                            // AH: Animation Scale Factor (factor de conversión mm a Babylon units)
                            AnimationScaleFactor = ParseDouble(sheet.Cells[$"AH{row}"].Text, 0.1),
                            
                            // ===== HIJO 1 (AI-AZ: 18 columnas) =====
                            Child1_Name = ParseString(sheet.Cells[$"AI{row}"].Text, string.Empty),
                            Child1_ParentName = ParseString(sheet.Cells[$"AJ{row}"].Text, string.Empty),
                            Child1_FileName = ParseString(sheet.Cells[$"AK{row}"].Text, string.Empty),
                            Child1_AnimationType = ParseString(sheet.Cells[$"AL{row}"].Text, "none"),
                            Child1_AnimationSpeed = ParseDouble(sheet.Cells[$"AM{row}"].Text, 1.0),
                            Child1_AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"AN{row}"].Text, true),
                            Child1_PlcVariable = ParseString(sheet.Cells[$"AO{row}"].Text, string.Empty),
                            Child1_Axis = ParseString(sheet.Cells[$"AP{row}"].Text, string.Empty),
                            Child1_MinValue = ParseDouble(sheet.Cells[$"AQ{row}"].Text, 0.0),
                            Child1_MaxValue = ParseDouble(sheet.Cells[$"AR{row}"].Text, 1000.0),
                            Child1_ScaleFactor = ParseDouble(sheet.Cells[$"AS{row}"].Text, 0.1),
                            Child1_ScaleX = ParseDoubleNullable(sheet.Cells[$"AT{row}"].Text),
                            Child1_ScaleY = ParseDoubleNullable(sheet.Cells[$"AU{row}"].Text),
                            Child1_ScaleZ = ParseDoubleNullable(sheet.Cells[$"AV{row}"].Text),
                            Child1_ColorOn = ParseString(sheet.Cells[$"AW{row}"].Text, string.Empty),
                            Child1_ColorOff = ParseString(sheet.Cells[$"AX{row}"].Text, string.Empty),
                            Child1_ColorDisabled = ParseString(sheet.Cells[$"AY{row}"].Text, string.Empty),
                            Child1_ColorAlarm = ParseString(sheet.Cells[$"AZ{row}"].Text, string.Empty),
                            Child1_OffsetX = ParseDouble(sheet.Cells[$"BA{row}"].Text, 0.0),
                            Child1_OffsetY = ParseDouble(sheet.Cells[$"BB{row}"].Text, 0.0),
                            Child1_OffsetZ = ParseDouble(sheet.Cells[$"BC{row}"].Text, 0.0),
                            
                            // ===== HIJO 2 (BD-BU: 18 columnas) =====
                            Child2_Name = ParseString(sheet.Cells[$"BD{row}"].Text, string.Empty),
                            Child2_ParentName = ParseString(sheet.Cells[$"BE{row}"].Text, string.Empty),
                            Child2_FileName = ParseString(sheet.Cells[$"BF{row}"].Text, string.Empty),
                            Child2_AnimationType = ParseString(sheet.Cells[$"BG{row}"].Text, "none"),
                            Child2_AnimationSpeed = ParseDouble(sheet.Cells[$"BH{row}"].Text, 1.0),
                            Child2_AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"BI{row}"].Text, true),
                            Child2_PlcVariable = ParseString(sheet.Cells[$"BJ{row}"].Text, string.Empty),
                            Child2_Axis = ParseString(sheet.Cells[$"BK{row}"].Text, string.Empty),
                            Child2_MinValue = ParseDouble(sheet.Cells[$"BL{row}"].Text, 0.0),
                            Child2_MaxValue = ParseDouble(sheet.Cells[$"BM{row}"].Text, 1000.0),
                            Child2_ScaleFactor = ParseDouble(sheet.Cells[$"BN{row}"].Text, 0.1),
                            Child2_ScaleX = ParseDoubleNullable(sheet.Cells[$"BO{row}"].Text),
                            Child2_ScaleY = ParseDoubleNullable(sheet.Cells[$"BP{row}"].Text),
                            Child2_ScaleZ = ParseDoubleNullable(sheet.Cells[$"BQ{row}"].Text),
                            Child2_ColorOn = ParseString(sheet.Cells[$"BR{row}"].Text, string.Empty),
                            Child2_ColorOff = ParseString(sheet.Cells[$"BS{row}"].Text, string.Empty),
                            Child2_ColorDisabled = ParseString(sheet.Cells[$"BT{row}"].Text, string.Empty),
                            Child2_ColorAlarm = ParseString(sheet.Cells[$"BU{row}"].Text, string.Empty),
                            Child2_OffsetX = ParseDouble(sheet.Cells[$"BV{row}"].Text, 0.0),
                            Child2_OffsetY = ParseDouble(sheet.Cells[$"BW{row}"].Text, 0.0),
                            Child2_OffsetZ = ParseDouble(sheet.Cells[$"BX{row}"].Text, 0.0),
                            
                            // ===== HIJO 3 (BY-CS: 21 columnas) =====
                            Child3_Name = ParseString(sheet.Cells[$"BY{row}"].Text, string.Empty),
                            Child3_ParentName = ParseString(sheet.Cells[$"BZ{row}"].Text, string.Empty),
                            Child3_FileName = ParseString(sheet.Cells[$"CA{row}"].Text, string.Empty),
                            Child3_AnimationType = ParseString(sheet.Cells[$"CB{row}"].Text, "none"),
                            Child3_AnimationSpeed = ParseDouble(sheet.Cells[$"CC{row}"].Text, 1.0),
                            Child3_AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"CD{row}"].Text, true),
                            Child3_PlcVariable = ParseString(sheet.Cells[$"CE{row}"].Text, string.Empty),
                            Child3_Axis = ParseString(sheet.Cells[$"CF{row}"].Text, string.Empty),
                            Child3_MinValue = ParseDouble(sheet.Cells[$"CG{row}"].Text, 0.0),
                            Child3_MaxValue = ParseDouble(sheet.Cells[$"CH{row}"].Text, 1000.0),
                            Child3_ScaleFactor = ParseDouble(sheet.Cells[$"CI{row}"].Text, 0.1),
                            Child3_ScaleX = ParseDoubleNullable(sheet.Cells[$"CJ{row}"].Text),
                            Child3_ScaleY = ParseDoubleNullable(sheet.Cells[$"CK{row}"].Text),
                            Child3_ScaleZ = ParseDoubleNullable(sheet.Cells[$"CL{row}"].Text),
                            Child3_ColorOn = ParseString(sheet.Cells[$"CM{row}"].Text, string.Empty),
                            Child3_ColorOff = ParseString(sheet.Cells[$"CN{row}"].Text, string.Empty),
                            Child3_ColorDisabled = ParseString(sheet.Cells[$"CO{row}"].Text, string.Empty),
                            Child3_ColorAlarm = ParseString(sheet.Cells[$"CP{row}"].Text, string.Empty),
                            Child3_OffsetX = ParseDouble(sheet.Cells[$"CQ{row}"].Text, 0.0),
                            Child3_OffsetY = ParseDouble(sheet.Cells[$"CR{row}"].Text, 0.0),
                            Child3_OffsetZ = ParseDouble(sheet.Cells[$"CS{row}"].Text, 0.0),
                            
                            // ===== HIJO 4 (CT-DN: 21 columnas) =====
                            Child4_Name = ParseString(sheet.Cells[$"CT{row}"].Text, string.Empty),
                            Child4_ParentName = ParseString(sheet.Cells[$"CU{row}"].Text, string.Empty),
                            Child4_FileName = ParseString(sheet.Cells[$"CV{row}"].Text, string.Empty),
                            Child4_AnimationType = ParseString(sheet.Cells[$"CW{row}"].Text, "none"),
                            Child4_AnimationSpeed = ParseDouble(sheet.Cells[$"CX{row}"].Text, 1.0),
                            Child4_AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"CY{row}"].Text, true),
                            Child4_PlcVariable = ParseString(sheet.Cells[$"CZ{row}"].Text, string.Empty),
                            Child4_Axis = ParseString(sheet.Cells[$"DA{row}"].Text, string.Empty),
                            Child4_MinValue = ParseDouble(sheet.Cells[$"DB{row}"].Text, 0.0),
                            Child4_MaxValue = ParseDouble(sheet.Cells[$"DC{row}"].Text, 1000.0),
                            Child4_ScaleFactor = ParseDouble(sheet.Cells[$"DD{row}"].Text, 0.1),
                            Child4_ScaleX = ParseDoubleNullable(sheet.Cells[$"DE{row}"].Text),
                            Child4_ScaleY = ParseDoubleNullable(sheet.Cells[$"DF{row}"].Text),
                            Child4_ScaleZ = ParseDoubleNullable(sheet.Cells[$"DG{row}"].Text),
                            Child4_ColorOn = ParseString(sheet.Cells[$"DH{row}"].Text, string.Empty),
                            Child4_ColorOff = ParseString(sheet.Cells[$"DI{row}"].Text, string.Empty),
                            Child4_ColorDisabled = ParseString(sheet.Cells[$"DJ{row}"].Text, string.Empty),
                            Child4_ColorAlarm = ParseString(sheet.Cells[$"DK{row}"].Text, string.Empty),
                            Child4_OffsetX = ParseDouble(sheet.Cells[$"DL{row}"].Text, 0.0),
                            Child4_OffsetY = ParseDouble(sheet.Cells[$"DM{row}"].Text, 0.0),
                            Child4_OffsetZ = ParseDouble(sheet.Cells[$"DN{row}"].Text, 0.0),
                            
                            // ===== HIJO 5 (DO-EI: 21 columnas) =====
                            Child5_Name = ParseString(sheet.Cells[$"DO{row}"].Text, string.Empty),
                            Child5_ParentName = ParseString(sheet.Cells[$"DP{row}"].Text, string.Empty),
                            Child5_FileName = ParseString(sheet.Cells[$"DQ{row}"].Text, string.Empty),
                            Child5_AnimationType = ParseString(sheet.Cells[$"DR{row}"].Text, "none"),
                            Child5_AnimationSpeed = ParseDouble(sheet.Cells[$"DS{row}"].Text, 1.0),
                            Child5_AnimateOnlyWhenOn = ParseBool(sheet.Cells[$"DT{row}"].Text, true),
                            Child5_PlcVariable = ParseString(sheet.Cells[$"DU{row}"].Text, string.Empty),
                            Child5_Axis = ParseString(sheet.Cells[$"DV{row}"].Text, string.Empty),
                            Child5_MinValue = ParseDouble(sheet.Cells[$"DW{row}"].Text, 0.0),
                            Child5_MaxValue = ParseDouble(sheet.Cells[$"DX{row}"].Text, 1000.0),
                            Child5_ScaleFactor = ParseDouble(sheet.Cells[$"DY{row}"].Text, 0.1),
                            Child5_ScaleX = ParseDoubleNullable(sheet.Cells[$"DZ{row}"].Text),
                            Child5_ScaleY = ParseDoubleNullable(sheet.Cells[$"EA{row}"].Text),
                            Child5_ScaleZ = ParseDoubleNullable(sheet.Cells[$"EB{row}"].Text),
                            Child5_ColorOn = ParseString(sheet.Cells[$"EC{row}"].Text, string.Empty),
                            Child5_ColorOff = ParseString(sheet.Cells[$"ED{row}"].Text, string.Empty),
                            Child5_ColorDisabled = ParseString(sheet.Cells[$"EE{row}"].Text, string.Empty),
                            Child5_ColorAlarm = ParseString(sheet.Cells[$"EF{row}"].Text, string.Empty),
                            Child5_OffsetX = ParseDouble(sheet.Cells[$"EG{row}"].Text, 0.0),
                            Child5_OffsetY = ParseDouble(sheet.Cells[$"EH{row}"].Text, 0.0),
                            Child5_OffsetZ = ParseDouble(sheet.Cells[$"EI{row}"].Text, 0.0),
                            
                            // X: Initially Visible
                            InitiallyVisible = ParseBool(sheet.Cells[$"X{row}"].Text, true),
                            
                            // Y: Category
                            Category = ParseString(sheet.Cells[$"Y{row}"].Text, "pumps"),
                            
                            // Z: Layer
                            Layer = ParseString(sheet.Cells[$"Z{row}"].Text, "default"),
                            
                            // AA: Cast Shadows
                            CastShadows = ParseBool(sheet.Cells[$"AA{row}"].Text, true),
                            
                            // AB: Receive Shadows
                            ReceiveShadows = ParseBool(sheet.Cells[$"AB{row}"].Text, true),
                            
                            // AC: LOD Level
                            LOD = ParseString(sheet.Cells[$"AC{row}"].Text, "high"),
                            
                            // Valores por defecto para campos eliminados
                            LabelFontSize = 20,
                            LabelOffsetX_Pos1 = 0,
                            LabelOffsetY_Pos1 = 0,
                            LabelOffsetZ_Pos1 = 0,
                            LabelOffsetX_Pos2 = 0,
                            LabelOffsetY_Pos2 = 0,
                            LabelOffsetZ_Pos2 = 0,
                            OffspringsCount = 0,
                            IconFileReference = "",
                            IconLanguageLabelRow = 0,
                            BrandAndModel = "",
                            BindGantryNumber = -1,
                            AvailableColors = ""
                        };

                        // 🔍 DEBUG: Log child data for gantry_1
                        if (element.Name == "gantry_1")
                        {
                            _logger.LogInformation("🎯 DEBUG gantry_1 en row {Row}:", row);
                            _logger.LogInformation("   AL (Child1_AnimationType) raw: '{RawValue}'", sheet.Cells[$"AL{row}"].Text);
                            _logger.LogInformation("   Child1_AnimationType parsed: '{ParsedValue}'", element.Child1_AnimationType);
                            _logger.LogInformation("   AI (Child1_Name): '{Child1Name}'", element.Child1_Name);
                            _logger.LogInformation("   AO (Child1_PlcVariable): '{PlcVar}'", element.Child1_PlcVariable);
                        }

                        elements.Add(element);
                    }

                    // Procesar jerarquía padre-hijo (offsprings)
                    await ProcessOffspringsAsync(elements);

                    _logger.LogInformation("Successfully loaded {Count} pump elements", elements.Count);
                }

                return elements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pump elements from {FilePath}", filePath);
                throw;
            }
        }

        public async Task<bool> SavePumpElementsAsync(List<PumpElement3D> elements, string filePath)
        {
            try
            {
                var fullPath = Path.IsPathFullyQualified(filePath) ? filePath : Path.Combine(_configFolder, filePath);

                using (var package = new ExcelPackage())
                {
                    // Crear hoja con el nuevo nombre y encabezados simplificados
                    var sheet = package.Workbook.Worksheets.Add("3D Elements");

                    // Encabezados (fila 1) según la nueva especificación
                    sheet.Cells["A1"].Value = "Num 3D elements";
                    sheet.Cells["B1"].Value = "Name";
                    sheet.Cells["C1"].Value = "File Name";
                    sheet.Cells["D1"].Value = "Offset file X";
                    sheet.Cells["E1"].Value = "Offset file Y";
                    sheet.Cells["F1"].Value = "Offset file Z";
                    sheet.Cells["G1"].Value = "PLC(main page reference)";
                    sheet.Cells["H1"].Value = "Color element on";
                    sheet.Cells["I1"].Value = "Color element off";
                    sheet.Cells["J1"].Value = "Color element disabled";
                    sheet.Cells["K1"].Value = "Color element alarm";
                    sheet.Cells["L1"].Value = "Element name descript.";
                    sheet.Cells["M1"].Value = "Rotation X";
                    sheet.Cells["N1"].Value = "Rotation Y";
                    sheet.Cells["O1"].Value = "Rotation Z";
                    sheet.Cells["P1"].Value = "Scale X";
                    sheet.Cells["Q1"].Value = "Scale Y";
                    sheet.Cells["R1"].Value = "Scale Z";
                    sheet.Cells["S1"].Value = "Is Clickable";
                    sheet.Cells["T1"].Value = "Show Tooltip";
                    sheet.Cells["U1"].Value = "Animation Type (none/REF PLC)";
                    sheet.Cells["V1"].Value = "Animation Speed";
                    sheet.Cells["W1"].Value = "Animate Only When On";
                    sheet.Cells["AD1"].Value = "Animation PLC Variable";
                    sheet.Cells["AE1"].Value = "Animation Min Value (mm)";
                    sheet.Cells["AF1"].Value = "Animation Max Value (mm)";
                    sheet.Cells["AG1"].Value = "Animation Axis (X/Y/Z)";
                    sheet.Cells["AH1"].Value = "Animation Scale Factor";
                    
                    // Hijo 1 (AI-BC: 21 columnas)
                    sheet.Cells["AI1"].Value = "Child1 Name";
                    sheet.Cells["AJ1"].Value = "Child1 Parent Name";
                    sheet.Cells["AK1"].Value = "Child1 File Name";
                    sheet.Cells["AL1"].Value = "Child1 Animation Type";
                    sheet.Cells["AM1"].Value = "Child1 Animation Speed";
                    sheet.Cells["AN1"].Value = "Child1 Animate Only When On";
                    sheet.Cells["AO1"].Value = "Child1 PLC Variable";
                    sheet.Cells["AP1"].Value = "Child1 Axis";
                    sheet.Cells["AQ1"].Value = "Child1 Min Value";
                    sheet.Cells["AR1"].Value = "Child1 Max Value";
                    sheet.Cells["AS1"].Value = "Child1 Scale Factor";
                    sheet.Cells["AT1"].Value = "Child1 Scale X";
                    sheet.Cells["AU1"].Value = "Child1 Scale Y";
                    sheet.Cells["AV1"].Value = "Child1 Scale Z";
                    sheet.Cells["AW1"].Value = "Child1 Color On";
                    sheet.Cells["AX1"].Value = "Child1 Color Off";
                    sheet.Cells["AY1"].Value = "Child1 Color Disabled";
                    sheet.Cells["AZ1"].Value = "Child1 Color Alarm";
                    sheet.Cells["BA1"].Value = "Child1 Offset X";
                    sheet.Cells["BB1"].Value = "Child1 Offset Y";
                    sheet.Cells["BC1"].Value = "Child1 Offset Z";
                    
                    // Hijo 2 (BD-BX: 21 columnas)
                    sheet.Cells["BD1"].Value = "Child2 Name";
                    sheet.Cells["BE1"].Value = "Child2 Parent Name";
                    sheet.Cells["BF1"].Value = "Child2 File Name";
                    sheet.Cells["BG1"].Value = "Child2 Animation Type";
                    sheet.Cells["BH1"].Value = "Child2 Animation Speed";
                    sheet.Cells["BI1"].Value = "Child2 Animate Only When On";
                    sheet.Cells["BJ1"].Value = "Child2 PLC Variable";
                    sheet.Cells["BK1"].Value = "Child2 Axis";
                    sheet.Cells["BL1"].Value = "Child2 Min Value";
                    sheet.Cells["BM1"].Value = "Child2 Max Value";
                    sheet.Cells["BN1"].Value = "Child2 Scale Factor";
                    sheet.Cells["BO1"].Value = "Child2 Scale X";
                    sheet.Cells["BP1"].Value = "Child2 Scale Y";
                    sheet.Cells["BQ1"].Value = "Child2 Scale Z";
                    sheet.Cells["BR1"].Value = "Child2 Color On";
                    sheet.Cells["BS1"].Value = "Child2 Color Off";
                    sheet.Cells["BT1"].Value = "Child2 Color Disabled";
                    sheet.Cells["BU1"].Value = "Child2 Color Alarm";
                    sheet.Cells["BV1"].Value = "Child2 Offset X";
                    sheet.Cells["BW1"].Value = "Child2 Offset Y";
                    sheet.Cells["BX1"].Value = "Child2 Offset Z";
                    
                    // Hijo 3 (BY-CS: 21 columnas)
                    sheet.Cells["BY1"].Value = "Child3 Name";
                    sheet.Cells["BZ1"].Value = "Child3 Parent Name";
                    sheet.Cells["CA1"].Value = "Child3 File Name";
                    sheet.Cells["CB1"].Value = "Child3 Animation Type";
                    sheet.Cells["CC1"].Value = "Child3 Animation Speed";
                    sheet.Cells["CD1"].Value = "Child3 Animate Only When On";
                    sheet.Cells["CE1"].Value = "Child3 PLC Variable";
                    sheet.Cells["CF1"].Value = "Child3 Axis";
                    sheet.Cells["CG1"].Value = "Child3 Min Value";
                    sheet.Cells["CH1"].Value = "Child3 Max Value";
                    sheet.Cells["CI1"].Value = "Child3 Scale Factor";
                    sheet.Cells["CJ1"].Value = "Child3 Scale X";
                    sheet.Cells["CK1"].Value = "Child3 Scale Y";
                    sheet.Cells["CL1"].Value = "Child3 Scale Z";
                    sheet.Cells["CM1"].Value = "Child3 Color On";
                    sheet.Cells["CN1"].Value = "Child3 Color Off";
                    sheet.Cells["CO1"].Value = "Child3 Color Disabled";
                    sheet.Cells["CP1"].Value = "Child3 Color Alarm";
                    sheet.Cells["CQ1"].Value = "Child3 Offset X";
                    sheet.Cells["CR1"].Value = "Child3 Offset Y";
                    sheet.Cells["CS1"].Value = "Child3 Offset Z";
                    
                    // Hijo 4 (CT-DN: 21 columnas)
                    sheet.Cells["CT1"].Value = "Child4 Name";
                    sheet.Cells["CU1"].Value = "Child4 Parent Name";
                    sheet.Cells["CV1"].Value = "Child4 File Name";
                    sheet.Cells["CW1"].Value = "Child4 Animation Type";
                    sheet.Cells["CX1"].Value = "Child4 Animation Speed";
                    sheet.Cells["CY1"].Value = "Child4 Animate Only When On";
                    sheet.Cells["CZ1"].Value = "Child4 PLC Variable";
                    sheet.Cells["DA1"].Value = "Child4 Axis";
                    sheet.Cells["DB1"].Value = "Child4 Min Value";
                    sheet.Cells["DC1"].Value = "Child4 Max Value";
                    sheet.Cells["DD1"].Value = "Child4 Scale Factor";
                    sheet.Cells["DE1"].Value = "Child4 Scale X";
                    sheet.Cells["DF1"].Value = "Child4 Scale Y";
                    sheet.Cells["DG1"].Value = "Child4 Scale Z";
                    sheet.Cells["DH1"].Value = "Child4 Color On";
                    sheet.Cells["DI1"].Value = "Child4 Color Off";
                    sheet.Cells["DJ1"].Value = "Child4 Color Disabled";
                    sheet.Cells["DK1"].Value = "Child4 Color Alarm";
                    sheet.Cells["DL1"].Value = "Child4 Offset X";
                    sheet.Cells["DM1"].Value = "Child4 Offset Y";
                    sheet.Cells["DN1"].Value = "Child4 Offset Z";
                    
                    // Hijo 5 (DO-EI: 21 columnas)
                    sheet.Cells["DO1"].Value = "Child5 Name";
                    sheet.Cells["DP1"].Value = "Child5 Parent Name";
                    sheet.Cells["DQ1"].Value = "Child5 File Name";
                    sheet.Cells["DR1"].Value = "Child5 Animation Type";
                    sheet.Cells["DS1"].Value = "Child5 Animation Speed";
                    sheet.Cells["DT1"].Value = "Child5 Animate Only When On";
                    sheet.Cells["DU1"].Value = "Child5 PLC Variable";
                    sheet.Cells["DV1"].Value = "Child5 Axis";
                    sheet.Cells["DW1"].Value = "Child5 Min Value";
                    sheet.Cells["DX1"].Value = "Child5 Max Value";
                    sheet.Cells["DY1"].Value = "Child5 Scale Factor";
                    sheet.Cells["DZ1"].Value = "Child5 Scale X";
                    sheet.Cells["EA1"].Value = "Child5 Scale Y";
                    sheet.Cells["EB1"].Value = "Child5 Scale Z";
                    sheet.Cells["EC1"].Value = "Child5 Color On";
                    sheet.Cells["ED1"].Value = "Child5 Color Off";
                    sheet.Cells["EE1"].Value = "Child5 Color Disabled";
                    sheet.Cells["EF1"].Value = "Child5 Color Alarm";
                    sheet.Cells["EG1"].Value = "Child5 Offset X";
                    sheet.Cells["EH1"].Value = "Child5 Offset Y";
                    sheet.Cells["EI1"].Value = "Child5 Offset Z";
                    
                    sheet.Cells["X1"].Value = "Initially Visible";
                    sheet.Cells["Y1"].Value = "Category";
                    sheet.Cells["Z1"].Value = "Layer";
                    sheet.Cells["AA1"].Value = "Cast Shadows";
                    sheet.Cells["AB1"].Value = "Receive Shadows";
                    sheet.Cells["AC1"].Value = "LOD Level";

                    // Escribir datos desde fila 2 según la nueva estructura
                    for (int i = 0; i < elements.Count; i++)
                    {
                        int row = 2 + i;
                        var element = elements[i];

                        // A: Total elementos solo en primera fila
                        if (i == 0)
                        {
                            sheet.Cells[$"A{row}"].Value = elements.Count;
                        }

                        sheet.Cells[$"B{row}"].Value = element.Name;
                        sheet.Cells[$"C{row}"].Value = element.FileName;

                        sheet.Cells[$"D{row}"].Value = element.OffsetX;
                        sheet.Cells[$"E{row}"].Value = element.OffsetY;
                        sheet.Cells[$"F{row}"].Value = element.OffsetZ;

                        sheet.Cells[$"G{row}"].Value = element.PlcMainPageReference;

                        sheet.Cells[$"H{row}"].Value = element.ColorElementOn;
                        sheet.Cells[$"I{row}"].Value = element.ColorElementOff;
                        sheet.Cells[$"J{row}"].Value = element.ColorElementDisabled;
                        sheet.Cells[$"K{row}"].Value = element.ColorElementAlarm;

                        sheet.Cells[$"L{row}"].Value = element.ElementNameDescription;

                        sheet.Cells[$"M{row}"].Value = element.RotationX;
                        sheet.Cells[$"N{row}"].Value = element.RotationY;
                        sheet.Cells[$"O{row}"].Value = element.RotationZ;

                        sheet.Cells[$"P{row}"].Value = element.ScaleX;
                        sheet.Cells[$"Q{row}"].Value = element.ScaleY;
                        sheet.Cells[$"R{row}"].Value = element.ScaleZ;

                        sheet.Cells[$"S{row}"].Value = element.IsClickable;
                        sheet.Cells[$"T{row}"].Value = element.ShowTooltip;
                        sheet.Cells[$"U{row}"].Value = element.AnimationType;
                        sheet.Cells[$"V{row}"].Value = element.AnimationSpeed;
                        sheet.Cells[$"W{row}"].Value = element.AnimateOnlyWhenOn;
                        sheet.Cells[$"AD{row}"].Value = element.AnimationPlcVariable;
                        sheet.Cells[$"AE{row}"].Value = element.AnimationMinValue;
                        sheet.Cells[$"AF{row}"].Value = element.AnimationMaxValue;
                        sheet.Cells[$"AG{row}"].Value = element.AnimationAxis;
                        sheet.Cells[$"AH{row}"].Value = element.AnimationScaleFactor;
                        
                        // Hijo 1 (AI-BC: 21 columnas)
                        sheet.Cells[$"AI{row}"].Value = element.Child1_Name;
                        sheet.Cells[$"AJ{row}"].Value = element.Child1_ParentName;
                        sheet.Cells[$"AK{row}"].Value = element.Child1_FileName;
                        sheet.Cells[$"AL{row}"].Value = element.Child1_AnimationType;
                        sheet.Cells[$"AM{row}"].Value = element.Child1_AnimationSpeed;
                        sheet.Cells[$"AN{row}"].Value = element.Child1_AnimateOnlyWhenOn;
                        sheet.Cells[$"AO{row}"].Value = element.Child1_PlcVariable;
                        sheet.Cells[$"AP{row}"].Value = element.Child1_Axis;
                        sheet.Cells[$"AQ{row}"].Value = element.Child1_MinValue;
                        sheet.Cells[$"AR{row}"].Value = element.Child1_MaxValue;
                        sheet.Cells[$"AS{row}"].Value = element.Child1_ScaleFactor;
                        sheet.Cells[$"AT{row}"].Value = element.Child1_ScaleX;
                        sheet.Cells[$"AU{row}"].Value = element.Child1_ScaleY;
                        sheet.Cells[$"AV{row}"].Value = element.Child1_ScaleZ;
                        sheet.Cells[$"AW{row}"].Value = element.Child1_ColorOn;
                        sheet.Cells[$"AX{row}"].Value = element.Child1_ColorOff;
                        sheet.Cells[$"AY{row}"].Value = element.Child1_ColorDisabled;
                        sheet.Cells[$"AZ{row}"].Value = element.Child1_ColorAlarm;
                        sheet.Cells[$"BA{row}"].Value = element.Child1_OffsetX;
                        sheet.Cells[$"BB{row}"].Value = element.Child1_OffsetY;
                        sheet.Cells[$"BC{row}"].Value = element.Child1_OffsetZ;
                        
                        // Hijo 2 (BD-BX: 21 columnas)
                        sheet.Cells[$"BD{row}"].Value = element.Child2_Name;
                        sheet.Cells[$"BE{row}"].Value = element.Child2_ParentName;
                        sheet.Cells[$"BF{row}"].Value = element.Child2_FileName;
                        sheet.Cells[$"BG{row}"].Value = element.Child2_AnimationType;
                        sheet.Cells[$"BH{row}"].Value = element.Child2_AnimationSpeed;
                        sheet.Cells[$"BI{row}"].Value = element.Child2_AnimateOnlyWhenOn;
                        sheet.Cells[$"BJ{row}"].Value = element.Child2_PlcVariable;
                        sheet.Cells[$"BK{row}"].Value = element.Child2_Axis;
                        sheet.Cells[$"BL{row}"].Value = element.Child2_MinValue;
                        sheet.Cells[$"BM{row}"].Value = element.Child2_MaxValue;
                        sheet.Cells[$"BN{row}"].Value = element.Child2_ScaleFactor;
                        sheet.Cells[$"BO{row}"].Value = element.Child2_ScaleX;
                        sheet.Cells[$"BP{row}"].Value = element.Child2_ScaleY;
                        sheet.Cells[$"BQ{row}"].Value = element.Child2_ScaleZ;
                        sheet.Cells[$"BR{row}"].Value = element.Child2_ColorOn;
                        sheet.Cells[$"BS{row}"].Value = element.Child2_ColorOff;
                        sheet.Cells[$"BT{row}"].Value = element.Child2_ColorDisabled;
                        sheet.Cells[$"BU{row}"].Value = element.Child2_ColorAlarm;
                        sheet.Cells[$"BV{row}"].Value = element.Child2_OffsetX;
                        sheet.Cells[$"BW{row}"].Value = element.Child2_OffsetY;
                        sheet.Cells[$"BX{row}"].Value = element.Child2_OffsetZ;
                        
                        // Hijo 3 (BY-CS: 21 columnas)
                        sheet.Cells[$"BY{row}"].Value = element.Child3_Name;
                        sheet.Cells[$"BZ{row}"].Value = element.Child3_ParentName;
                        sheet.Cells[$"CA{row}"].Value = element.Child3_FileName;
                        sheet.Cells[$"CB{row}"].Value = element.Child3_AnimationType;
                        sheet.Cells[$"CC{row}"].Value = element.Child3_AnimationSpeed;
                        sheet.Cells[$"CD{row}"].Value = element.Child3_AnimateOnlyWhenOn;
                        sheet.Cells[$"CE{row}"].Value = element.Child3_PlcVariable;
                        sheet.Cells[$"CF{row}"].Value = element.Child3_Axis;
                        sheet.Cells[$"CG{row}"].Value = element.Child3_MinValue;
                        sheet.Cells[$"CH{row}"].Value = element.Child3_MaxValue;
                        sheet.Cells[$"CI{row}"].Value = element.Child3_ScaleFactor;
                        sheet.Cells[$"CJ{row}"].Value = element.Child3_ScaleX;
                        sheet.Cells[$"CK{row}"].Value = element.Child3_ScaleY;
                        sheet.Cells[$"CL{row}"].Value = element.Child3_ScaleZ;
                        sheet.Cells[$"CM{row}"].Value = element.Child3_ColorOn;
                        sheet.Cells[$"CN{row}"].Value = element.Child3_ColorOff;
                        sheet.Cells[$"CO{row}"].Value = element.Child3_ColorDisabled;
                        sheet.Cells[$"CP{row}"].Value = element.Child3_ColorAlarm;
                        sheet.Cells[$"CQ{row}"].Value = element.Child3_OffsetX;
                        sheet.Cells[$"CR{row}"].Value = element.Child3_OffsetY;
                        sheet.Cells[$"CS{row}"].Value = element.Child3_OffsetZ;
                        
                        // Hijo 4 (CT-DN: 21 columnas)
                        sheet.Cells[$"CT{row}"].Value = element.Child4_Name;
                        sheet.Cells[$"CU{row}"].Value = element.Child4_ParentName;
                        sheet.Cells[$"CV{row}"].Value = element.Child4_FileName;
                        sheet.Cells[$"CW{row}"].Value = element.Child4_AnimationType;
                        sheet.Cells[$"CX{row}"].Value = element.Child4_AnimationSpeed;
                        sheet.Cells[$"CY{row}"].Value = element.Child4_AnimateOnlyWhenOn;
                        sheet.Cells[$"CZ{row}"].Value = element.Child4_PlcVariable;
                        sheet.Cells[$"DA{row}"].Value = element.Child4_Axis;
                        sheet.Cells[$"DB{row}"].Value = element.Child4_MinValue;
                        sheet.Cells[$"DC{row}"].Value = element.Child4_MaxValue;
                        sheet.Cells[$"DD{row}"].Value = element.Child4_ScaleFactor;
                        sheet.Cells[$"DE{row}"].Value = element.Child4_ScaleX;
                        sheet.Cells[$"DF{row}"].Value = element.Child4_ScaleY;
                        sheet.Cells[$"DG{row}"].Value = element.Child4_ScaleZ;
                        sheet.Cells[$"DH{row}"].Value = element.Child4_ColorOn;
                        sheet.Cells[$"DI{row}"].Value = element.Child4_ColorOff;
                        sheet.Cells[$"DJ{row}"].Value = element.Child4_ColorDisabled;
                        sheet.Cells[$"DK{row}"].Value = element.Child4_ColorAlarm;
                        sheet.Cells[$"DL{row}"].Value = element.Child4_OffsetX;
                        sheet.Cells[$"DM{row}"].Value = element.Child4_OffsetY;
                        sheet.Cells[$"DN{row}"].Value = element.Child4_OffsetZ;
                        
                        // Hijo 5 (DO-EI: 21 columnas)
                        sheet.Cells[$"DO{row}"].Value = element.Child5_Name;
                        sheet.Cells[$"DP{row}"].Value = element.Child5_ParentName;
                        sheet.Cells[$"DQ{row}"].Value = element.Child5_FileName;
                        sheet.Cells[$"DR{row}"].Value = element.Child5_AnimationType;
                        sheet.Cells[$"DS{row}"].Value = element.Child5_AnimationSpeed;
                        sheet.Cells[$"DT{row}"].Value = element.Child5_AnimateOnlyWhenOn;
                        sheet.Cells[$"DU{row}"].Value = element.Child5_PlcVariable;
                        sheet.Cells[$"DV{row}"].Value = element.Child5_Axis;
                        sheet.Cells[$"DW{row}"].Value = element.Child5_MinValue;
                        sheet.Cells[$"DX{row}"].Value = element.Child5_MaxValue;
                        sheet.Cells[$"DY{row}"].Value = element.Child5_ScaleFactor;
                        sheet.Cells[$"DZ{row}"].Value = element.Child5_ScaleX;
                        sheet.Cells[$"EA{row}"].Value = element.Child5_ScaleY;
                        sheet.Cells[$"EB{row}"].Value = element.Child5_ScaleZ;
                        sheet.Cells[$"EC{row}"].Value = element.Child5_ColorOn;
                        sheet.Cells[$"ED{row}"].Value = element.Child5_ColorOff;
                        sheet.Cells[$"EE{row}"].Value = element.Child5_ColorDisabled;
                        sheet.Cells[$"EF{row}"].Value = element.Child5_ColorAlarm;
                        sheet.Cells[$"EG{row}"].Value = element.Child5_OffsetX;
                        sheet.Cells[$"EH{row}"].Value = element.Child5_OffsetY;
                        sheet.Cells[$"EI{row}"].Value = element.Child5_OffsetZ;
                        
                        sheet.Cells[$"X{row}"].Value = element.InitiallyVisible;
                        sheet.Cells[$"Y{row}"].Value = element.Category;
                        sheet.Cells[$"Z{row}"].Value = element.Layer;
                        sheet.Cells[$"AA{row}"].Value = element.CastShadows;
                        sheet.Cells[$"AB{row}"].Value = element.ReceiveShadows;
                        sheet.Cells[$"AC{row}"].Value = element.LOD;
                    }

                    // Autoajustar columnas
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

                    // Guardar archivo
                    await package.SaveAsAsync(new FileInfo(fullPath));
                }

                _logger.LogInformation("Successfully saved {Count} pump elements to {FilePath}", elements.Count, fullPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pump elements to {FilePath}", filePath);
                return false;
            }
        }

        // Procesar jerarquía padre-hijo
        private async Task ProcessOffspringsAsync(List<PumpElement3D> elements)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                var parent = elements[i];
                
                if (parent.OffspringsCount > 0)
                {
                    parent.Children = new List<PumpElement3D>();

                    // Los hijos están en las siguientes filas
                    for (int j = 1; j <= parent.OffspringsCount && (i + j) < elements.Count; j++)
                    {
                        var child = elements[i + j];
                        parent.Children.Add(child);
                    }

                    _logger.LogDebug("Element {Name} has {Count} children", parent.Name, parent.Children.Count);
                }
            }

            await Task.CompletedTask;
        }

        // Métodos de ayuda para parsing
        private double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0.0;

            if (double.TryParse(value, out var result))
                return result;

            return 0.0;
        }

        private double ParseDouble(string value, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (double.TryParse(value, out var result))
                return result;

            return defaultValue;
        }

        private double? ParseDoubleNullable(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (double.TryParse(value, out var result))
                return result;

            return null;
        }

        private int ParseInt(string value, int defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (int.TryParse(value, out var result))
                return result;

            return defaultValue;
        }

        private bool ParseBool(string value, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            // Soportar varios formatos: true/false, 1/0, yes/no, si/no
            var lowerValue = value.ToLower().Trim();
            
            if (lowerValue == "true" || lowerValue == "1" || lowerValue == "yes" || lowerValue == "si" || lowerValue == "sí")
                return true;
            
            if (lowerValue == "false" || lowerValue == "0" || lowerValue == "no")
                return false;

            return defaultValue;
        }

        private string ParseString(string value, string defaultValue = "")
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }
    }
}
