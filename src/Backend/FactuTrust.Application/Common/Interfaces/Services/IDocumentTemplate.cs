using FactuTrust.Application.Common.Models;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Un modèle visuel d'impression. Rend un <see cref="DocumentRenderModel"/> en PDF.
/// Les implémentations doivent être pures (aucune I/O) et déterministes.
/// </summary>
public interface IDocumentTemplate
{
    /// <summary>Clé stable du modèle (ex. "classic-tva-synthese"). Persistée dans la préférence.</summary>
    string Key { get; }

    byte[] Render(DocumentRenderModel model);
}
