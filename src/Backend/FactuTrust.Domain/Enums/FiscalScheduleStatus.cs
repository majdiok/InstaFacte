namespace FactuTrust.Domain.Enums;

public enum FiscalScheduleStatus
{
    UpcomingWithin7Days = 0,
    UpcomingAfter7Days = 1,
    Overdue = 2,
    Deposited = 3,
    Paid = 4,
    Validated = 5,
    Cancelled = 9
}
