using System.Collections.Concurrent;
using System.Net;
using ClosedXML.Excel;
using FluentModbus;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Modbus;
using SW.PC.API.Backend.Models.TwinCAT;
namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 📡 Modbus TCP Service interface (own interface, aligned with ITwinCATService / IOpcUaServerService).
    /// </summary>
    public interface IModbusService
    {
        /// <summary>Whether Modbus is enabled in Excel (false in the disabled stub).</summary>
        bool IsEnabled { get; }

        /// <summary>Whether the Modbus TCP server is currently running.</summary>
        bool ServerRunning { get; }

        /// <summary>Runtime status (server + sources).</summary>
        ModbusStatus GetStatus();

        /// <summary>Loaded Modbus variables from Excel.</summary>
        List<ModbusVariable> GetVariables();

        /// <summary>Loaded Modbus alarms from Excel.</summary>
        List<ModbusAlarm> GetAlarms();

        /// <summary>Loaded Modbus configuration from Excel.</summary>
        ModbusConfig GetConfig();

        /// <summary>Current engineering values of all Modbus variables/alarms.</summary>
        Dictionary<string, object?> GetCurrentValues();

        /// <summary>Read a single variable from an external Modbus source (Client role).</summary>
        Task<object?> ReadAsync(string sourceId, ModbusVariable variable);

        /// <summary>Write a single variable to an external Modbus source (Client role).</summary>
        Task<bool> WriteAsync(string sourceId, ModbusVariable variable, object value);

        /// <summary>Raised when a Modbus variable value changes (reuses PlcNotification).</summary>
        event EventHandler<PlcNotification>? OnVariableChanged;
    }

    /// <summary>
    /// 📡 Disabled stub — registered when Modbus is disabled/absent in Excel.
    /// No sockets, no threads, zero resources. Mirrors DisabledOpcUaServerService.
    /// </summary>
    public class DisabledModbusService : IModbusService
    {
        public bool IsEnabled => false;
        public bool ServerRunning => false;
        public ModbusStatus GetStatus() => new() { Enabled = false, ServerRunning = false, StatusMessage = "Disabled in configuration" };
        public List<ModbusVariable> GetVariables() => new();
        public List<ModbusAlarm> GetAlarms() => new();
        public ModbusConfig GetConfig() => new() { Enabled = false };
        public Dictionary<string, object?> GetCurrentValues() => new();
        public Task<object?> ReadAsync(string sourceId, ModbusVariable variable) => Task.FromResult<object?>(null);
        public Task<bool> WriteAsync(string sourceId, ModbusVariable variable, object value) => Task.FromResult(false);
        public event EventHandler<PlcNotification>? OnVariableChanged { add { } remove { } }
    }

    /// <summary>
    /// 📡 Modbus TCP BackgroundService.
    /// SERVER role: exposes ADS values (read via ITwinCATService) as Modbus registers for other systems.
    /// CLIENT role: reads/writes up to 2 external Modbus TCP sources.
    /// Alarms are read from the shared AlarmNotificationService cache (NO duplicate polling).
    /// Fully isolated: any Modbus failure is caught and degraded; it never affects ADS/OPC-UA/HMI.
    /// </summary>
    public class ModbusService : BackgroundService, IModbusService
    {
        private readonly ILogger<ModbusService> _logger;
        private readonly ITwinCATService _twinCAT;
        private readonly IProjectContextService _projectContext;
        private readonly AlarmNotificationService _alarmNotificationService;
        private readonly IAuditLogService _auditLogService;
        private readonly IOperationLogService _operationLogService;
        private readonly IMetricsService _metrics;

        private ModbusConfig _config = new();
        private List<ModbusVariable> _variables = new();
        private List<ModbusAlarm> _alarms = new();

        private ModbusTcpServer? _server;
        private DateTime? _serverStartedAt;
        private bool _serverRunning;

        // External sources (Client role)
        private readonly ConcurrentDictionary<string, ModbusTcpClient> _clients = new();
        private readonly ConcurrentDictionary<string, ModbusSourceStatus> _sourceStatus = new();

        // Current engineering values + change detection
        private readonly ConcurrentDictionary<string, object?> _currentValues = new();
        private readonly ConcurrentDictionary<string, object?> _previousValues = new();
        private readonly ConcurrentDictionary<int, bool> _previousAlarmStates = new();
        // RW server registers: last value we pushed to the buffer (to detect client writes)
        private readonly ConcurrentDictionary<string, double> _lastServerPushed = new();
        // One-time diagnostic: warn about Modbus alarms whose PLC index is not monitored centrally
        private bool _alarmMonitoringChecked;

        // Client reconnection throttling — avoids log spam and cycle blocking on dead sources
        private readonly ConcurrentDictionary<string, DateTime> _nextSourceRetry = new();
        private readonly ConcurrentDictionary<string, bool> _sourceDownLogged = new();
        private const int SourceRetryBackoffSec = 15;

        public event EventHandler<PlcNotification>? OnVariableChanged;

        public bool IsEnabled => _config.Enabled;
        public bool ServerRunning => _serverRunning;

        public ModbusService(
            ILogger<ModbusService> logger,
            ITwinCATService twinCAT,
            IProjectContextService projectContext,
            AlarmNotificationService alarmNotificationService,
            IAuditLogService auditLogService,
            IOperationLogService operationLogService,
            IMetricsService metrics)
        {
            _logger = logger;
            _twinCAT = twinCAT;
            _projectContext = projectContext;
            _alarmNotificationService = alarmNotificationService;
            _auditLogService = auditLogService;
            _operationLogService = operationLogService;
            _metrics = metrics;
        }

        public ModbusConfig GetConfig() => _config;
        public List<ModbusVariable> GetVariables() => _variables;
        public List<ModbusAlarm> GetAlarms() => _alarms;
        public Dictionary<string, object?> GetCurrentValues() => new(_currentValues);

        public ModbusStatus GetStatus()
        {
            return new ModbusStatus
            {
                Enabled = _config.Enabled,
                ServerRunning = _serverRunning,
                StatusMessage = _serverRunning ? "Running" : (_config.Enabled ? "Stopped" : "Disabled"),
                BindIp = _config.ServerBindIp,
                Port = _config.ServerPort,
                UnitId = _config.ServerUnitId,
                ConnectedClients = _server?.ConnectionCount ?? 0, // Modbus masters connected to our server
                PublishedVariables = _variables.Count(v => !v.IsExternalSource),
                PublishedAlarms = _alarms.Count,
                StartedAt = _serverStartedAt,
                Uptime = _serverStartedAt.HasValue ? (DateTime.UtcNow - _serverStartedAt.Value).ToString(@"dd\.hh\:mm\:ss") : "",
                Sources = _sourceStatus.Values.OrderBy(s => s.Id).ToList()
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 🛡️ ISOLATION: the entire Modbus subsystem is wrapped so a failure never affects the host.
            try
            {
                await LoadConfigurationAsync();

                if (!_config.Enabled)
                {
                    _logger.LogInformation("📡 Modbus disabled — service idle");
                    return;
                }

                StartServer();
                ConnectSources();
                PublishStatus();

                await _auditLogService.LogAsync(
                    Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusServerStart,
                    _serverRunning ? Models.AuditResult.Success : Models.AuditResult.Warning,
                    $"Modbus TCP server {(_serverRunning ? "started" : "NOT started")} on {_config.ServerBindIp}:{_config.ServerPort} (unit {_config.ServerUnitId}) — {_variables.Count(v => !v.IsExternalSource)} vars, {_alarms.Count} alarms, {_clients.Count} sources",
                    userName: "System");

                var interval = _config.PollIntervalMs > 0 ? _config.PollIntervalMs : 1000;
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PollCycleAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("📡 Modbus poll cycle error: {Msg}", ex.Message);
                    }
                    PublishStatus();
                    await Task.Delay(interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📡 Modbus service fatal error (isolated — host unaffected)");
            }
            finally
            {
                StopServer();
                DisconnectSources();
                try { _metrics.SetModbusServerStatus(_config.Enabled, false, _config.Enabled ? "Stopped" : "Disabled", 0, _config.Sources.Count); } catch { }
            }
        }

        private void PublishStatus()
        {
            try
            {
                int clients = _server?.ConnectionCount ?? 0;
                _metrics.SetModbusServerStatus(
                    _config.Enabled, _serverRunning,
                    _serverRunning ? "Running" : (_config.Enabled ? "Stopped" : "Disabled"),
                    clients, _config.Sources.Count);
            }
            catch { /* never break the loop on metrics */ }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIG LOADING (self-contained Excel parsing — does not touch ExcelConfigService)
        // ═══════════════════════════════════════════════════════════════════════
        private async Task LoadConfigurationAsync()
        {
            var excelPath = _projectContext.ExcelConfigPath;
            try
            {
                if (!File.Exists(excelPath))
                {
                    _logger.LogInformation("📡 Modbus: Excel not found at {Path} — Modbus disabled", excelPath);
                    _config = new ModbusConfig { Enabled = false };
                    return;
                }

                using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var wb = new XLWorkbook(stream);

                _config = ParseConfig(wb);
                if (!_config.Enabled)
                {
                    _variables = new();
                    _alarms = new();
                    return;
                }

                _variables = ParseVariables(wb);
                _alarms = ParseAlarms(wb);

                _logger.LogInformation("📡 Modbus config loaded: {Vars} variables, {Alarms} alarms, {Sources} sources",
                    _variables.Count, _alarms.Count, _config.Sources.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📡 Modbus: failed to load Excel config — Modbus left disabled");
                _config = new ModbusConfig { Enabled = false };
                _variables = new();
                _alarms = new();
            }
            await Task.CompletedTask;
        }

        private static IXLWorksheet? FindSheet(XLWorkbook wb, string name)
            => wb.Worksheets.FirstOrDefault(s => s.Name.Replace(" ", "").Equals(name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        private static bool ParseBool(string? raw)
        {
            var v = raw?.Trim().ToLowerInvariant() ?? "";
            return v is "true" or "1" or "on" or "si" or "sí" or "yes" or "x";
        }

        private ModbusConfig ParseConfig(XLWorkbook wb)
        {
            var cfg = new ModbusConfig();
            var sheet = FindSheet(wb, "System Config") ?? FindSheet(wb, "SystemConfig");
            if (sheet == null) return cfg; // no sheet → disabled

            var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            for (int row = 1; row <= lastRow; row++)
            {
                var key = sheet.Cell(row, 1).GetString()?.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "") ?? "";
                if (key.Length == 0) continue;
                kv[key] = sheet.Cell(row, 2).GetString()?.Trim() ?? "";
            }

            cfg.Enabled = ParseBool(kv.GetValueOrDefault("modbusenabled"));
            if (!cfg.Enabled) return cfg;

            cfg.ServerBindIp = kv.GetValueOrDefault("modbusserverbindip", "0.0.0.0");
            if (string.IsNullOrWhiteSpace(cfg.ServerBindIp)) cfg.ServerBindIp = "0.0.0.0";
            cfg.ServerPort = int.TryParse(kv.GetValueOrDefault("modbusserverport"), out var p) && p > 0 ? p : 502;
            cfg.ServerUnitId = byte.TryParse(kv.GetValueOrDefault("modbusserverunitid"), out var uid) ? uid : (byte)1;
            cfg.ServerAddressOffset = int.TryParse(kv.GetValueOrDefault("modbusserveraddressoffset"), out var ao) ? ao : 0;
            cfg.PollIntervalMs = int.TryParse(kv.GetValueOrDefault("modbuspollintervalms"), out var pi) && pi > 0 ? pi : 1000;

            // External sources (max 2 by convention)
            for (int i = 1; i <= 2; i++)
            {
                var host = kv.GetValueOrDefault($"modbusclient{i}host");
                if (string.IsNullOrWhiteSpace(host)) continue;
                cfg.Sources.Add(new ModbusSource
                {
                    Id = $"ModbusClient{i}",
                    Host = host,
                    Port = int.TryParse(kv.GetValueOrDefault($"modbusclient{i}port"), out var sp) && sp > 0 ? sp : 502,
                    UnitId = byte.TryParse(kv.GetValueOrDefault($"modbusclient{i}unitid"), out var su) ? su : (byte)1
                });
            }
            return cfg;
        }

        private List<ModbusVariable> ParseVariables(XLWorkbook wb)
        {
            var list = new List<ModbusVariable>();
            var sheet = FindSheet(wb, "Modbus_Variables");
            if (sheet == null) return list; // absent → unused, no error

            // Header-driven column detection
            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= 20; col++)
            {
                var h = sheet.Cell(1, col).GetString()?.Trim();
                if (!string.IsNullOrEmpty(h)) header[h.Replace(" ", "")] = col;
            }
            int Col(string name, int fallback) => header.GetValueOrDefault(name, fallback);

            int cName = Col("Name", 1);
            int cAds = Col("AdsSymbol", 2);
            int cReg = Col("ModbusRegister", 3);
            int cFunc = Col("Function", 4);
            int cRegType = Col("RegisterType", 5);
            int cAddr = Col("Address", 6);
            int cType = Col("DataType", 7);
            int cWord = Col("WordOrder", 8);
            int cScale = Col("Scale", 9);
            int cOffset = Col("Offset", 10);
            int cAccess = Col("AccessMode", 11);
            int cSource = Col("Source", 12);
            int cExclude = Col("ExcludeFromLog", 13);
            int cDesc = Col("Description", 14);
            int cUnit = Col("Unit", 15);

            int row = 2;
            while (!string.IsNullOrEmpty(sheet.Cell(row, cName).GetString()) ||
                   !string.IsNullOrEmpty(sheet.Cell(row, cAds).GetString()) ||
                   !string.IsNullOrEmpty(sheet.Cell(row, cReg).GetString()))
            {
                try
                {
                    var v = new ModbusVariable
                    {
                        Name = sheet.Cell(row, cName).GetString().Trim(),
                        AdsSymbol = sheet.Cell(row, cAds).GetString().Trim(),
                        DataType = sheet.Cell(row, cType).GetString().Trim().ToUpperInvariant(),
                        WordOrder = string.IsNullOrWhiteSpace(sheet.Cell(row, cWord).GetString()) ? "ABCD" : sheet.Cell(row, cWord).GetString().Trim().ToUpperInvariant(),
                        Scale = double.TryParse(sheet.Cell(row, cScale).GetString().Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sc) && sc != 0 ? sc : 1.0,
                        Offset = double.TryParse(sheet.Cell(row, cOffset).GetString().Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var of) ? of : 0.0,
                        AccessMode = string.IsNullOrWhiteSpace(sheet.Cell(row, cAccess).GetString()) ? "R" : sheet.Cell(row, cAccess).GetString().Trim().ToUpperInvariant(),
                        Source = string.IsNullOrWhiteSpace(sheet.Cell(row, cSource).GetString()) ? "ADS" : sheet.Cell(row, cSource).GetString().Trim(),
                        ExcludeFromLog = ParseBool(sheet.Cell(row, cExclude).GetString()),
                        Description = sheet.Cell(row, cDesc).GetString().Trim(),
                        Unit = sheet.Cell(row, cUnit).GetString().Trim()
                    };
                    if (string.IsNullOrEmpty(v.DataType)) v.DataType = "INT16";

                    // Register/type/address: prefer explicit RegisterType+Address, else classic ModbusRegister
                    var regTypeRaw = sheet.Cell(row, cRegType).GetString().Trim();
                    var addrRaw = sheet.Cell(row, cAddr).GetString().Trim();
                    var classic = sheet.Cell(row, cReg).GetString().Trim();

                    if (!string.IsNullOrEmpty(regTypeRaw) && int.TryParse(addrRaw, out var explicitAddr))
                    {
                        v.RegisterType = ParseRegisterType(regTypeRaw);
                        v.Address = explicitAddr;
                    }
                    else if (!string.IsNullOrEmpty(classic) && int.TryParse(classic, out var classicNum))
                    {
                        (v.RegisterType, v.Address) = FromClassicRegister(classicNum);
                    }
                    else
                    {
                        row++;
                        continue; // no valid register mapping → skip row
                    }

                    if (string.IsNullOrEmpty(v.Name)) v.Name = string.IsNullOrEmpty(v.AdsSymbol) ? $"{v.RegisterType}_{v.Address}" : v.AdsSymbol;
                    list.Add(v);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("📡 Modbus_Variables row {Row} skipped: {Msg}", row, ex.Message);
                }
                row++;
            }
            return list;
        }

        private List<ModbusAlarm> ParseAlarms(XLWorkbook wb)
        {
            var list = new List<ModbusAlarm>();
            var sheet = FindSheet(wb, "Modbus_Alarms");
            if (sheet == null) return list;

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= 20; col++)
            {
                var h = sheet.Cell(1, col).GetString()?.Trim();
                if (!string.IsNullOrEmpty(h)) header[h.Replace(" ", "")] = col;
            }
            int Col(string name, int fallback) => header.GetValueOrDefault(name, fallback);

            int cName = Col("AlarmName", 1);
            int cIndex = Col("AlarmIndex", Col("Index", 2));
            int cReg = Col("ModbusRegister", 3);
            int cRegType = Col("RegisterType", 4);
            int cAddr = Col("Address", 5);
            int cSeverity = Col("Severity", 6);
            int cDesc = Col("Description", 7);
            int cBit = header.GetValueOrDefault("Bit", header.GetValueOrDefault("BitPosition", -1));

            int row = 2;
            while (!string.IsNullOrEmpty(sheet.Cell(row, cName).GetString()) ||
                   !string.IsNullOrEmpty(sheet.Cell(row, cIndex).GetString()))
            {
                try
                {
                    var alarmName = sheet.Cell(row, cName).GetString().Trim();

                    // ── Index resolution (DECOUPLED) ──────────────────────────────
                    // PLC state index: canonical = name suffix (e.g. "..._001" → 1),
                    //   EXACTLY like OPC-UA's ExtractAlarmIndex. This is what reads st_alarmPc[idx].
                    // Modbus index: the explicit AlarmIndex column (e.g. 0,1,2) used to position
                    //   the bit (model B) / for display. Falls back to the name index.
                    int nameIdx = ExtractAlarmIndex(alarmName, -1);
                    bool hasExplicit = int.TryParse(sheet.Cell(row, cIndex).GetString().Trim(), out var explicitIdx);

                    var a = new ModbusAlarm
                    {
                        AlarmName = alarmName,
                        AlarmIndex = hasExplicit ? explicitIdx : (nameIdx >= 0 ? nameIdx : 0),
                        PlcAlarmIndex = nameIdx >= 0 ? nameIdx : (hasExplicit ? explicitIdx : 0),
                        Severity = int.TryParse(sheet.Cell(row, cSeverity).GetString().Trim(), out var sev) ? sev : 0,
                        Description = sheet.Cell(row, cDesc).GetString().Trim()
                    };

                    // Optional explicit bit position (model B). -1 = auto (AlarmIndex % 16).
                    a.Bit = cBit > 0 && int.TryParse(sheet.Cell(row, cBit).GetString().Trim(), out var bitPos) ? bitPos : -1;

                    var regTypeRaw = sheet.Cell(row, cRegType).GetString().Trim();
                    var addrRaw = sheet.Cell(row, cAddr).GetString().Trim();
                    var classic = sheet.Cell(row, cReg).GetString().Trim();
                    if (!string.IsNullOrEmpty(regTypeRaw) && int.TryParse(addrRaw, out var explicitAddr))
                    {
                        a.RegisterType = ParseRegisterType(regTypeRaw);
                        a.Address = explicitAddr;
                    }
                    else if (!string.IsNullOrEmpty(classic) && int.TryParse(classic, out var classicNum))
                    {
                        (a.RegisterType, a.Address) = FromClassicRegister(classicNum);
                    }
                    else
                    {
                        row++;
                        continue;
                    }

                    // AlarmIndex is 0-based (st_alarmPc[0..N]); gate on a real name, not index.
                    if (!string.IsNullOrEmpty(a.AlarmName)) list.Add(a);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("📡 Modbus_Alarms row {Row} skipped: {Msg}", row, ex.Message);
                }
                row++;
            }
            return list;
        }

        private static ModbusRegisterType ParseRegisterType(string raw)
        {
            var r = raw.Trim().ToLowerInvariant();
            return r switch
            {
                "coil" or "coils" or "0" => ModbusRegisterType.Coil,
                "discreteinput" or "discrete" or "di" or "1" => ModbusRegisterType.DiscreteInput,
                "inputregister" or "input" or "ir" or "3" => ModbusRegisterType.InputRegister,
                _ => ModbusRegisterType.HoldingRegister
            };
        }

        /// <summary>Convert classic 0xxxx/1xxxx/3xxxx/4xxxx notation to (type, 0-based address).</summary>
        private static (ModbusRegisterType, int) FromClassicRegister(int classic)
        {
            // e.g. 40001 → Holding, addr 0 ; 10001 → DiscreteInput addr 0 ; 30001 → Input addr 0 ; 00001/1 → Coil addr 0
            if (classic >= 40001 && classic <= 49999) return (ModbusRegisterType.HoldingRegister, classic - 40001);
            if (classic >= 30001 && classic <= 39999) return (ModbusRegisterType.InputRegister, classic - 30001);
            if (classic >= 10001 && classic <= 19999) return (ModbusRegisterType.DiscreteInput, classic - 10001);
            // 0xxxx coils (1..9999) or raw 0-based small numbers
            return (ModbusRegisterType.Coil, classic >= 1 ? classic - 1 : 0);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SERVER (Slave) — expose ADS values + alarms as Modbus registers
        // ═══════════════════════════════════════════════════════════════════════
        private void StartServer()
        {
            try
            {
                _server = new ModbusTcpServer(_logger) { EnableRaisingEvents = false };
                // FluentModbus TCP server responds to unit id 0 by default. Modbus masters
                // (e.g. Modbus Poll) only allow unit ids 1..255, so register the configured
                // ServerUnitId as an additional active unit. All buffer access uses this unit.
                if (_config.ServerUnitId != 0)
                {
                    try { _server.AddUnit(_config.ServerUnitId); }
                    catch (Exception ux) { _logger.LogWarning("📡 Could not register Modbus unit id {Unit}: {Msg}", _config.ServerUnitId, ux.Message); }
                }
                var ip = ParseBindIp(_config.ServerBindIp);
                _server.Start(new IPEndPoint(ip, _config.ServerPort));
                _serverRunning = true;
                _serverStartedAt = DateTime.UtcNow;
                _logger.LogInformation("📡 Modbus TCP server started on {Ip}:{Port}", _config.ServerBindIp, _config.ServerPort);
            }
            catch (Exception ex)
            {
                _serverRunning = false;
                _logger.LogError(ex, "📡 Modbus TCP server could not start on {Ip}:{Port} (degraded, host unaffected)", _config.ServerBindIp, _config.ServerPort);
                _ = _auditLogService.LogAsync(
                    Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusConfigWarning, Models.AuditResult.Warning,
                    $"Modbus TCP server failed to start on {_config.ServerBindIp}:{_config.ServerPort}: {ex.Message}", userName: "System");
            }
        }

        private static IPAddress ParseBindIp(string bind)
        {
            if (string.IsNullOrWhiteSpace(bind) || bind == "0.0.0.0") return IPAddress.Any;
            return IPAddress.TryParse(bind, out var ip) ? ip : IPAddress.Any;
        }

        private void StopServer()
        {
            try
            {
                if (_server != null && _serverRunning)
                {
                    _server.Stop();
                    _ = _auditLogService.LogAsync(
                        Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusServerStop, Models.AuditResult.Success,
                        "Modbus TCP server stopped", userName: "System");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("📡 Modbus server stop error: {Msg}", ex.Message);
            }
            finally
            {
                _serverRunning = false;
                _server?.Dispose();
                _server = null;
            }
        }

        private async Task PollCycleAsync()
        {
            // ---- SERVER: ADS-sourced variables → registers ----
            if (_serverRunning && _server != null)
            {
                var serverVars = _variables.Where(v => !v.IsExternalSource && !string.IsNullOrEmpty(v.AdsSymbol)).ToList();

                // 1) Read ADS values (await OUTSIDE the buffer lock)
                var adsValues = new Dictionary<string, object?>();
                foreach (var v in serverVars)
                {
                    var access = v.AccessMode.ToUpperInvariant();
                    if (access == "W" || access == "WO") continue; // write-only: client → ADS only
                    try
                    {
                        var clr = MapDataTypeToClr(v.DataType);
                        adsValues[v.Name] = await _twinCAT.ReadVariableAsync(v.AdsSymbol, clr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("📡 ADS read failed for {Sym}: {Msg}", v.AdsSymbol, ex.Message);
                    }
                }

                // 2) Read alarm states (shared cache — push-based, 0 extra ADS reads)
                var alarmStates = _alarmNotificationService.GetCurrentAlarmStates();

                // One-time diagnostic: warn about Modbus alarms whose PLC index is not monitored
                // by the central Alarms sheet (otherwise they always report OK silently).
                ValidateAlarmMonitoringOnce();

                // 3) Apply to buffers + detect client writes (inside lock, NO await)
                var writeBacks = new List<(ModbusVariable v, double raw)>();
                lock (_server.Lock)
                {
                    foreach (var v in serverVars)
                    {
                        var access = v.AccessMode.ToUpperInvariant();
                        bool writable = access == "RW" || access == "W" || access == "WO";

                        if (writable && _lastServerPushed.TryGetValue(v.Name, out var pushed))
                        {
                            // Detect a client write: current buffer differs from what we last pushed
                            double current = ReadRawFromBuffer(v);
                            if (Math.Abs(current - pushed) > 0.0000001)
                            {
                                writeBacks.Add((v, current));
                                continue; // do not overwrite the client's value this cycle
                            }
                        }

                        if (access == "W" || access == "WO") continue; // write-only: nothing to expose

                        if (adsValues.TryGetValue(v.Name, out var value) && value != null)
                        {
                            double raw = WriteValueToBuffer(v, value);
                            _lastServerPushed[v.Name] = raw;
                            TrackValueChange(v, value);
                        }
                    }

                    // Alarms → coils/discrete (model A: 1 bit per alarm) OR
                    //          holding/input register (model B: bit packed in word).
                    foreach (var a in _alarms)
                    {
                        bool isActive = ResolveAlarmState(alarmStates, a);
                        if (a.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
                        {
                            // Model A — each alarm is its own coil/discrete-input bit.
                            WriteBoolToBuffer(a.RegisterType, a.Address, isActive);
                        }
                        else
                        {
                            // Model B — set a single bit inside the 16-bit register.
                            int bit = a.Bit >= 0 ? a.Bit : (a.AlarmIndex % 16);
                            WriteAlarmBitToRegister(a.RegisterType, a.Address, bit, isActive);
                        }
                        TrackAlarmChange(a, isActive);
                    }
                }

                // 4) Propagate client write-backs to ADS (await, outside lock)
                foreach (var (v, raw) in writeBacks)
                {
                    await PropagateWriteToAdsAsync(v, raw);
                }
            }

            // ---- CLIENT: poll external sources ----
            foreach (var source in _config.Sources)
            {
                await PollSourceAsync(source);
            }
        }

        private bool ResolveAlarmState(IReadOnlyDictionary<string, bool> alarmStates, ModbusAlarm a)
        {
            string suffix = a.Severity switch { 0 => "Alarm", 1 => "Notification", 2 => "Info", _ => "Alarm" };
            foreach (var kvp in alarmStates)
            {
                // Match the canonical PLC array index (name-derived), exactly like OPC-UA.
                if (kvp.Key.Contains($"st_alarmPc[{a.PlcAlarmIndex}].{suffix}"))
                    return kvp.Value;
            }
            return false;
        }

        /// <summary>Extract alarm index from name like "TLS_M3_MAL_Alarm_042" → 42 (mirror of OPC-UA).</summary>
        private static int ExtractAlarmIndex(string alarmName, int fallback)
        {
            if (string.IsNullOrEmpty(alarmName)) return fallback;
            var lastUnderscore = alarmName.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(alarmName.Substring(lastUnderscore + 1), out var idx))
                return idx;
            return fallback;
        }

        /// <summary>
        /// One-time check (after the central alarm subscription is ready): warns about any
        /// Modbus alarm whose <c>st_alarmPc[PlcAlarmIndex].{suffix}</c> is NOT declared/monitored
        /// in the central Alarms sheet — such alarms would silently always report OK.
        /// </summary>
        private void ValidateAlarmMonitoringOnce()
        {
            if (_alarmMonitoringChecked) return;
            if (_alarms.Count == 0) { _alarmMonitoringChecked = true; return; }
            // Wait until the central service has loaded its declared keys (avoids a false alarm at boot).
            if (_alarmNotificationService.DeclaredAlarmKeyCount == 0) return;

            _alarmMonitoringChecked = true;
            foreach (var a in _alarms)
            {
                string suffix = a.Severity switch { 0 => "Alarm", 1 => "Notification", 2 => "Info", _ => "Alarm" };
                if (_alarmNotificationService.IsAlarmDeclared(a.PlcAlarmIndex, suffix)) continue;

                _logger.LogWarning(
                    "📡 Modbus alarm '{Name}' → st_alarmPc[{Idx}].{Suffix} NO está monitorizado en la hoja Alarms; siempre se reportará OK.",
                    a.AlarmName, a.PlcAlarmIndex, suffix);
                _ = _auditLogService.LogAsync(
                    Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusConfigWarning, Models.AuditResult.Warning,
                    $"Alarma Modbus '{a.AlarmName}' → st_alarmPc[{a.PlcAlarmIndex}].{suffix} no está monitorizada (hoja Alarms). Siempre se reportará OK.",
                    userName: "System");
            }
        }

        private void TrackValueChange(ModbusVariable v, object? value)
        {
            _currentValues[v.Name] = value;
            if (_previousValues.TryGetValue(v.Name, out var prev))
            {
                if (!Equals(prev, value))
                {
                    _previousValues[v.Name] = value;
                    RaiseChanged(v.Name, prev, value);
                    if (!v.ExcludeFromLog)
                    {
                        _ = _operationLogService.LogAsync(
                            OperationCategory.Modbus, OperationAction.ModbusValueChange,
                            $"{v.Name}: {prev} → {value}", user: "PLC");
                    }
                }
            }
            else
            {
                _previousValues[v.Name] = value;
            }
        }

        private void TrackAlarmChange(ModbusAlarm a, bool isActive)
        {
            _currentValues[$"alarm:{a.AlarmName}"] = isActive;
            if (_previousAlarmStates.TryGetValue(a.AlarmIndex, out var prev))
            {
                if (prev != isActive)
                {
                    _previousAlarmStates[a.AlarmIndex] = isActive;
                    _ = _operationLogService.LogAsync(
                        OperationCategory.Modbus, OperationAction.ModbusAlarmChange,
                        $"Alarm[{a.AlarmIndex}] {a.AlarmName}: {(isActive ? "ACTIVE" : "CLEARED")}", user: "PLC");
                }
            }
            else
            {
                _previousAlarmStates[a.AlarmIndex] = isActive;
            }
        }

        private async Task PropagateWriteToAdsAsync(ModbusVariable v, double raw)
        {
            try
            {
                double eng = raw * v.Scale + v.Offset;
                var clr = MapDataTypeToClr(v.DataType);
                object value = ConvertEngineeringToClr(eng, v.DataType);
                bool ok = await _twinCAT.WriteVariableAsync(v.AdsSymbol, value, clr);
                _lastServerPushed[v.Name] = raw;
                _currentValues[v.Name] = value;

                // L2 operation log + L1 audit (CRA traceability for inbound writes)
                _ = _operationLogService.LogAsync(
                    OperationCategory.Modbus, OperationAction.ModbusRegisterWrite,
                    $"{v.Name} ({v.RegisterType}[{v.Address}]) ← Modbus client: {value} (ADS write {(ok ? "OK" : "FAILED")})",
                    user: "ModbusClient");
                _ = _auditLogService.LogAsync(
                    Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusClientConnect,
                    ok ? Models.AuditResult.Success : Models.AuditResult.Warning,
                    $"Modbus client wrote {v.Name}={value} → ADS {v.AdsSymbol} ({(ok ? "OK" : "FAILED")})", userName: "ModbusClient");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("📡 Modbus write-back to ADS failed for {Name}: {Msg}", v.Name, ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CLIENT (Master) — external Modbus TCP sources
        // ═══════════════════════════════════════════════════════════════════════
        private void ConnectSources()
        {
            foreach (var source in _config.Sources)
            {
                _sourceStatus[source.Id] = new ModbusSourceStatus
                {
                    Id = source.Id, Host = source.Host, Port = source.Port, UnitId = source.UnitId, Connected = false
                };
                // Defer the first connection attempt to the poll loop (throttled).
                _nextSourceRetry[source.Id] = DateTime.MinValue;
            }
        }

        private void TryConnectSource(ModbusSource source)
        {
            try
            {
                var client = new ModbusTcpClient();
                client.Connect(new IPEndPoint(IPAddress.Parse(source.Host), source.Port), ModbusEndianness.BigEndian);
                _clients[source.Id] = client;
                if (_sourceStatus.TryGetValue(source.Id, out var st)) { st.Connected = true; st.LastError = ""; }
                _nextSourceRetry.TryRemove(source.Id, out _);
                _sourceDownLogged[source.Id] = false;
                _logger.LogInformation("📡 Modbus source {Id} connected ({Host}:{Port})", source.Id, source.Host, source.Port);
                _ = _auditLogService.LogAsync(
                    Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusSourceConnect, Models.AuditResult.Success,
                    $"Connected to external Modbus source {source.Id} {source.Host}:{source.Port}", userName: "System");
            }
            catch (Exception ex)
            {
                if (_sourceStatus.TryGetValue(source.Id, out var st)) { st.Connected = false; st.LastError = ex.Message; }
                // Backoff: don't retry (or log) every cycle while the source is unreachable.
                _nextSourceRetry[source.Id] = DateTime.UtcNow.AddSeconds(SourceRetryBackoffSec);
                if (!_sourceDownLogged.GetValueOrDefault(source.Id))
                {
                    _sourceDownLogged[source.Id] = true;
                    _logger.LogWarning("📡 Modbus source {Id} ({Host}:{Port}) unreachable — retrying every {Sec}s: {Msg}",
                        source.Id, source.Host, source.Port, SourceRetryBackoffSec, ex.Message);
                    _ = _auditLogService.LogAsync(
                        Models.AuditCategory.OtCommunication, Models.AuditAction.ModbusSourceDisconnect, Models.AuditResult.Warning,
                        $"External Modbus source {source.Id} {source.Host}:{source.Port} unreachable: {ex.Message}", userName: "System");
                }
                else
                {
                    _logger.LogDebug("📡 Modbus source {Id} still unreachable: {Msg}", source.Id, ex.Message);
                }
            }
        }

        private void DisconnectSources()
        {
            foreach (var kvp in _clients)
            {
                try { kvp.Value.Disconnect(); } catch { /* ignore */ }
            }
            _clients.Clear();
        }

        private async Task PollSourceAsync(ModbusSource source)
        {
            if (!_clients.TryGetValue(source.Id, out var client) || !client.IsConnected)
            {
                // Throttle reconnection: don't hammer (or block on) a dead source every cycle.
                if (_nextSourceRetry.TryGetValue(source.Id, out var next) && DateTime.UtcNow < next)
                    return;
                TryConnectSource(source);
                if (!_clients.TryGetValue(source.Id, out client) || client == null || !client.IsConnected)
                    return;
            }

            var sourceVars = _variables.Where(v => string.Equals(v.Source, source.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var v in sourceVars)
            {
                try
                {
                    var raw = ReadRawFromClient(client, source.UnitId, v);
                    if (raw == null) continue;
                    double eng = Convert.ToDouble(raw) * v.Scale + v.Offset;
                    object value = ConvertEngineeringToClr(eng, v.DataType);
                    if (_sourceStatus.TryGetValue(source.Id, out var st)) { st.Connected = true; st.LastReadAt = DateTime.UtcNow; st.LastError = ""; }
                    TrackValueChange(v, value);
                    if (!v.ExcludeFromLog && _previousValues.TryGetValue(v.Name, out var pv) && !Equals(pv, value))
                    {
                        _ = _operationLogService.LogAsync(
                            OperationCategory.Modbus, OperationAction.ModbusSourceRead,
                            $"{source.Id}:{v.Name} = {value}", user: "ModbusSource");
                    }
                }
                catch (Exception ex)
                {
                    if (_sourceStatus.TryGetValue(source.Id, out var st)) { st.Connected = false; st.LastError = ex.Message; }
                    _logger.LogDebug("📡 Read failed {Id}:{Name}: {Msg}", source.Id, v.Name, ex.Message);
                    try { client.Disconnect(); } catch { }
                    _clients.TryRemove(source.Id, out _);
                    _nextSourceRetry[source.Id] = DateTime.UtcNow.AddSeconds(SourceRetryBackoffSec);
                    break;
                }
            }
            await Task.CompletedTask;
        }

        private double? ReadRawFromClient(ModbusTcpClient client, byte unitId, ModbusVariable v)
        {
            switch (v.RegisterType)
            {
                case ModbusRegisterType.Coil:
                {
                    var data = client.ReadCoils(unitId, v.Address, 1).ToArray();
                    return data.Length > 0 ? (data[0] & 0x01) : 0;
                }
                case ModbusRegisterType.DiscreteInput:
                {
                    var data = client.ReadDiscreteInputs(unitId, v.Address, 1).ToArray();
                    return data.Length > 0 ? (data[0] & 0x01) : 0;
                }
                case ModbusRegisterType.InputRegister:
                {
                    var regs = client.ReadInputRegisters<short>(unitId, v.Address, v.RegisterCount).ToArray();
                    return RegistersToDouble(regs, v);
                }
                default: // HoldingRegister
                {
                    var regs = client.ReadHoldingRegisters<short>(unitId, v.Address, v.RegisterCount).ToArray();
                    return RegistersToDouble(regs, v);
                }
            }
        }

        private static double RegistersToDouble(short[] regs, ModbusVariable v)
        {
            if (regs.Length == 0) return 0;
            bool little = v.WordOrder is "CDAB" or "DCBA";
            switch (v.DataType)
            {
                case "FLOAT32" or "REAL":
                {
                    uint u = WordsToUInt32(regs, little);
                    return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
                }
                case "INT32" or "DINT":
                    return unchecked((int)WordsToUInt32(regs, little));
                case "UINT32" or "UDINT":
                    return WordsToUInt32(regs, little);
                case "UINT16" or "WORD":
                    return (ushort)regs[0];
                default: // INT16/BOOL
                    return regs[0];
            }
        }

        private static uint WordsToUInt32(short[] regs, bool little)
        {
            ushort hi = (ushort)regs[0];
            ushort lo = regs.Length > 1 ? (ushort)regs[1] : (ushort)0;
            return little ? ((uint)lo << 16) | hi : ((uint)hi << 16) | lo;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BUFFER HELPERS (server)
        // ═══════════════════════════════════════════════════════════════════════
        /// <summary>Write an engineering value to the server buffer. Returns the raw value stored.</summary>
        private double WriteValueToBuffer(ModbusVariable v, object value)
        {
            if (v.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
            {
                bool b = ToBool(value);
                WriteBoolToBuffer(v.RegisterType, v.Address, b);
                return b ? 1 : 0;
            }

            double eng = ToDouble(value);
            double raw = v.Scale != 0 ? (eng - v.Offset) / v.Scale : eng;
            var registers = v.RegisterType == ModbusRegisterType.InputRegister
                ? _server!.GetInputRegisters(_config.ServerUnitId)
                : _server!.GetHoldingRegisters(_config.ServerUnitId);

            int addr = v.Address + _config.ServerAddressOffset;
            bool little = v.WordOrder is "CDAB" or "DCBA";
            switch (v.DataType)
            {
                case "FLOAT32" or "REAL":
                    if (little) registers.SetLittleEndian<float>(addr, (float)(eng));
                    else registers.SetBigEndian<float>(addr, (float)(eng));
                    break;
                case "INT32" or "DINT":
                    if (little) registers.SetLittleEndian<int>(addr, (int)Math.Round(raw));
                    else registers.SetBigEndian<int>(addr, (int)Math.Round(raw));
                    break;
                case "UINT32" or "UDINT":
                    if (little) registers.SetLittleEndian<uint>(addr, (uint)Math.Max(0, Math.Round(raw)));
                    else registers.SetBigEndian<uint>(addr, (uint)Math.Max(0, Math.Round(raw)));
                    break;
                default: // INT16/UINT16
                    // Modbus wire order is big-endian; the raw indexer would write native
                    // (little-endian) bytes and appear byte-swapped to the master.
                    if (little) registers.SetLittleEndian<short>(addr, (short)(int)Math.Round(raw));
                    else registers.SetBigEndian<short>(addr, (short)(int)Math.Round(raw));
                    break;
            }
            return raw;
        }

        private double ReadRawFromBuffer(ModbusVariable v)
        {
            if (v.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
                return ReadBoolFromBuffer(v.RegisterType, v.Address) ? 1 : 0;

            var registers = v.RegisterType == ModbusRegisterType.InputRegister
                ? _server!.GetInputRegisters(_config.ServerUnitId)
                : _server!.GetHoldingRegisters(_config.ServerUnitId);
            int addr = v.Address + _config.ServerAddressOffset;
            bool little = v.WordOrder is "CDAB" or "DCBA";
            switch (v.DataType)
            {
                case "FLOAT32" or "REAL":
                    return little ? registers.GetLittleEndian<float>(addr) : registers.GetBigEndian<float>(addr);
                case "INT32" or "DINT":
                    return little ? registers.GetLittleEndian<int>(addr) : registers.GetBigEndian<int>(addr);
                case "UINT32" or "UDINT":
                    return little ? registers.GetLittleEndian<uint>(addr) : registers.GetBigEndian<uint>(addr);
                case "UINT16" or "WORD":
                    return (ushort)(little ? registers.GetLittleEndian<short>(addr) : registers.GetBigEndian<short>(addr));
                default:
                    return little ? registers.GetLittleEndian<short>(addr) : registers.GetBigEndian<short>(addr);
            }
        }

        private void WriteBoolToBuffer(ModbusRegisterType type, int address, bool value)
        {
            var buffer = type == ModbusRegisterType.DiscreteInput ? _server!.GetDiscreteInputs(_config.ServerUnitId) : _server!.GetCoils(_config.ServerUnitId);
            address += _config.ServerAddressOffset;
            int byteIndex = address / 8;
            int bitPos = address % 8;
            if (byteIndex < 0 || byteIndex >= buffer.Length) return;
            if (value) buffer[byteIndex] |= (byte)(1 << bitPos);
            else buffer[byteIndex] &= (byte)~(1 << bitPos);
        }

        /// <summary>
        /// Model B — set/clear a single bit inside a 16-bit Holding/Input register
        /// (read-modify-write, big-endian on the wire). Several alarms can share the
        /// same register address, each owning a different bit.
        /// </summary>
        private void WriteAlarmBitToRegister(ModbusRegisterType type, int address, int bit, bool value)
        {
            if (bit < 0 || bit > 15) return;
            var registers = type == ModbusRegisterType.InputRegister
                ? _server!.GetInputRegisters(_config.ServerUnitId)
                : _server!.GetHoldingRegisters(_config.ServerUnitId);
            address += _config.ServerAddressOffset;
            ushort cur = (ushort)registers.GetBigEndian<short>(address);
            if (value) cur |= (ushort)(1 << bit);
            else cur &= (ushort)~(1 << bit);
            registers.SetBigEndian<short>(address, (short)cur);
        }

        private bool ReadBoolFromBuffer(ModbusRegisterType type, int address)
        {
            var buffer = type == ModbusRegisterType.DiscreteInput ? _server!.GetDiscreteInputs(_config.ServerUnitId) : _server!.GetCoils(_config.ServerUnitId);
            address += _config.ServerAddressOffset;
            int byteIndex = address / 8;
            int bitPos = address % 8;
            if (byteIndex < 0 || byteIndex >= buffer.Length) return false;
            return (buffer[byteIndex] & (1 << bitPos)) != 0;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC Client helpers (on-demand read/write for the controller)
        // ═══════════════════════════════════════════════════════════════════════
        public Task<object?> ReadAsync(string sourceId, ModbusVariable variable)
        {
            if (!_clients.TryGetValue(sourceId, out var client) || !client.IsConnected)
                return Task.FromResult<object?>(null);
            var src = _config.Sources.FirstOrDefault(s => s.Id == sourceId);
            byte unit = src?.UnitId ?? 1;
            try
            {
                var raw = ReadRawFromClient(client, unit, variable);
                if (raw == null) return Task.FromResult<object?>(null);
                double eng = Convert.ToDouble(raw) * variable.Scale + variable.Offset;
                return Task.FromResult<object?>(ConvertEngineeringToClr(eng, variable.DataType));
            }
            catch { return Task.FromResult<object?>(null); }
        }

        public async Task<bool> WriteAsync(string sourceId, ModbusVariable variable, object value)
        {
            if (!_clients.TryGetValue(sourceId, out var client) || !client.IsConnected) return false;
            var src = _config.Sources.FirstOrDefault(s => s.Id == sourceId);
            byte unit = src?.UnitId ?? 1;
            try
            {
                if (variable.RegisterType == ModbusRegisterType.Coil)
                {
                    client.WriteSingleCoil(unit, variable.Address, ToBool(value));
                }
                else
                {
                    double eng = ToDouble(value);
                    double raw = variable.Scale != 0 ? (eng - variable.Offset) / variable.Scale : eng;
                    client.WriteSingleRegister(unit, variable.Address, (short)(int)Math.Round(raw));
                }
                _ = _operationLogService.LogAsync(
                    OperationCategory.Modbus, OperationAction.ModbusSourceWrite,
                    $"{sourceId}:{variable.Name} ← {value}", user: "ModbusClient");
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("📡 Modbus source write failed {Id}:{Name}: {Msg}", sourceId, variable.Name, ex.Message);
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONVERSIONS
        // ═══════════════════════════════════════════════════════════════════════
        private void RaiseChanged(string name, object? oldValue, object? newValue)
        {
            try
            {
                OnVariableChanged?.Invoke(this, new PlcNotification
                {
                    VariableName = name,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch { /* never let subscribers break the loop */ }
        }

        private static Type MapDataTypeToClr(string dataType) => dataType.ToUpperInvariant() switch
        {
            "BOOL" => typeof(bool),
            "INT16" or "INT" => typeof(short),
            "UINT16" or "WORD" or "UINT" => typeof(ushort),
            "INT32" or "DINT" => typeof(int),
            "UINT32" or "UDINT" => typeof(uint),
            "FLOAT32" or "REAL" => typeof(float),
            "STRING" => typeof(string),
            _ => typeof(short)
        };

        private static object ConvertEngineeringToClr(double eng, string dataType) => dataType.ToUpperInvariant() switch
        {
            "BOOL" => eng != 0,
            "INT16" or "INT" => (short)Math.Round(eng),
            "UINT16" or "WORD" or "UINT" => (ushort)Math.Max(0, Math.Round(eng)),
            "INT32" or "DINT" => (int)Math.Round(eng),
            "UINT32" or "UDINT" => (uint)Math.Max(0, Math.Round(eng)),
            "FLOAT32" or "REAL" => (float)eng,
            _ => (short)Math.Round(eng)
        };

        private static bool ToBool(object value)
        {
            if (value is bool b) return b;
            try { return Convert.ToDouble(value) != 0; } catch { return false; }
        }

        private static double ToDouble(object value)
        {
            if (value is bool b) return b ? 1 : 0;
            try { return Convert.ToDouble(value); } catch { return 0; }
        }
    }
}
