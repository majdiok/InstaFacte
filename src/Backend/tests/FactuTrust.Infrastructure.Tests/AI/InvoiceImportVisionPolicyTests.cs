using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class InvoiceImportVisionPolicyTests
{
    private static OllamaSettings DefaultSettings() => new()
    {
        InvoiceImportVisionOnImages = true,
        InvoiceImportVisionOnEmptyOcr = true,
        InvoiceImportVisionMinOcrChars = 80
    };

    private static AiDocumentExtractionResult ImageExtraction(string text) => new()
    {
        Success = true,
        Text = text,
        Format = "image",
        OcrApplied = true,
        PageCount = 1,
        Pages =
        [
            new AiDocumentExtractedPage
            {
                PageIndex = 0,
                Text = text,
                ImageBase64 = "aW1hZ2U=",
                OcrApplied = true
            }
        ]
    };

    [Fact]
    public void ShouldUseVisionFallback_image_with_sufficient_ocr_uses_vision_when_on_images_enabled()
    {
        var text = "FACTURE n° 68 Client Mohamed Total TTC 279000 TVA 7% Désignation prix quantité "
                   + "matricule fiscal timbre fiscal base imposable montant";

        var useVision = InvoiceImportVisionPolicy.ShouldUseVisionFallback(
            DefaultSettings(),
            forceVision: false,
            ImageExtraction(text),
            text,
            sourceImageCount: 1,
            visionModelId: "gemma3:4b");

        Assert.True(useVision);
    }

    [Fact]
    public void ShouldUseVisionFallback_image_respects_on_images_disabled()
    {
        var settings = DefaultSettings();
        settings.InvoiceImportVisionOnImages = false;

        var text = "FACTURE n° 68 Client Mohamed Total TTC 279000 TVA 7% Désignation prix quantité "
                   + "matricule fiscal timbre fiscal base imposable montant";

        var useVision = InvoiceImportVisionPolicy.ShouldUseVisionFallback(
            settings,
            forceVision: false,
            ImageExtraction(text),
            text,
            sourceImageCount: 1,
            visionModelId: "gemma3:4b");

        Assert.False(useVision);
    }

    [Fact]
    public void ShouldUseVisionFallback_force_vision_overrides_text_path()
    {
        var useVision = InvoiceImportVisionPolicy.ShouldUseVisionFallback(
            DefaultSettings(),
            forceVision: true,
            ImageExtraction("x"),
            "x",
            sourceImageCount: 1,
            visionModelId: "gemma3:4b");

        Assert.True(useVision);
    }

    [Fact]
    public void ShouldUseVisionFallback_pdf_text_only_returns_false_without_ocr()
    {
        var extraction = new AiDocumentExtractionResult
        {
            Success = true,
            Text = "texte pdf natif",
            Format = "pdf",
            OcrApplied = false,
            PageCount = 1
        };

        var useVision = InvoiceImportVisionPolicy.ShouldUseVisionFallback(
            DefaultSettings(),
            forceVision: false,
            extraction,
            extraction.Text!,
            sourceImageCount: 0,
            visionModelId: "gemma3:4b");

        Assert.False(useVision);
    }

    [Fact]
    public void ShouldUseVisionFallback_no_vision_model_returns_false()
    {
        var useVision = InvoiceImportVisionPolicy.ShouldUseVisionFallback(
            DefaultSettings(),
            forceVision: false,
            ImageExtraction("facture"),
            "facture",
            sourceImageCount: 1,
            visionModelId: null);

        Assert.False(useVision);
    }
}
