using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// Régression : la facture fournisseur porte des navigations vers d'autres agrégats
/// (Supplier, PurchaseOrder, SourcePurchaseReceipt, Warehouse et leurs Product), chargés
/// par d'AUTRES DbContext. AddAsync ne doit jamais tenter de les réinsérer.
/// </summary>
public sealed class SupplierInvoiceRepositoryTests : IDisposable
{
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly SupplierInvoiceRepository _repository;

    public SupplierInvoiceRepositoryTests()
    {
        _contextFactory = new TestTenantDbContextFactory($"TestDb_SupplierInvoices_{Guid.NewGuid()}");
        _repository = new SupplierInvoiceRepository(_contextFactory);
    }

    [Fact]
    public async Task AddAsync_FromPurchaseReceipt_PersistsInvoiceWithoutReinsertingReferencedAggregates()
    {
        var seeded = await SeedAsync();

        // Le handler charge le BR et le BC via deux repositories distincts, donc deux
        // DbContext distincts : les instances de Product portent le même Id mais sont
        // des objets différents. C'est exactement ce qui faisait échouer AddAsync.
        var receipt = await LoadReceiptAsync(seeded.ReceiptId);
        var po = await LoadPurchaseOrderAsync(seeded.PurchaseOrderId);

        Assert.NotSame(
            receipt.Lines.First().Product,
            po.Lines.First().Product);

        var receiptLine = receipt.Lines.First();
        var invoice = SupplierInvoice.CreateFromPurchaseReceipt(
            receipt,
            po,
            "FS-2026-000001",
            new DateTime(2026, 4, 5),
            [(receiptLine.Id, receiptLine.ReceivedNotInvoicedQuantity)]).Value;

        await _repository.AddAsync(invoice);

        await using var assertContext = _contextFactory.CreateContext();

        // Aucun agrégat référencé n'a été dupliqué.
        Assert.Equal(1, await assertContext.Products.CountAsync());
        Assert.Equal(1, await assertContext.PurchaseReceipts.CountAsync());
        Assert.Equal(1, await assertContext.PurchaseOrders.CountAsync());
        Assert.Equal(1, await assertContext.Suppliers.CountAsync());
        Assert.Equal(1, await assertContext.Warehouses.CountAsync());

        // La facture et sa ligne sont bien persistées, valeurs monétaires comprises.
        var persisted = await assertContext.SupplierInvoices
            .Include(si => si.Lines)
            .SingleAsync();

        Assert.Equal("FS-2026-000001", persisted.InvoiceNumber);
        Assert.Equal(seeded.SupplierId, persisted.SupplierId);
        Assert.Equal(seeded.PurchaseOrderId, persisted.PurchaseOrderId);
        Assert.Equal(seeded.ReceiptId, persisted.SourcePurchaseReceiptId);
        Assert.Equal(seeded.WarehouseId, persisted.WarehouseId);

        var persistedLine = Assert.Single(persisted.Lines);
        Assert.Equal(seeded.ProductId, persistedLine.ProductId);
        Assert.Equal(4m, persistedLine.Quantity);
        Assert.Equal(400m, persistedLine.SubTotal.Amount);
        Assert.Equal(400m, persisted.SubTotal.Amount);
        Assert.Equal(invoice.TotalAmount.Amount, persisted.TotalAmount.Amount);
        Assert.True(persisted.TotalAmount.Amount > persisted.SubTotal.Amount, "La TVA doit être persistée");
    }

    [Fact]
    public async Task AddAsync_FromPurchaseOrder_PersistsInvoiceWithoutReinsertingReferencedAggregates()
    {
        var seeded = await SeedAsync();

        var po = await LoadPurchaseOrderAsync(seeded.PurchaseOrderId);
        var poLine = po.Lines.First();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            "FS-2026-000002",
            new DateTime(2026, 4, 5),
            [(poLine.Id, poLine.ReceivedNotInvoicedQuantity)]).Value;

        await _repository.AddAsync(invoice);

        await using var assertContext = _contextFactory.CreateContext();

        Assert.Equal(1, await assertContext.Products.CountAsync());
        Assert.Equal(1, await assertContext.PurchaseOrders.CountAsync());
        Assert.Equal(1, await assertContext.Suppliers.CountAsync());

        var persisted = await assertContext.SupplierInvoices
            .Include(si => si.Lines)
            .SingleAsync();

