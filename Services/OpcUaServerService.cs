using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using System.Collections.Concurrent;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Models.OpcUa;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🌐 OPC/UA Server Interface
    /// </summary>
    public interface IOpcUaServerService
    {
        /// <summary>Get current server status</summary>
        OpcUaServerStatus GetStatus();
        
        /// <summary>Whether the server is enabled in configuration</summary>
        bool IsEnabled { get; }
        
        /// <summary>Whether the server is currently running</summary>
        bool IsRunning { get; }
        
        /// <summary>Get loaded OPC/UA variables configuration</summary>
        List<OpcUaVariable> GetVariables();
        
        /// <summary>Get loaded OPC/UA alarms configuration</summary>
        List<OpcUaAlarm> GetAlarms();
        
        /// <summary>Get OPC/UA configuration from Excel</summary>
        OpcUaConfig GetConfig();

        /// <summary>Get current values of all OPC/UA variable and alarm nodes</summary>
        Dictionary<string, object?> GetCurrentValues();
    }

    /// <summary>
    /// 🌐 Disabled stub — registered when OPC/UA is disabled in Excel.
    /// Returns empty data, no background service, zero resources.
    /// </summary>
    public class DisabledOpcUaServerService : IOpcUaServerService
    {
        public bool IsEnabled => false;
        public bool IsRunning => false;
        public OpcUaServerStatus GetStatus() => new() { Running = false, StatusMessage = "Disabled in configuration" };
        public List<OpcUaVariable> GetVariables() => new();
        public List<OpcUaAlarm> GetAlarms() => new();
        public OpcUaConfig GetConfig() => new() { Enabled = false };
        public Dictionary<string, object?> GetCurrentValues() => new();
    }

    /// <summary>
    /// 🌐 OPC/UA Server BackgroundService
    /// Implements OPC Foundation standard server with TwinCAT PLC variable bridging.
    /// Configuration entirely from Excel (System Config + OPC_UA_Variables + OPC_UA_Alarms sheets).
    /// </summary>
    public class OpcUaServerService : BackgroundService, IOpcUaServerService
    {
        private readonly ILogger<OpcUaServerService> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IProjectContextService _projectContext;
        private readonly IMetricsService _metricsService;
        private readonly IAuditLogService _auditLogService;
        private readonly ITwinCATService _twinCATService;
        private readonly AlarmNotificationService _alarmNotificationService;
        private readonly IOperationLogService _operationLogService;

        // OPC/UA Foundation server
        private ApplicationInstance? _application;
        private StandardServer? _server;
        private AquafrischNodeManager? _nodeManager;

        // Configuration
        private OpcUaConfig _config = new();
        private List<OpcUaVariable> _variables = new();
        private List<OpcUaAlarm> _alarms = new();
        
        // State
        private bool _isRunning;
        private DateTime? _startedAt;
        private string _statusMessage = "Not initialized";
        private readonly object _lock = new();

        // Change detection for audit logging (zero extra resources — piggybacks on existing poll)
        private readonly Dictionary<string, object?> _previousValues = new();
        private readonly Dictionary<int, bool> _previousAlarmStates = new();

        // Per-variable polling: track last poll time to respect each variable's UpdateRateMs
        private readonly Dictionary<string, DateTime> _lastPollTime = new();



        public bool IsEnabled => _config.Enabled;
        public bool IsRunning => _isRunning;

        public OpcUaServerService(
            ILogger<OpcUaServerService> logger,
            IExcelConfigService excelConfigService,
            IProjectContextService projectContext,
            IMetricsService metricsService,
            IAuditLogService auditLogService,
            ITwinCATService twinCATService,
            AlarmNotificationService alarmNotificationService,
            IOperationLogService operationLogService)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _metricsService = metricsService;
            _auditLogService = auditLogService;
            _twinCATService = twinCATService;
            _alarmNotificationService = alarmNotificationService;
            _operationLogService = operationLogService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🌐 OPC/UA Server Service starting...");

            try
            {
                // Load configuration from Excel
                await LoadConfigurationAsync();

                if (!_config.Enabled)
                {
                    _statusMessage = "Disabled in configuration";
                    UpdateMetrics();
                    _logger.LogInformation("🌐 OPC/UA Server is DISABLED in Excel configuration");
                    
                    // Stay alive but idle — wait for cancellation
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                    return;
                }

                // Start OPC/UA server
                await StartServerAsync(stoppingToken);

                // Audit log
                await _auditLogService.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.OpcUaServerStart,
                    AuditResult.Success,
                    $"OPC/UA Server started on port {_config.Port} ({_config.SecurityPolicy}/{_config.SecurityMode})",
                    userName: "System");

                // Main loop — PLC polling + watchdog monitoring
                // Base tick = fastest variable rate (min 50ms), each variable polled at its own UpdateRateMs
                var rates = _variables
                    .Where(v => v.AccessMode?.ToLowerInvariant() is not ("writeonly" or "wo"))
                    .Select(v => v.UpdateRateMs > 0 ? v.UpdateRateMs : 1000)
                    .ToList();
                var baseTick = rates.Count > 0 ? Math.Max(rates.Min(), 50) : 1000;
                _logger.LogInformation("🌐 Starting PLC→OPC/UA bridge polling (base tick: {Ms}ms, variable rates: {Min}-{Max}ms)",
                    baseTick,
                    rates.Count > 0 ? rates.Min() : 0,
                    rates.Count > 0 ? rates.Max() : 0);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(baseTick, stoppingToken);

                        // Poll PLC variables and update OPC/UA nodes
                        if (_server != null && _isRunning && _nodeManager != null)
                        {
                            await PollPlcVariablesAsync();
                        }

                        // Watchdog / metrics update
                        if (_server != null && _isRunning)
                        {
                            var sessions = _server.CurrentInstance?.SessionManager?.GetSessions();
                            var clientCount = (int)(sessions?.Count ?? 0);
                            lock (_lock)
                            {
                                _statusMessage = $"Running - {clientCount} client(s) connected, {_variables.Count} variables";
                            }
                            UpdateMetrics();
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (System.Net.Sockets.SocketException sockEx)
                    {
                        _logger.LogWarning("🌐 Client socket error (client disconnected?): {Msg}", sockEx.Message);
                    }
                    catch (Exception loopEx)
                    {
                        _logger.LogWarning(loopEx, "🌐 Error in poll loop iteration (continuing)");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🌐 OPC/UA Server Service stopping (cancellation requested)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 OPC/UA Server Service fatal error");
                _statusMessage = $"Error: {ex.Message}";
                _isRunning = false;
                UpdateMetrics();
            }
            finally
            {
                await StopServerAsync();
            }
        }

        private async Task LoadConfigurationAsync()
        {
            var excelPath = _projectContext.ExcelConfigPath;
            _logger.LogInformation("🌐 Loading OPC/UA configuration from: {Path}", excelPath);

            // Load System Config (contains OPC/UA enable/disable, security, etc.)
            var sysConfig = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
            
            _config = new OpcUaConfig
            {
                Enabled = sysConfig.OpcUaEnabled,
                Port = sysConfig.OpcUaPort,
                ServerUri = sysConfig.OpcUaServerUri,
                ServerName = sysConfig.OpcUaServerName,
                CertificateMode = sysConfig.OpcUaCertificateMode,
                SecurityPolicy = sysConfig.OpcUaSecurityPolicy,
                SecurityMode = sysConfig.OpcUaSecurityMode,
                CertificatePath = sysConfig.OpcUaCertificatePath,
                PrivateKeyPath = sysConfig.OpcUaPrivateKeyPath,
                TrustedCertsFolder = sysConfig.OpcUaTrustedCertsFolder,
                RejectedCertsFolder = sysConfig.OpcUaRejectedCertsFolder,
                CrlCheckEnabled = sysConfig.OpcUaCrlCheckEnabled,
                CrlUrl = sysConfig.OpcUaCrlUrl,
                CrlCheckInterval = sysConfig.OpcUaCrlCheckInterval,
                CaCertPath = sysConfig.OpcUaCaCertPath,
                AllowAnonymous = sysConfig.OpcUaAllowAnonymous,
                UserName = sysConfig.OpcUaUserName,
                UserPassword = sysConfig.OpcUaUserPassword,
                SftpEnabled = sysConfig.OpcUaSftpEnabled,
                SftpHost = sysConfig.OpcUaSftpHost,
                SftpPort = sysConfig.OpcUaSftpPort,
                SftpUser = sysConfig.OpcUaSftpUser,
                SftpKeyPath = sysConfig.OpcUaSftpKeyPath,
                SftpRemotePath = sysConfig.OpcUaSftpRemotePath,
                SftpSyncInterval = sysConfig.OpcUaSftpSyncInterval
            };

            // ── Development SFTP override (localhost test server) ──
            var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            if (isDev && _config.SftpEnabled)
            {
                _config.SftpHost = "localhost";
                _config.SftpUser = Environment.UserName;
                _config.SftpRemotePath = "/C:/sftp-test/certs";
                _config.SftpSyncInterval = 0; // Disabled — use manual sync via UI buttons
                _logger.LogWarning("🔧 [DEV] SFTP config overridden → localhost:{Port} user={User} path={Path} sync={Interval}s",
                    _config.SftpPort, _config.SftpUser, _config.SftpRemotePath, _config.SftpSyncInterval);
            }

            // ═══════════════════════════════════════════════════════════════
            // CertificateMode controls CERTIFICATE VALIDATION behavior.
            // SecurityPolicy / SecurityMode are INDEPENDENT — configured per installation.
            // If there's a mismatch, we LOG WARNINGS but respect the Excel values.
            // ═══════════════════════════════════════════════════════════════
            var mode = _config.CertificateMode.ToLowerInvariant();
            _config.ConfigWarnings.Clear();

            // Validate configuration coherence — warn on mismatches
            var policyIsNone = string.IsNullOrEmpty(_config.SecurityPolicy) || 
                               _config.SecurityPolicy.Equals("None", StringComparison.OrdinalIgnoreCase);
            var modeIsNone = string.IsNullOrEmpty(_config.SecurityMode) || 
                             _config.SecurityMode.Equals("None", StringComparison.OrdinalIgnoreCase);

            switch (mode)
            {
                case "none":
                    // No certificate validation — SecurityPolicy/SecurityMode as configured
                    _logger.LogInformation("🌐 CertificateMode=none → No certificate validation");
                    break;

                case "auto-accept":
                    // Accept all certificates — warn if channel is unencrypted
                    if (policyIsNone || modeIsNone)
                    {
                        var warn = "⚠️ CertificateMode=auto-accept but SecurityPolicy/SecurityMode=None. " +
                                   "Certificates will be generated but channel is unencrypted. " +
                                   "Configure OpcUa_SecurityPolicy (e.g. Basic256Sha256) and OpcUa_SecurityMode (e.g. SignAndEncrypt) in Excel.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    break;

                case "manual-trust":
                case "ca":
                    // Strict certificate validation — MUST have encryption
                    if (policyIsNone)
                    {
                        var warn = $"⚠️ CONFIGURATION ERROR: CertificateMode={mode} requires OpcUa_SecurityPolicy to be configured " +
                                   $"(e.g. Basic256Sha256, Aes128_Sha256_RsaOaep, Aes256_Sha256_RsaPss). " +
                                   $"Current value: '{_config.SecurityPolicy}'. Falling back to Basic256Sha256.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogError(warn);
                        _config.SecurityPolicy = "Basic256Sha256";
                    }
                    if (modeIsNone)
                    {
                        var warn = $"⚠️ CONFIGURATION ERROR: CertificateMode={mode} requires OpcUa_SecurityMode to be configured " +
                                   $"(Sign or SignAndEncrypt). " +
                                   $"Current value: '{_config.SecurityMode}'. Falling back to SignAndEncrypt.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogError(warn);
                        _config.SecurityMode = "SignAndEncrypt";
                    }
                    if (_config.AllowAnonymous)
                    {
                        var warn = $"⚠️ CONFIGURATION WARNING: CertificateMode={mode} with AllowAnonymous=true is insecure. " +
                                   $"Set OpcUa_AllowAnonymous=false in Excel for production.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    if (!string.IsNullOrEmpty(_config.UserName) && string.IsNullOrEmpty(_config.UserPassword))
                    {
                        var warn = $"⚠️ CONFIGURATION WARNING: OpcUa_UserName is set but OpcUa_UserPassword is empty.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    if (string.IsNullOrEmpty(_config.UserName) && !_config.AllowAnonymous)
                    {
                        var warn = $"⚠️ CONFIGURATION WARNING: Anonymous disabled but no OpcUa_UserName configured. " +
                                   $"Clients will only be able to authenticate via certificate.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    if (mode == "ca" && !_config.CrlCheckEnabled)
                    {
                        var warn = "⚠️ CONFIGURATION WARNING: CertificateMode=ca but OpcUa_CrlCheckEnabled=false. " +
                                   "Revoked certificates will NOT be detected.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    if (mode == "ca" && _config.CrlCheckEnabled && string.IsNullOrEmpty(_config.CrlUrl))
                    {
                        var warn = "⚠️ CONFIGURATION ERROR: OpcUa_CrlCheckEnabled=true but OpcUa_Crl_Url is empty. " +
                                   "CRL checking will not work without a URL.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogError(warn);
                    }
                    if (mode == "ca" && string.IsNullOrEmpty(_config.CaCertPath))
                    {
                        var warn = "⚠️ CONFIGURATION WARNING: CertificateMode=ca but OpcUa_Ca_CertPath is empty. " +
                                   "CA root certificate required to validate client certificates signed by CA.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogWarning(warn);
                    }
                    if (mode == "ca" && !string.IsNullOrEmpty(_config.CaCertPath) && !File.Exists(_config.CaCertPath))
                    {
                        var warn = $"⚠️ CONFIGURATION ERROR: OpcUa_Ca_CertPath='{_config.CaCertPath}' — file not found.";
                        _config.ConfigWarnings.Add(warn);
                        _logger.LogError(warn);
                    }
                    // SFTP warnings (only relevant for ca mode)
                    if (mode == "ca" && _config.SftpEnabled)
                    {
                        if (string.IsNullOrEmpty(_config.SftpHost))
                        {
                            var warn = "⚠️ CONFIGURATION ERROR: OpcUa_Sftp_Enabled=true but OpcUa_Sftp_Host is empty.";
                            _config.ConfigWarnings.Add(warn);
                            _logger.LogError(warn);
                        }
                        if (string.IsNullOrEmpty(_config.SftpUser))
                        {
                            var warn = "⚠️ CONFIGURATION ERROR: OpcUa_Sftp_Enabled=true but OpcUa_Sftp_User is empty.";
                            _config.ConfigWarnings.Add(warn);
                            _logger.LogError(warn);
                        }
                        if (string.IsNullOrEmpty(_config.SftpKeyPath))
                        {
                            var warn = "⚠️ CONFIGURATION WARNING: OpcUa_Sftp_KeyPath is empty. " +
                                       "SSH key authentication will not be available (password auth only).";
                            _config.ConfigWarnings.Add(warn);
                            _logger.LogWarning(warn);
                        }
                        else if (!File.Exists(_config.SftpKeyPath))
                        {
                            var warn = $"⚠️ CONFIGURATION ERROR: OpcUa_Sftp_KeyPath='{_config.SftpKeyPath}' — file not found.";
                            _config.ConfigWarnings.Add(warn);
                            _logger.LogError(warn);
                        }
                    }
                    break;

                default:
                    var unknownWarn = $"⚠️ Unknown OpcUa_Certificate_Mode='{_config.CertificateMode}' in Excel. " +
                                      $"Valid values: none, auto-accept, manual-trust, ca. Treating as auto-accept.";
                    _config.ConfigWarnings.Add(unknownWarn);
                    _logger.LogWarning(unknownWarn);
                    break;
            }

            // ═══ INFORMATIONAL: Explain empty optional fields (have sensible defaults) ═══
            if (mode != "none")
            {
                if (string.IsNullOrEmpty(_config.CertificatePath))
                    _logger.LogInformation("🌐 OpcUa_CertificatePath is empty → certificate auto-generated in %LocalAppData%\\Aquafrisch\\opcua-certs\\own\\");
                if (string.IsNullOrEmpty(_config.TrustedCertsFolder))
                    _logger.LogInformation("🌐 OpcUa_TrustedCertsFolder is empty → using default: %LocalAppData%\\Aquafrisch\\opcua-certs\\trusted\\");
                if (string.IsNullOrEmpty(_config.RejectedCertsFolder))
                    _logger.LogInformation("🌐 OpcUa_RejectedCertsFolder is empty → using default: %LocalAppData%\\Aquafrisch\\opcua-certs\\rejected\\");
            }

            if (_config.ConfigWarnings.Count > 0)
            {
                _logger.LogWarning("════════════════════════════════════════════════════════════");
                _logger.LogWarning("⚠️ OPC/UA has {Count} configuration warning(s) — check project configuration!", _config.ConfigWarnings.Count);
                foreach (var w in _config.ConfigWarnings)
                {
                    _logger.LogWarning("  → {Warning}", w);
                    // Log each warning to L1 audit logs
                    _ = _auditLogService.LogAsync(
                        AuditCategory.OtCommunication, AuditAction.OpcUaConfigWarning, AuditResult.Warning,
                        w, userName: "System");
                }
                _logger.LogWarning("════════════════════════════════════════════════════════════");
            }

            _logger.LogInformation("🌐 OPC/UA Config → CertificateMode: {CertMode}, Security: {Policy}/{SecMode}, Anonymous: {Anon}", 
                _config.CertificateMode, _config.SecurityPolicy, _config.SecurityMode, _config.AllowAnonymous);

            // Load OPC/UA Variables from dedicated sheet
            _variables = await _excelConfigService.LoadOpcUaVariablesAsync(excelPath);
            _logger.LogInformation("🌐 Loaded {Count} OPC/UA variables", _variables.Count);

            // Load OPC/UA Alarms from dedicated sheet
            _alarms = await _excelConfigService.LoadOpcUaAlarmsAsync(excelPath);
            _logger.LogInformation("🌐 Loaded {Count} OPC/UA alarms", _alarms.Count);
        }

        private async Task StartServerAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🌐 Starting OPC/UA Server on port {Port}...", _config.Port);

            // Build OPC/UA Application Configuration
            var appConfig = new ApplicationConfiguration
            {
                ApplicationName = _config.ServerName,
                ApplicationUri = Utils.Format(@"urn:{0}:AquafrischSupervisor", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Server,
                ProductUri = "urn:Aquafrisch:SCADA:Server",

                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = { $"opc.tcp://0.0.0.0:{_config.Port}" },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 2000
                },

                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = @"Directory",
                        StorePath = GetCertificateStorePath("own"),
                        SubjectName = _config.ServerName
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = GetCertificateStorePath("issuers")
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = !string.IsNullOrEmpty(_config.TrustedCertsFolder) 
                            ? _config.TrustedCertsFolder 
                            : GetCertificateStorePath("trusted")
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = @"Directory",
                        StorePath = !string.IsNullOrEmpty(_config.RejectedCertsFolder) 
                            ? _config.RejectedCertsFolder 
                            : GetCertificateStorePath("rejected")
                    },
                    // AutoAccept driven by CertificateMode from Excel:
                    //   none / auto-accept → true (accept all)
                    //   manual-trust / ca  → false (validate against trust store)
                    AutoAcceptUntrustedCertificates = _config.CertificateMode.ToLowerInvariant() is "none" or "auto-accept",
                    RejectSHA1SignedCertificates = _config.CertificateMode.ToLowerInvariant() is "manual-trust" or "ca",
                    MinimumCertificateKeySize = 2048,
                    AddAppCertToTrustedStore = true
                },

                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 120000,
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 4194304,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },

                TraceConfiguration = new TraceConfiguration
                {
                    OutputFilePath = Path.Combine(AppContext.BaseDirectory, "opcua-trace.log"),
                    TraceMasks = 0x7FFFFFFF // All traces
                }
            };

            // Configure security policies based on Excel config
            appConfig.ServerConfiguration.SecurityPolicies = BuildSecurityPolicies();
            
            // Configure user token policies
            appConfig.ServerConfiguration.UserTokenPolicies = BuildUserTokenPolicies();

            _logger.LogInformation("🌐 Validating OPC/UA configuration...");
            await appConfig.Validate(ApplicationType.Server);
            _logger.LogInformation("🌐 Configuration validated OK");

            // Certificate validation behavior based on CertificateMode (from Excel)
            var certMode = _config.CertificateMode.ToLowerInvariant();
            switch (certMode)
            {
                case "none":
                case "auto-accept":
                    // Accept all certificates without validation
                    appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
                    appConfig.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = true; };
                    _logger.LogInformation("🌐 CertificateMode={Mode}: AutoAccept=true (all certificates accepted)", certMode);
                    break;

                case "manual-trust":
                    // Self-signed certs, manual .DER exchange — reject untrusted, log for approval
                    appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted)
                        {
                            _logger.LogWarning("🔐 Certificate rejected (untrusted): {Subject} — approve via /api/opcua/certificates/approve",
                                e.Certificate?.Subject ?? "unknown");
                        }
                    };
                    _logger.LogInformation("🔐 CertificateMode=manual-trust: Only trusted certificates accepted. Manage via /api/opcua/certificates/");
                    break;

                case "ca":
                    // CA-signed certificates ONLY — reject self-signed and untrusted certs
                    appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates = false;
                    appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        var cert = e.Certificate;
                        var subject = cert?.Subject ?? "unknown";

                        // In CA mode: reject self-signed certificates (issuer == subject)
                        if (cert != null && cert.Subject == cert.Issuer)
                        {
                            _logger.LogWarning("🔐 CA MODE: Rejected self-signed certificate: {Subject} (thumbprint: {Thumb})",
                                subject, cert.Thumbprint?.Substring(0, 16));
                            _ = _auditLogService.LogAsync(
                                AuditCategory.OtCommunication, AuditAction.OpcUaSecurityReject, AuditResult.Failure,
                                $"CA MODE: Rejected self-signed certificate: {subject}", userName: "System");
                            e.Accept = false;
                            return;
                        }

                        if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateUntrusted)
                        {
                            _logger.LogWarning("🔐 Certificate rejected (untrusted/not CA-signed): {Subject}", subject);
                            _ = _auditLogService.LogAsync(
                                AuditCategory.OtCommunication, AuditAction.OpcUaSecurityReject, AuditResult.Failure,
                                $"Certificate rejected (untrusted): {subject}", userName: "System");
                            e.Accept = false;
                        }
                        else if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateRevoked)
                        {
                            _logger.LogError("🔐 Certificate REVOKED: {Subject}", subject);
                            _ = _auditLogService.LogAsync(
                                AuditCategory.OtCommunication, AuditAction.OpcUaSecurityReject, AuditResult.Failure,
                                $"Certificate REVOKED: {subject}", userName: "System");
                            e.Accept = false;
                        }
                        else if (e.Error.StatusCode == Opc.Ua.StatusCodes.BadCertificateRevocationUnknown)
                        {
                            _logger.LogWarning("🔐 Certificate revocation status unknown (CRL unavailable?): {Subject}", subject);
                            // Allow connection but log warning — CRL may not be available yet
                        }
                    };

                    // ═══ CA MODE STARTUP: Move self-signed certs from trusted → rejected ═══
                    // The SDK accepts certs in trusted store WITHOUT calling the validator.
                    // In CA mode, only CA-signed certs belong in trusted. Self-signed must be removed.
                    // Skip in Development: test certs are self-signed, cleanup would conflict with SFTP sync.
                    var isDevEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                    if (isDevEnv)
                    {
                        _logger.LogInformation("🔧 [DEV] Skipping CA MODE trusted→rejected cleanup (test certs are self-signed)");
                    }
                    else try
                    {
                        var trustedPath = Path.Combine(GetCertificateStorePath("trusted"), "certs");
                        var rejectedPath = Path.Combine(GetCertificateStorePath("rejected"), "certs");
                        if (Directory.Exists(trustedPath))
                        {
                            Directory.CreateDirectory(rejectedPath);
                            foreach (var certFile in Directory.GetFiles(trustedPath, "*.der"))
                            {
                                try
                                {
                                    var x509 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile);
                                    if (x509.Subject == x509.Issuer) // Self-signed
                                    {
                                        var destFile = Path.Combine(rejectedPath, Path.GetFileName(certFile));
                                        File.Move(certFile, destFile, true);
                                        _logger.LogWarning("🔐 CA MODE: Moved self-signed cert from trusted → rejected: {Subject} ({File})",
                                            x509.Subject, Path.GetFileName(certFile));
                                        _ = _auditLogService.LogAsync(
                                            AuditCategory.OtCommunication, AuditAction.OpcUaConfigWarning, AuditResult.Warning,
                                            $"CA MODE: Moved self-signed cert from trusted → rejected: {x509.Subject}",
                                            userName: "System");
                                    }
                                }
                                catch (Exception certEx)
                                {
                                    _logger.LogWarning(certEx, "🔐 Could not check certificate: {File}", certFile);
                                }
                            }
                        }
                    }
                    catch (Exception cleanEx)
                    {
                        _logger.LogError(cleanEx, "🔐 Failed to clean self-signed certs from trusted store");
                    }

                    // Import CA root certificate into issuers store if configured
                    if (!string.IsNullOrEmpty(_config.CaCertPath) && File.Exists(_config.CaCertPath))
                    {
                        try
                        {
                            var caCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(_config.CaCertPath);
                            var issuersPath = GetCertificateStorePath("issuers");
                            var certsPath = Path.Combine(issuersPath, "certs");
                            Directory.CreateDirectory(certsPath);
                            var destFile = Path.Combine(certsPath, $"{caCert.Thumbprint}.der");
                            if (!File.Exists(destFile))
                            {
                                File.WriteAllBytes(destFile, caCert.RawData);
                                _logger.LogInformation("🔐 CA root certificate imported to issuers store: {Subject}", caCert.Subject);
                            }
                            else
                            {
                                _logger.LogInformation("🔐 CA root certificate already in issuers store: {Subject}", caCert.Subject);
                            }
                        }
                        catch (Exception caEx)
                        {
                            _logger.LogError(caEx, "🔐 Failed to import CA root certificate from: {Path}", _config.CaCertPath);
                        }
                    }

                    // Enable CRL checking if configured
                    if (_config.CrlCheckEnabled && !string.IsNullOrEmpty(_config.CrlUrl))
                    {
                        _logger.LogInformation("🔐 CertificateMode=ca: CRL check enabled (URL: {Url}, Interval: {Interval}s)", 
                            _config.CrlUrl, _config.CrlCheckInterval);
                    }
                    else
                    {
                        _logger.LogInformation("🔐 CertificateMode=ca: CA-signed mode (CRL check: {Enabled})", _config.CrlCheckEnabled);
                    }
                    break;

                default:
                    // Unknown mode → treat as auto-accept for safety
                    appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
                    appConfig.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = true; };
                    _logger.LogWarning("🌐 Unknown CertificateMode '{Mode}' — defaulting to auto-accept", certMode);
                    break;
            }

            // Create application instance
            _application = new ApplicationInstance
            {
                ApplicationName = _config.ServerName,
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = appConfig
            };

            // Check application certificate (create self-signed if needed)
            bool certOk = false;
            try
            {
                certOk = await _application.CheckApplicationInstanceCertificate(
                    silent: true, 
                    minimumKeySize: 2048);
            }
            catch (Exception certEx)
            {
                _logger.LogWarning(certEx, "🌐 Certificate check threw exception, will try to create new cert...");
                
                // Try to delete old cert and create fresh
                try
                {
                    var certStore = appConfig.SecurityConfiguration.ApplicationCertificate.OpenStore();
                    var certs = await certStore.Enumerate();
                    foreach (var cert in certs)
                    {
                        if (cert.Subject.Contains(_config.ServerName))
                        {
                            await certStore.Delete(cert.Thumbprint);
                            _logger.LogInformation("🌐 Removed old certificate: {Subject}", cert.Subject);
                        }
                    }
                    certStore.Close();
                    
                    certOk = await _application.CheckApplicationInstanceCertificate(
                        silent: true, 
                        minimumKeySize: 2048);
                }
                catch (Exception innerEx)
                {
                    _logger.LogWarning(innerEx, "🌐 Could not recreate certificate, continuing without cert...");
                }
            }
            
            if (!certOk)
            {
                _logger.LogWarning("🌐 OPC/UA certificate check failed, attempting to continue...");
            }

            // Create and start the server
            _server = new AquafrischOpcUaServer(
                _logger, _variables, _alarms, _config, _auditLogService, _twinCATService, _operationLogService);
            
            try
            {
                _logger.LogInformation("🌐 Cert store path: {Path}", GetCertificateStorePath("own"));
                _logger.LogInformation("🌐 Base address: opc.tcp://0.0.0.0:{Port}", _config.Port);
                _logger.LogInformation("🌐 Security: {Policy}/{Mode}, Anonymous: {Anon}", 
                    _config.SecurityPolicy, _config.SecurityMode, _config.AllowAnonymous);
                
                // Use ServerBase.Start directly instead of ApplicationInstance.Start
                _server.Start(appConfig);
                _logger.LogInformation("🌐 Server.Start() completed");
            }
            catch (ServiceResultException sre)
            {
                // Extract the REAL exception hidden inside OPC Foundation's ServiceResult chain
                _logger.LogError("🌐 ServiceResultException: {Msg}", sre.Message);
                _logger.LogError("🌐 StatusCode: {Code}", sre.StatusCode);
                
                // Walk the ServiceResult chain
                var innerResult = sre.Result;
                var depth = 0;
                while (innerResult != null && depth < 10)
                {
                    _logger.LogError("🌐 ServiceResult[{Depth}]: Code={Code}, Text={Text}", 
                        depth, innerResult.StatusCode, innerResult.LocalizedText);
                    innerResult = innerResult.InnerResult;
                    depth++;
                }
                
                // Walk the .NET InnerException chain
                Exception? innerEx = sre.InnerException;
                depth = 0;
                while (innerEx != null && depth < 10)
                {
                    _logger.LogError("🌐 InnerException[{Depth}] {Type}: {Message}\n{Stack}", 
                        depth, innerEx.GetType().FullName, innerEx.Message, innerEx.StackTrace);
                    innerEx = innerEx.InnerException;
                    depth++;
                }
                
                // Full stack trace
                _logger.LogError("🌐 Full StackTrace:\n{Stack}", sre.StackTrace);
                throw;
            }
            catch (Exception startEx)
            {
                _logger.LogError(startEx, "🌐 Non-SRE exception during server start");
                throw;
            }

            _isRunning = true;
            _startedAt = DateTime.UtcNow;
            _statusMessage = $"Running on port {_config.Port} ({_config.SecurityPolicy}/{_config.SecurityMode})";

            // Capture node manager reference for PLC polling bridge
            _nodeManager = ((AquafrischOpcUaServer)_server).NodeManager;
            _nodeManager.OnClientWrite = (varName, value) => _previousValues[varName] = value;
            _nodeManager.ResolveClientName = ResolveClientNameFromSessionId;
            
            UpdateMetrics();
            _logger.LogInformation("🌐 OPC/UA Server started successfully on port {Port}", _config.Port);
        }

        /// <summary>
        /// Resolve a friendly client name from a SessionId (NodeId).
        /// Looks up the session in the server's SessionManager, then delegates to GetClientName.
        /// Used by the NodeManager's write handler to log consistent names across L1/L2.
        /// </summary>
        private string ResolveClientNameFromSessionId(NodeId? sessionId)
        {
            if (sessionId == null) return "Unknown";
            try
            {
                var sessions = _server?.CurrentInstance?.SessionManager?.GetSessions();
                if (sessions != null)
                {
                    var session = sessions.FirstOrDefault(s => s.Id == sessionId);
                    if (session != null)
                        return ((AquafrischOpcUaServer)_server!).GetClientNamePublic(session);
                }
            }
            catch { /* best-effort */ }
            return $"Client [{sessionId}]";
        }

        /// <summary>
        /// Reads all OPC/UA variables + alarms from TwinCAT PLC and updates OPC/UA nodes
        /// </summary>
        private async Task PollPlcVariablesAsync()
        {
            if (!_twinCATService.IsConnected)
                return;

            try
            {
                var now = DateTime.UtcNow;

                foreach (var v in _variables)
                {
                    try
                    {
                        var access = v.AccessMode?.ToLowerInvariant();
                        var clrType = MapDataTypeToClr(v.DataType);

                        if (access is "writeonly" or "wo")
                        {
                            // WriteOnly: OPC UA → ADS only. No polling.
                            continue;
                        }

                        // Per-variable rate: skip if not due yet
                        var rateMs = v.UpdateRateMs > 0 ? v.UpdateRateMs : 1000;
                        if (_lastPollTime.TryGetValue(v.VariableName, out var lastTime)
                            && (now - lastTime).TotalMilliseconds < rateMs)
                            continue;

                        // ReadWrite variables that were just written: give ADS time
                        if ((access is "readwrite" or "rw") && _nodeManager!.IsWriteSuppressed(v.VariableName))
                            continue;

                        _lastPollTime[v.VariableName] = now;

                        var value = await _twinCATService.ReadVariableAsync(v.PlcSymbolPath, clrType);
                        if (value != null)
                        {
                            _nodeManager!.UpdateVariableValue(v.VariableName, value);

                            if (_previousValues.TryGetValue(v.VariableName, out var prev))
                            {
                                // Solo loguear cambios reales (no inicialización)
                                if (!Equals(prev, value))
                                {
                                    _previousValues[v.VariableName] = value;
                                    _ = _operationLogService.LogAsync(
                                        OperationCategory.OpcUa, OperationAction.OpcUaValueChange,
                                        $"{v.VariableName}: {prev} → {value}",
                                        user: "PLC");
                                }
                            }
                            else
                            {
                                // Primera lectura: guardar estado inicial sin loguear
                                _previousValues[v.VariableName] = value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("🌐 Poll failed for {Var}: {Msg}", v.VariableName, ex.Message);
                    }
                }

                // Update alarm nodes from AlarmNotificationService (push-based, 0 extra ADS reads)
                var alarmStates = _alarmNotificationService.GetCurrentAlarmStates();
                foreach (var a in _alarms)
                {
                    try
                    {
                        // Map severity to st_alarmPc suffix: 0=Alarm, 1=Notification, 2=Info
                        string suffix = a.Severity switch
                        {
                            0 => "Alarm",
                            1 => "Notification",
                            2 => "Info",
                            _ => "Alarm"
                        };
                        
                        // Excel Index matches PLC array: Index 1 → st_alarmPc[1]
                        // Find matching key in alarm states (e.g., "MAIN.fbMachine.st_alarmPc[1].Notification")
                        bool isActive = false;
                        foreach (var kvp in alarmStates)
                        {
                            if (kvp.Key.Contains($"st_alarmPc[{a.AlarmIndex}].{suffix}"))
                            {
                                isActive = kvp.Value;
                                break;
                            }
                        }
                        
                        _nodeManager!.UpdateAlarmValue(a.AlarmIndex, isActive);

                        // Alarm change detection
                        if (_previousAlarmStates.TryGetValue(a.AlarmIndex, out var prevState))
                        {
                            // Solo loguear cambios reales (no inicialización)
                            if (prevState != isActive)
                            {
                                _previousAlarmStates[a.AlarmIndex] = isActive;
                                _ = _operationLogService.LogAsync(
                                    OperationCategory.OpcUa, OperationAction.OpcUaAlarmChange,
                                    $"Alarm[{a.AlarmIndex}] {a.Description}: {(isActive ? "ACTIVE" : "CLEARED")}",
                                    user: "PLC");
                            }
                        }
                        else
                        {
                            // Primera lectura: guardar estado inicial sin loguear
                            _previousAlarmStates[a.AlarmIndex] = isActive;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("🌐 Alarm state read failed for Alarm[{Idx}]: {Msg}", a.AlarmIndex, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("🌐 PLC polling cycle failed: {Msg}", ex.Message);
            }
        }

        internal static Type MapDataTypeToClr(string dataType)
        {
            return dataType?.ToLowerInvariant() switch
            {
                "boolean" or "bool" => typeof(bool),
                "int16" or "int" => typeof(short),
                "int32" or "dint" => typeof(int),
                "int64" or "lint" => typeof(long),
                "uint16" or "uint" or "word" => typeof(ushort),
                "uint32" or "udint" or "dword" => typeof(uint),
                "float" or "real" => typeof(float),
                "double" or "lreal" => typeof(double),
                "string" or "wstring" => typeof(string),
                "byte" or "usint" => typeof(byte),
                _ => typeof(float)
            };
        }

        private async Task StopServerAsync()
        {
            if (_server != null)
            {
                _logger.LogInformation("🌐 Stopping OPC/UA Server...");
                
                try
                {
                    _server.Stop();
                    _isRunning = false;
                    _statusMessage = "Stopped";
                    UpdateMetrics();

                    await _auditLogService.LogAsync(
                        AuditCategory.OtCommunication,
                        AuditAction.OpcUaServerStop,
                        AuditResult.Success,
                        "OPC/UA Server stopped",
                        userName: "System");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🌐 Error stopping OPC/UA Server");
                }
                
                _server = null;
            }
        }

        private ServerSecurityPolicyCollection BuildSecurityPolicies()
        {
            var policies = new ServerSecurityPolicyCollection();
            
            switch (_config.SecurityPolicy.ToLowerInvariant())
            {
                case "basic256sha256":
                    policies.Add(new ServerSecurityPolicy
                    {
                        SecurityMode = ParseSecurityMode(_config.SecurityMode),
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                    });
                    break;
                case "aes128_sha256_rsaoaep":
                    policies.Add(new ServerSecurityPolicy
                    {
                        SecurityMode = ParseSecurityMode(_config.SecurityMode),
                        SecurityPolicyUri = SecurityPolicies.Aes128_Sha256_RsaOaep
                    });
                    break;
                case "aes256_sha256_rsapss":
                    policies.Add(new ServerSecurityPolicy
                    {
                        SecurityMode = ParseSecurityMode(_config.SecurityMode),
                        SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss
                    });
                    break;
                case "none":
                default:
                    // Only in development — CADRA requires Basic256Sha256
                    policies.Add(new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    });
                    break;
            }

            return policies;
        }

        private UserTokenPolicyCollection BuildUserTokenPolicies()
        {
            var policies = new UserTokenPolicyCollection();

            if (_config.AllowAnonymous)
            {
                policies.Add(new UserTokenPolicy(UserTokenType.Anonymous));
            }

            if (!string.IsNullOrEmpty(_config.UserName))
            {
                policies.Add(new UserTokenPolicy(UserTokenType.UserName));
            }

            // Certificate-based auth always available if security is enabled
            if (_config.SecurityPolicy.ToLowerInvariant() != "none")
            {
                policies.Add(new UserTokenPolicy(UserTokenType.Certificate));
            }

            // If no policy was added, add anonymous as fallback
            if (policies.Count == 0)
            {
                policies.Add(new UserTokenPolicy(UserTokenType.Anonymous));
            }

            return policies;
        }

        private static MessageSecurityMode ParseSecurityMode(string mode)
        {
            return mode?.ToLowerInvariant() switch
            {
                "sign" => MessageSecurityMode.Sign,
                "signandencrypt" => MessageSecurityMode.SignAndEncrypt,
                _ => MessageSecurityMode.None
            };
        }

        private string GetCertificateStorePath(string subfolder)
        {
            // Use a path without spaces to avoid potential OPC Foundation issues
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aquafrisch", "opcua-certs");
            var path = Path.Combine(basePath, subfolder);
            Directory.CreateDirectory(path);
            return path;
        }

        private void UpdateMetrics()
        {
            var clientCount = 0;
            if (_server?.CurrentInstance?.SessionManager != null)
            {
                clientCount = _server.CurrentInstance.SessionManager.GetSessions()?.Count ?? 0;
            }

            _metricsService.SetOpcUaServerStatus(
                _config.Enabled,
                _isRunning,
                _statusMessage,
                clientCount);
        }

        // ===== IOpcUaServerService =====

        public OpcUaServerStatus GetStatus()
        {
            var status = new OpcUaServerStatus
            {
                Enabled = _config.Enabled,
                Running = _isRunning,
                StatusMessage = _statusMessage,
                ServerUri = _config.ServerUri,
                Port = _config.Port,
                SecurityPolicy = _config.SecurityPolicy,
                SecurityMode = _config.SecurityMode,
                PublishedVariables = _variables.Count,
                PublishedAlarms = _alarms.Count,
                StartedAt = _startedAt
            };

            if (_startedAt.HasValue)
            {
                var uptime = DateTime.UtcNow - _startedAt.Value;
                status.Uptime = $"{uptime.Days:00}:{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
            }

            if (_server?.CurrentInstance?.SessionManager != null)
            {
                var sessions = _server.CurrentInstance.SessionManager.GetSessions();
                status.ConnectedClients = (int)(sessions?.Count ?? 0);
                
                if (sessions != null)
                {
                    foreach (var session in sessions)
                    {
                        var friendlyName = ((AquafrischOpcUaServer)_server!).GetClientNamePublic(session);
                        status.Clients.Add(new OpcUaClientInfo
                        {
                            SessionId = session.Id?.ToString() ?? "",
                            ClientName = friendlyName,
                            RemoteAddress = session.SessionDiagnostics?.ClientDescription?.ApplicationUri ?? "",
                            ConnectedAt = session.SessionDiagnostics?.ClientConnectionTime ?? DateTime.MinValue,
                            ActiveSubscriptions = (int)(session.SessionDiagnostics?.CurrentSubscriptionsCount ?? 0)
                        });
                    }
                }
            }

            return status;
        }

        public List<OpcUaVariable> GetVariables() => _variables;
        public List<OpcUaAlarm> GetAlarms() => _alarms;
        public OpcUaConfig GetConfig() => _config;

        public Dictionary<string, object?> GetCurrentValues()
        {
            if (_nodeManager == null)
                return new Dictionary<string, object?>();
            return _nodeManager.GetCurrentValues();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // 🌐 Custom OPC/UA Server Implementation
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aquafrisch OPC/UA Server that bridges TwinCAT PLC variables to OPC/UA namespace
    /// </summary>
    internal class AquafrischOpcUaServer : StandardServer
    {
        private readonly ILogger _logger;
        private readonly List<OpcUaVariable> _variables;
        private readonly List<OpcUaAlarm> _alarms;
        private readonly OpcUaConfig _config;
        private readonly IAuditLogService _auditLogService;
        private readonly ITwinCATService _twinCATService;
        private readonly IOperationLogService _operationLogService;

        /// <summary>Captured reference to the node manager for PLC polling bridge</summary>
        public AquafrischNodeManager? NodeManager { get; private set; }

        public AquafrischOpcUaServer(
            ILogger logger,
            List<OpcUaVariable> variables,
            List<OpcUaAlarm> alarms,
            OpcUaConfig config,
            IAuditLogService auditLogService,
            ITwinCATService twinCATService,
            IOperationLogService operationLogService)
        {
            _logger = logger;
            _variables = variables;
            _alarms = alarms;
            _config = config;
            _auditLogService = auditLogService;
            _twinCATService = twinCATService;
            _operationLogService = operationLogService;
        }

        protected override MasterNodeManager CreateMasterNodeManager(
            IServerInternal server, ApplicationConfiguration configuration)
        {
            _logger.LogInformation("🌐 CreateMasterNodeManager called");
            try
            {
                var nodeManagers = new List<INodeManager>();
                
                var nodeManager = new AquafrischNodeManager(
                    server, configuration, _logger, _variables, _alarms,
                    _twinCATService, _auditLogService, _operationLogService);
                nodeManagers.Add(nodeManager);

                // Store reference for PLC polling bridge
                NodeManager = nodeManager;

                _logger.LogInformation("🌐 MasterNodeManager created successfully");
                return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 FATAL: CreateMasterNodeManager failed!");
                throw;
            }
        }


        protected override ServerProperties LoadServerProperties()
        {
            return new ServerProperties
            {
                ManufacturerName = "Aquafrisch",
                ProductName = _config.ServerName,
                ProductUri = "urn:Aquafrisch:SCADA:Server",
                SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
                BuildNumber = "1.0.0",
                BuildDate = DateTime.UtcNow
            };
        }

        protected override void OnServerStarted(IServerInternal server)
        {
            base.OnServerStarted(server);
            
            // Register session event handlers for audit logging
            server.SessionManager.SessionActivated += OnSessionActivated;
            server.SessionManager.SessionClosing += OnSessionClosing;
            
            _logger.LogInformation("🌐 OPC/UA Server event handlers registered");
        }

        private string GetClientName(Session session)
        {
            if (session == null) return "unknown";

            var sessionName = session.SessionDiagnostics?.SessionName ?? "";
            var sessionId = session.Id?.ToString() ?? "";
            var identity = session.Identity?.DisplayName ?? "";

            // Check if SessionName is meaningful (not generic "Session i=N" pattern)
            bool isGenericName = string.IsNullOrEmpty(sessionName) 
                || sessionName.StartsWith("Session ", StringComparison.OrdinalIgnoreCase);

            // Try ClientDescription from diagnostics
            var appName = session.SessionDiagnostics?.ClientDescription?.ApplicationName?.Text ?? "";
            var appUri = session.SessionDiagnostics?.ClientDescription?.ApplicationUri ?? "";

            // Build best name: prefer real SessionName > AppName > AppUri > SessionId
            string clientName;
            if (!isGenericName)
                clientName = sessionName;
            else if (!string.IsNullOrEmpty(appName))
                clientName = appName;
            else if (!string.IsNullOrEmpty(appUri))
                clientName = appUri;
            else
                clientName = $"Client [{sessionId}]";

            // Append identity if not anonymous
            if (!string.IsNullOrEmpty(identity) && identity != "Anonymous")
                clientName += $" ({identity})";

            return clientName;
        }

        /// <summary>Public accessor for client name resolution (used by parent service).</summary>
        public string GetClientNamePublic(Session session) => GetClientName(session);

        private void OnSessionActivated(Session session, SessionEventReason reason)
        {
            var clientName = GetClientName(session);
            var details = $"Client '{clientName}' connected ({reason})";

            _logger.LogInformation("🌐 OPC/UA {Details}", details);

            _ = _auditLogService.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaClientConnect,
                AuditResult.Success,
                details,
                userName: "System");
        }

        private void OnSessionClosing(Session session, SessionEventReason reason)
        {
            var clientName = GetClientName(session);

            _logger.LogInformation("🌐 OPC/UA Client disconnected: {Client} (Reason: {Reason})", clientName, reason);

            _ = _auditLogService.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaClientDisconnect,
                AuditResult.Success,
                $"Client '{clientName}' disconnected ({reason})",
                userName: "System");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // 🌐 Custom Node Manager - Bridges Excel-configured variables to OPC/UA nodes
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Node Manager that creates OPC/UA nodes from Excel-configured variables and alarms.
    /// Values are updated via TwinCAT PLC polling service.
    /// </summary>
    internal class AquafrischNodeManager : CustomNodeManager2
    {
        private readonly ILogger _logger;
        private readonly List<OpcUaVariable> _variables;
        private readonly List<OpcUaAlarm> _alarms;
        private readonly ITwinCATService _twinCATService;
        private readonly IAuditLogService _auditLogService;
        private readonly IOperationLogService _operationLogService;
        private readonly Dictionary<string, BaseDataVariableState> _variableNodes = new();
        private readonly Dictionary<string, OpcUaVariable> _variableConfigByNodeId = new();

        // Write suppression: after a client writes a variable, skip polling it briefly
        // so the PLC has time to process the command before we read back
        private readonly ConcurrentDictionary<string, DateTime> _writeSuppression = new();
        private const int WRITE_SUPPRESS_SECONDS = 5;

        /// <summary>
        /// Callback to notify the parent service that a client wrote a value,
        /// so it can sync _previousValues and avoid phantom "PLC change" logs.
        /// </summary>
        public Action<string, object>? OnClientWrite { get; set; }

        /// <summary>
        /// Callback to resolve a friendly client name from ISystemContext.
        /// Set by the parent server (AquafrischOpcUaServer) after creation.
        /// </summary>
        public Func<NodeId?, string>? ResolveClientName { get; set; }

        public AquafrischNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            ILogger logger,
            List<OpcUaVariable> variables,
            List<OpcUaAlarm> alarms,
            ITwinCATService twinCATService,
            IAuditLogService auditLogService,
            IOperationLogService operationLogService)
            : base(server, configuration, "http://aquafrisch.com/SCADA")
        {
            _logger = logger;
            _variables = variables;
            _alarms = alarms;
            _twinCATService = twinCATService;
            _auditLogService = auditLogService;
            _operationLogService = operationLogService;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                base.CreateAddressSpace(externalReferences);

                // Create root folder: "Aquafrisch"
                var rootFolder = CreateFolder(null, "Aquafrisch", "Aquafrisch SCADA");
                rootFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                // Ensure external references
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
                {
                    references = new List<IReference>();
                    externalReferences[ObjectIds.ObjectsFolder] = references;
                }
                references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, rootFolder.NodeId));

                // Create Variables folder
                var variablesFolder = CreateFolder(rootFolder, "Variables", "Process Variables");

                // Create individual variable nodes from Excel config
                foreach (var v in _variables)
                {
                    var node = CreateVariable(variablesFolder, v);
                    if (node != null)
                    {
                        _variableNodes[v.VariableName] = node;
                        // Store lookup for write handler
                        var nodeIdStr = ParseNodeIdString(v.NodeId);
                        _variableConfigByNodeId[nodeIdStr] = v;

                        // Attach write handler for ReadWrite/WriteOnly variables
                        // Use OnWriteValue (full handler) instead of OnSimpleWriteValue
                        // to receive the client's SourceTimestamp via ref DateTime timestamp
                        if (v.AccessMode?.ToLowerInvariant() is "readwrite" or "rw" or "writeonly" or "wo")
                        {
                            node.OnWriteValue = OnWriteValueHandler;
                        }
                    }
                }

                // Create Alarms folder
                if (_alarms.Count > 0)
                {
                    var alarmsFolder = CreateFolder(rootFolder, "Alarms", "Process Alarms");
                    foreach (var a in _alarms)
                    {
                        CreateAlarmVariable(alarmsFolder, a);
                    }
                }

                _logger.LogInformation(
                    "🌐 OPC/UA Address Space created: {VarCount} variables, {AlarmCount} alarms",
                    _variableNodes.Count, _alarms.Count);
            }
        }

        /// <summary>
        /// Update a variable value (called from PLC polling service)
        /// </summary>
        public void UpdateVariableValue(string variableName, object value)
        {
            lock (Lock)
            {
                if (_variableNodes.TryGetValue(variableName, out var node))
                {
                    node.Value = value;
                    node.Timestamp = DateTime.UtcNow;
                    node.ClearChangeMasks(SystemContext, false);
                }
            }
        }

        /// <summary>
        /// Mark a variable as recently written by OPC UA client — suppress polling temporarily.
        /// </summary>
        public void SuppressPolling(string variableName)
        {
            _writeSuppression[variableName] = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if a variable's polling should be suppressed (recently written by client).
        /// </summary>
        public bool IsWriteSuppressed(string variableName)
        {
            if (_writeSuppression.TryGetValue(variableName, out var writeTime))
            {
                if ((DateTime.UtcNow - writeTime).TotalSeconds < WRITE_SUPPRESS_SECONDS)
                    return true;
                _writeSuppression.TryRemove(variableName, out _);
            }
            return false;
        }

        /// <summary>
        /// Update an alarm value (called from PLC polling service)
        /// </summary>
        public void UpdateAlarmValue(int alarmIndex, bool active)
        {
            lock (Lock)
            {
                var key = $"Alarm_{alarmIndex}";
                if (_variableNodes.TryGetValue(key, out var node))
                {
                    node.Value = active;
                    node.Timestamp = DateTime.UtcNow;
                    node.ClearChangeMasks(SystemContext, false);
                }
            }
        }

        /// <summary>
        /// Get the current OPC UA node value for a variable (for ReadWrite sync)
        /// </summary>
        public object? GetVariableValue(string variableName)
        {
            lock (Lock)
            {
                if (_variableNodes.TryGetValue(variableName, out var node))
                    return node.Value;
                return null;
            }
        }

        /// <summary>
        /// Get current values of all tracked nodes (variables + alarms)
        /// </summary>
        public Dictionary<string, object?> GetCurrentValues()
        {
            lock (Lock)
            {
                var result = new Dictionary<string, object?>(_variableNodes.Count);
                foreach (var kvp in _variableNodes)
                {
                    result[kvp.Key] = kvp.Value.Value;
                }
                return result;
            }
        }

        /// <summary>
        /// OPC UA client write handler (full) — intercepts writes, forwards to PLC, audits.
        /// Uses the full OnWriteValue delegate to receive SourceTimestamp from the client.
        /// Event-driven: fires only when a client explicitly writes a value.
        /// </summary>
        private ServiceResult OnWriteValueHandler(
            ISystemContext context,
            NodeState node,
            NumericRange indexRange,
            QualifiedName dataEncoding,
            ref object value,
            ref StatusCode statusCode,
            ref DateTime timestamp)
        {
            try
            {
                var variableNode = node as BaseDataVariableState;
                if (variableNode == null)
                    return Opc.Ua.StatusCodes.BadNodeIdUnknown;

                // Find the variable config by node symbolic name
                var varName = variableNode.SymbolicName;
                var varConfig = _variables.Find(v => v.VariableName == varName);
                if (varConfig == null)
                {
                    _logger.LogWarning("🌐 Write rejected: unknown variable {Name}", varName);
                    return Opc.Ua.StatusCodes.BadNodeIdUnknown;
                }

                var oldValue = variableNode.Value;
                // Resolve friendly client name (same as L1 logs) instead of raw SessionId
                var clientInfo = ResolveClientName?.Invoke(context?.SessionId) ?? context?.SessionId?.ToString() ?? "Unknown";
                // Capture ref parameters for use in lambdas
                var writeValue = value;

                // Per OPC UA Part 4 §5.10.4: "If the SourceTimestamp is specified,
                // the Server shall use these values."
                // If the client does NOT send a SourceTimestamp, we do NOT fabricate one.
                // The node keeps its previous SourceTimestamp (from last poll or creation).
                var clientTimestamp = timestamp;
                var hasClientTimestamp = clientTimestamp != DateTime.MinValue && clientTimestamp.Year > 2000;
                var tsInfo = hasClientTimestamp ? clientTimestamp.ToString("HH:mm:ss.fff") : "NOT_PROVIDED";

                _logger.LogInformation(
                    "🌐 OPC/UA Write: {Var} = {Old} → {New} (type: {ValType}, client: {Client}, clientTs: {ClientTs})",
                    varName, oldValue, writeValue, writeValue?.GetType().Name ?? "null", clientInfo,
                    hasClientTimestamp ? clientTimestamp.ToString("HH:mm:ss.fff") : "NOT_PROVIDED");

                if (hasClientTimestamp)
                {
                    // Client provided a SourceTimestamp — honour it per spec
                    variableNode.Timestamp = clientTimestamp;
                    timestamp = clientTimestamp;
                }
                // else: leave node.Timestamp untouched — don't invent data
                variableNode.ClearChangeMasks(SystemContext, false);

                var access = varConfig.AccessMode?.ToLowerInvariant();
                var clrType = OpcUaServerService.MapDataTypeToClr(varConfig.DataType);

                // Convert OPC UA value to expected CLR type (e.g., Int32 → Int16)
                object convertedValue;
                try
                {
                    convertedValue = Convert.ChangeType(writeValue, clrType);
                }
                catch
                {
                    convertedValue = writeValue; // fallback: let WriteVariableAsync handle it
                }

                if (access is "writeonly" or "wo")
                {
                    // WriteOnly: forward directly to ADS (no poll loop for these)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var success = await _twinCATService.WriteVariableAsync(
                                varConfig.PlcSymbolPath, convertedValue, clrType);
                            // Variable operations → solo L2 (Operation Log)
                            await _operationLogService.LogAsync(
                                OperationCategory.OpcUa, OperationAction.OpcUaNodeWrite,
                                $"WriteOnly OPC→ADS: {varName} = {convertedValue} (PLC: {(success ? "OK" : "FAILED")}, client: {clientInfo}, srcTs: {tsInfo})",
                                user: $"OpcUaClient:{clientInfo}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "🌐 WriteOnly forward failed for {Var}", varName);
                        }
                    });
                }
                else if (access is "readwrite" or "rw")
                {
                    // ReadWrite: forward to ADS immediately, then suppress polling briefly.
                    // Notify parent to sync _previousValues so the poll loop won't see
                    // a phantom "PLC change" when it resumes and reads the value we just wrote.
                    OnClientWrite?.Invoke(varName, convertedValue);
                    SuppressPolling(varName);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var success = await _twinCATService.WriteVariableAsync(
                                varConfig.PlcSymbolPath, convertedValue, clrType);
                            // Variable operations → solo L2 (Operation Log)
                            await _operationLogService.LogAsync(
                                OperationCategory.OpcUa, OperationAction.OpcUaNodeWrite,
                                $"ReadWrite OPC→ADS: {varName} = {convertedValue} (PLC: {(success ? "OK" : "FAILED")}, client: {clientInfo}, srcTs: {tsInfo})",
                                user: $"OpcUaClient:{clientInfo}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "🌐 ReadWrite forward failed for {Var}", varName);
                        }
                    });
                }

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error in OnWriteValue handler");
                return Opc.Ua.StatusCodes.BadInternalError;
            }
        }

        private FolderState CreateFolder(NodeState? parent, string name, string displayName)
        {
            var folder = new FolderState(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(name, NamespaceIndex),
                BrowseName = new QualifiedName(name, NamespaceIndex),
                DisplayName = new LocalizedText("en", displayName),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            parent?.AddChild(folder);
            AddPredefinedNode(SystemContext, folder);
            return folder;
        }

        private BaseDataVariableState? CreateVariable(FolderState folder, OpcUaVariable config)
        {
            try
            {
                var dataType = MapDataType(config.DataType);
                var node = new BaseDataVariableState(folder)
                {
                    SymbolicName = config.VariableName,
                    ReferenceTypeId = ReferenceTypes.Organizes,
                    TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                    NodeId = new NodeId(ParseNodeIdString(config.NodeId), NamespaceIndex),
                    BrowseName = new QualifiedName(config.VariableName, NamespaceIndex),
                    DisplayName = new LocalizedText("en", config.VariableName),
                    Description = new LocalizedText("en", config.Description),
                    DataType = dataType,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = MapAccessLevel(config.AccessMode),
                    UserAccessLevel = MapAccessLevel(config.AccessMode),
                    Historizing = false,
                    Value = GetDefaultValue(config.DataType),
                    StatusCode = Opc.Ua.StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                folder.AddChild(node);
                AddPredefinedNode(SystemContext, node);

                // Add EngineeringUnits property (OPC UA Part 8) if Unit is defined
                if (!string.IsNullOrWhiteSpace(config.Unit))
                {
                    var euProp = new PropertyState<EUInformation>(node)
                    {
                        NodeId = new NodeId($"{ParseNodeIdString(config.NodeId)}_EU", NamespaceIndex),
                        BrowseName = BrowseNames.EngineeringUnits,
                        DisplayName = new LocalizedText("EngineeringUnits"),
                        DataType = DataTypeIds.EUInformation,
                        ValueRank = ValueRanks.Scalar,
                        AccessLevel = AccessLevels.CurrentRead,
                        UserAccessLevel = AccessLevels.CurrentRead,
                        ReferenceTypeId = ReferenceTypes.HasProperty,
                        TypeDefinitionId = VariableTypeIds.PropertyType,
                        Value = new EUInformation(config.Unit, config.Unit, "http://www.opcfoundation.org/UA/units/un/cefact")
                    };
                    node.AddChild(euProp);
                    AddPredefinedNode(SystemContext, euProp);
                }

                return node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error creating OPC/UA variable node: {Name}", config.VariableName);
                return null;
            }
        }

        private void CreateAlarmVariable(FolderState folder, OpcUaAlarm config)
        {
            try
            {
                var node = new BaseDataVariableState(folder)
                {
                    SymbolicName = $"Alarm_{config.AlarmIndex}",
                    ReferenceTypeId = ReferenceTypes.Organizes,
                    TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                    NodeId = new NodeId(ParseNodeIdString(config.NodeId), NamespaceIndex),
                    BrowseName = new QualifiedName($"Alarm_{config.AlarmIndex}", NamespaceIndex),
                    DisplayName = new LocalizedText("en", config.Description),
                    DataType = DataTypeIds.Boolean,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentRead,
                    UserAccessLevel = AccessLevels.CurrentRead,
                    Value = false,
                    StatusCode = Opc.Ua.StatusCodes.Good,
                    Timestamp = DateTime.UtcNow
                };

                folder.AddChild(node);
                AddPredefinedNode(SystemContext, node);

                // Store reference for alarm polling updates
                _variableNodes[$"Alarm_{config.AlarmIndex}"] = node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error creating OPC/UA alarm node: {Index}", config.AlarmIndex);
            }
        }

        private static NodeId MapDataType(string dataType)
        {
            return dataType?.ToLowerInvariant() switch
            {
                "boolean" or "bool" => DataTypeIds.Boolean,
                "int16" or "int" => DataTypeIds.Int16,
                "int32" or "dint" => DataTypeIds.Int32,
                "int64" or "lint" => DataTypeIds.Int64,
                "uint16" or "uint" or "word" => DataTypeIds.UInt16,
                "uint32" or "udint" or "dword" => DataTypeIds.UInt32,
                "float" or "real" => DataTypeIds.Float,
                "double" or "lreal" => DataTypeIds.Double,
                "string" or "wstring" => DataTypeIds.String,
                "byte" or "usint" => DataTypeIds.Byte,
                "datetime" => DataTypeIds.DateTime,
                _ => DataTypeIds.Float
            };
        }

        private static byte MapAccessLevel(string accessMode)
        {
            return accessMode?.ToLowerInvariant() switch
            {
                "readwrite" or "rw" => AccessLevels.CurrentReadOrWrite,
                "writeonly" or "wo" => AccessLevels.CurrentWrite,
                _ => AccessLevels.CurrentRead // ReadOnly default
            };
        }

        /// <summary>
        /// Extracts the string identifier from a NodeId like "ns=2;s=VarName" → "VarName"
        /// </summary>
        private static string ParseNodeIdString(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return nodeId;
            // Strip "ns=N;s=" prefix if present
            var idx = nodeId.IndexOf("s=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return nodeId.Substring(idx + 2);
            // Strip "ns=N;i=" prefix for numeric ids
            idx = nodeId.IndexOf("i=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return nodeId.Substring(idx + 2);
            return nodeId;
        }

        private static object GetDefaultValue(string dataType)
        {
            return dataType?.ToLowerInvariant() switch
            {
                "boolean" or "bool" => false,
                "int16" or "int" => (short)0,
                "int32" or "dint" => 0,
                "int64" or "lint" => 0L,
                "uint16" or "uint" or "word" => (ushort)0,
                "uint32" or "udint" or "dword" => 0u,
                "float" or "real" => 0.0f,
                "double" or "lreal" => 0.0,
                "string" or "wstring" => "",
                "byte" or "usint" => (byte)0,
                "datetime" => DateTime.MinValue,
                _ => 0.0f
            };
        }
    }
}
