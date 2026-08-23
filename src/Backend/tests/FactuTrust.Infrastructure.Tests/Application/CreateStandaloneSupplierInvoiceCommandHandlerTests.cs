using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SupplierInvoices.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateStandaloneSupplierInvoiceCommandHandlerTests
{
    [Fact]
    public async Task Handle_UseSuggestedNumber_CreatesInvoiceWithNullSources()
    {
        var (supplier, product, handler, invoiceRepo, poRepo, numberService) = BuildScenario();
        numberService
            .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-000045");

        var result = await handler.Handle(Command(supplier.Id, product.Id, useSuggested: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FS-2026-000045", result.Value.InvoiceNumber);
        invoiceRepo.Verify(
            r => r.AddAsync(
                It.Is<SupplierInvoice>(i =>
                    !i.PurchaseOrderId.HasValue &&
                    !i.SourcePurchaseReceiptId.HasValue &&
                    i.InvoiceNumber == "FS-2026-000045"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        poRepo.Verify(
            r => r.UpdateAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ManualDuplicateNumber_ReturnsConflictWithoutPersist()
    {
        var (supplier, product, handler, invoiceRepo, _, numberService) = BuildScenario();
        numberService
            .Setup(s => s.IsAvailableAsync("FS-2026-DUP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await handler.Handle(
            Command(supplier.Id, product.Id, invoiceNumber: "FS-2026-DUP"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        invoiceRepo.Verify(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StockManagedProduct_FailsAndDoesNotReserveNumber()
    {
        var supplier = BuildSupplier();
        var stockProduct = BuildProduct("STK", ProductType.Product, isStockManaged: true);
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        var handler = BuildHandler(supplier, stockProduct, invoiceRepo, new Mock<IPurchaseOrderRepository>(), numberService);

        var result = await handler.Handle(Command(supplier.Id, stockProduct.Id, useSuggested: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("stock", result.Error.Description, StringComparison.OrdinalIgnoreCase);
        numberService.Verify(
            s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        invoiceRepo.Verify(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InactiveSupplier_Fails()
    {
        var supplier = BuildSupplier();
        supplier.Deactivate();
        var product = BuildProduct("SVC", ProductType.Service);
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        var handler = BuildHandler(supplier, product, invoiceRepo, new Mock<IPurchaseOrderRepository>(), numberService);

        var result = await handler.Handle(Command(supplier.Id, product.Id, useSuggested: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        numberService.Verify(
            s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateExternalReference_ReturnsConflict()
    {
        var supplier = BuildSupplier();
        var product = BuildProduct("SVC", ProductType.Service);
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        var handler = BuildHandler(supplier, product, invoiceRepo, new Mock<IPurchaseOrderRepository>(), numberService);
        invoiceRepo
            .Setup(r => r.GetNonCancelledExternalReferencesForSupplierAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["STEG-08-2026"]);

        var result = await handler.Handle(
            Command(supplier.Id, product.Id, useSuggested: true, externalReference: "steg 08 2026"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        numberService.Verify(
            s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FixedAssetClassification_IsApplied()
    {
        var (supplier, product, handler, invoiceRepo, _, numberService) = BuildScenario();
        numberService
            .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-IMMO");

        var dto = new CreateStandaloneSupplierInvoiceDto
        {
            SupplierId = supplier.Id,
            InvoiceDate = new DateTime(2026, 8, 17),
            UseSuggestedNumber = true,
            Lines =
            [
                new CreateStandaloneSupplierInvoiceLineDto
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    IsFixedAsset = true,
                    AssetAccountNumber = "223"
                }
            ]
        };

        var result = await handler.Handle(new CreateStandaloneSupplierInvoiceCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        invoiceRepo.Verify(
            r => r.AddAsync(
                It.Is<SupplierInvoice>(i => i.Lines.Single().IsFixedAsset && i.Lines.Single().AssetAccountNumber == "223"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (
        Supplier supplier,
        Product product,
        CreateStandaloneSupplierInvoiceCommandHandler handler,
        Mock<ISupplierInvoiceRepository> invoiceRepo,
        Mock<IPurchaseOrderRepository> poRepo,
        Mock<ISupplierInvoiceNumberService> numberService)
        BuildScenario()
    {
        var supplier = BuildSupplier();
        var product = BuildProduct("LOYER", ProductType.Service);
        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierInvoice invoice, CancellationToken _) => invoice);
        invoiceRepo
            .Setup(r => r.GetNonCancelledExternalReferencesForSupplierAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var poRepo = new Mock<IPurchaseOrderRepository>();
        var numberService = new Mock<ISupplierInvoiceNumberService>();
        var handler = BuildHandler(supplier, product, invoiceRepo, poRepo, numberService);
        return (supplier, product, handler, invoiceRepo, poRepo, numberService);
    }

    private static CreateStandaloneSupplierInvoiceCommandHandler BuildHandler(
        Supplier supplier,
        Product product,
        Mock<ISupplierInvoiceRepository> invoiceRepo,
        Mock<IPurchaseOrderRepository> poRepo,
        Mock<ISupplierInvoiceNumberService> numberService)
    {
        var suppliers = new Mock<ISupplierRepository>();
        suppliers.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);

        var products = new Mock<IProductRepository>();
        products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        invoiceRepo
            .Setup(r => r.GetNonCancelledExternalReferencesForSupplierAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        return new CreateStandaloneSupplierInvoiceCommandHandler(
            suppliers.Object,
            new Mock<IWarehouseRepository>().Object,
            products.Object,
            invoiceRepo.Object,
            poRepo.Object,
            new Mock<IAuditService>().Object,
            NullLogger<CreateStandaloneSupplierInvoiceCommandHandler>.Instance,
            new Mock<IPublisher>().Object,
            new Mock<IWithholdingTaxRepository>().Object,
            new Mock<IWithholdingTaxService>().Object,
            new Mock<IWithholdingFiscalYearParameterRepository>().Object,
            numberService.Object);
    }

    private static CreateStandaloneSupplierInvoiceCommand Command(
        Guid supplierId,
        Guid productId,
        bool useSuggested = false,
        string invoiceNumber = "",
        string? externalReference = null) =>
        new(new CreateStandaloneSupplierInvoiceDto
        {
            SupplierId = supplierId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateTime(2026, 8, 17),
            UseSuggestedNumber = useSuggested,
            ExternalReference = externalReference,
            Lines =
            [
                new CreateStandaloneSupplierInvoiceLineDto
                {
                    ProductId = productId,
                    Quantity = 1m
                }
            ]
        });

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("STEG", SupplierType.Business, address, email, nif: nif).Value;
    }

    private static Product BuildProduct(string code, ProductType type, bool isStockManaged = false)
    {
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        return Product.Create(
            code, code, type, price, VatRate.Standard, category.Id,
            purchasePrice: price, isStockManaged: isStockManaged).Value;
    }
}
