using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C1 — Feature flag d'un plan (booléen).
/// Exemples : <c>ElectronicSignature</c>, <c>XmlExport</c>, <c>PaymentTracking</c>, <c>PrioritySupport</c>.
/// </summary>
public sealed class PlanFeature : Entity
{
    public Guid PlanId { get; private set; }
    public string FeatureKey { get; private set; } = null!;
    public bool Enabled { get; private set; }

    private PlanFeature() { }

    public static PlanFeature Create(Guid planId, string featureKey, bool enabled)
    {
        return new PlanFeature
        {
            PlanId = planId,
            FeatureKey = (featureKey ?? string.Empty).Trim(),
            Enabled = enabled
        };
    }
}
