// ==================================================================
// Services/BackupCertificateService.cs
// DATA MANAGEMENT - Servicio de Certificados para Backups
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public interface IBackupCertificateService
    {
        /// <summary>Firmar un backup generando su certificado</summary>
        Task<BackupCertificate> SignBackupAsync(string projectId, string backupId, BackupManifest manifest);
        
        /// <summary>Verificar la validez de un certificado</summary>
        Task<bool> VerifyCertificateAsync(BackupCertificate certificate, BackupManifest manifest);
        
        /// <summary>Obtener el último hash de certificado de la cadena</summary>
        Task<string?> GetLastCertificateHashAsync(string projectId);
        
        /// <summary>Obtener el número de secuencia actual</summary>
        Task<int> GetCurrentSequenceNumberAsync(string projectId);
    }

    public class BackupCertificateService : IBackupCertificateService
    {
        private readonly ILogger<BackupCertificateService> _logger;
        private readonly IWebHostEnvironment _environment;
        
        // Clave secreta para firmar (en producción debería estar en un HSM o Azure Key Vault)
        // Esta clave se genera única por instalación
        private readonly string _signingSecret;
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BackupCertificateService(
            ILogger<BackupCertificateService> logger,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _logger = logger;
            _environment = environment;
            
            // Obtener o generar clave de firma
            _signingSecret = GetOrCreateSigningSecret(configuration);
        }

        /// <summary>
        /// Obtiene la ruta de backups para un proyecto específico
        /// </summary>
        private string GetBackupsPath(string projectId)
        {
            var contentRoot = _environment.ContentRootPath;
            
            if (projectId == "default")
            {
                return Path.Combine(contentRoot, "backups");
            }
            
            return Path.Combine(contentRoot, "Projects", projectId, "backups");
        }

        public async Task<BackupCertificate> SignBackupAsync(string projectId, string backupId, BackupManifest manifest)
        {
            _logger.LogInformation("Signing backup {BackupId} for project {ProjectId}", backupId, projectId);
            
            // Calcular hash del manifest (sin incluir información que cambie)
            var manifestForHash = new
            {
                manifest.ManifestVersion,
                manifest.BackupInfo.Id,
                manifest.BackupInfo.ProjectId,
                manifest.BackupInfo.CreatedAt,
                manifest.Files
            };
            var manifestJson = JsonSerializer.Serialize(manifestForHash, JsonOptions);
            var manifestHash = ComputeSha256(manifestJson);
            
            // Calcular hash del contenido (concatenación de hashes de archivos)
            var contentHashBuilder = new StringBuilder();
            foreach (var file in manifest.Files.OrderBy(f => f.RelativePath))
            {
                contentHashBuilder.Append(file.Hash);
            }
            var contentHash = ComputeSha256(contentHashBuilder.ToString());
            
            // Obtener hash del certificado anterior para la cadena
            var previousHash = await GetLastCertificateHashAsync(projectId);
            var sequenceNumber = await GetCurrentSequenceNumberAsync(projectId) + 1;
            
            // Crear firma combinada
            var signatureData = $"{backupId}|{manifestHash}|{contentHash}|{previousHash ?? "GENESIS"}|{sequenceNumber}|{_signingSecret}";
            var signature = ComputeSha256(signatureData);
            
            var certificate = new BackupCertificate
            {
                CertificateVersion = "1.0",
                BackupId = backupId,
                ManifestHash = manifestHash,
                ContentHash = contentHash,
                Signature = signature,
                SignedAt = DateTime.Now,
                SignedBy = $"SW.PC.API.Backend@{Environment.MachineName}",
                PreviousCertificateHash = previousHash,
                SequenceNumber = sequenceNumber,
                Compliance = new ComplianceMetadata
                {
                    Standard = "EU-CRA-2024",
                    Requirement = "Anexo I, Parte I, 2f",
                    Description = "Protección de integridad de datos almacenados, transmitidos o procesados",
                    HashAlgorithm = "SHA256"
                }
            };
            
            // Guardar referencia al último certificado
            await SaveLastCertificateInfoAsync(projectId, certificate);
            
            _logger.LogInformation("Backup {BackupId} signed successfully. Sequence: {Sequence}", 
                backupId, sequenceNumber);
            
            return certificate;
        }

        public Task<bool> VerifyCertificateAsync(BackupCertificate certificate, BackupManifest manifest)
        {
            try
            {
                _logger.LogInformation("Verifying certificate for backup {BackupId}", certificate.BackupId);
                
                // Recalcular hash del manifest
                var manifestForHash = new
                {
                    manifest.ManifestVersion,
                    manifest.BackupInfo.Id,
                    manifest.BackupInfo.ProjectId,
                    manifest.BackupInfo.CreatedAt,
                    manifest.Files
                };
                var manifestJson = JsonSerializer.Serialize(manifestForHash, JsonOptions);
                var manifestHash = ComputeSha256(manifestJson);
                
                // Verificar hash del manifest
                if (manifestHash != certificate.ManifestHash)
                {
                    _logger.LogWarning("Manifest hash mismatch for backup {BackupId}", certificate.BackupId);
                    return Task.FromResult(false);
                }
                
                // Recalcular hash del contenido
                var contentHashBuilder = new StringBuilder();
                foreach (var file in manifest.Files.OrderBy(f => f.RelativePath))
                {
                    contentHashBuilder.Append(file.Hash);
                }
                var contentHash = ComputeSha256(contentHashBuilder.ToString());
                
                // Verificar hash del contenido
                if (contentHash != certificate.ContentHash)
                {
                    _logger.LogWarning("Content hash mismatch for backup {BackupId}", certificate.BackupId);
                    return Task.FromResult(false);
                }
                
                // Recalcular y verificar firma
                var signatureData = $"{certificate.BackupId}|{certificate.ManifestHash}|{certificate.ContentHash}|{certificate.PreviousCertificateHash ?? "GENESIS"}|{certificate.SequenceNumber}|{_signingSecret}";
                var expectedSignature = ComputeSha256(signatureData);
                
                if (expectedSignature != certificate.Signature)
                {
                    _logger.LogWarning("Signature mismatch for backup {BackupId}", certificate.BackupId);
                    return Task.FromResult(false);
                }
                
                _logger.LogInformation("Certificate verified successfully for backup {BackupId}", certificate.BackupId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying certificate for backup {BackupId}", certificate.BackupId);
                return Task.FromResult(false);
            }
        }

        public async Task<string?> GetLastCertificateHashAsync(string projectId)
        {
            try
            {
                var chainFile = GetChainFilePath(projectId);
                if (!File.Exists(chainFile))
                    return null;
                
                var chainInfo = await ReadChainInfoAsync(chainFile);
                return chainInfo?.LastCertificateHash;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading last certificate hash");
                return null;
            }
        }

        public async Task<int> GetCurrentSequenceNumberAsync(string projectId)
        {
            try
            {
                var chainFile = GetChainFilePath(projectId);
                if (!File.Exists(chainFile))
                    return 0;
                
                var chainInfo = await ReadChainInfoAsync(chainFile);
                return chainInfo?.SequenceNumber ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading sequence number");
                return 0;
            }
        }

        // ==================== Private Methods ====================

        private string GetOrCreateSigningSecret(IConfiguration configuration)
        {
            // Intentar obtener de configuración
            var configuredSecret = configuration["Backup:SigningSecret"];
            if (!string.IsNullOrEmpty(configuredSecret))
                return configuredSecret;
            
            // Generar o leer de archivo local
            var secretFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".backup-signing-key");
            
            if (File.Exists(secretFile))
            {
                return File.ReadAllText(secretFile).Trim();
            }
            
            // Generar nueva clave
            var newSecret = GenerateSecureKey();
            try
            {
                File.WriteAllText(secretFile, newSecret);
                // Ocultar archivo
                File.SetAttributes(secretFile, FileAttributes.Hidden);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save signing key to file");
            }
            
            return newSecret;
        }

        private static string GenerateSecureKey()
        {
            var keyBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }

        private static string ComputeSha256(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string GetChainFilePath(string projectId)
        {
            var backupsPath = GetBackupsPath(projectId);
            return Path.Combine(backupsPath, ".certificate-chain.json");
        }

        private async Task<CertificateChainInfo?> ReadChainInfoAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<CertificateChainInfo>(json, JsonOptions);
        }

        private async Task SaveLastCertificateInfoAsync(string projectId, BackupCertificate certificate)
        {
            try
            {
                var chainFile = GetChainFilePath(projectId);
                Directory.CreateDirectory(Path.GetDirectoryName(chainFile)!);
                
                // Calcular hash del certificado actual
                var certJson = JsonSerializer.Serialize(certificate, JsonOptions);
                var certHash = ComputeSha256(certJson);
                
                var chainInfo = new CertificateChainInfo
                {
                    LastCertificateHash = certHash,
                    LastBackupId = certificate.BackupId,
                    SequenceNumber = certificate.SequenceNumber,
                    UpdatedAt = DateTime.Now
                };
                
                var json = JsonSerializer.Serialize(chainInfo, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                await File.WriteAllTextAsync(chainFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error saving certificate chain info");
            }
        }

        /// <summary>
        /// Información de la cadena de certificados
        /// </summary>
        private class CertificateChainInfo
        {
            public string? LastCertificateHash { get; set; }
            public string? LastBackupId { get; set; }
            public int SequenceNumber { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
