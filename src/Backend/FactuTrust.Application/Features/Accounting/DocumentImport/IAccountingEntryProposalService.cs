using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.DocumentImport;

/// <summary>
/// Transforme une pièce extraite en proposition d'écriture, selon le plan comptable tunisien.
///
/// Ce service est en LECTURE SEULE : il consulte le plan comptable, les tiers, les périodes et
/// les écritures existantes, mais n'écrit jamais. En particulier, il ne crée pas de sous-compte
/// manquant (contrairement à la comptabilisation automatique) : un compte absent est signalé.
/// </summary>
public interface IAccountingEntryProposalService
{
    /// <param name="directionOverride">
    /// "SALE" ou "PURCHASE" pour forcer le sens quand l'utilisateur corrige la détection.
    /// </param>
    Task<Result<JournalEntryProposalDto>> ProposeAsync(
        AccountingDocumentExtractionDto document,
        string? directionOverride,
        CancellationToken cancellationToken);
}
