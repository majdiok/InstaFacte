using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Événement de suivi (commentaire, assignation, changement de statut).</summary>
public sealed class AccountingAnomalyActivity : Entity
{
    public Guid AnomalyId { get; set; }
    public AccountingAnomaly Anomaly { get; set; } = null!;
    public AuditActivityType ActivityType { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Message { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
