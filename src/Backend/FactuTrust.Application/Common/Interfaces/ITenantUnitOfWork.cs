using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Unité de travail tenant : exécute <c>action</c> dans UNE transaction couvrant tous les
/// repositories utilisés à l'intérieur (leurs contextes rejoignent la transaction ambiante).
/// Commit si l'action rend un <see cref="Result"/> succès, rollback sinon (ou sur exception).
/// Réentrant : un appel imbriqué rejoint la transaction en cours.
/// ATTENTION : l'audit (chaîne hash-chaînée) et les réservations de numéros restent
/// volontairement dans leurs propres transactions (voir CreateIsolatedContext) — un numéro
/// « brûlé » par un rollback est signalé par les contrôles d'intégrité de séquence existants.
/// </summary>
public interface ITenantUnitOfWork
{
    Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> action, CancellationToken cancellationToken = default);

    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken = default);
}
