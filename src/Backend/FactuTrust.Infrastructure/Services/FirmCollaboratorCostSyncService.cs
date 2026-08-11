using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IFirmCollaboratorCostSyncService"/>
public sealed class FirmCollaboratorCostSyncService : IFirmCollaboratorCostSyncService
{
    private readonly MasterDbContext _master;
    private readonly IFirmCollaboratorCostService _costs;
    private readonly IFirmPayrollCostProvider _payrollCosts;
    private readonly FirmGovernanceOptions _options;
    private readonly ILogger<FirmCollaboratorCostSyncService> _logger;

    public FirmCollaboratorCostSyncService(
        MasterDbContext master,
        IFirmCollaboratorCostService costs,
        IFirmPayrollCostProvider payrollCosts,
        IOptions<FirmGovernanceOptions> options,
        ILogger<FirmCollaboratorCostSyncService> logger)
    {
        _master = master;
        _costs = costs;
        _payrollCosts = payrollCosts;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FirmCollaboratorCostSyncResultDto> EnsureFreshAsync(
        Guid firmTenantId,
        int year,
        FirmCostSyncTrigger trigger,
        bool forceImport = false,
        CancellationToken cancellationToken = default)
    {
        var syncedAt = DateTime.UtcNow;

        if (!await IsAccountingFirmAsync(firmTenantId, cancellationToken))
        {
            return new FirmCollaboratorCostSyncResultDto
            {
                PayrollAvailable = false,
                UnavailableReason = "Le tenant n'est pas un cabinet comptable.",
                SyncedAt = syncedAt
            };
        }

        // L'auto-liaison passe avant la lecture des bulletins : elle ne dépend que des salariés
        // actifs. La placer après rendait la mise en service impossible — sans liaison aucun
        // bulletin ne pouvait être rattaché, et sans bulletin la liaison n'était jamais tentée.
        var linkedByEmail = 0;
        if (ShouldAutoLink(trigger))
            linkedByEmail = await AutoLinkByEmailAsync(firmTenantId, cancellationToken);

        var snapshot = await _payrollCosts.GetAnnualEmployerCostsAsync(firmTenantId, year, cancellationToken);
        if (!snapshot.IsAvailable)
        {
            return new FirmCollaboratorCostSyncResultDto
            {
                PayrollAvailable = false,
                UnavailableReason = snapshot.UnavailableReason,
                LinkedByEmail = linkedByEmail,
                SyncedAt = syncedAt
            };
        }

        var staleOrMissingOnly = trigger == FirmCostSyncTrigger.RentabilityPrefill && !forceImport;
        var importResult = await _costs.ImportFromPayrollAsync(
            firmTenantId,
            isManager: true,
            year,
            forceOverwriteManual: forceImport,
            staleOrMissingOnly: staleOrMissingOnly,
            cancellationToken: cancellationToken);

        if (importResult.IsFailure)
        {
            _logger.LogWarning(
                "Synchronisation des coûts collaborateurs échouée pour {TenantId} ({Year}) : {Error}",
                firmTenantId,
                year,
                importResult.Error.Description);
            return new FirmCollaboratorCostSyncResultDto
            {
                PayrollAvailable = true,
                LinkedByEmail = linkedByEmail,
                UnavailableReason = importResult.Error.Description,
                SyncedAt = syncedAt
            };
        }

        return new FirmCollaboratorCostSyncResultDto
        {
            PayrollAvailable = true,
            LinkedByEmail = linkedByEmail,
            Imported = importResult.Value.Imported,
            SkippedManual = importResult.Value.SkippedManual,
            SkippedUnlinked = importResult.Value.Unlinked,
            SkippedUpToDate = importResult.Value.SkippedUpToDate,
            SyncedAt = syncedAt
        };
    }

    private bool ShouldAutoLink(FirmCostSyncTrigger trigger) =>
        _options.AutoLinkCollaboratorsByEmail;

    private async Task<bool> IsAccountingFirmAsync(Guid firmTenantId, CancellationToken cancellationToken)
    {
        var kind = await _master.Tenants.AsNoTracking()
            .Where(t => t.Id == firmTenantId)
            .Select(t => t.Kind)
            .FirstOrDefaultAsync(cancellationToken);
        return kind == TenantKind.AccountingFirm;
    }

    private async Task<int> AutoLinkByEmailAsync(Guid firmTenantId, CancellationToken cancellationToken)
    {
        var payrollEmployees = await _payrollCosts.GetActiveEmployeesForLinkingAsync(firmTenantId, cancellationToken);
        if (payrollEmployees.Count == 0)
            return 0;

        var employeesByEmail = payrollEmployees
            .Select(e => new { Employee = e, Normalized = NormalizeEmail(e.Email) })
            .Where(x => x.Normalized is not null)
            .GroupBy(x => x.Normalized!)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Employee);

        var collaborators = await (
            from u in _master.Users.AsNoTracking()
            join p in _master.FirmCollaboratorProfiles on u.Id equals p.UserId into profiles
            from p in profiles.DefaultIfEmpty()
            where u.TenantId == firmTenantId && u.IsActive
            select new
            {
                u.Id,
                u.Email,
                Profile = p
            }).ToListAsync(cancellationToken);

        var alreadyLinkedEmployeeIds = await (
            from p in _master.FirmCollaboratorProfiles.AsNoTracking()
            join u in _master.Users.AsNoTracking() on p.UserId equals u.Id
            where u.TenantId == firmTenantId && p.PayrollEmployeeId != null
            select p.PayrollEmployeeId!.Value).ToListAsync(cancellationToken);

        var linked = new HashSet<Guid>(alreadyLinkedEmployeeIds);
        var count = 0;

        foreach (var c in collaborators)
        {
            if (c.Profile?.PayrollEmployeeId is not null)
                continue;

            var normalized = NormalizeEmail(c.Email);
            if (normalized is null || !employeesByEmail.TryGetValue(normalized, out var employee))
                continue;

            if (linked.Contains(employee.EmployeeId))
                continue;

            var profile = c.Profile;
            if (profile is null)
            {
                profile = new FirmCollaboratorProfile
                {
                    UserId = c.Id,
                    Qualification = string.Empty
                };
                _master.FirmCollaboratorProfiles.Add(profile);
            }

            profile.PayrollEmployeeId = employee.EmployeeId;
            profile.PayrollLinkSource = FirmPayrollLinkSource.AutoEmail;
            profile.PayrollLinkedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
            linked.Add(employee.EmployeeId);
            count++;
        }

        if (count > 0)
            await _master.SaveChangesAsync(cancellationToken);

        return count;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return email.Trim().ToLowerInvariant();
    }
}
