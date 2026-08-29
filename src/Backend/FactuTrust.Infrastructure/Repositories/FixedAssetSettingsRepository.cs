using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// <see cref="IFixedAssetSettingsRepository"/> sur <c>TenantDbContext</c>. Singleton par tenant :
/// la table <c>FixedAssetSettings</c> porte au plus une ligne par base tenant. La lecture
/// (<see cref="GetForTenantAsync"/>) ne persiste rien et retourne le défaut usine (exercice civil)
/// si aucune ligne n'existe — préservant le comportement historique pour les dossiers non
/// configurés (anti-régression).
/// </summary>
public sealed class FixedAssetSettingsRepository : IFixedAssetSettingsRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public FixedAssetSettingsRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FixedAssetSettings> GetForTenantAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var persisted = await context.FixedAssetSettings
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Défaut usine non persisté : un tenant sans paramétrage conserve l'exercice civil.
        return persisted ?? FixedAssetSettings.CreateDefault();
    }

    public async Task<FixedAssetSettings> UpsertAsync(
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        // Validation domaine avant toute persistance (erreurs stables).
        var probe = FixedAssetSettings.Create(fiscalYearStartMonth, fiscalYearLabelFormat);
        if (probe.IsFailure)
            throw new ArgumentException(probe.Error.Description);

        await using var context = _contextFactory.CreateContext();
        var existing = await context.FixedAssetSettings
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var update = existing.Update(fiscalYearStartMonth, fiscalYearLabelFormat);
            if (update.IsFailure)
                throw new ArgumentException(update.Error.Description);
            existing.SetAuditInfo(updatedBy, isUpdate: true);
        }
        else
        {
            var created = FixedAssetSettings.Create(fiscalYearStartMonth, fiscalYearLabelFormat);
            if (created.IsFailure)
                throw new ArgumentException(created.Error.Description);
            created.Value.SetAuditInfo(updatedBy, isUpdate: false);
            context.FixedAssetSettings.Add(created.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        // Recharge l'unique ligne pour renvoyer l'entité persistée (audit + champs à jour).
        return await context.FixedAssetSettings
            .OrderBy(s => s.CreatedAt)
            .FirstAsync(cancellationToken);
    }
}
