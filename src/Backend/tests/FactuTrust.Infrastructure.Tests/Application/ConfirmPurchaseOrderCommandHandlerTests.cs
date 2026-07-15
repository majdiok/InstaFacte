using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class ConfirmPurchaseOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        var repository = new Mock<IPurchaseOrderRepository>();
        repository
            .Setup(x => x.GetByIdWithLinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);
        var auditService = new Mock<IAuditService>();
        var handler = new ConfirmPurchaseOrderCommandHandler(repository.Object, auditService.Object);

        var result = await handler.Handle(new ConfirmPurchaseOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PurchaseOrder.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenPersistenceConcurrencyFails_ShouldReturnConflict()
    {
        var order = BuildDraftOrderWithLine();
        var repository = new Mock<IPurchaseOrderRepository>();
        repository
            .Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository
            .Setup(x => x.TryConfirmDraftAsync(order.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var auditService = new Mock<IAuditService>();
        var handler = new ConfirmPurchaseOrderCommandHandler(repository.Object, auditService.Object);

        var result = await handler.Handle(new ConfirmPurchaseOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        auditService.Verify(x => x.LogAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDraftWithLines_ShouldConfirmAndAudit()
    {
        var order = BuildDraftOrderWithLine();
        var repository = new Mock<IPurchaseOrderRepository>();
        repository
            .Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository
            .Setup(x => x.TryConfirmDraftAsync(order.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var auditService = new Mock<IAuditService>();
        var handler = new ConfirmPurchaseOrderCommandHandler(repository.Object, auditService.Object);

        var result = await handler.Handle(new ConfirmPurchaseOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Confirmed, order.Status);
        repository.Verify(x => x.TryConfirmDraftAsync(order.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(x => x.LogAsync(
            It.IsAny<string>(),
            "PurchaseOrder",
            order.Id,
            null,
            It.IsAny<object?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PurchaseOrder BuildDraftOrderWithLine()
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier.command@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Supplier command", SupplierType.Business, address, email, nif: nif).Value;

        var category = ProductCategory.Create("CMD", "Command category").Value;
        var unitPrice = Money.Create(50m, Money.DefaultCurrency);
        var product = Product.Create(
            "PR-CMD-1",
            "Produit commande",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;

        var number = PurchaseOrderNumber.Create("BC", 2026, 100);
        var createResult = PurchaseOrder.Create(number, supplier, new DateTime(2026, 4, 1));
        var order = createResult.Value;
        var addLine = order.AddLine(product, 1m);
        Assert.True(addLine.IsSuccess);
        return order;
    }
}
