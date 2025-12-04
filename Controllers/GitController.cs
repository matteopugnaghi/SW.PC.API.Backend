using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SW.PC.API.Backend.Controllers;

// Modelo para registro de backups
public class BackupLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Repository { get; set; } = "";
    public string MachineId { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public string FileName { get; set; } = "";
    public string LastCommitHash { get; set; } = "";
    public string Branch { get; set; } = "";
    public bool WasSyncedWithRemote { get; set; }
    public string Reason { get; set; } = ""; // "Manual" o "Offline Backup"
}

// Modelo para certificados de deployment (generados en cada push)
public class DeploymentCertificate
{
    public string CertificateId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Repository { get; set; } = "";
    public string MachineId { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public string CommitHash { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Action { get; set; } = ""; // "Push", "Commit+Push"
    public string Description { get; set; } = "";
    public string IntegrityHash { get; set; } = "";
}

[ApiController]
[Route("api/[controller]")]
public class GitController : ControllerBase
{
    private readonly IGitOperationsService _gitService;
    private readonly ILogger<GitController> _logger;
    private static readonly string BackupLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup_log.json");
    private static readonly string DeploymentLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deployment_certificates.json");

    public GitController(IGitOperationsService gitService, ILogger<GitController> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    private Dictionary<string, string> GetRepoPaths()
    {
        var paths = _gitService.GetRepoPaths();
        return new Dictionary<string, string>
        {
            ["backend"] = paths.Backend,
            ["frontend"] = paths.Frontend,
            ["twincat"] = paths.TwinCAT
        };
    }

    [HttpGet("status")]
    public async Task<ActionResult<AllRepositoriesStatus>> GetAllStatus()
    {
        _logger.LogInformation("Getting status of all repositories");
        var status = await _gitService.GetAllRepositoriesStatusAsync();
        return Ok(status);
    }

    [HttpGet("status/{repoName}")]
    public async Task<ActionResult<RepositoryStatus>> GetRepositoryStatus(string repoName)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        var status = await _gitService.GetRepositoryStatusAsync(repoPath);
        return Ok(status);
    }

    [HttpGet("history/{repoName}")]
    public async Task<ActionResult<List<CommitInfo>>> GetCommitHistory(string repoName, [FromQuery] int count = 20)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        var history = await _gitService.GetCommitHistoryAsync(repoPath, count);
        return Ok(history);
    }

    [HttpGet("modified/{repoName}")]
    public async Task<ActionResult<List<ModifiedFile>>> GetModifiedFiles(string repoName)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        var files = await _gitService.GetModifiedFilesAsync(repoPath);
        return Ok(files);
    }

