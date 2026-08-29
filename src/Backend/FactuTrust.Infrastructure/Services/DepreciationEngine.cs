using FactuTrust.Application.Common.Fiscal;
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
/// <remarks>
/// <b>Exercices décalés (plan « Exercices décalés », P2).</b> Les lignes d'échéancier sont générées
/// par <b>exercice</b> (clé = année de début d'exercice, <see cref="DepreciationScheduleLine.FiscalYear"/>),
/// non par année civile. Le prorata 30/360 est calculé <b>relativement à la frontière d'exercice</b>
/// (depuis début d'exercice / restant jusqu'à fin d'exercice / entre deux dates d'un même exercice).
/// <para>
/// <b>Choix de signature.</b> Le mois de début d'exercice (<paramref name="fiscalYearStartMonth"/>)
/// est passé en paramètre des méthodes publiques (défaut 1) plutôt qu'injecté : le moteur est
/// statique par nature, n'a aucun état ni dépendance config/DB, et les tests existants l'appellent
/// sans ce paramètre. Les appelants (preview, generate-schedule, mise en service, cession, run)
/// récupèrent le mois configuré via <c>FixedAssetSettings</c> et le propagent.
/// </para>
/// <para>
/// <b>Anti-régression absolue.</b> Avec <paramref name="fiscalYearStartMonth"/> = 1 (exercice
/// civil), tous les résultats sont bit-à-bit identiques à l'existant : la clé d'exercice dégénère
/// en année civile (<see cref="FiscalYearMath.Key"/> retourne <c>date.Year</c>), et les helpers
/// 30/360 fiscaux se réduisent exactement aux helpers année civile d'origine (délégation par
/// défaut 1 — chemin unique). Convention 30/360, <see cref="Round3"/> (AwayFromZero) et
/// <see cref="AdjustFinalLineRounding"/> intacts.
/// </para>
/// </remarks>
public sealed class DepreciationEngine : IDepreciationEngine
{
    private const decimal DaysPerYear = 360m;

