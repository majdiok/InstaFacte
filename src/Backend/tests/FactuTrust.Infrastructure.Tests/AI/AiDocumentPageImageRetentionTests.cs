using FactuTrust.Infrastructure.Services.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Rétention du PNG de page PDF.
///
/// Avant correctif, l'image rasterisée POUR L'OCR était systématiquement jetée dès que le modèle
/// texte principal n'était pas multimodal. Un PDF scanné ressortait donc sans aucune image, et le
/// repli vision devenait structurellement inatteignable — alors que l'image existait déjà.
/// </summary>
public sealed class AiDocumentPageImageRetentionTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// Garde-fou de performance : sur un PDF à couche texte, aucune page n'est rasterisée, donc
    /// aucun encodage base64 n'a lieu, quels que soient les drapeaux.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NativeTextPdf_NeverProducesAnImage(bool renderImages, bool keepOcrImages)
    {
        Assert.Null(AiDocumentTextExtractor.BuildPageImageBase64(null, renderImages, keepOcrImages));
    }

    [Fact]
    public void OcrRenderedPage_IsKept_WhenRetentionEnabled()
    {
        var base64 = AiDocumentTextExtractor.BuildPageImageBase64(Png, renderImages: false, keepOcrImages: true);

        Assert.Equal(Convert.ToBase64String(Png), base64);
    }

    /// <summary>
    /// Comportement historique préservé pour les appelants qui ne posent pas le drapeau
    /// (notamment /api/ai/document-extract) : charge utile identique au bit près.
    /// </summary>
    [Fact]
    public void OcrRenderedPage_IsDropped_WhenRetentionDisabled()
    {
        Assert.Null(AiDocumentTextExtractor.BuildPageImageBase64(Png, renderImages: false, keepOcrImages: false));
    }

    [Fact]
    public void VisionRequested_KeepsTheImage_RegardlessOfRetentionFlag()
    {
        Assert.NotNull(AiDocumentTextExtractor.BuildPageImageBase64(Png, renderImages: true, keepOcrImages: false));
        Assert.NotNull(AiDocumentTextExtractor.BuildPageImageBase64(Png, renderImages: true, keepOcrImages: true));
    }
}
