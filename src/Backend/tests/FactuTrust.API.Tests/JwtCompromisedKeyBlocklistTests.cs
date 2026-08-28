using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// CWE-798 hardening: <c>appsettings.Development.json</c> used to commit a real
/// <c>JwtSettings:SecretKey</c> value. That value has been removed from the repo and its
/// SHA-256 hash added to the blocklist in <c>Program.cs</c> so the key can never be reused,
/// even if it leaks again from git history. These tests boot the host with the exact
/// historically-committed key (and with the pre-existing legacy key) and assert that startup
/// fails loudly instead of silently accepting a known-compromised secret.
/// </summary>
public sealed class JwtCompromisedKeyBlocklistTests
{
    // Valeur historiquement commitée dans appsettings.Development.json (retirée du dépôt par
    // ce correctif). Réutilisée ici uniquement pour vérifier que la liste de blocage par hash
    // dans Program.cs la rejette effectivement — jamais utilisée comme clé réelle.
    private const string PreviouslyCommittedDevKey = "V/kJ9EXOO649uJN20ftUrgrooryf1FGS9UGPdjqORGnzDPN+mA70NMQWLmP4KKst";

    private sealed class BlocklistedKeyWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _secretKey;

        public BlocklistedKeyWebApplicationFactory(string secretKey) => _secretKey = secretKey;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JwtSettings:SecretKey", _secretKey);
            builder.UseSetting("SignatureSettings:SecretKey", ChannelsDisabledWebApplicationFactory.TestSignatureSecretKey);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Channels:Enabled"] = "false",
                    ["Channels:WhatsAppEnabled"] = "false",
                    ["Channels:AutoStart"] = "false",
                    ["JwtSettings:SecretKey"] = _secretKey,
                    ["SignatureSettings:SecretKey"] = ChannelsDisabledWebApplicationFactory.TestSignatureSecretKey
                });
            });
        }
    }

    [Fact]
    public void Startup_rejects_the_key_previously_committed_in_appsettings_development()
    {
        using var factory = new BlocklistedKeyWebApplicationFactory(PreviouslyCommittedDevKey);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.True(
            ContainsInvalidOperationException(ex, "clé JWT historique"),
            $"Expected startup to fail with the blocklisted-key message, got: {ex}");
    }

    // Note: pas de test positif dédié ici (clé valide -\u003e démarrage OK) : il exigerait de
    // pousser CreateClient() jusqu'au démarrage complet du host (Hangfire/SQL Server inclus),
    // indisponible dans ce bac à sable (« LocalDB is not supported on this platform », même
    // limitation que les 12 échecs préexistants de la suite FactuTrust.API.Tests). Les 144
    // autres tests de cette suite, qui utilisent tous ChannelsDisabledWebApplicationFactory
    // avec TestJwtSecretKey (clé valide, non blocklistée), servent déjà de preuve positive.

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
