// ============================================================================
// OpcUaCertificateService.cs - OPC UA Certificate Trust Management
// ============================================================================
// Phase 1 (until June 2027): Self-signed certificates with manual .DER exchange
// - Export own application certificate in .DER format
// - Import trusted client certificates (.DER)
// - List/remove trusted and rejected certificates
// - Move rejected → trusted (approve)
//
// Phase 2 (post June 2027): SFTP-based CSR/CA workflow (not yet implemented)
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.OpcUa;

namespace SW.PC.API.Backend.Services
{
    public interface IOpcUaCertificateService
    {
        /// <summary>Get the OPC UA certificate store base path</summary>
        string GetStorePath(string subfolder);

        /// <summary>Export own application certificate as DER bytes</summary>
        Task<byte[]?> ExportOwnCertificateDerAsync();

        /// <summary>Get info about own application certificate</summary>
        Task<OpcUaCertificateInfo?> GetOwnCertificateInfoAsync();

        /// <summary>List certificates in a store (trusted, rejected, issuers)</summary>
        Task<List<OpcUaCertificateInfo>> ListCertificatesAsync(string store);

        /// <summary>Import a DER-encoded certificate into the trusted store</summary>
        Task<OpcUaCertificateInfo> ImportTrustedCertificateAsync(byte[] derBytes, string? label);

        /// <summary>Remove a certificate from a store by thumbprint</summary>
        Task<bool> RemoveCertificateAsync(string store, string thumbprint);

        /// <summary>Move a certificate from rejected to trusted (approve)</summary>
        Task<OpcUaCertificateInfo?> ApproveCertificateAsync(string thumbprint);

        /// <summary>List CRL files from issuers/crl and parse revoked serial numbers</summary>
        List<OpcUaCrlInfo> GetCrlFiles();

        /// <summary>Mark certificates as revoked if their serial appears in any CRL</summary>
        void MarkRevokedCertificates(List<OpcUaCertificateInfo> certs, List<OpcUaCrlInfo> crls);
    }

    public class OpcUaCertificateService : IOpcUaCertificateService
    {
        private readonly ILogger<OpcUaCertificateService> _logger;
        private readonly IAuditLogService _auditLogService;
        private readonly string _basePath;

        // Allowed store names to prevent path traversal
        private static readonly HashSet<string> ValidStores = new(StringComparer.OrdinalIgnoreCase)
        {
            "own", "trusted", "rejected", "issuers"
        };

        public OpcUaCertificateService(
            ILogger<OpcUaCertificateService> logger,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _auditLogService = auditLogService;
            _basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aquafrisch", "opcua-certs");
        }

        public string GetStorePath(string subfolder)
        {
            if (!ValidStores.Contains(subfolder))
                throw new ArgumentException($"Invalid store name: {subfolder}");

            var path = Path.Combine(_basePath, subfolder);
            Directory.CreateDirectory(path);
            return path;
        }

