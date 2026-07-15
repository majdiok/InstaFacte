using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class OpportunityRepository : IOpportunityRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public OpportunityRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Opportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Opportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Opportunity>> GetAllAsync(
        OpportunityStage? stage = null,
        Guid? assignedUserId = null,
        Guid? clientId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.Opportunities.AsNoTracking();

        if (stage.HasValue)
            query = query.Where(o => o.Stage == stage.Value);

        if (assignedUserId.HasValue)
            query = query.Where(o => o.AssignedUserId == assignedUserId.Value);

        if (clientId.HasValue)
            query = query.Where(o => o.ClientId == clientId.Value);

        return await query
            .OrderByDescending(o => o.ExpectedCloseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Opportunities.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Opportunities.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Opportunities.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
