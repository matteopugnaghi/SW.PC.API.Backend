// ============================================================================
// IExportService.cs / ExportService.cs — Orquestador del Export Manager Wizard
// ============================================================================
// Responsabilidades:
//   - CRUD de ExportTask en la BD del proyecto activo.
//   - Ejecutar una tarea: provider → formatter → runners (por cada destino).
//   - Preview (5 filas) usando provider.GetDatasetAsync con PreviewLimit.
//   - Persistir LastRunAt / LastResult.
//   - NO emite audit log directamente: lo hace el controller (tiene HttpContext).
//
// Lectura de SystemConfig (AllowedExportFolders, SMTP) → vía IExcelConfigService.
// Mientras Commit 6 no añada esas props al SystemConfiguration, devolverá vacío
// y los runners producirán ExportResult.Success=false con mensaje claro.
// ============================================================================

using System.Text.Json;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportService
{
    Task<List<ExportTaskResponse>> GetTasksAsync(string? source, CancellationToken ct = default);
    Task<ExportTaskResponse?> GetTaskByIdAsync(int id, CancellationToken ct = default);
    Task<ExportTaskResponse> CreateTaskAsync(ExportTaskRequest req, string createdBy, CancellationToken ct = default);
    Task<ExportTaskResponse?> UpdateTaskAsync(int id, ExportTaskRequest req, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default);
    Task<ExportTaskResponse?> ToggleTaskAsync(int id, bool enabled, CancellationToken ct = default);
    Task<ExportRunResponse> RunTaskAsync(int id, Dictionary<string, object?>? runtimeMetadata = null, CancellationToken ct = default);
    Task<ExportDataset> PreviewAsync(string datasetProvider, ExportSelection selection, CancellationToken ct = default);
    Task<ExportEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default);
}

/// <summary>
/// Información del entorno de exportación del proyecto activo. Usada por el Wizard
/// (Step 2/3) para deshabilitar checkboxes/controles cuando falta configuración.
/// </summary>
public class ExportEnvironmentInfo
{
    public IReadOnlyList<string> AllowedFolders { get; init; } = Array.Empty<string>();
    public bool SmtpConfigured { get; init; }
    public List<ExportFolderProfileResponse> FolderProfiles { get; init; } = new();
    public List<ExportEmailProfileResponse> EmailProfiles { get; init; } = new();
}

