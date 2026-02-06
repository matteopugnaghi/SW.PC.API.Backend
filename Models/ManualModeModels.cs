// ============================================================================
// ManualModeModels.cs - Modelos para Modo Manual/Mantenimiento
// ============================================================================
// Definiciones de elementos controlables manualmente (bombas, motores, etc.)
// Configurados desde Excel en la hoja "Manual"
// Estructura Excel:
//   A2: Título de la vista
//   B2+: Descripción del elemento
//   C2+: Imagen del elemento
//   D2+: Variable PLC (BOOL) para activar/desactivar
// ============================================================================

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Configuración completa del modo manual desde Excel hoja "Manual"
    /// </summary>
    public class ManualModeConfiguration
    {
        /// <summary>
        /// Título de la vista (leído de A2)
        /// </summary>
        public string ViewTitle { get; set; } = "MODO MANUAL";

        /// <summary>
        /// Lista de elementos controlables manualmente
        /// </summary>
        public List<ManualModeElement> Elements { get; set; } = new();
    }

    /// <summary>
    /// Elemento individual controlable en modo manual
    /// </summary>
    public class ManualModeElement
    {
        /// <summary>
        /// ID único del elemento (generado: manual_{sanitizedName}_{index})
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del elemento (ej: "Bomba Principal", "Motor Cepillo 1")
        /// Leído de columna B
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Ruta de la imagen del elemento (para identificación visual)
        /// Leído de columna C
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// Variable PLC BOOL para activar/desactivar el elemento
        /// Leído de columna D
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Estado actual del elemento (true = activo/ON, false = inactivo/OFF)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Orden de visualización (basado en fila del Excel)
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Índice de fila en el Excel (para referencia)
        /// </summary>
        public int RowIndex { get; set; }
    }

    /// <summary>
    /// Configuración leída directamente del Excel (estructura interna)
    /// </summary>
    public class ManualPageExcelConfiguration
    {
        /// <summary>
        /// Título de la vista desde A2
        /// </summary>
        public string ViewTitle { get; set; } = "MODO MANUAL";

        /// <summary>
        /// Elementos del modo manual
        /// </summary>
        public List<ManualElementSetting> Elements { get; set; } = new();
    }

    /// <summary>
    /// Configuración de un elemento desde Excel (estructura interna)
    /// </summary>
    public class ManualElementSetting
    {
        public string Description { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string PlcVariable { get; set; } = string.Empty;
        public int RowIndex { get; set; }
    }

    /// <summary>
    /// Request para activar/desactivar un elemento manual
    /// </summary>
    public class ManualModeToggleRequest
    {
        /// <summary>
        /// ID del elemento a modificar
        /// </summary>
        public string ElementId { get; set; } = string.Empty;

        /// <summary>
        /// Variable PLC a escribir
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Nuevo estado (true = activar, false = desactivar)
        /// </summary>
        public bool Value { get; set; }

        /// <summary>
        /// Descripción traducida del elemento (para logs)
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Respuesta de lectura de estados actuales
    /// </summary>
    public class ManualModeStatesResponse
    {
        /// <summary>
        /// Diccionario de estados: ElementId -> Estado actual
        /// </summary>
        public Dictionary<string, bool> States { get; set; } = new();

        /// <summary>
        /// Timestamp de la lectura
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request para activar/desactivar un elemento en modo semiautomático
    /// </summary>
    public class SemiautomaticToggleRequest
    {
        /// <summary>
        /// ID del elemento (opcional, para descripción en logs)
        /// </summary>
        public string? ElementId { get; set; }

        /// <summary>
        /// Variable PLC a escribir
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Nuevo estado (true = activar, false = desactivar)
        /// </summary>
        public bool Value { get; set; }

        /// <summary>
        /// Clave de traducción para la descripción (se traduce en el frontend)
        /// </summary>
        public string? Description { get; set; }
    }
}
