namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for searching product images from external APIs (Unsplash, Google Custom Search).
/// </summary>
public interface IProductImageSearchService
{
    /// <summary>
    /// Searches for an image URL matching the given query.
    /// Uses Unsplash first, then Google Custom Search as fallback.
    /// </summary>
    /// <param name="searchQuery">Search terms (e.g. product name + category).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Image URL if found, null otherwise.</returns>
    Task<string?> SearchImageUrlAsync(string searchQuery, CancellationToken cancellationToken = default);
}
