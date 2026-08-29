using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Accès aux <see cref="FixedAssetSettings"/> du dossier (tenant) — singleton par tenant
/// (plan « Exercices décalés », décision D3 = granularité par tenant).
/// </summary>
public interface IFixedAssetSettingsRepository
{
    /// <summary>
    /// Retourne les paramètres du tenant. Si aucune ligne n'existe, retourne le défaut usine
    /// (exercice civil : <see cref="FixedAssetSettings.FiscalYearStartMonth"/> = 1, libellé
    /// « N/N+1 ») sans l'écrire en base — la persistance n'a lieu que via
    /// <see cref="UpsertAsync"/>. Garantit que tout tenant non configuré conserve le
    /// comportement historique (exercice civil).
    /// </summary>
    Task<FixedAssetSettings> GetForTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée ou met à jour les paramètres du tenant (upsert idempotent). Retourne l'entité
    /// persistée. Valide via <see cref="FixedAssetSettings.Create"/> / <see cref="FixedAssetSettings.Update"/>.
    /// </summary>
    Task<FixedAssetSettings> UpsertAsync(
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat,
        string updatedBy,
        CancellationToken cancellationToken = default);
}
