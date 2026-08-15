using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Treasury;

/// <summary>Agrégat mensuel calculé, avant construction de l'entité persistée.</summary>
public sealed record CashFlowMonthlyAggregate(
    int SequenceIndex,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal Inflows,
    decimal Outflows,
    decimal Variance);

/// <summary>Résultat déterministe complet, prêt à être persisté.</summary>
public sealed record CashFlowDeterministicResult(
    decimal TotalInflows,
    decimal TotalOutflows,
    decimal ConfidencePercent,
    IReadOnlyList<CashFlowMonthlyAggregate> Monthly,
    IReadOnlyList<CashFlowScenarioResult> Scenarios);

/// <summary>Un scénario chiffré, avec sa probabilité déterministe et ses hypothèses.</summary>
public sealed record CashFlowScenarioResult(
    CashFlowScenarioKind Kind,
    decimal NetFlow,
    decimal ClosingBalance,
    decimal ProbabilityPercent,
    string AssumptionsJson);

/// <summary>
/// Cœur déterministe : agrège les flux collectés en mois, mesure l'incertitude et décline les
/// trois scénarios.
/// </summary>
/// <remarks>
/// <para>
/// Entièrement pur — aucune base, aucun appel réseau. C'est ce qui rend la projection reproductible
/// et testable : les mêmes flux en entrée produisent toujours les mêmes chiffres.
/// </para>
/// <para>
/// L'incertitude d'un mois est la variance de Bernoulli des flux non certains
/// (<c>Σ montant² × p × (1−p)</c>). Elle se cumule d'un mois sur l'autre parce que le solde de
/// clôture accumule les aléas des mois précédents — d'où un intervalle qui s'élargit avec
/// l'horizon, exactement comme on l'attend d'une prévision.
/// </para>
/// </remarks>
public static class CashFlowScenarioEngine
{
    /// <summary>
    /// Abattement supplémentaire appliqué aux encaissements incertains dans le scénario
    /// pessimiste : au-delà du retard, une partie des créances douteuses ne rentre pas du tout.
    /// </summary>
    private const decimal PessimisticInflowHaircut = 0.75m;

    public static CashFlowDeterministicResult Compute(
        IReadOnlyList<CashFlowLineDraft> lines,
        decimal openingBalance,
        DateTime from,
        DateTime to,
        double confidenceZ)
    {
        var months = BuildMonthSlots(from, to);
        var monthly = new List<CashFlowMonthlyAggregate>(months.Count);

        foreach (var (index, start, end) in months)
        {
            var inMonth = lines.Where(l => l.ExpectedDate >= start && l.ExpectedDate <= end).ToList();

            var inflows = SumWeighted(inMonth, CashFlowDirection.Inflow);
            var outflows = SumWeighted(inMonth, CashFlowDirection.Outflow);
            var variance = inMonth.Sum(Variance);

            monthly.Add(new CashFlowMonthlyAggregate(index, start, end, inflows, outflows, variance));
        }

        var totalInflows = MillimeRounding.Round(monthly.Sum(m => m.Inflows));
        var totalOutflows = MillimeRounding.Round(monthly.Sum(m => m.Outflows));

        var scenarios = BuildScenarios(lines, openingBalance);
        var confidence = ComputeConfidencePercent(lines);

        return new CashFlowDeterministicResult(
            totalInflows,
            totalOutflows,
            confidence,
            monthly,
            scenarios);
    }

    /// <summary>
    /// Demi-largeur de l'intervalle de prévision au mois <paramref name="index"/>, à partir de la
    /// variance cumulée depuis le début de l'horizon.
    /// </summary>
    public static decimal IntervalHalfWidth(
        IReadOnlyList<CashFlowMonthlyAggregate> monthly,
        int index,
        double confidenceZ)
    {
        var cumulativeVariance = monthly
            .Where(m => m.SequenceIndex <= index)
            .Sum(m => m.Variance);

        if (cumulativeVariance <= 0m) return 0m;

        var sigma = Math.Sqrt((double)cumulativeVariance);
        return MillimeRounding.Round((decimal)(sigma * confidenceZ));
    }

    private static decimal SumWeighted(IEnumerable<CashFlowLineDraft> lines, CashFlowDirection direction) =>
        MillimeRounding.Round(lines
            .Where(l => l.Direction == direction)
            .Sum(l => l.Amount * l.ProbabilityPercent / 100m));

    /// <summary>
    /// Variance de Bernoulli d'un flux. Un flux certain (probabilité 100 %) n'apporte aucune
    /// incertitude : c'est ce qui fait qu'une société dont tous les flux sont contractuels affiche
    /// un intervalle nul plutôt qu'une fourchette artificielle.
    /// </summary>
    private static decimal Variance(CashFlowLineDraft line)
    {
        var p = line.ProbabilityPercent / 100m;
        if (p <= 0m || p >= 1m) return 0m;
        return line.Amount * line.Amount * p * (1m - p);
    }

