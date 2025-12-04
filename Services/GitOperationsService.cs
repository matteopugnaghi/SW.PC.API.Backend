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
}

public class GitOperationsService : IGitOperationsService
{
    private readonly ILogger<GitOperationsService> _logger;
    private readonly ISoftwareIntegrityService _integrityService;

    public GitOperationsService(ILogger<GitOperationsService> logger, ISoftwareIntegrityService integrityService)
    {
        _logger = logger;
        _integrityService = integrityService;
        _logger.LogInformation("🔧 GitOperationsService initialized (using paths from SoftwareIntegrityService)");
    }

    /// <summary>
    /// Obtener las rutas de los repositorios (delegado a SoftwareIntegrityService que lee desde Excel)
    /// </summary>
    public (string Backend, string Frontend, string TwinCAT) GetRepoPaths()
    {
        return _integrityService.GetRepositoryPaths();
    }

    public async Task<AllRepositoriesStatus> GetAllRepositoriesStatusAsync()
    {
        var paths = GetRepoPaths();
        var result = new AllRepositoriesStatus { Timestamp = DateTime.UtcNow, Repositories = new Dictionary<string, RepositoryStatus>() };
        
        if (!string.IsNullOrEmpty(paths.Backend) && Directory.Exists(paths.Backend)) 
            result.Repositories["backend"] = await GetRepositoryStatusAsync(paths.Backend);
        
        if (!string.IsNullOrEmpty(paths.Frontend) && Directory.Exists(paths.Frontend)) 
            result.Repositories["frontend"] = await GetRepositoryStatusAsync(paths.Frontend);
        
        if (!string.IsNullOrEmpty(paths.TwinCAT) && Directory.Exists(paths.TwinCAT)) 
            result.Repositories["twincat"] = await GetRepositoryStatusAsync(paths.TwinCAT);
        
        return result;
    }

