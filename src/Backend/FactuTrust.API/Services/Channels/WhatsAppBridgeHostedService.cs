using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Services.Channels;

/// <summary>
/// Démarre le pont WhatsApp au boot de l'application, uniquement si la fonctionnalité est activée
/// et que le démarrage automatique est demandé. Inerte quand les flags sont OFF (défaut) — ce qui
/// garantit qu'aucun processus Node n'est lancé pendant les tests d'intégration (WebApplicationFactory).
/// </summary>
public sealed class WhatsAppBridgeHostedService : IHostedService
{
    private readonly WhatsAppBridgeHost _host;
    private readonly ChannelsSettings _settings;
    private readonly ILogger<WhatsAppBridgeHostedService> _logger;

    public WhatsAppBridgeHostedService(
        WhatsAppBridgeHost host,
        IOptions<ChannelsSettings> settings,
        ILogger<WhatsAppBridgeHostedService> logger)
    {
        _host = host;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!(_settings.Enabled && _settings.WhatsAppEnabled && _settings.AutoStart))
        {
            _logger.LogInformation("Pont WhatsApp : démarrage automatique désactivé (flags OFF ou AutoStart=false).");
            return;
        }

        _logger.LogInformation("Pont WhatsApp : démarrage automatique…");
        await _host.TryAutoStartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Arrêt du processus uniquement ; la libération des sémaphores est faite par le conteneur DI
        // (DisposeAsync, idempotent) — évite un double-dispose.
        await _host.ShutdownAsync();
    }
}
