namespace FactuTrust.Domain.Enums;

/// <summary>
/// Discriminates a regular invoice from a credit note (facture d'avoir).
/// Persisted on Invoice and InvoiceDraft, drives numbering prefix (FAC/AVO),
/// totals signing, stock direction, and accounting routing.
/// </summary>
public enum InvoiceType
{
    /// <summary>Regular sales invoice (FAC). Positive totals.</summary>
    Standard = 0,

    /// <summary>Credit note / facture d'avoir (AVO). Negative totals — refund/reversal.</summary>
    CreditNote = 1
}
