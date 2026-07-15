namespace FactuTrust.Application.DTOs;

/// <summary>Méthode utilisée lors de l'aperçu d'un fichier de relevé (PDF/image).</summary>
public enum BankStatementExtractionMethod
{
    Unknown = 0,
    TextParser = 1,
    OcrTextParser = 2,
    OcrLlm = 3
}
