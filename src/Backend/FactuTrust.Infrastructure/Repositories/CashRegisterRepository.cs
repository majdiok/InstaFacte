using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CashRegisterRepository : ICashRegisterRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CashRegisterRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisters.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<CashRegister?> GetByWarehouseIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisters
            .Where(r => r.WarehouseId == warehouseId)
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CashRegister?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var normalized = code.Trim().ToUpperInvariant();
        return await context.CashRegisters.FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var normalized = code.Trim().ToUpperInvariant();
        var query = context.CashRegisters.Where(r => r.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisters.OrderBy(r => r.Code).ToListAsync(cancellationToken);
    }

    public async Task<CashRegister> AddAsync(CashRegister entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CashRegisters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(CashRegister entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CashRegisters.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CashRegister entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CashRegisters.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisters.AnyAsync(r => r.Id == id, cancellationToken);
    }
}
