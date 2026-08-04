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

public sealed class GenerateDeliveryNoteFromSalesOrderCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesOrder?)null);

        var handler = BuildHandler(orderRepo: orderRepo.Object);

        var result = await handler.Handle(new GenerateDeliveryNoteFromSalesOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Commande.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenOrderIsDraft_ShouldReturnValidationError()
    {
        var order = NewDraftOrder();

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = BuildHandler(orderRepo: orderRepo.Object);

        var result = await handler.Handle(new GenerateDeliveryNoteFromSalesOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Status", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenQuantityExceedsPendingDelivery_ShouldReturnValidationError()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = BuildHandler(orderRepo: orderRepo.Object);

        var result = await handler.Handle(
            new GenerateDeliveryNoteFromSalesOrderCommand(
                order.Id,
                new[] { new SalesOrderDeliveryLineDto(line.Id, line.PendingDeliveryQuantity + 1) }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Quantity", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenNoLinesProvided_ShouldDeliverEntirePendingQuantity()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        DeliveryNote? capturedNote = null;
        var deliveryNoteRepo = new Mock<IDeliveryNoteRepository>();
        deliveryNoteRepo.Setup(x => x.AddAsync(It.IsAny<DeliveryNote>(), It.IsAny<CancellationToken>()))
            .Callback<DeliveryNote, CancellationToken>((n, _) => capturedNote = n)
            .Returns((DeliveryNote n, CancellationToken _) => Task.FromResult(n));

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct);

        var handler = BuildHandler(orderRepo: orderRepo.Object, deliveryNoteRepo: deliveryNoteRepo.Object, productRepo: productRepo.Object);

        var result = await handler.Handle(new GenerateDeliveryNoteFromSalesOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedNote);
        Assert.Equal(order.Id, capturedNote.SourceSalesOrderId);
        Assert.Equal(line.PendingDeliveryQuantity, capturedNote.Lines.Sum(l => l.OrderedQuantity));
    }

    [Fact]
    public async Task Handle_WhenPartialLinesProvided_ShouldDeliverRequestedQuantity()
    {
        var order = NewConfirmedOrder();
        var line = order.Lines.Single();

        var orderRepo = new Mock<ISalesOrderRepository>();
        orderRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        DeliveryNote? capturedNote = null;
        var deliveryNoteRepo = new Mock<IDeliveryNoteRepository>();
        deliveryNoteRepo.Setup(x => x.AddAsync(It.IsAny<DeliveryNote>(), It.IsAny<CancellationToken>()))
            .Callback<DeliveryNote, CancellationToken>((n, _) => capturedNote = n)
            .Returns((DeliveryNote n, CancellationToken _) => Task.FromResult(n));

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct);

        var handler = BuildHandler(orderRepo: orderRepo.Object, deliveryNoteRepo: deliveryNoteRepo.Object, productRepo: productRepo.Object);

        var requested = new[] { new SalesOrderDeliveryLineDto(line.Id, 1m) };
        var result = await handler.Handle(new GenerateDeliveryNoteFromSalesOrderCommand(order.Id, requested), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedNote);
        Assert.Single(capturedNote.Lines);
        Assert.Equal(1m, capturedNote.Lines.Single().OrderedQuantity);
    }

    private static GenerateDeliveryNoteFromSalesOrderCommandHandler BuildHandler(
        ISalesOrderRepository? orderRepo = null,
        IDeliveryNoteRepository? deliveryNoteRepo = null,
        IProductRepository? productRepo = null)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(TenantId);

        var numberService = new Mock<IDocumentNumberService>();
        numberService.Setup(x => x.ReserveNextAsync(TenantId, NumberingDocumentType.DeliveryNote, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("BL-2026-00001", 2026, 1, "BL"));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var auditService = new Mock<IAuditService>();
        auditService.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new GenerateDeliveryNoteFromSalesOrderCommandHandler(
            orderRepo ?? Mock.Of<ISalesOrderRepository>(),
            deliveryNoteRepo ?? Mock.Of<IDeliveryNoteRepository>(),
            productRepo ?? Mock.Of<IProductRepository>(),
            numberService.Object,
            tenantContext.Object,
            currentUser.Object,
            auditService.Object);
    }

    private static SalesOrder NewDraftOrder()
    {
        var (client, product) = NewClientAndProduct();
        var order = SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            client,
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 5)).Value;

        Assert.True(order.AddLine(product, quantity: 3m).IsSuccess);
        return order;
    }

    private static SalesOrder NewConfirmedOrder()
    {
        var order = NewDraftOrder();
        Assert.True(order.Confirm().IsSuccess);
        return order;
    }

    private static (Client Client, Product Product) NewClientAndProduct()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        return (client, NewProduct());
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
