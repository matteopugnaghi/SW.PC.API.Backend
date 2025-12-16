// ============================================================================
// WashTypeModels.cs - Modelos para Tipos de Lavados (Recetas de Lavado)
// ============================================================================
// Definiciones de tipos de lavado que el operador puede seleccionar.
// - Lista de tipos de lavado disponibles
// - Cada tipo tiene parámetros configurables (temperatura, presión, tiempos, etc.)
// - El editor permite modificar parámetros desde configuración Excel
// - Los tipos se guardan en DB y se pueden escribir al PLC
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Tipo de lavado (receta de lavado) que el operador puede seleccionar
    /// </summary>
    public class WashType
    {
        /// <summary>
        /// ID único del tipo de lavado
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Código único del tipo de lavado (ej: "WASH_STANDARD", "WASH_EXPRESS")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo del tipo de lavado
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del tipo de lavado
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Icono o imagen representativa (ruta o emoji)
        /// </summary>
        [MaxLength(100)]
        public string? Icon { get; set; }

        /// <summary>
        /// Color del tipo de lavado para UI (hex: #RRGGBB)
        /// </summary>
        [MaxLength(10)]
        public string? Color { get; set; }

        /// <summary>
        /// Indica si este tipo de lavado está activo/disponible
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indica si es el tipo de lavado por defecto
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Orden de visualización en la lista
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
        /// Parámetros de la receta de lavado (valores configurables)
        /// </summary>
        public virtual ICollection<WashTypeParameter> Parameters { get; set; } = new List<WashTypeParameter>();
    }

    /// <summary>
    /// Parámetro de un tipo de lavado (valor configurable de la receta)
    /// </summary>
    public class WashTypeParameter
    {
        /// <summary>
        /// ID único del parámetro
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del tipo de lavado al que pertenece
        /// </summary>
        public int WashTypeId { get; set; }

        /// <summary>
        /// Código del parámetro (ej: "TEMPERATURE", "PRESSURE", "TIME")
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
        /// Tipo de dato: BOOL, INT, LREAL, STRING
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
        /// Valor mínimo permitido
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Valor máximo permitido
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Unidad de medida (ej: "°C", "bar", "seg", "mm")
        /// </summary>
        [MaxLength(20)]
        public string? Unit { get; set; }

        /// <summary>
        /// Variable PLC asociada para escritura
        /// </summary>
        [MaxLength(200)]
        public string? PlcVariable { get; set; }

        /// <summary>
        /// Orden de visualización en el editor
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Indica si el parámetro es editable por el usuario
        /// </summary>
        public bool IsEditable { get; set; } = true;

        /// <summary>
        /// Relación con el tipo de lavado padre
        /// </summary>
        [ForeignKey("WashTypeId")]
        public virtual WashType? WashType { get; set; }
    }

    /// <summary>
    /// Tipo de lavado actualmente seleccionado (para persistir la selección del operador)
    /// </summary>
    public class ActiveWashType
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del tipo de lavado seleccionado
        /// </summary>
        public int WashTypeId { get; set; }

        /// <summary>
        /// Fecha de selección
        /// </summary>
        public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Usuario que seleccionó el tipo
        /// </summary>
        [MaxLength(100)]
        public string? SelectedBy { get; set; }

        /// <summary>
        /// Indica si ya fue escrito al PLC
        /// </summary>
        public bool WrittenToPlc { get; set; } = false;

        /// <summary>
        /// Fecha en que se escribió al PLC
        /// </summary>
        public DateTime? WrittenToPlcAt { get; set; }

        /// <summary>
        /// Relación con el tipo de lavado
        /// </summary>
        [ForeignKey("WashTypeId")]
        public virtual WashType? WashType { get; set; }
    }

    // ============================================================================
    // DTOs para API
    // ============================================================================

    /// <summary>
    /// DTO para listar tipos de lavado (vista simplificada)
    /// </summary>
    public class WashTypeListDto
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
    /// DTO para detalle completo de un tipo de lavado
    /// </summary>
    public class WashTypeDetailDto
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
        public List<WashTypeParameterDto> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DTO para parámetros de tipo de lavado
    /// </summary>
    public class WashTypeParameterDto
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
        public bool IsEditable { get; set; }
    }

    /// <summary>
    /// DTO para crear/actualizar tipo de lavado
    /// </summary>
    public class WashTypeCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Icon { get; set; }

        [MaxLength(10)]
        public string? Color { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;

        public List<WashTypeParameterCreateDto>? Parameters { get; set; }
    }

    /// <summary>
    /// DTO para crear/actualizar parámetros de tipo de lavado
    /// </summary>
    public class WashTypeParameterCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string ParameterCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string DataType { get; set; } = "LREAL";

        [MaxLength(200)]
        public string? Value { get; set; }

        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }

        [MaxLength(20)]
        public string? Unit { get; set; }

        [MaxLength(200)]
        public string? PlcVariable { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsEditable { get; set; } = true;
    }

    /// <summary>
    /// DTO para seleccionar un tipo de lavado
    /// </summary>
    public class SelectWashTypeDto
    {
        [Required]
        public int WashTypeId { get; set; }

        /// <summary>
        /// Si es true, también escribe al PLC inmediatamente
        /// </summary>
        public bool WriteToPlc { get; set; } = false;
    }

    /// <summary>
    /// DTO para respuesta de estado activo
    /// </summary>
    public class ActiveWashTypeDto
    {
        public int? WashTypeId { get; set; }
        public string? WashTypeCode { get; set; }
        public string? WashTypeName { get; set; }
        public DateTime? SelectedAt { get; set; }
        public string? SelectedBy { get; set; }
        public bool WrittenToPlc { get; set; }
        public DateTime? WrittenToPlcAt { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de escritura a PLC
    /// </summary>
    public class WriteToPlcResponseDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int ParametersWritten { get; set; }
        public List<string>? Errors { get; set; }
        public DateTime WrittenAt { get; set; }
    }
}
