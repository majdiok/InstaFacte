namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Produces a privacy-preserving one-way hash of an IP address.
/// Used for RGPD-compliant logging (consent trails, public order audit) where the raw IP must never
/// be persisted, but forensics still need a correlation key.
/// </summary>
public interface IIpAddressHasher
{
    /// <summary>Returns a hex-encoded SHA-256 hash of the IP, salted with the application secret.</summary>
    /// <param name="ipAddress">IPv4 or IPv6 address. <c>null</c> or empty returns <c>null</c>.</param>
    string? Hash(string? ipAddress);
}
