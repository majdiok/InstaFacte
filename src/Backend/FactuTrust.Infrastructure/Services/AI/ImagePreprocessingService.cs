using FactuTrust.Application.Common.Interfaces.Services;
using SkiaSharp;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Prépare les photos de documents avant OCR Tesseract.
/// </summary>
public sealed class ImagePreprocessingService : IImagePreprocessingService
{
    private const int MaxEdgePx = 2000;

    public byte[] PreprocessForOcr(byte[] imageBytes)
    {
        if (imageBytes is null or { Length: 0 })
            return imageBytes ?? Array.Empty<byte>();

        using var original = SKBitmap.Decode(imageBytes);
        if (original is null)
            return imageBytes;

        var maxEdge = Math.Max(original.Width, original.Height);
        var targetW = original.Width;
        var targetH = original.Height;
        if (maxEdge > MaxEdgePx)
        {
            var scale = MaxEdgePx / (float)maxEdge;
            targetW = Math.Max(1, (int)Math.Round(original.Width * scale));
            targetH = Math.Max(1, (int)Math.Round(original.Height * scale));
        }

        using var surface = SKSurface.Create(new SKImageInfo(targetW, targetH, SKColorType.Gray8, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        var dest = new SKRect(0, 0, targetW, targetH);
        canvas.DrawBitmap(original, dest, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
