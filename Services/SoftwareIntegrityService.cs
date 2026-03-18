using SW.PC.API.Backend.Models.Excel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        void UpdateTwinCATRuntimeInfo(string version, string adsVersion, bool isConnected, bool isSimulated, double taskCycleTimeMs = 0);

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
        private readonly IProjectContextService _projectContext;
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

        // Ruta al ejecutable git (resuelto una vez al inicio)
        private readonly string _gitExecutable;

        // Machine-specific TwinCAT file extensions to ignore in git status
        private static readonly HashSet<string> _machineSpecificExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xti", ".~u", ".~u1", ".sln", ".plcproj"
        };

        public SoftwareIntegrityService(
            ILogger<SoftwareIntegrityService> logger,
            IConfiguration configuration,
            IProjectContextService projectContext)
        {
            _logger = logger;
            _configuration = configuration;
            _projectContext = projectContext;

            // Resolver ruta de git.exe (SYSTEM user puede no tenerlo en PATH)
            _gitExecutable = ResolveGitExecutable();
            _logger.LogInformation("\ud83d\udd27 Git executable: {GitPath}", _gitExecutable);

            // Rutas por defecto (auto-detectadas) - se pueden sobrescribir desde Excel
            _backendRepoPath = FindGitRoot(AppDomain.CurrentDomain.BaseDirectory) 
                ?? AppDomain.CurrentDomain.BaseDirectory;

            _frontendRepoPath = Path.GetFullPath(Path.Combine(_backendRepoPath, "..", "SW.PC.REACT.Frontend", "my-3d-app"));

            _twinCatPlcRepoPath = AutoDetectTwinCatPath();
            _logger.LogInformation("🔧 Paths configured: Backend={Backend}, Frontend={Frontend}, TwinCAT={TwinCAT}",
                _backendRepoPath, _frontendRepoPath, _twinCatPlcRepoPath);

            // 🔐 Ruta del archivo de persistencia de estado
            _stateFilePath = Path.Combine(_backendRepoPath, "integrity-state.json");
            
            // Cargar estado guardado o crear nuevo
            _versionInfo = LoadPersistedState() ?? new SoftwareVersionInfo();
            
            // Siempre usar el nombre de la máquina actual (no el del estado persistido)
            _versionInfo.MachineName = Environment.MachineName;
            
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

        /// <summary>
        /// Auto-detecta la ruta del repositorio TwinCAT PLC:
        /// 1. Desarrollo: ../SW.PC.TwinCAT.PLC (repo hermano)
        /// 2. Desplegado: ../TwinCAT/{activeProjectId}/ (vinculado al proyecto activo)
        /// 3. Desplegado: ../TwinCAT/ primer subfolder con .git (fallback genérico)
        /// 4. Fallback: ruta de desarrollo (aunque no exista todavía)
        /// </summary>
        private string AutoDetectTwinCatPath()
        {
            var parentDir = Path.GetFullPath(Path.Combine(_backendRepoPath, ".."));
            
            // 1. Desarrollo: repo hermano SW.PC.TwinCAT.PLC
            var devPath = Path.Combine(parentDir, "SW.PC.TwinCAT.PLC");
            if (Directory.Exists(Path.Combine(devPath, ".git")))
            {
                _logger.LogInformation("🔧 TwinCAT auto-detected (dev): {Path}", devPath);
                return devPath;
            }

            // 2. Desplegado: carpeta SW.PC.Twincat_3/ hermana de Backend/
            var twinCatFolder = Path.Combine(parentDir, "SW.PC.Twincat_3");
            if (Directory.Exists(twinCatFolder))
            {
                // 2a. Preferir subfolder con mismo nombre que proyecto activo
                try
                {
                    var activeProjectId = _projectContext.ActiveProjectId;
                    if (!string.IsNullOrEmpty(activeProjectId) && activeProjectId != "default")
                    {
                        var projectTwinCatPath = Path.Combine(twinCatFolder, activeProjectId);
                        if (Directory.Exists(Path.Combine(projectTwinCatPath, ".git")))
                        {
                            _logger.LogInformation("🔧 TwinCAT auto-detected (project '{ProjectId}'): {Path}", activeProjectId, projectTwinCatPath);
                            return projectTwinCatPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read active project ID for TwinCAT path detection");
                }

                // 2b. Fallback: primer subfolder con .git
                try
                {
                    foreach (var subDir in Directory.GetDirectories(twinCatFolder))
                    {
                        if (Directory.Exists(Path.Combine(subDir, ".git")))
                        {
                            _logger.LogInformation("🔧 TwinCAT auto-detected (deployed): {Path}", subDir);
                            return subDir;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning TwinCAT folder: {Path}", twinCatFolder);
                }

                // 2c. Comprobar si TwinCAT/ raíz tiene .git directamente (clonado sin subfolder)
                if (Directory.Exists(Path.Combine(twinCatFolder, ".git")))
                {
                    _logger.LogInformation("🔧 TwinCAT auto-detected (root): {Path}", twinCatFolder);
                    return twinCatFolder;
                }

                // TwinCAT/ exists but no .git subfolder yet — return the folder itself
                _logger.LogInformation("🔧 TwinCAT folder exists but no repo found yet: {Path}", twinCatFolder);
                return twinCatFolder;
            }

            // 3. Fallback: ruta de desarrollo
            _logger.LogInformation("🔧 TwinCAT path (fallback): {Path}", devPath);
            return devPath;
        }

        private async Task InitializeGitInfoAsync()
        {
            try
            {
                // 🔐 PRIMERO: Asegurar que allowed_signers está configurado ANTES de verificar firmas
                // Sin esto, git log --format=%G? siempre devuelve "N" aunque el commit esté firmado
                await EnsureAllowedSignersBeforeVerificationAsync();

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

                    // Inicializar info de runtime con valores por defecto SOLO si no están ya configurados
                    if (_versionInfo.TwinCatRuntime == null || _versionInfo.TwinCatRuntime.Status == "unknown")
                    {
                        _versionInfo.TwinCatRuntime = new RuntimeVersionInfo
                        {
                            Name = "TwinCAT Runtime",
                            Version = "Pending connection",
                            Status = "unknown"
                        };
                    }

                    // AdsClient siempre se puede actualizar con la versión de la librería
                    _versionInfo.AdsClient = new RuntimeVersionInfo
                    {
                        Name = "TwinCAT ADS Client",
                        Version = typeof(TwinCAT.Ads.AdsClient).Assembly.GetName().Version?.ToString() ?? "Unknown",
                        Status = "loaded"
                    };

                    if (_versionInfo.Database == null || _versionInfo.Database.Status == "unknown")
                    {
                        _versionInfo.Database = new RuntimeVersionInfo
                        {
                            Name = "Database",
                            Version = "SQL Server",
                            Status = "disabled", // Por defecto deshabilitado, se actualiza desde Excel
                            Details = "Pending configuration"
                        };
                    }

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

        /// <summary>
        /// 🔐 Asegura que allowed_signers esté configurado ANTES de ejecutar git log --format=%G?
        /// Sin esto, git siempre devuelve "N" (unsigned) aunque el commit tenga firma SSH válida.
        /// Lee authorized_signing_keys.json y construye el archivo allowed_signers con todas las claves.
        /// </summary>
        private async Task EnsureAllowedSignersBeforeVerificationAsync()
        {
            try
            {
                // Verificar si SSH signing está configurado
                var gpgFormat = (await RunGitCommandAsync(_backendRepoPath, "config --global gpg.format")).Trim();
                if (gpgFormat != "ssh")
                {
                    // SSH signing no configurado - comprobar si hay authorized_signing_keys.json
                    // para configurar al menos la verificación
                    var authKeysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");
                    if (!File.Exists(authKeysPath))
                    {
                        _logger.LogDebug("🔐 No SSH signing configured and no authorized keys - skipping allowed_signers setup");
                        return;
                    }

                    // Hay claves autorizadas pero no hay SSH signing → configurar solo verificación
                    var keys = JsonSerializer.Deserialize<List<AuthorizedKeyForVerification>>(
                        await File.ReadAllTextAsync(authKeysPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // Filtrar entradas inválidas (fingerprint vacío = entrada corrupta/legacy)
                    keys = keys?.Where(k => !string.IsNullOrWhiteSpace(k.Fingerprint)).ToList();

                    if (keys == null || !keys.Any(k => !string.IsNullOrEmpty(k.PublicKey)))
                    {
                        _logger.LogDebug("🔐 No public keys in authorized_signing_keys.json");
                        return;
                    }

                    // Crear allowed_signers en .ssh del perfil de usuario
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var sshDir = Path.Combine(userProfile, ".ssh");
                    if (!Directory.Exists(sshDir))
                    {
                        // Fallback para LocalSystem
                        sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "config", "systemprofile", ".ssh");
                        if (!Directory.Exists(sshDir))
                            Directory.CreateDirectory(sshDir);
                    }

                    var allowedSignersPath = Path.Combine(sshDir, "allowed_signers");
                    var signerLines = new HashSet<string>();
                    foreach (var k in keys.Where(k => !string.IsNullOrEmpty(k.PublicKey)))
                    {
                        var email = !string.IsNullOrEmpty(k.OwnerEmail) ? k.OwnerEmail : "electronico@aquafrisch.com";
                        signerLines.Add($"{email} namespaces=\"git\" {k.PublicKey}");
                    }

                    if (signerLines.Count > 0)
                    {
                        var newContent = string.Join("\n", signerLines) + "\n";
                        await File.WriteAllTextAsync(allowedSignersPath, newContent);
                        await RunGitCommandAsync(_backendRepoPath, $"config --global gpg.format ssh");
                        await RunGitCommandAsync(_backendRepoPath, $"config --global gpg.ssh.allowedSignersFile \"{allowedSignersPath}\"");
                        _logger.LogInformation("🔐 Configured verification-only allowed_signers with {Count} keys", signerLines.Count);
                    }
                    return;
                }

                // SSH signing está configurado - asegurar allowed_signers
                var signingKey = (await RunGitCommandAsync(_backendRepoPath, "config --global user.signingkey")).Trim();
                if (string.IsNullOrEmpty(signingKey)) return;

                var sshDirectory = Path.GetDirectoryName(signingKey);
                if (string.IsNullOrEmpty(sshDirectory)) return;

                var allowedPath = (await RunGitCommandAsync(_backendRepoPath, "config --global gpg.ssh.allowedSignersFile")).Trim();
                var targetAllowedPath = string.IsNullOrEmpty(allowedPath) || !File.Exists(allowedPath)
                    ? Path.Combine(sshDirectory, "allowed_signers")
                    : allowedPath;

                var email2 = (await RunGitCommandAsync(_backendRepoPath, "config --global user.email")).Trim();
                if (string.IsNullOrEmpty(email2)) email2 = "electronico@aquafrisch.com";

                var lines = new HashSet<string>();

                // 1. Clave local
                if (File.Exists(signingKey))
                {
                    var localPubKey = (await File.ReadAllTextAsync(signingKey)).Trim();
                    lines.Add($"{email2} namespaces=\"git\" {localPubKey}");
                }

                // 2. Todas las claves autorizadas
                var authorizedKeysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");
                if (File.Exists(authorizedKeysPath))
                {
                    try
                    {
                        var authKeys = JsonSerializer.Deserialize<List<AuthorizedKeyForVerification>>(
                            await File.ReadAllTextAsync(authorizedKeysPath),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        if (authKeys != null)
                        {
                            foreach (var ak in authKeys.Where(k => !string.IsNullOrEmpty(k.PublicKey)))
                            {
                                var akEmail = !string.IsNullOrEmpty(ak.OwnerEmail) ? ak.OwnerEmail : email2;
                                lines.Add($"{akEmail} namespaces=\"git\" {ak.PublicKey}");
                            }
                        }
                    }
                    catch { /* Si falla, al menos tenemos la local */ }
                }

                if (lines.Count > 0)
                {
                    var newContent = string.Join("\n", lines) + "\n";
                    var existingContent = File.Exists(targetAllowedPath) ? await File.ReadAllTextAsync(targetAllowedPath) : "";
                    
                    if (existingContent.Trim() != newContent.Trim())
                    {
                        await File.WriteAllTextAsync(targetAllowedPath, newContent);
                        _logger.LogInformation("🔐 Updated allowed_signers with {Count} keys before signature verification", lines.Count);
                    }

                    // Asegurar que git config apunta al archivo
                    if (string.IsNullOrEmpty(allowedPath) || !File.Exists(allowedPath))
                    {
                        await RunGitCommandAsync(_backendRepoPath, $"config --global gpg.ssh.allowedSignersFile \"{targetAllowedPath}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔐 Could not ensure allowed_signers before verification - signatures may show as unsigned");
            }
        }

        /// <summary>
        /// Modelo simplificado para leer authorized_signing_keys.json (sin dependencia de GitOperationsService)
        /// </summary>
        private class AuthorizedKeyForVerification
        {
            public string Fingerprint { get; set; } = "";
            public string OwnerName { get; set; } = "";
            public string OwnerEmail { get; set; } = "";
            public string PublicKey { get; set; } = "";
            public string MachineName { get; set; } = "";
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

        #region 📦 DEPLOY VERSION (Producción sin .git)

        /// <summary>
        /// Modelo para la información de un componente en deploy-version.json
        /// </summary>
        private class DeployComponentInfo
        {
            public string? ComponentName { get; set; }
            public string? Version { get; set; }
            public string? CommitSha { get; set; }
            public string? CommitShaFull { get; set; }
            public string? Branch { get; set; }
            public string? CommitDate { get; set; }
            public string? CommitAuthor { get; set; }
            public string? CommitAuthorEmail { get; set; }
            public string? CommitMessage { get; set; }
            public string? LatestRelease { get; set; }
            public string? LatestReleaseDate { get; set; }
            public bool? IsSigned { get; set; }
            public string? SignatureStatus { get; set; }
            public string? SignatureSigner { get; set; }
            public string? SignatureKey { get; set; }
            public string? DeployedAt { get; set; }
            public string? DeployedFrom { get; set; }
            public string? DeployedBy { get; set; }
        }

        /// <summary>
        /// Modelo completo para deploy-version.json del proyecto
        /// </summary>
        private class ProjectDeployVersionInfo
        {
            public string? ProjectId { get; set; }
            public string? DeployedAt { get; set; }
            public string? DeployedFrom { get; set; }
            public string? DeployedBy { get; set; }
            public DeployComponentInfo? Backend { get; set; }
            public DeployComponentInfo? Frontend { get; set; }
        }

        /// <summary>
        /// Obtener la ruta del deploy-version.json (siempre en raiz de Backend)
        /// </summary>
        private string GetProjectDeployVersionPath()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            // deploy-version.json siempre en raiz de Backend (no dentro de Projects/)
            // Asi copiar carpetas de proyecto nunca afecta la version del servidor
            return Path.Combine(basePath, "deploy-version.json");
        }

        /// <summary>
        /// Intentar cargar información de versión desde deploy-version.json del proyecto
        /// </summary>
        private async Task<GitVersionComponent?> TryLoadDeployVersionAsync(string componentName, string ignoredPath)
        {
            try
            {
                // Usar la ruta del proyecto activo
                var deployVersionPath = GetProjectDeployVersionPath();
                
                _logger.LogInformation("📦 Intentando cargar deploy-version.json desde: {Path} para componente: {Component}", 
                    deployVersionPath, componentName);
                
                if (!File.Exists(deployVersionPath))
                {
                    _logger.LogWarning("📦 deploy-version.json NO encontrado en: {Path}", deployVersionPath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(deployVersionPath);
                _logger.LogDebug("📦 Contenido del deploy-version.json: {Json}", json.Substring(0, Math.Min(500, json.Length)));
                
                var projectDeploy = JsonSerializer.Deserialize<ProjectDeployVersionInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (projectDeploy == null)
                {
                    _logger.LogWarning("📦 No se pudo deserializar deploy-version.json");
                    return null;
                }

                _logger.LogInformation("📦 deploy-version.json deserializado: ProjectId={ProjectId}, Backend={HasBackend}, Frontend={HasFrontend}", 
                    projectDeploy.ProjectId, 
                    projectDeploy.Backend != null,
                    projectDeploy.Frontend != null);

                // Seleccionar el componente correcto según el nombre
                DeployComponentInfo? deployInfo = componentName.ToLower() switch
                {
                    "backend" => projectDeploy.Backend,
                    "frontend" => projectDeploy.Frontend,
                    _ => null
                };

                if (deployInfo == null)
                {
                    _logger.LogWarning("📦 Componente '{Component}' es NULL en deploy-version.json", componentName);
                    return null;
                }

                _logger.LogInformation("📦 Loading deploy-version.json for {Component}: v{Version} ({Sha}) - Project: {Project}", 
                    componentName, deployInfo.Version, deployInfo.CommitSha, projectDeploy.ProjectId);

                // Inferir IsSigned a partir de SignatureStatus si no está explícito en el JSON
                var signatureStatus = (deployInfo.SignatureStatus ?? "N/A").ToUpperInvariant();
                var isSigned = deployInfo.IsSigned ?? signatureStatus switch
                {
                    "SIGNED" => true,
                    "VALID" => true,
                    "BAD" => true,      // Tiene firma pero es mala
                    "UNTRUSTED" => true, // Tiene firma pero key no confiable
                    "EXPIRED" => true,   // Tiene firma pero expirada
                    "EXPIRED_KEY" => true,
                    "REVOKED" => true,   // Tiene firma pero revocada
                    _ => false
                };

                // Inferir SignatureType si hay firma
                var signatureType = isSigned ? "GPG" : "none";

                return new GitVersionComponent
                {
                    Name = componentName,
                    RepoPath = Path.GetDirectoryName(deployVersionPath) ?? "",
                    Version = deployInfo.Version ?? "0.0.0",
                    CommitSha = deployInfo.CommitSha ?? "unknown",
                    CommitShaFull = deployInfo.CommitShaFull ?? "unknown",
                    Branch = deployInfo.Branch ?? "deployed",
                    CommitDate = deployInfo.CommitDate ?? "",
                    CommitAuthor = deployInfo.CommitAuthor ?? "",
                    CommitAuthorEmail = deployInfo.CommitAuthorEmail ?? "",
                    CommitMessage = deployInfo.CommitMessage ?? "",
                    LatestRelease = deployInfo.LatestRelease ?? "",
                    LatestReleaseDate = deployInfo.LatestReleaseDate ?? "",
                    IsSigned = isSigned,
                    SignatureStatus = signatureStatus,
                    SignatureType = signatureType,
                    SignatureSigner = deployInfo.SignatureSigner ?? "",
                    SignatureKeyId = deployInfo.SignatureKey ?? "",
                    WorkingDirStatus = "deployed", // En producción siempre está "deployed"
                    ModifiedFiles = 0,
                    Integrity = "deployed", // Estado especial para producción
                    LastVerified = projectDeploy.DeployedAt ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not load deploy-version.json from project folder");
                return null;
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

            try
            {
                // 🚀 PRODUCCIÓN: Primero intentar leer deploy-version.json (generado durante deploy)
                // IMPORTANTE: Esto debe ejecutarse ANTES de verificar si el directorio del repo existe,
                // porque en producción el directorio del frontend no existe (está embebido en wwwroot/)
                var deployVersionPath = Path.Combine(repoPath, "deploy-version.json");
                var deployedComponent = await TryLoadDeployVersionAsync(name, deployVersionPath);
                if (deployedComponent != null)
                {
                    _logger.LogInformation("📦 {Name}: Loaded from deploy-version.json (production mode)", name);
                    return deployedComponent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "📦 deploy-version.json not available for {Name}, falling back to Git", name);
            }

            if (!Directory.Exists(repoPath))
            {
                _logger.LogWarning("⚠️ Repository path not found: {Path}", repoPath);
                component.Integrity = "unknown";
                return component;
            }

            try
            {
                // 🔧 DESARROLLO: Verificar si es un repositorio Git
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
                    // Filter out machine-specific TwinCAT files (.xti, .~u, .~u1, .sln, .plcproj)
                    var modifiedLines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => {
                            if (line.Length <= 3) return true;
                            var fileName = line[3..].TrimEnd('\r');
                            var ext = Path.GetExtension(fileName);
                            return !_machineSpecificExtensions.Contains(ext);
                        })
                        .ToArray();
                    
                    if (modifiedLines.Length == 0)
                    {
                        component.WorkingDirStatus = "clean";
                        component.ModifiedFiles = 0;
                        component.Integrity = "verified";
                    }
                    else
                    {
                        component.WorkingDirStatus = "dirty";
                        component.ModifiedFiles = modifiedLines.Length;
                        component.Integrity = "modified";
                        _logger.LogWarning("⚠️ {Name} has {Count} uncommitted changes", name, modifiedLines.Length);
                    }
                }

                // 📁 Fallback: si git commands devolvieron vacío, leer .git files directamente
                if (string.IsNullOrEmpty(component.CommitSha))
                {
                    _logger.LogWarning("⚠️ Git commands returned empty for {Name}, trying direct .git file read", name);
                    await TryReadGitFilesDirectlyAsync(component, repoPath);
                }

                _logger.LogInformation("📦 {Name}: {Version} ({Sha}) [{Status}]", 
                    name, component.Version, component.CommitSha, component.WorkingDirStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Git info for {Name}", name);
                // Fallback: intentar leer .git files directamente
                if (string.IsNullOrEmpty(component.CommitSha))
                {
                    await TryReadGitFilesDirectlyAsync(component, repoPath);
                }
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

        /// <summary>
        /// Fallback: lee información git directamente de archivos .git cuando los comandos git fallan
        /// (ej: servicio corriendo como SYSTEM sin acceso a ejecutar git)
        /// </summary>
        private async Task TryReadGitFilesDirectlyAsync(GitVersionComponent component, string repoPath)
        {
            try
            {
                var gitDir = Path.Combine(repoPath, ".git");

                // Handle .git file (submodule/worktree)
                if (File.Exists(gitDir) && !Directory.Exists(gitDir))
                {
                    var gitFileContent = (await File.ReadAllTextAsync(gitDir)).Trim();
                    if (gitFileContent.StartsWith("gitdir:"))
                    {
                        gitDir = gitFileContent.Substring(7).Trim();
                        if (!Path.IsPathRooted(gitDir))
                            gitDir = Path.GetFullPath(Path.Combine(repoPath, gitDir));
                    }
                }

                if (!Directory.Exists(gitDir))
                {
                    _logger.LogWarning("📁 .git directory not found at {Path}", gitDir);
                    return;
                }

                // Leer HEAD
                var headPath = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headPath)) return;

                var headContent = (await File.ReadAllTextAsync(headPath)).Trim();
                string branch = "";
                string commitSha = "";

                if (headContent.StartsWith("ref: "))
                {
                    var refPath = headContent.Substring(5).Trim();
                    branch = refPath.Replace("refs/heads/", "");

                    // Leer SHA del ref
                    var refFilePath = Path.Combine(gitDir, refPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (File.Exists(refFilePath))
                    {
                        commitSha = (await File.ReadAllTextAsync(refFilePath)).Trim();
                    }
                    else
                    {
                        // Buscar en packed-refs
                        var packedRefsPath = Path.Combine(gitDir, "packed-refs");
                        if (File.Exists(packedRefsPath))
                        {
                            var lines = await File.ReadAllLinesAsync(packedRefsPath);
                            foreach (var line in lines)
                            {
                                if (!line.StartsWith("#") && line.Contains(refPath))
                                {
                                    commitSha = line.Split(' ')[0];
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    commitSha = headContent;
                    branch = "detached";
                }

                if (string.IsNullOrEmpty(commitSha)) return;

                component.CommitShaFull = commitSha;
                component.CommitSha = commitSha.Length >= 7 ? commitSha.Substring(0, 7) : commitSha;
                component.Branch = branch;
                component.LastVerified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Buscar tags CalVer (20*) para versión
                var tagsDir = Path.Combine(gitDir, "refs", "tags");
                if (Directory.Exists(tagsDir))
                {
                    var tags = Directory.GetFiles(tagsDir, "20*")
                        .Select(f => new { Name = Path.GetFileName(f), Sha = File.ReadAllText(f).Trim() })
                        .OrderByDescending(t => t.Name)
                        .ToList();

                    if (tags.Count > 0)
                    {
                        component.LatestRelease = tags[0].Name;
                        component.Version = tags[0].Sha == commitSha 
                            ? tags[0].Name 
                            : $"{tags[0].Name}+ ({component.CommitSha})";
                    }
                }

                if (component.Version == "0.0.0")
                    component.Version = component.CommitSha;

                // Si no tenemos workingDirStatus, asumir clean (no podemos verificar sin git)
                if (string.IsNullOrEmpty(component.WorkingDirStatus) || component.WorkingDirStatus == "unknown")
                {
                    component.WorkingDirStatus = "clean";
                    component.Integrity = "verified";
                }

                _logger.LogInformation("📁 {Name}: Read from .git files: {Sha} branch={Branch} version={Version}",
                    component.Name, component.CommitSha, component.Branch, component.Version);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "📁 Could not read .git files directly from {Path}", repoPath);
            }
        }

        private async Task<string> RunGitCommandAsync(string workingDir, string arguments)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _gitExecutable,
                        Arguments = $"-c safe.directory=* {arguments}",
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
                
                // Leer stdout, stderr y esperar exit TODO en paralelo con timeout global
                var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
                var exitTask = process.WaitForExitAsync(cts.Token);

                try
                {
                    await Task.WhenAll(stdoutTask, stderrTask, exitTask);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("⏱️ Git command timed out after 15s: git {Args} (in {Dir})", arguments, workingDir);
                    try { process.Kill(true); } catch { }
                    return "";
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogDebug("Git exit code {Code}: git {Args} | stderr: {Err}", process.ExitCode, arguments, stderrTask.Result);
                }

                return stdoutTask.Result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("⏱️ Git command cancelled: git {Args}", arguments);
                return "";
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

        /// <summary>
        /// Busca git.exe en PATH y en rutas de instalación comunes.
        /// SYSTEM user frecuentemente no tiene git en su PATH.
        /// </summary>
        private string ResolveGitExecutable()
        {
            // 1. Intentar 'git' directamente (funciona si está en PATH)
            try
            {
                var testProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (testProcess != null)
                {
                    testProcess.WaitForExit(5000);
                    if (testProcess.ExitCode == 0)
                        return "git";
                    testProcess.Kill();
                }
            }
            catch { /* git not in PATH */ }

            // 2. Buscar en rutas comunes de instalación
            var commonPaths = new[]
            {
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files (x86)\Git\bin\git.exe",
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"),
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("🔧 Found git at: {Path}", path);
                    return path;
                }
            }

            // 3. Fallback: devolver "git" y dejar que falle con el fallback de .git files
            _logger.LogWarning("⚠️ git.exe not found in PATH or common locations");
            return "git";
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

        public void UpdateTwinCATRuntimeInfo(string version, string adsVersion, bool isConnected, bool isSimulated, double taskCycleTimeMs = 0)
        {
            lock (_lock)
            {
                _versionInfo.TwinCatRuntime = new RuntimeVersionInfo
                {
                    Name = "TwinCAT Runtime",
                    Version = version,
                    Status = isSimulated ? "simulated" : (isConnected ? "connected" : "disconnected"),
                    Details = isSimulated ? "Running in simulation mode" : "",
                    TaskCycleTimeMs = taskCycleTimeMs > 0 ? taskCycleTimeMs : (isSimulated ? 10.0 : null)
                };

                _versionInfo.AdsClient = new RuntimeVersionInfo
                {
                    Name = "TwinCAT ADS Client",
                    Version = adsVersion,
                    Status = "loaded",
                    Details = ""
                };

                // Solo log debug para evitar spam en cada ciclo de polling
                _logger.LogDebug("🔧 TwinCAT Runtime info updated: {Version} ({Status}) - Cycle Time: {CycleTime}ms", 
                    version, _versionInfo.TwinCatRuntime.Status, taskCycleTimeMs);
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
                _versionInfo.LastVerificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                _versionInfo.LastVerificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                var secondsUntil = (nextVerification - DateTime.Now).TotalSeconds;
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
                CheckedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // 1. Verificar conectividad a internet
            status.HasInternetConnection = await CheckInternetConnectivityAsync();
            
            _logger.LogInformation("🌐 Internet connectivity: {Status}", 
                status.HasInternetConnection == true ? "Connected" : "Offline");

            // 2. Si hay internet, verificar estado de sincronización con remotos
            if (status.HasInternetConnection == true)
            {
                // Ejecutar los 3 fetch en paralelo (cada uno hace git fetch al remoto)
                var backendSyncTask = GetRemoteSyncStatusAsync("Backend", _backendRepoPath);
                var frontendSyncTask = GetRemoteSyncStatusAsync("Frontend", _frontendRepoPath);
                var twinCatSyncTask = GetRemoteSyncStatusAsync("TwinCAT PLC", _twinCatPlcRepoPath);
                
                await Task.WhenAll(backendSyncTask, frontendSyncTask, twinCatSyncTask);
                
                status.BackendSync = backendSyncTask.Result;
                status.FrontendSync = frontendSyncTask.Result;
                status.TwinCatPlcSync = twinCatSyncTask.Result;

                // Calcular estado general: solo considerar componentes que tienen repo real
                // Componentes desplegados sin .git ("no-repo") son normales y no cuentan como out-of-sync
                var repoComponents = new[] { status.BackendSync, status.FrontendSync, status.TwinCatPlcSync }
                    .Where(c => c.Status != "no-repo")
                    .ToList();

                if (repoComponents.Count == 0)
                    status.OverallSyncStatus = "deployed"; // Ningún componente tiene repo (todo desplegado)
                else if (repoComponents.All(c => c.Status == "synced"))
                    status.OverallSyncStatus = "synced";
                else
                    status.OverallSyncStatus = "out-of-sync";
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

            _logger.LogInformation("🔄 Checking sync for {Name}: path={Path}", name, repoPath);

            if (!Directory.Exists(repoPath))
            {
                _logger.LogWarning("🔄 {Name}: directory does not exist: {Path}", name, repoPath);
                syncInfo.Status = "no-repo";
                return syncInfo;
            }

            // Verificar que es un repo Git antes de ejecutar comandos que se pueden colgar
            var gitDir = Path.Combine(repoPath, ".git");
            if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
            {
                _logger.LogWarning("🔄 {Name}: path exists but no .git found: {Path}", name, repoPath);
                syncInfo.Status = "no-repo";
                syncInfo.Message = $"No .git in: {repoPath}";
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
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
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
            certificate.SignedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");

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
        public string OverallSyncStatus { get; set; } = "unknown"; // synced, out-of-sync, deployed, offline, unknown
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
