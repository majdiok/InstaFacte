using FactuTrust.Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Idempotently inserts official TEJ system withholding types when missing (by unique <see cref="Domain.Entities.WithholdingTaxType.Code"/>).
/// </summary>
public static class WithholdingTaxCatalogInitializer
{
    /// <returns><c>true</c> when at least one new system type was inserted, otherwise <c>false</c>.</returns>
    public static async Task<bool> EnsureSystemTypesSeededAsync(TenantDbContext context, CancellationToken cancellationToken = default)
    {
        var existingCodes = await context.WithholdingTaxTypes
            .AsNoTracking()
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(existingCodes, StringComparer.Ordinal);
        var added = false;

        foreach (var type in WithholdingTaxTypeSeed.GetSystemTypes())
        {
            if (existing.Contains(type.Code))
                continue;

            context.WithholdingTaxTypes.Add(type);
            existing.Add(type.Code);
            added = true;
        }

        if (added)
            await context.SaveChangesAsync(cancellationToken);

        return added;
    }
}
