using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Critères de filtrage des flux d'une projection.</summary>
public sealed record CashFlowLineQueryCriteria
{
    public required Guid ForecastRunId { get; init; }
    public CashFlowDirection? Direction { get; init; }
    public CashFlowSourceType? SourceType { get; init; }
    public DateTime? ExpectedFrom { get; init; }
    public DateTime? ExpectedTo { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record CashFlowLineQueryResult(
    IReadOnlyList<CashFlowForecastLine> Items,
    int TotalCount,
    decimal TotalAmount);

public interface ICashFlowForecastRepository
{
    /// <summary>
    /// Dernier run exploitable (<c>Status = Computed</c>) pour l'horizon demandé, agrégats et
    /// enfants inclus. Null si aucun calcul n'a encore abouti.
    /// </summary>
    Task<CashFlowForecastRun?> GetLatestComputedAsync(
        int horizonMonths,
        CancellationToken cancellationToken = default);

    Task<CashFlowForecastRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CashFlowLineQueryResult> ListLinesAsync(
        CashFlowLineQueryCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste un run et ses enfants. À appeler dans une unité de travail
    /// (<c>ITenantUnitOfWork.ExecuteAsync</c>) : un run sans ses buckets n'a aucun sens.
    /// </summary>
    Task AddAsync(CashFlowForecastRun run, CancellationToken cancellationToken = default);

    Task UpdateAsync(CashFlowForecastRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge les runs antérieurs d'un même horizon (cascade sur les enfants). Un recalcul remplace
    /// le précédent : sans cette purge, la table de flux croît indéfiniment.
    /// </summary>
    Task DeletePreviousRunsAsync(
        int horizonMonths,
        Guid keepRunId,
        CancellationToken cancellationToken = default);

    /// <summary>Nombre de recalculs déclenchés depuis <paramref name="since"/> (garde-fou de débit).</summary>
    Task<int> CountRunsSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}