        Assert.Equal("FS-2026-000002", persisted.InvoiceNumber);
        Assert.Null(persisted.SourcePurchaseReceiptId);
        Assert.Single(persisted.Lines);
    }

    [Fact]
    public async Task AddAsync_FromStandalonePurchaseReceipt_PersistsWithNullPurchaseOrderId()
    {
        var seeded = await SeedStandaloneAsync();

        var receipt = await LoadReceiptAsync(seeded.ReceiptId);
        var receiptLine = receipt.Lines.First();
        var invoice = SupplierInvoice.CreateFromPurchaseReceipt(
            receipt,
            purchaseOrder: null,
            "FS-2026-000003",
            new DateTime(2026, 4, 5),
            [(receiptLine.Id, receiptLine.ReceivedNotInvoicedQuantity)]).Value;

        await _repository.AddAsync(invoice);

        await using var assertContext = _contextFactory.CreateContext();
        var persisted = await assertContext.SupplierInvoices
            .Include(si => si.Lines)
            .SingleAsync(si => si.InvoiceNumber == "FS-2026-000003");

        Assert.Null(persisted.PurchaseOrderId);
        Assert.Equal(seeded.ReceiptId, persisted.SourcePurchaseReceiptId);
        Assert.Single(persisted.Lines);
    }

    private async Task<SeededScenario> SeedAsync()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        // Chaque propriété "owned" doit recevoir SA propre instance de Money : partager
        // le même objet entre deux navigations owned casse le suivi EF à la persistance.
        var product = Product.Create(
            "ART-BR", "Papier", ProductType.Product, Money.Create(100m), VatRate.Standard,
            category.Id, purchasePrice: Money.Create(100m)).Value;

        var po = PurchaseOrder.Create(
            PurchaseOrderNumber.Create("BC", 2026, 42), supplier, new DateTime(2026, 4, 1)).Value;
        Assert.True(po.AddLine(product, 10m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var poLineId = po.Lines.First().Id;
        Assert.True(po.ReceiveGoods([(poLineId, 4m)]).IsSuccess);

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 3),
            supplier,
            warehouse,
            new DateTime(2026, 4, 2),
            purchaseOrderId: po.Id).Value;
        Assert.True(receipt.AddLine(product, 4m, Money.Create(100m), orderedQuantity: 10m, purchaseOrderLineId: poLineId).IsSuccess);
        Assert.True(receipt.MarkValidated().IsSuccess);

        await using var context = _contextFactory.CreateContext();
        context.ProductCategories.Add(category);
        context.Products.Add(product);
        context.Suppliers.Add(supplier);
        context.Warehouses.Add(warehouse);
        context.PurchaseOrders.Add(po);
        context.PurchaseReceipts.Add(receipt);
        await context.SaveChangesAsync();

        return new SeededScenario(
            supplier.Id, warehouse.Id, product.Id, po.Id, receipt.Id);
    }

    private async Task<StandaloneSeededScenario> SeedStandaloneAsync()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var product = Product.Create(
            "ART-SA", "Standalone", ProductType.Product, Money.Create(100m), VatRate.Standard,
            category.Id, purchasePrice: Money.Create(100m)).Value;

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 9),
            supplier,
            warehouse,
            new DateTime(2026, 4, 2)).Value;
        Assert.True(receipt.AddLine(product, 2m, Money.Create(100m)).IsSuccess);
        Assert.True(receipt.MarkValidated().IsSuccess);

        await using var context = _contextFactory.CreateContext();
        context.ProductCategories.Add(category);
        context.Products.Add(product);
        context.Suppliers.Add(supplier);
        context.Warehouses.Add(warehouse);
        context.PurchaseReceipts.Add(receipt);
        await context.SaveChangesAsync();

        return new StandaloneSeededScenario(supplier.Id, warehouse.Id, product.Id, receipt.Id);
    }

    private async Task<PurchaseReceipt> LoadReceiptAsync(Guid id)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
            .FirstAsync(r => r.Id == id);
    }

    private async Task<PurchaseOrder> LoadPurchaseOrderAsync(Guid id)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines)
                .ThenInclude(l => l.Product)
            .FirstAsync(po => po.Id == id);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }

    private sealed record SeededScenario(
        Guid SupplierId,
        Guid WarehouseId,
        Guid ProductId,
        Guid PurchaseOrderId,
        Guid ReceiptId);

    private sealed record StandaloneSeededScenario(
        Guid SupplierId,
        Guid WarehouseId,
        Guid ProductId,
        Guid ReceiptId);

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName)
        {
            _databaseName = databaseName;
        }

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            return new TenantDbContext(options);
        }
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
