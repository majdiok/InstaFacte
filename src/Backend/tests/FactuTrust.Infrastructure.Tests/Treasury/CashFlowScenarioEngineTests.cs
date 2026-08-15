using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Treasury;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Cœur déterministe de la projection. Toute régression ici fait sortir des chiffres faux à
/// l'écran sans qu'aucune erreur ne soit levée — d'où une couverture serrée.
/// </summary>
public sealed class CashFlowScenarioEngineTests
{
    private static readonly DateTime From = new(2026, 6, 1);
    private static readonly DateTime To = new(2026, 8, 31);

    private static CashFlowLineDraft Inflow(
        DateTime date,
        decimal amount,
        decimal probability = 100m,
        bool confirmed = true) =>
        new()
        {
            Direction = CashFlowDirection.Inflow,
            SourceType = CashFlowSourceType.ClientInvoice,
            Label = "Encaissement",
            ContractualDate = date,
            ExpectedDate = date,
            Amount = amount,
            ProbabilityPercent = probability,
            IsConfirmed = confirmed
        };

    private static CashFlowLineDraft Outflow(DateTime date, decimal amount) =>
        new()
        {
            Direction = CashFlowDirection.Outflow,
            SourceType = CashFlowSourceType.SupplierInvoice,
            Label = "Décaissement",
            ContractualDate = date,
            ExpectedDate = date,
            Amount = amount,
            ProbabilityPercent = 100m,
            IsConfirmed = true
        };

    // ──────────────────── Découpage mensuel ────────────────────

    [Fact]
    public void Compute_SplitsHorizonIntoOneSlotPerMonth()
    {
        var result = CashFlowScenarioEngine.Compute(
            Array.Empty<CashFlowLineDraft>(), 0m, From, To, 1.96);

        Assert.Equal(3, result.Monthly.Count);
        Assert.Equal(new DateTime(2026, 6, 1), result.Monthly[0].PeriodStart);
        Assert.Equal(new DateTime(2026, 6, 30), result.Monthly[0].PeriodEnd);
        Assert.Equal(new DateTime(2026, 8, 31), result.Monthly[2].PeriodEnd);
    }

    [Fact]
    public void Compute_FirstSlotStartsAtHorizonStartNotMonthStart()
    {
        // Une projection lancée le 15 ne doit pas embarquer les flux du 1er au 14, déjà passés.
        var result = CashFlowScenarioEngine.Compute(
            Array.Empty<CashFlowLineDraft>(), 0m, new DateTime(2026, 6, 15), To, 1.96);

        Assert.Equal(new DateTime(2026, 6, 15), result.Monthly[0].PeriodStart);
        Assert.Equal(new DateTime(2026, 6, 30), result.Monthly[0].PeriodEnd);
    }

