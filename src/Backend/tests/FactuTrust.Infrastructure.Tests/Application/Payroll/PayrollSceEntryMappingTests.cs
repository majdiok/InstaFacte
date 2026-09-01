using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Imputation des lignes de l'OD de paie sous le profil SCE 2026, comparée au profil historique.
/// Les assertions Legacy verrouillent l'absence de régression sur les dossiers non basculés.
/// </summary>
public sealed class PayrollSceEntryMappingTests
{
    private static PayrollJournalEntryAccountMap Map(PayrollAccountProfile profile) =>
        new AccountingSettings
        {
            PayrollAccountProfile = profile,
            PayrollAccountProfileEffectiveDate = profile == PayrollAccountProfile.Sce2026
                ? new DateTime(2026, 1, 1)
                : null
        }.BuildPayrollAccountMap(profile);

    /// <summary>Cycle portant un avantage en nature (300) et sa compensation (300), plus une avance (100).</summary>
    private static PayrollRun BuildRunWithInKindBenefit()
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            TaxableCnssableAllowances = 300m,
            AllowanceLines = new[]
            {
                new AllowanceLineInput("Avantage en nature véhicule", 300m, true, true, EarningKind.InKindBenefit)
            },
            DeductionLines = new[]
            {
                new DeductionLineInput("Compensation avantage en nature", 300m, DeductionKind.InKindBenefitOffset),
                new DeductionLineInput("Avance sur salaire", 100m, DeductionKind.Advance, SourceEntityId: Guid.NewGuid())
            }
        };

        var computation = PayrollCalculator.Compute(input, pars);
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);
        run.SetPayslips([Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate)]);
        return run;
    }

    /// <summary>Cycle mêlant salaire de base, heures supplémentaires et prime.</summary>
    private static PayrollRun BuildRunWithMixedEarnings()
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            TaxableCnssableAllowances = 500m,
            AllowanceLines = new[]
            {
                new AllowanceLineInput("Heures supplémentaires", 200m, true, true, EarningKind.Overtime),
                new AllowanceLineInput("Prime de rendement", 300m, true, true, EarningKind.Bonus)
            },
            DeductionLines = new[]
            {
                new DeductionLineInput("Avance sur salaire", 100m, DeductionKind.Advance, SourceEntityId: Guid.NewGuid())
            }
        };

        var computation = PayrollCalculator.Compute(input, pars);
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);
        run.SetPayslips([Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate)]);
        return run;
    }

    private static void AssertBalanced(IReadOnlyList<JournalLineInput> lines) =>
        Assert.Equal(0m, Math.Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit), 3));

    // ── Compensation d'avantage en nature ───────────────────────────────────────────────────

    [Fact]
    public void Sce2026_CreditsInKindOffsetTo4286_NotToTheAdvancesAccount()
    {
        var run = BuildRunWithInKindBenefit();

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Sce2026), null, PayrollAccountProfile.Sce2026);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var lines = result.Value;
        AssertBalanced(lines);

        // L'avantage sort du brut vers 6404, sa compensation va au 4286 « Personnel - autres charges
        // à payer » — et surtout pas au 421, qui est une créance sur le salarié.
        Assert.Equal(300m, lines.Where(l => l.AccountNumber == "6404").Sum(l => l.Debit));
        Assert.Equal(300m, lines.Where(l => l.AccountNumber == "4286").Sum(l => l.Credit));
        Assert.DoesNotContain(lines, l => l.AccountNumber == "4386");

        // Seule l'avance (100) reste au 421.
        Assert.Equal(100m, lines.Where(l => l.AccountNumber == "421").Sum(l => l.Credit));
    }

    [Fact]
    public void Legacy_KeepsInKindOffsetOn421()
    {
        var run = BuildRunWithInKindBenefit();

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Legacy), null, PayrollAccountProfile.Legacy);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var lines = result.Value;
        AssertBalanced(lines);

        // Imputation historique inchangée : AN dans le brut 640, compensation et avance au 421.
        Assert.DoesNotContain(lines, l => l.AccountNumber == "6404");
        Assert.DoesNotContain(lines, l => l.AccountNumber == "4286");
        Assert.Equal(400m, lines.Where(l => l.AccountNumber == "421").Sum(l => l.Credit));
    }

    // ── Taxes sur salaires ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Sce2026_BooksPayrollTaxesTo6611_6612_And437()
    {
        var run = BuildRunWithMixedEarnings();

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Sce2026), null, PayrollAccountProfile.Sce2026);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var lines = result.Value;
        AssertBalanced(lines);

        Assert.Equal(run.TotalTfp, lines.Where(l => l.AccountNumber == "6611").Sum(l => l.Debit));
        Assert.Equal(run.TotalFoprolos, lines.Where(l => l.AccountNumber == "6612").Sum(l => l.Debit));
        Assert.Equal(
            Math.Round(run.TotalTfp + run.TotalFoprolos + run.TotalCssEmployer, 3),
            lines.Where(l => l.AccountNumber == "437").Sum(l => l.Credit));

        // Les taxes ne polluent plus les charges sociales ni la retenue à la source.
        Assert.Equal(
            Math.Round(run.TotalCnssEmployer + run.TotalWorkAccident + run.TotalCssEmployer, 3),
            lines.Where(l => l.AccountNumber == "647").Sum(l => l.Debit));
        Assert.Equal(
            Math.Round(run.TotalIrpp + run.TotalCss, 3),
            lines.Where(l => l.AccountNumber == "432").Sum(l => l.Credit));
    }

    [Fact]
    public void Legacy_KeepsPayrollTaxesIn647And432()
    {
        var run = BuildRunWithMixedEarnings();

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Legacy), null, PayrollAccountProfile.Legacy);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var lines = result.Value;
        AssertBalanced(lines);

        Assert.DoesNotContain(lines, l => l.AccountNumber is "6611" or "6612" or "437");
        Assert.Equal(
            Math.Round(run.TotalCnssEmployer + run.TotalWorkAccident + run.TotalTfp
                       + run.TotalFoprolos + run.TotalCssEmployer, 3),
            lines.Where(l => l.AccountNumber == "647").Sum(l => l.Debit));
    }

    // ── Ventilation optionnelle du débit 640 ────────────────────────────────────────────────

    [Fact]
    public void SalarySplit_SpreadsGrossOverSubAccounts_AndStaysBalanced()
    {
        var run = BuildRunWithMixedEarnings();
        var salaryDebits = PayrollJournalEntryBuilder.ResolveSalaryDebits(run);

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Sce2026), null,
            PayrollAccountProfile.Sce2026, salaryDebits);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var lines = result.Value;
        AssertBalanced(lines);

        Assert.Equal(200m, lines.Where(l => l.AccountNumber == "6401").Sum(l => l.Debit));
        Assert.Equal(300m, lines.Where(l => l.AccountNumber == "6402").Sum(l => l.Debit));
        Assert.DoesNotContain(lines, l => l.AccountNumber == "640");

        // Le total des sous-comptes reste exactement le brut : le reliquat va au 6400.
        var salaryTotal = lines
            .Where(l => l.AccountNumber.StartsWith("640", StringComparison.Ordinal))
            .Sum(l => l.Debit);
        Assert.Equal(run.TotalGross, Math.Round(salaryTotal, 3));
    }

    [Fact]
    public void SalarySplit_IsIgnoredUnderLegacy()
    {
        var run = BuildRunWithMixedEarnings();
        var salaryDebits = PayrollJournalEntryBuilder.ResolveSalaryDebits(run);

        var result = PayrollJournalEntryBuilder.BuildLinesFromRun(
            run, "Paie 08/2026", Map(PayrollAccountProfile.Legacy), null,
            PayrollAccountProfile.Legacy, salaryDebits);

        Assert.True(result.IsSuccess);
        var lines = result.Value;

        // Le profil historique ignore la ventilation : le brut reste sur le compte collectif.
        Assert.Equal(run.TotalGross, lines.Where(l => l.AccountNumber == "640").Sum(l => l.Debit));
        Assert.DoesNotContain(lines, l => l.AccountNumber is "6401" or "6402");
    }

    [Fact]
    public void SalarySplit_FallsBackTo640_WhenEarningKindsAreMissing()
    {
        // Bulletins antérieurs à EarningKind : aucune ventilation fiable, donc aucune ventilation.
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var computation = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna }, pars);
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(SocialRegime.Rsna, pars);
        run.SetPayslips([Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate)]);

        foreach (var line in run.Payslips.SelectMany(p => p.Lines).Where(l => l.Kind == PayslipLineKind.Earning))
        {
            typeof(PayslipLine).GetProperty(nameof(PayslipLine.EarningKind))!
                .SetValue(line, null);
        }

        var salaryDebits = PayrollJournalEntryBuilder.ResolveSalaryDebits(run);

        Assert.Empty(salaryDebits);
    }
}
