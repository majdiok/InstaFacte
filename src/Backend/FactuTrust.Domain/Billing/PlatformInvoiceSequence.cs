using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C4 — Séquence de numérotation des documents fiscaux plateforme par année.
///
/// Garantit la <b>continuité stricte</b> imposée par le DGI tunisien : pas de saut, pas
/// de doublon, pas de remise à zéro hors changement d'année. La concurrence est gérée
/// via <see cref="RowVersion"/> + transaction <c>Serializable</c> côté
/// <c>PlatformInvoiceSequenceService</c>.
///
/// <para>Une ligne par <c>(DocumentType, Year)</c>. <c>DocumentType</c> = "Invoice" | "Receipt"
/// | "CreditNote".</para>
/// </summary>
public sealed class PlatformInvoiceSequence : Entity
{
    public string DocumentType { get; private set; } = null!;
    public int Year { get; private set; }
    public int NextNumber { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PlatformInvoiceSequence() { }

    public static PlatformInvoiceSequence Create(string documentType, int year, int startAt = 1)
    {
        if (string.IsNullOrWhiteSpace(documentType)) throw new ArgumentException("DocumentType requis", nameof(documentType));
        if (year < 2000 || year > 2200) throw new ArgumentOutOfRangeException(nameof(year));
        if (startAt < 1) throw new ArgumentOutOfRangeException(nameof(startAt));

        return new PlatformInvoiceSequence
        {
            DocumentType = documentType.Trim(),
            Year = year,
            NextNumber = startAt
        };
    }

    /// <summary>Réserve le prochain numéro et l'incrémente. À appeler dans une transaction Serializable.</summary>
    public int Reserve()
    {
        var assigned = NextNumber;
        NextNumber = assigned + 1;
        return assigned;
    }
}
