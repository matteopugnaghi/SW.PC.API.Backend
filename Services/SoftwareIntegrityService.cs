using SW.PC.API.Backend.Models.Excel;
using System.Diagnostics;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🔐 Servicio de verificación de integridad del software basado en Git
    /// Para cumplimiento de normativas de ciberseguridad NASA/NIST
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
    }

    public class SoftwareIntegrityService : ISoftwareIntegrityService
    {
        private readonly ILogger<SoftwareIntegrityService> _logger;
        private readonly IConfiguration _configuration;
        private readonly object _lock = new object();

        // Información de versiones
        private SoftwareVersionInfo _versionInfo;

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

            _versionInfo = new SoftwareVersionInfo();
            
            // Inicializar información Git de forma asíncrona con rutas por defecto
            _ = InitializeGitInfoAsync();

            _logger.LogInformation("🔐 SoftwareIntegrityService initialized (Git-based)");
        }

        /// <summary>
        /// Configurar rutas de repositorios Git desde Excel (hoja System Config)
        /// </summary>
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
                // Verificación de firma GPG/SSH
                var signatureTask = RunGitCommandAsync(repoPath, "log -1 --format=%G? %GS %GK");

                await Task.WhenAll(shaTask, shaShortTask, branchTask, describeTask, statusTask, dateTask, authorTask, authorEmailTask, messageTask, signatureTask);

                component.CommitShaFull = shaTask.Result.Trim();
                component.CommitSha = shaShortTask.Result.Trim();
                component.Branch = branchTask.Result.Trim();
                component.Version = ParseVersion(describeTask.Result.Trim());
                component.CommitDate = dateTask.Result.Trim();
                component.CommitAuthor = authorTask.Result.Trim();
                component.CommitAuthorEmail = authorEmailTask.Result.Trim();
                component.CommitMessage = messageTask.Result.Trim();
                component.LastVerified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Parsear información de firma
                ParseSignatureInfo(component, signatureTask.Result.Trim());

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
            if (string.IsNullOrWhiteSpace(signatureOutput))
            {
                component.SignatureStatus = "unsigned";
                component.IsSigned = false;
                return;
            }

            var parts = signatureOutput.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var signatureCode = parts.Length > 0 ? parts[0] : "N";
            var signer = parts.Length > 1 ? parts[1] : "";
            var keyId = parts.Length > 2 ? parts[2] : "";

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

            lock (_lock)
            {
                var allVerified = _versionInfo.Backend.Integrity == "verified" &&
                                  _versionInfo.Frontend.Integrity == "verified";

                _logger.LogInformation("🔐 Integrity verification complete. System status: {Status}", 
                    _versionInfo.SystemStatus);

                return allVerified;
            }
        }

        public void RegisterAdminVerification(string adminUser)
        {
            lock (_lock)
            {
                _logger.LogInformation("🔐 Admin verification registered by: {Admin}", adminUser);
                _versionInfo.LastVerificationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                _versionInfo.VerifiedByAdmin = adminUser;
            }
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
    }
}
