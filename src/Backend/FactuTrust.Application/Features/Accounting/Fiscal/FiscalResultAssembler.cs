using System.Globalization;
using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

/// <summary>
/// Assemble le DTO de la feuille de détermination du résultat fiscal (entité → DTO) et déclenche le
/// calcul de l'impôt via le moteur pur. Partagé par les handlers get / upsert.
///
/// Applique en amont du moteur les contrôles fiscaux sur les reports : un déficit ordinaire prescrit
/// (au-delà de la durée de report) n'est pas imputable, et l'imputation ne peut excéder le stock
/// disponible. Les écarts constatés sont remontés en avertissements non bloquants.
/// </summary>
public static class FiscalResultAssembler
{
    public static FiscalResultDeclarationDto Assemble(
        FiscalResultDeclaration d, IncomeTaxYearParameter p, bool enabled, decimal suggestedLocalTurnoverTtc = 0m,
        decimal? suggestedAccountingResult = null)
    {
        var adjustments = d.Adjustments
            .Select(a => new FiscalAdjustmentLineDto
            {
                CatalogCode = a.CatalogCode,
                Kind = (int)a.Kind,
                Label = a.Label,
                Amount = a.Amount,
                IsAutoSuggested = a.IsAutoSuggested
            })
            .ToList();

        var carry = d.CarryForwards
            .Select(c => new FiscalCarryForwardDto
            {
                Kind = (int)c.Kind,
                OriginYear = c.OriginYear,
                InitialAmount = c.InitialAmount,
                ImputedThisYear = c.ImputedThisYear,
                ExpiryYear = c.ExpiryYear
            })
            .ToList();

        var appliedRate = d.AppliedIsRate > 0 ? d.AppliedIsRate : p.IsStandardRate;
        var warnings = new List<string>();
        var computation = Compute(d.FiscalYear, d.TaxpayerKind, d.AccountingResult, adjustments, carry, appliedRate,
            d.LocalTurnoverTtc, d.AcomptesPaid, d.WithholdingSuffered, d.PriorTaxCredit, d.MinimumTaxRegime, p, warnings);

        return new FiscalResultDeclarationDto
        {
            FiscalYear = d.FiscalYear,
            TaxpayerKind = (int)d.TaxpayerKind,
            Status = (int)d.Status,
            AccountingResult = d.AccountingResult,
            AppliedIsRate = appliedRate,
            LocalTurnoverTtc = d.LocalTurnoverTtc,
            MinimumTaxRegime = (int)d.MinimumTaxRegime,
            SuggestedLocalTurnoverTtc = suggestedLocalTurnoverTtc,
            SuggestedAccountingResult = suggestedAccountingResult,
            AcomptesPaid = d.AcomptesPaid,
            WithholdingSuffered = d.WithholdingSuffered,
            PriorTaxCredit = d.PriorTaxCredit,
            Adjustments = adjustments,
            CarryForwards = carry,
            Computation = computation,
            IsFinalized = d.Status == FiscalDeclarationStatus.Finalized,
            FinalizedAt = d.FinalizedAt,
            IsNew = false,
            FiscalLiasseEnabled = enabled,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Aperçu initial (non persisté) d'un exercice sans feuille : résultat comptable NET (après impôt),
    /// suggestions automatiques et CA local TTC repris de la comptabilité.
    /// </summary>
    public static FiscalResultDeclarationDto AssembleNew(
        int fiscalYear, decimal accountingNetResult,
        IReadOnlyList<FiscalAdjustmentLineDto> suggestions, IncomeTaxYearParameter p, bool enabled,
        decimal suggestedLocalTurnoverTtc = 0m)
    {
        var carry = Array.Empty<FiscalCarryForwardDto>();
        var appliedRate = p.IsStandardRate;
        var warnings = new List<string>();
        // Le CA suggéré sert de valeur initiale : sans lui le minimum d'impôt serait figé à son plancher.
        var computation = Compute(fiscalYear, TaxpayerKind.CorporateIS, accountingNetResult, suggestions, carry, appliedRate,
            suggestedLocalTurnoverTtc, 0m, 0m, 0m, MinimumTaxRegime.Standard, p, warnings);

        return new FiscalResultDeclarationDto
        {
            FiscalYear = fiscalYear,
            TaxpayerKind = (int)TaxpayerKind.CorporateIS,
            Status = (int)FiscalDeclarationStatus.Draft,
            AccountingResult = accountingNetResult,
            AppliedIsRate = appliedRate,
            LocalTurnoverTtc = suggestedLocalTurnoverTtc,
            MinimumTaxRegime = (int)MinimumTaxRegime.Standard,
            SuggestedLocalTurnoverTtc = suggestedLocalTurnoverTtc,
            SuggestedAccountingResult = accountingNetResult,
            Adjustments = suggestions,
            CarryForwards = carry,
            Computation = computation,
            IsFinalized = false,
            IsNew = true,
            FiscalLiasseEnabled = enabled,
            Warnings = warnings
        };
    }

    private static IncomeTaxComputationDto Compute(
        int fiscalYear, TaxpayerKind kind, decimal accountingResult,
        IReadOnlyList<FiscalAdjustmentLineDto> adjustments, IReadOnlyList<FiscalCarryForwardDto> carry,
        decimal appliedRate, decimal localTurnoverTtc, decimal acomptes, decimal withholding, decimal priorCredit,
        MinimumTaxRegime minimumTaxRegime, IncomeTaxYearParameter p, List<string> warnings)
    {
        var totalReint = adjustments.Where(a => a.Kind == (int)FiscalAdjustmentKind.Reintegration).Sum(a => a.Amount);
        var totalDeduc = adjustments.Where(a => a.Kind == (int)FiscalAdjustmentKind.Deduction).Sum(a => a.Amount);

        var (deficitImputed, deferredImputed) = ResolveEligibleCarryForwards(fiscalYear, carry, warnings);

        var input = new IncomeTaxComputationInput(
            kind, accountingResult, totalReint, totalDeduc, deficitImputed, deferredImputed,
            appliedRate, localTurnoverTtc,
            p.MinTaxRate, p.MinTaxFloorTnd, p.CssApplies, p.CssRate, p.CssFloorTnd,
            IrppScale.Parse(p.IrppBracketsJson),
            acomptes, withholding, priorCredit,
            minimumTaxRegime, p.MinTaxReducedRate, p.MinTaxFloorReducedTnd, p.RoundTaxableToDinar);

        return IncomeTaxComputationService.Compute(input);
    }

    /// <summary>
    /// Estime le chiffre d'affaires local TTC de l'exercice à partir d'une balance :
    /// produits d'exploitation (classe 70) + TVA collectée (comptes 4367x), en crédit − débit.
    /// Assiette du minimum d'impôt. Indicatif : le comptable ajuste (ex. exclusion du CA export).
    /// </summary>
    public static decimal EstimateLocalTurnoverTtc(IReadOnlyList<BalanceRowDto> rows)
    {
        decimal total = 0m;
        foreach (var row in rows)
        {
            if (row.AccountNumber.StartsWith("70", StringComparison.Ordinal)
                || row.AccountNumber.StartsWith("4367", StringComparison.Ordinal))
            {
                total += row.MovementCredit - row.MovementDebit;
            }
        }
        return total > 0m ? Math.Round(total, 3) : 0m;
    }

    /// <summary>
    /// Retient les seuls reports fiscalement imputables sur l'exercice :
    /// déficit ordinaire non prescrit (<c>ExpiryYear >= exercice</c>) et imputation plafonnée au stock.
    /// Les amortissements réputés différés sont reportables sans limite de durée.
    /// </summary>
    private static (decimal Deficits, decimal Deferred) ResolveEligibleCarryForwards(
        int fiscalYear, IReadOnlyList<FiscalCarryForwardDto> carry, List<string> warnings)
    {
        decimal deficits = 0m, deferred = 0m;

        foreach (var c in carry)
        {
            var requested = Math.Max(c.ImputedThisYear, 0m);
            if (requested <= 0m)
                continue;

            var isDeficit = c.Kind == (int)FiscalCarryForwardKind.Deficit;
            var label = isDeficit ? "Déficit" : "Amortissement différé";

            // Prescription : applicable aux seuls déficits ordinaires.
            if (isDeficit && c.ExpiryYear.HasValue && c.ExpiryYear.Value < fiscalYear)
            {
                warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} d'origine {1} prescrit (imputable jusqu'à {2}) : non imputé sur l'exercice {3}.",
                    label, c.OriginYear, c.ExpiryYear.Value, fiscalYear));
                continue;
            }

