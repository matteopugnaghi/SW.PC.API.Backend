using ClosedXML.Excel;
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
        private readonly IWebHostEnvironment _environment;

        public PumpElementService(IWebHostEnvironment environment, ILogger<PumpElementService> logger)
        {
            _logger = logger;
            _environment = environment;
            _configFolder = Path.Combine(environment.ContentRootPath, "ExcelConfigs");

            // Solo crear carpeta ExcelConfigs en desarrollo - en producción debe ya existir
            if (environment.IsDevelopment() && !Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
                _logger.LogInformation("📁 PumpElementService: Created ExcelConfigs folder (development mode)");
            }
        }

        /// <summary>
        /// Helper: Get worksheet by name, returns null if not found (ClosedXML compatibility)
        /// </summary>
        private static IXLWorksheet? FindWorksheet(XLWorkbook workbook, string name)
            => workbook.TryGetWorksheet(name, out var ws) ? ws : null;

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

                // Read all bytes into memory to avoid issues when Excel has the file open
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                ms.Position = 0;
                fs.Close();

                using (var package = new XLWorkbook(ms))
                {
                    var sheet = FindWorksheet(package, "3D Elements");

                    if (sheet == null)
                    {
                        _logger.LogWarning("Sheet '3D Elements' not found in Excel file");
                        return elements;
                    }

                    // Leer número total de elementos desde A2
                    var totalElementsText = sheet.Cell("A2").GetString();
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
                            Name = sheet.Cell($"B{row}").GetString(),
                            
                            // C: File Name
                            FileName = sheet.Cell($"C{row}").GetString(),

                            // D-F: Offset file X/Y/Z
                            OffsetX = ParseDouble(sheet.Cell($"D{row}").GetString()),
                            OffsetY = ParseDouble(sheet.Cell($"E{row}").GetString()),
                            OffsetZ = ParseDouble(sheet.Cell($"F{row}").GetString()),

                            // G: PLC(main page reference)
                            PlcMainPageReference = sheet.Cell($"G{row}").GetString(),
                            PlcManualPageReference = "",  // No existe en nueva estructura
                            PlcConfigPageReference = "",  // No existe en nueva estructura

                            // H-K: Color element on/off/disabled/alarm (ORDEN CORRECTO)
                            ColorElementOn = sheet.Cell($"H{row}").GetString(),
                            ColorElementOff = sheet.Cell($"I{row}").GetString(),
                            ColorElementDisabled = sheet.Cell($"J{row}").GetString(),
                            ColorElementAlarm = sheet.Cell($"K{row}").GetString(),

                            // L: Element name descript.
                            ElementNameDescription = sheet.Cell($"L{row}").GetString(),
                            
                            // M-O: Rotation X/Y/Z (grados)
                            RotationX = ParseDouble(sheet.Cell($"M{row}").GetString()),
                            RotationY = ParseDouble(sheet.Cell($"N{row}").GetString()),
                            RotationZ = ParseDouble(sheet.Cell($"O{row}").GetString()),
                            
                            // P-R: Scale X/Y/Z
                            ScaleX = ParseDouble(sheet.Cell($"P{row}").GetString(), 1.0),
                            ScaleY = ParseDouble(sheet.Cell($"Q{row}").GetString(), 1.0),
                            ScaleZ = ParseDouble(sheet.Cell($"R{row}").GetString(), 1.0),
                            
                            // S: Pivot Offset para rotaciones (formato: "X,Y,Z")
                            PivotOffset = ParseString(sheet.Cell($"S{row}").GetString(), string.Empty),
                            
                            // T: EnableSwap - Hot-Swap condition (ej: "MAIN.var=1")
                            EnableSwap = ParseString(sheet.Cell($"T{row}").GetString(), string.Empty),
                            
                            // U: Animation Type
                            AnimationType = ParseString(sheet.Cell($"U{row}").GetString(), "none"),
                            
                            // V: Animation Speed
                            AnimationSpeed = ParseDouble(sheet.Cell($"V{row}").GetString(), 1.0),
                            
                            // W: Animate Only When On
                            AnimateOnlyWhenOn = ParseBool(sheet.Cell($"W{row}").GetString(), true),
                            
                            // AD: Animation PLC Variable (variable que controla la animación en mm)
                            AnimationPlcVariable = ParseString(sheet.Cell($"AD{row}").GetString(), string.Empty),
                            
                            // AE: Animation Min Value (valor mínimo en mm)
                            AnimationMinValue = ParseDouble(sheet.Cell($"AE{row}").GetString(), 0.0),
                            
                            // AF: Animation Max Value (valor máximo en mm)
                            AnimationMaxValue = ParseDouble(sheet.Cell($"AF{row}").GetString(), 1000.0),
                            
                            // AG: Animation Axis (X, Y o Z)
                            AnimationAxis = ParseString(sheet.Cell($"AG{row}").GetString(), "Y"),
                            
                            // AH: Animation Scale Factor (factor de conversión mm a Babylon units)
                            AnimationScaleFactor = ParseDouble(sheet.Cell($"AH{row}").GetString(), 0.1),
                            
                            // ===== HIJO 1 (AI-AZ: 18 columnas) =====
                            Child1_Name = ParseString(sheet.Cell($"AI{row}").GetString(), string.Empty),
                            Child1_ParentName = ParseString(sheet.Cell($"AJ{row}").GetString(), string.Empty),
                            Child1_FileName = ParseString(sheet.Cell($"AK{row}").GetString(), string.Empty),
                            Child1_AnimationType = ParseString(sheet.Cell($"AL{row}").GetString(), "none"),
                            Child1_AnimationSpeed = ParseDouble(sheet.Cell($"AM{row}").GetString(), 1.0),
                            Child1_AnimateOnlyWhenOn = ParseBool(sheet.Cell($"AN{row}").GetString(), true),
                            Child1_PlcVariable = ParseString(sheet.Cell($"AO{row}").GetString(), string.Empty),
                            Child1_Axis = ParseString(sheet.Cell($"AP{row}").GetString(), string.Empty),
                            Child1_MinValue = ParseDouble(sheet.Cell($"AQ{row}").GetString(), 0.0),
                            Child1_MaxValue = ParseDouble(sheet.Cell($"AR{row}").GetString(), 1000.0),
                            Child1_ScaleFactor = ParseDouble(sheet.Cell($"AS{row}").GetString(), 0.1),
                            Child1_ScaleX = ParseDoubleNullable(sheet.Cell($"AT{row}").GetString()),
                            Child1_ScaleY = ParseDoubleNullable(sheet.Cell($"AU{row}").GetString()),
                            Child1_ScaleZ = ParseDoubleNullable(sheet.Cell($"AV{row}").GetString()),
                            Child1_ColorOn = ParseString(sheet.Cell($"AW{row}").GetString(), string.Empty),
                            Child1_ColorOff = ParseString(sheet.Cell($"AX{row}").GetString(), string.Empty),
                            Child1_ColorDisabled = ParseString(sheet.Cell($"AY{row}").GetString(), string.Empty),
                            Child1_ColorAlarm = ParseString(sheet.Cell($"AZ{row}").GetString(), string.Empty),
                            Child1_OffsetX = ParseDouble(sheet.Cell($"BA{row}").GetString(), 0.0),
                            Child1_OffsetY = ParseDouble(sheet.Cell($"BB{row}").GetString(), 0.0),
                            Child1_OffsetZ = ParseDouble(sheet.Cell($"BC{row}").GetString(), 0.0),
                            
                            // ===== HIJO 2 (BD-BU: 18 columnas) =====
                            Child2_Name = ParseString(sheet.Cell($"BD{row}").GetString(), string.Empty),
                            Child2_ParentName = ParseString(sheet.Cell($"BE{row}").GetString(), string.Empty),
                            Child2_FileName = ParseString(sheet.Cell($"BF{row}").GetString(), string.Empty),
                            Child2_AnimationType = ParseString(sheet.Cell($"BG{row}").GetString(), "none"),
                            Child2_AnimationSpeed = ParseDouble(sheet.Cell($"BH{row}").GetString(), 1.0),
                            Child2_AnimateOnlyWhenOn = ParseBool(sheet.Cell($"BI{row}").GetString(), true),
                            Child2_PlcVariable = ParseString(sheet.Cell($"BJ{row}").GetString(), string.Empty),
                            Child2_Axis = ParseString(sheet.Cell($"BK{row}").GetString(), string.Empty),
                            Child2_MinValue = ParseDouble(sheet.Cell($"BL{row}").GetString(), 0.0),
                            Child2_MaxValue = ParseDouble(sheet.Cell($"BM{row}").GetString(), 1000.0),
                            Child2_ScaleFactor = ParseDouble(sheet.Cell($"BN{row}").GetString(), 0.1),
                            Child2_ScaleX = ParseDoubleNullable(sheet.Cell($"BO{row}").GetString()),
                            Child2_ScaleY = ParseDoubleNullable(sheet.Cell($"BP{row}").GetString()),
                            Child2_ScaleZ = ParseDoubleNullable(sheet.Cell($"BQ{row}").GetString()),
                            Child2_ColorOn = ParseString(sheet.Cell($"BR{row}").GetString(), string.Empty),
                            Child2_ColorOff = ParseString(sheet.Cell($"BS{row}").GetString(), string.Empty),
                            Child2_ColorDisabled = ParseString(sheet.Cell($"BT{row}").GetString(), string.Empty),
                            Child2_ColorAlarm = ParseString(sheet.Cell($"BU{row}").GetString(), string.Empty),
                            Child2_OffsetX = ParseDouble(sheet.Cell($"BV{row}").GetString(), 0.0),
                            Child2_OffsetY = ParseDouble(sheet.Cell($"BW{row}").GetString(), 0.0),
                            Child2_OffsetZ = ParseDouble(sheet.Cell($"BX{row}").GetString(), 0.0),
                            
                            // ===== HIJO 3 (BY-CS: 21 columnas) =====
                            Child3_Name = ParseString(sheet.Cell($"BY{row}").GetString(), string.Empty),
                            Child3_ParentName = ParseString(sheet.Cell($"BZ{row}").GetString(), string.Empty),
                            Child3_FileName = ParseString(sheet.Cell($"CA{row}").GetString(), string.Empty),
                            Child3_AnimationType = ParseString(sheet.Cell($"CB{row}").GetString(), "none"),
                            Child3_AnimationSpeed = ParseDouble(sheet.Cell($"CC{row}").GetString(), 1.0),
                            Child3_AnimateOnlyWhenOn = ParseBool(sheet.Cell($"CD{row}").GetString(), true),
                            Child3_PlcVariable = ParseString(sheet.Cell($"CE{row}").GetString(), string.Empty),
                            Child3_Axis = ParseString(sheet.Cell($"CF{row}").GetString(), string.Empty),
                            Child3_MinValue = ParseDouble(sheet.Cell($"CG{row}").GetString(), 0.0),
                            Child3_MaxValue = ParseDouble(sheet.Cell($"CH{row}").GetString(), 1000.0),
                            Child3_ScaleFactor = ParseDouble(sheet.Cell($"CI{row}").GetString(), 0.1),
                            Child3_ScaleX = ParseDoubleNullable(sheet.Cell($"CJ{row}").GetString()),
                            Child3_ScaleY = ParseDoubleNullable(sheet.Cell($"CK{row}").GetString()),
                            Child3_ScaleZ = ParseDoubleNullable(sheet.Cell($"CL{row}").GetString()),
                            Child3_ColorOn = ParseString(sheet.Cell($"CM{row}").GetString(), string.Empty),
                            Child3_ColorOff = ParseString(sheet.Cell($"CN{row}").GetString(), string.Empty),
                            Child3_ColorDisabled = ParseString(sheet.Cell($"CO{row}").GetString(), string.Empty),
                            Child3_ColorAlarm = ParseString(sheet.Cell($"CP{row}").GetString(), string.Empty),
                            Child3_OffsetX = ParseDouble(sheet.Cell($"CQ{row}").GetString(), 0.0),
                            Child3_OffsetY = ParseDouble(sheet.Cell($"CR{row}").GetString(), 0.0),
                            Child3_OffsetZ = ParseDouble(sheet.Cell($"CS{row}").GetString(), 0.0),
                            
                            // ===== HIJO 4 (CT-DN: 21 columnas) =====
                            Child4_Name = ParseString(sheet.Cell($"CT{row}").GetString(), string.Empty),
                            Child4_ParentName = ParseString(sheet.Cell($"CU{row}").GetString(), string.Empty),
                            Child4_FileName = ParseString(sheet.Cell($"CV{row}").GetString(), string.Empty),
                            Child4_AnimationType = ParseString(sheet.Cell($"CW{row}").GetString(), "none"),
                            Child4_AnimationSpeed = ParseDouble(sheet.Cell($"CX{row}").GetString(), 1.0),
                            Child4_AnimateOnlyWhenOn = ParseBool(sheet.Cell($"CY{row}").GetString(), true),
                            Child4_PlcVariable = ParseString(sheet.Cell($"CZ{row}").GetString(), string.Empty),
                            Child4_Axis = ParseString(sheet.Cell($"DA{row}").GetString(), string.Empty),
                            Child4_MinValue = ParseDouble(sheet.Cell($"DB{row}").GetString(), 0.0),
                            Child4_MaxValue = ParseDouble(sheet.Cell($"DC{row}").GetString(), 1000.0),
                            Child4_ScaleFactor = ParseDouble(sheet.Cell($"DD{row}").GetString(), 0.1),
                            Child4_ScaleX = ParseDoubleNullable(sheet.Cell($"DE{row}").GetString()),
                            Child4_ScaleY = ParseDoubleNullable(sheet.Cell($"DF{row}").GetString()),
                            Child4_ScaleZ = ParseDoubleNullable(sheet.Cell($"DG{row}").GetString()),
                            Child4_ColorOn = ParseString(sheet.Cell($"DH{row}").GetString(), string.Empty),
                            Child4_ColorOff = ParseString(sheet.Cell($"DI{row}").GetString(), string.Empty),
                            Child4_ColorDisabled = ParseString(sheet.Cell($"DJ{row}").GetString(), string.Empty),
                            Child4_ColorAlarm = ParseString(sheet.Cell($"DK{row}").GetString(), string.Empty),
                            Child4_OffsetX = ParseDouble(sheet.Cell($"DL{row}").GetString(), 0.0),
                            Child4_OffsetY = ParseDouble(sheet.Cell($"DM{row}").GetString(), 0.0),
                            Child4_OffsetZ = ParseDouble(sheet.Cell($"DN{row}").GetString(), 0.0),
                            
                            // ===== HIJO 5 (DO-EI: 21 columnas) =====
                            Child5_Name = ParseString(sheet.Cell($"DO{row}").GetString(), string.Empty),
                            Child5_ParentName = ParseString(sheet.Cell($"DP{row}").GetString(), string.Empty),
                            Child5_FileName = ParseString(sheet.Cell($"DQ{row}").GetString(), string.Empty),
                            Child5_AnimationType = ParseString(sheet.Cell($"DR{row}").GetString(), "none"),
                            Child5_AnimationSpeed = ParseDouble(sheet.Cell($"DS{row}").GetString(), 1.0),
                            Child5_AnimateOnlyWhenOn = ParseBool(sheet.Cell($"DT{row}").GetString(), true),
                            Child5_PlcVariable = ParseString(sheet.Cell($"DU{row}").GetString(), string.Empty),
                            Child5_Axis = ParseString(sheet.Cell($"DV{row}").GetString(), string.Empty),
                            Child5_MinValue = ParseDouble(sheet.Cell($"DW{row}").GetString(), 0.0),
                            Child5_MaxValue = ParseDouble(sheet.Cell($"DX{row}").GetString(), 1000.0),
                            Child5_ScaleFactor = ParseDouble(sheet.Cell($"DY{row}").GetString(), 0.1),
                            Child5_ScaleX = ParseDoubleNullable(sheet.Cell($"DZ{row}").GetString()),
                            Child5_ScaleY = ParseDoubleNullable(sheet.Cell($"EA{row}").GetString()),
                            Child5_ScaleZ = ParseDoubleNullable(sheet.Cell($"EB{row}").GetString()),
                            Child5_ColorOn = ParseString(sheet.Cell($"EC{row}").GetString(), string.Empty),
                            Child5_ColorOff = ParseString(sheet.Cell($"ED{row}").GetString(), string.Empty),
                            Child5_ColorDisabled = ParseString(sheet.Cell($"EE{row}").GetString(), string.Empty),
                            Child5_ColorAlarm = ParseString(sheet.Cell($"EF{row}").GetString(), string.Empty),
                            Child5_OffsetX = ParseDouble(sheet.Cell($"EG{row}").GetString(), 0.0),
                            Child5_OffsetY = ParseDouble(sheet.Cell($"EH{row}").GetString(), 0.0),
                            Child5_OffsetZ = ParseDouble(sheet.Cell($"EI{row}").GetString(), 0.0),
                            
                            // X: Initially Visible
                            InitiallyVisible = ParseBool(sheet.Cell($"X{row}").GetString(), true),
                            
                            // Y: Category
                            Category = ParseString(sheet.Cell($"Y{row}").GetString(), "pumps"),
                            
                            // Z: Layer
                            Layer = ParseString(sheet.Cell($"Z{row}").GetString(), "default"),
                            
                            // AA: Cast Shadows
                            CastShadows = ParseBool(sheet.Cell($"AA{row}").GetString(), true),
                            
                            // AB: Receive Shadows
                            ReceiveShadows = ParseBool(sheet.Cell($"AB{row}").GetString(), true),
                            
                            // AC: LOD Level
                            LOD = ParseString(sheet.Cell($"AC{row}").GetString(), "high"),
                            
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
                            _logger.LogInformation("   AL (Child1_AnimationType) raw: '{RawValue}'", sheet.Cell($"AL{row}").GetString());
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

                using (var package = new XLWorkbook())
                {
                    // Crear hoja con el nuevo nombre y encabezados simplificados
                    var sheet = package.Worksheets.Add("3D Elements");

                    // Encabezados (fila 1) según la nueva especificación
                    sheet.Cell("A1").Value = "Num 3D elements";
                    sheet.Cell("B1").Value = "Name";
                    sheet.Cell("C1").Value = "File Name";
                    sheet.Cell("D1").Value = "Offset file X";
                    sheet.Cell("E1").Value = "Offset file Y";
                    sheet.Cell("F1").Value = "Offset file Z";
                    sheet.Cell("G1").Value = "PLC(main page reference)";
                    sheet.Cell("H1").Value = "Color element on";
                    sheet.Cell("I1").Value = "Color element off";
                    sheet.Cell("J1").Value = "Color element disabled";
                    sheet.Cell("K1").Value = "Color element alarm";
                    sheet.Cell("L1").Value = "Element name descript.";
                    sheet.Cell("M1").Value = "Rotation X";
                    sheet.Cell("N1").Value = "Rotation Y";
                    sheet.Cell("O1").Value = "Rotation Z";
                    sheet.Cell("P1").Value = "Scale X";
                    sheet.Cell("Q1").Value = "Scale Y";
                    sheet.Cell("R1").Value = "Scale Z";
                    sheet.Cell("S1").Value = "Pivot Offset (X,Y,Z)";
                    sheet.Cell("T1").Value = "(Reserved)";
                    sheet.Cell("U1").Value = "Animation Type (none/REF PLC)";
                    sheet.Cell("V1").Value = "Animation Speed";
                    sheet.Cell("W1").Value = "Animate Only When On";
                    sheet.Cell("AD1").Value = "Animation PLC Variable";
                    sheet.Cell("AE1").Value = "Animation Min Value (mm)";
                    sheet.Cell("AF1").Value = "Animation Max Value (mm)";
                    sheet.Cell("AG1").Value = "Animation Axis (X/Y/Z)";
                    sheet.Cell("AH1").Value = "Animation Scale Factor";
                    
                    // Hijo 1 (AI-BC: 21 columnas)
                    sheet.Cell("AI1").Value = "Child1 Name";
                    sheet.Cell("AJ1").Value = "Child1 Parent Name";
                    sheet.Cell("AK1").Value = "Child1 File Name";
                    sheet.Cell("AL1").Value = "Child1 Animation Type";
                    sheet.Cell("AM1").Value = "Child1 Animation Speed";
                    sheet.Cell("AN1").Value = "Child1 Animate Only When On";
                    sheet.Cell("AO1").Value = "Child1 PLC Variable";
                    sheet.Cell("AP1").Value = "Child1 Axis";
                    sheet.Cell("AQ1").Value = "Child1 Min Value";
                    sheet.Cell("AR1").Value = "Child1 Max Value";
                    sheet.Cell("AS1").Value = "Child1 Scale Factor";
                    sheet.Cell("AT1").Value = "Child1 Scale X";
                    sheet.Cell("AU1").Value = "Child1 Scale Y";
                    sheet.Cell("AV1").Value = "Child1 Scale Z";
                    sheet.Cell("AW1").Value = "Child1 Color On";
                    sheet.Cell("AX1").Value = "Child1 Color Off";
                    sheet.Cell("AY1").Value = "Child1 Color Disabled";
                    sheet.Cell("AZ1").Value = "Child1 Color Alarm";
                    sheet.Cell("BA1").Value = "Child1 Offset X";
                    sheet.Cell("BB1").Value = "Child1 Offset Y";
                    sheet.Cell("BC1").Value = "Child1 Offset Z";
                    
                    // Hijo 2 (BD-BX: 21 columnas)
                    sheet.Cell("BD1").Value = "Child2 Name";
                    sheet.Cell("BE1").Value = "Child2 Parent Name";
                    sheet.Cell("BF1").Value = "Child2 File Name";
                    sheet.Cell("BG1").Value = "Child2 Animation Type";
                    sheet.Cell("BH1").Value = "Child2 Animation Speed";
                    sheet.Cell("BI1").Value = "Child2 Animate Only When On";
                    sheet.Cell("BJ1").Value = "Child2 PLC Variable";
                    sheet.Cell("BK1").Value = "Child2 Axis";
                    sheet.Cell("BL1").Value = "Child2 Min Value";
                    sheet.Cell("BM1").Value = "Child2 Max Value";
                    sheet.Cell("BN1").Value = "Child2 Scale Factor";
                    sheet.Cell("BO1").Value = "Child2 Scale X";
                    sheet.Cell("BP1").Value = "Child2 Scale Y";
                    sheet.Cell("BQ1").Value = "Child2 Scale Z";
                    sheet.Cell("BR1").Value = "Child2 Color On";
                    sheet.Cell("BS1").Value = "Child2 Color Off";
                    sheet.Cell("BT1").Value = "Child2 Color Disabled";
                    sheet.Cell("BU1").Value = "Child2 Color Alarm";
                    sheet.Cell("BV1").Value = "Child2 Offset X";
                    sheet.Cell("BW1").Value = "Child2 Offset Y";
                    sheet.Cell("BX1").Value = "Child2 Offset Z";
                    
                    // Hijo 3 (BY-CS: 21 columnas)
                    sheet.Cell("BY1").Value = "Child3 Name";
                    sheet.Cell("BZ1").Value = "Child3 Parent Name";
                    sheet.Cell("CA1").Value = "Child3 File Name";
                    sheet.Cell("CB1").Value = "Child3 Animation Type";
                    sheet.Cell("CC1").Value = "Child3 Animation Speed";
                    sheet.Cell("CD1").Value = "Child3 Animate Only When On";
                    sheet.Cell("CE1").Value = "Child3 PLC Variable";
                    sheet.Cell("CF1").Value = "Child3 Axis";
                    sheet.Cell("CG1").Value = "Child3 Min Value";
                    sheet.Cell("CH1").Value = "Child3 Max Value";
                    sheet.Cell("CI1").Value = "Child3 Scale Factor";
                    sheet.Cell("CJ1").Value = "Child3 Scale X";
                    sheet.Cell("CK1").Value = "Child3 Scale Y";
                    sheet.Cell("CL1").Value = "Child3 Scale Z";
                    sheet.Cell("CM1").Value = "Child3 Color On";
                    sheet.Cell("CN1").Value = "Child3 Color Off";
                    sheet.Cell("CO1").Value = "Child3 Color Disabled";
                    sheet.Cell("CP1").Value = "Child3 Color Alarm";
                    sheet.Cell("CQ1").Value = "Child3 Offset X";
                    sheet.Cell("CR1").Value = "Child3 Offset Y";
                    sheet.Cell("CS1").Value = "Child3 Offset Z";
                    
                    // Hijo 4 (CT-DN: 21 columnas)
                    sheet.Cell("CT1").Value = "Child4 Name";
                    sheet.Cell("CU1").Value = "Child4 Parent Name";
                    sheet.Cell("CV1").Value = "Child4 File Name";
                    sheet.Cell("CW1").Value = "Child4 Animation Type";
                    sheet.Cell("CX1").Value = "Child4 Animation Speed";
                    sheet.Cell("CY1").Value = "Child4 Animate Only When On";
                    sheet.Cell("CZ1").Value = "Child4 PLC Variable";
                    sheet.Cell("DA1").Value = "Child4 Axis";
                    sheet.Cell("DB1").Value = "Child4 Min Value";
                    sheet.Cell("DC1").Value = "Child4 Max Value";
                    sheet.Cell("DD1").Value = "Child4 Scale Factor";
                    sheet.Cell("DE1").Value = "Child4 Scale X";
                    sheet.Cell("DF1").Value = "Child4 Scale Y";
                    sheet.Cell("DG1").Value = "Child4 Scale Z";
                    sheet.Cell("DH1").Value = "Child4 Color On";
                    sheet.Cell("DI1").Value = "Child4 Color Off";
                    sheet.Cell("DJ1").Value = "Child4 Color Disabled";
                    sheet.Cell("DK1").Value = "Child4 Color Alarm";
                    sheet.Cell("DL1").Value = "Child4 Offset X";
                    sheet.Cell("DM1").Value = "Child4 Offset Y";
                    sheet.Cell("DN1").Value = "Child4 Offset Z";
                    
                    // Hijo 5 (DO-EI: 21 columnas)
                    sheet.Cell("DO1").Value = "Child5 Name";
                    sheet.Cell("DP1").Value = "Child5 Parent Name";
                    sheet.Cell("DQ1").Value = "Child5 File Name";
                    sheet.Cell("DR1").Value = "Child5 Animation Type";
                    sheet.Cell("DS1").Value = "Child5 Animation Speed";
                    sheet.Cell("DT1").Value = "Child5 Animate Only When On";
                    sheet.Cell("DU1").Value = "Child5 PLC Variable";
                    sheet.Cell("DV1").Value = "Child5 Axis";
                    sheet.Cell("DW1").Value = "Child5 Min Value";
                    sheet.Cell("DX1").Value = "Child5 Max Value";
                    sheet.Cell("DY1").Value = "Child5 Scale Factor";
                    sheet.Cell("DZ1").Value = "Child5 Scale X";
                    sheet.Cell("EA1").Value = "Child5 Scale Y";
                    sheet.Cell("EB1").Value = "Child5 Scale Z";
                    sheet.Cell("EC1").Value = "Child5 Color On";
                    sheet.Cell("ED1").Value = "Child5 Color Off";
                    sheet.Cell("EE1").Value = "Child5 Color Disabled";
                    sheet.Cell("EF1").Value = "Child5 Color Alarm";
                    sheet.Cell("EG1").Value = "Child5 Offset X";
                    sheet.Cell("EH1").Value = "Child5 Offset Y";
                    sheet.Cell("EI1").Value = "Child5 Offset Z";
                    
                    sheet.Cell("X1").Value = "Initially Visible";
                    sheet.Cell("Y1").Value = "Category";
                    sheet.Cell("Z1").Value = "Layer";
                    sheet.Cell("AA1").Value = "Cast Shadows";
                    sheet.Cell("AB1").Value = "Receive Shadows";
                    sheet.Cell("AC1").Value = "LOD Level";

                    // Escribir datos desde fila 2 según la nueva estructura
                    for (int i = 0; i < elements.Count; i++)
                    {
                        int row = 2 + i;
                        var element = elements[i];

                        // A: Total elementos solo en primera fila
                        if (i == 0)
                        {
                            sheet.Cell($"A{row}").Value = elements.Count;
                        }

                        sheet.Cell($"B{row}").Value = element.Name;
                        sheet.Cell($"C{row}").Value = element.FileName;

                        sheet.Cell($"D{row}").Value = element.OffsetX;
                        sheet.Cell($"E{row}").Value = element.OffsetY;
                        sheet.Cell($"F{row}").Value = element.OffsetZ;

                        sheet.Cell($"G{row}").Value = element.PlcMainPageReference;

                        sheet.Cell($"H{row}").Value = element.ColorElementOn;
                        sheet.Cell($"I{row}").Value = element.ColorElementOff;
                        sheet.Cell($"J{row}").Value = element.ColorElementDisabled;
                        sheet.Cell($"K{row}").Value = element.ColorElementAlarm;

                        sheet.Cell($"L{row}").Value = element.ElementNameDescription;

                        sheet.Cell($"M{row}").Value = element.RotationX;
                        sheet.Cell($"N{row}").Value = element.RotationY;
                        sheet.Cell($"O{row}").Value = element.RotationZ;

                        sheet.Cell($"P{row}").Value = element.ScaleX;
                        sheet.Cell($"Q{row}").Value = element.ScaleY;
                        sheet.Cell($"R{row}").Value = element.ScaleZ;

                        sheet.Cell($"S{row}").Value = element.PivotOffset;
                        // T: Reservado
                        sheet.Cell($"U{row}").Value = element.AnimationType;
                        sheet.Cell($"V{row}").Value = element.AnimationSpeed;
                        sheet.Cell($"W{row}").Value = element.AnimateOnlyWhenOn;
                        sheet.Cell($"AD{row}").Value = element.AnimationPlcVariable;
                        sheet.Cell($"AE{row}").Value = element.AnimationMinValue;
                        sheet.Cell($"AF{row}").Value = element.AnimationMaxValue;
                        sheet.Cell($"AG{row}").Value = element.AnimationAxis;
                        sheet.Cell($"AH{row}").Value = element.AnimationScaleFactor;
                        
                        // Hijo 1 (AI-BC: 21 columnas)
                        sheet.Cell($"AI{row}").Value = element.Child1_Name;
                        sheet.Cell($"AJ{row}").Value = element.Child1_ParentName;
                        sheet.Cell($"AK{row}").Value = element.Child1_FileName;
                        sheet.Cell($"AL{row}").Value = element.Child1_AnimationType;
                        sheet.Cell($"AM{row}").Value = element.Child1_AnimationSpeed;
                        sheet.Cell($"AN{row}").Value = element.Child1_AnimateOnlyWhenOn;
                        sheet.Cell($"AO{row}").Value = element.Child1_PlcVariable;
                        sheet.Cell($"AP{row}").Value = element.Child1_Axis;
                        sheet.Cell($"AQ{row}").Value = element.Child1_MinValue;
                        sheet.Cell($"AR{row}").Value = element.Child1_MaxValue;
                        sheet.Cell($"AS{row}").Value = element.Child1_ScaleFactor;
                        sheet.Cell($"AT{row}").Value = element.Child1_ScaleX;
                        sheet.Cell($"AU{row}").Value = element.Child1_ScaleY;
                        sheet.Cell($"AV{row}").Value = element.Child1_ScaleZ;
                        sheet.Cell($"AW{row}").Value = element.Child1_ColorOn;
                        sheet.Cell($"AX{row}").Value = element.Child1_ColorOff;
                        sheet.Cell($"AY{row}").Value = element.Child1_ColorDisabled;
                        sheet.Cell($"AZ{row}").Value = element.Child1_ColorAlarm;
                        sheet.Cell($"BA{row}").Value = element.Child1_OffsetX;
                        sheet.Cell($"BB{row}").Value = element.Child1_OffsetY;
                        sheet.Cell($"BC{row}").Value = element.Child1_OffsetZ;
                        
                        // Hijo 2 (BD-BX: 21 columnas)
                        sheet.Cell($"BD{row}").Value = element.Child2_Name;
                        sheet.Cell($"BE{row}").Value = element.Child2_ParentName;
                        sheet.Cell($"BF{row}").Value = element.Child2_FileName;
                        sheet.Cell($"BG{row}").Value = element.Child2_AnimationType;
                        sheet.Cell($"BH{row}").Value = element.Child2_AnimationSpeed;
                        sheet.Cell($"BI{row}").Value = element.Child2_AnimateOnlyWhenOn;
                        sheet.Cell($"BJ{row}").Value = element.Child2_PlcVariable;
                        sheet.Cell($"BK{row}").Value = element.Child2_Axis;
                        sheet.Cell($"BL{row}").Value = element.Child2_MinValue;
                        sheet.Cell($"BM{row}").Value = element.Child2_MaxValue;
                        sheet.Cell($"BN{row}").Value = element.Child2_ScaleFactor;
                        sheet.Cell($"BO{row}").Value = element.Child2_ScaleX;
                        sheet.Cell($"BP{row}").Value = element.Child2_ScaleY;
                        sheet.Cell($"BQ{row}").Value = element.Child2_ScaleZ;
                        sheet.Cell($"BR{row}").Value = element.Child2_ColorOn;
                        sheet.Cell($"BS{row}").Value = element.Child2_ColorOff;
                        sheet.Cell($"BT{row}").Value = element.Child2_ColorDisabled;
                        sheet.Cell($"BU{row}").Value = element.Child2_ColorAlarm;
                        sheet.Cell($"BV{row}").Value = element.Child2_OffsetX;
                        sheet.Cell($"BW{row}").Value = element.Child2_OffsetY;
                        sheet.Cell($"BX{row}").Value = element.Child2_OffsetZ;
                        
                        // Hijo 3 (BY-CS: 21 columnas)
                        sheet.Cell($"BY{row}").Value = element.Child3_Name;
                        sheet.Cell($"BZ{row}").Value = element.Child3_ParentName;
                        sheet.Cell($"CA{row}").Value = element.Child3_FileName;
                        sheet.Cell($"CB{row}").Value = element.Child3_AnimationType;
                        sheet.Cell($"CC{row}").Value = element.Child3_AnimationSpeed;
                        sheet.Cell($"CD{row}").Value = element.Child3_AnimateOnlyWhenOn;
                        sheet.Cell($"CE{row}").Value = element.Child3_PlcVariable;
                        sheet.Cell($"CF{row}").Value = element.Child3_Axis;
                        sheet.Cell($"CG{row}").Value = element.Child3_MinValue;
                        sheet.Cell($"CH{row}").Value = element.Child3_MaxValue;
                        sheet.Cell($"CI{row}").Value = element.Child3_ScaleFactor;
                        sheet.Cell($"CJ{row}").Value = element.Child3_ScaleX;
                        sheet.Cell($"CK{row}").Value = element.Child3_ScaleY;
                        sheet.Cell($"CL{row}").Value = element.Child3_ScaleZ;
                        sheet.Cell($"CM{row}").Value = element.Child3_ColorOn;
                        sheet.Cell($"CN{row}").Value = element.Child3_ColorOff;
                        sheet.Cell($"CO{row}").Value = element.Child3_ColorDisabled;
                        sheet.Cell($"CP{row}").Value = element.Child3_ColorAlarm;
                        sheet.Cell($"CQ{row}").Value = element.Child3_OffsetX;
                        sheet.Cell($"CR{row}").Value = element.Child3_OffsetY;
                        sheet.Cell($"CS{row}").Value = element.Child3_OffsetZ;
                        
                        // Hijo 4 (CT-DN: 21 columnas)
                        sheet.Cell($"CT{row}").Value = element.Child4_Name;
                        sheet.Cell($"CU{row}").Value = element.Child4_ParentName;
                        sheet.Cell($"CV{row}").Value = element.Child4_FileName;
                        sheet.Cell($"CW{row}").Value = element.Child4_AnimationType;
                        sheet.Cell($"CX{row}").Value = element.Child4_AnimationSpeed;
                        sheet.Cell($"CY{row}").Value = element.Child4_AnimateOnlyWhenOn;
                        sheet.Cell($"CZ{row}").Value = element.Child4_PlcVariable;
                        sheet.Cell($"DA{row}").Value = element.Child4_Axis;
                        sheet.Cell($"DB{row}").Value = element.Child4_MinValue;
                        sheet.Cell($"DC{row}").Value = element.Child4_MaxValue;
                        sheet.Cell($"DD{row}").Value = element.Child4_ScaleFactor;
                        sheet.Cell($"DE{row}").Value = element.Child4_ScaleX;
                        sheet.Cell($"DF{row}").Value = element.Child4_ScaleY;
                        sheet.Cell($"DG{row}").Value = element.Child4_ScaleZ;
                        sheet.Cell($"DH{row}").Value = element.Child4_ColorOn;
                        sheet.Cell($"DI{row}").Value = element.Child4_ColorOff;
                        sheet.Cell($"DJ{row}").Value = element.Child4_ColorDisabled;
                        sheet.Cell($"DK{row}").Value = element.Child4_ColorAlarm;
                        sheet.Cell($"DL{row}").Value = element.Child4_OffsetX;
                        sheet.Cell($"DM{row}").Value = element.Child4_OffsetY;
                        sheet.Cell($"DN{row}").Value = element.Child4_OffsetZ;
                        
                        // Hijo 5 (DO-EI: 21 columnas)
                        sheet.Cell($"DO{row}").Value = element.Child5_Name;
                        sheet.Cell($"DP{row}").Value = element.Child5_ParentName;
                        sheet.Cell($"DQ{row}").Value = element.Child5_FileName;
                        sheet.Cell($"DR{row}").Value = element.Child5_AnimationType;
                        sheet.Cell($"DS{row}").Value = element.Child5_AnimationSpeed;
                        sheet.Cell($"DT{row}").Value = element.Child5_AnimateOnlyWhenOn;
                        sheet.Cell($"DU{row}").Value = element.Child5_PlcVariable;
                        sheet.Cell($"DV{row}").Value = element.Child5_Axis;
                        sheet.Cell($"DW{row}").Value = element.Child5_MinValue;
                        sheet.Cell($"DX{row}").Value = element.Child5_MaxValue;
                        sheet.Cell($"DY{row}").Value = element.Child5_ScaleFactor;
                        sheet.Cell($"DZ{row}").Value = element.Child5_ScaleX;
                        sheet.Cell($"EA{row}").Value = element.Child5_ScaleY;
                        sheet.Cell($"EB{row}").Value = element.Child5_ScaleZ;
                        sheet.Cell($"EC{row}").Value = element.Child5_ColorOn;
                        sheet.Cell($"ED{row}").Value = element.Child5_ColorOff;
                        sheet.Cell($"EE{row}").Value = element.Child5_ColorDisabled;
                        sheet.Cell($"EF{row}").Value = element.Child5_ColorAlarm;
                        sheet.Cell($"EG{row}").Value = element.Child5_OffsetX;
                        sheet.Cell($"EH{row}").Value = element.Child5_OffsetY;
                        sheet.Cell($"EI{row}").Value = element.Child5_OffsetZ;
                        
                        sheet.Cell($"X{row}").Value = element.InitiallyVisible;
                        sheet.Cell($"Y{row}").Value = element.Category;
                        sheet.Cell($"Z{row}").Value = element.Layer;
                        sheet.Cell($"AA{row}").Value = element.CastShadows;
                        sheet.Cell($"AB{row}").Value = element.ReceiveShadows;
                        sheet.Cell($"AC{row}").Value = element.LOD;
                    }

                    // Autoajustar columnas
                    sheet.Columns().AdjustToContents();

                    // Guardar archivo
                    package.SaveAs(fullPath);
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
