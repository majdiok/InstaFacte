namespace FactuTrust.Domain.Enums;

/// <summary>
/// Discriminates tenant purpose in the multi-tenant platform.
/// </summary>
public enum TenantKind
{
    /// <summary>Standard company using the full ERP.</summary>
    Company = 0,

    /// <summary>Accounting firm managing delegated client dossiers.</summary>
    AccountingFirm = 1
}

public static class TenantKindExtensions
{
    public static string ToClaimValue(this TenantKind kind) => kind switch
    {
        TenantKind.Company => "company",
        TenantKind.AccountingFirm => "accountingFirm",
        _ => "company"
    };

    public static TenantKind FromClaimValue(string? value) => value switch
    {
        "accountingFirm" => TenantKind.AccountingFirm,
        _ => TenantKind.Company
    };

    /// <summary>Parses API/claim string values in PascalCase or camelCase.</summary>
    public static TenantKind FromApiValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return TenantKind.Company;

        return value.Trim().ToLowerInvariant() switch
        {
            "accountingfirm" or "accounting_firm" => TenantKind.AccountingFirm,
            "company" => TenantKind.Company,
            "1" => TenantKind.AccountingFirm,
            "0" => TenantKind.Company,
            _ => TenantKind.Company
        };
    }
}
