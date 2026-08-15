using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Treasury;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Garde-fous de la couche IA. C'est la barrière entre « le modèle éclaire une décision » et
/// « le modèle invente une trésorerie » : chaque règle du plan y est verrouillée par un test.
/// </summary>
public sealed class CashFlowAiAdjustmentApplierTests
{
    private static readonly AiCashForecastOptions DefaultOptions = new()
    {
        Enabled = true,
        MaxProbabilityShiftPoints = 15,
        MaxInsights = 8
    };

    private static CashFlowForecastRun BuildRun(
        decimal optimistic = 30m,
        decimal realistic = 50m,
        decimal pessimistic = 20m)
    {
        var run = CashFlowForecastRun.Start(
            new DateTime(2026, 6, 1), new DateTime(2026, 11, 30), 6, 100_000m, "TND", null);

        run.AddScenario(CashFlowScenario.Create(CashFlowScenarioKind.Optimistic, 160_000m, 60_000m, optimistic));
        run.AddScenario(CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 120_000m, 20_000m, realistic));
        run.AddScenario(CashFlowScenario.Create(CashFlowScenarioKind.Pessimistic, 80_000m, -20_000m, pessimistic));

        return run;
    }

    private static CashFlowAiAnalysis Analysis(
        decimal optimistic,
        decimal realistic,
        decimal pessimistic,
        IReadOnlyList<CashFlowAiInsight>? alerts = null) =>
        new()
        {
            ScenarioProbabilities = new Dictionary<CashFlowScenarioKind, decimal>
            {
                [CashFlowScenarioKind.Optimistic] = optimistic,
                [CashFlowScenarioKind.Realistic] = realistic,
                [CashFlowScenarioKind.Pessimistic] = pessimistic
            },
            ScenarioRationales = new Dictionary<CashFlowScenarioKind, string?>
            {
                [CashFlowScenarioKind.Optimistic] = "Carnet ferme",
                [CashFlowScenarioKind.Realistic] = "Tendance stable",
                [CashFlowScenarioKind.Pessimistic] = "Risque fournisseur"
            },
            Alerts = alerts ?? Array.Empty<CashFlowAiInsight>()
        };

    // ──────────────── Bornage ────────────────

    [Fact]
    public void Apply_ClampsEachProbabilityWithinTheConfiguredBand()
    {
        var run = BuildRun();

        // Le modèle tente de porter l'optimiste à 90 %, soit 60 points au-dessus du déterministe.
        CashFlowAiAdjustmentApplier.Apply(run, Analysis(90m, 5m, 5m), DefaultOptions);

        foreach (var scenario in run.Scenarios)
        {
            var shift = Math.Abs(scenario.ProbabilityPercent - scenario.DeterministicProbabilityPercent);
            Assert.True(
                shift <= DefaultOptions.MaxProbabilityShiftPoints + 0.01m,
                $"{scenario.Kind} a dérivé de {shift} points.");
        }
    }

    [Fact]
    public void Apply_AlwaysSumsToExactlyOneHundred()
    {
        foreach (var proposal in new[]
                 {
                     (90m, 5m, 5m),
                     (0m, 0m, 100m),
                     (33m, 33m, 33m),
                     (45m, 45m, 45m)
                 })
        {
            var run = BuildRun();
            CashFlowAiAdjustmentApplier.Apply(run, Analysis(proposal.Item1, proposal.Item2, proposal.Item3), DefaultOptions);

            Assert.Equal(100m, run.Scenarios.Sum(s => s.ProbabilityPercent));
        }
    }

    [Fact]
    public void Apply_PreservesTheDeterministicProbability()
    {
        var run = BuildRun();

        CashFlowAiAdjustmentApplier.Apply(run, Analysis(40m, 45m, 15m), DefaultOptions);

        Assert.Equal(30m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Optimistic).DeterministicProbabilityPercent);
        Assert.Equal(50m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Realistic).DeterministicProbabilityPercent);
        Assert.Equal(20m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Pessimistic).DeterministicProbabilityPercent);
    }

    [Fact]
    public void Apply_NeverTouchesAmounts()
    {
        var run = BuildRun();
        var before = run.Scenarios.ToDictionary(s => s.Kind, s => (s.ClosingBalance, s.NetFlow));

        CashFlowAiAdjustmentApplier.Apply(run, Analysis(40m, 45m, 15m), DefaultOptions);

        foreach (var scenario in run.Scenarios)
        {
            Assert.Equal(before[scenario.Kind].ClosingBalance, scenario.ClosingBalance);
            Assert.Equal(before[scenario.Kind].NetFlow, scenario.NetFlow);
        }
    }

    [Fact]
    public void Apply_ZeroShiftBudget_LeavesProbabilitiesUntouchedAndReportsNoAdjustment()
    {
        var run = BuildRun();
        var options = new AiCashForecastOptions { Enabled = true, MaxProbabilityShiftPoints = 0, MaxInsights = 8 };

        var adjusted = CashFlowAiAdjustmentApplier.Apply(run, Analysis(90m, 5m, 5m), options);

        Assert.False(adjusted);
        Assert.Equal(30m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Optimistic).ProbabilityPercent);
        Assert.Equal(100m, run.Scenarios.Sum(s => s.ProbabilityPercent));
    }

    [Fact]
    public void Apply_IncompleteProposal_IsRejectedEntirely()
    {
        var run = BuildRun();
        var partial = new CashFlowAiAnalysis
        {
            ScenarioProbabilities = new Dictionary<CashFlowScenarioKind, decimal>
            {
                [CashFlowScenarioKind.Optimistic] = 45m
            }
        };

        var adjusted = CashFlowAiAdjustmentApplier.Apply(run, partial, DefaultOptions);

        Assert.False(adjusted);
        Assert.All(run.Scenarios, s => Assert.Equal(CashFlowProbabilitySource.Deterministic, s.ProbabilitySource));
        Assert.Equal(100m, run.Scenarios.Sum(s => s.ProbabilityPercent));
    }

    [Fact]
    public void Apply_AllZeroProposal_FallsBackToDeterministic()
    {
        var run = BuildRun();

        CashFlowAiAdjustmentApplier.Apply(run, Analysis(0m, 0m, 0m), DefaultOptions);

        Assert.Equal(30m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Optimistic).ProbabilityPercent);
        Assert.Equal(50m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Realistic).ProbabilityPercent);
        Assert.Equal(20m, run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Pessimistic).ProbabilityPercent);
    }

    [Fact]
    public void Apply_MarksProvenanceAndKeepsRationale()
    {
        var run = BuildRun();

        CashFlowAiAdjustmentApplier.Apply(run, Analysis(40m, 45m, 15m), DefaultOptions);

        var optimistic = run.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Optimistic);
        Assert.Equal(CashFlowProbabilitySource.AiAdjusted, optimistic.ProbabilitySource);
        Assert.Equal("Carnet ferme", optimistic.AiRationale);
    }

    // ──────────────── Analyses ────────────────

    [Fact]
    public void Apply_AddsAiInsightsTaggedWithTheirOrigin()
    {
        var run = BuildRun();
        var analysis = Analysis(40m, 45m, 15m, new[]
        {
            new CashFlowAiInsight
            {
                Title = "Tension en août",
                Detail = "Concentration d'échéances fiscales.",
                Severity = CashFlowInsightSeverity.Warning,
                PeriodStart = new DateTime(2026, 8, 1)
            }
        });

        CashFlowAiAdjustmentApplier.Apply(run, analysis, DefaultOptions);

        var insight = Assert.Single(run.Insights);
        Assert.Equal(CashFlowInsightOrigin.Ai, insight.Origin);
        Assert.Equal(CashFlowInsightSeverity.Warning, insight.Severity);
        Assert.Equal("Tension en août", insight.Title);
    }

    [Fact]
    public void Apply_CapsTheNumberOfInsights()
    {
        var run = BuildRun();
        var many = Enumerable.Range(0, 20)
            .Select(i => new CashFlowAiInsight { Title = $"Alerte {i}" })
            .ToList();

        CashFlowAiAdjustmentApplier.Apply(
            run,
            Analysis(40m, 45m, 15m, many),
            new AiCashForecastOptions { Enabled = true, MaxProbabilityShiftPoints = 15, MaxInsights = 3 });

        Assert.Equal(3, run.Insights.Count);
    }

    [Fact]
    public void Apply_RuleInsightsKeepDisplayPriorityOverAiOnes()
    {
        var run = BuildRun();
        run.AddInsight(CashFlowForecastInsight.Alert(
            CashFlowInsightSeverity.Critical, "Rupture prévue", null, null, -5_000m,
            CashFlowInsightOrigin.Rule, 0));

        CashFlowAiAdjustmentApplier.Apply(
            run,
            Analysis(40m, 45m, 15m, new[] { new CashFlowAiInsight { Title = "Lecture IA" } }),
            DefaultOptions);

        var ruleInsight = run.Insights.Single(i => i.Origin == CashFlowInsightOrigin.Rule);
        var aiInsight = run.Insights.Single(i => i.Origin == CashFlowInsightOrigin.Ai);

        Assert.True(ruleInsight.SortOrder < aiInsight.SortOrder);
    }

    [Fact]
    public void Apply_ZeroInsightBudget_AddsNothing()
    {
        var run = BuildRun();

        CashFlowAiAdjustmentApplier.Apply(
            run,
            Analysis(40m, 45m, 15m, new[] { new CashFlowAiInsight { Title = "Ignorée" } }),
            new AiCashForecastOptions { Enabled = true, MaxProbabilityShiftPoints = 15, MaxInsights = 0 });

        Assert.Empty(run.Insights);
    }
}
