using FactuTrust.Infrastructure.Services.Studio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class StudioFileStorageServiceTests : IDisposable
{
    private static readonly Guid Tid = Guid.NewGuid();

    // Real PNG magic bytes (89 50 4E 47 0D 0A 1A 0A) + a couple of harmless trailing bytes: the
    // magic-byte check added alongside the shared UploadValidator now inspects the leading bytes
    // regardless of the declared Content-Type, so tests that exercise a successful PNG save must
    // supply a genuine signature.
    private static readonly byte[] ValidPngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };

    private readonly string _base;
    private readonly StudioFileStorageService _svc;

    public StudioFileStorageServiceTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "ft-studio-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_base);
        _svc = new StudioFileStorageService(
            Options.Create(new StudioFileStorageOptions { BasePath = _base }),
            NullLogger<StudioFileStorageService>.Instance);
    }

    private string ToFullPath(string url) => Path.Combine(_base, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public async Task Saves_png_under_tenant_entity_scoped_path()
    {
        using var ms = new MemoryStream(ValidPngBytes);
        var url = await _svc.SaveAsync(Tid, "contacts", ms, "image/png");

        Assert.StartsWith($"/uploads/tenants/{Tid}/studio/contacts/", url);
        Assert.EndsWith(".png", url);
        Assert.True(File.Exists(ToFullPath(url)));
    }

    [Fact]
    public async Task Rejects_disallowed_content_type()
    {
        using var ms = new MemoryStream(new byte[] { 1 });
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.SaveAsync(Tid, "contacts", ms, "application/x-msdownload"));
    }

    [Fact]
    public async Task Rejects_invalid_entity_key()
    {
        using var ms = new MemoryStream(new byte[] { 1 });
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.SaveAsync(Tid, "Bad Key!", ms, "image/png"));
    }

    [Fact]
    public async Task Rejects_empty_file()
    {
        using var ms = new MemoryStream(Array.Empty<byte>());
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.SaveAsync(Tid, "contacts", ms, "image/png"));
    }

    [Fact]
    public async Task Rejects_content_whose_magic_bytes_do_not_match_declared_type()
    {
        // Declares "image/png" but the bytes are an arbitrary (non-PNG) payload — the declared
        // Content-Type is client-supplied and must not be trusted on its own (CWE-434).
        using var ms = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }); // "MZ..." (PE header)
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.SaveAsync(Tid, "contacts", ms, "image/png"));
    }

    [Fact]
    public async Task Resolve_returns_saved_file_with_content_type()
    {
        using var ms = new MemoryStream(ValidPngBytes);
        var url = await _svc.SaveAsync(Tid, "contacts", ms, "image/png");
        var fileName = url[(url.LastIndexOf('/') + 1)..];

        var file = _svc.Resolve(Tid, "contacts", fileName);

        Assert.NotNull(file);
        Assert.Equal("image/png", file!.ContentType);
        Assert.Equal(Path.GetFullPath(ToFullPath(url)), file.FullPath);
    }

    [Fact]
    public async Task Resolve_is_tenant_and_entity_scoped()
    {
        using var ms = new MemoryStream(ValidPngBytes);
        var url = await _svc.SaveAsync(Tid, "contacts", ms, "image/png");
        var fileName = url[(url.LastIndexOf('/') + 1)..];

        // Same file name under another tenant or another entity must not resolve.
        Assert.Null(_svc.Resolve(Guid.NewGuid(), "contacts", fileName));
        Assert.Null(_svc.Resolve(Tid, "orders", fileName));
    }

    [Fact]
    public void Resolve_rejects_invalid_or_missing_names()
    {
        Assert.Null(_svc.Resolve(Tid, "contacts", "../secret.png"));
        Assert.Null(_svc.Resolve(Tid, "contacts", "notaguid.png"));
        Assert.Null(_svc.Resolve(Tid, "contacts", new string('a', 32) + ".exe"));
        Assert.Null(_svc.Resolve(Tid, "Bad Key!", new string('a', 32) + ".png"));
        // Well-formed name but no file on disk.
        Assert.Null(_svc.Resolve(Tid, "contacts", Guid.NewGuid().ToString("N") + ".png"));
    }

    [Fact]
    public async Task Delete_removes_own_file_but_ignores_foreign_paths()
    {
        using var ms = new MemoryStream(ValidPngBytes);
        var url = await _svc.SaveAsync(Tid, "contacts", ms, "image/png");
        var path = ToFullPath(url);

        // Another tenant's path and a traversal attempt must be no-ops.
        await _svc.DeleteAsync(Tid, $"/uploads/tenants/{Guid.NewGuid()}/studio/x/y.png");
        await _svc.DeleteAsync(Tid, "/../../etc/passwd");
        Assert.True(File.Exists(path));

        await _svc.DeleteAsync(Tid, url);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        try { Directory.Delete(_base, recursive: true); } catch { /* best-effort temp cleanup */ }
    }
}
