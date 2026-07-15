using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C1 — Module fonctionnel inclus (ou non) dans un plan.
/// <see cref="Module"/> stocke la valeur entière de <see cref="FactuTrust.Domain.Enums.AppModule"/>
/// pour rester découplé de l'enum (résiste aux ajouts futurs).
/// </summary>
public sealed class PlanModule : Entity
{
    public Guid PlanId { get; private set; }
    public int Module { get; private set; }
    public bool IsIncluded { get; private set; }

    private PlanModule() { }

    public static PlanModule Create(Guid planId, int module, bool isIncluded)
    {
        return new PlanModule
        {
            PlanId = planId,
            Module = module,
            IsIncluded = isIncluded
        };
    }
}
