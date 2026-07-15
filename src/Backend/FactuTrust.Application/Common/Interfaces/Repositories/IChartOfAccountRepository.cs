using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IChartOfAccountRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChartOfAccount>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<ChartOfAccount> AddAsync(ChartOfAccount entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChartOfAccount entity, CancellationToken cancellationToken = default);
}
