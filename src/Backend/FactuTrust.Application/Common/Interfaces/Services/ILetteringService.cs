using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface ILetteringService
{
    /// <summary>
    /// Lettre manuellement des lignes d'un même compte. Équilibre débit/crédit exigé, sauf si
    /// <paramref name="allowPartial"/> : le groupe est alors marqué partiel (code préfixé « P »).
    /// </summary>
    Task<Result> ManualLetterAsync(IReadOnlyList<Guid> journalEntryLineIds, bool allowPartial = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Délettre un groupe : libère les lignes (LetteringCode remis à null) et supprime le groupe.
    /// Un lettrage partiel se complète en délettrant puis relettrant l'ensemble soldé.
    /// </summary>
    Task<Result> UnletterAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to automatically letter a payment journal entry against the corresponding
    /// invoice journal entry on the third-party account (4111 for clients, 4011 for suppliers).
    /// Silently succeeds if lettering is not possible (partial payment, already lettered, etc.).
    /// </summary>
    Task<Result> AutoLetterPaymentAsync(
        string sourceEntityType,
        Guid sourceEntityId,
        string invoiceSourceType,
        Guid invoiceSourceId,
        CancellationToken cancellationToken = default);
}
