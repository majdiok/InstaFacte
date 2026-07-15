using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Code des providers de paiement supportés par FactuTrust (Lot C5).</summary>
public static class PaymentProviderCodes
{
    public const string Konnect = "konnect";
    public const string Paymee = "paymee";
    public const string Wire = "wire";

    public static readonly IReadOnlyList<string> All = new[] { Konnect, Paymee, Wire };

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Lot C5 — Configuration d'un provider de paiement plateforme.
///
/// Une ligne par provider (<c>konnect</c>, <c>paymee</c>, <c>wire</c>). Les secrets
/// (API key, webhook secret) sont chiffrés via <see cref="Microsoft.AspNetCore.DataProtection.IDataProtectionProvider"/>
/// et stockés sous forme JSON dans <see cref="EncryptedSecretsJson"/>.
///
/// Exemple :
/// <code>
/// EncryptedSecretsJson = "{\"apiKey\": \"PROTECTED:...\", \"merchantId\": \"5fa...\"}"
/// WebhookSecretEncrypted = "PROTECTED:..."
/// </code>
/// </summary>
public sealed class PaymentProviderConfig : Entity
{
    public string ProviderCode { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public bool IsTestMode { get; private set; }
    public string? EncryptedSecretsJson { get; private set; }
    public string? WebhookSecretEncrypted { get; private set; }
    /// <summary>URL de retour autorisée (whitelist domaine + path). Null = on accepte tout returnUrl HTTPS.</summary>
    public string? AllowedReturnDomain { get; private set; }

    private PaymentProviderConfig() { }

    public static PaymentProviderConfig CreateDisabled(string providerCode, string displayName)
    {
        if (!PaymentProviderCodes.IsKnown(providerCode))
            throw new ArgumentException($"Provider inconnu : {providerCode}", nameof(providerCode));
        return new PaymentProviderConfig
        {
            ProviderCode = providerCode.ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            IsEnabled = false,
            IsTestMode = true
        };
    }

    public void UpdateConfig(
        string displayName,
        bool isEnabled,
        bool isTestMode,
        string? encryptedSecretsJson,
        string? webhookSecretEncrypted,
        string? allowedReturnDomain)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("DisplayName requis", nameof(displayName));
        DisplayName = displayName.Trim();
        IsEnabled = isEnabled;
        IsTestMode = isTestMode;
        EncryptedSecretsJson = encryptedSecretsJson;
        WebhookSecretEncrypted = webhookSecretEncrypted;
        AllowedReturnDomain = string.IsNullOrWhiteSpace(allowedReturnDomain) ? null : allowedReturnDomain.Trim();
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
