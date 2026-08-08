using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Ligne d'écriture concernée par une anomalie.</summary>
public sealed class AccountingAnomalyLine : Entity
{
    public Guid AnomalyId { get; set; }
    public AccountingAnomaly Anomaly { get; set; } = null!;
    public Guid? JournalEntryId { get; set; }
    public Guid? JournalEntryLineId { get; set; }
    public DateTime? EntryDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? Label { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? PieceRef { get; set; }
    public string? JustificationStatus { get; set; }
}
