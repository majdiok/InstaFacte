namespace FactuTrust.Application.Features.Accounting.OfficialForm;

/// <summary>
/// Fusionne la retenue à la source salariale (IRPP article 1, CSS article 3) sur les lignes
/// officielles, sans passer par <see cref="WithholdingCategory"/> — la CSS n'est pas une
/// catégorie du module factures / TEJ.
/// </summary>
public static class PayrollWithholdingFormLines
{
    /// <summary>
    /// Ajoute les lignes paie aux lignes déjà ventilées depuis les factures, puis applique
    /// le garde-fou de total déclaré.
    /// </summary>
    /// <param name="invoiceLines">Ventilation factures (clés <c>Line*.Amount</c> / <c>Line*.Base</c>).</param>
    /// <param name="declaredTotal">Retenue à la source totale portée par la déclaration.</param>
    /// <param name="netTaxable">Net imposable cumulé des salariés du mois.</param>
    /// <param name="irpp">IRPP retenu (régularisations du mois comprises).</param>
    /// <param name="css">CSS salariale retenue (régularisations du mois comprises).</param>
    public static IReadOnlyDictionary<string, decimal> Merge(
        IReadOnlyDictionary<string, decimal>? invoiceLines,
        decimal declaredTotal,
        decimal netTaxable,
        decimal irpp,
        decimal css)
    {
        if (declaredTotal <= 0m)
            return WithholdingFormLineMapper.Empty;

        var lines = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (invoiceLines is { Count: > 0 })
        {
            foreach (var (key, value) in invoiceLines)
                lines[key] = value;
        }

        AddPayrollLine(lines, "Line1", irpp, netTaxable);
        AddPayrollLine(lines, "Line3", css, netTaxable);

        return WithholdingFormLineMapper.EnsureWithinDeclaredTotal(lines, declaredTotal);
    }

    private static void AddPayrollLine(
        Dictionary<string, decimal> lines,
        string line,
        decimal amount,
        decimal netTaxable)
    {
        if (amount == 0m)
            return;

        lines[$"{line}.Amount"] = lines.GetValueOrDefault($"{line}.Amount") + amount;
        if (netTaxable > 0m)
            lines[$"{line}.Base"] = lines.GetValueOrDefault($"{line}.Base") + netTaxable;
    }
}
