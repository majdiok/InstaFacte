using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class FiscalYear : AggregateRoot
{
    public int Year { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public string Currency { get; private set; } = "TND";

    private FiscalYear() { }

    public static FiscalYear Create(int year, DateTime startDate, DateTime endDate, string currency = "TND")
    {
        return new FiscalYear
        {
            Id = Guid.NewGuid(),
            Year = year,
            StartDate = startDate,
            EndDate = endDate,
            Currency = currency,
            IsClosed = false
        };
    }

    public void Close() => IsClosed = true;
    public void Reopen() => IsClosed = false;
}
