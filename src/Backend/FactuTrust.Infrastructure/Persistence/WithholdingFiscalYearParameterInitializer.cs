using FactuTrust.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Insère les paramètres fiscaux RS par exercice lorsque absents (seuil RS7 = 1000 TND par défaut).
/// </summary>
public static class WithholdingFiscalYearParameterInitializer
{
    public static async Task EnsureDefaultsSeededAsync(TenantDbContext context, CancellationToken cancellationToken = default)
    {
        var existing = await context.WithholdingFiscalYearParameters
            .AsNoTracking()
            .Select(p => p.FiscalYear)
            .ToListAsync(cancellationToken);

        var set = new HashSet<int>(existing);
        var added = false;
        const decimal defaultRs7Threshold = 1000m;

        for (var year = 2018; year <= 2040; year++)
        {
            if (set.Contains(year))
                continue;

            context.WithholdingFiscalYearParameters.Add(
                WithholdingFiscalYearParameter.Create(year, defaultRs7Threshold));
            set.Add(year);
            added = true;
        }

        if (added)
            await context.SaveChangesAsync(cancellationToken);
    }
}
