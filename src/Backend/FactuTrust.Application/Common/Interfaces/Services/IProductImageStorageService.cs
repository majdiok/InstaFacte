namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for storing and deleting product images on the file system.
/// </summary>
public interface IProductImageStorageService
{
    /// <summary>
    /// Saves a product image and returns the relative URL to store in the product.
    /// Validates content type (jpeg, png, webp) and size (max 2 MB).
    /// </summary>
    /// <param name="tenantId">Tenant identifier for path isolation.</param>
    /// <param name="productId">Product identifier.</param>
    /// <param name="content">Image stream.</param>
    /// <param name="contentType">MIME type (e.g. image/jpeg).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Relative URL (e.g. /uploads/tenants/{tenantId}/products/{productId}.jpg).</returns>
    Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the product image file if it exists. Idempotent.
    /// </summary>
    Task DeleteAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default);
}
