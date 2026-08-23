using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Fallback used when a caller constructs extraction/import handlers without a resolver
/// (historical unit tests). Production DI always injects the real scoped services.
/// </summary>
public static class ModalCredentialsFallback
{
    public static IModalCredentialsResolver ForPlatform(IPlatformAiSettingsService platform) =>
        new PlatformOnlyResolver(platform);

    public static ITenantContext EmptyTenant { get; } = new EmptyTenantContext();

    private sealed class PlatformOnlyResolver : IModalCredentialsResolver
    {
        private readonly IPlatformAiSettingsService _platform;

        public PlatformOnlyResolver(IPlatformAiSettingsService platform) => _platform = platform;

        public async Task<ResolvedModalCredentials> ResolveAsync(
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            var platform = await _platform.GetModalCredentialsAsync(cancellationToken);
            return ResolvedModalCredentials.FromPlatform(platform);
        }
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public string? ConnectionString => null;
        public void SetTenant(Guid tenantId, string connectionString) { }
        public void Clear() { }
    }
}
