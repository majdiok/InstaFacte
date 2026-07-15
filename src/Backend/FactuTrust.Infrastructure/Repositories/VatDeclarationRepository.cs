using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class VatDeclarationRepository : IVatDeclarationRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public VatDeclarationRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<VatDeclaration?> GetByYearMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.VatDeclarations.FirstOrDefaultAsync(v => v.Year == year && v.Month == month, cancellationToken);
    }

    public async Task<VatDeclaration> AddAsync(VatDeclaration entity, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.VatDeclarations.Add(entity);
        await ctx.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(VatDeclaration entity, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.VatDeclarations.Update(entity);
        await ctx.SaveChangesAsync(cancellationToken);
    }
}