    public IReadOnlyList<DepreciationScheduleLine> GenerateSchedule(
        FixedAsset asset,
        int? throughFiscalYear = null,
        int fiscalYearStartMonth = 1)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.DepreciationRatePercent <= 0 || asset.InServiceDate is null)
            return Array.Empty<DepreciationScheduleLine>();

        var baseAmount = asset.DepreciableBase;
        if (baseAmount <= 0)
            return Array.Empty<DepreciationScheduleLine>();

        return asset.DepreciationMethod switch
        {
            DepreciationMethod.Integral => GenerateIntegralSchedule(asset, baseAmount, fiscalYearStartMonth),
            DepreciationMethod.Accelerated => GenerateAcceleratedSchedule(asset, baseAmount, throughFiscalYear, fiscalYearStartMonth),
            _ => GenerateLinearSchedule(asset, baseAmount, throughFiscalYear, asset.DepreciationRatePercent, fiscalYearStartMonth)
        };
    }

    public decimal CalculateDisposalYearDepreciation(
        FixedAsset asset,
        int fiscalYear,
        decimal priorAccumulatedDepreciation,
        int fiscalYearStartMonth = 1)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.InServiceDate is null || asset.DisposalDate is null)
            return 0m;
        if (asset.DepreciationRatePercent <= 0)
            return 0m;

        var baseAmount = asset.DepreciableBase;
        if (baseAmount <= 0 || priorAccumulatedDepreciation >= baseAmount)
            return 0m;

        var lines = GenerateSchedule(asset, fiscalYearStartMonth: fiscalYearStartMonth);
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
        decimal effectiveRatePercent,
        int fiscalYearStartMonth)
    {
        var inService = asset.InServiceDate!.Value.Date;
        var normalAnnual = Round3(baseAmount * effectiveRatePercent / 100m);
        if (normalAnnual <= 0)
            return Array.Empty<DepreciationScheduleLine>();

        var effectiveLifeYears = effectiveRatePercent > 0 ? 100m / effectiveRatePercent : asset.UsefulLifeYears;
        var startKey = FiscalYearMath.Key(inService, fiscalYearStartMonth);
        var endKey = throughFiscalYear ?? EstimateEndYear(startKey, effectiveLifeYears, asset.DisposalDate, fiscalYearStartMonth);
        if (asset.DisposalDate is not null)
            endKey = Math.Min(endKey, FiscalYearMath.Key(asset.DisposalDate.Value, fiscalYearStartMonth));

        var lines = new List<DepreciationScheduleLine>();
        decimal priorAccumulated = 0m;
        decimal openingNbv = asset.TotalCapitalizedCost;

        for (var year = startKey; year <= endKey; year++)
        {
            var amount = CalculateLinearYearAmount(asset, year, normalAnnual, baseAmount, priorAccumulated, fiscalYearStartMonth);
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
        decimal priorAccumulated,
        int fiscalYearStartMonth)
    {
        if (priorAccumulated >= baseAmount)
            return 0m;

        var inService = asset.InServiceDate!.Value.Date;
        var startKey = FiscalYearMath.Key(inService, fiscalYearStartMonth);

        if (fiscalYear < startKey)
            return 0m;

        decimal amount;
        if (asset.DisposalDate is not null && fiscalYear == FiscalYearMath.Key(asset.DisposalDate.Value, fiscalYearStartMonth))
        {
            // Année de sortie : prorata en jours/360 jusqu'à la date de cession ; si la cession a
            // lieu l'exercice de mise en service, le prorata court de la mise en service à la cession.
            var disposalDate = asset.DisposalDate.Value.Date;
            var days = fiscalYear == startKey
                ? Days360BetweenFiscal(inService, disposalDate, fiscalYearStartMonth)
                : Days360FromFiscalYearStart(disposalDate, fiscalYearStartMonth);
            amount = Round3(normalAnnual * days / DaysPerYear);
        }
        else if (fiscalYear == startKey)
        {
            // Première année : prorata en jours/360 de la mise en service à la fin d'exercice (jour inclus).
            var days = Days360RemainingInFiscalYear(inService, fiscalYearStartMonth);
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
        int? throughFiscalYear,
        int fiscalYearStartMonth)
    {
        var coefficient = asset.AccelerationCoefficient <= 1m ? 1m : asset.AccelerationCoefficient;
        var effectiveRate = asset.DepreciationRatePercent * coefficient;
        return GenerateLinearSchedule(asset, baseAmount, throughFiscalYear, effectiveRate, fiscalYearStartMonth);
    }

    // ---------------------------------------------------------------
    // Intégral (dotation unique, sans prorata)
    // ---------------------------------------------------------------

    private static IReadOnlyList<DepreciationScheduleLine> GenerateIntegralSchedule(
        FixedAsset asset,
        decimal baseAmount,
        int fiscalYearStartMonth)
    {
        var fiscalYear = FiscalYearMath.Key(asset.InServiceDate!.Value.Date, fiscalYearStartMonth);
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

    private static int EstimateEndYear(int startKey, decimal usefulLifeYears, DateTime? disposalDate, int fiscalYearStartMonth)
    {
        if (disposalDate is not null)
            return FiscalYearMath.Key(disposalDate.Value, fiscalYearStartMonth);

        var extra = usefulLifeYears <= 0 ? 20 : (int)Math.Ceiling(usefulLifeYears) + 1;
        return startKey + extra;
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

    /// <summary>Jours écoulés depuis le début de l'exercice (inclus) jusqu'à la date donnée, convention 30/360.</summary>
    internal static decimal Days360FromFiscalYearStart(DateTime date, int fiscalYearStartMonth)
    {
        // Position (0-indexée) du mois dans l'exercice : mois ≥ startMonth → (month - startMonth) ;
        // sinon (mois de l'année civile suivante) → (month + 12 - startMonth). Avec startMonth = 1,
        // dégénère en (month - 1) = Days360FromYearStart.
        var monthIndex = date.Month >= fiscalYearStartMonth
            ? date.Month - fiscalYearStartMonth
            : date.Month + 12 - fiscalYearStartMonth;
        return monthIndex * 30 + Math.Min(date.Day, 30);
    }

    /// <summary>Jours restants dans l'exercice à compter de la date donnée (jour inclus), convention 30/360.</summary>
    internal static decimal Days360RemainingInFiscalYear(DateTime date, int fiscalYearStartMonth) =>
        DaysPerYear - Days360FromFiscalYearStart(date, fiscalYearStartMonth) + 1;

    /// <summary>Jours entre deux dates d'un même exercice (bornes incluses), convention 30/360.</summary>
    internal static decimal Days360BetweenFiscal(DateTime from, DateTime to, int fiscalYearStartMonth)
    {
        if (to < from)
            return 0m;
        return Days360FromFiscalYearStart(to, fiscalYearStartMonth) - Days360FromFiscalYearStart(from, fiscalYearStartMonth) + 1;
    }

    // --- Helpers année civile d'origine (conservés : testés directement par DepreciationEngineTests).
    //     Délèguent aux variantes fiscales avec startMonth = 1 (chemin unique ⇒ bit-à-bit identique). ---

    /// <summary>Jours écoulés depuis le 1er janvier (inclus) jusqu'à la date donnée, convention 30/360.</summary>
    internal static decimal Days360FromYearStart(DateTime date) => Days360FromFiscalYearStart(date, 1);

    /// <summary>Jours restants dans l'année à compter de la date donnée (jour inclus), convention 30/360.</summary>
    internal static decimal Days360RemainingInYear(DateTime date) => Days360RemainingInFiscalYear(date, 1);

    /// <summary>Jours entre deux dates de la même année (bornes incluses), convention 30/360.</summary>
    internal static decimal Days360Between(DateTime from, DateTime to) => Days360BetweenFiscal(from, to, 1);

    internal static decimal Round3(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
