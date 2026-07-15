using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Génération à la demande d'une occurrence d'un modèle d'écriture récurrent (bouton
/// « Générer maintenant ») — le job planifié couvre les échéances automatiques.
/// </summary>
public interface IRecurringEntryService
{
    /// <summary>Génère l'occurrence courante (NextRunDate, ou aujourd'hui) du modèle et avance l'échéance. Renvoie l'id de l'écriture.</summary>
    Task<Result<Guid>> GenerateNowAsync(Guid templateId, CancellationToken cancellationToken = default);
}
