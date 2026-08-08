using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AccountingAudit;

/// <summary>Paramétrage tenant d'une règle de contrôle (seuil, activation).</summary>
public sealed class AccountingControlRuleSetting : Entity
{
    public string RuleCode { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public int? IntThreshold { get; set; }
    public decimal? DecimalThreshold { get; set; }
    public string? JsonOptions { get; set; }
}