            var available = Math.Max(c.InitialAmount, 0m);
            var eligible = Math.Min(requested, available);
            if (requested > available)
            {
                warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} d'origine {1} : imputation ramenée de {2:N3} à {3:N3} (stock reportable disponible).",
                    label, c.OriginYear, requested, eligible));
            }

            if (isDeficit)
                deficits += eligible;
            else
                deferred += eligible;
        }

        // ── T12 / Option 2 (art. 8 code IRPP/IS) : avertissements non bloquants au brouillon ──
        // Ces mêmes règles deviennent BLOQUANTES à la finalisation (T10 étape 5). Au stade brouillon,
        // elles sont simplement signalées pour que le comptable corrige l'ordre d'imputation.
        AddCarryForwardOrderingWarnings(fiscalYear, carry, warnings);

        return (deficits, deferred);
    }

    /// <summary>
    /// Avertissements d'ordre d'imputation des reports (T12, Option 2) — non bloquants au brouillon :
    /// (a) ordre FIFO non respecté : un déficit ordinaire d'origine plus récente est imputé alors qu'un
    /// plus ancien imputable (montant restant > 0, non périmé) n'est pas intégralement imputé ;
    /// (b) amortissement différé imputé alors qu'un déficit ordinaire imputable subsiste.
    /// </summary>
    private static void AddCarryForwardOrderingWarnings(
        int fiscalYear, IReadOnlyList<FiscalCarryForwardDto> carry, List<string> warnings)
    {
        var deficits = carry
            .Where(c => c.Kind == (int)FiscalCarryForwardKind.Deficit)
            .OrderBy(c => c.OriginYear)
            .ToList();

        // (a) FIFO : pour chaque déficit ordinaire imputé, vérifier qu'aucun déficit plus ancien
        // et encore imputable n'a été laissé en stock.
        foreach (var imputed in deficits.Where(c => c.ImputedThisYear > 0m))
        {
            foreach (var older in deficits.Where(o => o.OriginYear < imputed.OriginYear))
            {
                var expired = older.ExpiryYear.HasValue && older.ExpiryYear.Value < fiscalYear;
                if (expired)
                    continue;
                var remaining = Math.Max(older.InitialAmount, 0m) - Math.Max(older.ImputedThisYear, 0m);
                if (remaining > 0.005m)
                {
                    warnings.Add(string.Format(CultureInfo.InvariantCulture,
                        "Ordre d'imputation FIFO non respecté : le déficit d'origine {0} est imputé " +
                        "alors que le déficit plus ancien d'origine {1} dispose encore d'un stock imputable " +
                        "de {2:N3} TND (imputer en priorité les déficits les plus anciens).",
                        imputed.OriginYear, older.OriginYear, remaining));
                    break; // un avertissement par déficit imputé hors-ordre suffit
                }
            }
        }

        // (b) Amortissement différé imputé avant épuisement des déficits ordinaires imputables.
        var hasDeferredImputed = carry.Any(c =>
            c.Kind == (int)FiscalCarryForwardKind.DeferredDepreciation && c.ImputedThisYear > 0m);
        if (hasDeferredImputed)
        {
            foreach (var d in deficits)
            {
                var expired = d.ExpiryYear.HasValue && d.ExpiryYear.Value < fiscalYear;
                if (expired)
                    continue;
                var remaining = Math.Max(d.InitialAmount, 0m) - Math.Max(d.ImputedThisYear, 0m);
                if (remaining > 0.005m)
                {
                    warnings.Add(string.Format(CultureInfo.InvariantCulture,
                        "Amortissement différé imputé alors que le déficit ordinaire d'origine {0} dispose " +
                        "encore d'un stock imputable de {1:N3} TND (imputer les déficits ordinaires avant les " +
                        "amortissements réputés différés).",
                        d.OriginYear, remaining));
                    break;
                }
            }
        }
    }
}