        public Task<byte[]?> ExportOwnCertificateDerAsync()
        {
            var ownPath = GetStorePath("own");
            var certFile = FindCertificateFile(ownPath);
            if (certFile == null)
                return Task.FromResult<byte[]?>(null);

            try
            {
                using var cert = new X509Certificate2(certFile);
                // Export ONLY public key (DER-encoded X.509)
                var derBytes = cert.Export(X509ContentType.Cert);
                return Task.FromResult<byte[]?>(derBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export own OPC UA certificate from {Path}", certFile);
                return Task.FromResult<byte[]?>(null);
            }
        }

        public Task<OpcUaCertificateInfo?> GetOwnCertificateInfoAsync()
        {
            var ownPath = GetStorePath("own");
            var certFile = FindCertificateFile(ownPath);
            if (certFile == null)
                return Task.FromResult<OpcUaCertificateInfo?>(null);

            try
            {
                using var cert = new X509Certificate2(certFile);
                return Task.FromResult<OpcUaCertificateInfo?>(BuildCertInfo(cert, "own"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read own certificate info");
                return Task.FromResult<OpcUaCertificateInfo?>(null);
            }
        }

        public Task<List<OpcUaCertificateInfo>> ListCertificatesAsync(string store)
        {
            var storePath = GetStorePath(store); // Validates store name
            var result = new List<OpcUaCertificateInfo>();

            if (!Directory.Exists(storePath))
                return Task.FromResult(result);

            // Search root + certs/ subdirectory (OPC Foundation SDK structure)
            var searchDirs = new[] { storePath, Path.Combine(storePath, "certs") };
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.der")
                    .Concat(Directory.GetFiles(dir, "*.cer"))
                    .Concat(Directory.GetFiles(dir, "*.crt")))
                {
                    try
                    {
                        using var cert = new X509Certificate2(file);
                        var info = BuildCertInfo(cert, store);
                        info.FileName = Path.GetFileName(file);
                        result.Add(info);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not parse certificate file: {File}", file);
                    }
                }
            }

            return Task.FromResult(result);
        }

        public async Task<OpcUaCertificateInfo> ImportTrustedCertificateAsync(byte[] derBytes, string? label)
        {
            // Validate the DER bytes are a valid certificate
            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(derBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Invalid certificate data. Expected DER-encoded X.509 certificate.", ex);
            }

            using (cert)
            {
                // Security: reject certificates with private keys
                if (cert.HasPrivateKey)
                    throw new InvalidOperationException("Certificate contains a private key. Only public certificates (.DER/.CER) are accepted.");

                // Security: reject expired certificates
                if (cert.NotAfter < DateTime.UtcNow)
                    throw new InvalidOperationException($"Certificate expired on {cert.NotAfter:yyyy-MM-dd}.");

                // Security: minimum key size
                var keySize = cert.PublicKey.GetRSAPublicKey()?.KeySize
                           ?? cert.PublicKey.GetECDsaPublicKey()?.KeySize
                           ?? 0;
                if (keySize < 2048)
                    throw new InvalidOperationException($"Certificate key size ({keySize} bits) is below minimum (2048 bits).");

                var trustedPath = GetStorePath("trusted");
                // OPC Foundation SDK expects certs in {store}/certs/ subdirectory
                var certsPath = Path.Combine(trustedPath, "certs");
                Directory.CreateDirectory(certsPath);
                var fileName = SanitizeFileName(label ?? cert.GetNameInfo(X509NameType.SimpleName, false) ?? cert.Thumbprint);
                fileName = $"{fileName}_{cert.Thumbprint[..8]}.der";
                var destPath = Path.Combine(certsPath, fileName);

                // Check for duplicate by thumbprint (search root + certs/)
                var searchDirs = new[] { trustedPath, certsPath };
                foreach (var dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "*.der")
                        .Concat(Directory.GetFiles(dir, "*.cer"))
                        .Concat(Directory.GetFiles(dir, "*.crt")))
                    {
                        try
                        {
                            using var existingCert = new X509Certificate2(f);
                            if (existingCert.Thumbprint == cert.Thumbprint)
                                throw new InvalidOperationException($"Certificate with thumbprint {cert.Thumbprint} is already trusted.");
                        }
                        catch (InvalidOperationException) { throw; }
                        catch { /* skip unreadable files */ }
                    }
                }

                // Write DER file
                await File.WriteAllBytesAsync(destPath, cert.Export(X509ContentType.Cert));

                // Also remove from rejected if it was there
                await RemoveFromStoreByThumbprint("rejected", cert.Thumbprint);

                var info = BuildCertInfo(cert, "trusted");
                info.FileName = fileName;

                _logger.LogInformation("🔐 Imported trusted OPC UA certificate: {Subject} ({Thumbprint})",
                    cert.Subject, cert.Thumbprint);

                await _auditLogService.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.CertificateImport,
                    AuditResult.Success,
                    $"Imported trusted OPC UA certificate: {cert.Subject} (thumbprint: {cert.Thumbprint})",
                    userName: "System");

