using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollProrataCalculatorTests
{
    private const decimal BaseSalary = 2600m; // 100 TND/jour à 26 jours

    [Fact]
    public void Compute_Disabled_ReturnsZeroDeduction()
    {
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 15),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = false
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.Equal(0m, result.DeductionAmount);
        Assert.Equal(26m, result.WorkedDays);
        Assert.Equal(0m, result.NonWorkedDays);
    }

    [Fact]
    public void Compute_FullMonth_NoSuspension_ReturnsZeroDeduction()
    {
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 1),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.Equal(0m, result.DeductionAmount);
        Assert.Equal(26m, result.WorkedDays);
        Assert.Equal(PayrollProrataReason.None, result.Reason);
    }

    [Fact]
    public void Compute_MidMonthHire_March2026_DeductsCorrectly()
    {
        // Embauche le 16 mars 2026 (lundi) : 12 jours ouvrables sur 22 dans le mois
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 16),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true
        };

        var result = PayrollProrataCalculator.Compute(input);

        var fullMonth = PayrollWorkingDaysCounter.CountWeekdaysInMonth(2026, 3);
        var worked = PayrollWorkingDaysCounter.CountWeekdays(new DateTime(2026, 3, 16), new DateTime(2026, 3, 31));
        var expectedWorked = Math.Round(26m * worked / fullMonth, 2, MidpointRounding.AwayFromZero);
        var expectedNonWorked = 26m - expectedWorked;
        var expectedDeduction = Math.Round(BaseSalary / 26m * expectedNonWorked, 3, MidpointRounding.AwayFromZero);

        Assert.Equal(expectedWorked, result.WorkedDays);
        Assert.Equal(expectedNonWorked, result.NonWorkedDays);
        Assert.Equal(expectedDeduction, result.DeductionAmount);
        Assert.Equal(PayrollProrataReason.Hire, result.Reason);
    }

    [Fact]
    public void Compute_MidMonthDeparture_March2026_DeductsCorrectly()
    {
        // Départ le 15 mars 2026 (dimanche) → dernier jour ouvré = 13 mars (vendredi)
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 1),
            EffectiveEnd = new DateTime(2026, 3, 15),
            IsEnabled = true
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.True(result.DeductionAmount > 0);
        Assert.Equal(PayrollProrataReason.Departure, result.Reason);
        Assert.True(result.NonWorkedDays > 0);
    }

    [Fact]
    public void Compute_UnpaidSuspensionFiveWeekdays_ReducesWorkedDays()
    {
        // R-23 (CAL-010) : mars 2026 compte 22 jours ouvrables (≠ 26). Les 5 jours ouvrables de
        // suspension non payée (2-6 mars) doivent être ramenés à la convention 26 jours avant
        // soustraction : 5 × 26 / 22 = 5,909... → 5,91 jours « paie » déduits (et non 5 bruts),
        // sinon la retenue est systématiquement sous-évaluée dans les mois ≠ 26 jours ouvrables.
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 1),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true,
            Suspensions =
            [
                new PayrollProrataSuspensionPeriod(
                    new DateTime(2026, 3, 2),
                    new DateTime(2026, 3, 6),
                    IsPaid: false,
                    IsApproved: true)
            ]
        };

        var result = PayrollProrataCalculator.Compute(input);

        var fullMonth = PayrollWorkingDaysCounter.CountWeekdaysInMonth(2026, 3);
        Assert.Equal(22, fullMonth); // garde-fou : mars 2026 = 22 jours ouvrables

        var suspendedPayrollDays = Math.Round(26m * 5m / fullMonth, 2, MidpointRounding.AwayFromZero);
        var expectedWorked = 26m - suspendedPayrollDays;
        var expectedNonWorked = Math.Round(26m - expectedWorked, 2, MidpointRounding.AwayFromZero);
        var expectedDeduction = Math.Round(BaseSalary / 26m * expectedNonWorked, 3, MidpointRounding.AwayFromZero);

        Assert.Equal(expectedWorked, result.WorkedDays);
        Assert.Equal(expectedNonWorked, result.NonWorkedDays);
        Assert.Equal(20.09m, result.WorkedDays);
        Assert.Equal(5.91m, result.NonWorkedDays);
        Assert.Equal(591.000m, result.DeductionAmount);
        Assert.Equal(PayrollProrataReason.Suspension, result.Reason);
    }

    [Fact]
    public void Compute_PaidSuspension_HasNoImpact()
    {
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 1),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true,
            Suspensions =
            [
                new PayrollProrataSuspensionPeriod(
                    new DateTime(2026, 3, 2),
                    new DateTime(2026, 3, 6),
                    IsPaid: true,
                    IsApproved: true)
            ]
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.Equal(0m, result.DeductionAmount);
        Assert.Equal(26m, result.WorkedDays);
    }

    [Fact]
    public void Compute_HireAndSuspensionSameMonth_CombinedReason()
    {
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 3, 16),
            EffectiveEnd = new DateTime(2026, 3, 31),
            IsEnabled = true,
            Suspensions =
            [
                new PayrollProrataSuspensionPeriod(
                    new DateTime(2026, 3, 23),
                    new DateTime(2026, 3, 27),
                    IsPaid: false,
                    IsApproved: true)
            ]
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.True(result.DeductionAmount > 0);
        Assert.Equal(PayrollProrataReason.Combined, result.Reason);
    }

    [Fact]
    public void Compute_NoPresenceInMonth_FullDeduction()
    {
        var input = new PayrollProrataMonthInput
        {
            Year = 2026,
            Month = 3,
            BaseSalary = BaseSalary,
            EffectiveStart = new DateTime(2026, 2, 1),
            EffectiveEnd = new DateTime(2026, 2, 28),
            IsEnabled = true
        };

        var result = PayrollProrataCalculator.Compute(input);

        Assert.Equal(0m, result.WorkedDays);
        Assert.Equal(26m, result.NonWorkedDays);
        Assert.Equal(BaseSalary, result.DeductionAmount);
    }
}
