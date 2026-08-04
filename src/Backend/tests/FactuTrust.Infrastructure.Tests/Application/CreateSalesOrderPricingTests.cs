using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SalesOrders.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateSalesOrderPricingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime OrderDate = new(2026, 8, 1);

    [Fact]
    public async Task Handle_WhenUnitPriceZero_UsesResolvedNegotiatedPrice()
    {
        var product = NewProduct(catalogHt: 2310m);
        SalesOrder? captured = null;

        var orchestrator = new Mock<ILinePricingOrchestrator>();
        orchestrator
            .Setup(o => o.ResolveAsync(
                ClientId, product, 1m, OrderDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ResolvedLinePricing(
                Money.Create(1900m), null, null)));

        var handler = BuildHandler(product, orchestrator.Object, capturedOrder: o => captured = o);

        var dto = new CreateSalesOrderDto
        {
            ClientId = ClientId,
            OrderDate = OrderDate,
            Lines = new[]
            {
                new CreateSalesOrderLineDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 0m
                }
            }
        };

        var result = await handler.Handle(new CreateSalesOrderCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        var line = Assert.Single(captured!.Lines);
        Assert.Equal(1900m, line.UnitPrice.Amount);
    }

    [Fact]
    public async Task Handle_WhenUnitPriceExplicit_SkipsOrchestratorPriceOverride()
    {
        var product = NewProduct(catalogHt: 2310m);
        SalesOrder? captured = null;

        var orchestrator = new Mock<ILinePricingOrchestrator>();
        orchestrator
            .Setup(o => o.ResolveAsync(
                ClientId, product, 1m, OrderDate, null, It.Is<Money?>(m => m!.Amount == 2500m), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ResolvedLinePricing(
                Money.Create(2500m), null, null)));

        var handler = BuildHandler(product, orchestrator.Object, capturedOrder: o => captured = o);

        var dto = new CreateSalesOrderDto
        {
            ClientId = ClientId,
            OrderDate = OrderDate,
            Lines = new[]
            {
                new CreateSalesOrderLineDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = 2500m
                }
            }
        };

        var result = await handler.Handle(new CreateSalesOrderCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2500m, Assert.Single(captured!.Lines).UnitPrice.Amount);
    }

    [Fact]
    public async Task Handle_WhenPromotionEligible_AppliesDiscountPercent()
    {
        var product = NewProduct(catalogHt: 1430m);
        SalesOrder? captured = null;
        var promoId = Guid.NewGuid();

        var orchestrator = new Mock<ILinePricingOrchestrator>();
        orchestrator
            .Setup(o => o.ResolveAsync(
                ClientId, product, 3m, OrderDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ResolvedLinePricing(
                Money.Create(1430m),
                10m,
                new PromotionResolution(promoId, "solde été", 10m))));

        var handler = BuildHandler(product, orchestrator.Object, capturedOrder: o => captured = o);

        var dto = new CreateSalesOrderDto
        {
            ClientId = ClientId,
            OrderDate = OrderDate,
            Lines = new[]
            {
                new CreateSalesOrderLineDto { ProductId = product.Id, Quantity = 3m, UnitPrice = 0m }
            }
        };

        var result = await handler.Handle(new CreateSalesOrderCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(captured!.Lines);
        Assert.Equal(10m, line.DiscountPercent);
        Assert.Equal(promoId, line.AppliedPromotionId);
        Assert.Equal("solde été", line.AppliedPromotionName);
    }

    [Fact]
    public async Task Handle_WhenBelowMinQuantity_NoDiscountApplied()
    {
        var product = NewProduct(catalogHt: 1430m);
        SalesOrder? captured = null;

        var orchestrator = new Mock<ILinePricingOrchestrator>();
        orchestrator
            .Setup(o => o.ResolveAsync(
                ClientId, product, 1m, OrderDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ResolvedLinePricing(
                Money.Create(1430m), null, null)));

        var handler = BuildHandler(product, orchestrator.Object, capturedOrder: o => captured = o);

        var dto = new CreateSalesOrderDto
        {
            ClientId = ClientId,
            OrderDate = OrderDate,
            Lines = new[]
            {
                new CreateSalesOrderLineDto { ProductId = product.Id, Quantity = 1m, UnitPrice = 0m }
            }
        };

        var result = await handler.Handle(new CreateSalesOrderCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(captured!.Lines);
        Assert.Null(line.DiscountPercent);
        Assert.Null(line.AppliedPromotionId);
    }

    private static CreateSalesOrderCommandHandler BuildHandler(
        Product product,
        ILinePricingOrchestrator orchestrator,
        Action<SalesOrder>? capturedOrder = null)
    {
        var clientRepo = new Mock<IClientRepository>();
        clientRepo.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewClient());

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var salesOrderRepo = new Mock<ISalesOrderRepository>();
        salesOrderRepo
            .Setup(r => r.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>()))
            .Callback<SalesOrder, CancellationToken>((order, _) => capturedOrder?.Invoke(order))
            .ReturnsAsync((SalesOrder order, CancellationToken _) => order);

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(TenantId);

        var documentNumber = new Mock<IDocumentNumberService>();
        documentNumber
            .Setup(s => s.ReserveNextAsync(
                TenantId,
                NumberingDocumentType.SalesOrder,
                OrderDate.Year,
                OrderDate,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("CDE-2026-00001", OrderDate.Year, 1, "CDE"));

        var fiscalStamp = new Mock<IFiscalStampResolver>();
        fiscalStamp
            .Setup(s => s.ResolveSignedStampAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Money.Create(1m));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var audit = new Mock<IAuditService>();
        audit
            .Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new CreateSalesOrderCommandHandler(
            salesOrderRepo.Object,
            clientRepo.Object,
            productRepo.Object,
            new Mock<IWarehouseRepository>().Object,
            documentNumber.Object,
            fiscalStamp.Object,
            tenantContext.Object,
            currentUser.Object,
            audit.Object,
            orchestrator);
    }

    private static Product NewProduct(decimal catalogHt)
    {
        var result = Product.Create(
            "P-001", "Article test", ProductType.Product,
            Money.Create(catalogHt), VatRate.Standard, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis", "1000").Value;
        var email = Email.Create("client@test.tn").Value;
        var result = Client.Create("Client test", ClientType.Individual, address, email);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
