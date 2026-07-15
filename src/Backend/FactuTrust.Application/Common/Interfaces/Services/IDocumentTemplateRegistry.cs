using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Registre central des modèles visuels disponibles + catalogue pour l'UI.
/// </summary>
public interface IDocumentTemplateRegistry
{
    /// <summary>Clé du modèle par défaut (= rendu historique). Toujours valide.</summary>
    string DefaultKey { get; }

    /// <summary>Vrai si la clé correspond au modèle par défaut (ou est vide/inconnue).</summary>
    bool IsDefault(string? key);

    /// <summary>Vrai si la clé existe dans le registre.</summary>
    bool Exists(string key);

    /// <summary>Récupère le modèle pour une clé. Retourne le modèle par défaut si la clé est inconnue.</summary>
    IDocumentTemplate Get(string? key);

    /// <summary>Catalogue complet (clé, nom, description, vignette, types supportés).</summary>
    IReadOnlyList<DocumentTemplateCatalogItem> Catalog { get; }

    /// <summary>Catalogue filtré pour un type de document donné.</summary>
    IReadOnlyList<DocumentTemplateCatalogItem> CatalogFor(PrintableDocumentType documentType);
}
