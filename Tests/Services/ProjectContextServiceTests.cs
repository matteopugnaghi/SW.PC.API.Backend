using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Moq;
using SW.PC.API.Backend.Services;
using System.Text.Json;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests for ProjectContextService — multi-project / legacy path resolution.
/// Uses temp directories to simulate the workspace structure.
/// </summary>
public class ProjectContextServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<ILogger<ProjectContextService>> _loggerMock = new();

    public ProjectContextServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"pcs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        // Create required directories that ProjectContextService expects
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Projects"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "ExcelConfigs"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "wwwroot", "models"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Data"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    private IWebHostEnvironment CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(e => e.ContentRootPath).Returns(_tempRoot);
        mock.Setup(e => e.WebRootPath).Returns(Path.Combine(_tempRoot, "wwwroot"));
        mock.Setup(e => e.ContentRootFileProvider).Returns(Mock.Of<IFileProvider>());
        mock.Setup(e => e.WebRootFileProvider).Returns(Mock.Of<IFileProvider>());
        return mock.Object;
    }

    private void WriteActiveProject(string projectId)
    {
        var json = JsonSerializer.Serialize(new { activeProject = projectId });
        File.WriteAllText(Path.Combine(_tempRoot, "active-project.json"), json);
    }

    private void CreateProjectFolder(string projectId)
    {
        var projectDir = Path.Combine(_tempRoot, "Projects", projectId);
        Directory.CreateDirectory(Path.Combine(projectDir, "config"));
        Directory.CreateDirectory(Path.Combine(projectDir, "models"));
        Directory.CreateDirectory(Path.Combine(projectDir, "data"));
        Directory.CreateDirectory(Path.Combine(projectDir, "backups"));
    }

    // ===== Legacy Mode Tests =====

    [Fact]
    public void LegacyMode_WhenNoActiveProjectFile()
    {
        // No active-project.json → defaults to "default" (legacy)
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    [Fact]
    public void LegacyMode_WhenActiveProjectIsDefault()
    {
        WriteActiveProject("default");

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    [Fact]
    public void LegacyMode_PathsPointToRootFolders()
    {
        WriteActiveProject("default");

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        Assert.Equal(Path.Combine(_tempRoot, "ExcelConfigs"), sut.ConfigPath);
        Assert.Equal(Path.Combine(_tempRoot, "wwwroot", "models"), sut.ModelsPath);
        Assert.Equal(Path.Combine(_tempRoot, "Data"), sut.DataPath);
        Assert.Contains("Aquafrisch.db", sut.DatabasePath);
    }

    // ===== Multi-Project Mode Tests =====

    [Fact]
    public void MultiProjectMode_WhenProjectFolderExists()
    {
        var projectId = "test-project-001";
        CreateProjectFolder(projectId);
        WriteActiveProject(projectId);

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        Assert.Equal(projectId, sut.ActiveProjectId);
        Assert.True(sut.IsMultiProjectMode);
    }

    [Fact]
    public void MultiProjectMode_PathsPointToProjectFolder()
    {
        var projectId = "test-project-001";
        CreateProjectFolder(projectId);
        WriteActiveProject(projectId);

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        var expectedBase = Path.Combine(_tempRoot, "Projects", projectId);
        Assert.Equal(expectedBase, sut.ProjectBasePath);
        Assert.Equal(Path.Combine(expectedBase, "config"), sut.ConfigPath);
        Assert.Equal(Path.Combine(expectedBase, "models"), sut.ModelsPath);
        Assert.Equal(Path.Combine(expectedBase, "data"), sut.DataPath);
        Assert.Equal(Path.Combine(expectedBase, "backups"), sut.BackupsPath);
        Assert.Equal(Path.Combine(expectedBase, "data", "project.db"), sut.DatabasePath);
        Assert.Equal(Path.Combine(expectedBase, "docs"), sut.DocsPath);
    }

    [Fact]
    public void FallsBackToLegacy_WhenProjectFolderMissing()
    {
        // Set a project ID but don't create its folder
        WriteActiveProject("nonexistent-project");

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    // ===== Project Management =====

    [Fact]
    public void ProjectExists_ReturnsTrueForDefault()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);
        Assert.True(sut.ProjectExists("default"));
    }

    [Fact]
    public void ProjectExists_ReturnsTrueForExistingProject()
    {
        CreateProjectFolder("my-project");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);
        Assert.True(sut.ProjectExists("my-project"));
    }

    [Fact]
    public void ProjectExists_ReturnsFalseForMissingProject()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);
        Assert.False(sut.ProjectExists("does-not-exist"));
    }

    [Fact]
    public async Task CreateProjectStructureAsync_CreatesAllFolders()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        var result = await sut.CreateProjectStructureAsync("new-project");

        Assert.True(result);
        var basePath = Path.Combine(_tempRoot, "Projects", "new-project");
        Assert.True(Directory.Exists(Path.Combine(basePath, "config")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "models")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "data")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "backups")));
        Assert.True(Directory.Exists(Path.Combine(basePath, "docs")));
        Assert.True(File.Exists(Path.Combine(basePath, "README.md")));
    }

    [Fact]
    public async Task CreateProjectStructureAsync_ReturnsFalseIfAlreadyExists()
    {
        CreateProjectFolder("existing");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        var result = await sut.CreateProjectStructureAsync("existing");

        Assert.False(result);
    }

    [Fact]
    public void GetAvailableProjects_IncludesDefaultAndCustomProjects()
    {
        CreateProjectFolder("project-a");
        CreateProjectFolder("project-b");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        var projects = sut.GetAvailableProjects().ToList();

        Assert.True(projects.Count >= 3); // default + project-a + project-b + _template might exist
        Assert.Contains(projects, p => p.Id == "default");
        Assert.Contains(projects, p => p.Id == "project-a");
        Assert.Contains(projects, p => p.Id == "project-b");
    }

    [Fact]
    public void GetAvailableProjects_ExcludesUnderscorePrefixed()
    {
        CreateProjectFolder("_template");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);

        var projects = sut.GetAvailableProjects().ToList();

        Assert.DoesNotContain(projects, p => p.Id == "_template");
    }

    [Fact]
    public void ReloadActiveProject_UpdatesWhenFileChanges()
    {
        WriteActiveProject("default");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object);
        Assert.Equal("default", sut.ActiveProjectId);

        // Change the active project and reload
        CreateProjectFolder("switched-project");
        WriteActiveProject("switched-project");
        sut.ReloadActiveProject();

        Assert.Equal("switched-project", sut.ActiveProjectId);
        Assert.True(sut.IsMultiProjectMode);
    }
}
