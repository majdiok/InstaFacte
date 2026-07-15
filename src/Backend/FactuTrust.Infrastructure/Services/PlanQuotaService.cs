using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Subscriptions;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Lot C1 — Implémentation EF Core de <see cref="IPlanQuotaService"/>.
/// Lit/écrit dans <c>MasterDbContext.Subscriptions</c> (la table vit en Master, pas Tenant).
/// </summary>
public sealed class PlanQuotaService : IPlanQuotaService
{
    public const string QuotaExceededCode = SubscriptionInvoiceEligibility.QuotaExceededCode;
    public const string SubscriptionCancelledCode = SubscriptionInvoiceEligibility.SubscriptionCancelledCode;
    public const string SubscriptionSuspendedCode = SubscriptionInvoiceEligibility.SubscriptionSuspendedCode;
    public const string SubscriptionExpiredCode = SubscriptionInvoiceEligibility.SubscriptionExpiredCode;
    public const string SubscriptionPastDueCode = SubscriptionInvoiceEligibility.SubscriptionPastDueCode;

    private readonly MasterDbContext _masterDb;
    private readonly IPlanResolver _planResolver;
    private readonly ILogger<PlanQuotaService> _logger;

    public PlanQuotaService(
        MasterDbContext masterDb,
        IPlanResolver planResolver,
        ILogger<PlanQuotaService> logger)
    {
        _masterDb = masterDb;
        _planResolver = planResolver;
        _logger = logger;
    }

    public async Task<Result> EnsureCanCreateInvoiceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            // Pas de tenant contextualisé → on n'applique pas de quota (les jobs système,
            // migrations, etc. ne sont pas concernés).
            return Result.Success();
        }

        var sub = await _masterDb.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (sub is null)
        {
            // Tenant sans subscription : on laisse passer (cas tests d'intégration ou
            // tenants pré-Lot C1 jamais provisionnés). À monitorer.
            _logger.LogWarning(
                "PlanQuota: aucune Subscription trouvée pour le tenant {TenantId}, quota ignoré.",
                tenantId);
            return Result.Success();
        }

        sub.ResetMonthlyCounter();

        var enumFallback = SubscriptionLimits.GetMaxInvoicesPerMonth(sub.Plan);
        var limit = await _planResolver.GetIntLimitAsync(
            sub.Plan, "MaxInvoicesPerMonth", fallback: enumFallback, cancellationToken);

        var eligibility = SubscriptionInvoiceEligibility.Evaluate(sub, limit);
        var error = SubscriptionInvoiceEligibility.ToError(eligibility);
        if (error is not null)
            return Result.Failure(error);

        return Result.Success();
    }

    public async Task OnInvoiceCreatedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return;

        var sub = await _masterDb.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (sub is null) return;

        sub.ResetMonthlyCounter();
        sub.IncrementInvoiceCount();
        await _masterDb.SaveChangesAsync(cancellationToken);
    }
}
