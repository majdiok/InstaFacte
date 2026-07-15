using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Forecasting.Statistics;

/// <summary>
/// Pure-C# statistical forecasting primitives used by the AI Forecasting module.
/// All methods are deterministic, side-effect-free and 100% unit-testable. They never call I/O,
/// LLMs, or external APIs. The LLM tools call these algorithms via service handlers — never the reverse.
/// </summary>
/// <remarks>
/// Conventions:
/// - Input series are ordered chronologically (oldest first).
/// - Negative values are not allowed (sales can be zero, never negative — credit notes are netted upstream).
/// - All forecasts return a (Point, Low, High) prediction interval at the requested Z-score.
/// - α/β/γ smoothing parameters are optimised by grid search on the in-sample SSE (sum of squared errors).
/// </remarks>
public static class StatisticalForecasting
{
    /// <summary>
    /// Result of a forecast: point estimate, prediction interval, in-sample residual standard deviation,
    /// and method actually used (the implementation may downgrade to a simpler method when history is short).
    /// </summary>
    public readonly record struct ForecastResult(
        double[] Point,
        double[] Low,
        double[] High,
        double ResidualStdDev,
        ForecastMethod MethodUsed,
        double ConfidencePercent);

    private const double DefaultConfidenceZ = 1.96; // 95%

