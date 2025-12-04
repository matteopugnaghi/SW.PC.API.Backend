using SW.PC.API.Backend.Models.Excel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.NetworkInformation;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🔐 Servicio de verificación de integridad del software basado en Git
    /// Para cumplimiento de normativas de ciberseguridad NASA/NIST y EU CRA
    /// </summary>
    public interface ISoftwareIntegrityService
    {
        /// <summary>
        /// Obtener información completa de versiones Git y estado de integridad
        /// </summary>
        SoftwareVersionInfo GetSoftwareVersionInfo();

        /// <summary>
        /// Verificar integridad de todos los componentes (working dir clean)
        /// </summary>
        Task<bool> VerifyAllIntegrityAsync();

        /// <summary>
        /// Registrar verificación por administrador
        /// </summary>
        void RegisterAdminVerification(string adminUser);

        /// <summary>
        /// Actualizar información de TwinCAT Runtime
        /// </summary>
        void UpdateTwinCATRuntimeInfo(string version, string adsVersion, bool isConnected, bool isSimulated);

        /// <summary>
        /// Configurar rutas de repositorios Git desde Excel
        /// </summary>
        void ConfigureGitPaths(string backendPath, string frontendPath, string twinCatPlcPath);

        /// <summary>
        /// Actualizar estado de Database desde configuración Excel
        /// </summary>
        void UpdateDatabaseStatus(bool enabled, bool connected, string details);

        /// <summary>
        /// Actualizar información de programación de verificación automática
        /// </summary>
        void UpdateVerificationSchedule(DateTime nextVerification, int intervalSeconds);

        /// <summary>
        /// Verificar conectividad a internet y estado de sincronización con remoto
        /// </summary>
        Task<NetworkSyncStatus> CheckNetworkAndSyncStatusAsync();

        /// <summary>
        /// Generar certificado de integridad firmado digitalmente
        /// </summary>
        Task<IntegrityCertificate> GenerateIntegrityCertificateAsync(string machineId, string operatorName);

        /// <summary>
        /// Verificar un certificado de integridad
        /// </summary>
        bool VerifyCertificateSignature(IntegrityCertificate certificate);

        /// <summary>
        /// Obtener rutas de repositorios Git configuradas (desde Excel)
        /// </summary>
        (string Backend, string Frontend, string TwinCAT) GetRepositoryPaths();
    }

    public class SoftwareIntegrityService : ISoftwareIntegrityService
    {
        private readonly ILogger<SoftwareIntegrityService> _logger;
        private readonly IConfiguration _configuration;
        private readonly object _lock = new object();

        // Información de versiones
        private SoftwareVersionInfo _versionInfo;
        
        // 🔐 Archivo de persistencia para el estado de integridad
        private readonly string _stateFilePath;

        // Configuración de repositorios (modificables desde Excel)
        private string _backendRepoPath;
        private string _frontendRepoPath;
        private string _twinCatPlcRepoPath;
        private bool _pathsConfigured = false;

        public SoftwareIntegrityService(
            ILogger<SoftwareIntegrityService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Rutas por defecto (auto-detectadas) - se pueden sobrescribir desde Excel
            _backendRepoPath = FindGitRoot(AppDomain.CurrentDomain.BaseDirectory) 
                ?? AppDomain.CurrentDomain.BaseDirectory;

            _frontendRepoPath = Path.GetFullPath(Path.Combine(_backendRepoPath, "..", "SW.PC.REACT.Frontend", "my-3d-app"));

            _twinCatPlcRepoPath = Path.GetFullPath(Path.Combine(_backendRepoPath, "..", "SW.PC.TwinCAT.PLC"));

            // 🔐 Ruta del archivo de persistencia de estado
            _stateFilePath = Path.Combine(_backendRepoPath, "integrity-state.json");
            
            // Cargar estado guardado o crear nuevo
            _versionInfo = LoadPersistedState() ?? new SoftwareVersionInfo();
            
            // Inicializar información Git de forma asíncrona con rutas por defecto
            _ = InitializeGitInfoAsync();

            _logger.LogInformation("🔐 SoftwareIntegrityService initialized (Git-based)");
        }

        /// <summary>
        /// Configurar rutas de repositorios Git desde Excel (hoja System Config)
        /// </summary>
        /// <summary>
        /// Obtener rutas de repositorios Git configuradas
        /// </summary>
        public (string Backend, string Frontend, string TwinCAT) GetRepositoryPaths()
        {
            return (_backendRepoPath, _frontendRepoPath, _twinCatPlcRepoPath);
        }

        public void ConfigureGitPaths(string backendPath, string frontendPath, string twinCatPlcPath)
        {
            var updated = false;

            if (!string.IsNullOrWhiteSpace(backendPath) && Directory.Exists(backendPath))
            {
                _backendRepoPath = backendPath;
                updated = true;
                _logger.LogInformation("🔐 Git Backend path from Excel: {Path}", backendPath);
            }

            if (!string.IsNullOrWhiteSpace(frontendPath) && Directory.Exists(frontendPath))
            {
                _frontendRepoPath = frontendPath;
                updated = true;
                _logger.LogInformation("🔐 Git Frontend path from Excel: {Path}", frontendPath);
            }

            if (!string.IsNullOrWhiteSpace(twinCatPlcPath) && Directory.Exists(twinCatPlcPath))
            {
                _twinCatPlcRepoPath = twinCatPlcPath;
                updated = true;
                _logger.LogInformation("🔐 Git TwinCAT PLC path from Excel: {Path}", twinCatPlcPath);
            }

            if (updated)
            {
                _pathsConfigured = true;
                // Re-inicializar con las nuevas rutas
                _ = InitializeGitInfoAsync();
            }
        }

        private async Task InitializeGitInfoAsync()
        {
            try
            {
                // Obtener info de cada componente en paralelo
                var backendTask = GetGitComponentInfoAsync("Backend", _backendRepoPath);
                var frontendTask = GetGitComponentInfoAsync("Frontend", _frontendRepoPath);
                var plcTask = GetGitComponentInfoAsync("TwinCAT PLC", _twinCatPlcRepoPath);

                await Task.WhenAll(backendTask, frontendTask, plcTask);

                lock (_lock)
                {
                    _versionInfo.Backend = backendTask.Result;
                    _versionInfo.Frontend = frontendTask.Result;
                    _versionInfo.TwinCatPlc = plcTask.Result;

                    // Inicializar info de runtime con valores por defecto
                    _versionInfo.TwinCatRuntime = new RuntimeVersionInfo
                    {
                        Name = "TwinCAT Runtime",
                        Version = "Pending connection",
                        Status = "unknown"
                    };

                    _versionInfo.AdsClient = new RuntimeVersionInfo
                    {
                        Name = "TwinCAT ADS Client",
                        Version = typeof(TwinCAT.Ads.AdsClient).Assembly.GetName().Version?.ToString() ?? "Unknown",
                        Status = "loaded"
                    };

                    _versionInfo.Database = new RuntimeVersionInfo
                    {
                        Name = "Database",
                        Version = "SQL Server",
                        Status = "disabled", // Por defecto deshabilitado, se actualiza desde Excel
                        Details = "Pending configuration"
                    };

                    // Calcular estado general del sistema
                    UpdateSystemStatus();
                }

                _logger.LogInformation("✅ Git version info initialized for all components");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Git info");
            }
        }

        #region 🔐 PERSISTENCIA DE ESTADO

        /// <summary>
        /// Cargar estado guardado desde archivo JSON
        /// </summary>
        private SoftwareVersionInfo? LoadPersistedState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var state = JsonSerializer.Deserialize<SoftwareVersionInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (state != null)
                    {
                        _logger.LogInformation("✅ Integrity state loaded from {Path}", _stateFilePath);
                        _logger.LogInformation("   📅 Last verification: {Date}", state.LastVerificationDate ?? "Never");
                        _logger.LogInformation("   🌐 Last network check: {Date}", state.NetworkStatus?.CheckedAt ?? "Never");
                        
                        // 🔐 NO cargar NetworkStatus - el estado de red puede haber cambiado
                        // Se verificará manualmente o cuando el usuario presione el botón
                        // Pero SÍ mantenemos la fecha del último chequeo como referencia
                        if (state.NetworkStatus != null)
                        {
                            // Marcar como "needs refresh" - mantener última fecha pero status desconocido
                            state.NetworkStatus.HasInternetConnection = null; // null = desconocido
                            state.NetworkStatus.OverallSyncStatus = "unknown";
                            _logger.LogInformation("   ⚠️ Network status marked as 'unknown' - needs refresh");
                        }
                        
                        return state;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not load persisted state from {Path}", _stateFilePath);
            }
            
            return null;
        }

        /// <summary>
        /// Guardar estado actual a archivo JSON
        /// </summary>
        private void SavePersistedState()
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(_versionInfo, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    File.WriteAllText(_stateFilePath, json);
                }
                
                _logger.LogDebug("💾 Integrity state saved to {Path}", _stateFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Could not save integrity state to {Path}", _stateFilePath);
            }
        }

        #endregion

        private string GetDatabaseVersion()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return "Disabled";
            return "SQL Server"; // En producción, consultar al servidor
        }

        private async Task<GitVersionComponent> GetGitComponentInfoAsync(string name, string repoPath)
        {
            var component = new GitVersionComponent
            {
                Name = name,
                RepoPath = repoPath
            };

            if (!Directory.Exists(repoPath))
            {
                _logger.LogWarning("⚠️ Repository path not found: {Path}", repoPath);
                component.Integrity = "unknown";
                return component;
            }

            try
            {
                // Verificar si es un repositorio Git
                var gitDir = Path.Combine(repoPath, ".git");
                if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
                {
                    // Buscar en directorios padre
                    var parentGitRoot = FindGitRoot(repoPath);
                    if (parentGitRoot == null)
                    {
                        _logger.LogWarning("⚠️ Not a Git repository: {Path}", repoPath);
                        component.Integrity = "unknown";
                        return component;
                    }
                    repoPath = parentGitRoot;
                    component.RepoPath = repoPath;
                }

                // Obtener información Git en paralelo
                var shaTask = RunGitCommandAsync(repoPath, "rev-parse HEAD");
                var shaShortTask = RunGitCommandAsync(repoPath, "rev-parse --short HEAD");
                var branchTask = RunGitCommandAsync(repoPath, "rev-parse --abbrev-ref HEAD");
                var describeTask = RunGitCommandAsync(repoPath, "describe --tags --always");
                var statusTask = RunGitCommandAsync(repoPath, "status --porcelain");
                var dateTask = RunGitCommandAsync(repoPath, "log -1 --format=%ci");
                var authorTask = RunGitCommandAsync(repoPath, "log -1 --format=%an");
                var authorEmailTask = RunGitCommandAsync(repoPath, "log -1 --format=%ae");
                var messageTask = RunGitCommandAsync(repoPath, "log -1 --format=%s");
                // Verificación de firma GPG/SSH - usar formato separado para evitar problemas
                var signatureCodeTask = RunGitCommandAsync(repoPath, "log -1 --format=%G?");
                var signatureSignerTask = RunGitCommandAsync(repoPath, "log -1 --format=%GS");
                var signatureKeyTask = RunGitCommandAsync(repoPath, "log -1 --format=%GK");
                // Obtener último tag CalVer con fecha
                var latestTagTask = RunGitCommandAsync(repoPath, "tag --sort=-version:refname --format=%(refname:short)|%(creatordate:short) -l \"20*\"");

                await Task.WhenAll(shaTask, shaShortTask, branchTask, describeTask, statusTask, dateTask, authorTask, authorEmailTask, messageTask, signatureCodeTask, signatureSignerTask, signatureKeyTask, latestTagTask);

                component.CommitShaFull = shaTask.Result.Trim();
                component.CommitSha = shaShortTask.Result.Trim();
                component.Branch = branchTask.Result.Trim();
                component.Version = ParseVersion(describeTask.Result.Trim());
                component.CommitDate = dateTask.Result.Trim();
                component.CommitAuthor = authorTask.Result.Trim();
                component.CommitAuthorEmail = authorEmailTask.Result.Trim();
                component.CommitMessage = messageTask.Result.Trim();
                component.LastVerified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Parsear información de firma (comandos separados para evitar problemas de formato)
                var sigCode = signatureCodeTask.Result.Trim();
                var sigSigner = signatureSignerTask.Result.Trim();
                var sigKey = signatureKeyTask.Result.Trim();
                _logger.LogInformation("🔐 {Name} Signature raw: Code=[{Code}] Signer=[{Signer}] Key=[{Key}]", component.Name, sigCode, sigSigner, sigKey);
                var signatureOutput = $"{sigCode} {sigSigner} {sigKey}".Trim();
                ParseSignatureInfo(component, signatureOutput);

                // Parsear último release CalVer
                var tagOutput = latestTagTask.Result.Trim();
                if (!string.IsNullOrEmpty(tagOutput))
                {
                    var firstLine = tagOutput.Split('\n').FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(firstLine) && firstLine.Contains("|"))
                    {
                        var parts = firstLine.Split('|');
                        component.LatestRelease = parts[0].Trim();
                        component.LatestReleaseDate = parts.Length > 1 ? parts[1].Trim() : "";
                    }
                    else if (!string.IsNullOrEmpty(firstLine))
                    {
                        component.LatestRelease = firstLine;
                    }
                }

                // Analizar estado del working directory
                var statusOutput = statusTask.Result.Trim();
                if (string.IsNullOrEmpty(statusOutput))
                {
                    component.WorkingDirStatus = "clean";
                    component.ModifiedFiles = 0;
                    component.Integrity = "verified";
                }
                else
                {
                    var modifiedLines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    component.WorkingDirStatus = "dirty";
                    component.ModifiedFiles = modifiedLines.Length;
                    component.Integrity = "modified";
                    
                    _logger.LogWarning("⚠️ {Name} has {Count} uncommitted changes", name, modifiedLines.Length);
                }

                _logger.LogInformation("📦 {Name}: {Version} ({Sha}) [{Status}]", 
                    name, component.Version, component.CommitSha, component.WorkingDirStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Git info for {Name}", name);
                component.Integrity = "unknown";
            }

            return component;
        }

        private string ParseVersion(string gitDescribe)
        {
            if (string.IsNullOrEmpty(gitDescribe))
                return "0.0.0";

            // Si es un tag semántico (v1.2.3 o 1.2.3)
            if (gitDescribe.StartsWith("v"))
                return gitDescribe;

            // Si incluye commits después del tag (v1.2.3-5-gabc1234)
            if (gitDescribe.Contains("-"))
            {
                var parts = gitDescribe.Split('-');
                if (parts[0].StartsWith("v") || char.IsDigit(parts[0][0]))
                    return parts[0];
            }

            // Solo commit hash
            return $"dev-{gitDescribe}";
        }

        /// <summary>
        /// Parsea la información de firma GPG/SSH del commit
        /// Formato git: %G? = status, %GS = signer, %GK = key ID
        /// %G? valores: G=good, B=bad, U=unknown, X=expired, Y=expired key, R=revoked, E=error, N=no signature
        /// </summary>
        private void ParseSignatureInfo(GitVersionComponent component, string signatureOutput)
        {
            _logger.LogInformation("🔍 ParseSignatureInfo input: [{Output}]", signatureOutput);
            
            if (string.IsNullOrWhiteSpace(signatureOutput))
            {
                component.SignatureStatus = "unsigned";
                component.IsSigned = false;
                _logger.LogWarning("🔓 {Name}: No signature output - marking as unsigned", component.Name);
                return;
            }

            var parts = signatureOutput.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var signatureCode = parts.Length > 0 ? parts[0].Trim() : "N";
            var signer = parts.Length > 1 ? parts[1].Trim() : "";
            var keyId = parts.Length > 2 ? parts[2].Trim() : "";
            
            _logger.LogInformation("🔍 Parsed: Code=[{Code}], Signer=[{Signer}], KeyId=[{KeyId}]", signatureCode, signer, keyId);

            component.SignatureKeyId = keyId;
            component.SignatureSigner = signer;

            // Interpretar código de firma
            switch (signatureCode.ToUpper())
            {
                case "G": // Good signature
                    component.IsSigned = true;
                    component.SignatureStatus = "valid";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"✅ Valid signature by {signer}";
                    _logger.LogInformation("🔐 {Name}: Commit signed and verified by {Signer}", component.Name, signer);
                    break;

                case "B": // Bad signature
                    component.IsSigned = true;
                    component.SignatureStatus = "invalid";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"❌ Invalid/tampered signature";
                    _logger.LogWarning("⚠️ {Name}: BAD SIGNATURE - commit may be tampered!", component.Name);
                    break;

                case "U": // Unknown key (signature exists but key not trusted)
                    component.IsSigned = true;
                    component.SignatureStatus = "untrusted";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"⚠️ Signed but key not trusted ({keyId})";
                    _logger.LogWarning("⚠️ {Name}: Signed with untrusted key {KeyId}", component.Name, keyId);
                    break;

                case "X": // Signature expired
                    component.IsSigned = true;
                    component.SignatureStatus = "expired";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"⚠️ Signature expired";
                    break;

                case "Y": // Key expired
                    component.IsSigned = true;
                    component.SignatureStatus = "key-expired";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"⚠️ Signing key expired";
                    break;

                case "R": // Key revoked
                    component.IsSigned = true;
                    component.SignatureStatus = "revoked";
                    component.SignatureType = "GPG";
                    component.SignatureMessage = $"❌ Signing key revoked!";
                    _logger.LogError("🚨 {Name}: Signed with REVOKED key!", component.Name);
                    break;

                case "E": // Error verifying
                    component.IsSigned = false;
                    component.SignatureStatus = "error";
                    component.SignatureMessage = $"Error verifying signature";
                    break;

                case "N": // No signature
                default:
                    component.IsSigned = false;
                    component.SignatureStatus = "unsigned";
                    component.SignatureType = "none";
                    component.SignatureMessage = "Commit not signed";
                    break;
            }
        }

        private async Task<string> RunGitCommandAsync(string workingDir, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = workingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                // Asegurar que Git tenga acceso a HOME para la configuración de firma SSH
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                process.StartInfo.EnvironmentVariables["HOME"] = userProfile;
                process.StartInfo.EnvironmentVariables["USERPROFILE"] = userProfile;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Git command failed: {Args}", arguments);
                return "";
            }
        }

        private string? FindGitRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private void UpdateSystemStatus()
        {
            var allClean = _versionInfo.Backend.WorkingDirStatus == "clean" &&
                          _versionInfo.Frontend.WorkingDirStatus == "clean" &&
                          _versionInfo.TwinCatPlc.WorkingDirStatus == "clean";

            var anyUnknown = _versionInfo.Backend.Integrity == "unknown" ||
                            _versionInfo.Frontend.Integrity == "unknown" ||
                            _versionInfo.TwinCatPlc.Integrity == "unknown";

            if (allClean && !anyUnknown)
                _versionInfo.SystemStatus = "clean";
            else if (anyUnknown)
                _versionInfo.SystemStatus = "unknown";
            else
                _versionInfo.SystemStatus = "modified";
        }

        public SoftwareVersionInfo GetSoftwareVersionInfo()
        {
            lock (_lock)
            {
                return _versionInfo;
            }
        }

        public void UpdateTwinCATRuntimeInfo(string version, string adsVersion, bool isConnected, bool isSimulated)
        {
            lock (_lock)
            {
                _versionInfo.TwinCatRuntime = new RuntimeVersionInfo
                {
                    Name = "TwinCAT Runtime",
                    Version = version,
                    Status = isSimulated ? "simulated" : (isConnected ? "connected" : "disconnected"),
                    Details = isSimulated ? "Running in simulation mode" : ""
                };

                _versionInfo.AdsClient = new RuntimeVersionInfo
                {
                    Name = "TwinCAT ADS Client",
                    Version = adsVersion,
                    Status = "loaded",
                    Details = ""
                };

                _logger.LogInformation("🔧 TwinCAT Runtime info updated: {Version} ({Status})", 
                    version, _versionInfo.TwinCatRuntime.Status);
            }
        }

        public async Task<bool> VerifyAllIntegrityAsync()
        {
            _logger.LogInformation("🔐 Starting full integrity verification...");

            await InitializeGitInfoAsync();

            bool allVerified;
            lock (_lock)
            {
                allVerified = _versionInfo.Backend.Integrity == "verified" &&
                                  _versionInfo.Frontend.Integrity == "verified";

                // 🔐 Actualizar fecha de última verificación
                _versionInfo.LastVerificationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                if (string.IsNullOrEmpty(_versionInfo.VerifiedByAdmin) || _versionInfo.VerifiedByAdmin == "Never")
                {
                    _versionInfo.VerifiedByAdmin = "System (Auto)";
                }

                _logger.LogInformation("🔐 Integrity verification complete. System status: {Status}", 
                    _versionInfo.SystemStatus);
            }
            
            // 💾 Persistir estado después de verificación
            SavePersistedState();
            return allVerified;
        }

        public void RegisterAdminVerification(string adminUser)
        {
            lock (_lock)
            {
                _logger.LogInformation("🔐 Admin verification registered by: {Admin}", adminUser);
                _versionInfo.LastVerificationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                _versionInfo.VerifiedByAdmin = adminUser + " (Manual)";
            }
            
            // 💾 Persistir estado después de verificación manual
            SavePersistedState();
        }

        public void UpdateDatabaseStatus(bool enabled, bool connected, string details)
        {
            lock (_lock)
            {
                string status;
                if (!enabled)
                    status = "disabled";
                else if (connected)
                    status = "connected";
                else
                    status = "disconnected";

                _versionInfo.Database = new RuntimeVersionInfo
                {
                    Name = "Database SQL",
                    Version = enabled ? "SQL Server" : "N/A",
                    Status = status,
                    Details = details
                };

                _logger.LogInformation("🔧 Database status updated: Enabled={Enabled}, Status={Status}", 
                    enabled, status);
            }
        }

        public void UpdateVerificationSchedule(DateTime nextVerification, int intervalSeconds)
        {
            lock (_lock)
            {
                _versionInfo.NextVerificationTime = nextVerification.ToString("yyyy-MM-ddTHH:mm:ssZ");
                _versionInfo.VerificationIntervalSeconds = intervalSeconds;
                _versionInfo.AutoVerificationEnabled = true;
                
                // Calcular segundos restantes
                var secondsUntil = (nextVerification - DateTime.UtcNow).TotalSeconds;
                _versionInfo.SecondsUntilNextVerification = Math.Max(0, (int)secondsUntil);
                
                _logger.LogDebug("🔐 Verification schedule updated: Next at {Next}, Interval: {Interval}s", 
                    _versionInfo.NextVerificationTime, intervalSeconds);
            }
        }

        /// <summary>
        /// Verificar conectividad a internet y estado de sincronización con remotos Git
        /// </summary>
        public async Task<NetworkSyncStatus> CheckNetworkAndSyncStatusAsync()
        {
            var status = new NetworkSyncStatus
            {
                CheckedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // 1. Verificar conectividad a internet
            status.HasInternetConnection = await CheckInternetConnectivityAsync();
            
            _logger.LogInformation("🌐 Internet connectivity: {Status}", 
                status.HasInternetConnection == true ? "Connected" : "Offline");

            // 2. Si hay internet, verificar estado de sincronización con remotos
            if (status.HasInternetConnection == true)
            {
                status.BackendSync = await GetRemoteSyncStatusAsync("Backend", _backendRepoPath);
                status.FrontendSync = await GetRemoteSyncStatusAsync("Frontend", _frontendRepoPath);
                status.TwinCatPlcSync = await GetRemoteSyncStatusAsync("TwinCAT PLC", _twinCatPlcRepoPath);

                // Calcular estado general
                var allSynced = status.BackendSync.Status == "synced" &&
                               status.FrontendSync.Status == "synced" &&
                               status.TwinCatPlcSync.Status == "synced";

                status.OverallSyncStatus = allSynced ? "synced" : "out-of-sync";
            }
            else
            {
                // Sin internet, marcar como desconocido
                status.BackendSync = new RemoteSyncInfo { Status = "offline", RemoteUrl = "N/A" };
                status.FrontendSync = new RemoteSyncInfo { Status = "offline", RemoteUrl = "N/A" };
                status.TwinCatPlcSync = new RemoteSyncInfo { Status = "offline", RemoteUrl = "N/A" };
                status.OverallSyncStatus = "offline";
            }

            // Actualizar en versionInfo
            lock (_lock)
            {
                _versionInfo.NetworkStatus = status;
            }
            
            // 💾 Persistir estado después de verificar red/sync
            SavePersistedState();

            return status;
        }

        private async Task<bool> CheckInternetConnectivityAsync()
        {
            var hostsToCheck = new[] { "github.com", "8.8.8.8", "1.1.1.1" };

            foreach (var host in hostsToCheck)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(host, 3000); // 3 segundos timeout
                    if (reply.Status == IPStatus.Success)
                    {
                        _logger.LogDebug("🌐 Ping to {Host}: {Status} ({Time}ms)", host, reply.Status, reply.RoundtripTime);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("🌐 Ping to {Host} failed: {Error}", host, ex.Message);
                }
            }

            return false;
        }

        private async Task<RemoteSyncInfo> GetRemoteSyncStatusAsync(string name, string repoPath)
        {
            var syncInfo = new RemoteSyncInfo { ComponentName = name };

            if (!Directory.Exists(repoPath))
            {
                syncInfo.Status = "no-repo";
                return syncInfo;
            }

            try
            {
                // Obtener URL del remoto
                var remoteUrl = await RunGitCommandAsync(repoPath, "remote get-url origin");
                syncInfo.RemoteUrl = remoteUrl.Trim();

                if (string.IsNullOrEmpty(syncInfo.RemoteUrl))
                {
                    syncInfo.Status = "no-remote";
                    syncInfo.Message = "No remote configured";
                    return syncInfo;
                }

                // Hacer fetch para actualizar referencias remotas
                _logger.LogDebug("🔄 Fetching remote for {Name}...", name);
                await RunGitCommandAsync(repoPath, "fetch --quiet");

                // Obtener commits ahead/behind
                var statusOutput = await RunGitCommandAsync(repoPath, "rev-list --left-right --count HEAD...@{upstream}");
                
                if (!string.IsNullOrWhiteSpace(statusOutput))
                {
                    var parts = statusOutput.Trim().Split('\t');
                    if (parts.Length >= 2)
                    {
                        syncInfo.CommitsAhead = int.TryParse(parts[0], out var ahead) ? ahead : 0;
                        syncInfo.CommitsBehind = int.TryParse(parts[1], out var behind) ? behind : 0;
                    }
                }

                // Determinar estado
                if (syncInfo.CommitsAhead == 0 && syncInfo.CommitsBehind == 0)
                {
                    syncInfo.Status = "synced";
                    syncInfo.Message = "✅ Synchronized with remote";
                }
                else if (syncInfo.CommitsAhead > 0 && syncInfo.CommitsBehind == 0)
                {
                    syncInfo.Status = "ahead";
                    syncInfo.Message = $"🟠 {syncInfo.CommitsAhead} commits pending push";
                }
                else if (syncInfo.CommitsAhead == 0 && syncInfo.CommitsBehind > 0)
                {
                    syncInfo.Status = "behind";
                    syncInfo.Message = $"🔴 {syncInfo.CommitsBehind} commits behind remote";
                }
                else
                {
                    syncInfo.Status = "diverged";
                    syncInfo.Message = $"⚠️ Diverged: {syncInfo.CommitsAhead} ahead, {syncInfo.CommitsBehind} behind";
                }

                _logger.LogInformation("🔄 {Name} sync status: {Status} (ahead: {Ahead}, behind: {Behind})",
                    name, syncInfo.Status, syncInfo.CommitsAhead, syncInfo.CommitsBehind);
            }
            catch (Exception ex)
            {
                syncInfo.Status = "error";
                syncInfo.Message = $"Error checking sync: {ex.Message}";
                _logger.LogWarning(ex, "Error checking sync status for {Name}", name);
            }

            return syncInfo;
        }

        /// <summary>
        /// Genera un certificado de integridad firmado digitalmente
        /// Para uso offline y auditorías EU CRA
        /// </summary>
        public async Task<IntegrityCertificate> GenerateIntegrityCertificateAsync(string machineId, string operatorName)
        {
            _logger.LogInformation("📜 Generating integrity certificate for machine: {MachineId}", machineId);

            // Asegurar que tenemos la última información
            await VerifyAllIntegrityAsync();

            var certificate = new IntegrityCertificate
            {
                CertificateId = Guid.NewGuid().ToString(),
                Version = "1.0",
                GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                MachineId = machineId,
                MachineName = Environment.MachineName,
                OperatorName = operatorName,
                OperatingSystem = Environment.OSVersion.ToString()
            };

            // Añadir información de componentes
            lock (_lock)
            {
                certificate.Components = new List<CertificateComponent>
                {
                    CreateCertificateComponent(_versionInfo.Backend),
                    CreateCertificateComponent(_versionInfo.Frontend),
                    CreateCertificateComponent(_versionInfo.TwinCatPlc)
                };

                // Añadir info de runtime
                certificate.RuntimeInfo = new CertificateRuntimeInfo
                {
                    TwinCatVersion = _versionInfo.TwinCatRuntime?.Version ?? "Unknown",
                    TwinCatStatus = _versionInfo.TwinCatRuntime?.Status ?? "unknown",
                    AdsClientVersion = _versionInfo.AdsClient?.Version ?? "Unknown",
                    DatabaseStatus = _versionInfo.Database?.Status ?? "unknown"
                };

                // Estado general
                certificate.OverallStatus = _versionInfo.SystemStatus == "clean" ? "VERIFIED" :
                                           _versionInfo.SystemStatus == "modified" ? "MODIFIED" : "UNKNOWN";
            }

            // Calcular hash del contenido (sin firma)
            certificate.ContentHash = CalculateCertificateContentHash(certificate);

            // Firmar el certificado
            certificate.Signature = SignCertificate(certificate);
            certificate.SignatureAlgorithm = "HMAC-SHA256";
            certificate.SignedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            _logger.LogInformation("📜 Certificate generated: {Id}, Status: {Status}", 
                certificate.CertificateId, certificate.OverallStatus);

            return certificate;
        }

        private CertificateComponent CreateCertificateComponent(GitVersionComponent git)
        {
            return new CertificateComponent
            {
                Name = git.Name,
                Version = git.Version,
                CommitSha = git.CommitShaFull,
                CommitShort = git.CommitSha,
                Branch = git.Branch,
                CommitDate = git.CommitDate,
                CommitAuthor = git.CommitAuthor,
                CommitAuthorEmail = git.CommitAuthorEmail,
                WorkingDirStatus = git.WorkingDirStatus,
                ModifiedFiles = git.ModifiedFiles,
                Integrity = git.Integrity,
                IsSigned = git.IsSigned,
                SignatureStatus = git.SignatureStatus,
                RepoPath = git.RepoPath
            };
        }

        private string CalculateCertificateContentHash(IntegrityCertificate cert)
        {
            // Crear un string con los datos importantes (sin firma)
            var contentBuilder = new StringBuilder();
            contentBuilder.Append(cert.CertificateId);
            contentBuilder.Append(cert.GeneratedAt);
            contentBuilder.Append(cert.MachineId);
            contentBuilder.Append(cert.MachineName);
            contentBuilder.Append(cert.OperatorName);

            foreach (var comp in cert.Components)
            {
                contentBuilder.Append(comp.Name);
                contentBuilder.Append(comp.CommitSha);
                contentBuilder.Append(comp.WorkingDirStatus);
                contentBuilder.Append(comp.Integrity);
            }

            // Calcular SHA256
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string SignCertificate(IntegrityCertificate cert)
        {
            // Usar una clave secreta para HMAC (en producción, usar certificado X.509 o HSM)
            // La clave debería estar en configuración segura o Azure Key Vault
            var secretKey = _configuration["IntegrityCertificate:SigningKey"] 
                ?? "AQUAFRISCH-CRA-INTEGRITY-KEY-2025-CHANGE-IN-PRODUCTION";

            var dataToSign = cert.ContentHash + cert.CertificateId + cert.GeneratedAt;
            
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            return Convert.ToBase64String(signatureBytes);
        }

        /// <summary>
        /// Verifica la firma de un certificado de integridad
        /// </summary>
        public bool VerifyCertificateSignature(IntegrityCertificate certificate)
        {
            try
            {
                var secretKey = _configuration["IntegrityCertificate:SigningKey"] 
                    ?? "AQUAFRISCH-CRA-INTEGRITY-KEY-2025-CHANGE-IN-PRODUCTION";

                var dataToSign = certificate.ContentHash + certificate.CertificateId + certificate.GeneratedAt;
                
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
                var expectedSignature = Convert.ToBase64String(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign)));

                var isValid = certificate.Signature == expectedSignature;
                
                _logger.LogInformation("📜 Certificate {Id} signature verification: {Result}", 
                    certificate.CertificateId, isValid ? "VALID" : "INVALID");

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying certificate signature");
                return false;
            }
        }
    }

    #region Network and Certificate Models

    /// <summary>
    /// Estado de conectividad y sincronización con remotos
    /// </summary>
    public class NetworkSyncStatus
    {
        public string CheckedAt { get; set; } = "";
        public bool? HasInternetConnection { get; set; } // null = desconocido, true = online, false = offline
        public string OverallSyncStatus { get; set; } = "unknown"; // synced, out-of-sync, offline, unknown
        public RemoteSyncInfo BackendSync { get; set; } = new();
        public RemoteSyncInfo FrontendSync { get; set; } = new();
        public RemoteSyncInfo TwinCatPlcSync { get; set; } = new();
    }

    /// <summary>
    /// Información de sincronización con remoto para un componente
    /// </summary>
    public class RemoteSyncInfo
    {
        public string ComponentName { get; set; } = "";
        public string RemoteUrl { get; set; } = "";
        public string Status { get; set; } = "unknown"; // synced, ahead, behind, diverged, no-remote, offline, error
        public int CommitsAhead { get; set; }
        public int CommitsBehind { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Certificado de integridad del software - Para auditorías EU CRA
    /// </summary>
    public class IntegrityCertificate
    {
        public string CertificateId { get; set; } = "";
        public string Version { get; set; } = "1.0";
        public string GeneratedAt { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string MachineName { get; set; } = "";
        public string OperatorName { get; set; } = "";
        public string OperatingSystem { get; set; } = "";
        
        public List<CertificateComponent> Components { get; set; } = new();
        public CertificateRuntimeInfo RuntimeInfo { get; set; } = new();
        
        public string OverallStatus { get; set; } = ""; // VERIFIED, MODIFIED, UNKNOWN
        public string ContentHash { get; set; } = "";
        
        // Firma digital
        public string Signature { get; set; } = "";
        public string SignatureAlgorithm { get; set; } = "";
        public string SignedAt { get; set; } = "";
    }

    /// <summary>
    /// Componente dentro del certificado
    /// </summary>
    public class CertificateComponent
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string CommitSha { get; set; } = "";
        public string CommitShort { get; set; } = "";
        public string Branch { get; set; } = "";
        public string CommitDate { get; set; } = "";
        public string CommitAuthor { get; set; } = "";
        public string CommitAuthorEmail { get; set; } = "";
        public string WorkingDirStatus { get; set; } = "";
        public int ModifiedFiles { get; set; }
        public string Integrity { get; set; } = "";
        public bool IsSigned { get; set; }
        public string SignatureStatus { get; set; } = "";
        public string RepoPath { get; set; } = "";
    }

    /// <summary>
    /// Información de runtime en el certificado
    /// </summary>
    public class CertificateRuntimeInfo
    {
        public string TwinCatVersion { get; set; } = "";
        public string TwinCatStatus { get; set; } = "";
        public string AdsClientVersion { get; set; } = "";
        public string DatabaseStatus { get; set; } = "";
    }

    #endregion
}
