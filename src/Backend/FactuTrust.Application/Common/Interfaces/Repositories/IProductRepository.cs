using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for Product aggregate.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Gets a product by its code.
    /// </summary>
    Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recherche par code-barres, en correspondance EXACTE. Aucune approximation : un scan
    /// qui ne correspond à rien doit échouer, jamais deviner.
    /// </summary>
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all active products.
    /// </summary>
    Task<IReadOnlyList<Product>> GetActiveProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active products with category included (for reports).
    /// </summary>
    Task<IReadOnlyList<Product>> GetActiveProductsWithCategoryAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets products by type.
    /// </summary>
    Task<IReadOnlyList<Product>> GetByTypeAsync(ProductType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches products by name or code.
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        ProductType? type,
        bool? isActive,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight search for autocomplete/select dropdowns.
    /// No category include, no tracking. Returns at most <paramref name="pageSize"/> items
    /// ordered by name; does not compute a full total count.
    /// </summary>
    Task<IReadOnlyList<Product>> SearchForSelectAsync(
        string? searchTerm,
        bool? isActive,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns FODEC flags for the given product ids (missing ids are omitted).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> GetFodecFlagsByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a product is used in any invoice.
    /// </summary>
    Task<bool> IsUsedInInvoicesAsync(Guid productId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all active products with stock management enabled.
    /// </summary>
    Task<IReadOnlyList<Product>> GetStockManagedProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the image URL for a product.
    /// </summary>
    Task UpdateImageUrlAsync(Guid productId, string imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the image URL for a product.
    /// </summary>
    Task ClearImageUrlAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns product display names for the given ids (missing ids are omitted).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetProductNamesByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}
