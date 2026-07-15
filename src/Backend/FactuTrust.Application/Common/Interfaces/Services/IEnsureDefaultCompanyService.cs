using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Ensures the current tenant has a default Company in the tenant database.
/// If none exists, creates one from the Tenant (Master) data.
/// Used when saving drafts or submitting invoices with sellerId = tenant.Id.
/// </summary>
public interface IEnsureDefaultCompanyService
{
    /// <summary>
    /// Gets the Id of the default Company for the current tenant.
    /// If no Company exists in the tenant database, creates one from the Tenant (Master) and returns its Id.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success with Company Id, or Failure with a descriptive error (e.g. tenant not found).</returns>
    Task<Result<Guid>> GetOrCreateDefaultCompanyIdAsync(CancellationToken cancellationToken = default);
}
