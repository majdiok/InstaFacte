using FactuTrust.Domain.Entities.Fiscal;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Insère les paramètres d'impôt par exercice lorsqu'ils sont absents (taux IS, minimum d'impôt, CSS,
/// acomptes, report des déficits, barème IRPP) à partir des défauts légaux indicatifs, puis rafraîchit
/// les lignes qui n'ont JAMAIS été modifiées par le comptable.
///
/// Le rafraîchissement est indispensable : sans lui, les dossiers déjà initialisés conserveraient
/// indéfiniment d'anciens défauts (ex. taux CSS nul) alors que les valeurs de référence ont évolué.
/// Une ligne marquée <see cref="IncomeTaxYearParameter.IsUserModified"/> n'est jamais écrasée.
/// </summary>
public static class IncomeTaxYearParameterInitializer
{
    private const int FirstYear = 2018;
    private const int LastYear = 2040;

    /// <returns><c>true</c> when at least one year parameter row was inserted or refreshed, otherwise <c>false</c>.</returns>
    public static async Task<bool> EnsureDefaultsSeededAsync(TenantDbContext context, CancellationToken cancellationToken = default)
    {
        var rows = await context.IncomeTaxYearParameters.ToListAsync(cancellationToken);
        var byYear = rows.ToDictionary(p => p.FiscalYear);
        var changed = false;

        for (var year = FirstYear; year <= LastYear; year++)
        {
            if (!byYear.TryGetValue(year, out var existing))
            {
                context.IncomeTaxYearParameters.Add(IncomeTaxYearParameterDefaults.Create(year));
                changed = true;
                continue;
            }

            // Paramètres saisis par le comptable : intouchables.
            if (existing.IsUserModified)
                continue;

            var defaults = IncomeTaxYearParameterDefaults.Create(year);
            if (!DiffersFromDefaults(existing, defaults))
                continue;

            existing.Update(
                defaults.IsStandardRate, defaults.IsReducedRate, defaults.IsSectorRate,
                defaults.MinTaxRate, defaults.MinTaxReducedRate, defaults.MinTaxFloorTnd,
                defaults.CssApplies, defaults.CssRate, defaults.CssFloorTnd,
                defaults.AcompteRate, defaults.AcompteCount, defaults.DeficitCarryForwardYears,
                defaults.IrppBracketsJson,
                defaults.MinTaxFloorReducedTnd, defaults.RoundTaxableToDinar,
                markUserModified: false);
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken);

        return changed;
    }

    private static bool DiffersFromDefaults(IncomeTaxYearParameter a, IncomeTaxYearParameter b) =>
        a.IsStandardRate != b.IsStandardRate
        || a.IsReducedRate != b.IsReducedRate
        || a.IsSectorRate != b.IsSectorRate
        || a.MinTaxRate != b.MinTaxRate
        || a.MinTaxReducedRate != b.MinTaxReducedRate
        || a.MinTaxFloorTnd != b.MinTaxFloorTnd
        || a.MinTaxFloorReducedTnd != b.MinTaxFloorReducedTnd
        || a.CssApplies != b.CssApplies
        || a.CssRate != b.CssRate
        || a.CssFloorTnd != b.CssFloorTnd
        || a.AcompteRate != b.AcompteRate
        || a.AcompteCount != b.AcompteCount
        || a.DeficitCarryForwardYears != b.DeficitCarryForwardYears
        || a.RoundTaxableToDinar != b.RoundTaxableToDinar
        || !string.Equals(a.IrppBracketsJson, b.IrppBracketsJson, StringComparison.Ordinal);
}
