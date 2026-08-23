using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class RecurringContractBillingRun : Entity
{
    public Guid RecurringContractId { get; private set; }
    public DateTime PeriodFrom { get; private set; }
    public DateTime PeriodTo { get; private set; }
    public RecurringContractBillingRunStatus Status { get; private set; }
    public Guid? InvoiceDraftId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public decimal FixedAmount { get; private set; }
    public decimal UsageAmount { get; private set; }
    public decimal ProrationAmount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public decimal TotalAmount => FixedAmount + UsageAmount + ProrationAmount;

    private RecurringContractBillingRun() { }

    public static RecurringContractBillingRun CreatePending(
        Guid recurringContractId,
        DateTime periodFrom,
        DateTime periodTo)
    {
        return new RecurringContractBillingRun
        {
            RecurringContractId = recurringContractId,
            PeriodFrom = periodFrom.Date,
            PeriodTo = periodTo.Date,
            Status = RecurringContractBillingRunStatus.Pending
        };
    }

    public void MarkDraftCreated(Guid invoiceDraftId, decimal fixedAmount, decimal usageAmount, decimal prorationAmount)
    {
        InvoiceDraftId = invoiceDraftId;
        FixedAmount = decimal.Round(fixedAmount, 3, MidpointRounding.AwayFromZero);
        UsageAmount = decimal.Round(usageAmount, 3, MidpointRounding.AwayFromZero);
        ProrationAmount = decimal.Round(prorationAmount, 3, MidpointRounding.AwayFromZero);
        Status = RecurringContractBillingRunStatus.DraftCreated;
        ErrorMessage = null;
    }

    public void MarkInvoiced(Guid invoiceId)
    {
        InvoiceId = invoiceId;
        Status = RecurringContractBillingRunStatus.Invoiced;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = RecurringContractBillingRunStatus.Failed;
        ErrorMessage = errorMessage?.Length > 2000 ? errorMessage[..2000] : errorMessage;
    }

    public void MarkSkipped(string reason)
    {
        Status = RecurringContractBillingRunStatus.Skipped;
        ErrorMessage = reason?.Length > 2000 ? reason[..2000] : reason;
    }
}
