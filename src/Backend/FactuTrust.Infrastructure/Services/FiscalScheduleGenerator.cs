using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

public sealed class FiscalScheduleGenerator : IFiscalScheduleGenerator
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ITunisianFiscalDeadlineService _deadlines;

    public FiscalScheduleGenerator(
        IFiscalScheduleRepository repository,
        ICurrentUser currentUser,
        ITunisianFiscalDeadlineService deadlines)
    {
        _repository = repository;
        _currentUser = currentUser;
        _deadlines = deadlines;
    }

    public async Task<Result<int>> EnsureFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<int>(Error.Validation("FiscalYear", "L'exercice doit etre compris entre 2000 et 2100."));

        var created = 0;
        foreach (var seed in BuildSeeds(fiscalYear, _deadlines))
        {
            var exists = await _repository.ExistsAsync(
                seed.ObligationType,
                seed.FiscalYear,
                seed.PeriodMonth,
                seed.PeriodQuarter,
                seed.SourceType,
                cancellationToken);
            if (exists)
                continue;

            var create = FiscalScheduleEntry.Create(
                seed.ObligationType,
                FiscalScheduleMappings.GetObligationDisplay(seed.ObligationType),
                seed.FiscalYear,
                seed.DueDate,
                0m,
                periodMonth: seed.PeriodMonth,
                periodQuarter: seed.PeriodQuarter,
                periodStart: seed.PeriodStart,
                periodEnd: seed.PeriodEnd,
                sourceType: seed.SourceType,
                observations: "Generee automatiquement");

            if (create.IsFailure)
                return Result.Failure<int>(create.Error);

            var entry = create.Value;
            entry.SetAuditInfo(_currentUser.Email ?? "system", false);
            await _repository.AddAsync(
                entry,
                FiscalScheduleHistoryEntry.Create(entry.Id, "Created", "Echeance fiscale generee automatiquement."),
                cancellationToken);
            created++;
        }

        return Result.Success(created);
    }

    private static IReadOnlyList<FiscalScheduleSeed> BuildSeeds(int fiscalYear, ITunisianFiscalDeadlineService deadlines)
    {
        var seeds = new List<FiscalScheduleSeed>();

        for (var month = 1; month <= 12; month++)
        {
            var start = new DateTime(fiscalYear, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var due = deadlines.ComputeVatFilingDeadline(fiscalYear, month);

            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.MonthlyDeclaration,
                fiscalYear,
                due,
                month,
                null,
                start,
                end,
                FiscalScheduleSourceType.VatDeclaration));

            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.WithholdingTax,
                fiscalYear,
                due,
                month,
                null,
                start,
                end,
                FiscalScheduleSourceType.WithholdingTaxTej));

            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.Fodec,
                fiscalYear,
                due,
                month,
                null,
                start,
                end,
                FiscalScheduleSourceType.Manual));

            // Retenue IRPP salariés : le 28 du mois suivant la période de paie.
            var irppDueMonth = month == 12 ? 1 : month + 1;
            var irppDueYear = month == 12 ? fiscalYear + 1 : fiscalYear;
            var irppDue = new DateTime(irppDueYear, irppDueMonth, 28);
            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.PayrollIrppWithholding,
                fiscalYear,
                irppDue,
                month,
                null,
                start,
                end,
                FiscalScheduleSourceType.Payroll));

            // Versement CNSS : le 15 du mois suivant la période de paie.
            var cnssDueMonth = month == 12 ? 1 : month + 1;
            var cnssDueYear = month == 12 ? fiscalYear + 1 : fiscalYear;
            var cnssDue = new DateTime(cnssDueYear, cnssDueMonth, 15);
            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.CnssMonthlyRemittance,
                fiscalYear,
                cnssDue,
                month,
                null,
                start,
                end,
                FiscalScheduleSourceType.Payroll));
        }

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var lastMonthOfQuarter = quarter * 3;
            var start = new DateTime(fiscalYear, lastMonthOfQuarter - 2, 1);
            var end = new DateTime(fiscalYear, lastMonthOfQuarter, DateTime.DaysInMonth(fiscalYear, lastMonthOfQuarter));
            var dtsDueMonth = lastMonthOfQuarter == 12 ? 1 : lastMonthOfQuarter + 1;
            var dtsDueYear = lastMonthOfQuarter == 12 ? fiscalYear + 1 : fiscalYear;
            var dtsDue = new DateTime(dtsDueYear, dtsDueMonth, 15);

            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.CnssDtsQuarterly,
                fiscalYear,
                dtsDue,
                null,
                quarter,
                start,
                end,
                FiscalScheduleSourceType.Payroll));
        }

        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var month = (quarter - 1) * 3 + 1;
            var start = new DateTime(fiscalYear, month, 1);
            var end = start.AddMonths(3).AddDays(-1);
            seeds.Add(new FiscalScheduleSeed(
                FiscalObligationType.QuarterlyVat,
                fiscalYear,
                deadlines.ComputeVatFilingDeadline(end.Year, end.Month),
                null,
                quarter,
                start,
                end,
                FiscalScheduleSourceType.VatDeclaration));
        }

        seeds.Add(new FiscalScheduleSeed(
            FiscalObligationType.FinancialStatements,
            fiscalYear,
            new DateTime(fiscalYear, 3, 31),
            null,
            null,
            new DateTime(fiscalYear - 1, 1, 1),
            new DateTime(fiscalYear - 1, 12, 31),
            FiscalScheduleSourceType.NctStatements));

        seeds.Add(new FiscalScheduleSeed(
            FiscalObligationType.SemiAnnualFinancialStatements,
            fiscalYear,
            new DateTime(fiscalYear, 8, 31),
            null,
            null,
            new DateTime(fiscalYear, 1, 1),
            new DateTime(fiscalYear, 6, 30),
            FiscalScheduleSourceType.NctStatements));

        return seeds;
    }

    private sealed record FiscalScheduleSeed(
        FiscalObligationType ObligationType,
        int FiscalYear,
        DateTime DueDate,
        int? PeriodMonth,
        int? PeriodQuarter,
        DateTime? PeriodStart,
        DateTime? PeriodEnd,
        FiscalScheduleSourceType SourceType);
}
