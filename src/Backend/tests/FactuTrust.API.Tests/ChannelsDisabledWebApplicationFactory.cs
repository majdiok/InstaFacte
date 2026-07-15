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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Channels:Enabled"] = "false",
                ["Channels:WhatsAppEnabled"] = "false",
                ["Channels:AutoStart"] = "false"
            });
        });
    }
}
