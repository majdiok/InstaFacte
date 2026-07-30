using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxRegime = FactuTrust.Domain.Entities.TaxRegime;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmGovernanceService : IFirmGovernanceService
{
    private readonly MasterDbContext _master;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantService _tenantService;
    private readonly IFirmFiscalOpsAggregator _fiscalOps;
    private readonly ICompanyProfileSnapshotProvider _companyProfileSnapshot;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmGovernanceService> _logger;

    public FirmGovernanceService(
        MasterDbContext master,
        UserManager<ApplicationUser> userManager,
        ITenantService tenantService,
        IFirmFiscalOpsAggregator fiscalOps,
        ICompanyProfileSnapshotProvider companyProfileSnapshot,
        IFirmDossierAccessService dossierAccess,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ILogger<FirmGovernanceService> logger)
    {
        _master = master;
        _userManager = userManager;
        _tenantService = tenantService;
        _fiscalOps = fiscalOps;
        _companyProfileSnapshot = companyProfileSnapshot;
        _dossierAccess = dossierAccess;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PermanentFileDto>> ListPermanentFilesAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var allowedAssignmentIds = await ResolveAccessibleAssignmentIdsAsync(firmTenantId, cancellationToken);
        if (allowedAssignmentIds is { Count: 0 })
            return [];

        var filesQuery = _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId);
        if (allowedAssignmentIds is not null)
            filesQuery = filesQuery.Where(p => allowedAssignmentIds.Contains(p.FirmClientAssignmentId));

        var files = await filesQuery.OrderBy(p => p.CompanyName).ToListAsync(cancellationToken);

        if (files.Count == 0)
            return [];

        var fileIds = files.Select(f => f.Id).ToList();
        var repCounts = await _master.LegalRepresentatives.AsNoTracking()
            .Where(r => fileIds.Contains(r.PermanentFileId) && r.IsActive)
            .GroupBy(r => r.PermanentFileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return files.Select(f => MapPermanentFile(f, activeRepCount: repCounts.GetValueOrDefault(f.Id))).ToList();
    }

    public async Task<PermanentFileDto?> GetPermanentFileAsync(Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAssignmentOrFailOpenManagerAsync(firmTenantId, assignmentId, cancellationToken))
            return null;

        var file = await _master.PermanentFiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.FirmTenantId == firmTenantId && p.FirmClientAssignmentId == assignmentId, cancellationToken);
        if (file is null)
            return null;

        var reps = await _master.LegalRepresentatives.AsNoTracking()
            .Where(r => r.PermanentFileId == file.Id && r.IsActive)
            .ToListAsync(cancellationToken);
        var shareholders = await _master.Shareholders.AsNoTracking()
            .Where(s => s.PermanentFileId == file.Id && s.IsActive)
            .ToListAsync(cancellationToken);

        return MapPermanentFile(file, reps, shareholders);
    }

    public async Task<Result<PermanentFileDto>> UpsertPermanentFileAsync(
        Guid firmTenantId,
        Guid assignmentId,
        UpsertPermanentFileDto dto,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<PermanentFileDto>(accessDenied);

        var assignment = await GetActiveAssignment(firmTenantId, assignmentId, cancellationToken);
        if (assignment is null)
            return Result.Failure<PermanentFileDto>(Error.NotFound("Assignment", assignmentId));

        var file = await _master.PermanentFiles
            .FirstOrDefaultAsync(p => p.FirmClientAssignmentId == assignmentId, cancellationToken);

        if (file is null)
        {
            var profile = await ResolveCompanyProfileAsync(assignment, cancellationToken);
            var create = PermanentFile.Create(
                assignmentId,
                firmTenantId,
                assignment.CompanyTenantId,
                dto.CompanyName ?? profile?.CompanyName,
                dto.Nif ?? profile?.Nif,
                dto.TaxRegime.HasValue && Enum.IsDefined(typeof(TaxRegime), dto.TaxRegime.Value)
                    ? (TaxRegime)dto.TaxRegime.Value
                    : profile is not null ? (TaxRegime)profile.TaxRegime : null);
            if (create.IsFailure)
                return Result.Failure<PermanentFileDto>(create.Error);
            file = create.Value;
            if (profile is not null)
                PermanentFileCompanyProfileSeeder.Apply(profile, file);
            file.SetAuditInfo(userEmail, false);
            _master.PermanentFiles.Add(file);
        }
        else
        {
            file.SetAuditInfo(userEmail, true);
        }

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return Result.Failure<PermanentFileDto>(mutable.Error);

        TunisianLegalForm? legalForm = dto.LegalForm.HasValue && Enum.IsDefined(typeof(TunisianLegalForm), dto.LegalForm.Value)
            ? (TunisianLegalForm)dto.LegalForm.Value
            : null;
        TaxRegime? taxRegime = dto.TaxRegime.HasValue && Enum.IsDefined(typeof(TaxRegime), dto.TaxRegime.Value)
            ? (TaxRegime)dto.TaxRegime.Value
            : null;

        var step = Math.Max(1, dto.WizardStep);

        // Partial upsert by wizard step — avoid wiping fields from other steps
        if (step == 1)
        {
            file.UpdateIdentity(
                dto.CompanyName ?? file.CompanyName,
                dto.Nif ?? file.Nif,
                dto.RneIdentifier ?? file.RneIdentifier,
                legalForm ?? file.LegalForm,
                dto.IncorporationDate ?? file.IncorporationDate,
                dto.ShareCapital ?? file.ShareCapital);
            file.UpdateTaxAdministration(
                dto.TaxOffice ?? file.TaxOffice,
                taxRegime ?? file.TaxRegime,
                dto.HasTaxCertificate ?? file.HasTaxCertificate);
        }
        else if (step == 2)
        {
            file.AdvanceWizard(2);
        }
        else if (step == 3)
        {
            file.UpdateRegisteredOffice(dto.Street, dto.City, dto.Governorate, dto.PostalCode);
            file.UpdateAccountingYear(dto.FiscalYearStartMonth, dto.FiscalYearEndMonth);
            file.AdvanceWizard(3);
        }
        else if (step == 4)
        {
            file.UpdateCompliance(dto.LabCompleted, dto.MissionAccepted);
            file.UpdateLegalStatus(
                dto.CurrentLegalAct,
                dto.MissionStatus,
                dto.IsDigitized,
                dto.MissionResigned,
                dto.ResignationFiscalYear,
                dto.ResignationNotes);
            file.AdvanceWizard(4);
        }
        else if (step == 5)
        {
            BillingFrequency? freq = dto.BillingFrequency.HasValue && Enum.IsDefined(typeof(BillingFrequency), dto.BillingFrequency.Value)
                ? (BillingFrequency)dto.BillingFrequency.Value
                : null;
            file.UpdateBilling(dto.AnnualFeeAmount, freq, dto.Currency, dto.BillingNotes);
            file.AdvanceWizard(5);
        }
        else if (step >= 6)
        {
            var previousAccountantId = file.AssignedAccountantUserId;
            if (dto.AssignedAccountantUserId != previousAccountantId
                || (dto.AssignedAccountantUserId is null && !string.IsNullOrWhiteSpace(dto.AssignedAccountantName)))
            {
                var resolved = await ResolveAssignableAccountantAsync(firmTenantId, dto.AssignedAccountantUserId, cancellationToken);
                if (!resolved.IsSuccess)
                    return Result.Failure<PermanentFileDto>(resolved.Error);

                file.AssignAccountant(resolved.Value.UserId, resolved.Value.DisplayName);

                if (previousAccountantId != resolved.Value.UserId)
                {
                    var actor = await _userManager.FindByEmailAsync(userEmail);
                    var actorId = actor?.Id ?? Guid.Empty;
                    var now = DateTime.UtcNow;
                    var open = await _master.FirmDossierAssignmentHistories
                        .Where(h => h.FirmTenantId == firmTenantId
                                    && h.FirmClientAssignmentId == assignmentId
                                    && h.EndedAt == null)
                        .ToListAsync(cancellationToken);
                    foreach (var h in open)
                        h.EndedAt = now;
                    if (resolved.Value.UserId.HasValue && actorId != Guid.Empty)
                    {
                        _master.FirmDossierAssignmentHistories.Add(new FirmDossierAssignmentHistory
                        {
                            Id = Guid.NewGuid(),
                            FirmTenantId = firmTenantId,
                            FirmClientAssignmentId = assignmentId,
                            CompanyTenantId = file.CompanyTenantId,
                            AccountantUserId = resolved.Value.UserId,
                            AccountantDisplayName = resolved.Value.DisplayName,
                            AssignedByUserId = actorId,
                            AssignedAt = now
                        });
                    }
                }
            }

            if (dto.LabCompleted || dto.MissionAccepted)
                file.UpdateCompliance(dto.LabCompleted, dto.MissionAccepted);
            file.AdvanceWizard(6);

            var activeReps = await _master.LegalRepresentatives.AsNoTracking()
                .CountAsync(r => r.PermanentFileId == file.Id && r.IsActive, cancellationToken);

            if (dto.RequestCompletion && dto.MissionAccepted)
            {
                var complete = file.MarkComplete(activeReps);
                if (complete.IsFailure)
                {
                    if (file.Status == PermanentFileStatus.Complete)
                        file.ReopenToInProgress();
                    return Result.Failure<PermanentFileDto>(complete.Error);
                }
            }
            else if (file.Status == PermanentFileStatus.Complete && !dto.MissionAccepted)
            {
                file.ReopenToInProgress();
            }
        }

        await _master.SaveChangesAsync(cancellationToken);

        var reps = await _master.LegalRepresentatives.AsNoTracking()
            .Where(r => r.PermanentFileId == file.Id && r.IsActive)
            .ToListAsync(cancellationToken);
        var shareholders = await _master.Shareholders.AsNoTracking()
            .Where(s => s.PermanentFileId == file.Id && s.IsActive)
            .ToListAsync(cancellationToken);

        return Result.Success(MapPermanentFile(file, reps, shareholders));
    }

    public async Task<Result> SyncPermanentFileToTenantAsync(Guid firmTenantId, Guid assignmentId, string userEmail, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var file = await _master.PermanentFiles
            .FirstOrDefaultAsync(p => p.FirmTenantId == firmTenantId && p.FirmClientAssignmentId == assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure(Error.NotFound("PermanentFile", assignmentId));

        var syncable = file.EnsureSyncable();
        if (syncable.IsFailure)
            return syncable;

        var tenant = await _master.Tenants.FirstOrDefaultAsync(t => t.Id == file.CompanyTenantId, cancellationToken);
        if (tenant is null)
            return Result.Failure(Error.NotFound("Tenant", file.CompanyTenantId));

        if (!string.IsNullOrWhiteSpace(file.CompanyName))
            tenant.UpdateCompanyNameFromPermanentFile(file.CompanyName);
        if (file.TaxRegime.HasValue)
            tenant.UpdateTaxRegime(file.TaxRegime.Value);
        if (!string.IsNullOrWhiteSpace(file.Nif))
        {
            var nifUpdate = tenant.UpdateNifFromPermanentFile(file.Nif);
            if (nifUpdate.IsFailure)
                return Result.Failure(nifUpdate.Error);
        }

        file.MarkSyncedToTenant();
        file.SetAuditInfo(userEmail, true);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LegalRepresentativeDto>> AddRepresentativeAsync(
        Guid firmTenantId, Guid assignmentId, LegalRepresentativeDto dto, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<LegalRepresentativeDto>(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure<LegalRepresentativeDto>(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return Result.Failure<LegalRepresentativeDto>(mutable.Error);

        var create = LegalRepresentative.Create(file.Id, dto.LastName, dto.FirstName, dto.Role);
        if (create.IsFailure)
            return Result.Failure<LegalRepresentativeDto>(create.Error);

        var rep = create.Value;
        rep.UpdateContact(dto.Email, dto.Phone, dto.Cin, dto.Nationality, dto.CnssNumber);
        _master.LegalRepresentatives.Add(rep);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapRepresentative(rep));
    }

    public async Task<Result<LegalRepresentativeDto>> UpdateRepresentativeAsync(
        Guid firmTenantId, Guid assignmentId, Guid representativeId, LegalRepresentativeDto dto, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<LegalRepresentativeDto>(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure<LegalRepresentativeDto>(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return Result.Failure<LegalRepresentativeDto>(mutable.Error);

        var rep = await _master.LegalRepresentatives
            .FirstOrDefaultAsync(r => r.Id == representativeId && r.PermanentFileId == file.Id, cancellationToken);
        if (rep is null || !rep.IsActive)
            return Result.Failure<LegalRepresentativeDto>(Error.NotFound("LegalRepresentative", representativeId));

        var update = rep.Update(dto.LastName, dto.FirstName, dto.Role, dto.Email, dto.Phone, dto.Cin, dto.Nationality, dto.CnssNumber);
        if (update.IsFailure)
            return Result.Failure<LegalRepresentativeDto>(update.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapRepresentative(rep));
    }

    public async Task<Result<ShareholderDto>> AddShareholderAsync(
        Guid firmTenantId, Guid assignmentId, ShareholderDto dto, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<ShareholderDto>(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure<ShareholderDto>(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return Result.Failure<ShareholderDto>(mutable.Error);

        var create = Shareholder.Create(file.Id, dto.Name, dto.IsLegalEntity, dto.ShareCount, dto.SharePercentage);
        if (create.IsFailure)
            return Result.Failure<ShareholderDto>(create.Error);

        var sh = create.Value;
        sh.Update(dto.CinOrNif, dto.ShareCount, dto.SharePercentage);
        _master.Shareholders.Add(sh);
        await _master.SaveChangesAsync(cancellationToken);
        var warnings = await BuildShareholderWarningsAsync(file.Id, cancellationToken);
        return Result.Success(MapShareholder(sh, warnings));
    }

    public async Task<Result<ShareholderDto>> UpdateShareholderAsync(
        Guid firmTenantId, Guid assignmentId, Guid shareholderId, ShareholderDto dto, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<ShareholderDto>(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure<ShareholderDto>(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return Result.Failure<ShareholderDto>(mutable.Error);

        var sh = await _master.Shareholders
            .FirstOrDefaultAsync(s => s.Id == shareholderId && s.PermanentFileId == file.Id, cancellationToken);
        if (sh is null || !sh.IsActive)
            return Result.Failure<ShareholderDto>(Error.NotFound("Shareholder", shareholderId));

        var update = sh.Update(dto.Name, dto.IsLegalEntity, dto.CinOrNif, dto.ShareCount, dto.SharePercentage);
        if (update.IsFailure)
            return Result.Failure<ShareholderDto>(update.Error);

        await _master.SaveChangesAsync(cancellationToken);
        var warnings = await BuildShareholderWarningsAsync(file.Id, cancellationToken);
        return Result.Success(MapShareholder(sh, warnings));
    }

    public async Task<Result> ArchivePermanentFileAsync(
        Guid firmTenantId, Guid assignmentId, string userEmail, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure(Error.NotFound("PermanentFile", assignmentId));

        file.Archive();
        file.SetAuditInfo(userEmail, true);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateRepresentativeAsync(
        Guid firmTenantId, Guid assignmentId, Guid representativeId, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return mutable;

        var rep = await _master.LegalRepresentatives
            .FirstOrDefaultAsync(r => r.Id == representativeId && r.PermanentFileId == file.Id, cancellationToken);
        if (rep is null)
            return Result.Failure(Error.NotFound("LegalRepresentative", representativeId));

        rep.Deactivate();
        await _master.SaveChangesAsync(cancellationToken);

        var activeReps = await _master.LegalRepresentatives
            .CountAsync(r => r.PermanentFileId == file.Id && r.IsActive, cancellationToken);
        if (file.Status == PermanentFileStatus.Complete && activeReps < 1)
            file.ReopenToInProgress();

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateShareholderAsync(
        Guid firmTenantId, Guid assignmentId, Guid shareholderId, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, assignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var file = await EnsurePermanentFile(firmTenantId, assignmentId, cancellationToken);
        if (file is null)
            return Result.Failure(Error.NotFound("PermanentFile", assignmentId));

        var mutable = file.EnsureMutable();
        if (mutable.IsFailure)
            return mutable;

        var sh = await _master.Shareholders
            .FirstOrDefaultAsync(s => s.Id == shareholderId && s.PermanentFileId == file.Id, cancellationToken);
        if (sh is null)
            return Result.Failure(Error.NotFound("Shareholder", shareholderId));

        sh.Deactivate();
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<FirmTimeSheetEntryDto>> ListTimeSheetsAsync(
        Guid firmTenantId,
        int? year,
        int? month,
        Guid? userId = null,
        Guid? assignmentId = null,
        CancellationToken cancellationToken = default)
    {
        var allowedAssignmentIds = await ResolveAccessibleAssignmentIdsAsync(firmTenantId, cancellationToken);
        if (allowedAssignmentIds is { Count: 0 })
            return [];

        var q = _master.FirmTimeSheetEntries.AsNoTracking().Where(t => t.FirmTenantId == firmTenantId);
        if (year.HasValue)
            q = q.Where(t => t.WorkDate.Year == year.Value);
        if (month.HasValue)
            q = q.Where(t => t.WorkDate.Month == month.Value);
        if (userId.HasValue)
            q = q.Where(t => t.UserId == userId.Value);
        if (assignmentId.HasValue)
            q = q.Where(t => t.FirmClientAssignmentId == assignmentId.Value);
        if (allowedAssignmentIds is not null)
            q = q.Where(t => t.FirmClientAssignmentId != null && allowedAssignmentIds.Contains(t.FirmClientAssignmentId.Value));

        var rows = await q.OrderByDescending(t => t.WorkDate).ToListAsync(cancellationToken);
        return rows.Select(MapTimeSheet).ToList();
    }

    public async Task<Result<FirmTimeSheetEntryDto>> CreateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        CreateTimeSheetEntryDto dto,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = actorUserId;
        var targetUserName = actorUserName;
        if (dto.TargetUserId.HasValue && dto.TargetUserId.Value != actorUserId)
        {
            if (!isManager)
                return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Seul un manager peut saisir pour un autre collaborateur."));
            var target = await _userManager.FindByIdAsync(dto.TargetUserId.Value.ToString());
            if (target is null || target.TenantId != firmTenantId)
                return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("User", dto.TargetUserId.Value));
            targetUserId = target.Id;
            targetUserName = $"{target.FirstName} {target.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(targetUserName))
                targetUserName = target.Email ?? "Collaborateur";
        }

        string? clientName = null;
        if (dto.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return Result.Failure<FirmTimeSheetEntryDto>(accessDenied);

            clientName = await ResolveAssignmentCompanyNameAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
        }

        var periodError = await EnsurePeriodOpenAsync(firmTenantId, dto.WorkDate, cancellationToken);
        if (periodError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(periodError);

        var codeError = await EnsureActivityCodeAllowedAsync(firmTenantId, dto.ActivityCode, cancellationToken);
        if (codeError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(codeError);

        var start = ParseTimeOfDay(dto.StartTime);
        var end = ParseTimeOfDay(dto.EndTime);
        if (start.IsFailure) return Result.Failure<FirmTimeSheetEntryDto>(start.Error);
        if (end.IsFailure) return Result.Failure<FirmTimeSheetEntryDto>(end.Error);

        var hoursResult = FirmTimeSheetEntry.ResolveHours(dto.Hours, start.Value, end.Value);
        if (hoursResult.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(hoursResult.Error);

        var legal = await CheckLegalLimitsAsync(
            firmTenantId, targetUserId, dto.WorkDate, hoursResult.Value,
            start.Value, end.Value, excludedEntryId: null, cancellationToken);
        if (legal.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(legal.Error);

        var create = FirmTimeSheetEntry.Create(
            firmTenantId, targetUserId, targetUserName, dto.WorkDate, hoursResult.Value,
            dto.FirmClientAssignmentId, clientName, start.Value, end.Value);
        if (create.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(create.Error);

        var entry = create.Value;
        var apply = entry.Update(
            dto.WorkDate, hoursResult.Value, dto.FirmClientAssignmentId, clientName,
            dto.ActivityCode, dto.Notes, dto.IsBillable, start.Value, end.Value, dto.WorkLocation, dto.Tags);
        if (apply.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(apply.Error);

        entry.SetAuditInfo(actorUserName, isUpdate: false);
        _master.FirmTimeSheetEntries.Add(entry);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry) with { Warnings = legal.Value });
    }

    public async Task<Result<FirmTimeSheetEntryDto>> UpdateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        UpdateTimeSheetEntryDto dto,
        CancellationToken cancellationToken = default)
    {
        var entry = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.Id == entryId && t.FirmTenantId == firmTenantId, cancellationToken);
        if (entry is null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("TimeSheet", entryId));

        if (!isManager && entry.UserId != actorUserId)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Vous ne pouvez modifier que vos propres saisies."));

        if (!entry.CanEdit())
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Validation("TimeSheet", "Feuille de temps non modifiable (soumise ou validée)."));

        // La période d'origine et la période cible doivent toutes deux être ouvertes : déplacer une
        // ligne hors d'un mois clôturé reviendrait à modifier ce mois.
        var originError = await EnsurePeriodOpenAsync(firmTenantId, entry.WorkDate, cancellationToken);
        if (originError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(originError);
        var targetError = await EnsurePeriodOpenAsync(firmTenantId, dto.WorkDate, cancellationToken);
        if (targetError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(targetError);

        string? clientName = null;
        if (dto.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return Result.Failure<FirmTimeSheetEntryDto>(accessDenied);
            clientName = await ResolveAssignmentCompanyNameAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
        }

        var codeError = await EnsureActivityCodeAllowedAsync(firmTenantId, dto.ActivityCode, cancellationToken);
        if (codeError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(codeError);

        var start = ParseTimeOfDay(dto.StartTime);
        var end = ParseTimeOfDay(dto.EndTime);
        if (start.IsFailure) return Result.Failure<FirmTimeSheetEntryDto>(start.Error);
        if (end.IsFailure) return Result.Failure<FirmTimeSheetEntryDto>(end.Error);

        var hoursResult = FirmTimeSheetEntry.ResolveHours(dto.Hours, start.Value, end.Value);
        if (hoursResult.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(hoursResult.Error);

        var legal = await CheckLegalLimitsAsync(
            firmTenantId, entry.UserId, dto.WorkDate, hoursResult.Value,
            start.Value, end.Value, excludedEntryId: entry.Id, cancellationToken);
        if (legal.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(legal.Error);

        var update = entry.Update(
            dto.WorkDate, hoursResult.Value, dto.FirmClientAssignmentId, clientName,
            dto.ActivityCode, dto.Notes, dto.IsBillable, start.Value, end.Value, dto.WorkLocation, dto.Tags);
        if (update.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(update.Error);

        entry.SetAuditInfo(_currentUser.Email ?? actorUserId.ToString(), isUpdate: true);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry) with { Warnings = legal.Value });
    }

    public async Task<Result> DeleteTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.Id == entryId && t.FirmTenantId == firmTenantId, cancellationToken);
        if (entry is null)
            return Result.Failure(Error.NotFound("TimeSheet", entryId));

        if (!isManager && entry.UserId != actorUserId)
            return Result.Failure(Error.Forbidden("Vous ne pouvez supprimer que vos propres saisies."));

        if (entry.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(
                firmTenantId, entry.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return Result.Failure(accessDenied);
        }

        // Une ligne validée ne se supprime jamais directement, manager compris : il faut d'abord la
        // dévalider, ce qui laisse une trace. Sinon la pièce disparaît sans rien conserver.
        if (entry.IsValidated)
            return Result.Failure(Error.Validation(
                "TimeSheet",
                "Feuille de temps validée — dévalidez-la d'abord pour pouvoir la supprimer."));
        if (entry.Status == FirmTimeSheetStatus.Submitted)
            return Result.Failure(Error.Validation(
                "TimeSheet",
                "Feuille de temps soumise — non supprimable."));

        var periodError = await EnsurePeriodOpenAsync(firmTenantId, entry.WorkDate, cancellationToken);
        if (periodError is not null)
            return Result.Failure(periodError);

        _master.FirmTimeSheetEntries.Remove(entry);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<FirmTimeSheetEntryDto>> ValidateTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Seul un manager peut valider une feuille de temps."));

        var entry = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.Id == entryId && t.FirmTenantId == firmTenantId, cancellationToken);
        if (entry is null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("TimeSheet", entryId));

        var guard = await EnsureValidationAllowedAsync(firmTenantId, entry, cancellationToken);
        if (guard is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(guard);

        var validate = entry.Validate(actorUserId, actorUserName);
        if (validate.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(validate.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry));
    }

    public async Task<Result<FirmTimeSheetEntryDto>> UnvalidateTimeSheetAsync(
        Guid firmTenantId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Seul un manager peut dévalider une feuille de temps."));

        var entry = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.Id == entryId && t.FirmTenantId == firmTenantId, cancellationToken);
        if (entry is null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("TimeSheet", entryId));

        var guard = await EnsureValidationAllowedAsync(firmTenantId, entry, cancellationToken);
        if (guard is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(guard);

        var unvalidate = entry.Unvalidate();
        if (unvalidate.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(unvalidate.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry));
    }

    public async Task<Result<FirmTimeSheetEntryDto>> SubmitTimeSheetAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.Id == entryId && t.FirmTenantId == firmTenantId, cancellationToken);
        if (entry is null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("TimeSheet", entryId));

        if (!isManager && entry.UserId != actorUserId)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Vous ne pouvez soumettre que vos propres saisies."));

        var periodError = await EnsurePeriodOpenAsync(firmTenantId, entry.WorkDate, cancellationToken);
        if (periodError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(periodError);

        if (entry.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(
                firmTenantId, entry.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return Result.Failure<FirmTimeSheetEntryDto>(accessDenied);
        }

        var submit = entry.Submit();
        if (submit.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(submit.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry));
    }

    public async Task<Result<FirmTimeSheetEntryDto>> StartTimeSheetTimerAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        StartTimeSheetTimerDto dto,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = actorUserId;
        var targetUserName = actorUserName;
        if (dto.TargetUserId.HasValue && dto.TargetUserId.Value != actorUserId)
        {
            if (!isManager)
                return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Seul un manager peut démarrer un timer pour un autre collaborateur."));
            var target = await _userManager.FindByIdAsync(dto.TargetUserId.Value.ToString());
            if (target is null || target.TenantId != firmTenantId)
                return Result.Failure<FirmTimeSheetEntryDto>(Error.NotFound("User", dto.TargetUserId.Value));
            targetUserId = target.Id;
            targetUserName = $"{target.FirstName} {target.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(targetUserName))
                targetUserName = target.Email ?? "Collaborateur";
        }

        var existing = await _master.FirmTimeSheetEntries
            .FirstOrDefaultAsync(t => t.FirmTenantId == firmTenantId
                                      && t.UserId == targetUserId
                                      && t.TimerStartedAtUtc != null, cancellationToken);
        if (existing is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Validation("Timer", "Un timer est déjà en cours."));

        var workDate = (dto.WorkDate ?? ReportingPeriodResolver.GetTodayInTunisia(_timeProvider).ToDateTime(TimeOnly.MinValue)).Date;
        var periodError = await EnsurePeriodOpenAsync(firmTenantId, workDate, cancellationToken);
        if (periodError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(periodError);

        string? clientName = null;
        if (dto.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return Result.Failure<FirmTimeSheetEntryDto>(accessDenied);
            clientName = await ResolveAssignmentCompanyNameAsync(firmTenantId, dto.FirmClientAssignmentId.Value, cancellationToken);
        }

        var codeError = await EnsureActivityCodeAllowedAsync(firmTenantId, dto.ActivityCode, cancellationToken);
        if (codeError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(codeError);

        var create = FirmTimeSheetEntry.CreateTimerDraft(
            firmTenantId, targetUserId, targetUserName, workDate, DateTime.UtcNow,
            dto.FirmClientAssignmentId, clientName, dto.ActivityCode, dto.IsBillable);
        if (create.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(create.Error);

        var entry = create.Value;
        entry.SetAuditInfo(actorUserName, isUpdate: false);
        _master.FirmTimeSheetEntries.Add(entry);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry));
    }

    public async Task<Result<FirmTimeSheetEntryDto>> StopTimeSheetTimerAsync(
        Guid firmTenantId,
        Guid actorUserId,
        bool isManager,
        StopTimeSheetTimerDto dto,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = actorUserId;
        if (dto.TargetUserId.HasValue && dto.TargetUserId.Value != actorUserId)
        {
            if (!isManager)
                return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Seul un manager peut arrêter un timer pour un autre collaborateur."));
            targetUserId = dto.TargetUserId.Value;
        }

        FirmTimeSheetEntry? entry;
        if (dto.EntryId.HasValue)
        {
            entry = await _master.FirmTimeSheetEntries
                .FirstOrDefaultAsync(t => t.Id == dto.EntryId.Value && t.FirmTenantId == firmTenantId, cancellationToken);
        }
        else
        {
            entry = await _master.FirmTimeSheetEntries
                .FirstOrDefaultAsync(t => t.FirmTenantId == firmTenantId
                                          && t.UserId == targetUserId
                                          && t.TimerStartedAtUtc != null, cancellationToken);
        }

        if (entry is null)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Validation("Timer", "Aucun timer actif."));

        if (!isManager && entry.UserId != actorUserId)
            return Result.Failure<FirmTimeSheetEntryDto>(Error.Forbidden("Vous ne pouvez arrêter que votre propre timer."));

        var periodError = await EnsurePeriodOpenAsync(firmTenantId, entry.WorkDate, cancellationToken);
        if (periodError is not null)
            return Result.Failure<FirmTimeSheetEntryDto>(periodError);

        var stop = entry.StopTimer(DateTime.UtcNow);
        if (stop.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(stop.Error);

        var legal = await CheckLegalLimitsAsync(
            firmTenantId, entry.UserId, entry.WorkDate, entry.Hours,
            entry.StartTime, entry.EndTime, excludedEntryId: entry.Id, cancellationToken);
        if (legal.IsFailure)
            return Result.Failure<FirmTimeSheetEntryDto>(legal.Error);

        entry.SetAuditInfo(_currentUser.Email ?? actorUserId.ToString(), isUpdate: true);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapTimeSheet(entry) with { Warnings = legal.Value });
    }

    public async Task<Result<IReadOnlyList<FirmTimeSheetEntryDto>>> DuplicateTimeSheetWeekAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        DuplicateTimeSheetWeekDto dto,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = actorUserId;
        var targetUserName = actorUserName;
        if (dto.UserId.HasValue && dto.UserId.Value != actorUserId)
        {
            if (!isManager)
                return Result.Failure<IReadOnlyList<FirmTimeSheetEntryDto>>(
                    Error.Forbidden("Seul un manager peut dupliquer la semaine d'un autre collaborateur."));
            var target = await _userManager.FindByIdAsync(dto.UserId.Value.ToString());
            if (target is null || target.TenantId != firmTenantId)
                return Result.Failure<IReadOnlyList<FirmTimeSheetEntryDto>>(Error.NotFound("User", dto.UserId.Value));
            targetUserId = target.Id;
            targetUserName = $"{target.FirstName} {target.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(targetUserName))
                targetUserName = target.Email ?? "Collaborateur";
        }

        var sourceStart = dto.SourceWeekStart.Date;
        var targetStart = dto.TargetWeekStart.Date;
        var sourceEnd = sourceStart.AddDays(6);
        var dayOffset = (targetStart - sourceStart).Days;

        var sourceRows = await _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(t => t.FirmTenantId == firmTenantId
                        && t.UserId == targetUserId
                        && t.WorkDate >= sourceStart
                        && t.WorkDate <= sourceEnd
                        && t.TimerStartedAtUtc == null)
            .OrderBy(t => t.WorkDate)
            .ToListAsync(cancellationToken);

        var created = new List<FirmTimeSheetEntryDto>();
        var warnings = new List<string>();

        foreach (var src in sourceRows)
        {
            var newDate = src.WorkDate.Date.AddDays(dayOffset);
            var periodError = await EnsurePeriodOpenAsync(firmTenantId, newDate, cancellationToken);
            if (periodError is not null)
            {
                warnings.Add($"{newDate:dd/MM}: {periodError.Description}");
                continue;
            }

            var legal = await CheckLegalLimitsAsync(
                firmTenantId, targetUserId, newDate, src.Hours,
                src.StartTime, src.EndTime, excludedEntryId: null, cancellationToken);
            if (legal.IsFailure)
            {
                warnings.Add($"{newDate:dd/MM}: {legal.Error.Description}");
                continue;
            }

            var create = FirmTimeSheetEntry.Create(
                firmTenantId, targetUserId, targetUserName, newDate, src.Hours,
                src.FirmClientAssignmentId, src.ClientCompanyName, src.StartTime, src.EndTime);
            if (create.IsFailure)
            {
                warnings.Add($"{newDate:dd/MM}: {create.Error.Description}");
                continue;
            }

            var entry = create.Value;
            var apply = entry.Update(
                newDate, src.Hours, src.FirmClientAssignmentId, src.ClientCompanyName,
                src.ActivityCode, src.Notes, src.IsBillable, src.StartTime, src.EndTime,
                src.WorkLocation, src.Tags);
            if (apply.IsFailure)
            {
                warnings.Add($"{newDate:dd/MM}: {apply.Error.Description}");
                continue;
            }

            entry.SetAuditInfo(actorUserName, isUpdate: false);
            _master.FirmTimeSheetEntries.Add(entry);
            created.Add(MapTimeSheet(entry) with { Warnings = legal.Value });
        }

        if (created.Count > 0)
            await _master.SaveChangesAsync(cancellationToken);

        if (created.Count == 0 && warnings.Count > 0)
            return Result.Failure<IReadOnlyList<FirmTimeSheetEntryDto>>(
                Error.Validation("DuplicateWeek", string.Join(" ", warnings)));

        return Result.Success<IReadOnlyList<FirmTimeSheetEntryDto>>(created);
    }

    public async Task<Result<FirmTimeSheetBulkValidationResultDto>> ValidateTimeSheetsBulkAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        IReadOnlyList<Guid> entryIds,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmTimeSheetBulkValidationResultDto>(
                Error.Forbidden("Seul un manager peut valider des feuilles de temps."));

        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success(new FirmTimeSheetBulkValidationResultDto());

        var entries = await _master.FirmTimeSheetEntries
            .Where(t => t.FirmTenantId == firmTenantId && ids.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var validated = 0;
        var failures = new List<FirmTimeSheetBulkValidationFailureDto>();

        foreach (var id in ids)
        {
            var entry = entries.FirstOrDefault(e => e.Id == id);
            if (entry is null)
            {
                failures.Add(new FirmTimeSheetBulkValidationFailureDto
                {
                    EntryId = id,
                    Error = "Feuille de temps introuvable."
                });
                continue;
            }

            var guard = await EnsureValidationAllowedAsync(firmTenantId, entry, cancellationToken);
            if (guard is not null)
            {
                failures.Add(new FirmTimeSheetBulkValidationFailureDto
                {
                    EntryId = id,
                    Error = guard.Description
                });
                continue;
            }

            var validate = entry.Validate(actorUserId, actorUserName);
            if (validate.IsFailure)
            {
                failures.Add(new FirmTimeSheetBulkValidationFailureDto
                {
                    EntryId = id,
                    Error = validate.Error.Description
                });
                continue;
            }

            validated++;
        }

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(new FirmTimeSheetBulkValidationResultDto
        {
            Validated = validated,
            Skipped = failures.Count,
            Failures = failures
        });
    }

    // ============================================
    // CLÔTURE MENSUELLE
    // ============================================

    public async Task<IReadOnlyList<FirmTimeSheetPeriodDto>> ListTimeSheetPeriodsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var locks = await _master.FirmTimeSheetPeriodLocks.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId && p.Year == year)
            .ToListAsync(cancellationToken);

        // Les 12 mois sont toujours retournés : un mois sans ligne de verrou est simplement ouvert.
        return Enumerable.Range(1, 12)
            .Select(month =>
            {
                var existing = locks.FirstOrDefault(p => p.Month == month);
                return existing is null
                    ? new FirmTimeSheetPeriodDto { Year = year, Month = month, IsLocked = false }
                    : MapPeriod(existing);
            })
            .ToList();
    }

    public async Task<Result<FirmTimeSheetPeriodDto>> LockTimeSheetPeriodAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        int year,
        int month,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmTimeSheetPeriodDto>(
                Error.Forbidden("Seul un manager peut clôturer une période."));

        var existing = await _master.FirmTimeSheetPeriodLocks
            .FirstOrDefaultAsync(
                p => p.FirmTenantId == firmTenantId && p.Year == year && p.Month == month, cancellationToken);

        if (existing is null)
        {
            var create = FirmTimeSheetPeriodLock.Create(
                firmTenantId, year, month, actorUserId, actorUserName, reason);
            if (create.IsFailure)
                return Result.Failure<FirmTimeSheetPeriodDto>(create.Error);
            _master.FirmTimeSheetPeriodLocks.Add(create.Value);
            await _master.SaveChangesAsync(cancellationToken);
            return Result.Success(MapPeriod(create.Value));
        }

        var relock = existing.Lock(actorUserId, actorUserName, reason);
        if (relock.IsFailure)
            return Result.Failure<FirmTimeSheetPeriodDto>(relock.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapPeriod(existing));
    }

    public async Task<Result<FirmTimeSheetPeriodDto>> UnlockTimeSheetPeriodAsync(
        Guid firmTenantId,
        Guid actorUserId,
        string actorUserName,
        bool isManager,
        int year,
        int month,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmTimeSheetPeriodDto>(
                Error.Forbidden("Seul un manager peut rouvrir une période."));

        var existing = await _master.FirmTimeSheetPeriodLocks
            .FirstOrDefaultAsync(
                p => p.FirmTenantId == firmTenantId && p.Year == year && p.Month == month, cancellationToken);
        if (existing is null)
            return Result.Failure<FirmTimeSheetPeriodDto>(
                Error.Validation("Period", "La période n'est pas clôturée."));

        var unlock = existing.Unlock(actorUserId, actorUserName, reason);
        if (unlock.IsFailure)
            return Result.Failure<FirmTimeSheetPeriodDto>(unlock.Error);

        await _master.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Période feuilles de temps {Year}-{Month:00} rouverte par {UserId} — motif : {Reason}",
            year, month, actorUserId, reason);
        return Result.Success(MapPeriod(existing));
    }

    // ============================================
    // RÉFÉRENTIEL DES CODES ACTIVITÉ
    // ============================================

    public async Task<IReadOnlyList<FirmActivityCodeDto>> ListActivityCodesAsync(
        Guid firmTenantId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var q = _master.FirmActivityCodes.AsNoTracking().Where(a => a.FirmTenantId == firmTenantId);
        if (!includeInactive)
            q = q.Where(a => a.IsActive);

        var rows = await q.OrderBy(a => a.SortOrder).ThenBy(a => a.Code).ToListAsync(cancellationToken);
        return rows.Select(MapActivityCode).ToList();
    }

    public async Task<IReadOnlyList<FirmActivityCodeDto>> SeedDefaultActivityCodesAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default)
    {
        // Idempotent et non destructif : un cabinet qui a adapté, renommé ou désactivé des codes ne
        // doit jamais les voir réapparaître.
        var alreadyConfigured = await _master.FirmActivityCodes
            .AnyAsync(a => a.FirmTenantId == firmTenantId, cancellationToken);
        if (alreadyConfigured)
            return await ListActivityCodesAsync(firmTenantId, includeInactive: false, cancellationToken);

        var order = 0;
        foreach (var (code, label, category, billable) in FirmActivityCode.DefaultCatalog)
        {
            var create = FirmActivityCode.Create(firmTenantId, code, label, category, billable, order += 10);
            if (create.IsSuccess)
                _master.FirmActivityCodes.Add(create.Value);
        }

        await _master.SaveChangesAsync(cancellationToken);
        return await ListActivityCodesAsync(firmTenantId, includeInactive: false, cancellationToken);
    }

    public async Task<Result<FirmActivityCodeDto>> CreateActivityCodeAsync(
        Guid firmTenantId,
        bool isManager,
        SaveFirmActivityCodeDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmActivityCodeDto>(
                Error.Forbidden("Seul un manager peut modifier le référentiel des activités."));

        var normalized = FirmActivityCode.NormalizeCode(dto.Code);
        var duplicate = await _master.FirmActivityCodes.AsNoTracking()
            .AnyAsync(a => a.FirmTenantId == firmTenantId && a.Code == normalized, cancellationToken);
        if (duplicate)
            return Result.Failure<FirmActivityCodeDto>(
                Error.Conflict($"Le code activité « {normalized} » existe déjà."));

        var create = FirmActivityCode.Create(
            firmTenantId, dto.Code, dto.Label, ResolveCategory(dto.Category), dto.IsBillableByDefault, dto.SortOrder);
        if (create.IsFailure)
            return Result.Failure<FirmActivityCodeDto>(create.Error);

        _master.FirmActivityCodes.Add(create.Value);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapActivityCode(create.Value));
    }

    public async Task<Result<FirmActivityCodeDto>> UpdateActivityCodeAsync(
        Guid firmTenantId,
        bool isManager,
        Guid codeId,
        SaveFirmActivityCodeDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmActivityCodeDto>(
                Error.Forbidden("Seul un manager peut modifier le référentiel des activités."));

        var entity = await _master.FirmActivityCodes
            .FirstOrDefaultAsync(a => a.Id == codeId && a.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmActivityCodeDto>(Error.NotFound("ActivityCode", codeId));

        var update = entity.Update(dto.Label, ResolveCategory(dto.Category), dto.IsBillableByDefault, dto.SortOrder);
        if (update.IsFailure)
            return Result.Failure<FirmActivityCodeDto>(update.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapActivityCode(entity));
    }

    public async Task<Result<FirmActivityCodeDto>> SetActivityCodeActiveAsync(
        Guid firmTenantId,
        bool isManager,
        Guid codeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmActivityCodeDto>(
                Error.Forbidden("Seul un manager peut modifier le référentiel des activités."));

        var entity = await _master.FirmActivityCodes
            .FirstOrDefaultAsync(a => a.Id == codeId && a.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure<FirmActivityCodeDto>(Error.NotFound("ActivityCode", codeId));

        if (isActive)
            entity.Activate();
        else
            entity.Deactivate();

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapActivityCode(entity));
    }

    /// <summary>
    /// Contrôle qu'un code de saisie appartient au référentiel.
    /// </summary>
    /// <remarks>
    /// Volontairement permissif tant que le cabinet n'a aucun code actif : imposer un référentiel
    /// vide bloquerait toute saisie. Un code vide reste toujours accepté — il n'est pas obligatoire.
    /// </remarks>
    private async Task<Error?> EnsureActivityCodeAllowedAsync(
        Guid firmTenantId, string? activityCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityCode))
            return null;

        var active = await _master.FirmActivityCodes.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.IsActive)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);
        if (active.Count == 0)
            return null;

        var normalized = FirmActivityCode.NormalizeCode(activityCode);
        return active.Contains(normalized)
            ? null
            : Error.Validation(
                "ActivityCode",
                $"Le code activité « {normalized} » ne fait pas partie du référentiel du cabinet.");
    }

    private static FirmActivityCategory ResolveCategory(int value) =>
        Enum.IsDefined(typeof(FirmActivityCategory), value)
            ? (FirmActivityCategory)value
            : FirmActivityCategory.Accounting;

    private static FirmActivityCodeDto MapActivityCode(FirmActivityCode a) => new()
    {
        Id = a.Id,
        Code = a.Code,
        Label = a.Label,
        Category = (int)a.Category,
        CategoryDisplay = a.Category switch
        {
            FirmActivityCategory.Accounting => "Comptabilité",
            FirmActivityCategory.Tax => "Fiscal",
            FirmActivityCategory.Social => "Social",
            FirmActivityCategory.Audit => "Audit",
            FirmActivityCategory.Advisory => "Conseil",
            FirmActivityCategory.Internal => "Interne",
            _ => "Autre"
        },
        IsBillableByDefault = a.IsBillableByDefault,
        IsActive = a.IsActive,
        SortOrder = a.SortOrder
    };

    // ============================================
    // PARAMÈTRES D'EXERCICE
    // ============================================

    public async Task<FirmTimeSheetYearSettingsDto> GetTimeSheetYearSettingsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var settings = await ResolveYearSettingsAsync(firmTenantId, year, cancellationToken);
        return MapYearSettings(settings);
    }

    public async Task<Result<FirmTimeSheetYearSettingsDto>> SaveTimeSheetYearSettingsAsync(
        Guid firmTenantId,
        int year,
        SaveFirmTimeSheetYearSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _master.FirmTimeSheetYearSettings
            .FirstOrDefaultAsync(s => s.FirmTenantId == firmTenantId && s.Year == year, cancellationToken);

        if (entity is null)
        {
            var create = FirmTimeSheetYearSettings.Create(firmTenantId, year);
            if (create.IsFailure)
                return Result.Failure<FirmTimeSheetYearSettingsDto>(create.Error);
            entity = create.Value;
            _master.FirmTimeSheetYearSettings.Add(entity);
        }

        var regime = Enum.IsDefined(typeof(WeeklyWorkRegime), dto.WeeklyRegime)
            ? (WeeklyWorkRegime)dto.WeeklyRegime
            : WeeklyWorkRegime.FortyEightHours;

        var update = entity.Update(
            regime,
            dto.MaxDailyHours,
            dto.MaxWeeklyHours,
            dto.AllowFutureEntryDays,
            dto.MaxBackdatingDays,
            dto.EnforceHardLimits,
            dto.PaidLeaveDaysPerYear,
            dto.PublicHolidayDaysPerYear,
            dto.ProductivityRatePercent,
            dto.CnssEmployerRate,
            dto.TfpRate,
            dto.FoprolosRate,
            dto.WorkAccidentRate);
        if (update.IsFailure)
            return Result.Failure<FirmTimeSheetYearSettingsDto>(update.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapYearSettings(entity));
    }

    // ============================================
    // HELPERS FEUILLES DE TEMPS
    // ============================================

    /// <summary>
    /// Renvoie les paramètres persistés de l'exercice, ou un jeu de défauts transitoire.
    /// </summary>
    /// <remarks>
    /// Volontairement sans écriture : une lecture ne doit pas créer de ligne en base. Les exercices
    /// antérieurs à l'année en cours sont retournés avec <c>EnforceHardLimits = false</c>, ce qui
    /// laisse l'historique modifiable même s'il enfreint les plafonds introduits depuis.
    /// </remarks>
    private async Task<FirmTimeSheetYearSettings> ResolveYearSettingsAsync(
        Guid firmTenantId, int year, CancellationToken cancellationToken)
    {
        var persisted = await _master.FirmTimeSheetYearSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FirmTenantId == firmTenantId && s.Year == year, cancellationToken);
        if (persisted is not null)
            return persisted;

        var currentYear = ReportingPeriodResolver.GetTodayInTunisia(_timeProvider).Year;
        var create = FirmTimeSheetYearSettings.Create(
            firmTenantId, year, enforceHardLimits: year >= currentYear);
        return create.IsSuccess
            ? create.Value
            : FirmTimeSheetYearSettings.Create(firmTenantId, currentYear, enforceHardLimits: false).Value;
    }

    /// <summary>
    /// Applique les contrôles de durée et de datation.
    /// </summary>
    /// <returns>
    /// Un échec si les plafonds sont opposables sur l'exercice, sinon la liste des avertissements
    /// à restituer sans blocage.
    /// </returns>
    private async Task<Result<IReadOnlyList<string>>> CheckLegalLimitsAsync(
        Guid firmTenantId,
        Guid userId,
        DateTime workDate,
        decimal hours,
        TimeSpan? startTime,
        TimeSpan? endTime,
        Guid? excludedEntryId,
        CancellationToken cancellationToken)
    {
        var settings = await ResolveYearSettingsAsync(firmTenantId, workDate.Year, cancellationToken);
        var (weekStart, weekEnd) = TimeSheetLegalValidator.IsoWeekBounds(workDate);
        var day = workDate.Date;

        // Une seule requête couvre la semaine ISO, qui contient forcément le jour concerné.
        var weekRows = await _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(t => t.FirmTenantId == firmTenantId
                        && t.UserId == userId
                        && t.WorkDate >= weekStart
                        && t.WorkDate <= weekEnd
                        && (excludedEntryId == null || t.Id != excludedEntryId))
            .Select(t => new { t.WorkDate, t.Hours, t.StartTime, t.EndTime })
            .ToListAsync(cancellationToken);

        var otherHoursSameDay = weekRows.Where(r => r.WorkDate.Date == day).Sum(r => r.Hours);
        var otherHoursSameWeek = weekRows.Sum(r => r.Hours);

        var today = ReportingPeriodResolver.GetTodayInTunisia(_timeProvider).ToDateTime(TimeOnly.MinValue);
        var anomalies = TimeSheetLegalValidator.Validate(
            workDate, hours, otherHoursSameDay, otherHoursSameWeek, today, settings).ToList();

        var otherSlots = weekRows
            .Where(r => r.WorkDate.Date == day && r.StartTime.HasValue && r.EndTime.HasValue)
            .Select(r => (r.StartTime!.Value, r.EndTime!.Value));
        anomalies.AddRange(TimeSheetLegalValidator.ValidateSlotOverlap(startTime, endTime, otherSlots));

        if (anomalies.Count == 0)
            return Result.Success<IReadOnlyList<string>>(Array.Empty<string>());

        var messages = anomalies.Select(a => a.Message).ToList();
        if (settings.EnforceHardLimits)
            return Result.Failure<IReadOnlyList<string>>(
                Error.Validation("TimeSheet", string.Join(" ", messages)));

        return Result.Success<IReadOnlyList<string>>(messages);
    }

    private static Result<TimeSpan?> ParseTimeOfDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Success<TimeSpan?>(null);
        if (TimeSpan.TryParse(value.Trim(), out var ts))
            return Result.Success<TimeSpan?>(ts);
        return Result.Failure<TimeSpan?>(Error.Validation("TimeSlot", $"Heure invalide : {value}"));
    }

    private static string? FormatTimeOfDay(TimeSpan? value)
        => value.HasValue ? $"{(int)value.Value.TotalHours:00}:{value.Value.Minutes:00}" : null;

    private static string StatusDisplayOf(FirmTimeSheetStatus status) => status switch
    {
        FirmTimeSheetStatus.Submitted => "Soumis",
        FirmTimeSheetStatus.Validated => "Validé",
        _ => "Brouillon"
    };

    /// <summary>Refuse toute écriture sur un mois clôturé.</summary>
    private async Task<Error?> EnsurePeriodOpenAsync(
        Guid firmTenantId, DateTime workDate, CancellationToken cancellationToken)
    {
        var locked = await _master.FirmTimeSheetPeriodLocks.AsNoTracking()
            .AnyAsync(p => p.FirmTenantId == firmTenantId
                           && p.Year == workDate.Year
                           && p.Month == workDate.Month
                           && p.IsLocked, cancellationToken);

        return locked
            ? Error.Validation(
                "Period",
                $"Période {workDate:MM/yyyy} clôturée — déverrouillage manager requis.")
            : null;
    }

    /// <summary>Contrôles communs à la validation et à la dévalidation : périmètre puis clôture.</summary>
    private async Task<Error?> EnsureValidationAllowedAsync(
        Guid firmTenantId, FirmTimeSheetEntry entry, CancellationToken cancellationToken)
    {
        if (entry.FirmClientAssignmentId.HasValue)
        {
            var accessDenied = await EnsureCanAccessAssignmentAsync(
                firmTenantId, entry.FirmClientAssignmentId.Value, cancellationToken);
            if (accessDenied is not null)
                return accessDenied;
        }

        return await EnsurePeriodOpenAsync(firmTenantId, entry.WorkDate, cancellationToken);
    }

    /// <summary>
    /// Libellé client d'une saisie de temps.
    /// </summary>
    /// <remarks>
    /// Le dossier permanent prime sur le tenant : c'est la source retenue par l'analyse de
    /// rentabilité, et deux libellés divergents pour un même dossier rendraient les deux écrans
    /// impossibles à rapprocher.
    /// </remarks>
    private async Task<string?> ResolveAssignmentCompanyNameAsync(
        Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var permanentName = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId && p.FirmClientAssignmentId == assignmentId)
            .Select(p => p.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(permanentName))
            return permanentName;

        var assignment = await _master.FirmClientAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId, cancellationToken);
        if (assignment is null) return null;
        var tenant = await _master.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == assignment.CompanyTenantId, cancellationToken);
        return tenant?.CompanyName;
    }

    public async Task<IReadOnlyList<FirmExpenseNoteDto>> ListExpenseNotesAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var allowedAssignmentIds = await ResolveAccessibleAssignmentIdsAsync(firmTenantId, cancellationToken);
        if (allowedAssignmentIds is { Count: 0 })
            return [];

        var notesQuery = _master.FirmExpenseNotes.AsNoTracking()
            .Where(n => n.FirmTenantId == firmTenantId);
        if (allowedAssignmentIds is not null)
            notesQuery = notesQuery.Where(n => allowedAssignmentIds.Contains(n.FirmClientAssignmentId));

        var notes = await notesQuery
            .OrderByDescending(n => n.PeriodYear).ThenByDescending(n => n.PeriodMonth)
            .ToListAsync(cancellationToken);
        return notes.Select(MapExpenseNote).ToList();
    }

    public async Task<Result<FirmExpenseNoteDto>> UpsertExpenseNoteAsync(
        Guid firmTenantId, UpsertFirmExpenseNoteDto dto, CancellationToken cancellationToken = default)
    {
        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, dto.FirmClientAssignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure<FirmExpenseNoteDto>(accessDenied);

        var assignment = await GetActiveAssignment(firmTenantId, dto.FirmClientAssignmentId, cancellationToken);
        if (assignment is null)
            return Result.Failure<FirmExpenseNoteDto>(Error.NotFound("Assignment", dto.FirmClientAssignmentId));

        var tenant = await _master.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == assignment.CompanyTenantId, cancellationToken);
        var note = await _master.FirmExpenseNotes.FirstOrDefaultAsync(
            n => n.FirmTenantId == firmTenantId && n.FirmClientAssignmentId == dto.FirmClientAssignmentId
                 && n.PeriodYear == dto.PeriodYear && n.PeriodMonth == dto.PeriodMonth, cancellationToken);

        if (note is null)
        {
            var create = FirmExpenseNote.Create(firmTenantId, dto.FirmClientAssignmentId, tenant?.CompanyName ?? "Client", dto.PeriodYear, dto.PeriodMonth);
            if (create.IsFailure)
                return Result.Failure<FirmExpenseNoteDto>(create.Error);
            note = create.Value;
            _master.FirmExpenseNotes.Add(note);
        }

        var update = note.UpdateAmounts(dto.TotalToReimburse, dto.MixedCharges, dto.OperatingExpenses, dto.MileageAllowance, dto.SalesAmount, dto.Notes);
        if (update.IsFailure)
            return Result.Failure<FirmExpenseNoteDto>(update.Error);

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(MapExpenseNote(note));
    }

    public async Task<Result> ProcessExpenseNoteAsync(Guid firmTenantId, Guid noteId, bool approve, CancellationToken cancellationToken = default)
    {
        var note = await _master.FirmExpenseNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.FirmTenantId == firmTenantId, cancellationToken);
        if (note is null)
            return Result.Failure(Error.NotFound("ExpenseNote", noteId));

        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, note.FirmClientAssignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var transition = approve ? note.Approve() : note.Reject();
        if (transition.IsFailure)
            return transition;

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SubmitExpenseNoteAsync(Guid firmTenantId, Guid noteId, CancellationToken cancellationToken = default)
    {
        var note = await _master.FirmExpenseNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.FirmTenantId == firmTenantId, cancellationToken);
        if (note is null)
            return Result.Failure(Error.NotFound("ExpenseNote", noteId));

        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, note.FirmClientAssignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var transition = note.Submit();
        if (transition.IsFailure)
            return transition;

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkExpenseNoteReimbursedAsync(Guid firmTenantId, Guid noteId, CancellationToken cancellationToken = default)
    {
        var note = await _master.FirmExpenseNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.FirmTenantId == firmTenantId, cancellationToken);
        if (note is null)
            return Result.Failure(Error.NotFound("ExpenseNote", noteId));

        var accessDenied = await EnsureCanAccessAssignmentAsync(firmTenantId, note.FirmClientAssignmentId, cancellationToken);
        if (accessDenied is not null)
            return Result.Failure(accessDenied);

        var transition = note.MarkReimbursed();
        if (transition.IsFailure)
            return transition;

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<FirmGovernanceDashboardDto> GetGovernanceDashboardAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var allowedAssignmentIds = await ResolveAccessibleAssignmentIdsAsync(firmTenantId, cancellationToken);
        var allowedCompanyIds = await ResolveAccessibleCompanyIdsAsync(firmTenantId, cancellationToken);
        if (allowedAssignmentIds is { Count: 0 })
        {
            return new FirmGovernanceDashboardDto();
        }

        var activeQuery = _master.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active);
        if (allowedAssignmentIds is not null)
            activeQuery = activeQuery.Where(a => allowedAssignmentIds.Contains(a.Id));
        var activeAssignments = await activeQuery.CountAsync(cancellationToken);

        var pfQuery = _master.PermanentFiles.AsNoTracking().Where(p => p.FirmTenantId == firmTenantId);
        if (allowedAssignmentIds is not null)
            pfQuery = pfQuery.Where(p => allowedAssignmentIds.Contains(p.FirmClientAssignmentId));
        var complete = await pfQuery.CountAsync(p => p.Status == PermanentFileStatus.Complete, cancellationToken);
        var inProgress = await pfQuery.CountAsync(
            p => p.Status != PermanentFileStatus.Complete && p.Status != PermanentFileStatus.Archived, cancellationToken);

        var now = DateTime.UtcNow;
        var tsQuery = _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(t => t.FirmTenantId == firmTenantId && t.IsBillable);
        if (allowedAssignmentIds is not null)
            tsQuery = tsQuery.Where(t => t.FirmClientAssignmentId != null && allowedAssignmentIds.Contains(t.FirmClientAssignmentId.Value));
        var monthHours = await tsQuery
            .Where(t => t.WorkDate.Year == now.Year && t.WorkDate.Month == now.Month)
            .SumAsync(t => t.Hours, cancellationToken);
        var yearHours = await tsQuery
            .Where(t => t.WorkDate.Year == now.Year)
            .SumAsync(t => t.Hours, cancellationToken);

        var expenseQuery = _master.FirmExpenseNotes.AsNoTracking()
            .Where(n => n.FirmTenantId == firmTenantId && n.Status == FirmExpenseNoteStatus.Submitted);
        if (allowedAssignmentIds is not null)
            expenseQuery = expenseQuery.Where(n => allowedAssignmentIds.Contains(n.FirmClientAssignmentId));
        var pendingExpenses = await expenseQuery.CountAsync(cancellationToken);

        List<Guid> companyIds;
        if (allowedCompanyIds is not null)
            companyIds = allowedCompanyIds.ToList();
        else
        {
            companyIds = await _master.FirmClientAssignments.AsNoTracking()
                .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active)
                .Select(a => a.CompanyTenantId)
                .ToListAsync(cancellationToken);
        }

        var fiscal = companyIds.Count > 0
            ? await _fiscalOps.AggregateAsync(companyIds, cancellationToken)
            : new FirmFiscalOpsSummaryDto();

        return new FirmGovernanceDashboardDto
        {
            ActiveDossiersCount = activeAssignments,
            PermanentFilesCompleteCount = complete,
            PermanentFilesInProgressCount = inProgress,
            TotalBillableHoursMonth = monthHours,
            TotalBillableHoursYear = yearHours,
            PendingExpenseNotesCount = pendingExpenses,
            OverdueFiscalSchedulesCount = fiscal.OverdueSchedulesCount
        };
    }

    public async Task<FirmSocialOverviewDto> GetSocialOverviewAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var allowedCompanyIds = await ResolveAccessibleCompanyIdsAsync(firmTenantId, cancellationToken);
        if (allowedCompanyIds is { Count: 0 })
            return new FirmSocialOverviewDto { Clients = [] };

        var clientsQuery =
            from a in _master.FirmClientAssignments.AsNoTracking()
            join t in _master.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            select new { a.CompanyTenantId, t.CompanyName };

        if (allowedCompanyIds is not null)
            clientsQuery = clientsQuery.Where(c => allowedCompanyIds.Contains(c.CompanyTenantId));

        var clients = await clientsQuery.ToListAsync(cancellationToken);

        var rows = new List<FirmSocialClientRowDto>();
        foreach (var c in clients)
        {
            var row = new FirmSocialClientRowDto
            {
                CompanyTenantId = c.CompanyTenantId,
                CompanyName = c.CompanyName
            };
            try
            {
                var conn = await _tenantService.GetConnectionStringAsync(c.CompanyTenantId, cancellationToken);
                if (string.IsNullOrEmpty(conn))
                {
                    rows.Add(row);
                    continue;
                }

                await using var ctx = CreateTenantContext(conn);
                row = row with
                {
                    EmployeeCount = await ctx.Set<Domain.Entities.Payroll.Employee>().AsNoTracking().CountAsync(e => e.IsActive, cancellationToken),
                    PendingLeaveRequests = await ctx.LeaveRequests.AsNoTracking()
                        .CountAsync(l => !l.IsApproved, cancellationToken),
                    PayrollRunsDraftCount = await ctx.PayrollRuns.AsNoTracking()
                        .CountAsync(r => r.Status == PayrollRunStatus.Draft, cancellationToken),
                    DtsPendingCount = await ctx.FiscalScheduleEntries.AsNoTracking()
                        .CountAsync(e =>
                            !e.IsCancelled
                            && e.ObligationType == FiscalObligationType.CnssDtsQuarterly
                            && e.DepositDate == null
                            && e.DueDate.Date <= DateTime.UtcNow.Date.AddDays(30),
                            cancellationToken)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Social overview fan-out failed for tenant {TenantId}", c.CompanyTenantId);
            }
            rows.Add(row);
        }

        return new FirmSocialOverviewDto { Clients = rows };
    }

    public async Task<Result> AssignDossierManagerAsync(
        Guid firmTenantId,
        Guid assignedByUserId,
        AssignDossierManagerDto dto,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAssignableAccountantAsync(firmTenantId, dto.AccountantUserId, cancellationToken);
        if (!resolved.IsSuccess)
            return Result.Failure(resolved.Error);

        return await AssignDossierManagerCoreAsync(
            firmTenantId,
            assignedByUserId,
            dto.AssignmentId,
            resolved.Value.UserId,
            resolved.Value.DisplayName,
            cancellationToken);
    }

    public async Task<Result<AssignDossierManagerBulkResultDto>> AssignDossierManagerBulkAsync(
        Guid firmTenantId,
        Guid assignedByUserId,
        AssignDossierManagerBulkDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.AssignmentIds.Count == 0)
            return Result.Failure<AssignDossierManagerBulkResultDto>(
                Error.Validation("AssignmentIds", "Sélectionnez au moins un dossier."));

        var resolved = await ResolveAssignableAccountantAsync(firmTenantId, dto.AccountantUserId, cancellationToken);
        if (!resolved.IsSuccess)
            return Result.Failure<AssignDossierManagerBulkResultDto>(resolved.Error);

        var succeeded = 0;
        var failed = new List<AssignDossierManagerBulkFailureDto>();
        foreach (var assignmentId in dto.AssignmentIds.Distinct())
        {
            var result = await AssignDossierManagerCoreAsync(
                firmTenantId,
                assignedByUserId,
                assignmentId,
                resolved.Value.UserId,
                resolved.Value.DisplayName,
                cancellationToken);
            if (result.IsSuccess)
                succeeded++;
            else
                failed.Add(new AssignDossierManagerBulkFailureDto
                {
                    AssignmentId = assignmentId,
                    Error = result.Error.Description
                });
        }

        return Result.Success(new AssignDossierManagerBulkResultDto
        {
            Succeeded = succeeded,
            Failed = failed
        });
    }

    public async Task<IReadOnlyList<FirmDossierAssignmentListItemDto>> ListDossierAssignmentsAsync(
        Guid firmTenantId,
        int assignmentFilter,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var clients = await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join t in _master.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            orderby t.CompanyName
            select new
            {
                a.Id,
                a.CompanyTenantId,
                t.CompanyName,
                ActiveSince = a.RespondedAt ?? a.RequestedAt
            }).ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2)
        {
            var n = name.Trim();
            clients = clients.Where(c => c.CompanyName.Contains(n, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var permanentByAssignment = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId)
            .Select(p => new
            {
                p.FirmClientAssignmentId,
                p.Status,
                p.AssignedAccountantUserId,
                p.AssignedAccountantName
            })
            .ToDictionaryAsync(p => p.FirmClientAssignmentId, cancellationToken);

        IEnumerable<FirmDossierAssignmentListItemDto> mapped = clients.Select(c =>
        {
            permanentByAssignment.TryGetValue(c.Id, out var pf);
            var awaiting = pf is null || pf.AssignedAccountantUserId is null;
            return new FirmDossierAssignmentListItemDto
            {
                AssignmentId = c.Id,
                CompanyTenantId = c.CompanyTenantId,
                CompanyName = c.CompanyName,
                ActiveSince = c.ActiveSince,
                HasPermanentFile = pf is not null,
                PermanentFileStatus = pf is not null ? (int?)pf.Status : null,
                PermanentFileStatusDisplay = FormatPermanentFileStatus(pf?.Status),
                AssignedAccountantUserId = pf?.AssignedAccountantUserId,
                AssignedAccountantName = pf?.AssignedAccountantName,
                IsAwaitingAccountantAssignment = awaiting
            };
        });

        // 1 = en attente, 2 = affectés, 0 = tous
        mapped = assignmentFilter switch
        {
            1 => mapped.Where(x => x.IsAwaitingAccountantAssignment),
            2 => mapped.Where(x => !x.IsAwaitingAccountantAssignment),
            _ => mapped
        };

        return mapped.ToList();
    }

    public async Task<IReadOnlyList<FirmAssignableAccountantDto>> ListAssignableAccountantsAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default)
    {
        var users = await _master.Users.AsNoTracking()
            .Where(u => u.TenantId == firmTenantId && u.IsActive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var result = new List<FirmAssignableAccountantDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains(UserRole.FirmAccountant.ToString()))
                continue;

            result.Add(new FirmAssignableAccountantDto
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Email = user.Email ?? "",
                Role = UserRole.FirmAccountant,
                RoleDisplay = UserRole.FirmAccountant.ToDisplayString()
            });
        }

        return result;
    }

    private async Task<Result> AssignDossierManagerCoreAsync(
        Guid firmTenantId,
        Guid assignedByUserId,
        Guid assignmentId,
        Guid? accountantUserId,
        string? accountantDisplayName,
        CancellationToken cancellationToken)
    {
        var assignment = await GetActiveAssignment(firmTenantId, assignmentId, cancellationToken);
        if (assignment is null)
            return Result.Failure(Error.NotFound("Assignment", assignmentId));

        var file = await _master.PermanentFiles
            .FirstOrDefaultAsync(p => p.FirmTenantId == firmTenantId && p.FirmClientAssignmentId == assignmentId, cancellationToken);

        if (file is null)
        {
            var profile = await ResolveCompanyProfileAsync(assignment, cancellationToken);
            var create = PermanentFile.Create(
                assignmentId,
                firmTenantId,
                assignment.CompanyTenantId,
                profile?.CompanyName);
            if (create.IsFailure)
                return Result.Failure(create.Error);
            file = create.Value;
            if (profile is not null)
                PermanentFileCompanyProfileSeeder.Apply(profile, file);
            _master.PermanentFiles.Add(file);
        }

        // Idempotence
        if (file.AssignedAccountantUserId == accountantUserId)
            return Result.Success();

        file.AssignAccountant(accountantUserId, accountantDisplayName);

        var openHistories = await _master.FirmDossierAssignmentHistories
            .Where(h => h.FirmTenantId == firmTenantId
                        && h.FirmClientAssignmentId == assignmentId
                        && h.EndedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var h in openHistories)
            h.EndedAt = now;

        if (accountantUserId.HasValue)
        {
            _master.FirmDossierAssignmentHistories.Add(new FirmDossierAssignmentHistory
            {
                Id = Guid.NewGuid(),
                FirmTenantId = firmTenantId,
                FirmClientAssignmentId = assignmentId,
                CompanyTenantId = assignment.CompanyTenantId,
                AccountantUserId = accountantUserId,
                AccountantDisplayName = accountantDisplayName,
                AssignedByUserId = assignedByUserId,
                AssignedAt = now
            });
        }

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<(Guid? UserId, string? DisplayName)>> ResolveAssignableAccountantAsync(
        Guid firmTenantId,
        Guid? accountantUserId,
        CancellationToken cancellationToken)
    {
        if (accountantUserId is null)
            return Result.Success<(Guid?, string?)>((null, null));

        var user = await _master.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == accountantUserId.Value && u.TenantId == firmTenantId, cancellationToken);
        if (user is null)
            return Result.Failure<(Guid?, string?)>(
                Error.Validation("Accountant", "Collaborateur introuvable dans ce cabinet."));
        if (!user.IsActive)
            return Result.Failure<(Guid?, string?)>(
                Error.Validation("Accountant", "Le collaborateur est inactif."));

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(UserRole.FirmAccountant.ToString()))
            return Result.Failure<(Guid?, string?)>(
                Error.Validation("Accountant", "Seuls les comptables cabinet (gestionnaires comptables) peuvent être affectés."));

        var display = $"{user.FirstName} {user.LastName}".Trim();
        return Result.Success<(Guid?, string?)>((user.Id, display));
    }

    private static string? FormatPermanentFileStatus(PermanentFileStatus? status) => status switch
    {
        null => null,
        PermanentFileStatus.Complete => "Complet",
        PermanentFileStatus.InProgress => "En cours",
        PermanentFileStatus.Archived => "Archivé",
        _ => "Brouillon"
    };

    private async Task<IReadOnlySet<Guid>?> ResolveAccessibleAssignmentIdsAsync(
        Guid firmTenantId, CancellationToken cancellationToken)
    {
        if (_currentUser.TryGetAccessScope(out var scope))
            return await _dossierAccess.GetAccessibleAssignmentIdsAsync(firmTenantId, scope, cancellationToken);

        if (_currentUser.IsAuthenticated && _currentUser.Role is UserRole.FirmAccountant)
            return new HashSet<Guid>();

        return null;
    }

    private async Task<IReadOnlySet<Guid>?> ResolveAccessibleCompanyIdsAsync(
        Guid firmTenantId, CancellationToken cancellationToken)
    {
        if (_currentUser.TryGetAccessScope(out var scope))
            return await _dossierAccess.GetAccessibleCompanyTenantIdsAsync(firmTenantId, scope, cancellationToken);

        if (_currentUser.IsAuthenticated && _currentUser.Role is UserRole.FirmAccountant)
            return new HashSet<Guid>();

        return null;
    }

    private async Task<bool> CanAccessAssignmentOrFailOpenManagerAsync(
        Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetAccessScope(out var scope))
        {
            // Hors contexte HTTP (tests) ou rôle non restreint : autoriser.
            return !(_currentUser.IsAuthenticated && _currentUser.Role is UserRole.FirmAccountant);
        }

        return await _dossierAccess.CanAccessAssignmentAsync(firmTenantId, scope, assignmentId, cancellationToken);
    }

    private async Task<Error?> EnsureCanAccessAssignmentAsync(
        Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken)
    {
        if (await CanAccessAssignmentOrFailOpenManagerAsync(firmTenantId, assignmentId, cancellationToken))
            return null;

        return Error.Forbidden(FirmDossierAccessService.NotAssignedMessage);
    }

    private async Task<FirmClientAssignment?> GetActiveAssignment(Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken)
    {
        return await _master.FirmClientAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active, cancellationToken);
    }

    private async Task<PermanentFile?> EnsurePermanentFile(Guid firmTenantId, Guid assignmentId, CancellationToken cancellationToken)
    {
        return await _master.PermanentFiles
            .FirstOrDefaultAsync(p => p.FirmTenantId == firmTenantId && p.FirmClientAssignmentId == assignmentId, cancellationToken);
    }

    private static PermanentFileDto MapPermanentFile(
        PermanentFile file,
        IReadOnlyList<LegalRepresentative>? reps = null,
        IReadOnlyList<Shareholder>? shareholders = null,
        int? activeRepCount = null)
    {
        var repCount = activeRepCount ?? reps?.Count ?? 0;
        var quality = PermanentFileQualityCalculator.Compute(file, repCount);

        return new PermanentFileDto
        {
        Id = file.Id,
        FirmClientAssignmentId = file.FirmClientAssignmentId,
        CompanyTenantId = file.CompanyTenantId,
        CompanyName = file.CompanyName,
        Status = (int)file.Status,
        StatusDisplay = file.Status switch
        {
            PermanentFileStatus.Complete => "Complet",
            PermanentFileStatus.InProgress => "En cours",
            PermanentFileStatus.Archived => "Archivé",
            _ => "Brouillon"
        },
        WizardStep = file.WizardStep,
        Nif = file.Nif,
        RneIdentifier = file.RneIdentifier,
        LegalForm = file.LegalForm.HasValue ? (int)file.LegalForm.Value : null,
        LegalFormDisplay = file.LegalForm?.ToDisplayString(),
        IncorporationDate = file.IncorporationDate,
        ShareCapital = file.ShareCapital,
        Street = file.Street,
        City = file.City,
        Governorate = file.Governorate,
        PostalCode = file.PostalCode,
        TaxOffice = file.TaxOffice,
        TaxRegime = file.TaxRegime.HasValue ? (int)file.TaxRegime.Value : null,
        HasTaxCertificate = file.HasTaxCertificate,
        FiscalYearStartMonth = file.FiscalYearStartMonth,
        FiscalYearEndMonth = file.FiscalYearEndMonth,
        CurrentLegalAct = file.CurrentLegalAct,
        MissionStatus = file.MissionStatus,
        IsDigitized = file.IsDigitized,
        MissionResigned = file.MissionResigned,
        ResignationFiscalYear = file.ResignationFiscalYear,
        ResignationNotes = file.ResignationNotes,
        LabCompleted = file.LabCompleted,
        MissionAccepted = file.MissionAccepted,
        LabCompletedAt = file.LabCompletedAt,
        MissionAcceptedAt = file.MissionAcceptedAt,
        AnnualFeeAmount = file.AnnualFeeAmount,
        BillingFrequency = file.BillingFrequency.HasValue ? (int)file.BillingFrequency.Value : null,
        BillingFrequencyDisplay = file.BillingFrequency switch
        {
            Domain.Enums.BillingFrequency.Monthly => "Mensuel",
            Domain.Enums.BillingFrequency.Quarterly => "Trimestriel",
            Domain.Enums.BillingFrequency.Annual => "Annuel",
            Domain.Enums.BillingFrequency.OneOff => "Ponctuel",
            _ => null
        },
        Currency = file.Currency,
        BillingNotes = file.BillingNotes,
        AssignedAccountantUserId = file.AssignedAccountantUserId,
        AssignedAccountantName = file.AssignedAccountantName,
        SyncedToTenantAt = file.SyncedToTenantAt,
        IsBusinessComplete = quality.IsBusinessComplete,
        MissingItems = quality.MissingItems,
        CompletionPercent = quality.CompletionPercent,
        NextRecommendedStep = quality.NextRecommendedStep,
        NextActionLabel = quality.NextActionLabel,
        Representatives = (reps ?? []).Select(MapRepresentative).ToList(),
        Shareholders = (shareholders ?? []).Select(s => MapShareholder(s)).ToList()
        };
    }

    private async Task<IReadOnlyList<string>> BuildShareholderWarningsAsync(Guid permanentFileId, CancellationToken cancellationToken)
    {
        var active = await _master.Shareholders.AsNoTracking()
            .Where(s => s.PermanentFileId == permanentFileId && s.IsActive)
            .ToListAsync(cancellationToken);
        if (active.Count == 0)
            return [];

        var total = active.Sum(s => s.SharePercentage);
        if (Math.Abs(total - 100m) > 0.01m)
            return [$"La somme des parts actives est {total:0.##}% (attendu ≈ 100%)."];
        return [];
    }

    private static LegalRepresentativeDto MapRepresentative(LegalRepresentative r) => new()
    {
        Id = r.Id,
        LastName = r.LastName,
        FirstName = r.FirstName,
        Cin = r.Cin,
        Nationality = r.Nationality,
        Email = r.Email,
        Phone = r.Phone,
        CnssNumber = r.CnssNumber,
        Role = r.Role,
        IsActive = r.IsActive,
        HasProSpace = r.HasProSpace
    };

    private static ShareholderDto MapShareholder(Shareholder s, IReadOnlyList<string>? warnings = null) => new()
    {
        Id = s.Id,
        Name = s.Name,
        IsLegalEntity = s.IsLegalEntity,
        CinOrNif = s.CinOrNif,
        ShareCount = s.ShareCount,
        SharePercentage = s.SharePercentage,
        IsActive = s.IsActive,
        Warnings = warnings ?? Array.Empty<string>()
    };

    private static FirmTimeSheetEntryDto MapTimeSheet(FirmTimeSheetEntry t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        UserDisplayName = t.UserDisplayName,
        FirmClientAssignmentId = t.FirmClientAssignmentId,
        ClientCompanyName = t.ClientCompanyName,
        WorkDate = t.WorkDate,
        Hours = t.Hours,
        StartTime = t.StartTime.HasValue ? FormatTimeOfDay(t.StartTime) : null,
        EndTime = t.EndTime.HasValue ? FormatTimeOfDay(t.EndTime) : null,
        ActivityCode = t.ActivityCode,
        Notes = t.Notes,
        IsBillable = t.IsBillable,
        WorkLocation = t.WorkLocation,
        Tags = t.Tags,
        Status = (int)t.Status,
        StatusDisplay = StatusDisplayOf(t.Status),
        IsValidated = t.IsValidated,
        TimerStartedAtUtc = t.TimerStartedAtUtc,
        ValidatedAt = t.ValidatedAt,
        ValidatedByDisplayName = t.ValidatedByDisplayName
    };

    private static FirmTimeSheetPeriodDto MapPeriod(FirmTimeSheetPeriodLock p) => new()
    {
        Year = p.Year,
        Month = p.Month,
        IsLocked = p.IsLocked,
        LockedAt = p.LockedAt,
        LockedByDisplayName = p.LockedByDisplayName,
        LockReason = p.LockReason,
        UnlockedAt = p.UnlockedAt,
        UnlockedByDisplayName = p.UnlockedByDisplayName,
        UnlockReason = p.UnlockReason
    };

    private static FirmTimeSheetYearSettingsDto MapYearSettings(FirmTimeSheetYearSettings s) => new()
    {
        Year = s.Year,
        WeeklyRegime = (int)s.WeeklyRegime,
        WeeklyRegimeDisplay = s.WeeklyRegime.ToDisplayString(),
        MaxDailyHours = s.MaxDailyHours,
        MaxWeeklyHours = s.MaxWeeklyHours,
        AllowFutureEntryDays = s.AllowFutureEntryDays,
        MaxBackdatingDays = s.MaxBackdatingDays,
        EnforceHardLimits = s.EnforceHardLimits,
        PaidLeaveDaysPerYear = s.PaidLeaveDaysPerYear,
        PublicHolidayDaysPerYear = s.PublicHolidayDaysPerYear,
        ProductivityRatePercent = s.ProductivityRatePercent,
        CnssEmployerRate = s.CnssEmployerRate,
        TfpRate = s.TfpRate,
        FoprolosRate = s.FoprolosRate,
        WorkAccidentRate = s.WorkAccidentRate,
        AnnualBaseHours = s.AnnualBaseHours,
        DailyHours = s.DailyHours,
        AnnualProductiveHours = s.AnnualProductiveHours,
        TotalEmployerChargeRate = s.TotalEmployerChargeRate
    };

    private static FirmExpenseNoteDto MapExpenseNote(FirmExpenseNote n) => new()
    {
        Id = n.Id,
        FirmClientAssignmentId = n.FirmClientAssignmentId,
        CompanyName = n.CompanyName,
        PeriodYear = n.PeriodYear,
        PeriodMonth = n.PeriodMonth,
        Status = (int)n.Status,
        StatusDisplay = n.Status switch
        {
            FirmExpenseNoteStatus.Submitted => "Soumise",
            FirmExpenseNoteStatus.Approved => "Approuvée",
            FirmExpenseNoteStatus.Rejected => "Rejetée",
            FirmExpenseNoteStatus.Reimbursed => "Remboursée",
            _ => "Brouillon"
        },
        TotalToReimburse = n.TotalToReimburse,
        MixedCharges = n.MixedCharges,
        OperatingExpenses = n.OperatingExpenses,
        MileageAllowance = n.MileageAllowance,
        SalesAmount = n.SalesAmount,
        Notes = n.Notes
    };

    private static TenantDbContext CreateTenantContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TenantDbContext(options);
    }

    private async Task<CompanyProfileSnapshotDto?> ResolveCompanyProfileAsync(
        FirmClientAssignment assignment,
        CancellationToken cancellationToken)
    {
        var fromAssignment = _companyProfileSnapshot.TryDeserialize(assignment.CompanyProfileSnapshotJson);
        if (fromAssignment is not null)
            return fromAssignment;

        try
        {
            return await _companyProfileSnapshot.CaptureForCompanyTenantAsync(assignment.CompanyTenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback live du profil société impossible pour {TenantId}", assignment.CompanyTenantId);
            return null;
        }
    }
}
