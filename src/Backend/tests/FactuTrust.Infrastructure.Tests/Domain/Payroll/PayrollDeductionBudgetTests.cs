using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// R-22 : allocation budgétaire des retenues volontaires quand le net disponible est insuffisant.
/// Les retenues sont servies par ordre de priorité (avantage en nature &gt; mutuelle &gt; prêt &gt; avance…)
/// et le reliquat est reporté au mois suivant via RequestedAmount/CarriedOverAmount — au lieu
/// d'écrêter le net à zéro tout en gardant le montant intégral des lignes. L'avance supporte un
/// règlement partiel : le reliquat reste à retenir sur un cycle ultérieur.
/// </summary>
public sealed class PayrollDeductionBudgetTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    [Fact]
    public void BudgetExhausted_HigherPriorityServedFirst_LowerPriorityPartiallyCarriedOver()
    {
        // BaseSalary 300 → net disponible (avant retenues) = 270,960. Retenues demandées = 300.
        // Mutuelle (priorité 1) servie en entier (100) ; avance (priorité 3) partiellement prélevée
        // (170,960) et le reliquat (29,040) reporté. Net écrêté à 0, jamais négatif.
        var input = new PayrollComputationInput
        {
            BaseSalary = 300m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            DeductionLines = new[]
            {
                new DeductionLineInput("Mutuelle", 100m, DeductionKind.MutuelleEmployee),
                new DeductionLineInput("Avance", 200m, DeductionKind.Advance, SourceEntityId: Guid.NewGuid())
            }
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.True(c.HasPartialDeductions);
        Assert.Equal(29.040m, c.PartialDeductionCarryOver);
        Assert.Equal(0m, c.NetSalary); // écrêté, jamais négatif

        var advanceLine = c.Lines.Single(l => l.DeductionKind == DeductionKind.Advance);
        Assert.Equal(170.960m, advanceLine.Amount);           // effectivement prélevé
        Assert.Equal(200m, advanceLine.RequestedAmount);      // initialement demandé
        Assert.Equal(29.040m, advanceLine.CarriedOverAmount); // reporté au mois suivant

        var mutuelleLine = c.Lines.Single(l => l.DeductionKind == DeductionKind.MutuelleEmployee);
        Assert.Equal(100m, mutuelleLine.Amount);
        Assert.Null(mutuelleLine.RequestedAmount);    // intégralement servie → ligne inchangée
        Assert.Null(mutuelleLine.CarriedOverAmount);
    }

    [Fact]
    public void BudgetSufficient_NoCarryOver_NoPartialFlag()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            DeductionLines = new[]
            {
                new DeductionLineInput("Mutuelle", 100m, DeductionKind.MutuelleEmployee),
                new DeductionLineInput("Avance", 200m, DeductionKind.Advance)
            }
        };

        var c = PayrollCalculator.Compute(input, Params());

        Assert.False(c.HasPartialDeductions);
        Assert.Equal(0m, c.PartialDeductionCarryOver);
    }

    [Fact]
    public void Advance_SettlePartial_LeavesReliquatUntilFullySettled()
    {
        var advance = EmployeeAdvance.Create(Guid.NewGuid(), new DateTime(2026, 1, 1), 500m).Value;

        Assert.True(advance.SettlePartial(Guid.NewGuid(), 300m).IsSuccess);
        Assert.Equal(300m, advance.SettledAmount);
        Assert.Equal(200m, advance.RemainingAmount);
        Assert.False(advance.IsSettled);

        // Règlement du reliquat → avance soldée.
        Assert.True(advance.SettlePartial(Guid.NewGuid(), 200m).IsSuccess);
        Assert.Equal(500m, advance.SettledAmount);
        Assert.Equal(0m, advance.RemainingAmount);
        Assert.True(advance.IsSettled);
    }

    [Fact]
    public void Advance_SettlePartial_OverReliquat_FailsAndLeavesAdvanceUnchanged()
    {
        var advance = EmployeeAdvance.Create(Guid.NewGuid(), new DateTime(2026, 1, 1), 500m).Value;

        var over = advance.SettlePartial(Guid.NewGuid(), 600m);

        Assert.True(over.IsFailure);
        Assert.Equal(0m, advance.SettledAmount);
        Assert.Equal(500m, advance.RemainingAmount);
        Assert.False(advance.IsSettled);
    }

    [Fact]
    public void Advance_SettlePartial_OnAlreadySettled_Fails()
    {
        var advance = EmployeeAdvance.Create(Guid.NewGuid(), new DateTime(2026, 1, 1), 500m).Value;
        Assert.True(advance.SettlePartial(Guid.NewGuid(), 500m).IsSuccess);
        Assert.True(advance.IsSettled);

        var second = advance.SettlePartial(Guid.NewGuid(), 1m);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public void Advance_SettlePartial_NonPositiveAmount_Fails()
    {
        var advance = EmployeeAdvance.Create(Guid.NewGuid(), new DateTime(2026, 1, 1), 500m).Value;

        Assert.True(advance.SettlePartial(Guid.NewGuid(), 0m).IsFailure);
    }

    [Fact]
    public void GarnishmentBudgetReduction_PreservesSeizableCapCarry()
    {
        // L2 : une saisie réduite par le cap saisisseur (RequestedAmount=60, Amount=50, cap carry=10)
        // puis à nouveau réduite par l'insuffisance du net doit CUMULER les deux reports (cap + budget),
        // non écraser le report de cap. BaseSalary 300 → net disponible avant retenues = 270,960 ; une
        // mutuelle de 226 laisse 44,960 au post-impôt, insuffisant pour la saisie de 50.
        // Report attendu = 60 − 44,960 = 15,040 (10 de cap + 5,040 de budget). L'ancien code
        // (CarriedOver = Amount − applied) donnait 5,040 et perdait le report de cap saisisseur.
        var input = new PayrollComputationInput
        {
            BaseSalary = 300m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            DeductionLines = new[]
            {
                new DeductionLineInput("Mutuelle", 226m, DeductionKind.MutuelleEmployee)
            },
            PostTaxDeductionLines = new[]
            {
                new DeductionLineInput("Saisie — Créancier X", 50m, DeductionKind.Garnishment,
                    SourceEntityId: Guid.NewGuid(), RequestedAmount: 60m, CarriedOverAmount: 10m)
            }
        };

        var c = PayrollCalculator.Compute(input, Params());

        var garnishmentLine = c.Lines.Single(l => l.DeductionKind == DeductionKind.Garnishment);
        Assert.Equal(60m, garnishmentLine.RequestedAmount);
        Assert.True(garnishmentLine.Amount < 50m);                                        // réduite par le budget
        // Report cumulé : cap carry (10) + budget carry (50 − applied) = RequestedAmount − applied.
        Assert.Equal(
            Math.Round(60m - garnishmentLine.Amount, 3, MidpointRounding.AwayFromZero),
            garnishmentLine.CarriedOverAmount);
        Assert.True(garnishmentLine.CarriedOverAmount > 10m);                             // cap carry préservé, non écrasé
        Assert.Equal(15.040m, garnishmentLine.CarriedOverAmount);                         // 60 − 44,960
    }
}
