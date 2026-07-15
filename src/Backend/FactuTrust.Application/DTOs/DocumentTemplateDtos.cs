namespace FactuTrust.Application.DTOs;

/// <summary>Préférence de modèle visuel d'un type de document.</summary>
public sealed record DocumentTemplatePreferenceDto(
    string DocumentType,
    int DocumentTypeValue,
    string DocumentTypeLabel,
    string TemplateKey,
    string TemplateName);

/// <summary>Élément du catalogue de modèles visuels.</summary>
public sealed record DocumentTemplateCatalogItemDto(
    string Key,
    string Name,
    string Description,
    string Thumbnail,
    IReadOnlyList<string> SupportedDocumentTypes);

/// <summary>Requête d'enregistrement du modèle choisi pour un type de document.</summary>
public sealed class SaveDocumentTemplateRequest
{
    public string TemplateKey { get; set; } = string.Empty;
}
