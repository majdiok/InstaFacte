using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Indice de doublon : une entité proposée par la spec ressemble à une table Studio EXISTANTE du
/// tenant. C'est un signal d'aperçu (résumé du plan), jamais un blocage : la réutilisation réelle
/// se déclare explicitement via <c>existingKey</c> sur l'entité.
/// </summary>
/// <param name="SpecRef">Référence de l'entité dans la spec (clé de correspondance interne).</param>
/// <param name="SpecDisplayName">Nom affiché proposé pour la nouvelle table.</param>
/// <param name="ExistingKey">Clé réelle de la table existante semblable.</param>
/// <param name="ExistingDisplayName">Nom affiché de la table existante.</param>
/// <param name="Reason">Force du rapprochement : <c>same_key</c> | <c>same_name</c> | <c>singular_plural</c>.</param>
public sealed record DuplicateHint(
    string SpecRef,
    string SpecDisplayName,
    string ExistingKey,
    string ExistingDisplayName,
    string Reason);

/// <summary>
/// Détecteur PUR (aucune base) de tables proposées équivalentes à des tables Studio existantes.
/// Comparaison conservative — trois familles de rapprochement, jamais de préfixe (« Contrat » ne
/// signale PAS « Contrats cadres ») — pour ne jamais effrayer l'utilisateur avec un faux positif
/// dans l'aperçu. Au plus UN indice par entité proposée : la raison la plus forte l'emporte
/// (<c>same_key</c> &gt; <c>same_name</c> &gt; <c>singular_plural</c>).
/// </summary>
public static class StudioAiDuplicateDetector
{
    public const string ReasonSameKey = "same_key";
    public const string ReasonSameName = "same_name";
    public const string ReasonSingularPlural = "singular_plural";

