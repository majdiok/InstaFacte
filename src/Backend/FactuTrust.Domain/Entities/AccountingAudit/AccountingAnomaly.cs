using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Anomalie comptable détectée et suivie dans le workflow d'audit.</summary>
public sealed class AccountingAnomaly : Entity
{
    public Guid RunId { get; set; }
    public AccountingControlRun Run { get; set; } = null!;

    public string RuleCode { get; set; } = null!;
    public string ModuleCode { get; set; } = null!;
    /// <summary>Empreinte stable pour déduplication entre runs.</summary>
    public string Fingerprint { get; set; } = null!;
    public int Severity { get; set; }
    public AnomalyCategory Category { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Impact { get; set; } = null!;
    public string? AccountRef { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public AnomalyStatus Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? DeepLinkRoute { get; set; }
    public string? RecommendationsJson { get; set; }
    public string? IgnoreReason { get; set; }

    public ICollection<AccountingAnomalyLine> Lines { get; set; } = new List<AccountingAnomalyLine>();
    public ICollection<AccountingAnomalyActivity> Activities { get; set; } = new List<AccountingAnomalyActivity>();
}
