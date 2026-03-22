using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

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
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    [Fact]
    public void LegacyMode_WhenActiveProjectIsDefault()
    {
        WriteActiveProject("default");

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    [Fact]
    public void LegacyMode_PathsPointToRootFolders()
    {
        WriteActiveProject("default");

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

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

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        Assert.Equal(projectId, sut.ActiveProjectId);
        Assert.True(sut.IsMultiProjectMode);
    }

    [Fact]
    public void MultiProjectMode_PathsPointToProjectFolder()
    {
        var projectId = "test-project-001";
        CreateProjectFolder(projectId);
        WriteActiveProject(projectId);

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

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

        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        Assert.Equal("default", sut.ActiveProjectId);
        Assert.False(sut.IsMultiProjectMode);
    }

    // ===== Project Management =====

    [Fact]
    public void ProjectExists_ReturnsTrueForDefault()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);
        Assert.True(sut.ProjectExists("default"));
    }

    [Fact]
    public void ProjectExists_ReturnsTrueForExistingProject()
    {
        CreateProjectFolder("my-project");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);
        Assert.True(sut.ProjectExists("my-project"));
    }

    [Fact]
    public void ProjectExists_ReturnsFalseForMissingProject()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);
        Assert.False(sut.ProjectExists("does-not-exist"));
    }

    [Fact]
    public async Task CreateProjectStructureAsync_CreatesAllFolders()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

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
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        var result = await sut.CreateProjectStructureAsync("existing");

        Assert.False(result);
    }

    [Fact]
    public void GetAvailableProjects_IncludesDefaultAndCustomProjects()
    {
        CreateProjectFolder("project-a");
        CreateProjectFolder("project-b");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

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
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        var projects = sut.GetAvailableProjects().ToList();

        Assert.DoesNotContain(projects, p => p.Id == "_template");
    }

    [Fact]
    public void ReloadActiveProject_UpdatesWhenFileChanges()
    {
        WriteActiveProject("default");
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);
        Assert.Equal("default", sut.ActiveProjectId);

        // Change the active project and reload
        CreateProjectFolder("switched-project");
        WriteActiveProject("switched-project");
        sut.ReloadActiveProject();

        Assert.Equal("switched-project", sut.ActiveProjectId);
        Assert.True(sut.IsMultiProjectMode);
    }

    // ===== ProjectsRootPath Tests (Custom shared folder for enterprise multi-instance) =====

    private IConfiguration CreateConfigWithProjectsRootPath(string customPath)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectsRootPath"] = customPath
            })
            .Build();
    }

    [Fact]
    public void ProjectsRootPath_DefaultsToContentRootProjects()
    {
        var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object);

        Assert.Equal(Path.Combine(_tempRoot, "Projects"), sut.ProjectsRootPath);
    }

    [Fact]
    public void ProjectsRootPath_UsesCustomPathFromConfig()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        Directory.CreateDirectory(customPath);
        try
        {
            var config = CreateConfigWithProjectsRootPath(customPath);
            var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);

            Assert.Equal(customPath, sut.ProjectsRootPath);
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }

    [Fact]
    public void ProjectsRootPath_Custom_PathsPointToSharedFolder()
    {
        // Simulate enterprise: shared Projects folder outside Backend
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        var projectId = "enterprise-project";
        var projectDir = Path.Combine(customPath, projectId);
        Directory.CreateDirectory(Path.Combine(projectDir, "config"));
        Directory.CreateDirectory(Path.Combine(projectDir, "models"));
        Directory.CreateDirectory(Path.Combine(projectDir, "data"));
        Directory.CreateDirectory(Path.Combine(projectDir, "backups"));

        try
        {
            WriteActiveProject(projectId);
            var config = CreateConfigWithProjectsRootPath(customPath);
            var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);

            Assert.Equal(projectId, sut.ActiveProjectId);
            Assert.True(sut.IsMultiProjectMode);
            Assert.Equal(Path.Combine(customPath, projectId), sut.ProjectBasePath);
            Assert.Equal(Path.Combine(customPath, projectId, "config"), sut.ConfigPath);
            Assert.Equal(Path.Combine(customPath, projectId, "models"), sut.ModelsPath);
            Assert.Equal(Path.Combine(customPath, projectId, "data"), sut.DataPath);
            Assert.Equal(Path.Combine(customPath, projectId, "backups"), sut.BackupsPath);
            Assert.Equal(Path.Combine(customPath, projectId, "data", "project.db"), sut.DatabasePath);
            Assert.Equal(Path.Combine(customPath, projectId, "docs"), sut.DocsPath);
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }

    [Fact]
    public void ProjectsRootPath_Custom_ProjectExistsChecksSharedFolder()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(customPath, "shared-proj", "config"));

        try
        {
            var config = CreateConfigWithProjectsRootPath(customPath);
            var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);

            Assert.True(sut.ProjectExists("shared-proj"));
            Assert.False(sut.ProjectExists("not-in-shared"));
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }

    [Fact]
    public void ProjectsRootPath_Custom_GetAvailableProjectsUsesSharedFolder()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(customPath, "proj-alpha", "config"));
        Directory.CreateDirectory(Path.Combine(customPath, "proj-beta", "config"));

        try
        {
            var config = CreateConfigWithProjectsRootPath(customPath);
            var sut = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);

            var projects = sut.GetAvailableProjects().ToList();

            Assert.Contains(projects, p => p.Id == "proj-alpha");
            Assert.Contains(projects, p => p.Id == "proj-beta");
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }

    [Fact]
    public void RequestProjectContext_InheritsCustomProjectsRootPath()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        var projectId = "req-test-proj";
        Directory.CreateDirectory(Path.Combine(customPath, projectId, "config"));
        Directory.CreateDirectory(Path.Combine(customPath, projectId, "models"));
        Directory.CreateDirectory(Path.Combine(customPath, projectId, "data"));
        Directory.CreateDirectory(Path.Combine(customPath, projectId, "backups"));

        try
        {
            WriteActiveProject(projectId);
            var config = CreateConfigWithProjectsRootPath(customPath);
            var globalContext = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);
            var requestContext = new RequestProjectContextService(
                globalContext, CreateMockEnvironment(), new Mock<ILogger<RequestProjectContextService>>().Object);

            Assert.Equal(projectId, requestContext.ProjectId);
            Assert.Equal(Path.Combine(customPath, projectId), requestContext.ProjectBasePath);
            Assert.Equal(Path.Combine(customPath, projectId, "backups"), requestContext.BackupsPath);
            Assert.Equal(Path.Combine(customPath, projectId, "config"), requestContext.ConfigPath);
            Assert.Equal(Path.Combine(customPath, projectId, "models"), requestContext.ModelsPath);
            Assert.Equal(Path.Combine(customPath, projectId, "data"), requestContext.DataPath);
            Assert.Equal(Path.Combine(customPath, projectId, "docs"), requestContext.DocsPath);
            Assert.Equal(Path.Combine(customPath, projectId, "logs"), requestContext.LogsPath);
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }

    [Fact]
    public void RequestProjectContext_SetProject_UsesCustomRoot()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"shared_projects_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(customPath, "proj-x", "config"));
        Directory.CreateDirectory(Path.Combine(customPath, "proj-x", "data"));
        Directory.CreateDirectory(Path.Combine(customPath, "proj-x", "backups"));
        Directory.CreateDirectory(Path.Combine(customPath, "proj-x", "models"));

        try
        {
            var config = CreateConfigWithProjectsRootPath(customPath);
            var globalContext = new ProjectContextService(CreateMockEnvironment(), _loggerMock.Object, _serviceProviderMock.Object, config);
            var requestContext = new RequestProjectContextService(
                globalContext, CreateMockEnvironment(), new Mock<ILogger<RequestProjectContextService>>().Object);

            // Switch project via header simulation
            requestContext.SetProject("proj-x");

            Assert.Equal("proj-x", requestContext.ProjectId);
            Assert.Equal(Path.Combine(customPath, "proj-x"), requestContext.ProjectBasePath);
            Assert.Equal(Path.Combine(customPath, "proj-x", "backups"), requestContext.BackupsPath);
        }
        finally
        {
            try { Directory.Delete(customPath, true); } catch { }
        }
    }
}
