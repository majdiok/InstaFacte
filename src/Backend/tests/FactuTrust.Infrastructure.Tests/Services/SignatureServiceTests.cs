using System.Text;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// CWE-327 hardening: <c>SignatureService</c> used to produce a pseudo-signature (plain SHA-256
/// of "hash|timestamp", no secret key) and <c>VerifyAsync</c> only checked that the signature
/// string was non-empty — it accepted any garbage as "valid". It now uses HMAC-SHA256 with a
/// configuration-provided key, versioned "v2:" format, and real constant-time verification.
/// </summary>
public sealed class SignatureServiceTests
{
    private const string ValidKey = "Unit-Test-Only-Signature-Key-Not-A-Production-Secret-0123456789";

    private static SignatureService CreateService(string? secretKey = ValidKey)
    {
        var configValues = new Dictionary<string, string?>();
        if (secretKey is not null)
        {
            configValues["SignatureSettings:SecretKey"] = secretKey;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        return new SignatureService(config);
    }

    [Fact]
    public void Constructor_throws_when_secret_key_missing()
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(secretKey: null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    public void Constructor_throws_when_secret_key_blank_or_under_32_bytes(string secretKey)
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(secretKey));
    }

    [Fact]
    public async Task SignAsync_produces_v2_prefixed_signature()
    {
        var service = CreateService();
        var signature = await service.SignAsync(Encoding.UTF8.GetBytes("invoice-bytes"));

        Assert.StartsWith("v2:", signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_accepts_matching_v2_signature()
    {
        var service = CreateService();
        var data = Encoding.UTF8.GetBytes("invoice-bytes");
        var signature = await service.SignAsync(data);

        var isValid = await service.VerifyAsync(data, signature);

        Assert.True(isValid);
    }

    [Fact]
    public async Task VerifyAsync_rejects_signature_for_different_data()
    {
        var service = CreateService();
        var signature = await service.SignAsync(Encoding.UTF8.GetBytes("original-bytes"));

        var isValid = await service.VerifyAsync(Encoding.UTF8.GetBytes("tampered-bytes"), signature);

        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyAsync_rejects_v2_signature_produced_with_a_different_key()
    {
        var data = Encoding.UTF8.GetBytes("invoice-bytes");
        var signedByOtherKey = await CreateService("Different-Unit-Test-Key-Not-A-Production-Secret-abcdefghijklmno")
            .SignAsync(data);

        var isValid = await CreateService().VerifyAsync(data, signedByOtherKey);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-signature-at-all")]
    [InlineData("dGVzdA==")] // legacy-shaped: plain base64, no "v2:" prefix
    public async Task VerifyAsync_never_treats_legacy_or_unknown_format_as_valid(string legacySignature)
    {
        var service = CreateService();
        var data = Encoding.UTF8.GetBytes("invoice-bytes");

        var isValid = await service.VerifyAsync(data, legacySignature);

        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyAsync_rejects_v2_signature_with_invalid_base64_payload()
    {
        var service = CreateService();

        var isValid = await service.VerifyAsync(Encoding.UTF8.GetBytes("invoice-bytes"), "v2:not-base64!!!");

        Assert.False(isValid);
    }

    [Fact]
    public void ComputeHash_bytes_and_string_overloads_remain_plain_sha256_utilities()
    {
        var service = CreateService();
        var bytes = Encoding.UTF8.GetBytes("hello");

        Assert.Equal(service.ComputeHash(bytes), service.ComputeHash("hello"));
    }
}
