using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class TunisianFiscalDeadlineService : ITunisianFiscalDeadlineService
{
    private readonly MasterDbContext _master;
    private readonly ITunisianCalendarService _calendar;

    public TunisianFiscalDeadlineService(MasterDbContext master, ITunisianCalendarService calendar)
    {
        _master = master;
        _calendar = calendar;
    }

    public DateTime ComputeVatFilingDeadline(int periodYear, int periodMonth, TaxRegime? taxRegime = null)
    {
        var rule = _master.FiscalCalendarRules.AsNoTracking()
            .Where(r => r.IsActive && r.ObligationType == FiscalObligationType.MonthlyDeclaration)
            .Where(r => r.ApplicableTaxRegime == null || r.ApplicableTaxRegime == taxRegime)
            .OrderByDescending(r => r.ApplicableTaxRegime != null)
            .FirstOrDefault();

        var due = rule is not null
            ? rule.ComputeDueDate(periodYear, periodMonth)
            : VatFilingDeadline.ForPeriod(periodYear, periodMonth);

        return rule?.ShiftWeekendsAndHolidays == true ? AdjustForWeekendsAndHolidays(due) : due;
    }

    public DateTime AdjustForWeekendsAndHolidays(DateTime dueDate)
    {
        var adjusted = dueDate.Date;
        var guard = 0;
        while (guard++ < 14 && (adjusted.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || _calendar.IsHoliday(adjusted)))
        {
            adjusted = adjusted.AddDays(1);
        }
        return adjusted;
    }
}
