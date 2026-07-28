using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class StudioAiBuildPlanRepository : IStudioAiBuildPlanRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public StudioAiBuildPlanRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<StudioAiBuildPlan?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioAiBuildPlans
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, cancellationToken);
    }

    public async Task AddAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioAiBuildPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryUpdateAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioAiBuildPlans.Update(plan);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Une autre requête a déjà fait avancer le plan (double clic « Valider ») — le perdant renonce.
            return false;
        }
    }
}
