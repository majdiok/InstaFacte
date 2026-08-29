using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Résolution, par <see cref="AccountingService.GenerateSupplierInvoiceEntryAsync"/>, de la
/// classification TVA-capitalisée par ligne facture fournisseur (plan T2.3) : catégorie chargée par
/// <see cref="IDepreciationRateCategoryRepository"/>, compte effectif = compte de la ligne ou défaut
/// de catégorie, TVA capitalisée décidée par <c>FixedAssetVatRules</c> — même résolution que
/// <c>CreateFixedAssetsFromSupplierInvoiceHandler</c>.
/// </summary>
public sealed class SupplierInvoiceFixedAssetClassificationTests
{
    private static readonly Guid VehPassCategoryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0001");

    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static (AccountingService service, List<JournalEntry> captured, Mock<IDepreciationRateCategoryRepository> categories) BuildService()
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(AccountingService.SourceSupplierInvoice, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings());

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, captured, categories);
    }

    private static SupplierInvoice BuildInvoiceWithAssetLine(string? assetAccountOnLine, Guid? categoryId)
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;

        var category = ProductCategory.Create("GEN", "Général").Value;
        var poNumber = PurchaseOrderNumber.Create("BC", 2026, 600001);
        var po = PurchaseOrder.Create(poNumber, supplier, new DateTime(2026, 4, 1)).Value;

        var unitPrice = Money.Create(50_000m, Money.DefaultCurrency);
        var product = Product.Create(
            "PR-VEH-1", "Véhicule de tourisme", ProductType.Product, unitPrice, VatRate.Standard,
            category.Id, purchasePrice: unitPrice).Value;

        var addLine = po.AddLine(product, 1m);
        if (addLine.IsFailure)
            throw new InvalidOperationException(addLine.Error.Description);

        var confirm = po.Confirm();
        if (confirm.IsFailure)
            throw new InvalidOperationException(confirm.Error.Description);

        var receiveLines = po.Lines.Select(l => (l.Id, l.Quantity)).ToArray();
        po.ReceiveGoods(receiveLines);

        var lineSelections = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po, "FS-2026-VEH", new DateTime(2026, 4, 5), lineSelections);
        if (invoice.IsFailure)
            throw new InvalidOperationException(invoice.Error.Description);

        var inv = invoice.Value;
        inv.ApplyLineAssetClassifications(new[]
        {
            (LineNumber: 1, IsFixedAsset: true, AssetAccountNumber: assetAccountOnLine, DepreciationRateCategoryId: categoryId)
        });

        return inv;
    }

    private static DepreciationRateCategory VehPassCategory() =>
        DepreciationRateCategory.Create("VEH_PASS", "Véhicule de tourisme", 20m, "2244", "2824", "68112", false, 1);

    [Fact]
    public async Task PassengerVehicleCategory_LineWithoutAccount_UsesCategoryDefault_CapitalizesVat()
    {
        var invoice = BuildInvoiceWithAssetLine(assetAccountOnLine: null, categoryId: VehPassCategoryId);
        var (service, captured, categories) = BuildService();
        categories.Setup(x => x.GetByIdAsync(VehPassCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VehPassCategory());

        var result = await service.GenerateSupplierInvoiceEntryAsync(invoice, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Description);
        var entry = Assert.Single(captured);
        entry.Lines.Should().NotContain(l => l.AccountNumber == "43662");
        var asset = entry.Lines.Single(l => l.AccountNumber == "2244");
        asset.DebitAmount.Amount.Should().Be(59_500m); // 50 000 HT + 9 500 TVA capitalisée
    }

    [Fact]
    public async Task NonAssetCategoryLine_NoDepreciationRateCategoryId_ResolvesViaOtherCode()
    {
        var invoice = BuildInvoiceWithAssetLine(assetAccountOnLine: "228", categoryId: null);
        var (service, captured, categories) = BuildService();
        var otherCategory = DepreciationRateCategory.Create("OTHER", "Autres", 10m, "228", "2828", "68112", false, 99);
        categories.Setup(x => x.GetByCodeAsync("OTHER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherCategory);

        var result = await service.GenerateSupplierInvoiceEntryAsync(invoice, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Description);
        var entry = Assert.Single(captured);
        entry.Lines.Should().Contain(l => l.AccountNumber == "43662");
        entry.Lines.Single(l => l.AccountNumber == "228").DebitAmount.Amount.Should().Be(50_000m);
        categories.Verify(x => x.GetByCodeAsync("OTHER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryNotResolvable_FallsBackToLegacyBehavior_NonRegression()
    {
        // Catégorie absente (GetByIdAsync renvoie null) → ligne omise de la classification :
        // le builder retombe sur son comportement historique (compte de la ligne, TVA sur 43662).
        var invoice = BuildInvoiceWithAssetLine(assetAccountOnLine: "224", categoryId: VehPassCategoryId);
        var (service, captured, categories) = BuildService();
        categories.Setup(x => x.GetByIdAsync(VehPassCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DepreciationRateCategory?)null);

        var result = await service.GenerateSupplierInvoiceEntryAsync(invoice, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Description);
        var entry = Assert.Single(captured);
        entry.Lines.Should().Contain(l => l.AccountNumber == "43662");
        entry.Lines.Single(l => l.AccountNumber == "224").DebitAmount.Amount.Should().Be(50_000m);
    }
}
