using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository de la grille tarifaire (<see cref="PriceList"/>).
/// </summary>
public sealed class PriceListRepository : IPriceListRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public PriceListRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PriceList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PriceLists
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PriceList?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceList>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PriceLists
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAssignedClientsAsync(Guid priceListId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Clients
            .CountAsync(c => c.PriceListId == priceListId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetItemCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var counts = await context.PriceListItems
            .AsNoTracking()
            .GroupBy(i => i.PriceListId)
            .Select(g => new { PriceListId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.PriceListId, c => c.Count);
    }

    public async Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PriceLists
            .Include(p => p.Items)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceList> AddAsync(PriceList entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PriceLists.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PriceList entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PriceLists.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PriceList entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PriceLists.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PriceLists.AnyAsync(p => p.Id == id, cancellationToken);
    }
}
