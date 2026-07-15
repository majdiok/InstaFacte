using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Resolves subscription plan from Master DB for a tenant.
/// Used to enforce quote limits per plan (e.g. Free: 10 devis/mois).
/// </summary>
public sealed class SubscriptionResolver : ISubscriptionResolver
{
    private readonly MasterDbContext _masterDb;

    public SubscriptionResolver(MasterDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    public async Task<SubscriptionPlan> GetPlanForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sub = await _masterDb.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(cancellationToken);

        return sub;
    }
}
