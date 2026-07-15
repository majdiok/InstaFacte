using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Résout la clé de modèle visuel effective pour un type de document, en tenant compte :
/// 1) d'une éventuelle surcharge ponctuelle (paramètre d'impression),
/// 2) de la préférence persistée du tenant,
/// 3) à défaut, du modèle par défaut (rendu historique).
/// </summary>
public interface IDocumentTemplateResolver
{
    Task<string> ResolveAsync(
        PrintableDocumentType documentType,
        string? overrideKey,
        CancellationToken cancellationToken = default);
}
