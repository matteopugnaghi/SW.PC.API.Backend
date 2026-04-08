using Renci.SshNet;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.OpcUa;

namespace SW.PC.API.Backend.Services
{
    public interface IOpcUaSftpService
    {
        /// <summary>Test SFTP connection with current configuration</summary>
        Task<SftpTestResult> TestConnectionAsync();

        /// <summary>Upload our server certificate CSR to SFTP server</summary>
        Task<SftpOperationResult> UploadCsrAsync(byte[] csrData, string fileName);

        /// <summary>Upload our server certificate (.DER) to SFTP server</summary>
        Task<SftpOperationResult> UploadCertificateAsync(byte[] certData, string fileName);

        /// <summary>Download a file from SFTP server (e.g., CA-signed cert, CRL)</summary>
        Task<SftpDownloadResult> DownloadFileAsync(string remoteFileName);

        /// <summary>List files in the remote SFTP certificate folder</summary>
        Task<SftpListResult> ListRemoteFilesAsync();

        /// <summary>Get current SFTP configuration status</summary>
        SftpStatus GetStatus();

        /// <summary>Run full sync cycle: upload our cert, download+import new certs/CRLs</summary>
        Task<SftpSyncResult> RunSyncCycleAsync(IOpcUaCertificateService certService);

        /// <summary>Auto-sync interval from config (0 = disabled)</summary>
        int SyncIntervalSeconds { get; }
    }

    public class SftpSyncResult
    {
        public bool Success { get; set; }
        public int FilesUploaded { get; set; }
        public int FilesImported { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Details { get; set; } = new();
    }

    // ===== Result Models =====

    public class SftpTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ServerFingerprint { get; set; } = "";
    }

