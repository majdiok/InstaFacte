using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Plan-limit enforcement for Studio resources. Reads the tenant's subscription from
/// <c>MasterDbContext</c> (Subscriptions live in Master) and resolves the limit via
/// <see cref="IPlanResolver"/> (Plans table → fallback). Tenants without a subscription pass through.
/// </summary>
public sealed class StudioQuotaService : IStudioQuotaService
{
    private readonly MasterDbContext _masterDb;
    private readonly IPlanResolver _planResolver;

    public StudioQuotaService(MasterDbContext masterDb, IPlanResolver planResolver)
    {
        _masterDb = masterDb;
        _planResolver = planResolver;
    }

    public async Task<Result> EnsureUnderLimitAsync(
        Guid tenantId, string limitKey, int currentCount, int fallback, string label, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result.Success();

        var sub = await _masterDb.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (sub is null)
            return Result.Success(); // No subscription provisioned → don't block (matches PlanQuotaService).

        var limit = await _planResolver.GetIntLimitAsync(sub.Plan, limitKey, fallback, cancellationToken);
        if (currentCount < limit)
            return Result.Success();

        return Result.Failure(Error.Validation("Plan",
            $"Limite du plan atteinte : {limit} {label} maximum. Mettez à niveau votre abonnement pour en créer davantage."));
    }
}
