using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Electronic signature service using HMAC-SHA256 with a configuration-provided secret key.
/// Signatures are versioned ("v2:&lt;base64(HMAC)&gt;") so that legacy pseudo-signatures
/// (plain SHA-256 of "hash|timestamp", produced by earlier versions of this service, without
/// any secret key) can never be mistaken for a cryptographically valid signature: any signature
/// that does not start with the "v2:" prefix is treated as unverifiable and <see cref="VerifyAsync"/>
/// always returns <c>false</c> for it. There is no silent fallback that treats an unknown format
/// as valid.
/// </summary>
public sealed class SignatureService : ISignatureService
{
    private const string SignaturePrefix = "v2:";

    private readonly byte[] _key;

    public SignatureService(IConfiguration configuration)
    {
        var secretKey = configuration["SignatureSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "La clé secrète de signature n'est pas configurée. Renseignez SignatureSettings:SecretKey " +
                "(dotnet user-secrets en développement, variable d'environnement SignatureSettings__SecretKey en production).");
        }

        if (Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException("SignatureSettings:SecretKey doit faire au moins 32 octets (256 bits).");
        }

        _key = Encoding.UTF8.GetBytes(secretKey);
    }

    public Task<string> SignAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var mac = HMACSHA256.HashData(_key, data);
        return Task.FromResult(SignaturePrefix + Convert.ToBase64String(mac));
    }

    public Task<bool> VerifyAsync(byte[] data, string signature, CancellationToken cancellationToken = default)
    {
        // Format legacy (ou toute valeur inconnue) : jamais vérifiable, jamais un "true" de complaisance.
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        byte[] provided;
        try
        {
            provided = Convert.FromBase64String(signature[SignaturePrefix.Length..]);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }

        var expected = HMACSHA256.HashData(_key, data);
        return Task.FromResult(CryptographicOperations.FixedTimeEquals(expected, provided));
    }

    public string ComputeHash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToBase64String(hashBytes);
    }

    public string ComputeHash(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return ComputeHash(bytes);
    }
}
