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

        public DateTime CreatedDate { get; set; } = DateTime.Now;

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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // 🔄 HOT-SWAP - Intercambio de modelos en caliente (columna T)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Condición PLC para mostrar/ocultar este modelo (Hot-Swap).
        /// Formato: "VARIABLE=VALOR" ej: "MAIN.fbMachine.st_ChgRecipe.i_numTrainRecipe=1"
        /// Si vacío, el modelo siempre está visible (si IsEnabled=true).
        /// Cuando la variable PLC tiene el valor especificado, el modelo se muestra.
        /// </summary>
        public string EnableSwap { get; set; } = string.Empty;
        
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
        
        /// <summary>
        /// Variable PLC (STRING/WSTRING) donde escribir la pantalla activa del HMI.
        /// Si vacío, no se notifica al PLC del cambio de pantalla.
        /// Ejemplo: "GVL.sHmiCurrentScreen" o "MAIN.fbHmi.sCurrentPage"
        /// Valores posibles: "principal", "alarmas", "estadisticas", "manual", etc.
        /// </summary>
        public string CurrentScreenPlcVariable { get; set; } = "";

        /// <summary>
        /// Variable PLC (ARRAY[0..5] OF WSTRING) donde escribir los nombres de usuarios conectados.
        /// Array paralelo a ClientsIdConnected - mismo índice relaciona usuario con su IP.
        /// Si vacío, no se escribe al PLC.
        /// Ejemplo: "GVL.asUserLogged" o "MAIN.fbHmi.asConnectedUsers"
        /// </summary>
        public string UserLogged { get; set; } = "";

        /// <summary>
        /// Variable PLC (INT) que incrementa mientras hay clientes conectados.
        /// Actúa como contador de ciclos de vida de la aplicación.
        /// Si vacío, no se escribe al PLC.
        /// Ejemplo: "GVL.iCounterCycleLive" o "MAIN.fbHmi.iConnectionCounter"
        /// </summary>
        public string CounterCycleLive { get; set; } = "";

        /// <summary>
        /// Variable PLC (ARRAY[0..5] OF WSTRING) donde escribir las IPs de clientes conectados.
        /// Array paralelo a UserLogged - mismo índice relaciona IP con su usuario.
        /// Se escriben hasta 6 clientes simultáneos (índices 0 a 5).
        /// Si vacío, no se escribe al PLC.
        /// Ejemplo: "GVL.asClientsIdConnected" o "MAIN.fbHmi.asConnectedIPs"
        /// </summary>
        public string ClientsIdConnected { get; set; } = "";

        /// <summary>
        /// Variable PLC (WSTRING) donde el PLC escribe mensajes/logs para registrar en Operation Log.
        /// Se usa ADS Notification para detectar cambios (eficiente, solo cuando hay nuevo mensaje).
        /// El PLC debe concatenar un ID/timestamp para asegurar que siempre sea diferente.
        /// Formato recomendado: "ID|CATEGORIA|MENSAJE" (ej: "001|PROCESS|Motor arrancado")
        /// Categorías válidas: PROCESS, ALARM, INFO, WARNING, COMMAND
        /// Si vacío, esta funcionalidad está deshabilitada.
        /// Ejemplo: "GVL.sLogToPC" o "MAIN.fbMachine.sLogMessage"
        /// </summary>
        public string LogFromTwincatPlcVariable { get; set; } = "";

        // ===== BASE DE DATOS SQLite =====
        /// <summary>
        /// Habilitar/deshabilitar base de datos SQLite (autenticación, audit logs, etc.)
        /// </summary>
        public bool EnableDatabase { get; set; } = true;

        /// <summary>
        /// Ruta del archivo SQLite (ej: "Data/Aquafrisch.db")
        /// </summary>
        public string? DatabaseConnectionString { get; set; } = "Data/Aquafrisch.db";

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

        // ===== 🔐 GIT REPOSITORIES (Cybersecurity) =====
        /// <summary>
        /// Ruta al repositorio Git del Backend (ASP.NET Core)
        /// Si vacío, se auto-detecta desde la ubicación del ejecutable
        /// </summary>
        public string GitRepoBackend { get; set; } = "";

        /// <summary>
        /// Ruta al repositorio Git del Frontend (React/Babylon.js)
        /// </summary>
        public string GitRepoFrontend { get; set; } = "";

        /// <summary>
        /// Ruta al repositorio Git del código TwinCAT PLC
        /// </summary>
        public string GitRepoTwinCatPlc { get; set; } = "";

        // ===== 🔐 MODO DE ENTORNO (EU CRA Compliance) =====
        /// <summary>
        /// Modo de entorno: "production" o "development"
        /// En producción: solo TwinCAT es editable desde Git Panel
        /// En desarrollo: todos los repos son editables
        /// </summary>
        public string EnvironmentMode { get; set; } = "development";

        // ===== 🛡️ VULNERABILITY SCANNER (EU CRA Compliance) =====
        /// <summary>
        /// URL del servicio de vulnerabilidades.
        /// Si vacío, el scanner está deshabilitado.
        /// Ejemplos:
        /// - OSV (Google): https://api.osv.dev/v1/query
        /// - GitHub: https://api.github.com/advisories
        /// - ENISA (futuro): https://api.enisa.europa.eu/vuln
        /// - Local: http://192.168.1.100:8080/api/vuln
        /// </summary>
        public string VulnScanApiUrl { get; set; } = "";

        /// <summary>
        /// Tipo de API para parsear respuestas correctamente.
        /// Valores: OSV, GitHub, NVD, ENISA, Custom
        /// </summary>
        public string VulnScanApiType { get; set; } = "OSV";

        /// <summary>
        /// Intervalo de escaneo automático en horas.
        /// 0 = solo escaneo manual
        /// </summary>
        public int VulnScanIntervalHours { get; set; } = 0;

        /// <summary>
        /// Generar alarma del sistema si se detecta vulnerabilidad crítica
        /// </summary>
        public bool VulnScanAlertOnCritical { get; set; } = true;

        /// <summary>
        /// API Key para servicios que lo requieran (NVD, GitHub con rate limit, etc.)
        /// </summary>
        public string VulnScanApiKey { get; set; } = "";

        // ═══════════════════════════════════════════════════════════════════════════
        // 📤 VULNERABILITY REPORT - EU CRA Art. 14 (Notificación de vulnerabilidades)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar envío de reportes de vulnerabilidades a servidor externo.
        /// Requerido por EU CRA para notificación a autoridades (ENISA) o SOC/SIEM interno.
        /// </summary>
        public bool VulnReportEnabled { get; set; } = false;

        /// <summary>
        /// URL del servidor destino para enviar reportes de vulnerabilidades.
        /// Ejemplos:
        /// - SOC/SIEM interno: https://soc.cliente.com/api/vulnerabilities
        /// - ENISA (futuro): https://api.enisa.europa.eu/notifications
        /// - Custom: http://192.168.1.100:8080/api/vuln-report
        /// </summary>
        public string VulnReportApiUrl { get; set; } = "";

        /// <summary>
        /// Tipo de destino para formatear el payload correctamente.
        /// Valores: SOC_SIEM, ENISA, Custom
        /// </summary>
        public string VulnReportApiType { get; set; } = "SOC_SIEM";

        /// <summary>
        /// Enviar automáticamente cuando se detecte vulnerabilidad crítica.
        /// Si false, solo envío manual desde UI.
        /// </summary>
        public bool VulnReportAutoSendOnCritical { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════════════
        // 💻 IPC HARDWARE INFO - EU CRA Compliance (System Documentation)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar monitoreo de hardware IPC (CPU, RAM, Disk, Network, Security).
        /// Requerido por EU CRA para documentación del entorno operativo.
        /// </summary>
        public bool IpcInfoEnabled { get; set; } = true;

        /// <summary>
        /// Intervalo de polling rápido (CPU%, RAM%, Disk%) en segundos.
        /// Recomendado: 30 segundos. 0 = deshabilitado.
        /// </summary>
        public int IpcInfoQuickPollSeconds { get; set; } = 30;

        /// <summary>
        /// Intervalo de actualización completa (full info) en minutos.
        /// Recomendado: 5 minutos. 0 = solo bajo demanda.
        /// </summary>
        public int IpcInfoFullPollMinutes { get; set; } = 5;

        // ═══════════════════════════════════════════════════════════════════════════
        // 📋 AUDIT LOG - EU CRA Compliance (CADRA/Alstom)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar sistema de auditoría. Requerido por EU CRA y CADRA.
        /// </summary>
        public bool AuditLogEnabled { get; set; } = true;

        /// <summary>
        /// Días de retención de logs. Mínimo 30 días según CADRA.
        /// </summary>
        public int AuditLogRetentionDays { get; set; } = 30;

        /// <summary>
        /// URL externa para envío de logs al SOC (ej: SOC PIVOT TISSEO).
        /// Vacío = no enviar a externo.
        /// </summary>
        public string AuditLogExternalUrl { get; set; } = "";

        /// <summary>
        /// Habilitar envío de logs a URL externa.
        /// </summary>
        public bool AuditLogExternalEnabled { get; set; } = false;

        /// <summary>
        /// Habilitar firma SHA256 en cada entrada de log para integridad.
        /// </summary>
        public bool AuditLogSignatureEnabled { get; set; } = true;

        /// <summary>
        /// Máximo de entradas por archivo antes de rotar.
        /// </summary>
        public int AuditLogMaxEntriesPerFile { get; set; } = 10000;

        // ═══════════════════════════════════════════════════════════════════════════
        // 🔐 AUTHENTICATION - EU CRA Compliance (CADRA/Alstom Phase 2)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Modo de autenticación: Local, ActiveDirectory, Hybrid
        /// </summary>
        public string AuthMode { get; set; } = "Local";

        /// <summary>
        /// Habilitar Active Directory (deshabilitado por defecto)
        /// </summary>
        public bool AuthEnableActiveDirectory { get; set; } = false;

        /// <summary>
        /// Servidor Active Directory (LDAP://server:port)
        /// </summary>
        public string AuthADServer { get; set; } = "";

        /// <summary>
        /// Dominio Active Directory
        /// </summary>
        public string AuthADDomain { get; set; } = "";

        /// <summary>
        /// Base DN para búsquedas en AD
        /// </summary>
        public string AuthADBaseDN { get; set; } = "";

        /// <summary>
        /// Timeout de conexión AD en segundos
        /// </summary>
        public int AuthADTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Si AD falla, usar autenticación local como fallback
        /// </summary>
        public bool AuthFallbackToLocal { get; set; } = true;

        /// <summary>
        /// Ruta a la base de datos SQLite
        /// </summary>
        public string AuthDatabasePath { get; set; } = "Data/Aquafrisch.db";

        /// <summary>
        /// Longitud mínima de contraseña (CADRA: 12 caracteres mínimo)
        /// </summary>
        public int AuthPasswordMinLength { get; set; } = 12;

        /// <summary>
        /// Requerir mayúsculas en contraseña
        /// </summary>
        public bool AuthRequireUppercase { get; set; } = true;

        /// <summary>
        /// Requerir minúsculas en contraseña
        /// </summary>
        public bool AuthRequireLowercase { get; set; } = true;

        /// <summary>
        /// Requerir números en contraseña
        /// </summary>
        public bool AuthRequireNumbers { get; set; } = true;

        /// <summary>
        /// Requerir caracteres especiales en contraseña
        /// </summary>
        public bool AuthRequireSpecialChars { get; set; } = true;

        /// <summary>
        /// Máximo de intentos de login fallidos antes de bloquear
        /// </summary>
        public int AuthMaxLoginAttempts { get; set; } = 6;

        /// <summary>
        /// Minutos de bloqueo después de exceder intentos
        /// </summary>
        public int AuthLockoutMinutes { get; set; } = 15;

        /// <summary>
        /// Timeout de sesión en minutos
        /// </summary>
        public int AuthSessionTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Forzar cambio de contraseña en primer login
        /// </summary>
        public bool AuthForcePasswordChangeOnFirstLogin { get; set; } = true;

        /// <summary>
        /// Mostrar banner de advertencia en login
        /// </summary>
        public bool AuthShowLoginBanner { get; set; } = true;

        /// <summary>
        /// Clave secreta JWT (se genera automáticamente si está vacío)
        /// </summary>
        public string AuthJwtSecretKey { get; set; } = "";

        /// <summary>
        /// Emisor JWT
        /// </summary>
        public string AuthJwtIssuer { get; set; } = "AquafrischSupervisor";

        /// <summary>
        /// Audiencia JWT
        /// </summary>
        public string AuthJwtAudience { get; set; } = "AquafrischClients";

        // ===== 🔐 SESSION MANAGEMENT - EU CRA Compliance (Phase 3) =====

        /// <summary>
        /// Máximo de sesiones concurrentes por usuario (0=ilimitado)
        /// </summary>
        public int AuthMaxConcurrentSessions { get; set; } = 2;

        /// <summary>
        /// Roles con sesión única (solo 1 usuario del rol activo a la vez)
        /// Separados por coma. Ej: "Operator,Maintenance"
        /// </summary>
        public string AuthSingleSessionRoles { get; set; } = "Operator";

        /// <summary>
        /// Timeout por inactividad en minutos (0=deshabilitado)
        /// Cierra sesión automáticamente si no hay actividad
        /// </summary>
        public int AuthInactivityTimeoutMinutes { get; set; } = 15;

        /// <summary>
        /// Rastrear última actividad de cada sesión
        /// Necesario para InactivityTimeout y auditoría
        /// </summary>
        public bool AuthTrackLastActivity { get; set; } = true;

        /// <summary>
        /// Comportamiento cuando usuario de rol único intenta login y ya hay otro activo:
        /// "reject" = Rechazar nuevo login
        /// "force" = Expulsar sesión anterior
        /// </summary>
        public string AuthSingleSessionBehavior { get; set; } = "reject";

        // ===== 🔐 PHASE 4: RBAC - Role Based Access Control (EU CRA) =====
        
        /// <summary>
        /// Rol por defecto asignado a nuevos usuarios creados por administrador
        /// Valores: Viewer, Operator, Maintenance, Auditor
        /// </summary>
        public string AuthDefaultRole { get; set; } = "Viewer";

        /// <summary>
        /// Habilitar rol de invitado (usuario anónimo con permisos limitados)
        /// CADRA recomienda deshabilitar en producción
        /// </summary>
        public bool AuthEnableGuestRole { get; set; } = false;

        /// <summary>
        /// Permisos para rol invitado (si está habilitado)
        /// Formato: area1:perm1,area2:perm2
        /// Ejemplo: "plc:read,alarms:read"
        /// </summary>
        public string AuthGuestPermissions { get; set; } = "plc:read";

        /// <summary>
        /// Requerir aprobación de administrador para nuevos usuarios
        /// Si true, usuarios creados están inactivos hasta aprobación
        /// </summary>
        public bool AuthRequireUserApproval { get; set; } = true;

        /// <summary>
        /// Notificar a administradores cuando se crea nuevo usuario
        /// </summary>
        public bool AuthNotifyAdminOnNewUser { get; set; } = true;

        /// <summary>
        /// Permisos adicionales para rol Operator (override desde Excel)
        /// Formato: area1:perm1,area2:perm2
        /// Ejemplo: "reports:export" para dar export además de los permisos base
        /// </summary>
        public string AuthOperatorExtraPermissions { get; set; } = "";

        /// <summary>
        /// Permisos adicionales para rol Maintenance (override desde Excel)
        /// </summary>
        public string AuthMaintenanceExtraPermissions { get; set; } = "";

        /// <summary>
        /// Permisos restringidos para todos los roles excepto Administrator
        /// Formato: area1:perm1,area2:perm2
        /// Ejemplo: "backup:restore" para bloquear restore a no-admins
        /// </summary>
        public string AuthRestrictedPermissions { get; set; } = "backup:restore,security:update";

        /// <summary>
        /// Habilitar herencia de permisos en jerarquía de roles
        /// Si true: Admin > Maintenance > Operator > Viewer
        /// </summary>
        public bool AuthEnableRoleHierarchy { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════════════
        // 🖥️ KIOSK MODE - Herramientas del Sistema (IPCs industriales)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar modo kiosk (herramientas de sistema disponibles)
        /// </summary>
        public bool KioskModeEnabled { get; set; } = true;

        /// <summary>
        /// ID único de la instalación (para soporte técnico)
        /// Ej: "AQF-ALSTOM-001", "AQF-TISSEO-042"
        /// </summary>
        public string InstallationId { get; set; } = "AQF-DEFAULT-001";

        /// <summary>
        /// Roles autorizados para usar herramientas del sistema
        /// Separados por coma. Ej: "SuperAdmin,Administrator,Maintenance"
        /// </summary>
        public string AllowedSystemToolsRoles { get; set; } = "SuperAdmin,Administrator,Maintenance";

        /// <summary>
        /// Habilitar botón de cerrar sesión de Windows
        /// </summary>
        public bool WindowsLogoutEnabled { get; set; } = true;

        /// <summary>
        /// Habilitar botón de reiniciar aplicación/kiosk
        /// </summary>
        public bool AppRestartEnabled { get; set; } = true;

        /// <summary>
        /// Habilitar diagnóstico de red
        /// </summary>
        public bool NetworkDiagnosticEnabled { get; set; } = true;

        /// <summary>
        /// IP del gateway para diagnóstico de red
        /// </summary>
        public string GatewayIP { get; set; } = "192.168.1.1";

        /// <summary>
        /// Ruta al navegador/aplicación del kiosk (para reiniciar)
        /// </summary>
        public string KioskBrowserPath { get; set; } = "";

        /// <summary>
        /// Argumentos para iniciar el navegador en modo kiosk
        /// Ej: "--kiosk http://localhost:3001"
        /// </summary>
        public string KioskBrowserArgs { get; set; } = "--kiosk http://localhost:3001";

        // ═══════════════════════════════════════════════════════════════════════════
        // 🖥️ TEAMVIEWER - Soporte Remoto
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar botón de TeamViewer
        /// </summary>
        public bool TeamViewerEnabled { get; set; } = true;

        /// <summary>
        /// Ruta personalizada a TeamViewer (vacío = autodetectar)
        /// </summary>
        public string TeamViewerPath { get; set; } = "";

        // ═══════════════════════════════════════════════════════════════════════════
        // 🔌 CUSTOM TOOLS - Software Adicional Configurable
        // ═══════════════════════════════════════════════════════════════════════════

        // --- HERRAMIENTA PERSONALIZADA 1 ---
        public bool CustomTool1Enabled { get; set; } = false;
        public string CustomTool1Name { get; set; } = "";
        public string CustomTool1Path { get; set; } = "";
        public string CustomTool1Args { get; set; } = "";
        public string CustomTool1Icon { get; set; } = "🔧";

        // --- HERRAMIENTA PERSONALIZADA 2 ---
        public bool CustomTool2Enabled { get; set; } = false;
        public string CustomTool2Name { get; set; } = "";
        public string CustomTool2Path { get; set; } = "";
        public string CustomTool2Args { get; set; } = "";
        public string CustomTool2Icon { get; set; } = "⚙️";

        // --- HERRAMIENTA PERSONALIZADA 3 ---
        public bool CustomTool3Enabled { get; set; } = false;
        public string CustomTool3Name { get; set; } = "";
        public string CustomTool3Path { get; set; } = "";
        public string CustomTool3Args { get; set; } = "";
        public string CustomTool3Icon { get; set; } = "🔌";

        // ═══════════════════════════════════════════════════════════════════════════
        // 📞 SOPORTE AQUAFRISCH - "Llamar a Aquafrisch" (cualquier usuario)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar función "Llamar a Aquafrisch" (visible para TODOS los usuarios)
        /// </summary>
        public bool SupportUnlockEnabled { get; set; } = true;

        /// <summary>
        /// Teléfono de soporte técnico Aquafrisch
        /// </summary>
        public string SupportPhoneNumber { get; set; } = "+34 900 123 456";

        /// <summary>
        /// Email de soporte técnico Aquafrisch
        /// </summary>
        public string SupportEmail { get; set; } = "soporte@aquafrisch.com";

        /// <summary>
        /// Duración del desbloqueo temporal en minutos (tras código de soporte)
        /// </summary>
        public int SupportUnlockDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Fecha fin de soporte CRA (EU Cyber Resilience Act) - Año hasta el cual se garantiza soporte
        /// </summary>
        public int SupportEndYear { get; set; } = 2035;

        // NOTA: SupportChallengeSecret NO se configura desde Excel
        // Está hardcodeado en SupportController.cs (igual que RecoveryCodeService)
        // Solo Aquafrisch conoce el secreto

        // ═══════════════════════════════════════════════════════════════════════════
        // 🚿 WASH RECIPE - Tipos de Lavado (Sistema de Recetas)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar módulo de Tipos de Lavado.
        /// Si false, el botón desaparece del menú lateral.
        /// </summary>
        public bool WashRecipeEnabled { get; set; } = true;

        /// <summary>
        /// Variable PLC para auto-carga de receta (PLC1).
        /// Cuando TwinCAT escribe un número != 0, el backend automáticamente
        /// carga la receta de esa línea y luego resetea la variable a 0.
        /// Ejemplo: "GVL.nAutoLoadRecipe"
        /// </summary>
        public string WashRecipeAutoLoadVar { get; set; } = "";

        /// <summary>
        /// Variable PLC para auto-carga de receta (PLC2/Alternativo).
        /// Igual que WashRecipeAutoLoadVar pero para el segundo PLC.
        /// Ejemplo: "GVL.nAutoLoadRecipe_2"
        /// </summary>
        public string WashRecipeAutoLoadVar2 { get; set; } = "";

        // ═══════════════════════════════════════════════════════════════════════════
        // 🚆 TRAIN RECIPE - Tipos de Tren (Sistema de Recetas de Trenes)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar módulo de Tipos de Tren.
        /// Si false, el botón desaparece del menú lateral.
        /// </summary>
        public bool TrainRecipeEnabled { get; set; } = true;

        /// <summary>
        /// Variable PLC para auto-carga de tipo de tren.
        /// Cuando TwinCAT escribe un número != 0, el backend automáticamente
        /// carga el tipo de tren de esa línea y luego resetea la variable a 0.
        /// Ejemplo: "GVL.nAutoLoadTrainType"
        /// </summary>
        public string TrainRecipeAutoLoadVar { get; set; } = "";

        /// <summary>
        /// Variable PLC para auto-carga de tipo de tren (PLC2/Alternativo).
        /// Igual que TrainRecipeAutoLoadVar pero para el segundo PLC.
        /// Ejemplo: "GVL.nAutoLoadTrainType_2"
        /// </summary>
        public string TrainRecipeAutoLoadVar2 { get; set; } = "";

        // ═══════════════════════════════════════════════════════════════════════════
        // ⚡ SEMIAUTOMATIC MODE - Modo Semiautomático
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar módulo de Modo Semiautomático.
        /// Si false, el botón desaparece de la TopBar.
        /// </summary>
        public bool SemiautomaticEnabled { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════════════
        // ⚡ FAST CONFIGURATION - Panel de Configuración Rápida
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar módulo de Configuración Rápida (Fast Configuration).
        /// Si false, el botón desaparece de la TopBar.
        /// </summary>
        public bool FastConfigurationEnabled { get; set; } = false;

        // ═══════════════════════════════════════════════════════════════════════════
        // 📷 3D SCENE / CAMERA - Configuración de escena 3D Babylon.js
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Factor de zoom de la cámara para la vista estándar/inicial.
        /// Controla la distancia de la cámara al modelo en la vista por defecto.
        /// - 1.0 = distancia normal (100%)
        /// - 0.5 = más cerca (50% de la distancia normal)
        /// - 0.8 = un poco más cerca (80%)
        /// - 1.2 = un poco más lejos (120%)
        /// - 2.0 = muy lejos (200%)
        /// </summary>
        public double CameraZoomFactor { get; set; } = 1.0;

        // ═══════════════════════════════════════════════════════════════════════════
        // 🚂 RIDE CAMERA - Cámara montada en modelo móvil (tren)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lista de ModelIds que permiten "Ride Camera" (cámara montada).
        /// Formato: IDs separados por coma.
        /// Ejemplo: "tren_lavado_1,tren_lavado_2,tren_lavado_3"
        /// El índice en esta lista corresponde con los offsets.
        /// </summary>
        public string RideableModelIds { get; set; } = "";

        /// <summary>
        /// Offsets de cámara frontal para cada modelo rideable.
        /// Formato: vectores (x,y,z) separados por coma.
        /// Ejemplo: "(0,1.5,2),(0,1.5,2),(0,2,3)"
        /// El índice corresponde con RideableModelIds.
        /// </summary>
        public string RideCameraFrontOffsets { get; set; } = "";

        /// <summary>
        /// Offsets de cámara trasera para cada modelo rideable.
        /// Formato: vectores (x,y,z) separados por coma.
        /// Ejemplo: "(0,1.5,-2),(0,1.5,-2),(0,2,-3)"
        /// El índice corresponde con RideableModelIds.
        /// </summary>
        public string RideCameraRearOffsets { get; set; } = "";

        /// <summary>
        /// Variable PLC que contiene la posición del tren (eje de movimiento).
        /// Ejemplo: "MAIN.fbMachine.rTrainPosition"
        /// </summary>
        public string RideCameraTrainPositionVar { get; set; } = "";

        /// <summary>
        /// Eje de movimiento de cada modelo rideable.
        /// Formato: valores separados por coma (X, -X, Z, -Z).
        /// Ejemplo: "Z,Z,Z" para 3 modelos que se mueven en eje Z.
        /// La cámara frontal mira en esta dirección, la trasera en la opuesta.
        /// El índice corresponde con RideableModelIds.
        /// </summary>
        public string RideCameraMovementAxes { get; set; } = "";

        // ═══════════════════════════════════════════════════════════════════════════
        // 🌐 ETHERCAT TOPOLOGY - Diagnóstico de Red Industrial
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Habilitar diagnóstico de topología EtherCAT.
        /// Permite visualizar el estado de la red industrial en el InfoPanel.
        /// </summary>
        public bool EnableEtherCATTopology { get; set; } = false;

        /// <summary>
        /// AMS Net ID del Master EtherCAT.
        /// Puede ser el mismo que PlcAmsNetId si el Master está en el mismo runtime,
        /// o diferente si es un Master dedicado.
        /// Ejemplo: "192.168.1.151.3.1" o "5.89.194.238.3.1"
        /// </summary>
        public string EtherCATMasterNetId { get; set; } = "";

        /// <summary>
        /// Dirección IP del PC con TwinCAT (ej: 192.168.1.160).
        /// Necesaria para conexión ADS remota cuando no hay ruta preconfigurada.
        /// Si vacío, se extrae de los primeros 4 octetos del NetId.
        /// </summary>
        public string EtherNETIdTwincat { get; set; } = "";

        /// <summary>
        /// Ruta a los archivos ESI (EtherCAT Slave Information).
        /// Si está vacío, usa la ruta estándar de TwinCAT: C:\TwinCAT\3.1\Config\Io\EtherCAT
        /// Los ESI files contienen nombres y descripciones de dispositivos EtherCAT.
        /// </summary>
        public string ESIFilesPath { get; set; } = "";

        /// <summary>
        /// Usar archivos ESI para obtener nombres de dispositivos.
        /// Si true, el servicio intentará leer los ESI files para mostrar
        /// nombres comerciales de los dispositivos (ej: "EL1008" en lugar de VendorId/ProductCode).
        /// </summary>
        public bool UseEtherCATESIFiles { get; set; } = false;

        /// <summary>
        /// Intervalo mínimo entre lecturas completas de topología (ms).
        /// Evita sobrecargar el Master EtherCAT con lecturas frecuentes.
        /// Recomendado: 2000ms (2 segundos).
        /// </summary>
        public int EtherCATTopologyReadIntervalMs { get; set; } = 2000;

        /// <summary>
        /// Nombre de la instancia del FB_EtherCATDiag en el PLC.
        /// Ejemplo: MAIN.fbEtherCATDiag, GVL.fbEtherCATDiag, PRG_Diagnostic.fbEtherCATDiag
        /// Este es el bloque de función de Beckhoff para diagnóstico EtherCAT.
        /// </summary>
        public string EtherCATDiagFbInstance { get; set; } = "MAIN.fbEtherCATDiag";

        // ═══════════════════════════════════════════════════════════════════════════
        // 🌐 INTERNATIONALIZATION (i18n) - Sistema de traducciones
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Idioma por defecto del sistema (ISO 639-2: SPA, ENG, FRA, ITA, DEU, etc.)
        /// Se usa como fallback cuando no existe traducción en el idioma seleccionado.
        /// </summary>
        public string DefaultLanguage { get; set; } = "SPA";

        /// <summary>
        /// Modo DEBUG: Mostrar IDs de labels en la interfaz.
        /// Cuando está activado, junto a cada texto traducible se muestra su ID.
        /// Útil para técnicos durante el proceso de traducción.
        /// En producción debe estar en false.
        /// </summary>
        public bool ExposeLabelIds { get; set; } = false;
    }

    /// <summary>
    /// Configuración del modo semiautomático desde Excel (hoja "Semiautomatic_Mode")
    /// </summary>
    public class SemiautomaticConfiguration
    {
        /// <summary>
        /// Variable PLC principal para activar/desactivar el modo semiautomático
        /// (Celda A2 del Excel)
        /// </summary>
        public string MainPlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Lista de elementos bool toggle del modo semiautomático
        /// </summary>
        public List<SemiautomaticElement> Elements { get; set; } = new();
    }

    /// <summary>
    /// Elemento individual del modo semiautomático
    /// </summary>
    public class SemiautomaticElement
    {
        /// <summary>
        /// Descripción del elemento (Columna B)
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Variable PLC bool toggle para este elemento (Columna C)
        /// </summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>
        /// Modo de visibilidad del elemento (Columna D):
        /// 0 = Invisible siempre
        /// 1 = Visible siempre
        /// 2 = Visible solo cuando el modo semiautomático está activo
        /// </summary>
        public int VisibilityMode { get; set; } = 1;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ⚡ FAST CONFIGURATION - Panel de Configuración Rápida (hoja "Fast_Configuration")
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuración del panel de configuración rápida desde Excel (hoja "Fast_Configuration")
    /// Similar a SettingsPage pero con estructura específica:
    /// - Columna A: Título página (solo A2)
    /// - Columnas B-E: Parámetros BOOL
    /// - Columnas F-L: Parámetros INT
    /// - Columnas M-T: Parámetros LREAL
    /// </summary>
    public class FastConfigurationPageConfiguration
    {
        /// <summary>
        /// Título de la página completa (celda A2)
        /// </summary>
        public string PageTitle { get; set; } = "Configuración Rápida";

        /// <summary>
        /// Título de la sección de booleanos (celda B2)
        /// </summary>
        public string BoolSectionTitle { get; set; } = "Booleanos";

        /// <summary>
        /// Título de la sección de enteros (celda F2)
        /// </summary>
        public string IntSectionTitle { get; set; } = "Enteros";

        /// <summary>
        /// Título de la sección de decimales (celda M2)
        /// </summary>
        public string LRealSectionTitle { get; set; } = "Decimales";

        /// <summary>
        /// Parámetros booleanos desde Excel (Columnas C, D, E)
        /// </summary>
        public List<FastConfigBoolSetting> BoolSettings { get; set; } = new();

        /// <summary>
        /// Parámetros enteros desde Excel (Columnas G-L)
        /// </summary>
        public List<FastConfigIntSetting> IntSettings { get; set; } = new();

        /// <summary>
        /// Parámetros LReal desde Excel (Columnas N-T)
        /// </summary>
        public List<FastConfigLRealSetting> LRealSettings { get; set; } = new();
    }

    /// <summary>
    /// Parámetro bool desde hoja Fast_Configuration (columnas C, D, E)
    /// </summary>
    public class FastConfigBoolSetting
    {
        /// <summary>Descripción del parámetro (columna C)</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna D)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna E)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }
    }

    /// <summary>
    /// Parámetro int desde hoja Fast_Configuration (columnas G-L)
    /// </summary>
    public class FastConfigIntSetting
    {
        /// <summary>Descripción del parámetro (columna G)</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna H)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna I)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Valor mínimo (columna J, opcional)</summary>
        public int? MinValue { get; set; }

        /// <summary>Valor máximo (columna K, opcional)</summary>
        public int? MaxValue { get; set; }

        /// <summary>Unidad de medida (columna L, opcional)</summary>
        public string? Unit { get; set; }

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }
    }

    /// <summary>
    /// Parámetro LReal desde hoja Fast_Configuration (columnas N-T)
    /// </summary>
    public class FastConfigLRealSetting
    {
        /// <summary>Descripción del parámetro (columna N)</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Ruta imagen de ayuda (columna O)</summary>
        public string? ImagePath { get; set; }

        /// <summary>Variable PLC (columna P)</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Valor mínimo (columna Q, opcional)</summary>
        public double? MinValue { get; set; }

        /// <summary>Valor máximo (columna R, opcional)</summary>
        public double? MaxValue { get; set; }

        /// <summary>Decimales a mostrar (columna S, opcional)</summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>Unidad de medida (columna T, opcional)</summary>
        public string? Unit { get; set; }

        /// <summary>Orden de fila en Excel (para DisplayOrder)</summary>
        public int RowIndex { get; set; }
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
        public DateTime LastUpdate { get; set; } = DateTime.Now;

        /// <summary>
        /// Uptime del servidor en formato legible
        /// </summary>
        public string ServerUptime { get; set; } = "00:00:00";

        // ===== ESTADO DE SISTEMAS HABILITADOS =====
        
        /// <summary>
        /// Estado de los servicios del sistema
        /// </summary>
        public SystemServicesStatus ServicesStatus { get; set; } = new SystemServicesStatus();

        // ===== 🔐 SOFTWARE VERSIONS - CYBERSECURITY COMPLIANCE =====

        /// <summary>
        /// Información de versiones y verificación de integridad del software
        /// </summary>
        public SoftwareVersionInfo SoftwareVersions { get; set; } = new SoftwareVersionInfo();
    }

    /// <summary>
    /// 🔐 Información de versiones de software basada en Git commits
    /// Para cumplimiento de normativas de ciberseguridad NASA/NIST
    /// </summary>
    public class SoftwareVersionInfo
    {
        // ===== IDENTIFICACIÓN DE MÁQUINA (EU CRA) =====
        /// <summary>Nombre del PC donde se ejecuta el sistema</summary>
        public string MachineName { get; set; } = Environment.MachineName;
        
        // ===== COMPONENTES CON CONTROL DE VERSIONES GIT =====
        public GitVersionComponent Backend { get; set; } = new();
        public GitVersionComponent Frontend { get; set; } = new();
        public GitVersionComponent TwinCatPlc { get; set; } = new();

        // ===== INFORMACIÓN DE RUNTIME (sin Git) =====
        public RuntimeVersionInfo TwinCatRuntime { get; set; } = new();
        public RuntimeVersionInfo AdsClient { get; set; } = new();
        public RuntimeVersionInfo Database { get; set; } = new();

        // ===== METADATOS DE VERIFICACIÓN =====
        public string LastVerificationDate { get; set; } = "Never";
        public string VerifiedByAdmin { get; set; } = "System";
        public string SystemStatus { get; set; } = "unknown"; // "clean", "modified", "unknown"

        // ===== TIMER DE RE-VERIFICACIÓN AUTOMÁTICA =====
        /// <summary>Próxima verificación programada (UTC ISO string)</summary>
        public string NextVerificationTime { get; set; } = "Pending";
        
        /// <summary>Intervalo de verificación en segundos (default: 120 = 2 min)</summary>
        public int VerificationIntervalSeconds { get; set; } = 120;
        
        /// <summary>Segundos restantes hasta próxima verificación</summary>
        public int SecondsUntilNextVerification { get; set; } = 0;
        
        /// <summary>Indica si la verificación automática está activa</summary>
        public bool AutoVerificationEnabled { get; set; } = true;

        // ===== ESTADO DE RED Y SINCRONIZACIÓN CON REMOTO =====
        /// <summary>Estado de conectividad y sincronización con repositorios remotos</summary>
        public SW.PC.API.Backend.Services.NetworkSyncStatus? NetworkStatus { get; set; }
    }

    /// <summary>
    /// Información de versión basada en Git commit
    /// </summary>
    public class GitVersionComponent
    {
        /// <summary>Nombre del componente</summary>
        public string Name { get; set; } = "Unknown";

        /// <summary>Tag de versión semántica (ej: v1.2.3)</summary>
        public string Version { get; set; } = "0.0.0";

        /// <summary>SHA corto del commit (7-8 caracteres)</summary>
        public string CommitSha { get; set; } = "unknown";

        /// <summary>SHA completo del commit (40 caracteres)</summary>
        public string CommitShaFull { get; set; } = "unknown";

        /// <summary>Rama actual</summary>
        public string Branch { get; set; } = "unknown";

        /// <summary>Fecha del commit</summary>
        public string CommitDate { get; set; } = "unknown";

        /// <summary>Autor del commit (nombre)</summary>
        public string CommitAuthor { get; set; } = "unknown";

        /// <summary>Email del autor del commit</summary>
        public string CommitAuthorEmail { get; set; } = "unknown";

        /// <summary>Mensaje del commit</summary>
        public string CommitMessage { get; set; } = "";

        /// <summary>Estado del working directory: "clean", "dirty", "unknown"</summary>
        public string WorkingDirStatus { get; set; } = "unknown";

        /// <summary>Archivos modificados (si dirty)</summary>
        public int ModifiedFiles { get; set; } = 0;

        /// <summary>Estado de integridad: "verified", "modified", "unknown"</summary>
        public string Integrity { get; set; } = "unknown";

        /// <summary>Última vez que se verificó este componente</summary>
        public string LastVerified { get; set; } = "Never";

        /// <summary>Ruta del repositorio</summary>
        public string RepoPath { get; set; } = "";

        // === FIRMA DIGITAL (GPG/SSH) ===
        
        /// <summary>Si el commit está firmado (GPG o SSH)</summary>
        public bool IsSigned { get; set; } = false;

        /// <summary>Estado de la firma: "valid", "invalid", "unsigned", "unknown"</summary>
        public string SignatureStatus { get; set; } = "unknown";

        /// <summary>Tipo de firma: "GPG", "SSH", "X509", "none"</summary>
        public string SignatureType { get; set; } = "none";

        /// <summary>ID de la clave usada para firmar (Key ID)</summary>
        public string SignatureKeyId { get; set; } = "";

        /// <summary>Nombre del firmante (de la clave GPG/SSH)</summary>
        public string SignatureSigner { get; set; } = "";

        /// <summary>Mensaje de verificación de firma</summary>
        public string SignatureMessage { get; set; } = "";

        // === RELEASE VERSION (CalVer) ===
        
        /// <summary>Última versión release (tag CalVer, ej: 2025.12.01)</summary>
        public string LatestRelease { get; set; } = "";

        /// <summary>Fecha del último release</summary>
        public string LatestReleaseDate { get; set; } = "";
    }

    /// <summary>
    /// Información de versión para componentes de runtime (sin Git)
    /// </summary>
    public class RuntimeVersionInfo
    {
        /// <summary>Nombre del componente</summary>
        public string Name { get; set; } = "Unknown";

        /// <summary>Versión del componente</summary>
        public string Version { get; set; } = "Unknown";

        /// <summary>Estado: "connected", "disconnected", "disabled"</summary>
        public string Status { get; set; } = "unknown";

        /// <summary>Información adicional</summary>
        public string Details { get; set; } = "";
        
        /// <summary>Task Cycle Time del PLC en milisegundos (solo para TwinCAT Runtime)</summary>
        public double? TaskCycleTimeMs { get; set; }
    }

    /// <summary>
    /// Información detallada de versión de TwinCAT
    /// </summary>
    public class TwinCATVersionInfo
    {
        public string RuntimeVersion { get; set; } = "Unknown";
        public string AdsVersion { get; set; } = "Unknown";
        public string DeviceName { get; set; } = "Unknown";
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public int BuildNumber { get; set; }
        public int RevisionNumber { get; set; }
        public string TargetNetId { get; set; } = "Unknown";
        public string DeviceState { get; set; } = "Unknown";
        public bool IsConnected { get; set; }
        public bool IsSimulated { get; set; }
        
        /// <summary>Task Cycle Time del PLC en microsegundos (100ns units from TwinCAT)</summary>
        public long TaskCycleTime100ns { get; set; }
        
        /// <summary>Task Cycle Time del PLC en milisegundos (para display)</summary>
        public double TaskCycleTimeMs { get; set; }
        
        /// <summary>Nombre de la tarea principal del PLC</summary>
        public string TaskName { get; set; } = "PlcTask";
    }

    /// <summary>
    /// Estado de los servicios habilitados del sistema
    /// </summary>
    public class SystemServicesStatus
    {
        /// <summary>
        /// PLC Polling habilitado en configuración
        /// </summary>
        public bool PlcPollingEnabled { get; set; }

        /// <summary>
        /// PLC Polling funcionando correctamente
        /// </summary>
        public bool PlcPollingConnected { get; set; }

        /// <summary>
        /// PLC en modo simulado (no conectado a PLC real)
        /// </summary>
        public bool PlcIsSimulated { get; set; }

        /// <summary>
        /// Último mensaje de estado del PLC
        /// </summary>
        public string PlcPollingStatus { get; set; } = "No iniciado";

        /// <summary>
        /// SignalR habilitado en configuración
        /// </summary>
        public bool SignalREnabled { get; set; }

        /// <summary>
        /// SignalR Hub funcionando correctamente
        /// </summary>
        public bool SignalRConnected { get; set; }

        /// <summary>
        /// Último mensaje de estado de SignalR
        /// </summary>
        public string SignalRStatus { get; set; } = "No iniciado";

        /// <summary>
        /// Base de datos habilitada en configuración
        /// </summary>
        public bool DatabaseEnabled { get; set; }

        /// <summary>
        /// Base de datos conectada correctamente
        /// </summary>
        public bool DatabaseConnected { get; set; }

        /// <summary>
        /// Último mensaje de estado de la base de datos
        /// </summary>
        public string DatabaseStatus { get; set; } = "Deshabilitada";

        /// <summary>
        /// Usando PLC simulado (no real)
        /// </summary>
        public bool UseSimulatedPlc { get; set; }

        /// <summary>
        /// Timestamp de la última actualización de estados
        /// </summary>
        public DateTime LastStatusUpdate { get; set; } = DateTime.Now;

        // ===== 🔔 ALARM NOTIFICATIONS =====
        
        /// <summary>
        /// Servicio de notificaciones de alarma habilitado
        /// </summary>
        public bool AlarmNotificationEnabled { get; set; }
        
        /// <summary>
        /// Notificaciones de alarma activas y funcionando
        /// </summary>
        public bool AlarmNotificationActive { get; set; }
        
        /// <summary>
        /// Último mensaje de estado del servicio de notificaciones
        /// </summary>
        public string AlarmNotificationStatus { get; set; } = "No iniciado";
    }

    #region Alarm System Models
    
    /// <summary>
    /// Tipo de alarma del sistema SCADA
    /// </summary>
    public enum AlarmType
    {
        /// <summary>Alarma crítica - requiere atención inmediata</summary>
        Alarm = 0,
        /// <summary>Notificación - aviso importante pero no crítico</summary>
        Notification = 1,
        /// <summary>Información - mensaje informativo</summary>
        Info = 2
    }

    /// <summary>
    /// Definición de una alarma desde Excel con soporte multilenguaje.
    /// Estructura PLC: MAIN.fbMachine.st_alarmPc.{Type}[Index]
    /// Códigos de idioma ISO 639-2 (3 letras): SPA, ENG, ITA, FRA, RUS, CZE, DAN, VIE, TAI, IND, MAY, GRE
    /// </summary>
    public class AlarmDefinition
    {
        /// <summary>Índice de la alarma (1-based, coincide con array PLC)</summary>
        public int Index { get; set; }
        
        /// <summary>Tipo de alarma (Alarm, Notification, Info)</summary>
        public AlarmType Type { get; set; }
        
        /// <summary>Variable PLC completa (ej: MAIN.fbMachine.st_alarmPc.Alarm[1])</summary>
        public string PlcVariable { get; set; } = string.Empty;
        
        /// <summary>Textos multilenguaje (clave = código ISO 639-2: "SPA", "ENG", "ITA", etc.)</summary>
        public Dictionary<string, string> Texts { get; set; } = new();
        
        /// <summary>Texto en español (acceso directo)</summary>
        public string TextSPA => Texts.GetValueOrDefault("SPA", $"Alarma {Type} #{Index}");
        
        /// <summary>Texto en inglés (acceso directo)</summary>
        public string TextENG => Texts.GetValueOrDefault("ENG", $"Alarm {Type} #{Index}");
        
        /// <summary>Obtener texto en el idioma especificado con fallback (ISO 639-2)</summary>
        public string GetText(string languageCode)
        {
            var code = languageCode?.ToUpperInvariant() ?? "SPA";
            
            // Convertir códigos de 2 letras a 3 letras si es necesario
            code = ConvertToISO639_2(code);
            
            // Intentar idioma exacto
            if (Texts.TryGetValue(code, out var text))
                return text;
            
            // Fallback a español
            if (Texts.TryGetValue("SPA", out var textSpa))
                return textSpa;
            
            // Fallback a inglés
            if (Texts.TryGetValue("ENG", out var textEng))
                return textEng;
            
            // Fallback genérico
            return $"{Type} #{Index}";
        }
        
        /// <summary>
        /// Convierte código de idioma de 2 letras (ISO 639-1) a 3 letras (ISO 639-2)
        /// </summary>
        private static string ConvertToISO639_2(string code)
        {
            if (code.Length == 3) return code;
            
            return code.ToUpperInvariant() switch
            {
                "ES" => "SPA",
                "EN" => "ENG",
                "IT" => "ITA",
                "FR" => "FRA",
                "RU" => "RUS",
                "CS" => "CZE",
                "DA" => "DAN",
                "VI" => "VIE",
                "TH" => "TAI",
                "ID" => "IND",
                "MS" => "MAY",
                "EL" => "GRE",
                "DE" => "DEU",
                "PT" => "POR",
                "NL" => "DUT",
                "PL" => "POL",
                "ZH" => "CHI",
                "JA" => "JPN",
                "KO" => "KOR",
                "AR" => "ARA",
                _ => code // Devolver el código original si no hay mapeo
            };
        }
    }

    /// <summary>
    /// Estado actual de una alarma (combinación de definición + estado del PLC)
    /// </summary>
    public class AlarmState
    {
        /// <summary>Definición de la alarma</summary>
        public AlarmDefinition Definition { get; set; } = new();
        
        /// <summary>Estado actual (true = activa)</summary>
        public bool IsActive { get; set; }
        
        /// <summary>Timestamp de activación</summary>
        public DateTime? ActivatedAt { get; set; }
        
        /// <summary>Timestamp de desactivación</summary>
        public DateTime? DeactivatedAt { get; set; }
        
        /// <summary>¿Ha sido reconocida por el operador?</summary>
        public bool IsAcknowledged { get; set; }
        
        /// <summary>Usuario que reconoció la alarma</summary>
        public string? AcknowledgedBy { get; set; }
        
        /// <summary>Timestamp del reconocimiento</summary>
        public DateTime? AcknowledgedAt { get; set; }
    }

    /// <summary>
    /// Configuración completa de alarmas cargada desde Excel
    /// </summary>
    public class AlarmConfiguration
    {
        /// <summary>Lista de definiciones de alarmas tipo Alarm (máx 300)</summary>
        public List<AlarmDefinition> Alarms { get; set; } = new();
        
        /// <summary>Lista de definiciones de alarmas tipo Notification (máx 100)</summary>
        public List<AlarmDefinition> Notifications { get; set; } = new();
        
        /// <summary>Lista de definiciones de alarmas tipo Info (máx 50)</summary>
        public List<AlarmDefinition> Infos { get; set; } = new();
        
        /// <summary>Idiomas disponibles detectados en Excel (ISO 639-2: SPA, ENG, ITA, etc.)</summary>
        public List<string> AvailableLanguages { get; set; } = new() { "SPA", "ENG" };
        
        /// <summary>Timestamp de última carga desde Excel</summary>
        public DateTime LoadedAt { get; set; } = DateTime.Now;
        
        /// <summary>Ruta del archivo Excel de origen</summary>
        public string SourceFile { get; set; } = string.Empty;
        
        /// <summary>Total de alarmas definidas</summary>
        public int TotalCount => Alarms.Count + Notifications.Count + Infos.Count;
        
        /// <summary>Obtener todas las definiciones como lista plana</summary>
        public IEnumerable<AlarmDefinition> GetAll() => 
            Alarms.Concat(Notifications).Concat(Infos);
        
        /// <summary>Buscar definición por variable PLC</summary>
        public AlarmDefinition? FindByPlcVariable(string plcVariable) =>
            GetAll().FirstOrDefault(a => a.PlcVariable.Equals(plcVariable, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resumen de alarmas activas para el frontend
    /// </summary>
    public class AlarmSummary
    {
        /// <summary>Total de alarmas activas</summary>
        public int ActiveAlarmsCount { get; set; }
        
        /// <summary>Total de notificaciones activas</summary>
        public int ActiveNotificationsCount { get; set; }
        
        /// <summary>Total de infos activos</summary>
        public int ActiveInfosCount { get; set; }
        
        /// <summary>Alarmas activas (lista detallada)</summary>
        public List<AlarmState> ActiveAlarms { get; set; } = new();
        
        /// <summary>Notificaciones activas (lista detallada)</summary>
        public List<AlarmState> ActiveNotifications { get; set; } = new();
        
        /// <summary>Infos activos (lista detallada)</summary>
        public List<AlarmState> ActiveInfos { get; set; } = new();
        
        /// <summary>Timestamp de la última actualización</summary>
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

    #endregion

    #region Variable Views Mapping

    /// <summary>
    /// Vistas disponibles en el frontend para filtrado de variables PLC
    /// </summary>
    public static class PlcViewIds
    {
        /// <summary>Variables que siempre se leen (alarmas, estados críticos)</summary>
        public const string GLOBAL = "GLOBAL";
        
        /// <summary>Vista Principal 3D (animaciones, estados visuales)</summary>
        public const string MAIN = "MAIN";
        
        /// <summary>Tipos de Tren y Editor de Tren</summary>
        public const string TRAIN = "TRAIN";
        
        /// <summary>Tipos de Lavado y Editor de Lavado</summary>
        public const string WASH = "WASH";
        
        /// <summary>Configuración de Máquina (Settings)</summary>
        public const string SETTINGS = "SETTINGS";
        
        /// <summary>Estadísticas</summary>
        public const string STATS = "STATS";
        
        /// <summary>Panel de alarmas</summary>
        public const string ALARMS = "ALARMS";
        
        /// <summary>Gestión de usuarios</summary>
        public const string USERS = "USERS";
        
        /// <summary>Modo Manual / Mantenimiento (JOG)</summary>
        public const string MANUAL = "MANUAL";

        /// <summary>Modo Semiautomático</summary>
        public const string SEMIAUTOMATIC = "SEMIAUTOMATIC";

        /// <summary>Detalle de modelo 3D (panel temporal)</summary>
        public const string MODEL_DETAIL = "MODEL_DETAIL";

        /// <summary>Panel de pantalla (panel temporal)</summary>
        public const string SCREEN_PANEL = "SCREEN_PANEL";

        /// <summary>Vistas principales (polling continuo)</summary>
        public static readonly string[] AllViews = { GLOBAL, MAIN, TRAIN, WASH, SETTINGS, STATS, ALARMS, USERS, MANUAL, SEMIAUTOMATIC };

        /// <summary>Vistas adicionales (temporales, activadas por demanda)</summary>
        public static readonly string[] AdditionalViews = { MODEL_DETAIL, SCREEN_PANEL };

        /// <summary>Todas las vistas válidas (principales + adicionales) - para parseo de Excel</summary>
        public static readonly string[] AllViewsIncludingAdditional = { GLOBAL, MAIN, TRAIN, WASH, SETTINGS, STATS, ALARMS, USERS, MANUAL, SEMIAUTOMATIC, MODEL_DETAIL, SCREEN_PANEL };

        /// <summary>
        /// Mapeo de currentView del frontend a PlcViewId
        /// </summary>
        public static string FromFrontendView(string currentView) => currentView?.ToLower() switch
        {
            "principal" => MAIN,
            "alarmas" => ALARMS,
            "estadisticas" => STATS,
            "usuarios" => USERS,
            "configuracion" => SETTINGS,
            "tipostren" => TRAIN,
            "tiposlavado" => WASH,
            "manual" => MANUAL,
            "semiautomatic" => SEMIAUTOMATIC,
            // Vistas adicionales (ya vienen en mayúsculas del frontend)
            "screen_panel" => SCREEN_PANEL,
            "model_detail" => MODEL_DETAIL,
            _ => currentView?.ToUpper() ?? MAIN // Si no se reconoce, devolver tal cual en mayúsculas
        };
    }

    /// <summary>
    /// Mapeo de patrón de variable a vistas donde debe leerse.
    /// Cargado desde hoja "Variable_Views" del Excel.
    /// </summary>
    public class VariableViewMapping
    {
        /// <summary>
        /// Patrón de nombre de variable. Soporta wildcards (*).
        /// Ejemplos: "st_pump[*].*", "st_Mainform.MachineState", "position*"
        /// </summary>
        public string VariablePattern { get; set; } = string.Empty;

        /// <summary>
        /// Lista de vistas donde esta variable debe leerse.
        /// Valores: GLOBAL, MAIN, CONFIG, STATS, ALARMS, USERS
        /// </summary>
        public List<string> Views { get; set; } = new();

        /// <summary>Descripción opcional para documentación</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Regex compilado para matching eficiente (se genera al cargar)
        /// </summary>
        public System.Text.RegularExpressions.Regex? CompiledPattern { get; set; }

        /// <summary>
        /// Indica si es un patrón exacto (sin wildcards) para prioridad
        /// </summary>
        public bool IsExactMatch => !VariablePattern.Contains('*');

        /// <summary>
        /// Número de caracteres antes del primer wildcard (para ordenar por especificidad)
        /// </summary>
        public int Specificity => VariablePattern.IndexOf('*') == -1 
            ? int.MaxValue // Match exacto = máxima especificidad
            : VariablePattern.IndexOf('*');
    }

    #endregion

    #region PLC InfoPanel Configuration

    /// <summary>
    /// Configuración completa de la card PLC InfoPanel.
    /// Cargada desde la hoja "Plc_InfoPanel" del Excel.
    /// </summary>
    public class PlcInfoPanelConfig
    {
        /// <summary>Título de la card (celda A2)</summary>
        public string Title { get; set; } = "PLC INFO";

        /// <summary>Icono del título (celda B2, opcional)</summary>
        public string? TitleIcon { get; set; }

        /// <summary>Contenido del botón de ayuda "i" (celda C2)</summary>
        public string HelpContent { get; set; } = string.Empty;

        /// <summary>Lista de líneas de datos</summary>
        public List<PlcInfoPanelLine> Lines { get; set; } = new();

        /// <summary>Lista de todas las variables PLC usadas (para Variable_Views)</summary>
        public List<string> AllVariables => Lines.Select(l => l.PlcVariable).ToList();

        /// <summary>Indica si la configuración está habilitada (hoja existe y tiene datos)</summary>
        public bool IsEnabled { get; set; } = false;
    }

    /// <summary>
    /// Una línea de datos en la card PLC InfoPanel.
    /// </summary>
    public class PlcInfoPanelLine
    {
        /// <summary>Nombre/descripción de la línea (columna D)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Icono de la línea (columna E, opcional)</summary>
        public string? Icon { get; set; }

        /// <summary>Nombre de la variable PLC (columna F) - WSTRING solo lectura</summary>
        public string PlcVariable { get; set; } = string.Empty;

        /// <summary>Valor actual de la variable (se actualiza en runtime)</summary>
        public string? CurrentValue { get; set; }
    }

    #endregion

    #region 🚂 Ride Camera - Cámara montada en tren

    /// <summary>
    /// Configuración completa de Ride Camera.
    /// Cargada desde System Config del Excel.
    /// </summary>
    public class RideCameraConfig
    {
        /// <summary>Indica si hay modelos rideables configurados</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Variable PLC que contiene la posición del tren</summary>
        public string TrainPositionVariable { get; set; } = string.Empty;

        /// <summary>Lista de modelos con sus offsets de cámara</summary>
        public List<RideableModelConfig> RideableModels { get; set; } = new();
    }

    /// <summary>
    /// Configuración de un modelo rideable con sus offsets de cámara.
    /// </summary>
    public class RideableModelConfig
    {
        /// <summary>ID del modelo (debe coincidir con ModelId en 3D_Models)</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>Offset de la cámara frontal desde el centro del modelo</summary>
        public Vector3Dto FrontOffset { get; set; } = new();

        /// <summary>Offset de la cámara trasera desde el centro del modelo</summary>
        public Vector3Dto RearOffset { get; set; } = new();

        /// <summary>
        /// Eje de movimiento del tren (X, -X, Z, -Z).
        /// La cámara frontal mira en esta dirección.
        /// La cámara trasera mira en la dirección opuesta automáticamente.
        /// </summary>
        public string MovementAxis { get; set; } = "X";
    }

    /// <summary>
    /// Vector 3D para transferencia de datos (DTO).
    /// </summary>
    public class Vector3Dto
    {
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Z { get; set; } = 0;
    }

    #endregion
}