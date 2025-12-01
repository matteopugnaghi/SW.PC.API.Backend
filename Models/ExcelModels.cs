namespace SW.PC.API.Backend.Models.Excel
{
    /// <summary>
    /// Configuración de proyecto desde Excel
    /// </summary>
    public class ProjectConfiguration
    {
        public string ProjectName { get; set; } = string.Empty;

        public string ProjectCode { get; set; } = string.Empty;

        public string Customer { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public List<HMIScreen> Screens { get; set; } = new();

        public List<PlcVariable> PlcVariables { get; set; } = new();

        public List<Model3DConfig> Models3D { get; set; } = new();

        public Dictionary<string, string> GeneralSettings { get; set; } = new();
    }

    /// <summary>
    /// Pantalla del HMI configurada en Excel
    /// </summary>
    public class HMIScreen
    {
        public string ScreenId { get; set; } = string.Empty;

        public string ScreenName { get; set; } = string.Empty;

        public string? Title { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsEnabled { get; set; } = true;

        public string? IconName { get; set; }

        public List<HMIComponent> Components { get; set; } = new();

        public Dictionary<string, string> Properties { get; set; } = new();
    }

    /// <summary>
    /// Componente de pantalla HMI
    /// </summary>
    public class HMIComponent
    {
        public string ComponentId { get; set; } = string.Empty;

        public string ComponentType { get; set; } = string.Empty; // Button, Indicator, Graph, Input, etc.

        public string Label { get; set; } = string.Empty;

        public string? PlcVariable { get; set; }  // Variable vinculada del PLC

        public Position Position { get; set; } = new();

        public Size Size { get; set; } = new();

        public Dictionary<string, object> Properties { get; set; } = new();

        public bool IsVisible { get; set; } = true;

        public bool IsEnabled { get; set; } = true;
    }

    public class Position
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Size
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Variable PLC configurada en Excel
    /// </summary>
    public class PlcVariable
    {
        public string VariableName { get; set; } = string.Empty;

        public string SymbolPath { get; set; } = string.Empty; // Path completo en TwinCAT

        public string DataType { get; set; } = string.Empty; // BOOL, INT, REAL, STRING, etc.

        public string AccessMode { get; set; } = "ReadOnly"; // ReadOnly, WriteOnly, ReadWrite

        public int? UpdateRateMs { get; set; } = 1000; // Tasa de actualización en milisegundos

        public string? Description { get; set; }

        public string? Unit { get; set; }  // Unidad de medida

        public double? MinValue { get; set; }

        public double? MaxValue { get; set; }

        public string? AlarmCondition { get; set; }

        public bool LogToDatabase { get; set; } = false;
    }

    /// <summary>
    /// Modelo 3D configurado en Excel
    /// </summary>
    public class Model3DConfig
    {
        // Animación del padre (columnas U, V, W, AD, AE, AF, AG, AH)
        public string AnimationType { get; set; } = string.Empty;
        public double AnimationSpeed { get; set; } = 1.0;
        public bool AnimateOnlyWhenOn { get; set; } = true;
        public string AnimationPlcVariable { get; set; } = string.Empty;
        public double AnimationMinValue { get; set; } = 0.0;
        public double AnimationMaxValue { get; set; } = 1000.0;
        public string AnimationAxis { get; set; } = "Y";
        public double AnimationScaleFactor { get; set; } = 0.1;
        public string ModelId { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty; // Ej: machine.glb

    public string FileType { get; set; } = "glb"; // glb, gltf, obj, stl, fbx

    public string? Description { get; set; }

    public string? Category { get; set; } // Machine, Equipment, Part, Assembly, etc.

    public string? AssociatedScreen { get; set; } // ScreenId relacionado

    public bool IsEnabled { get; set; } = true; 
        
        public int DisplayOrder { get; set; }
        
        // Configuración de vista inicial
        public ViewConfiguration? InitialView { get; set; }
        
        // Variables PLC vinculadas al modelo (para animaciones, cambios de color, etc.)
        public List<ModelVariableBinding> VariableBindings { get; set; } = new();
        
        // Modelos hijos (Child1-Child5 desde columnas AI-EI del Excel)
        public List<ChildModel3DConfig> Children { get; set; } = new();
        
        public Dictionary<string, string> Properties { get; set; } = new();
    }
    
    /// <summary>
    /// Modelo 3D hijo (Child1-Child5) - 21 columnas por hijo en Excel
    /// Child1: AI-BC, Child2: BD-BX, Child3: BY-CS, Child4: CT-DN, Child5: DO-EI
    /// </summary>
    public class ChildModel3DConfig
    {
        public string Name { get; set; } = string.Empty;           // Columna 0: AI, BD, BY, CT, DO
        public string ParentName { get; set; } = string.Empty;     // Columna 1: AJ, BE, BZ, CU, DP
        public string FileName { get; set; } = string.Empty;       // Columna 2: AK, BF, CA, CV, DQ
        public string AnimationType { get; set; } = string.Empty;  // Columna 3: AL, BG, CB, CW, DR
        public double AnimationSpeed { get; set; } = 1.0;          // Columna 4: AM, BH, CC, CX, DS
        public bool AnimateOnlyWhenOn { get; set; } = true;        // Columna 5: AN, BI, CD, CY, DT
        public string PlcVariable { get; set; } = string.Empty;    // Columna 6: AO, BJ, CE, CZ, DU
        public string Axis { get; set; } = "Y";                    // Columna 7: AP, BK, CF, DA, DV
        public double MinValue { get; set; } = 0.0;                // Columna 8: AQ, BL, CG, DB, DW
        public double MaxValue { get; set; } = 1000.0;             // Columna 9: AR, BM, CH, DC, DX
        public double ScaleFactor { get; set; } = 0.1;             // Columna 10: AS, BN, CI, DD, DY
        public double? ScaleX { get; set; }                        // Columna 11: AT, BO, CJ, DE, DZ
        public double? ScaleY { get; set; }                        // Columna 12: AU, BP, CK, DF, EA
        public double? ScaleZ { get; set; }                        // Columna 13: AV, BQ, CL, DG, EB
        public string ColorOn { get; set; } = "Lime";              // Columna 14: AW, BR, CM, DH, EC
        public string ColorOff { get; set; } = "Gray";             // Columna 15: AX, BS, CN, DI, ED
        public string ColorDisabled { get; set; } = "Violet";      // Columna 16: AY, BT, CO, DJ, EE
        public string ColorAlarm { get; set; } = "Red";            // Columna 17: AZ, BU, CP, DK, EF
        public double OffsetX { get; set; } = 0.0;                 // Columna 18: BA, BV, CQ, DL, EG
        public double OffsetY { get; set; } = 0.0;                 // Columna 19: BB, BW, CR, DM, EH
        public double OffsetZ { get; set; } = 0.0;                 // Columna 20: BC, BX, CS, DN, EI
    }
    
    /// <summary>
    /// Configuración de vista inicial del modelo 3D
    /// </summary>
    public class ViewConfiguration
    {
        public Vector3 CameraPosition { get; set; } = new();
        
        public Vector3 CameraTarget { get; set; } = new();
        
        public double CameraZoom { get; set; } = 1.0;
        
        public bool AutoRotate { get; set; } = false;
    }
    
    public class Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    
    /// <summary>
    /// Vinculación de variable PLC con partes del modelo 3D
    /// </summary>
    public class ModelVariableBinding
    {
        public string VariableName { get; set; } = string.Empty;
        
        public string ModelPart { get; set; } = string.Empty; // Nombre del mesh/node en el modelo 3D
        
        public string BindingType { get; set; } = string.Empty; // Position, Rotation, Scale, Color, Visibility, Animation
        
        public string? Axis { get; set; } // X, Y, Z para transformaciones
        
        public double? MinValue { get; set; }
        
        public double? MaxValue { get; set; }
        
        public double? MinRange { get; set; } // Rango mínimo de transformación
        
        public double? MaxRange { get; set; } // Rango máximo de transformación
        
        public Dictionary<string, string> Properties { get; set; } = new();
    }
    
    /// <summary>
    /// Configuración de colores por estado PLC desde Excel (hoja: PLC_State_Colors)
    /// </summary>
    public class StateColorConfig
    {
        /// <summary>
        /// Patrón de variable PLC (ej: "i_StatePumps[*]", "bMotorState", etc.)
        /// </summary>
        public string VariablePattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor del estado (0, 1, 2, 3, etc.)
        /// </summary>
        public int StateValue { get; set; }
        
        /// <summary>
        /// Nombre descriptivo del estado (Disabled, Off, On, Alarm)
        /// </summary>
        public string StateName { get; set; } = string.Empty;
        
        /// <summary>
        /// Componente Rojo del color RGB (0-255)
        /// </summary>
        public int ColorR { get; set; }
        
        /// <summary>
        /// Componente Verde del color RGB (0-255)
        /// </summary>
        public int ColorG { get; set; }
        
        /// <summary>
        /// Componente Azul del color RGB (0-255)
        /// </summary>
        public int ColorB { get; set; }
        
        /// <summary>
        /// Descripción opcional del estado y color
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Color en formato hexadecimal (#RRGGBB)
        /// </summary>
        public string ColorHex => $"#{ColorR:X2}{ColorG:X2}{ColorB:X2}";
        
        /// <summary>
        /// Color normalizado para Babylon.js (0.0-1.0)
        /// </summary>
        public ColorRGB ColorNormalized => new ColorRGB 
        { 
            R = ColorR / 255.0, 
            G = ColorG / 255.0, 
            B = ColorB / 255.0 
        };
    }
    
    /// <summary>
    /// Estructura de color RGB normalizado (0.0-1.0) para frontend
    /// </summary>
    public class ColorRGB
    {
        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
    }

    /// <summary>
    /// Configuración del sistema desde Excel (hoja "System Config")
    /// </summary>
    public class SystemConfiguration
    {
        // ===== SERVICIOS =====
        /// <summary>
        /// Habilitar/deshabilitar PlcPollingService
        /// </summary>
        public bool EnablePlcPolling { get; set; } = true;

        /// <summary>
        /// Intervalo de polling del PLC en milisegundos
        /// </summary>
        public int PlcPollingInterval { get; set; } = 1000;

        /// <summary>
        /// Habilitar/deshabilitar SignalR Hub
        /// </summary>
        public bool EnableSignalR { get; set; } = true;

        /// <summary>
        /// Habilitar/deshabilitar logging detallado
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        // ===== TWINCAT/PLC =====
        /// <summary>
        /// Usar simulación de PLC en lugar de TwinCAT real
        /// </summary>
        public bool UseSimulatedPlc { get; set; } = true;

        /// <summary>
        /// AMS Net ID del PLC (ej: 127.0.0.1.1.1)
        /// </summary>
        public string PlcAmsNetId { get; set; } = "127.0.0.1.1.1";

        /// <summary>
        /// Puerto ADS del PLC
        /// </summary>
        public int PlcAdsPort { get; set; } = 851;

        // ===== BASE DE DATOS =====
        /// <summary>
        /// Habilitar/deshabilitar Entity Framework / SQL Server
        /// </summary>
        public bool EnableDatabase { get; set; } = false;

        /// <summary>
        /// Connection string de la base de datos
        /// </summary>
        public string? DatabaseConnectionString { get; set; }

        // ===== API/WEB =====
        /// <summary>
        /// Puerto del servidor API
        /// </summary>
        public int ApiPort { get; set; } = 5000;

        /// <summary>
        /// Habilitar CORS
        /// </summary>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Orígenes permitidos para CORS (separados por coma)
        /// </summary>
        public string CorsOrigins { get; set; } = "http://localhost:3000,http://localhost:3001,http://localhost:5173";

        // ===== EXCEL/ARCHIVOS =====
        /// <summary>
        /// Nombre del archivo Excel principal
        /// </summary>
        public string ExcelConfigFileName { get; set; } = "ProjectConfig.xlsm";

        /// <summary>
        /// Carpeta donde están los archivos de configuración
        /// </summary>
        public string ConfigFolder { get; set; } = "ExcelConfigs";

        /// <summary>
        /// Carpeta donde están los modelos 3D
        /// </summary>
        public string ModelsFolder { get; set; } = "wwwroot/models";

        // ===== CACHE/PERFORMANCE =====
        /// <summary>
        /// Tiempo de cache de configuración en segundos
        /// </summary>
        public int ConfigCacheSeconds { get; set; } = 300;

        /// <summary>
        /// Máximo de conexiones SignalR concurrentes
        /// </summary>
        public int MaxSignalRConnections { get; set; } = 100;
    }

    /// <summary>
    /// Métricas de rendimiento del sistema en tiempo real
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// Tiempo de ciclo actual del polling PLC en ms
        /// </summary>
        public double PlcPollingScanTime { get; set; }

        /// <summary>
        /// Tiempo promedio del ciclo de polling en ms
        /// </summary>
        public double PlcPollingAvgScanTime { get; set; }

        /// <summary>
        /// Número de variables monitoreadas activamente
        /// </summary>
        public int PlcMonitoredVariables { get; set; }

        /// <summary>
        /// Número de conexiones SignalR activas
        /// </summary>
        public int SignalRActiveConnections { get; set; }

        /// <summary>
        /// Tiempo de respuesta del último broadcast SignalR en ms
        /// </summary>
        public double SignalRLastBroadcastTime { get; set; }

        /// <summary>
        /// Tiempo de respuesta promedio de broadcasts SignalR en ms
        /// </summary>
        public double SignalRAvgBroadcastTime { get; set; }

        /// <summary>
        /// Tiempo de carga del último Excel en ms
        /// </summary>
        public double ExcelLastLoadTime { get; set; }

        /// <summary>
        /// Timestamp de la última actualización de métricas
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Uptime del servidor en formato legible
        /// </summary>
        public string ServerUptime { get; set; } = "00:00:00";
    }
}

