using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class SocialFundSchemeRepository : ISocialFundSchemeRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public SocialFundSchemeRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SocialFundScheme?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SocialFundSchemes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SocialFundScheme?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var normalized = code.Trim().ToUpperInvariant();
        return await context.SocialFundSchemes.FirstOrDefaultAsync(s => s.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<SocialFundScheme>> ListAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.SocialFundSchemes.AsNoTracking();
        if (activeOnly)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Code).ToListAsync(cancellationToken);
    }

    public async Task<SocialFundScheme> AddAsync(SocialFundScheme entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SocialFundSchemes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(SocialFundScheme entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SocialFundSchemes.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
