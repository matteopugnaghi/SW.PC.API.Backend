// ============================================================================
// TrainRecipeModels.cs - Modelos para Editor de Recetas de Tren
// ============================================================================
// Configuración de tipos de tren leída desde Excel hoja "TrainRecipe"
// Estructura similar a WashRecipe pero simplificada (sin estaciones)
// BOOL: Columnas C (Nombre), D (Imagen), E (Variable PLC)
// DECIMAL: Columnas G (Nombre), H (Imagen), I (Variable PLC), J (Min), K (Max), L (Decimales), M (Unidad)
// ============================================================================

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Configuración completa del editor de recetas de tren
    /// Leída desde la hoja "TrainRecipe" del Excel
    /// </summary>
    public class TrainRecipeConfiguration
    {
        /// <summary>
        /// Etiqueta/título del nombre del tren (desde A2)
        /// </summary>
        public string TitleLabel { get; set; } = "NOMBRE TREN";
        
        /// <summary>
        /// Variable PLC para el nombre del tipo de tren (desde A3)
        /// </summary>
        public string? TrainNamePlcVariable { get; set; }
        
        /// <summary>
        /// Variable PLC para el número de línea (desde A4)
        /// </summary>
        public string? LineNumberPlcVariable { get; set; }
        
        /// <summary>
        /// Variable PLC de trigger de escritura (desde A5)
        /// Se pone en TRUE cuando se escribe al PLC, el PLC la pone en FALSE al recibir
        /// </summary>
        public string? WriteTriggerPlcVariable { get; set; }
        
        /// <summary>
        /// Valor actual del nombre de tren leído del PLC
        /// </summary>
        public string TrainNameValue { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor actual del número de línea leído del PLC
        /// </summary>
        public int LineNumberValue { get; set; }
        
        /// <summary>
        /// Prefijo alternativo para escribir al PLC2 (desde A14 del Excel)
        /// Ej: "st_TrainRecipe2" para segundo PLC
        /// </summary>
        public string? AlternatePlcPrefix { get; set; }
        
        /// <summary>
        /// Nombre de la sección BOOL (desde celda B2 del Excel) - vacío = ocultar botón
        /// </summary>
        public string SectionBoolName { get; set; } = string.Empty;
        
        /// <summary>
        /// Imagen de la sección BOOL (desde celda D2 del Excel)
        /// </summary>
        public string? SectionBoolImage { get; set; }
        
        /// <summary>
        /// Nombre de la sección DECIMAL (desde celda F2 del Excel) - vacío = ocultar botón
        /// </summary>
        public string SectionDecimalName { get; set; } = string.Empty;
        
        /// <summary>
        /// Imagen de la sección DECIMAL (desde celda H2 del Excel)
        /// </summary>
        public string? SectionDecimalImage { get; set; }
        
        /// <summary>
        /// Nombre de la sección GANTRY (desde celda N2 del Excel) - vacío = ocultar botón
        /// </summary>
        public string SectionGantryName { get; set; } = string.Empty;

        /// <summary>
        /// Variable PLC para el número de tablas activas del Gantry (desde celda W2 del Excel)
        /// Valor 1 = 4 tablas (TAB1_*), Valor 2 = 8 tablas (TAB1_* + TAB2_*)
        /// </summary>
        public string? GantryTableCountPlcVariable { get; set; }
        
        /// <summary>
        /// Valor del número de tablas activas del Gantry leído del PLC
        /// </summary>
        public int GantryTableCountValue { get; set; } = 1;
        
        /// <summary>
        /// Lista de parámetros booleanos (filas del Excel columnas C-E)
        /// </summary>
        public List<TrainRecipeParameter> BoolParameters { get; set; } = new();
        
        /// <summary>
        /// Lista de parámetros decimales (filas del Excel columnas G-M)
        /// </summary>
        public List<TrainRecipeParameter> DecimalParameters { get; set; } = new();
        
        /// <summary>
        /// Lista de parámetros de configuración del Gantry (filas del Excel columnas O-V)
        /// </summary>
        public List<GantryConfigParameter> GantryConfigParameters { get; set; } = new();
        
        /// <summary>
        /// Lista de tablas de interpolación del Gantry (8 tablas desde Excel columnas AC-BH)
        /// TAB1_FW_UP, TAB1_FW_DOWN, TAB1_BW_UP, TAB1_BW_DOWN,
        /// TAB2_FW_UP, TAB2_FW_DOWN, TAB2_BW_UP, TAB2_BW_DOWN
        /// </summary>
        public List<GantryInterpolationTable> GantryInterpolationTables { get; set; } = new();
        
        /// <summary>
        /// Fecha de última carga de configuración
        /// </summary>
        public DateTime LoadedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Parámetro de configuración del Gantry (desde columnas O-V del Excel)
    /// O2 = Nombre, P2 = Icono, Q2 = Variable PLC, R2 = Min, S2 = Max, T2 = Decimales, U2 = Unidad, V2 = Visibilidad
    /// </summary>
    public class GantryConfigParameter
    {
        /// <summary>
        /// Índice del parámetro (0-based)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Número de fila en el Excel
        /// </summary>
        public int RowIndex { get; set; }
        
        /// <summary>
        /// Nombre del parámetro (columna O)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Ruta de la imagen/icono del parámetro (columna P)
        /// </summary>
        public string? Image { get; set; }
        
        /// <summary>
        /// Variable PLC del parámetro (columna Q)
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor mínimo (columna R)
        /// </summary>
        public double? MinValue { get; set; }
        
        /// <summary>
        /// Valor máximo (columna S)
        /// </summary>
        public double? MaxValue { get; set; }
        
        /// <summary>
        /// Número de decimales (columna T)
        /// </summary>
        public int Decimals { get; set; } = 0;
        
        /// <summary>
        /// Unidad de medida (columna U)
        /// </summary>
        public string? Unit { get; set; }
        
        /// <summary>
        /// Visibilidad: nombre de tabla donde se muestra (columna V)
        /// Ej: "TAB1_FW_UP", "TAB2_BW_DOWN", vacío = no visible
        /// </summary>
        public string? Visibility { get; set; }
        
        /// <summary>
        /// Valor actual leído del PLC
        /// </summary>
        public double Value { get; set; }
        
        /// <summary>
        /// Indica si el parámetro está configurado
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(PlcVariable) && !string.IsNullOrEmpty(Visibility);
    }
    
    /// <summary>
    /// Configuración de una tabla de interpolación del Gantry.
    /// Cada tabla tiene 4 variables PLC (plantillas) para los puntos de interpolación.
    /// El array st_Points va de 1 a 249, donde Line N usa:
    ///   - INICIO: índice (2N-1)
    ///   - FIN: índice (2N)
    /// Ejemplo: Line 1 → INICIO=st_Points[1], FIN=st_Points[2]
    ///          Line 2 → INICIO=st_Points[3], FIN=st_Points[4]
    /// </summary>
    public class GantryInterpolationTable
    {
        /// <summary>
        /// Identificador de la tabla (ej: TAB1_FW_UP, TAB2_BW_DOWN)
        /// </summary>
        public string TableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Índice de la tabla (0-7)
        /// </summary>
        public int TableIndex { get; set; }
        
        /// <summary>
        /// Variable PLC para el número de líneas habilitadas en esta tabla
        /// Columnas del Excel: BI=TAB1_FW_UP, BJ=TAB1_FW_DOWN, BK=TAB1_BW_UP, BL=TAB1_BW_DOWN,
        ///                    BM=TAB2_FW_UP, BN=TAB2_FW_DOWN, BO=TAB2_BW_UP, BP=TAB2_BW_DOWN
        /// </summary>
        public string LineCountPlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Variable PLC para min_height (Position_X del índice 1)
        /// Columnas del Excel: BQ=TAB1_FW_UP, BR=TAB1_FW_DOWN, BS=TAB1_BW_UP, BT=TAB1_BW_DOWN,
        ///                    BU=TAB2_FW_UP, BV=TAB2_FW_DOWN, BW=TAB2_BW_UP, BX=TAB2_BW_DOWN
        /// </summary>
        public string MinHeightPlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Variable PLC para max_height (Position_X del último índice habilitado)
        /// Columnas del Excel: BY=TAB1_FW_UP, BZ=TAB1_FW_DOWN, CA=TAB1_BW_UP, CB=TAB1_BW_DOWN,
        ///                    CC=TAB2_FW_UP, CD=TAB2_FW_DOWN, CE=TAB2_BW_UP, CF=TAB2_BW_DOWN
        /// </summary>
        public string MaxHeightPlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor actual del número de líneas habilitadas (leído del PLC)
        /// Mínimo 1 (siempre debe haber al menos una línea)
        /// </summary>
        public int LineCountValue { get; set; } = 1;
        
        /// <summary>
        /// Plantilla de variable PLC para FunctionType (Syncron)
        /// Columnas: AF, AJ, AN, AR, AV, AZ, BD, BH
        /// Formato: "MAIN.fbMachine.st_TrainRecipe[1].st_Points[{index}].FunctionType"
        /// </summary>
        public string FunctionTypePlcTemplate { get; set; } = string.Empty;
        
        /// <summary>
        /// Plantilla de variable PLC para Position_X (Posición Master)
        /// Columnas: AC, AG, AK, AO, AS, AW, BA, BE
        /// Formato: "MAIN.fbMachine.st_TrainRecipe[1].st_Points[{index}].Position_X"
        /// </summary>
        public string PositionXPlcTemplate { get; set; } = string.Empty;
        
        /// <summary>
        /// Plantilla de variable PLC para Position_Y (Posición Slave)
        /// Columnas: AD, AH, AL, AP, AT, AX, BB, BF
        /// Formato: "MAIN.fbMachine.st_TrainRecipe[1].st_Points[{index}].Position_Y"
        /// </summary>
        public string PositionYPlcTemplate { get; set; } = string.Empty;
        
        /// <summary>
        /// Plantilla de variable PLC para Speed_Y (Velocidad)
        /// Columnas: AE, AI, AM, AQ, AU, AY, BC, BG
        /// Formato: "MAIN.fbMachine.st_TrainRecipe[1].st_Points[{index}].Speed_Y"
        /// </summary>
        public string SpeedYPlcTemplate { get; set; } = string.Empty;
        
        /// <summary>
        /// Indica si la tabla está configurada (tiene las 4 variables definidas)
        /// </summary>
        public bool IsConfigured => 
            !string.IsNullOrEmpty(FunctionTypePlcTemplate) &&
            !string.IsNullOrEmpty(PositionXPlcTemplate) &&
            !string.IsNullOrEmpty(PositionYPlcTemplate) &&
            !string.IsNullOrEmpty(SpeedYPlcTemplate);
            
        /// <summary>
        /// Genera la variable PLC real reemplazando el placeholder con el índice del punto
        /// Soporta formatos: {index}, [], [ ], {}, { }
        /// </summary>
        public string GetFunctionTypePlcVariable(int index) => ReplaceIndexPlaceholder(FunctionTypePlcTemplate, index);
        public string GetPositionXPlcVariable(int index) => ReplaceIndexPlaceholder(PositionXPlcTemplate, index);
        public string GetPositionYPlcVariable(int index) => ReplaceIndexPlaceholder(PositionYPlcTemplate, index);
        public string GetSpeedYPlcVariable(int index) => ReplaceIndexPlaceholder(SpeedYPlcTemplate, index);
        
        /// <summary>
        /// Reemplaza diferentes formatos de placeholder con el índice real
        /// Mantiene los corchetes del array: st_Points[] -> st_Points[1]
        /// </summary>
        private static string ReplaceIndexPlaceholder(string template, int index)
        {
            if (string.IsNullOrEmpty(template)) return template;
            
            // Intentar diferentes formatos de placeholder
            // IMPORTANTE: Para formatos de array [], reemplazar con [index] (mantener corchetes)
            var result = template
                .Replace("{index}", index.ToString())    // Formato preferido: {index} -> 1
                .Replace("{ }", index.ToString())        // Formato alternativo: { } -> 1
                .Replace("{}", index.ToString())         // Formato compacto: {} -> 1
                .Replace("[ ]", $"[{index}]")            // Formato array con espacio: [ ] -> [1]
                .Replace("[]", $"[{index}]");            // Formato array vacío: [] -> [1]
            
            return result;
        }
        
        /// <summary>
        /// Calcula el índice del array para el punto INICIO de una línea (1-based)
        /// Line N → índice = 2N - 1
        /// </summary>
        public static int GetStartPointIndex(int lineNumber) => (lineNumber * 2) - 1;
        
        /// <summary>
        /// Calcula el índice del array para el punto FIN de una línea (1-based)
        /// Line N → índice = 2N
        /// </summary>
        public static int GetEndPointIndex(int lineNumber) => lineNumber * 2;
    }
    
    /// <summary>
    /// Punto de interpolación con sus 4 valores
    /// </summary>
    public class GantryInterpolationPoint
    {
        /// <summary>
        /// Índice del punto en el array st_Points (1-249)
        /// </summary>
        public int PointIndex { get; set; }
        
        /// <summary>
        /// Tipo de punto: "start" (INICIO) o "end" (FIN)
        /// </summary>
        public string PointType { get; set; } = "start";
        
        /// <summary>
        /// Número de línea (1-124)
        /// </summary>
        public int LineNumber { get; set; }
        
        /// <summary>
        /// FunctionType (Syncron): 0=FREE, 1=LINEAR, etc.
        /// </summary>
        public int FunctionType { get; set; }
        
        /// <summary>
        /// Position_X (Posición Master) en mm
        /// </summary>
        public double PositionX { get; set; }
        
        /// <summary>
        /// Position_Y (Posición Slave) en mm
        /// </summary>
        public double PositionY { get; set; }
        
        /// <summary>
        /// Speed_Y (Velocidad) en mm/s
        /// </summary>
        public double SpeedY { get; set; }
        
        /// <summary>
        /// Indica si el punto está habilitado
        /// </summary>
        public bool Enabled { get; set; }
    }
    
    /// <summary>
    /// Línea de interpolación completa (INICIO + FIN)
    /// </summary>
    public class GantryInterpolationLine
    {
        /// <summary>
        /// Número de línea (1-124)
        /// </summary>
        public int LineNumber { get; set; }
        
        /// <summary>
        /// Punto de inicio
        /// </summary>
        public GantryInterpolationPoint Start { get; set; } = new();
        
        /// <summary>
        /// Punto de fin
        /// </summary>
        public GantryInterpolationPoint End { get; set; } = new();
        
        /// <summary>
        /// Indica si la línea está habilitada
        /// </summary>
        public bool Enabled { get; set; }
    }
    
    /// <summary>
    /// Parámetro de receta de tren (BOOL o DECIMAL)
    /// Cada fila del Excel representa un parámetro
    /// </summary>
    public class TrainRecipeParameter
    {
        /// <summary>
        /// Índice del parámetro (0-based, corresponde a fila-2 del Excel)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Número de fila en el Excel (para referencia)
        /// </summary>
        public int RowIndex { get; set; }
        
        /// <summary>
        /// Nombre/descripción del parámetro (columna C para BOOL, G para DECIMAL)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Ruta de la imagen del parámetro (columna D para BOOL, H para DECIMAL)
        /// Relativa a Projects/{projectId}/config/Images/
        /// </summary>
        public string? Image { get; set; }
        
        /// <summary>
        /// Variable PLC (columna E para BOOL, I para DECIMAL)
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo de dato: "BOOL" o "LREAL"
        /// </summary>
        public string DataType { get; set; } = "BOOL";
        
        /// <summary>
        /// Índice de columna en el Excel (para referencia)
        /// </summary>
        public int ColumnIndex { get; set; }
        
        /// <summary>
        /// Valor mínimo (solo para DECIMAL, columna J)
        /// </summary>
        public double? MinValue { get; set; }
        
        /// <summary>
        /// Valor máximo (solo para DECIMAL, columna K)
        /// </summary>
        public double? MaxValue { get; set; }
        
        /// <summary>
        /// Número de decimales (solo para DECIMAL, columna L)
        /// </summary>
        public int? Decimals { get; set; }
        
        /// <summary>
        /// Unidad de medida (solo para DECIMAL, columna M)
        /// </summary>
        public string? Unit { get; set; }
        
        /// <summary>
        /// Valor actual BOOL (para parámetros booleanos)
        /// </summary>
        public bool BoolValue { get; set; }
        
        /// <summary>
        /// Valor actual DECIMAL (para parámetros decimales)
        /// </summary>
        public double DecimalValue { get; set; }
        
        /// <summary>
        /// Indica si el parámetro está configurado (tiene variable PLC)
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(PlcVariable);
    }
    
    // ========================================================================
    // DTOs para API
    // ========================================================================
    
    /// <summary>
    /// DTO para respuesta del endpoint GET /api/train-recipe/config
    /// </summary>
    public class TrainRecipeConfigResponse
    {
        public string TitleLabel { get; set; } = "NOMBRE TREN";
        public string? TrainNamePlcVariable { get; set; }
        public string? LineNumberPlcVariable { get; set; }
        public string TrainNameValue { get; set; } = string.Empty;
        public int LineNumberValue { get; set; }
        
        // Nombres de secciones (desde Excel B2, F2, N2) - vacío = ocultar botón
        public string SectionBoolName { get; set; } = string.Empty;
        public string SectionDecimalName { get; set; } = string.Empty;
        public string SectionGantryName { get; set; } = string.Empty;
        
        // URLs de imágenes de secciones (desde Excel D2, H2)
        public string? SectionBoolImageUrl { get; set; }
        public string? SectionDecimalImageUrl { get; set; }
        
        // Variable y valor del número de tablas del Gantry (desde Excel W2)
        public string? GantryTableCountPlcVariable { get; set; }
        public int GantryTableCountValue { get; set; } = 1;
        
        public List<TrainRecipeParamDto> BoolParameters { get; set; } = new();
        public List<TrainRecipeParamDto> DecimalParameters { get; set; } = new();
        public List<GantryConfigParamDto> GantryConfigParameters { get; set; } = new();
        public List<GantryInterpolationTableDto> GantryInterpolationTables { get; set; } = new();
        public DateTime LoadedAt { get; set; }
    }
    
    /// <summary>
    /// DTO de tabla de interpolación del Gantry para la API
    /// </summary>
    public class GantryInterpolationTableDto
    {
        public string TableId { get; set; } = string.Empty;
        public int TableIndex { get; set; }
        public string FunctionTypePlcTemplate { get; set; } = string.Empty;
        public string PositionXPlcTemplate { get; set; } = string.Empty;
        public string PositionYPlcTemplate { get; set; } = string.Empty;
        public string SpeedYPlcTemplate { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
    }
    
    /// <summary>
    /// DTO de parámetro de configuración del Gantry para la API
    /// </summary>
    public class GantryConfigParamDto
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public int Decimals { get; set; }
        public string? Unit { get; set; }
        public string? Visibility { get; set; }
        public double Value { get; set; }
        public bool IsConfigured { get; set; }
    }
    
    /// <summary>
    /// DTO de parámetro para la API
    /// </summary>
    public class TrainRecipeParamDto
    {
        public int Index { get; set; }
        public int RowIndex { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public string DataType { get; set; } = "BOOL";
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public int? Decimals { get; set; }
        public string? Unit { get; set; }
        public bool BoolValue { get; set; }
        public double DecimalValue { get; set; }
        public bool IsConfigured { get; set; }
    }
    
    /// <summary>
    /// Request para escribir parámetros al PLC
    /// POST /api/train-recipe/write-to-plc
    /// </summary>
    public class WriteTrainRecipeToPlcRequest
    {
        /// <summary>
        /// Variable PLC para escribir el nombre del tren
        /// </summary>
        public string? TrainNamePlcVariable { get; set; }
        
        /// <summary>
        /// Nuevo valor del nombre de tren a escribir al PLC
        /// </summary>
        public string? TrainNameValue { get; set; }
        
        /// <summary>
        /// Variable PLC para escribir el número de línea
        /// </summary>
        public string? LineNumberPlcVariable { get; set; }
        
        /// <summary>
        /// Número de línea a escribir al PLC
        /// </summary>
        public int? LineNumberValue { get; set; }
        
        /// <summary>
        /// Valores de parámetros booleanos a escribir
        /// </summary>
        public List<TrainRecipeBoolValueDto> BoolValues { get; set; } = new();
        
        /// <summary>
        /// Valores de parámetros decimales a escribir
        /// </summary>
        public List<TrainRecipeDecimalValueDto> DecimalValues { get; set; } = new();
        
        /// <summary>
        /// Variable PLC para escribir el número de tablas del Gantry (desde W2)
        /// </summary>
        public string? GantryTableCountPlcVariable { get; set; }
        
        /// <summary>
        /// Valor del número de tablas del Gantry (1 = 4 tablas, 2 = 8 tablas)
        /// </summary>
        public int? GantryTableCountValue { get; set; }
        
        /// <summary>
        /// Valores de parámetros de configuración del Gantry (columnas O-V del Excel)
        /// </summary>
        public List<GantryConfigValueDto> GantryConfigValues { get; set; } = new();
    }
    
    /// <summary>
    /// Valor de parámetro de configuración del Gantry para escribir al PLC
    /// </summary>
    public class GantryConfigValueDto
    {
        public string PlcVariable { get; set; } = string.Empty;
        public double Value { get; set; }
    }
    
    /// <summary>
    /// Valor de parámetro booleano para escribir al PLC
    /// </summary>
    public class TrainRecipeBoolValueDto
    {
        public string PlcVariable { get; set; } = string.Empty;
        public bool Value { get; set; }
    }
    
    /// <summary>
    /// Valor de parámetro decimal para escribir al PLC
    /// </summary>
    public class TrainRecipeDecimalValueDto
    {
        public string PlcVariable { get; set; } = string.Empty;
        public double Value { get; set; }
    }
    
    /// <summary>
    /// Respuesta de operación PLC (lectura/escritura)
    /// </summary>
    public class TrainRecipePlcOperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int ParametersProcessed { get; set; }
        public int ParametersFailed { get; set; }
        public List<string>? Errors { get; set; }
        public TrainRecipeConfigResponse? Data { get; set; }
    }
    
    // ========================================================================
    // DTOs para Tablas de Interpolación del Gantry
    // ========================================================================
    
    /// <summary>
    /// Request para leer puntos de interpolación de una tabla específica
    /// </summary>
    public class GantryInterpolationReadRequest
    {
        /// <summary>
        /// ID de la tabla (ej: TAB1_FW_UP)
        /// </summary>
        public string TableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Número de líneas a leer (por defecto 10)
        /// </summary>
        public int LineCount { get; set; } = 10;
    }
    
    /// <summary>
    /// Respuesta con los puntos de interpolación leídos
    /// </summary>
    public class GantryInterpolationReadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string TableId { get; set; } = string.Empty;
        public List<GantryInterpolationLineDto> Lines { get; set; } = new();
    }
    
    /// <summary>
    /// DTO de una línea de interpolación (INICIO + FIN)
    /// </summary>
    public class GantryInterpolationLineDto
    {
        public int LineNumber { get; set; }
        public bool Enabled { get; set; }
        public GantryInterpolationPointDto Start { get; set; } = new();
        public GantryInterpolationPointDto End { get; set; } = new();
    }
    
    /// <summary>
    /// DTO de un punto de interpolación
    /// </summary>
    public class GantryInterpolationPointDto
    {
        public int PointIndex { get; set; }
        public string PointType { get; set; } = "start";
        public int FunctionType { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double SpeedY { get; set; }
    }
    
    /// <summary>
    /// Request para escribir puntos de interpolación a una tabla específica
    /// </summary>
    public class GantryInterpolationWriteRequest
    {
        /// <summary>
        /// ID de la tabla (ej: TAB1_FW_UP)
        /// </summary>
        public string TableId { get; set; } = string.Empty;
        
        /// <summary>
        /// Líneas de interpolación a escribir
        /// </summary>
        public List<GantryInterpolationLineDto> Lines { get; set; } = new();
    }
    
    /// <summary>
    /// Respuesta de escritura de puntos de interpolación
    /// </summary>
    public class GantryInterpolationWriteResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string TableId { get; set; } = string.Empty;
        public int PointsWritten { get; set; }
        public int PointsFailed { get; set; }
        public List<string>? Errors { get; set; }
    }
    
    // ========================================================================
    // Modelos Legacy para TrainTypesController (compatibilidad)
    // ========================================================================
    
    /// <summary>
    /// Request para escribir receta de tren al PLC (formato legacy)
    /// Usado por TrainTypesController.WriteRecipeToPlc
    /// </summary>
    public class TrainRecipeWriteRequest
    {
        /// <summary>
        /// Número de slot/línea (formato legacy)
        /// </summary>
        public int? SlotNumber { get; set; }
        
        /// <summary>
        /// Número de línea (formato nuevo desde frontend)
        /// </summary>
        public int? LineNumberValue { get; set; }
        
        /// <summary>
        /// Obtiene el número de slot/línea efectivo
        /// </summary>
        public int? EffectiveSlotNumber => SlotNumber ?? LineNumberValue;

        /// <summary>
        /// Nombre del tren/receta (formato legacy)
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Nombre del tren (formato nuevo desde frontend)
        /// </summary>
        public string? TrainNameValue { get; set; }
        
        /// <summary>
        /// Obtiene el nombre efectivo
        /// </summary>
        public string? EffectiveName => Name ?? TrainNameValue;

        /// <summary>
        /// Valores booleanos a escribir
        /// </summary>
        public List<TrainRecipeBoolValue>? BoolValues { get; set; }

        /// <summary>
        /// Valores decimales a escribir
        /// </summary>
        public List<TrainRecipeDecimalValue>? DecimalValues { get; set; }
    }

    /// <summary>
    /// Valor booleano para escribir al PLC (formato legacy)
    /// </summary>
    public class TrainRecipeBoolValue
    {
        public string? PlcVariable { get; set; }
        public bool Value { get; set; }
    }

    /// <summary>
    /// Valor decimal para escribir al PLC (formato legacy)
    /// </summary>
    public class TrainRecipeDecimalValue
    {
        public string? PlcVariable { get; set; }
        public double Value { get; set; }
    }
}
