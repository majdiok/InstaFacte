using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class UsageRecord : Entity
{
    public Guid RecurringContractId { get; private set; }
    public Guid UsageMetricId { get; private set; }
    public DateTime PeriodFrom { get; private set; }
    public DateTime PeriodTo { get; private set; }
    public decimal Quantity { get; private set; }
    public UsageRecordSource Source { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public string? Notes { get; private set; }

    private UsageRecord() { }

    public static Result<UsageRecord> Create(
        Guid recurringContractId,
        Guid usageMetricId,
        DateTime periodFrom,
        DateTime periodTo,
        decimal quantity,
        UsageRecordSource source,
        Guid? recordedByUserId = null,
        string? notes = null)
    {
        if (recurringContractId == Guid.Empty)
            return Result.Failure<UsageRecord>(Error.Validation("RecurringContractId", "Le contrat est obligatoire"));
        if (usageMetricId == Guid.Empty)
            return Result.Failure<UsageRecord>(Error.Validation("UsageMetricId", "La métrique est obligatoire"));
        if (periodTo.Date < periodFrom.Date)
            return Result.Failure<UsageRecord>(Error.Validation("PeriodTo", "La fin de période doit être postérieure au début"));
        if (quantity < 0)
            return Result.Failure<UsageRecord>(Error.Validation("Quantity", "La quantité ne peut pas être négative"));

        return Result.Success(new UsageRecord
        {
            RecurringContractId = recurringContractId,
            UsageMetricId = usageMetricId,
            PeriodFrom = periodFrom.Date,
            PeriodTo = periodTo.Date,
            Quantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero),
            Source = source,
            RecordedByUserId = recordedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
    }

    public Result UpdateQuantity(decimal quantity, string? notes = null)
    {
        if (quantity < 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité ne peut pas être négative"));
        Quantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
        return Result.Success();
    }
}
