using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Répartit une remise de pied de document sur les lignes, au prorata de leur base HT.
///
/// <b>Pourquoi répartir plutôt que soustraire au total.</b> La remise doit réduire la base de
/// TVA — le client ne paie pas de TVA sur ce qu'il ne règle pas — et le FODEC avec elle. En la
/// ramenant sur les lignes, chaque ligne recalcule son FODEC et sa TVA sur sa base réduite : la
/// ventilation par taux reste juste même avec des taux mêlés (7 / 13 / 19 %), et tout l'aval
/// (comptabilité, PDF, export fiscal) continue de fonctionner sans le savoir.
///
/// <b>Le résidu d'arrondi.</b> Répartir au millime laisse presque toujours quelques millimes
/// d'écart entre la somme des parts et la remise demandée. Ils sont donnés à la ligne de plus
/// forte base, plutôt que laissés de côté : la somme des remises de ligne doit être EXACTEMENT
/// la remise annoncée au pied, sans quoi le document ne s'équilibre pas au millime.
/// </summary>
public static class GlobalDiscountAllocator
{
    /// <summary>
    /// Répartit <paramref name="discountAmount"/> sur les bases de <paramref name="lineBases"/>.
    /// Renvoie une part par ligne, dans le même ordre, dont la somme vaut exactement la remise.
    /// </summary>
    /// <param name="lineBases">Base HT de chaque ligne, après remise de ligne et avant remise de pied.</param>
    /// <param name="discountAmount">Remise de pied à répartir.</param>
    public static IReadOnlyList<decimal> Allocate(IReadOnlyList<decimal> lineBases, decimal discountAmount)
    {
        var count = lineBases.Count;
        var allocations = new decimal[count];

        if (count == 0 || discountAmount <= 0)
            return allocations;

        var total = lineBases.Sum();

        // Base nulle (lignes à zéro, ou document vide) : rien à répartir au prorata. Répartir
        // « également » serait arbitraire et créerait des remises sur des lignes gratuites.
        if (total <= 0)
            return allocations;

        // Une remise supérieure à la base rendrait des lignes négatives : on la plafonne.
        var effective = Math.Min(discountAmount, total);

        var allocated = 0m;
        for (var i = 0; i < count; i++)
        {
            allocations[i] = Math.Round(effective * lineBases[i] / total, Money.DecimalPlaces);
            allocated += allocations[i];
        }

        // Résidu d'arrondi : à la ligne de plus forte base, celle qui l'absorbe le mieux.
        var residue = Math.Round(effective - allocated, Money.DecimalPlaces);
        if (residue != 0)
        {
            var heaviest = 0;
            for (var i = 1; i < count; i++)
            {
                if (lineBases[i] > lineBases[heaviest])
                    heaviest = i;
            }

            var adjusted = allocations[heaviest] + residue;

            // Le rattrapage ne doit jamais rendre une part négative ni dépasser sa base.
            allocations[heaviest] = Math.Clamp(adjusted, 0m, lineBases[heaviest]);
        }

        return allocations;
    }

    /// <summary>
    /// Convertit une remise exprimée en pourcentage en montant, sur la base totale donnée.
    /// </summary>
    public static decimal FromPercent(decimal totalBase, decimal percent) =>
        Math.Round(totalBase * percent / 100m, Money.DecimalPlaces);
}
