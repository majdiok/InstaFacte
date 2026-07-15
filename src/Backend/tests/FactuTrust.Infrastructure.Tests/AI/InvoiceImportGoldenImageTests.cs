using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Image synthétique : vérifie le pipeline OCR (mock) + conservation base64 pour vision.
/// </summary>
public sealed class InvoiceImportGoldenImageTests
{
    [Fact]
    public async Task ExtractAsync_SyntheticPng_WithOcr_ReturnsTextAndImagePayload()
    {
        var bytes = BuildSyntheticInvoicePng();
        using var stream = new MemoryStream(bytes);

        var ocr = new Mock<IAiOcrService>();
        ocr.SetupGet(x => x.IsAvailable).Returns(true);
        ocr.Setup(x => x.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("BON DE LIVRAISON 00396 Client Test Total 650");

        var preprocessing = new Mock<IImagePreprocessingService>();
        preprocessing.Setup(x => x.PreprocessForOcr(It.IsAny<byte[]>())).Returns<byte[]>(b => b);

        var extractor = new AiDocumentTextExtractor(
            NullLogger<AiDocumentTextExtractor>.Instance,
            Mock.Of<IAiPdfRenderer>(),
            ocr.Object,
            preprocessing.Object);

        var result = await extractor.ExtractAsync(
            stream, "bl-test.png", "image/png", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("image", result.Format);
        Assert.True(result.OcrApplied);
        Assert.Contains("BON DE LIVRAISON", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Pages[0].ImageBase64);
    }

    [Fact]
    public async Task ExtractAsync_OcrEmpty_StillSucceedsWithImageForVisionFallback()
    {
        var bytes = BuildSyntheticInvoicePng();
        using var stream = new MemoryStream(bytes);

        var ocr = new Mock<IAiOcrService>();
        ocr.SetupGet(x => x.IsAvailable).Returns(true);
        ocr.Setup(x => x.RecognizeAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var preprocessing = new Mock<IImagePreprocessingService>();
        preprocessing.Setup(x => x.PreprocessForOcr(It.IsAny<byte[]>())).Returns<byte[]>(b => b);

        var extractor = new AiDocumentTextExtractor(
            NullLogger<AiDocumentTextExtractor>.Instance,
            Mock.Of<IAiPdfRenderer>(),
            ocr.Object,
            preprocessing.Object);

        var result = await extractor.ExtractAsync(
            stream, "bl-empty-ocr.png", "image/png", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Text);
        Assert.NotNull(result.Pages[0].ImageBase64);
        Assert.Contains(result.Warnings, w => w.Contains("vision", StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] BuildSyntheticInvoicePng()
    {
        using var bitmap = new SKBitmap(400, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
