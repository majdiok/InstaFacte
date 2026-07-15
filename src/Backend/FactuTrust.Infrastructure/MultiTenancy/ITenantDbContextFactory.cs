using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Factory interface for creating tenant-specific DbContext instances.
/// This interface allows for easier testing by enabling mocking.
/// </summary>
public interface ITenantDbContextFactory
{
    /// <summary>
    /// Creates a new TenantDbContext instance for the current tenant.
    /// </summary>
    TenantDbContext CreateContext();
}
