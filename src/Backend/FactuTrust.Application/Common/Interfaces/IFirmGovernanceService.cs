using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmGovernanceService
{
    Task<IReadOnlyList<PermanentFileDto>> ListPermanentFilesAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<PermanentFileDto?> GetPermanentFileAsync(Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken = default);
    Task<Result<PermanentFileDto>> UpsertPermanentFileAsync(Guid firmTenantId, Guid assignmentId, UpsertPermanentFileDto dto, string userEmail, CancellationToken cancellationToken = default);
    Task<Result> SyncPermanentFileToTenantAsync(Guid firmTenantId, Guid assignmentId, string userEmail, CancellationToken cancellationToken = default);
    Task<Result<LegalRepresentativeDto>> AddRepresentativeAsync(Guid firmTenantId, Guid assignmentId, LegalRepresentativeDto dto, CancellationToken cancellationToken = default);
    Task<Result<LegalRepresentativeDto>> UpdateRepresentativeAsync(Guid firmTenantId, Guid assignmentId, Guid representativeId, LegalRepresentativeDto dto, CancellationToken cancellationToken = default);
    Task<Result<ShareholderDto>> AddShareholderAsync(Guid firmTenantId, Guid assignmentId, ShareholderDto dto, CancellationToken cancellationToken = default);
    Task<Result<ShareholderDto>> UpdateShareholderAsync(Guid firmTenantId, Guid assignmentId, Guid shareholderId, ShareholderDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeactivateRepresentativeAsync(Guid firmTenantId, Guid assignmentId, Guid representativeId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateShareholderAsync(Guid firmTenantId, Guid assignmentId, Guid shareholderId, CancellationToken cancellationToken = default);
    Task<Result> ArchivePermanentFileAsync(Guid firmTenantId, Guid assignmentId, string userEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirmTimeSheetEntryDto>> ListTimeSheetsAsync(
        Guid firmTenantId,
        int? year,
        int? month,
        Guid? userId = null,
        Guid? assignmentId = null,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> CreateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        CreateTimeSheetEntryDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> UpdateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        UpdateTimeSheetEntryDto dto,
        CancellationToken cancellationToken = default);
    Task<Result> DeleteTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> ValidateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> UnvalidateTimeSheetAsync(
        Guid firmTenantId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> SubmitTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> StartTimeSheetTimerAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        StartTimeSheetTimerDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetEntryDto>> StopTimeSheetTimerAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        StopTimeSheetTimerDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FirmTimeSheetEntryDto>>> DuplicateTimeSheetWeekAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        DuplicateTimeSheetWeekDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetBulkValidationResultDto>> ValidateTimeSheetsBulkAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        IReadOnlyList<Guid> entryIds,
        CancellationToken cancellationToken = default);

    // ---- Clôture mensuelle ----
    Task<IReadOnlyList<FirmTimeSheetPeriodDto>> ListTimeSheetPeriodsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetPeriodDto>> LockTimeSheetPeriodAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        int year,
        int month,
        string? reason,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetPeriodDto>> UnlockTimeSheetPeriodAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        int year,
        int month,
        string reason,
        CancellationToken cancellationToken = default);

    // ---- Référentiel des codes activité ----
    Task<IReadOnlyList<FirmActivityCodeDto>> ListActivityCodesAsync(
        Guid firmTenantId,
        bool includeInactive = false,
        bool billableOnly = false,
        CancellationToken cancellationToken = default);
    /// <summary>Installe la nomenclature par défaut. Sans effet si le cabinet a déjà des codes.</summary>
    Task<IReadOnlyList<FirmActivityCodeDto>> SeedDefaultActivityCodesAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default);
    Task<Result<FirmActivityCodeDto>> CreateActivityCodeAsync(
        Guid firmTenantId,
        bool isManager,
        SaveFirmActivityCodeDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<FirmActivityCodeDto>> UpdateActivityCodeAsync(
        Guid firmTenantId,
        bool isManager,
        Guid codeId,
        SaveFirmActivityCodeDto dto,
        CancellationToken cancellationToken = default);
    Task<Result<FirmActivityCodeDto>> SetActivityCodeActiveAsync(
        Guid firmTenantId,
        bool isManager,
        Guid codeId,
        bool isActive,
        CancellationToken cancellationToken = default);

    // ---- Paramètres d'exercice ----
    Task<FirmTimeSheetYearSettingsDto> GetTimeSheetYearSettingsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);
    Task<Result<FirmTimeSheetYearSettingsDto>> SaveTimeSheetYearSettingsAsync(
        Guid firmTenantId,
        int year,
        SaveFirmTimeSheetYearSettingsDto dto,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirmExpenseNoteDto>> ListExpenseNotesAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<Result<FirmExpenseNoteDto>> UpsertExpenseNoteAsync(Guid firmTenantId, UpsertFirmExpenseNoteDto dto, CancellationToken cancellationToken = default);
    Task<Result> ProcessExpenseNoteAsync(Guid firmTenantId, Guid noteId, bool approve, CancellationToken cancellationToken = default);
    Task<Result> SubmitExpenseNoteAsync(Guid firmTenantId, Guid noteId, CancellationToken cancellationToken = default);
    Task<Result> MarkExpenseNoteReimbursedAsync(Guid firmTenantId, Guid noteId, CancellationToken cancellationToken = default);
    Task<FirmGovernanceDashboardDto> GetGovernanceDashboardAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<Result> AssignDossierManagerAsync(
        Guid firmTenantId,
        Guid assignedByUserId,
        AssignDossierManagerDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<AssignDossierManagerBulkResultDto>> AssignDossierManagerBulkAsync(
        Guid firmTenantId,
        Guid assignedByUserId,
        AssignDossierManagerBulkDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmDossierAssignmentListItemDto>> ListDossierAssignmentsAsync(
        Guid firmTenantId,
        int assignmentFilter,
        string? name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmAssignableAccountantDto>> ListAssignableAccountantsAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default);
}
