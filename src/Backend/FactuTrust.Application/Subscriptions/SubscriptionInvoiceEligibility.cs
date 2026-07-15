using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Subscriptions;

/// <summary>
/// Single source of truth for invoice creation eligibility (status + monthly quota).
/// Used by <see cref="FactuTrust.Infrastructure.Services.PlanQuotaService"/> and subscription DTO mapping.
/// </summary>
public static class SubscriptionInvoiceEligibility
{
    public const string QuotaExceededCode = "PLAN_INVOICE_QUOTA_EXCEEDED";
    public const string SubscriptionCancelledCode = "PLAN_SUBSCRIPTION_CANCELLED";
    public const string SubscriptionSuspendedCode = "PLAN_SUBSCRIPTION_SUSPENDED";
    public const string SubscriptionExpiredCode = "PLAN_SUBSCRIPTION_EXPIRED";
    public const string SubscriptionPastDueCode = "PLAN_SUBSCRIPTION_PAST_DUE";

    public sealed record InvoiceEligibility(
        bool CanCreateInvoice,
        string? BlockCode,
        string? BlockMessage);

    /// <summary>
    /// Evaluates whether the tenant can create another invoice this month.
    /// Call after <see cref="Subscription.ResetMonthlyCounter"/> when mutating state.
    /// </summary>
    public static InvoiceEligibility Evaluate(Subscription subscription, int resolvedInvoiceLimit)
    {
        var statusError = GetStatusBlockError(subscription);
        if (statusError is not null)
        {
            return new InvoiceEligibility(
                CanCreateInvoice: false,
                BlockCode: statusError.Code,
                BlockMessage: statusError.Description);
        }

        if (subscription.InvoicesThisMonth < resolvedInvoiceLimit)
        {
            return new InvoiceEligibility(
                CanCreateInvoice: true,
                BlockCode: null,
                BlockMessage: null);
        }

        var message = resolvedInvoiceLimit >= int.MaxValue / 2
            ? "Création de facture refusée par le plan en cours."
            : $"Limite mensuelle atteinte ({subscription.InvoicesThisMonth}/{resolvedInvoiceLimit} factures). " +
              "Passez à un forfait supérieur ou attendez le prochain cycle.";

        return new InvoiceEligibility(
            CanCreateInvoice: false,
            BlockCode: QuotaExceededCode,
            BlockMessage: message);
    }

    /// <summary>
    /// Display-time evaluation using enum fallback limits (aligned with subscription usage DTO).
    /// </summary>
    public static InvoiceEligibility EvaluateForDisplay(Subscription subscription)
    {
        subscription.ResetMonthlyCounter();
        var limit = SubscriptionLimits.GetMaxInvoicesPerMonth(subscription.Plan);
        return Evaluate(subscription, limit);
    }

    public static Error? ToError(InvoiceEligibility eligibility)
    {
        if (eligibility.CanCreateInvoice || eligibility.BlockCode is null)
            return null;

        return new Error(eligibility.BlockCode, eligibility.BlockMessage ?? "Création de facture refusée.");
    }

    private static Error? GetStatusBlockError(Subscription sub) => sub.Status switch
    {
        SubscriptionStatus.Cancelled => new Error(
            SubscriptionCancelledCode,
            "Votre abonnement est annulé. Réactivez ou changez de forfait dans Paramètres > Abonnement pour émettre des factures."),
        SubscriptionStatus.Suspended => new Error(
            SubscriptionSuspendedCode,
            "Votre compte est suspendu. Contactez le support ou régularisez votre abonnement dans Paramètres > Abonnement."),
        SubscriptionStatus.Expired => new Error(
            SubscriptionExpiredCode,
            "Votre abonnement a expiré. Renouvelez ou changez de forfait dans Paramètres > Abonnement pour continuer à émettre des factures."),
        SubscriptionStatus.PastDue => new Error(
            SubscriptionPastDueCode,
            "Votre paiement est en retard. Régularisez votre abonnement dans Paramètres > Abonnement pour émettre de nouvelles factures."),
        _ => null
    };
}