    private static IReadOnlyList<(int Index, DateTime Start, DateTime End)> BuildMonthSlots(
        DateTime from,
        DateTime to)
    {
        var slots = new List<(int, DateTime, DateTime)>();
        var cursor = new DateTime(from.Year, from.Month, 1);
        var index = 0;

        while (cursor <= to)
        {
            var start = index == 0 ? from.Date : cursor;
            var monthEnd = cursor.AddMonths(1).AddDays(-1);
            var end = monthEnd > to.Date ? to.Date : monthEnd;

            slots.Add((index, start, end));
            cursor = cursor.AddMonths(1);
            index++;
        }

        return slots;
    }

    private static IReadOnlyList<CashFlowScenarioResult> BuildScenarios(
        IReadOnlyList<CashFlowLineDraft> lines,
        decimal openingBalance)
    {
        var inflows = lines.Where(l => l.Direction == CashFlowDirection.Inflow).ToList();
        var outflows = lines.Where(l => l.Direction == CashFlowDirection.Outflow).ToList();

        // Les décaissements sont identiques dans les trois scénarios : ce sont des engagements de
        // la société, pas des aléas. Seule la rentrée d'argent est incertaine.
        var totalOutflows = MillimeRounding.Round(outflows.Sum(l => l.Amount * l.ProbabilityPercent / 100m));

        var optimisticIn = MillimeRounding.Round(inflows.Sum(l => l.Amount));
        var realisticIn = MillimeRounding.Round(inflows.Sum(l => l.Amount * l.ProbabilityPercent / 100m));
        var pessimisticIn = MillimeRounding.Round(inflows.Sum(l =>
            l.IsConfirmed
                ? l.Amount * l.ProbabilityPercent / 100m
                : l.Amount * l.ProbabilityPercent / 100m * PessimisticInflowHaircut));

        var probabilities = ResolveScenarioProbabilities(inflows);

        return new[]
        {
            Build(CashFlowScenarioKind.Optimistic, optimisticIn, totalOutflows, openingBalance,
                probabilities.Optimistic,
                "{\"inflows\":\"contractuels, sans defaut\",\"outflows\":\"a l'echeance\"}"),
            Build(CashFlowScenarioKind.Realistic, realisticIn, totalOutflows, openingBalance,
                probabilities.Realistic,
                "{\"inflows\":\"ponderes par la probabilite observee\",\"outflows\":\"a l'echeance\"}"),
            Build(CashFlowScenarioKind.Pessimistic, pessimisticIn, totalOutflows, openingBalance,
                probabilities.Pessimistic,
                $"{{\"inflows\":\"ponderes puis abattus de {(1m - PessimisticInflowHaircut) * 100m:0}%\",\"outflows\":\"a l'echeance\"}}")
        };
    }

    private static CashFlowScenarioResult Build(
        CashFlowScenarioKind kind,
        decimal inflows,
        decimal outflows,
        decimal openingBalance,
        decimal probability,
        string assumptionsJson)
    {
        var net = MillimeRounding.Round(inflows - outflows);
        return new CashFlowScenarioResult(
            kind,
            net,
            MillimeRounding.Round(openingBalance + net),
            probability,
            assumptionsJson);
    }

    /// <summary>
    /// Probabilités déterministes des trois scénarios.
    /// </summary>
    /// <remarks>
    /// Le réaliste garde toujours 50 %. Les 50 restants se répartissent selon la part des
    /// encaissements contractuellement certains : plus le carnet est ferme, plus l'optimiste est
    /// crédible. Règle monotone et explicable à un comptable — ce qui compte davantage ici qu'une
    /// sophistication invérifiable.
    /// </remarks>
    internal static (decimal Optimistic, decimal Realistic, decimal Pessimistic) ResolveScenarioProbabilities(
        IReadOnlyList<CashFlowLineDraft> inflows)
    {
        var totalInflow = inflows.Sum(l => l.Amount);
        var confirmedShare = totalInflow <= 0m
            ? 0m
            : inflows.Where(l => l.IsConfirmed).Sum(l => l.Amount) / totalInflow;

        var optimistic = decimal.Round(20m + (20m * Math.Clamp(confirmedShare, 0m, 1m)), 0, MidpointRounding.AwayFromZero);
        const decimal realistic = 50m;
        var pessimistic = 100m - realistic - optimistic;

        return (optimistic, realistic, pessimistic);
    }

    /// <summary>
    /// Indice de confiance : part du volume de flux qui est contractuellement certaine.
    /// </summary>
    /// <remarks>
    /// Volontairement littéral. Une projection dont 90 % du volume est constitué d'échéances
    /// fermes mérite un indice élevé ; une projection reposant sur des créances douteuses ne le
    /// mérite pas, quelle que soit la finesse du calcul.
    /// </remarks>
    internal static decimal ComputeConfidencePercent(IReadOnlyList<CashFlowLineDraft> lines)
    {
        var total = lines.Sum(l => l.Amount);
        if (total <= 0m) return 0m;

        var weighted = lines.Sum(l => l.Amount * l.ProbabilityPercent / 100m);
        return decimal.Round(Math.Clamp(weighted / total * 100m, 0m, 100m), 2, MidpointRounding.AwayFromZero);
    }
}
