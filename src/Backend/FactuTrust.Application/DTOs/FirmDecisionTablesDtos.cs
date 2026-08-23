namespace FactuTrust.Application.DTOs;

public sealed record FirmCriticalFiscalRowDto
{
    public Guid EntryId { get; init; }
    public Guid? CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int ObligationType { get; init; }
    public string ObligationTypeDisplay { get; init; } = null!;
    public string ObligationLabel { get; init; } = null!;
    public DateTime DueDate { get; init; }
    public int DaysUntilDue { get; init; }
    public bool IsOverdue { get; init; }
    public decimal EstimatedAmount { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record FirmAtRiskDossierRowDto
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public IReadOnlyList<string> Signals { get; init; } = Array.Empty<string>();
    public DateTime? LastJournalEntryDate { get; init; }
    public int? PermanentFileCompletionPercent { get; init; }
    public int PermanentFileMissingItemsCount { get; init; }
    public bool HasPermanentFile { get; init; }
    public string? AssignedAccountantName { get; init; }
    public int PriorityScore { get; init; }
}

public sealed record FirmNegativeMarginRowDto
{
    public Guid? FirmClientAssignmentId { get; init; }
    public Guid? CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int Year { get; init; }
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public decimal BudgetAnnuel { get; init; }
    public decimal TotalHours { get; init; }
    public decimal Margin { get; init; }
}

public sealed record FirmPendingTimeSheetRowDto
{
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public decimal SubmittedHours { get; init; }
    public int EntryCount { get; init; }
    public IReadOnlyList<string> CompanyNames { get; init; } = Array.Empty<string>();
}

public sealed record FirmHonorairesAlertRowDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public Guid? CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int Year { get; init; }
    public decimal BilledYtdAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal DebitBalance { get; init; }
    public decimal RecoveryRatePercent { get; init; }
    public bool IsSnapshotData { get; init; }
}

public sealed record FirmDecisionTablesPartialFailureDto
{
    public string Section { get; init; } = null!;
    public string Message { get; init; } = null!;
}

public sealed record FirmDecisionTablesMetaDto
{
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<FirmDecisionTablesPartialFailureDto> PartialFailures { get; init; } =
        Array.Empty<FirmDecisionTablesPartialFailureDto>();
}

public sealed record FirmDecisionTablesDto
{
    public IReadOnlyList<FirmCriticalFiscalRowDto> CriticalFiscalSchedules { get; init; } =
        Array.Empty<FirmCriticalFiscalRowDto>();
    public IReadOnlyList<FirmAtRiskDossierRowDto> AtRiskDossiers { get; init; } =
        Array.Empty<FirmAtRiskDossierRowDto>();
    public IReadOnlyList<FirmNegativeMarginRowDto> NegativeMargins { get; init; } =
        Array.Empty<FirmNegativeMarginRowDto>();
    public IReadOnlyList<FirmPendingTimeSheetRowDto> PendingTimeSheets { get; init; } =
        Array.Empty<FirmPendingTimeSheetRowDto>();
    public IReadOnlyList<FirmHonorairesAlertRowDto> HonorairesAlerts { get; init; } =
        Array.Empty<FirmHonorairesAlertRowDto>();
    public FirmDecisionTablesMetaDto Meta { get; init; } = new();
}
