using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Moteur de calcul des amortissements — norme tunisienne (NCT 5, Décret 2008-492).
/// Prorata temporis en jours, base 360 (convention 30/360 : mois de 30 jours).
/// Méthodes supportées :
///   • Linéaire (droit commun) ;
///   • Accélérée (Décret 2008-492 art. 2 — taux linéaire × coefficient 1,5 ou 2 pour le matériel
///     industriel multi-équipes : annuité constante sur base constante, prorata temporis 1ʳᵉ année) ;
///   • Intégrale (Décret 2008-492 art. 4 — biens de faible valeur, dotation unique sans prorata).
/// </summary>
public sealed class DepreciationEngine : IDepreciationEngine
{
    private const decimal DaysPerYear = 360m;

    public IReadOnlyList<DepreciationScheduleLine> GenerateSchedule(FixedAsset asset, int? throughFiscalYear = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.DepreciationRatePercent <= 0 || asset.InServiceDate is null)
            return Array.Empty<DepreciationScheduleLine>();

        var baseAmount = asset.DepreciableBase;
        if (baseAmount <= 0)
            return Array.Empty<DepreciationScheduleLine>();

        return asset.DepreciationMethod switch
        {
            DepreciationMethod.Integral => GenerateIntegralSchedule(asset, baseAmount),
            DepreciationMethod.Accelerated => GenerateAcceleratedSchedule(asset, baseAmount, throughFiscalYear),
            _ => GenerateLinearSchedule(asset, baseAmount, throughFiscalYear, effectiveRatePercent: asset.DepreciationRatePercent)
        };
    }

    public decimal CalculateDisposalYearDepreciation(
        FixedAsset asset,
        int fiscalYear,
        decimal priorAccumulatedDepreciation)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.InServiceDate is null || asset.DisposalDate is null)
            return 0m;
        if (asset.DepreciationRatePercent <= 0)
            return 0m;

        var baseAmount = asset.DepreciableBase;
        if (baseAmount <= 0 || priorAccumulatedDepreciation >= baseAmount)
            return 0m;

        var lines = GenerateSchedule(asset);
        var line = lines.FirstOrDefault(l => l.FiscalYear == fiscalYear);
        if (line is null)
            return 0m;

        return Math.Max(0m, Math.Min(line.DepreciationAmount, baseAmount - priorAccumulatedDepreciation));
    }

    // ---------------------------------------------------------------
    // Linéaire (et accéléré, via un taux effectif)
    // ---------------------------------------------------------------

    private static IReadOnlyList<DepreciationScheduleLine> GenerateLinearSchedule(
        FixedAsset asset,
        decimal baseAmount,
        int? throughFiscalYear,
        decimal effectiveRatePercent)
    {
        var inService = asset.InServiceDate!.Value.Date;
        var normalAnnual = Round3(baseAmount * effectiveRatePercent / 100m);
        if (normalAnnual <= 0)
            return Array.Empty<DepreciationScheduleLine>();

        var effectiveLifeYears = effectiveRatePercent > 0 ? 100m / effectiveRatePercent : asset.UsefulLifeYears;
        var startYear = inService.Year;
        var endYear = throughFiscalYear ?? EstimateEndYear(startYear, effectiveLifeYears, asset.DisposalDate);
        if (asset.DisposalDate is not null)
            endYear = Math.Min(endYear, asset.DisposalDate.Value.Year);

        var lines = new List<DepreciationScheduleLine>();
        decimal priorAccumulated = 0m;
        decimal openingNbv = asset.TotalCapitalizedCost;

        for (var year = startYear; year <= endYear; year++)
        {
            var amount = CalculateLinearYearAmount(asset, year, normalAnnual, baseAmount, priorAccumulated);
            if (amount <= 0 && priorAccumulated >= baseAmount)
                break;

            amount = Math.Min(amount, baseAmount - priorAccumulated);
            if (amount < 0)
                amount = 0;

            AppendLine(lines, asset, year, openingNbv, normalAnnual, priorAccumulated, amount);
            priorAccumulated = Round3(priorAccumulated + amount);
            openingNbv = lines[^1].ClosingNbv;

            if (priorAccumulated >= baseAmount)
                break;
        }

        AdjustFinalLineRounding(lines, baseAmount, asset.ResidualValue, asset.TotalCapitalizedCost);
        return lines;
    }

    private static decimal CalculateLinearYearAmount(
        FixedAsset asset,
        int fiscalYear,
        decimal normalAnnual,
        decimal baseAmount,
        decimal priorAccumulated)
    {
        if (priorAccumulated >= baseAmount)
            return 0m;

        var inService = asset.InServiceDate!.Value.Date;
        var startYear = inService.Year;

        if (fiscalYear < startYear)
            return 0m;

        decimal amount;
        if (asset.DisposalDate is not null && fiscalYear == asset.DisposalDate.Value.Year)
        {
            // Année de sortie : prorata en jours/360 jusqu'à la date de cession ; si la cession a
            // lieu l'année de mise en service, le prorata court de la mise en service à la cession.
            var days = fiscalYear == startYear
                ? Days360Between(inService, asset.DisposalDate.Value.Date)
                : Days360FromYearStart(asset.DisposalDate.Value.Date);
            amount = Round3(normalAnnual * days / DaysPerYear);
        }
        else if (fiscalYear == startYear)
        {
            // Première année : prorata en jours/360 de la mise en service au 31/12 (jour inclus).
            var days = Days360RemainingInYear(inService);
            amount = Round3(normalAnnual * days / DaysPerYear);
        }
        else
        {
            amount = normalAnnual;
        }

        return Math.Min(amount, baseAmount - priorAccumulated);
    }

    // ---------------------------------------------------------------
    // Accéléré (Décret 2008-492 art. 2)
    // Taux effectif = taux linéaire × coefficient (1,5 ou 2), annuité constante
    // sur base constante, prorata jours/360 la 1ʳᵉ année — donc équivalent au
    // plan linéaire avec un taux relevé. La mécanique linéaire est réutilisée.
    // ---------------------------------------------------------------

    private static IReadOnlyList<DepreciationScheduleLine> GenerateAcceleratedSchedule(
        FixedAsset asset,
        decimal baseAmount,
        int? throughFiscalYear)
    {
        var coefficient = asset.AccelerationCoefficient <= 1m ? 1m : asset.AccelerationCoefficient;
        var effectiveRate = asset.DepreciationRatePercent * coefficient;
        return GenerateLinearSchedule(asset, baseAmount, throughFiscalYear, effectiveRate);
    }

    // ---------------------------------------------------------------
    // Intégral (dotation unique, sans prorata)
    // ---------------------------------------------------------------

    private static IReadOnlyList<DepreciationScheduleLine> GenerateIntegralSchedule(FixedAsset asset, decimal baseAmount)
    {
        var fiscalYear = asset.InServiceDate!.Value.Year;
        var lines = new List<DepreciationScheduleLine>();
        AppendLine(lines, asset, fiscalYear, asset.TotalCapitalizedCost, baseAmount, 0m, Round3(baseAmount));
        return lines;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static void AppendLine(
        List<DepreciationScheduleLine> lines,
        FixedAsset asset,
        int fiscalYear,
        decimal openingNbv,
        decimal normalAnnual,
        decimal priorAccumulated,
        decimal amount)
    {
        var accumulated = Round3(priorAccumulated + amount);
        var closingNbv = Round3(Math.Max(asset.ResidualValue, asset.TotalCapitalizedCost - accumulated));

        var lineResult = DepreciationScheduleLine.Create(
            asset.Id,
            fiscalYear,
            periodMonth: null,
            openingNbv,
            normalAnnual,
            priorAccumulated,
            amount,
            accumulated,
            closingNbv);

        if (lineResult.IsFailure)
            throw new InvalidOperationException(lineResult.Error.Description);

        lines.Add(lineResult.Value);
    }

    private static int EstimateEndYear(int startYear, decimal usefulLifeYears, DateTime? disposalDate)
    {
        if (disposalDate is not null)
            return disposalDate.Value.Year;

        var extra = usefulLifeYears <= 0 ? 20 : (int)Math.Ceiling(usefulLifeYears) + 1;
        return startYear + extra;
    }

    /// <summary>
    /// Ajuste la dernière annuité pour solder exactement la base amortissable
    /// (absorption des écarts d'arrondi), uniquement lorsque le plan va à son terme.
    /// </summary>
    private static void AdjustFinalLineRounding(
        List<DepreciationScheduleLine> lines,
        decimal baseAmount,
        decimal residualValue,
        decimal totalCost)
    {
        if (lines.Count == 0)
            return;

        var last = lines[^1];

        // Plan interrompu (cession avant terme) ou non soldé : aucun ajustement.
        if (Math.Abs(last.ClosingNbv - residualValue) > 0.001m)
            return;

        var expectedAccumulated = Round3(baseAmount);
        var drift = expectedAccumulated - last.AccumulatedDepreciation;
        if (drift == 0)
            return;

        var adjustedAmount = Round3(last.DepreciationAmount + drift);
        var adjustedAccumulated = Round3(last.AccumulatedDepreciation + drift);
        var adjustedClosing = Round3(Math.Max(residualValue, totalCost - adjustedAccumulated));

        var replacement = DepreciationScheduleLine.Create(
            last.FixedAssetId,
            last.FiscalYear,
            last.PeriodMonth,
            last.OpeningNbv,
            last.NormalAnnualAmount,
            last.PriorAccumulatedDepreciation,
            adjustedAmount,
            adjustedAccumulated,
            adjustedClosing);

        if (replacement.IsSuccess)
            lines[^1] = replacement.Value;
    }

    /// <summary>Jours écoulés depuis le 1er janvier (inclus) jusqu'à la date donnée, convention 30/360.</summary>
    internal static decimal Days360FromYearStart(DateTime date) =>
        (date.Month - 1) * 30 + Math.Min(date.Day, 30);

    /// <summary>Jours restants dans l'année à compter de la date donnée (jour inclus), convention 30/360.</summary>
    internal static decimal Days360RemainingInYear(DateTime date) =>
        DaysPerYear - Days360FromYearStart(date) + 1;

    /// <summary>Jours entre deux dates de la même année (bornes incluses), convention 30/360.</summary>
    internal static decimal Days360Between(DateTime from, DateTime to)
    {
        if (to < from)
            return 0m;
        return Days360FromYearStart(to) - Days360FromYearStart(from) + 1;
    }

    internal static decimal Round3(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
