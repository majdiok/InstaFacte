using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Émis lorsqu'un cycle de paie est validé (déclenche la génération des écritures comptables).
/// </summary>
public sealed class PayrollRunValidatedEvent : DomainEvent
{
    public Guid PayrollRunId { get; }
    public int Year { get; }
    public int Month { get; }

    public PayrollRunValidatedEvent(Guid payrollRunId, int year, int month)
    {
        PayrollRunId = payrollRunId;
        Year = year;
        Month = month;
    }
}

/// <summary>
/// Émis lorsqu'un cycle de paie est clôturé (période verrouillée).
/// </summary>
public sealed class PayrollRunClosedEvent : DomainEvent
{
    public Guid PayrollRunId { get; }
    public int Year { get; }
    public int Month { get; }

    public PayrollRunClosedEvent(Guid payrollRunId, int year, int month)
    {
        PayrollRunId = payrollRunId;
        Year = year;
        Month = month;
    }
}
