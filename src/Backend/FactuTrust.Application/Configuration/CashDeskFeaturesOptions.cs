namespace FactuTrust.Application.Configuration;

public sealed class CashDeskFeaturesOptions
{
    public const string SectionName = "Features";

    public bool AutoCashFromInvoicePayment { get; set; }

    /// <summary>
    /// When true, recording a supplier invoice payment in cash creates a matching cash desk debit operation.
    /// </summary>
    public bool AutoCashFromSupplierPayment { get; set; }
}
