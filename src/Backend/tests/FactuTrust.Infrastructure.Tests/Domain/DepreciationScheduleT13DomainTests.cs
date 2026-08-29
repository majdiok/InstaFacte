using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Domain T13/C6 : <see cref="DepreciationScheduleLine.Unpost"/> conserve le lien d'audit,
/// <see cref="DepreciationScheduleLine.UpdateAmounts"/> valide/refuse, <see cref="DepreciationScheduleLine.MarkPosted"/>
/// re-lie après dé-postage, et <see cref="FixedAsset.RecalculateDepreciationTotals"/> remet à jour le cumul.
/// </summary>
public sealed class DepreciationScheduleT13DomainTests
{
    private static readonly Guid AssetId = Guid.NewGuid();

    private static DepreciationScheduleLine CreateLine() =>
        DepreciationScheduleLine.Create(
            AssetId, 2026, null,
            openingNbv: 10_000m, normalAnnualAmount: 2_000m, priorAccumulatedDepreciation: 0m,
            depreciationAmount: 2_000m, accumulatedDepreciation: 2_000m, closingNbv: 8_000m).Value;

    // --- Unpost conserve le lien d'audit ---

    [Fact]
    public void Unpost_PreservesJournalEntryIdAndAccountingPeriodId()
    {
        var line = CreateLine();
        var entryId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        line.MarkPosted(entryId, periodId);

        line.Unpost();

        line.IsPosted.Should().BeFalse();
        line.JournalEntryId.Should().Be(entryId);
        line.AccountingPeriodId.Should().Be(periodId);
    }

    // --- UpdateAmounts ---

    [Fact]
    public void UpdateAmounts_RefusesWhenPosted()
    {
        var line = CreateLine();
        line.MarkPosted(Guid.NewGuid(), Guid.NewGuid());

        var result = line.UpdateAmounts(8_000m, 1_500m, 2_000m, 1_500m, 3_500m, 6_500m, null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateAmounts_RefusesNegativeAmount()
    {
        var line = CreateLine();

        var result = line.UpdateAmounts(10_000m, 2_000m, 0m, -1m, -1m, 10_001m, null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateAmounts_RefusesInvalidPeriodMonth()
    {
        var line = CreateLine();

        var result = line.UpdateAmounts(10_000m, 2_000m, 0m, 2_000m, 2_000m, 8_000m, 13);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateAmounts_UpdatesInPlace_WhenUnposted()
    {
        var line = CreateLine();
        var lineId = line.Id;

        var result = line.UpdateAmounts(8_000m, 1_500m, 2_000m, 1_500m, 3_500m, 6_500m, 6);

        result.IsSuccess.Should().BeTrue();
        line.Id.Should().Be(lineId, "l'identité de la ligne doit être préservée par le merge");
        line.DepreciationAmount.Should().Be(1_500m);
        line.AccumulatedDepreciation.Should().Be(3_500m);
        line.ClosingNbv.Should().Be(6_500m);
        line.PeriodMonth.Should().Be(6);
        line.IsPosted.Should().BeFalse();
    }

    // --- MarkPosted après Unpost re-lie au nouvel entryId ---

    [Fact]
    public void MarkPosted_AfterUnpost_ReLinksToNewEntryId()
    {
        var line = CreateLine();
        var firstEntry = Guid.NewGuid();
        var firstPeriod = Guid.NewGuid();
        line.MarkPosted(firstEntry, firstPeriod);

        line.Unpost(); // conserve firstEntry (écriture extournée)

        line.IsPosted.Should().BeFalse();
        line.JournalEntryId.Should().Be(firstEntry);

        var newEntry = Guid.NewGuid();
        var newPeriod = Guid.NewGuid();
        line.MarkPosted(newEntry, newPeriod); // recomptabilisation → re-lien

        line.IsPosted.Should().BeTrue();
        line.JournalEntryId.Should().Be(newEntry, "le re-lien vers la nouvelle écriture active écrase l'ancien JournalEntryId");
        line.AccountingPeriodId.Should().Be(newPeriod);
    }
}

/// <summary>
/// Domain T13/C6 : <see cref="FixedAsset.RecalculateDepreciationTotals"/> — remise à jour du
/// snapshot cumul (amortissement cumulé, VNC, statut) après dé-postage des lignes extournées.
/// </summary>
public sealed class FixedAssetRecalculateTotalsTests
{
    private static FixedAsset CreateInServiceAsset()
    {
        var asset = FixedAsset.Create(
            "IMMO-2026-0001", "Machine test", Guid.NewGuid(),
            20m, 5m, "228", "2828", "68112",
            10_000m, 0m, 0m, new DateTime(2026, 1, 1)).Value;
        asset.PutInService(new DateTime(2026, 1, 1), "404");
        return asset;
    }

    [Fact]
    public void RecalculateTotals_ZeroAccumulated_ResetsFullyDepreciatedBackToInService()
    {
        var asset = CreateInServiceAsset();
        // Amorti entièrement (VNC = 0 = valeur résiduelle) → FullyDepreciated.
        asset.ApplyDepreciation(10_000m, 10_000m, 0m);
        asset.Status.Should().Be(FixedAssetStatus.FullyDepreciated);

        // Toutes les dotations ont été extournées → plus rien de comptabilisé.
        asset.RecalculateDepreciationTotals(0m);

        asset.AccumulatedDepreciation.Should().Be(0m);
        asset.NetBookValue.Should().Be(10_000m);
        asset.Status.Should().Be(FixedAssetStatus.InService, "l'actif n'est plus amorti après extourne de toutes les dotations");
    }

    [Fact]
    public void RecalculateTotals_FullAccumulated_MarksFullyDepreciated()
    {
        var asset = CreateInServiceAsset();
        asset.AccumulatedDepreciation.Should().Be(0m);

        asset.RecalculateDepreciationTotals(10_000m);

        asset.AccumulatedDepreciation.Should().Be(10_000m);
        asset.NetBookValue.Should().Be(0m);
        asset.Status.Should().Be(FixedAssetStatus.FullyDepreciated);
    }

    [Fact]
    public void RecalculateTotals_PartialAccumulated_KeepsInService()
    {
        var asset = CreateInServiceAsset();

        asset.RecalculateDepreciationTotals(4_000m);

        asset.AccumulatedDepreciation.Should().Be(4_000m);
        asset.NetBookValue.Should().Be(6_000m);
        asset.Status.Should().Be(FixedAssetStatus.InService);
    }
}
