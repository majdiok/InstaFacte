using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CashRegisterSessionRepository : ICashRegisterSessionRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CashRegisterSessionRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CashRegisterSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisterSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<CashRegisterSession?> GetByIdWithRegisterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisterSessions
            .Include(s => s.CashRegister)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<CashRegisterSession?> GetOpenByRegisterIdAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisterSessions
            .Include(s => s.CashRegister)
            .FirstOrDefaultAsync(
                s => s.CashRegisterId == cashRegisterId && s.Status == CashRegisterSessionStatus.Open,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegisterSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisterSessions
            .OrderByDescending(s => s.OpenedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CashRegisterSession> AddAsync(CashRegisterSession entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CashRegisterSessions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(CashRegisterSession entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
        if (entity.CashRegister is not null)
            context.Entry(entity.CashRegister).State = EntityState.Unchanged;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CashRegisterSession entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CashRegisterSessions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CashRegisterSessions.AnyAsync(s => s.Id == id, cancellationToken);
    }
}
