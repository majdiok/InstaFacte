namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Pièce jointe envoyée avec un message utilisateur. Le texte extrait est déjà
/// inclus dans <see cref="AiChatHttpRequestDto.Message"/> par le frontend ; cette
/// entrée transporte les images base64 in-flight (non persistées) afin de les
/// pousser au modèle si celui-ci supporte la vision.
/// </summary>
public sealed record ChatAttachmentInput
{
    public string FileName { get; init; } = "";
    public string Format { get; init; } = "";
    public int PageCount { get; init; }
    public bool OcrApplied { get; init; }
    public bool Truncated { get; init; }
    /// <summary>PNG/JPG base64 (sans préfixe data:). Une entrée par page rendue.</summary>
    public IReadOnlyList<string>? ImagesBase64 { get; init; }
}
