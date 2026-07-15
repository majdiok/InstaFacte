namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for secure file storage and archiving.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Stores a file and returns its storage path.
    /// </summary>
    Task<string> StoreAsync(
        byte[] content,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a file by its path.
    /// </summary>
    Task<byte[]?> GetAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the total storage used by a tenant.
    /// </summary>
    Task<long> GetStorageUsedAsync(CancellationToken cancellationToken = default);
}
