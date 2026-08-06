using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollAccountingEntryTests
{
    private static readonly Guid PeriodId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollRun BuildRun(PayrollComputationInput input, PayrollYearParameters? parameters = null)
    {
        var pars = parameters ?? Params();
        var computation = PayrollCalculator.Compute(input, pars);
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, empoyerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);
        var payslip = Payslip.FromComputation(
            run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 8, computation,
            empRate, empoyerRate);
        run.SetPayslips([payslip]);
        return run;
    }

    private static PayrollYearParameters ParamsWithSmigMode(SmigIrppExemptionMode mode)
    {
        var p = Params();
        var result = p.UpdateRates(
            p.CnssEmployeeRate, p.CnssEmployerRate, p.CssRate, p.CssAnnualExemptionThreshold,
            p.ProfessionalExpensesRate, p.ProfessionalExpensesAnnualCap,
            p.HeadOfFamilyAnnualDeduction, p.ChildAnnualDeduction, p.MaxDeductibleChildren,
            p.TfpRateIndustry, p.TfpRateOther, p.FoprolosRate, p.MonthlySmig,
            p.CnssEmployeeRateRsa, p.CnssEmployerRateRsa,
            p.EnforceSmigOnContracts, p.EnableExtendedOvertimeRates, p.EnableAllowanceQuadrantMatrix,
            p.StudentChildAnnualDeduction, p.DisabledChildAnnualDeduction,
            p.ParentDeductionRatePercent, p.ParentAnnualDeductionCap, p.IsIndustrialSector,
            p.MealVoucherDailyExemptionCap, p.EnableIrppRegularization,
            smigIrppExemptionMode: mode);
        Assert.True(result.IsSuccess);
        return p;
    }

    private static void AssertCreateSucceeds(IReadOnlyList<JournalLineInput> lines, string label = "Paie 08/2026")
    {
        var create = JournalEntry.Create(
            1, "JOD", new DateTime(2026, 8, 31), label, PeriodId, true,
            "PayrollRun", Guid.NewGuid(), lines, Money.DefaultCurrency);
        Assert.True(create.IsSuccess, create.IsFailure ? create.Error.Description : null);
    }

    [Fact]
    public void BuildLines_RsnaTypical_HasFiveLinesAndCreatesBalancedEntry()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        });

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.Equal(5, lines.Count);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount && l.Debit > 0);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount && l.Debit > 0);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.PersonnelPayableAccount && l.Credit > 0);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount && l.Credit > 0);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SocialOrgAccount && l.Credit > 0);
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.AdvancesAccount);
        Assert.Equal(0m, run.TotalOtherDeductions);

        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_WithEmployeeAuxiliaryCredits_CreatesPerEmployee421Lines()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        });
        var payslip = run.Payslips.First();
        var credits = new List<PayrollJournalEntryBuilder.EmployeeAuxiliaryCredit>
        {
            new(payslip.EmployeeId, payslip.EmployeeName, "4210001", payslip.NetSalary)
        };

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026", credits);

        Assert.True(linesResult.IsSuccess);
        Assert.Contains(linesResult.Value, l => l.AccountNumber == "4210001" && l.Credit > 0);
        Assert.DoesNotContain(linesResult.Value, l => l.AccountNumber == "421" && l.ThirdPartyKind == ThirdPartyKind.None);
    }

    [Fact]
    public void BuildLines_VariableAllowance_IncreasesSalaryDebit()
    {
        var baseRun = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        });

        var withVariable = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            TaxableCnssableAllowances = 150m,
            AllowanceLines = [new AllowanceLineInput("Prime rendement", 150m, true, true)],
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        });

        Assert.True(withVariable.TotalGross > baseRun.TotalGross);
        Assert.True(withVariable.TotalNet > baseRun.TotalNet);

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            withVariable.TotalGross, withVariable.TotalNet, withVariable.TotalCnssEmployee,
            withVariable.TotalCnssEmployer, withVariable.TotalIrpp, withVariable.TotalCss,
            withVariable.TotalTfp, withVariable.TotalFoprolos, withVariable.TotalWorkAccident,
            withVariable.TotalOtherDeductions, "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var salaryLine = Assert.Single(linesResult.Value, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount);
        Assert.Equal(withVariable.TotalGross, salaryLine.Debit);
    }

    [Fact]
    public void BuildLines_SivpExonere_OmitsSocialOrgLineAndCreatesEntry()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 1500m,
            Regime = SocialRegime.SivpExonere,
            WorkAccidentRate = 1m
        });

        Assert.Equal(0m, run.TotalCnssEmployee);
        Assert.Equal(0m, run.TotalCnssEmployer);
        Assert.Equal(0m, run.TotalWorkAccident);

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SocialOrgAccount);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount);
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_WithOtherDeductions_CreditsAdvancesAccount()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            OtherDeductions = 150m
        });

        Assert.Equal(150m, run.TotalOtherDeductions);

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.Contains(lines, l =>
            l.AccountNumber == PayrollJournalEntryBuilder.AdvancesAccount
            && l.Credit == 150m
            && l.Label.Contains("Avances", StringComparison.Ordinal));
        Assert.Equal(6, lines.Count);
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_SivpWithAdvances_SteBouzgarouScenario_Succeeds()
    {
        // Reproduces the failing validate case: CNSS=0 + other deductions → no zero line, balanced OD.
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 1300m,
            Regime = SocialRegime.SivpExonere,
            WorkAccidentRate = 0m,
            OtherDeductions = 5.767m
        });

        Assert.Equal(0m, run.TotalCnssEmployee);
        Assert.Equal(0m, run.TotalCnssEmployer);
        Assert.True(run.TotalOtherDeductions > 0);

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SocialOrgAccount);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.AdvancesAccount && l.Credit > 0);

        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        Assert.Equal(Math.Round(debit, 3), Math.Round(credit, 3));
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_ZeroEmployerCharges_Omits647()
    {
        // Direct builder call with employer charges bucket forced to zero.
        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            totalGross: 1000m,
            totalNet: 800m,
            totalCnssEmployee: 100m,
            totalCnssEmployer: 0m,
            totalIrpp: 100m,
            totalCss: 0m,
            totalTfp: 0m,
            totalFoprolos: 0m,
            totalWorkAccident: 0m,
            totalOtherDeductions: 0m,
            label: "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount);
        // Debit 1000 = Credit 800+100+100
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_ZeroStateWithholding_Omits432_WhenBalanced()
    {
        // Identity: Gross + EmployerCharges = Net + State + SocialOrg + Other
        // Gross=1000, Employer=CNSS_PAT=100, Net=950, State=0, Social=CNSS_SAL+PAT=50+100=150
        // 1000+100 = 950+0+150 ✓
        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            totalGross: 1000m,
            totalNet: 950m,
            totalCnssEmployee: 50m,
            totalCnssEmployer: 100m,
            totalIrpp: 0m,
            totalCss: 0m,
            totalTfp: 0m,
            totalFoprolos: 0m,
            totalWorkAccident: 0m,
            totalOtherDeductions: 0m,
            label: "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount);
        Assert.DoesNotContain(lines, l => l.Debit == 0 && l.Credit == 0);
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_ScreenshotTotals_SteBouzgarou_SucceedsWithoutSocialOrgZeroLine()
    {
        // Exact aggregates from the failing UI (CNSS=0, TFP/FOPROLOS>0, avances ≈ 5.767).
        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            totalGross: 1300m,
            totalNet: 1151.733m,
            totalCnssEmployee: 0m,
            totalCnssEmployer: 0m,
            totalIrpp: 142.5m,
            totalCss: 0m,
            totalTfp: 26m,
            totalFoprolos: 13m,
            totalWorkAccident: 0m,
            totalOtherDeductions: 5.767m,
            label: "Paie 08/2026");

        Assert.True(linesResult.IsSuccess);
        var lines = linesResult.Value;
        Assert.DoesNotContain(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.SocialOrgAccount);
        Assert.Contains(lines, l => l.AccountNumber == PayrollJournalEntryBuilder.AdvancesAccount && l.Credit == 5.767m);
        Assert.Equal(Math.Round(lines.Sum(l => l.Debit), 3), Math.Round(lines.Sum(l => l.Credit), 3));
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLines_RejectsWhenFewerThanTwoNonZeroLines()
    {
        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            totalGross: 0m,
            totalNet: 0m,
            totalCnssEmployee: 0m,
            totalCnssEmployer: 0m,
            totalIrpp: 0m,
            totalCss: 0m,
            totalTfp: 0m,
            totalFoprolos: 0m,
            totalWorkAccident: 0m,
            totalOtherDeductions: 0m,
            label: "Paie 08/2026");

        Assert.True(linesResult.IsFailure);
        Assert.Contains("deux lignes", linesResult.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildLines_SmigExemption_Reduces432AndIncreases421()
    {
        var input = new PayrollComputationInput { BaseSalary = 528.320m, Regime = SocialRegime.Rsna };
        var baseRun = BuildRun(input);
        var exemptRun = BuildRun(input, ParamsWithSmigMode(SmigIrppExemptionMode.SmigPortion));

        Assert.True(exemptRun.TotalIrppSmigExemption > 0m);
        Assert.True(exemptRun.TotalIrpp < baseRun.TotalIrpp);
        Assert.True(exemptRun.TotalNet > baseRun.TotalNet);
        Assert.Equal(baseRun.TotalIrpp - exemptRun.TotalIrpp, exemptRun.TotalIrppSmigExemption);
        Assert.Equal(exemptRun.TotalNet - baseRun.TotalNet, exemptRun.TotalIrppSmigExemption);

        var baseLines = PayrollJournalEntryBuilder.BuildLines(
            baseRun.TotalGross, baseRun.TotalNet, baseRun.TotalCnssEmployee, baseRun.TotalCnssEmployer,
            baseRun.TotalIrpp, baseRun.TotalCss, baseRun.TotalTfp, baseRun.TotalFoprolos,
            baseRun.TotalWorkAccident, baseRun.TotalOtherDeductions, "Paie 08/2026").Value;
        var exemptLines = PayrollJournalEntryBuilder.BuildLines(
            exemptRun.TotalGross, exemptRun.TotalNet, exemptRun.TotalCnssEmployee, exemptRun.TotalCnssEmployer,
            exemptRun.TotalIrpp, exemptRun.TotalCss, exemptRun.TotalTfp, exemptRun.TotalFoprolos,
            exemptRun.TotalWorkAccident, exemptRun.TotalOtherDeductions, "Paie 08/2026").Value;

        var base432 = baseLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount).Credit;
        var exempt432 = exemptLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount).Credit;
        var base421 = baseLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.PersonnelPayableAccount).Credit;
        var exempt421 = exemptLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.PersonnelPayableAccount).Credit;

        Assert.Equal(base432 - exempt432, exemptRun.TotalIrppSmigExemption);
        Assert.Equal(exempt421 - base421, exemptRun.TotalIrppSmigExemption);
        AssertCreateSucceeds(exemptLines);
    }

    private static PayrollYearParameters ParamsWithCssEmployerRate(decimal cssEmployerRate)
    {
        var p = Params();
        var result = p.UpdateRates(
            p.CnssEmployeeRate, p.CnssEmployerRate, p.CssRate, p.CssAnnualExemptionThreshold,
            p.ProfessionalExpensesRate, p.ProfessionalExpensesAnnualCap,
            p.HeadOfFamilyAnnualDeduction, p.ChildAnnualDeduction, p.MaxDeductibleChildren,
            p.TfpRateIndustry, p.TfpRateOther, p.FoprolosRate, p.MonthlySmig,
            p.CnssEmployeeRateRsa, p.CnssEmployerRateRsa,
            p.EnforceSmigOnContracts, p.EnableExtendedOvertimeRates, p.EnableAllowanceQuadrantMatrix,
            p.StudentChildAnnualDeduction, p.DisabledChildAnnualDeduction,
            p.ParentDeductionRatePercent, p.ParentAnnualDeductionCap, p.IsIndustrialSector,
            cssEmployerRate: cssEmployerRate);
        Assert.True(result.IsSuccess);
        return p;
    }

    [Fact]
    public void BuildLines_WithCssEmployer_DebitsAccount647AndCreditsAccount432()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        }, ParamsWithCssEmployerRate(0.5m));

        Assert.Equal(10.000m, run.TotalCssEmployer);

        var baseline = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        });

        var linesResult = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross, run.TotalNet, run.TotalCnssEmployee, run.TotalCnssEmployer,
            run.TotalIrpp, run.TotalCss, run.TotalTfp, run.TotalFoprolos,
            run.TotalWorkAccident, run.TotalOtherDeductions, "Paie 08/2026",
            totalCssEmployer: run.TotalCssEmployer);
        Assert.True(linesResult.IsSuccess);

        var baselineLines = PayrollJournalEntryBuilder.BuildLines(
            baseline.TotalGross, baseline.TotalNet, baseline.TotalCnssEmployee, baseline.TotalCnssEmployer,
            baseline.TotalIrpp, baseline.TotalCss, baseline.TotalTfp, baseline.TotalFoprolos,
            baseline.TotalWorkAccident, baseline.TotalOtherDeductions, "Paie 08/2026").Value;

        var lines = linesResult.Value;
        var employerDebit = lines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount).Debit;
        var baselineEmployerDebit = baselineLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount).Debit;
        var stateCredit = lines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount).Credit;
        var baselineStateCredit = baselineLines.Single(l => l.AccountNumber == PayrollJournalEntryBuilder.StateWithholdingAccount).Credit;

        Assert.Equal(10.000m, employerDebit - baselineEmployerDebit);
        Assert.Equal(10.000m, stateCredit - baselineStateCredit);
        AssertCreateSucceeds(lines);
    }

    [Fact]
    public void BuildLinesFromRun_WithCssEmployer_DoesNotDoubleCountStandardLine()
    {
        var run = BuildRun(new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        }, ParamsWithCssEmployerRate(0.5m));

        var accountMap = new PayrollJournalEntryAccountMap();
        var linesResult = PayrollJournalEntryBuilder.BuildLinesFromRun(run, "Paie 08/2026", accountMap);
        Assert.True(linesResult.IsSuccess);

        var employerDebit = linesResult.Value
            .Single(l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount).Debit;
        Assert.Equal(
            run.TotalCnssEmployer + run.TotalTfp + run.TotalFoprolos + run.TotalWorkAccident + run.TotalCssEmployer,
            employerDebit);
    }
}
