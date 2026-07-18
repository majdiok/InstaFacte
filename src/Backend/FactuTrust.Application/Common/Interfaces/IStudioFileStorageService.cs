namespace FactuTrust.Application.Common.Interfaces;

/// <summary>A stored Studio file resolved for download: full path on disk + content type.</summary>
public sealed record StudioStoredFile(string FullPath, string ContentType);

/// <summary>
/// Saves and deletes files for Studio Attachment / Signature fields, under a tenant- and entity-scoped
/// folder. Mirrors the hardened product-image storage (size cap, MIME whitelist, path-traversal safe).
/// The field stores the returned relative URL; the authenticated download endpoint serves it
/// (these files are NOT statically served — see the studio-uploads guard in Program.cs).
/// </summary>
public interface IStudioFileStorageService
{
    /// <summary>Saves a file and returns its relative URL (e.g. <c>/uploads/tenants/{t}/studio/{entityKey}/{guid}.png</c>).</summary>
    Task<string> SaveAsync(
        Guid tenantId, string entityKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a stored file for the given tenant/entity for download. The tenant id must come from the
    /// authenticated context, never from the request path. Returns null when the name is invalid or the
    /// file does not exist under this tenant's studio folder.
    /// </summary>
    StudioStoredFile? Resolve(Guid tenantId, string entityKey, string fileName);

    /// <summary>Deletes the file at a previously returned relative URL (best-effort; ignores foreign/invalid paths).</summary>
    Task DeleteAsync(Guid tenantId, string relativeUrl, CancellationToken cancellationToken = default);
}
