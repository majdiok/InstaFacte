using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for ProductCategory.
/// </summary>
public interface IProductCategoryRepository
{
    Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategory>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Guid> GetDefaultCategoryIdAsync(CancellationToken cancellationToken = default);
    Task<ProductCategory> AddAsync(ProductCategory entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductCategory entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
}
