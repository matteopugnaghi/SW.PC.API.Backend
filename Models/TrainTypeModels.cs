// ============================================================================
// TrainTypeModels.cs - Modelos para Tipos de Trenes (Recetas de Tren)
// ============================================================================
// Definiciones de tipos de tren que el operador puede seleccionar.
// - Lista de tipos de tren disponibles
// - Cada tipo tiene parámetros configurables (bool y decimal)
// - El editor permite modificar parámetros desde configuración Excel
// - Los tipos se guardan en DB y se pueden escribir al PLC
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Tipo de tren (receta de tren) que el operador puede seleccionar
    /// </summary>
    public class TrainType
    {
        /// <summary>
        /// ID único del tipo de tren
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Código único del tipo de tren (ej: "TRAIN_METRO", "TRAIN_HIGH_SPEED")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo del tipo de tren
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del tipo de tren
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Icono o imagen representativa (ruta o emoji)
        /// </summary>
        [MaxLength(100)]
        public string? Icon { get; set; }

        /// <summary>
        /// Color del tipo de tren para UI (hex: #RRGGBB)
        /// </summary>
        [MaxLength(10)]
        public string? Color { get; set; }

        /// <summary>
        /// Indica si este tipo de tren está activo/disponible
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indica si es el tipo de tren por defecto
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Orden de visualización en la lista (número de línea)
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Usuario que creó el registro
        /// </summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Usuario que realizó la última modificación
        /// </summary>
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Parámetros de la receta de tren (valores configurables)
        /// </summary>
        public virtual ICollection<TrainTypeParameter> Parameters { get; set; } = new List<TrainTypeParameter>();
    }

    /// <summary>
    /// Parámetro de un tipo de tren (valor configurable de la receta)
    /// </summary>
    public class TrainTypeParameter
    {
        /// <summary>
        /// ID único del parámetro
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del tipo de tren al que pertenece
        /// </summary>
        public int TrainTypeId { get; set; }

        /// <summary>
        /// Código del parámetro (ej: "ENABLED", "LENGTH", "WEIGHT")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ParameterCode { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo del parámetro
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de dato: BOOL, LREAL (decimal)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string DataType { get; set; } = "LREAL";

        /// <summary>
        /// Valor actual del parámetro (serializado como string)
        /// </summary>
        [MaxLength(200)]
        public string? Value { get; set; }

        /// <summary>
        /// Valor mínimo permitido (para decimales)
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Valor máximo permitido (para decimales)
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Unidad de medida (ej: "m", "kg", "°C")
        /// </summary>
        [MaxLength(20)]
        public string? Unit { get; set; }

        /// <summary>
        /// Variable PLC asociada al parámetro
        /// </summary>
        [MaxLength(200)]
        public string? PlcVariable { get; set; }

        /// <summary>
        /// Orden de visualización del parámetro
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Grupo al que pertenece el parámetro (ej: "Bool", "Decimal")
        /// </summary>
        [MaxLength(50)]
        public string? GroupName { get; set; }

        /// <summary>
        /// Navegación al tipo de tren padre
        /// </summary>
        [ForeignKey("TrainTypeId")]
        public virtual TrainType? TrainType { get; set; }
    }

    // ========================================================================
    // DTOs para API
    // ========================================================================

    /// <summary>
    /// DTO para listado de tipos de tren
    /// </summary>
    public class TrainTypeListDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public int DisplayOrder { get; set; }
        public int ParameterCount { get; set; }
    }

    /// <summary>
    /// DTO para detalle de tipo de tren (incluye parámetros)
    /// </summary>
    public class TrainTypeDetailDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public List<TrainTypeParameterDto> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DTO para parámetro de tipo de tren
    /// </summary>
    public class TrainTypeParameterDto
    {
        public int Id { get; set; }
        public string ParameterCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = "LREAL";
        public string? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? Unit { get; set; }
        public string? PlcVariable { get; set; }
        public int DisplayOrder { get; set; }
        public string? GroupName { get; set; }
    }

    /// <summary>
    /// DTO para crear tipo de tren
    /// </summary>
    public class TrainTypeCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;

        public List<TrainTypeParameterCreateDto>? Parameters { get; set; }
    }

    /// <summary>
    /// DTO para crear parámetro de tipo de tren
    /// </summary>
    public class TrainTypeParameterCreateDto
    {
        [Required]
        public string ParameterCode { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string DataType { get; set; } = "LREAL";
        public string? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? Unit { get; set; }
        public string? PlcVariable { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public string? GroupName { get; set; }
    }

    /// <summary>
    /// DTO para actualizar tipo de tren
    /// </summary>
    public class TrainTypeUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDefault { get; set; }
        public int? DisplayOrder { get; set; }

        public List<TrainTypeParameterUpdateDto>? Parameters { get; set; }
    }

    /// <summary>
    /// DTO para actualizar parámetro de tipo de tren
    /// </summary>
    public class TrainTypeParameterUpdateDto
    {
        public int? Id { get; set; }
        public string ParameterCode { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? Unit { get; set; }
        public string? PlcVariable { get; set; }
        public int? DisplayOrder { get; set; }
        public string? GroupName { get; set; }
    }

    // ========================================================================
    // Active Train Type (Tipo de Tren Activo)
    // ========================================================================

    /// <summary>
    /// Tipo de tren actualmente seleccionado por el operador
    /// </summary>
    public class ActiveTrainType
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del tipo de tren seleccionado
        /// </summary>
        public int TrainTypeId { get; set; }

        /// <summary>
        /// Fecha/hora de selección
        /// </summary>
        public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Usuario que realizó la selección
        /// </summary>
        [MaxLength(100)]
        public string? SelectedBy { get; set; }

        /// <summary>
        /// Indica si ya se escribió al PLC
        /// </summary>
        public bool WrittenToPlc { get; set; } = false;

        /// <summary>
        /// Fecha/hora de escritura al PLC
        /// </summary>
        public DateTime? WrittenToPlcAt { get; set; }

        /// <summary>
        /// Navegación al tipo de tren
        /// </summary>
        [ForeignKey("TrainTypeId")]
        public virtual TrainType? TrainType { get; set; }
    }

    /// <summary>
    /// DTO para tipo de tren activo
    /// </summary>
    public class ActiveTrainTypeDto
    {
        public int? TrainTypeId { get; set; }
        public string? TrainTypeCode { get; set; }
        public string? TrainTypeName { get; set; }
        public DateTime? SelectedAt { get; set; }
        public string? SelectedBy { get; set; }
        public bool WrittenToPlc { get; set; }
        public DateTime? WrittenToPlcAt { get; set; }
    }

    /// <summary>
    /// DTO para seleccionar un tipo de tren
    /// </summary>
    public class SelectTrainTypeDto
    {
        [Required]
        public int TrainTypeId { get; set; }
        
        /// <summary>
        /// Si es true, también escribe al PLC después de seleccionar
        /// </summary>
        public bool WriteToPlc { get; set; } = false;
    }

    // ========================================================================
    // Clases para lectura de recetas desde PLC
    // ========================================================================

    /// <summary>
    /// Datos de receta de tren leídos del PLC
    /// </summary>
    public class PlcTrainRecipeData
    {
        public string? RecipeName { get; set; }
        public List<PlcTrainParameterData> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Datos de un parámetro de tren leído del PLC
    /// </summary>
    public class PlcTrainParameterData
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public string Value { get; set; } = "";
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? Unit { get; set; }
        public string? PlcVariable { get; set; }
        public int DisplayOrder { get; set; }
    }

    // Nota: Las clases TrainRecipeConfiguration, TrainRecipeParameter y DTOs relacionados
    // se han movido a TrainRecipeModels.cs para evitar duplicación
}