public class ExportService : IExportService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IExportDatasetRegistry _registry;
    private readonly IExportFormatterService _formatter;
    private readonly IEnumerable<IExportRunner> _runners;
    private readonly IRequestProjectContext _projectContext;
    private readonly IExcelConfigService _excelConfig;
    private readonly IExportProfileService _profiles;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IProjectDbContextFactory dbFactory,
        IExportDatasetRegistry registry,
        IExportFormatterService formatter,
        IEnumerable<IExportRunner> runners,
        IRequestProjectContext projectContext,
        IExcelConfigService excelConfig,
        IExportProfileService profiles,
        ILogger<ExportService> logger)
    {
        _dbFactory = dbFactory;
        _registry = registry;
        _formatter = formatter;
        _runners = runners;
        _projectContext = projectContext;
        _excelConfig = excelConfig;
        _profiles = profiles;
        _logger = logger;
    }

    // ───────────────────── CRUD ─────────────────────
    public async Task<List<ExportTaskResponse>> GetTasksAsync(string? source, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.ExportTasks.AsQueryable();
        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(t => t.Source == source);

        var list = new List<ExportTaskResponse>();
        foreach (var t in await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                            .ToListAsync(query.OrderByDescending(t => t.CreatedAt), ct))
        {
            list.Add(ToResponse(t));
        }
        return list;
    }

    public async Task<ExportTaskResponse?> GetTaskByIdAsync(int id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var task = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .FirstOrDefaultAsync(db.ExportTasks, t => t.Id == id, ct);
        return task is null ? null : ToResponse(task);
    }

    public async Task<ExportTaskResponse> CreateTaskAsync(ExportTaskRequest req, string createdBy, CancellationToken ct = default)
    {
        ValidateRequest(req);

        using var db = _dbFactory.CreateDbContext();
        var entity = new ExportTask
        {
            ProjectId = _projectContext.ProjectId ?? string.Empty,
            Source = req.Source,
            Name = req.Name,
            ExecutionType = req.ExecutionType,
            CronExpression = req.CronExpression,
            PlcVariable = req.PlcVariable,
            Format = req.Format,
            Destinations = string.Join(",", req.Destinations.Select(d => d.Trim().ToLowerInvariant())),
            ConfigJson = JsonSerializer.Serialize(req.Config),
            DatasetProvider = req.DatasetProvider,
            SelectionJson = JsonSerializer.Serialize(req.Selection),
            Enabled = req.Enabled,
            FolderProfileId = string.IsNullOrWhiteSpace(req.FolderProfileId) ? null : req.FolderProfileId,
            EmailProfileId = string.IsNullOrWhiteSpace(req.EmailProfileId) ? null : req.EmailProfileId,
            EmailRecipients = string.IsNullOrWhiteSpace(req.EmailRecipients) ? null : req.EmailRecipients,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        db.ExportTasks.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToResponse(entity);
    }

    public async Task<ExportTaskResponse?> UpdateTaskAsync(int id, ExportTaskRequest req, CancellationToken ct = default)
    {
        ValidateRequest(req);

        using var db = _dbFactory.CreateDbContext();
        var entity = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .FirstOrDefaultAsync(db.ExportTasks, t => t.Id == id, ct);
        if (entity is null) return null;

        entity.Source = req.Source;
        entity.Name = req.Name;
        entity.ExecutionType = req.ExecutionType;
        entity.CronExpression = req.CronExpression;
        entity.PlcVariable = req.PlcVariable;
        entity.Format = req.Format;
        entity.Destinations = string.Join(",", req.Destinations.Select(d => d.Trim().ToLowerInvariant()));
        entity.ConfigJson = JsonSerializer.Serialize(req.Config);
        entity.DatasetProvider = req.DatasetProvider;
        entity.SelectionJson = JsonSerializer.Serialize(req.Selection);
        entity.Enabled = req.Enabled;
        entity.FolderProfileId = string.IsNullOrWhiteSpace(req.FolderProfileId) ? null : req.FolderProfileId;
        entity.EmailProfileId = string.IsNullOrWhiteSpace(req.EmailProfileId) ? null : req.EmailProfileId;
        entity.EmailRecipients = string.IsNullOrWhiteSpace(req.EmailRecipients) ? null : req.EmailRecipients;

        await db.SaveChangesAsync(ct);
        return ToResponse(entity);
    }

    public async Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var entity = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .FirstOrDefaultAsync(db.ExportTasks, t => t.Id == id, ct);
        if (entity is null) return false;
        db.ExportTasks.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ExportTaskResponse?> ToggleTaskAsync(int id, bool enabled, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var entity = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                        .FirstOrDefaultAsync(db.ExportTasks, t => t.Id == id, ct);
        if (entity is null) return null;
        entity.Enabled = enabled;
        await db.SaveChangesAsync(ct);
        return ToResponse(entity);
    }

    // ───────────────────── PREVIEW ─────────────────────
    public async Task<ExportDataset> PreviewAsync(string datasetProvider, ExportSelection selection, CancellationToken ct = default)
    {
        var provider = _registry.Get(datasetProvider)
            ?? throw new InvalidOperationException($"Dataset provider '{datasetProvider}' no registrado.");
        selection.PreviewLimit ??= 5;
        return await provider.GetDatasetAsync(selection, ct);
    }

    // ───────────────────── RUN ─────────────────────
    public async Task<ExportRunResponse> RunTaskAsync(int id, Dictionary<string, object?>? runtimeMetadata = null, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var task = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .FirstOrDefaultAsync(db.ExportTasks, t => t.Id == id, ct)
                    ?? throw new InvalidOperationException($"ExportTask {id} no encontrada.");

        var response = new ExportRunResponse { TaskId = id };

        try
        {
            // 1) Resolver provider y dataset
            var provider = _registry.Get(task.DatasetProvider)
                ?? throw new InvalidOperationException($"Dataset provider '{task.DatasetProvider}' no registrado.");

            var selection = DeserializeSelection(task.SelectionJson);
            selection.PreviewLimit = null; // ejecución real, no preview
            if (runtimeMetadata is not null)
            {
                foreach (var kv in runtimeMetadata) selection.Metadata[kv.Key] = kv.Value;
            }
            var dataset = await provider.GetDatasetAsync(selection, ct);

            // 2) Cargar config (necesario antes del formatter para pasarle el diseño)
            var config = DeserializeConfig(task.ConfigJson);

            // 2.b) Inyectar metadatos auxiliares para el diseño del informe (cabecera/filtros).
            //
            // Combinamos en `appliedFilters` (en orden de prioridad creciente):
            //   1) selection.Filters       (lo guardado en el wizard — currentFilters del host)
            //   2) selection.Metadata["appliedFilters"]  (vino en runtimeMetadata desde el frontend
            //      al pulsar "Ejecutar"; típicamente { dateRange: { from, to } } del popup manual)
            //   3) dataset.Metadata["appliedFilters"]    (si el provider ya añadió alguno)
            {
                var combined = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (selection.Filters is not null)
                {
                    foreach (var kv in selection.Filters) combined[kv.Key] = kv.Value;
                }
                if (selection.Metadata.TryGetValue("appliedFilters", out var runtimeAf) && runtimeAf is not null)
                {
                    MergeAppliedFilters(combined, runtimeAf);
                }
                if (dataset.Metadata.TryGetValue("appliedFilters", out var dsAf) && dsAf is not null)
                {
                    MergeAppliedFilters(combined, dsAf);
                }
                if (combined.Count > 0)
                {
                    dataset.Metadata["appliedFilters"] = combined;
                }
            }
            if (!dataset.Metadata.ContainsKey("projectId")
                && !string.IsNullOrWhiteSpace(task.ProjectId))
            {
                dataset.Metadata["projectId"] = task.ProjectId;
            }

            // 3) Formatear bytes (incluyendo diseño del informe si aplica)
            var formatted = _formatter.Format(dataset, task.Format, config.Report);
            var filename = ResolveFilenameTokens(config.Filename, formatted.Extension, dataset.Metadata);

            // 4) Cargar SystemConfig (AllowedFolders + SMTP legacy desde Excel)
            var (allowedFolders, smtpFromExcel) = await LoadEnvironmentAsync();

            // 4.b) Resolver perfil de carpeta (DB) — sobreescribe config.Folder
            if (!string.IsNullOrWhiteSpace(task.FolderProfileId))
            {
                var folderProfile = await _profiles.GetFolderProfileEntityAsync(task.FolderProfileId!, ct);
                if (folderProfile is not null)
                {
                    var basePath = folderProfile.Path?.Trim() ?? string.Empty;
                    var sub = string.IsNullOrWhiteSpace(folderProfile.Subfolder)
                        ? null
                        : ResolveFilenameTokens(folderProfile.Subfolder!, "tmp", dataset.Metadata)
                            .Replace(".tmp", "", StringComparison.OrdinalIgnoreCase);
                    config.Folder = string.IsNullOrEmpty(sub) ? basePath : Path.Combine(basePath, sub);
                }
                else
                {
                    _logger.LogWarning("[Export] FolderProfileId {Id} no encontrado para task {TaskId}.", task.FolderProfileId, task.Id);
                }
            }

            // 4.c) Resolver perfil SMTP (DB) — si null, usa el legacy de Excel
            ExportSmtpSettings? smtp = smtpFromExcel;
            if (!string.IsNullOrWhiteSpace(task.EmailProfileId))
            {
                smtp = await _profiles.ResolveSmtpAsync(task.EmailProfileId!, ct) ?? smtpFromExcel;
            }

            // 4.d) Destinatarios específicos de la tarea → override de To
            if (!string.IsNullOrWhiteSpace(task.EmailRecipients) && config.Email is not null)
            {
                var list = task.EmailRecipients!
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (list.Count > 0) config.Email.To = list;
            }

            // 5) Ejecutar cada destino
            var destinations = ParseDestinations(task.Destinations);
            foreach (var dest in destinations)
            {
                var runner = _runners.FirstOrDefault(r =>
                    string.Equals(r.DestinationType, dest, StringComparison.OrdinalIgnoreCase));

                if (runner is null)
                {
                    response.Results.Add(new ExportResult
                    {
                        DestinationType = dest,
                        Success = false,
                        ErrorMessage = $"No hay runner registrado para destino '{dest}'."
                    });
                    continue;
                }

                var ctx = new ExportRunContext
                {
                    Task = task,
                    Config = config,
                    File = formatted,
                    Filename = filename,
                    AllowedFolders = allowedFolders,
                    Smtp = smtp
                };
                response.Results.Add(await runner.ExecuteAsync(ctx, ct));
            }

            // 6) Resumen
            var okCount = response.Results.Count(r => r.Success);
            response.Success = okCount > 0;
            response.Summary = okCount == response.Results.Count ? "ok"
                : okCount > 0 ? $"ok (parcial {okCount}/{response.Results.Count})"
                : "error: ningún destino completado";

            task.LastRunAt = DateTime.UtcNow;
            task.LastResult = response.Summary;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Export] Error ejecutando tarea {Id}", id);
            response.Success = false;
            response.Summary = $"error: {ex.Message}";
            task.LastRunAt = DateTime.UtcNow;
            task.LastResult = Truncate(response.Summary, 500);
            try { await db.SaveChangesAsync(ct); } catch { /* no enmascarar excepción original */ }
        }

        return response;
    }

    // ───────────────────── Entorno (Wizard / Run) ─────────────────────

    /// <summary>
    /// Devuelve el entorno público para el Wizard. Como ahora los destinos se
    /// configuran como perfiles desde la UI, ambos están siempre habilitados;
    /// devolvemos los perfiles existentes para el dropdown del paso 3.
    /// </summary>
    public async Task<ExportEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default)
    {
        var (folders, _) = await LoadEnvironmentAsync();
        var folderProfiles = await _profiles.ListFolderProfilesAsync(ct);
        var emailProfiles = await _profiles.ListEmailProfilesAsync(ct);
        return new ExportEnvironmentInfo
        {
            AllowedFolders = folders,
            SmtpConfigured = emailProfiles.Count > 0,
            FolderProfiles = folderProfiles,
            EmailProfiles = emailProfiles,
        };
    }

    // ───────────────────── Helpers ─────────────────────
    private async Task<(IReadOnlyList<string> Folders, ExportSmtpSettings? Smtp)> LoadEnvironmentAsync()
    {
        try
        {
            var sysCfg = await _excelConfig.LoadSystemConfigurationAsync(_projectContext.ExcelConfigPath);

            var folders = (sysCfg.AllowedExportFolders ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            ExportSmtpSettings? smtp = null;
            if (!string.IsNullOrWhiteSpace(sysCfg.SmtpHost))
            {
                smtp = new ExportSmtpSettings
                {
                    Host = sysCfg.SmtpHost,
                    Port = sysCfg.SmtpPort,
                    Username = string.IsNullOrWhiteSpace(sysCfg.SmtpUser) ? null : sysCfg.SmtpUser,
                    Password = string.IsNullOrWhiteSpace(sysCfg.SmtpPass) ? null : sysCfg.SmtpPass,
                    From = sysCfg.SmtpFrom ?? string.Empty,
                    EnableSsl = sysCfg.SmtpEnableSsl
                };
            }

            return (folders, smtp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Export] No se pudo cargar SystemConfig — destinos local/email reportarán fallo.");
            return (Array.Empty<string>(), null);
        }
    }

    private static IReadOnlyList<string> ParseDestinations(string csv) =>
        string.IsNullOrWhiteSpace(csv) ? Array.Empty<string>()
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Select(s => s.ToLowerInvariant())
             .Distinct()
             .ToArray();

    private static ExportConfig DeserializeConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ExportConfig();
        try { return JsonSerializer.Deserialize<ExportConfig>(json) ?? new ExportConfig(); }
        catch { return new ExportConfig(); }
    }

    private static ExportSelection DeserializeSelection(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ExportSelection();
        try { return JsonSerializer.Deserialize<ExportSelection>(json) ?? new ExportSelection(); }
        catch { return new ExportSelection(); }
    }

    /// <summary>
    /// Mergea entradas de filtros adicionales sobre `target`. Acepta:
    ///   - IDictionary&lt;string,object?&gt;
    ///   - JsonElement Object
    /// Las claves nuevas se añaden; las existentes se sobrescriben.
    /// </summary>
    private static void MergeAppliedFilters(Dictionary<string, object?> target, object source)
    {
        if (source is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict) target[kv.Key] = kv.Value;
            return;
        }
        if (source is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in je.EnumerateObject()) target[prop.Name] = prop.Value;
        }
    }

    /// <summary>
    /// Resuelve tokens del filename. Soporta:
    ///   {fecha} → yyyy-MM-dd
    ///   {hora}  → HH-mm-ss
    ///   {datetime} → yyyy-MM-dd_HH-mm-ss
    ///   {clave} → dataset.Metadata["clave"] si existe
    /// Garantiza extensión correcta al final.
    /// </summary>
    private static string ResolveFilenameTokens(string template, string extension, Dictionary<string, object?> metadata)
    {
        if (string.IsNullOrWhiteSpace(template))
            template = $"export_{{datetime}}.{extension}";

        var now = DateTime.Now;
        var resolved = template
            .Replace("{fecha}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{hora}", now.ToString("HH-mm-ss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{datetime}", now.ToString("yyyy-MM-dd_HH-mm-ss"), StringComparison.OrdinalIgnoreCase);

        foreach (var kv in metadata)
        {
            if (kv.Value is null) continue;
            resolved = resolved.Replace("{" + kv.Key + "}", kv.Value.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Saneado: caracteres inválidos → '_'
        foreach (var ch in Path.GetInvalidFileNameChars())
            resolved = resolved.Replace(ch, '_');

        // Garantiza la extensión correcta
        var expected = "." + extension;
        if (!resolved.EndsWith(expected, StringComparison.OrdinalIgnoreCase))
            resolved += expected;

        return resolved;
    }

    private static void ValidateRequest(ExportTaskRequest req)
    {
        if (req.Destinations.Count == 0)
            throw new ArgumentException("Debe seleccionar al menos un destino.");

        var valid = new[] { "local", "email" };
        foreach (var d in req.Destinations)
        {
            if (!valid.Contains(d.Trim().ToLowerInvariant()))
                throw new ArgumentException($"Destino no soportado: '{d}'.");
        }

        var validFmt = new[] { "xlsx", "csv", "json", "html", "png" };
        if (!validFmt.Contains(req.Format.ToLowerInvariant()))
            throw new ArgumentException($"Formato no soportado: '{req.Format}'.");

        var validExec = new[] { "manual", "cron", "plc" };
        if (!validExec.Contains(req.ExecutionType.ToLowerInvariant()))
            throw new ArgumentException($"ExecutionType no soportado: '{req.ExecutionType}'.");

        if (string.Equals(req.ExecutionType, "cron", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(req.CronExpression))
                throw new ArgumentException("ExecutionType='cron' requiere CronExpression.");
            var (ok, error, _) = CronExpressionEvaluator.TryParse(req.CronExpression);
            if (!ok)
                throw new ArgumentException($"CronExpression inválida: {error}");
        }

        if (string.Equals(req.ExecutionType, "plc", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(req.PlcVariable))
            throw new ArgumentException("ExecutionType='plc' requiere PlcVariable.");
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static ExportTaskResponse ToResponse(ExportTask t) => new()
    {
        Id = t.Id,
        ProjectId = t.ProjectId,
        Source = t.Source,
        Name = t.Name,
        ExecutionType = t.ExecutionType,
        CronExpression = t.CronExpression,
        PlcVariable = t.PlcVariable,
        Format = t.Format,
        Destinations = ParseDestinations(t.Destinations).ToList(),
        DatasetProvider = t.DatasetProvider,
        Config = DeserializeConfig(t.ConfigJson),
        Selection = DeserializeSelection(t.SelectionJson),
        Enabled = t.Enabled,
        CreatedBy = t.CreatedBy,
        CreatedAt = t.CreatedAt,
        LastRunAt = t.LastRunAt,
        LastResult = t.LastResult,
        FolderProfileId = t.FolderProfileId,
        EmailProfileId = t.EmailProfileId,
        EmailRecipients = t.EmailRecipients,
    };
}
