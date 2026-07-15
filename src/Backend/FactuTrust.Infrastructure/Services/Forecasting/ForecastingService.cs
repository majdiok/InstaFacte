using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Forecasting.Dtos;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Forecasting.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting;

/// <summary>
/// Deterministic implementation of IForecastingService.
/// All numeric outputs come from StatisticalForecasting; the LLM never invents these values.
/// Cached per tenant with a TTL of <see cref="ForecastingOptions.CacheTtlMinutes"/>.
/// </summary>
public sealed class ForecastingService : IForecastingService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ITunisianCalendarService _calendar;
    private readonly IMemoryCache _cache;
    private readonly ForecastingOptions _options;
    private readonly ILogger<ForecastingService> _logger;

    public ForecastingService(
        ITenantDbContextFactory contextFactory,
        ITenantContext tenantContext,
        ITunisianCalendarService calendar,
        IMemoryCache cache,
        IOptions<ForecastingOptions> options,
        ILogger<ForecastingService> logger)
    {
        _contextFactory = contextFactory;
        _tenantContext = tenantContext;
        _calendar = calendar;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    private string Tenant => _tenantContext.TenantId?.ToString() ?? "no-tenant";

    // ────────────────────── Revenue forecast ──────────────────────────────

    public async Task<SalesForecastDto> ForecastRevenueAsync(
        ForecastScopeType scopeType,
        Guid? scopeId,
        ForecastHorizon horizon,
        DateTime? customPeriodStart,
        DateTime? customPeriodEnd,
        CancellationToken ct = default)
    {
        EnsureFeatureEnabled();

        var cacheKey = $"forecast:{Tenant}:revenue:{scopeType}:{scopeId}:{horizon}:{customPeriodStart:yyyy-MM-dd}:{customPeriodEnd:yyyy-MM-dd}";
        if (_cache.TryGetValue<SalesForecastDto>(cacheKey, out var cached) && cached is not null)
            return cached;

        await using var ctx = _contextFactory.CreateContext();

        // 1) Build the historical monthly revenue series (last 36 months) for the scope.
        var (monthly, scopeLabel, currency) = await BuildMonthlyRevenueSeriesAsync(ctx, scopeType, scopeId, ct);

        // 2) Resolve the horizon → number of months to forecast (custom range expressed in days converted to whole months).
        var (periodStart, periodEnd) = ResolveHorizonRange(horizon, customPeriodStart, customPeriodEnd);
        var monthsAhead = (int)Math.Ceiling((periodEnd - periodStart).TotalDays / 30.0);
        if (monthsAhead < 1) monthsAhead = 1;

        // 3) Run the deterministic forecast.
        var historyValues = monthly.Select(m => (double)m.Revenue).ToArray();
        StatisticalForecasting.ForecastResult result;
        if (historyValues.Length < _options.MinHistoryPointsForHolt)
            result = ApplyCalendarHeuristic(historyValues, monthsAhead);
        else
            result = StatisticalForecasting.Forecast(
                historyValues,
                monthsAhead,
                seasonalPeriod: 12,
                minHistoryForHolt: _options.MinHistoryPointsForHolt,
                minHistoryForHoltWinters: _options.MinHistoryMonthsForHoltWinters,
                confidenceZ: _options.ConfidenceZ);

        // 4) Apply a calendar-based multiplicative adjustment over the forecast window.
        var adjustedPoint = ApplyCalendarUplift(result.Point, periodStart, scopeType, scopeId);

        // 5) Build the DTO.
        decimal expected = (decimal)adjustedPoint.Sum();
        decimal low = (decimal)result.Low.Sum();
        decimal high = (decimal)result.High.Sum();
        // Guard against the calendar adjustment lifting expected above high.
        if (expected > high) high = expected * 1.1m;
        if (expected < low) low = expected * 0.9m;

        var inputs = new
        {
            historyMonths = historyValues.Length,
            method = result.MethodUsed.ToString(),
            confidenceZ = _options.ConfidenceZ,
            seasonalAdjustment = !adjustedPoint.SequenceEqual(result.Point)
        };

        var breakdown = new List<ForecastBreakdownPointDto>();
        var stepStart = periodStart;
        for (int i = 0; i < adjustedPoint.Length; i++)
        {
            var stepEnd = stepStart.AddMonths(1).AddDays(-1);
            if (stepEnd > periodEnd) stepEnd = periodEnd;
            breakdown.Add(new ForecastBreakdownPointDto(
                stepStart,
                stepEnd,
                (decimal)adjustedPoint[i],
                (decimal)result.Low[i],
                (decimal)result.High[i]));
            stepStart = stepEnd.AddDays(1);
        }

        var entity = SalesForecast.Create(
            scopeType,
            scopeId,
            horizon,
            periodStart,
            periodEnd,
            Money.Create(expected, currency),
            Money.Create(Math.Max(0, low), currency),
            Money.Create(high, currency),
            (decimal)result.ConfidencePercent,
            result.MethodUsed,
            JsonSerializer.Serialize(inputs),
            null);
        ctx.SalesForecasts.Add(entity);
        await ctx.SaveChangesAsync(ct);

        var dto = new SalesForecastDto(
            entity.Id, scopeType, scopeId, scopeLabel, horizon,
            entity.GeneratedAt, periodStart, periodEnd,
            expected, Math.Max(0, low), high, currency,
            entity.ConfidencePercent, result.MethodUsed,
            historyValues.Length < _options.MinHistoryPointsForHolt
                ? "Historique limité — précision réduite. Continuer à enregistrer des ventes pour améliorer."
                : null,
            breakdown);

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(_options.CacheTtlMinutes));
        return dto;
    }

    // ────────────────────── Product demand forecast ───────────────────────

    public async Task<ProductDemandForecastDto> ForecastProductDemandAsync(
        Guid productId,
        ForecastHorizon horizon,
        CancellationToken ct = default)
    {
        EnsureFeatureEnabled();
        var cacheKey = $"forecast:{Tenant}:demand:{productId}:{horizon}";
        if (_cache.TryGetValue<ProductDemandForecastDto>(cacheKey, out var cached) && cached is not null)
            return cached;

        await using var ctx = _contextFactory.CreateContext();
        var product = await ctx.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        // History: monthly quantities sold over the last 24 months.
        var since = DateTime.UtcNow.Date.AddMonths(-24);
        var history = await ctx.InvoiceLines
            .Where(il => il.ProductId == productId
                         && il.Invoice.IssueDate >= since
                         && il.Invoice.Status != InvoiceStatus.Draft
                         && il.Invoice.Status != InvoiceStatus.Cancelled)
            .GroupBy(il => new { il.Invoice.IssueDate.Year, il.Invoice.IssueDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Qty = g.Sum(l => l.Quantity) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        // Pad missing months with 0.
        var historyByMonth = history.ToDictionary(h => (h.Year, h.Month), h => (double)h.Qty);
        var serialized = new List<double>();
        var cursor = new DateTime(since.Year, since.Month, 1);
        var todayAnchor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        while (cursor < todayAnchor)
        {
            serialized.Add(historyByMonth.TryGetValue((cursor.Year, cursor.Month), out var v) ? v : 0);
            cursor = cursor.AddMonths(1);
        }

        var (periodStart, periodEnd) = ResolveHorizonRange(horizon, null, null);
        var monthsAhead = Math.Max(1, (int)Math.Ceiling((periodEnd - periodStart).TotalDays / 30.0));
        var fc = serialized.Count < _options.MinHistoryPointsForHolt
            ? StatisticalForecasting.ForecastSma(serialized, monthsAhead, _options.ConfidenceZ)
            : StatisticalForecasting.Forecast(serialized, monthsAhead, 12,
                _options.MinHistoryPointsForHolt, _options.MinHistoryMonthsForHoltWinters, _options.ConfidenceZ);

        var expectedQty = (decimal)fc.Point.Sum();
        var lowQty = (decimal)fc.Low.Sum();
        var highQty = (decimal)fc.High.Sum();

        // Current stock summed across all warehouses.
        var stockOnHand = await ctx.StockItems
            .Where(s => s.ProductId == productId)
            .SumAsync(s => (decimal?)s.QuantityOnHand, ct) ?? 0m;

        var dailyDemand = expectedQty / Math.Max(1, (decimal)(periodEnd - periodStart).TotalDays);
        var daysOfStock = dailyDemand > 0 ? stockOnHand / dailyDemand : (decimal?)null;

        // Suggested replenishment: cover the forecasted demand minus current on-hand, never negative.
        var suggestedRefill = Math.Max(0, expectedQty - stockOnHand);

        var dto = new ProductDemandForecastDto(
            productId, product.Code, product.Name, horizon, periodStart, periodEnd,
            expectedQty, Math.Max(0, lowQty), highQty,
            stockOnHand, suggestedRefill, daysOfStock,
            (decimal)fc.ConfidencePercent, fc.MethodUsed);

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(_options.CacheTtlMinutes));
        return dto;
    }

    // ────────────────────── Seasonal impact ───────────────────────────────

    public async Task<SeasonalImpactDto> AnalyzeSeasonalImpactAsync(
        Guid? productId, Guid? categoryId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        EnsureFeatureEnabled();
        if (productId is null && categoryId is null)
            throw new ArgumentException("Provide either productId or categoryId");

        await using var ctx = _contextFactory.CreateContext();
        string? productName = null;
        string? categoryName = null;
        if (productId is not null)
        {
            var p = await ctx.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == productId, ct);
            productName = p?.Name;
            categoryName = p?.Category?.Name;
        }
        else
        {
            var c = await ctx.ProductCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
            categoryName = c?.Name;
        }

        var events = _calendar.GetEvents(periodStart, periodEnd);
        var dto = new SeasonalImpactDto(
            productId, productName, categoryId, categoryName,
            periodStart, periodEnd,
            events.Select(e => new CalendarEventDto(
                e.Code, e.DisplayName, e.StartDate, e.EndDate, e.IsHoliday, e.IsCommercialWindow, e.Category)).ToList(),
            ComputeWeightedSeasonalFactor(categoryName, periodStart, periodEnd),
            0m, 0m, Money.DefaultCurrency,
            BuildSeasonalNotes(events, categoryName));

        // Build baseline using the simple revenue forecast for the scope, then multiply by the weighted factor.
        if (productId is not null)
        {
            var demand = await ForecastProductDemandAsync(productId.Value, ForecastHorizon.Month, ct);
            var baseline = demand.ExpectedQty;
            var adjusted = baseline * (decimal)dto.WeightedSeasonalFactor;
            dto = dto with { BaselineExpected = baseline, AdjustedExpected = adjusted };
        }
        else if (categoryId is not null)
        {
            var fc = await ForecastRevenueAsync(ForecastScopeType.Category, categoryId, ForecastHorizon.Month, periodStart, periodEnd, ct);
            dto = dto with
            {
                BaselineExpected = fc.Expected,
                AdjustedExpected = fc.Expected * (decimal)dto.WeightedSeasonalFactor,
                Currency = fc.Currency
            };
        }

        return dto;
    }

    // ────────────────────── Promotion simulation ──────────────────────────

    public async Task<PromotionSimulationResultDto> SimulatePromotionImpactAsync(
        Guid productId, decimal discountPercent, int durationDays, CancellationToken ct = default)
    {
        EnsureFeatureEnabled();
        if (discountPercent < 0 || discountPercent > 90)
            throw new ArgumentException("DiscountPercent must be in [0..90]", nameof(discountPercent));
        if (durationDays < 1 || durationDays > 90)
            throw new ArgumentException("DurationDays must be in [1..90]", nameof(durationDays));

        await using var ctx = _contextFactory.CreateContext();
        var product = await ctx.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var demand = await ForecastProductDemandAsync(productId, ForecastHorizon.Month, ct);

        // Constant elasticity heuristic ε = -1.5 (typical retail). uplift% = (1 - (1 - d)^|ε|) - 1 if d small.
        const decimal elasticity = -1.5m;
        var priceFactor = 1 - (discountPercent / 100m);
        var quantityFactor = (decimal)Math.Pow((double)priceFactor, (double)elasticity);
        var uplift = quantityFactor - 1m;

        // Pro-rata for duration vs the 30-day demand horizon.
        var durationFactor = durationDays / 30m;

        var unitPrice = product.UnitPrice.Amount;
        var unitCost = product.PurchasePrice?.Amount ?? unitPrice * 0.6m; // fallback assumption when no purchase cost

        var baseline = unitPrice * demand.ExpectedQty * durationFactor;
        var withoutPromo = baseline;
        var qtyWith = demand.ExpectedQty * durationFactor * quantityFactor;
        var withPromo = unitPrice * priceFactor * qtyWith;

        var marginBaseline = (unitPrice - unitCost) * demand.ExpectedQty * durationFactor;
        var marginWith = (unitPrice * priceFactor - unitCost) * qtyWith;
        var marginImpactPercent = marginBaseline > 0
            ? (marginWith - marginBaseline) / marginBaseline * 100m
            : 0m;

        return new PromotionSimulationResultDto(
            productId, product.Code, product.Name,
            discountPercent, durationDays,
            Math.Round(baseline, 3),
            Math.Round(withoutPromo, 3),
            Math.Round(withPromo, 3),
            Math.Round(uplift * 100m, 2),
            Math.Round(marginImpactPercent, 2),
            elasticity,
            "Élasticité approximée -1,5 (commerce de détail). Affiner par catégorie en V2.");
    }

    // ────────────────────── Helpers ───────────────────────────────────────

    private void EnsureFeatureEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("AI Forecasting module is disabled (Features:Forecasting:Enabled=false).");
    }

    private static (DateTime start, DateTime end) ResolveHorizonRange(ForecastHorizon h, DateTime? cs, DateTime? ce)
    {
        if (h == ForecastHorizon.Custom && cs is not null && ce is not null)
            return (cs.Value.Date, ce.Value.Date);

        var today = DateTime.UtcNow.Date;
        return h switch
        {
            ForecastHorizon.Week => (today.AddDays(1), today.AddDays(7)),
            ForecastHorizon.Month => (today.AddDays(1), today.AddDays(30)),
            ForecastHorizon.Quarter => (today.AddDays(1), today.AddDays(90)),
            _ => (today.AddDays(1), today.AddDays(30))
        };
    }

    private async Task<(IReadOnlyList<MonthlyRevenue> Series, string ScopeLabel, string Currency)> BuildMonthlyRevenueSeriesAsync(
        TenantDbContext ctx, ForecastScopeType scope, Guid? scopeId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date.AddMonths(-36);
        var query = ctx.InvoiceLines
            .Where(il => il.Invoice.IssueDate >= since
                         && il.Invoice.Status != InvoiceStatus.Draft
                         && il.Invoice.Status != InvoiceStatus.Cancelled);

        string scopeLabel;
        switch (scope)
        {
            case ForecastScopeType.Global:
                scopeLabel = "Global";
                break;
            case ForecastScopeType.Product:
                if (scopeId is null) throw new InvalidOperationException("scopeId est obligatoire pour le périmètre Produit.");
                query = query.Where(il => il.ProductId == scopeId);
                scopeLabel = await ctx.Products.Where(p => p.Id == scopeId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "Produit inconnu";
                break;
            case ForecastScopeType.Category:
                if (scopeId is null) throw new InvalidOperationException("scopeId est obligatoire pour le périmètre Catégorie.");
                query = query.Where(il => il.Product != null && il.Product.CategoryId == scopeId);
                scopeLabel = await ctx.ProductCategories.Where(c => c.Id == scopeId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Catégorie inconnue";
                break;
            case ForecastScopeType.Warehouse:
                if (scopeId is null) throw new InvalidOperationException("scopeId est obligatoire pour le périmètre Entrepôt.");
                query = query.Where(il => il.Invoice.WarehouseId == scopeId);
                scopeLabel = await ctx.Warehouses.Where(w => w.Id == scopeId).Select(w => w.Name).FirstOrDefaultAsync(ct) ?? "Entrepôt inconnu";
                break;
            case ForecastScopeType.Client:
                if (scopeId is null) throw new InvalidOperationException("scopeId est obligatoire pour le périmètre Client.");
                query = query.Where(il => il.Invoice.ClientId == scopeId);
                scopeLabel = await ctx.Clients.Where(c => c.Id == scopeId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Client inconnu";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope));
        }

        var raw = await query
            .GroupBy(il => new { il.Invoice.IssueDate.Year, il.Invoice.IssueDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(l => l.SubTotal.Amount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        // Pad missing months with zero so seasonality detection is not biased.
        var dict = raw.ToDictionary(r => (r.Year, r.Month), r => r.Revenue);
        var series = new List<MonthlyRevenue>();
        var cursor = new DateTime(since.Year, since.Month, 1);
        var anchor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        while (cursor < anchor)
        {
            series.Add(new MonthlyRevenue(cursor.Year, cursor.Month,
                dict.TryGetValue((cursor.Year, cursor.Month), out var rev) ? rev : 0m));
            cursor = cursor.AddMonths(1);
        }

        return (series, scopeLabel, Money.DefaultCurrency);
    }

    private double[] ApplyCalendarUplift(double[] baselinePoints, DateTime periodStart, ForecastScopeType scope, Guid? scopeId)
    {
        // V1: pass-through. Per-month seasonal multipliers require resolving a category name from the
        // scope (a DB lookup) and are applied in AnalyzeSeasonalImpactAsync where the caller has already
        // provided the category. Keeping ApplyCalendarUplift inert avoids fabricating numbers; the LLM
        // and the dashboards explicitly call AnalyzeSeasonalImpactAsync when a seasonal adjustment is wanted.
        return baselinePoints;
    }

    private double ComputeWeightedSeasonalFactor(string? categoryName, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return 1.0;
        double sum = 0;
        int days = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            sum += _calendar.GetSeasonalFactor(categoryName, d);
            days++;
        }
        return days == 0 ? 1.0 : sum / days;
    }

    private static string BuildSeasonalNotes(IReadOnlyList<TunisianEvent> events, string? category)
    {
        if (events.Count == 0) return "Aucun événement notable sur la période.";
        var top = events.Take(3).Select(e => $"{e.DisplayName} ({e.StartDate:dd/MM} – {e.EndDate:dd/MM})");
        var head = $"Événements en cours/à venir : {string.Join(", ", top)}.";
        return string.IsNullOrWhiteSpace(category) ? head : head + $" Catégorie « {category} » ajustée selon la table saisonnière.";
    }

    private StatisticalForecasting.ForecastResult ApplyCalendarHeuristic(double[] history, int monthsAhead)
    {
        // Flat baseline (mean of history) — confidence is capped to 40 by SalesForecast.Create when
        // method == CalendarHeuristic, so we don't oversell short histories to the user.
        var baseline = history.Length > 0 ? StatisticalForecasting.Mean(history) : 0;
        var sma = new double[monthsAhead];
        for (int i = 0; i < monthsAhead; i++) sma[i] = baseline;

        var residualSd = StatisticalForecasting.StandardDeviation(history);
        var low = new double[monthsAhead];
        var high = new double[monthsAhead];
        for (int i = 0; i < monthsAhead; i++)
        {
            var w = _options.ConfidenceZ * residualSd * Math.Sqrt(i + 1);
            low[i] = Math.Max(0, sma[i] - w);
            high[i] = sma[i] + w;
        }
        return new StatisticalForecasting.ForecastResult(sma, low, high, residualSd, ForecastMethod.CalendarHeuristic, 30);
    }

    private sealed record MonthlyRevenue(int Year, int Month, decimal Revenue);
}