    [Fact]
    public void Compute_AssignsFlowsToTheirExpectedMonth()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 1_000m),
            Inflow(new DateTime(2026, 7, 10), 2_000m),
            Outflow(new DateTime(2026, 7, 20), 500m)
        };

        var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

        Assert.Equal(1_000m, result.Monthly[0].Inflows);
        Assert.Equal(2_000m, result.Monthly[1].Inflows);
        Assert.Equal(500m, result.Monthly[1].Outflows);
        Assert.Equal(0m, result.Monthly[2].Inflows);
    }

    [Fact]
    public void Compute_WeightsFlowsByProbability()
    {
        var lines = new[] { Inflow(new DateTime(2026, 6, 10), 10_000m, probability: 70m, confirmed: false) };

        var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

        Assert.Equal(7_000m, result.Monthly[0].Inflows);
        Assert.Equal(7_000m, result.TotalInflows);
    }

    // ──────────────────── Intervalle de confiance ────────────────────

    [Fact]
    public void IntervalHalfWidth_IsZeroWhenEveryFlowIsCertain()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 50_000m),
            Outflow(new DateTime(2026, 7, 10), 20_000m)
        };

        var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

        for (var i = 0; i < result.Monthly.Count; i++)
            Assert.Equal(0m, CashFlowScenarioEngine.IntervalHalfWidth(result.Monthly, i, 1.96));
    }

    [Fact]
    public void IntervalHalfWidth_WidensWithHorizon()
    {
        // L'incertitude du solde de clôture s'accumule : la fourchette du mois 3 ne peut pas être
        // plus étroite que celle du mois 1.
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 10_000m, 50m, confirmed: false),
            Inflow(new DateTime(2026, 7, 10), 10_000m, 50m, confirmed: false),
            Inflow(new DateTime(2026, 8, 10), 10_000m, 50m, confirmed: false)
        };

        var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

        var first = CashFlowScenarioEngine.IntervalHalfWidth(result.Monthly, 0, 1.96);
        var second = CashFlowScenarioEngine.IntervalHalfWidth(result.Monthly, 1, 1.96);
        var third = CashFlowScenarioEngine.IntervalHalfWidth(result.Monthly, 2, 1.96);

        Assert.True(first > 0m);
        Assert.True(second > first);
        Assert.True(third > second);
    }

    // ──────────────────── Scénarios ────────────────────

    [Fact]
    public void Scenarios_ProbabilitiesAlwaysSumToOneHundred()
    {
        foreach (var confirmed in new[] { true, false })
        {
            var lines = new[] { Inflow(new DateTime(2026, 6, 10), 10_000m, 60m, confirmed) };
            var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

            Assert.Equal(100m, result.Scenarios.Sum(s => s.ProbabilityPercent));
        }
    }

    [Fact]
    public void Scenarios_OptimisticNeverBelowRealisticNeverBelowPessimistic()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 100_000m, 60m, confirmed: false),
            Outflow(new DateTime(2026, 6, 20), 30_000m)
        };

        var result = CashFlowScenarioEngine.Compute(lines, 50_000m, From, To, 1.96);

        var optimistic = result.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Optimistic);
        var realistic = result.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Realistic);
        var pessimistic = result.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Pessimistic);

        Assert.True(optimistic.ClosingBalance > realistic.ClosingBalance);
        Assert.True(realistic.ClosingBalance > pessimistic.ClosingBalance);
    }

    [Fact]
    public void Scenarios_ClosingBalanceChainsFromOpeningBalance()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 40_000m),
            Outflow(new DateTime(2026, 6, 20), 15_000m)
        };

        var result = CashFlowScenarioEngine.Compute(lines, 87_540.250m, From, To, 1.96);
        var realistic = result.Scenarios.Single(s => s.Kind == CashFlowScenarioKind.Realistic);

        Assert.Equal(25_000m, realistic.NetFlow);
        Assert.Equal(112_540.250m, realistic.ClosingBalance);
    }

    [Fact]
    public void Scenarios_ConfirmedInflowsRaiseTheOptimisticProbability()
    {
        var uncertain = new[] { Inflow(new DateTime(2026, 6, 10), 10_000m, 60m, confirmed: false) };
        var certain = new[] { Inflow(new DateTime(2026, 6, 10), 10_000m, 100m, confirmed: true) };

        var uncertainOptimistic = CashFlowScenarioEngine.ResolveScenarioProbabilities(uncertain).Optimistic;
        var certainOptimistic = CashFlowScenarioEngine.ResolveScenarioProbabilities(certain).Optimistic;

        Assert.Equal(20m, uncertainOptimistic);
        Assert.Equal(40m, certainOptimistic);
    }

    [Fact]
    public void Scenarios_OutflowsAreIdenticalAcrossScenarios()
    {
        // Un décaissement est un engagement de la société : le faire varier d'un scénario à l'autre
        // reviendrait à budgéter un défaut de paiement dans le scénario pessimiste.
        var lines = new[] { Outflow(new DateTime(2026, 6, 20), 30_000m) };

        var result = CashFlowScenarioEngine.Compute(lines, 0m, From, To, 1.96);

        Assert.All(result.Scenarios, s => Assert.Equal(-30_000m, s.NetFlow));
    }

    // ──────────────────── Indice de confiance ────────────────────

    [Fact]
    public void Confidence_IsOneHundredWhenEveryFlowIsCertain()
    {
        var lines = new[] { Inflow(new DateTime(2026, 6, 10), 1_000m), Outflow(new DateTime(2026, 6, 11), 500m) };

        Assert.Equal(100m, CashFlowScenarioEngine.ComputeConfidencePercent(lines));
    }

    [Fact]
    public void Confidence_IsZeroWithoutAnyFlow()
    {
        Assert.Equal(0m, CashFlowScenarioEngine.ComputeConfidencePercent(Array.Empty<CashFlowLineDraft>()));
    }

    [Fact]
    public void Confidence_ReflectsWeightedShareOfVolume()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 10_000m, 100m),
            Inflow(new DateTime(2026, 6, 11), 10_000m, 50m, confirmed: false)
        };

        // (10 000 + 5 000) / 20 000 = 75 %
        Assert.Equal(75m, CashFlowScenarioEngine.ComputeConfidencePercent(lines));
    }

    // ──────────────────── Reproductibilité ────────────────────

    [Fact]
    public void Compute_IsReproducibleForIdenticalInput()
    {
        var lines = new[]
        {
            Inflow(new DateTime(2026, 6, 10), 12_345.678m, 63m, confirmed: false),
            Outflow(new DateTime(2026, 7, 3), 4_321.123m)
        };

        var first = CashFlowScenarioEngine.Compute(lines, 1_000m, From, To, 1.96);
        var second = CashFlowScenarioEngine.Compute(lines, 1_000m, From, To, 1.96);

        Assert.Equal(first.TotalInflows, second.TotalInflows);
        Assert.Equal(first.TotalOutflows, second.TotalOutflows);
        Assert.Equal(first.ConfidencePercent, second.ConfidencePercent);
        Assert.Equal(
            first.Scenarios.Select(s => s.ClosingBalance),
            second.Scenarios.Select(s => s.ClosingBalance));
    }
}
