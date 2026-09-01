namespace FactuTrust.Domain.Entities;

/// <summary>
/// Plan §3.3 — per-tenant dismissal of a module usage recommendation
/// (<c>ModuleUsageRecommendationService</c>). Once a tenant dismisses a recommendation for a given
/// module, it must never resurface for that tenant again (natural key: TenantId + Module).
/// Master-DB scoped, mirroring <see cref="ModuleGrantAuditEntry"/> (module configuration itself is
/// master-DB data).
/// </summary>
public sealed class ModuleRecommendationDismissal
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary><see cref="Enums.AppModule"/> value that was dismissed.</summary>
    public int Module { get; private set; }

    public DateTime DismissedAtUtc { get; private set; }

    /// <summary>User id who dismissed the recommendation. Null if system-initiated.</summary>
    public Guid? DismissedByUserId { get; private set; }

    private ModuleRecommendationDismissal() { }

    public static ModuleRecommendationDismissal Create(Guid tenantId, int module, Guid? dismissedByUserId)
    {
        return new ModuleRecommendationDismissal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Module = module,
            DismissedAtUtc = DateTime.UtcNow,
            DismissedByUserId = dismissedByUserId
        };
    }
}
