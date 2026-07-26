using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Registre des emprunts et de leurs échéanciers. Gabarit : <see cref="IFixedAssetRepository"/>.
/// </summary>
public interface ILoanRepository
{
    /// <summary><paramref name="includeSchedule"/> charge l'échéancier trié par numéro d'échéance.</summary>
    Task<Loan?> GetByIdAsync(Guid id, bool includeSchedule = false, CancellationToken cancellationToken = default);

    Task<Loan?> GetByLoanNumberAsync(string loanNumber, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(
        int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);

    /// <summary>Nombre d'emprunts créés sur une année (génération du numéro séquentiel).</summary>
    Task<int> CountByYearPrefixAsync(int year, CancellationToken cancellationToken = default);

    Task<Loan> AddAsync(Loan entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(Loan entity, CancellationToken cancellationToken = default);
}
