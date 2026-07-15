using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for company/seller entities.
/// </summary>
public sealed class CompanyRepository : ICompanyRepository
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;

    public CompanyRepository(IDbContextFactory<TenantDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Company?> GetByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Companies
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<Company>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Company?> GetDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Companies
            .FirstOrDefaultAsync(c => c.IsDefault && c.IsActive, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Companies
            .AnyAsync(c => c.Id == id && c.IsActive, cancellationToken);
    }

    public async Task<Company> AddAsync(
        Company entity, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Companies.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(
        Company entity, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Companies.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
