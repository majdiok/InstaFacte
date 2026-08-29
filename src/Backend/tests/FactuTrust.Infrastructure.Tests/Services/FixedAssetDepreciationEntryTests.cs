using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Écriture de dotation d'amortissement (<see cref="AccountingService.GenerateFixedAssetDepreciationEntryAsync"/>)
/// — plan T3 : date de l'écriture paramétrable (défaut inchangé : 31/12 de l'exercice de la ligne ;
/// le flux de cession — T4 — passe une date explicite).
/// </summary>
public sealed class FixedAssetDepreciationEntryTests
{
    private static readonly Guid CategoryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002");

    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static (AccountingService service, List<JournalEntry> captured, Mock<IAccountingPeriodService> periodServiceMock) BuildService(
        JournalEntry? existingBySource = null)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) =>
                Result.Success(AccountingPeriod.Create(d.Year, d.Month, new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1))));

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(AccountingService.SourceFixedAssetDepreciation, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBySource);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { BrouillardEnabled = false });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, captured, periodService);
    }

    private static FixedAsset CreateInServiceAsset()
    {
        var create = FixedAsset.Create(
            "IMMO-2026-0002", "Actif test", CategoryId, 20m, 5m,
            "2244", "2824", "68112", 40_000m, 0m, 0m, new DateTime(2026, 1, 15));
        var asset = create.Value;
        asset.PutInService(new DateTime(2026, 1, 15), "404");
        return asset;
    }

    private static DepreciationScheduleLine CreateLine(Guid assetId, int fiscalYear, decimal amount) =>
        DepreciationScheduleLine.Create(assetId, fiscalYear, null, 40_000m, amount, 0m, amount, amount, 40_000m - amount).Value;

    [Fact]
    public async Task NoEntryDate_EntryIsDatedDecember31OfFiscalYear_NonRegression()
    {
        var asset = CreateInServiceAsset();
        var line = CreateLine(asset.Id, 2026, 8_000m);
        var (service, captured, periodServiceMock) = BuildService();

        var result = await service.GenerateFixedAssetDepreciationEntryAsync(asset, line, cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Description);
        var entry = Assert.Single(captured);
        entry.EntryDate.Should().Be(new DateTime(2026, 12, 31));
        periodServiceMock.Verify(x => x.EnsureOpenPeriodAsync(new DateTime(2026, 12, 31), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplicitEntryDate_EntryIsDatedAtThatDate_UsedByDisposalFlow()
    {
        var asset = CreateInServiceAsset();
        var line = CreateLine(asset.Id, 2026, 3_000m);
        var (service, captured, periodServiceMock) = BuildService();
        var disposalDate = new DateTime(2026, 6, 15);

        var result = await service.GenerateFixedAssetDepreciationEntryAsync(asset, line, entryDate: disposalDate, cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Description);
        var entry = Assert.Single(captured);
        entry.EntryDate.Should().Be(disposalDate);
        periodServiceMock.Verify(x => x.EnsureOpenPeriodAsync(disposalDate, It.IsAny<CancellationToken>()), Times.Once);
    }
}
