using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository for tenant tax catalog.
/// </summary>
public sealed class TaxRepository : ITaxRepository
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;

    public TaxRepository(IDbContextFactory<TenantDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Tax>> GetAllAsync(
        TaxType? type = null,
        TaxContext? context = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Taxes.AsQueryable();

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (context.HasValue)
            query = query.Where(t => t.Context == context.Value);

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tax?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taxes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tax>> GetActiveVatRatesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taxes
            .Where(t => t.Type == TaxType.VAT && t.IsActive && t.ValueType == TaxValueType.Percentage)
            .OrderBy(t => t.DisplayOrder)
            .ThenByDescending(t => t.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tax?> GetActiveSalesStampTaxAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taxes
            .Where(t =>
                t.Type == TaxType.Stamp &&
                t.IsActive &&
                t.ValueType == TaxValueType.FixedAmount &&
                (t.Context == TaxContext.All || t.Context == TaxContext.Sales))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Tax> AddAsync(Tax entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Taxes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Tax entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Taxes.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tax entity, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Taxes.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
