using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Registre des modèles visuels (alimenté par DI) + catalogue pour l'UI. Seuls les modèles
/// effectivement enregistrés apparaissent dans le catalogue.
/// </summary>
public sealed class DocumentTemplateRegistry : IDocumentTemplateRegistry
{
    private static readonly PrintableDocumentType[] AllTypes = Enum.GetValues<PrintableDocumentType>();

    private readonly Dictionary<string, IDocumentTemplate> _byKey;
    private readonly IReadOnlyList<DocumentTemplateCatalogItem> _catalog;

    public DocumentTemplateRegistry(IEnumerable<IDocumentTemplate> templates)
    {
        _byKey = templates.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        _catalog = BuildCatalogMetadata()
            .Where(c => _byKey.ContainsKey(c.Key))
            .ToList();
    }

    public string DefaultKey => StandardDocumentTemplate.TemplateKey;

    public bool IsDefault(string? key) =>
        string.IsNullOrWhiteSpace(key)
        || key.Equals(DefaultKey, StringComparison.OrdinalIgnoreCase)
        || !_byKey.ContainsKey(key);

    public bool Exists(string key) =>
        !string.IsNullOrWhiteSpace(key) && _byKey.ContainsKey(key);

    public IDocumentTemplate Get(string? key) =>
        !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out var template)
            ? template
            : _byKey[DefaultKey];

    public IReadOnlyList<DocumentTemplateCatalogItem> Catalog => _catalog;

    public IReadOnlyList<DocumentTemplateCatalogItem> CatalogFor(PrintableDocumentType documentType) =>
        _catalog.Where(c => c.SupportedDocumentTypes.Contains(documentType)).ToList();

    private static IEnumerable<DocumentTemplateCatalogItem> BuildCatalogMetadata()
    {
        yield return new DocumentTemplateCatalogItem(
            StandardDocumentTemplate.TemplateKey, "Standard", "Modèle maison (bandeau bleu, ventilation TVA, totaux).",
            "templates/standard.png", AllTypes);

        yield return new DocumentTemplateCatalogItem(
            "classic-tva-synthese", "Classique TVA / Synthèse",
            "Titre coloré, bandeau orange, blocs « Détails de la TVA » et « Synthèse ».",
            "templates/classic-tva-synthese.png", AllTypes);

        yield return new DocumentTemplateCatalogItem(
            "orange-table", "Tableau Orange",
            "En-tête société, panneau destinataire encadré, tableau orange, totaux à droite.",
            "templates/orange-table.png", AllTypes);

        yield return new DocumentTemplateCatalogItem(
            "orange-table-compact", "Tableau Orange (compact)",
            "Variante allégée du tableau orange, sans ventilation TVA détaillée.",
            "templates/orange-table-compact.png", AllTypes);

        yield return new DocumentTemplateCatalogItem(
            "modern-boxed", "Moderne Encadré",
            "Bandeaux d'information encadrés, tableau bordé, style moderne.",
            "templates/modern-boxed.png", AllTypes);

        yield return new DocumentTemplateCatalogItem(
            "modern-boxed-payments", "Moderne Encadré + Règlements",
            "Variante moderne avec ventilation TVA et tableau des règlements.",
            "templates/modern-boxed-payments.png", AllTypes);
    }
}
