using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Models;
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
    private readonly IAuditLogService _auditLog;
    private readonly ISoftwareIntegrityService _integrityService;
    private readonly ILogger<GitController> _logger;
    private static readonly string BackupLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup_log.json");
    private static readonly string DeploymentLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deployment_certificates.json");

    public GitController(IGitOperationsService gitService, IAuditLogService auditLog, ISoftwareIntegrityService integrityService, ILogger<GitController> logger)
    {
        _gitService = gitService;
        _auditLog = auditLog;
        _integrityService = integrityService;
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

    /// <summary>
    /// Obtener informaci�n del entorno (production/development) y permisos de edici�n
    /// </summary>
    [HttpGet("environment")]
    public ActionResult<ScadaEnvironmentInfo> GetEnvironmentInfo()
    {
        _logger.LogInformation("?? Getting environment info");
        var envInfo = _gitService.GetEnvironmentInfo();
        return Ok(envInfo);
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
        
        // 📋 AUDIT LOG: Git Commit - 🌐 Log a TODOS los proyectos
        var loggedUser = GetLoggedUserName();
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitCommit,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"Commit en {repoName}: {request.Message}",
            null, loggedUser);
        
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
        
        // 📋 AUDIT LOG: Git Push - 🌐 Log a TODOS los proyectos
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitPush,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"Push en {repoName} al remoto",
            null, GetLoggedUserName());
        
        // Generar certificado automático después de push exitoso
        if (result.Success)
        {
            await GenerateDeploymentCertificateAsync(repoName, repoPath, GetLoggedUserName(), "Push to remote");
        }
        
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("force-push/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> ForcePush(string repoName, [FromQuery] string? operatorName = null)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        _logger.LogWarning("?? FORCE PUSH request for {Repo}", repoName);
        var result = await _gitService.ForcePushAsync(repoPath);
        
        // 📋 AUDIT LOG: Force Push (Warning por ser operación peligrosa) - 🌐 Log a TODOS los proyectos
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitPush,
            result.Success ? AuditResult.Warning : AuditResult.Failure,
            $"⚠️ FORCE PUSH en {repoName} - Operación forzada",
            null, GetLoggedUserName());
        
        // Generar certificado automático después de force push exitoso
        if (result.Success)
        {
            await GenerateDeploymentCertificateAsync(repoName, repoPath, GetLoggedUserName(), "Force Push (sync after revert)");
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
        
        // Usar usuario logueado para el certificado y audit log
        var loggedUser = GetLoggedUserName();
        await GenerateDeploymentCertificateAsync(repoName, repoPath, loggedUser, $"Commit+Push: {request.Message}");
        
        // 📋 AUDIT LOG: Commit + Push - 🌐 Log a TODOS los proyectos
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitCommit,
            AuditResult.Success,
            $"Commit+Push en {repoName}: {request.Message}",
            null, loggedUser);
        
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
        
        // 📋 AUDIT LOG: Discard Changes (Warning por pérdida de datos) - 🌐 Log a TODOS los proyectos
        if (result.Success)
        {
            await _auditLog.LogToAllProjectsAsync(
                AuditCategory.Git,
                AuditAction.GitDiscard,
                AuditResult.Warning,
                $"⚠️ Descartados cambios en {repoName}: {request?.FilePath ?? "TODOS los archivos"}",
                null, GetLoggedUserName());
        }
        
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
        
        // 📋 AUDIT LOG: Revert (Warning por ser operación crítica) - 🌐 Log a TODOS los proyectos
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitRevert,
            result.Success ? AuditResult.Warning : AuditResult.Failure,
            $"⚠️ REVERT en {repoName} al commit {request.CommitHash}",
            null, GetLoggedUserName());
        
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("commit-all")]
    public async Task<ActionResult<Dictionary<string, GitOperationResult>>> CommitAll([FromBody] CommitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Commit message is required");
        var repoPaths = GetRepoPaths();
        var results = new Dictionary<string, GitOperationResult>();
        var status = await _gitService.GetAllRepositoriesStatusAsync();
        var committedRepos = new List<string>();
        
        foreach (var (name, repoStatus) in status.Repositories)
        {
            if (repoStatus.IsValid && repoStatus.HasChanges)
            {
                var repoPath = repoPaths.GetValueOrDefault(name) ?? "";
                if (!string.IsNullOrEmpty(repoPath))
                {
                    results[name] = await _gitService.CommitAsync(repoPath, request.Message);
                    if (results[name].Success) committedRepos.Add(name);
                }
            }
            else if (!repoStatus.HasChanges) results[name] = new GitOperationResult { Success = true, Message = "No changes to commit" };
        }
        
        // 📋 AUDIT LOG: Commit All
        if (committedRepos.Count > 0)
        {
            await _auditLog.LogToAllProjectsAsync(
                AuditCategory.Git,
                AuditAction.GitCommit,
                AuditResult.Success,
                $"Commit ALL en [{string.Join(", ", committedRepos)}]: {request.Message}",
                null, GetLoggedUserName());
        }
        
        return Ok(results);
    }

    [HttpPost("push-all")]
    public async Task<ActionResult<Dictionary<string, GitOperationResult>>> PushAll([FromQuery] string? operatorName = null)
    {
        var repoPaths = GetRepoPaths();
        var results = new Dictionary<string, GitOperationResult>();
        var status = await _gitService.GetAllRepositoriesStatusAsync();
        var pushedRepos = new List<string>();
        
        foreach (var (name, repoStatus) in status.Repositories)
        {
            if (repoStatus.IsValid && repoStatus.CommitsAhead > 0)
            {
                var repoPath = repoPaths.GetValueOrDefault(name) ?? "";
                if (!string.IsNullOrEmpty(repoPath))
                {
                    results[name] = await _gitService.PushAsync(repoPath);
                    if (results[name].Success) pushedRepos.Add(name);
                }
            }
            else if (repoStatus.CommitsAhead == 0) results[name] = new GitOperationResult { Success = true, Message = "Nothing to push" };
        }
        
        // 📋 AUDIT LOG: Push All
        if (pushedRepos.Count > 0)
        {
            await _auditLog.LogToAllProjectsAsync(
                AuditCategory.Git,
                AuditAction.GitPush,
                AuditResult.Success,
                $"Push ALL en [{string.Join(", ", pushedRepos)}] al remoto",
                null, GetLoggedUserName());
        }
        
        return Ok(results);
    }

    #region Tag/Release Management (CalVer: YYYY.MM.increment)

    /// <summary>
    /// Obtiene todos los tags de un repositorio
    /// </summary>
    [HttpGet("tags/{repoName}")]
    public async Task<ActionResult<List<TagInfo>>> GetTags(string repoName)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        
        var tags = await _gitService.GetTagsAsync(repoPath);
        return Ok(tags);
    }

    /// <summary>
    /// Obtiene informaci�n de release: �ltimo tag y siguiente tag sugerido (CalVer)
    /// </summary>
    [HttpGet("release-info/{repoName}")]
    public async Task<ActionResult<ReleaseInfo>> GetReleaseInfo(string repoName)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        
        var latestTag = await _gitService.GetLatestTagAsync(repoPath);
        var nextTag = await _gitService.GetNextCalVerTagAsync(repoPath);
        var tags = await _gitService.GetTagsAsync(repoPath);
        
        return Ok(new ReleaseInfo
        {
            LatestTag = latestTag,
            NextSuggestedTag = nextTag,
            TotalTags = tags.Count,
            RecentTags = tags.Take(5).ToList()
        });
    }

    /// <summary>
    /// Crea un nuevo tag/release con formato CalVer (YYYY.MM.increment)
    /// </summary>
    [HttpPost("create-release/{repoName}")]
    public async Task<ActionResult<GitOperationResult>> CreateRelease(string repoName, [FromBody] CreateReleaseRequest request)
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");
        
        // Use suggested tag or custom tag
        var tagName = string.IsNullOrEmpty(request.CustomTag) 
            ? await _gitService.GetNextCalVerTagAsync(repoPath) 
            : request.CustomTag;
        
        var message = string.IsNullOrEmpty(request.Message) 
            ? $"Release {tagName}" 
            : $"Release {tagName}: {request.Message}";
        
        if (!string.IsNullOrEmpty(request.OperatorName))
            message = $"[Autor: {request.OperatorName}] {message}";
        
        _logger.LogInformation("?? Creating release {Tag} for {Repo} by {Operator}", tagName, repoName, request.OperatorName ?? "System");
        
        // Create tag
        var createResult = await _gitService.CreateTagAsync(repoPath, tagName, message);
        if (!createResult.Success)
            return BadRequest(createResult);
        
        // Push tag to remote
        var pushResult = await _gitService.PushTagsAsync(repoPath);
        if (!pushResult.Success)
            return Ok(new GitOperationResult 
            { 
                Success = true, 
                Message = $"Tag '{tagName}' created locally but failed to push: {pushResult.Message}. Push manually later." 
            });
        
        // Log the release
        await LogReleaseAsync(repoName, tagName, GetLoggedUserName(), message);
        
        // 📋 AUDIT LOG: Create Release - 🌐 Log a TODOS los proyectos
        await _auditLog.LogToAllProjectsAsync(
            AuditCategory.Git,
            AuditAction.GitRelease,
            AuditResult.Success,
            $"🌟 Release creado en {repoName}: {tagName}",
            null, GetLoggedUserName());
        
        return Ok(new GitOperationResult 
        { 
            Success = true, 
            Message = $"Release '{tagName}' created and pushed successfully!" 
        });
    }

    private async Task LogReleaseAsync(string repoName, string tagName, string operatorName, string message)
    {
        // Reutilizamos el log de deployment certificates
        var repoPaths = GetRepoPaths();
        var repoPath = repoPaths.GetValueOrDefault(repoName.ToLower()) ?? "";
        if (!string.IsNullOrEmpty(repoPath))
        {
            await GenerateDeploymentCertificateAsync(repoName, repoPath, operatorName, $"Release {tagName}: {message}");
        }
    }

    #endregion

    #region SSH Signing Management

    /// <summary>
    /// Obtiene el estado actual de la configuraci�n SSH Signing
    /// </summary>
    [HttpGet("ssh-signing/status")]
    public async Task<ActionResult<SshSigningStatus>> GetSshSigningStatus()
    {
        _logger.LogInformation("?? Getting SSH signing status");
        var status = await _gitService.GetSshSigningStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Configura SSH signing con la clave especificada
    /// </summary>
    [HttpPost("ssh-signing/configure")]
    public async Task<ActionResult<GitOperationResult>> ConfigureSshSigning([FromBody] ConfigureSshSigningRequest request)
    {
        if (string.IsNullOrEmpty(request.KeyPath))
            return BadRequest(new GitOperationResult { Success = false, Message = "KeyPath is required" });
        
        _logger.LogInformation("?? Configuring SSH signing with key: {KeyPath}", request.KeyPath);
        var result = await _gitService.ConfigureSshSigningAsync(request.KeyPath);
        
        // 📋 AUDIT LOG: SSH Signing Enable
        await _auditLog.LogAsync(
            AuditCategory.Security,
            result.Success ? AuditAction.SshSigningEnable : AuditAction.SshSigningEnable,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH signing configured: KeyPath={request.KeyPath} | Result: {result.Message}",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Genera una nueva clave SSH (Ed25519) si el usuario no tiene una
    /// </summary>
    [HttpPost("ssh-signing/generate-key")]
    public async Task<ActionResult<GenerateSshKeyResult>> GenerateSshKey([FromBody] GenerateSshKeyRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return BadRequest(new GenerateSshKeyResult { Success = false, Message = "Email is required" });
        
        try
        {
            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            var keyPath = Path.Combine(sshDir, "id_ed25519");
            var publicKeyPath = keyPath + ".pub";

            // Check if key already exists
            if (System.IO.File.Exists(publicKeyPath))
            {
                var existingKey = await System.IO.File.ReadAllTextAsync(publicKeyPath);
                return Ok(new GenerateSshKeyResult 
                { 
                    Success = true, 
                    Message = "SSH key already exists",
                    KeyPath = publicKeyPath,
                    PublicKey = existingKey.Trim(),
                    AlreadyExisted = true
                });
            }

            // Create .ssh directory if it doesn't exist
            if (!Directory.Exists(sshDir))
            {
                Directory.CreateDirectory(sshDir);
            }

            // Generate new Ed25519 key using ssh-keygen
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ssh-keygen",
                    Arguments = $"-t ed25519 -C \"{request.Email}\" -f \"{keyPath}\" -N \"\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return BadRequest(new GenerateSshKeyResult 
                { 
                    Success = false, 
                    Message = $"Failed to generate SSH key: {error}" 
                });
            }

            // Read the generated public key
            var publicKey = await System.IO.File.ReadAllTextAsync(publicKeyPath);

            _logger.LogInformation("?? Generated new SSH key for {Email}", request.Email);

            // 📋 AUDIT LOG: SSH Key Generate
            await _auditLog.LogAsync(
                AuditCategory.Security,
                AuditAction.SshKeyGenerate,
                AuditResult.Success,
                $"SSH Ed25519 key generated for {request.Email} | Path: {publicKeyPath}",
                null, "System");

            return Ok(new GenerateSshKeyResult
            {
                Success = true,
                Message = "SSH key generated successfully",
                KeyPath = publicKeyPath,
                PublicKey = publicKey.Trim(),
                AlreadyExisted = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SSH key");
            return BadRequest(new GenerateSshKeyResult 
            { 
                Success = false, 
                Message = $"Exception: {ex.Message}" 
            });
        }
    }

    /// <summary>
    /// Desactiva SSH signing - quita la configuraci�n de firma de commits/tags
    /// </summary>
    [HttpPost("ssh-signing/disable")]
    public async Task<ActionResult<GitOperationResult>> DisableSshSigning()
    {
        _logger.LogInformation("?? Request to disable SSH signing");
        var result = await _gitService.DisableSshSigningAsync();
        
        // 📋 AUDIT LOG: SSH Signing Disable
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshSigningDisable,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH signing disabled: {result.Message}",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Valida que la identidad del usuario Git coincida con la clave SSH (EU CRA anti-spoofing)
    /// </summary>
    [HttpGet("ssh-signing/validate-identity")]
    public async Task<ActionResult<IdentityValidationResult>> ValidateSigningIdentity()
    {
        _logger.LogInformation("?? Validating signing identity");
        var result = await _gitService.ValidateSigningIdentityAsync();
        return Ok(result);
    }

    /// <summary>
    /// Elimina las claves SSH del disco
    /// </summary>
    [HttpDelete("ssh-keys")]
    public async Task<ActionResult<GitOperationResult>> DeleteSshKeys()
    {
        _logger.LogInformation("??? Request to delete SSH keys");
        var result = await _gitService.DeleteSshKeysAsync();
        
        // 📋 AUDIT LOG: SSH Key Delete (CRITICAL)
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshKeyDelete,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH keys deleted from system: {result.Message}",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Exporta la clave SSH actual para guardarla externamente
    /// </summary>
    [HttpGet("ssh-keys/export")]
    public async Task<ActionResult<SshKeyExportResult>> ExportSshKey()
    {
        _logger.LogInformation("?? Request to export SSH key");
        var result = await _gitService.ExportSshKeyAsync();
        
        // 📋 AUDIT LOG: SSH Key Export
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshKeyExport,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH key exported: {(result.Success ? "Public key fingerprint available" : result.Message)}",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Importa una clave SSH (privada + p�blica)
    /// </summary>
    [HttpPost("ssh-keys/import")]
    public async Task<ActionResult<GitOperationResult>> ImportSshKey([FromBody] ImportSshKeyRequest request)
    {
        if (string.IsNullOrEmpty(request.PrivateKey) || string.IsNullOrEmpty(request.PublicKey))
            return BadRequest(new GitOperationResult { Success = false, Message = "Both private and public keys are required" });
        
        _logger.LogInformation("?? Request to import SSH key");
        var result = await _gitService.ImportSshKeyAsync(request.PrivateKey, request.PublicKey);
        
        // 📋 AUDIT LOG: SSH Key Import (CRITICAL)
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshKeyImport,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH key imported: {result.Message}",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Obtiene la lista de claves autorizadas para modificar el software
    /// </summary>
    [HttpGet("authorized-keys")]
    public async Task<ActionResult<List<AuthorizedKey>>> GetAuthorizedKeys()
    {
        var keys = await _gitService.GetAuthorizedKeysAsync();
        return Ok(keys);
    }

    /// <summary>
    /// A�ade una clave a la lista de autorizados
    /// </summary>
    [HttpPost("authorized-keys")]
    public async Task<ActionResult<GitOperationResult>> AddAuthorizedKey([FromBody] AddAuthorizedKeyRequest request)
    {
        if (string.IsNullOrEmpty(request.Fingerprint) || string.IsNullOrEmpty(request.OwnerName))
            return BadRequest(new GitOperationResult { Success = false, Message = "Fingerprint and owner name are required" });
        
        _logger.LogInformation("? Adding authorized key for {Owner}", request.OwnerName);
        var result = await _gitService.AddAuthorizedKeyAsync(request.Fingerprint, request.OwnerName, request.OwnerEmail ?? "");
        
        // 📋 AUDIT LOG: SSH Key Authorize (CRITICAL - who can modify software)
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshKeyAuthorize,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH key authorized: Owner={request.OwnerName}, Email={request.OwnerEmail ?? "N/A"}, Fingerprint={request.Fingerprint?.Substring(0, Math.Min(20, request.Fingerprint?.Length ?? 0))}...",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Elimina una clave de la lista de autorizados
    /// </summary>
    [HttpDelete("authorized-keys/{fingerprint}")]
    public async Task<ActionResult<GitOperationResult>> RemoveAuthorizedKey(string fingerprint)
    {
        _logger.LogInformation("? Removing authorized key: {Fingerprint}", fingerprint);
        // URL decode the fingerprint (SHA256: gets encoded)
        var decodedFingerprint = Uri.UnescapeDataString(fingerprint);
        var result = await _gitService.RemoveAuthorizedKeyAsync(decodedFingerprint);
        
        // 📋 AUDIT LOG: SSH Key Revoke (CRITICAL - revoking software modification rights)
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.SshKeyRevoke,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"SSH key authorization revoked: Fingerprint={decodedFingerprint?.Substring(0, Math.Min(30, decodedFingerprint?.Length ?? 0))}...",
            null, "System");
        
        return Ok(result);
    }

    /// <summary>
    /// Verifica si la clave actual est� autorizada para modificar el software
    /// </summary>
    [HttpGet("check-authorization")]
    public async Task<ActionResult<KeyAuthorizationResult>> CheckKeyAuthorization()
    {
        _logger.LogInformation("?? Checking key authorization");
        var result = await _gitService.CheckKeyAuthorizationAsync();
        return Ok(result);
    }

    /// <summary>
    /// Obtiene la configuraci�n del control de acceso
    /// </summary>
    [HttpGet("access-control")]
    public async Task<ActionResult<AccessControlConfig>> GetAccessControlConfig()
    {
        var config = await _gitService.GetAccessControlConfigAsync();
        return Ok(config);
    }

    /// <summary>
    /// Activa o desactiva el control de acceso por claves
    /// </summary>
    [HttpPost("access-control")]
    public async Task<ActionResult<GitOperationResult>> SetAccessControlEnabled([FromBody] SetAccessControlRequest request)
    {
        _logger.LogInformation("🔐 Setting access control to: {Enabled}", request.Enabled);
        var result = await _gitService.SetAccessControlEnabledAsync(request.Enabled);
        
        // 📋 AUDIT LOG: Access Control Change
        await _auditLog.LogAsync(
            AuditCategory.Security,
            AuditAction.GitAccessControl,
            result.Success ? AuditResult.Success : AuditResult.Failure,
            $"Git Access Control {(request.Enabled ? "ACTIVADO" : "DESACTIVADO")}",
            GetLoggedUserName());
        
        return Ok(result);
    }

    #endregion

    /// <summary>
    /// Genera un ZIP con el certificado de integridad + c�digo fuente del repositorio seleccionado
    /// Para backup offline cuando no hay conexi�n a internet (EU CRA compliance)
    /// </summary>
    [HttpGet("backup/{repoName}")]
    public async Task<IActionResult> DownloadBackupWithCertificate(string repoName, [FromQuery] string machineId = "PLANTA_001", [FromQuery] string operatorName = "System")
    {
        var repoPaths = GetRepoPaths();
        if (!repoPaths.TryGetValue(repoName.ToLower(), out var repoPath) || string.IsNullOrEmpty(repoPath))
            return NotFound($"Repository '{repoName}' not found");

        if (!Directory.Exists(repoPath))
            return NotFound($"Repository path not found: {repoPath}");

        _logger.LogInformation("?? Generating backup ZIP for {Repo} - Machine: {Machine}, Operator: {Operator}", repoName, machineId, operatorName);

        try
        {
            // Generar certificado espec�fico para este repo
            var repoStatus = await _gitService.GetRepositoryStatusAsync(repoPath);
            var certificate = GenerateRepoCertificate(repoName, repoStatus, machineId, operatorName);

            // Crear ZIP en memoria
            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                // 1. A�adir certificado JSON
                var certEntry = archive.CreateEntry($"certificate_{repoName}.json");
                using (var certStream = certEntry.Open())
                using (var writer = new StreamWriter(certStream))
                {
                    var certJson = System.Text.Json.JsonSerializer.Serialize(certificate, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await writer.WriteAsync(certJson);
                }

                // 2. A�adir c�digo fuente (excluyendo basura)
                var excludeFolders = GetExcludeFolders(repoName);
                var excludeExtensions = new[] { ".exe", ".dll", ".pdb", ".cache", ".log", ".db", ".db-shm", ".db-wal", ".zip" };
                
                await AddDirectoryToZipAsync(archive, repoPath, $"source_{repoName}", excludeFolders, excludeExtensions);
            }

            memoryStream.Position = 0;
            var fileName = $"backup_{repoName}_{machineId}_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
            
            // Registrar el backup en el log interno
            await LogBackupAsync(new BackupLogEntry
            {
                Timestamp = DateTime.Now,
                Repository = repoName,
                MachineId = machineId,
                OperatorName = operatorName,
                FileName = fileName,
                LastCommitHash = repoStatus.LastCommit?.Hash ?? "unknown",
                Branch = repoStatus.CurrentBranch ?? "unknown",
                WasSyncedWithRemote = repoStatus.CommitsAhead == 0,
                Reason = repoStatus.CommitsAhead > 0 ? "Offline Backup (commits pendientes)" : "Manual Export"
            });
            
            // 📋 Audit log unificado (EU CRA)
            var commitShort = repoStatus.LastCommit?.Hash is { Length: >= 8 } h ? h[..8] : (repoStatus.LastCommit?.Hash ?? "unknown");
            await _auditLog.LogAsync(
                AuditCategory.Git,
                AuditAction.GitBackupExport,
                AuditResult.Success,
                $"Git backup exported: {repoName.ToUpper()} - Branch: {repoStatus.CurrentBranch}, Commit: {commitShort}, Machine: {machineId}, File: {fileName}",
                GetLoggedUserName());
            
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
            GeneratedAt = DateTime.Now,
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
                VerificationHash = ComputeSimpleHash($"{status.LastCommit?.Hash}|{status.CurrentBranch}|{DateTime.Now:yyyyMMdd}")
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
            "backend" => new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages", "Data", "Projects", "backups", "publish", "wwwroot" },
            "frontend" => new[] { "node_modules", ".git", "build", "dist", ".cache", "coverage" },
            "twincat" => new[] { ".git", "_Boot", "_CompileInfo", "__Pou" },
            _ => new[] { ".git", "bin", "obj", "node_modules", "Data" }
        };
    }

    #region Backup Log Methods
    
    private async Task LogBackupAsync(BackupLogEntry entry)
    {
        try
        {
            var log = await LoadBackupLogAsync();
            log.Add(entry);
            
            // Mantener solo los �ltimos 100 registros
            if (log.Count > 100) log = log.Skip(log.Count - 100).ToList();
            
            var json = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(BackupLogPath, json);
            
            _logger.LogInformation("?? Backup logged: {Repo} by {Operator}", entry.Repository, entry.OperatorName);
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
    /// Obtiene el nombre del usuario logueado (JWT) para audit logs
    /// </summary>
    private string GetLoggedUserName()
    {
        return User.Identity?.Name ?? "System";
    }

    /// <summary>
    /// Genera un certificado de deployment despu�s de cada push exitoso
    /// </summary>
    private async Task GenerateDeploymentCertificateAsync(string repoName, string repoPath, string operatorName, string description)
    {
        try
        {
            var repoStatus = await _gitService.GetRepositoryStatusAsync(repoPath);
            var machineId = Environment.MachineName;
            
            var certificate = new DeploymentCertificate
            {
                CertificateId = $"DEPLOY-{repoName.ToUpper()}-{DateTime.Now:yyyyMMdd-HHmmss}",
                Timestamp = DateTime.Now,
                Repository = repoName,
                MachineId = machineId,
                OperatorName = operatorName,
                CommitHash = repoStatus.LastCommit?.Hash ?? "unknown",
                Branch = repoStatus.CurrentBranch ?? "unknown",
                Action = description.StartsWith("Commit+Push") ? "Commit+Push" : "Push",
                Description = description,
                IntegrityHash = ComputeSimpleHash($"{repoStatus.LastCommit?.Hash}|{repoName}|{DateTime.Now:yyyyMMddHHmmss}")
            };

            var certificates = await LoadDeploymentCertificatesAsync();
            certificates.Add(certificate);
            
            // Mantener �ltimos 200 certificados
            if (certificates.Count > 200) certificates = certificates.Skip(certificates.Count - 200).ToList();
            
            var json = JsonSerializer.Serialize(certificates, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(DeploymentLogPath, json);
            
            _logger.LogInformation("?? Deployment certificate generated: {CertId} for {Repo} by {Operator}", 
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
    /// Obtiene información de deployment: estado actual de componentes + historial de operaciones
    /// </summary>
    [HttpGet("deployment-certificates")]
    public async Task<ActionResult<object>> GetDeploymentCertificates([FromQuery] string? repository = null, [FromQuery] int count = 50)
    {
        // Obtener historial de operaciones (pushes/releases)
        var certificates = await LoadDeploymentCertificatesAsync();
        
        if (!string.IsNullOrEmpty(repository))
            certificates = certificates.Where(c => c.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Obtener estado actual de componentes (de deploy-version.json o git en vivo)
        var versionInfo = _integrityService.GetSoftwareVersionInfo();
        
        var currentState = new[]
        {
            new {
                Repository = "backend",
                Version = versionInfo.Backend?.Version ?? "N/A",
                CommitSha = versionInfo.Backend?.CommitSha ?? "N/A",
                Branch = versionInfo.Backend?.Branch ?? "N/A",
                CommitDate = versionInfo.Backend?.CommitDate ?? "N/A",
                CommitAuthor = versionInfo.Backend?.CommitAuthor ?? "N/A",
                IsSigned = versionInfo.Backend?.IsSigned ?? false,
                SignatureStatus = versionInfo.Backend?.SignatureStatus ?? "N/A",
                Integrity = versionInfo.Backend?.Integrity ?? "unknown"
            },
            new {
                Repository = "frontend",
                Version = versionInfo.Frontend?.Version ?? "N/A",
                CommitSha = versionInfo.Frontend?.CommitSha ?? "N/A",
                Branch = versionInfo.Frontend?.Branch ?? "N/A",
                CommitDate = versionInfo.Frontend?.CommitDate ?? "N/A",
                CommitAuthor = versionInfo.Frontend?.CommitAuthor ?? "N/A",
                IsSigned = versionInfo.Frontend?.IsSigned ?? false,
                SignatureStatus = versionInfo.Frontend?.SignatureStatus ?? "N/A",
                Integrity = versionInfo.Frontend?.Integrity ?? "unknown"
            },
            new {
                Repository = "twincat",
                Version = versionInfo.TwinCatPlc?.Version ?? "N/A",
                CommitSha = versionInfo.TwinCatPlc?.CommitSha ?? "N/A",
                Branch = versionInfo.TwinCatPlc?.Branch ?? "N/A",
                CommitDate = versionInfo.TwinCatPlc?.CommitDate ?? "N/A",
                CommitAuthor = versionInfo.TwinCatPlc?.CommitAuthor ?? "N/A",
                IsSigned = versionInfo.TwinCatPlc?.IsSigned ?? false,
                SignatureStatus = versionInfo.TwinCatPlc?.SignatureStatus ?? "N/A",
                Integrity = versionInfo.TwinCatPlc?.Integrity ?? "unknown"
            }
        };
        
        return Ok(new 
        {
            CurrentState = currentState,
            OperationHistory = certificates.OrderByDescending(c => c.Timestamp).Take(count).ToList(),
            LastVerification = versionInfo.LastVerificationDate,
            SystemStatus = versionInfo.SystemStatus
        });
    }

    /// <summary>
    /// Descarga información de deployment como archivo JSON para auditoría
    /// Incluye estado actual de componentes + historial de operaciones
    /// </summary>
    [HttpGet("deployment-certificates/download")]
    public async Task<IActionResult> DownloadDeploymentCertificates([FromQuery] string? repository = null)
    {
        var certificates = await LoadDeploymentCertificatesAsync();
        
        if (!string.IsNullOrEmpty(repository))
            certificates = certificates.Where(c => c.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Obtener estado actual de componentes
        var versionInfo = _integrityService.GetSoftwareVersionInfo();
        
        var exportData = new
        {
            ExportedAt = DateTime.Now,
            MachineId = Environment.MachineName,
            LegalNotice = "EU Cyber Resilience Act - Deployment Audit Trail",
            
            // Estado actual de cada componente (de deploy-version.json o git en vivo)
            CurrentState = new
            {
                LastVerification = versionInfo.LastVerificationDate,
                SystemStatus = versionInfo.SystemStatus,
                Components = new[]
                {
                    new {
                        Name = "Backend",
                        Version = versionInfo.Backend?.Version ?? "N/A",
                        CommitSha = versionInfo.Backend?.CommitSha ?? "N/A",
                        CommitShaFull = versionInfo.Backend?.CommitShaFull ?? "N/A",
                        Branch = versionInfo.Backend?.Branch ?? "N/A",
                        CommitDate = versionInfo.Backend?.CommitDate ?? "N/A",
                        CommitAuthor = versionInfo.Backend?.CommitAuthor ?? "N/A",
                        IsSigned = versionInfo.Backend?.IsSigned ?? false,
                        SignatureStatus = versionInfo.Backend?.SignatureStatus ?? "N/A",
                        Integrity = versionInfo.Backend?.Integrity ?? "unknown"
                    },
                    new {
                        Name = "Frontend",
                        Version = versionInfo.Frontend?.Version ?? "N/A",
                        CommitSha = versionInfo.Frontend?.CommitSha ?? "N/A",
                        CommitShaFull = versionInfo.Frontend?.CommitShaFull ?? "N/A",
                        Branch = versionInfo.Frontend?.Branch ?? "N/A",
                        CommitDate = versionInfo.Frontend?.CommitDate ?? "N/A",
                        CommitAuthor = versionInfo.Frontend?.CommitAuthor ?? "N/A",
                        IsSigned = versionInfo.Frontend?.IsSigned ?? false,
                        SignatureStatus = versionInfo.Frontend?.SignatureStatus ?? "N/A",
                        Integrity = versionInfo.Frontend?.Integrity ?? "unknown"
                    },
                    new {
                        Name = "TwinCAT PLC",
                        Version = versionInfo.TwinCatPlc?.Version ?? "N/A",
                        CommitSha = versionInfo.TwinCatPlc?.CommitSha ?? "N/A",
                        CommitShaFull = versionInfo.TwinCatPlc?.CommitShaFull ?? "N/A",
                        Branch = versionInfo.TwinCatPlc?.Branch ?? "N/A",
                        CommitDate = versionInfo.TwinCatPlc?.CommitDate ?? "N/A",
                        CommitAuthor = versionInfo.TwinCatPlc?.CommitAuthor ?? "N/A",
                        IsSigned = versionInfo.TwinCatPlc?.IsSigned ?? false,
                        SignatureStatus = versionInfo.TwinCatPlc?.SignatureStatus ?? "N/A",
                        Integrity = versionInfo.TwinCatPlc?.Integrity ?? "unknown"
                    }
                }
            },
            
            // Historial de operaciones (pushes/releases)
            OperationHistory = new
            {
                TotalOperations = certificates.Count,
                FilteredByRepository = repository ?? "ALL",
                Operations = certificates.OrderByDescending(c => c.Timestamp).ToList()
            }
        };
        
        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileName = $"deployment_info_{Environment.MachineName}_{DateTime.Now:yyyy-MM-dd}.json";
        
        // 📋 AUDIT LOG: Deployment Info Export (detallado)
        var backendStatus = $"Backend: v{versionInfo.Backend?.Version ?? "N/A"} [{versionInfo.Backend?.Integrity ?? "?"}]";
        var frontendStatus = $"Frontend: v{versionInfo.Frontend?.Version ?? "N/A"} [{versionInfo.Frontend?.Integrity ?? "?"}]";
        var plcStatus = $"TwinCAT: v{versionInfo.TwinCatPlc?.Version ?? "N/A"} [{versionInfo.TwinCatPlc?.Integrity ?? "?"}]";
        var signedCount = new[] { versionInfo.Backend?.IsSigned, versionInfo.Frontend?.IsSigned, versionInfo.TwinCatPlc?.IsSigned }.Count(s => s == true);
        
        await _auditLog.LogAsync(
            AuditCategory.Certificate,
            AuditAction.CertificateDownload,
            AuditResult.Success,
            $"Deployment info exported: {backendStatus}, {frontendStatus}, {plcStatus} | Signed: {signedCount}/3 | Operations: {certificates.Count} | File: {fileName}",
            null, "System");
        
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

            // Limitar tama�o de archivo (max 5MB por archivo)
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

// Release/Tag models
public class ReleaseInfo 
{ 
    public string LatestTag { get; set; } = ""; 
    public string NextSuggestedTag { get; set; } = ""; 
    public int TotalTags { get; set; }
    public List<TagInfo> RecentTags { get; set; } = new();
}

public class CreateReleaseRequest 
{ 
    public string? CustomTag { get; set; }  // Si est� vac�o, usa el sugerido
    public string? Message { get; set; } 
    public string? OperatorName { get; set; }
}

// SSH Signing models
public class ConfigureSshSigningRequest
{
    public string KeyPath { get; set; } = "";
}

public class GenerateSshKeyRequest
{
    public string Email { get; set; } = "";
}

public class GenerateSshKeyResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? KeyPath { get; set; }
    public string? PublicKey { get; set; }
    public bool AlreadyExisted { get; set; }
}

// SSH Key Management models
public class ImportSshKeyRequest
{
    public string PrivateKey { get; set; } = "";
    public string PublicKey { get; set; } = "";
}

public class AddAuthorizedKeyRequest
{
    public string Fingerprint { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string? OwnerEmail { get; set; }
}

public class SetAccessControlRequest
{
    public bool Enabled { get; set; }
}