    /// <summary>
    /// Top-level entry point: choose the best method given the history length and produce the forecast.
    /// </summary>
    /// <param name="history">Chronological values, oldest first. Must not contain negatives.</param>
    /// <param name="horizon">Number of future periods to forecast (≥ 1).</param>
    /// <param name="seasonalPeriod">Seasonality length (e.g. 12 for monthly with yearly cycle). Use 0 to disable.</param>
    /// <param name="minHistoryForHolt">Minimum points to use Holt double-exp smoothing.</param>
    /// <param name="minHistoryForHoltWinters">Minimum points to use Holt-Winters multiplicative.</param>
    /// <param name="confidenceZ">Z-score for the prediction interval (1.96 = 95%, 1.65 = 90%).</param>
    public static ForecastResult Forecast(
        IReadOnlyList<double> history,
        int horizon,
        int seasonalPeriod = 12,
        int minHistoryForHolt = 6,
        int minHistoryForHoltWinters = 24,
        double confidenceZ = DefaultConfidenceZ)
    {
        ValidateHistory(history);
        if (horizon < 1) throw new ArgumentException("Horizon must be ≥ 1", nameof(horizon));

        // Empty history → flat zero forecast (defensive; shouldn't be called).
        if (history.Count == 0)
            return new ForecastResult(new double[horizon], new double[horizon], new double[horizon], 0, ForecastMethod.Sma, 10);

        // < minHistoryForHolt → SMA only (caller may upgrade to CalendarHeuristic).
        if (history.Count < minHistoryForHolt)
        {
            return ForecastSma(history, horizon, confidenceZ);
        }

        // ≥ minHistoryForHoltWinters AND seasonality enabled → Holt-Winters multiplicative
        if (seasonalPeriod > 1 && history.Count >= Math.Max(minHistoryForHoltWinters, 2 * seasonalPeriod))
        {
            return ForecastHoltWintersMultiplicative(history, horizon, seasonalPeriod, confidenceZ);
        }

        // Else → Holt double-exponential
        return ForecastHolt(history, horizon, confidenceZ);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Simple Moving Average — baseline / fallback for short histories
    // ─────────────────────────────────────────────────────────────────────────

    public static ForecastResult ForecastSma(IReadOnlyList<double> history, int horizon, double confidenceZ = DefaultConfidenceZ)
    {
        ValidateHistory(history);
        if (horizon < 1) throw new ArgumentException("Horizon must be ≥ 1", nameof(horizon));

        // Window: at most 7 of the last points (chosen to stabilise short-history series).
        var window = Math.Min(7, history.Count);
        if (window == 0)
            return new ForecastResult(new double[horizon], new double[horizon], new double[horizon], 0, ForecastMethod.Sma, 10);

        double sum = 0;
        for (int i = history.Count - window; i < history.Count; i++) sum += history[i];
        var mean = sum / window;

        // Residual standard deviation on in-sample 1-step-ahead SMA forecasts
        var residuals = new List<double>();
        for (int i = window; i < history.Count; i++)
        {
            double w = 0;
            for (int j = i - window; j < i; j++) w += history[j];
            residuals.Add(history[i] - w / window);
        }
        var stdDev = StandardDeviation(residuals);

        var point = new double[horizon];
        var low = new double[horizon];
        var high = new double[horizon];
        for (int h = 0; h < horizon; h++)
        {
            point[h] = mean;
            // Variance widens with √h for SMA forecasts (random walk-like uncertainty).
            var width = confidenceZ * stdDev * Math.Sqrt(h + 1);
            low[h] = Math.Max(0, mean - width);
            high[h] = mean + width;
        }

        // Confidence: lower with shorter histories. Capped between 25 and 65 for SMA.
        var confidence = Math.Clamp(40 + (history.Count - window) * 3.0, 25, 65);

        return new ForecastResult(point, low, high, stdDev, ForecastMethod.Sma, confidence);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Holt double-exponential smoothing (level + trend)
    //  L_t = α y_t + (1-α)(L_{t-1} + T_{t-1})
    //  T_t = β (L_t - L_{t-1}) + (1-β) T_{t-1}
    //  ŷ_{t+h} = L_t + h × T_t
    // ─────────────────────────────────────────────────────────────────────────

    public static ForecastResult ForecastHolt(IReadOnlyList<double> history, int horizon, double confidenceZ = DefaultConfidenceZ)
    {
        ValidateHistory(history);
        if (horizon < 1) throw new ArgumentException("Horizon must be ≥ 1", nameof(horizon));
        if (history.Count < 3) return ForecastSma(history, horizon, confidenceZ);

        // Initial level / trend: simple linear regression on the first up-to-12 points.
        var initWindow = Math.Min(12, history.Count);
        var (initLevel, initTrend) = LinearInit(history, initWindow);

        // Grid search α, β ∈ {0.1 .. 0.9}, step 0.1 (81 combos).
        double bestSse = double.PositiveInfinity;
        double bestAlpha = 0.3, bestBeta = 0.1;
        double bestStdDev = 0;

        for (int ai = 1; ai <= 9; ai++)
        for (int bi = 1; bi <= 9; bi++)
        {
            var alpha = ai / 10.0;
            var beta = bi / 10.0;
            var (sse, stdDev) = HoltInSample(history, alpha, beta, initLevel, initTrend);
            if (sse < bestSse)
            {
                bestSse = sse;
                bestAlpha = alpha;
                bestBeta = beta;
                bestStdDev = stdDev;
            }
        }

        // Replay with best α/β to get final L, T.
        double level = initLevel, trend = initTrend;
        for (int t = 0; t < history.Count; t++)
        {
            var prevLevel = level;
            level = bestAlpha * history[t] + (1 - bestAlpha) * (prevLevel + trend);
            trend = bestBeta * (level - prevLevel) + (1 - bestBeta) * trend;
        }

        var point = new double[horizon];
        var low = new double[horizon];
        var high = new double[horizon];
        for (int h = 0; h < horizon; h++)
        {
            var p = level + (h + 1) * trend;
            point[h] = Math.Max(0, p); // sales cannot be negative
            var width = confidenceZ * bestStdDev * Math.Sqrt(h + 1);
            low[h] = Math.Max(0, point[h] - width);
            high[h] = point[h] + width;
        }

        // Confidence: scaled by ratio (1 - normalised RMSE).
        var meanHistory = Mean(history);
        var rmse = Math.Sqrt(bestSse / Math.Max(1, history.Count - 1));
        var conf = meanHistory > 0
            ? Math.Clamp(85 - 100 * rmse / Math.Max(1e-9, meanHistory), 30, 90)
            : 50;

        return new ForecastResult(point, low, high, bestStdDev, ForecastMethod.Holt, conf);
    }

    private static (double sse, double stdDev) HoltInSample(IReadOnlyList<double> history, double alpha, double beta, double initLevel, double initTrend)
    {
        double level = initLevel, trend = initTrend;
        double sse = 0;
        var residuals = new List<double>(history.Count);
        for (int t = 0; t < history.Count; t++)
        {
            // 1-step-ahead in-sample forecast = previous (level + trend)
            var f = level + trend;
            var residual = history[t] - f;
            residuals.Add(residual);
            sse += residual * residual;

            var prevLevel = level;
            level = alpha * history[t] + (1 - alpha) * (prevLevel + trend);
            trend = beta * (level - prevLevel) + (1 - beta) * trend;
        }
        return (sse, StandardDeviation(residuals));
    }

    private static (double level, double trend) LinearInit(IReadOnlyList<double> history, int n)
    {
        // Ordinary least squares slope and intercept on x=0..n-1, y=history[0..n-1]
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += history[i];
            sumXY += i * history[i];
            sumXX += i * i;
        }
        var meanX = sumX / n;
        var meanY = sumY / n;
        var denom = sumXX - n * meanX * meanX;
        var slope = denom == 0 ? 0 : (sumXY - n * meanX * meanY) / denom;
        var intercept = meanY - slope * meanX;
        return (intercept, slope);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Holt-Winters multiplicative (level + trend + seasonality)
    //  L_t = α (y_t / S_{t-m}) + (1-α)(L_{t-1} + T_{t-1})
    //  T_t = β (L_t - L_{t-1}) + (1-β) T_{t-1}
    //  S_t = γ (y_t / L_t) + (1-γ) S_{t-m}
    //  ŷ_{t+h} = (L_t + h T_t) × S_{t-m+h_mod_m}
    // ─────────────────────────────────────────────────────────────────────────

    public static ForecastResult ForecastHoltWintersMultiplicative(
        IReadOnlyList<double> history, int horizon, int seasonalPeriod, double confidenceZ = DefaultConfidenceZ)
    {
        ValidateHistory(history);
        if (horizon < 1) throw new ArgumentException("Horizon must be ≥ 1", nameof(horizon));
        if (seasonalPeriod < 2) throw new ArgumentException("SeasonalPeriod must be ≥ 2", nameof(seasonalPeriod));
        if (history.Count < 2 * seasonalPeriod) return ForecastHolt(history, horizon, confidenceZ);

        // Replace zeros with a small epsilon to avoid division by zero in multiplicative model.
        var safe = new double[history.Count];
        var maxValue = history.Max();
        var eps = Math.Max(1e-6, maxValue * 1e-6);
        for (int i = 0; i < history.Count; i++) safe[i] = history[i] <= 0 ? eps : history[i];

        // Initial seasonal indices: average ratio (y_i / level_year) across years.
        var initialIndices = InitialSeasonalIndices(safe, seasonalPeriod);

        // Initial level/trend: linear regression on the deseasonalised first `2*seasonalPeriod` points.
        var deseasonalised = new double[2 * seasonalPeriod];
        for (int i = 0; i < 2 * seasonalPeriod; i++) deseasonalised[i] = safe[i] / initialIndices[i % seasonalPeriod];
        var (initLevel, initTrend) = LinearInit(deseasonalised, 2 * seasonalPeriod);

        // Grid search α, β, γ ∈ {0.1, 0.3, 0.5, 0.7, 0.9}.
        double bestSse = double.PositiveInfinity;
        double bestAlpha = 0.3, bestBeta = 0.1, bestGamma = 0.1;
        double bestStdDev = 0;

        var alphas = new[] { 0.1, 0.3, 0.5, 0.7, 0.9 };
        foreach (var alpha in alphas)
        foreach (var beta in alphas)
        foreach (var gamma in alphas)
        {
            var (sse, stdDev) = HoltWintersInSample(safe, alpha, beta, gamma, seasonalPeriod, initLevel, initTrend, initialIndices);
            if (sse < bestSse)
            {
                bestSse = sse;
                bestAlpha = alpha;
                bestBeta = beta;
                bestGamma = gamma;
                bestStdDev = stdDev;
            }
        }

        // Replay with the best parameters to produce final L, T, S.
        double level = initLevel, trend = initTrend;
        var indices = (double[])initialIndices.Clone();
        for (int t = 0; t < safe.Length; t++)
        {
            var seasonal = indices[t % seasonalPeriod];
            var prevLevel = level;
            level = bestAlpha * (safe[t] / seasonal) + (1 - bestAlpha) * (prevLevel + trend);
            trend = bestBeta * (level - prevLevel) + (1 - bestBeta) * trend;
            indices[t % seasonalPeriod] = bestGamma * (safe[t] / level) + (1 - bestGamma) * seasonal;
        }

        var point = new double[horizon];
        var low = new double[horizon];
        var high = new double[horizon];
        var lastIdx = safe.Length;
        for (int h = 0; h < horizon; h++)
        {
            var seasonalNext = indices[(lastIdx + h) % seasonalPeriod];
            var p = (level + (h + 1) * trend) * seasonalNext;
            point[h] = Math.Max(0, p);
            var width = confidenceZ * bestStdDev * Math.Sqrt(h + 1);
            low[h] = Math.Max(0, point[h] - width);
            high[h] = point[h] + width;
        }

        var meanHistory = Mean(history);
        var rmse = Math.Sqrt(bestSse / Math.Max(1, history.Count - 1));
        var conf = meanHistory > 0
            ? Math.Clamp(90 - 100 * rmse / Math.Max(1e-9, meanHistory), 35, 95)
            : 60;

        return new ForecastResult(point, low, high, bestStdDev, ForecastMethod.HoltWinters, conf);
    }

    private static double[] InitialSeasonalIndices(double[] history, int seasonalPeriod)
    {
        var fullYears = history.Length / seasonalPeriod;
        if (fullYears < 1) throw new InvalidOperationException("Not enough history for seasonal init");

        // 1) Yearly average for each complete year
        var yearlyAvg = new double[fullYears];
        for (int y = 0; y < fullYears; y++)
        {
            double sum = 0;
            for (int p = 0; p < seasonalPeriod; p++) sum += history[y * seasonalPeriod + p];
            yearlyAvg[y] = sum / seasonalPeriod;
        }

        // 2) Average ratio per period across all years
        var indices = new double[seasonalPeriod];
        for (int p = 0; p < seasonalPeriod; p++)
        {
            double sum = 0;
            for (int y = 0; y < fullYears; y++)
            {
                var avg = yearlyAvg[y];
                sum += avg <= 0 ? 1.0 : history[y * seasonalPeriod + p] / avg;
            }
            indices[p] = sum / fullYears;
        }

        // 3) Renormalise so that mean(indices) = 1 (multiplicative decomposition convention)
        var meanIndex = indices.Average();
        if (meanIndex <= 0) for (int p = 0; p < seasonalPeriod; p++) indices[p] = 1.0;
        else for (int p = 0; p < seasonalPeriod; p++) indices[p] = indices[p] / meanIndex;

        return indices;
    }

    private static (double sse, double stdDev) HoltWintersInSample(
        double[] history, double alpha, double beta, double gamma, int m,
        double initLevel, double initTrend, double[] initialIndices)
    {
        double level = initLevel, trend = initTrend;
        var indices = (double[])initialIndices.Clone();

        double sse = 0;
        var residuals = new List<double>(history.Length);
        for (int t = 0; t < history.Length; t++)
        {
            var seasonal = indices[t % m];
            var f = (level + trend) * seasonal;
            var residual = history[t] - f;
            residuals.Add(residual);
            sse += residual * residual;

            var prevLevel = level;
            level = alpha * (history[t] / seasonal) + (1 - alpha) * (prevLevel + trend);
            trend = beta * (level - prevLevel) + (1 - beta) * trend;
            indices[t % m] = gamma * (history[t] / level) + (1 - gamma) * seasonal;
        }
        return (sse, StandardDeviation(residuals));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Population standard deviation (n divisor). 0 for &lt; 2 samples or constant input.</summary>
    public static double StandardDeviation(IReadOnlyList<double> samples)
    {
        if (samples.Count < 2) return 0;
        double mean = 0;
        for (int i = 0; i < samples.Count; i++) mean += samples[i];
        mean /= samples.Count;
        double sumSq = 0;
        for (int i = 0; i < samples.Count; i++) sumSq += (samples[i] - mean) * (samples[i] - mean);
        return Math.Sqrt(sumSq / samples.Count);
    }

    public static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        double sum = 0;
        for (int i = 0; i < values.Count; i++) sum += values[i];
        return sum / values.Count;
    }

    /// <summary>Coefficient of variation = σ / μ. Returns 0 when mean ≤ 0 (no rotation, undefined).</summary>
    public static double CoefficientOfVariation(IReadOnlyList<double> values)
    {
        var mean = Mean(values);
        if (mean <= 0) return 0;
        return StandardDeviation(values) / mean;
    }

    private static void ValidateHistory(IReadOnlyList<double> history)
    {
        if (history is null) throw new ArgumentNullException(nameof(history));
        for (int i = 0; i < history.Count; i++)
        {
            if (double.IsNaN(history[i]) || double.IsInfinity(history[i]))
                throw new ArgumentException($"History contains NaN/Infinity at index {i}", nameof(history));
            if (history[i] < 0)
                throw new ArgumentException($"History contains negative value {history[i]} at index {i}; sales cannot be negative", nameof(history));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Stock replenishment math (independent of forecasting method)
    // ─────────────────────────────────────────────────────────────────────────

    public readonly record struct ReplenishmentMath(decimal Rop, decimal SafetyStock, decimal RecommendedQty);

    /// <summary>
    /// Compute the reorder point, safety stock and recommended quantity for one product/warehouse.
    /// Inputs are expected as decimal to align with stock units; calculation uses double internally.
    /// </summary>
    /// <param name="dailyDemand">Forecasted units consumed per day.</param>
    /// <param name="demandStdDev">Standard deviation of daily demand (estimated from history).</param>
    /// <param name="leadTimeDays">Supplier lead time in days (≥ 0).</param>
    /// <param name="serviceLevelZ">Z-score for the service level (1.65 = 95%, 1.96 = 97.5%).</param>
    /// <param name="quantityOnHand">Current stock-on-hand.</param>
    /// <param name="maximumStock">Configured target / max stock for the product. 0 = unbounded.</param>
    public static ReplenishmentMath ComputeReplenishment(
        decimal dailyDemand,
        decimal demandStdDev,
        int leadTimeDays,
        decimal serviceLevelZ,
        decimal quantityOnHand,
        decimal maximumStock)
    {
        if (dailyDemand < 0) throw new ArgumentException("DailyDemand must be ≥ 0", nameof(dailyDemand));
        if (demandStdDev < 0) throw new ArgumentException("DemandStdDev must be ≥ 0", nameof(demandStdDev));
        if (leadTimeDays < 0) throw new ArgumentException("LeadTimeDays must be ≥ 0", nameof(leadTimeDays));
        if (serviceLevelZ < 0) throw new ArgumentException("ServiceLevelZ must be ≥ 0", nameof(serviceLevelZ));
        if (quantityOnHand < 0) throw new ArgumentException("QuantityOnHand must be ≥ 0", nameof(quantityOnHand));

        var ss = serviceLevelZ * demandStdDev * (decimal)Math.Sqrt(Math.Max(0, leadTimeDays));
        var rop = dailyDemand * leadTimeDays + ss;

        // Recommended qty: refill to max stock if defined, otherwise 2× lead-time demand.
        var qty = maximumStock > 0
            ? Math.Max(0, maximumStock - quantityOnHand)
            : Math.Max(0, 2 * dailyDemand * leadTimeDays);

        // Floor: at least the safety stock + lead-time demand minus current on-hand, but never < 0.
        var minQty = Math.Max(0, rop - quantityOnHand);
        if (qty < minQty) qty = minQty;

        return new ReplenishmentMath(
            Math.Round(rop, 3),
            Math.Round(ss, 3),
            Math.Round(qty, 3));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ABC / XYZ classification primitives
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute ABC class for a sequence of products sorted by revenue desc.
    /// Returns the cumulative revenue percent at each position, AND each product's ABC class.
    /// </summary>
    public static (AbcClass[] Classes, double[] CumulativePercent) ComputeAbc(
        IReadOnlyList<double> revenuesDescending,
        double aThreshold = 80.0,
        double bThreshold = 95.0)
    {
        if (revenuesDescending is null) throw new ArgumentNullException(nameof(revenuesDescending));
        if (aThreshold <= 0 || aThreshold >= 100) throw new ArgumentException("aThreshold must be in (0..100)", nameof(aThreshold));
        if (bThreshold <= aThreshold || bThreshold >= 100) throw new ArgumentException("bThreshold must be in (aThreshold..100)", nameof(bThreshold));

        var n = revenuesDescending.Count;
        var classes = new AbcClass[n];
        var cumulative = new double[n];
        if (n == 0) return (classes, cumulative);

        double total = 0;
        for (int i = 0; i < n; i++) total += revenuesDescending[i];
        if (total <= 0)
        {
            for (int i = 0; i < n; i++) classes[i] = AbcClass.Unclassified;
            return (classes, cumulative);
        }

        double running = 0;
        for (int i = 0; i < n; i++)
        {
            running += revenuesDescending[i];
            cumulative[i] = 100 * running / total;
            if (revenuesDescending[i] <= 0) classes[i] = AbcClass.Unclassified;
            else if (cumulative[i] <= aThreshold) classes[i] = AbcClass.A;
            else if (cumulative[i] <= bThreshold) classes[i] = AbcClass.B;
            else classes[i] = AbcClass.C;
        }
        return (classes, cumulative);
    }

    public static XyzClass ComputeXyz(IReadOnlyList<double> monthlyDemand, double xThreshold = 0.5, double yThreshold = 1.0)
    {
        if (monthlyDemand is null || monthlyDemand.Count == 0) return XyzClass.Unclassified;
        var totalDemand = monthlyDemand.Sum();
        if (totalDemand <= 0) return XyzClass.Unclassified;

        var cv = CoefficientOfVariation(monthlyDemand);
        if (cv < xThreshold) return XyzClass.X;
        if (cv < yThreshold) return XyzClass.Y;
        return XyzClass.Z;
    }
}
