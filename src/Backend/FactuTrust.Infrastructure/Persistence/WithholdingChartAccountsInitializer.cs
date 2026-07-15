using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>Idempotent insertion of 4456x withholding accounts used by <see cref="Services.AccountingService"/>.</summary>
public static class WithholdingChartAccountsInitializer
{
    private static readonly (string Number, string Label)[] Accounts =
    [
        ("44560", "Retenue à la source — divers"),
        ("44561", "Retenue RS1 (loyers)"),
        ("44562", "Retenue RS2 (honoraires)"),
        ("44563", "Retenue RS3 (revenus de capitaux)"),
        ("44564", "Retenue RS7 (achats)"),
        ("44565", "Retenue RS4 (dividendes)"),
        ("44566", "Retenue RS5 (plus-values)"),
        ("44567", "Retenue RS6 (immobilier foncier)")
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
                parentAccountNumber: null,
                natureType: AccountNatureType.Credit,
                isSystem: true);

            if (row.IsFailure)
                continue;

            context.ChartOfAccounts.Add(row.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
