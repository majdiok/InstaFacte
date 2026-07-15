using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C6 — Renouvellement manuel + extension de période de grâce.
///
/// <list type="bullet">
///   <item><see cref="RenewNowAsync"/> : génère un brouillon de facture, l'émet, étend EndDate.</item>
///   <item><see cref="ExtendGraceAsync"/> : repousse <c>NextActionAt</c> du DunningState courant
///         (utile si l'admin négocie un délai supplémentaire avec le tenant).</item>
/// </list>
/// </summary>
public sealed class SubscriptionRenewalService : ISubscriptionRenewalService
{
    private readonly MasterDbContext _db;
    private readonly IPlatformInvoiceAdminService _invoiceAdmin;
    private readonly ILogger<SubscriptionRenewalService> _logger;

    public SubscriptionRenewalService(
        MasterDbContext db,
        IPlatformInvoiceAdminService invoiceAdmin,
        ILogger<SubscriptionRenewalService> logger)
    {
        _db = db;
        _invoiceAdmin = invoiceAdmin;
        _logger = logger;
    }

    public async Task<Result> RenewNowAsync(Guid subscriptionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
        if (subscription is null) return Result.Failure(Error.NotFound(nameof(Subscription), subscriptionId));

        if (subscription.Plan == SubscriptionPlan.Free)
            return Result.Failure(Error.Validation("Plan", "Le plan gratuit ne nécessite pas de renouvellement."));

        // Génère le brouillon de facture pour la période suivante.
        // Priorité au prix figé sur la subscription, fallback sur les valeurs par défaut.
        var amountTnd = subscription.Plan switch
        {
            SubscriptionPlan.Annual => subscription.AnnualPrice?.Amount ?? 468m,
            SubscriptionPlan.Monthly => subscription.MonthlyPrice?.Amount ?? 49m,
            _ => 0m
        };
        var description = subscription.Plan == SubscriptionPlan.Annual
            ? "Renouvellement annuel — abonnement FactuTrust"
            : "Renouvellement mensuel — abonnement FactuTrust";

        var nextPeriodStart = subscription.EndDate ?? DateTime.UtcNow;
        var nextPeriodEnd = subscription.Plan == SubscriptionPlan.Annual
            ? nextPeriodStart.AddYears(1)
            : nextPeriodStart.AddMonths(1);

        var draftRequest = new CreatePlatformInvoiceRequest
        {
            TenantId = subscription.TenantId,
            BillingType = PlatformInvoiceBillingType.Subscription,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            PeriodFrom = nextPeriodStart,
            PeriodTo = nextPeriodEnd,
            DiscountAmount = 0m,
            CreditsApplied = 0m,
            Lines = new[]
            {
                new CreatePlatformInvoiceLineRequest
                {
                    Description = description,
                    Quantity = 1,
                    UnitPriceHT = amountTnd,
                    VatRate = 19m
                }
            }
        };

        var draftResult = await _invoiceAdmin.CreateDraftAsync(draftRequest, actorUserId, cancellationToken);
        if (draftResult.IsFailure)
        {
            _logger.LogWarning("RenewNow: draft creation failed for subscription {Id}: {Err}", subscriptionId, draftResult.Error.Description);
            return Result.Failure(draftResult.Error);
        }

        var issued = await _invoiceAdmin.IssueAsync(draftResult.Value.Id, cancellationToken);
        if (issued.IsFailure)
        {
            _logger.LogWarning("RenewNow: issue failed for subscription {Id}: {Err}", subscriptionId, issued.Error.Description);
            return Result.Failure(issued.Error);
        }

        // Renouvelle l'abonnement (étend EndDate).
        var renewResult = subscription.Renew();
        if (renewResult.IsFailure)
            return Result.Failure(renewResult.Error);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Subscription {Id} renewed via {Number}", subscriptionId, issued.Value.Number);
        return Result.Success();
    }

    public async Task<Result> ExtendGraceAsync(Guid subscriptionId, int days, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (days <= 0 || days > 30)
            return Result.Failure(Error.Validation("Days", "Le délai doit être entre 1 et 30 jours."));

        var state = await _db.DunningStates
            .Where(s => s.SubscriptionId == subscriptionId && s.Outcome == DunningOutcome.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null) return Result.Failure(Error.NotFound(nameof(DunningState), subscriptionId));

        // On ne change pas l'index, juste on repousse NextActionAt.
        state.AdvanceTo(state.CurrentStepIndex, state.NextActionAt.AddDays(days));
        state.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grace extended for subscription {Id} by {Days} days", subscriptionId, days);
        return Result.Success();
    }
}