    /// <summary>Mots vides FR ignorés par la comparaison normalisée (si au moins un token plein reste).</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "de", "des", "du", "d", "la", "le", "les", "l", "un", "une", "et", "a", "au", "aux",
        "en", "pour", "par", "sur"
    };

    /// <summary>
    /// Rapproche chaque entité NOUVELLE de la spec des tables actives existantes. Une entité qui
    /// déclare déjà <c>existingKey</c> est un choix explicite de réutilisation : pas d'indice.
    /// </summary>
    public static IReadOnlyList<DuplicateHint> Detect(
        ParsedSystemSpec spec, IReadOnlyList<CustomEntityDefinition> existing)
    {
        var hints = new List<DuplicateHint>();
        foreach (var entity in spec.Entities)
        {
            if (entity.ExistingKey is not null) continue;
            var hint = BestMatch(entity.Ref, entity.EntityDisplayName, entity.EntityDisplayNamePlural, existing);
            if (hint is not null) hints.Add(hint);
        }
        return hints;
    }

    /// <summary>Rapproche la table proposée d'une spec « application » (une seule table).</summary>
    public static IReadOnlyList<DuplicateHint> Detect(
        ParsedAppSpec spec, IReadOnlyList<CustomEntityDefinition> existing)
    {
        var hint = BestMatch(
            StudioAiAppSpec.SlugKey(spec.EntityDisplayName),
            spec.EntityDisplayName, spec.EntityDisplayNamePlural, existing);
        return hint is null ? Array.Empty<DuplicateHint>() : new[] { hint };
    }

    private static DuplicateHint? BestMatch(
        string specRef, string displayName, string displayNamePlural,
        IReadOnlyList<CustomEntityDefinition> existing)
    {
        DuplicateHint? best = null;
        foreach (var current in existing)
        {
            if (!current.IsActive) continue;

            // same_key : la référence proposée EST la clé réelle (clés slugifiées, déjà normalisées).
            var reason =
                string.Equals(specRef, current.Key, StringComparison.Ordinal) ? ReasonSameKey
                : SameName(displayName, displayNamePlural, current) ? ReasonSameName
                : SameNormalized(displayName, displayNamePlural, current) ? ReasonSingularPlural
                : null;
            if (reason is null) continue;

            // Ordre de force : same_key > same_name > singular_plural (le premier gagnant par entité).
            if (best is null || Rank(reason) > Rank(best.Reason))
                best = new DuplicateHint(specRef, displayName, current.Key, current.DisplayName, reason);
            if (reason == ReasonSameKey) break; // imbattable
        }
        return best;

        static int Rank(string r) => r switch
        {
            ReasonSameKey => 3,
            ReasonSameName => 2,
            _ => 1
        };
    }

    /// <summary>
    /// same_name : égalité de slug accent-insensible entre n'importe lequel des libellés proposés
    /// (singulier/pluriel) et la clé ou un libellé existant (« Employés » ≡ clé « employes »).
    /// La clé existante est comparée BRUTE : elle est déjà slugifiée.
    /// </summary>
    private static bool SameName(string displayName, string displayNamePlural, CustomEntityDefinition current) =>
        AnyMatch(
            new[] { StudioAiAppSpec.SlugKey(displayName), StudioAiAppSpec.SlugKey(displayNamePlural) },
            new[]
            {
                current.Key,
                StudioAiAppSpec.SlugKey(current.DisplayName),
                StudioAiAppSpec.SlugKey(current.DisplayNamePlural)
            });

    /// <summary>
    /// singular_plural : égalité après normalisation (mots vides écartés, singulier FR conservateur)
    /// — « Demande de congé » ≡ « Demandes de congés », « bureaux » ≡ « bureau ».
    /// </summary>
    private static bool SameNormalized(string displayName, string displayNamePlural, CustomEntityDefinition current) =>
        AnyMatch(
            new[] { NormalizeForMatch(displayName), NormalizeForMatch(displayNamePlural) },
            new[]
            {
                NormalizeForMatch(current.Key),
                NormalizeForMatch(current.DisplayName),
                NormalizeForMatch(current.DisplayNamePlural)
            });

    /// <summary>Égalité stricte (ordinal) entre une forme proposée non vide et une forme existante.</summary>
    private static bool AnyMatch(string[] proposed, string[] existingKeys) =>
        proposed.Any(p => p.Length > 0 && existingKeys.Contains(p, StringComparer.Ordinal));

    /// <summary>
    /// Forme de comparaison : slug accent-insensible découpé en tokens, mots vides FR écartés (s'il
    /// reste au moins un token plein), singulier FR conservateur par token (« eaux » → « eau »,
    /// « aux » → « al », « x »/« s » finals retirés hors mots courts et « ss »). Jamais de préfixe.
    /// </summary>
    public static string NormalizeForMatch(string input)
    {
        var slug = StudioAiAppSpec.SlugKey(input);
        if (slug.Length == 0) return string.Empty;

        var tokens = slug.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var kept = tokens.Where(t => !StopWords.Contains(t)).ToList();
        if (kept.Count == 0) kept = tokens.ToList();
        return string.Join('_', kept.Select(Singularize));
    }

    /// <summary>
    /// Singulier FR conservateur : « bureaux » → « bureau », « journaux » → « journal »,
    /// « congés » → « conge », « services » → « service ». Un token « ss » (« adresse ») ou court
    /// (« fils », « prix », « taux ») n'est jamais touché : mieux vaut manquer un doublon qu'en
    /// inventer un.
    /// </summary>
    private static string Singularize(string token)
    {
        if (token.EndsWith("eaux", StringComparison.Ordinal))
            return token[..^1]; // bureaux → bureau
        if (token.EndsWith("aux", StringComparison.Ordinal) && token.Length > 4)
            return string.Concat(token.AsSpan(0, token.Length - 3), "al"); // journaux → journal
        if (token.EndsWith('x') && token.Length > 4)
            return token[..^1]; // bijoux → bijou ; prix/taux (4) inchangés
        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal) && token.Length > 4)
            return token[..^1]; // conges → conge ; adresse/fils inchangés
        return token;
    }
}
