using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;

namespace SW.PC.API.Backend.Models.EtherCAT
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 📦 ESTRUCTURAS TWINCAT EXACTAS (basadas en XML exportado del PLC)
    // Usadas para parsear datos binarios de FB_EtherCATDiag
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// E_EcCommState - Estados de comunicación EtherCAT
    /// </summary>
    public enum E_EcCommState : ushort
    {
        UNDEFINED = 0,
        INIT = 1,
        PREOP = 2,
        BOOT = 3,
        SAFEOP = 4,
        OP = 8
    }

    /// <summary>
    /// ST_PortAddr - Direcciones de los 4 puertos de un esclavo EtherCAT
    /// Tamaño: 8 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_PortAddr
    {
        public ushort portA;  // 2 bytes - Puerto A (upstream/entrada)
        public ushort portB;  // 2 bytes - Puerto B (downstream/siguiente)
        public ushort portC;  // 2 bytes - Puerto C (ramificación)
        public ushort portD;  // 2 bytes - Puerto D (ramificación)
        
        public const int Size = 8;
    }

    /// <summary>
    /// ST_TopologyData - Datos de topología de un esclavo
    /// Tamaño: 64 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_TopologyData
    {
        public ushort iOwnPhysicalAddr;   // 2 bytes - Dirección física propia
        public ushort iOwnAutoIncAddr;    // 2 bytes - Dirección auto-incremento
        public ST_PortAddr stPhysicalAddr; // 8 bytes - Direcciones físicas de puertos conectados
        public ST_PortAddr stAutoIncAddr;  // 8 bytes - Direcciones auto-inc de puertos conectados
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] iPortDelay;         // 12 bytes - Delays: EC_AD, EC_DB, EC_BC
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] iReserved;          // 32 bytes - Reservado
        
        public const int Size = 64;
    }

    /// <summary>
    /// ST_SlaveState - Estado de un esclavo EtherCAT
    /// Tamaño: 16 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_SlaveState
    {
        public E_EcCommState eEcState;    // 2 bytes - Estado EtherCAT (INIT, PREOP, SAFEOP, OP)
        public ushort nReserved;          // 2 bytes
        public byte bError;               // 1 byte (BOOL)
        public byte bInvalidVPRS;         // 1 byte (BOOL)
        public ushort nReserved2;         // 2 bytes
        
        // Link state flags
        public byte bNoCommToSlave;       // 1 byte
        public byte bLinkError;           // 1 byte
        public byte bMissingLink;         // 1 byte
        public byte bUnexpectedLink;      // 1 byte
        public byte bPortA;               // 1 byte - Link activo en Puerto A
        public byte bPortB;               // 1 byte - Link activo en Puerto B
        public byte bPortC;               // 1 byte - Link activo en Puerto C
        public byte bPortD;               // 1 byte - Link activo en Puerto D
        
        public const int Size = 16;
    }

    /// <summary>
    /// ST_EcMasterDevState - Estado del dispositivo Master EtherCAT
    /// Tamaño: 16 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_EcMasterDevState
    {
        public E_EcCommState eEcState;    // 2 bytes
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] nReserved;        // 6 bytes
        
        public byte bLinkError;           // 1 byte
        public byte bResetRequired;       // 1 byte
        public byte bMissFrmRedMode;      // 1 byte
        public byte bWatchdogTriggerd;    // 1 byte
        public byte bDriverNotFound;      // 1 byte
        public byte bResetActive;         // 1 byte
        public byte bAtLeastOneNotInOp;   // 1 byte
        public byte bDcNotInSync;         // 1 byte
        
        public const int Size = 16;
    }

    /// <summary>
    /// ST_EcCrcErrorEx - Errores CRC por puerto (de Tc2_EtherCAT library)
    /// Tamaño: 16 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_EcCrcErrorEx
    {
        public uint portA;    // 4 bytes - Errores CRC en Puerto A
        public uint portB;    // 4 bytes - Errores CRC en Puerto B
        public uint portC;    // 4 bytes - Errores CRC en Puerto C
        public uint portD;    // 4 bytes - Errores CRC en Puerto D
        
        public const int Size = 16;
    }

    /// <summary>
    /// ST_SlaveStateInfo - Información completa de un esclavo configurado
    /// Tamaño: 208 bytes exactos
    /// </summary>
    public class ST_SlaveStateInfo_Parsed
    {
        public int nIndex;                    // DINT (4 bytes)
        public string sName = "";             // STRING(80) + 1 = 81 bytes
        public string sType = "";             // STRING(80) + 1 = 81 bytes
        public ushort nECAddr;                // UINT (2 bytes)
        public bool bDiagData;                // BOOL (1 byte + 1 padding)
        public ST_EcCrcErrorEx stPortCRCErrors; // 16 bytes
        public uint nSumCRCErrors;            // UDINT (4 bytes)
        public ST_SlaveState stState;         // 16 bytes
        
        // Offsets exactos para parsing manual
        public const int Offset_nIndex = 0;           // 0
        public const int Offset_sName = 4;            // 4
        public const int Offset_sType = 85;           // 4 + 81 = 85
        public const int Offset_nECAddr = 166;        // 85 + 81 = 166
        public const int Offset_bDiagData = 168;      // 166 + 2 = 168
        public const int Offset_stPortCRCErrors = 170; // 168 + 2 (con padding)
        public const int Offset_nSumCRCErrors = 186;  // 170 + 16 = 186
        public const int Offset_stState = 190;        // 186 + 4 = 190
        
        public const int Size = 208;                  // 190 + 16 + padding = ~208
    }

    /// <summary>
    /// ST_SlaveStateInfoScanned - Información de esclavo escaneado vs configurado
    /// Tamaño: ~172 bytes
    /// </summary>
    public class ST_SlaveStateInfoScanned_Parsed
    {
        public int nIndex;                    // DINT (4 bytes)
        public string sName = "";             // STRING(80) + 1 = 81 bytes
        public string sType = "";             // STRING(80) + 1 = 81 bytes
        public ushort nECAddr;                // UINT (2 bytes)
        public bool bDifferentName;           // BOOL (1 byte)
        public bool bDifferentType;           // BOOL (1 byte)
        public bool bDifferentAddr;           // BOOL (1 byte)
        
        public const int Offset_nIndex = 0;
        public const int Offset_sName = 4;
        public const int Offset_sType = 85;
        public const int Offset_nECAddr = 166;
        public const int Offset_bDifferentName = 168;
        public const int Offset_bDifferentType = 169;
        public const int Offset_bDifferentAddr = 170;
        
        public const int EstimatedSize = 172;
    }

    /// <summary>
    /// ST_EcSlaveState - Estado crudo de esclavo (de FB_EcGetAllSlaveStates)
    /// Tamaño: 4 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_EcSlaveState
    {
        public ushort deviceState;  // 2 bytes - bits: [3:0]=EcState, [4]=Error, [5]=InvalidVPRS
        public ushort linkState;    // 2 bytes - bits: [0]=NoComm, [1]=LinkErr, [2]=Missing, [3]=Unexpected, [4-7]=PortA-D
        
        public const int Size = 4;
        
        // Helpers para extraer información
        public E_EcCommState EcState => (E_EcCommState)(deviceState & 0x0F);
        public bool HasError => (deviceState & 0x10) != 0;
        public bool InvalidVPRS => (deviceState & 0x20) != 0;
        public bool NoCommToSlave => (linkState & 0x01) != 0;
        public bool LinkError => (linkState & 0x02) != 0;
        public bool MissingLink => (linkState & 0x04) != 0;
        public bool UnexpectedLink => (linkState & 0x08) != 0;
        public bool PortALinked => (linkState & 0x10) != 0;
        public bool PortBLinked => (linkState & 0x20) != 0;
        public bool PortCLinked => (linkState & 0x40) != 0;
        public bool PortDLinked => (linkState & 0x80) != 0;
    }

    /// <summary>
    /// Constantes para tamaños de arrays en FB_EtherCATDiag
    /// </summary>
    public static class EtherCATConstants
    {
        /// <summary>iSLAVEADDR_ARR_SIZE - Tamaño máximo de arrays de esclavos</summary>
        public const int SLAVEADDR_ARR_SIZE = 256;
        
        /// <summary>Puerto ADS del PLC Runtime</summary>
        public const int ADS_PORT_PLC = 851;
        
        /// <summary>Puerto ADS del EtherCAT Master (IO)</summary>
        public const int ADS_PORT_ECAT_MASTER = 0xFFFF; // 65535
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 💾 MODELO DE BASE DE DATOS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 💾 Configuración de topología EtherCAT guardada en DB
    /// Permite comparar configuración esperada vs estado actual del sistema
    /// </summary>
    [Table("EtherCATSavedConfigurations")]
    public class EtherCATSavedConfiguration
    {
        [Key]
        public int Id { get; set; }

        /// <summary>ID del proyecto (para multi-proyecto)</summary>
        [Required]
        [MaxLength(100)]
        public string ProjectId { get; set; } = "";

        /// <summary>Fecha/hora cuando se guardó la configuración</summary>
        public DateTime SavedAt { get; set; } = DateTime.Now;

        /// <summary>Topología completa serializada como JSON</summary>
        [Required]
        public string TopologyJson { get; set; } = "";

        /// <summary>Número total de esclavos en la configuración</summary>
        public int TotalSlaves { get; set; }

        /// <summary>Notas opcionales del operador</summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>Hash de la configuración para detectar cambios rápidamente</summary>
        [MaxLength(64)]
        public string? ConfigurationHash { get; set; }
    }

    /// <summary>
    /// Resultado de comparación entre configuración guardada y estado actual
    /// </summary>
    public class EtherCATConfigurationComparison
    {
        /// <summary>¿Existe configuración guardada?</summary>
        public bool HasSavedConfiguration { get; set; }

        /// <summary>Fecha de la configuración guardada</summary>
        public DateTime? SavedAt { get; set; }

        /// <summary>Notas de la configuración guardada</summary>
        public string? SavedNotes { get; set; }

        /// <summary>Total de esclavos en config guardada</summary>
        public int SavedSlaveCount { get; set; }

        /// <summary>Total de esclavos en sistema actual</summary>
        public int CurrentSlaveCount { get; set; }

        /// <summary>¿Coincide la configuración?</summary>
        public bool ConfigurationMatches { get; set; }

        /// <summary>Esclavos que faltan (estaban en config guardada pero no están ahora)</summary>
        public List<MissingSlaveInfo> MissingSlaves { get; set; } = new();

        /// <summary>Esclavos nuevos (no estaban en config guardada)</summary>
        public List<NewSlaveInfo> NewSlaves { get; set; } = new();

        /// <summary>Esclavos con diferencias (posición, estado, etc.)</summary>
        public List<SlaveConfigDifference> Differences { get; set; } = new();
    }

    public class MissingSlaveInfo
    {
        public ushort Position { get; set; }
        public ushort ConfiguredAddress { get; set; }
        public string Name { get; set; } = "";
        public uint VendorId { get; set; }
        public uint ProductCode { get; set; }
    }

    public class NewSlaveInfo
    {
        public ushort Position { get; set; }
        public ushort ConfiguredAddress { get; set; }
        public string Name { get; set; } = "";
        public uint VendorId { get; set; }
        public uint ProductCode { get; set; }
    }

    public class SlaveConfigDifference
    {
        public ushort Position { get; set; }
        public string SlaveName { get; set; } = "";
        public string Field { get; set; } = "";
        public string SavedValue { get; set; } = "";
        public string CurrentValue { get; set; } = "";
    }

    /// <summary>
    /// 🔍 Diagnóstico detallado de conexión al Master EtherCAT
    /// </summary>
    public class EtherCATConnectionDiagnostics
    {
        /// <summary>Timestamp del diagnóstico</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>¿Está habilitado el diagnóstico EtherCAT en Excel?</summary>
        public bool IsEnabled { get; set; }

        /// <summary>¿Está en modo simulado?</summary>
        public bool IsSimulatedMode { get; set; }

        /// <summary>NetId configurado en Excel</summary>
        public string ConfiguredNetId { get; set; } = "";

        // --- Pasos de diagnóstico ---

        /// <summary>¿TwinCAT ADS está instalado?</summary>
        public bool TwinCATAdsInstalled { get; set; }

        /// <summary>¿Se pudo crear el cliente ADS?</summary>
        public bool AdsClientCreated { get; set; }

        /// <summary>¿Se pudo parsear el NetId?</summary>
        public bool NetIdValid { get; set; }
        public string? NetIdParseError { get; set; }

        /// <summary>¿Se pudo conectar al target?</summary>
        public bool ConnectionSuccessful { get; set; }
        public string? ConnectionError { get; set; }

        /// <summary>¿Se pudo leer el estado?</summary>
        public bool StateReadSuccessful { get; set; }
        public string? AdsState { get; set; }
        public string? DeviceState { get; set; }
        public string? StateReadError { get; set; }

        /// <summary>Información del dispositivo</summary>
        public string? DeviceName { get; set; }
        public string? DeviceVersion { get; set; }

        /// <summary>Lista de errores/warnings durante el diagnóstico</summary>
        public List<string> DiagnosticMessages { get; set; } = new();

        /// <summary>Resumen del diagnóstico</summary>
        public string Summary { get; set; } = "";

        /// <summary>¿Conexión exitosa general?</summary>
        public bool OverallSuccess { get; set; }
    }

    // ===== CONFIGURACIÓN DESDE EXCEL =====

    /// <summary>
    /// 🌐 Configuración del diagnóstico EtherCAT desde Excel
    /// </summary>
    public class EtherCATConfiguration
    {
        /// <summary>Habilitar diagnóstico de topología EtherCAT</summary>
        public bool EnableEtherCATTopology { get; set; } = false;

        /// <summary>AMS Net ID del Master EtherCAT (ej: 192.168.1.151.3.1)</summary>
        public string EtherCATMasterNetId { get; set; } = "";

        /// <summary>
        /// Dirección IP del PC con TwinCAT (ej: 192.168.1.160).
        /// Necesaria para conexión ADS remota cuando no hay ruta preconfigurada.
        /// Si vacío, se extrae de los primeros 4 octetos del NetId.
        /// </summary>
        public string EtherNETIdTwincat { get; set; } = "";

        /// <summary>
        /// Ruta a los archivos ESI (EtherCAT Slave Information).
        /// Si vacío, usa la ruta estándar de TwinCAT: C:\TwinCAT\3.1\Config\Io\EtherCAT
        /// </summary>
        public string ESIFilesPath { get; set; } = "";

        /// <summary>
        /// Intervalo mínimo entre lecturas completas de topología (ms).
        /// Para evitar sobrecarga del Master EtherCAT.
        /// </summary>
        public int TopologyReadIntervalMs { get; set; } = 2000;

        /// <summary>
        /// Habilitar lectura de ESI files para nombres de dispositivos
        /// </summary>
        public bool UseESIFiles { get; set; } = false;

        /// <summary>
        /// Nombre de la instancia del FB_EtherCATDiag en el PLC.
        /// Ejemplo: MAIN.fbEtherCATDiag, GVL.fbEtherCATDiag, PRG_Diagnostic.fbEtherCATDiag
        /// </summary>
        public string EtherCATDiagFbInstance { get; set; } = "MAIN.fbEtherCATDiag";
    }

    /// <summary>
    /// Estado completo de la topología EtherCAT
    /// </summary>
    public class EtherCATTopology
    {
        /// <summary>Información del Master</summary>
        public EtherCATMaster Master { get; set; } = new();

        /// <summary>Lista de esclavos en orden de bus</summary>
        public List<EtherCATSlaveNode> Slaves { get; set; } = new();

        /// <summary>Grafo de conexiones para visualización</summary>
        public TopologyGraph Graph { get; set; } = new();

        /// <summary>Timestamp de la última lectura</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Tipo de topología detectada</summary>
        public TopologyType DetectedTopology { get; set; } = TopologyType.Unknown;

        /// <summary>Resumen rápido para el panel compacto</summary>
        public EtherCATSummary Summary { get; set; } = new();

        /// <summary>Indica si hay error de comunicación con el Master</summary>
        public bool HasCommunicationError { get; set; } = false;

        /// <summary>Mensaje de error si aplica</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Indica si los datos son SIMULADOS (controlado por UseSimulatedPlc del Excel).
        /// Si UseSimulatedPlc=true y no hay conexión real, se simulan los datos.
        /// </summary>
        public bool IsSimulated { get; set; } = false;
    }

    /// <summary>
    /// Resumen rápido para mostrar en el InfoPanel (sin abrir modal)
    /// </summary>
    public class EtherCATSummary
    {
        /// <summary>Estado general: Healthy, Warning, Error, Offline</summary>
        public NetworkHealth OverallHealth { get; set; } = NetworkHealth.Unknown;

        /// <summary>Número total de esclavos configurados</summary>
        public int ConfiguredSlaveCount { get; set; }

        /// <summary>Número de esclavos en OP (operacional)</summary>
        public int OperationalSlaveCount { get; set; }

        /// <summary>Número de esclavos con errores</summary>
        public int SlavesWithErrors { get; set; }

        /// <summary>Total de errores CRC acumulados</summary>
        public long TotalCRCErrors { get; set; }

        /// <summary>Total de Lost Links acumulados</summary>
        public long TotalLostLinks { get; set; }

        /// <summary>Estado del Master (texto corto)</summary>
        public string MasterStateText { get; set; } = "Unknown";

        /// <summary>Texto resumen para UI</summary>
        public string StatusText => OverallHealth switch
        {
            NetworkHealth.Healthy => $"✓ {OperationalSlaveCount}/{ConfiguredSlaveCount} OP",
            NetworkHealth.Warning => $"⚠ {OperationalSlaveCount}/{ConfiguredSlaveCount} OP ({SlavesWithErrors} warnings)",
            NetworkHealth.Error => $"✕ {OperationalSlaveCount}/{ConfiguredSlaveCount} OP ({SlavesWithErrors} errors)",
            NetworkHealth.Offline => "⊘ Offline",
            _ => "? Unknown"
        };
    }

    /// <summary>
    /// Información del Master EtherCAT
    /// </summary>
    public class EtherCATMaster
    {
        public string NetId { get; set; } = "";
        public string Name { get; set; } = "EtherCAT Master";
        public EtherCATState State { get; set; } = EtherCATState.Unknown;
        public int ConfiguredSlaveCount { get; set; }
        public int ActualSlaveCount { get; set; }
        public bool IsConnected { get; set; }
        public string DeviceName { get; set; } = "";
        public string RuntimeVersion { get; set; } = "";
    }

    /// <summary>
    /// Información de un esclavo EtherCAT
    /// </summary>
    public class EtherCATSlaveNode
    {
        // === Direccionamiento ===
        /// <summary>Posición en el bus (auto-increment address)</summary>
        public ushort Position { get; set; }

        /// <summary>Dirección configurada (fixed address)</summary>
        public ushort ConfiguredAddress { get; set; }

        /// <summary>Dirección alias (opcional)</summary>
        public ushort AliasAddress { get; set; }

        // === Identificación ===
        /// <summary>Vendor ID (Beckhoff = 0x00000002)</summary>
        public uint VendorId { get; set; }

        /// <summary>Product Code</summary>
        public uint ProductCode { get; set; }

        /// <summary>Revision Number</summary>
        public uint RevisionNumber { get; set; }

        /// <summary>Serial Number</summary>
        public uint SerialNumber { get; set; }

        /// <summary>Nombre del dispositivo (del ESI o genérico)</summary>
        public string Name { get; set; } = "";

        /// <summary>Nombre del fabricante</summary>
        public string VendorName { get; set; } = "";

        /// <summary>Descripción del producto</summary>
        public string Description { get; set; } = "";

        /// <summary>Tipo de dispositivo (I/O, Drive, Gateway, etc.)</summary>
        public string DeviceType { get; set; } = "";

        // === Estado ===
        /// <summary>Estado AL (Application Layer)</summary>
        public EtherCATState State { get; set; } = EtherCATState.Unknown;

        /// <summary>Código de error AL Status</summary>
        public ushort ALStatusCode { get; set; }

        /// <summary>Descripción del error</summary>
        public string ALStatusDescription { get; set; } = "";

        /// <summary>Salud del nodo</summary>
        public NodeHealth Health { get; set; } = NodeHealth.Unknown;

        // === Topología/Puertos ===
        /// <summary>Información de los 4 puertos (0-3)</summary>
        public List<EtherCATPort> Ports { get; set; } = new(4);

        /// <summary>Bitmap de puertos activos (bit 0=port0, etc.)</summary>
        public byte ActivePortsBitmap { get; set; }

        /// <summary>Tipo físico (E-Bus, MII/100BASE-TX, etc.)</summary>
        public PhysicalType PhysicalType { get; set; }

        /// <summary>Número de puertos con comunicación activa</summary>
        public int ActivePortCount { get; set; }

        // === Relaciones en el grafo ===
        /// <summary>Índice del esclavo padre (-1 = conectado al Master)</summary>
        public int ParentSlaveIndex { get; set; } = -1;

        /// <summary>Puerto del padre donde estamos conectados</summary>
        public byte? ParentPort { get; set; }

        /// <summary>Puerto de entrada en este esclavo</summary>
        public byte? EntryPort { get; set; }

        /// <summary>Índices de esclavos hijos</summary>
        public List<int> ChildSlaveIndices { get; set; } = new();

        // === Distributed Clock ===
        /// <summary>Tiene soporte DC</summary>
        public bool HasDC { get; set; }

        /// <summary>Delay de propagación en ns</summary>
        public int PropagationDelayNs { get; set; }

        // === Contadores de errores ===
        public SlaveErrorCounters ErrorCounters { get; set; } = new();

        // === Capacidades ===
        public bool SupportsCoE { get; set; }
        public bool SupportsFoE { get; set; }
        public bool SupportsEoE { get; set; }
        public bool SupportsSoE { get; set; }

        // === Diagnóstico desde FB_EtherCATDiag ===
        /// <summary>Indica si hay datos de diagnóstico disponibles (bDiagData del FB)</summary>
        public bool DiagnosticsAvailable { get; set; }

        /// <summary>Contador total de errores CRC (nSumCRCErrors del FB)</summary>
        public int ErrorCount { get; set; }

        // === Imágenes ===
        /// <summary>URL o ruta a imagen del dispositivo (desde ESI)</summary>
        public string ImageUrl { get; set; } = "";

        // === Para visualización ===
        /// <summary>Posición X en el layout (calculada)</summary>
        public int LayoutX { get; set; }

        /// <summary>Posición Y en el layout (calculada)</summary>
        public int LayoutY { get; set; }

        /// <summary>Nivel en el árbol (0 = conectado directo al master)</summary>
        public int TreeLevel { get; set; }
    }

    /// <summary>
    /// Información de un puerto de esclavo EtherCAT
    /// </summary>
    public class EtherCATPort
    {
        /// <summary>Número de puerto (0-3)</summary>
        public byte PortNumber { get; set; }

        /// <summary>Tipo de puerto</summary>
        public PortType Type { get; set; } = PortType.NotImplemented;

        /// <summary>Física del puerto</summary>
        public PortPhysics Physics { get; set; } = PortPhysics.Unknown;

        /// <summary>¿Está físicamente conectado?</summary>
        public bool IsOpen { get; set; }

        /// <summary>¿Tiene comunicación establecida?</summary>
        public bool HasCommunication { get; set; }

        /// <summary>¿Hay link activo?</summary>
        public bool LinkUp { get; set; }

        /// <summary>¿Está en loop (cerrado)?</summary>
        public bool IsLoop { get; set; }

        /// <summary>Índice del esclavo conectado a este puerto (-1 si ninguno)</summary>
        public int ConnectedToSlaveIndex { get; set; } = -1;

        /// <summary>Puerto del otro esclavo donde está conectado</summary>
        public byte? ConnectedToPort { get; set; }

        /// <summary>DC Receive Time para este puerto</summary>
        public uint ReceiveTime { get; set; }

        // === Contadores de errores por puerto ===
        /// <summary>Errores RX</summary>
        public byte RxErrorCount { get; set; }

        /// <summary>Contador de Lost Link</summary>
        public byte LostLinkCount { get; set; }

        /// <summary>Errores CRC detectados</summary>
        public uint CRCErrors { get; set; }

        /// <summary>Salud del enlace</summary>
        public LinkHealth Health { get; set; } = LinkHealth.Unknown;
    }

    /// <summary>
    /// Contadores de errores de un esclavo
    /// </summary>
    public class SlaveErrorCounters
    {
        public uint InvalidFrameCount { get; set; }
        public uint RxErrorCount { get; set; }
        public uint ForwardedRxErrorCount { get; set; }
        public uint ProcessingUnitErrorCount { get; set; }
        public uint PDIErrorCount { get; set; }
        public uint LostLinkCount { get; set; }
        public uint CRCErrorCount { get; set; }
        public uint WatchdogErrors { get; set; }
        
        /// <summary>¿Tiene errores significativos?</summary>
        public bool HasErrors => 
            InvalidFrameCount > 10 || 
            RxErrorCount > 10 || 
            CRCErrorCount > 10 || 
            LostLinkCount > 5 ||
            WatchdogErrors > 0;

        /// <summary>Total de errores</summary>
        public long TotalErrors => 
            InvalidFrameCount + RxErrorCount + ForwardedRxErrorCount + 
            ProcessingUnitErrorCount + PDIErrorCount + LostLinkCount + 
            CRCErrorCount + WatchdogErrors;
    }

    /// <summary>
    /// Grafo para visualización de topología
    /// </summary>
    public class TopologyGraph
    {
        public List<TopologyNode> Nodes { get; set; } = new();
        public List<TopologyEdge> Edges { get; set; } = new();
    }

    /// <summary>
    /// Nodo en el grafo de visualización
    /// </summary>
    public class TopologyNode
    {
        /// <summary>ID único (ej: "master" o "slave_0")</summary>
        public string Id { get; set; } = "";

        /// <summary>Etiqueta para mostrar</summary>
        public string Label { get; set; } = "";

        /// <summary>Tipo: "master" o "slave"</summary>
        public string Type { get; set; } = "slave";

        /// <summary>Índice del esclavo (si aplica)</summary>
        public int? SlaveIndex { get; set; }

        /// <summary>Estado EtherCAT</summary>
        public EtherCATState State { get; set; }

        /// <summary>Nombre del fabricante</summary>
        public string VendorName { get; set; } = "";

        /// <summary>Nombre del producto</summary>
        public string ProductName { get; set; } = "";

        /// <summary>Posición X en layout</summary>
        public int X { get; set; }

        /// <summary>Posición Y en layout</summary>
        public int Y { get; set; }

        /// <summary>Salud del nodo</summary>
        public NodeHealth Health { get; set; }

        /// <summary>Ancho del nodo (para rendering)</summary>
        public int Width { get; set; } = 180;

        /// <summary>Alto del nodo (para rendering)</summary>
        public int Height { get; set; } = 80;
    }

    /// <summary>
    /// Arista/conexión en el grafo
    /// </summary>
    public class TopologyEdge
    {
        public string Id { get; set; } = "";
        public string SourceNodeId { get; set; } = "";
        public byte SourcePort { get; set; }
        public string TargetNodeId { get; set; } = "";
        public byte TargetPort { get; set; }
        public bool HasErrors { get; set; }
        public LinkHealth Health { get; set; } = LinkHealth.Unknown;
        public uint ErrorCount { get; set; }
    }

    // ===== ENUMS =====

    /// <summary>
    /// Estados EtherCAT (AL Status)
    /// </summary>
    public enum EtherCATState : byte
    {
        Unknown = 0x00,
        Init = 0x01,
        PreOp = 0x02,
        Bootstrap = 0x03,
        SafeOp = 0x04,
        Operational = 0x08,
        // Estados con error (OR con 0x10)
        InitError = 0x11,
        PreOpError = 0x12,
        SafeOpError = 0x14,
        OperationalError = 0x18
    }

    /// <summary>
    /// Tipo de topología detectada
    /// </summary>
    public enum TopologyType
    {
        Unknown,
        Line,       // Cadena lineal simple
        Tree,       // Árbol con ramificaciones
        Star,       // Estrella desde un junction
        Ring,       // Anillo (redundancia)
        Mixed       // Combinación
    }

    /// <summary>
    /// Salud general de la red
    /// </summary>
    public enum NetworkHealth
    {
        Unknown,
        Healthy,    // Todo OK, todos en OP
        Warning,    // Algunos warnings o SafeOp
        Error,      // Errores o esclavos caídos
        Offline     // Sin comunicación
    }

    /// <summary>
    /// Salud de un nodo individual
    /// </summary>
    public enum NodeHealth
    {
        Unknown,
        Healthy,    // Operational sin errores
        Warning,    // SafeOp o errores menores
        Error,      // No operational o errores graves
        Offline     // No responde
    }

    /// <summary>
    /// Salud de un enlace
    /// </summary>
    public enum LinkHealth
    {
        Unknown,
        Good,       // Sin errores
        Degraded,   // Algunos errores CRC/lost link
        Critical    // Muchos errores
    }

    /// <summary>
    /// Tipo de puerto EtherCAT
    /// </summary>
    public enum PortType : byte
    {
        NotImplemented = 0,
        NotConfigured = 1,
        EBUS = 2,
        MII = 3,    // 100BASE-TX (Ethernet externo)
        Reserved = 4
    }

    /// <summary>
    /// Física del puerto
    /// </summary>
    public enum PortPhysics : byte
    {
        Unknown = 0,
        EBus = 1,           // E-Bus interno (terminales en rail)
        Ethernet = 2,        // 100BASE-TX externo
        LVDS = 3,           // Low-voltage differential
        FastHotConnect = 4   // Fast Hot Connect
    }

    /// <summary>
    /// Tipo físico del esclavo
    /// </summary>
    public enum PhysicalType : byte
    {
        Unknown = 0,
        EBusOnly = 1,       // Solo E-Bus (terminal típico)
        EthernetOnly = 2,   // Solo Ethernet (dispositivo standalone)
        Mixed = 3           // Mixto (coupler, junction)
    }

    // ===== HELPERS =====

    /// <summary>
    /// Extensiones para estados EtherCAT
    /// </summary>
    public static class EtherCATStateExtensions
    {
        public static string ToShortString(this EtherCATState state) => state switch
        {
            EtherCATState.Unknown => "??",
            EtherCATState.Init => "INIT",
            EtherCATState.PreOp => "PREOP",
            EtherCATState.Bootstrap => "BOOT",
            EtherCATState.SafeOp => "SAFEOP",
            EtherCATState.Operational => "OP",
            EtherCATState.InitError => "INIT+E",
            EtherCATState.PreOpError => "PREOP+E",
            EtherCATState.SafeOpError => "SAFEOP+E",
            EtherCATState.OperationalError => "OP+E",
            _ => "??"
        };

        public static string ToColorCode(this EtherCATState state) => state switch
        {
            EtherCATState.Operational => "#4ade80",      // Verde
            EtherCATState.SafeOp => "#fbbf24",           // Amarillo
            EtherCATState.PreOp => "#60a5fa",            // Azul
            EtherCATState.Init => "#9ca3af",             // Gris
            EtherCATState.Bootstrap => "#c084fc",         // Morado
            EtherCATState.InitError or
            EtherCATState.PreOpError or
            EtherCATState.SafeOpError or
            EtherCATState.OperationalError => "#ef4444", // Rojo
            _ => "#6b7280"                                // Gris oscuro
        };

        public static bool HasError(this EtherCATState state) => 
            ((byte)state & 0x10) != 0;

        public static bool IsOperational(this EtherCATState state) => 
            state == EtherCATState.Operational;
    }

    /// <summary>
    /// Diccionario de Vendor IDs conocidos
    /// </summary>
    public static class EtherCATVendors
    {
        public static readonly Dictionary<uint, string> KnownVendors = new()
        {
            { 0x00000002, "Beckhoff Automation" },
            { 0x000000B0, "Omron" },
            { 0x000001DD, "Copley Controls" },
            { 0x0000006A, "Kollmorgen" },
            { 0x00000022, "Hilscher" },
            { 0x00000156, "WAGO" },
            { 0x00000047, "Phoenix Contact" },
            { 0x0000000E, "B&R" },
            { 0x000001A1, "Yaskawa" },
            { 0x0000004C, "Lenze" },
            { 0x00000030, "Rexroth" },
            { 0x00000059, "Siemens" },
            { 0x000000C7, "ABB" },
            { 0x0000015B, "Delta Electronics" },
            { 0x00000114, "Festo" },
            { 0x000000CB, "Baumer" },
            { 0x00000049, "Leuze" },
            { 0x0000002D, "IFM" },
            { 0x00000020, "Pilz" }
        };

        public static string GetVendorName(uint vendorId) =>
            KnownVendors.TryGetValue(vendorId, out var name) 
                ? name 
                : $"Unknown (0x{vendorId:X8})";
    }

    /// <summary>
    /// Códigos de error AL Status conocidos
    /// </summary>
    public static class ALStatusCodes
    {
        public static readonly Dictionary<ushort, string> KnownCodes = new()
        {
            { 0x0000, "No error" },
            { 0x0001, "Unspecified error" },
            { 0x0011, "Invalid requested state change" },
            { 0x0012, "Unknown requested state" },
            { 0x0013, "Bootstrap not supported" },
            { 0x0014, "No valid firmware" },
            { 0x0015, "Invalid mailbox configuration" },
            { 0x0016, "Invalid mailbox configuration - Boot state" },
            { 0x0017, "Invalid sync manager configuration" },
            { 0x0018, "No valid inputs available" },
            { 0x0019, "No valid outputs" },
            { 0x001A, "Synchronization error" },
            { 0x001B, "Sync manager watchdog" },
            { 0x001C, "Invalid sync manager types" },
            { 0x001D, "Invalid output configuration" },
            { 0x001E, "Invalid input configuration" },
            { 0x001F, "Invalid watchdog configuration" },
            { 0x0020, "Slave needs cold start" },
            { 0x0021, "Slave needs INIT" },
            { 0x0022, "Slave needs PREOP" },
            { 0x0023, "Slave needs SAFEOP" },
            { 0x0024, "Invalid input mapping" },
            { 0x0025, "Invalid output mapping" },
            { 0x0026, "Inconsistent settings" },
            { 0x0027, "FreeRun not supported" },
            { 0x0028, "SyncMode not supported" },
            { 0x0029, "FreeRun needs 3 buffer mode" },
            { 0x002A, "Background watchdog" },
            { 0x002B, "No valid inputs and outputs" },
            { 0x002C, "Fatal sync error" },
            { 0x002D, "No sync error" },
            { 0x0030, "Invalid DC SYNC configuration" },
            { 0x0031, "Invalid DC latch configuration" },
            { 0x0032, "PLL error" },
            { 0x0033, "DC sync IO error" },
            { 0x0034, "DC sync timeout error" },
            { 0x0035, "DC invalid sync cycle time" },
            { 0x0036, "DC sync0 cycle time" },
            { 0x0037, "DC sync1 cycle time" },
            { 0x0041, "MBX_AOE" },
            { 0x0042, "MBX_EOE" },
            { 0x0043, "MBX_COE" },
            { 0x0044, "MBX_FOE" },
            { 0x0045, "MBX_SOE" },
            { 0x004F, "MBX_VOE" },
            { 0x0050, "EEPROM no access" },
            { 0x0051, "EEPROM error" },
            { 0x0060, "Slave restarted locally" },
            { 0x0061, "Device identification value updated" },
            { 0x00F0, "Application controller available" }
        };

        public static string GetDescription(ushort code) =>
            KnownCodes.TryGetValue(code, out var desc) 
                ? desc 
                : $"Unknown error (0x{code:X4})";
    }
}
