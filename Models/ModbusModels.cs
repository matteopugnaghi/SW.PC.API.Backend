namespace SW.PC.API.Backend.Models.Modbus
{
    /// <summary>
    /// 📡 Modbus register type (classic 0xxxx/1xxxx/3xxxx/4xxxx data model).
    /// </summary>
    public enum ModbusRegisterType
    {
        /// <summary>0xxxx — read/write bit (FC01 read, FC05/FC15 write)</summary>
        Coil = 0,
        /// <summary>1xxxx — read-only bit (FC02)</summary>
        DiscreteInput = 1,
        /// <summary>3xxxx — read-only 16-bit register (FC04)</summary>
        InputRegister = 3,
        /// <summary>4xxxx — read/write 16-bit register (FC03 read, FC06/FC16 write)</summary>
        HoldingRegister = 4
    }

    /// <summary>
    /// 📡 Modbus TCP configuration (from Excel "System Config" sheet).
    /// Mirrors OpcUaConfig. Empty/disabled = feature does not exist.
    /// </summary>
    public class ModbusConfig
    {
        // ===== GENERAL =====
        public bool Enabled { get; set; } = false;

        // ===== SERVER (Slave) — exposes ADS data to other systems =====
        /// <summary>Listen IP for the Modbus TCP server. "0.0.0.0" = all interfaces.</summary>
        public string ServerBindIp { get; set; } = "0.0.0.0";
        /// <summary>Modbus TCP server port. 502 is privileged on Windows; 1502 is a safe alternative.</summary>
        public int ServerPort { get; set; } = 502;
        /// <summary>Server unit/slave id.</summary>
        public byte ServerUnitId { get; set; } = 1;

        // ===== CLIENT (Master) — reads/writes up to 2 external Modbus devices =====
        /// <summary>External Modbus TCP sources (max 2 by convention).</summary>
        public List<ModbusSource> Sources { get; set; } = new();
        /// <summary>Polling interval (ms) for external sources (Client role).</summary>
        public int PollIntervalMs { get; set; } = 1000;

        // ===== RUNTIME WARNINGS =====
        /// <summary>Configuration warnings detected at startup. Empty = consistent.</summary>
        public List<string> ConfigWarnings { get; set; } = new();
    }

    /// <summary>
    /// 📡 External Modbus TCP source (device the Supervisor reads/writes as a Client/Master).
    /// </summary>
    public class ModbusSource
    {
        /// <summary>Logical id used in Modbus_Variables.Source (e.g. "MB1", "ModbusClient1").</summary>
        public string Id { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 502;
        public byte UnitId { get; set; } = 1;
    }

    /// <summary>
    /// 📡 Modbus variable definition (from Excel "Modbus_Variables" sheet).
    /// Maps an ADS symbol (Server) or external source register (Client) to a Modbus register.
    /// </summary>
    public class ModbusVariable
    {
        /// <summary>Logical name (optional if AdsSymbol is provided).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full ADS symbol path (Server role link to TwinCAT), e.g. "GVL_Modbus.TLS_M3_MAL_RemoteMode".
        /// Must match EXACTLY a VariableName declared in PLC_Variables.
        /// </summary>
        public string AdsSymbol { get; set; } = string.Empty;

        /// <summary>Register type (Coil/DiscreteInput/InputRegister/HoldingRegister).</summary>
        public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

        /// <summary>0-based register/coil address.</summary>
        public int Address { get; set; }

        /// <summary>Data type: BOOL, INT16, UINT16, INT32, UINT32, FLOAT32, STRING.</summary>
        public string DataType { get; set; } = "INT16";

        /// <summary>Word order for &gt;16-bit types: ABCD/CDAB/BADC/DCBA.</summary>
        public string WordOrder { get; set; } = "ABCD";

        /// <summary>Engineering scaling: eng = raw * Scale + Offset.</summary>
        public double Scale { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;

        /// <summary>Access mode: R / W / RW.</summary>
        public string AccessMode { get; set; } = "R";

        /// <summary>"ADS" (Server, value from TwinCAT) or external source id (Client, e.g. "MB1").</summary>
        public string Source { get; set; } = "ADS";

        /// <summary>If TRUE, value changes are NOT written to the OperationLog (high-frequency vars).</summary>
        public bool ExcludeFromLog { get; set; } = false;

        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        /// <summary>True if the variable lives in an external Modbus source (Client role).</summary>
        public bool IsExternalSource => !string.Equals(Source, "ADS", StringComparison.OrdinalIgnoreCase)
                                        && !string.IsNullOrWhiteSpace(Source);

        /// <summary>Number of 16-bit registers occupied by this variable.</summary>
        public int RegisterCount => DataType.ToUpperInvariant() switch
        {
            "INT32" or "UINT32" or "FLOAT32" or "DINT" or "UDINT" or "REAL" => 2,
            "INT64" or "UINT64" or "DOUBLE" or "LREAL" => 4,
            _ => 1
        };
    }

    /// <summary>
    /// 📡 Modbus alarm definition (from Excel "Modbus_Alarms" sheet).
    /// Mirrors OpcUaAlarm: publishes a central alarm state to a Modbus register/coil.
    /// </summary>
    public class ModbusAlarm
    {
        /// <summary>Alarm name from Excel.</summary>
        public string AlarmName { get; set; } = string.Empty;

        /// <summary>Alarm index (1-based, same as PLC/Excel st_alarmPc[idx]).</summary>
        public int AlarmIndex { get; set; }

        /// <summary>Register type where the alarm state is published (Coil/DiscreteInput/HoldingRegister).</summary>
        public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.Coil;

        /// <summary>0-based register/coil address.</summary>
        public int Address { get; set; }

        /// <summary>Severity (0=Alarm, 1=Notification, 2=Info) — matches AlarmType.</summary>
        public int Severity { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 📡 Modbus runtime status (for API and MetricsService).
    /// </summary>
    public class ModbusStatus
    {
        public bool Enabled { get; set; }
        public bool ServerRunning { get; set; }
        public string StatusMessage { get; set; } = "Not started";
        public string BindIp { get; set; } = "";
        public int Port { get; set; }
        public int UnitId { get; set; }
        public int ConnectedClients { get; set; }
        public int PublishedVariables { get; set; }
        public int PublishedAlarms { get; set; }
        public DateTime? StartedAt { get; set; }
        public string Uptime { get; set; } = "";
        public List<ModbusSourceStatus> Sources { get; set; } = new();
    }

    /// <summary>
    /// 📡 Status of an external Modbus source (Client role).
    /// </summary>
    public class ModbusSourceStatus
    {
        public string Id { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public int UnitId { get; set; }
        public bool Connected { get; set; }
        public string LastError { get; set; } = "";
        public DateTime? LastReadAt { get; set; }
    }
}
