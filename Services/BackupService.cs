// ==================================================================
// Services/BackupService.cs
// DATA MANAGEMENT - Servicio Principal de Backup/Restore
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using System.IO.Compression;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Rutas de un proyecto específico
    /// </summary>
    public class ProjectPaths
    {
        public string ProjectId { get; set; } = "";
        public string ProjectRoot { get; set; } = "";
        public string ConfigPath { get; set; } = "";
        public string ModelsPath { get; set; } = "";
        public string DataPath { get; set; } = "";
        public string BackupsPath { get; set; } = "";
        public string DatabasePath { get; set; } = "";
        public string ExcelConfigPath { get; set; } = "";
        public string SbomPath { get; set; } = "";      // EU CRA Compliance
        public string AuditPath { get; set; } = "";     // EU CRA Compliance
        public string TranslationsPath { get; set; } = ""; // Translations per project
        public string DocsPath { get; set; } = "";       // DMS: Document Management System
        public string LogsPath { get; set; } = "";       // NxLog JSONL Export (SOC PIVOT TISSEO)
        public string TwinCatPath { get; set; } = "";    // TwinCAT PLC repo (hermano de Backend/)
    }

    public interface IBackupService
    {
        /// <summary>Crear un nuevo backup</summary>
        Task<BackupOperationResponse> CreateBackupAsync(string projectId, CreateBackupRequest request, string? userId = null);
        
        /// <summary>Restaurar desde un backup</summary>
        Task<BackupOperationResponse> RestoreBackupAsync(string projectId, RestoreBackupRequest request, string? userId = null);
        
        /// <summary>Listar backups disponibles</summary>
        Task<BackupListResponse> ListBackupsAsync(string projectId);
        
        /// <summary>Obtener información de un backup específico</summary>
        Task<BackupInfo?> GetBackupAsync(string projectId, string backupId);
        
        /// <summary>Eliminar un backup</summary>
        Task<BackupOperationResponse> DeleteBackupAsync(string projectId, string backupId);
        
        /// <summary>Verificar integridad de un backup</summary>
        Task<BackupVerificationResponse> VerifyBackupAsync(string projectId, string backupId);
        
        /// <summary>Obtener ruta para exportar backup</summary>
        Task<string?> GetBackupFilePathAsync(string projectId, string backupId);
        
        /// <summary>Importar backup desde archivo</summary>
        Task<BackupOperationResponse> ImportBackupAsync(string projectId, Stream fileStream, string fileName);
        
        /// <summary>Obtener configuración de backup del proyecto</summary>
        Task<BackupConfig> GetBackupConfigAsync(string projectId);
        
        /// <summary>Obtener estado del sistema de backup</summary>
        Task<BackupSystemStatus> GetSystemStatusAsync(string projectId);
        
        /// <summary>Limpiar backups antiguos según política de retención</summary>
        Task<int> CleanupOldBackupsAsync(string projectId);
    }

    public class BackupService : IBackupService
    {
        private readonly ILogger<BackupService> _logger;
        private readonly IProjectContextService _projectContext;
        private readonly IBackupCertificateService _certificateService;
        private readonly IExcelConfigService _excelConfig;
        private readonly IAuditLogService _auditLog;
        private readonly IWebHostEnvironment _environment;
        private readonly ISoftwareIntegrityService _integrityService;
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BackupService(
            ILogger<BackupService> logger,
            IProjectContextService projectContext,
            IBackupCertificateService certificateService,
            IExcelConfigService excelConfig,
            IAuditLogService auditLog,
            IWebHostEnvironment environment,
            ISoftwareIntegrityService integrityService)
        {
            _logger = logger;
            _projectContext = projectContext;
            _certificateService = certificateService;
            _excelConfig = excelConfig;
            _auditLog = auditLog;
            _environment = environment;
            _integrityService = integrityService;
        }

        /// <summary>
        /// Obtiene las rutas de un proyecto específico
        /// </summary>
        private ProjectPaths GetProjectPaths(string projectId)
        {
            var contentRoot = _environment.ContentRootPath;
            var webRoot = _environment.WebRootPath ?? contentRoot;
            
            if (projectId == "default")
            {
                // Modo legacy
                return new ProjectPaths
                {
                    ProjectId = projectId,
                    ProjectRoot = contentRoot,
                    ConfigPath = Path.Combine(contentRoot, "ExcelConfigs"),
                    ModelsPath = Path.Combine(webRoot, "models"),
                    DataPath = Path.Combine(contentRoot, "Data"),
                    BackupsPath = Path.Combine(contentRoot, "backups"),
                    DatabasePath = Path.Combine(contentRoot, "Data", "Aquafrisch.db"),
                    ExcelConfigPath = Path.Combine(contentRoot, "ExcelConfigs", "ProjectConfig.xlsm"),
                    SbomPath = Path.Combine(webRoot, "sbom"),           // Legacy: wwwroot/sbom
                    AuditPath = Path.Combine(webRoot, "audit"),          // Legacy: wwwroot/audit
                    TranslationsPath = Path.Combine(contentRoot, "translations"), // Legacy: root/translations
                    DocsPath = Path.Combine(contentRoot, "docs"),          // Legacy: root/docs
                    LogsPath = Path.Combine(Path.Combine(webRoot, "logs")), // Legacy: wwwroot/logs
                    TwinCatPath = "" // Legacy: no TwinCAT
                };
            }
            
            // Modo multi-proyecto
            var projectRoot = Path.Combine(contentRoot, "Projects", projectId);
            return new ProjectPaths
            {
                ProjectId = projectId,
                ProjectRoot = projectRoot,
                ConfigPath = Path.Combine(projectRoot, "config"),
                ModelsPath = Path.Combine(projectRoot, "models"),
                DataPath = Path.Combine(projectRoot, "data"),
                BackupsPath = Path.Combine(projectRoot, "backups"),
                DatabasePath = Path.Combine(projectRoot, "data", "project.db"),
                ExcelConfigPath = Path.Combine(projectRoot, "config", "ProjectConfig.xlsm"),
                SbomPath = Path.Combine(projectRoot, "sbom"),           // Multi-proyecto: Projects/{id}/sbom
                AuditPath = Path.Combine(projectRoot, "audit"),          // Multi-proyecto: Projects/{id}/audit
                TranslationsPath = Path.Combine(projectRoot, "translations"), // Multi-proyecto: Projects/{id}/translations
                DocsPath = Path.Combine(projectRoot, "docs"),              // Multi-proyecto: Projects/{id}/docs
                LogsPath = Path.Combine(projectRoot, "logs"),               // Multi-proyecto: Projects/{id}/logs (NxLog JSONL)
                TwinCatPath = ResolveActualTwinCatPath(contentRoot, projectId) // Auto-detectado via ISoftwareIntegrityService
            };
        }

        /// <summary>
        /// Resuelve la ruta real del repo TwinCAT usando la auto-detección de SoftwareIntegrityService.
        /// El repo puede tener un nombre diferente al projectId (ej: A72.TOUTWP vs test-proyecto).
        /// </summary>
        private string ResolveActualTwinCatPath(string contentRoot, string projectId)
        {
            try
            {
                // Usar la ruta auto-detectada por SoftwareIntegrityService (que ya maneja fallbacks)
                var (_, _, twinCatPath) = _integrityService.GetRepositoryPaths();
                if (!string.IsNullOrEmpty(twinCatPath) && Directory.Exists(twinCatPath))
                {
                    _logger.LogInformation("🔧 Backup TwinCAT path (auto-detected): {Path}", twinCatPath);
                    return twinCatPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get TwinCAT path from IntegrityService, using fallback");
            }

            // Fallback: ruta estática ../SW.PC.Twincat_3/{projectId}/
            return Path.Combine(Path.GetFullPath(Path.Combine(contentRoot, "..")), "SW.PC.Twincat_3", projectId);
        }

        public async Task<BackupOperationResponse> CreateBackupAsync(string projectId, CreateBackupRequest request, string? userId = null)
        {
            var response = new BackupOperationResponse();
            
            try
            {
                _logger.LogInformation("Creating backup for project {ProjectId}", projectId);
                
                var projectPaths = GetProjectPaths(projectId);
                var backupsDir = projectPaths.BackupsPath;
                Directory.CreateDirectory(backupsDir);
                
                // Generar ID y nombre del backup
                var timestamp = DateTime.Now;
                var backupId = $"backup_{projectId}_{timestamp:yyyyMMdd_HHmmss}";
                
                // Nombre limpio: solo la descripción del usuario o nombre por tipo
                // La fecha, proyecto y tipo se muestran en la UI desde metadata
                string backupName;
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    backupName = request.Name;
                }
                else
                {
                    backupName = request.Type switch
                    {
                        BackupType.Scheduled => $"Backup programado",
                        BackupType.PreRestore => $"Backup pre-restauración",
                        BackupType.PreUpdate => $"Backup pre-actualización",
                        _ => $"Backup manual"
                    };
                }
                
                var zipFileName = $"{backupId}.zip";
                var zipPath = Path.Combine(backupsDir, zipFileName);
                
                // Crear información del backup
                var backupInfo = new BackupInfo
                {
                    Id = backupId,
                    ProjectId = projectId,
                    Name = backupName,
                    Description = request.Description,
                    CreatedAt = timestamp,
                    CreatedBy = userId ?? "system",
                    Type = request.Type,
                    FilePath = zipPath,
                    AppVersion = GetAppVersion(),
                    Contents = new BackupContents()
                };
                
                // Crear manifest
                var manifest = new BackupManifest
                {
                    ManifestVersion = "1.0",
                    BackupInfo = backupInfo,
                    GeneratedAt = timestamp,
                    Metadata = new Dictionary<string, string>
                    {
                        ["projectId"] = projectId,
                        ["appVersion"] = backupInfo.AppVersion ?? "unknown",
                        ["hostName"] = Environment.MachineName
                    }
                };
                
                // Crear archivo ZIP
                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    // Agregar configuración Excel
                    if (request.IncludeConfig)
                    {
                        var configPath = projectPaths.ConfigPath;
                        if (Directory.Exists(configPath))
                        {
                            var configFiles = Directory.GetFiles(configPath, "*.*", SearchOption.AllDirectories)
                                .Where(f => !Path.GetFileName(f).StartsWith("~$")).ToArray(); // Excluir archivos temporales de Excel
                            foreach (var file in configFiles)
                            {
                                var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, file).Replace('\\', '/');
                                
                                // Usar FileShare.ReadWrite para copiar archivos que pueden estar en uso (ej: Excel abierto por EPPlus)
                                var entry = zipArchive.CreateEntry(relativePath);
                                using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var entryStream = entry.Open())
                                {
                                    await sourceStream.CopyToAsync(entryStream);
                                }
                                
                                manifest.Files.Add(new BackupFileEntry
                                {
                                    RelativePath = relativePath,
                                    Hash = await ComputeFileHashAsync(file),
                                    SizeBytes = new FileInfo(file).Length,
                                    ModifiedAt = File.GetLastWriteTimeUtc(file)
                                });
                            }
                            backupInfo.Contents.HasConfig = true;
                            backupInfo.Contents.ConfigFilesCount = configFiles.Length;
                        }
                    }
                    
                    // Agregar modelos 3D y archivos relacionados
                    if (request.IncludeModels)
                    {
                        var modelsPath = projectPaths.ModelsPath;
                        if (Directory.Exists(modelsPath))
                        {
                            // Incluir TODOS los archivos de la carpeta models (modelos 3D + README, texturas, etc.)
                            var allModelFiles = Directory.GetFiles(modelsPath, "*.*", SearchOption.AllDirectories);
                            
                            foreach (var file in allModelFiles)
                            {
                                var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, file).Replace('\\', '/');
                                await AddFileToZipAsync(zipArchive, file, relativePath);
                                
                                manifest.Files.Add(new BackupFileEntry
                                {
                                    RelativePath = relativePath,
                                    Hash = await ComputeFileHashAsync(file),
                                    SizeBytes = new FileInfo(file).Length,
                                    ModifiedAt = File.GetLastWriteTimeUtc(file)
                                });
                            }
                            backupInfo.Contents.HasModels = true;
                            // Contar solo archivos de modelo 3D para el conteo
                            backupInfo.Contents.ModelsCount = allModelFiles.Count(f => IsModelFile(f));
                        }
                    }
                    
                    // Agregar base de datos
                    if (request.IncludeDatabase)
                    {
                        var dbPath = projectPaths.DatabasePath;
                        if (File.Exists(dbPath))
                        {
                            var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, dbPath).Replace('\\', '/');
                            
                            // SQLite: Crear copia temporal para evitar bloqueo
                            var tempDbPath = Path.Combine(Path.GetTempPath(), $"backup_db_{Guid.NewGuid()}.db");
                            try
                            {
                                // Usar File.Copy con FileShare.ReadWrite para copiar DB en uso
                                using (var sourceStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                using (var destStream = new FileStream(tempDbPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await sourceStream.CopyToAsync(destStream);
                                }
                                
                                await AddFileToZipAsync(zipArchive, tempDbPath, relativePath);
                                
                                var dbFileInfo = new FileInfo(tempDbPath);
                                manifest.Files.Add(new BackupFileEntry
                                {
                                    RelativePath = relativePath,
                                    Hash = await ComputeFileHashAsync(tempDbPath),
                                    SizeBytes = dbFileInfo.Length,
                                    ModifiedAt = File.GetLastWriteTimeUtc(dbPath)
                                });
                                backupInfo.Contents.HasDatabase = true;
                                backupInfo.Contents.DatabaseSizeBytes = dbFileInfo.Length;
                            }
                            finally
                            {
                                // Limpiar archivo temporal
                                if (File.Exists(tempDbPath))
                                {
                                    try { File.Delete(tempDbPath); } catch { /* ignore */ }
                                }
                            }
                        }
                    }
                    
                    // Agregar repositorio TwinCAT PLC (carpeta hermana: ../SW.PC.Twincat_3/{projectId}/)
                    if (request.IncludeTwinCAT && !string.IsNullOrEmpty(projectPaths.TwinCatPath))
                    {
                        var twinCatPath = projectPaths.TwinCatPath;
                        if (Directory.Exists(twinCatPath))
                        {
                            // Full copy — no exclusions, backup everything as-is
                            var twinCatFiles = Directory.GetFiles(twinCatPath, "*.*", SearchOption.AllDirectories);

                            foreach (var file in twinCatFiles)
                            {
                                var relativePath = Path.Combine("twincat", Path.GetRelativePath(twinCatPath, file)).Replace('\\', '/');
                                await AddFileToZipAsync(zipArchive, file, relativePath);
                                
                                manifest.Files.Add(new BackupFileEntry
                                {
                                    RelativePath = relativePath,
                                    Hash = await ComputeFileHashAsync(file),
                                    SizeBytes = new FileInfo(file).Length,
                                    ModifiedAt = File.GetLastWriteTimeUtc(file)
                                });
                            }
                            backupInfo.Contents.HasTwinCAT = true;
                            backupInfo.Contents.TwinCatFilesCount = twinCatFiles.Length;
                            
                            if (twinCatFiles.Length > 0)
                            {
                                _logger.LogInformation("✅ TwinCAT PLC incluido en backup ({Count} archivos, copia completa)", twinCatFiles.Length);
                            }
                        }
                    }
                    
                    // Agregar authorized_signing_keys.json (claves SSH para verificación cross-server)
                    var authKeysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");
                    if (File.Exists(authKeysPath))
                    {
                        var relativePath = "authorized_signing_keys.json";
                        await AddFileToZipAsync(zipArchive, authKeysPath, relativePath);
                        
                        manifest.Files.Add(new BackupFileEntry
                        {
                            RelativePath = relativePath,
                            Hash = await ComputeFileHashAsync(authKeysPath),
                            SizeBytes = new FileInfo(authKeysPath).Length,
                            ModifiedAt = File.GetLastWriteTimeUtc(authKeysPath)
                        });
                        
                        _logger.LogInformation("✅ authorized_signing_keys.json incluido en backup (SSH cross-server verification)");
                    }
                    
                    // Agregar deploy-version.json como metadata de trazabilidad (desde raiz de Backend, NO del proyecto)
                    // Se incluye en el backup como referencia read-only pero NO se restaura
                    var deployVersionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deploy-version.json");
                    if (File.Exists(deployVersionPath))
                    {
                        var relativePath = "deploy-version.json";
                        await AddFileToZipAsync(zipArchive, deployVersionPath, relativePath);
                        
                        manifest.Files.Add(new BackupFileEntry
                        {
                            RelativePath = relativePath,
                            Hash = await ComputeFileHashAsync(deployVersionPath),
                            SizeBytes = new FileInfo(deployVersionPath).Length,
                            ModifiedAt = File.GetLastWriteTimeUtc(deployVersionPath)
                        });
                        
                        _logger.LogInformation("✅ deploy-version.json incluido en backup (trazabilidad EU CRA)");
                    }
                    
                    // Agregar README.md del proyecto (documentación)
                    var readmePath = Path.Combine(projectPaths.ProjectRoot, "README.md");
                    if (File.Exists(readmePath))
                    {
                        var relativePath = "README.md";
                        await AddFileToZipAsync(zipArchive, readmePath, relativePath);
                        
                        manifest.Files.Add(new BackupFileEntry
                        {
                            RelativePath = relativePath,
                            Hash = await ComputeFileHashAsync(readmePath),
                            SizeBytes = new FileInfo(readmePath).Length,
                            ModifiedAt = File.GetLastWriteTimeUtc(readmePath)
                        });
                    }
                    
                    // Agregar SBOM (EU CRA Compliance - trazabilidad de dependencias)
                    var sbomPath = projectPaths.SbomPath;
                    if (Directory.Exists(sbomPath))
                    {
                        var sbomFiles = Directory.GetFiles(sbomPath, "*.*", SearchOption.AllDirectories);
                        foreach (var sbomFile in sbomFiles)
                        {
                            var relativePath = Path.Combine("sbom", Path.GetRelativePath(sbomPath, sbomFile)).Replace('\\', '/');
                            await AddFileToZipAsync(zipArchive, sbomFile, relativePath);
                            
                            manifest.Files.Add(new BackupFileEntry
                            {
                                RelativePath = relativePath,
                                Hash = await ComputeFileHashAsync(sbomFile),
                                SizeBytes = new FileInfo(sbomFile).Length,
                                ModifiedAt = File.GetLastWriteTimeUtc(sbomFile)
                            });
                        }
                        
                        if (sbomFiles.Length > 0)
                        {
                            _logger.LogInformation("✅ SBOM incluido en backup ({Count} archivos - EU CRA Compliance)", sbomFiles.Length);
                        }
                    }
                    
                    // Agregar Audit Logs (EU CRA Compliance - trazabilidad de acciones)
                    // ⚠️ IMPORTANTE: Forzar flush antes de copiar para incluir entradas en cache
                    await _auditLog.FlushAsync();
                    
                    var auditPath = projectPaths.AuditPath;
                    if (Directory.Exists(auditPath))
                    {
                        var auditFiles = Directory.GetFiles(auditPath, "*.json", SearchOption.AllDirectories);
                        foreach (var auditFile in auditFiles)
                        {
                            var relativePath = Path.Combine("audit", Path.GetRelativePath(auditPath, auditFile)).Replace('\\', '/');
                            await AddFileToZipAsync(zipArchive, auditFile, relativePath);
                            
                            manifest.Files.Add(new BackupFileEntry
                            {
                                RelativePath = relativePath,
                                Hash = await ComputeFileHashAsync(auditFile),
                                SizeBytes = new FileInfo(auditFile).Length,
                                ModifiedAt = File.GetLastWriteTimeUtc(auditFile)
                            });
                        }
                        
                        if (auditFiles.Length > 0)
                        {
                            _logger.LogInformation("✅ Audit logs incluidos en backup ({Count} archivos - EU CRA Compliance)", auditFiles.Length);
                        }
                    }
                    
                    // Agregar Translations (traducciones del proyecto)
                    var translationsPath = projectPaths.TranslationsPath;
                    if (Directory.Exists(translationsPath))
                    {
                        var translationFiles = Directory.GetFiles(translationsPath, "*.*", SearchOption.AllDirectories);
                        foreach (var transFile in translationFiles)
                        {
                            var relativePath = Path.Combine("translations", Path.GetRelativePath(translationsPath, transFile)).Replace('\\', '/');
                            await AddFileToZipAsync(zipArchive, transFile, relativePath);
                            
                            manifest.Files.Add(new BackupFileEntry
                            {
                                RelativePath = relativePath,
                                Hash = await ComputeFileHashAsync(transFile),
                                SizeBytes = new FileInfo(transFile).Length,
                                ModifiedAt = File.GetLastWriteTimeUtc(transFile)
                            });
                        }
                        
                        if (translationFiles.Length > 0)
                        {
                            _logger.LogInformation("✅ Translations incluidas en backup ({Count} archivos)", translationFiles.Length);
                        }
                    }
                    
                    // Agregar NxLog JSONL Logs (SOC PIVOT TISSEO - TLS_M3_ALS_EXI_CYB_SYS_00514)
                    var logsPath = projectPaths.LogsPath;
                    if (Directory.Exists(logsPath))
                    {
                        var logFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.AllDirectories);
                        foreach (var logFile in logFiles)
                        {
                            var relativePath = Path.Combine("logs", Path.GetRelativePath(logsPath, logFile)).Replace('\\', '/');
                            await AddFileToZipAsync(zipArchive, logFile, relativePath);
                            
                            manifest.Files.Add(new BackupFileEntry
                            {
                                RelativePath = relativePath,
                                Hash = await ComputeFileHashAsync(logFile),
                                SizeBytes = new FileInfo(logFile).Length,
                                ModifiedAt = File.GetLastWriteTimeUtc(logFile)
                            });
                        }
                        
                        if (logFiles.Length > 0)
                        {
                            _logger.LogInformation("✅ NxLog JSONL logs incluidos en backup ({Count} archivos - TISSEO Compliance)", logFiles.Length);
                        }
                    }
                    
                    // Agregar manifest al ZIP
                    var manifestEntry = zipArchive.CreateEntry("manifest.json");
                    using (var stream = manifestEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions);
                    }
                }
                
                // Obtener tamaño del backup
                var zipFileInfo = new FileInfo(zipPath);
                backupInfo.SizeBytes = zipFileInfo.Length;
                
                // Generar certificado si está habilitado
                var config = await GetBackupConfigAsync(projectId);
                if (config.SignEnabled)
                {
                    var certificate = await _certificateService.SignBackupAsync(projectId, backupId, manifest);
                    backupInfo.IsSigned = true;
                    backupInfo.CertificateStatus = CertificateStatus.Valid;
                    backupInfo.Hash = certificate.Signature;
                    
                    // Agregar certificado al ZIP
                    using var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
                    var certEntry = zipArchive.CreateEntry("backup_certificate.json");
                    using var stream = certEntry.Open();
                    await JsonSerializer.SerializeAsync(stream, certificate, JsonOptions);
                }
                
                // Registrar en audit log (en el proyecto correcto)
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupCreate,
                    AuditResult.Success,
                    $"Backup created: {backupName} (Project: {projectId}, Size: {backupInfo.SizeFormatted})",
                    userId ?? "system",
                    projectId: projectId);  // 📁 Guardar en audit del proyecto correcto
                
                // Limpiar backups antiguos
                await CleanupOldBackupsAsync(projectId);
                
                response.Success = true;
                response.Message = $"Backup created successfully: {backupName}";
                response.BackupInfo = backupInfo;
                
                _logger.LogInformation("Backup created successfully: {BackupId}, Size: {Size}", 
                    backupId, backupInfo.SizeFormatted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup for project {ProjectId}", projectId);
                response.Success = false;
                response.Message = "Error creating backup";
                response.Errors.Add(ex.Message);
            }
            
            return response;
        }

        public async Task<BackupOperationResponse> RestoreBackupAsync(string projectId, RestoreBackupRequest request, string? userId = null)
        {
            var response = new BackupOperationResponse();
            var sw = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("⏱️ Restore START — backup {BackupId} for project {ProjectId}", 
                    request.BackupId, projectId);
                
                var projectPaths = GetProjectPaths(projectId);
                var backupInfo = await GetBackupAsync(projectId, request.BackupId);
                
                if (backupInfo == null)
                {
                    response.Success = false;
                    response.Message = "Backup not found";
                    response.Errors.Add($"Backup {request.BackupId} not found");
                    return response;
                }
                
                // Verificar integridad si está firmado (verificación ligera: manifest + certificado, sin SHA256 por fichero)
                if (backupInfo.IsSigned)
                {
                    try
                    {
                        using var checkZip = ZipFile.OpenRead(backupInfo.FilePath);
                        var manifestEntry = checkZip.GetEntry("manifest.json");
                        if (manifestEntry == null)
                        {
                            response.Success = false;
                            response.Message = "Backup integrity verification failed";
                            response.Errors.Add("Manifest missing from backup ZIP");
                            return response;
                        }
                        
                        // Verificar certificado
                        var certEntry = checkZip.GetEntry("backup_certificate.json");
                        if (certEntry != null)
                        {
                            BackupManifest? checkManifest;
                            using (var ms = manifestEntry.Open())
                            {
                                checkManifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms, JsonOptions);
                            }
                            
                            BackupCertificate? certificate;
                            using (var cs = certEntry.Open())
                            {
                                certificate = await JsonSerializer.DeserializeAsync<BackupCertificate>(cs, JsonOptions);
                            }
                            
                            if (checkManifest != null && certificate != null)
                            {
                                var certOk = await _certificateService.VerifyCertificateAsync(certificate, checkManifest);
                                if (!certOk)
                                {
                                    // Certificate from another machine → warning, allow restore
                                    response.Warnings.Add("Backup was signed on a different machine — certificate cannot be verified locally.");
                                    _logger.LogWarning("⚠️ Backup {BackupId} certificate mismatch (imported from different machine) — allowing restore", request.BackupId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Could not verify backup {BackupId} — proceeding with restore", request.BackupId);
                        response.Warnings.Add($"Could not verify backup integrity: {ex.Message}");
                    }
                }
                
                // Crear backup previo si está configurado (solo config+DB, sin modelos/TwinCAT para rapidez)
                if (request.CreateBackupFirst)
                {
                    _logger.LogInformation("⏱️ [{Elapsed}ms] Pre-backup START", sw.ElapsedMilliseconds);
                    var preBackupRequest = new CreateBackupRequest
                    {
                        Description = $"Backup automático antes de restaurar {request.BackupId}",
                        IncludeConfig = request.RestoreConfig,
                        IncludeModels = false,  // Skip models — they're large and rarely change
                        IncludeDatabase = request.RestoreDatabase,
                        IncludeTwinCAT = false,  // Skip TwinCAT — large and machine-specific
                        Type = BackupType.PreRestore
                    };
                    
                    var preBackupResponse = await CreateBackupAsync(projectId, preBackupRequest, userId);
                    _logger.LogInformation("⏱️ [{Elapsed}ms] Pre-backup END", sw.ElapsedMilliseconds);
                    if (!preBackupResponse.Success)
                    {
                        response.Warnings.Add("Could not create pre-restore backup, continuing anyway");
                    }
                }
                
                // Restaurar desde ZIP
                _logger.LogInformation("⏱️ [{Elapsed}ms] Extract START", sw.ElapsedMilliseconds);
                using (var zipArchive = ZipFile.OpenRead(backupInfo.FilePath))
                {
                    var twinCatCleaned = false; // Track if TwinCAT destination was cleaned
                    foreach (var entry in zipArchive.Entries)
                    {
                        // Normalizar separadores a forward slash para comparaciones consistentes
                        var entryPath = entry.FullName.Replace('\\', '/');
                        // Saltar archivos de metadata
                        if (entry.Name == "manifest.json" || entry.Name == "backup_certificate.json")
                            continue;
                        
                        // Saltar entradas de directorio (sin nombre de archivo)
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;
                        
                        // Saltar archivos temporales de Excel (~$) que no deberían haberse incluido
                        if (entry.Name.StartsWith("~$"))
                        {
                            _logger.LogInformation("⏭️ Saltando archivo temporal: {FileName}", entryPath);
                            continue;
                        }
                        
                        var fullPath = Path.Combine(projectPaths.ProjectRoot, entry.FullName);
                        var directory = Path.GetDirectoryName(fullPath);
                        
                        // Determinar si debemos restaurar este archivo
                        bool shouldRestore = false;
                        if (entryPath.StartsWith("config/") && request.RestoreConfig)
                            shouldRestore = true;
                        else if (entryPath.StartsWith("models/") && request.RestoreModels)
                            shouldRestore = true;
                        else if (entryPath.StartsWith("data/") && request.RestoreDatabase)
                            shouldRestore = true;
                        else if (entryPath.StartsWith("sbom/"))
                        {
                            // Siempre restaurar SBOM (EU CRA Compliance)
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando SBOM: {FileName}", entryPath);
                        }
                        else if (entryPath.StartsWith("audit/"))
                        {
                            // Siempre restaurar Audit Logs (EU CRA Compliance)
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando Audit Log: {FileName}", entryPath);
                        }
                        else if (entryPath == "authorized_signing_keys.json")
                        {
                            // Fusionar claves autorizadas del backup con las locales (no sobreescribir)
                            try
                            {
                                using var akStream = entry.Open();
                                var backupKeys = await JsonSerializer.DeserializeAsync<List<AuthorizedKey>>(akStream, JsonOptions) ?? new();
                                
                                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");
                                var localKeys = new List<AuthorizedKey>();
                                if (File.Exists(localPath))
                                {
                                    var localJson = await File.ReadAllTextAsync(localPath);
                                    localKeys = JsonSerializer.Deserialize<List<AuthorizedKey>>(localJson, JsonOptions) ?? new();
                                }
                                
                                // Merge: añadir claves del backup que no existan localmente (por fingerprint)
                                var localFingerprints = new HashSet<string>(localKeys.Select(k => k.Fingerprint), StringComparer.OrdinalIgnoreCase);
                                var added = 0;
                                foreach (var bk in backupKeys)
                                {
                                    if (!localFingerprints.Contains(bk.Fingerprint))
                                    {
                                        localKeys.Add(bk);
                                        added++;
                                    }
                                    else
                                    {
                                        // Actualizar campos vacíos en la clave local con datos del backup
                                        var local = localKeys.First(k => k.Fingerprint.Equals(bk.Fingerprint, StringComparison.OrdinalIgnoreCase));
                                        var updated = false;
                                        if (string.IsNullOrEmpty(local.PublicKey) && !string.IsNullOrEmpty(bk.PublicKey))
                                        {
                                            local.PublicKey = bk.PublicKey;
                                            updated = true;
                                        }
                                        if (string.IsNullOrEmpty(local.OwnerEmail) && !string.IsNullOrEmpty(bk.OwnerEmail))
                                        {
                                            local.OwnerEmail = bk.OwnerEmail;
                                            updated = true;
                                        }
                                        if (string.IsNullOrEmpty(local.OwnerName) && !string.IsNullOrEmpty(bk.OwnerName))
                                        {
                                            local.OwnerName = bk.OwnerName;
                                            updated = true;
                                        }
                                        if (string.IsNullOrEmpty(local.MachineName) && !string.IsNullOrEmpty(bk.MachineName))
                                        {
                                            local.MachineName = bk.MachineName;
                                            updated = true;
                                        }
                                        if (updated) added++;
                                    }
                                }
                                
                                if (added > 0)
                                {
                                    var mergedJson = JsonSerializer.Serialize(localKeys, JsonOptions);
                                    await File.WriteAllTextAsync(localPath, mergedJson);
                                    _logger.LogInformation("✅ Fusionadas {Count} claves autorizadas del backup", added);
                                    response.Warnings.Add($"Merged {added} authorized signing key(s) from backup");
                                }
                                else
                                {
                                    _logger.LogInformation("ℹ️ authorized_signing_keys.json del backup — sin claves nuevas");
                                }
                                
                                // Reconstruir allowed_signers inmediatamente (sin esperar commit)
                                // Esto es necesario para servidores que solo hacen push (nunca commit)
                                try
                                {
                                    await RebuildAllowedSignersAsync(localKeys);
                                }
                                catch (Exception asEx)
                                {
                                    _logger.LogWarning(asEx, "⚠️ Could not rebuild allowed_signers after restore");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Could not merge authorized_signing_keys.json from backup");
                            }
                        }
                        else if (entryPath == "deploy-version.json")
                        {
                            // deploy-version.json NO se restaura - refleja la version del servidor, no del backup
                            _logger.LogInformation("⏭️ Saltando deploy-version.json (version del servidor no se sobreescribe)");
                        }
                        else if (entryPath == "README.md")
                        {
                            // Siempre restaurar documentacion
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando {FileName}", entryPath);
                        }
                        else if (entryPath.StartsWith("logs/"))
                        {
                            // Siempre restaurar NxLog JSONL logs (TISSEO Compliance)
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando NxLog JSONL: {FileName}", entryPath);
                        }
                        else if (entryPath.StartsWith("translations/"))
                        {
                            // Siempre restaurar Translations
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando Translation: {FileName}", entryPath);
                        }
                        else if (entryPath.StartsWith("twincat/") && request.RestoreTwinCAT)
                        {
                            // TwinCAT se restaura a su carpeta propia (fuera de ProjectRoot)
                            if (!string.IsNullOrEmpty(projectPaths.TwinCatPath))
                            {
                                // Clean destination ONCE before restoring (exact 1:1 copy)
                                if (!twinCatCleaned && Directory.Exists(projectPaths.TwinCatPath))
                                {
                                    _logger.LogInformation("🧹 Cleaning TwinCAT destination before restore: {Path}", projectPaths.TwinCatPath);
                                    foreach (var dir in Directory.GetDirectories(projectPaths.TwinCatPath))
                                    {
                                        try
                                        {
                                            // Clear read-only attributes recursively (git objects are read-only)
                                            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                                                File.SetAttributes(f, FileAttributes.Normal);
                                            Directory.Delete(dir, true);
                                        }
                                        catch (Exception ex) { _logger.LogWarning("⚠️ Could not delete dir {Dir}: {Err}", dir, ex.Message); }
                                    }
                                    foreach (var file in Directory.GetFiles(projectPaths.TwinCatPath))
                                    {
                                        try
                                        {
                                            File.SetAttributes(file, FileAttributes.Normal);
                                            File.Delete(file);
                                        }
                                        catch (Exception ex) { _logger.LogWarning("⚠️ Could not delete file {File}: {Err}", file, ex.Message); }
                                    }
                                    twinCatCleaned = true;
                                }

                                var twinCatRelative = entryPath.Substring("twincat/".Length);

                                // Full restore — no exclusions, restore everything as-is
                                try
                                {
                                    var twinCatFullPath = Path.Combine(projectPaths.TwinCatPath, twinCatRelative);
                                    var twinCatDir = Path.GetDirectoryName(twinCatFullPath);
                                    if (!string.IsNullOrEmpty(twinCatDir))
                                    {
                                        Directory.CreateDirectory(twinCatDir);
                                    }
                                    // Clear read-only attribute before overwrite (.git/objects/ are read-only)
                                    if (File.Exists(twinCatFullPath))
                                    {
                                        File.SetAttributes(twinCatFullPath, FileAttributes.Normal);
                                    }
                                    entry.ExtractToFile(twinCatFullPath, overwrite: true);
                                    _logger.LogInformation("✅ Restaurando TwinCAT: {FileName}", entryPath);
                                }
                                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                                {
                                    response.Warnings.Add($"Could not overwrite TwinCAT file: {entryPath}");
                                    _logger.LogWarning("⚠️ Skipping TwinCAT file during restore: {File} — {Error}", entryPath, ex.Message);
                                }
                            }
                            // Skip normal restore flow — already handled
                            continue;
                        }
                        
                        if (shouldRestore)
                        {
                            try
                            {
                                // Crear directorio si existe, o extraer directamente en raíz
                                if (!string.IsNullOrEmpty(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }
                                entry.ExtractToFile(fullPath, overwrite: true);
                            }
                            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                            {
                                // Archivo bloqueado o sin permisos — warning, no abortar
                                response.Warnings.Add($"Could not overwrite file: {entryPath}");
                                _logger.LogWarning("⚠️ Skipping file during restore: {File} — {Error}", entryPath, ex.Message);
                            }
                        }
                    }
                }
                
                // Registrar en audit log (en el proyecto correcto)
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupRestore,
                    AuditResult.Success,
                    $"Backup restored: {backupInfo.Name} (Project: {projectId})",
                    userId ?? "system",
                    projectId: projectId);  // 📁 Guardar en audit del proyecto correcto
                
                response.Success = true;
                response.Message = $"Backup restored successfully: {backupInfo.Name}";
                response.BackupInfo = backupInfo;
                
                _logger.LogInformation("⏱️ [{Elapsed}ms] Restore COMPLETE — {BackupId}", sw.ElapsedMilliseconds, request.BackupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring backup {BackupId} for project {ProjectId}", 
                    request.BackupId, projectId);
                response.Success = false;
                response.Message = "Error restoring backup";
                response.Errors.Add(ex.Message);
            }
            
            return response;
        }

        public async Task<BackupListResponse> ListBackupsAsync(string projectId)
        {
            var response = new BackupListResponse
            {
                Config = await GetBackupConfigAsync(projectId)
            };
            
            try
            {
                var projectPaths = GetProjectPaths(projectId);
                var backupsDir = projectPaths.BackupsPath;
                
                if (!Directory.Exists(backupsDir))
                {
                    return response;
                }
                
                var zipFiles = Directory.GetFiles(backupsDir, "*.zip");
                
                foreach (var zipFile in zipFiles)
                {
                    try
                    {
                        var backupInfo = await ExtractBackupInfoFromZip(zipFile, projectId);
                        if (backupInfo != null)
                        {
                            response.Backups.Add(backupInfo);
                            response.TotalSizeBytes += backupInfo.SizeBytes;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading backup file: {File}", zipFile);
                    }
                }
                
                // Ordenar por fecha descendente
                response.Backups = response.Backups.OrderByDescending(b => b.CreatedAt).ToList();
                response.TotalCount = response.Backups.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing backups for project {ProjectId}", projectId);
            }
            
            return response;
        }

        public async Task<BackupInfo?> GetBackupAsync(string projectId, string backupId)
        {
            var projectPaths = GetProjectPaths(projectId);
            var zipPath = Path.Combine(projectPaths.BackupsPath, $"{backupId}.zip");
            
            // Si no existe con el nombre exacto, buscar en todos los archivos del directorio
            // Esto soporta backups creados con formato antiguo (sin projectId en el nombre)
            if (!File.Exists(zipPath))
            {
                _logger.LogDebug("Backup not found at {Path}, searching in directory...", zipPath);
                
                if (Directory.Exists(projectPaths.BackupsPath))
                {
                    // Buscar archivo que coincida con el backupId (puede ser parcial)
                    var allZips = Directory.GetFiles(projectPaths.BackupsPath, "*.zip");
                    var matchingZip = allZips.FirstOrDefault(f => 
                        Path.GetFileNameWithoutExtension(f).Equals(backupId, StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileNameWithoutExtension(f).Contains(backupId, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingZip != null)
                    {
                        _logger.LogDebug("Found matching backup: {Path}", matchingZip);
                        zipPath = matchingZip;
                    }
                    else
                    {
                        _logger.LogWarning("No backup found matching {BackupId} in {Path}", backupId, projectPaths.BackupsPath);
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            
            return await ExtractBackupInfoFromZip(zipPath, projectId);
        }

        public async Task<BackupOperationResponse> DeleteBackupAsync(string projectId, string backupId)
        {
            var response = new BackupOperationResponse();
            
            try
            {
                var backupInfo = await GetBackupAsync(projectId, backupId);
                if (backupInfo == null)
                {
                    response.Success = false;
                    response.Message = "Backup not found";
                    return response;
                }
                
                File.Delete(backupInfo.FilePath);
                
                // Registrar en audit log (en el proyecto correcto)
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupDelete,
                    AuditResult.Success,
                    $"Backup deleted: {backupInfo.Name} (Project: {projectId})",
                    projectId: projectId);  // 📁 Guardar en audit del proyecto correcto
                
                response.Success = true;
                response.Message = $"Backup deleted: {backupInfo.Name}";
                
                _logger.LogInformation("Backup deleted: {BackupId}", backupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting backup {BackupId}", backupId);
                response.Success = false;
                response.Message = "Error deleting backup";
                response.Errors.Add(ex.Message);
            }
            
            return response;
        }

        public async Task<BackupVerificationResponse> VerifyBackupAsync(string projectId, string backupId)
        {
            var response = new BackupVerificationResponse();
            
            try
            {
                var backupInfo = await GetBackupAsync(projectId, backupId);
                if (backupInfo == null)
                {
                    response.IsValid = false;
                    response.CertificateStatus = CertificateStatus.Error;
                    response.Details.Add(new VerificationDetail
                    {
                        Component = "Backup",
                        IsValid = false,
                        Message = "Backup not found"
                    });
                    return response;
                }
                
                using var zipArchive = ZipFile.OpenRead(backupInfo.FilePath);
                
                // Leer manifest
                var manifestEntry = zipArchive.GetEntry("manifest.json");
                if (manifestEntry == null)
                {
                    response.IsValid = false;
                    response.Details.Add(new VerificationDetail
                    {
                        Component = "Manifest",
                        IsValid = false,
                        Message = "Manifest not found in backup"
                    });
                    return response;
                }
                
                BackupManifest? manifest;
                using (var stream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions);
                }
                
                if (manifest == null)
                {
                    response.IsValid = false;
                    response.Details.Add(new VerificationDetail
                    {
                        Component = "Manifest",
                        IsValid = false,
                        Message = "Invalid manifest format"
                    });
                    return response;
                }
                
                response.Details.Add(new VerificationDetail
                {
                    Component = "Manifest",
                    IsValid = true,
                    Message = "Manifest is valid"
                });
                
                // Verificar certificado si existe
                var certEntry = zipArchive.GetEntry("backup_certificate.json");
                if (certEntry != null)
                {
                    BackupCertificate? certificate;
                    using (var stream = certEntry.Open())
                    {
                        certificate = await JsonSerializer.DeserializeAsync<BackupCertificate>(stream, JsonOptions);
                    }
                    
                    if (certificate != null)
                    {
                        var certVerification = await _certificateService.VerifyCertificateAsync(certificate, manifest);
                        response.CertificateStatus = certVerification ? CertificateStatus.Valid : CertificateStatus.Invalid;
                        
                        response.Details.Add(new VerificationDetail
                        {
                            Component = "Certificate",
                            IsValid = certVerification,
                            Message = certVerification ? "Certificate signature is valid" : "Certificate signature mismatch"
                        });
                    }
                }
                else
                {
                    response.CertificateStatus = CertificateStatus.NotSigned;
                    response.Details.Add(new VerificationDetail
                    {
                        Component = "Certificate",
                        IsValid = true,
                        Message = "Backup is not signed"
                    });
                }
                
                // Verificar archivos
                // Build a normalized lookup for ZIP entries (handles both \ and / in old/new backups)
                var zipEntryLookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zipArchive.Entries)
                {
                    var key = entry.FullName.Replace('\\', '/');
                    zipEntryLookup.TryAdd(key, entry);
                }
                
                int validFiles = 0;
                int invalidFiles = 0;
                
                foreach (var fileEntry in manifest.Files)
                {
                    var normalizedPath = fileEntry.RelativePath.Replace('\\', '/');
                    zipEntryLookup.TryGetValue(normalizedPath, out var zipEntry);
                    if (zipEntry == null)
                    {
                        invalidFiles++;
                        response.Details.Add(new VerificationDetail
                        {
                            Component = $"File: {fileEntry.RelativePath}",
                            IsValid = false,
                            ExpectedHash = fileEntry.Hash,
                            Message = "File missing from backup"
                        });
                        continue;
                    }
                    
                    // Verificar hash
                    using var stream = zipEntry.Open();
                    var actualHash = await ComputeStreamHashAsync(stream);
                    
                    if (actualHash == fileEntry.Hash)
                    {
                        validFiles++;
                    }
                    else
                    {
                        invalidFiles++;
                        response.Details.Add(new VerificationDetail
                        {
                            Component = $"File: {fileEntry.RelativePath}",
                            IsValid = false,
                            ExpectedHash = fileEntry.Hash,
                            ActualHash = actualHash,
                            Message = "File hash mismatch"
                        });
                    }
                }
                
                response.Details.Add(new VerificationDetail
                {
                    Component = "Files",
                    IsValid = invalidFiles == 0,
                    Message = $"Verified {validFiles} files, {invalidFiles} invalid"
                });
                
                response.IsValid = invalidFiles == 0 && 
                    (response.CertificateStatus == CertificateStatus.Valid || 
                     response.CertificateStatus == CertificateStatus.NotSigned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying backup {BackupId}", backupId);
                response.IsValid = false;
                response.CertificateStatus = CertificateStatus.Error;
                response.Details.Add(new VerificationDetail
                {
                    Component = "Verification",
                    IsValid = false,
                    Message = ex.Message
                });
            }
            
            return response;
        }

        public Task<string?> GetBackupFilePathAsync(string projectId, string backupId)
        {
            var projectPaths = GetProjectPaths(projectId);
            var zipPath = Path.Combine(projectPaths.BackupsPath, $"{backupId}.zip");
            
            return Task.FromResult(File.Exists(zipPath) ? zipPath : null);
        }

        public async Task<BackupOperationResponse> ImportBackupAsync(string projectId, Stream fileStream, string fileName)
        {
            var response = new BackupOperationResponse();
            
            try
            {
                var projectPaths = GetProjectPaths(projectId);
                Directory.CreateDirectory(projectPaths.BackupsPath);
                
                // Generar nombre único si ya existe
                var targetPath = Path.Combine(projectPaths.BackupsPath, fileName);
                if (File.Exists(targetPath))
                {
                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    targetPath = Path.Combine(projectPaths.BackupsPath, 
                        $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                
                // Copiar archivo
                using (var targetStream = File.Create(targetPath))
                {
                    await fileStream.CopyToAsync(targetStream);
                }
                
                // Verificar que es un backup válido
                var backupInfo = await ExtractBackupInfoFromZip(targetPath, projectId);
                if (backupInfo == null)
                {
                    File.Delete(targetPath);
                    response.Success = false;
                    response.Message = "Invalid backup file format";
                    return response;
                }
                
                // Marcar como importado — reescribir manifest dentro del ZIP
                backupInfo.Type = BackupType.Imported;
                try
                {
                    using var zipArchive = ZipFile.Open(targetPath, ZipArchiveMode.Update);
                    var manifestEntry = zipArchive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        using var stream = manifestEntry.Open();
                        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions);
                        if (manifest?.BackupInfo != null)
                        {
                            manifest.BackupInfo.Type = BackupType.Imported;
                            stream.SetLength(0);
                            stream.Position = 0;
                            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update manifest type to Imported — cosmetic only");
                }
                
                // Registrar en audit log (en el proyecto correcto)
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupCreate,
                    AuditResult.Success,
                    $"Backup imported: {fileName} (Project: {projectId})",
                    projectId: projectId);  // 📁 Guardar en audit del proyecto correcto
                
                response.Success = true;
                response.Message = $"Backup imported: {backupInfo.Name}";
                response.BackupInfo = backupInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing backup for project {ProjectId}", projectId);
                response.Success = false;
                response.Message = "Error importing backup";
                response.Errors.Add(ex.Message);
            }
            
            return response;
        }

        public async Task<BackupConfig> GetBackupConfigAsync(string projectId)
        {
            // TODO: Leer configuración desde Excel SystemConfig cuando se agreguen los campos
            // Por ahora devolvemos valores por defecto sensatos
            try
            {
                var projectPaths = GetProjectPaths(projectId);
                var excelPath = Path.Combine(projectPaths.ConfigPath, "ProjectConfig.xlsm");
                
                // Si no existe el Excel, devolver defaults con backup DESHABILITADO
                // para no crear carpetas innecesarias en producción
                if (!File.Exists(excelPath))
                {
                    _logger.LogDebug("Excel config not found for project {ProjectId}, returning disabled backup config", projectId);
                    return new BackupConfig
                    {
                        Enabled = false, // DESHABILITADO por defecto hasta que se configure explícitamente
                        IntervalHours = 0,
                        RetentionDays = 30,
                        SignEnabled = true,
                        RemoteEnabled = false,
                        RemoteUrl = null,
                        RemoteApiKey = null,
                        BackupBeforeRestore = true,
                        MaxBackups = 10
                    };
                }
                
                // Valores por defecto hasta que se implementen los campos en Excel
                // Si el Excel existe, habilitamos backup con valores razonables
                return new BackupConfig
                {
                    Enabled = true,
                    IntervalHours = 24,
                    RetentionDays = 30,
                    SignEnabled = true,
                    RemoteEnabled = false,
                    RemoteUrl = null,
                    RemoteApiKey = null,
                    BackupBeforeRestore = true,
                    MaxBackups = 10
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading backup config, using defaults");;
                return new BackupConfig();
            }
        }

        public async Task<BackupSystemStatus> GetSystemStatusAsync(string projectId)
        {
            var config = await GetBackupConfigAsync(projectId);
            var status = new BackupSystemStatus
            {
                Config = config
            };
            
            try
            {
                // Quick status without opening ZIP files — just count files and get dates from filesystem
                var projectPaths = GetProjectPaths(projectId);
                var backupsDir = projectPaths.BackupsPath;
                
                if (Directory.Exists(backupsDir))
                {
                    var zipFiles = Directory.GetFiles(backupsDir, "*.zip");
                    status.TotalBackups = zipFiles.Length;
                    status.UsedSpaceBytes = zipFiles.Sum(f => new FileInfo(f).Length);
                    
                    // Get most recent backup from filesystem date (fast, no ZIP open)
                    if (zipFiles.Length > 0)
                    {
                        var newestZip = zipFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
                        status.LastBackup = await ExtractBackupInfoFromZip(newestZip, projectId);
                    }
                }

                status.Enabled = config.Enabled;
                
                if (config.IntervalHours > 0 && status.LastBackup != null)
                {
                    status.NextScheduledBackup = status.LastBackup.CreatedAt
                        .AddHours(config.IntervalHours);
                }
                
                // Determinar estado de salud
                if (!status.Enabled)
                {
                    status.HealthStatus = "DISABLED";
                    status.StatusMessages.Add("Backup system is disabled");
                }
                else if (status.TotalBackups == 0)
                {
                    status.HealthStatus = "WARNING";
                    status.StatusMessages.Add("No backups available");
                }
                else if (status.LastBackup != null && 
                         status.LastBackup.CreatedAt < DateTime.Now.AddDays(-7))
                {
                    status.HealthStatus = "WARNING";
                    status.StatusMessages.Add("Last backup is more than 7 days old");
                }
                else
                {
                    status.HealthStatus = "OK";
                    status.StatusMessages.Add("Backup system is healthy");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup system status");
                status.HealthStatus = "ERROR";
                status.StatusMessages.Add($"Error: {ex.Message}");
            }
            
            return status;
        }

        public async Task<int> CleanupOldBackupsAsync(string projectId)
        {
            int deleted = 0;
            
            try
            {
                var config = await GetBackupConfigAsync(projectId);
                var list = await ListBackupsAsync(projectId);
                
                // Ordenar por fecha (más recientes primero)
                var backups = list.Backups.OrderByDescending(b => b.CreatedAt).ToList();
                
                // Eliminar por cantidad máxima
                if (config.MaxBackups > 0 && backups.Count > config.MaxBackups)
                {
                    var toDelete = backups.Skip(config.MaxBackups);
                    foreach (var backup in toDelete)
                    {
                        await DeleteBackupAsync(projectId, backup.Id);
                        deleted++;
                    }
                }
                
                // Eliminar por antigüedad
                if (config.RetentionDays > 0)
                {
                    var cutoffDate = DateTime.Now.AddDays(-config.RetentionDays);
                    var oldBackups = backups.Where(b => b.CreatedAt < cutoffDate);
                    
                    foreach (var backup in oldBackups)
                    {
                        // No eliminar si ya fue eliminado por cantidad
                        if (File.Exists(backup.FilePath))
                        {
                            await DeleteBackupAsync(projectId, backup.Id);
                            deleted++;
                        }
                    }
                }
                
                if (deleted > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old backups for project {ProjectId}", 
                        deleted, projectId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old backups");
            }
            
            return deleted;
        }

        // ==================== Helper Methods ====================

        private async Task<BackupInfo?> ExtractBackupInfoFromZip(string zipPath, string projectId)
        {
            try
            {
                using var zipArchive = ZipFile.OpenRead(zipPath);
                var manifestEntry = zipArchive.GetEntry("manifest.json");
                
                if (manifestEntry == null)
                {
                    // Backup sin manifest - crear info básica
                    var fileInfo = new FileInfo(zipPath);
                    return new BackupInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(zipPath),
                        ProjectId = projectId,
                        Name = Path.GetFileNameWithoutExtension(zipPath),
                        CreatedAt = fileInfo.CreationTimeUtc,
                        SizeBytes = fileInfo.Length,
                        FilePath = zipPath,
                        IsSigned = false,
                        CertificateStatus = CertificateStatus.NotSigned
                    };
                }
                
                BackupManifest? manifest;
                using (var stream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions);
                }
                
                if (manifest?.BackupInfo == null)
                    return null;
                
                var backupInfo = manifest.BackupInfo;
                backupInfo.FilePath = zipPath;
                backupInfo.SizeBytes = new FileInfo(zipPath).Length;
                
                // Siempre usar el nombre del archivo ZIP como ID (garantiza unicidad tras import/rename)
                backupInfo.Id = Path.GetFileNameWithoutExtension(zipPath);
                
                // Verificar si tiene certificado
                var certEntry = zipArchive.GetEntry("backup_certificate.json");
                backupInfo.IsSigned = certEntry != null;
                backupInfo.CertificateStatus = certEntry != null 
                    ? CertificateStatus.Valid  // Se verificará al restaurar
                    : CertificateStatus.NotSigned;
                
                return backupInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting backup info from {ZipPath}", zipPath);
                return null;
            }
        }

        /// <summary>
        /// Agrega un archivo al ZIP usando FileShare.ReadWrite para evitar IOException cuando
        /// el archivo está en uso por otro proceso (ej: ExcelConfigService tiene ProjectConfig.xlsm abierto).
        /// Reemplaza a zipArchive.CreateEntryFromFile() que abre en modo exclusivo.
        /// </summary>
        private static async Task AddFileToZipAsync(ZipArchive zipArchive, string sourceFilePath, string entryName)
        {
            // Normalizar separadores a forward slash (estándar ZIP)
            var normalizedName = entryName.Replace('\\', '/');
            var entry = zipArchive.CreateEntry(normalizedName);
            using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var entryStream = entry.Open();
            await sourceStream.CopyToAsync(entryStream);
        }

        /// <summary>
        /// Reconstruye el archivo allowed_signers de git SSH a partir de las claves autorizadas.
        /// Necesario tras restore para que git pueda verificar firmas de otros servidores.
        /// </summary>
        private async Task RebuildAllowedSignersAsync(List<AuthorizedKey> keys)
        {
            // Detectar la ruta del allowed_signers desde git config
            var psi = new ProcessStartInfo("git", "config --global gpg.ssh.allowedSignersFile")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            string? allowedPath = null;
            try
            {
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    allowedPath = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                    await proc.WaitForExitAsync();
                }
            }
            catch { /* git not configured for SSH signing */ }
            
            // Si no hay allowedSignersFile configurado, intentar detectar la ruta del signingkey
            if (string.IsNullOrEmpty(allowedPath) || !File.Exists(allowedPath))
            {
                psi.Arguments = "config --global user.signingkey";
                try
                {
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var keyPath = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                        await proc.WaitForExitAsync();
                        if (!string.IsNullOrEmpty(keyPath))
                        {
                            var sshDir = Path.GetDirectoryName(keyPath);
                            if (!string.IsNullOrEmpty(sshDir))
                                allowedPath = Path.Combine(sshDir, "allowed_signers");
                        }
                    }
                }
                catch { }
            }
            
            if (string.IsNullOrEmpty(allowedPath))
            {
                // Servidor sin SSH signing configurado — pero puede necesitar verificar firmas de otros servidores
                // Crear allowed_signers en la carpeta .ssh del perfil del servicio
                var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                if (string.IsNullOrEmpty(sshDir) || sshDir == "\\.ssh")
                {
                    // Fallback para LocalSystem: usar systemprofile
                    sshDir = @"C:\Windows\system32\config\systemprofile\.ssh";
                }
                allowedPath = Path.Combine(sshDir, "allowed_signers");
                _logger.LogInformation("ℹ️ SSH signing not configured locally — will create allowed_signers for verification: {Path}", allowedPath);
            }
            
            // Obtener email de git
            psi.Arguments = "config --global user.email";
            var email = "electronico@aquafrisch.com";
            try
            {
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var result = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                    await proc.WaitForExitAsync();
                    if (!string.IsNullOrEmpty(result)) email = result;
                }
            }
            catch { }
            
            // Construir allowed_signers con todas las claves que tienen PublicKey
            var signerLines = new HashSet<string>();
            
            // Clave local del servidor
            psi.Arguments = "config --global user.signingkey";
            try
            {
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var keyPath = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                    await proc.WaitForExitAsync();
                    if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
                    {
                        var localPubKey = (await File.ReadAllTextAsync(keyPath)).Trim();
                        signerLines.Add($"{email} namespaces=\"git\" {localPubKey}");
                    }
                }
            }
            catch { }
            
            // Todas las claves autorizadas con PublicKey
            foreach (var ak in keys.Where(k => !string.IsNullOrEmpty(k.PublicKey)))
            {
                var akEmail = !string.IsNullOrEmpty(ak.OwnerEmail) ? ak.OwnerEmail : email;
                signerLines.Add($"{akEmail} namespaces=\"git\" {ak.PublicKey}");
            }
            
            if (signerLines.Count > 0)
            {
                var dir = Path.GetDirectoryName(allowedPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                
                var newContent = string.Join("\n", signerLines) + "\n";
                await File.WriteAllTextAsync(allowedPath, newContent);
                _logger.LogInformation("✅ Rebuilt allowed_signers with {Count} key(s): {Path}", signerLines.Count, allowedPath);
                
                // Asegurar que git config apunta al archivo (necesario para verificar firmas)
                try
                {
                    var cfgPsi = new ProcessStartInfo("git", $"config --global gpg.ssh.allowedSignersFile \"{allowedPath}\"")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var cfgProc = Process.Start(cfgPsi);
                    if (cfgProc != null) await cfgProc.WaitForExitAsync();
                    
                    // También asegurar gpg.format=ssh para que git entienda las firmas
                    cfgPsi.Arguments = "config --global gpg.format ssh";
                    using var fmtProc = Process.Start(cfgPsi);
                    if (fmtProc != null) await fmtProc.WaitForExitAsync();
                    
                    _logger.LogInformation("✅ Git configured: gpg.format=ssh, allowedSignersFile={Path}", allowedPath);
                }
                catch (Exception cfgEx)
                {
                    _logger.LogWarning(cfgEx, "⚠️ Could not set git gpg.ssh.allowedSignersFile config");
                }
            }
        }

        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            // Usar FileShare.ReadWrite para no fallar si el archivo está en uso (ej: Excel abierto por EPPlus)
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task<string> ComputeStreamHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool IsModelFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".glb" or ".gltf" or ".obj" or ".fbx" or ".mtl" or ".babylon";
        }

        private string? GetAppVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
    }
}
