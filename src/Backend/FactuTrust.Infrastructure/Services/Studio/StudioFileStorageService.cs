using System.Collections.Frozen;
using System.Text.RegularExpressions;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Studio.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>Options for Studio file storage. BasePath is typically the application's wwwroot (full path).</summary>
public sealed class StudioFileStorageOptions
{
    public string BasePath { get; set; } = "wwwroot";
}

/// <summary>
/// Saves/deletes Studio attachment &amp; signature files under
/// BasePath/uploads/tenants/{tenantId}/studio/{entityKey}/. Hardened like the product-image storage:
/// MIME whitelist, size cap, validated entity key, and path-traversal containment.
/// </summary>
public sealed partial class StudioFileStorageService : IStudioFileStorageService
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const string StudioSegment = "studio";

    private static readonly FrozenDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["application/pdf"] = ".pdf"
    }.ToFrozenDictionary();

    // Reverse map for downloads (extension → MIME). Values mirror AllowedContentTypes.
    private static readonly FrozenDictionary<string, string> ExtensionContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".pdf"] = "application/pdf"
    }.ToFrozenDictionary();

    // Exactly what SaveAsync produces: 32-hex GUID ("N" format) + whitelisted lowercase extension.
    [GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png|webp|gif|pdf)$")]
    private static partial Regex StoredFileNamePattern();

    private readonly StudioFileStorageOptions _options;
    private readonly ILogger<StudioFileStorageService> _logger;

    public StudioFileStorageService(IOptions<StudioFileStorageOptions> options, ILogger<StudioFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Guid tenantId, string entityKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!StudioKey.IsValidShape(entityKey))
            throw new ArgumentException("Clé de table invalide.", nameof(entityKey));
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.TryGetValue(contentType.Trim(), out var ext))
            throw new ArgumentException("Type de fichier non autorisé. Utilisez une image (JPEG, PNG, WebP, GIF) ou un PDF.", nameof(contentType));

        var basePath = Path.GetFullPath(_options.BasePath ?? "wwwroot");
        var relativeDir = Path.Combine("uploads", "tenants", tenantId.ToString(), StudioSegment, entityKey);
        var fullDir = Path.Combine(basePath, relativeDir);
        var fileName = Guid.NewGuid().ToString("N") + ext;
        var fullPath = Path.Combine(fullDir, fileName);

        var normalizedFull = Path.GetFullPath(fullPath);
        if (!normalizedFull.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chemin de stockage invalide.");

        Directory.CreateDirectory(fullDir);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxFileSizeBytes)
                {
                    fileStream.Close();
                    TryDeleteFile(fullPath);
                    throw new InvalidOperationException($"Le fichier ne doit pas dépasser {MaxFileSizeBytes / (1024 * 1024)} Mo.");
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (totalRead == 0)
            {
                fileStream.Close();
                TryDeleteFile(fullPath);
                throw new ArgumentException("Le fichier est vide.", nameof(content));
            }
        }

        var relativeUrl = "/" + relativeDir.Replace('\\', '/').TrimStart('/') + "/" + fileName;
        _logger.LogDebug("Studio file saved for tenant {TenantId}, entity {EntityKey}", tenantId, entityKey);
        return relativeUrl;
    }

    public StudioStoredFile? Resolve(Guid tenantId, string entityKey, string fileName)
    {
        if (!StudioKey.IsValidShape(entityKey)) return null;
        if (string.IsNullOrWhiteSpace(fileName) || !StoredFileNamePattern().IsMatch(fileName)) return null;

        var basePath = Path.GetFullPath(_options.BasePath ?? "wwwroot");
        var tenantEntityDir = Path.GetFullPath(
            Path.Combine(basePath, "uploads", "tenants", tenantId.ToString(), StudioSegment, entityKey));

        // The strict file-name pattern already excludes traversal; containment is belt-and-braces.
        var fullPath = Path.GetFullPath(Path.Combine(tenantEntityDir, fileName));
        if (!fullPath.StartsWith(tenantEntityDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!File.Exists(fullPath)) return null;

        return new StudioStoredFile(fullPath, ExtensionContentTypes[Path.GetExtension(fileName)]);
    }

    public Task DeleteAsync(Guid tenantId, string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return Task.CompletedTask;

        // Only ever delete inside THIS tenant's studio folder (defense against foreign/forged paths).
        var basePath = Path.GetFullPath(_options.BasePath ?? "wwwroot");
        var tenantStudioRel = Path.Combine("uploads", "tenants", tenantId.ToString(), StudioSegment);
        var tenantStudioFull = Path.GetFullPath(Path.Combine(basePath, tenantStudioRel));

        var trimmed = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(basePath, trimmed));
        if (!candidate.StartsWith(tenantStudioFull, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        TryDeleteFile(candidate);
        return Task.CompletedTask;
    }

    private void TryDeleteFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException ex) { _logger.LogWarning(ex, "Could not delete Studio file at {Path}", fullPath); }
        catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "Could not delete Studio file at {Path}", fullPath); }
    }
}
