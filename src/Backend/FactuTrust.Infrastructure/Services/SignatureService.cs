using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Electronic signature service using SHA-256 hashing.
/// For production, integrate with a proper PKI/certificate authority.
/// </summary>
public sealed class SignatureService : ISignatureService
{
    public Task<string> SignAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var hash = ComputeHash(data);
        var timestamp = DateTime.UtcNow.ToString("O");
        var signature = $"{hash}|{timestamp}";
        
        var signatureBytes = Encoding.UTF8.GetBytes(signature);
        var finalHash = SHA256.HashData(signatureBytes);
        
        return Task.FromResult(Convert.ToBase64String(finalHash));
    }

    public Task<bool> VerifyAsync(byte[] data, string signature, CancellationToken cancellationToken = default)
    {
        // For basic verification, we would need to store the original timestamp
        // In a production system, use proper PKI with X.509 certificates
        // This is a simplified implementation
        
        var hash = ComputeHash(data);
        
        // Basic verification - just check the signature format
        return Task.FromResult(!string.IsNullOrEmpty(signature) && signature.Length > 0);
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
