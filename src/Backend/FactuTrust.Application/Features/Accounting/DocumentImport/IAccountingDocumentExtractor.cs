using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.DocumentImport;

/// <summary>Fichier soumis à l'extraction d'une pièce comptable.</summary>
public sealed record AccountingDocumentExtractionRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }

    /// <summary>Modèle IA explicite. Sans effet lorsque le parseur natif reconnaît la pièce.</summary>
    public string? ModelOverride { get; init; }

    /// <summary>
    /// Quand faux, seul le parseur natif est tenté : l'utilisateur n'a pas la permission d'usage de l'IA.
    /// </summary>
    public bool AllowAiFallback { get; init; } = true;
}

/// <summary>
/// Extrait une pièce commerciale en deux passes : parseur déterministe du gabarit InstaFact,
/// puis, si la pièce n'est pas reconnue ou ne se réconcilie pas, extraction IA (OCR + LLM).
/// </summary>
public interface IAccountingDocumentExtractor
{
    Task<Result<AccountingDocumentExtractionDto>> ExtractAsync(
        AccountingDocumentExtractionRequest request,
        CancellationToken cancellationToken);
}
