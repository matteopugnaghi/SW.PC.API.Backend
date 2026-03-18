using System.Diagnostics;
using System.Text;

namespace SW.PC.API.Backend.Services;

public interface IGitOperationsService
{
    Task<AllRepositoriesStatus> GetAllRepositoriesStatusAsync();
    Task<RepositoryStatus> GetRepositoryStatusAsync(string repoPath);
    Task<List<CommitInfo>> GetCommitHistoryAsync(string repoPath, int count = 20);
    Task<GitOperationResult> CommitAsync(string repoPath, string message);
    Task<GitOperationResult> PushAsync(string repoPath);
    Task<GitOperationResult> ForcePushAsync(string repoPath);
    Task<GitOperationResult> DiscardChangesAsync(string repoPath, string? filePath = null);
    Task<GitOperationResult> RevertToCommitAsync(string repoPath, string commitHash);
    Task<List<ModifiedFile>> GetModifiedFilesAsync(string repoPath);
    (string Backend, string Frontend, string TwinCAT) GetRepoPaths();
    // Tag/Release methods
    Task<List<TagInfo>> GetTagsAsync(string repoPath);
    Task<string> GetLatestTagAsync(string repoPath);
    Task<string> GetNextCalVerTagAsync(string repoPath);
    Task<GitOperationResult> CreateTagAsync(string repoPath, string tagName, string message);
    Task<GitOperationResult> PushTagsAsync(string repoPath);
    // SSH Signing methods
    Task<SshSigningStatus> GetSshSigningStatusAsync();
    Task<GitOperationResult> ConfigureSshSigningAsync(string keyPath);
    Task<GitOperationResult> DisableSshSigningAsync();
    Task<IdentityValidationResult> ValidateSigningIdentityAsync();
    // Release Notes methods
    Task<List<CommitInfo>> GetCommitsBetweenTagsAsync(string repoPath, string? fromTag, string? toTag);
    Task<ReleaseNotesResult> GenerateReleaseNotesAsync(string repoPath, string? fromTag = null, string? toTag = null);
    Task<List<ReleaseNotesResult>> GenerateFullChangelogAsync(string repoPath, int maxReleases = 20);
    Task<GitOperationResult> WriteChangelogFileAsync(string repoPath, int maxReleases = 20);
    Task<GitOperationResult> WriteProjectChangelogAsync(string projectPath, int maxReleases = 20);
    // SSH Key Management (authorized keys system)
    Task<GitOperationResult> DeleteSshKeysAsync();
    Task<SshKeyExportResult> ExportSshKeyAsync();
    Task<GitOperationResult> ImportSshKeyAsync(string privateKey, string publicKey);
    Task<List<AuthorizedKey>> GetAuthorizedKeysAsync();
    Task<GitOperationResult> AddAuthorizedKeyAsync(string fingerprint, string ownerName, string ownerEmail);
    Task<GitOperationResult> RemoveAuthorizedKeyAsync(string fingerprint);
    Task<KeyAuthorizationResult> CheckKeyAuthorizationAsync();
    // Access Control Configuration
    Task<AccessControlConfig> GetAccessControlConfigAsync();
    Task<GitOperationResult> SetAccessControlEnabledAsync(bool enabled);
    // Environment Info (Production/Development mode)
    ScadaEnvironmentInfo GetEnvironmentInfo();
}

public class GitOperationsService : IGitOperationsService
{
    private readonly ILogger<GitOperationsService> _logger;
    private readonly ISoftwareIntegrityService _integrityService;
    private readonly IExcelConfigService _excelConfigService;

    public GitOperationsService(ILogger<GitOperationsService> logger, ISoftwareIntegrityService integrityService, IExcelConfigService excelConfigService)
    {
        _logger = logger;
        _integrityService = integrityService;
        _excelConfigService = excelConfigService;
        _logger.LogInformation("?? GitOperationsService initialized (using paths from SoftwareIntegrityService)");
    }

    /// <summary>
    /// Obtener las rutas de los repositorios (delegado a SoftwareIntegrityService que lee desde Excel)
    /// </summary>
    public (string Backend, string Frontend, string TwinCAT) GetRepoPaths()
    {
        return _integrityService.GetRepositoryPaths();
    }

    /// <summary>
    /// Detecta el entorno (production/development) basado en configuraci�n Excel (EnvironmentMode)
    /// </summary>
    public ScadaEnvironmentInfo GetEnvironmentInfo()
    {
        // Leer desde Excel SystemConfiguration
        string environmentMode = "development";
        try
        {
            var systemConfig = _excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm").GetAwaiter().GetResult();
            environmentMode = systemConfig?.EnvironmentMode?.ToLower() ?? "development";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "?? Could not read EnvironmentMode from Excel, defaulting to 'development'");
        }
        
        var paths = GetRepoPaths();
        
        // ?? AUTO-DETECTAR PRODUCCI�N: Si no hay .git en Backend/Frontend, es producci�n
        // Esto permite detectar autom�ticamente sin depender del Excel
        var hasBackendGit = Directory.Exists(Path.Combine(paths.Backend ?? "", ".git"));
        var hasFrontendGit = Directory.Exists(Path.Combine(paths.Frontend ?? "", ".git"));
        
        // Si el Excel no especifica producci�n pero no hay repos Git, forzar producci�n
        if (environmentMode != "production" && !hasBackendGit && !hasFrontendGit)
        {
            _logger.LogInformation("?? Auto-detecting PRODUCTION mode: No Git repos found for Backend/Frontend");
            environmentMode = "production";
        }
        
        var isProduction = environmentMode == "production";
        
        // En producci�n: solo TwinCAT es editable
        // En desarrollo: todos son editables (si tienen .git)
        var permissions = new Dictionary<string, bool>
        {
            ["backend"] = !isProduction && hasBackendGit,
            ["frontend"] = !isProduction && hasFrontendGit,
            ["twincat"] = Directory.Exists(Path.Combine(paths.TwinCAT ?? "", ".git")) // TwinCAT siempre editable si tiene .git
        };

        _logger.LogInformation("?? Environment: {Env} | Permissions: Backend={B}, Frontend={F}, TwinCAT={T}", 
            environmentMode, permissions["backend"], permissions["frontend"], permissions["twincat"]);

