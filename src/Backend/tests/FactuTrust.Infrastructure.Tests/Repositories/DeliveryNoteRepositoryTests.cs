using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class DeliveryNoteRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly DeliveryNoteRepository _repository;

    public DeliveryNoteRepositoryTests()
    {
        _databaseName = $"TestDb_DeliveryNotes_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new DeliveryNoteRepository(_contextFactory);
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
    public async Task GetForReportAsync_WhenDatesAreNull_ShouldReturnAllDeliveryNotes()
    {
        // Arrange
        var client1 = await CreateClientAsync(
            name: "Client BL 1",
            email: "bl1@test.com",
            nifValue: "1234567/A/B/C/101");

        var client2 = await CreateClientAsync(
            name: "Client BL 2",
            email: "bl2@test.com",
            nifValue: "1234567/A/B/C/102");

        await AddDeliveryNoteAsync(client1, issueDate: new DateTime(2026, 1, 10), sequence: 1);
        await AddDeliveryNoteAsync(client1, issueDate: new DateTime(2026, 1, 20), sequence: 2);
        await AddDeliveryNoteAsync(client2, issueDate: new DateTime(2026, 2, 5), sequence: 3);

        // Act
        var notes = await _repository.GetForReportAsync(fromDate: null, toDate: null, clientId: null);

        // Assert
        Assert.Equal(3, notes.Count);
        Assert.Equal(new DateTime(2026, 2, 5), notes[0].IssueDate);
        Assert.Equal(new DateTime(2026, 1, 20), notes[1].IssueDate);
        Assert.Equal(new DateTime(2026, 1, 10), notes[2].IssueDate);
    }

    [Fact]
    public async Task GetForReportAsync_WhenOnlyFromDateIsProvided_ShouldFilterByIssueDateInclusive()
    {
        // Arrange
        var client = await CreateClientAsync(
            name: "Client BL FromDate",
            email: "blfrom@test.com",
            nifValue: "1234567/A/B/C/110");

        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 10), sequence: 1);
        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 20), sequence: 2);
        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 2, 5), sequence: 3);

        // Act
        var notes = await _repository.GetForReportAsync(
            fromDate: new DateTime(2026, 1, 15),
            toDate: null,
            clientId: null);

        // Assert
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.True(n.IssueDate >= new DateTime(2026, 1, 15)));
    }

    [Fact]
    public async Task GetForReportAsync_WhenOnlyToDateIsProvided_ShouldFilterByIssueDateInclusive()
    {
        // Arrange
        var client = await CreateClientAsync(
            name: "Client BL ToDate",
            email: "blto@test.com",
            nifValue: "1234567/A/B/C/120");

        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 10), sequence: 1);
        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 20), sequence: 2);
        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 2, 5), sequence: 3);

        // Act
        var notes = await _repository.GetForReportAsync(
            fromDate: null,
            toDate: new DateTime(2026, 1, 20),
            clientId: null);

        // Assert
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.True(n.IssueDate <= new DateTime(2026, 1, 20)));
    }

    [Fact]
    public async Task GetForReportAsync_WhenClientIdIsProvided_ShouldFilterByClient()
    {
        // Arrange
        var client1 = await CreateClientAsync(
            name: "Client BL Filter 1",
            email: "blf1@test.com",
            nifValue: "1234567/A/B/C/130");

        var client2 = await CreateClientAsync(
            name: "Client BL Filter 2",
            email: "blf2@test.com",
            nifValue: "1234567/A/B/C/131");

        await AddDeliveryNoteAsync(client1, issueDate: new DateTime(2026, 1, 10), sequence: 1);
        await AddDeliveryNoteAsync(client2, issueDate: new DateTime(2026, 1, 15), sequence: 2);

        // Act
        var notes = await _repository.GetForReportAsync(
            fromDate: null,
            toDate: null,
            clientId: client1.Id);

        // Assert
        Assert.Single(notes);
        Assert.Equal(client1.Id, notes[0].ClientId);
    }

    [Fact]
    public async Task GetForReportAsync_WhenFromDateIsAfterToDate_ShouldReturnEmpty()
    {
        // Arrange
        var client = await CreateClientAsync(
            name: "Client BL Range",
            email: "blrange@test.com",
            nifValue: "1234567/A/B/C/140");

        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 10), sequence: 1);
        await AddDeliveryNoteAsync(client, issueDate: new DateTime(2026, 1, 20), sequence: 2);

        // Act
        var notes = await _repository.GetForReportAsync(
            fromDate: new DateTime(2026, 1, 20),
            toDate: new DateTime(2026, 1, 10),
            clientId: null);

        // Assert
        Assert.Empty(notes);
    }

    [Fact]
    public async Task AddAsync_WithMultipleLinesSharingSameCategory_ShouldNotThrowTrackingException()
    {
        // Arrange: create a shared product category in its own context
        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Cat. BL Partagée", "Catégorie de test BL");
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
                code: "BL001",
                name: "Produit BL 1",
                type: ProductType.Service,
                unitPrice: Money.Create(100m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p1.IsSuccess, p1.Error?.Description);
            product1Persisted = p1.Value;

            var p2 = Product.Create(
                code: "BL002",
                name: "Produit BL 2",
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
        // scenario reproduced by ProductRepository.GetByIdAsync inside the real handler.
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

        // Sanity check: the two Category instances must be DIFFERENT references.
        Assert.NotSame(reloadedProduct1.Category, reloadedProduct2.Category);
        Assert.Equal(reloadedProduct1.Category!.Id, reloadedProduct2.Category!.Id);

        // Create a client
        var client = await CreateClientAsync(
            name: "Client BL Tracking",
            email: "bl-tracking@test.com",
            nifValue: "1234567/A/B/C/900");

        // Build a DeliveryNote aggregate with two lines referencing products from the same category
        var noteNumberResult = DeliveryNoteNumber.Generate(2026, 99);
        Assert.True(noteNumberResult.IsSuccess, noteNumberResult.Error?.Description);

        var deliveryNoteResult = DeliveryNote.Create(
            number: noteNumberResult.Value,
            client: client,
            issueDate: DateTime.UtcNow.Date,
            deliveryAddress: "456 Adresse Livraison",
            deliveryCity: "Tunis");
        Assert.True(deliveryNoteResult.IsSuccess, deliveryNoteResult.Error?.Description);
        var deliveryNote = deliveryNoteResult.Value;

        var addLine1 = deliveryNote.AddLine(reloadedProduct1, 2);
        var addLine2 = deliveryNote.AddLine(reloadedProduct2, 3);
        Assert.True(addLine1.IsSuccess, addLine1.Error?.Description);
        Assert.True(addLine2.IsSuccess, addLine2.Error?.Description);

        // Act: before the fix this threw InvalidOperationException on the 2nd Product.Attach
        var saved = await _repository.AddAsync(deliveryNote);

        // Assert
        Assert.NotEqual(Guid.Empty, saved.Id);

        using var verify = _contextFactory.CreateContext();
        var reloaded = await verify.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == saved.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Lines.Count);
    }

    [Fact]
    public async Task UpdateAsync_WithMultipleLinesSharingSameCategory_ShouldNotThrowTrackingException()
    {
        // Arrange: create a shared product category in its own context
        ProductCategory category;
        using (var context = _contextFactory.CreateContext())
        {
            var categoryResult = ProductCategory.Create("Cat. BL Update", "Catégorie de test BL Update");
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
                code: "BLU001",
                name: "Produit BL Update 1",
                type: ProductType.Service,
                unitPrice: Money.Create(200m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p1.IsSuccess, p1.Error?.Description);
            product1Persisted = p1.Value;

            var p2 = Product.Create(
                code: "BLU002",
                name: "Produit BL Update 2",
                type: ProductType.Service,
                unitPrice: Money.Create(250m, "TND"),
                vatRate: VatRate.Standard,
                categoryId: category.Id);
            Assert.True(p2.IsSuccess, p2.Error?.Description);
            product2Persisted = p2.Value;

            context.Products.AddRange(product1Persisted, product2Persisted);
            await context.SaveChangesAsync();
        }

        // Create a client and a delivery note via AddAsync (warm-up path)
        var client = await CreateClientAsync(
            name: "Client BL Update",
            email: "bl-update@test.com",
            nifValue: "1234567/A/B/C/901");

        var noteNumberResult = DeliveryNoteNumber.Generate(2026, 100);
        Assert.True(noteNumberResult.IsSuccess, noteNumberResult.Error?.Description);

        var deliveryNoteResult = DeliveryNote.Create(
            number: noteNumberResult.Value,
            client: client,
            issueDate: DateTime.UtcNow.Date,
            deliveryAddress: "789 Adresse Livraison",
            deliveryCity: "Tunis");
        Assert.True(deliveryNoteResult.IsSuccess, deliveryNoteResult.Error?.Description);

        // Reload products with their Category navigation in distinct contexts (two distinct Category instances)
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

        var deliveryNote = deliveryNoteResult.Value;
        Assert.True(deliveryNote.AddLine(reloadedProduct1, 1).IsSuccess);
        Assert.True(deliveryNote.AddLine(reloadedProduct2, 1).IsSuccess);

        var addedNote = await _repository.AddAsync(deliveryNote);
        Assert.NotEqual(Guid.Empty, addedNote.Id);

        // Act: apply a benign update (notes) and call UpdateAsync — this exercises the same
        // ProductCategory-tracking path that previously threw InvalidOperationException.
        deliveryNote.UpdateNotes("Notes mises à jour pour test régression tracking");

        var updateException = await Record.ExceptionAsync(() => _repository.UpdateAsync(deliveryNote));

        // Assert: UpdateAsync must not throw a tracking exception
        Assert.Null(updateException);

        using var verify = _contextFactory.CreateContext();
        var verified = await verify.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == addedNote.Id);
        Assert.NotNull(verified);
        Assert.Equal("Notes mises à jour pour test régression tracking", verified!.Notes);
        Assert.Equal(2, verified.Lines.Count);
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

    private async Task AddDeliveryNoteAsync(Client client, DateTime issueDate, int sequence)
    {
        var noteNumberResult = DeliveryNoteNumber.Generate(year: 2026, sequence: sequence);
        Assert.True(noteNumberResult.IsSuccess, noteNumberResult.Error?.Description);

        var noteResult = DeliveryNote.Create(
            number: noteNumberResult.Value,
            client: client,
            issueDate: issueDate,
            deliveryAddress: "123 Delivery Address",
            deliveryCity: "Tunis");

        Assert.True(noteResult.IsSuccess, noteResult.Error?.Description);

        await _repository.AddAsync(noteResult.Value);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}

