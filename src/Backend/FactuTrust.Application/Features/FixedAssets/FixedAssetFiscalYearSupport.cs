using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.FixedAssets;

/// <summary>
/// Aide partagée (plan « Exercices décalés », P2/P3) : récupération du mois de début d'exercice
/// configuré pour le tenant. Retourne le défaut usine (1 = exercice civil) lorsque le dépôt de
/// paramètres n'est pas fourni (tests directs des handlers) — préserve le comportement historique.
/// </summary>
internal static class FixedAssetFiscalYearSupport
{
    public static async Task<int> GetStartMonthAsync(
        IFixedAssetSettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
            return FixedAssetSettings.DefaultFiscalYearStartMonth;

        var settings = await settingsRepository.GetForTenantAsync(cancellationToken);
        return settings.FiscalYearStartMonth;
    }
}
