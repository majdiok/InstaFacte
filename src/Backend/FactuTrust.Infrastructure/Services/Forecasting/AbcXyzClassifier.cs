using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Forecasting.Dtos;
using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.Forecasting.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting;

/// <summary>
/// Computes ABC (cumulative revenue) and XYZ (demand variability) classifications using the
/// last 12 months of sales. Persists one row per product into ProductAbcXyzClassifications,
/// keeping a history (older rows are not deleted; the latest is used by the matrix view).
/// </summary>
public sealed class AbcXyzClassifier : IAbcXyzClassifier
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ForecastingOptions _options;
    private readonly ILogger<AbcXyzClassifier> _logger;

    public AbcXyzClassifier(
        ITenantDbContextFactory contextFactory,
        IOptions<ForecastingOptions> options,
        ILogger<AbcXyzClassifier> logger)
    {
        _contextFactory = contextFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ClassifyAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return 0;

        await using var ctx = _contextFactory.CreateContext();
        var since = DateTime.UtcNow.Date.AddMonths(-12);

        // Aggregate revenue and per-month qty per product over the last 12 months.
        var rows = await ctx.InvoiceLines
            .Where(il => il.Invoice.IssueDate >= since
                         && il.Invoice.Status != InvoiceStatus.Draft
                         && il.Invoice.Status != InvoiceStatus.Cancelled
                         && il.ProductId.HasValue)
            .GroupBy(il => new { ProductId = il.ProductId!.Value, il.Invoice.IssueDate.Year, il.Invoice.IssueDate.Month })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                g.Key.Year,
                g.Key.Month,
                Qty = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.SubTotal.Amount)
            })
            .ToListAsync(ct);

        var byProduct = rows.GroupBy(r => r.ProductId).ToList();

        // ABC: sort products by total revenue desc, compute cumulative.
        var revenuesByProduct = byProduct
            .Select(g => new { ProductId = g.Key, Total = g.Sum(r => r.Revenue) })
            .OrderByDescending(x => x.Total)
            .ToList();
        var revenues = revenuesByProduct.Select(x => (double)x.Total).ToList();
        var (abcClasses, cumulative) = StatisticalForecasting.ComputeAbc(
            revenues, _options.AbcAClassThreshold, _options.AbcBClassThreshold);

        var newClassifications = new List<ProductAbcXyzClassification>();
        for (int i = 0; i < revenuesByProduct.Count; i++)
        {
            var productId = revenuesByProduct[i].ProductId;
            var monthsForProduct = byProduct.First(g => g.Key == productId)
                .OrderBy(r => r.Year).ThenBy(r => r.Month)
                .Select(r => (double)r.Qty)
                .ToList();

            // Pad to a full 12-month window for stable CV.
            var padded = new double[12];
            var anchor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var cursor = anchor.AddMonths(-12);
            var monthMap = byProduct.First(g => g.Key == productId)
                .ToDictionary(r => (r.Year, r.Month), r => (double)r.Qty);
            for (int m = 0; m < 12; m++)
            {
                padded[m] = monthMap.TryGetValue((cursor.Year, cursor.Month), out var v) ? v : 0;
                cursor = cursor.AddMonths(1);
            }

            var xyz = StatisticalForecasting.ComputeXyz(padded, _options.XyzXClassThreshold, _options.XyzYClassThreshold);
            var cv = StatisticalForecasting.CoefficientOfVariation(padded);
            var activeMonths = padded.Count(v => v > 0);

            newClassifications.Add(ProductAbcXyzClassification.Create(
                productId,
                abcClasses[i],
                xyz,
                (decimal)cumulative[i],
                (decimal)cv,
                revenuesByProduct[i].Total,
                activeMonths));
        }

        // Stock-managed products with NO sales in the window → Unclassified rows so the matrix is exhaustive.
        var classifiedIds = revenuesByProduct.Select(x => x.ProductId).ToHashSet();
        var unsoldProducts = await ctx.Products
            .Where(p => p.IsStockManaged && !classifiedIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        foreach (var pid in unsoldProducts)
        {
            newClassifications.Add(ProductAbcXyzClassification.Create(
                pid, AbcClass.Unclassified, XyzClass.Unclassified, 0m, 0m, 0m, 0));
        }

        // Persist as a new snapshot — keep history; queries always pick MAX(ComputedAt) per product.
        ctx.ProductAbcXyzClassifications.AddRange(newClassifications);
        await ctx.SaveChangesAsync(ct);

        _logger.LogInformation("ABC/XYZ classification: {Count} products classified.", newClassifications.Count);
        return newClassifications.Count;
    }

    public async Task<AbcXyzMatrixDto> GetMatrixAsync(
        Guid? warehouseId, AbcClass? abcFilter, XyzClass? xyzFilter, CancellationToken ct = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        // Latest classification per product.
        var latestPerProduct = await ctx.ProductAbcXyzClassifications
            .GroupBy(c => c.ProductId)
            .Select(g => g.OrderByDescending(c => c.ComputedAt).First())
            .ToListAsync(ct);

        if (abcFilter.HasValue) latestPerProduct = latestPerProduct.Where(c => c.AbcClass == abcFilter.Value).ToList();
        if (xyzFilter.HasValue) latestPerProduct = latestPerProduct.Where(c => c.XyzClass == xyzFilter.Value).ToList();

        // Optional warehouse filter — only keep products that have stock in the given warehouse.
        if (warehouseId.HasValue)
        {
            var inWarehouse = await ctx.StockItems
                .Where(s => s.WarehouseId == warehouseId.Value)
                .Select(s => s.ProductId)
                .ToListAsync(ct);
            var set = inWarehouse.ToHashSet();
            latestPerProduct = latestPerProduct.Where(c => set.Contains(c.ProductId)).ToList();
        }

        var productIds = latestPerProduct.Select(c => c.ProductId).ToList();
        var products = await ctx.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToDictionaryAsync(x => x.Id, ct);

        var totalRevenue = latestPerProduct.Sum(c => c.ReferenceRevenue);

        var productDtos = latestPerProduct
            .OrderByDescending(c => c.ReferenceRevenue)
            .Select(c => new ProductClassificationDto(
                c.ProductId,
                products.TryGetValue(c.ProductId, out var p) ? p.Code : "?",
                products.TryGetValue(c.ProductId, out var pn) ? pn.Name : "?",
                c.AbcClass, c.XyzClass, c.MatrixCode,
                c.CumulativeRevenuePercent, c.DemandCv, c.ReferenceRevenue, c.ActiveMonths, c.ComputedAt))
            .ToList();

        // Build the 9-cell matrix (A/B/C × X/Y/Z) — Unclassified is reported as a separate cell only if non-empty.
        var cells = new List<AbcXyzCellDto>();
        foreach (var abc in new[] { AbcClass.A, AbcClass.B, AbcClass.C })
        foreach (var xyz in new[] { XyzClass.X, XyzClass.Y, XyzClass.Z })
        {
            var subset = latestPerProduct.Where(c => c.AbcClass == abc && c.XyzClass == xyz).ToList();
            cells.Add(new AbcXyzCellDto(
                $"{abc}{xyz}",
                abc, xyz,
                subset.Count,
                totalRevenue > 0 ? Math.Round(subset.Sum(c => c.ReferenceRevenue) / totalRevenue * 100m, 2) : 0m));
        }
        var unclassifiedCount = latestPerProduct.Count(c => c.AbcClass == AbcClass.Unclassified || c.XyzClass == XyzClass.Unclassified);
        if (unclassifiedCount > 0)
            cells.Add(new AbcXyzCellDto("--", AbcClass.Unclassified, XyzClass.Unclassified, unclassifiedCount, 0m));

        return new AbcXyzMatrixDto(
            latestPerProduct.Count == 0 ? DateTime.UtcNow : latestPerProduct.Max(c => c.ComputedAt),
            latestPerProduct.Count, totalRevenue,
            cells, productDtos);
    }
}
