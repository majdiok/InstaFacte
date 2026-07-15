using FactuTrust.Application.Subscriptions;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public static class SubscriptionDtoMapper
{
    public static SubscriptionDto ToDto(Subscription subscription)
    {
        var plan = subscription.Plan;
        var maxInvoices = SubscriptionLimits.GetMaxInvoicesPerMonth(plan);
        var maxQuotes = SubscriptionLimits.GetMaxQuotesPerMonth(plan);
        var isUnlimited = plan != SubscriptionPlan.Free;
        var eligibility = SubscriptionInvoiceEligibility.EvaluateForDisplay(subscription);

        return new SubscriptionDto
        {
            Id = subscription.Id,
            Plan = plan.ToString(),
            PlanDisplay = plan.ToDisplayString(),
            Status = subscription.Status.ToString(),
            StatusDisplay = subscription.Status.ToDisplayString(),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            TrialEndDate = subscription.TrialEndDate,
            MonthlyPrice = subscription.MonthlyPrice?.Amount,
            AnnualPrice = subscription.AnnualPrice?.Amount,
            Currency = "TND",
            InvoicesThisMonth = subscription.InvoicesThisMonth,
            CurrentPeriodStart = subscription.CurrentPeriodStart,
            Usage = new SubscriptionUsageDto
            {
                Invoices = new UsageItemDto
                {
                    Label = "Factures ce mois",
                    Used = subscription.InvoicesThisMonth,
                    Limit = maxInvoices,
                    IsUnlimited = isUnlimited
                },
                Quotes = new UsageItemDto
                {
                    Label = "Devis ce mois",
                    Used = 0,
                    Limit = maxQuotes,
                    IsUnlimited = isUnlimited
                },
                Clients = new UsageItemDto
                {
                    Label = "Clients",
                    Used = 0,
                    Limit = isUnlimited ? int.MaxValue : SubscriptionLimits.Free.MaxClients,
                    IsUnlimited = isUnlimited
                },
                Products = new UsageItemDto
                {
                    Label = "Produits",
                    Used = 0,
                    Limit = isUnlimited ? int.MaxValue : SubscriptionLimits.Free.MaxProducts,
                    IsUnlimited = isUnlimited
                },
                Storage = new UsageItemDto
                {
                    Label = "Stockage",
                    Used = 0,
                    Limit = isUnlimited ? int.MaxValue : (int)(SubscriptionLimits.Free.MaxStorageBytes / (1024 * 1024)),
                    IsUnlimited = isUnlimited
                }
            },
            CanCreateInvoice = eligibility.CanCreateInvoice,
            InvoiceBlockCode = eligibility.BlockCode,
            InvoiceBlockMessage = eligibility.BlockMessage
        };
    }
}
