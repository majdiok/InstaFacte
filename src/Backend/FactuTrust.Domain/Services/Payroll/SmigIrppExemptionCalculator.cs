using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Exonération IRPP SMIG — logique partagée entre le calcul mensuel et la régularisation annuelle.
///
/// <para>
/// <b>Avertissement de conformité (R-12 / CAL-006, vérifié 2026-08)</b> : aucune base légale
/// vérifiable n'a été trouvée pour le mode <see cref="SmigIrppExemptionMode.SmigPortion"/> tel
/// que codé (crédit mensuel <c>taux × min(net imposable ; SMIG)</c> appliqué à TOUS les salariés,
/// y compris ceux gagnant bien plus que le SMIG). Ce mode reste fonctionnel pour ne pas modifier
/// silencieusement les bulletins des tenants qui l'ont déjà activé, mais n'est plus recommandé —
/// voir <c>docs/payroll/smig-irpp-exemption.md</c> et l'avertissement de conformité qui y est
/// désormais documenté. Le changement de libellé UI (« recommandé » → avertissement explicite)
/// est un item frontend différé, voir <c>/code/.plans/audit/phase3-deferred.md</c>.
/// </para>
///
/// <para>
/// La règle légale vérifiable identifiée (art. 26 CIR — à faire confirmer par un fiscaliste avant
/// mise en avant commerciale) est une <b>déduction annuelle supplémentaire de 500 TND</b> de la
/// base imposable pour les salariés rémunérés au niveau du SMIG/SMAG, et non un crédit d'impôt.
/// Elle est exposée ici via <see cref="SmigAnnualDeductionAmount"/> et
/// <see cref="ComputeMonthlySmigAnnualDeductionEffect"/> comme fonctions pures, prêtes à être
/// câblées une fois le mode <c>SmigAnnualDeduction</c> ajouté à l'énumération
/// <see cref="SmigIrppExemptionMode"/> (fichier <c>PayrollEnums.cs</c>, hors périmètre de cette
/// vague — voir le fichier de report ci-dessus pour le câblage exact dans
/// <c>PayrollCalculator.cs</c>/<c>IrppRegularizationCalculator.cs</c>).
/// </para>
/// </summary>
public static class SmigIrppExemptionCalculator
{
    /// <summary>
    /// Déduction annuelle supplémentaire (TND) pour les salariés rémunérés au SMIG/SMAG — règle
    /// vérifiable distincte du crédit <see cref="SmigIrppExemptionMode.SmigPortion"/> (voir
    /// remarques de la classe). Source : art. 26 CIR (profiscal.com, paie-tunisie.com — vérifié
    /// 2026-08 ; libellé exact de l'éligibilité à confirmer par un fiscaliste, Q2 du plan).
    /// </summary>
    public const decimal SmigAnnualDeductionAmount = 500m;

    /// <summary>
    /// Un salarié est éligible à la déduction SMIG annuelle pour un mois donné si sa rémunération
    /// mensuelle totale imposable ne dépasse pas le SMIG mensuel de l'exercice.
    /// </summary>
    public static bool IsEligibleForSmigAnnualDeduction(decimal monthlyTaxableRemuneration, decimal monthlySmig) =>
        monthlySmig > 0 && monthlyTaxableRemuneration <= monthlySmig;

    /// <summary>
    /// Effet mensuel de la déduction SMIG annuelle (500 TND/an ÷ 12) sur la base imposable, nul si
    /// le salarié n'est pas éligible ce mois-ci. Fonction pure : le câblage dans le moteur mensuel
    /// (avant annualisation ×12) est différé, voir la remarque de classe.
    /// </summary>
    public static decimal ComputeMonthlySmigAnnualDeductionEffect(decimal monthlyTaxableRemuneration, decimal monthlySmig) =>
        IsEligibleForSmigAnnualDeduction(monthlyTaxableRemuneration, monthlySmig)
            ? R(SmigAnnualDeductionAmount / 12m)
            : 0m;

