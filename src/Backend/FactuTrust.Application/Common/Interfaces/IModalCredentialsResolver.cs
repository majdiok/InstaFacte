using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolves Modal (Kimi) credentials for a tenant: optional Master-DB override, else platform fallback.
/// </summary>
public interface IModalCredentialsResolver
{
    /// <summary>
    /// Resolves credentials for <paramref name="tenantId"/>.
    /// A null tenant id always returns the platform singleton (admin / anonymous health).
    /// </summary>
    Task<ResolvedModalCredentials> ResolveAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
