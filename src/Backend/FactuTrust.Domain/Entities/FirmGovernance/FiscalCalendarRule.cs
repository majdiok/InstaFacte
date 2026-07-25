using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Règle configurable de calendrier fiscal tunisien (Master DB).</summary>
public sealed class FiscalCalendarRule : Entity
{
    public FiscalObligationType ObligationType { get; private set; }
    public TaxRegime? ApplicableTaxRegime { get; private set; }
    public int DueDayOfMonth { get; private set; }
    public int MonthsAfterPeriod { get; private set; }
    public bool ShiftWeekendsAndHolidays { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public string? Label { get; private set; }

    private FiscalCalendarRule() { }

    public static FiscalCalendarRule CreateDefault(
        FiscalObligationType obligationType,
        int dueDayOfMonth,
        int monthsAfterPeriod = 1,
        TaxRegime? taxRegime = null,
        string? label = null)
    {
        return new FiscalCalendarRule
        {
            ObligationType = obligationType,
            ApplicableTaxRegime = taxRegime,
            DueDayOfMonth = dueDayOfMonth,
            MonthsAfterPeriod = monthsAfterPeriod,
            Label = label,
            IsActive = true,
            ShiftWeekendsAndHolidays = true
        };
    }

    public DateTime ComputeDueDate(int periodYear, int periodMonth)
    {
        var baseMonth = new DateTime(periodYear, periodMonth, 1).AddMonths(MonthsAfterPeriod);
        var day = Math.Min(DueDayOfMonth, DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month));
        return new DateTime(baseMonth.Year, baseMonth.Month, day);
    }
}
