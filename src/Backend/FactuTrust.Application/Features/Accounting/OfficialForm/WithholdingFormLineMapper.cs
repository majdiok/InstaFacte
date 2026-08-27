using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.OfficialForm;

/// <summary>
/// Ventile la retenue à la source de l'application sur les lignes numérotées du formulaire
/// officiel (« الخصم من المورد », articles 1 à 31, pages 1 à 3).
///
/// <para>
/// <b>Avertissement fiscal.</b> Cette table de correspondance traduit des catégories internes
/// vers des lignes réglementaires ; certaines lignes se distinguent par la qualité du bénéficiaire
/// (personne physique / morale, résident / non-résident) que l'application ne modélise pas. Les
/// choix par défaut sont documentés ligne à ligne ci-dessous et <b>doivent être validés par un
/// fiscaliste</b> avant activation en production.
/// </para>
///
/// <para>
/// <b>Règle de sûreté.</b> Le total de la page 4 et le récapitulatif de la page 9 font toujours
/// foi : ils reportent le montant agrégé de la déclaration. Le détail par ligne n'est
/// qu'informatif, et il est intégralement abandonné si la ventilation dépasse ce total (signe
/// d'une incohérence de données) — mieux vaut une déclaration moins détaillée qu'une déclaration
/// fausse.
/// </para>
/// </summary>
public static class WithholdingFormLineMapper
{
    /// <summary>Tolérance d'arrondi au millime.</summary>
    internal const decimal Epsilon = 0.001m;

    internal static readonly IReadOnlyDictionary<string, decimal> Empty =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Construit la ventilation « suffixe de ligne → montant » consommée par
    /// <see cref="MonthlyDeclarationFormBinder"/>.
    /// </summary>
    /// <param name="categories">Ventilation par catégorie interne (rapport RS mensuel).</param>
    /// <param name="declaredTotal">Retenue à la source totale portée par la déclaration.</param>
    /// <returns>
    /// Dictionnaire des lignes officielles à renseigner. Vide si aucune ventilation exploitable
    /// n'est disponible, ou si la règle de sûreté s'est déclenchée.
    /// </returns>
    public static IReadOnlyDictionary<string, decimal> Map(
        IReadOnlyList<WithholdingReportByCategoryDto>? categories,
        decimal declaredTotal)
    {
        if (declaredTotal <= 0m)
            return Empty;

        return EnsureWithinDeclaredTotal(Accumulate(categories), declaredTotal);
    }

    /// <summary>
    /// Ventile les catégories factures sans garde-fou de total. Permet d'y fusionner ensuite
    /// la RS salariale, puis d'appliquer <see cref="EnsureWithinDeclaredTotal"/> une seule fois
    /// sur l'ensemble — sinon un dépassement factures abandonnerait aussi l'IRPP et la CSS.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> Accumulate(
        IReadOnlyList<WithholdingReportByCategoryDto>? categories)
    {
        var lines = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (categories is null || categories.Count == 0)
            return lines;

        foreach (var category in categories)
        {
            if (category.TotalWithheld <= 0m)
                continue;

            var line = ResolveLine(category);
            if (line is null)
                continue; // Catégorie sans ligne dédiée : reste agrégée dans le total.

            // Plusieurs catégories peuvent viser la même ligne officielle (ex. loyers et
            // commissions relèvent tous deux de l'article 4) : on cumule.
            lines[$"{line}.Amount"] = lines.GetValueOrDefault($"{line}.Amount") + category.TotalWithheld;

            if (category.TotalHT > 0m)
                lines[$"{line}.Base"] = lines.GetValueOrDefault($"{line}.Base") + category.TotalHT;
        }

        return lines;
    }

    /// <summary>
    /// Règle de sûreté : la somme ventilée ne peut jamais excéder le montant déclaré.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> EnsureWithinDeclaredTotal(
        IReadOnlyDictionary<string, decimal> lines,
        decimal declaredTotal)
    {
        if (lines.Count == 0)
            return Empty;

        var ventilated = lines.Where(kv => kv.Key.EndsWith(".Amount", StringComparison.Ordinal))
                              .Sum(kv => kv.Value);

        return ventilated > declaredTotal + Epsilon ? Empty : lines;
    }

    /// <summary>
    /// Détermine la ligne officielle d'une catégorie. Retourne <c>null</c> lorsque la catégorie
    /// n'a pas de ligne dédiée exploitable : son montant reste alors compris dans le total.
    /// </summary>
    private static string? ResolveLine(WithholdingReportByCategoryDto category)
    {
        var rate = EffectiveRatePercent(category);

        return category.Category switch
        {
            // Article 1 — salaires et traitements soumis au barème de droit commun.
            WithholdingCategory.Salaires => "Line1",

            // Articles 5 et 6 — honoraires. Le formulaire sépare selon le régime du bénéficiaire,
            // ce que le taux appliqué révèle sans ambiguïté : 3 % pour le régime réel / personnes
            // morales, 10 % pour les personnes physiques hors régime réel.
            WithholdingCategory.Honoraires => IsAbout(rate, 3m) ? "Line6" : "Line5",

            // Article 4 — commissions, courtages et loyers. Le formulaire distingue personnes
            // physiques et morales (10 % dans les deux cas : le taux ne permet pas de trancher).
            // Défaut retenu : personnes morales, cas dominant pour des factures fournisseurs.
            WithholdingCategory.Commissions or WithholdingCategory.Loyers => "Line4Entities",

            // Article 17 — acquisitions ≥ 1 000 D. Ici le taux identifie la sous-ligne.
            WithholdingCategory.Achats => IsAbout(rate, 1m) ? "Line17Rate1"
                : IsAbout(rate, 0.5m) ? "Line17Rate05"
                : IsAbout(rate, 1.5m) ? "Line17Rate15"
                : null,

            WithholdingCategory.RevenusCapitaux => "Line10",
            WithholdingCategory.Dividendes => "Line12",
            WithholdingCategory.ImmobilierFoncier => "Line16",

            // Articles 18 et 19 — retenue au titre de la TVA : 25 % sur les marchés publics,
            // 100 % pour les opérations avec des personnes non établies en Tunisie.
            WithholdingCategory.TVA => IsAbout(rate, 100m) ? "Line19" : "Line18",

            WithholdingCategory.PlusValues => "Line24",
            WithholdingCategory.NonResidents => "Line25",

            // Jeux (article 29) et « Autres » : lignes non calibrées à ce jour — le montant reste
            // porté par le total de la page 4.
            _ => null
        };
    }

    private static decimal EffectiveRatePercent(WithholdingReportByCategoryDto category)
        => category.TotalHT > 0m ? category.TotalWithheld / category.TotalHT * 100m : 0m;

    private static bool IsAbout(decimal value, decimal target) => Math.Abs(value - target) < 0.15m;
}
