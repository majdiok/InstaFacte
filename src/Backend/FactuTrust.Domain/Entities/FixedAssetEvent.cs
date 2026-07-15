using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

public sealed class FixedAssetEvent : Entity
{
    public Guid FixedAssetId { get; private set; }
    public FixedAsset? FixedAsset { get; private set; }
    public FixedAssetEventType EventType { get; private set; }
    public DateTime EventDate { get; private set; }
    public decimal? Amount { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public string? Notes { get; private set; }
    public string? MetadataJson { get; private set; }

    private FixedAssetEvent() { }

    public static FixedAssetEvent Create(
        Guid fixedAssetId,
        FixedAssetEventType eventType,
        DateTime eventDate,
        decimal? amount,
        Guid? journalEntryId,
        string? notes = null,
        string? metadataJson = null)
    {
        return new FixedAssetEvent
        {
            FixedAssetId = fixedAssetId,
            EventType = eventType,
            EventDate = eventDate.Date,
            Amount = amount,
            JournalEntryId = journalEntryId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            MetadataJson = metadataJson
        };
    }
}