using System.ComponentModel.DataAnnotations;

namespace FactuTrust.Application.DTOs;

public sealed record SubscriptionDto
{
    public Guid Id { get; init; }
    public string Plan { get; init; } = null!;
    public string PlanDisplay { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? TrialEndDate { get; init; }
    public decimal? MonthlyPrice { get; init; }
    public decimal? AnnualPrice { get; init; }
    public string Currency { get; init; } = "TND";
    public int InvoicesThisMonth { get; init; }
    public DateTime CurrentPeriodStart { get; init; }
    public SubscriptionUsageDto Usage { get; init; } = null!;
    public bool CanCreateInvoice { get; init; }
    public string? InvoiceBlockCode { get; init; }
    public string? InvoiceBlockMessage { get; init; }
}

public sealed record SubscriptionUsageDto
{
    public UsageItemDto Invoices { get; init; } = null!;
    public UsageItemDto Quotes { get; init; } = null!;
    public UsageItemDto Clients { get; init; } = null!;
    public UsageItemDto Products { get; init; } = null!;
    public UsageItemDto Storage { get; init; } = null!;
}

public sealed record UsageItemDto
{
    public string Label { get; init; } = null!;
    public int Used { get; init; }
    public int Limit { get; init; }
    public bool IsUnlimited { get; init; }
}

public sealed record SubscriptionPlanOptionDto
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public decimal PriceMonthly { get; init; }
    public decimal? PriceAnnual { get; init; }
    public string Currency { get; init; } = "TND";
    public string Period { get; init; } = null!;
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DisabledFeatures { get; init; } = Array.Empty<string>();
    public bool IsPopular { get; init; }
    public bool IsCurrent { get; init; }
    public int? SavePercentage { get; init; }
    public PlanLimitsDto Limits { get; init; } = null!;
}

public sealed record PlanLimitsDto
{
    public int MaxInvoicesPerMonth { get; init; }
    public int MaxQuotesPerMonth { get; init; }
    public int MaxClients { get; init; }
    public int MaxProducts { get; init; }
    public long MaxStorageBytes { get; init; }
    public bool ElectronicSignature { get; init; }
    public bool XmlExport { get; init; }
    public bool PaymentTracking { get; init; }
    public bool PrioritySupport { get; init; }
    public bool IsUnlimited { get; init; }
}

public sealed record ChangePlanRequest
{
    [Required(ErrorMessage = "Le plan est obligatoire")]
    public string Plan { get; init; } = null!;
}

public sealed record CancelSubscriptionRequest
{
    [Required(ErrorMessage = "La raison d'annulation est obligatoire")]
    [MinLength(10, ErrorMessage = "La raison doit contenir au moins 10 caractères")]
    public string Reason { get; init; } = null!;
}

public sealed record BillingHistoryItemDto
{
    public string Id { get; init; } = null!;
    public DateTime Date { get; init; }
    public string Description { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TND";
    public string Status { get; init; } = null!;
}
