namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current tenant context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID from the request context.
    /// </summary>
    Guid? TenantId { get; }
    
    /// <summary>
    /// Gets the current tenant's database connection string.
    /// </summary>
    string? ConnectionString { get; }
    
    /// <summary>
    /// Sets the current tenant context.
    /// </summary>
    void SetTenant(Guid tenantId, string connectionString);
    
    /// <summary>
    /// Clears the current tenant context.
    /// </summary>
    void Clear();
}

/// <summary>
/// Service for resolving and managing tenant databases.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets the connection string for a specific tenant.
    /// </summary>
    Task<string?> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new database for a tenant and seeds default data.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="databaseName">The database name to create.</param>
    /// <param name="warehouseName">Optional name for the default warehouse. Defaults to "Entrepôt Principal".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> CreateTenantDatabaseAsync(Guid tenantId, string databaseName, string? warehouseName = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a lightweight database for an accounting firm tenant (migrations only, no business seeds).
    /// </summary>
    Task<string> CreateAccountingFirmDatabaseAsync(Guid tenantId, string databaseName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a tenant's database exists.
    /// </summary>
    Task<bool> DatabaseExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies migrations to a tenant's database.
    /// </summary>
    Task ApplyMigrationsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a tenant SQL database (orphan cleanup on failed registration).
    /// </summary>
    Task TryDropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the pre-migrated template database and backup file are up to date.
    /// </summary>
    Task EnsureTenantTemplateAsync(CancellationToken cancellationToken = default);
}
