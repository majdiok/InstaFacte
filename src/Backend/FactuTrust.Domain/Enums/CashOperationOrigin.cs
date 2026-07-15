namespace FactuTrust.Domain.Enums;

/// <summary>
/// Source origin for a cash operation.
/// </summary>
public enum CashOperationOrigin
{
    /// <summary>
    /// Operation entered manually from the cash desk module.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Operation generated automatically from an invoice payment.
    /// </summary>
    InvoicePayment = 1,

    /// <summary>
    /// Operation generated automatically from a supplier invoice payment.
    /// </summary>
    SupplierPayment = 2
}
