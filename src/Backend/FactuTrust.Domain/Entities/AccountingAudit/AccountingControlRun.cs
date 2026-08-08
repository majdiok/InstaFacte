using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Exécution d'un contrôle d'intégrité comptable sur un exercice / période.</summary>
public sealed class AccountingControlRun : Entity
{
    public int FiscalYear { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public string? TriggeredByUserName { get; set; }
    public ControlRunStatus Status { get; set; }
    public decimal ComplianceRate { get; set; }
    public int TotalAnomalies { get; set; }
    public int BlockingCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ModuleCodesFilter { get; set; }

    public ICollection<AccountingAnomaly> Anomalies { get; set; } = new List<AccountingAnomaly>();
}
