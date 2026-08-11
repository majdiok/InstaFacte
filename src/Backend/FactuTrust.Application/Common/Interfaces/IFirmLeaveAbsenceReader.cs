namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Congés réellement consommés par collaborateur, pour le calcul du temps de présence productif.
/// </summary>
/// <remarks>
/// Volontairement distinct de <c>IFirmLeaveService</c> : ce contrat n'expose qu'une agrégation en
/// lecture, sollicitée par les écrans de coût et de rentabilité. Le passer par le service complet
/// des congés y ferait remonter tout le circuit d'approbation sans nécessité.
/// </remarks>
public interface IFirmLeaveAbsenceReader
{
    /// <summary>
    /// Jours approuvés de l'exercice, par collaborateur, pour les seuls types décomptés du
    /// temps de présence.
    /// </summary>
    /// <remarks>
    /// Une seule requête par écran : les appelants itèrent ensuite sur le dictionnaire, sans
    /// requête par ligne.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, decimal>> GetApprovedAbsenceDaysByUserAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);
}
