namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Rend une page PDF en image bitmap PNG. Utilisé par le pipeline d'extraction IA
/// pour (1) appliquer un OCR sur les PDF scannés et (2) fournir les pages à un modèle vision.
/// </summary>
public interface IAiPdfRenderer
{
    bool IsAvailable { get; }

    /// <summary>
    /// Rend la page spécifiée en PNG. Renvoie null si le rendu échoue (PDF protégé, libpdfium absent…).
    /// </summary>
    /// <param name="pdfBytes">Contenu binaire complet du PDF.</param>
    /// <param name="pageIndex">Index 0-based de la page.</param>
    /// <param name="dpi">Résolution de rendu (typiquement 150-200 pour OCR/vision).</param>
    Task<AiRenderedPage?> RenderPageAsync(byte[] pdfBytes, int pageIndex, int dpi, CancellationToken cancellationToken = default);
}

public sealed record AiRenderedPage(byte[] PngBytes, int Width, int Height);
