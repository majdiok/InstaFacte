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

    /// <summary>Type de congé de paie produit à l'approbation. Null = aucun effet sur le bulletin.</summary>
    public int? PayrollLeaveType { get; init; }

    public string PayrollEffectDisplay { get; init; } = "Aucun effet paie";

    /// <summary>Le temps posé est-il décompté du temps de présence productif ?</summary>
    public bool CountsAsAbsence { get; init; }
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
    public int? PayrollLeaveType { get; init; }
    public bool CountsAsAbsence { get; init; }
}

/// <summary>Un congé approuvé dont le report vers la paie n'a pas abouti.</summary>
public sealed record FirmLeaveReconciliationRowDto
{
    public Guid LeaveRequestId { get; init; }
    public Guid UserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public string LeaveTypeLabel { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal Days { get; init; }

    /// <summary>Voir <c>FirmLeavePayrollMirrorState</c>.</summary>
    public int MirrorState { get; init; }

    public string MirrorStateDisplay { get; init; } = null!;
    public string? MirrorMessage { get; init; }
    public DateTime? MirroredAt { get; init; }

    /// <summary>Un rejeu peut-il encore aboutir ? Faux sur un mois de paie arrêté.</summary>
    public bool CanReplay { get; init; }
}

/// <summary>État du rapprochement congés ↔ paie pour un exercice.</summary>
public sealed record FirmLeaveReconciliationDto
{
    public int Year { get; init; }

    /// <summary>Congés approuvés effectivement reportés en paie.</summary>
    public int MirroredCount { get; init; }

    /// <summary>Congés approuvés sans effet paie attendu (type non mappé).</summary>
    public int NoPayrollEffectCount { get; init; }

    /// <summary>Écarts à traiter : report en échec, jamais tenté, ou bloqué par une paie arrêtée.</summary>
    public IReadOnlyList<FirmLeaveReconciliationRowDto> Pending { get; init; }
        = Array.Empty<FirmLeaveReconciliationRowDto>();

    /// <summary>Vrai si la paie interne est exploitable ; sinon le rapprochement est sans objet.</summary>
    public bool PayrollAvailable { get; init; }

    public string? UnavailableReason { get; init; }
}

/// <summary>Résultat d'un rejeu de reports.</summary>
public sealed record FirmLeaveReplayResultDto
{
    public int Replayed { get; init; }
    public int Succeeded { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

/// <summary>Issue du report d'un congé cabinet vers la paie.</summary>
public sealed record FirmLeaveMirrorResultDto
{
    /// <summary>Voir <c>FirmLeavePayrollMirrorState</c>.</summary>
    public int State { get; init; }

    public string StateDisplay { get; init; } = null!;

    /// <summary>Explication destinée à l'approbateur, vide dans le cas nominal.</summary>
    public string? Message { get; init; }

    public Guid? PayrollLeaveRequestId { get; init; }

    /// <summary>Vrai si le congé a bien produit son effet en paie.</summary>
    public bool IsApplied { get; init; }
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

    /// <summary>État persistant du report vers la paie — voir <c>FirmLeavePayrollMirrorState</c>.</summary>
    public int PayrollMirrorState { get; init; }

    public string PayrollMirrorStateDisplay { get; init; } = "Non reporté";

    public string? PayrollMirrorMessage { get; init; }

    public DateTime? PayrollMirroredAt { get; init; }

    /// <summary>
    /// Issue du report déclenché par l'appel en cours. Nul en lecture : seule une approbation
    /// la renseigne, pour que l'écran d'approbation puisse alerter immédiatement.
    /// </summary>
    public FirmLeaveMirrorResultDto? PayrollMirror { get; init; }
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
