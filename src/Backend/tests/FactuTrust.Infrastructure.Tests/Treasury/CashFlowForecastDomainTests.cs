using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Invariants du domaine « Trésorerie prévisionnelle ». Ces règles sont la seule protection contre
/// une projection incohérente : un solde qui ne s'enchaîne pas d'un mois sur l'autre, ou une
/// probabilité de scénario que le modèle de langage aurait pu déplacer sans limite.
/// </summary>
public sealed class CashFlowForecastDomainTests
{
    private static CashFlowForecastRun StartRun(decimal opening = 100_000m) =>
        CashFlowForecastRun.Start(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 11, 30),
            horizonMonths: 6,
            openingBalance: opening,
            currency: "TND",
            computedByUserId: Guid.NewGuid());

    // ──────────────────────────── Run ────────────────────────────

    [Fact]
    public void Start_PeriodEndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastRun.Start(
            new DateTime(2026, 11, 30),
            new DateTime(2026, 6, 1),
            6,
            0m,
            "TND",
            null));
    }

    [Fact]
    public void Start_HorizonBelowOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastRun.Start(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 11, 30),
            0,
            0m,
            "TND",
            null));
    }

    [Fact]
    public void Start_NegativeOpeningBalance_IsAccepted()
    {
        // Une société à découvert doit pouvoir être projetée : c'est précisément le cas où
        // l'écran est utile. Money.Subtract aurait levé ici, d'où le choix du decimal signé.
        var run = StartRun(opening: -12_500.750m);

        Assert.Equal(-12_500.750m, run.OpeningBalance);
        Assert.Equal(CashFlowForecastRunStatus.Running, run.Status);
    }

    [Fact]
    public void Complete_DerivesNetFlowAndClosingBalance()
    {
        var run = StartRun(opening: 87_540.250m);

        run.Complete(
            totalInflows: 1_245_780.500m,
            totalOutflows: 1_220_890.000m,
            confidencePercent: 87m,
            methodUsed: ForecastMethod.Holt,
            durationMs: 1_234,
            inputsJson: "{}");

        Assert.Equal(24_890.500m, run.NetFlow);
        Assert.Equal(112_430.750m, run.ClosingBalance);
        Assert.Equal(CashFlowForecastRunStatus.Computed, run.Status);
    }

    [Fact]
    public void Complete_OutflowsAboveInflows_ProducesNegativeClosingBalance()
    {
        var run = StartRun(opening: 10_000m);

        run.Complete(
            totalInflows: 5_000m,
            totalOutflows: 27_230m,
            confidencePercent: 60m,
            methodUsed: ForecastMethod.Sma,
            durationMs: 10,
            inputsJson: null);

        Assert.Equal(-22_230m, run.NetFlow);
        Assert.Equal(-12_230m, run.ClosingBalance);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Complete_ConfidenceOutOfRange_Throws(decimal confidence)
    {
        var run = StartRun();

        Assert.Throws<ArgumentException>(() => run.Complete(0m, 0m, confidence, ForecastMethod.Sma, 0, null));
    }

    [Fact]
    public void MarkFailed_KeepsRunOutOfComputedResults()
    {
        var run = StartRun();

        run.MarkFailed("Balance indisponible", 42);

        Assert.Equal(CashFlowForecastRunStatus.Failed, run.Status);
        Assert.Equal("Balance indisponible", run.ErrorMessage);
    }

    [Fact]
    public void MarkAiAnalyzed_WithoutAdjustment_KeepsFlagFalse()
    {
        // Cas du repli : le modèle a répondu mais rien n'a été retenu. L'écran ne doit alors
        // afficher aucun badge « pondéré par l'IA ».
        var run = StartRun();

        run.MarkAiAnalyzed("ollama:qwen2.5", probabilitiesAdjusted: false);

        Assert.False(run.AiAdjustmentApplied);
        Assert.Equal("ollama:qwen2.5", run.AiModelRef);
        Assert.NotNull(run.AiAnalyzedAt);
    }

    // ──────────────────────────── Bucket ────────────────────────────

    [Fact]
    public void Bucket_ChainsOpeningToClosing()
    {
        var bucket = CashFlowForecastBucket.Create(
            sequenceIndex: 0,
            periodStart: new DateTime(2026, 6, 1),
            periodEnd: new DateTime(2026, 6, 30),
            openingBalance: 87_540.250m,
            inflows: 245_680.500m,
            outflows: 254_900.250m,
            intervalHalfWidth: 12_000m);

        Assert.Equal(-9_219.750m, bucket.NetFlow);
        Assert.Equal(78_320.500m, bucket.ClosingBalance);
        Assert.Equal(66_320.500m, bucket.LowClosingBalance);
        Assert.Equal(90_320.500m, bucket.HighClosingBalance);
    }

    [Fact]
    public void Bucket_ZeroInterval_CollapsesLowAndHighOntoClosing()
    {
        // Quand tous les flux du mois sont certains, l'intervalle doit disparaître : afficher une
        // fourchette là où il n'y a aucune incertitude induirait le lecteur en erreur.
        var bucket = CashFlowForecastBucket.Create(
            0, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
            openingBalance: 1_000m, inflows: 500m, outflows: 200m, intervalHalfWidth: 0m);

        Assert.Equal(bucket.ClosingBalance, bucket.LowClosingBalance);
        Assert.Equal(bucket.ClosingBalance, bucket.HighClosingBalance);
    }

    [Theory]
    [InlineData(-1d, 0d, 0d)]
    [InlineData(0d, -1d, 0d)]
    [InlineData(0d, 0d, -1d)]
    public void Bucket_NegativeInputs_Throw(double inflows, double outflows, double halfWidth)
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastBucket.Create(
            0,
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 30),
            openingBalance: 0m,
            inflows: (decimal)inflows,
            outflows: (decimal)outflows,
            intervalHalfWidth: (decimal)halfWidth));
    }

    // ──────────────────────────── Line ────────────────────────────

    [Fact]
    public void Line_WeightsAmountByProbability()
    {
        var line = CashFlowForecastLine.Create(
            CashFlowDirection.Inflow,
            CashFlowSourceType.ClientInvoice,
            "Facture FAC-2026-0142",
            contractualDate: new DateTime(2026, 6, 5),
            expectedDate: new DateTime(2026, 6, 19),
            amount: 85_200m,
            probabilityPercent: 70m,
            isConfirmed: false,
            thirdPartyName: "Société Alpha");

        Assert.Equal(85_200m, line.Amount);
        Assert.Equal(59_640m, line.WeightedAmount);
        // Le décalage contractuel → attendu doit rester lisible : c'est ce qui explique à
        // l'utilisateur pourquoi l'encaissement n'est pas projeté à l'échéance.
        Assert.Equal(new DateTime(2026, 6, 5), line.ContractualDate);
        Assert.Equal(new DateTime(2026, 6, 19), line.ExpectedDate);
    }

    [Fact]
    public void Line_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastLine.Create(
            CashFlowDirection.Outflow,
            CashFlowSourceType.SupplierInvoice,
            "Facture fournisseur",
            new DateTime(2026, 6, 5),
            new DateTime(2026, 6, 5),
            amount: -10m,
            probabilityPercent: 100m,
            isConfirmed: true));
    }

    [Fact]
    public void Line_ProbabilityAboveHundred_Throws()
    {
        Assert.Throws<ArgumentException>(() => CashFlowForecastLine.Create(
            CashFlowDirection.Inflow,
            CashFlowSourceType.ClientEffet,
            "Traite",
            new DateTime(2026, 6, 5),
            new DateTime(2026, 6, 5),
            amount: 10m,
            probabilityPercent: 101m,
            isConfirmed: true));
    }

    // ──────────────────────────── Scenario ────────────────────────────

    [Fact]
    public void Scenario_StartsDeterministic()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 112_430m, 24_890m, 50m);

        Assert.Equal(50m, scenario.DeterministicProbabilityPercent);
        Assert.Equal(50m, scenario.ProbabilityPercent);
        Assert.Equal(CashFlowProbabilitySource.Deterministic, scenario.ProbabilitySource);
        Assert.Null(scenario.AiRationale);
    }

    [Fact]
    public void ApplyAiProbability_ClampsAboveUpperBound()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Optimistic, 158_780m, 71_350m, 30m);

        scenario.ApplyAiProbability(proposedPercent: 90m, maxShiftPoints: 15, rationale: "Carnet solide");

        // 30 + 15 = 45 : le modèle ne peut pas transformer un scénario secondaire en scénario
        // dominant, quel que soit son aplomb.
        Assert.Equal(45m, scenario.ProbabilityPercent);
        Assert.Equal(30m, scenario.DeterministicProbabilityPercent);
        Assert.Equal(CashFlowProbabilitySource.AiAdjusted, scenario.ProbabilitySource);
        Assert.Equal("Carnet solide", scenario.AiRationale);
    }

    [Fact]
    public void ApplyAiProbability_ClampsBelowLowerBound()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Pessimistic, -12_230m, -99_980m, 20m);

        scenario.ApplyAiProbability(proposedPercent: 0m, maxShiftPoints: 15, rationale: null);

        Assert.Equal(5m, scenario.ProbabilityPercent);
    }

    [Fact]
    public void ApplyAiProbability_NeverLeavesZeroHundredRange()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 0m, 0m, 8m);

        scenario.ApplyAiProbability(proposedPercent: -50m, maxShiftPoints: 15, rationale: null);

        // 8 − 15 = −7 doit être ramené à 0, pas persisté négatif.
        Assert.Equal(0m, scenario.ProbabilityPercent);
    }

    [Fact]
    public void ApplyAiProbability_ZeroShiftBudget_KeepsDeterministicValue()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 0m, 0m, 50m);

        scenario.ApplyAiProbability(proposedPercent: 99m, maxShiftPoints: 0, rationale: "…");

        Assert.Equal(50m, scenario.ProbabilityPercent);
    }

    [Fact]
    public void NormalizeProbability_KeepsDeterministicValueAndSource()
    {
        var scenario = CashFlowScenario.Create(CashFlowScenarioKind.Realistic, 0m, 0m, 50m);
        scenario.ApplyAiProbability(55m, 15, "…");

        scenario.NormalizeProbability(52.35m);

        Assert.Equal(52.35m, scenario.ProbabilityPercent);
        Assert.Equal(50m, scenario.DeterministicProbabilityPercent);
        Assert.Equal(CashFlowProbabilitySource.AiAdjusted, scenario.ProbabilitySource);
    }

    // ──────────────────── Engagements récurrents ────────────────────

    [Theory]
    [InlineData(CashCommitmentFrequency.Monthly, 1)]
    [InlineData(CashCommitmentFrequency.Quarterly, 3)]
    [InlineData(CashCommitmentFrequency.SemiAnnual, 6)]
    [InlineData(CashCommitmentFrequency.Annual, 12)]
    public void Commitment_MonthStepMatchesFrequency(CashCommitmentFrequency frequency, int expected)
    {
        var commitment = RecurringCashCommitment.Create(
            "Loyer", CashFlowDirection.Outflow, 2_500m, frequency, 5, new DateTime(2026, 1, 1));

        Assert.Equal(expected, commitment.MonthStep);
    }

    [Fact]
    public void Commitment_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringCashCommitment.Create(
            "Loyer",
            CashFlowDirection.Outflow,
            2_500m,
            CashCommitmentFrequency.Monthly,
            5,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 1, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Commitment_DayOfMonthOutOfRange_Throws(int day)
    {
        Assert.Throws<ArgumentException>(() => RecurringCashCommitment.Create(
            "Loyer", CashFlowDirection.Outflow, 2_500m, CashCommitmentFrequency.Monthly, day, new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void Commitment_NonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringCashCommitment.Create(
            "Loyer", CashFlowDirection.Outflow, 0m, CashCommitmentFrequency.Monthly, 5, new DateTime(2026, 1, 1)));
    }

    // ──────────────────────────── Settings ────────────────────────────

    [Fact]
    public void Settings_DefaultsDerivedFromPayrollCost()
    {
        var settings = CashFlowForecastSettings.CreateDefault(averageMonthlyPayrollCost: 40_000m);

        Assert.Equal(0m, settings.CriticalThreshold);
        Assert.Equal(20_000m, settings.AlertThreshold);
        Assert.Equal(40_000m, settings.ComfortThreshold);
    }

    [Fact]
    public void Settings_UpdateRejectsOutOfOrderThresholds()
    {
        var settings = CashFlowForecastSettings.CreateDefault(40_000m);

        Assert.Throws<ArgumentException>(() => settings.Update(
            criticalThreshold: 50_000m, alertThreshold: 20_000m, comfortThreshold: 100_000m, null));

        Assert.Throws<ArgumentException>(() => settings.Update(
            criticalThreshold: 0m, alertThreshold: 80_000m, comfortThreshold: 50_000m, null));
    }

    [Fact]
    public void Settings_UpdateAcceptsNegativeCriticalThreshold()
    {
        // Une société disposant d'une autorisation de découvert place légitimement son seuil
        // critique sous zéro.
        var settings = CashFlowForecastSettings.CreateDefault(40_000m);

        settings.Update(-20_000m, 20_000m, 100_000m, payrollPaymentDayOfMonth: 25);

        Assert.Equal(-20_000m, settings.CriticalThreshold);
        Assert.Equal(25, settings.PayrollPaymentDayOfMonth);
    }
}
