using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>Idempotent insertion of NCT 01 withholding accounts used by <see cref="Services.AccountingService"/>.</summary>
public static class WithholdingChartAccountsInitializer
{
    private static readonly (string Number, string Label)[] Accounts =
    [
        ("4320", "Retenue à la source — divers"),
        ("4321", "Retenue RS1 (loyers)"),
        ("4322", "Retenue RS2 (honoraires)"),
        ("4323", "Retenue RS3 (revenus de capitaux)"),
        ("4324", "Retenue RS4 (dividendes)"),
        ("4325", "Retenue RS5 (plus-values)"),
        ("4326", "Retenue RS6 (immobilier foncier)"),
        ("4327", "Retenue RS7 (achats)")
    ];

    public static async Task EnsureAccountsAsync(TenantDbContext context, CancellationToken cancellationToken = default)
    {
        var existingNumbers = await context.ChartOfAccounts
            .AsNoTracking()
            .Select(a => a.AccountNumber)
            .ToListAsync(cancellationToken);
        var existing = existingNumbers.ToHashSet(StringComparer.Ordinal);

        foreach (var (number, label) in Accounts)
        {
            if (existing.Contains(number))
                continue;

            var row = ChartOfAccount.Create(
                number,
                label,
                accountClass: 4,
                parentAccountNumber: "432",
                natureType: AccountNatureType.Credit,
                isSystem: true);

            if (row.IsFailure)
                continue;

            context.ChartOfAccounts.Add(row.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
