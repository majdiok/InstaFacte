using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C1 — Limite d'un plan exprimée en clé-valeur souple.
/// Exemples : <c>MaxInvoicesPerMonth=∞</c>, <c>MaxStorageBytes=5368709120</c>, <c>MaxUsers=5</c>.
/// </summary>
public sealed class PlanLimit : Entity
{
    public Guid PlanId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    private PlanLimit() { }

    public static PlanLimit Create(Guid planId, string key, string value)
    {
        return new PlanLimit
        {
            PlanId = planId,
            Key = (key ?? string.Empty).Trim(),
            Value = value ?? string.Empty
        };
    }
}
