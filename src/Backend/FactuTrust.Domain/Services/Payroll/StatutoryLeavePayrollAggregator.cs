using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Agrège les montants statutaires (maladie, maternité, paternité) pour un salarié sur un mois.
/// </summary>
public static class StatutoryLeavePayrollAggregator
{
    public sealed class StatutoryLeaveAmounts
    {
        public static StatutoryLeaveAmounts Empty { get; } = new();

        public decimal SickLeaveDeduction { get; init; }
        public decimal SickLeaveTopUp { get; init; }
        public decimal SickLeaveSubrogation { get; init; }
        public decimal MaternityTopUp { get; init; }
        public decimal PaternityMaintenance { get; init; }
        public IReadOnlyList<CnssIjClaimDraft> IjClaims { get; init; } = Array.Empty<CnssIjClaimDraft>();
    }

    public sealed record CnssIjClaimDraft(Guid LeaveRequestId, decimal Amount);

    public static StatutoryLeaveAmounts Compute(
        Guid employeeId,
        decimal baseSalary,
        IReadOnlyList<LeaveRequest> monthLeaves,
        IReadOnlyList<LeaveRequest> yearSickLeaves,
        PayrollYearParameters parameters,
        int year,
        int month,
        bool sickLeaveEnabled,
        bool maternityLeaveEnabled,
        bool paternityLeaveEnabled)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        decimal sickDeduction = 0m;
        decimal sickTopUp = 0m;
        decimal sickSubrogation = 0m;
        decimal maternityTopUp = 0m;
        decimal paternityMaintenance = 0m;
        var ijClaims = new List<CnssIjClaimDraft>();

        var employeeLeaves = monthLeaves
            .Where(l => l.EmployeeId == employeeId && l.IsApproved && l.Type.AffectsPayrollComputation())
            .ToList();

        if (sickLeaveEnabled)
        {
            foreach (var leave in employeeLeaves.Where(l => l.Type == LeaveType.Sick))
            {
                var daysInMonth = CountDaysInMonth(leave, year, month);
                if (daysInMonth <= 0) continue;

                var priorInEpisode = CountPriorDaysInEpisode(leave, monthStart);
                var priorIjInYear = CountPriorIjDaysInYear(leave, yearSickLeaves, year, monthStart, parameters.SickLeaveWaitingDays);

                var result = SickLeaveCalculator.Compute(new SickLeaveInput
                {
                    SickDaysInMonth = daysInMonth,
                    BaseSalary = baseSalary,
                    PriorSickDaysInEpisode = priorInEpisode,
                    PriorIjDaysInYear = priorIjInYear,
                    WaitingDaysPolicy = parameters.SickLeaveWaitingDays,
                    IjRatePercent = parameters.SickLeaveIjRatePercent,
                    SubrogationEnabled = leave.SubrogationEnabled,
                    EmployerTopUpPercent = leave.EmployerTopUpPercent,
                    EmployerTopUpDays = leave.EmployerTopUpDays
                });

                sickDeduction += result.DeductionAmount;
                sickTopUp += result.EmployerTopUpAmount;
                sickSubrogation += result.SubrogationAdvanceAmount;

                if (result.CnssIjAmount > 0 && leave.SubrogationEnabled)
                    ijClaims.Add(new CnssIjClaimDraft(leave.Id, result.CnssIjAmount));
            }
        }

        if (maternityLeaveEnabled)
        {
            foreach (var leave in employeeLeaves.Where(l => l.Type == LeaveType.Maternity))
            {
                var daysInMonth = CountDaysInMonth(leave, year, month);
                if (daysInMonth <= 0) continue;

                var topUpPercent = leave.EmployerTopUpPercent ?? parameters.MaternityEmployerTopUpDefault;
                var result = MaternityLeaveCalculator.Compute(new MaternityLeaveInput
                {
                    MaternityDaysInMonth = daysInMonth,
                    BaseSalary = baseSalary,
                    IjRatePercent = parameters.SickLeaveIjRatePercent,
                    EmployerTopUpPercent = topUpPercent
                });

                maternityTopUp += result.EmployerTopUpAmount;

                if (result.CnssIjAmount > 0)
                    ijClaims.Add(new CnssIjClaimDraft(leave.Id, result.CnssIjAmount));
            }
        }

        if (paternityLeaveEnabled)
        {
            foreach (var leave in employeeLeaves.Where(l => l.Type == LeaveType.Paternity))
            {
                var daysInMonth = CountDaysInMonth(leave, year, month);
                if (daysInMonth <= 0) continue;

                var result = PaternityLeaveCalculator.Compute(new PaternityLeaveInput
                {
                    PaternityDaysInMonth = daysInMonth,
                    BaseSalary = baseSalary
                });

                paternityMaintenance += result.MaintenanceAmount;
            }
        }

