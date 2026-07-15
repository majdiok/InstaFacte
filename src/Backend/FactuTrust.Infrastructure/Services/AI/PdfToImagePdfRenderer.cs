using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Rend des pages PDF en PNG via PDFtoImage (libpdfium + SkiaSharp).
/// </summary>
public sealed class PdfToImagePdfRenderer : IAiPdfRenderer
{
    private readonly ILogger<PdfToImagePdfRenderer> _logger;

    public PdfToImagePdfRenderer(ILogger<PdfToImagePdfRenderer> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => true;

    public Task<AiRenderedPage?> RenderPageAsync(byte[] pdfBytes, int pageIndex, int dpi, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // PDFtoImage.Conversion.ToImage est marqué "supported on Android 31+/Linux/macOS/Windows",
            // ce qui couvre tous les environnements de déploiement FactuTrust. CA1416 suppress.
#pragma warning disable CA1416
            using var bitmap = Conversion.ToImage(pdfBytes, page: (Index)pageIndex, options: new(Dpi: dpi));
#pragma warning restore CA1416
            if (bitmap is null)
            {
                return Task.FromResult<AiRenderedPage?>(null);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            var png = data.ToArray();
            return Task.FromResult<AiRenderedPage?>(new AiRenderedPage(png, bitmap.Width, bitmap.Height));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF page render failed (pageIndex={PageIndex} dpi={Dpi})", pageIndex, dpi);
            return Task.FromResult<AiRenderedPage?>(null);
        }
    }
}