    public async Task<RepositoryStatus> GetRepositoryStatusAsync(string repoPath)
    {
        var status = new RepositoryStatus { Path = repoPath, IsValid = false };
        try
        {
            var gitDir = Path.Combine(repoPath, ".git");
            if (!Directory.Exists(gitDir)) { status.Error = "Not a git repository"; return status; }
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

    public async Task<GitOperationResult> CommitAsync(string repoPath, string message)
    {
        try
        {
            // 🔐 EU CRA: Verificar autorización de clave antes de permitir commit
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("🚫 Commit rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"🚫 COMMIT RECHAZADO: Tu clave SSH no está en la lista de autorizadas.\n" +
                              $"Fingerprint: {authResult.CurrentFingerprint}\n" +
                              $"Contacta al administrador para autorizar tu clave."
                };
            }

            _logger.LogInformation("Creating commit in {Path}: {Message}", repoPath, message);
            var addResult = await RunGitCommandAsync(repoPath, "add -A");
            if (!addResult.Success) return new GitOperationResult { Success = false, Message = $"Failed to stage changes: {addResult.Error}" };
            var escapedMessage = message.Replace("\"", "\\\"");
            var commitResult = await RunGitCommandAsync(repoPath, $"commit -m \"{escapedMessage}\"");
            if (commitResult.Success) return new GitOperationResult { Success = true, Message = "Commit created successfully", Output = commitResult.Output };
            if (commitResult.Output?.Contains("nothing to commit") == true) return new GitOperationResult { Success = true, Message = "Nothing to commit - working tree clean" };
            return new GitOperationResult { Success = false, Message = $"Commit failed: {commitResult.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating commit in {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> PushAsync(string repoPath)
    {
        try
        {
            // 🔐 EU CRA: Verificar autorización antes de push
            var authResult = await CheckKeyAuthorizationAsync();
            _logger.LogWarning("🔍 DEBUG Push - AccessControlEnabled: {Enabled}, IsAuthorized: {Auth}, Message: {Msg}", 
                authResult.AccessControlEnabled, authResult.IsAuthorized, authResult.Message);
            
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("🚫 Push rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"🚫 PUSH RECHAZADO: Tu clave SSH no está autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Pushing changes from {Path}", repoPath);
            var result = await RunGitCommandAsync(repoPath, "push");
            if (result.Success) return new GitOperationResult { Success = true, Message = "Push completed successfully", Output = result.Output };
            return new GitOperationResult { Success = false, Message = $"Push failed: {result.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error pushing from {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> ForcePushAsync(string repoPath)
    {
        try
        {
            // 🔐 EU CRA: Verificar autorización antes de force push
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("🚫 Force Push rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"🚫 FORCE PUSH RECHAZADO: Tu clave SSH no está autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogWarning("⚠️ FORCE PUSHING changes from {Path} - This will overwrite remote!", repoPath);
            var result = await RunGitCommandAsync(repoPath, "push --force");
            if (result.Success) return new GitOperationResult { Success = true, Message = "✅ Force Push completado - Remoto sincronizado con local", Output = result.Output };
            return new GitOperationResult { Success = false, Message = $"Force Push failed: {result.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error force pushing from {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

    public async Task<GitOperationResult> DiscardChangesAsync(string repoPath, string? filePath = null)
    {
        try
        {
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
            _logger.LogWarning("REVERTING to commit {Hash} in {Path}", commitHash, repoPath);
            var result = await RunGitCommandAsync(repoPath, $"reset --hard {commitHash}");
            if (result.Success) return new GitOperationResult { Success = true, Message = $"Successfully reverted to commit {commitHash[..7]}", Output = result.Output };
            return new GitOperationResult { Success = false, Message = $"Revert failed: {result.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error reverting to commit {Hash} in {Path}", commitHash, repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
    }

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
                        var fileName = line[3..];
                        files.Add(new ModifiedFile { Path = fileName, Status = ParseGitStatus(status), StatusCode = status });
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error getting modified files for {Path}", repoPath); }
        return files;
    }

    private async Task<(bool Success, string? Output, string? Error)> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = "git", Arguments = arguments, WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 }
            };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var completed = await Task.Run(() => process.WaitForExit(30000));
            if (!completed) { process.Kill(); return (false, null, "Command timed out"); }
            return (process.ExitCode == 0, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (Exception ex) { _logger.LogError(ex, "Error running git command: {Args}", arguments); return (false, null, ex.Message); }
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
            // 🔐 EU CRA: Verificar autorización antes de crear tag/release
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("🚫 Tag creation rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"🚫 RELEASE RECHAZADO: Tu clave SSH no está autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Creating tag {Tag} in {Path}: {Message}", tagName, repoPath, message);
            var escapedMessage = message.Replace("\"", "\\\"");
            var result = await RunGitCommandAsync(repoPath, $"tag -a {tagName} -m \"{escapedMessage}\"");
            
            if (result.Success)
                return new GitOperationResult { Success = true, Message = $"Tag '{tagName}' created successfully", Output = result.Output };
            
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
            // 🔐 EU CRA: Verificar autorización antes de push tags
            var authResult = await CheckKeyAuthorizationAsync();
            if (authResult.AccessControlEnabled && !authResult.IsAuthorized)
            {
                _logger.LogWarning("🚫 Push tags rejected: SSH key not authorized. Fingerprint: {Fingerprint}", authResult.CurrentFingerprint);
                return new GitOperationResult 
                { 
                    Success = false, 
                    Message = $"🚫 PUSH TAGS RECHAZADO: Tu clave SSH no está autorizada.\nFingerprint: {authResult.CurrentFingerprint}"
                };
            }

            _logger.LogInformation("Pushing tags from {Path}", repoPath);
            var result = await RunGitCommandAsync(repoPath, "push --tags");
            
            if (result.Success)
                return new GitOperationResult { Success = true, Message = "Tags pushed successfully", Output = result.Output };
            
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

            _logger.LogInformation("🔐 SSH Signing Status: Configured={Configured}, HasKeys={HasKeys}, FullyConfigured={FullyConfigured}",
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
            _logger.LogInformation("🔐 Configuring SSH signing with key: {KeyPath}", keyPath);

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
    /// Desactiva SSH signing - quita la configuración de firma
    /// </summary>
    public async Task<GitOperationResult> DisableSshSigningAsync()
    {
        try
        {
            _logger.LogInformation("🔐 Disabling SSH signing...");

            // Disable commit signing
            var commitResult = await RunGitCommandAsync(".", "config --global --unset commit.gpgsign");
            
            // Disable tag signing
            var tagResult = await RunGitCommandAsync(".", "config --global --unset tag.gpgsign");
            
            // Remove signing key configuration
            var keyResult = await RunGitCommandAsync(".", "config --global --unset user.signingkey");
            
            // Reset gpg format to default (optional)
            var formatResult = await RunGitCommandAsync(".", "config --global --unset gpg.format");

            _logger.LogInformation("🔐 SSH signing disabled successfully");

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
    /// Para evitar suplantación de identidad (EU CRA compliance)
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
                    result.Message = $"⚠️ IDENTITY MISMATCH: Git email ({result.GitEmail}) doesn't match SSH key email ({result.KeyEmail}). This could indicate identity spoofing!";
                    result.Warning = "La identidad del commit podría no coincidir con el firmante real.";
                    _logger.LogWarning("🚨 Identity mismatch detected! Git: {GitEmail}, Key: {KeyEmail}", result.GitEmail, result.KeyEmail);
                }
                else
                {
                    result.IsValid = true;
                    result.Message = "✅ Identity verified: Git email matches SSH key email";
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
    /// Obtiene la configuración de control de acceso
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
                LastModified = DateTime.UtcNow,
                ModifiedBy = Environment.UserName
            };
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AccessControlConfigPath, json);
            
            _logger.LogInformation("🔒 Access control {Status} by {User}", enabled ? "ENABLED" : "DISABLED", Environment.UserName);
            return new GitOperationResult 
            { 
                Success = true, 
                Message = enabled 
                    ? "✅ Control de acceso ACTIVADO. Solo claves autorizadas pueden modificar el software." 
                    : "⚠️ Control de acceso DESACTIVADO. Cualquiera puede modificar el software."
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
            _logger.LogInformation("🗑️ Deleting SSH keys...");
            
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

            _logger.LogInformation("🗑️ Deleted SSH keys: {Keys}", string.Join(", ", keysDeleted));
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
            _logger.LogInformation("📤 SSH key exported for {Email}", result.Email);
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
    /// Importa una clave SSH (privada + pública) al sistema
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

            _logger.LogInformation("📥 SSH key imported: {KeyName}", keyName);
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
    /// Añade una clave a la lista de autorizados
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

            authorizedKeys.Add(new AuthorizedKey
            {
                Fingerprint = fingerprint,
                OwnerName = ownerName,
                OwnerEmail = ownerEmail,
                AuthorizedAt = DateTime.UtcNow,
                AuthorizedBy = Environment.UserName
            });

            var json = System.Text.Json.JsonSerializer.Serialize(authorizedKeys, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AuthorizedKeysFilePath, json);

            _logger.LogInformation("✅ Authorized key added for {Owner} ({Email})", ownerName, ownerEmail);
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

            _logger.LogInformation("🚫 Removed authorized key for {Owner}", keyToRemove.OwnerName);
            return new GitOperationResult { Success = true, Message = $"Key for {keyToRemove.OwnerName} removed from authorized list." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing authorized key");
            return new GitOperationResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Verifica si la clave SSH actual está en la lista de autorizados
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
                result.Message = "⚠️ Control de acceso DESACTIVADO. Cualquiera puede modificar el software.";
                result.AuthorizationMode = "disabled";
                return result;
            }

            // Check against authorized keys
            var authorizedKeys = await GetAuthorizedKeysAsync();
            
            if (authorizedKeys.Count == 0)
            {
                // Access control enabled but no keys = block everyone (must add keys first)
                result.IsAuthorized = false;
                result.Message = "🚫 Control de acceso ACTIVADO pero no hay claves autorizadas. Añade claves para poder modificar.";
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
                result.Message = $"✅ Clave autorizada para: {matchingKey.OwnerName} ({matchingKey.OwnerEmail})";
                result.AuthorizationMode = "restricted";
            }
            else
            {
                result.IsAuthorized = false;
                result.Message = $"🚫 ACCESO DENEGADO: Tu clave ({result.CurrentFingerprint}) no está en la lista de autorizados.";
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

public class AllRepositoriesStatus { public DateTime Timestamp { get; set; } public Dictionary<string, RepositoryStatus> Repositories { get; set; } = new(); }
public class RepositoryStatus { public string Path { get; set; } = ""; public bool IsValid { get; set; } public string? Error { get; set; } public string CurrentBranch { get; set; } = ""; public CommitInfo? LastCommit { get; set; } public bool HasChanges { get; set; } public List<ModifiedFile> ModifiedFiles { get; set; } = new(); public int CommitsAhead { get; set; } public int CommitsBehind { get; set; } public string? RemoteUrl { get; set; } }
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
