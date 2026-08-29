using System.Globalization;
using System.Text.Json;

namespace FactuTrust.Application.Common.Fiscal;

/// <summary>Tranche du barème IRPP progressif : borne inférieure (TND) et taux marginal (en %).</summary>
public sealed record IrppBracket(decimal Lower, decimal Rate);

/// <summary>Parsing du barème IRPP sérialisé (JSON) stocké dans les paramètres d'exercice.</summary>
public static class IrppScale
{
    private sealed record RawBracket(decimal lower, decimal rate);

    /// <summary>
    /// Parsing TOLÉRANT : ordonne les tranches et ignore silencieusement un JSON malformé ou vide.
    /// Conservé pour la lecture des données historiques (une ligne déjà persistée ne doit pas faire
    /// planter le moteur de calcul) — ne doit PAS être utilisé pour valider une saisie utilisateur.
    /// </summary>
    public static IReadOnlyList<IrppBracket> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<IrppBracket>();

        try
        {
            var raw = JsonSerializer.Deserialize<List<RawBracket>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (raw is null)
                return Array.Empty<IrppBracket>();

            return raw
                .Select(b => new IrppBracket(b.lower, b.rate))
                .OrderBy(b => b.Lower)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<IrppBracket>();
        }
    }

    /// <summary>
    /// Parsing STRICT (T13) : valide intégralement une saisie utilisateur avant persistance.
    /// Règles : JSON bien formé et tableau non vide ; chaque tranche a une borne <c>lower ≥ 0</c> ;
    /// bornes strictement croissantes et uniques (triées) ; taux marginal <c>rate ∈ [0;100]</c> (%).
    /// Rend <c>false</c> et un message français précis en cas de rejet ; le <paramref name="brackets"/>
    /// de sortie est vide en échec.
    /// </summary>
    public static bool TryParseStrict(string? json, out IReadOnlyList<IrppBracket> brackets, out string? error)
    {
        brackets = Array.Empty<IrppBracket>();
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Le barème IRPP est vide.";
            return false;
        }

        List<RawBracket>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RawBracket>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            error = "Le barème IRPP n'est pas un JSON valide.";
            return false;
        }

        if (raw is null || raw.Count == 0)
        {
            error = "Le barème IRPP doit contenir au moins une tranche.";
            return false;
        }

        // Vérifie chaque tranche individuellement (borne ≥ 0, taux ∈ [0;100]) avant de contrôler l'ordre,
        // afin que le message reflète la première anomalie rencontrée.
        for (var i = 0; i < raw.Count; i++)
        {
            var b = raw[i];
            if (b.lower < 0m)
            {
                error = string.Format(CultureInfo.InvariantCulture,
                    "Tranche {0} : la borne inférieure doit être positive ou nulle (reçu {1}).", i + 1, b.lower);
                return false;
            }
            if (b.rate < 0m || b.rate > 100m)
            {
                error = string.Format(CultureInfo.InvariantCulture,
                    "Tranche {0} : le taux marginal doit être compris entre 0 et 100 % (reçu {1}).", i + 1, b.rate);
                return false;
            }
        }

        // Bornes strictement croissantes et uniques (on ordonne puis on contrôle la progression).
        var ordered = raw.OrderBy(b => b.lower).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].lower <= ordered[i - 1].lower)
            {
                error = "Les bornes inférieures du barème IRPP doivent être strictement croissantes et uniques.";
                return false;
            }
        }

        brackets = ordered.Select(b => new IrppBracket(b.lower, b.rate)).ToList();
        return true;
    }
}
