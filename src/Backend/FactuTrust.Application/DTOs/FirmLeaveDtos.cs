namespace FactuTrust.Application.DTOs;

public sealed record FirmLeaveTypeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string ColorHex { get; init; } = null!;
    public bool DeductsBalance { get; init; }
    public bool RequiresApproval { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpsertFirmLeaveTypeDto
{
    public Guid? Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string ColorHex { get; init; } = "#64748b";
    public bool DeductsBalance { get; init; }
    public bool RequiresApproval { get; init; } = true;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record FirmLeaveSettingsDto
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public decimal DefaultAnnualPaidDays { get; init; }
    public bool AllowHalfDays { get; init; }
    public int MinNoticeDays { get; init; }
    public bool BlockOverlap { get; init; }
    public bool CarryOverEnabled { get; init; }
    public decimal MaxCarryOverDays { get; init; }
}

public sealed record UpdateFirmLeaveSettingsDto
{
    public decimal DefaultAnnualPaidDays { get; init; }
    public bool AllowHalfDays { get; init; }
    public int MinNoticeDays { get; init; }
    public bool BlockOverlap { get; init; }
    public bool CarryOverEnabled { get; init; }
    public decimal MaxCarryOverDays { get; init; }
}

public sealed record FirmLeaveBalanceDto
{
    public Guid? Id { get; init; }
    public Guid UserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int Year { get; init; }
    public decimal OpeningBalanceDays { get; init; }
    public decimal AdjustmentDays { get; init; }
    public decimal EntitlementDays { get; init; }
    public decimal ConsumedDays { get; init; }
    public decimal RemainingDays { get; init; }
    public string? Notes { get; init; }
}

public sealed record SetFirmLeaveBalanceDto
{
    public decimal OpeningBalanceDays { get; init; }
    public decimal AdjustmentDays { get; init; }
    public string? Notes { get; init; }
}

public sealed record FirmLeaveRequestDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public Guid LeaveTypeId { get; init; }
    public string LeaveTypeCode { get; init; } = null!;
    public string LeaveTypeLabel { get; init; } = null!;
    public string LeaveTypeColorHex { get; init; } = null!;
    public bool DeductsBalance { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int StartUnit { get; init; }
    public int EndUnit { get; init; }
    public decimal Days { get; init; }
    public string? Reason { get; init; }
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public Guid? ProcessedByUserId { get; init; }
    public string? ProcessedByName { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed record CreateFirmLeaveRequestDto
{
    public Guid? UserId { get; init; }
    public Guid LeaveTypeId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int StartUnit { get; init; }
    public int EndUnit { get; init; }
    public string? Reason { get; init; }
    public bool SubmitImmediately { get; init; }
}

public sealed record UpdateFirmLeaveRequestDto
{
    public Guid LeaveTypeId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int StartUnit { get; init; }
    public int EndUnit { get; init; }
    public string? Reason { get; init; }
}

public sealed record ProcessFirmLeaveDto
{
    public bool Approve { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed record ComputeFirmLeaveDaysDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int StartUnit { get; init; }
    public int EndUnit { get; init; }
}

public sealed record ComputeFirmLeaveDaysResultDto
{
    public decimal Days { get; init; }
}

public sealed record FirmLeaveOverviewDto
{
    public int Year { get; init; }
    public decimal TotalPaidBalanceDays { get; init; }
    public decimal TakenDays { get; init; }
    public decimal PendingDays { get; init; }
    public decimal AbsenteeismRatePercent { get; init; }
    public IReadOnlyList<FirmLeaveRequestDto> PendingRequests { get; init; } = Array.Empty<FirmLeaveRequestDto>();
    public IReadOnlyList<FirmLeaveBalanceDto> TopBalances { get; init; } = Array.Empty<FirmLeaveBalanceDto>();
    public IReadOnlyList<FirmLeaveTypeSummaryDto> TypeSummaries { get; init; } = Array.Empty<FirmLeaveTypeSummaryDto>();
}

public sealed record FirmLeaveTypeSummaryDto
{
    public Guid LeaveTypeId { get; init; }
    public string Label { get; init; } = null!;
    public string ColorHex { get; init; } = null!;
    public decimal TakenDays { get; init; }
    public decimal BalanceDays { get; init; }
}

public sealed record FirmLeaveCalendarEntryDto
{
    public Guid RequestId { get; init; }
    public Guid UserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public string? Qualification { get; init; }
    public Guid LeaveTypeId { get; init; }
    public string LeaveTypeLabel { get; init; } = null!;
    public string ColorHex { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int StartUnit { get; init; }
    public int EndUnit { get; init; }
    public decimal Days { get; init; }
    public int Status { get; init; }
}
