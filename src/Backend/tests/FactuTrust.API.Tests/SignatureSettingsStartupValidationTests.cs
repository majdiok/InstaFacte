using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// <c>SignatureService</c> (Infrastructure) validates <c>SignatureSettings:SecretKey</c> in its
/// constructor, but it is registered as Scoped, so that check only ran lazily — the first time
/// something in a request scope resolved the service (i.e. the first invoice signature) — and a
/// missing/misconfigured key would silently pass application startup instead of failing fast,
/// unlike the equivalent <c>JwtSettings:SecretKey</c> check. These tests assert that
/// <c>Program.cs</c> now validates the key eagerly at host startup, exactly like the JWT key
/// (see <see cref="JwtCompromisedKeyBlocklistTests"/> for the analogous JWT blocklist tests and
/// the same WebApplicationFactory pattern, which works here because the check runs in
/// Program.cs top-level statements before Hangfire/SQL Server initialization — avoiding this
/// sandbox's "LocalDB is not supported on this platform" limitation).
/// </summary>
public sealed class SignatureSettingsStartupValidationTests
{
    private sealed class SignatureKeyWebApplicationFactory : ChannelsDisabledWebApplicationFactory
    {
        private readonly string? _signatureSecretKey;

        public SignatureKeyWebApplicationFactory(string? signatureSecretKey) => _signatureSecretKey = signatureSecretKey;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // Override the base factory's test signature key with the scenario under test.
            // UseSetting is read during host construction; in-memory config wins last for the same key.
            builder.UseSetting("SignatureSettings:SecretKey", _signatureSecretKey ?? string.Empty);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SignatureSettings:SecretKey"] = _signatureSecretKey
                });
            });
        }
    }

    [Fact]
    public void Startup_fails_when_signature_secret_key_is_missing()
    {
        using var factory = new SignatureKeyWebApplicationFactory(signatureSecretKey: null);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.True(
            ContainsInvalidOperationException(ex, "clé secrète de signature n'est pas configurée"),
            $"Expected startup to fail with the missing-signature-key message, got: {ex}");
    }

    [Fact]
    public void Startup_fails_when_signature_secret_key_is_blank()
    {
        using var factory = new SignatureKeyWebApplicationFactory(signatureSecretKey: "   ");

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.True(
            ContainsInvalidOperationException(ex, "clé secrète de signature n'est pas configurée"),
            $"Expected startup to fail with the missing-signature-key message, got: {ex}");
    }

    [Fact]
    public void Startup_fails_when_signature_secret_key_is_too_short()
    {
        // 20 ASCII bytes < the required 32-byte (256-bit) minimum.
        using var factory = new SignatureKeyWebApplicationFactory(signatureSecretKey: "short-signature-key");

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.True(
            ContainsInvalidOperationException(ex, "SignatureSettings:SecretKey doit faire au moins 32 octets"),
            $"Expected startup to fail with the too-short-signature-key message, got: {ex}");
    }

    private static bool ContainsInvalidOperationException(Exception? exception, string messageFragment)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is InvalidOperationException && current.Message.Contains(messageFragment, StringComparison.Ordinal))
            {
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (ContainsInvalidOperationException(inner, messageFragment))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
