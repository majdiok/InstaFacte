using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.EventHandlers;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateFixedAssetsFromSupplierInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_WithFixedAssetLine_ShouldCreateDraftLinkedToInvoiceLine()
    {
        var category = DepreciationRateCategory.Create(
            "OTHER",
            "Autres immobilisations",
            10m,
            "228",
            "2828",
            "68112",
            isNonDepreciable: false,
            sortOrder: 99);

        var invoice = BuildSupplierInvoiceWithAssetLine(category.Id);
        var assetLine = invoice.Lines.First(l => l.IsFixedAsset);

        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices
            .Setup(x => x.GetByIdWithLinesAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        FixedAsset? saved = null;
        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets
            .Setup(x => x.CountByYearPrefixAsync(invoice.InvoiceDate.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        fixedAssets
            .Setup(x => x.AddAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FixedAsset asset, CancellationToken _) =>
            {
                saved = asset;
                return asset;
            });

        var categories = new Mock<IDepreciationRateCategoryRepository>();
        categories
            .Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var handler = new CreateFixedAssetsFromSupplierInvoiceHandler(
            supplierInvoices.Object,
            fixedAssets.Object,
            categories.Object,
            NullLogger<CreateFixedAssetsFromSupplierInvoiceHandler>.Instance,
            Options.Create(new FixedAssetsOptions { Enabled = true }));

        await handler.Handle(new SupplierInvoiceCreatedForAccountingNotification(invoice.Id), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(invoice.Id, saved!.SupplierInvoiceId);
        Assert.Equal(assetLine.Id, saved.SupplierInvoiceLineId);
        Assert.Equal(FixedAssetStatus.Draft, saved.Status);
        Assert.Equal(assetLine.SubTotal.Amount, saved.AcquisitionCost);
        fixedAssets.Verify(x => x.AddAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFeatureDisabled_ShouldSkipCreation()
    {
        var category = DepreciationRateCategory.Create(
            "OTHER", "Autres", 10m, "228", "2828", "68112", isNonDepreciable: false, sortOrder: 99);
        var invoice = BuildSupplierInvoiceWithAssetLine(category.Id);

        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices
            .Setup(x => x.GetByIdWithLinesAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var fixedAssets = new Mock<IFixedAssetRepository>();

        var handler = new CreateFixedAssetsFromSupplierInvoiceHandler(
            supplierInvoices.Object,
            fixedAssets.Object,
            Mock.Of<IDepreciationRateCategoryRepository>(),
            NullLogger<CreateFixedAssetsFromSupplierInvoiceHandler>.Instance,
            Options.Create(new FixedAssetsOptions { Enabled = false }));

        await handler.Handle(new SupplierInvoiceCreatedForAccountingNotification(invoice.Id), CancellationToken.None);

        fixedAssets.Verify(x => x.AddAsync(It.IsAny<FixedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SupplierInvoice BuildSupplierInvoiceWithAssetLine(Guid categoryId)
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;

        var productCategory = ProductCategory.Create("GEN", "Général").Value;
        var unitPrice = Money.Create(2000m, Money.DefaultCurrency);
        var product = Product.Create(
            "PR-IMMO-1",
            "Ordinateur portable",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            productCategory.Id,
            purchasePrice: unitPrice).Value;

        var poNumber = PurchaseOrderNumber.Create("BC", 2026, 500010);
        var po = PurchaseOrder.Create(poNumber, supplier, new DateTime(2026, 4, 1)).Value;
        var addLine = po.AddLine(product, 1m);
        if (addLine.IsFailure) throw new InvalidOperationException(addLine.Error.Description);
        var confirm = po.Confirm();
        if (confirm.IsFailure) throw new InvalidOperationException(confirm.Error.Description);
        var lineId = po.Lines.First().Id;
        var receive = po.ReceiveGoods(new[] { (lineId, 1m) });
        if (receive.IsFailure) throw new InvalidOperationException(receive.Error.Description);

        var lineSelections = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoiceResult = SupplierInvoice.CreateFromPurchaseOrder(
            po, "FS-2026-IMMO", new DateTime(2026, 4, 5), lineSelections);
        if (invoiceResult.IsFailure) throw new InvalidOperationException(invoiceResult.Error.Description);

        var invoice = invoiceResult.Value;
        invoice.ApplyLineAssetClassifications([
            (LineNumber: 1, IsFixedAsset: true, AssetAccountNumber: "228", DepreciationRateCategoryId: categoryId)
        ]);
        return invoice;
    }
}