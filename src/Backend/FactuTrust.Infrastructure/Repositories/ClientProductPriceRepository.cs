using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository du prix négocié par couple client / produit (<see cref="ClientProductPrice"/>).
/// </summary>
public sealed class ClientProductPriceRepository : IClientProductPriceRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public ClientProductPriceRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ClientProductPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<ClientProductPrice?> GetForClientProductAsync(
        Guid clientId, Guid productId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ClientId == clientId && p.ProductId == productId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ClientProductPrice>> GetByClientAsync(
        Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices
            .AsNoTracking()
            .Where(p => p.ClientId == clientId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientProductPrice>> GetByProductAsync(
        Guid productId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices
            .AsNoTracking()
            .Where(p => p.ProductId == productId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientProductPrice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientProductPrice> AddAsync(ClientProductPrice entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ClientProductPrices.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ClientProductPrice entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ClientProductPrices.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ClientProductPrice entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ClientProductPrices.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ClientProductPrices.AnyAsync(p => p.Id == id, cancellationToken);
    }
}