    public class SftpOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string RemotePath { get; set; } = "";
    }

    public class SftpDownloadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string FileName { get; set; } = "";
        public byte[]? Data { get; set; }
    }

    public class SftpListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<SftpRemoteFile> Files { get; set; } = new();
    }

    public class SftpRemoteFile
    {
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class SftpStatus
    {
        public bool Enabled { get; set; }
        public bool Configured { get; set; }
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public bool HasSshKey { get; set; }
        public string RemotePath { get; set; } = "";
        public int SyncIntervalSeconds { get; set; }
        public DateTime? LastSync { get; set; }
        public string LastSyncResult { get; set; } = "";
    }

    // ===== Service Implementation =====

    public class OpcUaSftpService : IOpcUaSftpService
    {
        private readonly ILogger<OpcUaSftpService> _logger;
        private readonly IOpcUaServerService _opcUaService;
        private readonly IAuditLogService _auditLog;

        private DateTime? _lastSync;
        private string _lastSyncResult = "";
        // No in-memory cache — ImportTrustedCertificateAsync checks for duplicates by thumbprint,
        // and CRLs + certs are deleted from SFTP after import.

        public int SyncIntervalSeconds => Config.SftpSyncInterval;

        public OpcUaSftpService(
            ILogger<OpcUaSftpService> logger,
            IOpcUaServerService opcUaService,
            IAuditLogService auditLog)
        {
            _logger = logger;
            _opcUaService = opcUaService;
            _auditLog = auditLog;
        }

        private OpcUaConfig Config => _opcUaService.GetConfig();

        public SftpStatus GetStatus()
        {
            var config = Config;
            return new SftpStatus
            {
                Enabled = config.SftpEnabled,
                Configured = config.SftpEnabled && !string.IsNullOrEmpty(config.SftpHost) && !string.IsNullOrEmpty(config.SftpUser),
                Host = config.SftpHost,
                Port = config.SftpPort,
                HasSshKey = !string.IsNullOrEmpty(config.SftpKeyPath) && File.Exists(config.SftpKeyPath),
                RemotePath = config.SftpRemotePath,
                SyncIntervalSeconds = config.SftpSyncInterval,
                LastSync = _lastSync,
                LastSyncResult = _lastSyncResult
            };
        }

        public async Task<SftpTestResult> TestConnectionAsync()
        {
            var config = Config;
            if (!config.SftpEnabled)
                return new SftpTestResult { Success = false, Message = "SFTP is disabled in configuration" };

            if (string.IsNullOrEmpty(config.SftpHost) || string.IsNullOrEmpty(config.SftpUser))
                return new SftpTestResult { Success = false, Message = "SFTP host or user not configured" };

            // Pre-check: avoid SocketException that breaks VS debugger
            var reachError = await CheckHostReachableAsync(config.SftpHost, config.SftpPort);
            if (reachError != null)
            {
                var msg = $"SFTP connection failed: {reachError}";
                _logger.LogWarning("\ud83d\udd10 {Msg}", msg);
                return new SftpTestResult { Success = false, Message = msg };
            }

            try
            {
                using var client = CreateSftpClient(config);
                await Task.Run(() => client.Connect());

                var fingerprint = client.ConnectionInfo.ServerVersion ?? "unknown";
                var result = new SftpTestResult
                {
                    Success = true,
                    Message = $"Connected to {config.SftpHost}:{config.SftpPort}",
                    ServerFingerprint = fingerprint
                };

                // Test if remote path exists
                if (client.Exists(config.SftpRemotePath))
                {
                    result.Message += $" — remote path '{config.SftpRemotePath}' exists";
                }
                else
                {
                    result.Message += $" — WARNING: remote path '{config.SftpRemotePath}' does NOT exist";
                }

                client.Disconnect();

                _logger.LogInformation("🔐 SFTP test connection OK: {Msg}", result.Message);
                _ = _auditLog.LogAsync(
                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Success,
                    $"SFTP test connection OK: {result.Message}", userName: "System");

                return result;
            }
            catch (Exception ex)
            {
                var msg = $"SFTP connection failed: {ex.Message}";
                _logger.LogError(ex, "🔐 {Msg}", msg);
                _ = _auditLog.LogAsync(
                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Failure,
                    msg, userName: "System");
                return new SftpTestResult { Success = false, Message = msg };
            }
        }

        public async Task<SftpOperationResult> UploadCsrAsync(byte[] csrData, string fileName)
        {
            return await UploadDataAsync(csrData, fileName, "CSR");
        }

        public async Task<SftpOperationResult> UploadCertificateAsync(byte[] certData, string fileName)
        {
            return await UploadDataAsync(certData, fileName, "Certificate");
        }

        private async Task<SftpOperationResult> UploadDataAsync(byte[] data, string fileName, string fileType)
        {
            var config = Config;
            if (!config.SftpEnabled)
                return new SftpOperationResult { Success = false, Message = "SFTP is disabled" };

            var reachError = await CheckHostReachableAsync(config.SftpHost, config.SftpPort);
            if (reachError != null)
            {
                var msg = $"SFTP upload failed ({fileType}): {reachError}";
                _logger.LogWarning("\ud83d\udd10 {Msg}", msg);
                _lastSyncResult = $"Upload FAILED: {reachError}";
                return new SftpOperationResult { Success = false, Message = msg };
            }

            try
            {
                using var client = CreateSftpClient(config);
                await Task.Run(() => client.Connect());

                var remotePath = config.SftpRemotePath.TrimEnd('/') + "/" + fileName;

                using var ms = new MemoryStream(data);
                client.UploadFile(ms, remotePath, true);
                client.Disconnect();

                var msg = $"{fileType} uploaded to SFTP: {remotePath} ({data.Length} bytes)";
                _logger.LogInformation("🔐 {Msg}", msg);
                _lastSync = DateTime.UtcNow;
                _lastSyncResult = $"Upload OK: {fileName}";

                _ = _auditLog.LogAsync(
                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Success,
                    msg, userName: "System");

                return new SftpOperationResult { Success = true, Message = msg, RemotePath = remotePath };
            }
            catch (Exception ex)
            {
                var msg = $"SFTP upload failed ({fileType}): {ex.Message}";
                _logger.LogError(ex, "🔐 {Msg}", msg);
                _lastSyncResult = $"Upload FAILED: {ex.Message}";
                return new SftpOperationResult { Success = false, Message = msg };
            }
        }

        public async Task<SftpDownloadResult> DownloadFileAsync(string remoteFileName)
        {
            var config = Config;
            if (!config.SftpEnabled)
                return new SftpDownloadResult { Success = false, Message = "SFTP is disabled" };

            var reachError = await CheckHostReachableAsync(config.SftpHost, config.SftpPort);
            if (reachError != null)
            {
                var msg = $"SFTP download failed: {reachError}";
                _logger.LogWarning("\ud83d\udd10 {Msg}", msg);
                _lastSyncResult = $"Download FAILED: {reachError}";
                return new SftpDownloadResult { Success = false, Message = msg };
            }

            try
            {
                using var client = CreateSftpClient(config);
                await Task.Run(() => client.Connect());

                var remotePath = config.SftpRemotePath.TrimEnd('/') + "/" + remoteFileName;

                if (!client.Exists(remotePath))
                {
                    client.Disconnect();
                    return new SftpDownloadResult { Success = false, Message = $"File not found: {remotePath}" };
                }

                using var ms = new MemoryStream();
                client.DownloadFile(remotePath, ms);
                client.Disconnect();

                var data = ms.ToArray();
                var msg = $"Downloaded from SFTP: {remotePath} ({data.Length} bytes)";
                _logger.LogInformation("🔐 {Msg}", msg);
                _lastSync = DateTime.UtcNow;
                _lastSyncResult = $"Download OK: {remoteFileName}";

                return new SftpDownloadResult { Success = true, Message = msg, FileName = remoteFileName, Data = data };
            }
            catch (Exception ex)
            {
                var msg = $"SFTP download failed: {ex.Message}";
                _logger.LogError(ex, "🔐 {Msg}", msg);
                _lastSyncResult = $"Download FAILED: {ex.Message}";
                return new SftpDownloadResult { Success = false, Message = msg };
            }
        }

        public async Task<SftpListResult> ListRemoteFilesAsync()
        {
            var config = Config;
            if (!config.SftpEnabled)
                return new SftpListResult { Success = false, Message = "SFTP is disabled" };

            var reachError = await CheckHostReachableAsync(config.SftpHost, config.SftpPort);
            if (reachError != null)
            {
                var msg = $"SFTP list failed: {reachError}";
                _logger.LogWarning("\ud83d\udd10 {Msg}", msg);
                return new SftpListResult { Success = false, Message = msg };
            }

            try
            {
                using var client = CreateSftpClient(config);
                await Task.Run(() => client.Connect());

                var files = new List<SftpRemoteFile>();
                var listing = client.ListDirectory(config.SftpRemotePath);
                foreach (var entry in listing)
                {
                    if (entry.IsDirectory || entry.Name.StartsWith(".")) continue;
                    files.Add(new SftpRemoteFile
                    {
                        Name = entry.Name,
                        Size = entry.Length,
                        LastModified = entry.LastWriteTimeUtc
                    });
                }

                client.Disconnect();

                return new SftpListResult
                {
                    Success = true,
                    Message = $"Found {files.Count} file(s) in {config.SftpRemotePath}",
                    Files = files
                };
            }
            catch (Exception ex)
            {
                var msg = $"SFTP list failed: {ex.Message}";
                _logger.LogError(ex, "🔐 {Msg}", msg);
                return new SftpListResult { Success = false, Message = msg };
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Auto-Sync Cycle: upload our cert + download/import new certs
        // ═══════════════════════════════════════════════════════════════

        public async Task<SftpSyncResult> RunSyncCycleAsync(IOpcUaCertificateService certService)
        {
            var result = new SftpSyncResult();
            var config = Config;

            if (!config.SftpEnabled)
            {
                result.Details.Add("SFTP disabled");
                return result;
            }

            _logger.LogInformation("🔐 [SFTP-SYNC] Starting sync cycle...");

            try
            {
                // Step 1: Upload our server certificate (and clean old versions)
                var certBytes = await certService.ExportOwnCertificateDerAsync();

                // Extract our thumbprint for identification (used in cleanup + import skip)
                string? ownThumbprint = null;
                if (certBytes != null && certBytes.Length > 0)
                {
                    try
                    {
                        var ownCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certBytes);
                        ownThumbprint = ownCert.Thumbprint;
                    }
                    catch { /* ignore */ }
                }

                if (certBytes != null && certBytes.Length > 0)
                {
                    var fileName = $"AquafrischServer_{DateTime.UtcNow:yyyyMMdd}.der";
                    var uploadResult = await UploadCertificateAsync(certBytes, fileName);
                    if (uploadResult.Success)
                    {
                        result.FilesUploaded++;
                        result.Details.Add($"Uploaded: {fileName}");

                        // Delete old copies of our cert from SFTP (by thumbprint, not filename)
                        try
                        {
                            var cleanConfig = Config;
                            using var cleanClient = CreateSftpClient(cleanConfig);
                            await Task.Run(() => cleanClient.Connect());
                            var remoteDir = cleanConfig.SftpRemotePath.TrimEnd('/');
                            foreach (var f in cleanClient.ListDirectory(remoteDir))
                            {
                                if (f.IsDirectory || f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                var fExt = Path.GetExtension(f.Name).ToLowerInvariant();
                                if (fExt != ".der" && fExt != ".cer" && fExt != ".crt")
                                    continue;
                                // Download and check thumbprint
                                try
                                {
                                    using var ms = new MemoryStream();
                                    cleanClient.DownloadFile($"{remoteDir}/{f.Name}", ms);
                                    var remoteCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(ms.ToArray());
                                    if (string.Equals(remoteCert.Thumbprint, ownThumbprint, StringComparison.OrdinalIgnoreCase))
                                    {
                                        cleanClient.DeleteFile($"{remoteDir}/{f.Name}");
                                        _logger.LogInformation("🔐 [SFTP-SYNC] Deleted old copy of our cert: {Name}", f.Name);
                                    }
                                }
                                catch { /* not a valid cert or download failed — skip */ }
                            }
                            cleanClient.Disconnect();
                        }
                        catch (Exception cleanEx)
                        {
                            _logger.LogWarning(cleanEx, "🔐 [SFTP-SYNC] Could not clean old certs from SFTP");
                        }
                    }
                    else
                    {
                        result.Details.Add($"Upload failed: {uploadResult.Message}");
                    }
                }

                // Step 2: List remote files
                var listResult = await ListRemoteFilesAsync();
                if (!listResult.Success)
                {
                    result.Details.Add($"List failed: {listResult.Message}");
                    _lastSync = DateTime.UtcNow;
                    _lastSyncResult = $"Sync partial — upload OK, list failed";
                    return result;
                }

                // Step 3: Download and import new .der/.crl files
                var certExtensions = new[] { ".der", ".cer", ".crt" };
                var crlExtensions = new[] { ".crl" };

                foreach (var file in listResult.Files)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();

                    // Skip non-cert/crl files
                    if (!certExtensions.Contains(ext) && !crlExtensions.Contains(ext))
                        continue;

                    if (certExtensions.Contains(ext))
                    {
                        // Download and import as trusted
                        var dlResult = await DownloadFileAsync(file.Name);
                        if (dlResult.Success && dlResult.Data != null)
                        {
                            // Skip our own certificate (compare by thumbprint, not filename)
                            if (ownThumbprint != null)
                            {
                                try
                                {
                                    var remoteCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(dlResult.Data);
                                    if (string.Equals(remoteCert.Thumbprint, ownThumbprint, StringComparison.OrdinalIgnoreCase))
                                    {
                                        result.FilesSkipped++;
                                        continue;
                                    }
                                }
                                catch { /* not a valid cert — try to import anyway */ }
                            }

                            bool imported = false;
                            try
                            {
                                await certService.ImportTrustedCertificateAsync(dlResult.Data, file.Name);
                                result.FilesImported++;
                                result.Details.Add($"Imported trusted: {file.Name}");
                                _logger.LogInformation("🔐 [SFTP-SYNC] Imported trusted cert: {Name}", file.Name);
                                imported = true;

                                _ = _auditLog.LogAsync(
                                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Success,
                                    $"Certificate imported from SFTP: {file.Name} ({dlResult.Data.Length} bytes)",
                                    userName: "System");
                            }
                            catch (InvalidOperationException alreadyEx) when (alreadyEx.Message.Contains("already trusted"))
                            {
                                result.FilesSkipped++;
                                result.Details.Add($"Already trusted: {file.Name}");
                                imported = true; // Already in trusted store — safe to remove from SFTP
                            }
                            catch (Exception importEx)
                            {
                                result.Details.Add($"Import failed ({file.Name}): {importEx.Message}");
                                _logger.LogWarning(importEx, "🔐 [SFTP-SYNC] Failed to import: {Name}", file.Name);
                            }

                            // Remove from SFTP "inbox" after successful import
                            if (imported)
                            {
                                try
                                {
                                    await DeleteRemoteFileAsync(file.Name);
                                    _logger.LogInformation("🔐 [SFTP-SYNC] Removed from SFTP after import: {Name}", file.Name);
                                }
                                catch (Exception delEx)
                                {
                                    _logger.LogWarning(delEx, "🔐 [SFTP-SYNC] Could not remove {Name} from SFTP", file.Name);
                                }
                            }
                        }
                    }
                    else if (crlExtensions.Contains(ext))
                    {
                        // Download CRL — store in issuers/crl folder
                        var dlResult = await DownloadFileAsync(file.Name);
                        if (dlResult.Success && dlResult.Data != null)
                        {
                            try
                            {
                                var crlDir = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "Aquafrisch", "opcua-certs", "issuers", "crl");
                                Directory.CreateDirectory(crlDir);
                                var crlPath = Path.Combine(crlDir, file.Name);
                                await File.WriteAllBytesAsync(crlPath, dlResult.Data);
                                result.FilesImported++;
                                result.Details.Add($"Imported CRL: {file.Name}");
                                _logger.LogInformation("🔐 [SFTP-SYNC] Imported CRL: {Name} → {Path}", file.Name, crlPath);

                                _ = _auditLog.LogAsync(
                                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Success,
                                    $"CRL imported: {file.Name} ({dlResult.Data.Length} bytes)",
                                    userName: "System");

                                // Remove from SFTP after import
                                try
                                {
                                    await DeleteRemoteFileAsync(file.Name);
                                }
                                catch { /* best-effort */ }
                            }
                            catch (Exception crlEx)
                            {
                                result.Details.Add($"CRL import failed ({file.Name}): {crlEx.Message}");
                                _logger.LogWarning(crlEx, "🔐 [SFTP-SYNC] Failed to save CRL: {Name}", file.Name);
                            }
                        }
                    }
                }

                // Step 4: Ensure our own cert is in trusted store + cleanup old certs
                try
                {
                    var trustedCertsDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Aquafrisch", "opcua-certs", "trusted", "certs");
                    Directory.CreateDirectory(trustedCertsDir);

                    // 4a: If our own cert is missing from trusted, re-add it
                    if (ownThumbprint != null && certBytes != null)
                    {
                        var ownInTrusted = Directory.GetFiles(trustedCertsDir, "*.der")
                            .Concat(Directory.GetFiles(trustedCertsDir, "*.cer"))
                            .Concat(Directory.GetFiles(trustedCertsDir, "*.crt"))
                            .Any(f =>
                            {
                                try
                                {
                                    using var x = new System.Security.Cryptography.X509Certificates.X509Certificate2(f);
                                    return string.Equals(x.Thumbprint, ownThumbprint, StringComparison.OrdinalIgnoreCase);
                                }
                                catch { return false; }
                            });

                        if (!ownInTrusted)
                        {
                            var ownFileName = $"AquafrischServer_{ownThumbprint[..8]}.der";
                            await File.WriteAllBytesAsync(Path.Combine(trustedCertsDir, ownFileName), certBytes);
                            _logger.LogWarning("🔐 [SFTP-SYNC] Our own cert was missing from trusted store — re-added: {File}", ownFileName);
                            result.Details.Add($"Re-added own cert to trusted: {ownFileName}");
                        }
                    }

                    // 4b: Cleanup — only remove EXPIRED certificates (never delete valid certs)
                    if (Directory.Exists(trustedCertsDir))
                    {
                        const int maxTrustedCerts = 20;
                        var certFiles = Directory.GetFiles(trustedCertsDir, "*.der")
                            .Concat(Directory.GetFiles(trustedCertsDir, "*.cer"))
                            .Concat(Directory.GetFiles(trustedCertsDir, "*.crt"))
                            .Select(f =>
                            {
                                try
                                {
                                    var x = new System.Security.Cryptography.X509Certificates.X509Certificate2(f);
                                    return new { Path = f, Cert = x, x.NotAfter, x.Thumbprint };
                                }
                                catch { return null; }
                            })
                            .Where(x => x != null)
                            // Never delete our own certificate from trusted store
                            .Where(x => !string.Equals(x!.Thumbprint, ownThumbprint, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // Only auto-delete expired certificates
                        var expired = certFiles.Where(x => x!.NotAfter < DateTime.UtcNow).ToList();
                        foreach (var old in expired)
                        {
                            try
                            {
                                File.Delete(old!.Path);
                                _logger.LogInformation("🔐 [SFTP-SYNC] Removed expired cert: {Subject} (expired: {Date})",
                                    old.Cert.Subject, old.NotAfter.ToUniversalTime().ToString("yyyy-MM-dd"));
                                result.Details.Add($"Expired removed: {Path.GetFileName(old.Path)}");

                                _ = _auditLog.LogAsync(
                                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Warning,
                                    $"Trusted store cleanup: removed expired {old.Cert.Subject} (expired {old.NotAfter.ToUniversalTime():yyyy-MM-dd}).",
                                    userName: "System");
                            }
                            catch { /* ignore */ }
                            finally { old!.Cert.Dispose(); }
                        }

                        // Warn if approaching the safety limit (but never auto-delete valid certs)
                        var validCount = certFiles.Count - expired.Count;
                        if (validCount > maxTrustedCerts)
                        {
                            _logger.LogWarning("🔐 [SFTP-SYNC] Trusted store has {Count} valid certs (limit: {Max}). Manual cleanup recommended.",
                                validCount, maxTrustedCerts);
                        }

                        // Dispose remaining certs
                        foreach (var c in certFiles.Where(x => !expired.Contains(x)))
                            c?.Cert.Dispose();
                    }
                }
                catch (Exception cleanEx)
                {
                    _logger.LogWarning(cleanEx, "🔐 [SFTP-SYNC] Trusted cert cleanup failed");
                }

                result.Success = true;
                _lastSync = DateTime.UtcNow;
                _lastSyncResult = $"Sync OK — {result.FilesUploaded} uploaded, {result.FilesImported} imported, {result.FilesSkipped} skipped";
                _logger.LogInformation("🔐 [SFTP-SYNC] {Result}", _lastSyncResult);

                _ = _auditLog.LogAsync(
                    AuditCategory.OtCommunication, AuditAction.SftpSync, AuditResult.Success,
                    _lastSyncResult, userName: "System");
            }
            catch (Exception ex)
            {
                result.Details.Add($"Sync error: {ex.Message}");
                _lastSync = DateTime.UtcNow;
                _lastSyncResult = $"Sync FAILED: {ex.Message}";
                _logger.LogError(ex, "🔐 [SFTP-SYNC] Cycle failed");
            }

            return result;
        }

        private async Task DeleteRemoteFileAsync(string fileName)
        {
            var config = Config;
            using var client = CreateSftpClient(config);
            await Task.Run(() => client.Connect());
            var remotePath = config.SftpRemotePath.TrimEnd('/') + "/" + fileName;
            client.DeleteFile(remotePath);
            client.Disconnect();
        }

        private SftpClient CreateSftpClient(OpcUaConfig config)
        {
            var connectionInfo = BuildConnectionInfo(config);
            return new SftpClient(connectionInfo);
        }

        /// <summary>
        /// Quick TCP check before SSH — avoids SocketException that crashes VS debugger.
        /// </summary>
        private static async Task<string?> CheckHostReachableAsync(string host, int port, int timeoutMs = 5000)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                    return $"Host {host}:{port} is not reachable (timeout {timeoutMs}ms)";
                if (connectTask.IsFaulted)
                    return $"Host {host}:{port} refused connection: {connectTask.Exception?.InnerException?.Message}";
                return null; // reachable
            }
            catch (Exception ex)
            {
                return $"Host {host}:{port} not reachable: {ex.Message}";
            }
        }

        private static Renci.SshNet.ConnectionInfo BuildConnectionInfo(OpcUaConfig config)
        {
            var authMethods = new List<AuthenticationMethod>();

            // SSH key authentication (preferred — CSP generates the key)
            if (!string.IsNullOrEmpty(config.SftpKeyPath) && File.Exists(config.SftpKeyPath))
            {
                var keyFile = new PrivateKeyFile(config.SftpKeyPath);
                authMethods.Add(new PrivateKeyAuthenticationMethod(config.SftpUser, keyFile));
            }

            if (authMethods.Count == 0)
                throw new InvalidOperationException("No SFTP authentication method available. Configure OpcUa_Sftp_KeyPath.");

            var connInfo = new Renci.SshNet.ConnectionInfo(config.SftpHost, config.SftpPort, config.SftpUser, authMethods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(10)    // Don't hang forever on unreachable hosts
            };
            return connInfo;
        }
    }

    /// <summary>
    /// Disabled stub for when SFTP is not needed
    /// </summary>
    public class DisabledOpcUaSftpService : IOpcUaSftpService
    {
        public Task<SftpTestResult> TestConnectionAsync() => 
            Task.FromResult(new SftpTestResult { Success = false, Message = "SFTP not enabled" });
        public Task<SftpOperationResult> UploadCsrAsync(byte[] csrData, string fileName) =>
            Task.FromResult(new SftpOperationResult { Success = false, Message = "SFTP not enabled" });
        public Task<SftpOperationResult> UploadCertificateAsync(byte[] certData, string fileName) =>
            Task.FromResult(new SftpOperationResult { Success = false, Message = "SFTP not enabled" });
        public Task<SftpDownloadResult> DownloadFileAsync(string remoteFileName) =>
            Task.FromResult(new SftpDownloadResult { Success = false, Message = "SFTP not enabled" });
        public Task<SftpListResult> ListRemoteFilesAsync() =>
            Task.FromResult(new SftpListResult { Success = false, Message = "SFTP not enabled" });
        public SftpStatus GetStatus() => new() { Enabled = false };
        public Task<SftpSyncResult> RunSyncCycleAsync(IOpcUaCertificateService certService) =>
            Task.FromResult(new SftpSyncResult { Success = false, Details = { "SFTP not enabled" } });
        public int SyncIntervalSeconds => 0;
    }
}
