using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Modelo completo de elemento 3D según estructura de Excel "1) Pumps"
    /// Soporta 30 columnas base + parámetros adicionales para web
    /// </summary>
    public class PumpElement3D
    {
        // ===== A-C: IDENTIFICACIÓN =====
        /// <summary>A: Total de elementos (solo en primera fila)</summary>
        public int? TotalElements { get; set; }
        
        /// <summary>B: Nombre/descripción del elemento</summary>
        [Required]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>C: Ruta del archivo 3D (ej: Pumps/PUMP_01.OBJ)</summary>
        [Required]
        public string FileName { get; set; } = string.Empty;

        // ===== D-F: POSICIÓN 3D =====
        /// <summary>D: Offset X del modelo en la escena</summary>
        public double OffsetX { get; set; }
        
        /// <summary>E: Offset Y del modelo en la escena</summary>
        public double OffsetY { get; set; }
        
        /// <summary>F: Offset Z del modelo en la escena</summary>
        public double OffsetZ { get; set; }

        // ===== G-I: VARIABLES PLC TWINCAT =====
        /// <summary>G: Variable PLC para página principal (estado del elemento)</summary>
        public string PlcMainPageReference { get; set; } = string.Empty;
        
        /// <summary>H: Variable PLC para página de manuales (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public string PlcManualPageReference { get; set; } = string.Empty;
        
        /// <summary>I: Variable PLC para página de configuración (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public string PlcConfigPageReference { get; set; } = string.Empty;

        // ===== J-M: COLORES SEGÚN ESTADO =====
        /// <summary>J: Color cuando PlcMainPageReference = 2 (elemento encendido)</summary>
        public string ColorElementOn { get; set; } = "Lime";
        
        /// <summary>K: Color cuando PlcMainPageReference = 1 (elemento apagado)</summary>
        public string ColorElementOff { get; set; } = "Gray";
        
        /// <summary>L: Color cuando PlcMainPageReference = 0 (elemento deshabilitado)</summary>
        public string ColorElementDisabled { get; set; } = "Violet";
        
        /// <summary>M: Color cuando PlcMainPageReference = 3 (alarma)</summary>
        public string ColorElementAlarm { get; set; } = "Red";

        // ===== N-U: LABEL/ETIQUETA 3D (CAMPOS ELIMINADOS) =====
        /// <summary>N: Texto del label que aparece en 3D</summary>
        public string ElementNameDescription { get; set; } = string.Empty;
        
        /// <summary>O: Tamaño de fuente del label (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public int LabelFontSize { get; set; } = 20;
        
        /// <summary>P: Offset X de la etiqueta (Posición 1) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetX_Pos1 { get; set; }
        
        /// <summary>Q: Offset Y de la etiqueta (Posición 1) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetY_Pos1 { get; set; }
        
        /// <summary>R: Offset Z de la etiqueta (Posición 1) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetZ_Pos1 { get; set; }
        
        /// <summary>S: Offset X de la etiqueta (Posición 2) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetX_Pos2 { get; set; }
        
        /// <summary>T: Offset Y de la etiqueta (Posición 2) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetY_Pos2 { get; set; }
        
        /// <summary>U: Offset Z de la etiqueta (Posición 2) (NO USADO - columna eliminada)</summary>
        [JsonIgnore]
        public double LabelOffsetZ_Pos2 { get; set; }

        // ===== V: JERARQUÍA PADRE-HIJO (NO USADO - columna eliminada) =====
        /// <summary>V (col 22): Número de elementos hijos (offsprings) que heredan este modelo (NO USADO)</summary>
        [JsonIgnore]
        public int OffspringsCount { get; set; }
        
        /// <summary>Lista de elementos hijos cargados (NO USADO)</summary>
        [JsonIgnore]
        public List<PumpElement3D>? Children { get; set; }

        // ===== W-Z: METADATOS (NO USADOS - columnas eliminadas) =====
        /// <summary>W (col 23): Ruta de imagen .jpg/.png para UI 2D (NO USADO)</summary>
        [JsonIgnore]
        public string IconFileReference { get; set; } = string.Empty;
        
        /// <summary>X (col 24): Línea en archivos MSG.ENG/ITA/ESP para multiidioma (NO USADO)</summary>
        [JsonIgnore]
        public int IconLanguageLabelRow { get; set; }
        
        /// <summary>Y (col 25): Marca y modelo (NO USADO)</summary>
        [JsonIgnore]
        public string BrandAndModel { get; set; } = string.Empty;
        
        /// <summary>Z (col 26): Vinculación a Gantry (NO USADO)</summary>
        [JsonIgnore]
        public int BindGantryNumber { get; set; } = -1;

        // ===== AA: CATÁLOGO DE COLORES (NO USADO - columna eliminada) =====
        /// <summary>AA (col 30): Lista de colores disponibles (NO USADO)</summary>
        [JsonIgnore]
        public string AvailableColors { get; set; } = string.Empty;

        // ===== PARÁMETROS ADICIONALES PARA WEB =====
        
        // Transformaciones 3D avanzadas
        /// <summary>Rotación X en grados</summary>
        public double RotationX { get; set; }
        
        /// <summary>Rotación Y en grados</summary>
        public double RotationY { get; set; }
        
        /// <summary>Rotación Z en grados</summary>
        public double RotationZ { get; set; }
        
        /// <summary>Escala X (1.0 = normal)</summary>
        public double ScaleX { get; set; } = 1.0;
        
        /// <summary>Escala Y (1.0 = normal)</summary>
        public double ScaleY { get; set; } = 1.0;
        
        /// <summary>Escala Z (1.0 = normal)</summary>
        public double ScaleZ { get; set; } = 1.0;

        // Interacción web
        /// <summary>¿El modelo es clickeable en el 3D?</summary>
        public bool IsClickable { get; set; } = true;
        
        /// <summary>¿Mostrar tooltip al hacer hover?</summary>
        public bool ShowTooltip { get; set; } = true;
        
        /// <summary>Pantalla a la que navegar al hacer click (manual/config/null)</summary>
        public string? NavigateToScreen { get; set; }

        // Animaciones
        /// <summary>
        /// Tipo de animación:
        /// - "none": Sin animación
        /// - "REF PLC": Control directo desde variable PLC (requiere AnimationPlcVariable configurado)
        /// - "rotate/pulse/bounce": Animaciones procedurales (no implementado)
        /// </summary>
        public string AnimationType { get; set; } = "none";
        
        /// <summary>Velocidad de animación (0-10) - Solo para animaciones procedurales</summary>
        public double AnimationSpeed { get; set; } = 1.0;
        
        /// <summary>¿Solo animar cuando estado = 2 (on)? - Solo para animaciones procedurales</summary>
        public bool AnimateOnlyWhenOn { get; set; } = true;
        
        /// <summary>Variable PLC que controla la posición de la animación (LREAL en mm) - Usado cuando AnimationType = "REF PLC"</summary>
        public string? AnimationPlcVariable { get; set; }
        
        /// <summary>Valor mínimo del rango de movimiento (en mm)</summary>
        public double AnimationMinValue { get; set; } = 0.0;
        
        /// <summary>Valor máximo del rango de movimiento (en mm)</summary>
        public double AnimationMaxValue { get; set; } = 1000.0;
        
        /// <summary>Eje de movimiento de la animación: X, Y o Z</summary>
        public string AnimationAxis { get; set; } = "Y";
        
        /// <summary>Factor de escala para convertir mm del PLC a unidades Babylon (1.0 = 1mm PLC = 1 unidad Babylon)</summary>
        public double AnimationScaleFactor { get; set; } = 0.1;

        // ===== AI-CZ: JERARQUÍA CON HASTA 5 HIJOS POR MODELO (18 COLUMNAS POR HIJO) =====
        // HIJO 1 (AI-AZ: 18 columnas)
        public string? Child1_Name { get; set; }
        public string? Child1_ParentName { get; set; }
        public string? Child1_FileName { get; set; }
        public string Child1_AnimationType { get; set; } = "none";
        public double Child1_AnimationSpeed { get; set; } = 1.0;
        public bool Child1_AnimateOnlyWhenOn { get; set; } = true;
        public string? Child1_PlcVariable { get; set; }
        public string? Child1_Axis { get; set; }
        public double Child1_MinValue { get; set; } = 0.0;
        public double Child1_MaxValue { get; set; } = 1000.0;
        public double Child1_ScaleFactor { get; set; } = 0.1;
        public double? Child1_ScaleX { get; set; }
        public double? Child1_ScaleY { get; set; }
        public double? Child1_ScaleZ { get; set; }
        public string Child1_ColorOn { get; set; } = "Lime";
        public string Child1_ColorOff { get; set; } = "Gray";
        public string Child1_ColorDisabled { get; set; } = "Violet";
        public string Child1_ColorAlarm { get; set; } = "Red";
        public double Child1_OffsetX { get; set; } = 0.0;
        public double Child1_OffsetY { get; set; } = 0.0;
        public double Child1_OffsetZ { get; set; } = 0.0;

        // HIJO 2 (BA-BS: 18 columnas)
        public string? Child2_Name { get; set; }
        public string? Child2_ParentName { get; set; }
        public string? Child2_FileName { get; set; }
        public string Child2_AnimationType { get; set; } = "none";
        public double Child2_AnimationSpeed { get; set; } = 1.0;
        public bool Child2_AnimateOnlyWhenOn { get; set; } = true;
        public string? Child2_PlcVariable { get; set; }
        public string? Child2_Axis { get; set; }
        public double Child2_MinValue { get; set; } = 0.0;
        public double Child2_MaxValue { get; set; } = 1000.0;
        public double Child2_ScaleFactor { get; set; } = 0.1;
        public double? Child2_ScaleX { get; set; }
        public double? Child2_ScaleY { get; set; }
        public double? Child2_ScaleZ { get; set; }
        public string Child2_ColorOn { get; set; } = "Lime";
        public string Child2_ColorOff { get; set; } = "Gray";
        public string Child2_ColorDisabled { get; set; } = "Violet";
        public string Child2_ColorAlarm { get; set; } = "Red";
        public double Child2_OffsetX { get; set; } = 0.0;
        public double Child2_OffsetY { get; set; } = 0.0;
        public double Child2_OffsetZ { get; set; } = 0.0;

        // HIJO 3 (BT-CL: 18 columnas)
        public string? Child3_Name { get; set; }
        public string? Child3_ParentName { get; set; }
        public string? Child3_FileName { get; set; }
        public string Child3_AnimationType { get; set; } = "none";
        public double Child3_AnimationSpeed { get; set; } = 1.0;
        public bool Child3_AnimateOnlyWhenOn { get; set; } = true;
        public string? Child3_PlcVariable { get; set; }
        public string? Child3_Axis { get; set; }
        public double Child3_MinValue { get; set; } = 0.0;
        public double Child3_MaxValue { get; set; } = 1000.0;
        public double Child3_ScaleFactor { get; set; } = 0.1;
        public double? Child3_ScaleX { get; set; }
        public double? Child3_ScaleY { get; set; }
        public double? Child3_ScaleZ { get; set; }
        public string Child3_ColorOn { get; set; } = "Lime";
        public string Child3_ColorOff { get; set; } = "Gray";
        public string Child3_ColorDisabled { get; set; } = "Violet";
        public string Child3_ColorAlarm { get; set; } = "Red";
        public double Child3_OffsetX { get; set; } = 0.0;
        public double Child3_OffsetY { get; set; } = 0.0;
        public double Child3_OffsetZ { get; set; } = 0.0;

        // HIJO 4 (CM-DE: 18 columnas)
        public string? Child4_Name { get; set; }
        public string? Child4_ParentName { get; set; }
        public string? Child4_FileName { get; set; }
        public string Child4_AnimationType { get; set; } = "none";
        public double Child4_AnimationSpeed { get; set; } = 1.0;
        public bool Child4_AnimateOnlyWhenOn { get; set; } = true;
        public string? Child4_PlcVariable { get; set; }
        public string? Child4_Axis { get; set; }
        public double Child4_MinValue { get; set; } = 0.0;
        public double Child4_MaxValue { get; set; } = 1000.0;
        public double Child4_ScaleFactor { get; set; } = 0.1;
        public double? Child4_ScaleX { get; set; }
        public double? Child4_ScaleY { get; set; }
        public double? Child4_ScaleZ { get; set; }
        public string Child4_ColorOn { get; set; } = "Lime";
        public string Child4_ColorOff { get; set; } = "Gray";
        public string Child4_ColorDisabled { get; set; } = "Violet";
        public string Child4_ColorAlarm { get; set; } = "Red";
        public double Child4_OffsetX { get; set; } = 0.0;
        public double Child4_OffsetY { get; set; } = 0.0;
        public double Child4_OffsetZ { get; set; } = 0.0;

        // HIJO 5 (DF-DW: 18 columnas)
        public string? Child5_Name { get; set; }
        public string? Child5_ParentName { get; set; }
        public string? Child5_FileName { get; set; }
        public string Child5_AnimationType { get; set; } = "none";
        public double Child5_AnimationSpeed { get; set; } = 1.0;
        public bool Child5_AnimateOnlyWhenOn { get; set; } = true;
        public string? Child5_PlcVariable { get; set; }
        public string? Child5_Axis { get; set; }
        public double Child5_MinValue { get; set; } = 0.0;
        public double Child5_MaxValue { get; set; } = 1000.0;
        public double Child5_ScaleFactor { get; set; } = 0.1;
        public double? Child5_ScaleX { get; set; }
        public double? Child5_ScaleY { get; set; }
        public double? Child5_ScaleZ { get; set; }
        public string Child5_ColorOn { get; set; } = "Lime";
        public string Child5_ColorOff { get; set; } = "Gray";
        public string Child5_ColorDisabled { get; set; } = "Violet";
        public string Child5_ColorAlarm { get; set; } = "Red";
        public double Child5_OffsetX { get; set; } = 0.0;
        public double Child5_OffsetY { get; set; } = 0.0;
        public double Child5_OffsetZ { get; set; } = 0.0;

        // Visibilidad
        /// <summary>¿Visible al cargar la escena?</summary>
        public bool InitiallyVisible { get; set; } = true;
        
        /// <summary>Variable PLC que controla visibilidad (opcional)</summary>
        public string? VisibilityCondition { get; set; }

        // Agrupación/Filtros
        /// <summary>Categoría para filtrado (pumps, valves, tanks...)</summary>
        public string Category { get; set; } = "pumps";
        
        /// <summary>Capa de visualización (para ocultar/mostrar grupos)</summary>
        public string Layer { get; set; } = "default";

        // Performance
        /// <summary>¿Proyectar sombras?</summary>
        public bool CastShadows { get; set; } = true;
        
        /// <summary>¿Recibir sombras?</summary>
        public bool ReceiveShadows { get; set; } = true;
        
        /// <summary>Nivel de detalle: high, medium, low</summary>
        public string LOD { get; set; } = "high";

        // Metadatos internos
        /// <summary>ID único generado</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>Índice de fila en el Excel (para debugging)</summary>
        public int ExcelRowIndex { get; set; }
        
        /// <summary>Fecha de carga</summary>
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    }
}