    [HttpPost("commit/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> Commit(string repoName, [FromBody] CommitRequest request)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Commit message is required");
        _logger.LogInformation("Commit request for {Repo}: {Message}", repoName, request.Message);
        var result = await _gitService.CommitAsync(repoPath, request.Message);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("push/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> Push(string repoName, [FromQuery] string? operatorName = null)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        _logger.LogInformation("Push request for {Repo}", repoName);
        var result = await _gitService.PushAsync(repoPath);
        
        // Generar certificado automático después de push exitoso
        if (result.Success)
        {
            await GenerateDeploymentCertificateAsync(repoName, repoPath, operatorName ?? "System", "Push to remote");
        }
        
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("commit-and-push/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> CommitAndPush(string repoName, [FromBody] CommitRequest request)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Commit message is required");
        var commitResult = await _gitService.CommitAsync(repoPath, request.Message);
        if (!commitResult.Success) return BadRequest(commitResult);
        var pushResult = await _gitService.PushAsync(repoPath);
        if (!pushResult.Success) return BadRequest(new GitOperationResult { Success = false, Message = $"Commit succeeded but push failed: {pushResult.Message}" });
        
        // Extraer nombre del autor del mensaje de commit [Autor: XXX]
        var operatorName = ExtractAuthorFromMessage(request.Message);
        await GenerateDeploymentCertificateAsync(repoName, repoPath, operatorName, $"Commit+Push: {request.Message}");
        
        return Ok(new GitOperationResult { Success = true, Message = "Commit and push completed successfully. Deployment certificate generated." });
    }

    [HttpPost("discard/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> DiscardChanges(string repoName, [FromBody] DiscardRequest? request = null)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        _logger.LogWarning("Discard request for {Repo}, file: {File}", repoName, request?.FilePath ?? "ALL");
        var result = await _gitService.DiscardChangesAsync(repoPath, request?.FilePath);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("revert/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> RevertToCommit(string repoName, [FromBody] RevertRequest request)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        if (string.IsNullOrWhiteSpace(request.CommitHash)) return BadRequest("Commit hash is required");
        _logger.LogWarning("REVERT request for {Repo} to commit {Hash}", repoName, request.CommitHash);
        var result = await _gitService.RevertToCommitAsync(repoPath, request.CommitHash);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("commit-all")]
    public async Task<ActionResult<Dictionary<string, GitOperationResult>>> CommitAll([FromBody] CommitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Commit message is required");
        var repoPaths = GetRepoPaths();
        var results = new Dictionary<string, GitOperationResult>();
        var status = await _gitService.GetAllRepositoriesStatusAsync();
        foreach (var (name, repoStatus) in status.Repositories)
        {
            if (repoStatus.IsValid && repoStatus.HasChanges)
            {
                var repoPath = repoPaths.GetValueOrDefault(name) ?? "";
                if (!string.IsNullOrEmpty(repoPath)) results[name] = await _gitService.CommitAsync(repoPath, request.Message);
            }
            else if (!repoStatus.HasChanges) results[name] = new GitOperationResult { Success = true, Message = "No changes to commit" };
        }
        return Ok(results);
    }

    [HttpPost("push-all")]
    public async Task<ActionResult<Dictionary<string, GitOperationResult>>> PushAll()
    {
        var repoPaths = GetRepoPaths();
        var results = new Dictionary<string, GitOperationResult>();
        var status = await _gitService.GetAllRepositoriesStatusAsync();
        foreach (var (name, repoStatus) in status.Repositories)
        {
            if (repoStatus.IsValid && repoStatus.CommitsAhead > 0)
            {
                var repoPath = repoPaths.GetValueOrDefault(name) ?? "";
                if (!string.IsNullOrEmpty(repoPath)) results[name] = await _gitService.PushAsync(repoPath);
            }
            else if (repoStatus.CommitsAhead == 0) results[name] = new GitOperationResult { Success = true, Message = "Nothing to push" };
        }
        return Ok(results);
    }

    /// <summary>
    /// Genera un ZIP con el certificado de integridad + código fuente del repositorio seleccionado
    /// Para backup offline cuando no hay conexión a internet (EU CRA compliance)
    /// </summary>
    [HttpGet("backup/{repoName}")]
    public async Task<IActionResult> DownloadBackupWithCertificate(string repoName, [FromQuery] string machineId = "PLANTA_001", [FromQuery] string operatorName = "System")
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");

        if (!Directory.Exists(repoPath))
            return NotFound($"Repository path not found: {repoPath}");

        _logger.LogInformation("📦 Generating backup ZIP for {Repo} - Machine: {Machine}, Operator: {Operator}", repoName, machineId, operatorName);

        try
        {
            // Generar certificado específico para este repo
            var repoStatus = await _gitService.GetRepositoryStatusAsync(repoPath);
            var certificate = GenerateRepoCertificate(repoName, repoStatus, machineId, operatorName);

            // Crear ZIP en memoria
            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                // 1. Añadir certificado JSON
                var certEntry = archive.CreateEntry($"certificate_{repoName}.json");
                using (var certStream = certEntry.Open())
                using (var writer = new StreamWriter(certStream))
                {
                    var certJson = System.Text.Json.JsonSerializer.Serialize(certificate, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await writer.WriteAsync(certJson);
                }

                // 2. Añadir código fuente (excluyendo basura)
                var excludeFolders = GetExcludeFolders(repoName);
                var excludeExtensions = new[] { ".exe", ".dll", ".pdb", ".cache", ".log" };
                
                await AddDirectoryToZipAsync(archive, repoPath, $"source_{repoName}", excludeFolders, excludeExtensions);
            }

            memoryStream.Position = 0;
            var fileName = $"backup_{repoName}_{machineId}_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
            
            // Registrar el backup en el log
            await LogBackupAsync(new BackupLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Repository = repoName,
                MachineId = machineId,
                OperatorName = operatorName,
                FileName = fileName,
                LastCommitHash = repoStatus.LastCommit?.Hash ?? "unknown",
                Branch = repoStatus.CurrentBranch ?? "unknown",
                WasSyncedWithRemote = repoStatus.CommitsAhead == 0,
                Reason = repoStatus.CommitsAhead > 0 ? "Offline Backup (commits pendientes)" : "Manual Export"
            });
            
            _logger.LogInformation("✅ Backup ZIP generated: {FileName}", fileName);
            return File(memoryStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating backup for {Repo}", repoName);
            return StatusCode(500, $"Error generating backup: {ex.Message}");
        }
    }

    private object GenerateRepoCertificate(string repoName, RepositoryStatus status, string machineId, string operatorName)
    {
        return new
        {
            CertificateType = "EU_CRA_Integrity_Certificate",
            Version = "1.0",
            GeneratedAt = DateTime.UtcNow,
            MachineId = machineId,
            OperatorName = operatorName,
            Repository = new
            {
                Name = repoName.ToUpper(),
                Path = status.Path,
                Branch = status.CurrentBranch,
                LastCommit = status.LastCommit,
                HasUncommittedChanges = status.HasChanges,
                ModifiedFilesCount = status.ModifiedFiles?.Count ?? 0,
                CommitsPendingPush = status.CommitsAhead,
                RemoteUrl = status.RemoteUrl,
                IsValid = status.IsValid
            },
            Integrity = new
            {
                Status = status.HasChanges ? "MODIFIED" : "CLEAN",
                SyncedWithRemote = status.CommitsAhead == 0,
                VerificationHash = ComputeSimpleHash($"{status.LastCommit?.Hash}|{status.CurrentBranch}|{DateTime.UtcNow:yyyyMMdd}")
            },
            LegalNotice = "This certificate attests the software integrity state at generation time per EU Cyber Resilience Act requirements."
        };
    }

    private string ComputeSimpleHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16]; // Primeros 16 chars
    }

