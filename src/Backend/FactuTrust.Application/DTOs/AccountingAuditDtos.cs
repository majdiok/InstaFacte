namespace FactuTrust.Application.DTOs;

public sealed record AccountingAuditRunRequestDto
{
    public int FiscalYear { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public IReadOnlyList<string>? ModuleCodes { get; init; }
}

public sealed record AccountingAuditRunResultDto
{
    public Guid RunId { get; init; }
    public int FiscalYear { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = null!;
    public decimal ComplianceRate { get; init; }
    public int TotalAnomalies { get; init; }
    public int BlockingCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public AccountingAuditDashboardDto? Dashboard { get; init; }
}

public sealed record AccountingAuditRunStatusDto
{
    public Guid RunId { get; init; }
    public string Status { get; init; } = null!;
    public string? ErrorMessage { get; init; }
    public decimal ComplianceRate { get; init; }
    public int TotalAnomalies { get; init; }
}

public sealed record AccountingAuditDashboardDto
{
    public int FiscalYear { get; init; }
    public int BlockingCount { get; init; }
    public int WarningCount { get; init; }
    public int AnomalyCount { get; init; }
    public int InfoCount { get; init; }
    public decimal ComplianceRate { get; init; }
    public decimal? ComplianceRateDeltaVsPriorYear { get; init; }
    public AccountingAuditLastRunDto? LastRun { get; init; }
}

public sealed record AccountingAuditLastRunDto
{
    public Guid RunId { get; init; }
    public DateTime CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public string? TriggeredByUserName { get; init; }
}

public sealed record AccountingAnomalyFilterDto
{
    public int FiscalYear { get; init; }
    public int? Severity { get; init; }
    public int? Category { get; init; }
    public int? Status { get; init; }
    public string? Account { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public string? Search { get; init; }
    public string? ModuleCode { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}

public sealed record PagedAnomaliesDto
{
    public IReadOnlyList<AccountingAnomalyListItemDto> Items { get; init; } = Array.Empty<AccountingAnomalyListItemDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int OpenCount { get; init; }
    public int IgnoredCount { get; init; }
}

public sealed record AccountingAnomalyListItemDto
{
    public Guid Id { get; init; }
    public string RuleCode { get; init; } = null!;
    public string ModuleCode { get; init; } = null!;
    public int Severity { get; init; }
    public int Category { get; init; }
    public string Title { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string DetailSummary { get; init; } = null!;
    public string? AccountRef { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public decimal Amount { get; init; }
    public int Status { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public string? AssignedToUserName { get; init; }
    public string? DeepLinkRoute { get; init; }
    public int LineCount { get; init; }
    public DateTime DetectedAt { get; init; }
}

public sealed record AccountingAnomalyDetailDto
{
    public Guid Id { get; init; }
    public string RuleCode { get; init; } = null!;
    public string ModuleCode { get; init; } = null!;
    public int Severity { get; init; }
    public int Category { get; init; }
    public string Title { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Impact { get; init; } = null!;
    public string? AccountRef { get; init; }
    public decimal Amount { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public int Status { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public string? AssignedToUserName { get; init; }
    public DateTime DetectedAt { get; init; }
    public string? DeepLinkRoute { get; init; }
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AccountingAnomalyLineDto> Lines { get; init; } = Array.Empty<AccountingAnomalyLineDto>();
    public IReadOnlyList<AccountingAnomalyActivityDto> Activities { get; init; } = Array.Empty<AccountingAnomalyActivityDto>();
}

public sealed record AccountingAnomalyLineDto
{
    public Guid Id { get; init; }
    public Guid? JournalEntryId { get; init; }
    public DateTime? EntryDate { get; init; }
    public string? AccountNumber { get; init; }
    public string? Label { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public string? PieceRef { get; init; }
    public string? JustificationStatus { get; init; }
}

public sealed record AccountingAnomalyActivityDto
{
    public Guid Id { get; init; }
    public int ActivityType { get; init; }
    public string? UserName { get; init; }
    public string Message { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
}

public sealed record AccountingAuditAnalyticsDto
{
    public IReadOnlyList<AccountingAuditCategorySliceDto> ByCategory { get; init; } = Array.Empty<AccountingAuditCategorySliceDto>();
    public IReadOnlyList<AccountingAuditTrendPointDto> Trend { get; init; } = Array.Empty<AccountingAuditTrendPointDto>();
    public IReadOnlyList<AccountingAuditTopAccountDto> TopAccounts { get; init; } = Array.Empty<AccountingAuditTopAccountDto>();
    public IReadOnlyList<AccountingAuditRiskAxisDto> RiskRadar { get; init; } = Array.Empty<AccountingAuditRiskAxisDto>();
    public IReadOnlyList<AccountingAnomalyActivityDto> RecentActivity { get; init; } = Array.Empty<AccountingAnomalyActivityDto>();
}

public sealed record AccountingAuditCategorySliceDto
{
    public int Category { get; init; }
    public string Label { get; init; } = null!;
    public int Count { get; init; }
}

public sealed record AccountingAuditTrendPointDto
{
    public string Label { get; init; } = null!;
    public int Blocking { get; init; }
    public int Warning { get; init; }
    public int Info { get; init; }
}

public sealed record AccountingAuditTopAccountDto
{
    public string AccountNumber { get; init; } = null!;
    public string? AccountLabel { get; init; }
    public int AnomalyCount { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed record AccountingAuditRiskAxisDto
{
    public string Axis { get; init; } = null!;
    public decimal CurrentScore { get; init; }
    public decimal SectorAverage { get; init; }
}

public sealed record AccountingControlModuleDto
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string Icon { get; init; } = null!;
    public int Count { get; init; }
}

public sealed record AccountingControlScheduleDto
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = null!;
    public string CronExpression { get; init; } = null!;
    public bool IsActive { get; init; } = true;
    public int? FiscalYearOffset { get; init; }
    public string? ModuleCodesFilter { get; init; }
    public string? NotifyEmails { get; init; }
    public DateTime? LastRunAt { get; init; }
}

public sealed record AccountingControlRuleSettingDto
{
    public string RuleCode { get; init; } = null!;
    public string Title { get; init; } = null!;
    public bool IsEnabled { get; init; } = true;
    public int? IntThreshold { get; init; }
    public decimal? DecimalThreshold { get; init; }
}

public sealed record AssignAnomalyRequestDto
{
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeUserName { get; init; }
}

public sealed record UpdateAnomalyStatusRequestDto
{
    public int Status { get; init; }
}

public sealed record CommentAnomalyRequestDto
{
    public string Comment { get; init; } = null!;
}

public sealed record IgnoreAnomalyRequestDto
{
    public string Reason { get; init; } = null!;
}
