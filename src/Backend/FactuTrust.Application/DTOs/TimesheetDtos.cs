using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record TenantTimesheetSettingsDto
{
    public bool BillingRateIndicatorsEnabled { get; init; }
    public bool BillingRateLeaderboardEnabled { get; init; }
    public bool TimeOffEntriesEnabled { get; init; }
    public TimesheetEncodingMethod EncodingMethod { get; init; }
    public Guid? TimeOffProjectId { get; init; }
    public Guid? TimeOffTaskId { get; init; }
    public decimal DefaultDailyWorkingHours { get; init; }
}

public sealed record UpdateTenantTimesheetSettingsDto
{
    public bool BillingRateIndicatorsEnabled { get; init; }
    public bool BillingRateLeaderboardEnabled { get; init; }
    public bool TimeOffEntriesEnabled { get; init; }
    public TimesheetEncodingMethod EncodingMethod { get; init; }
    public Guid? TimeOffProjectId { get; init; }
    public Guid? TimeOffTaskId { get; init; }
    public decimal DefaultDailyWorkingHours { get; init; } = 8m;
}

public sealed record EmployeeBillingTimeTargetDto
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetHours { get; init; }
}

public sealed record UpsertEmployeeBillingTimeTargetDto
{
    public Guid UserId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TargetHours { get; init; }
}

public sealed record TimesheetTipDto
{
    public Guid Id { get; init; }
    public string Text { get; init; } = null!;
    public bool IsActive { get; init; }
}

public sealed record UpsertTimesheetTipDto
{
    public string Text { get; init; } = null!;
    public bool IsActive { get; init; } = true;
}

public sealed record TimesheetBillingRateKpiDto
{
    public decimal BillableLoggedHours { get; init; }
    public decimal TargetHours { get; init; }
    public decimal CompletionPercent { get; init; }
    public decimal TotalLoggedHours { get; init; }
    public bool TargetReached { get; init; }
}

public sealed record TimesheetLeaderboardEntryDto
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public decimal BillableLoggedHours { get; init; }
    public decimal TargetHours { get; init; }
    public decimal CompletionPercent { get; init; }
    public decimal TotalLoggedHours { get; init; }
    public int Rank { get; init; }
}

public sealed record TimesheetLeaderboardDto
{
    public IReadOnlyList<TimesheetLeaderboardEntryDto> TopThree { get; init; } = Array.Empty<TimesheetLeaderboardEntryDto>();
    public IReadOnlyList<TimesheetLeaderboardEntryDto> FullRanking { get; init; } = Array.Empty<TimesheetLeaderboardEntryDto>();
    public TimesheetBillingRateKpiDto? CurrentUser { get; init; }
    public string? DailyTip { get; init; }
    public string Mode { get; init; } = "billingRate";
}

public sealed record TimesheetGridQuery
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public Guid? UserId { get; init; }
}

public sealed record TimesheetGridDayCellDto
{
    public DateTime Date { get; init; }
    public decimal Hours { get; init; }
    public decimal ExpectedHours { get; init; }
    public string ColorCode { get; init; } = "neutral";
}

public sealed record TimesheetGridProjectRowDto
{
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = null!;
    public bool TimesheetsEnabled { get; init; }
    public IReadOnlyList<TimesheetGridDayCellDto> Days { get; init; } = Array.Empty<TimesheetGridDayCellDto>();
    public decimal PeriodTotalHours { get; init; }
    public decimal ExpectedPeriodHours { get; init; }
    public string PeriodColorCode { get; init; } = "neutral";
}

public sealed record TimesheetGridDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public IReadOnlyList<TimesheetGridProjectRowDto> Rows { get; init; } = Array.Empty<TimesheetGridProjectRowDto>();
    public IReadOnlyList<TimesheetGridDayCellDto> DailyTotals { get; init; } = Array.Empty<TimesheetGridDayCellDto>();
    public TimesheetBillingRateKpiDto? BillingRateKpi { get; init; }
    public TimesheetLeaderboardDto? Leaderboard { get; init; }
}

public sealed record StartTimesheetTimerDto
{
    public Guid ProjectId { get; init; }
    public Guid? TaskId { get; init; }
    public Guid? SalesOrderLineId { get; init; }
    public string? Notes { get; init; }
}

public sealed record TimesheetTimerStateDto
{
    public Guid EntryId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = null!;
    public Guid? TaskId { get; init; }
    public DateTime StartedAtUtc { get; init; }
}

public sealed record TimeOffRequestDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal HoursPerDay { get; init; }
    public string TypeName { get; init; } = null!;
    public TimeOffRequestStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
}

public sealed record CreateTimeOffRequestDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal HoursPerDay { get; init; } = 8m;
    public string TypeName { get; init; } = "Congé";
    public bool RequiresApproval { get; init; }
}

public sealed record ProjectProfitabilityLineDto
{
    public string Category { get; init; } = null!;
    public decimal Expected { get; init; }
    public decimal ToInvoiceOrBill { get; init; }
    public decimal InvoicedOrBilled { get; init; }
}

public sealed record ProjectProfitabilityDto
{
    public Guid ProjectId { get; init; }
    public IReadOnlyList<ProjectProfitabilityLineDto> Revenues { get; init; } = Array.Empty<ProjectProfitabilityLineDto>();
    public IReadOnlyList<ProjectProfitabilityLineDto> Costs { get; init; } = Array.Empty<ProjectProfitabilityLineDto>();
    public decimal TotalRevenueExpected { get; init; }
    public decimal TotalRevenueToInvoice { get; init; }
    public decimal TotalRevenueInvoiced { get; init; }
    public decimal TotalCostExpected { get; init; }
    public decimal TotalCostToBill { get; init; }
    public decimal TotalCostBilled { get; init; }
    public decimal MarginInvoiced { get; init; }
}

public sealed record ProjectUpdateDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public ProjectUpdateStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public int ProgressPercent { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorUserName { get; init; } = null!;
    public DateTime UpdateDate { get; init; }
    public string? Description { get; init; }
}

public sealed record CreateProjectUpdateDto
{
    public ProjectUpdateStatus Status { get; init; }
    public int ProgressPercent { get; init; }
    public string? Description { get; init; }
}

public sealed record LinkProjectSalesOrderDto
{
    public Guid SalesOrderId { get; init; }
}

public sealed record ServiceProductBillingPolicyDto
{
    public Guid ProductId { get; init; }
    public ServiceInvoicingPolicy ServiceInvoicingPolicy { get; init; }
    public ServiceCreateOnOrder ServiceCreateOnOrder { get; init; }
    public string PolicyDisplay { get; init; } = null!;
}
