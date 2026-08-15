using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Un des trois scénarios d'une projection (optimiste, réaliste, pessimiste).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DeterministicProbabilityPercent"/> est conservée en permanence à côté de
/// <see cref="ProbabilityPercent"/>. Le modèle de langage peut décaler la seconde, jamais la
/// première : un expert-comptable doit pouvoir constater ce que le moteur seul aurait produit, et
/// l'écran affiche la valeur déterministe en infobulle.
/// </para>
/// <para>
/// Les montants ne sont jamais touchés par le modèle : ils viennent des collecteurs.
/// </para>
/// </remarks>
public sealed class CashFlowScenario : Entity
{
    public Guid ForecastRunId { get; private set; }

    public CashFlowScenarioKind Kind { get; private set; }

    /// <summary>Solde projeté en fin d'horizon pour ce scénario. Peut être négatif.</summary>
    public decimal ClosingBalance { get; private set; }

    /// <summary>Flux net cumulé sur l'horizon. Peut être négatif.</summary>
    public decimal NetFlow { get; private set; }

    /// <summary>Probabilité produite par le moteur statistique seul, dans [0..100].</summary>
    public decimal DeterministicProbabilityPercent { get; private set; }

    /// <summary>Probabilité affichée, éventuellement ajustée par le modèle, dans [0..100].</summary>
    public decimal ProbabilityPercent { get; private set; }

    public CashFlowProbabilitySource ProbabilitySource { get; private set; }

    /// <summary>Justification rédigée par le modèle quand il a décalé la probabilité.</summary>
    public string? AiRationale { get; private set; }

    /// <summary>Hypothèses retenues par le moteur (décalages appliqués, taux de défaut…).</summary>
    public string? AssumptionsJson { get; private set; }

    private CashFlowScenario() { }

    public static CashFlowScenario Create(
        CashFlowScenarioKind kind,
        decimal closingBalance,
        decimal netFlow,
        decimal deterministicProbabilityPercent,
        string? assumptionsJson = null)
    {
        if (deterministicProbabilityPercent < 0m || deterministicProbabilityPercent > 100m)
            throw new ArgumentException(
                "La probabilité déterministe doit être dans [0..100].",
                nameof(deterministicProbabilityPercent));

        return new CashFlowScenario
        {
            Kind = kind,
            ClosingBalance = MillimeRounding.Round(closingBalance),
            NetFlow = MillimeRounding.Round(netFlow),
            DeterministicProbabilityPercent = deterministicProbabilityPercent,
            ProbabilityPercent = deterministicProbabilityPercent,
            ProbabilitySource = CashFlowProbabilitySource.Deterministic,
            AssumptionsJson = assumptionsJson
        };
    }

    /// <summary>
    /// Applique la probabilité proposée par le modèle, bornée à <paramref name="maxShiftPoints"/>
    /// points autour de la valeur déterministe puis ramenée dans [0..100].
    /// </summary>
    /// <remarks>
    /// Le bornage vit ici, dans le domaine, et non dans le service qui appelle le modèle : c'est
    /// l'invariant qui garantit qu'aucun chemin de code ne pourra persister une probabilité
    /// arbitraire, quelle que soit la réponse du LLM. La renormalisation de la somme à 100 reste,
    /// elle, la responsabilité de l'appelant, qui seul voit les trois scénarios.
    /// </remarks>
    public void ApplyAiProbability(decimal proposedPercent, int maxShiftPoints, string? rationale)
    {
        if (maxShiftPoints < 0)
            throw new ArgumentException("maxShiftPoints doit être ≥ 0.", nameof(maxShiftPoints));

        var lowerBound = Math.Max(0m, DeterministicProbabilityPercent - maxShiftPoints);
        var upperBound = Math.Min(100m, DeterministicProbabilityPercent + maxShiftPoints);
        var clamped = Math.Clamp(proposedPercent, lowerBound, upperBound);

        ProbabilityPercent = decimal.Round(clamped, 2, MidpointRounding.AwayFromZero);
        ProbabilitySource = CashFlowProbabilitySource.AiAdjusted;
        AiRationale = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim();
    }

    /// <summary>
    /// Réajuste la probabilité affichée pour que la somme des trois scénarios fasse exactement 100,
    /// sans changer la provenance ni la valeur déterministe.
    /// </summary>
    public void NormalizeProbability(decimal normalizedPercent)
    {
        ProbabilityPercent = decimal.Round(
            Math.Clamp(normalizedPercent, 0m, 100m),
            2,
            MidpointRounding.AwayFromZero);
    }
}
