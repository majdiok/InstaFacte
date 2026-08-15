using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Treasury;

namespace FactuTrust.Application.Common.Interfaces.Treasury;

/// <summary>
/// Orchestration du prévisionnel de trésorerie : collecte, agrégation, scénarios, persistance.
/// </summary>
public interface ICashFlowForecastService
{
    /// <summary>
    /// Dernier run exploitable pour l'horizon demandé, recalculé si sa fraîcheur a expiré
    /// (<c>CacheTtlMinutes</c>) ou si aucun n'existe.
    /// </summary>
    Task<Result<CashFlowForecastRun>> GetOrComputeAsync(
        int horizonMonths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcul explicite déclenché par l'utilisateur ou la tâche de fond.
    /// </summary>
    /// <param name="userId">
    /// Auteur du recalcul. Null pour la tâche de fond — c'est ce qui distingue les recalculs
    /// manuels, seuls soumis au plafond journalier.
    /// </param>
    Task<Result<CashFlowForecastRun>> RecomputeAsync(
        int horizonMonths,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
