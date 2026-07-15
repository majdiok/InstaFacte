namespace FactuTrust.Application.DTOs;

/// <summary>
/// Aggregated totals for a sales-activity list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record ActivityListSummaryDto
{
    public int Count { get; init; }
    public int OpenCount { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }
}

public sealed record OpportunityDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = null!;
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid AssignedUserId { get; init; }
    public string AssignedUserName { get; init; } = null!;
    public int Stage { get; init; }
    public string StageName { get; init; } = null!;
    public string StageColor { get; init; } = null!;
    public decimal ExpectedAmount { get; init; }
    public int Probability { get; init; }
    public decimal WeightedAmount { get; init; }
    public DateTime ExpectedCloseDate { get; init; }
    public DateTime? ActualCloseDate { get; init; }
    public string? LostReason { get; init; }
    public Guid? LinkedQuoteId { get; init; }
    public Guid? LinkedInvoiceId { get; init; }
    public string? Notes { get; init; }
    public string? Source { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record PipelineStageSummaryDto
{
    public int Stage { get; init; }
    public string StageName { get; init; } = null!;
    public int Count { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal WeightedAmount { get; init; }
}

public sealed record PipelineSummaryDto
{
    public IReadOnlyList<PipelineStageSummaryDto> ByStage { get; init; } = Array.Empty<PipelineStageSummaryDto>();
    public decimal TotalWeightedAmount { get; init; }
    public decimal ConversionRate { get; init; }
    public double AverageCycleDays { get; init; }
    public int TotalOpen { get; init; }
}

public sealed record CrmAssignableUserDto
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = null!;
}

public sealed record SalesActivityDto
{
    public Guid Id { get; init; }
    public int Type { get; init; }
    public string TypeName { get; init; } = null!;
    public string TypeIcon { get; init; } = null!;
    public string Subject { get; init; } = null!;
    public string? Description { get; init; }
    public Guid ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid? OpportunityId { get; init; }
    public Guid AssignedUserId { get; init; }
    public string AssignedUserName { get; init; } = null!;
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool IsCompleted { get; init; }
    public int Priority { get; init; }
    public string PriorityName { get; init; } = null!;
    public string PriorityColor { get; init; } = null!;
    public DateTime? ReminderDate { get; init; }
    public string? LinkedEntityType { get; init; }
    public Guid? LinkedEntityId { get; init; }
}

public sealed record SalesTargetDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetAmount { get; init; }
    public decimal AchievedAmount { get; init; }
    public decimal ProgressPercent { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record LeaderboardEntryDto
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public decimal TargetAmount { get; init; }
    public decimal AchievedAmount { get; init; }
    public decimal ProgressPercent { get; init; }
    public int Rank { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record QuoteTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? DefaultNotes { get; init; }
    public string? DefaultTermsAndConditions { get; init; }
    public int DefaultValidityDays { get; init; }
    public bool IsActive { get; init; }
    public int UsageCount { get; init; }
    public IReadOnlyList<QuoteTemplateLineDto> Lines { get; init; } = Array.Empty<QuoteTemplateLineDto>();
}

public sealed record QuoteTemplateLineDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string? ProductName { get; init; }
    public decimal Quantity { get; init; }
    public decimal? CustomUnitPrice { get; init; }
    public decimal? DiscountPercent { get; init; }
    public int SortOrder { get; init; }
}

public sealed record ClientTimelineItemDto
{
    public string Type { get; init; } = null!;
    public string Icon { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public DateTime Date { get; init; }
    public Guid? EntityId { get; init; }
}

public sealed record SalesDashboardDto
{
    public decimal MyRevenueThisMonth { get; init; }
    public decimal MyRevenueLastMonth { get; init; }
    public decimal RevenueDeltaPercent { get; init; }
    public decimal? MonthlyTarget { get; init; }
    public decimal? TargetProgressPercent { get; init; }
    public int PendingQuotesCount { get; init; }
    public decimal PendingQuotesAmount { get; init; }
    public decimal QuoteConversionRate { get; init; }
    public int RemindersToday { get; init; }
    public decimal PipelineWeightedTotal { get; init; }
    public IReadOnlyList<OpportunityDto> TopOpportunities { get; init; } = Array.Empty<OpportunityDto>();
    public int NewClientsThisMonth { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record CreateOpportunityRequest
{
    public string Title { get; init; } = null!;
    public Guid ClientId { get; init; }
    public decimal ExpectedAmount { get; init; }
    public int Probability { get; init; }
    public DateTime ExpectedCloseDate { get; init; }
    public string? Source { get; init; }
    public string? Notes { get; init; }
}

public sealed record UpdateOpportunityRequest
{
    public string Title { get; init; } = null!;
    public decimal ExpectedAmount { get; init; }
    public int Probability { get; init; }
    public DateTime ExpectedCloseDate { get; init; }
    public string? Source { get; init; }
    public string? Notes { get; init; }
}

public sealed record CreateActivityRequest
{
    public int Type { get; init; }
    public string Subject { get; init; } = null!;
    public string? Description { get; init; }
    public Guid ClientId { get; init; }
    public Guid? OpportunityId { get; init; }
    /// <summary>When null, the activity is assigned to the current user.</summary>
    public Guid? AssignedUserId { get; init; }
    public int Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReminderDate { get; init; }
    public string? LinkedEntityType { get; init; }
    public Guid? LinkedEntityId { get; init; }
}

public sealed record UpdateActivityRequest
{
    public int Type { get; init; }
    public string Subject { get; init; } = null!;
    public string? Description { get; init; }
    public int Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReminderDate { get; init; }
    public Guid? OpportunityId { get; init; }
    /// <summary>When null, assignee is unchanged.</summary>
    public Guid? AssignedUserId { get; init; }
}

public sealed record CreateSalesTargetRequest
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetAmount { get; init; }
}

public sealed record CreateQuoteTemplateRequest
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? DefaultNotes { get; init; }
    public string? DefaultTermsAndConditions { get; init; }
    public int DefaultValidityDays { get; init; }
    public IReadOnlyList<CreateQuoteTemplateLineRequest> Lines { get; init; } = Array.Empty<CreateQuoteTemplateLineRequest>();
}

public sealed record CreateQuoteTemplateLineRequest
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? CustomUnitPrice { get; init; }
    public decimal? DiscountPercent { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Updates a quote template (same shape as create + optional active flag).</summary>
public sealed record UpdateQuoteTemplateRequest
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? DefaultNotes { get; init; }
    public string? DefaultTermsAndConditions { get; init; }
    public int DefaultValidityDays { get; init; }
    /// <summary>When set, replaces active state.</summary>
    public bool? IsActive { get; init; }
    public IReadOnlyList<CreateQuoteTemplateLineRequest> Lines { get; init; } = Array.Empty<CreateQuoteTemplateLineRequest>();
}

public sealed record SetQuoteTemplateActiveRequest
{
    public bool IsActive { get; init; }
}

public sealed record LoseOpportunityRequest
{
    public string Reason { get; init; } = null!;
}
