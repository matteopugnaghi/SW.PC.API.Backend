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
    Task<GitOperationResult> DiscardChangesAsync(string repoPath, string? filePath = null);
    Task<GitOperationResult> RevertToCommitAsync(string repoPath, string commitHash);
    Task<List<ModifiedFile>> GetModifiedFilesAsync(string repoPath);
    (string Backend, string Frontend, string TwinCAT) GetRepoPaths();
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
            _logger.LogInformation("Pushing changes from {Path}", repoPath);
            var result = await RunGitCommandAsync(repoPath, "push");
            if (result.Success) return new GitOperationResult { Success = true, Message = "Push completed successfully", Output = result.Output };
            return new GitOperationResult { Success = false, Message = $"Push failed: {result.Error}" };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error pushing from {Path}", repoPath); return new GitOperationResult { Success = false, Message = $"Exception: {ex.Message}" }; }
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
}

public class AllRepositoriesStatus { public DateTime Timestamp { get; set; } public Dictionary<string, RepositoryStatus> Repositories { get; set; } = new(); }
public class RepositoryStatus { public string Path { get; set; } = ""; public bool IsValid { get; set; } public string? Error { get; set; } public string CurrentBranch { get; set; } = ""; public CommitInfo? LastCommit { get; set; } public bool HasChanges { get; set; } public List<ModifiedFile> ModifiedFiles { get; set; } = new(); public int CommitsAhead { get; set; } public int CommitsBehind { get; set; } public string? RemoteUrl { get; set; } }
public class CommitInfo { public string Hash { get; set; } = ""; public string ShortHash { get; set; } = ""; public string Message { get; set; } = ""; public DateTime Date { get; set; } public string Author { get; set; } = ""; }
public class ModifiedFile { public string Path { get; set; } = ""; public string Status { get; set; } = ""; public string StatusCode { get; set; } = ""; }
public class GitOperationResult { public bool Success { get; set; } public string Message { get; set; } = ""; public string? Output { get; set; } }
