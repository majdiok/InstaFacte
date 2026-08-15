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

    /// <summary>
    /// Nombre de règles réellement évaluées par ce run (activées ∩ dans le périmètre de modules).
    /// Dénominateur de <see cref="ComplianceRate"/> : sans lui, ajouter des règles au catalogue
    /// ferait bondir le taux de tous les tenants et rendrait deux exercices incomparables.
    /// 0 sur les runs antérieurs à la colonne — le taux historique est alors laissé tel quel.
    /// </summary>
    public int EvaluatedRuleCount { get; set; }

    public string? ErrorMessage { get; set; }
    public string? ModuleCodesFilter { get; set; }

    public ICollection<AccountingAnomaly> Anomalies { get; set; } = new List<AccountingAnomaly>();
}
