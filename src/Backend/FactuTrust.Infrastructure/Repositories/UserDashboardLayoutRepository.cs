using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class UserDashboardLayoutRepository : IUserDashboardLayoutRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public UserDashboardLayoutRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<UserDashboardLayout?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.UserDashboardLayouts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(UserDashboardLayout layout, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.UserDashboardLayouts.Add(layout);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserDashboardLayout layout, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.UserDashboardLayouts.Update(layout);
        await context.SaveChangesAsync(cancellationToken);
    }
}
