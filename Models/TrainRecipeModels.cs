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
        /// Lista de parámetros booleanos (filas del Excel columnas C-E)
        /// </summary>
        public List<TrainRecipeParameter> BoolParameters { get; set; } = new();
        
        /// <summary>
        /// Lista de parámetros decimales (filas del Excel columnas G-M)
        /// </summary>
        public List<TrainRecipeParameter> DecimalParameters { get; set; } = new();
        
        /// <summary>
        /// Fecha de última carga de configuración
        /// </summary>
        public DateTime LoadedAt { get; set; } = DateTime.Now;
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
        public List<TrainRecipeParamDto> BoolParameters { get; set; } = new();
        public List<TrainRecipeParamDto> DecimalParameters { get; set; } = new();
        public DateTime LoadedAt { get; set; }
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
    // Modelos Legacy para TrainTypesController (compatibilidad)
    // ========================================================================
    
    /// <summary>
    /// Request para escribir receta de tren al PLC (formato legacy)
    /// Usado por TrainTypesController.WriteRecipeToPlc
    /// </summary>
    public class TrainRecipeWriteRequest
    {
        /// <summary>
        /// Número de slot/línea
        /// </summary>
        public int? SlotNumber { get; set; }

        /// <summary>
        /// Nombre del tren/receta
        /// </summary>
        public string? Name { get; set; }

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
