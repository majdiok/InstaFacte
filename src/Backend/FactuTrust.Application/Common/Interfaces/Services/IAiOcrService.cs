namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Reconnaissance optique de caractères sur une image PNG/JPG. Utilisé pour les PDF
/// scannés (sans glyphes natifs) et les pièces jointes images. L'implémentation par
/// défaut (Tesseract) doit être disponible localement avec les fichiers de langue
/// embarqués (fra, eng, ara) dans Resources/Tessdata.
/// </summary>
public interface IAiOcrService
{
    bool IsAvailable { get; }

    /// <summary>
    /// Reconnaît le texte de l'image fournie. Renvoie une chaîne vide si l'OCR n'est
    /// pas disponible, si l'image est illisible ou si aucun caractère n'est détecté.
    /// </summary>
    /// <param name="imageBytes">Image PNG/JPG en mémoire.</param>
    /// <param name="languages">Liste Tesseract des langues, ex: "fra+eng+ara". Si null, utilise la valeur par défaut configurée.</param>
    Task<string> RecognizeAsync(byte[] imageBytes, string? languages = null, CancellationToken cancellationToken = default);
}
