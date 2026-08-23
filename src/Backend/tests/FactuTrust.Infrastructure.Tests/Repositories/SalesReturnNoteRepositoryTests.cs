using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class SalesReturnNoteRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly SalesReturnNoteRepository _returnNotes;
    private readonly DeliveryNoteRepository _deliveryNotes;

    public SalesReturnNoteRepositoryTests()
    {
        _databaseName = $"TestDb_SalesReturnNotes_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _returnNotes = new SalesReturnNoteRepository(_contextFactory);
        _deliveryNotes = new DeliveryNoteRepository(_contextFactory);
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

    [Fact]
    public async Task AddAsync_WithFullyLoadedDeliveryNoteGraph_DoesNotThrow_AndDoesNotInsertParents()
    {
        var seed = await SeedDeliveredNoteWithTwoProductsAsync();
        var loadedBl = await _deliveryNotes.GetByIdWithDetailsAsync(seed.DeliveryNoteId);
        Assert.NotNull(loadedBl);
        Assert.Equal(2, loadedBl!.Lines.Count);
        Assert.All(loadedBl.Lines, l => Assert.NotNull(l.Product));

        var note = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 1).Value,
            loadedBl,
            new DateTime(2026, 8, 16),
            "Marchandise endommagée").Value;
        var firstLine = loadedBl.Lines.OrderBy(l => l.LineNumber).First();
        Assert.True(note.AddLine(firstLine, 1m).IsSuccess);

        var exception = await Record.ExceptionAsync(() => _returnNotes.AddAsync(note));
        Assert.Null(exception);

        using var verify = _contextFactory.CreateContext();
        Assert.Equal(1, await verify.SalesReturnNotes.CountAsync());
        Assert.Equal(1, await verify.SalesReturnNoteLines.CountAsync());
        Assert.Equal(1, await verify.DeliveryNotes.CountAsync());
        Assert.Equal(1, await verify.Clients.CountAsync());
        Assert.Equal(2, await verify.Products.CountAsync());

        var persisted = await verify.SalesReturnNotes
            .Include(n => n.Lines)
            .FirstAsync();
        Assert.Equal(seed.DeliveryNoteId, persisted.DeliveryNoteId);
        Assert.Equal(seed.ClientId, persisted.ClientId);
        Assert.Equal(SalesReturnNoteStatus.Draft, persisted.Status);
        var persistedLine = Assert.Single(persisted.Lines);
        Assert.Equal(firstLine.Id, persistedLine.DeliveryNoteLineId);
        Assert.Equal(1m, persistedLine.ReturnedQuantity);
    }

    [Fact]
    public async Task UpdateAsync_AfterConfirm_DoesNotOverwriteDeliveryNoteReturnedQuantity()
    {
        var seed = await SeedDeliveredNoteWithTwoProductsAsync();
        var loadedBl = await _deliveryNotes.GetByIdWithDetailsAsync(seed.DeliveryNoteId);
        Assert.NotNull(loadedBl);
        var sourceLine = loadedBl!.Lines.OrderBy(l => l.LineNumber).First();

        var note = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 2).Value,
            loadedBl,
            new DateTime(2026, 8, 16),
            "Erreur de commande").Value;
        Assert.True(note.AddLine(sourceLine, 1m).IsSuccess);
        await _returnNotes.AddAsync(note);

        var staleNote = await _returnNotes.GetByIdWithDetailsAsync(note.Id);
        Assert.NotNull(staleNote);
        Assert.NotNull(staleNote!.DeliveryNote);
        Assert.Equal(0m, staleNote.DeliveryNote.Lines.First(l => l.Id == sourceLine.Id).ReturnedQuantity);

        var applyResult = await _deliveryNotes.ApplyReturnsAsync(
            seed.DeliveryNoteId,
            new List<(Guid LineId, decimal Quantity)> { (sourceLine.Id, 1m) },
            "test-user");
        Assert.True(applyResult.IsSuccess, applyResult.Error?.Description);

        Assert.True(staleNote.Confirm().IsSuccess);
        var persistConfirm = await _returnNotes.ConfirmPersistedAsync(staleNote.Id, "test-user");
        Assert.True(persistConfirm.IsSuccess, persistConfirm.Error?.Description);

        var updateException = await Record.ExceptionAsync(() => _returnNotes.UpdateAsync(staleNote));
        Assert.Null(updateException);

        using var verify = _contextFactory.CreateContext();
        var blLine = await verify.Set<DeliveryNoteLine>().FirstAsync(l => l.Id == sourceLine.Id);
        Assert.Equal(1m, blLine.ReturnedQuantity);

        var confirmed = await verify.SalesReturnNotes.FirstAsync(n => n.Id == note.Id);
        Assert.Equal(SalesReturnNoteStatus.Confirmed, confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
    }

    private async Task<SeededDelivery> SeedDeliveredNoteWithTwoProductsAsync()
    {
        ProductCategory category;
        Product product1;
        Product product2;
        Client client;
        Warehouse warehouse;

        using (var context = _contextFactory.CreateContext())
        {
            category = ProductCategory.Create("CAT-BRT", "Catégorie BRT").Value;
            context.ProductCategories.Add(category);

            warehouse = Warehouse.Create("WH-BRT", "Dépôt Monastir", isDefault: true).Value;
            context.Warehouses.Add(warehouse);

            var address = Address.Create("1 rue Test", "Tunis", "1000").Value;
            var email = Email.Create("brt-repo@test.com").Value;
            client = Client.Create("Client BRT repo", ClientType.Individual, address, email).Value;
            context.Clients.Add(client);

            product1 = Product.Create(
                "CUIS001", "cuisinière montblanc", ProductType.Product,
                Money.Create(100m), VatRate.Standard, category.Id, isStockManaged: true).Value;
            product2 = Product.Create(
                "BAIGN001", "Baignoire conf", ProductType.Product,
                Money.Create(200m), VatRate.Standard, category.Id, isStockManaged: true).Value;
            context.Products.AddRange(product1, product2);
            await context.SaveChangesAsync();
        }

        Product reloaded1;
        Product reloaded2;
        using (var context = _contextFactory.CreateContext())
        {
            reloaded1 = await context.Products.Include(p => p.Category).FirstAsync(p => p.Id == product1.Id);
        }
        using (var context = _contextFactory.CreateContext())
        {
            reloaded2 = await context.Products.Include(p => p.Category).FirstAsync(p => p.Id == product2.Id);
        }

        var bl = DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 80).Value,
            client,
            new DateTime(2026, 8, 1),
            "12 avenue Habib Bourguiba",
            warehouseId: warehouse.Id).Value;
        Assert.True(bl.AddLine(reloaded1, 1m).IsSuccess);
        Assert.True(bl.AddLine(reloaded2, 1m).IsSuccess);
        Assert.True(bl.Confirm().IsSuccess);
        foreach (var line in bl.Lines)
            Assert.True(line.RecordDelivery(1m).IsSuccess);
        Assert.True(bl.RecordDelivery(new DateTime(2026, 8, 2), "Réceptionnaire").IsSuccess);

        await _deliveryNotes.AddAsync(bl);

        return new SeededDelivery(bl.Id, client.Id, warehouse.Id);
    }

    private sealed record SeededDelivery(Guid DeliveryNoteId, Guid ClientId, Guid WarehouseId);

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
