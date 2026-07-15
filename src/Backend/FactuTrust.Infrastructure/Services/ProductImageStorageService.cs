using System.Collections.Frozen;
using System.IO;
using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Options for product image file storage. BasePath is typically the application's wwwroot (full path).
/// </summary>
public sealed class ProductImageStorageOptions
{
    /// <summary>
    /// Full path to the root directory for uploads (e.g. wwwroot). Uploads are stored under uploads/tenants/{tenantId}/products/.
    /// </summary>
    public string BasePath { get; set; } = "wwwroot";
}

/// <summary>
/// Saves and deletes product images under BasePath/uploads/tenants/{tenantId}/products/.
/// </summary>
public sealed class ProductImageStorageService : IProductImageStorageService
{
    private const int MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    private static readonly FrozenDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    }.ToFrozenDictionary();

    private readonly ProductImageStorageOptions _options;
    private readonly ILogger<ProductImageStorageService> _logger;

    public ProductImageStorageService(
        IOptions<ProductImageStorageOptions> options,
        ILogger<ProductImageStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        Guid tenantId,
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.TryGetValue(contentType.Trim(), out var ext))
            throw new ArgumentException("Type de fichier non autorisé. Utilisez JPEG, PNG ou WebP.", nameof(contentType));

        var basePath = Path.GetFullPath(_options.BasePath ?? "wwwroot");
        var relativePath = Path.Combine("uploads", "tenants", tenantId.ToString(), "products");
        var fullDir = Path.Combine(basePath, relativePath);
        var fileName = productId.ToString("N") + ext;
        var fullPath = Path.Combine(fullDir, fileName);

        // Ensure we stay under base path (path traversal)
        var normalizedFull = Path.GetFullPath(fullPath);
        if (!normalizedFull.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chemin de stockage invalide.");

        Directory.CreateDirectory(fullDir);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxFileSizeBytes)
                throw new InvalidOperationException($"L'image ne doit pas dépasser {MaxFileSizeBytes / (1024 * 1024)} Mo.");
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (totalRead == 0)
            throw new ArgumentException("Le fichier image est vide.", nameof(content));

        var relativeUrl = "/" + relativePath.Replace('\\', '/').TrimStart('/') + "/" + fileName;
        _logger.LogDebug("Product image saved for product {ProductId}, tenant {TenantId}", productId, tenantId);
        return relativeUrl;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        var basePath = Path.GetFullPath(_options.BasePath ?? "wwwroot");
        var relativePath = Path.Combine("uploads", "tenants", tenantId.ToString(), "products");

        foreach (var ext in AllowedContentTypes.Values.Distinct())
        {
            var fileName = productId.ToString("N") + ext;
            var fullPath = Path.Combine(basePath, relativePath, fileName);
            var normalizedFull = Path.GetFullPath(fullPath);
            if (!normalizedFull.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                    _logger.LogDebug("Product image deleted for product {ProductId}, tenant {TenantId}", productId, tenantId);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not delete product image at {Path}", fullPath);
                }
                break;
            }
        }

        return Task.CompletedTask;
    }
}
