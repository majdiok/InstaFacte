using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C1 — Override de module pour un tenant donné.
///
/// Permet d'activer ou de désactiver un module au-delà de ce qu'autorise le plan
/// du tenant (offre commerciale ad hoc négociée par un BillingAdmin).
///
/// Le job Hangfire <c>ExpireModuleOverridesJob</c> retire automatiquement
/// les overrides dont <see cref="ExpiresAt"/> est dépassé.
/// </summary>
public sealed class TenantModuleOverride : Entity
{
    public Guid TenantId { get; private set; }
    public int Module { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public string? Reason { get; private set; }

    private TenantModuleOverride() { }

    public static TenantModuleOverride Create(
        Guid tenantId,
        int module,
        bool isEnabled,
        Guid grantedByUserId,
        DateTime? expiresAt,
        string? reason)
    {
        return new TenantModuleOverride
        {
            TenantId = tenantId,
            Module = module,
            IsEnabled = isEnabled,
            ExpiresAt = expiresAt,
            GrantedByUserId = grantedByUserId,
            Reason = reason?.Trim()
        };
    }

    public void UpdateState(bool isEnabled, DateTime? expiresAt, string? reason)
    {
        IsEnabled = isEnabled;
        ExpiresAt = expiresAt;
        Reason = reason?.Trim();
    }

    /// <summary>Indique si l'override est encore actif (non expiré).</summary>
    public bool IsCurrentlyActive => ExpiresAt is null || ExpiresAt > DateTime.UtcNow;
}
