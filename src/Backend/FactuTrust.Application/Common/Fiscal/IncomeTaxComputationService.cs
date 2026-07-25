using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Fiscal;

/// <summary>
/// Données d'entrée du calcul de l'impôt (toutes fournies par l'appelant — service sans I/O).
/// </summary>
public sealed record IncomeTaxComputationInput(
    TaxpayerKind TaxpayerKind,
    decimal AccountingResult,
    decimal TotalReintegrations,
    decimal TotalDeductions,
    decimal DeficitsImputed,
    decimal DeferredDepreciationImputed,
    decimal AppliedIsRate,
    decimal LocalTurnoverTtc,
    decimal MinTaxRate,
    decimal MinTaxFloorTnd,
    bool CssApplies,
    decimal CssRate,
    decimal CssFloorTnd,
    IReadOnlyList<IrppBracket> IrppBrackets,
    decimal AcomptesPaid,
    decimal WithholdingSuffered,
    decimal PriorTaxCredit,
    // ── Paramètres additionnels (valeurs par défaut = comportement historique inchangé) ──────
    /// <summary>Régime de minimum d'impôt : droit commun, réduit ou exonéré.</summary>
    MinimumTaxRegime MinimumTaxRegime = MinimumTaxRegime.Standard,
    /// <summary>Taux du minimum d'impôt au régime réduit (0,1 % en référence).</summary>
    decimal MinTaxReducedRate = 0m,
    /// <summary>Plancher du minimum d'impôt au régime réduit (300 TND en référence).</summary>
    decimal MinTaxFloorReducedTnd = 0m,
    /// <summary>Arrondir l'assiette imposable au dinar inférieur avant application du taux/barème.</summary>
    bool RoundTaxableToDinar = false);

/// <summary>
/// Moteur PUR de détermination du résultat fiscal et de calcul de l'impôt (IS ou IRPP-BIC).
/// Aucune dépendance ni I/O : entièrement testable. Toute la logique volatile (taux, planchers) est
/// injectée via <see cref="IncomeTaxComputationInput"/> (résolue depuis les paramètres d'exercice).
/// </summary>
public static class IncomeTaxComputationService
{
    private static decimal R(decimal v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    public static IncomeTaxComputationDto Compute(IncomeTaxComputationInput i)
    {
        var reint = i.TotalReintegrations < 0 ? 0 : i.TotalReintegrations;
        var deduc = i.TotalDeductions < 0 ? 0 : i.TotalDeductions;

        var resultBeforeCarryForward = i.AccountingResult + reint - deduc;

        // Les reports ne s'imputent que sur un résultat positif ; déficits d'abord, puis amortissements différés.
        var positiveBase = Math.Max(resultBeforeCarryForward, 0m);
        var deficitsImputed = Math.Min(Math.Max(i.DeficitsImputed, 0m), positiveBase);
        var afterDeficits = positiveBase - deficitsImputed;
        var deferredImputed = Math.Min(Math.Max(i.DeferredDepreciationImputed, 0m), afterDeficits);
        var taxable = afterDeficits - deferredImputed;

        // Les déclarations fiscales tunisiennes retiennent l'assiette arrondie au dinar inférieur.
        if (i.RoundTaxableToDinar)
            taxable = Math.Floor(taxable);

        var deficitGenerated = resultBeforeCarryForward < 0 ? -resultBeforeCarryForward : 0m;

        var taxOnResult = i.TaxpayerKind == TaxpayerKind.IndividualIrppBic
            ? ComputeIrpp(taxable, i.IrppBrackets)
            : R(taxable * i.AppliedIsRate);

        // Minimum d'impôt sur CA local TTC (plancher applicable même en déficit), selon le régime :
        // droit commun, réduit, ou exonéré (société nouvellement créée, ZDR, totalement exportatrice…).
        var localTurnover = Math.Max(i.LocalTurnoverTtc, 0m);
        var minimumTax = i.MinimumTaxRegime switch
        {
            MinimumTaxRegime.Exempt => 0m,
            MinimumTaxRegime.Reduced =>
                Math.Max(R(i.MinTaxReducedRate * localTurnover), Math.Max(i.MinTaxFloorReducedTnd, 0m)),
            _ => Math.Max(R(i.MinTaxRate * localTurnover), Math.Max(i.MinTaxFloorTnd, 0m))
        };

        var taxDue = Math.Max(taxOnResult, minimumTax);

        var css = i.CssApplies ? Math.Max(R(i.CssRate * taxable), Math.Max(i.CssFloorTnd, 0m)) : 0m;

        var totalTaxDue = taxDue + css;

        var credits = Math.Max(i.AcomptesPaid, 0m) + Math.Max(i.WithholdingSuffered, 0m) + Math.Max(i.PriorTaxCredit, 0m);
        var balance = totalTaxDue - credits;

        return new IncomeTaxComputationDto
        {
            AccountingResult = R(i.AccountingResult),
            TotalReintegrations = R(reint),
            TotalDeductions = R(deduc),
            ResultBeforeCarryForward = R(resultBeforeCarryForward),
            DeficitsImputed = R(deficitsImputed),
            DeferredDepreciationImputed = R(deferredImputed),
            TaxableResult = R(taxable),
            DeficitGeneratedThisYear = R(deficitGenerated),
            TaxpayerKind = (int)i.TaxpayerKind,
            AppliedIsRate = i.AppliedIsRate,
            TaxOnResult = R(taxOnResult),
            MinimumTaxRegime = (int)i.MinimumTaxRegime,
            MinimumTax = R(minimumTax),
            TaxDue = R(taxDue),
            Css = R(css),
            TotalTaxDue = R(totalTaxDue),
            AcomptesPaid = R(Math.Max(i.AcomptesPaid, 0m)),
            WithholdingSuffered = R(Math.Max(i.WithholdingSuffered, 0m)),
            PriorTaxCredit = R(Math.Max(i.PriorTaxCredit, 0m)),
            NetToPay = balance > 0 ? R(balance) : 0m,
            CreditToCarry = balance < 0 ? R(-balance) : 0m
        };
    }

    /// <summary>Barème IRPP progressif : impôt par tranches (Rate en %, dernière tranche illimitée).</summary>
    public static decimal ComputeIrpp(decimal taxable, IReadOnlyList<IrppBracket> brackets)
    {
        if (taxable <= 0 || brackets is null || brackets.Count == 0)
            return 0m;

        var ordered = brackets.OrderBy(b => b.Lower).ToList();
        decimal tax = 0m;
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var lower = ordered[idx].Lower;
            if (taxable <= lower)
                break;
            var upper = idx + 1 < ordered.Count ? ordered[idx + 1].Lower : decimal.MaxValue;
            var portion = Math.Min(taxable, upper) - lower;
            if (portion > 0)
                tax += portion * ordered[idx].Rate / 100m;
        }
        return R(tax);
    }
}
