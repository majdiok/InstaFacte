using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class DepreciationRateCategoryRepository : IDepreciationRateCategoryRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public DepreciationRateCategoryRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<DepreciationRateCategory>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DepreciationRateCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<DepreciationRateCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DepreciationRateCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<DepreciationRateCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var c = code.Trim().ToUpperInvariant();
        return await context.DepreciationRateCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == c, cancellationToken);
    }
}