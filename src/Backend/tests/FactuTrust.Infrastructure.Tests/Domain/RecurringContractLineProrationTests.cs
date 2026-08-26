using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Tests d'équivalence de l'extraction T1 : <see cref="RecurringContractLineProration.Prorate"/>
/// reproduit à l'identique l'ancienne logique privée de RecurringContractBillingService
/// (branche bornes contrat prioritaire, puis bornes de la ligne, sinon plein tarif).
/// </summary>
public sealed class RecurringContractLineProrationTests
{
    private static readonly DateTime PeriodFrom = new(2026, 1, 1);
    private static readonly DateTime PeriodTo = new(2026, 1, 31); // 31 jours

    [Fact]
    public void Prorate_FullPeriodLine_ReturnsFullAmount()
    {
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: new DateTime(2026, 1, 1), contractEnd: null,
            lineEffectiveFrom: new DateTime(2026, 1, 1), lineEffectiveTo: null,
            fullAmount: 100m);

        Assert.Equal(100m, amount);
    }

    [Fact]
    public void Prorate_LineStartingMidPeriod_ProratesFromEffectiveFrom()
    {
        // Ligne effective du 16/01 au 31/01 → 16 jours sur 31.
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: PeriodFrom, contractEnd: null,
            lineEffectiveFrom: new DateTime(2026, 1, 16), lineEffectiveTo: null,
            fullAmount: 100m);

        Assert.Equal(51.613m, amount); // round(100 × 16/31, 3)
    }

    [Fact]
    public void Prorate_ContractEndingMidPeriod_ProratesToContractEnd()
    {
        // Contrat se terminant le 15/01 → 15 jours sur 31 (borne contrat prioritaire).
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: PeriodFrom, contractEnd: new DateTime(2026, 1, 15),
            lineEffectiveFrom: PeriodFrom, lineEffectiveTo: null,
            fullAmount: 100m);

        Assert.Equal(48.387m, amount); // round(100 × 15/31, 3)
    }

    [Fact]
    public void Prorate_ContractBoundsTakePrecedenceOverLineBounds()
    {
        // Verrouille la priorité de la branche contrat (comportement historique) :
        // ligne effective depuis le 20/01 mais contrat démarrant le 10/01 → on proratise
        // sur les bornes contrat (10/01→31/01 = 22 jours), pas sur les bornes de la ligne.
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: new DateTime(2026, 1, 10), contractEnd: null,
            lineEffectiveFrom: new DateTime(2026, 1, 20), lineEffectiveTo: null,
            fullAmount: 100m);

        Assert.Equal(70.968m, amount); // round(100 × 22/31, 3)
    }

    [Fact]
    public void Prorate_ZeroAmount_ReturnsZero()
    {
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: new DateTime(2026, 1, 10), contractEnd: null,
            lineEffectiveFrom: PeriodFrom, lineEffectiveTo: null,
            fullAmount: 0m);

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void Prorate_RoundsToThreeDecimals_AwayFromZero()
    {
        // 3 jours actifs sur 7 → 10 × 3/7 = 4,2857… → 4,286 (AwayFromZero, comme le domaine).
        var amount = RecurringContractLineProration.Prorate(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 7),
            contractStart: new DateTime(2026, 3, 1), contractEnd: null,
            lineEffectiveFrom: new DateTime(2026, 3, 5), lineEffectiveTo: null,
            fullAmount: 10m);

        Assert.Equal(4.286m, amount);
    }

    [Fact]
    public void Prorate_LineEndingMidPeriod_ProratesToEffectiveTo()
    {
        // Ligne clôturée au 10/01 (avenant) → 10 jours sur 31.
        var amount = RecurringContractLineProration.Prorate(
            PeriodFrom, PeriodTo,
            contractStart: PeriodFrom, contractEnd: null,
            lineEffectiveFrom: PeriodFrom, lineEffectiveTo: new DateTime(2026, 1, 10),
            fullAmount: 100m);

        Assert.Equal(32.258m, amount); // round(100 × 10/31, 3)
    }
}
