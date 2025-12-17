// ============================================================================
// WashRecipeModels.cs - Modelos para Editor de Recetas de Lavado
// ============================================================================
// Configuración de estaciones de lavado leídas desde Excel hoja "WashRecipe"
// Cada estación tiene: imagen, nombre, 10 parámetros BOOL, 10 parámetros INT
// ============================================================================

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Configuración completa del editor de recetas de lavado
    /// Leída desde la hoja "WashRecipe" del Excel
    /// </summary>
    public class WashRecipeEditorConfiguration
    {
        /// <summary>
        /// Descripción/nombre del tipo de lavado (desde A2)
        /// </summary>
        public string RecipeNameDescription { get; set; } = "Nombre de la Receta";
        
        /// <summary>
        /// Variable PLC para el nombre de la receta (desde A3)
        /// Esta variable contiene el nombre activo que se muestra en la lista
        /// </summary>
        public string? RecipeNamePlcVariable { get; set; }
        
        /// <summary>
        /// Variable PLC para la línea/número de receta (desde A4)
        /// Esta variable contiene el índice de la receta seleccionada
        /// </summary>
        public string? RecipeLineNumberPlcVariable { get; set; }
        
        /// <summary>
        /// Valor actual del nombre de receta leído del PLC
        /// </summary>Vale vall
        public string RecipeNameValue { get; set; } = string.Empty;
        
        /// <summary>
        /// Habilitar escritura alternativa al PLC (desde A13: ON/OFF)
        /// Si es true, se muestra un segundo botón "ESCRIBIR PLC 2"
        /// </summary>
        public bool AlternateWriteEnabled { get; set; } = false;
        
        /// <summary>
        /// Prefijo PLC alternativo para escritura (desde A14)
        /// Ej: "st_WashPreview" - Se sustituye "st_WashRecipe" por este valor
        /// </summary>
        public string? AlternateWritePlcPrefix { get; set; }
        
        /// <summary>
        /// Lista de estaciones configuradas (una por fila del Excel)
        /// </summary>
        public List<WashRecipeStation> Stations { get; set; } = new();
        
        /// <summary>
        /// Fecha de última carga de configuración
        /// </summary>
        public DateTime LoadedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Estación de lavado con sus parámetros
    /// Cada fila del Excel (2, 3, 4...) representa una estación
    /// </summary>
    public class WashRecipeStation
    {
        /// <summary>
        /// Índice de la estación (0-based, corresponde a fila-2 del Excel)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Número de fila en el Excel (para referencia)
        /// </summary>
        public int ExcelRow { get; set; }
        
        /// <summary>
        /// Nombre/descripción de la estación (columna B)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Ruta de la imagen de la estación (columna C)
        /// Relativa a Projects/{projectId}/config/Images/
        /// </summary>
        public string? ImagePath { get; set; }
        
        /// <summary>
        /// Parámetros booleanos (switches) - 10 máximo
        /// Columnas D-E, F-G, H-I... hasta V-W (pares Variable/Descripción)
        /// </summary>
        public List<WashRecipeBoolParam> BoolParameters { get; set; } = new();
        
        /// <summary>
        /// Parámetros enteros (textbox) - 10 máximo
        /// Columnas X-Y, Z-AA, AB-AC... hasta AP-AQ (pares Variable/Descripción)
        /// </summary>
        public List<WashRecipeIntParam> IntParameters { get; set; } = new();
    }
    
    /// <summary>
    /// Parámetro booleano para receta de lavado (Switch ON/OFF)
    /// </summary>
    public class WashRecipeBoolParam
    {
        /// <summary>
        /// Índice del parámetro (0-9)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Variable PLC (ej: MAIN.fbWash.bParam1)
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Descripción para mostrar al operador
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor actual (leído del PLC o establecido por el operador)
        /// </summary>
        public bool Value { get; set; }
        
        /// <summary>
        /// Indica si el parámetro está configurado (tiene variable PLC)
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(PlcVariable);
    }
    
    /// <summary>
    /// Parámetro entero para receta de lavado (Textbox numérico)
    /// </summary>
    public class WashRecipeIntParam
    {
        /// <summary>
        /// Índice del parámetro (0-9)
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Variable PLC (ej: MAIN.fbWash.nTime1)
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;
        
        /// <summary>
        /// Descripción para mostrar al operador
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor actual (leído del PLC o establecido por el operador)
        /// </summary>
        public int Value { get; set; }
        
        /// <summary>
        /// Valor mínimo (opcional, para validación UI)
        /// </summary>
        public int? MinValue { get; set; }
        
        /// <summary>
        /// Valor máximo (opcional, para validación UI)
        /// </summary>
        public int? MaxValue { get; set; }
        
        /// <summary>
        /// Unidad de medida (opcional, ej: "seg", "°C", "bar")
        /// </summary>
        public string? Unit { get; set; }
        
        /// <summary>
        /// Indica si el parámetro está configurado (tiene variable PLC)
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(PlcVariable);
    }
    
    // ========================================================================
    // DTOs para API
    // ========================================================================
    
    /// <summary>
    /// DTO para respuesta del endpoint GET /api/wash-recipe/config
    /// </summary>
    public class WashRecipeConfigResponse
    {
        public string RecipeNameDescription { get; set; } = string.Empty;
        /// <summary>
        /// Variable PLC para leer/escribir el nombre de la receta activa (desde A3)
        /// </summary>
        public string? RecipeNamePlcVariable { get; set; }
        /// <summary>
        /// Valor actual del nombre de receta leído del PLC
        /// </summary>
        public string RecipeNameValue { get; set; } = string.Empty;
        
        /// <summary>
        /// Habilitar escritura alternativa al PLC (desde A13: ON/OFF)
        /// </summary>
        public bool AlternateWriteEnabled { get; set; } = false;
        
        /// <summary>
        /// Prefijo PLC alternativo para escritura (desde A14)
        /// </summary>
        public string? AlternateWritePlcPrefix { get; set; }
        
        /// <summary>
        /// Valor actual del nombre de receta alternativa leído del PLC (usando A14 prefix)
        /// </summary>
        public string? AlternateRecipeNameValue { get; set; }
        
        public List<WashRecipeStationDto> Stations { get; set; } = new();
        public DateTime LoadedAt { get; set; }
    }
    
    /// <summary>
    /// DTO de estación para la API
    /// </summary>
    public class WashRecipeStationDto
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<WashRecipeBoolParamDto> BoolParameters { get; set; } = new();
        public List<WashRecipeIntParamDto> IntParameters { get; set; } = new();
    }
    
    /// <summary>
    /// DTO de parámetro booleano para la API
    /// </summary>
    public class WashRecipeBoolParamDto
    {
        public int Index { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Value { get; set; }
        public bool IsConfigured { get; set; }
    }
    
    /// <summary>
    /// DTO de parámetro entero para la API
    /// </summary>
    public class WashRecipeIntParamDto
    {
        public int Index { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Value { get; set; }
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
        public string? Unit { get; set; }
        public bool IsConfigured { get; set; }
    }
    
    /// <summary>
    /// Request para escribir todos los parámetros al PLC
    /// POST /api/wash-recipe/write-to-plc
    /// </summary>
    public class WriteWashRecipeToPlcRequest
    {
        public string? RecipeName { get; set; }
        /// <summary>
        /// Variable PLC para escribir el nombre de la receta
        /// </summary>
        public string? RecipeNamePlcVariable { get; set; }
        /// <summary>
        /// Nuevo valor del nombre de receta a escribir al PLC
        /// </summary>
        public string? RecipeNameValue { get; set; }
        public List<WashRecipeStationValuesDto> Stations { get; set; } = new();
    }
    
    /// <summary>
    /// Valores de una estación para escribir al PLC
    /// </summary>
    public class WashRecipeStationValuesDto
    {
        public int StationIndex { get; set; }
        public List<BoolParamValueDto> BoolValues { get; set; } = new();
        public List<IntParamValueDto> IntValues { get; set; } = new();
    }
    
    public class BoolParamValueDto
    {
        public string PlcVariable { get; set; } = string.Empty;
        public bool Value { get; set; }
    }
    
    public class IntParamValueDto
    {
        public string PlcVariable { get; set; } = string.Empty;
        public int Value { get; set; }
    }
    
    /// <summary>
    /// Respuesta de lectura/escritura PLC
    /// </summary>
    public class WashRecipePlcOperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int ParametersProcessed { get; set; }
        public int ParametersFailed { get; set; }
        public List<string>? Errors { get; set; }
        public WashRecipeConfigResponse? Data { get; set; }
    }
}
