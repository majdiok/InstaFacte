using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Logue un avertissement au démarrage si Tesseract OCR n'est pas disponible (tessdata manquant).
/// </summary>
public sealed class OcrStartupCheckHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OcrStartupCheckHostedService> _logger;

    public OcrStartupCheckHostedService(IServiceProvider services, ILogger<OcrStartupCheckHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var ocr = scope.ServiceProvider.GetRequiredService<IAiOcrService>();
        if (!ocr.IsAvailable)
        {
            _logger.LogWarning(
                "OCR Tesseract indisponible : déposez fra.traineddata et eng.traineddata dans Resources/Tessdata "
                + "(voir scripts/install-tessdata.ps1). L'import de photos utilisera le fallback vision si configuré.");
        }
        else
        {
            _logger.LogInformation("OCR Tesseract disponible pour l'import de documents scannés et images.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
