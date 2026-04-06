namespace SW.PC.API.Backend.Models.OpcUa
{
    /// <summary>
    /// 🌐 OPC/UA Server Configuration (from Excel System Config sheet)
    /// </summary>
    public class OpcUaConfig
    {
        // ===== GENERAL =====
        public bool Enabled { get; set; } = false;
        public int Port { get; set; } = 4840;
        public string ServerUri { get; set; } = "opc.tcp://localhost:4840";
        public string ServerName { get; set; } = "Aquafrisch SCADA OPC/UA Server";

        // ===== SECURITY =====
        public string SecurityPolicy { get; set; } = "Basic256Sha256";
        public string SecurityMode { get; set; } = "SignAndEncrypt";
        public string CertificatePath { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public string TrustedCertsFolder { get; set; } = "";
        public string RejectedCertsFolder { get; set; } = "";
        public bool CrlCheckEnabled { get; set; } = false;
        public string CrlUrl { get; set; } = "";

        // ===== AUTHENTICATION =====
        public bool AllowAnonymous { get; set; } = false;
        public string UserName { get; set; } = "";
        public string UserPassword { get; set; } = "";

        // ===== TIMING =====
        public int WatchdogIntervalMs { get; set; } = 5000;
        public int CommandFeedbackDurationMs { get; set; } = 2000;
        public int DefaultSubscriptionIntervalMs { get; set; } = 1000;
    }

    /// <summary>
    /// 🌐 OPC/UA Variable definition (from Excel OPC_UA_Variables sheet)
    /// </summary>
    public class OpcUaVariable
    {
        /// <summary>Variable name in OPC/UA namespace</summary>
        public string VariableName { get; set; } = string.Empty;

        /// <summary>OPC/UA Node ID (e.g., "ns=2;s=Temperature")</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Data type: Boolean, Int16, Int32, Float, Double, String</summary>
        public string DataType { get; set; } = "Float";

        /// <summary>Access mode: ReadOnly, WriteOnly, ReadWrite</summary>
        public string AccessMode { get; set; } = "ReadOnly";

        /// <summary>TwinCAT PLC symbol path (e.g., "MAIN.fbMachine.rTemperature")</summary>
        public string PlcSymbolPath { get; set; } = string.Empty;

        /// <summary>Human-readable description</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Update rate in milliseconds for subscriptions</summary>
        public int UpdateRateMs { get; set; } = 1000;

        /// <summary>Engineering unit (e.g., "°C", "bar", "m/s")</summary>
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🌐 OPC/UA Alarm definition (from Excel OPC_UA_Alarms sheet)
    /// </summary>
    public class OpcUaAlarm
    {
        /// <summary>Alarm index (1-based)</summary>
        public int AlarmIndex { get; set; }

        /// <summary>OPC/UA Node ID for alarm condition</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Alarm description</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity 1-1000 (OPC/UA standard)</summary>
        public int Severity { get; set; } = 500;
    }

    /// <summary>
    /// 🌐 OPC/UA Server runtime status (for API and MetricsService)
    /// </summary>
    public class OpcUaServerStatus
    {
        public bool Enabled { get; set; }
        public bool Running { get; set; }
        public string StatusMessage { get; set; } = "Not started";
        public string ServerUri { get; set; } = "";
        public int Port { get; set; }
        public string SecurityPolicy { get; set; } = "";
        public string SecurityMode { get; set; } = "";
        public int ConnectedClients { get; set; }
        public int PublishedVariables { get; set; }
        public int PublishedAlarms { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastClientConnection { get; set; }
        public string Uptime { get; set; } = "";
        public List<OpcUaClientInfo> Clients { get; set; } = new();
    }

    /// <summary>
    /// 🌐 Connected OPC/UA client information
    /// </summary>
    public class OpcUaClientInfo
    {
        public string SessionId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string ClientUri { get; set; } = "";
        public string RemoteAddress { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
        public int ActiveSubscriptions { get; set; }
    }
}
