using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Tableau de bord mensuel : lecture pure des totaux gelés sur les cycles. Aucun montant ne doit
/// être recalculé, sous peine de faire diverger l'écran des bulletins réellement émis.
/// </summary>
public sealed class PayrollDashboardQueryTests
{
    private static readonly Guid EmployeeAId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid EmployeeBId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity)
            .GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!
            .SetValue(entity, id);

    private static Payslip BuildPayslip(Guid runId, Guid employeeId, string name, int month, decimal baseSalary)
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);

        return Payslip.FromComputation(
            runId, employeeId, name, "EMP-1", "1234567890", 2026, month, computation, empRate, employerRate);
    }

    private static PayrollRun BuildRun(
        int month,
        PayrollRunStatus status = PayrollRunStatus.Calculated,
        params (Guid Id, string Name, decimal Salary)[] employees)
    {
        var run = PayrollRun.Create(2026, month, 2026).Value;
        SetEntityId(run, Guid.NewGuid());

        run.SetPayslips(employees
            .Select(e => BuildPayslip(run.Id, e.Id, e.Name, month, e.Salary))
            .ToList());

        if (status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
        {
            run.Validate("tester");
            if (status == PayrollRunStatus.Closed)
                run.Close();
        }

        return run;
    }

    private static GetPayrollDashboardQueryHandler CreateHandler(
        IReadOnlyList<PayrollRun> runs,
        IReadOnlyList<PayrollOvertimeLine>? overtime = null,
        IReadOnlyList<PayrollVariableAllowanceLine>? allowances = null,
        IReadOnlyList<EmployeeInKindBenefit>? inKind = null,
        IReadOnlyList<FiscalScheduleEntry>? schedule = null,
        Mock<IPayrollRunRepository>? runsRepoOut = null)
    {
        var runsRepo = runsRepoOut ?? new Mock<IPayrollRunRepository>();
        runsRepo
            .Setup(r => r.ListAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        runsRepo
            .Setup(r => r.GetByPeriodAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int y, int m, CancellationToken _) =>
                runs.FirstOrDefault(r => r.Year == y && r.Month == m));
        runsRepo
            .Setup(r => r.GetByIdWithPayslipsForPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => runs.FirstOrDefault(r => r.Id == id));

        var overtimeRepo = new Mock<IPayrollOvertimeRepository>();
        overtimeRepo
            .Setup(r => r.ListForMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overtime ?? Array.Empty<PayrollOvertimeLine>());

        var allowanceRepo = new Mock<IPayrollVariableAllowanceRepository>();
        allowanceRepo
            .Setup(r => r.ListForMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowances ?? Array.Empty<PayrollVariableAllowanceLine>());

        var inKindRepo = new Mock<IEmployeeInKindBenefitRepository>();
        inKindRepo
            .Setup(r => r.ListActiveForEmployeesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inKind ?? Array.Empty<EmployeeInKindBenefit>());

        var scheduleRepo = new Mock<IFiscalScheduleRepository>();
        scheduleRepo
            .Setup(r => r.ListAsync(It.IsAny<FiscalScheduleQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiscalScheduleQueryResult(
                schedule ?? Array.Empty<FiscalScheduleEntry>(),
                Array.Empty<FiscalScheduleEntry>(),
                schedule?.Count ?? 0));

        return new GetPayrollDashboardQueryHandler(
            runsRepo.Object,
            overtimeRepo.Object,
            allowanceRepo.Object,
            inKindRepo.Object,
            scheduleRepo.Object,
            NullLogger<GetPayrollDashboardQueryHandler>.Instance);
    }

    // ============================================
    // MOIS SANS CYCLE
    // ============================================

    [Fact]
    public async Task A_month_without_a_run_still_renders()
    {
        // Le tableau de bord doit rester utile avant le premier calcul du mois.
        var handler = CreateHandler(Array.Empty<PayrollRun>());

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.False(dto.HasRun);
        Assert.Null(dto.RunId);
        Assert.Equal(0m, dto.Gross.Amount);
        Assert.Equal(0, dto.EmployeeCount);
        Assert.Empty(dto.EarningsBreakdown);
        Assert.Empty(dto.Payslips);
        // La série reste rendue sur douze mois, même vide.
        Assert.Equal(12, dto.MonthlySeries.Count);
        Assert.Equal("Juin 2026", dto.PeriodLabel);
    }

    // ============================================
    // INDICATEURS
    // ============================================

    [Fact]
    public async Task Totals_are_read_from_the_run_without_recomputation()
    {
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m), (EmployeeBId, "Sonia T", 1_500m));
        var handler = CreateHandler(new[] { run });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.True(dto.HasRun);
        Assert.Equal(run.TotalGross, dto.Gross.Amount);
        Assert.Equal(run.TotalNet, dto.Net.Amount);
        Assert.Equal(2, dto.EmployeeCount);

        var expectedCharges = run.TotalCnssEmployer + run.TotalWorkAccident + run.TotalTfp
                              + run.TotalFoprolos + run.TotalCssEmployer;
        Assert.Equal(expectedCharges, dto.EmployerCharges.Amount);
    }

    [Fact]
    public async Task No_previous_run_means_no_trend_rather_than_a_meaningless_hundred_percent()
    {
        var handler = CreateHandler(new[] { BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m)) });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.Null(dto.Gross.PreviousAmount);
        Assert.Null(dto.Gross.ChangePercent);
        Assert.Null(dto.PreviousEmployeeCount);
    }

    [Fact]
    public async Task The_previous_month_drives_the_trend()
    {
        var may = BuildRun(5, PayrollRunStatus.Validated, (EmployeeAId, "Ali Ben", 1_000m));
        var june = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 1_000m), (EmployeeBId, "Sonia T", 1_000m));
        var handler = CreateHandler(new[] { june, may });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.Equal(may.TotalGross, dto.Gross.PreviousAmount);
        Assert.NotNull(dto.Gross.ChangePercent);
        Assert.True(dto.Gross.ChangePercent > 0);
        Assert.Equal(1, dto.PreviousEmployeeCount);
    }

    [Fact]
    public async Task January_compares_against_december_of_the_previous_year()
    {
        var runsRepo = new Mock<IPayrollRunRepository>();
        var handler = CreateHandler(Array.Empty<PayrollRun>(), runsRepoOut: runsRepo);

        await handler.Handle(new GetPayrollDashboardQuery(2026, 1), CancellationToken.None);

        runsRepo.Verify(r => r.GetByPeriodAsync(2025, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================
    // RÉPARTITIONS
    // ============================================

    [Fact]
    public async Task Employer_charges_are_split_into_their_five_statutory_parts()
    {
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m));
        var handler = CreateHandler(new[] { run });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.Contains(dto.EmployerChargeBreakdown, s => s.Label == "CNSS patronale");
        Assert.Contains(dto.EmployerChargeBreakdown, s => s.Label == "TFP");
        Assert.Contains(dto.EmployerChargeBreakdown, s => s.Label == "FOPROLOS");
        // La somme des parts doit égaler l'indicateur, sinon le donut ment sur son total.
        Assert.Equal(dto.EmployerCharges.Amount, dto.EmployerChargeBreakdown.Sum(s => s.Amount));
    }

    [Fact]
    public async Task Zero_slices_are_dropped_from_the_breakdowns()
    {
        // La CSS patronale est optionnelle : à 0 %, elle ne doit pas encombrer le graphe.
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m));
        var handler = CreateHandler(new[] { run });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.DoesNotContain(dto.EmployerChargeBreakdown, s => s.Amount == 0m);
        Assert.DoesNotContain(dto.DeductionBreakdown, s => s.Amount == 0m);
    }

    [Fact]
    public async Task Earnings_breakdown_closes_exactly_on_the_run_gross()
    {
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m));
        var handler = CreateHandler(new[] { run });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        // Le salaire de base est déduit par différence : la ventilation ne peut pas s'écarter du brut.
        Assert.Equal(run.TotalGross, dto.EarningsBreakdown.Sum(s => s.Amount));
        Assert.Null(dto.BreakdownWarning);
    }

    [Fact]
    public async Task Variable_elements_exceeding_the_gross_are_reported_not_hidden()
    {
        // Cas limite (prorata, absences non rémunérées) : mieux vaut signaler que d'afficher
        // un salaire de base négatif ou une ventilation silencieusement fausse.
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 500m));
        var allowance = PayrollVariableAllowanceLine.Create(
            EmployeeAId, 2026, 6, "Prime exceptionnelle", 50_000m, taxable: true, subjectToCnss: true).Value;
        var handler = CreateHandler(new[] { run }, allowances: new[] { allowance });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.NotNull(dto.BreakdownWarning);
        Assert.DoesNotContain(dto.EarningsBreakdown, s => s.Amount < 0m);
    }

    [Fact]
    public async Task Irpp_regularization_is_folded_into_the_withholding_line()
    {
        // Le cycle tient la régularisation à part, mais elle est retenue sur le même bulletin :
        // la séparer à l'écran ferait un total de retenues qui ne colle pas au net payé.
        var run = BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 3_000m));
        var handler = CreateHandler(new[] { run });

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        var withholding = dto.DeductionBreakdown.Single(s => s.Label.Contains("IRPP", StringComparison.Ordinal));
        Assert.Equal(run.TotalIrpp + run.TotalIrppRegularization, withholding.Amount);
    }

    // ============================================
    // SÉRIE ET CYCLES
    // ============================================

    [Fact]
    public async Task The_annual_series_is_loaded_in_a_single_query()
    {
        // Le chemin de la liste des cycles chargeait un exercice en douze requêtes complètes ;
        // le tableau de bord ne doit pas reproduire ce défaut.
        var runsRepo = new Mock<IPayrollRunRepository>();
        var handler = CreateHandler(
            new[] { BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m)) },
            runsRepoOut: runsRepo);

        await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        runsRepo.Verify(r => r.ListAsync(2026, It.IsAny<CancellationToken>()), Times.Once);
        runsRepo.Verify(
            r => r.GetByIdWithPayslipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Recent_runs_are_listed_most_recent_first()
    {
        var runs = new[]
        {
            BuildRun(6, PayrollRunStatus.Calculated, (EmployeeAId, "Ali Ben", 2_000m)),
            BuildRun(5, PayrollRunStatus.Validated, (EmployeeAId, "Ali Ben", 2_000m)),
            BuildRun(4, PayrollRunStatus.Closed, (EmployeeAId, "Ali Ben", 2_000m))
        };
        var handler = CreateHandler(runs);

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.Equal(new[] { 6, 5, 4 }, dto.RecentRuns.Select(r => r.Month));
        Assert.Equal("Clôturé", dto.RecentRuns[2].StatusDisplay);
    }

    // ============================================
    // ÉCHÉANCES
    // ============================================

    [Fact]
    public async Task A_missing_schedule_yields_no_deadlines_rather_than_an_error()
    {
        var handler = CreateHandler(Array.Empty<PayrollRun>());

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.Empty(dto.UpcomingDeadlines);
    }

    [Fact]
    public async Task Only_payroll_obligations_are_surfaced_and_they_are_sorted_by_due_date()
    {
        var today = DateTime.UtcNow.Date;
        var entries = new[]
        {
            BuildScheduleEntry(FiscalObligationType.MonthlyDeclaration, today.AddDays(3)),
            BuildScheduleEntry(FiscalObligationType.CnssMonthlyRemittance, today.AddDays(10)),
            BuildScheduleEntry(FiscalObligationType.PayrollIrppWithholding, today.AddDays(5))
        };
        var handler = CreateHandler(Array.Empty<PayrollRun>(), schedule: entries);

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        // La TVA n'a rien à faire sur un tableau de bord de paie.
        Assert.Equal(2, dto.UpcomingDeadlines.Count);
        Assert.True(dto.UpcomingDeadlines[0].DueDate < dto.UpcomingDeadlines[1].DueDate);
        Assert.Equal(5, dto.UpcomingDeadlines[0].DaysRemaining);
        Assert.False(dto.UpcomingDeadlines[0].IsOverdue);
    }

    [Fact]
    public async Task An_overdue_deadline_is_flagged()
    {
        var entries = new[]
        {
            BuildScheduleEntry(FiscalObligationType.CnssMonthlyRemittance, DateTime.UtcNow.Date.AddDays(-4))
        };
        var handler = CreateHandler(Array.Empty<PayrollRun>(), schedule: entries);

        var dto = await handler.Handle(new GetPayrollDashboardQuery(2026, 6), CancellationToken.None);

        Assert.True(Assert.Single(dto.UpcomingDeadlines).IsOverdue);
    }

    private static FiscalScheduleEntry BuildScheduleEntry(FiscalObligationType type, DateTime dueDate) =>
        FiscalScheduleEntry.Create(
            type,
            type.ToString(),
            dueDate.Year,
            dueDate,
            estimatedAmount: 0m,
            periodMonth: dueDate.Month,
            sourceType: FiscalScheduleSourceType.Payroll).Value;
}
