namespace FactuTrust.Domain.Enums;

/// <summary>
/// Category of configurable tax (TVA, timbre, FODEC, etc.).
/// </summary>
public enum TaxType
{
    VAT = 0,
    Stamp = 1,
    FODEC = 2,
    Consumption = 3,
    Other = 99
}

/// <summary>
/// How the tax amount is computed.
/// </summary>
public enum TaxValueType
{
    Percentage = 0,
    FixedAmount = 1
}

/// <summary>
/// Document scope for the tax.
/// </summary>
public enum TaxContext
{
    All = 0,
    Sales = 1,
    Purchases = 2
}
