using System;
using System.Collections.Generic;
using System.Linq;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Bornage déterministe des listes de classement (meilleurs clients / produits, soldes) AVANT réinjection dans le
/// contexte LLM. Trie par montant décroissant et applique <c>top_n</c> (ou un plafond par défaut) afin d'éviter la
/// troncature au milieu d'une ligne (« [résultat tronqué] ») et de garder le prompt court (prefill rapide).
/// Logique PURE : testable indépendamment de l'exécuteur d'outils.
/// </summary>
public static class AiRankingLimiter
{
    /// <summary>Clamp dur du nombre de lignes (garde-fou contre un top_n démesuré).</summary>
    public const int MaxRows = 200;

    /// <summary>Plafond par défaut appliqué quand le réglage tenant est absent / non positif.</summary>
    public const int FallbackDefaultCap = 50;

    /// <summary>
    /// Renvoie au plus N lignes, triées par <paramref name="rankKey"/> décroissant.
    /// Si <paramref name="hasExplicitTopN"/> est vrai, trie toujours puis prend N (clampé à [1, <see cref="MaxRows"/>]).
    /// Sinon, applique <paramref name="defaultCap"/> UNIQUEMENT si la liste le dépasse — en deçà, la liste d'origine
    /// est renvoyée telle quelle (aucun changement de comportement ni d'ordre pour les petits jeux de données).
    /// </summary>
    public static IReadOnlyList<T> Apply<T>(
        IReadOnlyList<T> rows,
        bool hasExplicitTopN,
        int requestedTopN,
        int defaultCap,
        Func<T, decimal> rankKey)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(rankKey);

        var cap = defaultCap > 0 ? defaultCap : FallbackDefaultCap;
        var requested = hasExplicitTopN ? requestedTopN : cap;
        var effective = Math.Clamp(requested, 1, MaxRows);

        if (!hasExplicitTopN && rows.Count <= effective)
            return rows;

        return rows.OrderByDescending(rankKey).Take(effective).ToList();
    }
}
