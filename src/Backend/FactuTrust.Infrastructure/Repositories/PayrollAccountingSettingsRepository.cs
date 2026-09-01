using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// <see cref="IPayrollAccountingSettingsRepository"/> sur <c>TenantDbContext</c>. Singleton par
/// tenant : la table porte au plus une ligne. La lecture ne persiste rien et retourne <c>null</c>
/// si le dossier n'a jamais été paramétré — le résolveur retombe alors sur la configuration globale,
/// ce qui préserve à l'identique le comportement des dossiers existants.
/// </summary>
public sealed class PayrollAccountingSettingsRepository : IPayrollAccountingSettingsRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollAccountingSettingsRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollAccountingSettings?> GetForTenantAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollAccountingSettings
            .AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<PayrollAccountingSettings>> UpsertAsync(
        PayrollAccountProfile accountProfile,
        DateTime? accountProfileEffectiveDate,
        string? inKindOffsetAccount,
        bool disbursementEntriesEnabled,
        bool detailedSalarySplitEnabled,
        bool employeeAuxiliaryEnabled,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var existing = await context.PayrollAccountingSettings
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var update = existing.Update(
                accountProfile, accountProfileEffectiveDate, inKindOffsetAccount,
                disbursementEntriesEnabled, detailedSalarySplitEnabled, employeeAuxiliaryEnabled);
            if (update.IsFailure)
                return Result.Failure<PayrollAccountingSettings>(update.Error);

            existing.SetAuditInfo(updatedBy, isUpdate: true);
        }
        else
        {
            var created = PayrollAccountingSettings.Create(
                accountProfile, accountProfileEffectiveDate, inKindOffsetAccount,
                disbursementEntriesEnabled, detailedSalarySplitEnabled, employeeAuxiliaryEnabled);
            if (created.IsFailure)
                return Result.Failure<PayrollAccountingSettings>(created.Error);

            created.Value.SetAuditInfo(updatedBy, isUpdate: false);
            context.PayrollAccountingSettings.Add(created.Value);
            existing = created.Value;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(existing);
    }
}
