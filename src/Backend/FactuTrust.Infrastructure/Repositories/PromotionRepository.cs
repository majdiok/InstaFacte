using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>Repository des promotions datées.</summary>
public sealed class PromotionRepository : IPromotionRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public PromotionRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <summary>
    /// Filtre en base sur la fenêtre et l'activation — l'index composite est fait pour cela. Le
    /// ciblage (produit / catégorie / client) reste au domaine : ce sont des règles, pas du SQL.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetRunningAtAsync(
        DateTime date, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var day = date.Date;

        return await context.Promotions
            .AsNoTracking()
            .Where(p => p.IsActive && p.StartsOn <= day && p.EndsOn >= day)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Promotions
            .AsNoTracking()
            .OrderByDescending(p => p.StartsOn)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await GetAllOrderedAsync(cancellationToken);

    public async Task<Promotion> AddAsync(Promotion entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Promotions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Promotion entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Promotions.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Promotion entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Promotions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Promotions.AnyAsync(p => p.Id == id, cancellationToken);
    }
}
