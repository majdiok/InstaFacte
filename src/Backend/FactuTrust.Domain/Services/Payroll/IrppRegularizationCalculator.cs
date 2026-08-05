using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcul de la régularisation IRPP/CSS annuelle — fonction pure et déterministe.
///
/// Le moteur mensuel (<see cref="PayrollCalculator"/>) annualise l'impôt par *projection*
/// (net imposable du mois × 12). Dès que la rémunération varie au fil de l'année (prime,
/// heures supplémentaires exceptionnelles, embauche en cours d'année, congé sans solde,
/// changement de situation familiale), la somme des retenues mensuelles s'écarte de l'impôt
/// réellement dû sur le revenu annuel.
///
/// Cette classe compare l'impôt dû sur le **cumul réel** de l'année à l'impôt **déjà retenu**
/// et produit l'écart : positif pour un rappel, négatif pour une restitution.
///
/// Le barème et les seuils proviennent intégralement de <see cref="PayrollYearParameters"/> :
/// aucune valeur légale n'est codée ici.
/// </summary>
public static class IrppRegularizationCalculator
{
    /// <summary>
    /// Calcule la régularisation à partir des cumuls mensuels de l'exercice.
    /// </summary>
    /// <param name="months">
    /// Bulletins de l'année pris en compte : les mois antérieurs proviennent des cycles
    /// **Validés ou Clôturés** (source figée, donc stable d'un calcul à l'autre), le mois
    /// courant du cycle en cours de calcul. Les montants attendus sont l'IRPP et la CSS
    /// **mensuels purs**, hors régularisation — c'est ce qui rend l'opération idempotente.
    /// </param>
    /// <param name="parameters">Paramètres légaux de l'exercice (barème, taux et seuil CSS).</param>
    public static IrppRegularizationResult Compute(
        IReadOnlyList<IrppRegularizationMonth> months,
        PayrollYearParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(months);
        ArgumentNullException.ThrowIfNull(parameters);

        var cumulNetTaxable = R(months.Sum(m => m.MonthlyNetTaxable));
        if (cumulNetTaxable < 0) cumulNetTaxable = 0m;

        var cumulIrppWithheld = R(months.Sum(m => m.Irpp));
        var cumulCssWithheld = R(months.Sum(m => m.Css));

        // Le barème annuel s'applique au cumul réel, sans proratisation des tranches : un
        // salarié entré en cours d'année a un revenu annuel effectivement plus faible, et la
        // restitution qui en découle est le comportement attendu (cf. docs/paie.md).
        var irppDueBeforeExemption = PayrollCalculator.ComputeProgressiveTax(cumulNetTaxable, parameters);

        var smigExemptionResult = SmigIrppExemptionCalculator.ApplyOnCumul(
            irppDueBeforeExemption, cumulNetTaxable, months, parameters);
        var irppDue = smigExemptionResult.IrppFinal;

        var cssDue = 0m;
        if (cumulNetTaxable > parameters.CssAnnualExemptionThreshold)
            cssDue = R(cumulNetTaxable * parameters.CssRate / 100m);

        return new IrppRegularizationResult
        {
            MonthsCounted = months.Count,
            CumulNetTaxable = cumulNetTaxable,
            CumulIrppWithheld = cumulIrppWithheld,
            CumulCssWithheld = cumulCssWithheld,
            IrppDue = irppDue,
            CssDue = cssDue,
            IrppDelta = R(irppDue - cumulIrppWithheld),
            CssDelta = R(cssDue - cumulCssWithheld)
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Contribution d'un mois au cumul annuel : montants figés lus sur le bulletin.
/// </summary>
/// <param name="Month">Mois civil (1 à 12).</param>
/// <param name="MonthlyNetTaxable">Net imposable mensuel ayant servi de base au barème.</param>
/// <param name="Irpp">IRPP mensuel pur retenu, hors régularisation.</param>
/// <param name="Css">CSS mensuelle pure retenue, hors régularisation.</param>
/// <param name="BaseSalary">Salaire de base mensuel (pour éligibilité exonération SMIG).</param>
public sealed record IrppRegularizationMonth(
    int Month,
    decimal MonthlyNetTaxable,
    decimal Irpp,
    decimal Css,
    decimal BaseSalary = 0m);

/// <summary>
/// Résultat d'une régularisation : cumuls, impôt dû et écarts à porter sur le bulletin.
/// </summary>
public sealed class IrppRegularizationResult
{
    /// <summary>Nombre de mois entrant dans le cumul (utile pour détecter une année partielle).</summary>
    public int MonthsCounted { get; init; }

    /// <summary>Cumul du net imposable de l'année.</summary>
    public decimal CumulNetTaxable { get; init; }
    /// <summary>Cumul de l'IRPP déjà retenu (hors régularisation).</summary>
    public decimal CumulIrppWithheld { get; init; }
    /// <summary>Cumul de la CSS déjà retenue (hors régularisation).</summary>
    public decimal CumulCssWithheld { get; init; }

    /// <summary>IRPP réellement dû sur le cumul, barème de l'exercice appliqué.</summary>
    public decimal IrppDue { get; init; }
    /// <summary>CSS réellement due sur le cumul (nulle sous le seuil d'exonération).</summary>
    public decimal CssDue { get; init; }

    /// <summary>Écart IRPP : positif = rappel à prélever, négatif = restitution à reverser.</summary>
    public decimal IrppDelta { get; init; }
    /// <summary>Écart CSS, même convention de signe que <see cref="IrppDelta"/>.</summary>
    public decimal CssDelta { get; init; }

    /// <summary>Écart total, toutes contributions confondues.</summary>
    public decimal TotalDelta => Math.Round(IrppDelta + CssDelta, 3, MidpointRounding.AwayFromZero);

    /// <summary>Vrai si la régularisation aboutit à un rappel (prélèvement complémentaire).</summary>
    public bool IsAdditionalWithholding => TotalDelta > 0;

    /// <summary>Vrai si aucun écart n'est constaté : rien à porter sur le bulletin.</summary>
    public bool IsNeutral => TotalDelta == 0m;
}
