using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AI;

/// <summary>
/// Per-tenant configuration for a cloud LLM endpoint (OpenAI-compatible API, e.g. OpenRouter).
/// API keys are stored encrypted at rest; see infrastructure layer.
/// </summary>
public sealed class TenantAiProvider : Entity
{
    /// <summary>Stable key, e.g. "openrouter".</summary>
    public string ProviderKey { get; private set; } = "openrouter";

    public string? DisplayName { get; private set; }

    /// <summary>Base URL without trailing slash; null means provider default (OpenRouter).</summary>
    public string? BaseUrl { get; private set; }

    public string EncryptedApiKey { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public DateTime? LastValidatedAtUtc { get; private set; }

    private TenantAiProvider() { }

    public static TenantAiProvider CreateOpenRouterDefault(
        string encryptedApiKey,
        bool isEnabled,
        string? displayName = null,
        string? baseUrl = null)
    {
        return new TenantAiProvider
        {
            ProviderKey = "openrouter",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "OpenRouter" : displayName.Trim(),
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/'),
            EncryptedApiKey = encryptedApiKey,
            IsEnabled = isEnabled
        };
    }

    public void Update(string? displayName, string? baseUrl, string encryptedApiKey, bool isEnabled)
    {
        DisplayName = displayName;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
        EncryptedApiKey = encryptedApiKey;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkValidated()
    {
        LastValidatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
