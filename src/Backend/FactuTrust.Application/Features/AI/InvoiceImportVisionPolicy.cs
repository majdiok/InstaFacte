using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Décide si l'import documentaire doit utiliser le modèle vision (photos, OCR insuffisant).
/// </summary>
public static class InvoiceImportVisionPolicy
{
    public static bool ShouldUseVisionFallback(
        OllamaSettings settings,
        bool forceVision,
        AiDocumentExtractionResult extraction,
        string extractedText,
        int sourceImageCount,
        string? visionModelId)
    {
        if (string.IsNullOrWhiteSpace(visionModelId) || sourceImageCount == 0)
            return false;

        if (forceVision)
            return true;

        var isImage = string.Equals(extraction.Format, "image", StringComparison.OrdinalIgnoreCase);
        var isOcrPdf = extraction.OcrApplied
            && string.Equals(extraction.Format, "pdf", StringComparison.OrdinalIgnoreCase);
        if (!isImage && !isOcrPdf)
            return false;

        if (isImage && settings.InvoiceImportVisionOnImages)
            return true;

        var minChars = Math.Max(0, settings.InvoiceImportVisionMinOcrChars);
        if (settings.InvoiceImportVisionOnEmptyOcr && extractedText.Length == 0)
            return true;

        return !InvoiceImportOcrQuality.IsSufficient(extractedText, minChars, 0.35);
    }
}
