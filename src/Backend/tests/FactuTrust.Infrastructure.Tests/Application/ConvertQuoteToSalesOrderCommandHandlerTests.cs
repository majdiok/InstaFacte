using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.SalesOrders.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class ConvertQuoteToSalesOrderCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenQuoteNotFound_ShouldReturnNotFound()
    {
        var quoteRepo = new Mock<IQuoteRepository>();
        quoteRepo.Setup(x => x.GetByIdWithLinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);

        var handler = BuildHandler(quoteRepo: quoteRepo.Object);

        var result = await handler.Handle(new ConvertQuoteToSalesOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Devis.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenQuoteNotAccepted_ShouldReturnValidationError()
    {
        var quote = NewQuote(status: QuoteStatus.Sent);
        var quoteRepo = new Mock<IQuoteRepository>();
        quoteRepo.Setup(x => x.GetByIdWithLinesAsync(quote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var handler = BuildHandler(quoteRepo: quoteRepo.Object);

        var result = await handler.Handle(new ConvertQuoteToSalesOrderCommand(quote.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Status", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenQuoteAlreadyConvertedToSalesOrder_ShouldReturnConflict()
    {
        var quote = NewQuote(status: QuoteStatus.Accepted);
        quote.MarkAsConvertedToSalesOrder(Guid.NewGuid());

        var quoteRepo = new Mock<IQuoteRepository>();
        quoteRepo.Setup(x => x.GetByIdWithLinesAsync(quote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var handler = BuildHandler(quoteRepo: quoteRepo.Object);

        var result = await handler.Handle(new ConvertQuoteToSalesOrderCommand(quote.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenQuoteHasFreeLine_ShouldReturnValidationError()
    {
        var quote = NewQuote(status: QuoteStatus.Accepted, includeFreeLine: true);
        var freeLine = quote.Lines.FirstOrDefault(l => !l.ProductId.HasValue);
        Assert.NotNull(freeLine);

        var quoteRepo = new Mock<IQuoteRepository>();
        quoteRepo.Setup(x => x.GetByIdWithLinesAsync(quote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var handler = BuildHandler(quoteRepo: quoteRepo.Object);

        var result = await handler.Handle(new ConvertQuoteToSalesOrderCommand(quote.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Lines", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenAcceptedQuote_ShouldCreateSalesOrder_AndLockQuote()
    {
        var quote = NewQuote(status: QuoteStatus.Draft);
        var product = NewProduct();
        Assert.True(quote.AddLine(product, quantity: 2m).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(1m)).IsSuccess);
        Assert.True(quote.Send().IsSuccess);
        Assert.True(quote.Accept().IsSuccess);

        var quoteRepo = new Mock<IQuoteRepository>();
        quoteRepo.Setup(x => x.GetByIdWithLinesAsync(quote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        quoteRepo.Setup(x => x.UpdateAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var salesOrderRepo = new Mock<ISalesOrderRepository>();
        salesOrderRepo.Setup(x => x.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>()))
            .Returns((SalesOrder o, CancellationToken _) => Task.FromResult(o));

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = BuildHandler(
            quoteRepo: quoteRepo.Object,
            salesOrderRepo: salesOrderRepo.Object,
            productRepo: productRepo.Object);

        var result = await handler.Handle(
            new ConvertQuoteToSalesOrderCommand(quote.Id, OrderDate: new DateTime(2026, 7, 25), ExpectedDeliveryDate: new DateTime(2026, 8, 5), WarehouseId: WarehouseId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : string.Empty);
        Assert.NotEqual(Guid.Empty, result.Value);

        salesOrderRepo.Verify(x => x.AddAsync(It.Is<SalesOrder>(o =>
            o.ClientId == quote.ClientId &&
            o.SourceQuoteId == quote.Id &&
            o.WarehouseId == WarehouseId &&
            o.Lines.Count == 1 &&
            o.Lines.Single().ProductId == product.Id &&
            o.Lines.Single().Quantity == 2m), It.IsAny<CancellationToken>()), Times.Once);

        quoteRepo.Verify(x => x.UpdateAsync(It.Is<Quote>(q =>
            q.Id == quote.Id &&
            q.Status == QuoteStatus.Converted &&
            q.ConvertedSalesOrderId == result.Value), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConvertQuoteToSalesOrderCommandHandler BuildHandler(
        IQuoteRepository? quoteRepo = null,
        ISalesOrderRepository? salesOrderRepo = null,
        IProductRepository? productRepo = null)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(TenantId);

        var numberService = new Mock<IDocumentNumberService>();
        numberService.Setup(x => x.ReserveNextAsync(TenantId, NumberingDocumentType.SalesOrder, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("CDE-2026-00001", 2026, 1, "CDE"));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var auditService = new Mock<IAuditService>();
        auditService.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ConvertQuoteToSalesOrderCommandHandler(
            quoteRepo ?? Mock.Of<IQuoteRepository>(),
            salesOrderRepo ?? Mock.Of<ISalesOrderRepository>(),
            productRepo ?? Mock.Of<IProductRepository>(),
            numberService.Object,
            tenantContext.Object,
            currentUser.Object,
            auditService.Object);
    }

    private static Quote NewQuote(QuoteStatus status, bool includeFreeLine = true)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var quote = Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            client,
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20)).Value;

        if (status == QuoteStatus.Draft)
            return quote;

        var product = NewProduct();
        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(1m)).IsSuccess);

        if (includeFreeLine)
        {
            Assert.True(quote.AddCustomLine(
                designation: "Ligne libre",
                description: null,
                quantity: 1m,
                unit: "Unité",
                unitPrice: Money.Create(10m),
                vatRate: VatRate.Standard).IsSuccess);
        }

        if (status == QuoteStatus.Sent)
            Assert.True(quote.Send().IsSuccess);
        else if (status == QuoteStatus.Accepted)
        {
            Assert.True(quote.Send().IsSuccess);
            Assert.True(quote.Accept().IsSuccess);
        }

        return quote;
    }

    private static Product NewProduct() =>
        Product.Create(
            code: "P-CMD",
            name: "Produit commande",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid()).Value;
}
