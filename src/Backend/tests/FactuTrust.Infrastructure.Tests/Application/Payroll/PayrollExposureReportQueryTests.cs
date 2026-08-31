using System.Reflection;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Reports;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Rapport d'exposition SCE (plan §5.4 item 6) : recalcul des deltas CNSS sal/pat + IRPP (barème)
/// par bulletin vs preset légal, sans réécriture de l'historique.
/// </summary>
public sealed class PayrollExposureReportQueryTests
{
    private static readonly Guid EmpId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity).GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!.SetValue(entity, id);

    private static void SetProp<T>(Payslip p, string name, T value) =>
        typeof(Payslip).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(p, value);

    private static Payslip BuildPayslip(Guid runId, int month, decimal baseSalary)
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput { BaseSalary = baseSalary, Regime = SocialRegime.Rsna, WorkAccidentRate = 0.4m };
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
        return Payslip.FromComputation(runId, EmpId, "Ahmed Ben Ali", "EMP001", "1234567890", 2026, month, computation, empRate, employerRate);
    }

    private static PayrollRun BuildValidatedRun(int month, params decimal[] salaries)
    {
        var run = PayrollRun.Create(2026, month, 2026).Value;
        SetEntityId(run, Guid.NewGuid());
        run.SetPayslips(salaries.Select(s => BuildPayslip(run.Id, month, s)).ToList());
        run.Validate("tester");
        return run;
    }

    private static (GetPayrollExposureReportQueryHandler Handler, Mock<IPayrollRunRepository> Runs) CreateHandler(
        IReadOnlyList<PayrollRun> runs)
    {
        var runsRepo = new Mock<IPayrollRunRepository>();
        runsRepo.Setup(r => r.ListByMonthRangeWithPayslipsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var employeesRepo = new Mock<IEmployeeRepository>();
        employeesRepo.Setup(e => e.GetFullNamesByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [EmpId] = "Ahmed Ben Ali" });

        return (new GetPayrollExposureReportQueryHandler(runsRepo.Object, employeesRepo.Object), runsRepo);
    }

    private static decimal R(decimal v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    [Fact]
    public async Task NoDrift_WhenAppliedRatesMatchPreset_YieldsZeroCnssDeltas()
    {
        // Bulletin calculé avec le preset 2026 (taux légaux) -> taux appliqués == taux légaux, base < plafond CNSS.
        var run = BuildValidatedRun(3, 600m);
        var (handler, _) = CreateHandler(new[] { run });

        var result = await handler.Handle(new GetPayrollExposureReportQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Rows);
        var row = result.Value.Rows[0];
        Assert.Equal(0m, row.DeltaCnssSal);
        Assert.Equal(0m, row.DeltaCnssPat);
        Assert.False(row.RateMismatch);
    }

    [Fact]
    public async Task Drift_WhenBadPresetRatesApplied_ReportsPositiveCnssDeltaAndMismatch()
    {
        var run = BuildValidatedRun(3, 600m);
        var payslip = run.Payslips.First();
        // Simule un bulletin issu du mauvais preset CNSS 9,18/16,57 (R-09) via les taux appliqués.
        var cnssable = payslip.CnssableGross;
        SetProp(payslip, "AppliedCnssEmployeeRate", 9.18m);
        SetProp(payslip, "AppliedCnssEmployerRate", 16.57m);
        SetProp(payslip, "CnssEmployee", R(cnssable * 9.18m / 100m));
        SetProp(payslip, "CnssEmployer", R(cnssable * 16.57m / 100m));
        var (handler, _) = CreateHandler(new[] { run });

        var result = await handler.Handle(new GetPayrollExposureReportQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = result.Value.Rows[0];
        Assert.True(row.RateMismatch);
        // Taux légal 9,68 > taux appliqué 9,18 -> sous-retenue -> delta positif (à retenir plus).
        Assert.True(row.DeltaCnssSal > 0m);
        // Montant recalculé = assiette × 9,68 % (assiette retrouvée via montant / taux appliqué).
        var expectedCorrected = R(payslip.CnssEmployee / 9.18m * 9.68m);
        Assert.Equal(expectedCorrected, row.CorrectedCnssSal);
        Assert.Equal(row.DeltaCnssSal, result.Value.TotalDeltaCnssSal);
    }

    [Fact]
    public async Task IrppGross_RecomputedViaProgressiveBareme_OnStoredAnnualNetTaxable()
    {
        var run = BuildValidatedRun(3, 600m);
        var payslip = run.Payslips.First();
        // Contrôle déterministe de l'assiette et du brut IRPP stocké pour valider le recalcul progressif.
        SetProp(payslip, "AnnualNetTaxable", 20000m);
        SetProp(payslip, "IrppBeforeSmigExemption", 3000m);
        var (handler, _) = CreateHandler(new[] { run });

        var result = await handler.Handle(new GetPayrollExposureReportQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = result.Value.Rows[0];
        var brackets = PayrollParameterDefaults.GetPreset(2026).IrppBrackets;
        var expectedGross = ProgressiveIrpp(20000m, brackets);
        Assert.Equal(expectedGross, row.RecomputedIrppGross);
        Assert.Equal(R(expectedGross - 3000m), row.DeltaIrppGross);
    }

    private static decimal ProgressiveIrpp(decimal annualNetTaxable, IReadOnlyList<(decimal LowerBound, decimal Rate)> brackets)
    {
        if (annualNetTaxable <= 0m || brackets.Count == 0) return 0m;
        var ordered = brackets.OrderBy(b => b.LowerBound).ToList();
        decimal tax = 0m;
        for (int i = 0; i < ordered.Count; i++)
        {
            var lower = ordered[i].LowerBound;
            if (annualNetTaxable <= lower) break;
            var upper = i + 1 < ordered.Count ? ordered[i + 1].LowerBound : decimal.MaxValue;
            tax += (Math.Min(annualNetTaxable, upper) - lower) * ordered[i].Rate;
        }
        return R(tax);
    }
}
