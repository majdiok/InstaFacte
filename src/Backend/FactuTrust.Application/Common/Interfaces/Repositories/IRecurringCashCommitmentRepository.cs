using FactuTrust.Domain.Entities.Treasury;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IRecurringCashCommitmentRepository
{
    Task<IReadOnlyList<RecurringCashCommitment>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Engagements actifs susceptibles de produire une occurrence dans la fenêtre demandée
    /// (démarrés avant la fin de fenêtre et non terminés avant son début).
    /// </summary>
    Task<IReadOnlyList<RecurringCashCommitment>> ListActiveForWindowAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<RecurringCashCommitment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default);

    Task UpdateAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default);

    Task RemoveAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default);
}

public interface ICashFlowForecastSettingsRepository
{
    /// <summary>Réglages du tenant. Null tant qu'aucun n'a été enregistré.</summary>
    Task<CashFlowForecastSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CashFlowForecastSettings settings, CancellationToken cancellationToken = default);
}
