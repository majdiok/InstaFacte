using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Mode de paiement enregistré dans un reçu plateforme.</summary>
public enum PlatformPaymentMethod
{
    BankTransfer = 0,
    CardKonnect = 1,
    CardPaymee = 2,
    Cash = 3,
    Manual = 4
}

/// <summary>Statut d'un reçu (paiement) plateforme.</summary>
public enum PlatformReceiptStatus
{
    /// <summary>Saisi mais non confirmé (ex. virement à valider).</summary>
    Pending = 0,
    /// <summary>Confirmé / encaissé. Compte dans le total payé.</summary>
    Confirmed = 1,
    /// <summary>Annulé manuellement (erreur).</summary>
    Cancelled = 2
}

/// <summary>
/// Lot C4 — Reçu / encaissement d'une facture plateforme.
///
/// Chaque reçu est rattaché à une <see cref="PlatformInvoice"/>. Numéroté
/// séquentiellement (<c>FT-RC-2026-000045</c>). Plusieurs reçus partiels possibles
/// par facture ; la facture passe à <c>Paid</c> quand la somme des reçus confirmés
/// atteint <c>TotalTTC</c>.
/// </summary>
public sealed class PlatformReceipt : Entity
{
    public Guid InvoiceId { get; private set; }
    public string ReceiptNumber { get; private set; } = null!;
    public int? SequenceYear { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public PlatformPaymentMethod Method { get; private set; }
    public string? Reference { get; private set; }
    public decimal AmountTND { get; private set; }
    public string? ProviderTxId { get; private set; }
    public string? ProviderPayload { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    public string? PdfStorageKey { get; private set; }
    public PlatformReceiptStatus Status { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelledReason { get; private set; }

    private PlatformReceipt() { }

    public static PlatformReceipt Create(
        Guid invoiceId,
        string receiptNumber,
        int sequenceYear,
        DateTime paymentDate,
        PlatformPaymentMethod method,
        decimal amountTND,
        Guid? receivedByUserId,
        string? reference = null,
        string? providerTxId = null,
        string? providerPayload = null,
        bool autoConfirm = true)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId requis", nameof(invoiceId));
        if (string.IsNullOrWhiteSpace(receiptNumber)) throw new ArgumentException("Numéro requis", nameof(receiptNumber));
        if (amountTND <= 0) throw new ArgumentOutOfRangeException(nameof(amountTND));

        var r = new PlatformReceipt
        {
            InvoiceId = invoiceId,
            ReceiptNumber = receiptNumber.Trim(),
            SequenceYear = sequenceYear,
            PaymentDate = DateTime.SpecifyKind(paymentDate.Date, DateTimeKind.Utc),
            Method = method,
            AmountTND = Math.Round(amountTND, 3),
            ReceivedByUserId = receivedByUserId,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            ProviderTxId = string.IsNullOrWhiteSpace(providerTxId) ? null : providerTxId.Trim(),
            ProviderPayload = providerPayload,
            Status = autoConfirm ? PlatformReceiptStatus.Confirmed : PlatformReceiptStatus.Pending,
            ConfirmedAt = autoConfirm ? DateTime.UtcNow : null
        };
        return r;
    }

    public void Confirm()
    {
        if (Status == PlatformReceiptStatus.Confirmed) return;
        if (Status == PlatformReceiptStatus.Cancelled)
            throw new InvalidOperationException("Reçu annulé non re-confirmable.");
        Status = PlatformReceiptStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (Status == PlatformReceiptStatus.Cancelled) return;
        Status = PlatformReceiptStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelledReason = (reason ?? string.Empty).Trim();
    }

    public void AttachPdf(string key) => PdfStorageKey = key;
}