    /// <summary>
    /// Applique l'exonération IRPP SMIG sur un IRPP mensuel déjà calculé au barème progressif.
    /// </summary>
    /// <param name="irppBeforeExemption">IRPP mensuel avant exonération.</param>
    /// <param name="monthlyNetTaxable">
    /// Net imposable mensuel du salarié (base après CNSS, frais professionnels et déductions
    /// familiales) — utilisé à la fois comme assiette de la portion SMIG exonérée (mode
    /// <see cref="SmigIrppExemptionMode.SmigPortion"/>) et, depuis la correction R-12, comme
    /// critère d'éligibilité du mode <see cref="SmigIrppExemptionMode.FullIfBelow"/> : c'est la
    /// rémunération totale imposable du mois disponible dans les variables du moteur, pas
    /// seulement le salaire de base contractuel (un salarié au SMIG avec de fortes primes n'est
    /// plus exonéré à tort — CAL-006).
    /// </param>
    /// <param name="baseSalary">
    /// Salaire de base contractuel, conservé pour compatibilité de signature ; n'est plus utilisé
    /// pour l'éligibilité <c>FullIfBelow</c> (voir <paramref name="monthlyNetTaxable"/>).
    /// </param>
    public static SmigIrppExemptionResult ApplyMonthly(
        decimal irppBeforeExemption,
        decimal monthlyNetTaxable,
        decimal baseSalary,
        PayrollYearParameters parameters)
    {
        if (parameters.SmigIrppExemptionMode == SmigIrppExemptionMode.None || irppBeforeExemption <= 0)
        {
            return new SmigIrppExemptionResult(irppBeforeExemption, 0m);
        }

        return parameters.SmigIrppExemptionMode switch
        {
            SmigIrppExemptionMode.SmigPortion => ApplySmigPortion(
                irppBeforeExemption,
                monthlyNetTaxable,
                parameters.MonthlySmig,
                parameters.ResolveSmigExemptionRate()),
            SmigIrppExemptionMode.FullIfBelow when monthlyNetTaxable <= parameters.MonthlySmig =>
                new SmigIrppExemptionResult(0m, irppBeforeExemption),
            _ => new SmigIrppExemptionResult(irppBeforeExemption, 0m)
        };
    }

    /// <summary>
    /// Applique l'exonération IRPP SMIG sur l'IRPP dû au cumul annuel (régularisation).
    /// </summary>
    /// <param name="months">
    /// Mois de l'exercice pris en compte. Depuis la correction R-12, l'éligibilité
    /// <see cref="SmigIrppExemptionMode.FullIfBelow"/> se juge sur
    /// <see cref="IrppRegularizationMonth.MonthlyNetTaxable"/> (rémunération totale imposable de
    /// chaque mois) plutôt que sur le seul salaire de base contractuel.
    /// </param>
    public static SmigIrppExemptionResult ApplyOnCumul(
        decimal irppDueBeforeExemption,
        decimal cumulNetTaxable,
        IReadOnlyList<IrppRegularizationMonth> months,
        PayrollYearParameters parameters)
    {
        if (parameters.SmigIrppExemptionMode == SmigIrppExemptionMode.None || irppDueBeforeExemption <= 0)
        {
            return new SmigIrppExemptionResult(irppDueBeforeExemption, 0m);
        }

        return parameters.SmigIrppExemptionMode switch
        {
            SmigIrppExemptionMode.SmigPortion => ApplySmigPortion(
                irppDueBeforeExemption,
                cumulNetTaxable,
                parameters.MonthlySmig * months.Count,
                parameters.ResolveSmigExemptionRate()),
            SmigIrppExemptionMode.FullIfBelow when months.Count > 0
                && months.All(m => m.MonthlyNetTaxable <= parameters.MonthlySmig) =>
                new SmigIrppExemptionResult(0m, irppDueBeforeExemption),
            _ => new SmigIrppExemptionResult(irppDueBeforeExemption, 0m)
        };
    }

    private static SmigIrppExemptionResult ApplySmigPortion(
        decimal irppBeforeExemption,
        decimal taxableBase,
        decimal smigCap,
        decimal ratePercent)
    {
        var exemptedBase = Math.Min(taxableBase, smigCap);
        var exemption = R(exemptedBase * ratePercent / 100m);
        var irppFinal = Math.Max(0m, R(irppBeforeExemption - exemption));
        return new SmigIrppExemptionResult(irppFinal, R(irppBeforeExemption - irppFinal));
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>Résultat d'une exonération IRPP SMIG.</summary>
public readonly record struct SmigIrppExemptionResult(decimal IrppFinal, decimal ExemptionAmount);
