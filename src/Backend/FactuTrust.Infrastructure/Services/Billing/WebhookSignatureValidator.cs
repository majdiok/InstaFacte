using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Validation HMAC SHA-256 des webhooks providers.
///
/// Compare la signature reçue avec une signature recalculée à partir du secret
/// stocké, en utilisant <see cref="CryptographicOperations.FixedTimeEquals"/>
/// pour éviter les attaques de timing. Accepte plusieurs formats de header :
/// <list type="bullet">
///   <item><c>sha256=...</c> (style GitHub/Konnect)</item>
///   <item><c>...</c> (hex brut)</item>
///   <item><c>t=...,v1=...</c> (style Stripe — extrait <c>v1</c>)</item>
/// </list>
/// </summary>
public sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly PaymentProviderConfigService _configService;

    public WebhookSignatureValidator(PaymentProviderConfigService configService)
    {
        _configService = configService;
    }

    public bool ValidateHmacSha256(string payload, string? signatureHeader, string? webhookSecretEncrypted)
    {
        if (string.IsNullOrWhiteSpace(payload)) return false;
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        if (string.IsNullOrWhiteSpace(webhookSecretEncrypted)) return false;

        // Le secret est attendu déjà déchiffré ici. Le caller utilise ReadDecryptedAsync pour récupérer le clear-text.
        var receivedHex = ExtractSignatureHex(signatureHeader);
        if (string.IsNullOrEmpty(receivedHex)) return false;

        byte[] received;
        try { received = Convert.FromHexString(receivedHex); }
        catch { return false; }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecretEncrypted));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        if (received.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(received, expected);
    }

    private static string ExtractSignatureHex(string header)
    {
        var trimmed = header.Trim();
        if (trimmed.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring("sha256=".Length).Trim();

        // Style "t=12345,v1=abcdef..."
        if (trimmed.Contains(","))
        {
            foreach (var part in trimmed.Split(','))
            {
                var p = part.Trim();
                if (p.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
                    return p.Substring("v1=".Length).Trim();
            }
        }
        return trimmed;
    }
}
