using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using Xunit;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests for BackupCertificateService — HMAC signing/verification of backups.
/// Uses temp directories and in-memory IConfiguration.
/// </summary>
public class BackupCertificateServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<ILogger<BackupCertificateService>> _loggerMock = new();

    public BackupCertificateServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"bcs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "backups"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "Projects", "test-project", "backups"));
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

    private IConfiguration CreateConfiguration(string? signingSecret = null)
    {
        var config = new Dictionary<string, string?>();
        if (signingSecret != null)
            config["Backup:SigningSecret"] = signingSecret;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    /// <summary>Create a sample manifest for testing</summary>
    private static BackupManifest CreateSampleManifest(string projectId = "default", string backupId = "backup-001")
    {
        return new BackupManifest
        {
            ManifestVersion = "1.0",
            BackupInfo = new BackupInfo
            {
                Id = backupId,
                ProjectId = projectId,
                Name = "Test Backup",
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0),
                CreatedBy = "test-user"
            },
            Files = new List<BackupFileEntry>
            {
                new() { RelativePath = "config/ProjectConfig.xlsm", Hash = "abc123def456", SizeBytes = 1024, ModifiedAt = DateTime.Now },
                new() { RelativePath = "data/project.db", Hash = "789xyz000111", SizeBytes = 2048, ModifiedAt = DateTime.Now }
            },
            GeneratedAt = DateTime.Now
        };
    }

    private BackupCertificateService CreateService(string? signingSecret = "test-secret-key-12345")
    {
        var projectContextMock = new Mock<IProjectContextService>();
        projectContextMock.Setup(p => p.ProjectsRootPath).Returns(Path.Combine(_tempRoot, "Projects"));
        
        return new BackupCertificateService(
            _loggerMock.Object,
            CreateMockEnvironment(),
            CreateConfiguration(signingSecret),
            projectContextMock.Object);
    }

    // ===== Sign + Verify Round-Trip =====

    [Fact]
    public async Task SignAndVerify_RoundTrip_Succeeds()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        var certificate = await sut.SignBackupAsync("default", "backup-001", manifest);
        var isValid = await sut.VerifyCertificateAsync(certificate, manifest);

        Assert.True(isValid);
    }

    [Fact]
    public async Task SignBackup_ReturnsCertificateWithAllFields()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        var cert = await sut.SignBackupAsync("default", "backup-001", manifest);

        Assert.Equal("1.0", cert.CertificateVersion);
        Assert.Equal("backup-001", cert.BackupId);
        Assert.NotEmpty(cert.ManifestHash);
        Assert.NotEmpty(cert.ContentHash);
        Assert.NotEmpty(cert.Signature);
        Assert.NotEmpty(cert.SignedBy);
        Assert.Equal(1, cert.SequenceNumber);
        Assert.Equal("EU-CRA-2024", cert.Compliance.Standard);
        Assert.Equal("SHA256", cert.Compliance.HashAlgorithm);
    }

    [Fact]
    public async Task SignBackup_FirstInChain_HasGenesisAndSequence1()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        var cert = await sut.SignBackupAsync("default", "backup-001", manifest);

        Assert.Null(cert.PreviousCertificateHash);
        Assert.Equal(1, cert.SequenceNumber);
    }

    [Fact]
    public async Task SignBackup_ChainSequenceIncrements()
    {
        var sut = CreateService();
        var manifest1 = CreateSampleManifest(backupId: "backup-001");
        var manifest2 = CreateSampleManifest(backupId: "backup-002");

        var cert1 = await sut.SignBackupAsync("default", "backup-001", manifest1);
        var cert2 = await sut.SignBackupAsync("default", "backup-002", manifest2);

        Assert.Equal(1, cert1.SequenceNumber);
        Assert.Equal(2, cert2.SequenceNumber);
        Assert.NotNull(cert2.PreviousCertificateHash);
    }

    // ===== Verification Failures =====

    [Fact]
    public async Task Verify_FailsWhenManifestTampered()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        var cert = await sut.SignBackupAsync("default", "backup-001", manifest);

        // Tamper with the manifest
        manifest.Files[0].Hash = "tampered_hash";

        var isValid = await sut.VerifyCertificateAsync(cert, manifest);
        Assert.False(isValid);
    }

    [Fact]
    public async Task Verify_FailsWhenSignatureTampered()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        var cert = await sut.SignBackupAsync("default", "backup-001", manifest);
        cert.Signature = "tampered_signature";

        var isValid = await sut.VerifyCertificateAsync(cert, manifest);
        Assert.False(isValid);
    }

    [Fact]
    public async Task Verify_FailsWithDifferentSigningSecret()
    {
        var sut1 = CreateService(signingSecret: "secret-A");
        var manifest = CreateSampleManifest();

        var cert = await sut1.SignBackupAsync("default", "backup-001", manifest);

        // Create a new service with a different secret
        var sut2 = CreateService(signingSecret: "secret-B");
        var isValid = await sut2.VerifyCertificateAsync(cert, manifest);

        Assert.False(isValid);
    }

    // ===== Chain Info Persistence =====

    [Fact]
    public async Task GetCurrentSequenceNumber_ReturnsZeroInitially()
    {
        var sut = CreateService();
        var seq = await sut.GetCurrentSequenceNumberAsync("default");
        Assert.Equal(0, seq);
    }

    [Fact]
    public async Task GetLastCertificateHash_ReturnsNullInitially()
    {
        var sut = CreateService();
        var hash = await sut.GetLastCertificateHashAsync("default");
        Assert.Null(hash);
    }

    [Fact]
    public async Task GetCurrentSequenceNumber_IncrementsAfterSign()
    {
        var sut = CreateService();
        var manifest = CreateSampleManifest();

        await sut.SignBackupAsync("default", "backup-001", manifest);
        var seq = await sut.GetCurrentSequenceNumberAsync("default");

        Assert.Equal(1, seq);
    }

    // ===== Multi-Project Isolation =====

    [Fact]
    public async Task SignBackup_ProjectsHaveIndependentChains()
    {
        var sut = CreateService();

        var manifestA = CreateSampleManifest(projectId: "default");
        var manifestB = CreateSampleManifest(projectId: "test-project");

        var certA = await sut.SignBackupAsync("default", "backup-a1", manifestA);
        var certB = await sut.SignBackupAsync("test-project", "backup-b1", manifestB);

        // Each project starts its own chain at sequence 1
        Assert.Equal(1, certA.SequenceNumber);
        Assert.Equal(1, certB.SequenceNumber);

        // Sequence numbers are independent
        var seqA = await sut.GetCurrentSequenceNumberAsync("default");
        var seqB = await sut.GetCurrentSequenceNumberAsync("test-project");
        Assert.Equal(1, seqA);
        Assert.Equal(1, seqB);
    }

    // ===== Auto-Generated Secret =====

    [Fact]
    public async Task SignAndVerify_WorksWithAutoGeneratedSecret()
    {
        // No explicit signing secret — service should auto-generate one
        var sut = CreateService(signingSecret: null);
        var manifest = CreateSampleManifest();

        var cert = await sut.SignBackupAsync("default", "backup-001", manifest);
        var isValid = await sut.VerifyCertificateAsync(cert, manifest);

        Assert.True(isValid);
        Assert.NotEmpty(cert.Signature);
    }
}
