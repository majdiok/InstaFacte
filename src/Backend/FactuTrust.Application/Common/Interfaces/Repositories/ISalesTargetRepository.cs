using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ISalesTargetRepository
{
    Task<SalesTarget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalesTarget?> GetByUserYearMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesTarget>> GetByYearAsync(int year, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<SalesTarget> AddAsync(SalesTarget entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(SalesTarget entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(SalesTarget entity, CancellationToken cancellationToken = default);
}
