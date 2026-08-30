namespace FactuTrust.Application.Features.Accounting.OfficialForm;

/// <summary>
/// Fusionne la retenue à la source sur loyers (article 4 — personnes physiques résidentes 10 %)
/// à partir des débits journal du compte 613, distincte du module RS factures.
/// </summary>
public static class RentWithholdingFormLines
{
    /// <summary>Sous-ligne officielle : commissions / loyers — personnes physiques résidentes 10 %.</summary>
    public const string Line = "Line4Individuals";

    /// <summary>Taux réglementaire pour les loyers versés à des personnes physiques résidentes.</summary>
    public const decimal Rate = 0.10m;

    /// <summary>Retenue arrondie au millime (convention déclaration).</summary>
    public static decimal ComputeAmount(decimal rentBase) => Math.Round(rentBase * Rate, 3);

    /// <summary>
    /// Ajoute la ligne loyers 613 aux lignes déjà ventilées (factures + paie), puis applique
    /// le garde-fou de total déclaré.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> Merge(
        IReadOnlyDictionary<string, decimal>? priorLines,
        decimal declaredTotal,
        decimal rentBase,
        decimal rentWithheld)
    {
        if (declaredTotal <= 0m)
            return WithholdingFormLineMapper.Empty;

        var lines = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (priorLines is { Count: > 0 })
        {
            foreach (var (key, value) in priorLines)
                lines[key] = value;
        }

        if (rentWithheld > 0m)
        {
            lines[$"{Line}.Amount"] = lines.GetValueOrDefault($"{Line}.Amount") + rentWithheld;
            if (rentBase > 0m)
                lines[$"{Line}.Base"] = lines.GetValueOrDefault($"{Line}.Base") + rentBase;
        }

        return WithholdingFormLineMapper.EnsureWithinDeclaredTotal(lines, declaredTotal);
    }
}
