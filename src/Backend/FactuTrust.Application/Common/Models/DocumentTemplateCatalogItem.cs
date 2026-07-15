using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Models;

/// <summary>
/// Décrit un modèle visuel disponible dans le catalogue (pour l'écran de configuration et l'aperçu).
/// </summary>
public sealed record DocumentTemplateCatalogItem(
    string Key,
    string Name,
    string Description,
    string ThumbnailAsset,
    IReadOnlyList<PrintableDocumentType> SupportedDocumentTypes);
