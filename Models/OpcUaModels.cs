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
        /// <summary>
        /// Certificate trust mode (from Excel). Controls how the server validates client certificates.
        /// Values: "none" | "auto-accept" | "manual-trust" | "ca"
        ///   none         → No certificate validation (SecurityPolicy/SecurityMode can be anything)
        ///   auto-accept  → All client certificates accepted automatically (development/testing)
        ///   manual-trust → Only trusted certificates accepted, manual .DER exchange required
        ///   ca           → Only CA-signed certificates accepted, CRL checking possible
        /// 
        /// IMPORTANT: SecurityPolicy and SecurityMode are INDEPENDENT settings.
        /// Each installation configures them based on their requirements.
        /// If CertificateMode requires certificates but SecurityPolicy=None, a WARNING is logged.
        /// Default: "auto-accept" for backward compatibility with existing installations.
        /// </summary>
        public string CertificateMode { get; set; } = "auto-accept";
        public string SecurityPolicy { get; set; } = "Basic256Sha256";
        public string SecurityMode { get; set; } = "SignAndEncrypt";
        public string CertificatePath { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public string TrustedCertsFolder { get; set; } = "";
        public string RejectedCertsFolder { get; set; } = "";
        public bool CrlCheckEnabled { get; set; } = false;
        public string CrlUrl { get; set; } = "";
        public int CrlCheckInterval { get; set; } = 3600;
        public string CaCertPath { get; set; } = "";

        // ===== AUTHENTICATION =====
        public bool AllowAnonymous { get; set; } = false;
        public string UserName { get; set; } = "";
        public string UserPassword { get; set; } = "";

        // ===== SFTP Certificate Exchange (Phase 2) =====
        public bool SftpEnabled { get; set; } = false;
        public string SftpHost { get; set; } = "";
        public int SftpPort { get; set; } = 22;
        public string SftpUser { get; set; } = "";
        public string SftpKeyPath { get; set; } = "";
        public string SftpRemotePath { get; set; } = "/certs/";
        public int SftpSyncInterval { get; set; } = 86400;

        // ===== 🔒 EU CRA v1.4 - DoS / FLOODING PROTECTION (Server Quotas) =====
        // Aplicadas por el OPC Foundation SDK. Configurables desde Excel (System Config).
        public int MaxSessionCount { get; set; } = 50;
        public int MaxSubscriptionCount { get; set; } = 200;
        public int MinSessionTimeoutMs { get; set; } = 10000;
        public int MaxSessionTimeoutMs { get; set; } = 3600000;
        public int MinPublishingIntervalMs { get; set; } = 100;
        public int MaxPublishingIntervalMs { get; set; } = 3600000;

        // ===== EU CRA v1.4 - SECURITY AUDIT HOOKS (Paso B) =====
        public bool AuditSessions { get; set; } = true;
        public int QuotaPollIntervalSeconds { get; set; } = 30;

        // ===== RUNTIME WARNINGS =====
        /// <summary>
        /// Configuration warnings detected at startup. Exposed via /api/opcua/config and /api/opcua/status.
        /// Empty list = configuration is consistent.
        /// </summary>
        public List<string> ConfigWarnings { get; set; } = new();
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
        /// <summary>Alarm name from Excel (e.g., "TLS_M3_MAL_Alarm_042")</summary>
        public string AlarmName { get; set; } = string.Empty;

        /// <summary>Alarm index (1-based)</summary>
        public int AlarmIndex { get; set; }

        /// <summary>OPC/UA Node ID for alarm condition</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>Alarm description from OPC_UA_Alarms sheet</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity (0=Alarm, 1=Notification, 2=Info)</summary>
        public int Severity { get; set; }
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

    /// <summary>
    /// 🔐 OPC/UA Certificate information (for trust management API)
    /// </summary>
    public class OpcUaCertificateInfo
    {
        public string Subject { get; set; } = "";
        public string Issuer { get; set; } = "";
        public string Thumbprint { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public int DaysUntilExpiry { get; set; }
        public int KeySize { get; set; }
        public string SignatureAlgorithm { get; set; } = "";
        public bool IsSelfSigned { get; set; }
        public bool IsValid { get; set; }
        public bool IsRevoked { get; set; }
        public string Store { get; set; } = "";
        public string? FileName { get; set; }
    }

    /// <summary>
    /// CRL (Certificate Revocation List) file information
    /// </summary>
    public class OpcUaCrlInfo
    {
        public string FileName { get; set; } = "";
        public string Issuer { get; set; } = "";
        public int RevokedCount { get; set; }
        public List<string> RevokedSerials { get; set; } = new();
        public DateTime? LastUpdate { get; set; }
        public DateTime? NextUpdate { get; set; }
    }
}
