using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FactuTrust.Application.Common;

/// <summary>
/// Lecture d'un nombre écrit à la tunisienne ou à la française.
///
/// <para><see cref="ParseDecimal"/> est le portage LITTÉRAL de l'ancien
/// <c>InstaFactInvoicePdfParser.ParseAmount</c> : même code, même résultat sur les cas « golden »
/// déjà couverts. Le parseur natif délègue désormais ici, ses tests restent la référence et n'ont
/// pas été modifiés.</para>
///
/// <para><see cref="ParseDecimalLenient"/> ajoute un nettoyage typographique (%, TND, DT, €,
/// lettres, espaces insécables) réservé aux SORTIES DE LLM. Il n'est jamais appliqué par le parseur
/// natif : aucun risque de dérive sur la lecture des factures InstaFact.</para>
///
/// <para><b>Ambiguïté assumée</b> : « 1,250 » est lu 1.250 (un dinar deux cent cinquante millimes)
/// et non 1250. Une virgule suivie de 1 à 3 chiffres est traitée comme séparateur décimal — c'est
/// la convention TND, déjà scellée par le cas golden ("650,000" → 650.000). La changer casserait la
/// lecture des factures InstaFact. Un montant ambigu reste donc possible sur une facture en euros ;
/// le contrôle de cohérence totaux/lignes en aval le signale.</para>
/// </summary>
public static class TunisianNumberParsing
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Une virgule suivie de 1 à 3 chiffres : séparateur décimal français/tunisien.</summary>
    private static readonly Regex FrenchDecimal = new(@"^\d+,\d{1,3}$", RegexOptions.Compiled);

    /// <summary>
    /// Montant tel qu'imprimé sur une facture. Gère « 1,250.000 » (virgule = millier),
    /// « +3700.000 », « 1 250,000 » (format français) et « 650,000 ».
    /// Ne pas modifier sans repasser les tests golden du parseur InstaFact.
    /// </summary>
    public static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Replace(' ', ' ').Replace(" ", string.Empty).Trim();
        if (text.Length == 0)
            return null;

        var negative = text.StartsWith('-');
        text = text.TrimStart('+', '-');

        var hasDot = text.Contains('.');
        var hasComma = text.Contains(',');

        if (hasDot && hasComma)
        {
            // « 1,250.000 » : la virgule est le séparateur de milliers.
            text = text.Replace(",", string.Empty);
        }
        else if (hasComma)
        {
            // Une seule virgule suivie de 1 à 3 chiffres : séparateur décimal français.
            text = FrenchDecimal.IsMatch(text)
                ? text.Replace(',', '.')
                : text.Replace(",", string.Empty);
        }

        if (!decimal.TryParse(text, NumberStyles.Number, Inv, out var value))
            return null;

        return negative ? -value : value;
    }

    /// <summary>
    /// Variante « sortie de LLM » : accepte « 19 % », « 1 000,50 TND », « ~278,000 », « 12 dinars ».
    /// Tout ce qui n'est ni chiffre, ni séparateur, ni signe est considéré comme du bruit.
    /// </summary>
    public static decimal? ParseDecimalLenient(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : ParseDecimal(StripNoise(raw));

    /// <summary>Idem <see cref="ParseDecimalLenient"/>, ramené à un entier (arrondi commercial).</summary>
    public static int? ParseInt32Lenient(string? raw) => ToInt32OrNull(ParseDecimalLenient(raw));

    /// <summary>
    /// Arrondi commercial vers l'entier le plus proche ; <c>null</c> si la valeur sort des bornes
    /// d'un <see cref="int"/> (un taux de TVA fantaisiste vaut mieux absent que tronqué).
    /// </summary>
    public static int? ToInt32OrNull(decimal? value)
    {
        if (value is not { } v)
            return null;
        if (v < int.MinValue || v > int.MaxValue)
            return null;
        return (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Ne conserve que chiffres, séparateurs décimaux et signe. Élimine au passage toutes les
    /// variantes d'espace (fine insécable U+202F, insécable U+00A0, espace ordinaire).
    /// </summary>
    private static string StripNoise(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsDigit(c) || c is '.' or ',' or '+' or '-')
                sb.Append(c);
        }
        return sb.ToString();
    }
}
