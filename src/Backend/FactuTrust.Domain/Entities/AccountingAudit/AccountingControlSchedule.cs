using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Planification d'un contrôle automatique (Hangfire).</summary>
public sealed class AccountingControlSchedule : Entity
{
    public string Name { get; set; } = null!;
    public string CronExpression { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int? FiscalYearOffset { get; set; }
    public string? ModuleCodesFilter { get; set; }
    public string? NotifyEmails { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? LastRunAt { get; set; }
}
