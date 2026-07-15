using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public class WithholdingTaxRepository : IWithholdingTaxRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public WithholdingTaxRepository(ITenantDbContextFactory contextFactory)
        => _contextFactory = contextFactory;

    public async Task<List<WithholdingTaxType>> GetAllTypesAsync(CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.WithholdingTaxTypes.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code).ToListAsync(ct);
    }

    public async Task<List<WithholdingTaxType>> GetActiveTypesAsync(CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.WithholdingTaxTypes.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code).ToListAsync(ct);
    }

    public async Task<WithholdingTaxType?> GetTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.WithholdingTaxTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<WithholdingTaxType?> GetTypeByCodeAsync(string code, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.WithholdingTaxTypes.FirstOrDefaultAsync(t => t.Code == code, ct);
    }

    public async Task AddTypeAsync(WithholdingTaxType type, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        await context.WithholdingTaxTypes.AddAsync(type, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateTypeAsync(WithholdingTaxType type, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.WithholdingTaxTypes.Update(type);
        await context.SaveChangesAsync(ct);
    }
}
