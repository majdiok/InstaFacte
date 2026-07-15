using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Vérifie l'extraction PdfPig sur un PDF facture minimal (non régression layout).
/// </summary>
public sealed class InvoiceImportGoldenPdfTests
{
    static InvoiceImportGoldenPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task ExtractAsync_GoldenInvoicePdf_ProducesReadableText()
    {
        var bytes = BuildMinimalInvoicePdf();
        using var stream = new MemoryStream(bytes);

        var preprocessing = new Mock<IImagePreprocessingService>();
        preprocessing.Setup(x => x.PreprocessForOcr(It.IsAny<byte[]>())).Returns<byte[]>(b => b);

        var extractor = new AiDocumentTextExtractor(
            NullLogger<AiDocumentTextExtractor>.Instance,
            BuildPdfRendererStub().Object,
            BuildOcrStub().Object,
            preprocessing.Object);

        var result = await extractor.ExtractAsync(
            stream,
            "FAC-2026-000093.pdf",
            "application/pdf",
            new AiDocumentExtractOptions { RenderPagesAsImages = false },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("pdf", result.Format);
        Assert.False(result.OcrApplied);
        Assert.True(result.Text.Length >= 50);
        Assert.Contains("FAC-2026-000093", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Client Test", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildMinimalInvoicePdf()
    {
        using var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Content().Column(col =>
                {
                    col.Item().Text("FACTURE FAC-2026-000093");
                    col.Item().Text("Date facture : 09/05/2026");
                    col.Item().Text("Client Test SARL");
                    col.Item().Text("Désignation : Prestation conseil");
                    col.Item().Text("Total TTC : 1190.000 TND");
                });
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }

    private static Mock<IAiPdfRenderer> BuildPdfRendererStub()
    {
        var m = new Mock<IAiPdfRenderer>(MockBehavior.Loose);
        m.SetupGet(x => x.IsAvailable).Returns(false);
        return m;
    }

    private static Mock<IAiOcrService> BuildOcrStub()
    {
        var m = new Mock<IAiOcrService>(MockBehavior.Loose);
        m.SetupGet(x => x.IsAvailable).Returns(false);
        return m;
    }
}
