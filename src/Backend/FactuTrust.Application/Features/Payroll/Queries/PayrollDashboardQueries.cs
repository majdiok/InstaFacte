using System.Globalization;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Payroll.Queries;

/// <summary>Vue d'ensemble de la paie d'un mois.</summary>
public sealed record GetPayrollDashboardQuery(int Year, int Month) : IRequest<PayrollDashboardDto>;

/// <summary>
/// Assemble le tableau de bord mensuel à partir des totaux déjà figés sur les cycles.
/// </summary>
/// <remarks>
/// <para>
/// Aucun montant n'est recalculé : les indicateurs, les répartitions et la série de l'exercice
/// lisent les colonnes de <see cref="PayrollRun"/>, arrêtées au calcul du cycle. Refaire le calcul
/// ici ferait diverger le tableau de bord des bulletins réellement émis.
/// </para>
/// <para>
/// Seule exception : la décomposition du brut (base, primes, heures supplémentaires, avantages)
/// n'est pas portée par le bulletin et doit être reconstituée depuis les tables d'entrée.
/// </para>
/// </remarks>
public sealed class GetPayrollDashboardQueryHandler
    : IRequestHandler<GetPayrollDashboardQuery, PayrollDashboardDto>
{
    /// <summary>Obligations sociales issues de la paie, à l'exclusion du reste de l'échéancier.</summary>
    private static readonly FiscalObligationType[] PayrollObligations =
    {
        FiscalObligationType.CnssMonthlyRemittance,
        FiscalObligationType.CnssDtsQuarterly,
        FiscalObligationType.PayrollIrppWithholding
    };

    private const int MaxUpcomingDeadlines = 5;
    private const int MaxRecentRuns = 6;

    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeInKindBenefitRepository _inKindBenefits;
    private readonly IFiscalScheduleRepository _schedule;
    private readonly ILogger<GetPayrollDashboardQueryHandler> _logger;

    public GetPayrollDashboardQueryHandler(
        IPayrollRunRepository runs,
        IPayrollOvertimeRepository overtime,
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeInKindBenefitRepository inKindBenefits,
        IFiscalScheduleRepository schedule,
        ILogger<GetPayrollDashboardQueryHandler> logger)
    {
        _runs = runs;
        _overtime = overtime;
        _variableAllowances = variableAllowances;
        _inKindBenefits = inKindBenefits;
        _schedule = schedule;
        _logger = logger;
    }

    public async Task<PayrollDashboardDto> Handle(
        GetPayrollDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var (year, month) = (request.Year, request.Month);

        var current = await _runs.GetByPeriodAsync(year, month, cancellationToken);
        var previousPeriod = PreviousPeriod(year, month);
        var previous = await _runs.GetByPeriodAsync(previousPeriod.Year, previousPeriod.Month, cancellationToken);

        // Une seule lecture pour la série de l'exercice ET les derniers cycles : ListAsync ne
        // charge pas les bulletins, contrairement au chemin de la liste des cycles.
        var yearRuns = await _runs.ListAsync(year, cancellationToken);

        var payslips = await LoadPayslipsAsync(current, cancellationToken);
        var previousPayslipCount = previous is null
            ? (int?)null
            : (await LoadPayslipsAsync(previous, cancellationToken)).Count;

        var earnings = await BuildEarningsBreakdownAsync(current, year, month, cancellationToken);
        var deadlines = await LoadUpcomingDeadlinesAsync(cancellationToken);

        return new PayrollDashboardDto
        {
            Year = year,
            Month = month,
            PeriodLabel = FormatPeriod(year, month),
            HasRun = current is not null,
            RunId = current?.Id,
            RunStatus = current?.Status.ToString(),
            RunStatusDisplay = current?.Status.ToDisplayString(),

            Gross = BuildKpi(current?.TotalGross, previous?.TotalGross),
            Net = BuildKpi(current?.TotalNet, previous?.TotalNet),
            EmployerCharges = BuildKpi(EmployerCharges(current), EmployerCharges(previous)),

            EmployeeCount = payslips.Count,
            PreviousEmployeeCount = previousPayslipCount,

            EarningsBreakdown = earnings.Slices,
            BreakdownWarning = earnings.Warning,
            DeductionBreakdown = BuildDeductionBreakdown(current),
            EmployerChargeBreakdown = BuildEmployerChargeBreakdown(current),

            MonthlySeries = BuildMonthlySeries(yearRuns),
            Payslips = payslips.Select(PayrollMappings.ToPayslipListDto).ToList(),
            RecentRuns = BuildRecentRuns(yearRuns),
            UpcomingDeadlines = deadlines
        };
    }

    // ============================================
    // INDICATEURS
    // ============================================

    private static PayrollDashboardKpiDto BuildKpi(decimal? amount, decimal? previous)
    {
        var value = MillimeRounding.Round(amount ?? 0m);

        // Pas de comparaison possible sans mois précédent, et pas de division par zéro.
        if (previous is not > 0m)
            return new PayrollDashboardKpiDto(value, previous, null);

        var change = Math.Round((value - previous.Value) / previous.Value * 100m, 1, MidpointRounding.AwayFromZero);
        return new PayrollDashboardKpiDto(value, MillimeRounding.Round(previous.Value), change);
    }

    private static decimal? EmployerCharges(PayrollRun? run) =>
        run is null
            ? null
            : MillimeRounding.Round(
                run.TotalCnssEmployer + run.TotalWorkAccident + run.TotalTfp
                + run.TotalFoprolos + run.TotalCssEmployer);

    // ============================================
    // RÉPARTITIONS
    // ============================================

    private static IReadOnlyList<PayrollDashboardSliceDto> BuildEmployerChargeBreakdown(PayrollRun? run)
    {
        if (run is null)
            return Array.Empty<PayrollDashboardSliceDto>();

        return NonZero(
            new PayrollDashboardSliceDto("CNSS patronale", run.TotalCnssEmployer),
            new PayrollDashboardSliceDto("Accident du travail", run.TotalWorkAccident),
            new PayrollDashboardSliceDto("TFP", run.TotalTfp),
            new PayrollDashboardSliceDto("FOPROLOS", run.TotalFoprolos),
            new PayrollDashboardSliceDto("CSS patronale", run.TotalCssEmployer));
    }

    private static IReadOnlyList<PayrollDashboardSliceDto> BuildDeductionBreakdown(PayrollRun? run)
    {
        if (run is null)
            return Array.Empty<PayrollDashboardSliceDto>();

        // Les régularisations annuelles sont tenues à part sur le cycle mais retenues sur le même
        // bulletin : les additionner ici est ce qui fait retomber le total sur le net réellement payé.
        return NonZero(
            new PayrollDashboardSliceDto("CNSS salariale", run.TotalCnssEmployee),
            new PayrollDashboardSliceDto("Retenue à la source (IRPP)", run.TotalIrpp + run.TotalIrppRegularization),
            new PayrollDashboardSliceDto("CSS", run.TotalCss + run.TotalCssRegularization),
            new PayrollDashboardSliceDto("Autres retenues", run.TotalOtherDeductions));
    }

    /// <summary>
    /// Reconstitue la ventilation du brut, que le bulletin ne porte pas.
    /// </summary>
    /// <remarks>
    /// Le salaire de base est obtenu <b>par différence</b> pour que les quatre postes retombent
    /// exactement sur le brut du cycle : afficher une somme qui ne colle pas au total serait pire
    /// qu'une ventilation absente. Le cas limite (primes + heures + avantages au-delà du brut, sous
    /// l'effet d'un prorata ou d'absences non rémunérées) est signalé plutôt que masqué.
    /// </remarks>
    private async Task<(IReadOnlyList<PayrollDashboardSliceDto> Slices, string? Warning)> BuildEarningsBreakdownAsync(
        PayrollRun? run,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (run is null)
            return (Array.Empty<PayrollDashboardSliceDto>(), null);

        var overtimeLines = await _overtime.ListForMonthAsync(year, month, cancellationToken);
        var overtime = MillimeRounding.Round(overtimeLines.Sum(l => l.EffectiveAmount));

        var allowanceLines = await _variableAllowances.ListForMonthAsync(year, month, cancellationToken);
        var allowances = MillimeRounding.Round(allowanceLines.Sum(l => l.Amount));

        var employeeIds = run.Payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var inKind = 0m;
        if (employeeIds.Count > 0)
        {
            var referenceDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            var benefits = await _inKindBenefits.ListActiveForEmployeesAsync(
                employeeIds, referenceDate, cancellationToken);
            inKind = MillimeRounding.Round(benefits.Sum(b => b.MonthlyValue));
        }

        var accounted = MillimeRounding.Round(overtime + allowances + inKind);
        var baseSalary = MillimeRounding.Round(run.TotalGross - accounted);

        string? warning = null;
        if (baseSalary < 0m)
        {
            warning = "La ventilation du brut est approchée : les éléments variables du mois "
                      + "dépassent le brut arrêté (prorata ou absences non rémunérées).";
            baseSalary = 0m;
        }

        return (NonZero(
            new PayrollDashboardSliceDto("Salaires de base", baseSalary),
            new PayrollDashboardSliceDto("Primes & indemnités", allowances),
            new PayrollDashboardSliceDto("Heures supplémentaires", overtime),
            new PayrollDashboardSliceDto("Avantages en nature", inKind)), warning);
    }

    /// <summary>Écarte les parts nulles : un secteur à zéro n'apprend rien et brouille le graphe.</summary>
    private static IReadOnlyList<PayrollDashboardSliceDto> NonZero(params PayrollDashboardSliceDto[] slices) =>
        slices
            .Select(s => s with { Amount = MillimeRounding.Round(s.Amount) })
            .Where(s => s.Amount != 0m)
            .ToList();

    // ============================================
    // SÉRIE ET CYCLES
    // ============================================

    private static IReadOnlyList<PayrollDashboardMonthDto> BuildMonthlySeries(IReadOnlyList<PayrollRun> yearRuns)
    {
        var byMonth = yearRuns.ToDictionary(r => r.Month);
        var series = new List<PayrollDashboardMonthDto>(12);

        // Les douze mois sont émis, même vides : un graphe à trous se lit mal.
        for (var m = 1; m <= 12; m++)
        {
            if (byMonth.TryGetValue(m, out var run))
            {
                series.Add(new PayrollDashboardMonthDto(
                    m,
                    run.TotalGross,
                    run.TotalNet,
                    EmployerCharges(run) ?? 0m,
                    run.Status.ToString(),
                    run.Status.ToDisplayString()));
            }
            else
            {
                series.Add(new PayrollDashboardMonthDto(m, 0m, 0m, 0m, string.Empty, "Aucun cycle"));
            }
        }

        return series;
    }

    private static IReadOnlyList<PayrollDashboardRunDto> BuildRecentRuns(IReadOnlyList<PayrollRun> yearRuns) =>
        yearRuns
            .OrderByDescending(r => r.Month)
            .Take(MaxRecentRuns)
            .Select(r => new PayrollDashboardRunDto(
                r.Id, r.Year, r.Month, r.Label,
                r.Status.ToString(), r.Status.ToDisplayString(),
                r.TotalGross, r.TotalNet, r.ValidatedAt, r.ClosedAt))
            .ToList();

    /// <summary>Bulletins du cycle, sans leurs lignes — seuls les totaux nous intéressent ici.</summary>
    private async Task<IReadOnlyList<Payslip>> LoadPayslipsAsync(
        PayrollRun? run,
        CancellationToken cancellationToken)
    {
        if (run is null)
            return Array.Empty<Payslip>();

        var loaded = await _runs.GetByIdWithPayslipsForPaymentAsync(run.Id, cancellationToken);
        return loaded?.Payslips.OrderBy(p => p.EmployeeName).ToList() ?? (IReadOnlyList<Payslip>)Array.Empty<Payslip>();
    }

    // ============================================
    // ÉCHÉANCES
    // ============================================

    /// <summary>
    /// Prochaines obligations sociales, ou une liste vide si l'échéancier n'a pas été généré.
    /// </summary>
    /// <remarks>
    /// Dégradation silencieuse assumée : un échéancier absent ne doit pas priver le cabinet de son
    /// tableau de bord.
    /// </remarks>
    private async Task<IReadOnlyList<PayrollDashboardDeadlineDto>> LoadUpcomingDeadlinesAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        try
        {
            var result = await _schedule.ListAsync(
                new FiscalScheduleQueryCriteria
                {
                    DueFrom = today.AddMonths(-1),
                    DueTo = today.AddMonths(3),
                    IncludeCancelled = false,
                    PageSize = 100
                },
                cancellationToken);

            return result.Items
                .Where(e => PayrollObligations.Contains(e.ObligationType))
                // Une obligation déposée et payée n'est plus une échéance à venir.
                .Where(e => e.PaymentDate is null || e.DepositDate is null)
                .OrderBy(e => e.DueDate)
                .Take(MaxUpcomingDeadlines)
                .Select(e => new PayrollDashboardDeadlineDto(
                    e.ObligationLabel,
                    e.DueDate,
                    (int)(e.DueDate.Date - today).TotalDays,
                    e.DueDate.Date < today,
                    e.EstimatedAmount))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échéancier social illisible pour le tableau de bord paie.");
            return Array.Empty<PayrollDashboardDeadlineDto>();
        }
    }

    // ============================================
    // UTILITAIRES
    // ============================================

    private static (int Year, int Month) PreviousPeriod(int year, int month) =>
        month == 1 ? (year - 1, 12) : (year, month - 1);

    private static string FormatPeriod(int year, int month)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var label = culture.DateTimeFormat.GetMonthName(month);
        return $"{char.ToUpper(label[0], culture)}{label[1..]} {year}";
    }
}
