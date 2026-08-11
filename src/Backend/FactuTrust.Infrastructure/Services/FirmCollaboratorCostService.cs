using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IFirmCollaboratorCostService"/>
public sealed class FirmCollaboratorCostService : IFirmCollaboratorCostService
{
    private readonly MasterDbContext _master;
    private readonly IFirmPayrollCostProvider _payrollCosts;
    private readonly IFirmLeaveAbsenceReader _absences;
    private readonly FirmGovernanceOptions _options;

    public FirmCollaboratorCostService(
        MasterDbContext master,
        IFirmPayrollCostProvider payrollCosts,
        IFirmLeaveAbsenceReader absences,
        IOptions<FirmGovernanceOptions> options)
    {
        _master = master;
        _payrollCosts = payrollCosts;
        _absences = absences;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<FirmCollaboratorYearCostDto>> ListAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var users = await _master.Users.AsNoTracking()
            .Where(u => u.TenantId == firmTenantId && u.IsActive)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
            return [];

        var userIds = users.Select(u => u.Id).ToList();
        var costs = await _master.FirmCollaboratorYearCosts.AsNoTracking()
            .Where(c => c.FirmTenantId == firmTenantId && c.Year == year && userIds.Contains(c.CollaboratorUserId))
            .ToListAsync(cancellationToken);
        var profiles = await _master.FirmCollaboratorProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                p.HourlyCostRate,
                p.PayrollEmployeeId,
                p.PayrollLinkSource,
                p.PayrollLinkedAt,
                p.HiredOn,
                p.LeftOn
            })
            .ToListAsync(cancellationToken);

        var payrollSnapshot = await _payrollCosts.GetAnnualEmployerCostsAsync(firmTenantId, year, cancellationToken);
        var payrollById = payrollSnapshot.IsAvailable
            ? payrollSnapshot.Employees.ToDictionary(e => e.PayrollEmployeeId)
            : new Dictionary<Guid, FirmPayrollEmployeeCostDto>();

        var settings = await ResolveSettingsAsync(firmTenantId, year, cancellationToken);
        var defaultRate = ResolveDefaultRate();

        // Une seule lecture des congés pour tout l'écran, même quand le mode est paramétrique :
        // l'écart entre congés réels et jours paramétrés est affiché dans les deux cas.
        var absenceDays = await _absences.GetApprovedAbsenceDaysByUserAsync(firmTenantId, year, cancellationToken);

        return users.Select(u =>
        {
            var cost = costs.FirstOrDefault(c => c.CollaboratorUserId == u.Id);
            var profile = profiles.FirstOrDefault(p => p.UserId == u.Id);

            absenceDays.TryGetValue(u.Id, out var realAbsenceDays);
            var presenceRatio = CollaboratorProductiveHoursCalculator.ComputePresenceRatio(
                year, profile?.HiredOn, profile?.LeftOn);
            var productiveHours = CollaboratorProductiveHoursCalculator.Resolve(
                settings, realAbsenceDays, presenceRatio);

            var resolution = HourlyCostRateCalculator.Resolve(
                cost, productiveHours.Hours, profile?.HourlyCostRate, defaultRate);

            var displayName = $"{u.FirstName} {u.LastName}".Trim();
            FirmPayrollEmployeeCostDto? payrollMatch = null;
            if (profile?.PayrollEmployeeId is Guid payrollId)
                payrollById.TryGetValue(payrollId, out payrollMatch);

            var diagnostic = DiagnoseCost(
                cost, profile?.PayrollEmployeeId, payrollMatch, payrollSnapshot);

            return new FirmCollaboratorYearCostDto
            {
                CollaboratorUserId = u.Id,
                CollaboratorName = string.IsNullOrEmpty(displayName) ? u.Email ?? "Collaborateur" : displayName,
                Year = year,
                GrossAnnualSalary = cost?.GrossAnnualSalary ?? 0m,
                EmployerContributions = cost?.EmployerContributions ?? 0m,
                PayrollExtras = cost?.PayrollExtras ?? 0m,
                TotalEmployerCost = cost?.TotalEmployerCost ?? 0m,
                Source = (int)(cost?.Source ?? FirmPayrollCostSource.None),
                SourceDisplay = DescribeCostSource(cost?.Source ?? FirmPayrollCostSource.None),
                ImportedAt = cost?.ImportedAt,
                CostDiagnostic = (int)diagnostic,
                CostDiagnosticDisplay = DescribeDiagnostic(diagnostic),
                CostDiagnosticHint = DescribeDiagnosticHint(diagnostic, payrollSnapshot),
                HourlyRateOverride = cost?.HourlyRateOverride,
                OverrideJustification = cost?.OverrideJustification,
                EffectiveHourlyRate = resolution.Rate,
                HourlyRateSource = (int)resolution.Source,
                HourlyRateSourceDisplay = DescribeRateSource(resolution.Source),
                HourlyRateBasis = resolution.Basis,
                AnnualProductiveHours = productiveHours.Hours,
                ProductiveHoursMode = (int)productiveHours.Mode,
                ProductiveHoursBasis = productiveHours.Basis,
                RealAbsenceDays = realAbsenceDays,
                ParametricLeaveDays = settings.PaidLeaveDaysPerYear,
                PayrollEmployeeId = profile?.PayrollEmployeeId,
                PayrollEmployeeName = payrollMatch?.EmployeeName,
                PayrollLinkSource = (int)(profile?.PayrollLinkSource ?? FirmPayrollLinkSource.None),
                PayrollLinkSourceDisplay = DescribeLinkSource(profile?.PayrollLinkSource ?? FirmPayrollLinkSource.None),
                PayrollLinkedAt = profile?.PayrollLinkedAt,
                PayslipCount = payrollMatch?.PayslipCount ?? 0
            };
        }).ToList();
    }

    public async Task<Result<FirmCollaboratorYearCostDto>> SaveAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        int year,
        SaveFirmCollaboratorYearCostDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmCollaboratorYearCostDto>(
                Error.Forbidden("Seul un manager peut modifier le coût d'un collaborateur."));

        var user = await _master.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == collaboratorUserId && u.TenantId == firmTenantId, cancellationToken);
        if (user is null)
            return Result.Failure<FirmCollaboratorYearCostDto>(Error.NotFound("User", collaboratorUserId));

        var entity = await _master.FirmCollaboratorYearCosts
            .FirstOrDefaultAsync(
                c => c.FirmTenantId == firmTenantId
                     && c.CollaboratorUserId == collaboratorUserId
                     && c.Year == year, cancellationToken);

        if (entity is null)
        {
            var create = FirmCollaboratorYearCost.Create(firmTenantId, collaboratorUserId, year);
            if (create.IsFailure)
                return Result.Failure<FirmCollaboratorYearCostDto>(create.Error);
            entity = create.Value;
            _master.FirmCollaboratorYearCosts.Add(entity);
        }

        var settings = await ResolveSettingsAsync(firmTenantId, year, cancellationToken);

        // Charges patronales non fournies : on les dérive des taux de l'exercice plutôt que de
        // laisser un zéro qui sous-estimerait le coût de revient.
        var contributions = dto.EmployerContributions
                            ?? FirmCollaboratorYearCost.ComputeEmployerContributions(
                                dto.GrossAnnualSalary, settings.TotalEmployerChargeRate);

        var applied = entity.SetManualCost(dto.GrossAnnualSalary, contributions, dto.PayrollExtras);
        if (applied.IsFailure)
            return Result.Failure<FirmCollaboratorYearCostDto>(applied.Error);

        var overrideApplied = entity.SetHourlyRateOverride(dto.HourlyRateOverride, dto.OverrideJustification);
        if (overrideApplied.IsFailure)
            return Result.Failure<FirmCollaboratorYearCostDto>(overrideApplied.Error);

        await _master.SaveChangesAsync(cancellationToken);

        var refreshed = await ListAsync(firmTenantId, year, cancellationToken);
        var row = refreshed.FirstOrDefault(r => r.CollaboratorUserId == collaboratorUserId);
        return row is null
            ? Result.Failure<FirmCollaboratorYearCostDto>(Error.NotFound("CollaboratorCost", collaboratorUserId))
            : Result.Success(row);
    }

    public async Task<Result<FirmPayrollImportResultDto>> ImportFromPayrollAsync(
        Guid firmTenantId,
        bool isManager,
        int year,
        bool forceOverwriteManual = false,
        bool staleOrMissingOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmPayrollImportResultDto>(
                Error.Forbidden("Seul un manager peut importer les coûts de paie."));

        var snapshot = await _payrollCosts.GetAnnualEmployerCostsAsync(firmTenantId, year, cancellationToken);
        if (!snapshot.IsAvailable)
        {
            return Result.Success(new FirmPayrollImportResultDto
            {
                PayrollAvailable = false,
                UnavailableReason = snapshot.UnavailableReason
            });
        }

        var latestActivity = await _payrollCosts.GetLatestPayrollActivityAtAsync(firmTenantId, year, cancellationToken);

        var linkedProfiles = await (
            from p in _master.FirmCollaboratorProfiles
            join u in _master.Users on p.UserId equals u.Id
            where u.TenantId == firmTenantId && p.PayrollEmployeeId != null
            select p).ToListAsync(cancellationToken);

        var totalCollaborators = await _master.Users.AsNoTracking()
            .CountAsync(u => u.TenantId == firmTenantId && u.IsActive, cancellationToken);

        var existing = await _master.FirmCollaboratorYearCosts
            .Where(c => c.FirmTenantId == firmTenantId && c.Year == year)
            .ToListAsync(cancellationToken);

        var imported = 0;
        var skippedManual = 0;
        var skippedUpToDate = 0;

        foreach (var profile in linkedProfiles)
        {
            var match = snapshot.Employees.FirstOrDefault(e => e.PayrollEmployeeId == profile.PayrollEmployeeId);
            if (match is null)
                continue;

            var entity = existing.FirstOrDefault(c => c.CollaboratorUserId == profile.UserId);

            if (entity?.Source == FirmPayrollCostSource.Manual && !forceOverwriteManual)
            {
                skippedManual++;
                continue;
            }

            if (staleOrMissingOnly
                && entity?.Source == FirmPayrollCostSource.ImportedFromPayroll
                && entity.ImportedAt.HasValue
                && latestActivity.HasValue
                && entity.ImportedAt.Value >= latestActivity.Value)
            {
                skippedUpToDate++;
                continue;
            }

            if (entity is null)
            {
                var create = FirmCollaboratorYearCost.Create(firmTenantId, profile.UserId, year);
                if (create.IsFailure)
                    continue;
                entity = create.Value;
                _master.FirmCollaboratorYearCosts.Add(entity);
                existing.Add(entity);
            }

            var applied = entity.SetImportedCost(
                match.GrossAnnualSalary, match.EmployerContributions, entity.PayrollExtras);
            if (applied.IsSuccess)
                imported++;
        }

        await _master.SaveChangesAsync(cancellationToken);

        return Result.Success(new FirmPayrollImportResultDto
        {
            PayrollAvailable = true,
            Imported = imported,
            Unlinked = Math.Max(0, totalCollaborators - linkedProfiles.Count),
            SkippedManual = skippedManual,
            SkippedUpToDate = skippedUpToDate
        });
    }

    public Task<FirmPayrollCostSnapshotDto> GetPayrollEmployeesAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default) =>
        _payrollCosts.GetAnnualEmployerCostsAsync(firmTenantId, year, cancellationToken);

    public async Task<Result> LinkPayrollEmployeeAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        Guid? payrollEmployeeId,
        FirmPayrollLinkSource linkSourceWhenSet = FirmPayrollLinkSource.Manual,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure(Error.Forbidden("Seul un manager peut lier un collaborateur à la paie."));

        var user = await _master.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == collaboratorUserId && u.TenantId == firmTenantId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User", collaboratorUserId));

        // Un même salarié ne peut alimenter qu'un seul collaborateur, sinon son coût serait
        // compté plusieurs fois dans la rentabilité du cabinet.
        if (payrollEmployeeId.HasValue)
        {
            var alreadyLinked = await (
                from p in _master.FirmCollaboratorProfiles.AsNoTracking()
                join u in _master.Users.AsNoTracking() on p.UserId equals u.Id
                where u.TenantId == firmTenantId
                      && p.PayrollEmployeeId == payrollEmployeeId.Value
                      && p.UserId != collaboratorUserId
                select p.UserId).AnyAsync(cancellationToken);
            if (alreadyLinked)
                return Result.Failure(Error.Conflict(
                    "Ce salarié est déjà lié à un autre collaborateur : son coût serait compté deux fois."));
        }

        var profile = await _master.FirmCollaboratorProfiles
            .FirstOrDefaultAsync(p => p.UserId == collaboratorUserId, cancellationToken);
        if (profile is null)
        {
            profile = new FirmCollaboratorProfile { UserId = collaboratorUserId, Qualification = string.Empty };
            _master.FirmCollaboratorProfiles.Add(profile);
        }

        profile.PayrollEmployeeId = payrollEmployeeId;
        profile.PayrollLinkSource = payrollEmployeeId.HasValue
            ? linkSourceWhenSet
            : FirmPayrollLinkSource.None;
        profile.PayrollLinkedAt = payrollEmployeeId.HasValue ? DateTime.UtcNow : null;
        profile.UpdatedAt = DateTime.UtcNow;
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<FirmPayrollLinkBatchResultDto>> LinkPayrollEmployeesAsync(
        Guid firmTenantId,
        bool isManager,
        IReadOnlyList<FirmPayrollEmployeeLinkRequest> links,
        FirmPayrollLinkSource linkSource,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmPayrollLinkBatchResultDto>(
                Error.Forbidden("Seul un manager peut lier un collaborateur à la paie."));

        if (links.Count == 0)
            return Result.Success(new FirmPayrollLinkBatchResultDto());

        var requestedUserIds = links.Select(l => l.CollaboratorUserId).ToList();
        var knownUserIds = await _master.Users.AsNoTracking()
            .Where(u => u.TenantId == firmTenantId && requestedUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var knownUsers = new HashSet<Guid>(knownUserIds);

        // Salariés déjà rattachés à un autre collaborateur : les lier de nouveau ferait compter
        // leur coût deux fois dans la rentabilité du cabinet.
        var alreadyLinked = await (
            from p in _master.FirmCollaboratorProfiles.AsNoTracking()
            join u in _master.Users.AsNoTracking() on p.UserId equals u.Id
            where u.TenantId == firmTenantId && p.PayrollEmployeeId != null
            select new { p.UserId, EmployeeId = p.PayrollEmployeeId!.Value })
            .ToListAsync(cancellationToken);
        var takenEmployees = alreadyLinked
            .ToDictionary(x => x.EmployeeId, x => x.UserId);

        var profiles = await _master.FirmCollaboratorProfiles
            .Where(p => requestedUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var messages = new List<string>();
        var linked = 0;
        var now = DateTime.UtcNow;

        foreach (var link in links)
        {
            if (!knownUsers.Contains(link.CollaboratorUserId))
            {
                messages.Add($"Collaborateur {link.CollaboratorUserId} introuvable dans le cabinet.");
                continue;
            }

            if (takenEmployees.TryGetValue(link.PayrollEmployeeId, out var holder)
                && holder != link.CollaboratorUserId)
            {
                messages.Add("Un salarié paie est déjà lié à un autre collaborateur : liaison ignorée.");
                continue;
            }

            var profile = profiles.FirstOrDefault(p => p.UserId == link.CollaboratorUserId);
            if (profile is null)
            {
                profile = new FirmCollaboratorProfile
                {
                    UserId = link.CollaboratorUserId,
                    Qualification = string.Empty
                };
                _master.FirmCollaboratorProfiles.Add(profile);
                profiles.Add(profile);
            }

            profile.PayrollEmployeeId = link.PayrollEmployeeId;
            profile.PayrollLinkSource = linkSource;
            profile.PayrollLinkedAt = now;
            profile.UpdatedAt = now;

            takenEmployees[link.PayrollEmployeeId] = link.CollaboratorUserId;
            linked++;
        }

        // Une seule écriture : soit toutes les liaisons retenues sont posées, soit aucune.
        await _master.SaveChangesAsync(cancellationToken);

        return Result.Success(new FirmPayrollLinkBatchResultDto
        {
            Linked = linked,
            Messages = messages
        });
    }

    private decimal ResolveDefaultRate() =>
        _options.DefaultHourlyCostRate <= 0 ? 50m : _options.DefaultHourlyCostRate;

    /// <summary>Paramètres de l'exercice, repliés sur les défauts tunisiens sans écriture en base.</summary>
    private async Task<FirmTimeSheetYearSettings> ResolveSettingsAsync(
        Guid firmTenantId, int year, CancellationToken cancellationToken)
    {
        var persisted = await _master.FirmTimeSheetYearSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FirmTenantId == firmTenantId && s.Year == year, cancellationToken);
        return persisted ?? FirmTimeSheetYearSettings.Create(firmTenantId, year).Value;
    }

    private static string DescribeLinkSource(FirmPayrollLinkSource source) => source switch
    {
        FirmPayrollLinkSource.Manual => "Manuelle",
        FirmPayrollLinkSource.AutoEmail => "Auto (email)",
        FirmPayrollLinkSource.ProvisionedFromCollaborator => "Provisionné",
        _ => "—"
    };

    private static string DescribeCostSource(FirmPayrollCostSource source) => source switch
    {
        FirmPayrollCostSource.ImportedFromPayroll => "Importé de la paie",
        FirmPayrollCostSource.Manual => "Saisi",
        _ => "Aucune donnée"
    };

    /// <summary>
    /// Explique l'état d'une ligne de coût, du plus spécifique au plus général.
    /// </summary>
    /// <remarks>
    /// L'ordre compte : une ligne saisie reste une ligne saisie même si la paie est illisible, et
    /// l'absence de liaison prime sur l'indisponibilité de la paie — c'est le premier obstacle que
    /// le cabinet doit lever, et le seul qu'il puisse lever sans attendre un cycle de paie.
    /// </remarks>
    private static FirmCollaboratorCostDiagnostic DiagnoseCost(
        FirmCollaboratorYearCost? cost,
        Guid? payrollEmployeeId,
        FirmPayrollEmployeeCostDto? payrollMatch,
        FirmPayrollCostSnapshotDto snapshot)
    {
        if (cost?.Source == FirmPayrollCostSource.Manual)
            return FirmCollaboratorCostDiagnostic.ManualEntry;

        if (cost?.Source == FirmPayrollCostSource.ImportedFromPayroll)
            return FirmCollaboratorCostDiagnostic.Ok;

        if (payrollEmployeeId is null)
            return FirmCollaboratorCostDiagnostic.NoPayrollLink;

        if (!snapshot.IsAvailable)
        {
            // Le fournisseur distingue déjà « aucun cycle arrêté » des autres indisponibilités ;
            // on relaie cette nuance plutôt que de tout ramener à « paie illisible ».
            return snapshot.UnavailableReason?.Contains("validée", StringComparison.OrdinalIgnoreCase) == true
                ? FirmCollaboratorCostDiagnostic.NoValidatedRun
                : FirmCollaboratorCostDiagnostic.PayrollUnreadable;
        }

        return payrollMatch is null
            ? FirmCollaboratorCostDiagnostic.LinkedWithoutPayslip
            : FirmCollaboratorCostDiagnostic.Ok;
    }

    private static string DescribeDiagnostic(FirmCollaboratorCostDiagnostic diagnostic) => diagnostic switch
    {
        FirmCollaboratorCostDiagnostic.ManualEntry => "Coût saisi",
        FirmCollaboratorCostDiagnostic.NoPayrollLink => "Non lié à la paie",
        FirmCollaboratorCostDiagnostic.PayrollUnreadable => "Paie illisible",
        FirmCollaboratorCostDiagnostic.NoValidatedRun => "Aucune paie arrêtée",
        FirmCollaboratorCostDiagnostic.LinkedWithoutPayslip => "Aucun bulletin sur l'exercice",
        _ => "À jour"
    };

    private static string? DescribeDiagnosticHint(
        FirmCollaboratorCostDiagnostic diagnostic,
        FirmPayrollCostSnapshotDto snapshot) => diagnostic switch
    {
        FirmCollaboratorCostDiagnostic.ManualEntry =>
            "L'import de paie ne remplacera pas ce montant sans « Importer (forcer) ».",
        FirmCollaboratorCostDiagnostic.NoPayrollLink =>
            "Rattachez ce collaborateur à un salarié de la paie interne pour alimenter son coût automatiquement.",
        FirmCollaboratorCostDiagnostic.PayrollUnreadable => snapshot.UnavailableReason,
        FirmCollaboratorCostDiagnostic.NoValidatedRun =>
            "Validez ou clôturez un cycle de paie de l'exercice : seuls les bulletins arrêtés alimentent le coût.",
        FirmCollaboratorCostDiagnostic.LinkedWithoutPayslip =>
            "Le salarié lié n'a aucun bulletin sur les cycles arrêtés de l'exercice.",
        _ => null
    };

    private static string DescribeRateSource(FirmHourlyRateSource source) => source switch
    {
        FirmHourlyRateSource.Derived => "Calculé",
        FirmHourlyRateSource.Override => "Imposé",
        FirmHourlyRateSource.LegacyProfile => "Profil",
        _ => "Défaut cabinet"
    };
}
