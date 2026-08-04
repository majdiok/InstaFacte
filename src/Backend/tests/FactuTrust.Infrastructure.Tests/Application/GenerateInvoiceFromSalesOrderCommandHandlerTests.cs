using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.SalesOrders.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GenerateInvoiceFromSalesOrderCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenNoDeliveryAndNoAdvanceBilling_ShouldReturnValidationError()
    {
        var order = NewConfirmedOrder();
        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = BuildHandler(orderRepo: orderRepo.Object);

        var result = await handler.Handle(new GenerateInvoiceFromSalesOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Lines", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenQuantityExceedsDeliveredNotInvoiced_ShouldReturnValidationError()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();
        order.RecordDeliveries(new[] { (line.Id, 1m) });

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = BuildHandler(orderRepo: orderRepo.Object);

        var result = await handler.Handle(
            new GenerateInvoiceFromSalesOrderCommand(
                order.Id,
                new[] { new SalesOrderInvoiceLineDto(line.Id, 2m) }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Quantity", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenAdvanceBilling_ShouldInvoicePendingQuantity()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Invoice? capturedInvoice = null;
        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo.Setup(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((i, _) => capturedInvoice = i)
            .Returns((Invoice i, CancellationToken _) => Task.FromResult(i));

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct);

        var handler = BuildHandler(orderRepo: orderRepo.Object, invoiceRepo: invoiceRepo.Object, productRepo: productRepo.Object);

        var result = await handler.Handle(
            new GenerateInvoiceFromSalesOrderCommand(order.Id, AdvanceBilling: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedInvoice);
        Assert.False(capturedInvoice.SourceDeliveryNoteId.HasValue);
        Assert.Single(capturedInvoice.Lines);
        Assert.Equal(line.PendingInvoiceQuantity, capturedInvoice.Lines.Sum(l => l.Quantity));
    }

    [Fact]
    public async Task Handle_WhenDeliveredNotInvoiced_ShouldCreateInvoiceWithoutSourceDeliveryNote()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();
        order.RecordDeliveries(new[] { (line.Id, 2m) });

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Invoice? capturedInvoice = null;
        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo.Setup(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((i, _) => capturedInvoice = i)
            .Returns((Invoice i, CancellationToken _) => Task.FromResult(i));

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct);

        var handler = BuildHandler(orderRepo: orderRepo.Object, invoiceRepo: invoiceRepo.Object, productRepo: productRepo.Object);

        var result = await handler.Handle(
            new GenerateInvoiceFromSalesOrderCommand(order.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedInvoice);
        Assert.False(capturedInvoice.SourceDeliveryNoteId.HasValue);
        Assert.Equal(2m, capturedInvoice.Lines.Sum(l => l.Quantity));
    }

    private static GenerateInvoiceFromSalesOrderCommandHandler BuildHandler(
        ISalesOrderRepository? orderRepo = null,
        IInvoiceRepository? invoiceRepo = null,
        IProductRepository? productRepo = null)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(TenantId);

        var numberService = new Mock<IDocumentNumberService>();
        numberService.Setup(x => x.ReserveNextAsync(TenantId, NumberingDocumentType.Invoice, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("FAC-2026-00001", 2026, 1, "FAC"));

        var fiscalStampResolver = new Mock<IFiscalStampResolver>();
        fiscalStampResolver.Setup(x => x.ResolveSignedStampAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Money.Create(1m));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var auditService = new Mock<IAuditService>();
        auditService.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new GenerateInvoiceFromSalesOrderCommandHandler(
            orderRepo ?? Mock.Of<ISalesOrderRepository>(),
            invoiceRepo ?? Mock.Of<IInvoiceRepository>(),
            productRepo ?? Mock.Of<IProductRepository>(),
            numberService.Object,
            fiscalStampResolver.Object,
            tenantContext.Object,
            currentUser.Object,
            auditService.Object);
    }

    private static SalesOrder NewConfirmedOrder()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var product = NewProduct();

        var order = SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            client,
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 5)).Value;

        Assert.True(order.AddLine(product, quantity: 3m).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);
        return order;
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
