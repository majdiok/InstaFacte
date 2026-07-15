using FactuTrust.Domain.Entities.AI;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ITenantAiProviderRepository
{
    Task<TenantAiProvider?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// When <paramref name="plainApiKey"/> is null or whitespace, the existing encrypted key is preserved.
    /// </summary>
    Task UpsertOpenRouterAsync(
        string? displayName,
        string? baseUrl,
        string? plainApiKey,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>Returns decrypted API key when OpenRouter is enabled and configured.</summary>
    Task<string?> GetDecryptedApiKeyForOpenRouterAsync(CancellationToken cancellationToken = default);
}
