using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class QuoteRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly QuoteRepository _repository;

    public QuoteRepositoryTests()
    {
        _databaseName = $"TestDb_Quotes_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new QuoteRepository(_contextFactory);
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
    public async Task AddAsync_WithMultipleLinesSameCategory_ShouldNotThrowTrackingException()
    {
        // Arrange: create a shared product category
        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Services", "Catégorie de test");
            Assert.True(categoryResult.IsSuccess, categoryResult.Error?.Description);
            category = categoryResult.Value;
            context.ProductCategories.Add(category);
            await context.SaveChangesAsync();
        }

        // Create two products in a separate context that share the same category
        Product product1;
        Product product2;
        using (var context = _contextFactory.CreateContext())
        {
            var unitPrice1 = Money.Create(100m, "TND");
            var unitPrice2 = Money.Create(150m, "TND");

            var p1Result = Product.Create(
                code: "PQT001",
                name: "Produit devis 1",
                type: ProductType.Service,
                unitPrice: unitPrice1,
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p1Result.IsSuccess, p1Result.Error?.Description);
            product1 = p1Result.Value;

            var p2Result = Product.Create(
                code: "PQT002",
                name: "Produit devis 2",
                type: ProductType.Service,
                unitPrice: unitPrice2,
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p2Result.IsSuccess, p2Result.Error?.Description);
            product2 = p2Result.Value;

            context.Products.AddRange(product1, product2);
            await context.SaveChangesAsync();
        }

        // Create a client
        Client client;
        using (var context = _contextFactory.CreateContext())
        {
            var address = Address.Create("Rue Test", "Tunis", "1000", "Tunisie").Value;
            var email = Email.Create("client-quote@test.com").Value;
            var nif = NIF.Create("1234567/A/B/C/000").Value;

            var clientResult = Client.Create("Client Devis", ClientType.Business, address, email, nif);
            Assert.True(clientResult.IsSuccess, clientResult.Error?.Description);

            client = clientResult.Value;
            context.Clients.Add(client);
            await context.SaveChangesAsync();
        }

        // Build a Quote aggregate with two lines referencing products from the same category,
        // using detached entities to mimic the real handler behavior.
        var quoteNumber = QuoteNumber.Create("DEV", DateTime.UtcNow.Year, 1);
        var quoteResult = Quote.Create(
            quoteNumber,
            client,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(7));
        Assert.True(quoteResult.IsSuccess, quoteResult.Error?.Description);
        var quote = quoteResult.Value;

        var addLine1 = quote.AddLine(product1, 1);
        var addLine2 = quote.AddLine(product2, 2);
        Assert.True(addLine1.IsSuccess);
        Assert.True(addLine2.IsSuccess);

        // Act & Assert: the repository should be able to save without EF tracking exceptions
        var savedQuote = await _repository.AddAsync(quote);
        Assert.NotEqual(Guid.Empty, savedQuote.Id);
    }

    public void Dispose()
    {
        // Nothing to dispose: InMemory database is scoped by name and will be GC'ed.
    }
}

