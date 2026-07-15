using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Reads rows from whitelisted existing first-party sources for the report builder. Each source has
/// a hand-written, read-only projection that exposes ONLY the catalog's allowed fields. The tenant
/// DbContext is the tenant's own database (DB-per-tenant), so no extra tenant filter is needed.
/// Deny-by-default: unknown source keys return null.
/// </summary>
public sealed class ExistingDataSourceProvider : IExistingDataSourceProvider
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ExistingDataSourceProvider(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> GetRowsAsync(
        Guid tenantId, string sourceKey, int max, CancellationToken cancellationToken = default)
    {
        if (!ExistingDataSourceCatalog.IsWhitelisted(sourceKey))
            return null;

        await using var ctx = _contextFactory.CreateContext();

        return sourceKey switch
        {
            "clients" => await ClientsAsync(ctx, max, cancellationToken),
            "products" => await ProductsAsync(ctx, max, cancellationToken),
            "invoices" => await InvoicesAsync(ctx, max, cancellationToken),
            _ => null
        };
    }

    public async Task<IReadOnlyList<SelectOptionDto>?> GetRelationOptionsAsync(
        Guid tenantId, string sourceKey, int max, CancellationToken cancellationToken = default)
    {
        if (!ExistingRelationSources.IsRelationTarget(sourceKey))
            return null;

        await using var ctx = _contextFactory.CreateContext();
        switch (sourceKey)
        {
            case "clients":
            {
                var rows = await ctx.Clients.AsNoTracking().OrderBy(c => c.Name).Take(max)
                    .Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);
                return rows.Select(r => new SelectOptionDto(r.Id.ToString(), r.Name)).ToList();
            }
            case "products":
            {
                var rows = await ctx.Products.AsNoTracking().OrderBy(p => p.Name).Take(max)
                    .Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken);
                return rows.Select(r => new SelectOptionDto(r.Id.ToString(), r.Name)).ToList();
            }
            default:
                return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>?> GetRelationRecordsByIdsAsync(
        Guid tenantId, string sourceKey, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (!ExistingRelationSources.IsRelationTarget(sourceKey))
            return null;

        var guids = ids
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();

        var result = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        if (guids.Count == 0) return result;

        await using var ctx = _contextFactory.CreateContext();
        switch (sourceKey)
        {
            case "clients":
            {
                var rows = await ctx.Clients.AsNoTracking().Where(c => guids.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name, c.Type, City = c.Address.City, c.IsActive, c.CreatedAt })
                    .ToListAsync(cancellationToken);
                foreach (var r in rows)
                    result[r.Id.ToString()] = new Dictionary<string, object?>
                    {
                        ["name"] = r.Name,
                        ["type"] = r.Type.ToString(),
                        ["city"] = r.City,
                        ["active"] = r.IsActive ? "Oui" : "Non",
                        ["createdAt"] = r.CreatedAt.ToString("yyyy-MM-dd")
                    };
                return result;
            }
            case "products":
            {
                var rows = await ctx.Products.AsNoTracking().Where(p => guids.Contains(p.Id))
                    .Select(p => new { p.Id, p.Code, p.Name, p.Type, Price = p.UnitPrice.Amount, p.IsActive, p.CreatedAt })
                    .ToListAsync(cancellationToken);
                foreach (var r in rows)
                    result[r.Id.ToString()] = new Dictionary<string, object?>
                    {
                        ["code"] = r.Code,
                        ["name"] = r.Name,
                        ["type"] = r.Type.ToString(),
                        ["unitPrice"] = r.Price,
                        ["active"] = r.IsActive ? "Oui" : "Non",
                        ["createdAt"] = r.CreatedAt.ToString("yyyy-MM-dd")
                    };
                return result;
            }
            default:
                return null;
        }
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ClientsAsync(
        Persistence.TenantDbContext ctx, int max, CancellationToken ct)
    {
        var rows = await ctx.Clients.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(max)
            .Select(c => new { c.Name, c.Type, City = c.Address.City, c.IsActive, c.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["name"] = r.Name,
            ["type"] = r.Type.ToString(),
            ["city"] = r.City,
            ["active"] = r.IsActive ? "Oui" : "Non",
            ["createdAt"] = r.CreatedAt.ToString("yyyy-MM-dd")
        }).ToList();
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ProductsAsync(
        Persistence.TenantDbContext ctx, int max, CancellationToken ct)
    {
        var rows = await ctx.Products.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(max)
            .Select(p => new { p.Code, p.Name, p.Type, Price = p.UnitPrice.Amount, p.IsActive, p.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["code"] = r.Code,
            ["name"] = r.Name,
            ["type"] = r.Type.ToString(),
            ["unitPrice"] = r.Price,
            ["active"] = r.IsActive ? "Oui" : "Non",
            ["createdAt"] = r.CreatedAt.ToString("yyyy-MM-dd")
        }).ToList();
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> InvoicesAsync(
        Persistence.TenantDbContext ctx, int max, CancellationToken ct)
    {
        var rows = await ctx.Invoices.AsNoTracking()
            .OrderByDescending(i => i.IssueDate)
            .Take(max)
            .Select(i => new
            {
                i.IssueDate,
                i.Status,
                ClientName = i.Client.Name,
                Total = i.TotalAmount.Amount,
                Vat = i.TotalVat.Amount
            })
            .ToListAsync(ct);

        return rows.Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["issueDate"] = r.IssueDate.ToString("yyyy-MM-dd"),
            ["status"] = r.Status.ToString(),
            ["clientName"] = r.ClientName,
            ["totalAmount"] = r.Total,
            ["totalVat"] = r.Vat
        }).ToList();
    }
}
