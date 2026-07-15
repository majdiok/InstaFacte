namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Saves and deletes files for Studio Attachment / Signature fields, under a tenant- and entity-scoped
/// folder. Mirrors the hardened product-image storage (size cap, MIME whitelist, path-traversal safe).
/// The field stores the returned relative URL; static files serve it.
/// </summary>
public interface IStudioFileStorageService
{
    /// <summary>Saves a file and returns its relative URL (e.g. <c>/uploads/tenants/{t}/studio/{entityKey}/{guid}.png</c>).</summary>
    Task<string> SaveAsync(
        Guid tenantId, string entityKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file at a previously returned relative URL (best-effort; ignores foreign/invalid paths).</summary>
    Task DeleteAsync(Guid tenantId, string relativeUrl, CancellationToken cancellationToken = default);
}
