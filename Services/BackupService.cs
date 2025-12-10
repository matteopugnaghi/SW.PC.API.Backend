// ==================================================================
// Services/BackupService.cs
// DATA MANAGEMENT - Servicio Principal de Backup/Restore
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using System.IO.Compression;
using System.Security.Cryptography;
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
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _projectContext = projectContext;
            _certificateService = certificateService;
            _excelConfig = excelConfig;
            _auditLog = auditLog;
            _environment = environment;
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
                    AuditPath = Path.Combine(webRoot, "audit")          // Legacy: wwwroot/audit
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
                AuditPath = Path.Combine(projectRoot, "audit")          // Multi-proyecto: Projects/{id}/audit
            };
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
                var timestamp = DateTime.UtcNow;
                var backupId = $"backup_{projectId}_{timestamp:yyyyMMdd_HHmmss}";
                
                // Nombre automático: [Proyecto] - Fecha Hora (o nombre personalizado + fecha)
                string backupName;
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    // Si el usuario puso nombre, añadir proyecto y fecha
                    backupName = $"[{projectId}] {request.Name} - {timestamp:yyyy-MM-dd HH:mm}";
                }
                else
                {
                    // Nombre automático con proyecto y fecha
                    backupName = $"[{projectId}] Backup {timestamp:yyyy-MM-dd HH:mm}";
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
                    Type = BackupType.Manual,
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
                            var configFiles = Directory.GetFiles(configPath, "*.*", SearchOption.AllDirectories);
                            foreach (var file in configFiles)
                            {
                                var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, file);
                                var entry = zipArchive.CreateEntryFromFile(file, relativePath);
                                
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
                                var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, file);
                                zipArchive.CreateEntryFromFile(file, relativePath);
                                
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
                            var relativePath = Path.GetRelativePath(projectPaths.ProjectRoot, dbPath);
                            
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
                                
                                zipArchive.CreateEntryFromFile(tempDbPath, relativePath);
                                
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
                    
                    // Agregar deploy-version.json (trazabilidad de versión - EU CRA)
                    var deployVersionPath = Path.Combine(projectPaths.ProjectRoot, "deploy-version.json");
                    if (File.Exists(deployVersionPath))
                    {
                        var relativePath = "deploy-version.json";
                        zipArchive.CreateEntryFromFile(deployVersionPath, relativePath);
                        
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
                        zipArchive.CreateEntryFromFile(readmePath, relativePath);
                        
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
                            var relativePath = Path.Combine("sbom", Path.GetRelativePath(sbomPath, sbomFile));
                            zipArchive.CreateEntryFromFile(sbomFile, relativePath);
                            
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
                    var auditPath = projectPaths.AuditPath;
                    if (Directory.Exists(auditPath))
                    {
                        var auditFiles = Directory.GetFiles(auditPath, "*.json", SearchOption.AllDirectories);
                        foreach (var auditFile in auditFiles)
                        {
                            var relativePath = Path.Combine("audit", Path.GetRelativePath(auditPath, auditFile));
                            zipArchive.CreateEntryFromFile(auditFile, relativePath);
                            
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
                
                // Registrar en audit log
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupCreate,
                    AuditResult.Success,
                    $"Backup created: {backupName} (Project: {projectId}, Size: {backupInfo.SizeFormatted})",
                    userId ?? "system");
                
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
            
            try
            {
                _logger.LogInformation("Restoring backup {BackupId} for project {ProjectId}", 
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
                
                // Verificar integridad si está firmado
                if (backupInfo.IsSigned)
                {
                    var verification = await VerifyBackupAsync(projectId, request.BackupId);
                    if (!verification.IsValid)
                    {
                        response.Success = false;
                        response.Message = "Backup integrity verification failed";
                        response.Errors.Add("The backup certificate is invalid or the content has been modified");
                        return response;
                    }
                }
                
                // Crear backup previo si está configurado
                if (request.CreateBackupFirst)
                {
                    var preBackupRequest = new CreateBackupRequest
                    {
                        Name = $"Pre-Restore",
                        Description = $"Backup automático antes de restaurar {request.BackupId}",
                        IncludeConfig = request.RestoreConfig,
                        IncludeModels = request.RestoreModels,
                        IncludeDatabase = request.RestoreDatabase
                    };
                    
                    var preBackupResponse = await CreateBackupAsync(projectId, preBackupRequest, userId);
                    if (!preBackupResponse.Success)
                    {
                        response.Warnings.Add("Could not create pre-restore backup, continuing anyway");
                    }
                }
                
                // Restaurar desde ZIP
                using (var zipArchive = ZipFile.OpenRead(backupInfo.FilePath))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        // Saltar archivos de metadata
                        if (entry.Name == "manifest.json" || entry.Name == "backup_certificate.json")
                            continue;
                        
                        var fullPath = Path.Combine(projectPaths.ProjectRoot, entry.FullName);
                        var directory = Path.GetDirectoryName(fullPath);
                        
                        // Determinar si debemos restaurar este archivo
                        bool shouldRestore = false;
                        if (entry.FullName.StartsWith("config/") && request.RestoreConfig)
                            shouldRestore = true;
                        else if (entry.FullName.StartsWith("models/") && request.RestoreModels)
                            shouldRestore = true;
                        else if (entry.FullName.StartsWith("data/") && request.RestoreDatabase)
                            shouldRestore = true;
                        else if (entry.FullName.StartsWith("sbom/"))
                        {
                            // Siempre restaurar SBOM (EU CRA Compliance)
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando SBOM: {FileName}", entry.FullName);
                        }
                        else if (entry.FullName.StartsWith("audit/"))
                        {
                            // Siempre restaurar Audit Logs (EU CRA Compliance)
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando Audit Log: {FileName}", entry.FullName);
                        }
                        else if (entry.FullName == "deploy-version.json" || entry.FullName == "README.md")
                        {
                            // Siempre restaurar archivos de trazabilidad y documentación
                            shouldRestore = true;
                            _logger.LogInformation("✅ Restaurando {FileName}", entry.FullName);
                        }
                        
                        if (shouldRestore)
                        {
                            // Crear directorio si existe, o extraer directamente en raíz
                            if (!string.IsNullOrEmpty(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }
                            entry.ExtractToFile(fullPath, overwrite: true);
                        }
                    }
                }
                
                // Registrar en audit log
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupRestore,
                    AuditResult.Success,
                    $"Backup restored: {backupInfo.Name} (Project: {projectId})",
                    userId ?? "system");
                
                response.Success = true;
                response.Message = $"Backup restored successfully: {backupInfo.Name}";
                response.BackupInfo = backupInfo;
                
                _logger.LogInformation("Backup restored successfully: {BackupId}", request.BackupId);
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
                
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupDelete,
                    AuditResult.Success,
                    $"Backup deleted: {backupInfo.Name} (Project: {projectId})");
                
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
                int validFiles = 0;
                int invalidFiles = 0;
                
                foreach (var fileEntry in manifest.Files)
                {
                    var zipEntry = zipArchive.GetEntry(fileEntry.RelativePath);
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
                        $"{baseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{ext}");
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
                
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupCreate,
                    AuditResult.Success,
                    $"Backup imported: {fileName} (Project: {projectId})");
                
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
                
                // Si no existe el Excel, devolver defaults
                if (!File.Exists(excelPath))
                {
                    _logger.LogDebug("Excel config not found for project {ProjectId}, using backup defaults", projectId);
                }
                
                // Valores por defecto hasta que se implementen los campos en Excel
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
                _logger.LogWarning(ex, "Error reading backup config, using defaults");
                return new BackupConfig();
            }
        }

        public async Task<BackupSystemStatus> GetSystemStatusAsync(string projectId)
        {
            var status = new BackupSystemStatus
            {
                Config = await GetBackupConfigAsync(projectId)
            };
            
            try
            {
                var list = await ListBackupsAsync(projectId);
                status.Enabled = status.Config.Enabled;
                status.TotalBackups = list.TotalCount;
                status.UsedSpaceBytes = list.TotalSizeBytes;
                status.LastBackup = list.Backups.FirstOrDefault();
                
                if (status.Config.IntervalHours > 0 && status.LastBackup != null)
                {
                    status.NextScheduledBackup = status.LastBackup.CreatedAt
                        .AddHours(status.Config.IntervalHours);
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
                         status.LastBackup.CreatedAt < DateTime.UtcNow.AddDays(-7))
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
                    var cutoffDate = DateTime.UtcNow.AddDays(-config.RetentionDays);
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

        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
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
