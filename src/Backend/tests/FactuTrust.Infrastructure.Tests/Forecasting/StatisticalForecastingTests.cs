using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.Forecasting.Statistics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Forecasting;

/// <summary>
/// Unit tests for the deterministic statistical forecasting primitives.
/// These tests are the single source of truth for the algorithmic correctness:
/// any regression here means the AI Forecasting module ships wrong numbers, full stop.
/// </summary>
public sealed class StatisticalForecastingTests
{
    // ──────────────────── Validation / edge cases ─────────────────────────

    [Fact]
    public void Forecast_NullHistory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StatisticalForecasting.Forecast(null!, 1));
    }

    [Fact]
    public void Forecast_NegativeValueInHistory_Throws()
    {
        var history = new[] { 10.0, -5.0, 12.0 };
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.Forecast(history, 1));
    }

    [Fact]
    public void Forecast_NaNInHistory_Throws()
    {
        var history = new[] { 10.0, double.NaN, 12.0 };
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.Forecast(history, 1));
    }

    [Fact]
    public void Forecast_HorizonZero_Throws()
    {
        var history = new[] { 10.0, 11.0, 12.0 };
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.Forecast(history, 0));
    }

    [Fact]
    public void Forecast_EmptyHistory_ReturnsZeroFlatForecast()
    {
        var result = StatisticalForecasting.Forecast(Array.Empty<double>(), 3);
        Assert.All(result.Point, p => Assert.Equal(0, p));
        Assert.Equal(ForecastMethod.Sma, result.MethodUsed);
    }

    // ──────────────────── SMA — short history ─────────────────────────────

    [Fact]
    public void Forecast_ShortHistory_UsesSma()
    {
        // Only 3 points — below default minHistoryForHolt (6) → SMA
        var history = new[] { 100.0, 110.0, 120.0 };
        var result = StatisticalForecasting.Forecast(history, 2);

        Assert.Equal(ForecastMethod.Sma, result.MethodUsed);
        // SMA point estimate = mean of last 3 = 110
        Assert.Equal(110.0, result.Point[0], 6);
        Assert.Equal(110.0, result.Point[1], 6);
        Assert.True(result.Low[0] <= result.Point[0]);
        Assert.True(result.High[0] >= result.Point[0]);
        // Variance widens
        Assert.True(result.High[1] - result.Low[1] >= result.High[0] - result.Low[0]);
    }

    [Fact]
    public void ForecastSma_ConstantSeries_ReturnsExactMean_NoVariance()
    {
        var history = new[] { 50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0, 50.0 };
        var result = StatisticalForecasting.ForecastSma(history, 3);
        Assert.Equal(50.0, result.Point[0], 6);
        Assert.Equal(50.0, result.Point[1], 6);
        Assert.Equal(50.0, result.Point[2], 6);
        // Constant series → zero residual std dev → zero-width interval
        Assert.Equal(0, result.ResidualStdDev, 6);
        Assert.Equal(50.0, result.Low[0], 6);
        Assert.Equal(50.0, result.High[0], 6);
    }

    [Fact]
    public void ForecastSma_ZeroSeries_ReturnsZeroForecast()
    {
        var history = new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
        var result = StatisticalForecasting.ForecastSma(history, 2);
        Assert.Equal(0.0, result.Point[0], 6);
        Assert.Equal(0.0, result.Low[0], 6);
        Assert.Equal(0.0, result.High[0], 6);
    }

    [Fact]
    public void ForecastSma_LowBound_NeverNegative()
    {
        // High variance series — low bound must clamp to 0 (sales can't be negative).
        var history = new[] { 10.0, 0.0, 50.0, 0.0, 100.0, 0.0, 25.0 };
        var result = StatisticalForecasting.ForecastSma(history, 5);
        Assert.All(result.Low, l => Assert.True(l >= 0, $"Low={l} must be ≥ 0"));
    }

    // ──────────────────── Holt — trend extrapolation ──────────────────────

    [Fact]
    public void ForecastHolt_LinearTrend_RespectsTrendDirection()
    {
        // Strong linear upward trend: 100, 110, 120, ..., 200
        var history = Enumerable.Range(0, 11).Select(i => 100.0 + 10 * i).ToArray();
        var result = StatisticalForecasting.ForecastHolt(history, 3);

        Assert.Equal(ForecastMethod.Holt, result.MethodUsed);
        // Forecast must continue the trend upward: each step ≥ previous
        Assert.True(result.Point[0] >= 200, $"Point[0]={result.Point[0]} should ≥ 200");
        Assert.True(result.Point[1] >= result.Point[0]);
        Assert.True(result.Point[2] >= result.Point[1]);
        // Roughly: 210, 220, 230 with some smoothing tolerance
        Assert.InRange(result.Point[0], 200, 220);
        Assert.InRange(result.Point[2], 215, 245);
    }

    [Fact]
    public void ForecastHolt_DownwardTrend_DoesNotProduceNegatives()
    {
        // Steep downward trend that would hit 0 quickly
        var history = new[] { 50.0, 40.0, 30.0, 20.0, 10.0, 5.0 };
        var result = StatisticalForecasting.ForecastHolt(history, 5);
        Assert.All(result.Point, p => Assert.True(p >= 0, $"Point={p} must be ≥ 0"));
        Assert.All(result.Low, l => Assert.True(l >= 0));
    }

    [Fact]
    public void ForecastHolt_FlatSeries_ReturnsFlatForecast()
    {
        var history = new[] { 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0 };
        var result = StatisticalForecasting.ForecastHolt(history, 4);
        // Flat series → trend ≈ 0, level ≈ 100 → forecast ≈ 100
        Assert.All(result.Point, p => Assert.InRange(p, 99.5, 100.5));
    }

    // ──────────────────── Holt-Winters — seasonality ──────────────────────

    [Fact]
    public void ForecastHoltWinters_RespectsSeasonality()
    {
        // 24 months: peak in months 3, 11, 23 (every 12 months); base 100, peak 200
        var history = Enumerable.Range(0, 24)
            .Select(i => i % 12 == 11 ? 200.0 : 100.0)
            .ToArray();

        var result = StatisticalForecasting.Forecast(history, 12, seasonalPeriod: 12);

        Assert.Equal(ForecastMethod.HoltWinters, result.MethodUsed);
        // Index 0 of the forecast = position 24 mod 12 = 0 → low
        // Index 11 of the forecast = position 35 mod 12 = 11 → high (peak)
        Assert.True(result.Point[11] > result.Point[0],
            $"Seasonal peak at h=11 ({result.Point[11]}) must exceed off-season at h=0 ({result.Point[0]})");
        // Peak should be roughly in the right ballpark
        Assert.InRange(result.Point[11], 150, 250);
    }

    [Fact]
    public void ForecastHoltWinters_InsufficientHistory_DowngradesToHolt()
    {
        // 18 months — below the 24 default → expects Holt fallback
        var history = Enumerable.Range(0, 18).Select(i => 100.0 + i * 2).ToArray();
        var result = StatisticalForecasting.Forecast(history, 3, seasonalPeriod: 12);
        Assert.Equal(ForecastMethod.Holt, result.MethodUsed);
    }

    // ──────────────────── Confidence interval consistency ─────────────────

    [Fact]
    public void Forecast_PointInsidePredictionInterval_AlwaysHolds()
    {
        var rng = new Random(42);
        var history = Enumerable.Range(0, 30).Select(_ => 50 + rng.NextDouble() * 50).ToArray();
        var result = StatisticalForecasting.Forecast(history, 6);
        for (int i = 0; i < 6; i++)
        {
            Assert.True(result.Low[i] <= result.Point[i], $"Low[{i}]={result.Low[i]} > Point[{i}]={result.Point[i]}");
            Assert.True(result.Point[i] <= result.High[i], $"Point[{i}]={result.Point[i]} > High[{i}]={result.High[i]}");
        }
    }

    [Fact]
    public void Forecast_ConfidencePercent_InRange()
    {
        var history = Enumerable.Range(0, 24).Select(i => 100.0 + 2 * i).ToArray();
        var result = StatisticalForecasting.Forecast(history, 3);
        Assert.InRange(result.ConfidencePercent, 0, 100);
    }

    // ──────────────────── Helpers ─────────────────────────────────────────

    [Fact]
    public void StandardDeviation_LessThanTwoSamples_ReturnsZero()
    {
        Assert.Equal(0, StatisticalForecasting.StandardDeviation(Array.Empty<double>()), 6);
        Assert.Equal(0, StatisticalForecasting.StandardDeviation(new[] { 42.0 }), 6);
    }

    [Fact]
    public void StandardDeviation_KnownSeries()
    {
        // Population std dev of {2, 4, 4, 4, 5, 5, 7, 9} = 2 (classic stats example)
        var values = new[] { 2.0, 4, 4, 4, 5, 5, 7, 9 };
        Assert.Equal(2.0, StatisticalForecasting.StandardDeviation(values), 6);
    }

    [Fact]
    public void Mean_EmptyOrSingle()
    {
        Assert.Equal(0, StatisticalForecasting.Mean(Array.Empty<double>()), 6);
        Assert.Equal(42.0, StatisticalForecasting.Mean(new[] { 42.0 }), 6);
    }

    [Fact]
    public void CoefficientOfVariation_ZeroMean_ReturnsZero()
    {
        var values = new[] { 0.0, 0.0, 0.0 };
        Assert.Equal(0, StatisticalForecasting.CoefficientOfVariation(values), 6);
    }

    [Fact]
    public void CoefficientOfVariation_Constant_ReturnsZero()
    {
        var values = new[] { 100.0, 100.0, 100.0, 100.0 };
        Assert.Equal(0, StatisticalForecasting.CoefficientOfVariation(values), 6);
    }

    // ──────────────────── ABC / XYZ classification ────────────────────────

    [Fact]
    public void ComputeAbc_ParetoDistribution_ClassifiesTopSellersAsA()
    {
        // 80/20 distribution: top 2 products = 80% of revenue, last 8 = 20%
        var revenues = new[] { 400.0, 400.0, 25.0, 25.0, 25.0, 25.0, 25.0, 25.0, 25.0, 25.0 };
        var (classes, cum) = StatisticalForecasting.ComputeAbc(revenues);

        // Sum = 400+400 + 8*25 = 1000; the first 2 = 800/1000 = 80% → exactly A boundary.
        Assert.Equal(AbcClass.A, classes[0]);
        Assert.Equal(AbcClass.A, classes[1]);
        Assert.Equal(80.0, cum[1], 1);
        // Subsequent items are B then eventually C.
        Assert.True(classes.Count(c => c == AbcClass.A) >= 2);
        Assert.True(classes.Count(c => c == AbcClass.C) >= 1);
    }

    [Fact]
    public void ComputeAbc_AllZeroRevenue_AllUnclassified()
    {
        var revenues = new[] { 0.0, 0.0, 0.0 };
        var (classes, cum) = StatisticalForecasting.ComputeAbc(revenues);
        Assert.All(classes, c => Assert.Equal(AbcClass.Unclassified, c));
        Assert.All(cum, p => Assert.Equal(0, p, 6));
    }

    [Fact]
    public void ComputeAbc_EmptyInput_ReturnsEmpty()
    {
        var (classes, cum) = StatisticalForecasting.ComputeAbc(Array.Empty<double>());
        Assert.Empty(classes);
        Assert.Empty(cum);
    }

    [Fact]
    public void ComputeAbc_InvalidThresholds_Throws()
    {
        var revenues = new[] { 100.0, 50.0 };
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeAbc(revenues, aThreshold: -1));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeAbc(revenues, aThreshold: 50, bThreshold: 40));
    }

    [Fact]
    public void ComputeXyz_StableDemand_ReturnsX()
    {
        var demand = new[] { 100.0, 102.0, 98.0, 100.0, 101.0, 99.0 };
        Assert.Equal(XyzClass.X, StatisticalForecasting.ComputeXyz(demand));
    }

    [Fact]
    public void ComputeXyz_ErraticDemand_ReturnsZ()
    {
        var demand = new[] { 0.0, 200.0, 0.0, 50.0, 0.0, 150.0 };
        Assert.Equal(XyzClass.Z, StatisticalForecasting.ComputeXyz(demand));
    }

    [Fact]
    public void ComputeXyz_ZeroDemand_Unclassified()
    {
        var demand = new[] { 0.0, 0.0, 0.0 };
        Assert.Equal(XyzClass.Unclassified, StatisticalForecasting.ComputeXyz(demand));
    }

    // ──────────────────── Replenishment math ──────────────────────────────

    [Fact]
    public void ComputeReplenishment_BasicCase()
    {
        // d=5 units/day, σ=2, L=7 days, Z=1.65 → SS = 1.65 × 2 × √7 ≈ 8.731
        // ROP = 5×7 + 8.731 ≈ 43.731
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 5m,
            demandStdDev: 2m,
            leadTimeDays: 7,
            serviceLevelZ: 1.65m,
            quantityOnHand: 10m,
            maximumStock: 60m);

        // Tolerate small rounding
        Assert.InRange((double)math.SafetyStock, 8.6, 8.8);
        Assert.InRange((double)math.Rop, 43.6, 43.8);
        // Max stock - on hand = 60 - 10 = 50 ≥ minQty (rop - 10 ≈ 33.7) → 50
        Assert.Equal(50m, math.RecommendedQty);
    }

    [Fact]
    public void ComputeReplenishment_ZeroLeadTime_NoSafetyStock()
    {
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 5m,
            demandStdDev: 2m,
            leadTimeDays: 0,
            serviceLevelZ: 1.65m,
            quantityOnHand: 10m,
            maximumStock: 0m);
        Assert.Equal(0m, math.SafetyStock);
        Assert.Equal(0m, math.Rop);
    }

    [Fact]
    public void ComputeReplenishment_NoMaxStock_FallsBackTo2xLeadDemand()
    {
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 4m,
            demandStdDev: 1m,
            leadTimeDays: 5,
            serviceLevelZ: 1.65m,
            quantityOnHand: 0m,
            maximumStock: 0m);
        // 2 × 4 × 5 = 40, but rop ≈ 20 + 1.65*1*√5 ≈ 23.69 < 40 → 40
        Assert.Equal(40m, math.RecommendedQty);
    }

    [Fact]
    public void ComputeReplenishment_NegativeInputs_Throw()
    {
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(-1m, 0m, 1, 1m, 0m, 0m));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(1m, -1m, 1, 1m, 0m, 0m));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(1m, 0m, -1, 1m, 0m, 0m));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(1m, 0m, 1, -1m, 0m, 0m));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(1m, 0m, 1, 1m, -1m, 0m));
        Assert.Throws<ArgumentException>(() => StatisticalForecasting.ComputeReplenishment(1m, 0m, 1, 1m, 0m, 0m, -1m));
    }

    [Fact]
    public void ComputeReplenishment_OnOrderDeductedFromMaxStockRefill()
    {
        // Same baseline as ComputeReplenishment_BasicCase (qty would be 50 without on-order).
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 5m,
            demandStdDev: 2m,
            leadTimeDays: 7,
            serviceLevelZ: 1.65m,
            quantityOnHand: 10m,
            maximumStock: 60m,
            quantityOnOrder: 20m);

        // available = 10 + 20 = 30 → refill = 60 - 30 = 30 ≥ floor (rop - 30 ≈ 13.7) → 30.
        Assert.Equal(30m, math.RecommendedQty);
    }

    [Fact]
    public void ComputeReplenishment_OnOrderDeductedFromFallback()
    {
        // No max stock → fallback 2 × d × L, minus the on-order part only.
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 4m,
            demandStdDev: 1m,
            leadTimeDays: 5,
            serviceLevelZ: 1.65m,
            quantityOnHand: 0m,
            maximumStock: 0m,
            quantityOnOrder: 15m);

        // 2 × 4 × 5 = 40 − 15 = 25 ≥ floor (rop ≈ 23.69 − 15 ≈ 8.69) → 25.
        Assert.Equal(25m, math.RecommendedQty);
    }

    [Fact]
    public void ComputeReplenishment_OnOrderCoveringNeeds_YieldsZero()
    {
        var math = StatisticalForecasting.ComputeReplenishment(
            dailyDemand: 5m,
            demandStdDev: 2m,
            leadTimeDays: 7,
            serviceLevelZ: 1.65m,
            quantityOnHand: 10m,
            maximumStock: 60m,
            quantityOnOrder: 50m);

        // available = 60 ≥ max stock and ≥ rop → nothing to order (the service skips qty ≤ 0).
        Assert.Equal(0m, math.RecommendedQty);
    }

    [Fact]
    public void ComputeReplenishment_ZeroOnOrder_KeepsHistoricalBehaviour()
    {
        // Regression pin: omitting the new parameter (or passing 0) yields the pre-C2 results.
        var explicit0 = StatisticalForecasting.ComputeReplenishment(5m, 2m, 7, 1.65m, 10m, 60m, 0m);
        var omitted = StatisticalForecasting.ComputeReplenishment(5m, 2m, 7, 1.65m, 10m, 60m);

        Assert.Equal(50m, explicit0.RecommendedQty);
        Assert.Equal(omitted.RecommendedQty, explicit0.RecommendedQty);
        Assert.Equal(omitted.Rop, explicit0.Rop);
        Assert.Equal(omitted.SafetyStock, explicit0.SafetyStock);
    }
}
