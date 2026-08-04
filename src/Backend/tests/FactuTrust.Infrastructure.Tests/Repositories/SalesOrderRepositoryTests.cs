using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class SalesOrderRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly SalesOrderRepository _repository;

    public SalesOrderRepositoryTests()
    {
        _databaseName = $"TestDb_SalesOrders_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new SalesOrderRepository(_contextFactory);
    }

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

    [Fact]
    public async Task AddAsync_WithMultipleLinesSharingSameCategory_ShouldNotThrowTrackingException()
    {
        // Arrange: create a shared product category in its own context
        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Cat. SO Partagée", "Catégorie de test commande");
            Assert.True(categoryResult.IsSuccess, categoryResult.Error?.Description);
            category = categoryResult.Value;
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync();
        }

        // Persist two products linked to the shared category
        Product product1Persisted;
        Product product2Persisted;
        using (var context = _contextFactory.CreateContext())
        {
            var p1 = Product.Create(
                code: "SO001",
                name: "Produit SO 1",
                type: ProductType.Service,
                unitPrice: Money.Create(100m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p1.IsSuccess, p1.Error?.Description);
            product1Persisted = p1.Value;

            var p2 = Product.Create(
                code: "SO002",
                name: "Produit SO 2",
                type: ProductType.Service,
                unitPrice: Money.Create(150m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p2.IsSuccess, p2.Error?.Description);
            product2Persisted = p2.Value;

            context.Products.AddRange(product1Persisted, product2Persisted);
            await context.SaveChangesAsync();
        }

        // Reload each product in a SEPARATE context with .Include(p => p.Category) — this
        // produces TWO distinct runtime instances of the same ProductCategory, exactly the
        // scenario reproduced by ProductRepository.GetByIdAsync inside CreateSalesOrderCommand.
        Product reloadedProduct1;
        Product reloadedProduct2;
        using (var context = _contextFactory.CreateContext())
        {
            reloadedProduct1 = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == product1Persisted.Id);
        }
        using (var context = _contextFactory.CreateContext())
        {
            reloadedProduct2 = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == product2Persisted.Id);
        }

        Assert.NotSame(reloadedProduct1.Category, reloadedProduct2.Category);
        Assert.Equal(reloadedProduct1.Category!.Id, reloadedProduct2.Category!.Id);

        var client = await CreateClientAsync(
            name: "Client SO Tracking",
            email: "so-tracking@test.com",
            nifValue: "1234567/A/B/C/800");

        var orderNumber = SalesOrderNumber.Create("CDE", 2026, 1);
        var orderResult = SalesOrder.Create(
            number: orderNumber,
            client: client,
            orderDate: DateTime.UtcNow.Date);
        Assert.True(orderResult.IsSuccess, orderResult.Error?.Description);
        var order = orderResult.Value;

        Assert.True(order.AddLine(reloadedProduct1, 2).IsSuccess);
        Assert.True(order.AddLine(reloadedProduct2, 3).IsSuccess);

        // Act: before the fix this threw InvalidOperationException on the 2nd Product.Attach
        var saved = await _repository.AddAsync(order);

        // Assert
        Assert.NotEqual(Guid.Empty, saved.Id);

        using var verify = _contextFactory.CreateContext();
        var reloaded = await verify.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == saved.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Lines.Count);
    }

    [Fact]
    public async Task AddAsync_WithSameProductOnMultipleLines_ShouldNotThrowTrackingException()
    {
        // Arrange
        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Cat. SO Dup", "Catégorie produit dupliqué");
            Assert.True(categoryResult.IsSuccess, categoryResult.Error?.Description);
            category = categoryResult.Value;
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync();
        }

        Product productPersisted;
        using (var context = _contextFactory.CreateContext())
        {
            var productResult = Product.Create(
                code: "SODUP01",
                name: "Produit SO Dupliqué",
                type: ProductType.Service,
                unitPrice: Money.Create(200m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(productResult.IsSuccess, productResult.Error?.Description);
            productPersisted = productResult.Value;
            context.Products.Add(productPersisted);
            await context.SaveChangesAsync();
        }

        Product reloadedProduct1;
        Product reloadedProduct2;
        using (var context = _contextFactory.CreateContext())
        {
            reloadedProduct1 = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == productPersisted.Id);
        }
        using (var context = _contextFactory.CreateContext())
        {
            reloadedProduct2 = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == productPersisted.Id);
        }

        Assert.NotSame(reloadedProduct1, reloadedProduct2);
        Assert.Equal(reloadedProduct1.Id, reloadedProduct2.Id);

        var client = await CreateClientAsync(
            name: "Client SO Dup",
            email: "so-dup@test.com",
            nifValue: "1234567/A/B/C/801");

        var orderNumber = SalesOrderNumber.Create("CDE", 2026, 2);
        var orderResult = SalesOrder.Create(
            number: orderNumber,
            client: client,
            orderDate: DateTime.UtcNow.Date);
        Assert.True(orderResult.IsSuccess, orderResult.Error?.Description);
        var order = orderResult.Value;

        Assert.True(order.AddLine(reloadedProduct1, 1).IsSuccess);
        Assert.True(order.AddLine(reloadedProduct2, 2).IsSuccess);

        // Act
        var saved = await _repository.AddAsync(order);

        // Assert
        Assert.NotEqual(Guid.Empty, saved.Id);

        using var verify = _contextFactory.CreateContext();
        var reloaded = await verify.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == saved.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Lines.Count);
        Assert.Equal(productPersisted.Id, reloaded.Lines.First().ProductId);
        Assert.Equal(productPersisted.Id, reloaded.Lines.Last().ProductId);
    }

    [Fact]
    public async Task AddAsync_WithClientFromDifferentContext_ShouldNotThrowTrackingException()
    {
        // Arrange: persist client, then reload in a separate context (handler pattern)
        var persistedClient = await CreateClientAsync(
            name: "Client SO CrossCtx",
            email: "so-cross@test.com",
            nifValue: "1234567/A/B/C/802");

        Client reloadedClient;
        using (var context = _contextFactory.CreateContext())
        {
            reloadedClient = await context.Clients.FirstAsync(c => c.Id == persistedClient.Id);
        }

        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Cat. SO Client", "Catégorie test client");
            Assert.True(categoryResult.IsSuccess, categoryResult.Error?.Description);
            category = categoryResult.Value;
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync();
        }

        Product reloadedProduct;
        using (var context = _contextFactory.CreateContext())
        {
            var productResult = Product.Create(
                code: "SOCLI01",
                name: "Produit SO Client",
                type: ProductType.Service,
                unitPrice: Money.Create(50m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(productResult.IsSuccess, productResult.Error?.Description);
            context.Products.Add(productResult.Value);
            await context.SaveChangesAsync();

            reloadedProduct = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Id == productResult.Value.Id);
        }

        var orderNumber = SalesOrderNumber.Create("CDE", 2026, 3);
        var orderResult = SalesOrder.Create(
            number: orderNumber,
            client: reloadedClient,
            orderDate: DateTime.UtcNow.Date);
        Assert.True(orderResult.IsSuccess, orderResult.Error?.Description);
        var order = orderResult.Value;
        Assert.True(order.AddLine(reloadedProduct, 1).IsSuccess);

        // Act
        var saved = await _repository.AddAsync(order);

        // Assert
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(reloadedClient.Id, saved.ClientId);
    }

    private async Task<Client> CreateClientAsync(string name, string email, string nifValue)
    {
        using var context = _contextFactory.CreateContext();

        var addressResult = Address.Create("123 Rue Test", "Tunis", "1000", "Tunisie");
        Assert.True(addressResult.IsSuccess, addressResult.Error?.Description);

        var emailResult = Email.Create(email);
        Assert.True(emailResult.IsSuccess, emailResult.Error?.Description);

        var nifResult = NIF.Create(nifValue);
        Assert.True(nifResult.IsSuccess, nifResult.Error?.Description);

        var clientResult = Client.Create(
            name: name,
            type: ClientType.Business,
            address: addressResult.Value,
            email: emailResult.Value,
            nif: nifResult.Value);

        Assert.True(clientResult.IsSuccess, clientResult.Error?.Description);

        context.Clients.Add(clientResult.Value);
        await context.SaveChangesAsync();

        return clientResult.Value;
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
