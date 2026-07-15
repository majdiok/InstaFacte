using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot D : GED des écritures — upload durci (MIME, taille), téléchargement par flux,
/// suppression interdite quand la période de l'écriture est clôturée.
/// </summary>
public sealed class JournalEntryAttachmentServiceTests : IDisposable
{
    private readonly string _dbName = $"AttachDb_{Guid.NewGuid()}";
    private readonly string _storageRoot;
    private readonly TestTenantDbContextFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();

    public JournalEntryAttachmentServiceTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
        _storageRoot = Path.Combine(Path.GetTempPath(), $"ft-attach-tests-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); } catch { /* best effort */ }
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private (Guid EntryId, Guid PeriodId) SeedEntry(bool periodClosed = false)
    {
        var period = AccountingPeriod.Create(2026, 5, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
        if (periodClosed) period.Close("test");
        period.SetAuditInfo("test", false);

        var lines = new[]
        {
            new JournalLineInput("4111", "Client", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("707", "Vente", 0m, 100m, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", period.Id,
            false, "Manual", null, lines).Value;
        entry.SetAuditInfo("test", false);

        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
        return (entry.Id, period.Id);
    }

    private JournalEntryAttachmentService BuildService()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.TenantId).Returns(_tenantId);
        currentUser.SetupGet(u => u.Email).Returns("comptable@test.tn");

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = Options.Create(new AccountingAttachmentsOptions { BasePath = _storageRoot });
        return new JournalEntryAttachmentService(
            _factory, currentUser.Object, audit.Object, options,
            NullLogger<JournalEntryAttachmentService>.Instance);
    }

    private static MemoryStream Pdf(string content = "%PDF-1.4 test") => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Upload_Pdf_PersistsMetadataAndFile()
    {
        var (entryId, _) = SeedEntry();
        var service = BuildService();

        var result = await service.UploadAsync(entryId, "facture fournisseur.pdf", "application/pdf", Pdf());

        Assert.True(result.IsSuccess);
        Assert.Equal("facture fournisseur.pdf", result.Value.FileName);
        Assert.True(result.Value.SizeBytes > 0);
        Assert.Equal("comptable@test.tn", result.Value.UploadedBy);

        var list = await service.ListAsync(entryId);
        var stored = Assert.Single(list.Value);
        var fullPath = Path.Combine(_storageRoot, GetStoragePath(stored.Id));
        Assert.True(File.Exists(fullPath));
        Assert.Contains(_tenantId.ToString(), fullPath);
    }

    private string GetStoragePath(Guid attachmentId)
    {
        using var ctx = _factory.CreateContext();
        return ctx.JournalEntryAttachments.AsNoTracking().Single(a => a.Id == attachmentId).StoragePath;
    }

    [Fact]
    public async Task Upload_DisallowedMime_Fails()
    {
        var (entryId, _) = SeedEntry();

        var result = await BuildService().UploadAsync(entryId, "script.exe", "application/x-msdownload", Pdf());

        Assert.True(result.IsFailure);
        Assert.Contains("non autorisé", result.Error.Description);
    }

    [Fact]
    public async Task Upload_UnknownEntry_Fails()
    {
        var result = await BuildService().UploadAsync(Guid.NewGuid(), "a.pdf", "application/pdf", Pdf());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Download_ReturnsContentAndMetadata()
    {
        var (entryId, _) = SeedEntry();
        var service = BuildService();
        var uploaded = (await service.UploadAsync(entryId, "note.pdf", "application/pdf", Pdf("%PDF contenu"))).Value;

        var result = await service.DownloadAsync(uploaded.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("note.pdf", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
        using var reader = new StreamReader(result.Value.Content);
        Assert.Contains("contenu", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Delete_RemovesMetadataAndFile()
    {
        var (entryId, _) = SeedEntry();
        var service = BuildService();
        var uploaded = (await service.UploadAsync(entryId, "note.pdf", "application/pdf", Pdf())).Value;
        var storagePath = GetStoragePath(uploaded.Id);

        var result = await service.DeleteAsync(uploaded.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty((await service.ListAsync(entryId)).Value);
        Assert.False(File.Exists(Path.Combine(_storageRoot, storagePath)));
    }

    [Fact]
    public async Task Delete_InClosedPeriod_Fails()
    {
        var (entryId, _) = SeedEntry(periodClosed: true);
        var service = BuildService();
        var uploaded = (await service.UploadAsync(entryId, "note.pdf", "application/pdf", Pdf())).Value;

        var result = await service.DeleteAsync(uploaded.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("clôturée", result.Error.Description);
        Assert.Single((await service.ListAsync(entryId)).Value); // toujours présent
    }
}
