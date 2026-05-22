// ============================================================================
// ModelAssetValidationService.cs — SCG-05 / SCG-66 / SCG-143 (EU CRA)
// ============================================================================
// Background service que, al arranque, valida los assets 3D del proyecto activo:
//   • Magic bytes (.glb → "glTF" / 0x46546C67) — SCG-143
//   • Tamaño máximo configurable (Limits:MaxModel3DSizeMB, default 100 MB) — SCG-66
//   • Extensiones .gltf (JSON) y .obj/.stl también verificadas básicamente
//
// Cualquier asset que viole magic-bytes o supere el límite se reporta como
// LogWarning y se incluye en un audit L1 (AuditCategory.System /
// AuditAction.Model3DValidation) con summary completo (total/valid/oversize/
// badMagic). NO se borran ficheros: la validación es advisory para FAT/SAT.
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public class ModelAssetValidationService : BackgroundService
    {
        private readonly ILogger<ModelAssetValidationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _maxSizeMB;
        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(30);

        // GLB header: bytes 0-3 = "glTF" (ASCII 0x67 0x6C 0x54 0x46)
        private static readonly byte[] GlbMagic = new byte[] { 0x67, 0x6C, 0x54, 0x46 };

        public ModelAssetValidationService(
            ILogger<ModelAssetValidationService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _maxSizeMB = configuration.GetValue<int>("Limits:MaxModel3DSizeMB", 100);

            _logger.LogInformation(
                "🛡️ ModelAssetValidationService initialized — max size: {MaxMB} MB, initial delay: {Delay}s",
                _maxSizeMB, (int)_initialDelay.TotalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(_initialDelay, stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            try
            {
                await RunValidationAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ModelAssetValidationService: unexpected error during startup validation");
            }
        }

        private async Task RunValidationAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var modelService = scope.ServiceProvider.GetRequiredService<IModelService>();
            var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var projectContext = scope.ServiceProvider.GetService<IProjectContextService>();
            var projectId = projectContext?.ActiveProjectId ?? "default";
            var modelsPath = modelService.GetModelsPath();

            if (!Directory.Exists(modelsPath))
            {
                _logger.LogInformation("🛡️ Model validation skipped — folder does not exist: {Path}", modelsPath);
                return;
            }

            var started = DateTime.Now;
            long maxBytes = (long)_maxSizeMB * 1024 * 1024;
            int total = 0, valid = 0, oversize = 0, badMagic = 0, unreadable = 0;
            var anomalies = new List<string>();

            // v1.7.2: recursivo para soportar subcarpetas como Projects/{id}/models/Pumps/, /Tanks/, etc.
            var files = Directory.EnumerateFiles(modelsPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".glb" || ext == ".gltf" || ext == ".obj" || ext == ".stl";
                })
                .ToList();

            foreach (var filePath in files)
            {
                if (ct.IsCancellationRequested) break;
                total++;
                var fileName = Path.GetFileName(filePath);
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                long sizeBytes;
                bool magicOk;

                try
                {
                    var fi = new FileInfo(filePath);
                    sizeBytes = fi.Length;
                    magicOk = await CheckMagicBytesAsync(filePath, ext, ct);
                }
                catch (Exception ex)
                {
                    unreadable++;
                    anomalies.Add($"{fileName}=UNREADABLE({ex.GetType().Name})");
                    _logger.LogWarning(ex, "🛡️ Could not read model asset for validation: {File}", fileName);
                    continue;
                }

                bool over = sizeBytes > maxBytes;
                if (over) oversize++;
                if (!magicOk) badMagic++;

                if (over)
                {
                    anomalies.Add($"{fileName}=OVERSIZE({sizeBytes / 1024 / 1024}MB>{_maxSizeMB}MB)");
                    _logger.LogWarning(
                        "🛡️ SCG-66: model '{File}' exceeds max size — {Actual} MB > {Limit} MB",
                        fileName, sizeBytes / 1024 / 1024, _maxSizeMB);
                }
                if (!magicOk)
                {
                    anomalies.Add($"{fileName}=BAD_MAGIC({ext})");
                    _logger.LogWarning(
                        "🛡️ SCG-143: model '{File}' failed magic-bytes check for extension {Ext}",
                        fileName, ext);
                }
                if (!over && magicOk) valid++;
            }

            var durationMs = (DateTime.Now - started).TotalMilliseconds;
            var anomaliesText = anomalies.Count == 0
                ? "none"
                : string.Join(", ", anomalies.Take(20)) + (anomalies.Count > 20 ? $" (+{anomalies.Count - 20} more)" : "");
            var details =
                $"project={projectId}; modelsPath={modelsPath}; total={total}; valid={valid}; " +
                $"oversize={oversize}; badMagic={badMagic}; unreadable={unreadable}; " +
                $"maxSizeMB={_maxSizeMB}; anomalies=[{anomaliesText}]";

            var result = (oversize > 0 || badMagic > 0 || unreadable > 0)
                ? AuditResult.Warning
                : AuditResult.Success;

            try
            {
                await auditLog.LogAsync(
                    AuditCategory.System,
                    AuditAction.Model3DValidation,
                    result,
                    details: details,
                    userId: "system",
                    userName: "ModelAssetValidationService",
                    affectedItemCount: total,
                    durationMs: durationMs,
                    projectId: projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to write Model3DValidation audit entry");
            }

            _logger.LogInformation(
                "🛡️ Model validation finished — result: {Result}, total={Total}, valid={Valid}, " +
                "oversize={Oversize}, badMagic={BadMagic}, unreadable={Unreadable}, duration={Duration:N0} ms",
                result, total, valid, oversize, badMagic, unreadable, durationMs);
        }

        private static async Task<bool> CheckMagicBytesAsync(string filePath, string ext, CancellationToken ct)
        {
            await using var fs = File.OpenRead(filePath);
            var header = new byte[8];
            var read = await fs.ReadAsync(header.AsMemory(0, 8), ct);
            if (read < 4) return false;

            switch (ext)
            {
                case ".glb":
                    // glTF binary: bytes 0-3 = "glTF"
                    return header[0] == GlbMagic[0] && header[1] == GlbMagic[1]
                        && header[2] == GlbMagic[2] && header[3] == GlbMagic[3];
                case ".gltf":
                    // JSON: first non-whitespace must be '{'
                    for (int i = 0; i < read; i++)
                    {
                        var b = header[i];
                        if (b == 0x20 || b == 0x09 || b == 0x0A || b == 0x0D) continue;
                        return b == 0x7B; // '{'
                    }
                    return false;
                case ".obj":
                case ".stl":
                    // Sin firma binaria estándar fiable; aceptar (size-only check aplica).
                    return true;
                default:
                    return true;
            }
        }
    }
}
