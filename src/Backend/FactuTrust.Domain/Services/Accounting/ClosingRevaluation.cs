using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Accounting;

/// <summary>
/// Position ouverte d'un compte dans une devise, à la date de clôture.
/// </summary>
/// <param name="AccountNumber">Compte porteur de la position (tiers ou trésorerie).</param>
/// <param name="CurrencyCode">Devise de la position.</param>
/// <param name="NetInCurrency">Position nette en devise, mesurée en <b>débit net</b> (débits − crédits).</param>
/// <param name="NetFunctional">Contre-valeur historique en devise de tenue, même convention de signe.</param>
public sealed record CurrencyPosition(
    string AccountNumber,
    string CurrencyCode,
    decimal NetInCurrency,
    decimal NetFunctional);

/// <summary>
/// Résultat de la réévaluation d'une position.
/// </summary>
/// <param name="Delta">
/// Écart de conversion : contre-valeur au taux de clôture moins contre-valeur historique, en
/// convention de débit net. Positif = gain latent, négatif = perte latente.
/// </param>
public sealed record RevaluedPosition(
    CurrencyPosition Position,
    decimal ClosingRate,
    decimal RevaluedFunctional,
    decimal Delta)
{
    public bool IsLatentGain => Delta > 0;
    public bool IsLatentLoss => Delta < 0;
}

/// <summary>
/// Réévaluation des positions en devise à la clôture (NCT).
///
/// <para>
/// <b>Convention de signe, unique et systématique : tout se mesure en débit net</b>
/// (débits − crédits). Une créance en devise donne une position positive, une dette une position
/// négative. L'écart vaut alors <c>contre-valeur au taux de clôture − contre-valeur historique</c>,
/// et son signe se lit de la même façon dans les deux cas :
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>Delta &gt; 0</c> — <b>gain latent</b> : la créance vaut davantage de dinars, ou la dette
///     en coûte moins. Le compte est débité, l'écart de conversion <b>passif</b> (185) crédité.
///   </description></item>
///   <item><description>
///     <c>Delta &lt; 0</c> — <b>perte latente</b> : l'écart de conversion <b>actif</b> (275) est
///     débité, le compte crédité. Une provision pour perte de change (1515) peut compléter,
///     principe de prudence : la perte latente se provisionne, le gain latent ne se constate pas.
///   </description></item>
/// </list>
///
/// <para>
/// L'écriture produite est <b>contre-passée à l'ouverture de la période suivante</b> : les écarts
/// de conversion sont des comptes de régularisation, pas des comptes de résultat. Sans
/// contre-passation, la réévaluation suivante se cumulerait à celle-ci.
/// </para>
/// </summary>
public static class ClosingRevaluation
{
    /// <summary>Réévalue une position au taux de clôture. Le taux doit être strictement positif.</summary>
    public static Result<RevaluedPosition> Revalue(CurrencyPosition position, decimal closingRate)
    {
        if (closingRate <= 0)
            return Result.Failure<RevaluedPosition>(
                Error.Validation("ExchangeRate", "Le taux de clôture doit être strictement positif."));

        var revalued = MillimeRounding.Round(position.NetInCurrency * closingRate);
        var delta = MillimeRounding.Round(revalued - position.NetFunctional);

        return Result.Success(new RevaluedPosition(position, closingRate, revalued, delta));
    }

    /// <summary>
    /// Construit les lignes de l'écriture de réévaluation. Les positions dont l'écart est nul sont
    /// ignorées : elles n'ont rien à régulariser.
    /// </summary>
    /// <param name="gainAccount">Écarts de conversion passif (185 en NCT 01).</param>
    /// <param name="lossAccount">Écarts de conversion actif (275 en NCT 01).</param>
    public static IReadOnlyList<JournalLineInput> BuildLines(
        IReadOnlyList<RevaluedPosition> positions,
        string gainAccount,
        string lossAccount,
        string label)
    {
        var lines = new List<JournalLineInput>();

        foreach (var p in positions)
        {
            if (p.Delta == 0)
                continue;

            var amount = Math.Abs(p.Delta);
            var lineLabel = $"{label} — {p.Position.CurrencyCode}";

            if (p.IsLatentGain)
            {
                // Le compte gagne de la valeur : on le débite, la contrepartie va au passif.
                lines.Add(new JournalLineInput(p.Position.AccountNumber, lineLabel, amount, 0m, null, ThirdPartyKind.None));
                lines.Add(new JournalLineInput(gainAccount, lineLabel, 0m, amount, null, ThirdPartyKind.None));
            }
            else
            {
                lines.Add(new JournalLineInput(lossAccount, lineLabel, amount, 0m, null, ThirdPartyKind.None));
                lines.Add(new JournalLineInput(p.Position.AccountNumber, lineLabel, 0m, amount, null, ThirdPartyKind.None));
            }
        }

        return lines;
    }

    /// <summary>
    /// Lignes de la provision pour pertes de change : seules les pertes latentes sont provisionnées
    /// (principe de prudence). Rend une liste vide s'il n'y a aucune perte.
    /// </summary>
    /// <param name="expenseAccount">Dotation aux provisions (compte de charges).</param>
    /// <param name="provisionAccount">Provisions pour pertes de change (1515 en NCT 01).</param>
    public static IReadOnlyList<JournalLineInput> BuildProvisionLines(
        IReadOnlyList<RevaluedPosition> positions,
        string expenseAccount,
        string provisionAccount,
        string label)
    {
        var total = positions.Where(p => p.IsLatentLoss).Sum(p => Math.Abs(p.Delta));
        if (total == 0)
            return Array.Empty<JournalLineInput>();

        return new[]
        {
            new JournalLineInput(expenseAccount, label, total, 0m, null, ThirdPartyKind.None),
            new JournalLineInput(provisionAccount, label, 0m, total, null, ThirdPartyKind.None)
        };
    }
}
