namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Services;

/// <summary>
/// Persists generated exports (PowerPoint, future formats) and returns short-lived signed URLs.
/// Implementations must scope storage by tenant and clean up after the configured TTL.
/// </summary>
public interface IExportStorageService
{
    /// <summary>
    /// Stores the binary content and returns the generated export identifier.
    /// </summary>
    Task<StoredExport> StoreAsync(
        Guid tenantId,
        Guid userId,
        string format,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a stored export from its identifier + signed token. Returns <c>null</c> if the
    /// token is invalid, expired, or the file no longer exists on disk.
    /// </summary>
    Task<StoredExportContent?> ResolveAsync(
        Guid exportId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes exports older than the retention window. Called by a periodic background job.
    /// Returns the count of deleted files.
    /// </summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>Result of a successful store operation.</summary>
public sealed record StoredExport
{
    public Guid ExportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

/// <summary>Result of a successful resolve operation.</summary>
public sealed record StoredExportContent
{
    public Guid ExportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] Content { get; init; } = Array.Empty<byte>();
}
