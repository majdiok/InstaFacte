using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class SubscriptionAdminService : ISubscriptionAdminService
{
    private readonly MasterDbContext _db;
    private readonly ILogger<SubscriptionAdminService> _logger;

    public SubscriptionAdminService(MasterDbContext db, ILogger<SubscriptionAdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<Subscription>> ChangePlanAsync(Guid tenantId, SubscriptionPlan newPlan, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            subscription = Subscription.CreateFree(tenantId);
            _db.Subscriptions.Add(subscription);
            await _db.SaveChangesAsync(cancellationToken);
        }

        Result domainResult;
        if (newPlan == SubscriptionPlan.Free)
            domainResult = subscription.DowngradeToFree();
        else if (newPlan == subscription.Plan &&
                 subscription.Status is SubscriptionStatus.Suspended or SubscriptionStatus.PastDue)
            domainResult = subscription.Reactivate();
        else
        {
            var price = newPlan == SubscriptionPlan.Monthly
                ? Money.Create(49m)
                : Money.Create(468m);
            domainResult = subscription.UpgradeTo(newPlan, price);
        }

        if (domainResult.IsFailure)
            return Result.Failure<Subscription>(domainResult.Error);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subscription changed to {Plan} for tenant {TenantId}", newPlan, tenantId);
        return Result.Success(subscription);
    }

    public async Task<Result<Subscription>> CancelAsync(Guid tenantId, string reason, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (subscription is null)
            return Result.Failure<Subscription>(new Error("Subscription.NotFound", "Aucun abonnement trouvé"));

        var domainResult = subscription.Cancel(reason);
        if (domainResult.IsFailure)
            return Result.Failure<Subscription>(domainResult.Error);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subscription cancelled for tenant {TenantId}. Reason: {Reason}", tenantId, reason);
        return Result.Success(subscription);
    }
}
