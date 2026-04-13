using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Background service that downloads CRL (Certificate Revocation List) from HTTP URL.
    /// Only active when CertificateMode=CA, CrlCheckEnabled=true, and CrlUrl is set.
    /// Downloaded .crl files are stored in %LOCALAPPDATA%\Aquafrisch\opcua-certs\issuers\crl\
    /// </summary>
    public class OpcUaCrlDownloadService : BackgroundService
    {
        private readonly ILogger<OpcUaCrlDownloadService> _logger;
        private readonly IOpcUaServerService _opcUaService;
        private readonly IOpcUaCertificateService _certService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;

        public OpcUaCrlDownloadService(
            ILogger<OpcUaCrlDownloadService> logger,
            IOpcUaServerService opcUaService,
            IOpcUaCertificateService certService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _opcUaService = opcUaService;
            _certService = certService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for OPC UA server to load config from Excel
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var config = _opcUaService.GetConfig();

            // Only active in CA mode
            if (!string.Equals(config.CertificateMode, "ca", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("🔐 [CRL] CRL download DISABLED — CertificateMode={Mode} (requires 'ca')",
                    config.CertificateMode);
                return;
            }

            if (!config.CrlCheckEnabled)
            {
                _logger.LogInformation("🔐 [CRL] CRL download DISABLED — OpcUa_CrlCheckEnabled=false");
                return;
            }

            if (string.IsNullOrWhiteSpace(config.CrlUrl))
            {
                _logger.LogWarning("🔐 [CRL] CRL download DISABLED — OpcUa_CrlUrl is empty");
                return;
            }

            var interval = config.CrlCheckInterval > 0 ? config.CrlCheckInterval : 604800; // default: 1 week
            _logger.LogInformation("🔐 [CRL] CRL download ENABLED — URL: {Url}, interval: {Interval}s ({Hours}h)",
                config.CrlUrl, interval, interval / 3600.0);

            // Run first download immediately
            await DownloadCrl(config.CrlUrl, stoppingToken);

            // Then loop on interval
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
                    await DownloadCrl(config.CrlUrl, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🔐 [CRL] Unexpected error in download loop");
                    // Wait before retrying (5 minutes)
                    try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            _logger.LogInformation("🔐 [CRL] Service stopped");
        }

        private async Task DownloadCrl(string url, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                _logger.LogInformation("🔐 [CRL] Downloading CRL from {Url}...", url);

                var client = _httpClientFactory.CreateClient("CrlDownload");
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadAsByteArrayAsync(ct);
                if (data.Length == 0)
                {
                    _logger.LogWarning("🔐 [CRL] Empty response from {Url}", url);
                    return;
                }

                // Save to issuers/crl/ folder
                var crlDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aquafrisch", "opcua-certs", "issuers", "crl");
                Directory.CreateDirectory(crlDir);

                // Use filename from URL, or default to "ca.crl"
                var fileName = GetFileNameFromUrl(url);
                var crlPath = Path.Combine(crlDir, fileName);

                // Check if file changed (compare bytes)
                var changed = true;
                if (File.Exists(crlPath))
                {
                    var existing = await File.ReadAllBytesAsync(crlPath, ct);
                    if (existing.SequenceEqual(data))
                    {
                        changed = false;
                        _logger.LogInformation("🔐 [CRL] CRL unchanged ({Size} bytes) — {File}",
                            data.Length, fileName);
                    }
                }

                if (changed)
                {
                    await File.WriteAllBytesAsync(crlPath, data, ct);
                    _logger.LogInformation("🔐 [CRL] CRL updated: {File} ({Size} bytes)", fileName, data.Length);

                    // Parse and log revoked count
                    var crls = _certService.GetCrlFiles();
                    var totalRevoked = crls.Sum(c => c.RevokedCount);
                    _logger.LogInformation("🔐 [CRL] Total CRL files: {Count}, total revoked certificates: {Revoked}",
                        crls.Count, totalRevoked);

                    await _auditLogService.LogAsync(
                        AuditCategory.OtCommunication,
                        AuditAction.CrlDownload,
                        AuditResult.Success,
                        details: $"CRL downloaded from {url} — {fileName} ({data.Length} bytes, {totalRevoked} revoked)",
                        userId: "System");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("🔐 [CRL] Failed to download CRL from {Url}: {Error}", url, ex.Message);
                await _auditLogService.LogAsync(
                    AuditCategory.OtCommunication,
                    AuditAction.CrlDownload,
                    AuditResult.Failure,
                    details: $"Failed to download CRL from {url}: {ex.Message}",
                    userId: "System");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("🔐 [CRL] Download timed out for {Url}", url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔐 [CRL] Error processing CRL from {Url}", url);
            }
        }

        private static string GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(name) && name.EndsWith(".crl", StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            catch { }
            return "ca.crl";
        }
    }
}
