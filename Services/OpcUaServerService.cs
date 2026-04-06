using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
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

        public bool IsEnabled => _config.Enabled;
        public bool IsRunning => _isRunning;

        public OpcUaServerService(
            ILogger<OpcUaServerService> logger,
            IExcelConfigService excelConfigService,
            IProjectContextService projectContext,
            IMetricsService metricsService,
            IAuditLogService auditLogService,
            ITwinCATService twinCATService,
            AlarmNotificationService alarmNotificationService)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _metricsService = metricsService;
            _auditLogService = auditLogService;
            _twinCATService = twinCATService;
            _alarmNotificationService = alarmNotificationService;
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
                var pollInterval = _config.DefaultSubscriptionIntervalMs > 0 
                    ? _config.DefaultSubscriptionIntervalMs : 1000;
                _logger.LogInformation("🌐 Starting PLC→OPC/UA bridge polling (interval: {Ms}ms)", pollInterval);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(pollInterval, stoppingToken);

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
                SecurityPolicy = sysConfig.OpcUaSecurityPolicy,
                SecurityMode = sysConfig.OpcUaSecurityMode,
                CertificatePath = sysConfig.OpcUaCertificatePath,
                PrivateKeyPath = sysConfig.OpcUaPrivateKeyPath,
                TrustedCertsFolder = sysConfig.OpcUaTrustedCertsFolder,
                RejectedCertsFolder = sysConfig.OpcUaRejectedCertsFolder,
                CrlCheckEnabled = sysConfig.OpcUaCrlCheckEnabled,
                CrlUrl = sysConfig.OpcUaCrlUrl,
                AllowAnonymous = sysConfig.OpcUaAllowAnonymous,
                UserName = sysConfig.OpcUaUserName,
                UserPassword = sysConfig.OpcUaUserPassword,
                WatchdogIntervalMs = sysConfig.OpcUaWatchdogIntervalMs,
                CommandFeedbackDurationMs = sysConfig.OpcUaCommandFeedbackDurationMs,
                DefaultSubscriptionIntervalMs = sysConfig.OpcUaDefaultSubscriptionIntervalMs
            };

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
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
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

            // Auto-accept certificates in development/testing
            if (_config.SecurityPolicy.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
                appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                {
                    e.Accept = true;
                };
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
                _logger, _variables, _alarms, _config, _auditLogService);
            
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
            
            UpdateMetrics();
            _logger.LogInformation("🌐 OPC/UA Server started successfully on port {Port}", _config.Port);
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
                // Poll process variables
                foreach (var v in _variables)
                {
                    try
                    {
                        var clrType = MapDataTypeToClr(v.DataType);
                        var value = await _twinCATService.ReadVariableAsync(v.PlcSymbolPath, clrType);
                        if (value != null)
                        {
                            _nodeManager!.UpdateVariableValue(v.VariableName, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("🌐 PLC read failed for {Var}: {Msg}", v.VariableName, ex.Message);
                    }
                }

                // Update alarm nodes from AlarmNotificationService (push-based, 0 extra ADS reads)
                var alarmStates = _alarmNotificationService.GetCurrentAlarmStates();
                foreach (var a in _alarms)
                {
                    try
                    {
                        // Map alarm index + severity to st_alarmPc key
                        // Severity: >=800 → .Alarm (0), 500-799 → .Notification (1), <500 → .Info (2)
                        string suffix = a.Severity >= 800 ? "Alarm" : a.Severity >= 500 ? "Notification" : "Info";
                        
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

        private static Type MapDataTypeToClr(string dataType)
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
                        status.Clients.Add(new OpcUaClientInfo
                        {
                            SessionId = session.Id?.ToString() ?? "",
                            ClientName = session.SessionDiagnostics?.SessionName ?? "Unknown",
                            RemoteAddress = session.SessionDiagnostics?.ClientConnectionTime.ToString("o") ?? "",
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

        /// <summary>Captured reference to the node manager for PLC polling bridge</summary>
        public AquafrischNodeManager? NodeManager { get; private set; }

        public AquafrischOpcUaServer(
            ILogger logger,
            List<OpcUaVariable> variables,
            List<OpcUaAlarm> alarms,
            OpcUaConfig config,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _variables = variables;
            _alarms = alarms;
            _config = config;
            _auditLogService = auditLogService;
        }

        protected override MasterNodeManager CreateMasterNodeManager(
            IServerInternal server, ApplicationConfiguration configuration)
        {
            _logger.LogInformation("🌐 CreateMasterNodeManager called");
            try
            {
                var nodeManagers = new List<INodeManager>();
                
                var nodeManager = new AquafrischNodeManager(
                    server, configuration, _logger, _variables, _alarms);
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

        private void OnSessionActivated(Session session, SessionEventReason reason)
        {
            var clientName = session?.SessionDiagnostics?.SessionName ?? "Unknown";
            _logger.LogInformation("🌐 OPC/UA Client connected: {Client} (Reason: {Reason})", clientName, reason);

            _ = _auditLogService.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.OpcUaClientConnect,
                AuditResult.Success,
                $"Client '{clientName}' connected ({reason})",
                userName: "System");
        }

        private void OnSessionClosing(Session session, SessionEventReason reason)
        {
            var clientName = session?.SessionDiagnostics?.SessionName ?? "Unknown";
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
        private readonly Dictionary<string, BaseDataVariableState> _variableNodes = new();

        public AquafrischNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            ILogger logger,
            List<OpcUaVariable> variables,
            List<OpcUaAlarm> alarms)
            : base(server, configuration, "http://aquafrisch.com/SCADA")
        {
            _logger = logger;
            _variables = variables;
            _alarms = alarms;
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
