using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Treasury;

/// <summary>
/// Applique l'analyse du modèle à une projection, sous garde-fous.
/// </summary>
/// <remarks>
/// <para>
/// C'est ici que se joue la crédibilité du module vis-à-vis d'un expert-comptable. Trois règles :
/// </para>
/// <list type="number">
/// <item>aucune probabilité ne s'écarte de plus de <c>MaxProbabilityShiftPoints</c> de sa valeur
/// déterministe — le bornage lui-même vit dans l'entité <see cref="CashFlowScenario"/> ;</item>
/// <item>la somme des trois probabilités vaut exactement 100 après renormalisation ;</item>
/// <item>rien n'est appliqué si le modèle n'a pas fourni les trois scénarios — un ajustement
/// partiel produirait une répartition incohérente.</item>
/// </list>
/// <para>
/// Les montants, les dates et les soldes ne sont jamais touchés : ils restent ceux du moteur.
/// </para>
/// </remarks>
public static class CashFlowAiAdjustmentApplier
{
    /// <summary>
    /// Applique l'analyse et indique si les probabilités ont effectivement été ajustées.
    /// </summary>
    public static bool Apply(
        CashFlowForecastRun run,
        CashFlowAiAnalysis analysis,
        AiCashForecastOptions options)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(options);

        AppendInsights(run, analysis, options);

        return ApplyProbabilities(run, analysis, options);
    }

    private static bool ApplyProbabilities(
        CashFlowForecastRun run,
        CashFlowAiAnalysis analysis,
        AiCashForecastOptions options)
    {
        var scenarios = run.Scenarios.ToList();
        if (scenarios.Count == 0) return false;

        // Tout ou rien : si le modèle a omis un scénario, la répartition qu'il propose est
        // incomplète et ne peut pas être renormalisée honnêtement.
        var complete = scenarios.All(s => analysis.ScenarioProbabilities.ContainsKey(s.Kind));
        if (!complete) return false;

        var maxShift = Math.Max(0, options.MaxProbabilityShiftPoints);

        foreach (var scenario in scenarios)
        {
            analysis.ScenarioRationales.TryGetValue(scenario.Kind, out var rationale);
            scenario.ApplyAiProbability(
                analysis.ScenarioProbabilities[scenario.Kind],
                maxShift,
                rationale);
        }

        Normalize(scenarios, maxShift);

        // Un budget de décalage nul, ou un modèle qui reproduit exactement le déterministe, ne
        // constitue pas un ajustement : l'écran ne doit pas afficher de badge « pondéré par l'IA ».
        return scenarios.Any(s => s.ProbabilityPercent != s.DeterministicProbabilityPercent);
    }

    /// <summary>
    /// Ramène la somme des probabilités à exactement 100 <b>sans jamais sortir de la bande</b>
    /// autorisée autour de la valeur déterministe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Une renormalisation proportionnelle naïve viole le bornage : partant de 45/35/5 (déjà
    /// bornés), une mise à l'échelle par 100/85 porterait l'optimiste à 52,9 — soit 23 points
    /// au-dessus du déterministe, pour un budget de 15. L'écart est donc réparti au prorata de la
    /// <i>marge restante</i> de chaque scénario, ce qui garantit qu'aucun ne franchit sa borne.
    /// </para>
    /// <para>
    /// Effet de bord voulu : une proposition entièrement à zéro se ramène exactement à la
    /// répartition déterministe, chaque scénario étant remonté depuis sa borne basse au prorata
    /// d'une marge identique.
    /// </para>
    /// </remarks>
    internal static void Normalize(IReadOnlyList<CashFlowScenario> scenarios, int maxShiftPoints)
    {
        var count = scenarios.Count;
        if (count == 0) return;

        var lower = new decimal[count];
        var upper = new decimal[count];
        var current = new decimal[count];

        for (var i = 0; i < count; i++)
        {
            var deterministic = scenarios[i].DeterministicProbabilityPercent;
            lower[i] = Math.Max(0m, deterministic - maxShiftPoints);
            upper[i] = Math.Min(100m, deterministic + maxShiftPoints);
            current[i] = Math.Clamp(scenarios[i].ProbabilityPercent, lower[i], upper[i]);
        }

        // Quelques passes suffisent : chacune consomme la marge disponible, les scénarios saturés
        // sortant naturellement du calcul à la suivante.
        for (var pass = 0; pass < 4; pass++)
        {
            var drift = 100m - current.Sum();
            if (Math.Abs(drift) < 0.0001m) break;

            var headroom = new decimal[count];
            for (var i = 0; i < count; i++)
                headroom[i] = drift > 0m ? upper[i] - current[i] : current[i] - lower[i];

            var totalHeadroom = headroom.Sum();
            if (totalHeadroom <= 0m) break;

            for (var i = 0; i < count; i++)
                current[i] += drift * headroom[i] / totalHeadroom;
        }

        for (var i = 0; i < count; i++)
            current[i] = decimal.Round(current[i], 2, MidpointRounding.AwayFromZero);

        // Reliquat d'arrondi : imputé à un scénario qui peut l'absorber sans quitter sa bande.
        var residual = 100m - current.Sum();
        if (residual != 0m)
        {
            for (var i = 0; i < count; i++)
            {
                var candidate = current[i] + residual;
                if (candidate < lower[i] || candidate > upper[i]) continue;

                current[i] = candidate;
                break;
            }
        }

        for (var i = 0; i < count; i++)
            scenarios[i].NormalizeProbability(current[i]);
    }

    private static void AppendInsights(
        CashFlowForecastRun run,
        CashFlowAiAnalysis analysis,
        AiCashForecastOptions options)
    {
        var budget = Math.Max(0, options.MaxInsights);
        if (budget == 0) return;

        // Les analyses de règle gardent la priorité d'affichage : elles signalent des faits, là où
        // le modèle apporte une lecture. Les rangs de l'IA démarrent donc après les leurs.
        var sortOrder = run.Insights.Count == 0 ? 0 : run.Insights.Max(i => i.SortOrder) + 1;
        var added = 0;

        foreach (var alert in analysis.Alerts)
        {
            if (added++ >= budget) return;
            run.AddInsight(CashFlowForecastInsight.Alert(
                alert.Severity,
                alert.Title,
                alert.Detail,
                alert.PeriodStart,
                null,
                CashFlowInsightOrigin.Ai,
                sortOrder++));
        }

        foreach (var driver in analysis.Drivers)
        {
            if (added++ >= budget) return;
            run.AddInsight(CashFlowForecastInsight.Driver(
                driver.Title,
                driver.Detail,
                driver.Impact ?? CashFlowImpactLevel.Medium,
                driver.ImpactDirection ?? CashFlowDirection.Outflow,
                CashFlowInsightOrigin.Ai,
                sortOrder++));
        }

        foreach (var recommendation in analysis.Recommendations)
        {
            if (added++ >= budget) return;
            run.AddInsight(CashFlowForecastInsight.Recommendation(
                recommendation.Title,
                recommendation.Detail,
                CashFlowInsightOrigin.Ai,
                sortOrder++));
        }
    }
}
