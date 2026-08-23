using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PosCartDraftRepository : IPosCartDraftRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PosCartDraftRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PosCartDraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosCartDrafts.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<PosCartDraft?> GetByUserAndRegisterAsync(
        Guid userId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosCartDrafts.FirstOrDefaultAsync(
            d => d.UserId == userId && d.CashRegisterId == cashRegisterId,
            cancellationToken);
    }

    public async Task<PosCartDraft?> GetLatestByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosCartDrafts
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteByUserAndRegisterAsync(
        Guid userId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var drafts = await context.PosCartDrafts
            .Where(d => d.UserId == userId && d.CashRegisterId == cashRegisterId)
            .ToListAsync(cancellationToken);
        if (drafts.Count == 0)
            return;
        context.PosCartDrafts.RemoveRange(drafts);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PosCartDraft>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosCartDrafts.ToListAsync(cancellationToken);
    }

    public async Task<PosCartDraft> AddAsync(PosCartDraft entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosCartDrafts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PosCartDraft entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosCartDrafts.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PosCartDraft entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PosCartDrafts.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PosCartDrafts.AnyAsync(d => d.Id == id, cancellationToken);
    }
}
