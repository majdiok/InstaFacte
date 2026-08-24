using FactuTrust.Domain.Entities;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IProductAttributeRepository
{
    Task<ProductAttributeDefinition?> GetByIdWithValuesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductAttributeDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductAttributeDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default);
    Task DeleteAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default);
    Task<bool> IsDefinitionInUseAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task<bool> IsValueInUseAsync(Guid valueId, CancellationToken cancellationToken = default);
    Task AddAxisAsync(ProductVariantAxis axis, CancellationToken cancellationToken = default);
    Task AddVariantLinkAsync(ProductVariantAttributeValue link, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductVariantAxis>> ListAxesAsync(Guid parentProductId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attribute/value pairs for variant child products.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductVariantAttributePairDto>>> GetVariantAttributesByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}
