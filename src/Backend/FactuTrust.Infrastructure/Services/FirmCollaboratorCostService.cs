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
    private readonly FirmGovernanceOptions _options;

    public FirmCollaboratorCostService(
        MasterDbContext master,
        IFirmPayrollCostProvider payrollCosts,
        IOptions<FirmGovernanceOptions> options)
    {
        _master = master;
        _payrollCosts = payrollCosts;
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
            .Select(p => new { p.UserId, p.HourlyCostRate, p.PayrollEmployeeId })
            .ToListAsync(cancellationToken);

        var settings = await ResolveSettingsAsync(firmTenantId, year, cancellationToken);
        var defaultRate = ResolveDefaultRate();

        return users.Select(u =>
        {
            var cost = costs.FirstOrDefault(c => c.CollaboratorUserId == u.Id);
            var profile = profiles.FirstOrDefault(p => p.UserId == u.Id);
            var resolution = HourlyCostRateCalculator.Resolve(
                cost, settings, profile?.HourlyCostRate, defaultRate);

            var displayName = $"{u.FirstName} {u.LastName}".Trim();
            return new FirmCollaboratorYearCostDto
            {
                CollaboratorUserId = u.Id,
                CollaboratorName = string.IsNullOrEmpty(displayName) ? u.Email ?? "Collaborateur" : displayName,
                Year = year,
                GrossAnnualSalary = cost?.GrossAnnualSalary ?? 0m,
                EmployerContributions = cost?.EmployerContributions ?? 0m,
                PayrollExtras = cost?.PayrollExtras ?? 0m,
                TotalEmployerCost = cost?.TotalEmployerCost ?? 0m,
                Source = (int)(cost?.Source ?? FirmPayrollCostSource.Manual),
                SourceDisplay = DescribeCostSource(cost?.Source ?? FirmPayrollCostSource.Manual),
                ImportedAt = cost?.ImportedAt,
                HourlyRateOverride = cost?.HourlyRateOverride,
                OverrideJustification = cost?.OverrideJustification,
                EffectiveHourlyRate = resolution.Rate,
                HourlyRateSource = (int)resolution.Source,
                HourlyRateSourceDisplay = DescribeRateSource(resolution.Source),
                HourlyRateBasis = resolution.Basis,
                AnnualProductiveHours = settings.AnnualProductiveHours,
                PayrollEmployeeId = profile?.PayrollEmployeeId
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
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmPayrollImportResultDto>(
                Error.Forbidden("Seul un manager peut importer les coûts de paie."));

        var snapshot = await _payrollCosts.GetAnnualEmployerCostsAsync(firmTenantId, year, cancellationToken);
        if (!snapshot.IsAvailable)
        {
            // Absence de paie exploitable : ce n'est pas une erreur, le cabinet saisit à la main.
            return Result.Success(new FirmPayrollImportResultDto
            {
                PayrollAvailable = false,
                UnavailableReason = snapshot.UnavailableReason
            });
        }

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
        foreach (var profile in linkedProfiles)
        {
            var match = snapshot.Employees.FirstOrDefault(e => e.PayrollEmployeeId == profile.PayrollEmployeeId);
            if (match is null)
                continue;

            var entity = existing.FirstOrDefault(c => c.CollaboratorUserId == profile.UserId);
            if (entity is null)
            {
                var create = FirmCollaboratorYearCost.Create(firmTenantId, profile.UserId, year);
                if (create.IsFailure)
                    continue;
                entity = create.Value;
                _master.FirmCollaboratorYearCosts.Add(entity);
                existing.Add(entity);
            }

            // Les extras saisis sont préservés : l'import ne connaît que ce qui figure au bulletin.
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
            Unlinked = Math.Max(0, totalCollaborators - linkedProfiles.Count)
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
        profile.UpdatedAt = DateTime.UtcNow;
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
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

    private static string DescribeCostSource(FirmPayrollCostSource source) => source switch
    {
        FirmPayrollCostSource.ImportedFromPayroll => "Importé de la paie",
        _ => "Saisi"
    };

    private static string DescribeRateSource(FirmHourlyRateSource source) => source switch
    {
        FirmHourlyRateSource.Derived => "Calculé",
        FirmHourlyRateSource.Override => "Imposé",
        FirmHourlyRateSource.LegacyProfile => "Profil",
        _ => "Défaut cabinet"
    };
}
