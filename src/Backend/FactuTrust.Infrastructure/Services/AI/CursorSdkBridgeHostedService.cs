using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class CursorSdkBridgeHostedService : IHostedService
{
    private readonly CursorSdkBridgeHost _host;
    private readonly CursorSdkSettings _settings;
    private readonly ILogger<CursorSdkBridgeHostedService> _logger;

    public CursorSdkBridgeHostedService(
        CursorSdkBridgeHost host,
        IOptions<CursorSdkSettings> settings,
        ILogger<CursorSdkBridgeHostedService> logger)
    {
        _host = host;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Pont Cursor SDK : démarrage automatique désactivé.");
            return;
        }

        await _host.TryAutoStartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => _host.ShutdownAsync();
}