        return new ScadaEnvironmentInfo
        {
            Environment = environmentMode,
            RepoEditPermissions = permissions
        };
    }

    public async Task<AllRepositoriesStatus> GetAllRepositoriesStatusAsync()
    {
        var paths = GetRepoPaths();
        var envInfo = GetEnvironmentInfo();
        var result = new AllRepositoriesStatus 
        { 
            Timestamp = DateTime.Now, 
            Repositories = new Dictionary<string, RepositoryStatus>(),
            EnvironmentInfo = envInfo
        };
        
        if (!string.IsNullOrEmpty(paths.Backend) && Directory.Exists(paths.Backend))
        {
            var status = await GetRepositoryStatusAsync(paths.Backend);
            status.IsEditable = envInfo.RepoEditPermissions.GetValueOrDefault("backend", false);
            result.Repositories["backend"] = status;
        }
        
        if (!string.IsNullOrEmpty(paths.Frontend) && Directory.Exists(paths.Frontend))
        {
            var status = await GetRepositoryStatusAsync(paths.Frontend);
            status.IsEditable = envInfo.RepoEditPermissions.GetValueOrDefault("frontend", false);
            result.Repositories["frontend"] = status;
        }
        
        if (!string.IsNullOrEmpty(paths.TwinCAT) && Directory.Exists(paths.TwinCAT))
        {
            var status = await GetRepositoryStatusAsync(paths.TwinCAT);
            status.IsEditable = envInfo.RepoEditPermissions.GetValueOrDefault("twincat", false);
            result.Repositories["twincat"] = status;
        }
        
        return result;
    }

    public async Task<RepositoryStatus> GetRepositoryStatusAsync(string repoPath)
    {
        var status = new RepositoryStatus { Path = repoPath, IsValid = false };
        try
        {
            var gitDir = Path.Combine(repoPath, ".git");
            status.IsGitRepo = Directory.Exists(gitDir);
            if (!status.IsGitRepo) { status.Error = "Not a git repository"; return status; }
            status.IsValid = true;
            var branchResult = await RunGitCommandAsync(repoPath, "rev-parse --abbrev-ref HEAD");
            status.CurrentBranch = branchResult.Output?.Trim() ?? "unknown";
            var lastCommitResult = await RunGitCommandAsync(repoPath, "log -1 --format=%H|%s|%ai|%an");
            if (lastCommitResult.Success && !string.IsNullOrEmpty(lastCommitResult.Output))
            {
                var parts = lastCommitResult.Output.Trim().Split('|');
                if (parts.Length >= 4)
                {
                    status.LastCommit = new CommitInfo { Hash = parts[0], ShortHash = parts[0].Length > 7 ? parts[0][..7] : parts[0], Message = parts[1], Date = DateTime.TryParse(parts[2], out var date) ? date : DateTime.MinValue, Author = parts[3] };
                }
            }
            status.ModifiedFiles = await GetModifiedFilesAsync(repoPath);
            status.HasChanges = status.ModifiedFiles.Count > 0;
            var aheadBehindResult = await RunGitCommandAsync(repoPath, "rev-list --left-right --count HEAD...@{upstream}");
            if (aheadBehindResult.Success && !string.IsNullOrEmpty(aheadBehindResult.Output))
            {
                var counts = aheadBehindResult.Output.Trim().Split('\t');
                if (counts.Length >= 2) { status.CommitsAhead = int.TryParse(counts[0], out var ahead) ? ahead : 0; status.CommitsBehind = int.TryParse(counts[1], out var behind) ? behind : 0; }
            }
            var remoteResult = await RunGitCommandAsync(repoPath, "remote get-url origin");
            status.RemoteUrl = remoteResult.Output?.Trim();
        }
        catch (Exception ex) { status.Error = ex.Message; _logger.LogError(ex, "Error getting repository status for {Path}", repoPath); }
        return status;
    }

    public async Task<List<CommitInfo>> GetCommitHistoryAsync(string repoPath, int count = 20)
    {
        var commits = new List<CommitInfo>();
        try
        {
            var result = await RunGitCommandAsync(repoPath, $"log -{count} --format=%H|%s|%ai|%an");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 4) commits.Add(new CommitInfo { Hash = parts[0], ShortHash = parts[0].Length > 7 ? parts[0][..7] : parts[0], Message = parts[1], Date = DateTime.TryParse(parts[2], out var date) ? date : DateTime.MinValue, Author = parts[3] });
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting commit history for {Path}", repoPath); }
        return commits;
    }

    #region Release Notes

    /// <summary>
    /// Gets commits between two tags (or from a tag to HEAD, or all commits if no tags)
    /// </summary>
    public async Task<List<CommitInfo>> GetCommitsBetweenTagsAsync(string repoPath, string? fromTag, string? toTag)
    {
        var commits = new List<CommitInfo>();
        try
        {
            string range;
            if (!string.IsNullOrEmpty(fromTag) && !string.IsNullOrEmpty(toTag))
                range = $"{fromTag}..{toTag}";
            else if (!string.IsNullOrEmpty(fromTag))
                range = $"{fromTag}..HEAD";
            else if (!string.IsNullOrEmpty(toTag))
                range = toTag; // All commits up to this tag
            else
                range = "HEAD";

            var result = await RunGitCommandAsync(repoPath, $"log {range} --format=%H|%s|%ai|%an --reverse");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        commits.Add(new CommitInfo
                        {
                            Hash = parts[0],
                            ShortHash = parts[0].Length > 7 ? parts[0][..7] : parts[0],
                            Message = parts[1],
                            Date = DateTime.TryParse(parts[2], out var date) ? date : DateTime.MinValue,
                            Author = parts[3]
                        });
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting commits between tags for {Path}", repoPath); }
        return commits;
    }

    /// <summary>
    /// Generates release notes for a specific version (between two tags)
    /// If toTag is null, generates for unreleased changes (latest tag..HEAD)
    /// </summary>
    public async Task<ReleaseNotesResult> GenerateReleaseNotesAsync(string repoPath, string? fromTag = null, string? toTag = null)
    {
        try
        {
            // If no fromTag specified, try to find the previous tag
            if (string.IsNullOrEmpty(fromTag) && !string.IsNullOrEmpty(toTag))
            {
                var tags = await GetTagsAsync(repoPath);
                var tagIndex = tags.FindIndex(t => t.Name == toTag);
                if (tagIndex >= 0 && tagIndex + 1 < tags.Count)
                    fromTag = tags[tagIndex + 1].Name; // Tags are already sorted descending
            }
            else if (string.IsNullOrEmpty(fromTag) && string.IsNullOrEmpty(toTag))
            {
                // Unreleased: from latest tag to HEAD
                fromTag = await GetLatestTagAsync(repoPath);
                if (string.IsNullOrEmpty(fromTag)) fromTag = null; // No tags at all
            }

            var commits = await GetCommitsBetweenTagsAsync(repoPath, fromTag, toTag);

            // Get tag info for metadata
            TagInfo? toTagInfo = null;
            if (!string.IsNullOrEmpty(toTag))
            {
                var tags = await GetTagsAsync(repoPath);
                toTagInfo = tags.FirstOrDefault(t => t.Name == toTag);
            }

            var versionName = toTag ?? "Sin publicar";
            var versionDate = toTagInfo?.Date ?? DateTime.Now;

            // Generate markdown
            var sb = new StringBuilder();
            sb.AppendLine($"## {versionName}");
            sb.AppendLine();
            sb.AppendLine($"**Fecha**: {versionDate:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrEmpty(fromTag))
                sb.AppendLine($"**Desde**: {fromTag}");
            sb.AppendLine($"**Commits**: {commits.Count}");
            if (!string.IsNullOrEmpty(toTagInfo?.Message))
                sb.AppendLine($"**Nota**: {toTagInfo.Message}");
            sb.AppendLine();

            if (commits.Count > 0)
            {
                foreach (var commit in commits)
                {
                    sb.AppendLine($"- `{commit.ShortHash}` {commit.Message} — *{commit.Author}*");
                }
            }
            else
            {
                sb.AppendLine("*Sin cambios registrados.*");
            }
            sb.AppendLine();

            return new ReleaseNotesResult
            {
                Version = versionName,
                Date = versionDate,
                FromTag = fromTag ?? "",
                ToTag = toTag ?? "HEAD",
                CommitCount = commits.Count,
                Commits = commits,
                Markdown = sb.ToString(),
                TagMessage = toTagInfo?.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating release notes");
            return new ReleaseNotesResult
            {
                Version = toTag ?? "Error",
                Markdown = $"Error generating release notes: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generates full changelog with all releases (tag pairs)
    /// </summary>
    public async Task<List<ReleaseNotesResult>> GenerateFullChangelogAsync(string repoPath, int maxReleases = 20)
    {
        var changelog = new List<ReleaseNotesResult>();
        try
        {
            var tags = await GetTagsAsync(repoPath); // Already sorted descending

            // First: unreleased changes (latest tag → HEAD)
            if (tags.Count > 0)
            {
                var unreleased = await GenerateReleaseNotesAsync(repoPath, tags[0].Name, null);
                if (unreleased.CommitCount > 0)
                    changelog.Add(unreleased);
            }

            // Then: each tag pair
            for (int i = 0; i < Math.Min(tags.Count, maxReleases); i++)
            {
                var toTag = tags[i].Name;
                var fromTag = (i + 1 < tags.Count) ? tags[i + 1].Name : null;
                var notes = await GenerateReleaseNotesAsync(repoPath, fromTag, toTag);
                changelog.Add(notes);
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error generating full changelog"); }
        return changelog;
    }

    /// <summary>
    /// Writes a CHANGELOG.md file in the repo root
    /// </summary>
    public async Task<GitOperationResult> WriteChangelogFileAsync(string repoPath, int maxReleases = 20)
    {
        try
        {
            var changelog = await GenerateFullChangelogAsync(repoPath, maxReleases);
            if (changelog.Count == 0)
                return new GitOperationResult { Success = false, Message = "No releases found to generate changelog" };

            var sb = new StringBuilder();
            sb.AppendLine("# CHANGELOG");
            sb.AppendLine();
            sb.AppendLine($"> Auto-generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var release in changelog)
            {
                sb.Append(release.Markdown);
                sb.AppendLine("---");
                sb.AppendLine();
            }

            var changelogPath = Path.Combine(repoPath, "CHANGELOG.md");
            await File.WriteAllTextAsync(changelogPath, sb.ToString(), Encoding.UTF8);

            _logger.LogInformation("📋 CHANGELOG.md written to {Path} ({Count} releases)", changelogPath, changelog.Count);
            return new GitOperationResult
            {
                Success = true,
                Message = $"CHANGELOG.md generated with {changelog.Count} releases ({changelog.Sum(c => c.CommitCount)} commits total)",
                Output = changelogPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing CHANGELOG.md");
            return new GitOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Generates a SINGLE combined CHANGELOG.md in the project folder (Projects/{projectId}/)
    /// merging Backend, Frontend and TwinCAT release notes into one document.
    /// </summary>
    public async Task<GitOperationResult> WriteProjectChangelogAsync(string projectPath, int maxReleases = 20)
    {
        try
        {
            var paths = GetRepoPaths();
            var repos = new Dictionary<string, string>
            {
                ["Backend"] = paths.Backend,
                ["Frontend"] = paths.Frontend,
                ["TwinCAT"] = paths.TwinCAT
            };

            var sb = new StringBuilder();
            sb.AppendLine("# CHANGELOG — Proyecto Unificado");
            sb.AppendLine();
            sb.AppendLine($"> Auto-generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"> Backend + Frontend + TwinCAT");
            sb.AppendLine();

            int totalReleases = 0;
            int totalCommits = 0;

            foreach (var (componentName, repoPath) in repos)
            {
                if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    sb.AppendLine($"# {componentName}");
                    sb.AppendLine();
                    sb.AppendLine("*Repositorio no disponible.*");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    continue;
                }

                var changelog = await GenerateFullChangelogAsync(repoPath, maxReleases);
                totalReleases += changelog.Count;
                totalCommits += changelog.Sum(c => c.CommitCount);

                sb.AppendLine($"# {componentName}");
                sb.AppendLine();

                if (changelog.Count == 0)
                {
                    sb.AppendLine("*Sin releases registradas.*");
                    sb.AppendLine();
                }
                else
                {
                    foreach (var release in changelog)
                    {
                        sb.Append(release.Markdown);
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }
                }
            }

            // Ensure project path exists
            Directory.CreateDirectory(projectPath);
            var changelogPath = Path.Combine(projectPath, "CHANGELOG.md");
            await File.WriteAllTextAsync(changelogPath, sb.ToString(), Encoding.UTF8);

            _logger.LogInformation("📋 Combined CHANGELOG.md written to {Path} ({Releases} releases, {Commits} commits)",
                changelogPath, totalReleases, totalCommits);

            return new GitOperationResult
            {
                Success = true,
                Message = $"CHANGELOG.md unificado generado con {totalReleases} releases ({totalCommits} commits) de Backend + Frontend + TwinCAT",
                Output = changelogPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing combined CHANGELOG.md to project folder");
            return new GitOperationResult { Success = false, Message = ex.Message };
        }
    }

    #endregion

    /// <summary>
    /// Verifica si el repositorio es editable seg�n el entorno (production/development)
    /// </summary>
    private GitOperationResult? CheckEditPermission(string repoPath)
    {
        var paths = GetRepoPaths();
        var envInfo = GetEnvironmentInfo();
        
        string repoName = "";
        if (repoPath == paths.Backend) repoName = "backend";
        else if (repoPath == paths.Frontend) repoName = "frontend";
        else if (repoPath == paths.TwinCAT) repoName = "twincat";
        
        if (!string.IsNullOrEmpty(repoName) && !envInfo.RepoEditPermissions.GetValueOrDefault(repoName, false))
        {
            _logger.LogWarning("?? Edit blocked: {Repo} not editable in {Env} environment", repoName, envInfo.Environment);
            return new GitOperationResult
            {
                Success = false,
                Message = $"?? OPERACI�N BLOQUEADA: El repositorio '{repoName}' no es editable en modo {envInfo.Environment.ToUpper()}.\n" +
                          (envInfo.IsProduction ? "En producci�n solo se puede editar TwinCAT." : "Verifica que existe la carpeta .git")
            };
        }
        
        return null; // null = permitido
    }

    public async Task<GitOperationResult> CommitAsync(string repoPath, string message)
    {
        try
        {
            // ?? Verificar si el repo es editable en este entorno
            var editCheck = CheckEditPermission(repoPath);
            if (editCheck != null) return editCheck;

            // ?? EU CRA: Verificar autorizaci�n de clave antes de permitir commit
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("?? Commit rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"?? COMMIT RECHAZADO: Tu clave SSH no est� en la lista de autorizadas.\n" +
                              $"Fingerprint: {authResult.CurrentFingerprint}\n" +
                              $"Contacta al administrador para autorizar tu clave."
                };
            }

            _logger.LogInformation("Creating commit in {Path}: {Message}", repoPath, message);
            
            // 🔧 Auto-reparación: verificar y corregir estado del repo antes de operar
            var repairs = await EnsureRepoHealthAsync(repoPath);
            
            // 60s timeout para add (puede haber muchos archivos)
            var addResult = await RunGitCommandAsync(repoPath, "add -A", 60000);
            if (!addResult.Success) return new GitOperationResult { Success = false, Message = $"Failed to stage changes: {addResult.Error}" };
            var escapedMessage = message.Replace("\"", "\\\"");
            
            // 🔐 Detectar si la firma SSH está configurada → usar -S
            var signingEnabled = await IsCommitSigningEnabledAsync();
            var commitCmd = signingEnabled 
                ? $"commit -S -m \"{escapedMessage}\"" 
                : $"commit -m \"{escapedMessage}\"";
            _logger.LogInformation("🔐 Commit signing: {Enabled}, command: git {Cmd}", signingEnabled, signingEnabled ? "commit -S ..." : "commit ...");
            
            // 60s timeout para commit
            var commitResult = await RunGitCommandAsync(repoPath, commitCmd, 60000);
            
            // Si falla con firma, reintentar sin firma para no bloquear
            if (!commitResult.Success && signingEnabled && 
                (commitResult.Error?.Contains("signing") == true || commitResult.Error?.Contains("gpg") == true || commitResult.Error?.Contains("ssh") == true))
            {
                _logger.LogWarning("⚠️ Signed commit failed, retrying without signature: {Error}", commitResult.Error);
                commitResult = await RunGitCommandAsync(repoPath, $"commit --no-gpg-sign -m \"{escapedMessage}\"", 60000);
                if (commitResult.Success)
                {
                    repairs.Add("Commit signed failed → committed unsigned");
                }
            }
            
            if (commitResult.Success) 
            {
                // 🔄 Actualizar información de firma en el servicio de integridad
                _ = Task.Run(async () => {
                    try { await _integrityService.VerifyAllIntegrityAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh integrity info after commit"); }
                });
                var msg = "Commit created successfully";
                if (repairs.Count > 0) msg += $"\n🔧 Auto-repairs applied: {string.Join("; ", repairs)}";
                return new GitOperationResult { Success = true, Message = msg, Output = commitResult.Output };
            }
            if (commitResult.Output?.Contains("nothing to commit") == true) return new GitOperationResult { Success = true, Message = "Nothing to commit - working tree clean" };
            return new GitOperationResult { Success = false, Message = $"Commit failed: {commitResult.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating commit in {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> PushAsync(string repoPath)
    {
        try
        {
            // ?? Verificar si el repo es editable en este entorno
            var editCheck = CheckEditPermission(repoPath);
            if (editCheck != null) return editCheck;

            // ?? EU CRA: Verificar autorizaci�n antes de push
            var authResult = await CheckKeyAuthorizationAsync();
            _logger.LogWarning("?? DEBUG Push - AccessControlEnabled: {Enabled}, IsAuthorized: {Auth}, Message: {Msg}", 
                authResult.AccessControlEnabled, authResult.IsAuthorized, authResult.Message);
            
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("?? Push rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"?? PUSH RECHAZADO: Tu clave SSH no est� autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Pushing changes from {Path}", repoPath);
            await RunGitCommandAsync(repoPath, "config http.postBuffer 524288000");
            // Use authenticated URL from shared credentials file
            var authUrl = await GetAuthenticatedRemoteUrlAsync(repoPath);
            var pushArgs = authUrl != null ? $"push {authUrl}" : "push";
            var result = await RunGitCommandAsync(repoPath, pushArgs, 120000);
            if (result.Success) 
            {
                // ?? Actualizar informaci�n de integridad despu�s de push
                _ = Task.Run(async () => {
                    try { await _integrityService.VerifyAllIntegrityAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh integrity info after push"); }
                });
                return new GitOperationResult { Success = true, Message = "Push completed successfully", Output = SanitizeGitArgs(result.Output ?? "") };
            }
            return new GitOperationResult { Success = false, Message = $"Push failed: {SanitizeGitArgs(result.Error ?? "")}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error pushing from {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> ForcePushAsync(string repoPath)
    {
        try
        {
            // ?? MODO PRODUCCI�N: Verificar permisos de edici�n
            var editPermission = CheckEditPermission(repoPath);
            if (editPermission != null) return editPermission;

            // ?? EU CRA: Verificar autorizaci�n antes de force push
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("?? Force Push rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"?? FORCE PUSH RECHAZADO: Tu clave SSH no est� autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogWarning("?? FORCE PUSHING changes from {Path} - This will overwrite remote!", repoPath);
            await RunGitCommandAsync(repoPath, "config http.postBuffer 524288000");
            // Use authenticated URL from shared credentials file
            var authUrl = await GetAuthenticatedRemoteUrlAsync(repoPath);
            var pushArgs = authUrl != null ? $"push --force {authUrl}" : "push --force";
            var result = await RunGitCommandAsync(repoPath, pushArgs, 120000);
            if (result.Success) 
            {
                // ?? Actualizar informaci�n de integridad despu�s de force push
                _ = Task.Run(async () => {
                    try { await _integrityService.VerifyAllIntegrityAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh integrity info after force push"); }
                });
                return new GitOperationResult { Success = true, Message = "? Force Push completado - Remoto sincronizado con local", Output = SanitizeGitArgs(result.Output ?? "") };
            }
            return new GitOperationResult { Success = false, Message = $"Force Push failed: {SanitizeGitArgs(result.Error ?? "")}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error force pushing from {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> DiscardChangesAsync(string repoPath, string? filePath = null)
    {
        try
        {
            // ?? MODO PRODUCCI�N: Verificar permisos de edici�n
            var editPermission = CheckEditPermission(repoPath);
            if (editPermission != null) return editPermission;

            string command;
            if (string.IsNullOrEmpty(filePath)) { await RunGitCommandAsync(repoPath, "checkout -- ."); command = "clean -fd"; }
            else { command = $"checkout -- \"{filePath}\""; }
            var result = await RunGitCommandAsync(repoPath, command);
            return new GitOperationResult { Success = result.Success, Message = result.Success ? "Changes discarded successfully" : $"Failed: {result.Error}", Output = result.Output };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error discarding changes in {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> RevertToCommitAsync(string repoPath, string commitHash)
    {
        try
        {
            // ?? MODO PRODUCCI�N: Verificar permisos de edici�n
            var editPermission = CheckEditPermission(repoPath);
            if (editPermission != null) return editPermission;

            _logger.LogWarning("REVERTING to commit {Hash} in {Path}", commitHash, repoPath);
            var result = await RunGitCommandAsync(repoPath, $"reset --hard {commitHash}");
            if (result.Success) return new GitOperationResult { Success = true, Message = $"Successfully reverted to commit {commitHash[..7]}", Output = result.Output };
            return new GitOperationResult { Success = false, Message = $"Revert failed: {result.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error reverting to commit {Hash} in {Path}", commitHash, repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    // Machine-specific TwinCAT extensions that should NEVER appear as changes
    // .xti = AMS NetId target config (different per machine)
    // .~u/.~u1 = TwinCAT temp user files
    // .sln/.plcproj = modified with machine AMS NetId when TwinCAT opens them
    private static readonly HashSet<string> _machineSpecificExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".xti", ".~u", ".~u1", ".sln", ".plcproj" };

    public async Task<List<ModifiedFile>> GetModifiedFilesAsync(string repoPath)
    {
        var files = new List<ModifiedFile>();
        try
        {
            var result = await RunGitCommandAsync(repoPath, "status --porcelain");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length > 3)
                    {
                        var status = line[..2].Trim();
                        var fileName = line[3..].TrimEnd('\r');
                        
                        // Filter out machine-specific files (never travel between machines)
                        var ext = Path.GetExtension(fileName);
                        if (_machineSpecificExtensions.Contains(ext))
                        {
                            _logger.LogDebug("Filtering machine-specific file from git status: {File}", fileName);
                            continue;
                        }
                        
                        files.Add(new ModifiedFile { Path = fileName, Status = ParseGitStatus(status), StatusCode = status });
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting modified files for {Path}", repoPath); }
        return files;
    }

    private async Task<(bool Success, string? Output, string? Error)> RunGitCommandAsync(string workingDirectory, string arguments, int timeoutMs = 30000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = "git", Arguments = $"-c safe.directory=* {arguments}", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 }
            };
            // Prevent git from hanging waiting for interactive input (credentials, passphrase, etc.)
            process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            process.StartInfo.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));
            if (!completed) { process.Kill(); return (false, null, $"Command timed out after {timeoutMs/1000}s"); }
            return (process.ExitCode == 0, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (Exception ex) { _logger.LogError(ex, "Error running git command: {Args}", SanitizeGitArgs(arguments)); return (false, null, ex.Message); }
    }

    /// <summary>Removes credentials from git arguments for safe logging</summary>
    private static string SanitizeGitArgs(string args) =>
        System.Text.RegularExpressions.Regex.Replace(args, @"https://[^@]+@", "https://***@");

    // ═══════════════════════════════════════════════════════════════════════════
    // Credenciales compartidas — usa un archivo accesible por todos los usuarios
    // (Administrator, SYSTEM, etc.) para que el push funcione desde el servicio.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads shared credentials from C:\ProgramData\Aquafrisch\git-credentials
    /// and returns an authenticated remote URL for push operations.
    /// Returns null if no credentials found (falls back to default push).
    /// </summary>
    private async Task<string?> GetAuthenticatedRemoteUrlAsync(string repoPath)
    {
        try
        {
            var credFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Aquafrisch", "git-credentials");

            if (!File.Exists(credFile) || new FileInfo(credFile).Length == 0)
            {
                _logger.LogWarning("⚠️ No shared credentials file at {File}. Create it with: echo https://user:TOKEN@host > \"{File}\"", credFile, credFile);
                return null;
            }

            // Get current remote URL
            var remoteResult = await RunGitCommandAsync(repoPath, "remote get-url origin");
            if (!remoteResult.Success || string.IsNullOrWhiteSpace(remoteResult.Output))
                return null;

            var remoteUrl = remoteResult.Output.Trim();
            if (!remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return null; // SSH remotes don't need this

            // Parse remote URL
            if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var remoteUri))
                return null;

            // Read credentials and find matching host
            var lines = await File.ReadAllLinesAsync(credFile);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (Uri.TryCreate(trimmed, UriKind.Absolute, out var credUri) &&
                    credUri.Host.Equals(remoteUri.Host, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(credUri.UserInfo))
                {
                    // Build authenticated URL: replace userinfo in remote URL with credentials
                    var builder = new UriBuilder(remoteUri);
                    var parts = credUri.UserInfo.Split(':', 2);
                    builder.UserName = parts[0];
                    builder.Password = parts.Length > 1 ? parts[1] : "";
                    var authUrl = builder.Uri.AbsoluteUri;
                    _logger.LogInformation("✅ Using shared credentials for {Host}", remoteUri.Host);
                    return authUrl;
                }
            }

            _logger.LogWarning("⚠️ No credentials found for host {Host} in {File}", remoteUri.Host, credFile);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read shared credentials");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Auto-reparación Git — ejecutar antes de commit para corregir problemas
    // comunes que un técnico no debería tener que arreglar manualmente.
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Verifica y repara automáticamente el estado del repositorio Git:
    /// 1. Configura identidad si falta (user.email/user.name)
    /// 2. Repara reflog corrupto
    /// 3. Regenera objetos faltantes (blobs de archivos actuales)
    /// 4. Reconstruye index corrupto
    /// Devuelve lista de reparaciones aplicadas (vacía si todo OK).
    /// </summary>
    private async Task<List<string>> EnsureRepoHealthAsync(string repoPath)
    {
        var repairs = new List<string>();
        
        try
        {
            // 1. Identidad: configurar user.email y user.name si falta (evita "Author identity unknown")
            var emailCheck = await RunGitCommandAsync(repoPath, "config user.email");
            if (!emailCheck.Success || string.IsNullOrWhiteSpace(emailCheck.Output))
            {
                await RunGitCommandAsync(repoPath, "config user.email \"electronico@aquafrisch.com\"");
                await RunGitCommandAsync(repoPath, "config user.name \"Aquafrisch Supervisor\"");
                repairs.Add("Configured git identity (user.email + user.name)");
                _logger.LogInformation("🔧 Auto-configured git identity in {Path}", repoPath);
            }
            else if (emailCheck.Output?.Trim().Contains("aquafrsich", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Fix typo en email anterior (aquafrsich → aquafrisch)
                await RunGitCommandAsync(repoPath, "config user.email \"electronico@aquafrisch.com\"");
                repairs.Add("Fixed email typo (aquafrsich → aquafrisch)");
                _logger.LogInformation("🔧 Fixed email typo in {Path}", repoPath);
            }

            // 2. Verificar integridad rápida (git fsck sin --full para velocidad)
            var fsckResult = await RunGitCommandAsync(repoPath, "fsck --no-full --no-dangling", 30000);
            var fsckOutput = $"{fsckResult.Output} {fsckResult.Error}";
            
            bool hasCorruptIndex = fsckOutput.Contains("invalid sha1 pointer in resolve-undo", StringComparison.OrdinalIgnoreCase);
            bool hasMissingBlob = fsckOutput.Contains("missing blob", StringComparison.OrdinalIgnoreCase);
            bool hasInvalidReflog = fsckOutput.Contains("invalid reflog entry", StringComparison.OrdinalIgnoreCase);

            // 3. Reflog corrupto → limpiar
            if (hasInvalidReflog)
            {
                await RunGitCommandAsync(repoPath, "reflog expire --expire=now --all");
                repairs.Add("Cleaned corrupted reflog entries");
                _logger.LogWarning("🔧 Auto-cleaned corrupted reflog in {Path}", repoPath);
            }

            // 4. Blob faltante → regenerar objetos de los archivos que existen en disco
            if (hasMissingBlob)
            {
                var lsFiles = await RunGitCommandAsync(repoPath, "ls-files");
                if (lsFiles.Success && !string.IsNullOrWhiteSpace(lsFiles.Output))
                {
                    foreach (var file in lsFiles.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var filePath = Path.Combine(repoPath, file.Trim());
                        if (File.Exists(filePath))
                        {
                            await RunGitCommandAsync(repoPath, $"hash-object -w \"{file.Trim()}\"");
                        }
                    }
                }
                repairs.Add("Regenerated missing blob objects from working tree");
                _logger.LogWarning("🔧 Auto-regenerated missing blobs in {Path}", repoPath);
            }

            // 5. Index corrupto → reconstruir
            if (hasCorruptIndex)
            {
                var indexPath = Path.Combine(repoPath, ".git", "index");
                if (File.Exists(indexPath))
                {
                    File.Delete(indexPath);
                }
                await RunGitCommandAsync(repoPath, "reset");
                repairs.Add("Rebuilt corrupted git index");
                _logger.LogWarning("🔧 Auto-rebuilt corrupted index in {Path}", repoPath);
            }

            if (repairs.Count > 0)
            {
                _logger.LogInformation("🔧 Auto-repair completed for {Path}: {Repairs}", repoPath, string.Join("; ", repairs));
            }

            // 6. SSH Signing: asegurar que allowedSignersFile existe si la firma está configurada
            try
            {
                var gpgFormat = await RunGitCommandAsync(repoPath, "config --global gpg.format");
                var signingKey = await RunGitCommandAsync(repoPath, "config --global user.signingkey");
                var allowedSigners = await RunGitCommandAsync(repoPath, "config --global gpg.ssh.allowedSignersFile");
                
                if (gpgFormat.Success && gpgFormat.Output?.Trim() == "ssh" && 
                    signingKey.Success && !string.IsNullOrWhiteSpace(signingKey.Output))
                {
                    var keyPath = signingKey.Output!.Trim();
                    var allowedPath = allowedSigners.Output?.Trim() ?? "";
                    var sshDir = Path.GetDirectoryName(keyPath);
                    
                    if (!string.IsNullOrEmpty(sshDir))
                    {
                        var newAllowedPath = string.IsNullOrEmpty(allowedPath) || !File.Exists(allowedPath)
                            ? Path.Combine(sshDir, "allowed_signers")
                            : allowedPath;
                        
                        // Construir allowed_signers con TODAS las claves conocidas (local + autorizadas)
                        var email = (await RunGitCommandAsync(repoPath, "config --global user.email")).Output?.Trim() ?? "electronico@aquafrisch.com";
                        var signerLines = new HashSet<string>();
                        
                        // 1. Clave local del servidor
                        if (File.Exists(keyPath))
                        {
                            var localPubKey = (await File.ReadAllTextAsync(keyPath)).Trim();
                            signerLines.Add($"{email} namespaces=\"git\" {localPubKey}");
                        }
                        
                        // 2. Todas las claves autorizadas que tienen PublicKey guardada
                        try
                        {
                            var authorizedKeys = await GetAuthorizedKeysAsync();
                            foreach (var ak in authorizedKeys.Where(k => !string.IsNullOrEmpty(k.PublicKey)))
                            {
                                var akEmail = !string.IsNullOrEmpty(ak.OwnerEmail) ? ak.OwnerEmail : email;
                                signerLines.Add($"{akEmail} namespaces=\"git\" {ak.PublicKey}");
                            }
                        }
                        catch { /* Si falla leer las autorizadas, al menos tenemos la local */ }
                        
                        if (signerLines.Count > 0)
                        {
                            var newContent = string.Join("\n", signerLines) + "\n";
                            var existingContent = File.Exists(newAllowedPath) ? await File.ReadAllTextAsync(newAllowedPath) : "";
                            
                            // Solo reescribir si cambió (evitar I/O innecesario)
                            if (existingContent.Trim() != newContent.Trim())
                            {
                                await File.WriteAllTextAsync(newAllowedPath, newContent);
                                repairs.Add($"Updated SSH allowedSignersFile with {signerLines.Count} key(s)");
                                _logger.LogInformation("🔧 Updated allowedSignersFile with {Count} keys: {Path}", signerLines.Count, newAllowedPath);
                            }
                            
                            // Asegurar que git config apunta al archivo
                            if (string.IsNullOrEmpty(allowedPath) || !File.Exists(allowedPath))
                            {
                                await RunGitCommandAsync(repoPath, $"config --global gpg.ssh.allowedSignersFile \"{newAllowedPath}\"");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔧 Could not auto-configure allowedSignersFile");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🔧 Auto-repair encountered an error in {Path} — continuing with commit", repoPath);
            repairs.Add($"Auto-repair warning: {ex.Message}");
        }

        return repairs;
    }

    private static string ParseGitStatus(string code) => code switch { "M" => "Modified", "A" => "Added", "D" => "Deleted", "R" => "Renamed", "C" => "Copied", "U" => "Unmerged", "?" => "Untracked", "!" => "Ignored", _ => code };

    #region Tag/Release Methods (CalVer: YYYY.MM.increment)

    public async Task<List<TagInfo>> GetTagsAsync(string repoPath)
    {
        var tags = new List<TagInfo>();
        try
        {
            // Get tags with date and message
            var result = await RunGitCommandAsync(repoPath, "tag -l --format=%(refname:short)|%(creatordate:iso)|%(subject)");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 1)
                    {
                        tags.Add(new TagInfo
                        {
                            Name = parts[0],
                            Date = parts.Length > 1 && DateTime.TryParse(parts[1], out var date) ? date : DateTime.MinValue,
                            Message = parts.Length > 2 ? parts[2] : ""
                        });
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting tags for {Path}", repoPath); }
        return tags.OrderByDescending(t => t.Name).ToList();
    }

    public async Task<string> GetLatestTagAsync(string repoPath)
    {
        try
        {
            var result = await RunGitCommandAsync(repoPath, "describe --tags --abbrev=0");
            if (result.Success && !string.IsNullOrEmpty(result.Output))
                return result.Output.Trim();
        }
        catch { }
        return "";
    }

    /// <summary>
    /// Generates next CalVer tag: YYYY.MM.increment
    /// If current month has no tags, starts at .01
    /// If current month has tags, increments the last number
    /// </summary>
    public async Task<string> GetNextCalVerTagAsync(string repoPath)
    {
        var now = DateTime.Now;
        var yearMonth = $"{now.Year}.{now.Month:D2}";
        
        try
        {
            var tags = await GetTagsAsync(repoPath);
            var currentMonthTags = tags
                .Where(t => t.Name.StartsWith(yearMonth))
                .Select(t => t.Name)
                .OrderByDescending(t => t)
                .ToList();

            if (currentMonthTags.Count == 0)
            {
                return $"{yearMonth}.01";
            }

            // Get the highest increment for current month
            var latestTag = currentMonthTags.First();
            var parts = latestTag.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var increment))
            {
                return $"{yearMonth}.{(increment + 1):D2}";
            }

            return $"{yearMonth}.01";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating next CalVer tag");
            return $"{yearMonth}.01";
        }
    }

    public async Task<GitOperationResult> CreateTagAsync(string repoPath, string tagName, string message)
    {
        try
        {
            // ?? MODO PRODUCCI�N: Verificar permisos de edici�n
            var editPermission = CheckEditPermission(repoPath);
            if (editPermission != null) return editPermission;

            // ?? EU CRA: Verificar autorizaci�n antes de crear tag/release
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("?? Tag creation rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"?? RELEASE RECHAZADO: Tu clave SSH no est� autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Creating tag {Tag} in {Path}: {Message}", tagName, repoPath, message);
            var escapedMessage = message.Replace("\"", "\\\"");
            var result = await RunGitCommandAsync(repoPath, $"tag -a {tagName} -m \"{escapedMessage}\"");
            
            if (result.Success)
            {
                // ?? Actualizar informaci�n de integridad (incluye latest release)
                _ = Task.Run(async () => {
                    try { await _integrityService.VerifyAllIntegrityAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh integrity info after tag creation"); }
                });
                return new GitOperationResult { Success = true, Message = $"Tag '{tagName}' created successfully", Output = result.Output };
            }
            
            return new GitOperationResult { Success = false, Message = $"Failed to create tag: {result.Error}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag {Tag} in {Path}", tagName, repoPath);
            return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" };
        }
    }

    public async Task<GitOperationResult> PushTagsAsync(string repoPath)
    {
        try
        {
            // ?? MODO PRODUCCI�N: Verificar permisos de edici�n
            var editPermission = CheckEditPermission(repoPath);
            if (editPermission != null) return editPermission;

            // ?? EU CRA: Verificar autorizaci�n antes de push tags
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("?? Push tags rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"?? PUSH TAGS RECHAZADO: Tu clave SSH no est� autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Pushing tags from {Path}", repoPath);
            var result = await RunGitCommandAsync(repoPath, "push --tags");
            
            if (result.Success)
            {
                // ?? Actualizar informaci�n de integridad despu�s de push tags
                _ = Task.Run(async () => {
                    try { await _integrityService.VerifyAllIntegrityAsync(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh integrity info after push tags"); }
                });
                return new GitOperationResult { Success = true, Message = "Tags pushed successfully", Output = result.Output };
            }
            
            return new GitOperationResult { Success = false, Message = $"Failed to push tags: {result.Error}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing tags from {Path}", repoPath);
            return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" };
        }
    }

    #endregion

    #region SSH Signing Methods

    /// <summary>
    /// Checks if commit signing is enabled in git config (global or local)
    /// </summary>
    private async Task<bool> IsCommitSigningEnabledAsync()
    {
        try
        {
            var result = await RunGitCommandAsync(".", "config --global commit.gpgsign");
            return result.Success && result.Output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current SSH signing configuration status
    /// </summary>
    public async Task<SshSigningStatus> GetSshSigningStatusAsync()
    {
        var status = new SshSigningStatus();

        try
        {
            // Check if Git is configured to use SSH for signing
            var gpgFormatResult = await RunGitCommandAsync(".", "config --global gpg.format");
            status.GpgFormat = gpgFormatResult.Output?.Trim() ?? "";
            status.IsConfiguredForSsh = status.GpgFormat.Equals("ssh", StringComparison.OrdinalIgnoreCase);

            // Get the signing key path
            var signingKeyResult = await RunGitCommandAsync(".", "config --global user.signingkey");
            status.SigningKeyPath = signingKeyResult.Output?.Trim() ?? "";

            // Check if commit signing is enabled
            var commitSignResult = await RunGitCommandAsync(".", "config --global commit.gpgsign");
            status.CommitSigningEnabled = commitSignResult.Output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            // Check if tag signing is enabled
            var tagSignResult = await RunGitCommandAsync(".", "config --global tag.gpgsign");
            status.TagSigningEnabled = tagSignResult.Output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            // Get user email configured in git
            var emailResult = await RunGitCommandAsync(".", "config --global user.email");
            status.GitUserEmail = emailResult.Output?.Trim() ?? "";

            // Get user name configured in git
            var nameResult = await RunGitCommandAsync(".", "config --global user.name");
            status.GitUserName = nameResult.Output?.Trim() ?? "";

            // Check for available SSH keys
            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            status.SshKeysFound = new List<SshKeyInfo>();

            if (Directory.Exists(sshDir))
            {
                // Look for common SSH key files
                var keyPatterns = new[] { "id_ed25519", "id_rsa", "id_ecdsa" };
                foreach (var pattern in keyPatterns)
                {
                    var privateKeyPath = Path.Combine(sshDir, pattern);
                    var publicKeyPath = Path.Combine(sshDir, $"{pattern}.pub");

                    if (File.Exists(publicKeyPath))
                    {
                        var keyInfo = new SshKeyInfo
                        {
                            Name = pattern,
                            PublicKeyPath = publicKeyPath,
                            PrivateKeyPath = File.Exists(privateKeyPath) ? privateKeyPath : null,
                            Type = pattern.Contains("ed25519") ? "Ed25519" : 
                                   pattern.Contains("ecdsa") ? "ECDSA" : "RSA"
                        };

                        // Try to read the public key
                        try
                        {
                            var pubKeyContent = await File.ReadAllTextAsync(publicKeyPath);
                            keyInfo.PublicKey = pubKeyContent.Trim();
                            
                            // Extract email from key comment (usually at the end)
                            var parts = pubKeyContent.Split(' ');
                            if (parts.Length >= 3)
                            {
                                keyInfo.Email = parts[^1].Trim();
                            }
                        }
                        catch { }

                        status.SshKeysFound.Add(keyInfo);
                    }
                }
            }

            status.HasSshKeys = status.SshKeysFound.Count > 0;
            
            // Determine if signing is fully configured
            status.IsFullyConfigured = status.IsConfiguredForSsh && 
                                       status.CommitSigningEnabled && 
                                       !string.IsNullOrEmpty(status.SigningKeyPath) &&
                                       File.Exists(status.SigningKeyPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

            _logger.LogInformation("?? SSH Signing Status: Configured={Configured}, HasKeys={HasKeys}, FullyConfigured={FullyConfigured}",
                status.IsConfiguredForSsh, status.HasSshKeys, status.IsFullyConfigured);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SSH signing status");
            status.Error = ex.Message;
        }

        return status;
    }

    /// <summary>
    /// Configures Git to use SSH signing with the specified key
    /// </summary>
    public async Task<GitOperationResult> ConfigureSshSigningAsync(string keyPath)
    {
        try
        {
            _logger.LogInformation("?? Configuring SSH signing with key: {KeyPath}", keyPath);

            // Normalize the key path
            var normalizedPath = keyPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            
            // Ensure it's the public key
            if (!normalizedPath.EndsWith(".pub"))
            {
                normalizedPath += ".pub";
            }

            if (!File.Exists(normalizedPath))
            {
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"SSH public key not found: {normalizedPath}" 
                };
            }

            // Set gpg.format to ssh
            var formatResult = await RunGitCommandAsync(".", "config --global gpg.format ssh");
            if (!formatResult.Success)
            {
                return new GitOperationResult { Success = false, Message = $"Failed to set gpg.format: {formatResult.Error}" };
            }

            // Set the signing key
            var keyResult = await RunGitCommandAsync(".", $"config --global user.signingkey \"{normalizedPath}\"");
            if (!keyResult.Success)
            {
                return new GitOperationResult { Success = false, Message = $"Failed to set signing key: {keyResult.Error}" };
            }

            // Enable commit signing
            var commitSignResult = await RunGitCommandAsync(".", "config --global commit.gpgsign true");
            if (!commitSignResult.Success)
            {
                return new GitOperationResult { Success = false, Message = $"Failed to enable commit signing: {commitSignResult.Error}" };
            }

            // Enable tag signing
            var tagSignResult = await RunGitCommandAsync(".", "config --global tag.gpgsign true");
            if (!tagSignResult.Success)
            {
                return new GitOperationResult { Success = false, Message = $"Failed to enable tag signing: {tagSignResult.Error}" };
            }

            // 🔐 Configurar allowedSignersFile para que git pueda VERIFICAR las firmas SSH
            // Sin esto, "git log --format=%G?" siempre devuelve "N" aunque el commit esté firmado
            var allowedSignersPath = Path.Combine(Path.GetDirectoryName(normalizedPath)!, "allowed_signers");
            try
            {
                // Leer la clave pública y el email de git
                var pubKeyContent = (await File.ReadAllTextAsync(normalizedPath)).Trim();
                var emailResult = await RunGitCommandAsync(".", "config --global user.email");
                var email = emailResult.Output?.Trim() ?? "electronico@aquafrisch.com";
                
                // Formato: email namespaces="git" <key-type> <key-data>
                var allowedSignerLine = $"{email} namespaces=\"git\" {pubKeyContent}";
                
                // Crear/actualizar el archivo allowed_signers
                var existingContent = File.Exists(allowedSignersPath) ? await File.ReadAllTextAsync(allowedSignersPath) : "";
                if (!existingContent.Contains(pubKeyContent.Split(' ')[1])) // No duplicar por key data
                {
                    var newContent = string.IsNullOrWhiteSpace(existingContent) 
                        ? allowedSignerLine 
                        : existingContent.TrimEnd() + "\n" + allowedSignerLine;
                    await File.WriteAllTextAsync(allowedSignersPath, newContent + "\n");
                }
                
                // Configurar git para usar el archivo
                var allowedResult = await RunGitCommandAsync(".", $"config --global gpg.ssh.allowedSignersFile \"{allowedSignersPath}\"");
                if (!allowedResult.Success)
                {
                    _logger.LogWarning("⚠️ Failed to set allowedSignersFile: {Error}", allowedResult.Error);
                }
                else
                {
                    _logger.LogInformation("✅ allowedSignersFile configured: {Path}", allowedSignersPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not configure allowedSignersFile - signature verification may not work");
            }

            // 🔐 Auto-guardar la clave pública en authorized_signing_keys.json para sincronización entre servidores
            try
            {
                var pubKeyContent = (await File.ReadAllTextAsync(normalizedPath)).Trim();
                var parts = pubKeyContent.Split(' ');
                if (parts.Length >= 2)
                {
                    var fp = CalculateKeyFingerprint(parts[1]);
                    var authorizedKeys = await GetAuthorizedKeysAsync();
                    var existing = authorizedKeys.FirstOrDefault(k => k.Fingerprint.Equals(fp, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        // Actualizar la clave pública si no la tenía
                        if (string.IsNullOrEmpty(existing.PublicKey))
                        {
                            existing.PublicKey = pubKeyContent;
                            existing.MachineName = Environment.MachineName;
                        }
                    }
                    else
                    {
                        // Clave no existe en authorized_signing_keys → crear entrada nueva
                        var emailResult2 = await RunGitCommandAsync(".", "config --global user.email");
                        var keyEmail = emailResult2.Output?.Trim() ?? "electronico@aquafrisch.com";
                        authorizedKeys.Add(new AuthorizedKey
                        {
                            Fingerprint = fp,
                            OwnerName = Environment.MachineName,
                            OwnerEmail = keyEmail,
                            AuthorizedAt = DateTime.UtcNow,
                            AuthorizedBy = "system-auto",
                            PublicKey = pubKeyContent,
                            MachineName = Environment.MachineName
                        });
                        _logger.LogInformation("🔐 Auto-added local SSH key to authorized_signing_keys for cross-server sync");
                    }
                    
                    var json = System.Text.Json.JsonSerializer.Serialize(authorizedKeys, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(AuthorizedKeysFilePath, json);
                    _logger.LogInformation("🔐 Saved authorized_signing_keys.json with PublicKey for {Machine}", Environment.MachineName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not update authorized key with public key");
            }

            return new GitOperationResult 
            { 
                Success = true, 
                Message = $"SSH signing configured successfully with key: {normalizedPath}" 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring SSH signing");
            return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" };
        }
    }

    /// <summary>
    /// Desactiva SSH signing - quita la configuraci�n de firma
    /// </summary>
    public async Task<GitOperationResult> DisableSshSigningAsync()
    {
        try
        {
            _logger.LogInformation("?? Disabling SSH signing...");

            // Disable commit signing
            var commitResult = await RunGitCommandAsync(".", "config --global --unset commit.gpgsign");
            
            // Disable tag signing
            var tagResult = await RunGitCommandAsync(".", "config --global --unset tag.gpgsign");
            
            // Remove signing key configuration
            var keyResult = await RunGitCommandAsync(".", "config --global --unset user.signingkey");
            
            // Reset gpg format to default (optional)
            var formatResult = await RunGitCommandAsync(".", "config --global --unset gpg.format");

            _logger.LogInformation("?? SSH signing disabled successfully");

            return new GitOperationResult 
            { 
                Success = true, 
                Message = "SSH signing disabled. Commits will no longer be signed." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling SSH signing");
            return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" };
        }
    }

    /// <summary>
    /// Valida que la identidad del usuario Git coincida con la clave SSH
    /// Para evitar suplantaci�n de identidad (EU CRA compliance)
    /// </summary>
    public async Task<IdentityValidationResult> ValidateSigningIdentityAsync()
    {
        var result = new IdentityValidationResult();

        try
        {
            // Get current Git user email
            var emailResult = await RunGitCommandAsync(".", "config --global user.email");
            result.GitEmail = emailResult.Output?.Trim() ?? "";

            // Get current Git user name
            var nameResult = await RunGitCommandAsync(".", "config --global user.name");
            result.GitUserName = nameResult.Output?.Trim() ?? "";

            // Get signing key path
            var signingKeyResult = await RunGitCommandAsync(".", "config --global user.signingkey");
            var signingKeyPath = signingKeyResult.Output?.Trim() ?? "";
            result.SigningKeyPath = signingKeyPath;

            if (string.IsNullOrEmpty(signingKeyPath))
            {
                result.IsValid = true; // No signing configured = no identity check needed
                result.Message = "SSH signing not configured";
                return result;
            }

            // Normalize path and read SSH key to get email from comment
            var normalizedPath = signingKeyPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            
            if (!File.Exists(normalizedPath))
            {
                result.IsValid = false;
                result.Message = $"Signing key not found: {normalizedPath}";
                return result;
            }

            var publicKeyContent = await File.ReadAllTextAsync(normalizedPath);
            var parts = publicKeyContent.Trim().Split(' ');
            
            // SSH public key format: type base64key email@comment
            if (parts.Length >= 3)
            {
                result.KeyEmail = parts[^1].Trim();
            }

            // Calculate key fingerprint for display
            if (parts.Length >= 2)
            {
                result.KeyFingerprint = CalculateKeyFingerprint(parts[1]);
            }

            // Validate: Git email should match SSH key email
            if (!string.IsNullOrEmpty(result.KeyEmail) && !string.IsNullOrEmpty(result.GitEmail))
            {
                result.EmailsMatch = result.GitEmail.Equals(result.KeyEmail, StringComparison.OrdinalIgnoreCase);
                
                if (!result.EmailsMatch)
                {
                    result.IsValid = false;
                    result.Message = $"?? IDENTITY MISMATCH: Git email ({result.GitEmail}) doesn't match SSH key email ({result.KeyEmail}). This could indicate identity spoofing!";
                    result.Warning = "La identidad del commit podr�a no coincidir con el firmante real.";
                    _logger.LogWarning("?? Identity mismatch detected! Git: {GitEmail}, Key: {KeyEmail}", result.GitEmail, result.KeyEmail);
                }
                else
                {
                    result.IsValid = true;
                    result.Message = "? Identity verified: Git email matches SSH key email";
                }
            }
            else
            {
                result.IsValid = true;
                result.Message = "Identity validation skipped (missing email in key or git config)";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating signing identity");
            result.IsValid = false;
            result.Message = $"Error validating identity: {ex.Message}";
            return result;
        }
    }

    private string CalculateKeyFingerprint(string base64Key)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(base64Key);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(keyBytes);
            return "SHA256:" + Convert.ToBase64String(hashBytes).TrimEnd('=');
        }
        catch
        {
            return "Unknown";
        }
    }

    #endregion

    #region SSH Key Management (Authorized Keys System)

    private static readonly string AuthorizedKeysFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "authorized_signing_keys.json");
    private static readonly string AccessControlConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "access_control_config.json");

    /// <summary>
    /// Obtiene la configuraci�n de control de acceso
    /// </summary>
    public async Task<AccessControlConfig> GetAccessControlConfigAsync()
    {
        try
        {
            if (!File.Exists(AccessControlConfigPath))
            {
                // Default: disabled (open mode)
                return new AccessControlConfig { IsEnabled = false };
            }
            var json = await File.ReadAllTextAsync(AccessControlConfigPath);
            return System.Text.Json.JsonSerializer.Deserialize<AccessControlConfig>(json) ?? new AccessControlConfig();
        }
        catch
        {
            return new AccessControlConfig { IsEnabled = false };
        }
    }

    /// <summary>
    /// Activa o desactiva el control de acceso por claves
    /// </summary>
    public async Task<GitOperationResult> SetAccessControlEnabledAsync(bool enabled)
    {
        try
        {
            var config = new AccessControlConfig 
            { 
                IsEnabled = enabled,
                LastModified = DateTime.Now,
                ModifiedBy = Environment.UserName
            };
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AccessControlConfigPath, json);
            
            _logger.LogInformation("?? Access control {Status} by {User}", enabled ? "ENABLED" : "DISABLED", Environment.UserName);
            return new GitOperationResult 
            { 
                Success = true, 
                Message = enabled 
                    ? "? Control de acceso ACTIVADO. Solo claves autorizadas pueden modificar el software." 
                    : "?? Control de acceso DESACTIVADO. Cualquiera puede modificar el software."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting access control");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Elimina las claves SSH del disco (~/.ssh/id_ed25519*)
    /// </summary>
    public async Task<GitOperationResult> DeleteSshKeysAsync()
    {
        try
        {
            _logger.LogInformation("??? Deleting SSH keys...");
            
            // First disable SSH signing
            await DisableSshSigningAsync();

            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            var keysDeleted = new List<string>();
            var keyPatterns = new[] { "id_ed25519", "id_rsa", "id_ecdsa" };

            foreach (var pattern in keyPatterns)
            {
                var privateKeyPath = Path.Combine(sshDir, pattern);
                var publicKeyPath = Path.Combine(sshDir, $"{pattern}.pub");

                if (File.Exists(privateKeyPath))
                {
                    File.Delete(privateKeyPath);
                    keysDeleted.Add(pattern);
                }
                if (File.Exists(publicKeyPath))
                {
                    File.Delete(publicKeyPath);
                }
            }

            if (keysDeleted.Count == 0)
            {
                return new GitOperationResult { Success = true, Message = "No SSH keys found to delete." };
            }

            _logger.LogInformation("??? Deleted SSH keys: {Keys}", string.Join(", ", keysDeleted));
            return new GitOperationResult 
            { 
                Success = true, 
                Message = $"SSH keys deleted: {string.Join(", ", keysDeleted)}. SSH signing disabled." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SSH keys");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Exporta la clave SSH actual para que el usuario pueda guardarla
    /// </summary>
    public async Task<SshKeyExportResult> ExportSshKeyAsync()
    {
        var result = new SshKeyExportResult();
        try
        {
            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            var privateKeyPath = Path.Combine(sshDir, "id_ed25519");
            var publicKeyPath = Path.Combine(sshDir, "id_ed25519.pub");

            if (!File.Exists(privateKeyPath) || !File.Exists(publicKeyPath))
            {
                result.Success = false;
                result.Message = "No Ed25519 SSH key found to export.";
                return result;
            }

            result.PrivateKey = await File.ReadAllTextAsync(privateKeyPath);
            result.PublicKey = await File.ReadAllTextAsync(publicKeyPath);
            
            // Calculate fingerprint
            var pubKeyParts = result.PublicKey.Trim().Split(' ');
            if (pubKeyParts.Length >= 2)
            {
                result.Fingerprint = CalculateKeyFingerprint(pubKeyParts[1]);
                if (pubKeyParts.Length >= 3)
                {
                    result.Email = pubKeyParts[^1].Trim();
                }
            }

            result.Success = true;
            result.Message = "SSH key exported successfully. Keep the private key secure!";
            _logger.LogInformation("?? SSH key exported for {Email}", result.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting SSH key");
            result.Success = false;
            result.Message = $"Error: {ex.Message}";
        }
        return result;
    }

    /// <summary>
    /// Importa una clave SSH (privada + p�blica) al sistema
    /// </summary>
    public async Task<GitOperationResult> ImportSshKeyAsync(string privateKey, string publicKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(publicKey))
            {
                return new GitOperationResult { Success = false, Message = "Both private and public keys are required." };
            }

            // Validate key format
            if (!privateKey.Contains("BEGIN OPENSSH PRIVATE KEY") && !privateKey.Contains("BEGIN RSA PRIVATE KEY"))
            {
                return new GitOperationResult { Success = false, Message = "Invalid private key format." };
            }

            if (!publicKey.StartsWith("ssh-ed25519") && !publicKey.StartsWith("ssh-rsa"))
            {
                return new GitOperationResult { Success = false, Message = "Invalid public key format." };
            }

            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            
            // Create .ssh directory if needed
            if (!Directory.Exists(sshDir))
            {
                Directory.CreateDirectory(sshDir);
            }

            // Determine key type
            var keyName = publicKey.StartsWith("ssh-ed25519") ? "id_ed25519" : "id_rsa";
            var privateKeyPath = Path.Combine(sshDir, keyName);
            var publicKeyPath = Path.Combine(sshDir, $"{keyName}.pub");

            // Check if keys already exist
            if (File.Exists(privateKeyPath))
            {
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"SSH key already exists at {privateKeyPath}. Delete existing keys first." 
                };
            }

            // Write keys with proper permissions
            await File.WriteAllTextAsync(privateKeyPath, privateKey.Trim() + "\n");
            await File.WriteAllTextAsync(publicKeyPath, publicKey.Trim() + "\n");

            // On Windows, we need to set proper permissions for the private key
            // This is done automatically by OpenSSH on Windows for user-owned files

            _logger.LogInformation("?? SSH key imported: {KeyName}", keyName);
            return new GitOperationResult 
            { 
                Success = true, 
                Message = $"SSH key imported to {privateKeyPath}. Now configure signing to use it." 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing SSH key");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Obtiene la lista de claves autorizadas para modificar el software
    /// </summary>
    public async Task<List<AuthorizedKey>> GetAuthorizedKeysAsync()
    {
        try
        {
            if (!File.Exists(AuthorizedKeysFilePath))
            {
                return new List<AuthorizedKey>();
            }

            var json = await File.ReadAllTextAsync(AuthorizedKeysFilePath);
            return System.Text.Json.JsonSerializer.Deserialize<List<AuthorizedKey>>(json) ?? new List<AuthorizedKey>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading authorized keys");
            return new List<AuthorizedKey>();
        }
    }

    /// <summary>
    /// A�ade una clave a la lista de autorizados
    /// </summary>
    public async Task<GitOperationResult> AddAuthorizedKeyAsync(string fingerprint, string ownerName, string ownerEmail)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(ownerName))
            {
                return new GitOperationResult { Success = false, Message = "Fingerprint and owner name are required." };
            }

            var authorizedKeys = await GetAuthorizedKeysAsync();
            
            // Check if already exists
            if (authorizedKeys.Any(k => k.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase)))
            {
                return new GitOperationResult { Success = false, Message = "This key is already authorized." };
            }

            // Intentar obtener la clave pública si es la clave local
            var publicKeyContent = "";
            try
            {
                var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                var pubKeyPath = Path.Combine(sshDir, "id_ed25519.pub");
                if (File.Exists(pubKeyPath))
                {
                    var localPubKey = (await File.ReadAllTextAsync(pubKeyPath)).Trim();
                    var localFingerprint = CalculateKeyFingerprint(localPubKey.Split(' ')[1]);
                    if (localFingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        publicKeyContent = localPubKey;
                    }
                }
            }
            catch { }

            authorizedKeys.Add(new AuthorizedKey
            {
                Fingerprint = fingerprint,
                OwnerName = ownerName,
                OwnerEmail = ownerEmail,
                AuthorizedAt = DateTime.Now,
                AuthorizedBy = Environment.UserName,
                PublicKey = publicKeyContent,
                MachineName = Environment.MachineName
            });

            var json = System.Text.Json.JsonSerializer.Serialize(authorizedKeys, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AuthorizedKeysFilePath, json);

            _logger.LogInformation("? Authorized key added for {Owner} ({Email})", ownerName, ownerEmail);
            return new GitOperationResult { Success = true, Message = $"Key authorized for {ownerName}." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding authorized key");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Elimina una clave de la lista de autorizados
    /// </summary>
    public async Task<GitOperationResult> RemoveAuthorizedKeyAsync(string fingerprint)
    {
        try
        {
            var authorizedKeys = await GetAuthorizedKeysAsync();
            var keyToRemove = authorizedKeys.FirstOrDefault(k => k.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase));
            
            if (keyToRemove == null)
            {
                return new GitOperationResult { Success = false, Message = "Key not found in authorized list." };
            }

            authorizedKeys.Remove(keyToRemove);

            var json = System.Text.Json.JsonSerializer.Serialize(authorizedKeys, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AuthorizedKeysFilePath, json);

            _logger.LogInformation("?? Removed authorized key for {Owner}", keyToRemove.OwnerName);
            return new GitOperationResult { Success = true, Message = $"Key for {keyToRemove.OwnerName} removed from authorized list." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing authorized key");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Verifica si la clave SSH actual est� en la lista de autorizados
    /// </summary>
    public async Task<KeyAuthorizationResult> CheckKeyAuthorizationAsync()
    {
        var result = new KeyAuthorizationResult();
        try
        {
            // Get current key fingerprint
            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            var publicKeyPath = Path.Combine(sshDir, "id_ed25519.pub");

            if (!File.Exists(publicKeyPath))
            {
                result.HasKey = false;
                result.IsAuthorized = false;
                result.Message = "No SSH key found. Import or generate a key first.";
                return result;
            }

            result.HasKey = true;
            var publicKey = await File.ReadAllTextAsync(publicKeyPath);
            var parts = publicKey.Trim().Split(' ');
            
            if (parts.Length >= 2)
            {
                result.CurrentFingerprint = CalculateKeyFingerprint(parts[1]);
                if (parts.Length >= 3)
                {
                    result.CurrentKeyEmail = parts[^1].Trim();
                }
            }

            // Get access control configuration
            var accessConfig = await GetAccessControlConfigAsync();
            result.AccessControlEnabled = accessConfig.IsEnabled;

            // If access control is disabled, allow everyone
            if (!accessConfig.IsEnabled)
            {
                result.IsAuthorized = true;
                result.Message = "?? Control de acceso DESACTIVADO. Cualquiera puede modificar el software.";
                result.AuthorizationMode = "disabled";
                return result;
            }

            // Check against authorized keys
            var authorizedKeys = await GetAuthorizedKeysAsync();
            
            if (authorizedKeys.Count == 0)
            {
                // Access control enabled but no keys = block everyone (must add keys first)
                result.IsAuthorized = false;
                result.Message = "?? Control de acceso ACTIVADO pero no hay claves autorizadas. A�ade claves para poder modificar.";
                result.AuthorizationMode = "restricted";
                return result;
            }

            var matchingKey = authorizedKeys.FirstOrDefault(k => 
                k.Fingerprint.Equals(result.CurrentFingerprint, StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null)
            {
                result.IsAuthorized = true;
                result.AuthorizedOwner = matchingKey.OwnerName;
                result.AuthorizedEmail = matchingKey.OwnerEmail;
                result.Message = $"? Clave autorizada para: {matchingKey.OwnerName} ({matchingKey.OwnerEmail})";
                result.AuthorizationMode = "restricted";
            }
            else
            {
                result.IsAuthorized = false;
                result.Message = $"?? ACCESO DENEGADO: Tu clave ({result.CurrentFingerprint}) no est� en la lista de autorizados.";
                result.AuthorizationMode = "restricted";
            }

            result.TotalAuthorizedKeys = authorizedKeys.Count;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key authorization");
            result.IsAuthorized = false;
            result.Message = $"Error: {ex.Message}";
            return result;
        }
    }

    #endregion
}

// Environment Detection
public class ScadaEnvironmentInfo
{
    public string Environment { get; set; } = "development"; // "development" or "production"
    public bool IsProduction => Environment.ToLower() == "production";
    public bool IsDevelopment => !IsProduction;
    public Dictionary<string, bool> RepoEditPermissions { get; set; } = new();
}

public class AllRepositoriesStatus { public DateTime Timestamp { get; set; } public Dictionary<string, RepositoryStatus> Repositories { get; set; } = new(); public ScadaEnvironmentInfo? EnvironmentInfo { get; set; } }
public class RepositoryStatus { public string Path { get; set; } = ""; public bool IsValid { get; set; } public string? Error { get; set; } public string CurrentBranch { get; set; } = ""; public CommitInfo? LastCommit { get; set; } public bool HasChanges { get; set; } public List<ModifiedFile> ModifiedFiles { get; set; } = new(); public int CommitsAhead { get; set; } public int CommitsBehind { get; set; } public string? RemoteUrl { get; set; } public bool IsGitRepo { get; set; } = true; public bool IsEditable { get; set; } = true; }
public class CommitInfo { public string Hash { get; set; } = ""; public string ShortHash { get; set; } = ""; public string Message { get; set; } = ""; public DateTime Date { get; set; } public string Author { get; set; } = ""; }
public class ModifiedFile { public string Path { get; set; } = ""; public string Status { get; set; } = ""; public string StatusCode { get; set; } = ""; }
public class GitOperationResult { public bool Success { get; set; } public string Message { get; set; } = ""; public string? Output { get; set; } }
public class TagInfo { public string Name { get; set; } = ""; public DateTime Date { get; set; } public string Message { get; set; } = ""; }

// SSH Signing Models
public class SshSigningStatus
{
    public bool IsConfiguredForSsh { get; set; }
    public bool IsFullyConfigured { get; set; }
    public bool CommitSigningEnabled { get; set; }
    public bool TagSigningEnabled { get; set; }
    public string GpgFormat { get; set; } = "";
    public string SigningKeyPath { get; set; } = "";
    public string GitUserEmail { get; set; } = "";
    public string GitUserName { get; set; } = "";
    public bool HasSshKeys { get; set; }
    public List<SshKeyInfo> SshKeysFound { get; set; } = new();
    public string? Error { get; set; }
}

public class SshKeyInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? PublicKeyPath { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PublicKey { get; set; }
    public string? Email { get; set; }
}

// Identity Validation for EU CRA compliance
public class IdentityValidationResult
{
    public bool IsValid { get; set; }
    public bool EmailsMatch { get; set; }
    public string GitEmail { get; set; } = "";
    public string GitUserName { get; set; } = "";
    public string KeyEmail { get; set; } = "";
    public string SigningKeyPath { get; set; } = "";
    public string KeyFingerprint { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Warning { get; set; }
}

// SSH Key Export Result
public class SshKeyExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? PrivateKey { get; set; }
    public string? PublicKey { get; set; }
    public string? Fingerprint { get; set; }
    public string? Email { get; set; }
}

// Authorized Key for software modification
public class AuthorizedKey
{
    public string Fingerprint { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string OwnerEmail { get; set; } = "";
    public DateTime AuthorizedAt { get; set; }
    public string AuthorizedBy { get; set; } = "";
    public string PublicKey { get; set; } = ""; // Clave pública completa (ssh-ed25519 AAAA...)
    public string MachineName { get; set; } = ""; // Nombre del PC donde se generó
}

// Key Authorization Check Result
public class KeyAuthorizationResult
{
    public bool HasKey { get; set; }
    public bool IsAuthorized { get; set; }
    public bool AccessControlEnabled { get; set; }
    public string CurrentFingerprint { get; set; } = "";
    public string CurrentKeyEmail { get; set; } = "";
    public string? AuthorizedOwner { get; set; }
    public string? AuthorizedEmail { get; set; }
    public string Message { get; set; } = "";
    public string AuthorizationMode { get; set; } = ""; // "disabled", "restricted"
    public int TotalAuthorizedKeys { get; set; }
}

// Access Control Configuration
public class AccessControlConfig
{
    public bool IsEnabled { get; set; }
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; } = "";
}

// Release Notes Result
public class ReleaseNotesResult
{
    public string Version { get; set; } = "";
    public DateTime Date { get; set; }
    public string FromTag { get; set; } = "";
    public string ToTag { get; set; } = "";
    public int CommitCount { get; set; }
    public List<CommitInfo> Commits { get; set; } = new();
    public string Markdown { get; set; } = "";
    public string? TagMessage { get; set; }
}
