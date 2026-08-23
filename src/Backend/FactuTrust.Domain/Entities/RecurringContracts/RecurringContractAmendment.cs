using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.RecurringContracts;

public sealed class RecurringContractAmendment : Entity
{
    public Guid RecurringContractId { get; private set; }
    public RecurringContractAmendmentType AmendmentType { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public ProrationPolicy ProrationPolicy { get; private set; }
    public string? Notes { get; private set; }
    public string? SnapshotBeforeJson { get; private set; }
    public string? SnapshotAfterJson { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private RecurringContractAmendment() { }

    public static RecurringContractAmendment Create(
        Guid recurringContractId,
        RecurringContractAmendmentType amendmentType,
        DateTime effectiveDate,
        ProrationPolicy prorationPolicy,
        string? notes,
        string? snapshotBeforeJson,
        string? snapshotAfterJson,
        Guid? createdByUserId)
    {
        return new RecurringContractAmendment
        {
            RecurringContractId = recurringContractId,
            AmendmentType = amendmentType,
            EffectiveDate = effectiveDate.Date,
            ProrationPolicy = prorationPolicy,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            SnapshotBeforeJson = snapshotBeforeJson,
            SnapshotAfterJson = snapshotAfterJson,
            CreatedByUserId = createdByUserId
        };
    }
}
