using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.Services.AI.Export.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI.Export.Storage;

/// <summary>
/// Verifies the on-disk persistence + signed-token behaviour of <see cref="FilesystemExportStorageService"/>.
/// Each test runs in an isolated temp directory and cleans up afterwards.
/// </summary>
public sealed class FilesystemExportStorageServiceTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly string _tempRoot;
    private readonly FilesystemExportStorageService _service;

    public FilesystemExportStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "factutrust-ai-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var tenantCtx = new Mock<ITenantContext>();
        tenantCtx.SetupGet(t => t.TenantId).Returns(TenantId);

        var options = Options.Create(new ExportStorageOptions
        {
            RootDirectory = _tempRoot,
            RetentionOverride = TimeSpan.FromMinutes(5),
            DownloadLinkTtlOverride = TimeSpan.FromMinutes(5)
        });

        _service = new FilesystemExportStorageService(
            tenantCtx.Object,
            new EphemeralDataProtectionProvider(),
            options,
            NullLogger<FilesystemExportStorageService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task StoreAsync_persists_file_and_returns_signed_token()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var stored = await _service.StoreAsync(TenantId, UserId, "pptx", "deck.pptx", bytes);

        Assert.NotEqual(Guid.Empty, stored.ExportId);
        Assert.False(string.IsNullOrWhiteSpace(stored.Token));
        Assert.True(File.Exists(stored.StoragePath));
        Assert.Equal(bytes.Length, stored.SizeBytes);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ResolveAsync_returns_content_when_token_is_valid()
    {
        var bytes = new byte[] { 9, 8, 7, 6 };
        var stored = await _service.StoreAsync(TenantId, UserId, "pptx", "deck.pptx", bytes);

        var resolved = await _service.ResolveAsync(stored.ExportId, stored.Token);

        Assert.NotNull(resolved);
        Assert.Equal(stored.ExportId, resolved!.ExportId);
        Assert.Equal(bytes, resolved.Content);
        Assert.Equal("deck.pptx", resolved.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.presentationml.presentation", resolved.ContentType);
    }

    [Fact]
    public async Task ResolveAsync_rejects_tampered_token()
    {
        var stored = await _service.StoreAsync(TenantId, UserId, "pptx", "deck.pptx", new byte[] { 1 });

        var tampered = stored.Token.Substring(0, stored.Token.Length - 5) + "AAAAA";
        var resolved = await _service.ResolveAsync(stored.ExportId, tampered);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_rejects_token_for_different_export()
    {
        var stored1 = await _service.StoreAsync(TenantId, UserId, "pptx", "deck1.pptx", new byte[] { 1 });
        var stored2 = await _service.StoreAsync(TenantId, UserId, "pptx", "deck2.pptx", new byte[] { 2 });

        // Token of export 2 used against export id 1 must fail.
        var resolved = await _service.ResolveAsync(stored1.ExportId, stored2.Token);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task CleanupExpiredAsync_removes_files_past_retention()
    {
        var bytes = new byte[] { 1, 2 };
        var stored = await _service.StoreAsync(TenantId, UserId, "pptx", "deck.pptx", bytes);

        // Force the manifest to have expired in the past — emulate retention exhaustion.
        var tenantDir = Path.Combine(_tempRoot, TenantId.ToString("N"));
        var manifestPath = Path.Combine(tenantDir, $"{stored.ExportId:N}.manifest.json");
        var manifest = await File.ReadAllTextAsync(manifestPath);
        manifest = manifest.Replace(
            "\"expiresAt\":",
            "\"expiresAt\":\"" + DateTime.UtcNow.AddHours(-1).ToString("O") + "\",\"_replacedAt\":");
        await File.WriteAllTextAsync(manifestPath, manifest);

        var deleted = await _service.CleanupExpiredAsync();

        Assert.True(deleted >= 1);
        Assert.False(File.Exists(stored.StoragePath));
    }

    /// <summary>
    /// Provides an in-memory ephemeral DataProtection chain so the test doesn't need an EF key
    /// repository. Wires <see cref="EphemeralDataProtectionProvider"/> from Microsoft.AspNetCore.DataProtection.
    /// </summary>
    private sealed class EphemeralDataProtectionProvider : IDataProtectionProvider
    {
        private readonly Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider _inner =
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();

        public IDataProtector CreateProtector(string purpose) => _inner.CreateProtector(purpose);
    }
}
