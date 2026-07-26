using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Calcul d'échéancier d'emprunt. L'exigence structurante est l'INVARIANT D'ARRONDI :
/// Σ capital remboursé == capital emprunté (exactement) et solde final == 0, quelles que soient la
/// méthode, la périodicité et le taux. Un échéancier qui ne se solde pas est un bug.
/// </summary>
public sealed class LoanScheduleCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 31);

    private static IReadOnlyList<LoanInstallment> Build(
        decimal principal = 100_000m,
        decimal rate = 7.5m,
        int count = 24,
        LoanPeriodicity periodicity = LoanPeriodicity.Monthly,
        LoanAmortizationMethod method = LoanAmortizationMethod.ConstantAnnuity)
        => LoanScheduleCalculator.Build(principal, rate, count, periodicity, method, Start);

    // ── Invariant central : l'échéancier se solde exactement ───────────────────

    [Theory]
    [InlineData(LoanAmortizationMethod.ConstantAnnuity, LoanPeriodicity.Monthly, 24, 7.5)]
    [InlineData(LoanAmortizationMethod.ConstantAnnuity, LoanPeriodicity.Quarterly, 20, 6.25)]
    [InlineData(LoanAmortizationMethod.ConstantAnnuity, LoanPeriodicity.Annual, 7, 9.0)]
    [InlineData(LoanAmortizationMethod.ConstantPrincipal, LoanPeriodicity.Monthly, 36, 8.4)]
    [InlineData(LoanAmortizationMethod.ConstantPrincipal, LoanPeriodicity.SemiAnnual, 10, 5.0)]
    [InlineData(LoanAmortizationMethod.ConstantAnnuity, LoanPeriodicity.Monthly, 13, 3.33)]
    public void Schedule_AlwaysSettlesExactly(
        LoanAmortizationMethod method, LoanPeriodicity periodicity, int count, double rate)
    {
        const decimal principal = 137_500.750m;   // montant volontairement « sale » au millime

        var schedule = LoanScheduleCalculator.Build(principal, (decimal)rate, count, periodicity, method, Start);

        Assert.Equal(count, schedule.Count);
        Assert.Equal(principal, schedule.Sum(i => i.PrincipalAmount));   // égalité EXACTE
        Assert.Equal(0m, schedule[^1].ClosingBalance);
    }

    [Fact]
    public void Schedule_ChainsBalancesContinuously()
    {
        var schedule = Build();

        Assert.Equal(100_000m, schedule[0].OpeningBalance);
        for (var i = 1; i < schedule.Count; i++)
        {
            // Le solde de clôture d'une échéance est l'ouverture de la suivante — aucune rupture.
            Assert.Equal(schedule[i - 1].ClosingBalance, schedule[i].OpeningBalance);
        }
    }

    [Fact]
    public void Installment_EqualsPrincipalPlusInterest_OnEveryLine()
    {
        foreach (var line in Build())
            Assert.Equal(line.InstallmentAmount, line.PrincipalAmount + line.InterestAmount);
    }

    // ── Conformité des formules ────────────────────────────────────────────────

    [Fact]
    public void ConstantAnnuity_MatchesTheoreticalFormula()
    {
        // Cas de référence : 100 000 TND, 12 % annuel, 12 mensualités → i = 1 % ; A ≈ 8 884,879.
        var schedule = LoanScheduleCalculator.Build(
            100_000m, 12m, 12, LoanPeriodicity.Monthly, LoanAmortizationMethod.ConstantAnnuity, Start);

        // Les 11 premières échéances portent l'annuité théorique (la 12ᵉ absorbe l'arrondi).
        foreach (var line in schedule.Take(11))
            Assert.InRange(line.InstallmentAmount, 8_884.5m, 8_885.5m);

        // Premier intérêt = 100 000 × 1 % = 1 000 exactement.
        Assert.Equal(1_000m, schedule[0].InterestAmount);
    }

    [Fact]
    public void ConstantPrincipal_KeepsPrincipalFlat_AndDecreasesInstallments()
    {
        var schedule = LoanScheduleCalculator.Build(
            120_000m, 6m, 12, LoanPeriodicity.Monthly, LoanAmortizationMethod.ConstantPrincipal, Start);

        // Capital constant sur toutes les échéances sauf la dernière (absorption d'arrondi).
        foreach (var line in schedule.Take(11))
            Assert.Equal(10_000m, line.PrincipalAmount);

        // L'annuité décroît puisque l'intérêt porte sur un capital restant qui diminue.
        Assert.True(schedule[0].InstallmentAmount > schedule[^1].InstallmentAmount);
    }

    // ── Cas limites ────────────────────────────────────────────────────────────

    [Fact]
    public void ZeroRate_ProducesNoInterest_AndEvenPrincipal()
    {
        var schedule = LoanScheduleCalculator.Build(
            60_000m, 0m, 12, LoanPeriodicity.Monthly, LoanAmortizationMethod.ConstantAnnuity, Start);

        Assert.All(schedule, l => Assert.Equal(0m, l.InterestAmount));
        Assert.All(schedule, l => Assert.Equal(5_000m, l.PrincipalAmount));
        Assert.Equal(0m, schedule[^1].ClosingBalance);
    }

    [Fact]
    public void SingleInstallment_RepaysEverythingAtOnce()
    {
        var schedule = LoanScheduleCalculator.Build(
            50_000m, 10m, 1, LoanPeriodicity.Annual, LoanAmortizationMethod.ConstantAnnuity, Start);

        var only = Assert.Single(schedule);
        Assert.Equal(50_000m, only.PrincipalAmount);
        Assert.Equal(5_000m, only.InterestAmount);        // 50 000 × 10 %
        Assert.Equal(55_000m, only.InstallmentAmount);
        Assert.Equal(0m, only.ClosingBalance);
    }

    [Fact]
    public void DueDates_FollowPeriodicityStep()
    {
        var monthly = LoanScheduleCalculator.Build(
            10_000m, 5m, 3, LoanPeriodicity.Monthly, LoanAmortizationMethod.ConstantPrincipal, new DateTime(2026, 1, 15));
        var quarterly = LoanScheduleCalculator.Build(
            10_000m, 5m, 3, LoanPeriodicity.Quarterly, LoanAmortizationMethod.ConstantPrincipal, new DateTime(2026, 1, 15));

        Assert.Equal(new DateTime(2026, 2, 15), monthly[0].DueDate);
        Assert.Equal(new DateTime(2026, 4, 15), monthly[2].DueDate);
        Assert.Equal(new DateTime(2026, 4, 15), quarterly[0].DueDate);
        Assert.Equal(new DateTime(2026, 10, 15), quarterly[2].DueDate);
    }

    // ── Gardes défensives ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void NonPositivePrincipal_Throws(decimal principal)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Build(principal: principal));

    [Fact]
    public void NegativeRate_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Build(rate: -1m));

    [Fact]
    public void ZeroInstallments_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Build(count: 0));
}
