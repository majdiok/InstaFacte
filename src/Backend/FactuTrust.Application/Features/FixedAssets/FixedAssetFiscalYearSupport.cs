using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.FixedAssets;

/// <summary>
/// Aide partagée (plan « Exercices décalés », P2/P3) : récupération des paramètres d'exercice
/// configurés pour le tenant. Retourne le défaut usine (1 = exercice civil, libellé « N/N+1 »)
/// lorsque le dépôt de paramètres n'est pas fourni (tests directs des handlers) — préserve le
/// comportement historique.
/// </summary>
internal static class FixedAssetFiscalYearSupport
{
    /// <summary>
    /// Paramètres complets du tenant (mois de début + format de libellé). Retourne le défaut usine
    /// (<see cref="FixedAssetSettings.CreateDefault"/>) lorsque le dépôt n'est pas fourni.
    /// Utilisé par le run de dotations (P3) qui a besoin du format de libellé pour le résultat.
    /// </summary>
    public static async Task<FixedAssetSettings> GetSettingsAsync(
        IFixedAssetSettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
            return FixedAssetSettings.CreateDefault();

        return await settingsRepository.GetForTenantAsync(cancellationToken);
    }

    /// <summary>
    /// Mois de début d'exercice configuré (1 = exercice civil). Délègue à
    /// <see cref="GetSettingsAsync"/> (lecture unique) — utilisé par les handlers n'ayant besoin
    /// que du mois (mise en service, régénération, aperçu, cession — P2).
    /// </summary>
    public static async Task<int> GetStartMonthAsync(
        IFixedAssetSettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
        => (await GetSettingsAsync(settingsRepository, cancellationToken)).FiscalYearStartMonth;
}