    private string[] GetExcludeFolders(string repoName)
    {
        return repoName.ToLower() switch
        {
            "backend" => new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages" },
            "frontend" => new[] { "node_modules", ".git", "build", "dist", ".cache", "coverage" },
            "twincat" => new[] { ".git", "_Boot", "_CompileInfo", "__Pou" },
            _ => new[] { ".git", "bin", "obj", "node_modules" }
        };
    }

    #region Backup Log Methods
    
    private async Task LogBackupAsync(BackupLogEntry entry)
    {
        try
        {
            var log = await LoadBackupLogAsync();
            log.Add(entry);
            
            // Mantener solo los últimos 100 registros
            if (log.Count > 100) log = log.Skip(log.Count - 100).ToList();
            
            var json = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(BackupLogPath, json);
            
            _logger.LogInformation("📝 Backup logged: {Repo} by {Operator}", entry.Repository, entry.OperatorName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log backup");
        }
    }

    private async Task<List<BackupLogEntry>> LoadBackupLogAsync()
    {
        try
        {
            if (System.IO.File.Exists(BackupLogPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(BackupLogPath);
                return JsonSerializer.Deserialize<List<BackupLogEntry>>(json) ?? new List<BackupLogEntry>();
            }
        }
        catch { }
        return new List<BackupLogEntry>();
    }

    /// <summary>
    /// Obtiene el historial de backups realizados
    /// </summary>
    [HttpGet("backup-log")]
    public async Task<ActionResult<List<BackupLogEntry>>> GetBackupLog([FromQuery] string? repository = null, [FromQuery] int count = 50)
    {
        var log = await LoadBackupLogAsync();
        
        if (!string.IsNullOrEmpty(repository))
            log = log.Where(e => e.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Ok(log.OrderByDescending(e => e.Timestamp).Take(count).ToList());
    }

    #endregion

    #region Deployment Certificate Methods

    /// <summary>
    /// Extrae el nombre del autor del mensaje de commit [Autor: XXX]
    /// </summary>
    private string ExtractAuthorFromMessage(string message)
    {
        var match = Regex.Match(message, @"\[Autor:\s*([^\]]+)\]");
        return match.Success ? match.Groups[1].Value.Trim() : "System";
    }

    /// <summary>
    /// Genera un certificado de deployment después de cada push exitoso
    /// </summary>
    private async Task GenerateDeploymentCertificateAsync(string repoName, string repoPath, string operatorName, string description)
    {
        try
        {
            var repoStatus = await _gitService.GetRepositoryStatusAsync(repoPath);
            var machineId = Environment.MachineName;
            
            var certificate = new DeploymentCertificate
            {
                CertificateId = $"DEPLOY-{repoName.ToUpper()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                Timestamp = DateTime.UtcNow,
                Repository = repoName,
                MachineId = machineId,
                OperatorName = operatorName,
                CommitHash = repoStatus.LastCommit?.Hash ?? "unknown",
                Branch = repoStatus.CurrentBranch ?? "unknown",
                Action = description.StartsWith("Commit+Push") ? "Commit+Push" : "Push",
                Description = description,
                IntegrityHash = ComputeSimpleHash($"{repoStatus.LastCommit?.Hash}|{repoName}|{DateTime.UtcNow:yyyyMMddHHmmss}")
            };

            var certificates = await LoadDeploymentCertificatesAsync();
            certificates.Add(certificate);
            
            // Mantener últimos 200 certificados
            if (certificates.Count > 200) certificates = certificates.Skip(certificates.Count - 200).ToList();
            
            var json = JsonSerializer.Serialize(certificates, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(DeploymentLogPath, json);
            
            _logger.LogInformation("📜 Deployment certificate generated: {CertId} for {Repo} by {Operator}", 
                certificate.CertificateId, repoName, operatorName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate deployment certificate");
        }
    }

    private async Task<List<DeploymentCertificate>> LoadDeploymentCertificatesAsync()
    {
        try
        {
            if (System.IO.File.Exists(DeploymentLogPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(DeploymentLogPath);
                return JsonSerializer.Deserialize<List<DeploymentCertificate>>(json) ?? new List<DeploymentCertificate>();
            }
        }
        catch { }
        return new List<DeploymentCertificate>();
    }

    /// <summary>
    /// Obtiene el historial de certificados de deployment (generados en cada push)
    /// </summary>
    [HttpGet("deployment-certificates")]
    public async Task<ActionResult<List<DeploymentCertificate>>> GetDeploymentCertificates([FromQuery] string? repository = null, [FromQuery] int count = 50)
    {
        var certificates = await LoadDeploymentCertificatesAsync();
        
        if (!string.IsNullOrEmpty(repository))
            certificates = certificates.Where(c => c.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Ok(certificates.OrderByDescending(c => c.Timestamp).Take(count).ToList());
    }

    /// <summary>
    /// Descarga todos los certificados de deployment como archivo JSON para auditoría
    /// </summary>
    [HttpGet("deployment-certificates/download")]
    public async Task<IActionResult> DownloadDeploymentCertificates([FromQuery] string? repository = null)
    {
        var certificates = await LoadDeploymentCertificatesAsync();
        
        if (!string.IsNullOrEmpty(repository))
            certificates = certificates.Where(c => c.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToList();
        
        var exportData = new
        {
            ExportedAt = DateTime.UtcNow,
            MachineId = Environment.MachineName,
            TotalCertificates = certificates.Count,
            FilteredByRepository = repository ?? "ALL",
            LegalNotice = "EU Cyber Resilience Act - Deployment Audit Trail",
            Certificates = certificates.OrderByDescending(c => c.Timestamp).ToList()
        };
        
        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileName = $"deployment_certificates_{Environment.MachineName}_{DateTime.Now:yyyy-MM-dd}.json";
        
        return File(bytes, "application/json", fileName);
    }

    #endregion

    private async Task AddDirectoryToZipAsync(System.IO.Compression.ZipArchive archive, string sourceDir, string entryPrefix, string[] excludeFolders, string[] excludeExtensions)
    {
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            
            // Excluir carpetas
            var shouldExclude = excludeFolders.Any(folder => 
                relativePath.StartsWith(folder + Path.DirectorySeparatorChar) || 
                relativePath.Contains(Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar));
            
            if (shouldExclude) continue;

            // Excluir extensiones
            if (excludeExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

            // Limitar tamaño de archivo (max 5MB por archivo)
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length > 5 * 1024 * 1024) continue;

            var entryName = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
            var entry = archive.CreateEntry(entryName);
            
            using var entryStream = entry.Open();
            using var fileStream = System.IO.File.OpenRead(file);
            await fileStream.CopyToAsync(entryStream);
        }
    }
}

public class CommitRequest { public string Message { get; set; } = ""; }
public class DiscardRequest { public string? FilePath { get; set; } }
public class RevertRequest { public string CommitHash { get; set; } = ""; }
