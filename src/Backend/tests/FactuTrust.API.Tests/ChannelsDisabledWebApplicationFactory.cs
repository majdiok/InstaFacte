using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FactuTrust.API.Tests;

/// <summary>
/// Factory de base des tests d'intégration : force les flags <c>Channels</c> à OFF, quel que soit
/// le contenu d'<c>appsettings.json</c> (qui peut être activé en dev). Garantit qu'aucun processus
/// Node (pont WhatsApp) n'est jamais lancé pendant <c>dotnet test</c>. La configuration in-memory
/// posée ici est ajoutée en dernier, donc prioritaire sur les fichiers appsettings.
/// </summary>
public class ChannelsDisabledWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Clé de signature JWT réservée aux tests (les appsettings ne contiennent plus de clé).
    /// Doit être utilisée par les tests qui forgent des tokens (voir PlatformApiIntegrationTests).
    /// </summary>
    public const string TestJwtSecretKey = "FactuTrust-Tests-Only-Signing-Key-Not-A-Production-Secret-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting est injecté dans la configuration initiale du WebApplicationBuilder :
        // indispensable pour les valeurs lues par Program.cs pendant la construction de l'hôte
        // (la clé JWT est capturée dans TokenValidationParameters à ce moment-là).
        builder.UseSetting("JwtSettings:SecretKey", TestJwtSecretKey);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Channels:Enabled"] = "false",
                ["Channels:WhatsAppEnabled"] = "false",
                ["Channels:AutoStart"] = "false",
                ["JwtSettings:SecretKey"] = TestJwtSecretKey
            });
        });
    }
}
