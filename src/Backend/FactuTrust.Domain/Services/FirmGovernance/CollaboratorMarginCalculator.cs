using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Services.FirmGovernance;

/// <summary>
/// Répartit les honoraires et les charges de structure entre collaborateurs, puis en déduit la
/// marge sur coût direct.
/// </summary>
/// <remarks>
/// <para>
/// Service de domaine pur : aucune dépendance à la base ni à l'horloge. L'appelant fournit les
/// heures et les coûts déjà agrégés, ce qui rend chaque règle de répartition testable isolément.
/// </para>
/// <para>
/// Modèle retenu — celui des services professionnels : les honoraires d'un dossier reviennent aux
/// collaborateurs <b>au prorata des heures réellement passées</b>, et non au gestionnaire
/// nominalement assigné. Un associé qui délègue ne capte donc plus la totalité des honoraires d'un
/// dossier traité par ses rattachés.
/// </para>
/// </remarks>
public static class CollaboratorMarginCalculator
{
    /// <summary>
    /// Répartit le budget annuel d'un dossier entre les collaborateurs qui y ont passé du temps.
    /// </summary>
    /// <param name="dossierBudget">Honoraires annuels du dossier.</param>
    /// <param name="hoursByCollaborator">Heures saisies sur ce dossier, par collaborateur.</param>
    /// <returns>
    /// Quote-part par collaborateur. La somme des quote-parts est <b>rigoureusement égale</b> au
    /// budget : l'écart d'arrondi au millime est absorbé par le plus gros contributeur en heures,
    /// faute de quoi une fraction d'honoraires disparaîtrait à chaque répartition.
    /// </returns>
    public static IReadOnlyDictionary<Guid, decimal> AllocateDossierRevenue(
        decimal dossierBudget,
        IReadOnlyDictionary<Guid, decimal> hoursByCollaborator)
        => AllocateProRata(dossierBudget, hoursByCollaborator);

    /// <summary>
    /// Répartit la masse salariale du personnel support sur les collaborateurs productifs.
    /// </summary>
    /// <param name="supportTotal">Coût employeur cumulé des collaborateurs non productifs.</param>
    /// <param name="directCostByCollaborator">Coût employeur de chaque collaborateur productif.</param>
    /// <returns>
    /// Quote-part de structure par collaborateur, au prorata de son propre coût chargé : un senior
    /// coûteux absorbe davantage de structure qu'un junior. C'est la convention de contrôle de
    /// gestion la plus défendable en revue, et elle évite de faire porter la même charge à des
    /// profils très inégaux.
    /// </returns>
    public static IReadOnlyDictionary<Guid, decimal> AllocateSupportCost(
        decimal supportTotal,
        IReadOnlyDictionary<Guid, decimal> directCostByCollaborator)
        => AllocateProRata(supportTotal, directCostByCollaborator);

    /// <summary>Marge sur coût direct : chiffre d'affaires produit, moins le coût direct et la structure absorbée.</summary>
    public static decimal ComputeMargin(decimal revenue, decimal directCost, decimal supportShare) =>
        MillimeRounding.Round(revenue - directCost - supportShare);

    /// <summary>
    /// Répartition proportionnelle générique, sans perte ni création de centimes.
    /// </summary>
    /// <remarks>
    /// Le résidu d'arrondi va au plus fort contributeur, avec départage déterministe par
    /// identifiant : deux exécutions sur les mêmes données produisent exactement le même résultat,
    /// condition nécessaire pour qu'un recalcul soit idempotent.
    /// </remarks>
    private static IReadOnlyDictionary<Guid, decimal> AllocateProRata(
        decimal amountToShare,
        IReadOnlyDictionary<Guid, decimal> weights)
    {
        var result = new Dictionary<Guid, decimal>();
        if (weights.Count == 0)
            return result;

        var positive = weights.Where(w => w.Value > 0).ToList();
        var totalWeight = positive.Sum(w => w.Value);

        // Poids total nul (aucune heure, ou aucun coût connu) : on ne divise pas par zéro et on
        // n'invente aucune répartition — chacun reçoit zéro.
        if (totalWeight <= 0 || amountToShare == 0)
        {
            foreach (var key in weights.Keys)
                result[key] = 0m;
            return result;
        }

        foreach (var key in weights.Keys)
            result[key] = 0m;

        foreach (var (key, weight) in positive)
            result[key] = MillimeRounding.Round(amountToShare * weight / totalWeight);

        var residual = amountToShare - result.Values.Sum();
        if (residual != 0)
        {
            var absorber = positive
                .OrderByDescending(w => w.Value)
                .ThenBy(w => w.Key)
                .First().Key;
            result[absorber] = MillimeRounding.Round(result[absorber] + residual);
        }

        return result;
    }
}
