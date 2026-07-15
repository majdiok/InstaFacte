namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for audit logging.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event.
    /// </summary>
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the last audit hash for chain integrity.
    /// </summary>
    Task<string> GetLastHashAsync(CancellationToken cancellationToken = default);
}
