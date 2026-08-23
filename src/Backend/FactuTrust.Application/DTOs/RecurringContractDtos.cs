using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record RecurringContractListQuery
{
    public string? Search { get; init; }
    public RecurringContractStatus? Status { get; init; }
    public Guid? ClientId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record RecurringContractListItemDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public RecurringContractStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public BillingFrequency BillingFrequency { get; init; }
    public string BillingFrequencyDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? NextBillingDate { get; init; }
    public string Currency { get; init; } = "TND";
    public decimal EstimatedMonthlyAmount { get; init; }
}

public sealed record RecurringContractDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public RecurringContractStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public BillingFrequency BillingFrequency { get; init; }
    public string BillingFrequencyDisplay { get; init; } = null!;
    public int BillingDayOfMonth { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? NextBillingDate { get; init; }
    public DateTime? LastBilledPeriodEnd { get; init; }
    public Guid? PaymentTermTemplateId { get; init; }
    public Guid? PriceListId { get; init; }
    public bool AutoRenew { get; init; }
    public int NoticePeriodDays { get; init; }
    public string Currency { get; init; } = "TND";
    public Guid? SourceQuoteId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public bool SetupFeeBilled { get; init; }
    public IReadOnlyList<RecurringContractLineDto> Lines { get; init; } = Array.Empty<RecurringContractLineDto>();
}

public sealed record RecurringContractLineDto
{
    public Guid Id { get; init; }
    public RecurringContractLineType LineType { get; init; }
    public string LineTypeDisplay { get; init; } = null!;
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal VatRate { get; init; }
    public Guid? UsageMetricId { get; init; }
    public string? UsageMetricName { get; init; }
    public decimal? IncludedQuantity { get; init; }
    public decimal? OverageUnitPriceHT { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpsertRecurringContractDto
{
    public Guid ClientId { get; init; }
    public BillingFrequency BillingFrequency { get; init; }
    public int BillingDayOfMonth { get; init; } = 1;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool AutoRenew { get; init; } = true;
    public int NoticePeriodDays { get; init; } = 30;
    public Guid? PaymentTermTemplateId { get; init; }
    public Guid? PriceListId { get; init; }
    public Guid? SourceQuoteId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<UpsertRecurringContractLineDto> Lines { get; init; } = Array.Empty<UpsertRecurringContractLineDto>();
}

public sealed record UpsertRecurringContractLineDto
{
    public Guid? Id { get; init; }
    public RecurringContractLineType LineType { get; init; }
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal VatRate { get; init; } = 19m;
    public Guid? UsageMetricId { get; init; }
    public decimal? IncludedQuantity { get; init; }
    public decimal? OverageUnitPriceHT { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UsageMetricDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public UsageAggregationMode AggregationMode { get; init; }
    public string AggregationModeDisplay { get; init; } = null!;
    public Guid? ProductId { get; init; }
    public bool IsActive { get; init; }
}

public sealed record UpsertUsageMetricDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public UsageAggregationMode AggregationMode { get; init; }
    public Guid? ProductId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record UsageRecordDto
{
    public Guid Id { get; init; }
    public Guid RecurringContractId { get; init; }
    public Guid UsageMetricId { get; init; }
    public string UsageMetricName { get; init; } = null!;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public UsageRecordSource Source { get; init; }
    public string SourceDisplay { get; init; } = null!;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record RecordUsageDto
{
    public Guid UsageMetricId { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public string? Notes { get; init; }
}

public sealed record RecurringContractBillingRunDto
{
    public Guid Id { get; init; }
    public Guid RecurringContractId { get; init; }
    public string? ContractNumber { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public RecurringContractBillingRunStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public Guid? InvoiceDraftId { get; init; }
    public Guid? InvoiceId { get; init; }
    public decimal FixedAmount { get; init; }
    public decimal UsageAmount { get; init; }
    public decimal ProrationAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record PendingRecurringDraftDto
{
    public Guid BillingRunId { get; init; }
    public Guid RecurringContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid InvoiceDraftId { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed record AmendRecurringContractDto
{
    public RecurringContractAmendmentType AmendmentType { get; init; }
    public DateTime EffectiveDate { get; init; }
    public ProrationPolicy ProrationPolicy { get; init; } = ProrationPolicy.DailyProration;
    public string? Notes { get; init; }
    public UpsertRecurringContractDto? UpdatedContract { get; init; }
}

public sealed record ImportUsageRecordRowDto
{
    public string MetricCode { get; init; } = null!;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public string? Notes { get; init; }
}