                return info;
            }
        }

        public async Task<bool> RemoveCertificateAsync(string store, string thumbprint)
        {
            if (store.Equals("own", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot remove the server's own certificate via API.");

            var storePath = GetStorePath(store);
            var removed = await RemoveFromStoreByThumbprint(store, thumbprint);

            if (removed)
            {
                _logger.LogInformation("🔐 Removed certificate {Thumbprint} from {Store} store", thumbprint, store);

                await _auditLogService.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.CertificateRemove,
                    AuditResult.Success,
                    $"Removed certificate from {store} store (thumbprint: {thumbprint})",
                    userName: "System");
            }

            return removed;
        }

        public async Task<OpcUaCertificateInfo?> ApproveCertificateAsync(string thumbprint)
        {
            var rejectedPath = GetStorePath("rejected");
            var certFile = FindByThumbprint(rejectedPath, thumbprint);
            if (certFile == null)
                return null;

            byte[] derBytes;
            try
            {
                using var cert = new X509Certificate2(certFile);
                derBytes = cert.Export(X509ContentType.Cert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read rejected certificate {Thumbprint}", thumbprint);
                return null;
            }

            // Import into trusted (also removes from rejected)
            var info = await ImportTrustedCertificateAsync(derBytes, null);

            _logger.LogInformation("🔐 Approved certificate {Thumbprint}: moved rejected → trusted", thumbprint);

            await _auditLogService.LogAsync(
                AuditCategory.OtCommunication,
                AuditAction.CertificateApprove,
                AuditResult.Success,
                $"Approved OPC UA certificate: {info.Subject} (moved rejected → trusted)",
                userName: "System");

            return info;
        }

        // ===== Private Helpers =====

        private string? FindCertificateFile(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            // OPC Foundation SDK stores certs in {store}/certs/ subdirectory
            var searchDirs = new[] { folder, Path.Combine(folder, "certs") };
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                var found = Directory.GetFiles(dir, "*.der").FirstOrDefault()
                    ?? Directory.GetFiles(dir, "*.cer").FirstOrDefault()
                    ?? Directory.GetFiles(dir, "*.crt").FirstOrDefault();
                if (found != null) return found;
            }
            // PFX only in root or private/ (own store)
            var pfx = Directory.GetFiles(folder, "*.pfx").FirstOrDefault();
            if (pfx != null) return pfx;
            var privatePath = Path.Combine(folder, "private");
            if (Directory.Exists(privatePath))
                return Directory.GetFiles(privatePath, "*.pfx").FirstOrDefault();
            return null;
        }

        private string? FindByThumbprint(string folder, string thumbprint)
        {
            if (!Directory.Exists(folder)) return null;
            // Search root + certs/ subdirectory (OPC Foundation SDK structure)
            var searchDirs = new[] { folder, Path.Combine(folder, "certs") };
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.der")
                    .Concat(Directory.GetFiles(dir, "*.cer"))
                    .Concat(Directory.GetFiles(dir, "*.crt")))
                {
                    try
                    {
                        using var cert = new X509Certificate2(file);
                        if (cert.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase))
                            return file;
                    }
                    catch { /* skip */ }
                }
            }
            return null;
        }

        private async Task<bool> RemoveFromStoreByThumbprint(string store, string thumbprint)
        {
            var storePath = GetStorePath(store);
            if (!Directory.Exists(storePath)) return false;
            // Search root + certs/ subdirectory (OPC Foundation SDK structure)
            var searchDirs = new[] { storePath, Path.Combine(storePath, "certs") };
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.der")
                    .Concat(Directory.GetFiles(dir, "*.cer"))
                    .Concat(Directory.GetFiles(dir, "*.crt")))
                {
                    try
                    {
                        using var cert = new X509Certificate2(file);
                        if (cert.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase))
                        {
                            cert.Dispose();
                            File.Delete(file);
                            return true;
                        }
                    }
                    catch { /* skip */ }
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        // CRL (Certificate Revocation List) parsing
        // ═══════════════════════════════════════════════════════════════

        public List<OpcUaCrlInfo> GetCrlFiles()
        {
            var crlDir = Path.Combine(_basePath, "issuers", "crl");
            if (!Directory.Exists(crlDir))
                return new List<OpcUaCrlInfo>();

            var results = new List<OpcUaCrlInfo>();
            foreach (var file in Directory.GetFiles(crlDir, "*.crl"))
            {
                try
                {
                    var info = ParseCrlFile(file);
                    if (info != null) results.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse CRL file: {File}", file);
                }
            }
            return results;
        }

        public void MarkRevokedCertificates(List<OpcUaCertificateInfo> certs, List<OpcUaCrlInfo> crls)
        {
            // Collect all revoked serials from all CRLs (normalized: uppercase, no leading zeros)
            var revokedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var crl in crls)
            {
                foreach (var serial in crl.RevokedSerials)
                    revokedSerials.Add(serial.TrimStart('0'));
            }

            if (revokedSerials.Count == 0) return;

            foreach (var cert in certs)
            {
                var certSerial = cert.SerialNumber.TrimStart('0');
                if (revokedSerials.Contains(certSerial))
                    cert.IsRevoked = true;
            }
        }

        private OpcUaCrlInfo? ParseCrlFile(string filePath)
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var derBytes = ConvertPemToDer(fileBytes);
            if (derBytes == null || derBytes.Length == 0) return null;

            var info = new OpcUaCrlInfo { FileName = Path.GetFileName(filePath) };

            var reader = new System.Formats.Asn1.AsnReader(derBytes, System.Formats.Asn1.AsnEncodingRules.DER);
            var certList = reader.ReadSequence(); // CertificateList
            var tbsCertList = certList.ReadSequence(); // TBSCertList

            // Version (optional INTEGER)
            var peek = tbsCertList.PeekTag();
            if (peek.TagClass == System.Formats.Asn1.TagClass.Universal &&
                peek.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.Integer)
            {
                tbsCertList.ReadInteger(); // skip version
            }

            // Signature algorithm (SEQUENCE) — skip
            tbsCertList.ReadSequence();

            // Issuer (SEQUENCE) — read as X500DistinguishedName
            try
            {
                var issuerBytes = tbsCertList.PeekEncodedValue().ToArray();
                info.Issuer = new System.Security.Cryptography.X509Certificates.X500DistinguishedName(issuerBytes).Name;
            }
            catch { info.Issuer = "Unknown"; }
            tbsCertList.ReadSequence(); // advance past issuer

            // thisUpdate (UTCTime or GeneralizedTime)
            info.LastUpdate = ReadAsnTime(tbsCertList)?.UtcDateTime;

            // nextUpdate (optional)
            if (tbsCertList.HasData)
            {
                peek = tbsCertList.PeekTag();
                if (peek.TagClass == System.Formats.Asn1.TagClass.Universal &&
                    (peek.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.UtcTime ||
                     peek.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.GeneralizedTime))
                {
                    info.NextUpdate = ReadAsnTime(tbsCertList)?.UtcDateTime;
                }
            }

            // revokedCertificates (optional SEQUENCE OF SEQUENCE)
            if (tbsCertList.HasData)
            {
                peek = tbsCertList.PeekTag();
                if (peek.TagClass == System.Formats.Asn1.TagClass.Universal &&
                    peek.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.Sequence)
                {
                    var revokedCerts = tbsCertList.ReadSequence();
                    while (revokedCerts.HasData)
                    {
                        var entry = revokedCerts.ReadSequence();
                        var serialBytes = entry.ReadIntegerBytes().Span;
                        info.RevokedSerials.Add(Convert.ToHexString(serialBytes));
                    }
                }
            }

            info.RevokedCount = info.RevokedSerials.Count;
            return info;
        }

        private static DateTimeOffset? ReadAsnTime(System.Formats.Asn1.AsnReader reader)
        {
            try
            {
                var tag = reader.PeekTag();
                if (tag.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.UtcTime)
                    return reader.ReadUtcTime();
                if (tag.TagValue == (int)System.Formats.Asn1.UniversalTagNumber.GeneralizedTime)
                    return reader.ReadGeneralizedTime();
            }
            catch { /* ignore */ }
            return null;
        }

        private static byte[]? ConvertPemToDer(byte[] fileBytes)
        {
            var text = System.Text.Encoding.ASCII.GetString(fileBytes);
            if (text.Contains("-----BEGIN"))
            {
                // PEM format — strip headers and base64 decode
                var b64 = string.Join("", text.Split('\n')
                    .Where(l => !l.StartsWith("-----") && !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim()));
                return Convert.FromBase64String(b64);
            }
            return fileBytes; // Already DER
        }

        private static OpcUaCertificateInfo BuildCertInfo(X509Certificate2 cert, string store)
        {
            var keySize = cert.PublicKey.GetRSAPublicKey()?.KeySize
                       ?? cert.PublicKey.GetECDsaPublicKey()?.KeySize
                       ?? 0;

            return new OpcUaCertificateInfo
            {
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                Thumbprint = cert.Thumbprint,
                SerialNumber = cert.SerialNumber,
                NotBefore = cert.NotBefore.ToUniversalTime(),
                NotAfter = cert.NotAfter.ToUniversalTime(),
                DaysUntilExpiry = (int)(cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays,
                KeySize = keySize,
                SignatureAlgorithm = cert.SignatureAlgorithm.FriendlyName ?? "Unknown",
                IsSelfSigned = cert.Subject == cert.Issuer,
                IsValid = DateTime.UtcNow >= cert.NotBefore.ToUniversalTime() && DateTime.UtcNow <= cert.NotAfter.ToUniversalTime(),
                Store = store
            };
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c) && c != ' ').ToArray());
            return string.IsNullOrEmpty(sanitized) ? "certificate" : sanitized;
        }
    }
}
