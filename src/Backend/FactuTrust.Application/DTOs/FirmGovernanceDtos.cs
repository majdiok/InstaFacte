namespace FactuTrust.Application.DTOs;

public sealed record PermanentFileDto
{
    public Guid Id { get; init; }
    public Guid FirmClientAssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string? CompanyName { get; init; }
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public int WizardStep { get; init; }
    public string? Nif { get; init; }
    public string? RneIdentifier { get; init; }
    public int? LegalForm { get; init; }
    public string? LegalFormDisplay { get; init; }
    public DateTime? IncorporationDate { get; init; }
    public decimal? ShareCapital { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? Governorate { get; init; }
    public string? PostalCode { get; init; }
    public string? TaxOffice { get; init; }
    public int? TaxRegime { get; init; }
    public bool HasTaxCertificate { get; init; }
    public int? FiscalYearStartMonth { get; init; }
    public int? FiscalYearEndMonth { get; init; }
    public string? CurrentLegalAct { get; init; }
    public string? MissionStatus { get; init; }
    public bool IsDigitized { get; init; }
    public bool MissionResigned { get; init; }
    public int? ResignationFiscalYear { get; init; }
    public string? ResignationNotes { get; init; }
    public bool LabCompleted { get; init; }
    public bool MissionAccepted { get; init; }
    public DateTime? LabCompletedAt { get; init; }
    public DateTime? MissionAcceptedAt { get; init; }
    public decimal? AnnualFeeAmount { get; init; }
    public int? BillingFrequency { get; init; }
    public string? BillingFrequencyDisplay { get; init; }
    public string Currency { get; init; } = "TND";
    public string? BillingNotes { get; init; }
    public Guid? AssignedAccountantUserId { get; init; }
    public string? AssignedAccountantName { get; init; }
    public DateTime? SyncedToTenantAt { get; init; }
    public bool IsBusinessComplete { get; init; }
    public IReadOnlyList<string> MissingItems { get; init; } = Array.Empty<string>();
    public int CompletionPercent { get; init; }
    public int? NextRecommendedStep { get; init; }
    public string? NextActionLabel { get; init; }
    public IReadOnlyList<LegalRepresentativeDto> Representatives { get; init; } = Array.Empty<LegalRepresentativeDto>();
    public IReadOnlyList<ShareholderDto> Shareholders { get; init; } = Array.Empty<ShareholderDto>();
}

public sealed record LegalRepresentativeDto
{
    public Guid Id { get; init; }
    public string LastName { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? Nationality { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? CnssNumber { get; init; }
    public string Role { get; init; } = null!;
    public bool IsActive { get; init; }
    public bool HasProSpace { get; init; }
}

public sealed record ShareholderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public bool IsLegalEntity { get; init; }
    public string? CinOrNif { get; init; }
    public decimal ShareCount { get; init; }
    public decimal SharePercentage { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record UpsertPermanentFileDto
{
    public int WizardStep { get; init; }
    public string? CompanyName { get; init; }
    public string? Nif { get; init; }
    public string? RneIdentifier { get; init; }
    public int? LegalForm { get; init; }
    public DateTime? IncorporationDate { get; init; }
    public decimal? ShareCapital { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? Governorate { get; init; }
    public string? PostalCode { get; init; }
    public string? TaxOffice { get; init; }
    public int? TaxRegime { get; init; }
    public bool? HasTaxCertificate { get; init; }
    public int? FiscalYearStartMonth { get; init; }
    public int? FiscalYearEndMonth { get; init; }
    public string? CurrentLegalAct { get; init; }
    public string? MissionStatus { get; init; }
    public bool IsDigitized { get; init; }
    public bool MissionResigned { get; init; }
    public int? ResignationFiscalYear { get; init; }
    public string? ResignationNotes { get; init; }
    public bool LabCompleted { get; init; }
    public bool MissionAccepted { get; init; }
    public decimal? AnnualFeeAmount { get; init; }
    public int? BillingFrequency { get; init; }
    public string? Currency { get; init; }
    public string? BillingNotes { get; init; }
    public Guid? AssignedAccountantUserId { get; init; }
    public string? AssignedAccountantName { get; init; }
    public bool RequestCompletion { get; init; }
}

public sealed record FirmTimeSheetEntryDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserDisplayName { get; init; } = null!;
    public Guid? FirmClientAssignmentId { get; init; }
    public string? ClientCompanyName { get; init; }
    public DateTime WorkDate { get; init; }
    public decimal Hours { get; init; }
    public string? ActivityCode { get; init; }
    public string? Notes { get; init; }
    public bool IsBillable { get; init; }
    public bool IsValidated { get; init; }

    public DateTime? ValidatedAt { get; init; }
    public string? ValidatedByDisplayName { get; init; }

    /// <summary>
    /// Dépassements constatés mais non bloquants (exercice dont les plafonds ne sont pas encore
    /// appliqués). Vide dans le cas nominal ; jamais persisté.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record CreateTimeSheetEntryDto
{
    public DateTime WorkDate { get; init; }
    public decimal Hours { get; init; }
    public Guid? FirmClientAssignmentId { get; init; }
    public string? ActivityCode { get; init; }
    public string? Notes { get; init; }
    public bool IsBillable { get; init; } = true;
    /// <summary>Optional: manager may create an entry on behalf of another collaborator.</summary>
    public Guid? TargetUserId { get; init; }
}

public sealed record UpdateTimeSheetEntryDto
{
    public DateTime WorkDate { get; init; }
    public decimal Hours { get; init; }
    public Guid? FirmClientAssignmentId { get; init; }
    public string? ActivityCode { get; init; }
    public string? Notes { get; init; }
    public bool IsBillable { get; init; } = true;
}

public sealed record ValidateTimeSheetsBulkDto
{
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}

/// <summary>Résultat détaillé d'une validation en lot : ce qui est passé, et pourquoi le reste ne l'est pas.</summary>
public sealed record FirmTimeSheetBulkValidationResultDto
{
    public int Validated { get; init; }
    public int Skipped { get; init; }
    public IReadOnlyList<FirmTimeSheetBulkValidationFailureDto> Failures { get; init; } =
        Array.Empty<FirmTimeSheetBulkValidationFailureDto>();
}

public sealed record FirmTimeSheetBulkValidationFailureDto
{
    public Guid EntryId { get; init; }
    public string Error { get; init; } = null!;
}

/// <summary>Code de la nomenclature des diligences du cabinet.</summary>
public sealed record FirmActivityCodeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Category { get; init; }
    public string CategoryDisplay { get; init; } = null!;
    public bool IsBillableByDefault { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

public sealed record SaveFirmActivityCodeDto
{
    /// <summary>Immuable après création : les saisies existantes référencent le code par sa valeur.</summary>
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Category { get; init; }
    public bool IsBillableByDefault { get; init; } = true;
    public int SortOrder { get; init; }
}

/// <summary>État de clôture d'un mois de feuilles de temps.</summary>
public sealed record FirmTimeSheetPeriodDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public bool IsLocked { get; init; }
    public DateTime? LockedAt { get; init; }
    public string? LockedByDisplayName { get; init; }
    public string? LockReason { get; init; }
    public DateTime? UnlockedAt { get; init; }
    public string? UnlockedByDisplayName { get; init; }
    public string? UnlockReason { get; init; }
}

public sealed record LockTimeSheetPeriodDto
{
    public string? Reason { get; init; }
}

public sealed record UnlockTimeSheetPeriodDto
{
    /// <summary>Motif obligatoire : c'est la pièce justificative de la réouverture.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Paramètres de gouvernance des temps et des coûts pour un exercice.</summary>
public sealed record FirmTimeSheetYearSettingsDto
{
    public int Year { get; init; }
    public int WeeklyRegime { get; init; }
    public string WeeklyRegimeDisplay { get; init; } = null!;
    public decimal MaxDailyHours { get; init; }
    public decimal MaxWeeklyHours { get; init; }
    public int AllowFutureEntryDays { get; init; }
    public int MaxBackdatingDays { get; init; }
    public bool EnforceHardLimits { get; init; }
    public decimal PaidLeaveDaysPerYear { get; init; }
    public decimal PublicHolidayDaysPerYear { get; init; }
    public decimal ProductivityRatePercent { get; init; }
    public decimal CnssEmployerRate { get; init; }
    public decimal TfpRate { get; init; }
    public decimal FoprolosRate { get; init; }
    public decimal WorkAccidentRate { get; init; }

    // Grandeurs dérivées, exposées pour rendre le taux horaire lisible côté écran.
    public decimal AnnualBaseHours { get; init; }
    public decimal DailyHours { get; init; }
    public decimal AnnualProductiveHours { get; init; }
    public decimal TotalEmployerChargeRate { get; init; }
}

public sealed record SaveFirmTimeSheetYearSettingsDto
{
    public int WeeklyRegime { get; init; }
    public decimal MaxDailyHours { get; init; }
    public decimal MaxWeeklyHours { get; init; }
    public int AllowFutureEntryDays { get; init; }
    public int MaxBackdatingDays { get; init; }
    public bool EnforceHardLimits { get; init; }
    public decimal PaidLeaveDaysPerYear { get; init; }
    public decimal PublicHolidayDaysPerYear { get; init; }
    public decimal ProductivityRatePercent { get; init; }
    public decimal CnssEmployerRate { get; init; }
    public decimal TfpRate { get; init; }
    public decimal FoprolosRate { get; init; }
    public decimal WorkAccidentRate { get; init; }
}

public sealed record FirmExpenseNoteDto
{
    public Guid Id { get; init; }
    public Guid FirmClientAssignmentId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal TotalToReimburse { get; init; }
    public decimal MixedCharges { get; init; }
    public decimal OperatingExpenses { get; init; }
    public decimal MileageAllowance { get; init; }
    public decimal SalesAmount { get; init; }
    public string? Notes { get; init; }
}

public sealed record UpsertFirmExpenseNoteDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public decimal TotalToReimburse { get; init; }
    public decimal MixedCharges { get; init; }
    public decimal OperatingExpenses { get; init; }
    public decimal MileageAllowance { get; init; }
    public decimal SalesAmount { get; init; }
    public string? Notes { get; init; }
}

public sealed record FirmGovernanceDashboardDto
{
    public int ActiveDossiersCount { get; init; }
    public int PermanentFilesCompleteCount { get; init; }
    public int PermanentFilesInProgressCount { get; init; }
    public decimal TotalBillableHoursMonth { get; init; }
    public decimal TotalBillableHoursYear { get; init; }
    public int PendingExpenseNotesCount { get; init; }
    public int OverdueFiscalSchedulesCount { get; init; }
    public int UnreadNotificationsCount { get; init; }
}

public sealed record FirmSocialOverviewDto
{
    public IReadOnlyList<FirmSocialClientRowDto> Clients { get; init; } = Array.Empty<FirmSocialClientRowDto>();
}

public sealed record FirmSocialClientRowDto
{
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int EmployeeCount { get; init; }
    public int PendingLeaveRequests { get; init; }
    public int PayrollRunsDraftCount { get; init; }
    public int DtsPendingCount { get; init; }
}

public sealed record AssignDossierManagerDto
{
    public Guid AssignmentId { get; init; }
    public Guid? AccountantUserId { get; init; }
    /// <summary>Ignoré côté serveur — le nom est résolu depuis Identity.</summary>
    public string? AccountantName { get; init; }
}

public sealed record AssignDossierManagerBulkDto
{
    public IReadOnlyList<Guid> AssignmentIds { get; init; } = Array.Empty<Guid>();
    /// <summary>Null = désaffectation.</summary>
    public Guid? AccountantUserId { get; init; }
}

public sealed record AssignDossierManagerBulkResultDto
{
    public int Succeeded { get; init; }
    public IReadOnlyList<AssignDossierManagerBulkFailureDto> Failed { get; init; } = Array.Empty<AssignDossierManagerBulkFailureDto>();
}

public sealed record AssignDossierManagerBulkFailureDto
{
    public Guid AssignmentId { get; init; }
    public string Error { get; init; } = null!;
}

/// <summary>0=Tous, 1=En attente d'affectation, 2=Affectés.</summary>
public sealed record FirmDossierAssignmentListItemDto
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public DateTime ActiveSince { get; init; }
    public bool HasPermanentFile { get; init; }
    public int? PermanentFileStatus { get; init; }
    public string? PermanentFileStatusDisplay { get; init; }
    public Guid? AssignedAccountantUserId { get; init; }
    public string? AssignedAccountantName { get; init; }
    public bool IsAwaitingAccountantAssignment { get; init; }
}
