using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class StockVoucherDomainTests
{
    [Fact]
    public void Create_WithValidData_ShouldBeDraft()
    {
        var voucher = BuildDraftEntry();
        Assert.Equal(StockVoucherStatus.Draft, voucher.Status);
        Assert.Equal(StockVoucherKind.Entry, voucher.Kind);
        Assert.Single(voucher.Lines);
        Assert.Equal("BE BE-2026-000001", voucher.StockMovementReference);
    }

    [Fact]
    public void NumberingTypes_EntryAndIssue_AreIndependent()
    {
        Assert.Equal(NumberingDocumentType.StockEntry, StockVoucherKind.Entry.ToNumberingDocumentType());
        Assert.Equal(NumberingDocumentType.StockIssue, StockVoucherKind.Issue.ToNumberingDocumentType());
        Assert.Equal("BE", NumberingDocumentType.StockEntry.DefaultFreeText());
        Assert.Equal("BS", NumberingDocumentType.StockIssue.DefaultFreeText());
        Assert.NotEqual(
            NumberingDocumentType.PurchaseReceipt,
            NumberingDocumentType.StockEntry);
        Assert.NotEqual(
            NumberingDocumentType.DeliveryNote,
            NumberingDocumentType.StockIssue);
    }

    [Fact]
    public void Create_InactiveWarehouse_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH", "Entrepôt").Value;
        warehouse.Deactivate();
        var result = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            DateTime.Today,
            MovementReason.InitialStock);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithForbiddenReason_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH", "Entrepôt").Value;
        var number = StockVoucherNumber.Create("BE", 2026, 1);
        var result = StockVoucher.Create(
            number, StockVoucherKind.Entry, warehouse, DateTime.Today, MovementReason.Transfer);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_IssueWithSaleReason_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH", "Entrepôt").Value;
        var number = StockVoucherNumber.Create("BS", 2026, 1);
        var result = StockVoucher.Create(
            number, StockVoucherKind.Issue, warehouse, DateTime.Today, MovementReason.Sale);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AddLine_SameProductTwice_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH1", "Entrepôt Principal").Value;
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-1", "Article test", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, unit: "U", isStockManaged: true).Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            new DateTime(2026, 5, 15),
            MovementReason.InitialStock).Value;
        Assert.True(voucher.AddLine(product, 2m, 8m).IsSuccess);
        var duplicate = voucher.AddLine(product, 1m, 8m);
        Assert.True(duplicate.IsFailure);
    }

    [Fact]
    public void AddLine_NonStockManaged_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH1", "Entrepôt Principal").Value;
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var service = Product.Create(
            "SRV-1", "Service", ProductType.Service, Money.Create(10m), VatRate.Standard,
            category.Id, isStockManaged: false).Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            DateTime.Today,
            MovementReason.InitialStock).Value;
        var result = voucher.AddLine(service, 1m, 10m);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MarkValidated_WhenDraftWithLines_ShouldSucceed()
    {
        var voucher = BuildDraftEntry();
        var result = voucher.MarkValidated();
        Assert.True(result.IsSuccess);
        Assert.Equal(StockVoucherStatus.Validated, voucher.Status);
        Assert.NotNull(voucher.ValidatedAt);
    }

    [Fact]
    public void MarkValidated_WhenNoLines_ShouldFail()
    {
        var warehouse = Warehouse.Create("WH", "Entrepôt").Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            DateTime.Today,
            MovementReason.InitialStock).Value;
        var result = voucher.MarkValidated();
        Assert.True(result.IsFailure);
        Assert.Equal(StockVoucherStatus.Draft, voucher.Status);
    }

    [Fact]
    public void UpdateHeader_WhenValidated_ShouldFail()
    {
        var voucher = BuildDraftEntry();
        Assert.True(voucher.MarkValidated().IsSuccess);
        var result = voucher.UpdateHeader(DateTime.Today, voucher.WarehouseId, MovementReason.FoundOrOther, null, null);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Cancel_Draft_ShouldSucceedWithoutWasValidatedEventPayload()
    {
        var voucher = BuildDraftEntry();
        var result = voucher.Cancel("Erreur de saisie");
        Assert.True(result.IsSuccess);
        Assert.Equal(StockVoucherStatus.Cancelled, voucher.Status);
        Assert.Equal("Erreur de saisie", voucher.CancellationReason);
    }

    [Fact]
    public void Cancel_Validated_ShouldSucceed()
    {
        var voucher = BuildDraftEntry();
        Assert.True(voucher.MarkValidated().IsSuccess);
        var result = voucher.Cancel("Erreur de saisie");
        Assert.True(result.IsSuccess);
        Assert.Equal(StockVoucherStatus.Cancelled, voucher.Status);
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldFail()
    {
        var voucher = BuildDraftEntry();
        var result = voucher.Cancel("  ");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void IssueVoucher_InternalUse_ShouldSucceed()
    {
        var warehouse = Warehouse.Create("WH1", "Entrepôt Principal").Value;
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-2", "Article 2", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, isStockManaged: true).Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BS", 2026, 3),
            StockVoucherKind.Issue,
            warehouse,
            DateTime.Today,
            MovementReason.InternalUse).Value;
        Assert.True(voucher.AddLine(product, 1m, 12.5m).IsSuccess);
        Assert.Equal("BS BS-2026-000003", voucher.StockMovementReference);
        Assert.Equal("ANNUL BS BS-2026-000003", voucher.StockReversalReference);
    }

    private static StockVoucher BuildDraftEntry()
    {
        var warehouse = Warehouse.Create("WH1", "Entrepôt Principal").Value;
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-1", "Article test", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, unit: "U", isStockManaged: true).Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            new DateTime(2026, 5, 15),
            MovementReason.InitialStock,
            externalReference: "INV-INIT").Value;
        Assert.True(voucher.AddLine(product, 2m, 8m).IsSuccess);
        return voucher;
    }
}
