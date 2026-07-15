using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.BankAccounts;

internal static class BankAccountMapping
{
    public static BankAccountDto ToDto(this BankAccount b) => new()
    {
        Id = b.Id,
        CompanyId = b.CompanyId,
        BankCode = b.BankCode,
        BankName = b.BankName,
        Designation = b.Designation,
        AgencyName = b.AgencyName,
        Rib = b.Rib,
        Iban = b.Iban,
        SwiftBic = b.SwiftBic,
        IsDefault = b.IsDefault,
        IsActive = b.IsActive,
        ChartOfAccountNumber = b.ChartOfAccountNumber,
        Currency = b.Currency
    };
}
