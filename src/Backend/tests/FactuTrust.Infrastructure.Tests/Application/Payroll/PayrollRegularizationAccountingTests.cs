using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Écriture comptable de paie en présence d'une régularisation IRPP/CSS annuelle.
///
/// Le point critique : la régularisation transite par le compte 432 (État, retenues à la
/// source). Un cycle à dominante restitution rend ce bucket négatif, et l'ancien
/// <c>AddCredit</c> — qui ignore tout montant ≤ 0 — aurait supprimé la ligne en silence,
/// laissant passer une écriture déséquilibrée.
/// </summary>
public sealed class PayrollRegularizationAccountingTests
{
    private static readonly Guid PeriodId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollRun BuildRun(decimal irppRegularization, decimal cssRegularization = 0m)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            IrppRegularization = irppRegularization,
            CssRegularization = cssRegularization
        };

        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        var run = PayrollRun.Create(2026, 12, 2026).Value;
        var (employeeRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
        var payslip = Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 12, computation,
            employeeRate, employerRate);
        run.SetPayslips([payslip]);
        return run;
    }

    private static IReadOnlyList<JournalLineInput> BuildLines(PayrollRun run)
    {
        var result = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 12/2026",
            run.TotalIrppRegularization, run.TotalCssRegularization);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    /// <summary>L'écriture doit être équilibrée quel que soit le signe de la régularisation.</summary>
    private static void AssertBalanced(IReadOnlyList<JournalLineInput> lines)
    {
        var debits = lines.Sum(l => l.Debit);
        var credits = lines.Sum(l => l.Credit);
        Assert.Equal(debits, credits);

        var create = JournalEntry.Create(
            1, "JOD", new DateTime(2026, 12, 31), "Paie 12/2026", PeriodId, true,
            "PayrollRun", Guid.NewGuid(), lines, Money.DefaultCurrency);
        Assert.True(create.IsSuccess, create.IsFailure ? create.Error.Description : null);
    }

    [Fact]
    public void Rappel_CreditsStateAccountAndBalances()
    {
        var run = BuildRun(irppRegularization: 180m);
        var lines = BuildLines(run);

        var state = Assert.Single(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);
        Assert.True(state.Credit > 0, "Un rappel doit rester au crédit du 432.");
        Assert.Equal(0m, state.Debit);

        // Le rappel s'ajoute à la retenue mensuelle du même compte.
        var baseline = BuildLines(BuildRun(0m));
        var baselineState = baseline.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);
        Assert.Equal(baselineState.Credit + 180m, state.Credit);

        AssertBalanced(lines);
    }

    [Fact]
    public void RestitutionNette_MovesStateAccountToDebitAndStaysBalanced()
    {
        // Restitution largement supérieure à IRPP + CSS + TFP + FOPROLOS du mois : le bucket
        // 432 devient négatif. Sans AddSigned la ligne disparaîtrait et l'écriture serait
        // déséquilibrée du montant de la restitution.
        var run = BuildRun(irppRegularization: -500m);
        Assert.True(run.TotalIrppRegularization < 0);

        var lines = BuildLines(run);

        var state = Assert.Single(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);
        Assert.True(state.Debit > 0, "Une restitution nette doit basculer le 432 au débit.");
        Assert.Equal(0m, state.Credit);

        AssertBalanced(lines);
    }

    [Fact]
    public void RestitutionNette_IncreasesNetPay()
    {
        var baseline = BuildRun(0m);
        var withRestitution = BuildRun(-500m);

        Assert.Equal(baseline.TotalNet + 500m, withRestitution.TotalNet);
    }

    [Fact]
    public void RegularisationNulle_ProduitExactementLesMemesLignesQuAvant()
    {
        // Garde-fou de non-régression : sans régularisation, l'écriture est identique à
        // celle produite par l'appel historique (sans les nouveaux paramètres).
        var run = BuildRun(0m);

        var withNewParameters = BuildLines(run);
        var legacyResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 12/2026");

        Assert.True(legacyResult.IsSuccess);
        var legacy = legacyResult.Value;

        Assert.Equal(legacy.Count, withNewParameters.Count);
        foreach (var (expected, actual) in legacy.Zip(withNewParameters))
        {
            Assert.Equal(expected.AccountNumber, actual.AccountNumber);
            Assert.Equal(expected.Debit, actual.Debit);
            Assert.Equal(expected.Credit, actual.Credit);
        }

        AssertBalanced(withNewParameters);
    }

    [Fact]
    public void RegularisationCss_EstAgregeeAuMemeCompte()
    {
        var run = BuildRun(irppRegularization: 100m, cssRegularization: 25m);
        var lines = BuildLines(run);

        var state = Assert.Single(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);
        var baseline = BuildLines(BuildRun(0m));
        var baselineState = baseline.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);

        Assert.Equal(baselineState.Credit + 125m, state.Credit);
        AssertBalanced(lines);
    }
}
