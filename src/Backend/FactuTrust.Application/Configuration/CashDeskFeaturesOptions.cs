namespace FactuTrust.Application.Configuration;

public sealed class CashDeskFeaturesOptions
{
    public const string SectionName = "Features";

    public bool AutoCashFromInvoicePayment { get; set; }

    /// <summary>
    /// When true, recording a supplier invoice payment in cash creates a matching cash desk debit operation.
    /// </summary>
    public bool AutoCashFromSupplierPayment { get; set; }

    /// <summary>
    /// When true, POS sales require an open cash-register session (vacation).
    /// Cart and held tickets are always persisted in the tenant database.
    /// </summary>
    public bool PosRegisterSessions { get; set; } = true;
}
