using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PosHeldTicketRepository : IPosHeldTicketRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PosHeldTicketRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PosHeldTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosHeldTickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PosHeldTicket>> ListByRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosHeldTickets
            .Where(t => t.CashRegisterId == cashRegisterId)
            .OrderByDescending(t => t.HeldAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByRegisterAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosHeldTickets.CountAsync(t => t.CashRegisterId == cashRegisterId, cancellationToken);
    }

    public async Task<IReadOnlyList<PosHeldTicket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosHeldTickets.OrderByDescending(t => t.HeldAt).ToListAsync(cancellationToken);
    }

    public async Task<PosHeldTicket> AddAsync(PosHeldTicket entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosHeldTickets.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PosHeldTicket entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosHeldTickets.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PosHeldTicket entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosHeldTickets.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosHeldTickets.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
