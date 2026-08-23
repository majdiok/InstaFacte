using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AI;

/// <summary>
/// Per-tenant Modal (Kimi) endpoint override stored in the master database.
/// Absence of a row means the tenant inherits <see cref="PlatformAiSettings"/> Modal credentials.
/// </summary>
public sealed class TenantModalSettings : Entity
{
    public Guid TenantId { get; private set; }

    public bool IsEnabled { get; private set; }

    public string? DisplayName { get; private set; }

    /// <summary>HTTPS base including /v1; null = appsettings Modal:DefaultBaseUrl.</summary>
    public string? BaseUrl { get; private set; }

    public string? EncryptedApiKey { get; private set; }

    public string? ApiKeyLast4 { get; private set; }

    private TenantModalSettings() { }

    public static TenantModalSettings Create(Guid tenantId) => new() { TenantId = tenantId };

    /// <summary>
    /// Updates Modal settings. Pass <paramref name="encryptedApiKey"/> / <paramref name="apiKeyLast4"/>
    /// as null to keep the existing secret.
    /// </summary>
    public void SetModalConfig(
        bool isEnabled,
        string? displayName,
        string? baseUrl,
        string? encryptedApiKey,
        string? apiKeyLast4)
    {
        IsEnabled = isEnabled;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(DisplayName) ? "Modal (Kimi)" : DisplayName)
            : displayName.Trim();
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');

        if (encryptedApiKey is not null)
        {
            EncryptedApiKey = string.IsNullOrWhiteSpace(encryptedApiKey) ? null : encryptedApiKey;
            ApiKeyLast4 = string.IsNullOrWhiteSpace(apiKeyLast4) ? null : apiKeyLast4.Trim();
        }
    }

    public bool HasModalApiKey => !string.IsNullOrWhiteSpace(EncryptedApiKey);
}
