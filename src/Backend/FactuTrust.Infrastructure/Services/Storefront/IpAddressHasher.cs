using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace FactuTrust.Infrastructure.Services.Storefront;

public sealed class IpAddressHasher : IIpAddressHasher
{
    private readonly byte[] _salt;

    public IpAddressHasher(IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:SecretKey"]
                     ?? configuration["JwtSettings:Secret"]
                     ?? throw new InvalidOperationException("JwtSettings:SecretKey is required for IP hashing.");
        _salt = Encoding.UTF8.GetBytes(secret);
    }

    public string? Hash(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        var ip = ipAddress.Trim();
        var input = Encoding.UTF8.GetBytes(ip);
        var buffer = new byte[_salt.Length + input.Length];
        Buffer.BlockCopy(_salt, 0, buffer, 0, _salt.Length);
        Buffer.BlockCopy(input, 0, buffer, _salt.Length, input.Length);

        var hash = SHA256.HashData(buffer);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
