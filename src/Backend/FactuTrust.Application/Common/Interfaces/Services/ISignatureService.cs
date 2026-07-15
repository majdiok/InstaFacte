namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for electronic signature operations.
/// </summary>
public interface ISignatureService
{
    /// <summary>
    /// Signs data and returns the signature hash.
    /// </summary>
    Task<string> SignAsync(byte[] data, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifies a signature against the original data.
    /// </summary>
    Task<bool> VerifyAsync(byte[] data, string signature, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Computes a SHA-256 hash of the data.
    /// </summary>
    string ComputeHash(byte[] data);
    
    /// <summary>
    /// Computes a SHA-256 hash of a string.
    /// </summary>
    string ComputeHash(string data);
}
