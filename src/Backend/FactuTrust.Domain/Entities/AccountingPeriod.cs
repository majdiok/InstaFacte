using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Monthly accounting period (clôture).
/// </summary>
public sealed class AccountingPeriod : Entity
{
    public int FiscalYear { get; private set; }
    public int Month { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }

    private AccountingPeriod() { }

    public static AccountingPeriod Create(int fiscalYear, int month, DateTime startDate, DateTime endDate)
    {
        return new AccountingPeriod
        {
            FiscalYear = fiscalYear,
            Month = month,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            IsClosed = false
        };
    }

    public void Close(string closedBy)
    {
        if (IsClosed)
            return;
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        ClosedBy = closedBy;
    }

    public void Reopen()
    {
        IsClosed = false;
        ClosedAt = null;
        ClosedBy = null;
    }
}
