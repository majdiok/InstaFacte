using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IDepreciationRateCategoryRepository
{
    Task<IReadOnlyList<DepreciationRateCategory>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<DepreciationRateCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DepreciationRateCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}