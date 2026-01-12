using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models.Excel
{
    /// <summary>
    /// Configuración de visualización de información para elementos 3D.
    /// Cargado desde la hoja "3D_Elements_Info_Setting" del Excel.
    /// </summary>
    public class ElementInfoSettingConfig
    {
        #region Identificación y Configuración Base (Columnas A-K)

        /// <summary>A: Nombre del modelo padre (debe existir en hoja "3D Elements")</summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>B: Tipo de visualización</summary>
        public ElementDisplayType DisplayType { get; set; } = ElementDisplayType.AttachedLabel;

        /// <summary>C: Posición en pantalla (para screen-fixed, linked, screen-always)</summary>
        public string? ScreenPosition { get; set; }

        /// <summary>D: Posición relativa al modelo (top, right, bottom, left)</summary>
        public string ModelPosition { get; set; } = "top";

        /// <summary>E: Ajuste fino posición X</summary>
        public double OffsetX { get; set; }

        /// <summary>F: Ajuste fino posición Y (arriba/abajo)</summary>
        public double OffsetY { get; set; }

        /// <summary>G: Ajuste fino posición Z (adelante/atrás)</summary>
        public double OffsetZ { get; set; }

        /// <summary>H: Icono del modelo (emoji o nombre de archivo, null = sin icono)</summary>
        public string? ModelIcon { get; set; }

        /// <summary>Icono del modelo como data URL base64 (llenado automáticamente por el backend)</summary>
        public string? ModelIconBase64 { get; set; }

        /// <summary>H: Ancho de la etiqueta 3D (multiplicador del tamaño del modelo, default=0.6)</summary>
        public double LabelWidth { get; set; } = 0.6;

        /// <summary>I: Alto de la etiqueta 3D (multiplicador del tamaño del modelo, default=0.2)</summary>
        public double LabelHeight { get; set; } = 0.2;

        /// <summary>J: Escala general de la etiqueta (0.1-5.0, default=1.0)</summary>
        public double LabelScale { get; set; } = 1.0;

        /// <summary>L: Nombre corto para mostrar (ej: T1, T2). Si vacío, no se muestra</summary>
        public string? ShortName { get; set; }

        /// <summary>Número de slots a mostrar en modo compacto (extraído del sufijo :N del DisplayType). null = mostrar todos</summary>
        public int? CompactSlots { get; set; }

        #endregion

        #region Botones de Escritura PLC (Columnas L-Z)

        /// <summary>Botones de acción para escritura al PLC (máximo 5)</summary>
        public List<InfoSettingButton> Buttons { get; set; } = new();

        #endregion

        #region Slots de Lectura PLC (Columnas AA en adelante)

        /// <summary>Slots de datos para lectura del PLC (máximo 10)</summary>
        public List<InfoSettingSlot> Slots { get; set; } = new();

        #endregion

        #region Propiedades Calculadas

        /// <summary>Indica si tiene checkbox en la lista de visibilidad</summary>
        [JsonIgnore]
        public bool HasCheckbox => DisplayType != ElementDisplayType.AlwaysVisible;

        /// <summary>Indica si el panel en pantalla es siempre visible</summary>
        [JsonIgnore]
        public bool IsScreenAlwaysVisible => DisplayType == ElementDisplayType.ScreenAlways;

        /// <summary>Indica si tiene label pegado al modelo</summary>
        [JsonIgnore]
        public bool HasModelLabel => DisplayType == ElementDisplayType.AlwaysVisible ||
                                      DisplayType == ElementDisplayType.AttachedLabel ||
                                      DisplayType == ElementDisplayType.Linked ||
                                      DisplayType == ElementDisplayType.ScreenAlways ||
                                      DisplayType == ElementDisplayType.AlwaysLinked ||
                                      DisplayType == ElementDisplayType.DualToggle;

        /// <summary>Indica si tiene panel en pantalla</summary>
        [JsonIgnore]
        public bool HasScreenPanel => DisplayType == ElementDisplayType.ScreenFixed ||
                                       DisplayType == ElementDisplayType.Linked ||
                                       DisplayType == ElementDisplayType.ScreenAlways ||
                                       DisplayType == ElementDisplayType.DualToggle;

        /// <summary>Obtiene todas las variables PLC usadas (para registro en Variable_Views)</summary>
        public List<string> GetAllPlcVariables()
        {
            var variables = new List<string>();
            
            // Variables de botones (escritura)
            variables.AddRange(Buttons
                .Where(b => !string.IsNullOrWhiteSpace(b.PlcVariable))
                .Select(b => b.PlcVariable!));
            
            // Variables de slots (lectura)
            variables.AddRange(Slots
                .Where(s => !string.IsNullOrWhiteSpace(s.PlcVariable))
                .Select(s => s.PlcVariable!));
            
            return variables.Distinct().ToList();
        }

        #endregion

        /// <summary>Índice de fila en Excel (para debugging)</summary>
        public int ExcelRowIndex { get; set; }
    }

    /// <summary>
    /// Tipos de visualización disponibles para elementos 3D
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ElementDisplayType
    {
        /// <summary>Info pegada al modelo, siempre visible, sin checkbox</summary>
        AlwaysVisible,
        
        /// <summary>Info pegada al modelo, toggle con checkbox</summary>
        AttachedLabel,
        
        /// <summary>Panel en pantalla, toggle con checkbox</summary>
        ScreenFixed,
        
        /// <summary>Panel en pantalla + nombre en modelo, ambos toggle con checkbox</summary>
        Linked,
        
        /// <summary>Panel en pantalla siempre visible + checkbox para mostrar nombre en modelo</summary>
        ScreenAlways,
        
        /// <summary>Info pegada siempre visible + checkbox para resaltar/localizar modelo</summary>
        AlwaysLinked,
        
        /// <summary>Dos checkboxes: uno para modelLabel (3D) y otro para screenPanel (UI)</summary>
        DualToggle
    }

    /// <summary>
    /// Botón de acción para escritura al PLC
    /// </summary>
    public class InfoSettingButton
    {
        /// <summary>Índice del botón (1-5)</summary>
        public int Index { get; set; }

        /// <summary>Variable PLC a escribir (BOOL)</summary>
        public string? PlcVariable { get; set; }

        /// <summary>Texto/descripción del botón</summary>
        public string? Description { get; set; }

        /// <summary>Icono (emoji o nombre de archivo)</summary>
        public string? Icon { get; set; }

        /// <summary>Indica si el botón está configurado (tiene variable PLC)</summary>
        [JsonIgnore]
        public bool IsConfigured => !string.IsNullOrWhiteSpace(PlcVariable);
    }

    /// <summary>
    /// Slot de datos para lectura del PLC
    /// </summary>
    public class InfoSettingSlot
    {
        /// <summary>Índice del slot (1-10)</summary>
        public int Index { get; set; }

        /// <summary>Tipo de visualización del slot</summary>
        public SlotDisplayType Type { get; set; } = SlotDisplayType.Numeric;

        /// <summary>Variable PLC a leer</summary>
        public string? PlcVariable { get; set; }

        /// <summary>Etiqueta/descripción del dato</summary>
        public string? Description { get; set; }

        /// <summary>Unidad de medida (mm, °C, A, %, etc.)</summary>
        public string? Unit { get; set; }

        /// <summary>Formato numérico (#.0, #.00, etc.)</summary>
        public string? Format { get; set; }

        /// <summary>Valor mínimo (para gauge/progress)</summary>
        public double? Min { get; set; }

        /// <summary>Valor máximo (para gauge/progress)</summary>
        public double? Max { get; set; }

        /// <summary>Umbral de warning/amarillo (para gauge)</summary>
        public double? WarningThreshold { get; set; }

        /// <summary>Umbral crítico/rojo (para gauge)</summary>
        public double? CriticalThreshold { get; set; }

        /// <summary>Tamaño del historial (para sparkline)</summary>
        public int? HistorySize { get; set; }

        /// <summary>Texto cuando valor es TRUE (para boolean)</summary>
        public string? TextOn { get; set; }

        /// <summary>Texto cuando valor es FALSE (para boolean)</summary>
        public string? TextOff { get; set; }

        /// <summary>Icono del slot (emoji o nombre de archivo)</summary>
        public string? Icon { get; set; }

        /// <summary>Icono del slot como data URL base64 (llenado automáticamente por el backend)</summary>
        public string? IconBase64 { get; set; }

        /// <summary>Indica si el slot está configurado</summary>
        [JsonIgnore]
        public bool IsConfigured => !string.IsNullOrWhiteSpace(PlcVariable) && 
                                     Type != SlotDisplayType.None;

        /// <summary>Indica si el tipo incluye sparkline</summary>
        [JsonIgnore]
        public bool HasSparkline => Type == SlotDisplayType.Sparkline ||
                                     Type == SlotDisplayType.NumericSparkline ||
                                     Type == SlotDisplayType.GaugeSparkline;

        /// <summary>Indica si el tipo incluye gauge</summary>
        [JsonIgnore]
        public bool HasGauge => Type == SlotDisplayType.Gauge ||
                                 Type == SlotDisplayType.NumericGauge ||
                                 Type == SlotDisplayType.GaugeSparkline;

        /// <summary>Indica si el tipo incluye indicador de nivel de tanque</summary>
        [JsonIgnore]
        public bool HasTankLevel => Type == SlotDisplayType.TankLevel ||
                                     Type == SlotDisplayType.TankLevelNumeric;
    }

    /// <summary>
    /// Tipos de visualización para slots de datos
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SlotDisplayType
    {
        /// <summary>Slot no configurado</summary>
        None,
        
        /// <summary>Valor numérico simple (45.2 mm)</summary>
        Numeric,
        
        /// <summary>Estado ON/OFF con indicador LED</summary>
        Boolean,
        
        /// <summary>Texto literal del PLC</summary>
        Text,
        
        /// <summary>Barra de progreso horizontal</summary>
        Progress,
        
        /// <summary>Velocímetro/gauge circular</summary>
        Gauge,
        
        /// <summary>Mini gráfico de tendencia</summary>
        Sparkline,
        
        /// <summary>Valor numérico + gráfico de tendencia</summary>
        NumericSparkline,
        
        /// <summary>Valor numérico + gauge</summary>
        NumericGauge,
        
        /// <summary>Barra de progreso + valor numérico</summary>
        ProgressNumeric,
        
        /// <summary>Gauge + gráfico de tendencia</summary>
        GaugeSparkline,
        
        /// <summary>Indicador de nivel de tanque vertical</summary>
        TankLevel,
        
        /// <summary>Indicador de nivel de tanque + valor numérico</summary>
        TankLevelNumeric
    }

    /// <summary>
    /// Helper para parsear tipos desde strings del Excel
    /// </summary>
    public static class SlotDisplayTypeParser
    {
        public static SlotDisplayType Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return SlotDisplayType.None;

            return value.ToLowerInvariant().Replace(" ", "").Replace("_", "") switch
            {
                "numeric" => SlotDisplayType.Numeric,
                "boolean" or "bool" => SlotDisplayType.Boolean,
                "text" or "string" => SlotDisplayType.Text,
                "progress" or "progressbar" => SlotDisplayType.Progress,
                "gauge" => SlotDisplayType.Gauge,
                "sparkline" => SlotDisplayType.Sparkline,
                "numeric+sparkline" or "numericsparkline" => SlotDisplayType.NumericSparkline,
                "numeric+gauge" or "numericgauge" or "gaugenumeric" or "gauge+numeric" => SlotDisplayType.NumericGauge,
                "progress+numeric" or "progressnumeric" => SlotDisplayType.ProgressNumeric,
                "gauge+sparkline" or "gaugesparkline" => SlotDisplayType.GaugeSparkline,
                "tanklevel" or "tank" or "level" or "verticalprogress" or "progressvertical" => SlotDisplayType.TankLevel,
                "tanklevel+numeric" or "tanklevelnumeric" or "numerictanklevel" or "tank+numeric" => SlotDisplayType.TankLevelNumeric,
                _ => SlotDisplayType.None
            };
        }
    }

    /// <summary>
    /// Helper para parsear DisplayType desde strings del Excel
    /// Soporta sufijo :N para modo compacto (ej: "always-visible:2" → 2 slots visibles)
    /// </summary>
    public static class ElementDisplayTypeParser
    {
        /// <summary>
        /// Parsea el DisplayType y extrae CompactSlots si hay sufijo :N
        /// </summary>
        public static (ElementDisplayType Type, int? CompactSlots) ParseWithCompact(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (ElementDisplayType.AttachedLabel, null);

            int? compactSlots = null;
            var input = value.Trim();

            // Extraer sufijo :N si existe (ej: "always-visible:2")
            var colonIndex = input.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < input.Length - 1)
            {
                var suffix = input.Substring(colonIndex + 1);
                if (int.TryParse(suffix, out var slots) && slots >= 0 && slots <= 10)
                {
                    compactSlots = slots;
                    input = input.Substring(0, colonIndex);
                }
            }

            var displayType = Parse(input);
            return (displayType, compactSlots);
        }

        public static ElementDisplayType Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ElementDisplayType.AttachedLabel;

            return value.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "") switch
            {
                "alwaysvisible" or "always" => ElementDisplayType.AlwaysVisible,
                "attachedlabel" or "attached" or "label" => ElementDisplayType.AttachedLabel,
                "screenfixed" or "screen" or "fixed" => ElementDisplayType.ScreenFixed,
                "linked" => ElementDisplayType.Linked,
                "screenalways" => ElementDisplayType.ScreenAlways,
                "alwayslinked" => ElementDisplayType.AlwaysLinked,
                "dualtoggle" or "dual" or "both" => ElementDisplayType.DualToggle,
                _ => ElementDisplayType.AttachedLabel
            };
        }
    }
}