        return new StatutoryLeaveAmounts
        {
            SickLeaveDeduction = R(sickDeduction),
            SickLeaveTopUp = R(sickTopUp),
            SickLeaveSubrogation = R(sickSubrogation),
            MaternityTopUp = R(maternityTopUp),
            PaternityMaintenance = R(paternityMaintenance),
            IjClaims = ijClaims
        };
    }

    /// <summary>Proportion des jours du congé tombant dans le mois (approximation linéaire).</summary>
    public static decimal CountDaysInMonth(LeaveRequest leave, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var overlapStart = leave.StartDate > monthStart ? leave.StartDate : monthStart;
        var overlapEnd = leave.EndDate < monthEnd ? leave.EndDate : monthEnd;
        if (overlapEnd < overlapStart)
            return 0m;

        var totalCalendarDays = (leave.EndDate - leave.StartDate).Days + 1;
        if (totalCalendarDays <= 0)
            return 0m;

        var overlapCalendarDays = (overlapEnd - overlapStart).Days + 1;
        return R(leave.Days * overlapCalendarDays / totalCalendarDays);
    }

    private static decimal CountPriorDaysInEpisode(LeaveRequest leave, DateTime monthStart)
    {
        if (leave.StartDate >= monthStart)
            return 0m;

        var priorEnd = monthStart.AddDays(-1);
        if (priorEnd < leave.StartDate)
            return 0m;

        var totalCalendarDays = (leave.EndDate - leave.StartDate).Days + 1;
        if (totalCalendarDays <= 0)
            return 0m;

        var priorCalendarDays = (priorEnd - leave.StartDate).Days + 1;
        return R(leave.Days * priorCalendarDays / totalCalendarDays);
    }

    private static decimal CountPriorIjDaysInYear(
        LeaveRequest currentLeave,
        IReadOnlyList<LeaveRequest> yearSickLeaves,
        int year,
        DateTime monthStart,
        int waitingDays)
    {
        decimal priorIj = 0m;

        // R-35 (CAL-016) : le plafond annuel de 180 jours IJ doit tenir compte de tout épisode
        // qui *chevauche* l'exercice, pas seulement de ceux dont StartDate.Year == year — un
        // congé maladie débuté en décembre de l'année précédente contribue des jours IJ à
        // l'exercice courant et doit donc entrer dans le décompte du plafond de cet exercice.
        // Seule la portion de l'épisode tombant dans l'exercice est comptée (proratisation
        // calendaire, même convention que CountDaysInMonth/CountPriorDaysInEpisode).
        foreach (var leave in yearSickLeaves.Where(l =>
                     l.IsApproved && l.Type == LeaveType.Sick && OverlapsYear(l, year) && l.Id != currentLeave.Id))
        {
            if (leave.EndDate >= monthStart)
                continue;

            var daysInYear = CountDaysOverlappingYear(leave, year);
            var waiting = Math.Min(daysInYear, waitingDays);
            priorIj += Math.Max(0m, daysInYear - waiting);
        }

        // Jours IJ de l'épisode courant avant le mois.
        var priorInEpisode = CountPriorDaysInEpisode(currentLeave, monthStart);
        var waitingInEpisode = Math.Min(priorInEpisode, waitingDays);
        priorIj += Math.Max(0m, priorInEpisode - waitingInEpisode);

        return priorIj;
    }

    private static bool OverlapsYear(LeaveRequest leave, int year) =>
        leave.StartDate.Year <= year && leave.EndDate.Year >= year;

    /// <summary>
    /// Portion (proratisée au calendaire) des jours du congé qui tombe dans l'exercice
    /// <paramref name="year"/> — utilisé pour le plafond annuel de 180 jours IJ (R-35).
    /// </summary>
    private static decimal CountDaysOverlappingYear(LeaveRequest leave, int year)
    {
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);
        var overlapStart = leave.StartDate > yearStart ? leave.StartDate : yearStart;
        var overlapEnd = leave.EndDate < yearEnd ? leave.EndDate : yearEnd;
        if (overlapEnd < overlapStart)
            return 0m;

        var totalCalendarDays = (leave.EndDate - leave.StartDate).Days + 1;
        if (totalCalendarDays <= 0)
            return 0m;

        var overlapCalendarDays = (overlapEnd - overlapStart).Days + 1;
        return R(leave.Days * overlapCalendarDays / totalCalendarDays);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
