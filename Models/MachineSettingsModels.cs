// ============================================================================
// MachineSettingsModels.cs - Modelos para Parámetros de Configuración de Máquina
// ============================================================================
// Definiciones de parámetros configurables de la máquina que se leen/escriben
// desde/hacia el PLC y la base de datos (memoria).
// Configurados desde Excel en la hoja "setting page"
// ============================================================================

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Tipo de parámetro de configuración
    /// </summary>
    public enum SettingParameterType
    {
        Bool,
        Int,
        LongReal  // LREAL en TwinCAT = double en C#
    }

    /// <summary>
    /// Parámetro de configuración de máquina (base)
    /// Leído desde Excel hoja "setting page"
    /// </summary>
    public class SettingParameter
    {
        /// <summary>
        /// ID único del parámetro (usado como clave de traducción i18n)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del parámetro (puede ser en español o ID para traducción)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de dato: Bool, Int, LongReal
        /// </summary>
        public SettingParameterType Type { get; set; }

        /// <summary>
        /// Ruta de la imagen de ayuda (foto del elemento para el operador)
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// Variable PLC asociada (ej: MAIN.fbMachine.st_GenericConfiguration.b_param[1])
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Valor actual del parámetro (deserializado según Type)
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Orden de visualización en la UI
        /// </summary>
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Parámetro booleano de configuración
    /// Excel: Columnas B (Nombre), C (Imagen), D (Variable PLC)
    /// </summary>
    public class BoolSettingParameter : SettingParameter
    {
        public BoolSettingParameter()
        {
            Type = SettingParameterType.Bool;
        }

        /// <summary>
        /// Valor booleano tipado
        /// </summary>
        public new bool Value { get; set; }
    }

    /// <summary>
    /// Parámetro entero de configuración
    /// Excel: Columnas F (Nombre), G (Imagen), H (Variable PLC)
    /// </summary>
    public class IntSettingParameter : SettingParameter
    {
        public IntSettingParameter()
        {
            Type = SettingParameterType.Int;
        }

        /// <summary>
        /// Valor entero tipado
        /// </summary>
        public new int Value { get; set; }

        /// <summary>
        /// Valor mínimo permitido (opcional)
        /// </summary>
        public int? MinValue { get; set; }

        /// <summary>
        /// Valor máximo permitido (opcional)
        /// </summary>
        public int? MaxValue { get; set; }

        /// <summary>
        /// Unidad de medida (opcional, ej: "mm", "seg", etc.)
        /// </summary>
        public string? Unit { get; set; }
    }

    /// <summary>
    /// Parámetro LongReal (LREAL/double) de configuración
    /// Excel: Columnas J (Nombre), K (Imagen), L (Variable PLC)
    /// </summary>
    public class LongRealSettingParameter : SettingParameter
    {
        public LongRealSettingParameter()
        {
            Type = SettingParameterType.LongReal;
        }

        /// <summary>
        /// Valor double tipado
        /// </summary>
        public new double Value { get; set; }

        /// <summary>
        /// Valor mínimo permitido (opcional)
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Valor máximo permitido (opcional)
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Número de decimales a mostrar
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>
        /// Unidad de medida (opcional, ej: "mm", "m/s", etc.)
        /// </summary>
        public string? Unit { get; set; }
    }

    /// <summary>
    /// Configuración completa de settings de máquina desde Excel
    /// </summary>
    public class MachineSettingsConfiguration
    {
        /// <summary>
        /// Título de la sección de booleanos (desde Excel A2)
        /// </summary>
        public string BoolSectionTitle { get; set; } = "Booleanos";

        /// <summary>
        /// Título de la sección de enteros (desde Excel E2)
        /// </summary>
        public string IntSectionTitle { get; set; } = "Enteros";

        /// <summary>
        /// Título de la sección de decimales (desde Excel L2)
        /// </summary>
        public string LongRealSectionTitle { get; set; } = "Decimales";

        /// <summary>
        /// Título de la segunda sección de decimales (desde Excel T2)
        /// </summary>
        public string LongReal2SectionTitle { get; set; } = "Decimales 2";

        /// <summary>
        /// Lista de parámetros booleanos
        /// </summary>
        public List<BoolSettingParameter> BoolParameters { get; set; } = new();

        /// <summary>
        /// Lista de parámetros enteros
        /// </summary>
        public List<IntSettingParameter> IntParameters { get; set; } = new();

        /// <summary>
        /// Lista de parámetros LREAL (double)
        /// </summary>
        public List<LongRealSettingParameter> LongRealParameters { get; set; } = new();

        /// <summary>
        /// Lista de parámetros LREAL segunda sección (double)
        /// </summary>
        public List<LongRealSettingParameter> LongReal2Parameters { get; set; } = new();

        /// <summary>
        /// Total de parámetros configurados
        /// </summary>
        public int TotalCount => BoolParameters.Count + IntParameters.Count + LongRealParameters.Count + LongReal2Parameters.Count;
    }

    /// <summary>
    /// DTO para actualizar un parámetro (request del frontend)
    /// </summary>
    public class SettingParameterUpdateRequest
    {
        /// <summary>
        /// ID del parámetro
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Variable PLC asociada
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de parámetro
        /// </summary>
        public SettingParameterType Type { get; set; }

        /// <summary>
        /// Nuevo valor (serializado como string para JSON)
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para respuesta con valores de todos los parámetros
    /// </summary>
    public class MachineSettingsValuesResponse
    {
        /// <summary>
        /// Valores de parámetros booleanos (Id -> Value)
        /// </summary>
        public Dictionary<string, bool> BoolValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros enteros (Id -> Value)
        /// </summary>
        public Dictionary<string, int> IntValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros LongReal (Id -> Value)
        /// </summary>
        public Dictionary<string, double> LongRealValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros LongReal segunda sección (Id -> Value)
        /// </summary>
        public Dictionary<string, double> LongReal2Values { get; set; } = new();

        /// <summary>
        /// Fuente de los datos: "PLC" o "Database"
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp de la lectura
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// DTO para escribir todos los parámetros al PLC o DB
    /// </summary>
    public class MachineSettingsWriteRequest
    {
        /// <summary>
        /// Valores de parámetros booleanos (Id -> Value)
        /// </summary>
        public Dictionary<string, bool> BoolValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros enteros (Id -> Value)
        /// </summary>
        public Dictionary<string, int> IntValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros LongReal (Id -> Value)
        /// </summary>
        public Dictionary<string, double> LongRealValues { get; set; } = new();

        /// <summary>
        /// Valores de parámetros LongReal segunda sección (Id -> Value)
        /// </summary>
        public Dictionary<string, double> LongReal2Values { get; set; } = new();
    }
}

namespace SW.PC.API.Backend.Models.Excel
{
    /// <summary>
    /// Configuración de settings desde Excel (extensión de ExcelModels)
    /// </summary>
    public class SettingsPageConfiguration
    {
        /// <summary>
        /// Título de la sección de booleanos (celda A2)
        /// </summary>
        public string BoolSectionTitle { get; set; } = "Booleanos";

        /// <summary>
        /// Título de la sección de enteros (celda E2)
        /// </summary>
        public string IntSectionTitle { get; set; } = "Enteros";

        /// <summary>
        /// Título de la sección de decimales (celda L2)
        /// </summary>
        public string LongRealSectionTitle { get; set; } = "Decimales";

        /// <summary>
        /// Título de la segunda sección de decimales (celda T2)
        /// </summary>
        public string LongReal2SectionTitle { get; set; } = "Decimales 2";

        /// <summary>
        /// Parámetros booleanos desde Excel
        /// </summary>
        public List<ExcelBoolSetting> BoolSettings { get; set; } = new();

        /// <summary>
        /// Parámetros enteros desde Excel
        /// </summary>
        public List<ExcelIntSetting> IntSettings { get; set; } = new();

        /// <summary>
        /// Parámetros LongReal desde Excel (columnas M-S)
        /// </summary>
        public List<ExcelLongRealSetting> LongRealSettings { get; set; } = new();

        /// <summary>
        /// Parámetros LongReal segunda sección desde Excel (columnas U-AA)
        /// </summary>
        public List<ExcelLongRealSetting> LongReal2Settings { get; set; } = new();
    }

    /// <summary>
    /// Parámetro bool desde Excel (columnas B, C, D)
    /// </summary>
    public class ExcelBoolSetting
    {
        /// <summary>Nombre/ID del parámetro (columna B)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna C)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna D)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }
    }

    /// <summary>
    /// Parámetro int desde Excel (columnas F, G, H, I, J, K)
    /// </summary>
    public class ExcelIntSetting
    {
        /// <summary>Nombre/ID del parámetro (columna F)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna G)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna H)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }

        /// <summary>Valor mínimo (columna I, opcional)</summary>
        public int? MinValue { get; set; }

        /// <summary>Valor máximo (columna J, opcional)</summary>
        public int? MaxValue { get; set; }

        /// <summary>Unidad de medida (columna K, opcional)</summary>
        public string? Unit { get; set; }
    }

    /// <summary>
    /// Parámetro LongReal desde Excel (columnas L, M, N, O, P, Q, R)
    /// </summary>
    public class ExcelLongRealSetting
    {
        /// <summary>Nombre/ID del parámetro (columna L)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna M)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna N)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }

        /// <summary>Valor mínimo (columna O, opcional)</summary>
        public double? MinValue { get; set; }

        /// <summary>Valor máximo (columna P, opcional)</summary>
        public double? MaxValue { get; set; }

        /// <summary>Decimales a mostrar (columna Q, opcional)</summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>Unidad de medida (columna R, opcional)</summary>
        public string? Unit { get; set; }
    }
}
